using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mythologicum;

public static class WorldRegionCatalogChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { if (!value) throw new Exception(message); checks++; }

    public static void Main()
    {
        var expected = new Dictionary<ExpeditionContinent, string[]> {
            { ExpeditionContinent.Americas, new[] { "north-america", "central-america", "caribbean", "south-america" } },
            { ExpeditionContinent.Europe, new[] { "british-isles", "northern-europe", "western-europe", "eastern-europe", "southern-europe" } },
            { ExpeditionContinent.Africa, new[] { "north-africa", "west-africa", "central-africa", "east-africa", "south-africa" } },
            { ExpeditionContinent.Asia, new[] { "west-asia", "central-asia", "south-asia", "east-asia", "southeast-asia", "north-asia" } },
            { ExpeditionContinent.Oceania, new[] { "australia-new-zealand", "pacific-islands" } },
            { ExpeditionContinent.Antarctica, new[] { "antarctica" } }
        };
        var regions = WorldRegionCatalog.CreateDefaults();
        Check(regions.Length == 23 && regions.Select(region => region.id).Distinct().Count() == 23,
            "The agreed map contains exactly 23 distinct regions.");
        Check(regions.Select(region => region.id).SequenceEqual(WorldRegionCatalog.RegionIds), "The public catalog uses the same stable region IDs.");
        Check(new ProgressionRules().regions.Select(region => region.id).SequenceEqual(WorldRegionCatalog.RegionIds),
            "Resource-free progression defaults use the canonical catalog.");
        foreach (var group in expected)
        {
            Check(regions.Where(region => region.continent == group.Key).Select(region => region.id).SequenceEqual(group.Value),
                "The supplied map's regional division remains fixed: " + group.Key);
            Check(Loc.Table.Contains(WorldRegionCatalog.ContinentName(group.Key)), "Each map group has its localized heading: " + group.Key);
        }
        foreach (var region in regions)
        {
            Check(region.name == "expedition.region." + region.id.Replace('-', '_') && Loc.Table.Contains(region.name)
                && !string.IsNullOrWhiteSpace(Loc.Table.Raw(region.name, GameLanguage.ENGLISH)),
                "Region labels have distinct IDs and English source text: " + region.id);
            Check(region.mapPosition.x >= 0 && region.mapPosition.x <= 1 && region.mapPosition.y >= 0 && region.mapPosition.y <= 1,
                "The marker is on the map: " + region.id);
            Check(region.species.Distinct().Count() == region.species.Length && region.species.All(id => File.Exists("Assets/Mythologicum/Resources/Creatures/" + id + ".asset")),
                "All regional specimens use existing, unduplicated resource IDs: " + region.id);
            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
                Check(!string.IsNullOrWhiteSpace(Loc.Table.Get(region.name, language)), "The region resolves with localized text or English fallback: " + region.id + "/" + language);
        }
        var expectedSpecies = new[] { "Chupacabra", "Hitotsume", "Yurei", "Oni", "Kappa", "Nokk", "Wolpertinger", "Unicorn", "Waldschrat", "Tatzelwurm",
            "AllghoiKhorkhoi", "MokeleMbembe", "NinkiNanka", "Abada", "NandiBear", "Dingonek", "Marozi", "Grootslang", "Inkanyamba", "Aigamuxa", "Manticore",
            "Wendigo", "Thunderbird", "Kalupelik" };
        var actualSpecies = new HashSet<string>(regions.SelectMany(region => region.species));
        Check(actualSpecies.SetEquals(expectedSpecies) && !actualSpecies.Contains("Goat"), "The new grouping retains every mythological species and excludes domestic goats.");
        Check(regions.Count(region => region.species.Length == 0) == 11, "Empty future regions stay represented without invented species.");
        Check(regions.Where(region => region.continent == ExpeditionContinent.Americas).All(region => region.species.Contains("Chupacabra")),
            "New American areas include Chupacabra while preserving its old North/South acquisition paths.");
        var copy = WorldRegionCatalog.CreateDefaults();
        copy[0].id = "changed"; copy[0].species[0] = "changed";
        copy[1].accessCost = 0;
        var unchanged = WorldRegionCatalog.CreateDefaults();
        Check(unchanged[0].id == "north-america" && unchanged[0].species[0] == "Chupacabra" && unchanged[1].accessCost > 0,
            "Designer rule instances never modify the shared catalog templates.");
        Console.WriteLine("PASS: " + checks + " fixed world-region grouping, roster, localization, marker and isolation checks.");
    }
}

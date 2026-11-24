using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mythologicum
{
    public enum ExpeditionContinent
    {
        Americas = 0, Europe = 1, Africa = 2, Asia = 3, Oceania = 4, Antarctica = 5
    }

    /// <summary>
    /// The game's agreed geographic regions. New species belong to these areas;
    /// country names and cultural origins remain separate creature data.
    /// </summary>
    public static class WorldRegionCatalog
    {
        private static readonly ExpeditionRegion[] templates = {
            Region("north-america", ExpeditionContinent.Americas, .18f, .73f, "Chupacabra", "Wendigo", "Thunderbird", "Kalupelik"),
            Region("central-america", ExpeditionContinent.Americas, .25f, .51f, "Chupacabra"),
            Region("caribbean", ExpeditionContinent.Americas, .33f, .53f, "Chupacabra"),
            Region("south-america", ExpeditionContinent.Americas, .31f, .26f, "Chupacabra"),
            Region("british-isles", ExpeditionContinent.Europe, .45f, .76f),
            Region("northern-europe", ExpeditionContinent.Europe, .53f, .86f, "Nokk"),
            Region("western-europe", ExpeditionContinent.Europe, .49f, .70f, "Wolpertinger", "Unicorn", "Waldschrat", "Tatzelwurm"),
            Region("eastern-europe", ExpeditionContinent.Europe, .57f, .72f),
            Region("southern-europe", ExpeditionContinent.Europe, .52f, .63f),
            Region("north-africa", ExpeditionContinent.Africa, .51f, .53f),
            Region("west-africa", ExpeditionContinent.Africa, .46f, .43f, "NinkiNanka"),
            Region("central-africa", ExpeditionContinent.Africa, .54f, .36f, "MokeleMbembe", "Abada"),
            Region("east-africa", ExpeditionContinent.Africa, .61f, .36f, "NandiBear", "Dingonek", "Marozi"),
            Region("south-africa", ExpeditionContinent.Africa, .55f, .18f, "Grootslang", "Inkanyamba", "Aigamuxa"),
            Region("west-asia", ExpeditionContinent.Asia, .63f, .57f, "Manticore"),
            Region("central-asia", ExpeditionContinent.Asia, .70f, .72f),
            Region("south-asia", ExpeditionContinent.Asia, .72f, .47f),
            Region("east-asia", ExpeditionContinent.Asia, .84f, .65f, "Hitotsume", "Yurei", "Oni", "Kappa", "AllghoiKhorkhoi"),
            Region("southeast-asia", ExpeditionContinent.Asia, .82f, .38f),
            Region("north-asia", ExpeditionContinent.Asia, .78f, .86f),
            Region("australia-new-zealand", ExpeditionContinent.Oceania, .87f, .18f),
            Region("pacific-islands", ExpeditionContinent.Oceania, .96f, .36f),
            Region("antarctica", ExpeditionContinent.Antarctica, .51f, .035f)
        };
        private static readonly IReadOnlyList<string> ids = MakeIds();
        public static IReadOnlyList<string> RegionIds => ids;

        private static ExpeditionRegion Region(string id, ExpeditionContinent continent, float x, float y, params string[] species)
            => new ExpeditionRegion {
                id = id, name = "expedition.region." + id.Replace('-', '_'), continent = continent,
                mapPosition = new Vector2(x, y), species = species
            };

        private static IReadOnlyList<string> MakeIds()
        {
            var result = new string[templates.Length];
            for (int i = 0; i < templates.Length; i++) result[i] = templates[i].id;
            return Array.AsReadOnly(result);
        }

        public static ExpeditionRegion[] CreateDefaults()
        {
            var result = new ExpeditionRegion[templates.Length];
            for (int i = 0; i < result.Length; i++)
            {
                var source = templates[i];
                result[i] = new ExpeditionRegion {
                    id = source.id, name = source.name, continent = source.continent,
                    accessCost = source.accessCost, mapPosition = source.mapPosition,
                    species = (string[])source.species.Clone()
                };
            }
            return result;
        }

        public static string ContinentName(ExpeditionContinent continent)
        {
            switch (continent)
            {
                case ExpeditionContinent.Europe: return "expedition.continent.europe";
                case ExpeditionContinent.Africa: return "expedition.continent.africa";
                case ExpeditionContinent.Asia: return "expedition.continent.asia";
                case ExpeditionContinent.Oceania: return "expedition.continent.oceania";
                case ExpeditionContinent.Antarctica: return "expedition.continent.antarctica";
                default: return "expedition.continent.americas";
            }
        }

    }
}

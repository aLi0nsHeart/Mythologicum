using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class AttractionDataChecks
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    private static void Close(float actual, float expected, string reason) => Check(Math.Abs(actual - expected) < .00001f, reason);
    public static void Main()
    {
        Check((int)BuildingKind.ParkEntrance == 0 && (int)BuildingKind.ZookeeperHut == 7, "Existing building enum values remain stable for saves");
        Check((int)BuildingKind.PettingZoo == 8 && (int)BuildingKind.ClockworkCarousel == 15 && (int)BuildingKind.ViewingWindow == 16,
            "Attractions append stable building values without reordering older saves");
        var defaults = BuildingDefinition.Defaults();
        Check(defaults.Length == 17 && defaults.Select(item => item.kind).Distinct().Count() == 17, "The fallback catalogue has every distinct building kind");
        var attractions = defaults.Where(item => item.IsAttraction).ToArray();
        Check(attractions.Length == 9, "All nine attractions include the new viewing-window building");
        foreach (var building in attractions)
        {
            Check(building.CanSetPrice && building.RequiresPathConnection && !building.IsEmployeeBuilding,
                "Attraction accounts are editable, connected and separate from employee hiring: " + building.kind);
            Check(building.constructionCost > 0 && building.operatingCostPerSecond > 0 && building.attractionPrice > 0
                && building.serviceCapacity > 0 && building.serviceSeconds > 0, "Attraction economy and capacity have usable editable defaults");
            var copy = building.Copy();
            Check(!ReferenceEquals(copy.attraction, building.attraction) && !ReferenceEquals(copy.attraction.appeal, building.attraction.appeal),
                "Runtime attraction copies own their settings and appeal");
            float original = building.attraction.appeal.children;
            copy.attraction.appeal.children = .123f; copy.attraction.satisfactionReward = 91;
            Close(building.attraction.appeal.children, original, "Editing runtime appeal never changes authored defaults");
            Check(building.attraction.satisfactionReward != 91, "Editing runtime rewards never changes authored defaults");
            var grid = new SquareBuildGrid(Vector3.zero, 2, 20, 20);
            var record = new ParkBuildingRecord(new BuildingFootprint(building, Vector2Int.zero, 0));
            Close(record.Price, building.attractionPrice, "New attraction records use the configured attraction price");
            Check(record.SetPrice(17.25f) && !record.SetPrice(float.NaN), "Attraction pricing accepts valid prices and rejects invalid values");
            record.RecordRevenue(17.25f); record.RecordExpense(3.25f);
            Close(record.Profit, 14f, "Attraction revenue and expenses use existing building accounts");
            record.RestoreAccounts(8, 21, 9, 0); Close(record.Price, 8, "Saved attraction pricing restores independently of its starting price");
            Close(record.Profit, 12, "Saved attraction revenue and upkeep restore");
        }
        var petting = defaults.Single(item => item.kind == BuildingKind.PettingZoo);
        Check(petting.width == 1 && petting.depth == 1 && petting.attraction.requiresPettableCreatures,
            "Petting entrances are one-tile safe-contact portals");
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var footprint = new BuildingFootprint(petting, new Vector2Int(8, 8), rotation);
            Check(footprint.InsideAccessCell == footprint.RearAccessCell && !footprint.Contains(footprint.RearAccessCell),
                "Rotated petting portals expose exterior enclosure access");
            Check(ConstructionRouteGraph.CellDistance(footprint.RearDoorCell, footprint.RearAccessCell) == 1,
                "Rear access remains cardinal and adjacent for every rotation");
            Check(footprint.RearEdge.CellA == footprint.RearAccessCell || footprint.RearEdge.CellB == footprint.RearAccessCell,
                "The rear barrier edge follows the rotated portal");
        }
        MountedPettingChecks(petting);
        var viewing = defaults.Single(item => item.kind == BuildingKind.ViewingWindow);
        Check(viewing.width == 2 && viewing.depth == 1 && viewing.attraction.kind == AttractionKind.ViewingWindow
            && viewing.attraction.requiresCreatures, "Viewing bays are two tiles along the fence and one tile into the enclosure");
        MountedViewingChecks(viewing);
        var gallery = defaults.Single(item => item.kind == BuildingKind.PredatorFeedingGallery);
        var theatre = defaults.Single(item => item.kind == BuildingKind.TricksterTheatre);
        Check(gallery.attraction.requiresCreatures && gallery.attraction.requiresDangerousCreatures,
            "Feeding galleries require real dangerous exhibits");
        Check(theatre.attraction.requiresCreatures && theatre.attraction.requiresFearFeeder,
            "Trickster performances require an appropriate creature");
        Check(petting.attraction.AppealFor(VisitorType.Children) > petting.attraction.AppealFor(VisitorType.ThrillSeekers), "Petting appeals especially to children");
        Check(gallery.attraction.AppealFor(VisitorType.ThrillSeekers) > gallery.attraction.AppealFor(VisitorType.Children), "Predator galleries appeal especially to thrill seekers");
        Check(defaults.Single(item => item.kind == BuildingKind.ExpeditionPavilion).attraction.AppealFor(VisitorType.Adventurers) == 1,
            "Expedition exhibitions support adventurous visitors");
        Check(defaults.Single(item => item.kind == BuildingKind.CabinetOfCuriosities).attraction.AppealFor(VisitorType.Wonderlust) == 1,
            "Creature lore supports Wonderlust visitors");
        var invalid = new AttractionDefinition { satisfactionReward = float.NaN, exhibitRadius = float.PositiveInfinity,
            minimumInterest = 2, scareFoodRelief = 1000, maximumCreatureThreat = -3, appeal = null };
        invalid.Validate();
        Check(invalid.satisfactionReward == 0 && invalid.exhibitRadius == 0 && invalid.minimumInterest == 1
            && invalid.scareFoodRelief == 100 && invalid.maximumCreatureThreat == 0 && invalid.appeal != null,
            "Attraction tuning sanitizes invalid numeric and missing appeal data");

        var rules = new ProgressionRules();
        var expectedIds = new[] { "attractions", "petting-zoo", "viewing-tower", "viewing-window", "balloon-ride",
            "predator-feeding-gallery", "trickster-theatre", "cabinet-of-curiosities", "expedition-pavilion", "clockwork-carousel" };
        Check(expectedIds.All(id => rules.technologies.Any(item => item.id == id)), "All approved attraction research projects exist");
        Check(rules.technologies.Select(item => item.id).Distinct().Count() == rules.technologies.Length, "Technology IDs remain unique");
        foreach (var technology in rules.technologies.Where(item => expectedIds.Contains(item.id)))
        {
            Check(technology.cost > 0 && technology.workSeconds > 0 && technology.prerequisites.All(id => rules.technologies.Any(item => item.id == id)),
                "Attraction projects have editable prices, positive work and valid prerequisites");
            if (technology.id != "attractions") Check(technology.prerequisites.Contains("attractions"), "Every attraction branch requires the foundation");
        }
        var progressionGrid = new SquareBuildGrid(Vector3.zero, 2, 16, 16);
        var fences = new FenceLayout(progressionGrid); var buildings = new ParkBuildingNetwork(progressionGrid, fences);
        var park = new ParkProgression(rules, new ParkEconomy(10000), buildings, new Dictionary<string, int>());
        foreach (var attraction in attractions) Check(!park.BuildingUnlocked(attraction.kind), "New parks must research each attraction: " + attraction.kind);
        Check(!park.Completed("viewing-window") && !rules.startingTechnologies.Intersect(expectedIds).Any(), "Viewing windows and attraction projects never begin researched");
        Check(rules.technologies.Single(item => item.id == "balloon-ride").prerequisites.Contains("viewing-tower"), "Aerial attractions build upon elevated viewing research");
        Check(rules.technologies.Single(item => item.id == "viewing-window").buildings.Contains(BuildingKind.ViewingWindow),
            "Existing viewing-window research unlocks the enterable building");
        Console.WriteLine("PASS: " + checks + " attraction defaults, pricing, copies, portal geometry and research gating checks.");
    }

    private static void MountedPettingChecks(BuildingDefinition petting)
    {
        var inward = new[] { Vector2Int.up, Vector2Int.left, Vector2Int.down, Vector2Int.right };
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var grid = new SquareBuildGrid(new Vector3(3, 0, -7), 2, 20, 20);
            var fences = new FenceLayout(grid); var network = new ParkBuildingNetwork(grid, fences);
            network.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, Vector2Int.one, 0));
            var anchor = new Vector2Int(8, 8);
            var mounted = new BuildingFootprint(petting, anchor, rotation, true);
            var legacy = new BuildingFootprint(petting, anchor, rotation);
            Check(mounted.IsFenceMounted && mounted.AccessCell == anchor && mounted.RearAccessCell == anchor + inward[rotation],
                "Fence-mounted entrances use the two cells immediately beside the selected edge in every rotation");
            Check(!mounted.Cells().Any() && !mounted.Contains(anchor) && !mounted.Contains(mounted.RearAccessCell),
                "A mounted gateway reserves an edge rather than a construction tile");
            Check(!legacy.IsFenceMounted && legacy.Cells().Count() == 1 && legacy.Contains(anchor)
                && ConstructionRouteGraph.CellDistance(legacy.AccessCell, legacy.RearAccessCell) == 2,
                "Existing unmounted petting footprints retain their original occupied tile and two-step access");
            Check((mounted.RearEdge.CellA == mounted.AccessCell && mounted.RearEdge.CellB == mounted.RearAccessCell)
                || (mounted.RearEdge.CellB == mounted.AccessCell && mounted.RearEdge.CellA == mounted.RearAccessCell),
                "The mounted edge separates public access from enclosure access");
            var center = mounted.Center(grid); var expected = fences.Center(mounted.RearEdge);
            Close(center.x, expected.x, "The mounted entrance sits on the fence centre instead of the exterior tile centre");
            Close(center.z, expected.z, "Fence-centred geometry respects every rotation and the grid origin");
            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Depth; y++)
            {
                var cell = new Vector2Int(x, y);
                if (cell != mounted.RearAccessCell) grid.TryOccupy(cell, BuildCellContent.Path);
            }
            Check(!network.CanPlace(mounted), "A newly mounted entrance requires an existing fence");
            Check(fences.TryAdd(mounted.RearEdge, FenceKind.Heavy), "Every orientation can attach to a reinforced boundary");
            Check(fences.TrySetGate(mounted.RearEdge) && !network.CanPlace(mounted), "A staff gate cannot also host a petting entrance");
            fences.TryRemove(mounted.RearEdge); fences.TryAdd(mounted.RearEdge, FenceKind.Heavy);
            Check(fences.TrySetWindow(mounted.RearEdge, 5) && !network.CanPlace(mounted), "A viewing window cannot also host a petting entrance");
            fences.TryRemove(mounted.RearEdge); fences.TryAdd(mounted.RearEdge, FenceKind.Heavy);
            var record = network.TryPlace(mounted);
            Check(record != null && record.IsOpen, "An existing connected path can directly serve the mounted entrance");
            Check(grid.ContentAt(anchor) == BuildCellContent.Path && grid.IsAvailable(mounted.RearAccessCell),
                "Mounting preserves the public path and clear enclosure ground");
            Check(!network.CanPlace(mounted) && !network.CanPlace(new BuildingFootprint(petting, mounted.RearAccessCell, rotation + 2, true)),
                "The same fence segment cannot receive duplicate entrances from either side");
            Check(!network.CanPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.Kiosk, width = 1, depth = 1 }, mounted.RearAccessCell, 0)),
                "Later building placement cannot cover a gateway's enclosure access");
            Check(network.Remove(record) && grid.ContentAt(anchor) == BuildCellContent.Path && fences.HasFence(mounted.RearEdge),
                "Removing a mounted entrance keeps the original path and fence intact");
            fences.TryRemove(mounted.RearEdge); network.RestoringSave = true;
            Check(network.TryPlace(mounted) != null, "An already saved gateway can restore after its fence was broken");
        }
    }

    private static void MountedViewingChecks(BuildingDefinition definition)
    {
        var inward = new[] { Vector2Int.up, Vector2Int.left, Vector2Int.down, Vector2Int.right };
        var tangent = new[] { Vector2Int.right, Vector2Int.up, Vector2Int.left, Vector2Int.down };
        for (int rotation = 0; rotation < 4; rotation++)
        {
            var grid = new SquareBuildGrid(new Vector3(3, 0, -7), 2, 20, 20);
            var fences = new FenceLayout(grid); var network = new ParkBuildingNetwork(grid, fences);
            network.TryPlace(new BuildingFootprint(new BuildingDefinition
                { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, Vector2Int.one, 0));
            var anchor = new Vector2Int(8, 8);
            var bay = new BuildingFootprint(definition, anchor, rotation, true);
            var expected = new[] { anchor + inward[rotation], anchor + inward[rotation] + tangent[rotation] };
            Check(bay.IsFenceMounted && bay.RequiresFoundation && bay.AccessCell == anchor
                && bay.RearAccessCell == expected[0], "A mounted bay has adjacent outside and protected indoor access");
            Check(bay.Cells().Count() == 2 && bay.Cells().All(expected.Contains)
                && expected.All(bay.Contains) && !bay.Contains(anchor),
                "Every rotated bay reserves exactly the two interior tiles and leaves its entrance path outside");
            var edges = bay.AttachmentEdges().ToArray();
            Check(edges.Length == 2 && edges[0].Equals(bay.RearEdge) && !edges[0].Equals(edges[1]),
                "The two-tile bay attaches to two distinct adjacent fence segments");
            var center = (grid.CellCenter(expected[0]) + grid.CellCenter(expected[1])) * .5f;
            Close(bay.Center(grid).x, center.x, "The viewing bay centre lies over its reserved interior footprint");
            Close(bay.Center(grid).z, center.z, "Rotating a viewing bay retains its two-by-one centre");
            for (int x = 0; x < grid.Width; x++)
            for (int y = 0; y < grid.Depth; y++)
            {
                var cell = new Vector2Int(x, y);
                if (!expected.Contains(cell)) grid.TryOccupy(cell, BuildCellContent.Path);
            }
            Check(!network.CanPlace(bay), "A new bay cannot attach to missing fence segments");
            Check(fences.TryAdd(edges[0], FenceKind.Heavy) && !network.CanPlace(bay),
                "A single fence segment is not enough for the two-tile bay");
            Check(fences.TryAdd(edges[1], FenceKind.Heavy) && network.CanPlace(bay),
                "Two clear adjacent segments support the bay in every rotation");
            Check(fences.TrySetGate(edges[1]) && !network.CanPlace(bay), "A staff gate on either attachment edge prevents placement");
            fences.TryRemove(edges[1]); fences.TryAdd(edges[1], FenceKind.Heavy);
            Check(fences.TrySetWindow(edges[1], 5) && !network.CanPlace(bay), "A legacy window on either attachment edge prevents placement");
            fences.TryRemove(edges[1]); fences.TryAdd(edges[1], FenceKind.Heavy);
            var cross = expected[0].x == expected[1].x
                ? new FenceEdge(new Vector2Int(expected[0].x, Math.Max(expected[0].y, expected[1].y)), true)
                : new FenceEdge(new Vector2Int(Math.Max(expected[0].x, expected[1].x), expected[0].y), false);
            Check(fences.TryAdd(cross) && !network.CanPlace(bay), "A cross-fence cannot cut through the reserved viewing room");
            fences.TryRemove(cross);
            var record = network.TryPlace(bay);
            Check(record != null && record.IsOpen && grid.ContentAt(anchor) == BuildCellContent.Path,
                "A mounted bay opens from the existing connected public path");
            Check(expected.All(cell => grid.ContentAt(cell) == BuildCellContent.Building),
                "A protected viewing room occupies both interior tiles");
            var animals = new CreatureNavigation(grid, fences);
            Check(expected.All(cell => !animals.IsWalkable(cell)), "Ordinary animals cannot enter the protected visitor room");
            var petting = AttractionDefinition.Building(BuildingKind.PettingZoo);
            Check(!network.CanPlace(bay) && !network.CanPlace(new BuildingFootprint(petting, anchor, rotation, true))
                && !network.CanPlace(new BuildingFootprint(petting, anchor + tangent[rotation], rotation, true)),
                "Both attachment edges are reserved against duplicate viewing bays and petting entrances");
            Check(network.Remove(record) && expected.All(grid.IsAvailable) && grid.ContentAt(anchor) == BuildCellContent.Path
                && edges.All(fences.HasFence), "Demolition releases the room but preserves the original fence segments and path");
            foreach (var edge in edges) fences.TryRemove(edge);
            network.RestoringSave = true;
            Check(network.TryPlace(bay) != null, "A saved viewing bay can restore after its attachment fences were broken");
        }
    }
}

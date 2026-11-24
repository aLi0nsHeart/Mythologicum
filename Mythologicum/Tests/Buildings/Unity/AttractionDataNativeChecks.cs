using System;
using System.Linq;
using Mythologicum;
using UnityEngine;

/// <summary>Authored attraction resource values and clone isolation; call from a disposable native fixture.</summary>
public static class AttractionDataNativeChecks
{
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception("Attraction data: " + reason); checks++; }
        var source = Resources.Load<BuildingCatalogDefinition>("Buildings/DefaultBuildings");
        Check(source != null && source.buildings.Length == 16, "the authored catalogue contains all old buildings and eight attraction buildings");
        var runtime = source.CreateRuntimeCatalog();
        Check(runtime.Length == 17 && runtime.Select(item => item.kind).Distinct().Count() == 17, "the runtime catalogue retains every unique kind and adds the viewing room");
        var windowSettings = Resources.Load<ViewingWindowDefinition>("Attractions/ViewingWindow");
        Check(windowSettings != null, "the viewing-room building uses the existing editable viewing-window asset");
        foreach (var item in runtime.Where(item => item.IsAttraction))
        {
            var authored = item.kind == BuildingKind.ViewingWindow ? windowSettings.CreateBuildingDefinition()
                : source.buildings.Single(entry => entry.kind == item.kind);
            var fallback = item.kind == BuildingKind.ViewingWindow ? windowSettings.CreateBuildingDefinition()
                : BuildingDefinition.Defaults().Single(entry => entry.kind == item.kind);
            Check(item.width == fallback.width && item.depth == fallback.depth && Mathf.Approximately(item.attractionPrice, fallback.attractionPrice),
                "serialized attraction footprint and ticket match their shipped defaults: " + item.kind);
            Check(item.attraction.kind == fallback.attraction.kind && item.serviceCapacity == fallback.serviceCapacity
                && Mathf.Approximately(item.serviceSeconds, fallback.serviceSeconds), "serialized mechanisms and service capacity load correctly: " + item.kind);
            Check(item.displayName == fallback.displayName && !string.IsNullOrWhiteSpace(Loc.Resolve(item.displayName)), "attraction names use a resolvable localization ID");
            Check(!ReferenceEquals(item.attraction, authored.attraction) && !ReferenceEquals(item.attraction.appeal, authored.attraction.appeal),
                "loaded runtime settings and appeal are independent of the authored asset");
            float childInterest = authored.attraction.appeal.children;
            item.attraction.appeal.children = .123f;
            Check(Mathf.Approximately(authored.attraction.appeal.children, childInterest), "runtime balance edits leave the source asset unchanged");
        }
        var room = windowSettings.CreateBuildingDefinition();
        Check(room.kind == BuildingKind.ViewingWindow && room.width == 2 && room.depth == 1
            && Mathf.Approximately(room.height, windowSettings.buildingHeight), "the room uses a two-by-one footprint and editable building height");
        Check(Mathf.Approximately(room.constructionCost, windowSettings.constructionCost)
            && Mathf.Approximately(room.attractionPrice, windowSettings.ticketPrice)
            && Mathf.Approximately(room.operatingCostPerSecond, windowSettings.operatingCostPerSecond)
            && Mathf.Approximately(room.serviceSeconds, windowSettings.serviceSeconds) && room.serviceCapacity == windowSettings.serviceCapacity,
            "new building costs, pricing, duration and capacity retain the existing window tuning source");
        float sourceAppeal = windowSettings.attraction.appeal.thrill;
        room.attraction.appeal.thrill = .123f;
        Check(Mathf.Approximately(windowSettings.attraction.appeal.thrill, sourceAppeal), "building factory copies do not mutate viewing-window asset appeal");
        var progression = Resources.Load<ProgressionDefinition>("Progression/DefaultProgression");
        Check(progression != null && progression.rules.technologies.Length == 18, "the authored research catalogue retains old projects and adds all attraction projects");
        Check(progression.rules.technologies.Select(item => item.id).Distinct().Count() == 18, "authored technology IDs are unique");
        foreach (var item in source.buildings.Where(item => item.IsAttraction))
        {
            var technology = progression.rules.technologies.Single(project => project.buildings.Contains(item.kind));
            Check(technology.prerequisites.Contains("attractions"), "the serialized attraction unlock has its foundation prerequisite: " + item.kind);
            Check(technology.cost > 0 && technology.workSeconds > 0, "attraction research requires payment and researcher work");
        }
        var window = progression.rules.technologies.Single(item => item.id == "viewing-window");
        Check(window.buildings.Contains(BuildingKind.ViewingWindow) && window.prerequisites.Contains("attractions"),
            "existing viewing-window research unlocks the enterable building with the same foundation prerequisite");
        Check(!(progression.rules.startingTechnologies ?? Array.Empty<string>()).Contains("attractions"), "new parks do not start with attraction research completed");
        foreach (var id in new[] { "Goat", "Wolpertinger", "Unicorn", "Chupacabra", "Hitotsume", "Yurei", "Oni", "Waldschrat" })
        {
            var species = Resources.Load<CreatureDefinition>("Creatures/" + id);
            bool expected = id == "Goat" || id == "Wolpertinger" || id == "Unicorn";
            Check(species != null && species.pettable == expected, "authored contact suitability loads correctly: " + id);
        }
        return checks;
    }
}

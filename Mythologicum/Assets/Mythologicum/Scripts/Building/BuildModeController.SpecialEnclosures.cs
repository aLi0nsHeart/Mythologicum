using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        private SpecialEnclosureMap specialEnclosureMap;
        private SpecialEnclosureConstruction specialEnclosures;
        private bool refreshingSpecialEnclosures;

        private void CreateSpecialEnclosures()
        {
            var definition = SpecialEnclosureDefinition.Current;
            specialEnclosureMap = new SpecialEnclosureMap(grid, fences.Layout, definition.rules);
            var root = new GameObject("Completed aviaries and aquariums");
            root.transform.SetParent(buildings.transform, false);
            specialEnclosures = new SpecialEnclosureConstruction(grid, specialEnclosureMap, root.transform,
                buildingMaterial, definition, SpecialPoolGroundEditable);
            creatureWorld.ConfigureFlightContainment(specialEnclosureMap.CanFlyAcross, specialEnclosureMap.FlightCeiling,
                () => { specialEnclosureMap.Refresh(); return specialEnclosureMap.Revision; });
            RefreshSpecialEnclosures();
        }

        private bool SpecialPoolGroundEditable(Vector2Int cell)
        {
            if (!grid.Contains(cell) || grid.ContentAt(cell) != BuildCellContent.Empty || ReservedBuildingAccess(cell)
                || trees.Occupies(cell) || graves.Occupies(cell) || scarecrows.Occupies(cell)
                || rocks.Occupies(cell) || HabitatFeatureAt(cell)) return false;
            foreach (var actor in creatureWorld.Agents)
                if (actor.Alive && !actor.Definition.Aquatic.canSwim && !(actor.Definition.Flight.canFly && !actor.IsSedated)
                    && (actor.Cell == cell || actor.Route.Contains(cell))) return false;
            foreach (var guest in visitorWorld.Visitors)
                if (guest.Actor.Alive && (guest.Actor.Cell == cell || guest.Actor.Route.Contains(cell))) return false;
            foreach (var worker in staffWorld.Staff)
                if (worker.Actor.Alive && (worker.Actor.Cell == cell || worker.Actor.Route.Contains(cell))) return false;
            return true;
        }

        private void RefreshSpecialEnclosures()
        {
            if (specialEnclosures == null || refreshingSpecialEnclosures) return;
            refreshingSpecialEnclosures = true;
            try { specialEnclosures.Refresh(); }
            finally { refreshingSpecialEnclosures = false; }
        }

        // A delayed fill can follow an animal leaving an initially blocked pool.
        // Do not let an older fence-only journal entry silently strand that earthwork.
        private bool SpecialPoolChangedAfterAction(ConstructionUndo action)
        {
            if (specialEnclosures == null || action.fences.Count == 0) return false;
            var saved = action.after.specialEnclosures;
            var known = new HashSet<Vector2Int>(saved?.completedPoolCells ?? new List<Vector2Int>());
            var current = specialEnclosures.Capture();
            var added = new HashSet<Vector2Int>(current.completedPoolCells.Where(cell => !known.Contains(cell)));
            if (added.Count == 0) return false;
            foreach (var region in specialEnclosureMap.Records)
                if (region.Kind == FenceKind.Aquarium && region.Cells.Any(added.Contains)
                    && region.Boundary.Any(action.fences.Contains)) return true;
            return false;
        }

        private void RestoreSpecialConstructionUndo(ConstructionUndo action)
        {
            if (specialEnclosures == null) return;
            var before = action.before.specialEnclosures ?? new SavedSpecialEnclosures();
            var after = action.after.specialEnclosures ?? new SavedSpecialEnclosures();
            var current = specialEnclosures.Capture();
            var originalCells = new HashSet<Vector2Int>(before.completedPoolCells);
            var removedCells = new HashSet<Vector2Int>(after.completedPoolCells.Where(cell => !originalCells.Contains(cell)));
            var originalNodes = new HashSet<Vector2Int>(before.terrain.Select(item => item.node));
            var removedNodes = new HashSet<Vector2Int>(after.terrain.Where(item => !originalNodes.Contains(item.node)).Select(item => item.node));
            var originalWater = new HashSet<Vector2Int>(before.water.Select(item => item.cell));
            var removedWater = new HashSet<Vector2Int>(after.water.Where(item => !originalWater.Contains(item.cell)).Select(item => item.cell));
            current.completedPoolCells.RemoveAll(removedCells.Contains);
            current.terrain.RemoveAll(item => removedNodes.Contains(item.node));
            current.water.RemoveAll(item => removedWater.Contains(item.cell));
            specialEnclosures.Restore(current);
            RefreshSpecialEnclosures();
        }
    }
}

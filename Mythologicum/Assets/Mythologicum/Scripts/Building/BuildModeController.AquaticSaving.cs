using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        private static void CaptureAquaticLinks(Dictionary<CreatureAgent,SavedActor> actors, List<CreatureAgent> creatures)
        {
            foreach (var entry in actors)
            {
                var hunter = entry.Key.CapturedBy;
                if (hunter == null || !hunter.Alive || !hunter.Definition.Aquatic.dragPreyToWater) continue;
                int index = creatures.IndexOf(hunter);
                if (index < 0 || !actors.TryGetValue(hunter,out var savedHunter) || !savedHunter.hasAquaticHunt || savedHunter.aquaticHunt == null)
                    throw new InvalidDataException(Loc.Text("aquatic.save.invalid_capture"));
                entry.Value.capturedByAquatic = true; entry.Value.captureHunter = index;
            }
        }

        private void ValidateAquaticLinks(ParkSaveData data, SquareBuildGrid checkGrid, FenceLayout checkFences)
        {
            var claims = new Dictionary<int,SavedActor>();
            var all = new List<SavedActor>(data.creatures);
            all.AddRange(data.staff); all.AddRange(data.visitors);
            foreach (var actor in all)
            {
                if (!actor.capturedByAquatic) continue;
                if (actor.captureHunter < 0 || actor.captureHunter >= data.creatures.Count || actor.hasAquaticHunt
                    || actor.sedation > 0 || actor.shelterSleeping || actor.shelterRetreat || claims.ContainsKey(actor.captureHunter))
                    throw new InvalidDataException(Loc.Text("aquatic.save.invalid_capture"));
                var hunter = data.creatures[actor.captureHunter];
                if (ReferenceEquals(hunter,actor) || hunter.capturedByAquatic || !hunter.hasAquaticHunt || hunter.aquaticHunt == null
                    || ConstructionRouteGraph.CellDistance(hunter.cell,actor.cell) > 1
                    || actor.attraction?.insideInterior == true || actor.attraction?.insideViewingRoom == true)
                    throw new InvalidDataException(Loc.Text("aquatic.save.invalid_capture"));
                claims.Add(actor.captureHunter,actor);
            }
            for (int index = 0; index < data.creatures.Count; index++)
            {
                var actor = data.creatures[index]; var hunt = actor.aquaticHunt;
                if (!actor.hasAquaticHunt) continue;
                if (hunt == null) throw new InvalidDataException(Loc.Text("aquatic.save.invalid_capture"));
                var species = Species(actor.species);
                bool feeding = hunt.phase == CreatureActivity.Feeding;
                if (species == null || !species.Aquatic.canSwim || !species.Aquatic.dragPreyToWater
                    || species.diet != CreatureDiet.Carnivore || actor.capturedByAquatic || actor.sedation > 0 || actor.shelterSleeping
                    || !Finite(hunt.remaining) || hunt.remaining < 0 || !checkGrid.Contains(hunt.waterCell)
                    || !species.Aquatic.IsSwimmable(checkGrid,hunt.waterCell)
                    || !checkGrid.GroundWalkable(hunt.releaseCell) || checkGrid.ContentAt(hunt.releaseCell) == BuildCellContent.Building
                    || hunt.phase != CreatureActivity.Attacking && hunt.phase != CreatureActivity.DraggingPrey
                        && hunt.phase != CreatureActivity.DrowningPrey && !feeding
                    || feeding == claims.ContainsKey(index)
                    || (feeding || hunt.phase == CreatureActivity.DrowningPrey) && actor.cell != hunt.waterCell)
                    throw new InvalidDataException(Loc.Text("aquatic.save.invalid_capture"));
                if(hunt.phase==CreatureActivity.DraggingPrey)
                {
                    var movement=new CreatureNavigation(checkGrid,checkFences,canSwim:true,minimumSwimDepth:species.Aquatic.minimumSwimDepth,
                        canWadeShallows:species.Aquatic.canWadeShallows);
                    if(movement.FindPath(actor.cell,hunt.waterCell).Count==0)
                        throw new InvalidDataException(Loc.Text("aquatic.save.invalid_capture"));
                }
            }
            foreach (var actor in data.staff)
                if (actor.hasAquaticHunt) throw new InvalidDataException(Loc.Text("aquatic.save.invalid_capture"));
            foreach (var actor in data.visitors)
                if (actor.hasAquaticHunt) throw new InvalidDataException(Loc.Text("aquatic.save.invalid_capture"));
        }

        private void RestoreAquaticLinks(ParkSaveData data, Dictionary<SavedActor,CreatureAgent> restored, List<CreatureAgent> creatures)
        {
            var prey = new Dictionary<int,CreatureAgent>();
            foreach (var entry in restored)
                if (entry.Key.capturedByAquatic) prey.Add(entry.Key.captureHunter,entry.Value);
            for (int index = 0; index < data.creatures.Count; index++)
            {
                var hunt = data.creatures[index].aquaticHunt;
                if (!data.creatures[index].hasAquaticHunt) continue;
                prey.TryGetValue(index,out var captured);
                if (!creatureWorld.RestoreAquaticHunt(creatures[index],captured,hunt))
                    throw new InvalidDataException(Loc.Text("aquatic.save.restore_capture_failed"));
            }
        }
    }
}

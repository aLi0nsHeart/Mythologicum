using System.Collections.Generic;
using System.IO;

namespace Mythologicum
{
    /// <summary>Preserves paid permits and dispatched rewards when old locations become world regions.</summary>
    public static class ExpeditionRegionMigration
    {
        public const int CurrentVersion = 1;

        public static string CanonicalId(string id)
        {
            switch(id)
            {
                case "japan": case "china": case "mongolia": return "east-asia";
                case "central-europe": return "western-europe";
                case "middle-east": return "west-asia";
                default: return id;
            }
        }

        // Only region lists and missions need copies here. The other collections are read by
        // validation, then copied by ParkProgression.Restore without changing the source snapshot.
        internal static ProgressionSave CanonicalSnapshot(ProgressionSave saved)
        {
            if(saved.regionVersion<0||saved.regionVersion>CurrentVersion)
                throw new InvalidDataException(Loc.Text("park.park_progression.invalid_park_progression"));
            bool legacy=saved.regionVersion==0;
            var result=new ProgressionSave {
                regionVersion=CurrentVersion,teams=saved.teams,nextMission=saved.nextMission,time=saved.time,
                technologies=saved.technologies,inventory=saved.inventory,projects=saved.projects,notices=saved.notices
            };
            var originalIds=new HashSet<string>();
            var canonicalIds=new HashSet<string>();
            foreach(string id in saved.regions)
            {
                // Different old locations can merge into one permit; repeated original IDs
                // are still malformed saves and must not disappear during deduplication.
                if(!originalIds.Add(id))
                    throw new InvalidDataException(Loc.Text("park.park_progression.unknown_or_duplicate_expedition_location"));
                string canonical=CanonicalId(id);
                if(canonicalIds.Add(canonical))result.regions.Add(canonical);
                if(legacy&&id=="west-africa"&&canonicalIds.Add("central-africa"))result.regions.Add("central-africa");
            }
            foreach(var mission in saved.missions)
            {
                if(mission==null){result.missions.Add(null);continue;}
                var copy=mission.Copy();
                copy.region=legacy&&mission.region=="west-africa"&&IsCentralAfrican(mission.species)
                    ? "central-africa" : CanonicalId(mission.region);
                result.missions.Add(copy);
            }
            return result;
        }

        private static bool IsCentralAfrican(string species) => species=="MokeleMbembe"||species=="Abada";
    }
}

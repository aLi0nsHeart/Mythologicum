using System;
using System.Collections.Generic;
using System.IO;
namespace Mythologicum
{
    [Serializable] public sealed class CreatureStock { public string species; public int count; }
    [Serializable] public sealed class ExpeditionMission
    {
        public int id, teams, quantity;
        public string region, difficulty, species;
        public float remaining, duration, chance;
        public bool succeeds;
        public ExpeditionMission Copy() => (ExpeditionMission)MemberwiseClone();
    }
    public sealed class TechnologyProject
    {
        public ParkBuildingRecord Home { get; internal set; }
        public TechnologyDefinition Definition { get; internal set; }
        public float Work { get; internal set; }
    }
    [Serializable] public sealed class SavedTechnology { public int building; public string technology; public float work; }
    [Serializable] public sealed class ProgressionSave
    {
        public int teams, nextMission;
        public float time;
        public List<string> regions=new List<string>(), technologies=new List<string>();
        public List<CreatureStock> inventory=new List<CreatureStock>();
        public List<ExpeditionMission> missions=new List<ExpeditionMission>();
        public List<SavedTechnology> projects=new List<SavedTechnology>();
        public List<ParkNotice> notices=new List<ParkNotice>();
    }
    /// <summary>Purchases, reserved expedition teams, acquired animals and permanent technology unlocks.</summary>
    public sealed class ParkProgression
    {
        public ProgressionRules Rules { get; }
        public NotificationInbox Inbox { get; } = new NotificationInbox();
        private readonly ParkEconomy economy;
        private readonly ParkBuildingNetwork buildings;
        private readonly Dictionary<string,int> ratings;
        private readonly Dictionary<string,int> stock=new Dictionary<string,int>();
        private readonly HashSet<string> access=new HashSet<string>(), completed=new HashSet<string>();
        private readonly List<ExpeditionMission> missions=new List<ExpeditionMission>();
        private readonly List<TechnologyProject> projects=new List<TechnologyProject>();
        private readonly Func<double> random;
        private readonly Func<string,string> speciesName;
        private int nextMission;
        public int Teams { get; private set; }
        public int Revision { get; private set; }
        public int AvailableTeams { get {int total=Teams;foreach(var m in missions)total-=m.teams;return total;} }
        public float NextTeamCost => Rules.teamCost+Teams*Rules.additionalTeamCost;
        public IReadOnlyList<ExpeditionMission> Missions => missions;
        public IReadOnlyList<TechnologyProject> Projects => projects;
        public ParkProgression(ProgressionRules rules, ParkEconomy economy, ParkBuildingNetwork buildings,
            Dictionary<string,int> ratings, Func<double> random=null, Func<string,string> speciesName=null)
        {
            Rules=rules;this.economy=economy;this.buildings=buildings;this.ratings=ratings;
            var rng=new Random();this.random=random ?? rng.NextDouble;this.speciesName=speciesName ?? (id=>id);
        }
        public ExpeditionRegion Region(string id) => Array.Find(Rules.regions,r=>r.id==id);
        public ExpeditionDifficulty Difficulty(string id) => Array.Find(Rules.difficulties,d=>d.id==id);
        public TechnologyDefinition Technology(string id) => Array.Find(Rules.technologies,t=>t.id==id);
        public bool HasAccess(string region) => access.Contains(region);
        public bool Completed(string technology) => completed.Contains(technology);
        public int Stock(string species) => species!=null && stock.TryGetValue(species,out int count) ? count : 0;
        public bool CanUse(ParkBuildingRecord home,BuildingKind kind) => home!=null && buildings.Contains(home) && home.IsOpen && home.Footprint.Definition.kind==kind;
        public bool BuildingUnlocked(BuildingKind kind)
        {
            bool requiresResearch=false;
            foreach(var t in Rules.technologies) if(Array.IndexOf(t.buildings,kind)>=0)
            {requiresResearch=true;if(Completed(t.id))return true;}
            return !requiresResearch;
        }
        public bool FenceUnlocked(FenceKind kind)
        {
            if(kind==FenceKind.Wooden) return true;
            foreach(var t in Rules.technologies) if(Array.IndexOf(t.fences,kind)>=0 && Completed(t.id)) return true;
            return false;
        }
        public string BuildingResearch(BuildingKind kind)
        {foreach(var t in Rules.technologies)if(Array.IndexOf(t.buildings,kind)>=0)return t.name;return "";}
        public float StaffEfficiency {get {float rate=1;foreach(var t in Rules.technologies)if(Completed(t.id))rate=Math.Max(rate,t.staffEfficiency);return rate;}}
        public float SuccessBonus {get {float rate=0;foreach(var t in Rules.technologies)if(Completed(t.id))rate+=t.expeditionSuccessBonus;return rate;}}
        public float DurationMultiplier {get {float rate=1;foreach(var t in Rules.technologies)if(Completed(t.id))rate*=t.expeditionDurationMultiplier;return rate;}}
        public List<string> Eligible(string region,string difficulty)
        {
            var result=new List<string>();var r=Region(region);var d=Difficulty(difficulty);
            if(r==null||d==null)return result;
            foreach(string id in r.species) if(ratings.TryGetValue(id,out int rating)&&rating>=d.minimumRating&&rating<=d.maximumRating)result.Add(id);
            return result;
        }
        public bool RegionHasCreatures(string id)
        {foreach(var d in Rules.difficulties)if(Eligible(id,d.id).Count>0)return true;return false;}
        public float Chance(string difficulty,int teams)
        {
            var d=Difficulty(difficulty); if(d==null||teams<1)return 0;
            return Math.Max(0f,Math.Min(Rules.maximumSuccess,d.successChance+(teams-1)*Rules.extraTeamSuccess+SuccessBonus));
        }
        public bool BuyTeam(ParkBuildingRecord home)
        {
            float cost=NextTeamCost;
            if(!CanUse(home,BuildingKind.ExpeditionCentre)||Teams>=Rules.maximumTeams||!economy.TrySpend(cost))return false;
            home.RecordExpense(cost);
            Teams++;Revision++;return true;
        }
        public bool BuyAccess(ParkBuildingRecord home,string region)
        {
            var r=Region(region);
            if(!CanUse(home,BuildingKind.ExpeditionCentre)||r==null||HasAccess(region)||!RegionHasCreatures(region)||!economy.TrySpend(r.accessCost))return false;
            home.RecordExpense(r.accessCost);
            access.Add(region);Revision++;return true;
        }
        public bool Launch(ParkBuildingRecord home,string region,string difficulty,int teams)
        {
            var d=Difficulty(difficulty);var eligible=Eligible(region,difficulty);
            if(!CanUse(home,BuildingKind.ExpeditionCentre)||!HasAccess(region)||d==null||eligible.Count==0||teams<1||teams>AvailableTeams
                ||!economy.TrySpend(d.costPerTeam*teams))return false;
            home.RecordExpense(d.costPerTeam*teams);
            float chance=Chance(difficulty,teams), duration=Math.Max(1f,d.seconds*DurationMultiplier);
            missions.Add(new ExpeditionMission {id=++nextMission,region=region,difficulty=difficulty,teams=teams,
                remaining=duration,duration=duration,chance=chance,succeeds=random()<chance,
                species=eligible[Math.Min(eligible.Count-1,(int)(random()*eligible.Count))],quantity=Math.Max(1,d.creaturesFound)});
            Revision++;return true;
        }
        public bool TryPlace(string species,Func<bool> place)
        {
            if(Stock(species)<1||place==null||!place())return false;
            stock[species]--;Revision++;return true;
        }
        public TechnologyProject ActiveProject(ParkBuildingRecord home) => projects.Find(p=>p.Home==home);
        public string ResearchBlock(ParkBuildingRecord home,string id)
        {
            var t=Technology(id);
            if(t==null)return "Unknown project";
            if(Completed(id))return "Completed";
            if(!CanUse(home,BuildingKind.ResearchCentre))return "Reconnect this research centre";
            if(home.HiredCount<1)return "Hire a researcher first";
            foreach(string prerequisite in t.prerequisites)if(!Completed(prerequisite))return "Requires "+(Technology(prerequisite)?.name??prerequisite);
            foreach(var p in projects)if(p.Definition.id==id)return "Already being researched";
            if(ActiveProject(home)!=null)return "This centre already has a project";
            if(!economy.CanAfford(t.cost))return "Insufficient funds";
            return "";
        }
        public bool StartResearch(ParkBuildingRecord home,string id)
        {
            if(ResearchBlock(home,id)!="")return false;
            var t=Technology(id);if(!economy.TrySpend(t.cost))return false;
            home.RecordExpense(t.cost);
            projects.Add(new TechnologyProject {Home=home,Definition=t});Revision++;return true;
        }
        public void AdvanceResearch(ParkBuildingRecord home,float work)
        {
            if(!Finite(work)||work<=0||!CanUse(home,BuildingKind.ResearchCentre)||home.HiredCount<1)return;
            var p=ActiveProject(home);if(p==null)return;
            p.Work=Math.Min(p.Definition.workSeconds,p.Work+work);
            if(p.Work<p.Definition.workSeconds)return;
            completed.Add(p.Definition.id);projects.Remove(p);Revision++;
            Inbox.Post("technology:"+p.Definition.id,ParkNoticeKind.ResearchFinished,"Research finished: "+p.Definition.name,
                p.Definition.description,home.Footprint.AccessCell);
        }
        public void Tick(float dt)
        {
            if(!Finite(dt)||dt<=0)return;
            Inbox.Tick(dt);
            for(int i=missions.Count-1;i>=0;i--)
            {
                var m=missions[i];m.remaining=Math.Max(0,m.remaining-dt);if(m.remaining>0)continue;
                if(m.succeeds) {stock[m.species]=(int)Math.Min(int.MaxValue,(long)Stock(m.species)+m.quantity);}
                Inbox.Post("mission:"+m.id,m.succeeds?ParkNoticeKind.ExpeditionSuccess:ParkNoticeKind.ExpeditionFailure,
                    "Expedition "+(m.succeeds?"successful":"failed")+": "+Region(m.region).name,
                    m.succeeds?m.quantity+" "+speciesName(m.species)+" added to Creatures. All teams have returned.":"No creatures found. All teams have returned.");
                missions.RemoveAt(i);Revision++;
            }
            for(int i=projects.Count-1;i>=0;i--)
                if(!buildings.Contains(projects[i].Home))
                {
                    Inbox.Post("research-lost:"+projects[i].Definition.id,ParkNoticeKind.ResearchInterrupted,"Research interrupted",
                        projects[i].Definition.name+": its research centre was demolished. Start it again in another centre.");
                    projects.RemoveAt(i);Revision++;
                }
        }
        public ProgressionSave Capture(IReadOnlyList<ParkBuildingRecord> records)
        {
            var result=new ProgressionSave {teams=Teams,nextMission=nextMission,time=Inbox.Time};
            result.regions.AddRange(access);result.technologies.AddRange(completed);result.regions.Sort();result.technologies.Sort();
            foreach(var pair in stock)result.inventory.Add(new CreatureStock {species=pair.Key,count=pair.Value});
            foreach(var mission in missions)result.missions.Add(mission.Copy());
            foreach(var p in projects)
            {int index=-1;for(int i=0;i<records.Count;i++)if(records[i]==p.Home)index=i;if(index>=0)result.projects.Add(new SavedTechnology{building=index,technology=p.Definition.id,work=p.Work});}
            foreach(var n in Inbox.Entries)result.notices.Add(n.Copy());
            return result;
        }
        public void Validate(ProgressionSave saved,IReadOnlyList<ParkBuildingRecord> records)
        {
            if(saved==null||saved.teams<0||saved.teams>Rules.maximumTeams||saved.nextMission<0||!Finite(saved.time)||saved.time<0
                ||saved.regions==null||saved.technologies==null||saved.inventory==null||saved.missions==null||saved.projects==null||saved.notices==null)
                throw new InvalidDataException("Invalid park progression.");
            var regions=new HashSet<string>();foreach(string id in saved.regions)if(Region(id)==null||!regions.Add(id))throw new InvalidDataException("Unknown or duplicate expedition location.");
            var technologies=new HashSet<string>();foreach(string id in saved.technologies)if(Technology(id)==null||!technologies.Add(id))throw new InvalidDataException("Unknown or duplicate technology.");
            var species=new HashSet<string>();foreach(var s in saved.inventory)if(s==null||!ratings.ContainsKey(s.species)||s.count<0||!species.Add(s.species))throw new InvalidDataException("Invalid creature stock.");
            var ids=new HashSet<int>();int busy=0;
            foreach(var m in saved.missions)
            {
                if(m==null||m.id<1||m.id>saved.nextMission||!ids.Add(m.id)||!regions.Contains(m.region)||Difficulty(m.difficulty)==null||m.teams<1||m.teams>saved.teams
                    ||m.quantity<1||m.quantity>100||!Finite(m.remaining)||m.remaining<0||!Finite(m.duration)||m.duration<=0||m.remaining>m.duration
                    ||!Finite(m.chance)||m.chance<0||m.chance>1||!ratings.ContainsKey(m.species))throw new InvalidDataException("Invalid active expedition.");
                busy+=m.teams;
            }
            if(busy>saved.teams)throw new InvalidDataException("Expedition teams are double booked.");
            var homes=new HashSet<int>();var researching=new HashSet<string>();
            foreach(var p in saved.projects)
                if(p==null||p.building<0||p.building>=records.Count||records[p.building].Footprint.Definition.kind!=BuildingKind.ResearchCentre
                    ||Technology(p.technology)==null||technologies.Contains(p.technology)||!researching.Add(p.technology)||!homes.Add(p.building)
                    ||!Finite(p.work)||p.work<0||p.work>=Technology(p.technology).workSeconds)throw new InvalidDataException("Invalid active research project.");
            ids.Clear();if(saved.notices.Count>NotificationInbox.Capacity)throw new InvalidDataException("Inbox is too large.");
            foreach(var n in saved.notices)if(n==null||n.id<1||!ids.Add(n.id)||n.count<1||!Finite(n.time)||n.time<0||n.time>saved.time||n.title==null||n.body==null||n.key==null
                ||!Enum.IsDefined(typeof(ParkNoticeKind),n.kind))throw new InvalidDataException("Invalid inbox entry.");
        }
        public void Restore(ProgressionSave saved,IReadOnlyList<ParkBuildingRecord> records)
        {
            Validate(saved,records);Teams=saved.teams;nextMission=saved.nextMission;
            access.Clear();foreach(string id in saved.regions)access.Add(id);
            completed.Clear();foreach(string id in saved.technologies)completed.Add(id);
            stock.Clear();foreach(var s in saved.inventory)stock.Add(s.species,s.count);
            missions.Clear();foreach(var m in saved.missions)missions.Add(m.Copy());
            projects.Clear();foreach(var p in saved.projects)projects.Add(new TechnologyProject{Home=records[p.building],Definition=Technology(p.technology),Work=p.work});
            Inbox.Restore(saved.time,saved.notices);Revision++;
        }
        public void AdoptLegacyPark(IEnumerable<ParkBuildingRecord> records,IEnumerable<FenceEdge> edges,FenceLayout fences)
        {
            foreach(var t in Rules.technologies)
            {
                foreach(var r in records)if(Array.IndexOf(t.buildings,r.Footprint.Definition.kind)>=0)completed.Add(t.id);
                foreach(var edge in edges)if(Array.IndexOf(t.fences,fences.KindAt(edge))>=0)completed.Add(t.id);
            }
            bool changed;do {changed=false;foreach(var t in Rules.technologies)if(completed.Contains(t.id))foreach(string id in t.prerequisites)if(completed.Add(id))changed=true;}while(changed);
            Revision++;
        }
        private static bool Finite(float value) => !float.IsNaN(value)&&!float.IsInfinity(value);
    }
}

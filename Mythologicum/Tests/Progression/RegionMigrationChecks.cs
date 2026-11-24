using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class RegionMigrationChecks
{
    private static int checks;
    private static void Check(bool value,string message)
    {checks++;if(!value)throw new Exception(message);}
    private static void Reject(Action action,string message)
    {bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,message);}

    private static ExpeditionMission Mission(int id,string region,string species,int quantity,float remaining,bool succeeds=true)
        => new ExpeditionMission {id=id,region=region,difficulty="survey",species=species,quantity=quantity,
            remaining=remaining,duration=90+id,chance=.37f,teams=1,succeeds=succeeds};

    private static bool SameMission(ExpeditionMission a,ExpeditionMission b,bool includeRegion=true)
        => a.id==b.id&&(!includeRegion||a.region==b.region)&&a.difficulty==b.difficulty&&a.species==b.species
            &&a.quantity==b.quantity&&a.remaining==b.remaining&&a.duration==b.duration&&a.chance==b.chance
            &&a.teams==b.teams&&a.succeeds==b.succeeds;

    public static void Main()
    {
        var grid=new SquareBuildGrid(Vector3.zero,2,20,12);
        var buildings=new ParkBuildingNetwork(grid,new FenceLayout(grid));
        ParkBuildingRecord Add(BuildingKind kind,int x) => buildings.TryPlace(new BuildingFootprint(
            new BuildingDefinition {kind=kind,width=1,depth=1},new Vector2Int(x,2),0));
        Add(BuildingKind.ParkEntrance,1);var centre=Add(BuildingKind.ExpeditionCentre,3);var research=Add(BuildingKind.ResearchCentre,5);
        for(int x=1;x<=5;x++)grid.TryOccupy(new Vector2Int(x,1),BuildCellContent.Path);
        buildings.Refresh();Check(centre.IsOpen&&research.IsOpen,"Migration fixture uses connected production buildings.");
        var ratings=new Dictionary<string,int> {
            {"Oni",5},{"Hitotsume",1},{"AllghoiKhorkhoi",3},{"MokeleMbembe",4},{"Abada",1},
            {"NinkiNanka",3},{"Manticore",4},{"Chupacabra",2},{"Wolpertinger",1}
        };
        var rules=new ProgressionRules();var economy=new ParkEconomy(10000);
        ParkProgression Park(ProgressionRules custom=null) => new ParkProgression(custom??rules,economy,buildings,ratings,
            ()=>throw new Exception("Restoring or completing saved missions must not reroll them."));

        var legacy=new ProgressionSave {teams=6,nextMission=17,time=17};
        Check(legacy.regionVersion==0,"Saves without a region version retain the legacy default.");
        legacy.regions.AddRange(new[]{"japan","china","mongolia","central-europe","middle-east","west-africa","south-america"});
        legacy.missions.AddRange(new[]{
            Mission(3,"japan","Oni",3,8),
            // Dispatched rewards stay valid even if they do not belong to the new regional pool or tier.
            Mission(5,"china","Chupacabra",2,9),
            Mission(8,"mongolia","AllghoiKhorkhoi",4,10),
            Mission(11,"west-africa","MokeleMbembe",5,11),
            Mission(13,"west-africa","NinkiNanka",6,12,false),
            Mission(17,"west-africa","Abada",7,13)
        });
        legacy.inventory.AddRange(new[]{new CreatureStock {species="Oni",count=4},new CreatureStock {species="Chupacabra",count=2}});
        legacy.technologies.Add("staff-training");
        legacy.projects.Add(new SavedTechnology {building=2,technology="food-service",work=12.5f});
        legacy.notices.Add(new ParkNotice {id=7,count=2,key="existing-letter",title="Original title",body="Original body",
            titleId="old.title",bodyId="old.body",titleArguments=new[]{"unchanged"},bodyArguments=new[]{"3"},
            time=3,kind=ParkNoticeKind.FenceOpened,severity=ParkNoticeSeverity.Warning,read=true,hasLocation=true,location=new Vector2Int(4,7)});
        var originalRegions=legacy.regions.ToArray();var originalMissions=legacy.missions.Select(m=>m.Copy()).ToArray();
        var park=Park();float balance=economy.Balance,expenses=centre.Expenses;
        park.Validate(legacy,buildings.Records);
        Check(legacy.regionVersion==0&&legacy.regions.SequenceEqual(originalRegions),"Validation does not rewrite caller region data or its version.");
        Check(legacy.missions.Zip(originalMissions,(a,b)=>SameMission(a,b)).All(same=>same),"Validation does not rewrite caller mission data.");
        park.Restore(legacy,buildings.Records);
        Check(legacy.regionVersion==0&&legacy.regions.SequenceEqual(originalRegions)&&legacy.missions.Zip(originalMissions,(a,b)=>SameMission(a,b)).All(same=>same),
            "Restoration also leaves the complete legacy region and mission snapshot unchanged.");
        Check(park.Teams==6&&park.AvailableTeams==0&&park.Missions.Count==6,"Migrating several locations retains every reserved team and active mission.");
        Check(park.HasAccess("east-asia")&&park.HasAccess("japan")&&park.HasAccess("china")&&park.HasAccess("mongolia"),"Three old Asian permits merge into one accessible canonical region.");
        Check(park.HasAccess("western-europe")&&park.HasAccess("west-asia")&&park.HasAccess("south-america"),"Renamed and unchanged permits are retained.");
        Check(park.HasAccess("west-africa")&&park.HasAccess("central-africa"),"The formerly combined African permit retains both paid areas.");
        Check(park.Stock("Oni")==4&&park.Stock("Chupacabra")==2&&park.Stock("MokeleMbembe")==0,"Migration keeps stock and does not issue early mission rewards.");
        Check(economy.Balance==balance&&centre.Expenses==expenses,"Migration does not charge for replacement permits or existing missions.");
        Check(park.Completed("staff-training")&&park.Projects.Count==1&&park.Projects[0].Home==research&&park.Projects[0].Definition.id=="food-service"&&park.Projects[0].Work==12.5f,
            "Knowledge, active research and accumulated research work survive unchanged.");
        var notice=park.Inbox.Entries.Single();
        Check(park.Inbox.Time==17&&notice.id==7&&notice.key=="existing-letter"&&notice.count==2&&notice.read&&notice.title=="Original title"&&notice.body=="Original body"
            &&notice.titleId=="old.title"&&notice.bodyId=="old.body"&&notice.titleArguments.SequenceEqual(new[]{"unchanged"})&&notice.bodyArguments.SequenceEqual(new[]{"3"})
            &&notice.hasLocation&&notice.location==new Vector2Int(4,7)&&notice.time==3&&notice.severity==ParkNoticeSeverity.Warning,
            "Unrelated inbox entries keep their contents, read state, arguments, time and location.");
        var canonical=park.Capture(buildings.Records);
        Check(canonical.regionVersion==1,"Every new capture writes the canonical region schema marker.");
        Check(canonical.regions.SequenceEqual(new[]{"central-africa","east-asia","south-america","west-africa","west-asia","western-europe"}),
            "Merged permits are deduplicated and captures contain only canonical region IDs.");
        string[] destinations={"east-asia","east-asia","east-asia","central-africa","west-africa","central-africa"};
        for(int i=0;i<canonical.missions.Count;i++)
        {
            Check(canonical.missions[i].region==destinations[i],"Active mission migrates to the selected creature's canonical region: "+canonical.missions[i].id);
            Check(SameMission(canonical.missions[i],originalMissions[i],false),"Mission ID, outcome, reward, timer, chance and reservation are preserved: "+canonical.missions[i].id);
        }
        var resumed=Park();resumed.Restore(canonical,buildings.Records);var roundtrip=resumed.Capture(buildings.Records);
        Check(roundtrip.regionVersion==canonical.regionVersion&&roundtrip.regions.SequenceEqual(canonical.regions)
            &&roundtrip.missions.Zip(canonical.missions,(a,b)=>SameMission(a,b)).All(same=>same)&&roundtrip.nextMission==17,
            "Canonical capture and restoration are idempotent after the migration.");
        resumed.Tick(7);Check(resumed.Stock("Oni")==4&&resumed.AvailableTeams==0,"Preserved mission timers prevent rewards before arrival.");
        resumed.Tick(1);Check(resumed.Stock("Oni")==7&&resumed.AvailableTeams==1&&resumed.Missions.Count==5,"The first saved mission completes once at its original arrival time.");
        resumed.Tick(100);
        Check(resumed.Stock("Chupacabra")==4&&resumed.Stock("AllghoiKhorkhoi")==4&&resumed.Stock("MokeleMbembe")==5&&resumed.Stock("Abada")==7
            &&resumed.Stock("NinkiNanka")==0&&resumed.AvailableTeams==6,"All dispatched rewards and failures survive changed regional pools.");
        resumed.Tick(100);var finished=resumed.Capture(buildings.Records);var finishedPark=Park();finishedPark.Restore(finished,buildings.Records);finishedPark.Tick(100);
        Check(finishedPark.Missions.Count==0&&finishedPark.Stock("Oni")==7&&finishedPark.Stock("Abada")==7&&finishedPark.Stock("Chupacabra")==4,
            "Further ticking and completed-save restoration never duplicate rewards.");
        Check(economy.Balance==balance&&centre.Expenses==expenses,"Rewards and round trips never recharge saved expedition costs.");

        var current=new ParkProgression(rules,economy,buildings,ratings,()=>0d);
        Check(current.BuyAccess(centre,"west-africa")&&!current.HasAccess("central-africa"),"Buying a new West Africa permit grants only West Africa.");
        var west=current.Capture(buildings.Records);var westReloaded=Park();westReloaded.Restore(west,buildings.Records);
        Check(west.regionVersion==1&&westReloaded.HasAccess("west-africa")&&!westReloaded.HasAccess("central-africa"),"The schema marker prevents granting Central Africa when a new West Africa permit is reloaded.");
        balance=economy.Balance;
        Check(current.BuyAccess(centre,"japan")&&current.HasAccess("east-asia")&&economy.Balance<balance,"Legacy API aliases purchase the canonical permit.");
        balance=economy.Balance;Check(!current.BuyAccess(centre,"china")&&economy.Balance==balance,"A second legacy alias cannot charge for the same merged permit again.");
        Check(current.BuyTeam(centre)&&current.Launch(centre,"mongolia","capture",1)&&current.Missions.Single().region=="east-asia",
            "New missions launched through legacy aliases store canonical destinations.");
        var starting=Park(new ProgressionRules {startingRegions=new[]{"japan","china","central-europe","middle-east"}}).Capture(buildings.Records);
        Check(starting.regions.SequenceEqual(new[]{"east-asia","west-asia","western-europe"}),"Old starting-region aliases are canonicalized and merged in new parks.");

        var invalid=new ProgressionSave();invalid.regions.AddRange(new[]{"japan","japan","china"});
        Reject(()=>park.Validate(invalid,buildings.Records),"Exact duplicate old IDs remain invalid even alongside mergeable aliases.");
        Check(invalid.regionVersion==0&&invalid.regions.SequenceEqual(new[]{"japan","japan","china"}),"Rejecting duplicates does not change the invalid caller snapshot.");
        invalid=new ProgressionSave();invalid.regions.AddRange(new[]{"west-africa","west-africa"});
        Reject(()=>park.Validate(invalid,buildings.Records),"Repeated legacy split permits remain invalid.");
        invalid=new ProgressionSave {regionVersion=1};invalid.regions.AddRange(new[]{"east-asia","east-asia"});
        Reject(()=>park.Validate(invalid,buildings.Records),"Exact duplicate canonical permits remain invalid.");
        invalid=new ProgressionSave();invalid.regions.Add("atlantis");
        int revision=park.Revision;
        Reject(()=>park.Restore(invalid,buildings.Records),"Unknown permit IDs cannot be migrated or restored.");
        Check(park.Revision==revision&&park.Teams==6&&park.Missions.Count==6,"Rejecting a migrated snapshot leaves the active park intact.");
        invalid=new ProgressionSave {teams=1,nextMission=1};invalid.regions.Add("japan");invalid.missions.Add(Mission(1,"atlantis","Oni",1,1));
        Reject(()=>park.Validate(invalid,buildings.Records),"Unknown mission destinations remain invalid.");
        invalid=new ProgressionSave {teams=1,nextMission=1};invalid.regions.Add("japan");invalid.missions.Add(Mission(1,"west-africa","Abada",1,1));
        Reject(()=>park.Validate(invalid,buildings.Records),"Mission migration cannot create a missing paid permit.");
        invalid=new ProgressionSave {teams=1,nextMission=2};invalid.regions.AddRange(new[]{"japan","china"});
        invalid.missions.AddRange(new[]{Mission(1,"japan","Oni",1,1),Mission(2,"china","Hitotsume",1,1)});
        Reject(()=>park.Validate(invalid,buildings.Records),"Merging permits cannot hide double-booked saved expedition teams.");
        invalid=new ProgressionSave {teams=2,nextMission=1};invalid.regions.AddRange(new[]{"japan","china"});
        invalid.missions.AddRange(new[]{Mission(1,"japan","Oni",1,1),Mission(1,"china","Hitotsume",1,1)});
        Reject(()=>park.Validate(invalid,buildings.Records),"Merging permits cannot hide duplicate mission IDs.");
        invalid=new ProgressionSave {regionVersion=2};Reject(()=>park.Validate(invalid,buildings.Records),"Unknown future region schema versions are rejected.");
        invalid=new ProgressionSave {regionVersion=-1};Reject(()=>park.Validate(invalid,buildings.Records),"Negative region schema versions are rejected.");
        Console.WriteLine("PASS: "+checks+" region migration, permit, mission, reward and snapshot checks.");
    }
}

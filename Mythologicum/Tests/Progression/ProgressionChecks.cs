using System;
using System.Collections.Generic;
using System.IO;
using Mythologicum;
using UnityEngine;
public static class ProgressionChecks
{
    static int checks;
    static void Check(bool yes,string why){checks++;if(!yes)throw new Exception(why);}
    static void Reject(Action action,string why){bool rejected=false;try{action();}catch(InvalidDataException){rejected=true;}Check(rejected,why);}
    public static void Main()
    {
        var grid=new SquareBuildGrid(Vector3.zero,2,40,40);var fences=new FenceLayout(grid);var buildings=new ParkBuildingNetwork(grid,fences);
        ParkBuildingRecord Add(BuildingKind kind,int x)=>buildings.TryPlace(new BuildingFootprint(new BuildingDefinition{kind=kind,width=1,depth=1},new Vector2Int(x,2),0));
        Add(BuildingKind.ParkEntrance,1);var expedition=Add(BuildingKind.ExpeditionCentre,3);var research=Add(BuildingKind.ResearchCentre,5);var second=Add(BuildingKind.ResearchCentre,7);
        for(int x=1;x<9;x++)grid.TryOccupy(new Vector2Int(x,1),BuildCellContent.Path);buildings.Refresh();
        var economy=new ParkEconomy(100000);var rules=new ProgressionRules();
        var ratings=new Dictionary<string,int>{{"Chupacabra",2},{"Hitotsume",1},{"Yurei",3},{"Oni",5}};
        var rolls=new Queue<double>(new[]{0d,0d,.999d,0d,0d,0d,0d,0d});
        var park=new ParkProgression(rules,economy,buildings,ratings,()=>rolls.Dequeue());
        Check(park.Stock("Chupacabra")==0&&park.Stock("Oni")==0,"Mythical stock starts at zero");
        Check(park.BuildingUnlocked(BuildingKind.ResearchCentre)&&park.BuildingUnlocked(BuildingKind.ExpeditionCentre),"No progression building deadlock");
        Check(!park.BuildingUnlocked(BuildingKind.Kiosk)&&!park.BuildingUnlocked(BuildingKind.RestArea),"Visitor service research gates new construction");
        Check(park.FenceUnlocked(FenceKind.Wooden)&&!park.FenceUnlocked(FenceKind.Heavy)&&!park.FenceUnlocked(FenceKind.Spiked),"Only basic fences at start");
        Check(park.Eligible("japan","survey").Count==1&&park.Eligible("japan","survey")[0]=="Hitotsume","Easy Japanese missions yield low-rated creatures");
        Check(park.Eligible("japan","dangerous").Contains("Oni")&&!park.Eligible("japan","survey").Contains("Oni"),"High-rated creatures require dangerous missions");
        Check(park.Chance("survey",1)>park.Chance("capture",1)&&park.Chance("capture",1)>park.Chance("dangerous",1),"Difficulty lowers success chance");
        Check(park.Chance("survey",2)>park.Chance("survey",1)&&park.Chance("survey",100)<=rules.maximumSuccess,"Teams help but never guarantee success");
        float balance=economy.Balance;
        Check(!park.Launch(expedition,"south-america","survey",1)&&economy.Balance==balance,"Cannot dispatch without permit or teams");
        Check(!park.BuyAccess(expedition,"china")&&economy.Balance==balance,"Do not sell empty regions");
        expedition.IsOpen=false;Check(!park.BuyTeam(expedition)&&economy.Balance==balance,"Disconnected centre cannot buy teams");expedition.IsOpen=true;
        Check(park.BuyAccess(expedition,"south-america")&&park.HasAccess("south-america"),"Buy regional permit");
        balance=economy.Balance;Check(!park.BuyAccess(expedition,"south-america")&&economy.Balance==balance,"Cannot charge twice for access");
        Check(park.BuyTeam(expedition)&&park.BuyTeam(expedition)&&park.Teams==2,"Purchased reusable teams");
        Check(expedition.Expenses==950,"Team and regional purchases belong to the expedition centre's accounts");
        Check(park.NextTeamCost==450,"Additional teams have increasing purchase costs");
        Check(park.Launch(expedition,"south-america","survey",1)&&park.AvailableTeams==1,"Dispatch reserves a team");
        Check(park.Launch(expedition,"south-america","survey",1)&&park.AvailableTeams==0,"Can run concurrent missions");
        Check(expedition.Expenses==1190,"Mission fees are recorded once in building expenses");
        balance=economy.Balance;Check(!park.Launch(expedition,"south-america","survey",1)&&economy.Balance==balance,"Cannot double book teams or charge failed launch");
        park.Tick(59);Check(park.Stock("Chupacabra")==0&&park.AvailableTeams==0,"No reward before arrival");
        var saved=park.Capture(buildings.Records);Check(saved.missions.Count==2&&saved.missions[0].succeeds&&!saved.missions[1].succeeds,"Store outcomes at dispatch, before saving");
        var restored=new ParkProgression(rules,economy,buildings,ratings,()=>throw new Exception("Loading must never reroll"));
        restored.Restore(saved,buildings.Records);Check(restored.AvailableTeams==0,"Loading retains team reservations");
        restored.Tick(1);Check(restored.Stock("Chupacabra")==2&&restored.AvailableTeams==2,"Success adds stock and failure still returns teams");
        Check(restored.Inbox.Entries.Count==2&&restored.Inbox.UnreadCount==2,"Mission success and failure produce unread letters");
        restored.Tick(100);Check(restored.Stock("Chupacabra")==2,"Mission rewards are issued once");
        Check(!restored.TryPlace("Oni",()=>throw new Exception("Should not spawn")),"No spawning without owned stock");
        Check(!restored.TryPlace("Chupacabra",()=>false)&&restored.Stock("Chupacabra")==2,"Invalid placement keeps the creature");
        Check(restored.TryPlace("Chupacabra",()=>true)&&restored.Stock("Chupacabra")==1,"Successful placement consumes one creature");
        Check(restored.TryPlace("Chupacabra",()=>true)&&!restored.TryPlace("Chupacabra",()=>true),"Cannot consume beyond zero stock");
        balance=economy.Balance;Check(!restored.StartResearch(research,"food-service")&&economy.Balance==balance,"Technology requires hired personnel");
        research.HiredCount=1;second.HiredCount=1;
        Check(!restored.StartResearch(research,"reinforced-fences"),"Technology prerequisites enforced");
        Check(restored.StartResearch(research,"spiked-fences"),"Start research with connected staffed lab");
        Check(research.Expenses==250,"Technology cost is recorded in the research centre's accounts");
        Check(!restored.StartResearch(research,"food-service")&&!restored.StartResearch(second,"spiked-fences"),"One project per lab and no duplicate global project");
        restored.AdvanceResearch(research,20);Check(restored.ActiveProject(research).Work==20,"Research accumulates actual researcher effort");
        research.IsOpen=false;restored.AdvanceResearch(research,100);Check(restored.ActiveProject(research).Work==20,"Disconnected laboratory pauses work");research.IsOpen=true;
        var mid=restored.Capture(buildings.Records);var resumed=new ParkProgression(rules,economy,buildings,ratings);resumed.Restore(mid,buildings.Records);
        Check(resumed.ActiveProject(research).Work==20&&!resumed.FenceUnlocked(FenceKind.Spiked),"Partial technology survives save without premature unlock");
        resumed.AdvanceResearch(research,25);Check(resumed.Completed("spiked-fences")&&resumed.FenceUnlocked(FenceKind.Spiked),"Completion unlocks matching construction");
        Check(resumed.Inbox.Entries[resumed.Inbox.Entries.Count-1].kind==ParkNoticeKind.ResearchFinished,"Technology completion creates letter");
        int count=resumed.Inbox.Entries.Count;resumed.AdvanceResearch(research,50);Check(resumed.Inbox.Entries.Count==count&&!resumed.StartResearch(research,"spiked-fences"),"No repeated completion or repurchase");
        Check(resumed.StartResearch(research,"reinforced-fences"),"Prerequisite unlock permits advanced research");resumed.AdvanceResearch(research,100);
        Check(resumed.FenceUnlocked(FenceKind.Heavy),"Heavy barrier unlocked independently");
        Check(resumed.StartResearch(research,"food-service"),"Food service project available");resumed.AdvanceResearch(research,30);Check(resumed.BuildingUnlocked(BuildingKind.Kiosk),"Kiosk unlocked");
        Check(resumed.StartResearch(research,"staff-training"),"Staff upgrade can start");resumed.AdvanceResearch(research,60);Check(resumed.StaffEfficiency==1.25f,"Staff upgrade has real efficiency effect");
        Check(resumed.StartResearch(research,"field-equipment"),"Equipment project available");resumed.AdvanceResearch(research,45);Check(Math.Abs(resumed.Chance("dangerous",1)-.45f)<.001f,"Equipment improves success preview");
        Check(resumed.StartResearch(research,"field-logistics"),"Logistics project unlocked");resumed.AdvanceResearch(research,60);Check(resumed.DurationMultiplier==.75f,"Logistics reduces duration");
        Check(resumed.StartResearch(research,"visitor-rest"),"Start project before demolition");buildings.Remove(research);resumed.Tick(1);
        Check(resumed.ActiveProject(research)==null&&!resumed.Completed("visitor-rest"),"Demolition removes project without granting technology");
        Check(resumed.Inbox.Entries[resumed.Inbox.Entries.Count-1].kind==ParkNoticeKind.ResearchInterrupted,"Interrupted project produces letter");
        var bad=park.Capture(buildings.Records);bad.teams=1;Reject(()=>park.Validate(bad,buildings.Records),"Reject overbooked saved teams");
        var invalid=new ProgressionSave();invalid.inventory.Add(new CreatureStock{species="Oni",count=-1});Reject(()=>park.Validate(invalid,buildings.Records),"Reject negative creature stock");
        var inbox=new NotificationInbox();inbox.Post("gap",ParkNoticeKind.FenceOpened,"Gap","Fence opened");inbox.MarkAllRead();Check(inbox.UnreadCount==0,"Opening inbox can mark letters read");
        inbox.Post("gap",ParkNoticeKind.FenceOpened,"Gap","Fence opened");Check(inbox.Entries.Count==1&&inbox.Entries[0].count==2&&inbox.UnreadCount==1,"Group repeated gaps and restore unread badge");
        inbox.Tick(6);inbox.Post("gap",ParkNoticeKind.FenceOpened,"Gap","Fence opened");Check(inbox.Entries.Count==2,"Later independent event gets a new letter");
        for(int i=0;i<110;i++)inbox.Post("new"+i,ParkNoticeKind.FenceOpened,"Gap","Fence opened");Check(inbox.Entries.Count==100,"Inbox history is bounded");
        inbox.MarkRead(inbox.Entries[0].id);var clone=new NotificationInbox();clone.Restore(inbox.Time,inbox.Entries);Check(clone.Entries.Count==100&&clone.UnreadCount==99,"Saved read state survives reload");
        Console.WriteLine("PASS: "+checks+" progression, expedition, inventory, technology and inbox checks.");
    }
}

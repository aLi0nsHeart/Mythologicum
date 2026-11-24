using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class BurrowingProgressionChecks
{
    private static int checks;
    private static void Check(bool value,string why){if(!value)throw new Exception(why);checks++;}
    public static void Main()
    {
        var grid=new SquareBuildGrid(Vector3.zero,2,18,8);
        var network=new ParkBuildingNetwork(grid,new FenceLayout(grid));
        network.TryPlace(new BuildingFootprint(new BuildingDefinition{kind=BuildingKind.ParkEntrance,width=1,depth=1},new Vector2Int(1,2),0));
        var expedition=network.TryPlace(new BuildingFootprint(new BuildingDefinition{kind=BuildingKind.ExpeditionCentre,width=1,depth=1},new Vector2Int(3,2),0));
        var research=network.TryPlace(new BuildingFootprint(new BuildingDefinition{kind=BuildingKind.ResearchCentre,width=1,depth=1},new Vector2Int(5,2),0));
        for(int x=1;x<=5;x++)grid.TryOccupy(new Vector2Int(x,1),BuildCellContent.Path);
        network.Refresh();Check(expedition.IsOpen&&research.IsOpen,"Both employee facilities have ordinary public path access.");
        research.HiredCount=1;
        var rules=new ProgressionRules();
        var ratings=new Dictionary<string,int>{{"Tatzelwurm",2},{"AllghoiKhorkhoi",4}};
        var park=new ParkProgression(rules,new ParkEconomy(100000),network,ratings,()=>0d);
        Check(park.Region("western-europe").species.Where(ratings.ContainsKey).SequenceEqual(new[]{"Tatzelwurm"}),"Western Europe offers the European burrowing species.");
        Check(park.Region("east-asia").species.Where(ratings.ContainsKey).SequenceEqual(new[]{"AllghoiKhorkhoi"}),"East Asia offers the Mongolian burrowing species.");
        Check(park.Stock("Tatzelwurm")==0&&park.Stock("AllghoiKhorkhoi")==0,"New creatures require actual acquisitions.");
        Check(park.Eligible("western-europe","survey").SequenceEqual(new[]{"Tatzelwurm"}),"Survey missions can find the smaller burrowing predator.");
        Check(!park.Eligible("east-asia","survey").Any()&&!park.Eligible("east-asia","capture").Any(),"Easy missions cannot award the high-rated death worm.");
        Check(park.Eligible("east-asia","dangerous").SequenceEqual(new[]{"AllghoiKhorkhoi"}),"Dangerous expeditions can acquire the death worm.");
        Check(park.BuyTeam(expedition),"A normal expedition team can be bought.");
        foreach(var request in new[]{new[]{"western-europe","survey","Tatzelwurm"},new[]{"east-asia","dangerous","AllghoiKhorkhoi"}})
        {
            Check(park.BuyAccess(expedition,request[0]),"The requested expedition region unlocks.");
            Check(park.Launch(expedition,request[0],request[1],1),"An expedition can be dispatched.");
            var mission=park.Missions.Single();
            Check(mission.species==request[2]&&mission.succeeds,"The mission has its eligible species and successful test roll.");
            var snapshot=park.Capture(network.Records);
            park=new ParkProgression(rules,new ParkEconomy(100000),network,ratings,()=>0d);park.Restore(snapshot,network.Records);
            Check(park.Stock(request[2])==0,"Loading an active mission does not grant early specimens.");
            park.Tick(mission.remaining+1);
            Check(park.Stock(request[2])==mission.quantity,"Completion grants the actual mission reward.");
            Check(!park.TryPlace(request[2],()=>false)&&park.Stock(request[2])==mission.quantity,"Blocked placement preserves stock.");
            Check(park.TryPlace(request[2],()=>true)&&park.Stock(request[2])==mission.quantity-1,"Successful placement consumes one specimen.");
        }
        Check(!park.FenceUnlocked(FenceKind.Deep),"Deep fences require their research project.");
        Check(park.StartResearch(research,"deep-fences"),"Research starts through the ordinary research API.");
        var active=park.Capture(network.Records);
        park=new ParkProgression(rules,new ParkEconomy(100000),network,ratings,()=>0d);park.Restore(active,network.Records);
        Check(!park.FenceUnlocked(FenceKind.Deep),"Restoring an active project retains its lock.");
        park.AdvanceResearch(research,park.Technology("deep-fences").workSeconds+1);
        Check(park.FenceUnlocked(FenceKind.Deep),"Completing the project unlocks deep fencing.");
        var complete=park.Capture(network.Records);
        park=new ParkProgression(rules,new ParkEconomy(100000),network,ratings,()=>0d);park.Restore(complete,network.Records);
        Check(park.FenceUnlocked(FenceKind.Deep),"The fence unlock persists across save/load.");
        Console.WriteLine($"PASS: {checks} burrowing species expedition, inventory and deep-fence research checks.");
    }
}

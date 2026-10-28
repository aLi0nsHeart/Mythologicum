using System;
using Mythologicum;
using UnityEngine;

public static class BuildingAccountChecks
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void Equal(float expected,float actual,string message)
    { Check(!float.IsNaN(actual) && Math.Abs(expected-actual)<.001f,message); }
    private static ParkBuildingRecord Record(BuildingDefinition definition) =>
        new ParkBuildingRecord(new BuildingFootprint(definition,new Vector2Int(1,1),0));

    public static void Main()
    {
        var kioskDefinition = new BuildingDefinition { kind=BuildingKind.Kiosk, foodPrice=8 };
        var first=Record(kioskDefinition); var second=Record(kioskDefinition);
        Equal(8,first.Price,"Placed kiosk starts with its configured food price");
        Check(first.SetPrice(12.5f),"Kiosk price can be changed");
        Equal(12.5f,first.Price,"Fractional price retained"); Equal(8,second.Price,"Other kiosks retain independent prices");
        Equal(8,kioskDefinition.foodPrice,"Editing a placed price does not change shared definition");
        Check(first.RuntimeId!=second.RuntimeId,"Placed buildings have distinct runtime identities");
        Check(first.SetPrice(0),"Free service is allowed");
        foreach(float invalid in new[]{-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
            Check(!first.SetPrice(invalid),"Invalid service price rejected");
        Equal(0,first.Price,"Rejected prices preserve current price");
        var entry=Record(new BuildingDefinition { kind=BuildingKind.ParkEntrance });
        var rest=Record(new BuildingDefinition { kind=BuildingKind.RestArea });
        Equal(15,entry.Price,"Entrance starts with prototype ticket price");
        Equal(0,rest.Price,"Rest starts free"); Check(rest.SetPrice(3),"Rest area price can be set");
        var staffDefinition=new BuildingDefinition { kind=BuildingKind.MercenaryHQ, operatingCostPerSecond=.1f,staffSalaryPerSecond=.5f };
        var home=Record(staffDefinition);
        Check(!home.SetPrice(20),"Employee building cannot sell nonexistent visitor service");
        Equal(.1f,home.UpkeepPerSecond,"Empty staff building has base upkeep only");
        home.HiredCount=3; Equal(1.6f,home.UpkeepPerSecond,"Three salaries add to base upkeep");
        home.HiredCount=10; Check(home.HiredCount==5,"Per-building personnel limit is five");
        home.HiredCount=-1; Check(home.HiredCount==0,"Personnel count cannot become negative");
        first.HiredCount=4; Check(first.HiredCount==0,"Ordinary service cannot accrue employee slots");
        home.RecordRevenue(50); home.RecordExpense(20); Equal(30,home.Profit,"Operating profit subtracts attributed costs");
        foreach(float invalid in new[]{-1f,float.NaN,float.PositiveInfinity,float.NegativeInfinity})
        { home.RecordRevenue(invalid); home.RecordExpense(invalid); }
        Equal(50,home.Revenue,"Invalid revenue ignored"); Equal(20,home.Expenses,"Invalid expenses ignored");
        var feeder=Record(new BuildingDefinition { kind=BuildingKind.GoatDispenser,feederCapacity=3 });
        Check(feeder.FeedStock==3,"Feeder begins stocked");
        Check(feeder.ConsumeFeed()&&feeder.ConsumeFeed()&&feeder.ConsumeFeed(),"Successful releases consume stock");
        Check(!feeder.ConsumeFeed()&&feeder.FeedStock==0,"Empty feeder cannot consume or go negative");
        feeder.RestockFeed(); Check(feeder.FeedStock==3,"Delivery restores capacity");
        Check(!first.ConsumeFeed(),"Other buildings cannot consume dispenser supplies");
        var grid=new SquareBuildGrid(Vector3.zero,2,12,12); var fences=new FenceLayout(grid);
        var network=new ParkBuildingNetwork(grid,fences); var economy=new ParkEconomy(100);
        var entrance=network.TryPlace(new BuildingFootprint(new BuildingDefinition { kind=BuildingKind.ParkEntrance,width=1,depth=1,operatingCostPerSecond=.2f },new Vector2Int(1,1),0));
        var research=network.TryPlace(new BuildingFootprint(new BuildingDefinition { kind=BuildingKind.ResearchCentre,width=1,depth=1,operatingCostPerSecond=.3f,staffSalaryPerSecond=.5f },new Vector2Int(4,1),0));
        Check(entrance!=null&&research!=null,"Accounting fixture reserves actual building records");
        research.HiredCount=2;
        BuildingEconomy.Tick(network,economy,10);
        Equal(85,economy.Balance,"All building costs charged once"); Equal(15,economy.Expenses,"Bank records aggregate costs");
        Equal(2,entrance.Expenses,"Entrance attributes its base upkeep"); Equal(13,research.Expenses,"Research building attributes upkeep and salaries");
        Equal(-13,research.Profit,"Unprofitable building reports operating loss");
        foreach(float invalid in new[]{0f,-1f,float.NaN,float.PositiveInfinity}) BuildingEconomy.Tick(network,economy,invalid);
        Equal(85,economy.Balance,"Invalid time cannot charge upkeep");
        network.Remove(research); BuildingEconomy.Tick(network,economy,10);
        Equal(83,economy.Balance,"Demolished building stops charging future salaries and upkeep");
        Equal(13,research.Expenses,"Demolished building's historical accounts remain fixed");
        economy.UnlimitedFunds=true; BuildingEconomy.Tick(network,economy,10);
        Equal(83,economy.Balance,"Infinite funds preserve balance"); Equal(6,entrance.Expenses,"Infinite funds still attribute operating expenses");
        Check((int)BuildingKind.RestArea==4 && (int)BuildingKind.ResearchCentre==5,"New building values preserve serialized originals");
        Console.WriteLine("PASS: "+checks+" building accounts, pricing, salaries and feed stock checks.");
    }
}

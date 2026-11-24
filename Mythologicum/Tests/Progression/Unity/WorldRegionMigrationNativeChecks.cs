using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;

/// <summary>Called by WorldRegionsUIValidation inside its disposable Unity Play mode session.</summary>
public static class WorldRegionMigrationNativeChecks
{
    static int checks;
    static void Check(bool value,string why)
    {checks++;if(!value)throw new Exception(why);Debug.Log("WORLD REGION MIGRATION PASS: "+why);}
    static bool SameMission(ExpeditionMission a,ExpeditionMission b)
        => a.id==b.id&&a.teams==b.teams&&a.quantity==b.quantity&&a.difficulty==b.difficulty&&a.species==b.species
            &&a.remaining==b.remaining&&a.duration==b.duration&&a.chance==b.chance&&a.succeeds==b.succeeds;

    public static int RunChecks()
    {
        checks=0;
        // Deliberately written as old on-disk JSON: the regionVersion field is absent.
        const string legacyJson=@"{
            ""teams"":6,""nextMission"":17,""time"":17,
            ""regions"":[""japan"",""china"",""mongolia"",""central-europe"",""middle-east"",""west-africa"",""south-america""],
            ""technologies"":[""staff-training""],""inventory"":[{""species"":""Oni"",""count"":4},{""species"":""Chupacabra"",""count"":2}],
            ""missions"":[
                {""id"":3,""teams"":1,""quantity"":3,""region"":""japan"",""difficulty"":""survey"",""species"":""Oni"",""remaining"":8,""duration"":93,""chance"":0.37,""succeeds"":true},
                {""id"":5,""teams"":1,""quantity"":2,""region"":""china"",""difficulty"":""survey"",""species"":""Chupacabra"",""remaining"":9,""duration"":95,""chance"":0.37,""succeeds"":true},
                {""id"":8,""teams"":1,""quantity"":4,""region"":""mongolia"",""difficulty"":""survey"",""species"":""AllghoiKhorkhoi"",""remaining"":10,""duration"":98,""chance"":0.37,""succeeds"":true},
                {""id"":11,""teams"":1,""quantity"":5,""region"":""west-africa"",""difficulty"":""survey"",""species"":""MokeleMbembe"",""remaining"":11,""duration"":101,""chance"":0.37,""succeeds"":true},
                {""id"":13,""teams"":1,""quantity"":6,""region"":""west-africa"",""difficulty"":""survey"",""species"":""NinkiNanka"",""remaining"":12,""duration"":103,""chance"":0.37,""succeeds"":false},
                {""id"":17,""teams"":1,""quantity"":7,""region"":""west-africa"",""difficulty"":""survey"",""species"":""Abada"",""remaining"":13,""duration"":107,""chance"":0.37,""succeeds"":true}],
            ""projects"":[{""building"":2,""technology"":""food-service"",""work"":12.5}],
            ""notices"":[{""id"":7,""count"":2,""key"":""existing-letter"",""title"":""Original title"",""body"":""Original body"",
                ""titleArguments"":[""unchanged""],""bodyArguments"":[""3""],""kind"":0,""severity"":1,""read"":true,""time"":3}]}";
        Check(!legacyJson.Contains("regionVersion"),"Legacy native JSON genuinely omits the region schema marker");
        var legacy=JsonUtility.FromJson<ProgressionSave>(legacyJson);
        Check(legacy.regionVersion==0,"Native JsonUtility supplies version zero for an absent region marker");
        string untouched=JsonUtility.ToJson(legacy);
        var grid=new SquareBuildGrid(new Vector3(10000,0,10000),2,20,12);
        var network=new ParkBuildingNetwork(grid,new FenceLayout(grid));
        ParkBuildingRecord Add(BuildingKind kind,int x) => network.TryPlace(new BuildingFootprint(
            new BuildingDefinition {kind=kind,width=1,depth=1},new Vector2Int(x,2),0));
        Add(BuildingKind.ParkEntrance,1);var centre=Add(BuildingKind.ExpeditionCentre,3);var research=Add(BuildingKind.ResearchCentre,5);
        for(int x=1;x<=5;x++)grid.TryOccupy(new Vector2Int(x,1),BuildCellContent.Path);network.Refresh();
        Check(centre!=null&&research!=null&&centre.IsOpen&&research.IsOpen,"Native migration fixture has ordinary connected model buildings");
        var ratings=new Dictionary<string,int> {{"Oni",5},{"Chupacabra",2},{"AllghoiKhorkhoi",3},{"MokeleMbembe",4},{"NinkiNanka",3},{"Abada",1}};
        var economy=new ParkEconomy(10000);var rules=new ProgressionRules();
        ParkProgression Park() => new ParkProgression(rules,economy,network,ratings,()=>throw new Exception("Saved missions must not reroll"));
        var park=Park();park.Validate(legacy,network.Records);
        Check(JsonUtility.ToJson(legacy)==untouched,"Native legacy validation leaves the deserialized caller snapshot unchanged");
        park.Restore(legacy,network.Records);
        Check(JsonUtility.ToJson(legacy)==untouched&&JsonUtility.FromJson<ProgressionSave>(legacyJson).regionVersion==0,
            "Native legacy restoration leaves both the source JSON and caller snapshot unchanged");
        Check(park.Teams==6&&park.AvailableTeams==0&&park.Stock("Oni")==4&&park.Stock("Chupacabra")==2,"Native migration preserves teams, reservations and existing stock");
        Check(economy.Balance==10000&&centre.Expenses==0,"Native migration does not recharge permits or missions");
        Check(park.Completed("staff-training")&&park.Projects.Single().Home==research&&park.Projects.Single().Work==12.5f,"Native migration preserves knowledge and partial research");
        Check(park.Inbox.Time==17&&JsonUtility.ToJson(park.Inbox.Entries.Single())==JsonUtility.ToJson(legacy.notices.Single()),"Native migration preserves unrelated inbox contents and time");
        var captured=park.Capture(network.Records);
        Check(captured.regions.SequenceEqual(new[]{"central-africa","east-asia","south-america","west-africa","west-asia","western-europe"}),
            "Native migration merges Asian aliases, renames permits and splits the old combined African permit");
        string[] expected={"east-asia","east-asia","east-asia","central-africa","west-africa","central-africa"};
        for(int i=0;i<6;i++)Check(captured.missions[i].region==expected[i]&&SameMission(captured.missions[i],legacy.missions[i]),
            "Native migration preserves destination mapping, identity, timer, chance, outcome and reward of mission "+legacy.missions[i].id);
        string canonicalJson=JsonUtility.ToJson(captured);var canonical=JsonUtility.FromJson<ProgressionSave>(canonicalJson);
        Check(canonical.regionVersion==1&&canonicalJson.Contains("\"regionVersion\":1"),"Native canonical JSON explicitly writes and reads the new region schema marker");
        var resumed=Park();resumed.Validate(canonical,network.Records);resumed.Restore(canonical,network.Records);
        Check(JsonUtility.ToJson(resumed.Capture(network.Records))==canonicalJson,"Native canonical JSON capture and restoration are idempotent");
        resumed.Tick(7);Check(resumed.Stock("Oni")==4&&resumed.AvailableTeams==0,"Native roundtrip retains original mission arrival times");
        resumed.Tick(6);
        Check(resumed.Stock("Oni")==7&&resumed.Stock("Chupacabra")==4&&resumed.Stock("AllghoiKhorkhoi")==4&&resumed.Stock("MokeleMbembe")==5
            &&resumed.Stock("Abada")==7&&resumed.Stock("NinkiNanka")==0&&resumed.AvailableTeams==6,"Native roundtrip keeps all dispatched rewards and failures, including old pool selections");
        var complete=Park();complete.Restore(JsonUtility.FromJson<ProgressionSave>(JsonUtility.ToJson(resumed.Capture(network.Records))),network.Records);complete.Tick(100);
        Check(complete.Missions.Count==0&&complete.Stock("Oni")==7&&complete.Stock("Abada")==7&&economy.Balance==10000,"Native completed JSON roundtrip never duplicates rewards or costs");
        var westOnly=new ProgressionSave {regionVersion=1};westOnly.regions.Add("west-africa");
        string westJson=JsonUtility.ToJson(westOnly);var west=Park();west.Restore(JsonUtility.FromJson<ProgressionSave>(westJson),network.Records);
        Check(west.HasAccess("west-africa")&&!west.HasAccess("central-africa"),"A native version-one West Africa permit never grants Central Africa");
        string westCanonical=JsonUtility.ToJson(west.Capture(network.Records));var westAgain=Park();westAgain.Restore(JsonUtility.FromJson<ProgressionSave>(westCanonical),network.Records);
        Check(!westAgain.HasAccess("central-africa")&&JsonUtility.ToJson(westAgain.Capture(network.Records))==westCanonical,"Native West-only JSON roundtrips stay idempotent");
        return checks;
    }
}

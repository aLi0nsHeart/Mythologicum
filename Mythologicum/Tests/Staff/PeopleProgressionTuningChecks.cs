using System;
using System.Collections.Generic;
using Mythologicum;

public static class PeopleProgressionTuningChecks
{
    static int checks;
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Equal(float expected, float actual, string message) => Check(Math.Abs(expected - actual) < .001f, message + ": " + actual);
    public static void Main()
    {
        var tuning = new VisitorMoodRules { initialSatisfaction = 40, patienceSeconds = 20,
            unhappyPatienceRate = 1, happyPatienceRate = 1, baselineLossPerSecond = 0 };
        var guest = new VisitorMood(rules: tuning);
        Equal(40, guest.Satisfaction, "Configured starting satisfaction");
        guest.Tick(10, 20, 80, 0, false, false, 0, 300);
        Equal(.5f, guest.VisitProgress, "Configured patience duration controls visit progress");
        Equal(10, guest.RemainingPatience, "Remaining patience uses the same configured duration");
        tuning.watchingGainPerSecond = 3;
        guest.Tick(2, 20, 80, 0, true, false, 0, 300);
        Equal(46, guest.Satisfaction, "Watching gain responds to edited tuning");
        tuning.baselineLossPerSecond = 2;
        guest.Tick(2, 20, 80, 0, false, false, 0, 300);
        Equal(42, guest.Satisfaction, "Baseline dissatisfaction responds to edited tuning");
        tuning.leaveSatisfactionThreshold = 45;
        guest.Tick(.1f, 20, 80, 0, false, false, 0, 300);
        Check(guest.ShouldLeave, "Edited departure threshold controls the leave decision");

        var discoveries = new SpeciesResearchRules { foodDiscovery = 5, habitatDiscovery = 10,
            socialDiscovery = 50, containmentDiscovery = 90 };
        var research = new ResearchProgress(discoveries);
        research.Study(6); Check(research.UnlockedStages == 1, "Custom food discovery threshold");
        research.Study(34); Check(research.UnlockedStages == 2, "Custom habitat discovery threshold");
        research.Study(10); Check(research.UnlockedStages == 3, "Custom social discovery threshold");
        research.Study(40); Check(research.UnlockedStages == 4 && research.Progress == 90, "Full biography can unlock before research completion");
        discoveries.containmentDiscovery = 95;
        Check(research.UnlockedStages == 3, "Existing research reads edited discovery milestones");

        var rules = new ProgressionRules { startingTeams = 3,
            startingRegions = new[] { "japan" }, startingTechnologies = new[] { "staff-training" },
            startingCreatures = new[] { new CreatureStock { species = "Oni", count = 7 } },
            startingFences = new[] { FenceKind.Heavy }, extraTeamSuccess = .2f, maximumSuccess = .99f,
            notificationMergeSeconds = 2f };
        var park = new ParkProgression(rules, new ParkEconomy(), null, new Dictionary<string,int> { { "Oni", 5 } });
        Check(park.Teams == 3 && park.AvailableTeams == 3, "New parks use configured initial expedition teams");
        Check(park.HasAccess("japan") && park.Completed("staff-training") && park.Stock("Oni") == 7,
            "New parks use configured locations, technology and creature stock");
        Check(park.FenceUnlocked(FenceKind.Heavy) && !park.FenceUnlocked(FenceKind.Wooden), "Starting fence selection is data driven");
        Equal(.55f, park.Chance("dangerous", 2), "Team probability bonus uses edited rules");
        Equal(1.25f, park.StaffEfficiency, "Starting technologies apply actual staff upgrades");
        park.Inbox.Post("same", ParkNoticeKind.FenceOpened, "First", "Body");
        park.Inbox.Tick(3);
        park.Inbox.Post("same", ParkNoticeKind.FenceOpened, "Second", "Body");
        Check(park.Inbox.Entries.Count == 2, "Configured notice merge duration is used by the live inbox");
        Console.WriteLine("PASS: " + checks + " visitor, research and progression tuning checks.");
    }
}

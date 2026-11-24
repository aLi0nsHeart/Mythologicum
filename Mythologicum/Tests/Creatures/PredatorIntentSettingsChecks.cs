using System;
using Mythologicum;

public static class PredatorIntentSettingsChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); checks++; }
        var stalk = new CreatureStalkingSettings();
        Check(!stalk.enabled, "Stalking is opt-in for every existing species.");
        stalk.minimumStalkSeconds = 4; stalk.maximumStalkSeconds = 1;
        stalk.coverSearchSteps = 10000; stalk.coverTreeRadius = -1; stalk.stalkSpeedMultiplier = float.NaN;
        stalk.ambushRange = float.PositiveInfinity; stalk.coverWaitSeconds = -10; stalk.Validate();
        Check(stalk.maximumStalkSeconds >= stalk.minimumStalkSeconds, "The stalking deadline can never precede its deliberate approach.");
        Check(stalk.coverSearchSteps == 64 && stalk.coverTreeRadius == 0, "Cover queries have a finite local work limit.");
        Check(stalk.stalkSpeedMultiplier > 0 && stalk.stalkSpeedMultiplier <= 1 && stalk.ambushRange > 0 && stalk.coverWaitSeconds == 0,
            "Non-finite movement and timing values become usable.");
        var lure = new CreatureLureSettings();
        Check(!lure.enabled && !lure.Permits(VisitorType.Children), "Luring is disabled by default.");
        lure.enabled = true;
        Check(lure.Permits(VisitorType.Children) && !lure.Permits(VisitorType.Adventurers) && !lure.Permits(VisitorType.ThrillSeekers),
            "The child-only profile cannot lure adult visitor types.");
        lure.visitorTypes = new[] { VisitorType.Wonderlust };
        Check(lure.Permits(VisitorType.Wonderlust) && !lure.Permits(VisitorType.Children), "Visitor eligibility is species data rather than a hardcoded creature branch.");
        lure.visitorTypes = null; lure.range = float.NaN; lure.cooldownSeconds = -1; lure.durationSeconds = float.PositiveInfinity;
        lure.shorelineSearchSteps = 2000; lure.maximumApproachSteps = 2000; lure.maximumVisitorCandidates = 2000; lure.Validate();
        Check(lure.visitorTypes != null && !lure.Permits(VisitorType.Children), "An empty eligibility list remains safe and cannot lure anyone.");
        Check(lure.range > 0 && lure.cooldownSeconds > 0 && lure.durationSeconds > 0, "Invalid lure range and clocks cannot create an unending effect.");
        Check(lure.shorelineSearchSteps == 64 && lure.maximumApproachSteps == 64 && lure.maximumVisitorCandidates == 32,
            "Candidate and shore routing work remain bounded by validated data.");
        Check((int)VisitorActivity.Lured > (int)VisitorActivity.EnjoyingAttraction, "The appended activity preserves existing saved visitor enum values.");
        Console.WriteLine("PASS: " + checks + " optional stalking/lure data, work bounds, visitor eligibility and saved activity checks.");
    }
}

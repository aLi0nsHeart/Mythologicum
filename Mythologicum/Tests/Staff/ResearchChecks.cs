using System;
using Mythologicum;

public static class ResearchChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool condition,string message) { checks++; if (!condition) throw new Exception(message); }
        var first = new ResearchProgress();
        var second = new ResearchProgress();
        Check(first.Progress == 0 && first.UnlockedStages == 0,"New species has no discoveries.");
        foreach (float amount in new[] { -1f,0f,float.NaN,float.PositiveInfinity,float.NegativeInfinity })
            Check(!first.Study(amount) && first.Progress == 0,"Invalid research cannot alter knowledge.");
        Check(first.Study(24.99f) && first.UnlockedStages == 0,"Partial research does not reveal the first note.");
        Check(first.Study(.01f) && first.UnlockedStages == 1,"Food note unlocks at 25 percent.");
        Check(second.Progress == 0 && second.UnlockedStages == 0,"Studying one species does not unlock another.");
        Check(first.Study(25) && first.Progress == 50 && first.UnlockedStages == 2,"Habitat notes unlock at 50 percent.");
        Check(first.Study(25) && first.UnlockedStages == 3,"Space and social notes unlock at 75 percent.");
        Check(first.Study(25) && first.Progress == 100 && first.UnlockedStages == SpeciesResearch.StageCount,"Containment note unlocks at full research.");
        Check(!first.Study(1) && first.Progress == 100,"Completed research cannot overflow or consume more work.");
        Check(second.Study(float.MaxValue) && second.Progress == 100 && second.UnlockedStages == 4,"Oversized finite research safely caps at completion.");
        var combined = new ResearchProgress(); var split = new ResearchProgress();
        combined.Study(17.5f); for (int i=0;i<70;i++) split.Study(.25f);
        Check(combined.Progress == split.Progress,"Several employees share the same additive study effort.");
        Console.WriteLine("PASS: " + checks + " research progress checks.");
    }
}

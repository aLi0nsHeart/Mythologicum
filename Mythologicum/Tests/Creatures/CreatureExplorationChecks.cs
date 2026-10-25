using System;
using Mythologicum;
public static class CreatureExplorationChecks
{
    public static void Main()
    {
        var exploration = new CreatureExploration();
        void Check(bool condition) { if (!condition) throw new Exception("Exploration policy failed"); }
        Check(!exploration.WillClimb(45, 55, 100, false));
        Check(exploration.WillClimb(45, 80, 100, false));
        Check(exploration.WillClimb(45, 0, 8, false));
        Check(exploration.WillClimb(45, 0, 100, true));
        exploration.Tick(float.NaN); exploration.Tick(-1);
        Check(exploration.AwakeSeconds == 0);
        exploration.Tick(44);
        Check(!exploration.WillClimb(45, 55, 100, false));
        exploration.Tick(1);
        Check(exploration.WillClimb(45, 55, 100, false));
        Check(new CreatureExploration().WillClimb(0, 55, 100, false));
        Console.WriteLine("PASS: 8 settling and urgent-needs checks.");
    }
}

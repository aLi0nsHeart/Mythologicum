using System;
using Mythologicum;

public static class CreatureHealthChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool condition) { checks++; if (!condition) throw new Exception("Health check failed: " + checks); }
        var goat = new CreatureHealth(40);
        Check(goat.Maximum == 40 && goat.Current == 40 && goat.Alive);
        Check(goat.Damage(40) == 40 && goat.Current == 0 && !goat.Alive);
        Check(goat.Damage(100) == 0);
        goat.Restore(40); Check(!goat.Alive && goat.Current == 0);
        var creature = new CreatureHealth(120);
        Check(creature.Damage(35) == 35 && creature.Current == 85 && creature.Alive);
        Check(creature.Damage(35) == 35 && creature.Current == 50);
        creature.Restore(20); Check(creature.Current == 70);
        creature.Restore(float.MaxValue); Check(creature.Current == 120);
        foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            Check(creature.Damage(invalid) == 0 && creature.Current == 120);
            creature.Damage(10); creature.Restore(invalid); Check(creature.Current == 110);
            creature.Restore(10);
        }
        Check(new CreatureHealth(0).Maximum == 1);
        Check(new CreatureHealth(float.NaN).Maximum == 100);
        Check(new CreatureHealth(float.PositiveInfinity).Maximum == 100);
        Check(creature.Damage(float.MaxValue) == 120 && !creature.Alive);
        Console.WriteLine("PASS: " + checks + " creature health checks.");
    }
}

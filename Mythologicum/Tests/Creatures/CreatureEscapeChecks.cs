using System;
using Mythologicum;
public static class CreatureEscapeChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool condition) { checks++; if (!condition) throw new Exception("Escape probability check failed: " + checks); }
        float Chance(float hunger, float agitation, float crowding) => CreatureEscape.Chance(hunger, agitation, crowding, .0005f, .3f, .35f, .35f);
        Check(Math.Abs(Chance(0,0,0) - .0005f) < .00001f);
        Check(Chance(75,0,0) == Chance(0,0,0));
        Check(Chance(90,0,0) > Chance(80,0,0));
        Check(Chance(0,1,0) > Chance(0,.5f,0));
        Check(Chance(0,0,1) > Chance(0,0,0));
        Check(Chance(100,1,1) <= .95f);
        Check(CreatureEscape.Overcrowding(100, 100) == 0);
        Check(CreatureEscape.Overcrowding(100, 200) == .5f);
        Check(CreatureEscape.Overcrowding(10,0) == 0);
        Check(CreatureEscape.Probability(.3f,0) == 0);
        Check(CreatureEscape.Probability(.3f,float.NaN) == 0);
        Check(Math.Abs(CreatureEscape.Probability(.3f,60) - .3f) < .00001f);
        double half = CreatureEscape.Probability(.3f,30);
        Check(Math.Abs(1 - (1-half)*(1-half) - .3) < .00001);
        double frame = CreatureEscape.Probability(.3f,1);
        Check(Math.Abs(1 - Math.Pow(1-frame,60) - .3) < .00001);
        Console.WriteLine("PASS: " + checks + " escape pressure and time probability checks.");
    }
}

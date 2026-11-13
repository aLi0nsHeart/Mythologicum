using System;
using Mythologicum;
using UnityEngine;

public static class CreatureScareChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool value) { checks++; if (!value) throw new Exception("Scare motion check " + checks); }
        bool Near(float a, float b) => Math.Abs(a - b) < .0001f;
        foreach (float progress in new[] { -1f, 0f, 1f, 2f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var pose = CreatureScareMotion.Sample(progress, 1.4f, .65f, .25f);
            Check(pose.Offset.sqrMagnitude < .000001f);
            Check((pose.Scale - Vector3.one).sqrMagnitude < .000001f);
            Check(Near(pose.Tilt, 0));
        }
        var anticipation = CreatureScareMotion.Sample(.1f, 1.4f, .65f, .25f);
        Check(anticipation.Offset.y < 0 && anticipation.Scale.y < 1 && anticipation.Scale.x > 1);
        var peak = CreatureScareMotion.Sample(.5f, 1.4f, .65f, .25f);
        Check(Near(peak.Offset.y, 1.4f) && Near(peak.Offset.z, .65f));
        Check(peak.Scale.y > 1 && peak.Tilt < 0);
        var landing = CreatureScareMotion.Sample(.9f, 1.4f, .65f, .25f);
        Check(landing.Offset.y < 0 && landing.Scale.y < 1);
        for (int i = 0; i <= 1000; i++)
        {
            var pose = CreatureScareMotion.Sample(i / 1000f, 1.4f, .65f, .25f);
            Check(pose.Offset.y >= -.1401f && pose.Offset.y <= 1.4001f);
            Check(pose.Offset.z >= 0 && pose.Offset.z <= .6501f && pose.Offset.x == 0);
            Check(pose.Scale.x > 0 && pose.Scale.y > 0 && pose.Scale.z > 0);
        }
        foreach (float invalid in new[] { -100f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var pose = CreatureScareMotion.Sample(.5f, invalid, invalid, invalid);
            Check(pose.Offset == Vector3.zero && !float.IsNaN(pose.Scale.y) && pose.Scale.y > 0);
        }
        Console.WriteLine("PASS: " + checks + " scare anticipation, jump, landing and numeric-safety checks.");
    }
}

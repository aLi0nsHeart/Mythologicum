using System;
using Mythologicum;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Run from a disposable native fixture; verifies real species data and editable clone isolation.</summary>
public static class CreatureAppealNativeChecks
{
    public static int RunChecks()
    {
        int checks = 0;
        void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
        CreatureDefinition Species(string id)
        {
            var species = Resources.Load<CreatureDefinition>("Creatures/" + id);
            Check(species != null, "Species asset resolves for visitor appeal: " + id);
            var appeal = species.VisitorAppeal;
            foreach (float value in new[] { appeal.thrill, appeal.wonder, appeal.adventure, appeal.children })
                Check(!float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1,
                    "All exhibit appeal values are valid artist-editable data: " + id);
            return species;
        }
        var goat = Species("Goat"); var chupacabra = Species("Chupacabra"); var hitotsume = Species("Hitotsume");
        var yurei = Species("Yurei"); var oni = Species("Oni"); var rabbit = Species("Wolpertinger");
        var unicorn = Species("Unicorn"); var waldschrat = Species("Waldschrat");
        Check(oni.VisitorAppeal.thrill > oni.VisitorAppeal.wonder
            && chupacabra.VisitorAppeal.thrill > chupacabra.VisitorAppeal.children,
            "Dangerous exhibits have their own thrill appeal without deriving it from combat values");
        Check(unicorn.VisitorAppeal.wonder > unicorn.VisitorAppeal.thrill
            && yurei.VisitorAppeal.wonder > yurei.VisitorAppeal.children,
            "Both peaceful magic and ghostly mystery appeal to Wonderlust visitors");
        Check(rabbit.VisitorAppeal.children > rabbit.VisitorAppeal.thrill
            && goat.VisitorAppeal.children > goat.VisitorAppeal.adventure
            && hitotsume.VisitorAppeal.children > hitotsume.VisitorAppeal.thrill,
            "Small, approachable and playful exhibits have distinct child interest");
        Check(waldschrat.VisitorAppeal.adventure > goat.VisitorAppeal.adventure,
            "A forest giant provides more adventure interest than a domestic animal");
        var clone = Object.Instantiate(unicorn);
        try
        {
            float originalAppeal = unicorn.VisitorAppeal.thrill;
            float threat = clone.threat, health = clone.maximumHealth, damage = clone.damage;
            clone.visitorAppeal.thrill = .83f; clone.visitorAppeal.children = .12f;
            Check(clone.VisitorAppeal.thrill == .83f && clone.VisitorAppeal.children == .12f,
                "A designer can tune audience interest without adding species-specific code");
            Check(unicorn.VisitorAppeal.thrill == originalAppeal,
                "Editing a runtime profile clone leaves the authored species appeal unchanged");
            Check(clone.threat == threat && clone.maximumHealth == health && clone.damage == damage,
                "Audience appeal never modifies creature danger, health or damage");
        }
        finally { Object.DestroyImmediate(clone); }
        return checks;
    }
}

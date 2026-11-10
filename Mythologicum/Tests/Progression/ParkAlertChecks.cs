using System;
using System.Collections.Generic;
using Mythologicum;
using UnityEngine;

public static class ParkAlertChecks
{
    private static int checks;
    private static void Check(bool condition, string message)
    { checks++; if (!condition) throw new Exception(message); }
    public static void Main()
    {
        var latch = new ParkAlertLatch();
        Check(!latch.Observe("feeder", true, 5, 30), "Brief empty stock is not yet reported");
        latch.Advance(4);
        Check(!latch.Observe("feeder", true, 5, 30), "A sustained problem waits for its configured delay");
        latch.Advance(1);
        Check(latch.Observe("feeder", true, 5, 30), "The completed hold period produces one report");
        latch.Advance(100);
        Check(!latch.Observe("feeder", true, 5, 30), "An unresolved condition never spams repeat reports");
        latch.Observe("feeder", false, 5, 30);
        Check(!latch.Observe("feeder", true, 5, 30), "A recurrence starts a fresh sustained-condition delay");
        latch.Advance(5);
        Check(latch.Observe("feeder", true, 5, 30), "A later recurrence reports after its delay");
        latch.Observe("feeder", false, 0, 30);
        latch.Advance(1);
        Check(!latch.Observe("feeder", true, 0, 30), "Separate occurrences also respect the recurrence cooldown");
        latch.Advance(29);
        Check(latch.Observe("feeder", true, 0, 30), "An active recurrence reports after the cooldown elapses");
        Check(!latch.Observe("loaded", true, 0, 0, true), "Loading silently establishes the existing state");
        latch.Advance(100);
        Check(!latch.Observe("loaded", true, 0, 0), "Existing loaded problems do not generate duplicate letters");
        latch.Observe("loaded", false, 0, 0);
        Check(latch.Observe("loaded", true, 0, 0), "A problem that resolves after loading can be reported again");
        double before = latch.Time;
        foreach (float dt in new[] { 0f, -1f, float.NaN, float.PositiveInfinity }) latch.Advance(dt);
        Check(latch.Time == before, "Pause and invalid time cannot advance alert timers");
        latch.Retain(new HashSet<string> { "loaded" });
        Check(!latch.IsActive("feeder") && latch.IsActive("loaded"), "Removed actors do not leave growing alert state behind");

        var history = new CreatureContainmentObservation();
        var inside = new Vector2Int(2, 2); var outside = new Vector2Int(3, 2);
        var area = new HashSet<Vector2Int> { inside };
        Check(!history.Observe(outside, null), "Placing a creature outside fences is not called an escape");
        Check(!history.Observe(inside, area), "Entering secured ground arms observation without assigning ownership");
        Check(!history.Observe(inside, null), "Opening the fence alone is not the same as the creature leaving its area");
        Check(history.Observe(outside, null), "Physically crossing outside the observed area is an escape");
        Check(!history.Observe(inside, area), "Returning to secured ground resolves the escape condition");
        Check(!history.Observe(outside, null, true), "Keeper transport does not create an escape warning");
        Check(!history.Observe(outside, null), "A transported creature placed on free ground remains unassigned");
        history.Observe(inside, area);
        area.Add(outside);
        Check(history.Observe(outside, null), "The recorded area is a snapshot rather than a mutable enclosure binding");

        var inbox = new NotificationInbox();
        inbox.Post("injury", ParkNoticeKind.VisitorInjured, "Injured", "Body", inside);
        Check(inbox.UnreadCount == 1 && inbox.Entries[0].severity == ParkNoticeSeverity.Warning,
            "Operational warnings enter the shared unread inbox with severity and location");
        inbox.MarkRead(inbox.Entries[0].id);
        Check(inbox.UnreadCount == 0, "Only reading a letter or marking read consumes its unread state");
        inbox.Post("death", ParkNoticeKind.VisitorDied, "Died", "Body");
        Check(inbox.Entries[1].severity == ParkNoticeSeverity.Critical, "Visitor deaths are critical notifications");
        var copy = inbox.Entries[1].Copy();
        Check(copy.severity == ParkNoticeSeverity.Critical && !ReferenceEquals(copy, inbox.Entries[1]),
            "Severity is retained by independent notification snapshots");
        Check((int)ParkNoticeKind.ResearchInterrupted == 4 && (int)ParkNoticeKind.CreatureEscaped == 5,
            "New event kinds append to the enum without changing saved historical event numbers");
        Console.WriteLine("PASS: " + checks + " park alert transition, containment and notification checks.");
    }
}

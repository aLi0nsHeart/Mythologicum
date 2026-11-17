using System;
using System.IO;
using Mythologicum;

public static class AttractionRideCycleChecks
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    private static void Close(float value, float expected, string reason) => Check(Math.Abs(value - expected) < .00001f, reason);
    private static void Reject(AttractionRideSave saved, string reason)
    {
        bool failed = false;
        try { AttractionRideCycle.ValidateSave(saved, 10, 5); } catch (InvalidDataException) { failed = true; }
        Check(failed, reason);
    }
    public static void Main()
    {
        var ride = new AttractionRideCycle(10, 5, 4);
        Check(ride.CanBoard && ride.Phase == AttractionRidePhase.Boarding, "A fresh ride starts docked and accepts its first group");
        Close(ride.Progress, 0, "A boarding ride is at its dock");
        Check(!ride.Tick(100, 0, 0, true), "An empty park cannot dispatch an empty balloon");
        Close(ride.Elapsed, 0, "The boarding window starts with a passenger reservation rather than world time");
        Check(!ride.Tick(2, 1, 1, true), "A partial group waits for additional arrivals");
        Close(ride.Elapsed, 2, "The partial-group boarding clock advances in simulation time");
        Check(!ride.Tick(100, 1, 2, false), "A guest physically boarding prevents departure even after the wait expires");
        Close(ride.Elapsed, 5, "An expired boarding window remains bounded while boarding finishes");
        Check(ride.Tick(.01f, 2, 2, true) && ride.Phase == AttractionRidePhase.Riding && !ride.CanBoard,
            "A ready partial group departs after its boarding window");
        Close(ride.Progress, 0, "The dispatched ride starts its route at the dock");
        Check(!ride.Tick(5, 2, 2, true), "An active ride stays airborne partway through its route");
        Close(ride.Progress, .5f, "All passengers share one authoritative halfway progress");
        var saved = ride.Capture(); var restored = new AttractionRideCycle(10, 5, 4); restored.Restore(saved);
        Check(restored.Phase == AttractionRidePhase.Riding && !restored.CanBoard, "Restoring an airborne group does not reopen boarding");
        Close(restored.Progress, .5f, "A save resumes the exact point of the flight");
        saved.elapsed = 0;
        Close(restored.Progress, .5f, "Saved state is copied rather than retained by reference");
        var rebalanced = new AttractionRideCycle(2, 1, 1); rebalanced.Restore(ride.Capture());
        Check(rebalanced.Capacity == 4 && rebalanced.RideSeconds == 10 && rebalanced.BoardingWindowSeconds == 5,
            "A saved group retains its original capacity and timing after a designer lowers the current defaults");
        Close(rebalanced.Progress, .5f, "Shortened balance settings do not teleport an existing flight to its destination");
        Check(!rebalanced.Tick(2, 2, 2, true) && rebalanced.Phase == AttractionRidePhase.Riding,
            "The restored group finishes the remaining original route rather than a shortened new route");
        rebalanced.Tick(3, 2, 2, true);
        Check(rebalanced.Phase == AttractionRidePhase.Unloading && rebalanced.Capacity == 4,
            "All saved passengers can unload using their original seat capacity");
        rebalanced.Tick(.1f, 0, 0, true);
        Check(rebalanced.CanBoard && rebalanced.Capacity == 1 && rebalanced.RideSeconds == 2 && rebalanced.BoardingWindowSeconds == 1,
            "The next group adopts current designer defaults only after the saved group fully exits");
        var savedLoading = new AttractionRideCycle(10, 5, 4); savedLoading.Tick(4, 1, 1, false);
        var loadingRebalanced = new AttractionRideCycle(2, 1, 1); loadingRebalanced.Restore(savedLoading.Capture());
        Check(!loadingRebalanced.Tick(.5f, 1, 1, true) && loadingRebalanced.Elapsed == 4.5f,
            "A loading group also retains its original reservation window and capacity");
        Check(restored.Tick(100, 2, 2, true) && restored.Phase == AttractionRidePhase.Unloading, "The flight lands before unloading starts");
        Check(!restored.CanBoard, "A landed ride rejects the next group until current passengers leave");
        Close(restored.Progress, 1, "The unloading pose is the completed, landed route");
        Check(!restored.Tick(100, 0, 1, true), "An exiting reservation holds the unloading phase even if no guest remains seated");
        Check(restored.Tick(.1f, 0, 0, true) && restored.CanBoard, "Only a fully cleared ride can take a new group");
        Close(restored.Progress, 0, "The next group begins at the same dock");
        var full = new AttractionRideCycle(10, 100, 2);
        Check(full.Tick(.1f, 2, 2, true), "A full, ready group departs without wasting the remaining wait");
        var instant = new AttractionRideCycle(10, 0, 4);
        Check(instant.Tick(.1f, 1, 1, true), "A designer can disable the extra boarding wait");
        var abandoned = new AttractionRideCycle(10, 5, 4); abandoned.Tick(4, 0, 1, false); abandoned.Tick(1, 0, 0, true);
        Close(abandoned.Elapsed, 0, "Cancelling every reservation clears a stale loading window");
        Check(!abandoned.Tick(1, 1, 1, true), "A later group receives its own complete boarding window");
        var emptyReturn = new AttractionRideCycle(10, 0, 1); emptyReturn.Tick(1, 1, 1, true);
        Check(emptyReturn.Tick(10, 0, 0, true) && emptyReturn.Phase == AttractionRidePhase.Unloading,
            "An unexpectedly empty airborne ride still returns to the ground before taking another group");
        foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var previous = full.Capture(); Check(!full.Tick(invalid, 2, 2, true), "Paused and invalid deltas cannot advance ride state");
            Check(full.Phase == previous.phase && full.Elapsed == previous.elapsed, "Rejected time steps preserve the exact cycle state");
        }
        var overflow = new AttractionRideCycle(10, 5, 4); overflow.Tick(float.MaxValue, 1, 1, true); overflow.Tick(float.MaxValue, 1, 1, true);
        Check(overflow.Phase == AttractionRidePhase.Unloading && !float.IsInfinity(overflow.Elapsed), "Large finite time steps cannot overflow timers");
        var tuning = new AttractionDefinition { entrySeconds = float.NaN, exitSeconds = -1, boardingWindowSeconds = float.PositiveInfinity,
            passengerSpacing = -3, balloonAscentFraction = 1, carouselTurns = 0 }; tuning.Validate();
        Check(tuning.entrySeconds >= .1f && tuning.exitSeconds >= .1f && tuning.boardingWindowSeconds == 0 && tuning.passengerSpacing == 0,
            "Passenger timing and spacing reject invalid balance inputs");
        Check(tuning.balloonAscentFraction == .49f && tuning.carouselTurns == 1, "Ride profiles preserve a cruise section and complete carousel turns");
        var copy = tuning.Copy(); Check(copy.entrySeconds == tuning.entrySeconds && copy.passengerSpacing == tuning.passengerSpacing,
            "Copied building settings preserve passenger tuning");
        AttractionRideCycle.ValidateSave(null, 10, 5);
        foreach (var phase in new[] { AttractionRidePhase.Boarding, AttractionRidePhase.Riding, AttractionRidePhase.Unloading })
        {
            var valid = new AttractionRideSave { phase = phase, elapsed = phase == AttractionRidePhase.Boarding ? 5 : phase == AttractionRidePhase.Riding ? 10 : 0 };
            AttractionRideCycle.ValidateSave(valid, 10, 5); checks++;
        }
        Reject(new AttractionRideSave { phase = (AttractionRidePhase)99 }, "Unknown saved phases are rejected");
        Reject(new AttractionRideSave { elapsed = float.NaN }, "NaN saved clocks are rejected");
        Reject(new AttractionRideSave { elapsed = float.PositiveInfinity }, "Infinite saved clocks are rejected");
        Reject(new AttractionRideSave { elapsed = -1 }, "Negative saved clocks are rejected");
        Reject(new AttractionRideSave { elapsed = 6 }, "Boarding clocks cannot exceed their loading window");
        Reject(new AttractionRideSave { phase = AttractionRidePhase.Riding, elapsed = 11 }, "Flight clocks cannot exceed the route duration");
        Reject(new AttractionRideSave { phase = AttractionRidePhase.Unloading, elapsed = 1 }, "Unloading saves use the landed endpoint without a second flight clock");
        Reject(new AttractionRideSave { rideSeconds = 10, boardingWindowSeconds = 5 }, "Partial snapshots require a valid saved capacity");
        Reject(new AttractionRideSave { rideSeconds = float.NaN, capacity = 2 }, "Saved ride duration must be finite");
        Reject(new AttractionRideSave { rideSeconds = 10, boardingWindowSeconds = float.PositiveInfinity, capacity = 2 }, "Saved loading windows must be finite");
        Reject(new AttractionRideSave { rideSeconds = 10, boardingWindowSeconds = -1, capacity = 2 }, "Saved loading windows cannot be negative");
        Reject(new AttractionRideSave { rideSeconds = .01f, capacity = 2 }, "Saved ride durations preserve the minimum supported simulation duration");
        Reject(new AttractionRideSave { rideSeconds = 10, capacity = -1 }, "Saved capacity must remain positive");
        Reject(new AttractionRideSave { phase = AttractionRidePhase.Riding, rideSeconds = 2, capacity = 2, elapsed = 3 },
            "Elapsed time remains bounded by its own saved flight duration");
        restored.Restore(null); Check(restored.CanBoard && restored.Elapsed == 0, "Missing older-save ride state safely starts a docked group");
        Console.WriteLine("PASS: " + checks + " attraction batch boarding, shared progress, landing, unloading, time and save checks.");
    }
}

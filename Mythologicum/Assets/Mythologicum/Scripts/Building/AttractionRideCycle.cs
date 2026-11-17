using System;
using System.IO;

namespace Mythologicum
{
    public enum AttractionRidePhase { Boarding, Riding, Unloading }

    /// <summary>Optional per-building batch state. Passenger identities and fees remain with visitor visits.</summary>
    [Serializable]
    public sealed class AttractionRideSave
    {
        public AttractionRidePhase phase;
        public float elapsed;
        // Optional snapshots protect an active group from later balance edits.
        // All three values are zero in earlier saves without a timing snapshot.
        public float rideSeconds, boardingWindowSeconds;
        public int capacity;
    }

    /// <summary>
    /// One simulation clock for a group ride. It cannot admit passengers in flight
    /// or start another group before the previous group has physically disembarked.
    /// </summary>
    public sealed class AttractionRideCycle
    {
        public AttractionRidePhase Phase { get; private set; }
        public float Elapsed { get; private set; }
        private readonly float configuredRideSeconds, configuredBoardingWindowSeconds;
        private readonly int configuredCapacity;
        public float RideSeconds { get; private set; }
        public float BoardingWindowSeconds { get; private set; }
        public int Capacity { get; private set; }
        public bool CanBoard => Phase == AttractionRidePhase.Boarding;
        public float Progress => Phase == AttractionRidePhase.Boarding ? 0f
            : Phase == AttractionRidePhase.Unloading ? 1f : Math.Min(1f, Elapsed / RideSeconds);

        public AttractionRideCycle(float rideSeconds, float boardingWindowSeconds, int capacity)
        {
            configuredRideSeconds = Math.Max(.1f, Amount(rideSeconds));
            configuredBoardingWindowSeconds = Amount(boardingWindowSeconds);
            configuredCapacity = Math.Max(1, capacity);
            UseConfiguredSettings();
        }

        public AttractionRideCycle(BuildingDefinition definition)
            : this(definition?.serviceSeconds ?? 0f, definition?.attraction?.boardingWindowSeconds ?? 0f,
                definition?.serviceCapacity ?? 1) { }

        /// <summary>
        /// Counts describe this batch only; distant visitors still approaching a
        /// running attraction belong to a later batch. Returns whether the phase changed.
        /// </summary>
        public bool Tick(float deltaTime, int boardedCount, int reservedCount, bool allBoardedReady)
        {
            if (!Finite(deltaTime) || deltaTime <= 0f) return false;
            boardedCount = Math.Max(0, boardedCount);
            reservedCount = Math.Max(boardedCount, reservedCount);
            AttractionRidePhase before = Phase;
            switch (Phase)
            {
                case AttractionRidePhase.Boarding:
                    if (reservedCount == 0) { Elapsed = 0f; UseConfiguredSettings(); }
                    else Elapsed = AddBounded(Elapsed, deltaTime, BoardingWindowSeconds);
                    if (boardedCount > 0 && allBoardedReady
                        && (boardedCount >= Capacity || Elapsed >= BoardingWindowSeconds))
                    { Phase = AttractionRidePhase.Riding; Elapsed = 0f; }
                    break;
                case AttractionRidePhase.Riding:
                    Elapsed = AddBounded(Elapsed, deltaTime, RideSeconds);
                    if (Elapsed >= RideSeconds) { Phase = AttractionRidePhase.Unloading; Elapsed = 0f; }
                    break;
                case AttractionRidePhase.Unloading:
                    if (reservedCount == 0) { Phase = AttractionRidePhase.Boarding; Elapsed = 0f; UseConfiguredSettings(); }
                    break;
            }
            return Phase != before;
        }

        public AttractionRideSave Capture() => new AttractionRideSave { phase = Phase, elapsed = Elapsed,
            rideSeconds = RideSeconds, boardingWindowSeconds = BoardingWindowSeconds, capacity = Capacity };

        public void Restore(AttractionRideSave saved)
        {
            ValidateSave(saved, configuredRideSeconds, configuredBoardingWindowSeconds);
            if (saved != null && HasSnapshot(saved))
            {
                RideSeconds = saved.rideSeconds; BoardingWindowSeconds = saved.boardingWindowSeconds; Capacity = saved.capacity;
            }
            else UseConfiguredSettings();
            Phase = saved?.phase ?? AttractionRidePhase.Boarding;
            Elapsed = saved?.elapsed ?? 0f;
        }

        public static void ValidateSave(AttractionRideSave saved, float rideSeconds, float boardingWindowSeconds)
        {
            if (saved == null) return;
            if (HasSnapshot(saved))
            {
                if (!Finite(saved.rideSeconds) || saved.rideSeconds < .1f
                    || !Finite(saved.boardingWindowSeconds) || saved.boardingWindowSeconds < 0f || saved.capacity < 1)
                    throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
                rideSeconds = saved.rideSeconds; boardingWindowSeconds = saved.boardingWindowSeconds;
            }
            float limit = saved.phase == AttractionRidePhase.Boarding ? Amount(boardingWindowSeconds)
                : saved.phase == AttractionRidePhase.Riding ? Math.Max(.1f, Amount(rideSeconds)) : 0f;
            if (!Enum.IsDefined(typeof(AttractionRidePhase), saved.phase) || !Finite(saved.elapsed)
                || saved.elapsed < 0f || saved.elapsed > limit)
                throw new InvalidDataException(Loc.Text("attraction.save.invalid"));
        }

        private static bool HasSnapshot(AttractionRideSave saved)
            => saved.rideSeconds != 0f || saved.boardingWindowSeconds != 0f || saved.capacity != 0;
        private void UseConfiguredSettings()
        { RideSeconds = configuredRideSeconds; BoardingWindowSeconds = configuredBoardingWindowSeconds; Capacity = configuredCapacity; }

        private static float AddBounded(float current, float deltaTime, float limit)
            => (float)Math.Min(limit, (double)current + deltaTime);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Amount(float value) => Finite(value) ? Math.Max(0f, value) : 0f;
    }
}

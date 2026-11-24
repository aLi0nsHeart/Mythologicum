using System;
using UnityEngine;

namespace Mythologicum
{
    /// <summary>Passenger poses and ride art driven by the authoritative visitor simulation.</summary>
    public sealed class AttractionVisualAnimator : MonoBehaviour
    {
        private BuildingDefinition definition;
        private Func<int> occupancy;
        private Transform rig;
        private Vector3 docked;
        private Quaternion dockedRotation;
        private float legacyProgress;
        private bool simulationControlled;
        private Transform entrance;
        private Transform[] passengerAnchors = Array.Empty<Transform>();
        private Transform[][] entryRoutes = Array.Empty<Transform[]>();
        private Vector3 passengerCentre;
        private Vector2 passengerArea;
        private float passengerYaw, passengerFloor;
        private bool hasPassengerLayout, passengerCircular, passengerOutward, passengerCarousel;
        public Transform RideAnchor { get; private set; }
        public Vector3 EntrancePosition => entrance != null ? entrance.position : transform.position;

        public void Initialize(BuildingDefinition definition)
        {
            this.definition = definition;
            rig = transform.Find("Moving rig");
            if (rig != null) { docked = rig.localPosition; dockedRotation = rig.localRotation; }
            RideAnchor = rig != null ? rig.Find("Ride anchor") : transform.Find("Ride anchor");
            entrance = transform.Find("Entrance anchor");
            Transform seats = rig != null ? rig.Find("Passenger anchors") : transform.Find("Passenger anchors");
            int count = seats != null ? seats.childCount : 0;
            passengerAnchors = new Transform[count]; entryRoutes = new Transform[count][];
            for (int slot = 0; slot < count; slot++)
            {
                passengerAnchors[slot] = seats.GetChild(slot);
                var route = transform.Find("Entry route " + slot);
                entryRoutes[slot] = new Transform[route != null ? route.childCount : 0];
                for (int point = 0; point < entryRoutes[slot].Length; point++) entryRoutes[slot][point] = route.GetChild(point);
            }
        }
        public void Bind(Func<int> count) => occupancy = count;
        public void BindSimulation() => simulationControlled = true;

        internal void ConfigurePassengerLayout(Vector3 centre, Vector2 area, float yaw, bool circular, bool outward,
            bool carousel = false, float floor = 0)
        {
            passengerCentre = centre; passengerArea = area; passengerYaw = yaw;
            passengerCircular = circular; passengerOutward = outward; passengerCarousel = carousel;
            passengerFloor = floor; hasPassengerLayout = true;
        }

        private bool UsesCapacityOverride(int capacity) => hasPassengerLayout && capacity > 0 && capacity != passengerAnchors.Length;
        private Transform PassengerFrame => rig != null ? rig : transform;

        public Vector3 PassengerPosition(int slot, int capacity, float bodyOffset)
        {
            if (UsesCapacityOverride(capacity))
            {
                CapacityPose(slot, capacity, out var point, out _);
                return PassengerFrame.TransformPoint(point) + Vector3.up * Mathf.Max(0, bodyOffset);
            }
            Transform seat = PassengerAnchor(slot);
            return (seat != null ? seat.position : RideAnchor != null ? RideAnchor.position : transform.position)
                + Vector3.up * Mathf.Max(0, bodyOffset);
        }

        public Quaternion PassengerRotation(int slot, int capacity)
        {
            if (UsesCapacityOverride(capacity))
            {
                CapacityPose(slot, capacity, out _, out float yaw);
                return PassengerFrame.rotation * Quaternion.Euler(0, yaw, 0);
            }
            Transform seat = PassengerAnchor(slot);
            return seat != null ? seat.rotation : RideAnchor != null ? RideAnchor.rotation : transform.rotation;
        }

        private Transform PassengerAnchor(int slot)
            => passengerAnchors.Length == 0 ? null : passengerAnchors[Math.Max(0, slot) % passengerAnchors.Length];

        // An in-flight group's saved capacity can outlive a later balance edit.
        // Refit those existing people to the present visual without rebuilding it.
        private void CapacityPose(int slot, int capacity, out Vector3 point, out float yaw)
        {
            capacity = Math.Max(1, capacity); slot = Math.Max(0, slot) % capacity;
            if (passengerCircular)
            {
                float angle = (passengerCarousel ? 0 : Mathf.PI) + slot * Mathf.PI * 2 / capacity;
                var offset = new Vector3(Mathf.Sin(angle) * passengerArea.x * .5f, 0, Mathf.Cos(angle) * passengerArea.y * .5f);
                point = passengerCentre + offset;
                yaw = passengerCarousel ? angle * Mathf.Rad2Deg + 90 : Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg;
                if (passengerCarousel)
                {
                    bool hasMount = false;
                    foreach (var anchor in passengerAnchors)
                    {
                        var mount = anchor.localPosition;
                        if (new Vector2(mount.x - point.x, mount.z - point.z).sqrMagnitude < .0001f) { hasMount = true; break; }
                    }
                    if (!hasMount) point.y = passengerFloor;
                }
                return;
            }
            int columns = Math.Max(1, Mathf.CeilToInt(Mathf.Sqrt(capacity * Mathf.Max(.1f, passengerArea.x) / Mathf.Max(.1f, passengerArea.y))));
            columns = Math.Min(columns, capacity);
            int rows = Math.Max(1, Mathf.CeilToInt(capacity / (float)columns));
            float spacing = Mathf.Max(.1f, definition.attraction.passengerSpacing);
            float dx = Mathf.Min(spacing, passengerArea.x / Math.Max(1, columns - 1));
            float dz = Mathf.Min(spacing, passengerArea.y / Math.Max(1, rows - 1));
            var gridOffset = new Vector3((slot % columns - (columns - 1) * .5f) * dx, 0,
                (slot / columns - (rows - 1) * .5f) * dz);
            point = passengerCentre + gridOffset;
            yaw = passengerOutward && gridOffset.sqrMagnitude > .001f ? Mathf.Atan2(gridOffset.x, gridOffset.z) * Mathf.Rad2Deg : passengerYaw;
        }

        /// <summary>Walks the authored entry route without allocating per frame. Reverse progress to leave.</summary>
        public Vector3 EntryPosition(float progress, int slot, int capacity, float bodyOffset)
        {
            progress = float.IsNaN(progress) ? 0 : Mathf.Clamp01(progress);
            var start = EntrancePosition + Vector3.up * Mathf.Max(0, bodyOffset);
            var end = PassengerPosition(slot, capacity, bodyOffset);
            if (progress <= 0) return start;
            if (progress >= 1) return end;
            var points = entryRoutes.Length == 0 ? null : entryRoutes[Math.Max(0, slot) % entryRoutes.Length];
            bool overrideRoute = UsesCapacityOverride(capacity) && passengerCircular;
            int count = points != null ? points.Length : 0;
            Vector3 first = default, second = default;
            if (overrideRoute)
            {
                CapacityPose(slot, capacity, out var target, out _);
                float side = target.x < 0 ? -1 : 1;
                if (passengerCarousel)
                {
                    first = new Vector3(side * passengerArea.x * (.38f / .58f), .48f, -passengerArea.y * (.35f / .58f));
                    second = new Vector3(first.x, first.y, passengerArea.y * (.32f / .58f));
                    count = target.z > 0 ? 2 : 1;
                }
                else
                {
                    first = new Vector3(side * passengerArea.x * .525f, passengerCentre.y, -passengerArea.y * .5f);
                    second = new Vector3(first.x, first.y, target.z);
                    count = target.z > -passengerArea.y * .3125f ? 2 : 0;
                }
                first = PassengerFrame.TransformPoint(first); second = PassengerFrame.TransformPoint(second);
            }
            if (count == 0) return Vector3.Lerp(start, end, progress);
            float length = 0;
            var previous = start;
            for (int index = 0; index <= count; index++)
            {
                var next = index == count ? end : (overrideRoute ? index == 0 ? first : second : points[index].position) + Vector3.up * Mathf.Max(0, bodyOffset);
                length += Vector3.Distance(previous, next); previous = next;
            }
            float distance = length * progress; previous = start;
            for (int index = 0; index <= count; index++)
            {
                var next = index == count ? end : (overrideRoute ? index == 0 ? first : second : points[index].position) + Vector3.up * Mathf.Max(0, bodyOffset);
                float segment = Vector3.Distance(previous, next);
                if (distance <= segment) return Vector3.Lerp(previous, next, segment > .0001f ? distance / segment : 1);
                distance -= segment; previous = next;
            }
            return end;
        }

        public void SetRideProgress(float progress, bool active)
        {
            simulationControlled = true;
            ApplyRidePose(progress, active);
        }

        private void Update() => Advance(Time.deltaTime);
        internal void Advance(float deltaTime)
        {
            if (simulationControlled || definition?.attraction == null || rig == null || deltaTime <= 0 || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            bool occupied = (occupancy?.Invoke() ?? 0) > 0;
            float duration = Mathf.Max(1, definition.serviceSeconds);
            legacyProgress = occupied ? Mathf.Repeat(legacyProgress + deltaTime / duration, 1) : 0;
            ApplyRidePose(legacyProgress, occupied);
        }

        private void ApplyRidePose(float progress, bool active)
        {
            if (definition?.attraction == null || rig == null || float.IsNaN(progress) || float.IsInfinity(progress)) return;
            progress = Mathf.Clamp01(progress);
            if (!active || progress <= 0 || progress >= 1)
            { rig.localPosition = docked; rig.localRotation = dockedRotation; return; }
            if (definition.attraction.kind == AttractionKind.BalloonRide)
            {
                float height = Mathf.Max(0, definition.attraction.rideHeight);
                float fraction = Mathf.Clamp(definition.attraction.balloonAscentFraction, .01f, .49f);
                float ascent = Mathf.SmoothStep(0, 1, Mathf.Clamp01(progress / fraction));
                float descent = Mathf.SmoothStep(0, 1, Mathf.Clamp01((1 - progress) / fraction));
                float lift = Mathf.Min(ascent, descent);
                float cruise = Mathf.Clamp01((progress - fraction) / (1 - fraction * 2));
                float angle = cruise * 360f, radians = angle * Mathf.Deg2Rad;
                float radius = Mathf.Max(0, definition.attraction.rideRadius) * lift;
                rig.localPosition = docked + new Vector3(Mathf.Sin(radians) * radius, height * lift, (Mathf.Cos(radians) - 1f) * radius);
                rig.localRotation = dockedRotation * Quaternion.Euler(0, angle, 0);
            }
            else if (definition.attraction.kind == AttractionKind.ClockworkCarousel)
                rig.localRotation = dockedRotation * Quaternion.Euler(0, progress * 360f * Math.Max(1, definition.attraction.carouselTurns), 0);
        }
    }
}

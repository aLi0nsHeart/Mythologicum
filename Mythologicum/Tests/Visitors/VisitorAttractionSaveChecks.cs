using System;
using System.Collections.Generic;
using System.IO;
using Mythologicum;
using UnityEngine;

public static class VisitorAttractionSaveChecks
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2, 20, 20);
        var fences = new FenceLayout(grid);
        var network = new ParkBuildingNetwork(grid, fences);
        network.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.ParkEntrance, width = 1, depth = 1 }, new Vector2Int(1, 1), 0));
        var carouselDefinition = AttractionDefinition.Building(BuildingKind.ClockworkCarousel); carouselDefinition.width = carouselDefinition.depth = 1;
        var carousel = network.TryPlace(new BuildingFootprint(carouselDefinition, new Vector2Int(4, 4), 0));
        var petting = network.TryPlace(new BuildingFootprint(AttractionDefinition.Building(BuildingKind.PettingZoo), new Vector2Int(9, 3), 0));
        network.TryPlace(new BuildingFootprint(new BuildingDefinition { kind = BuildingKind.Kiosk, width = 1, depth = 1 }, new Vector2Int(13, 3), 0));
        var mountedFootprint = new BuildingFootprint(AttractionDefinition.Building(BuildingKind.PettingZoo), new Vector2Int(15, 8), 0, true);
        fences.TryAdd(mountedFootprint.RearEdge, FenceKind.Heavy);
        var mounted = network.TryPlace(mountedFootprint);
        var roomFootprint = new BuildingFootprint(AttractionDefinition.Building(BuildingKind.ViewingWindow), new Vector2Int(15, 14), 0, true);
        foreach (var edge in roomFootprint.AttachmentEdges()) fences.TryAdd(edge, FenceKind.Heavy);
        var room = network.TryPlace(roomFootprint);
        void Valid(VisitorAttractionSave data) { VisitorAttractionSaveRules.Validate(data, network.Records, grid, fences); checks++; }
        void Invalid(VisitorAttractionSave data)
        {
            bool rejected = false;
            try { VisitorAttractionSaveRules.Validate(data, network.Records, grid, fences); }
            catch (InvalidDataException) { rejected = true; }
            Check(rejected, "Corrupt attraction state must be rejected");
        }
        VisitorAttractionSave Visit() => new VisitorAttractionSave { building = 1, phase = VisitorAttractionPhase.Visiting,
            access = carousel.Footprint.AccessCell, remaining = 4, quotedPrice = 7, interest = .8f };
        VisitorAttractionSave Interior(VisitorAttractionPhase phase = VisitorAttractionPhase.Visiting) => new VisitorAttractionSave
        {
            building = 1, phase = phase, hasInterior = true, insideInterior = phase != VisitorAttractionPhase.Approaching,
            access = carousel.Footprint.AccessCell, interior = carousel.Footprint.DoorCell, remaining = 4, quotedPrice = 7,
            interest = .8f, seat = 0, seatCapacity = 4, hasQuote = phase != VisitorAttractionPhase.Approaching,
            transitionOrigin = grid.CellCenter(carousel.Footprint.DoorCell) + Vector3.up
        };
        VisitorAttractionSave Petting(VisitorAttractionPhase phase = VisitorAttractionPhase.Visiting) => new VisitorAttractionSave
        {
            building = 2, phase = phase, hasPettingPortal = true, insidePetting = phase != VisitorAttractionPhase.Approaching
                && phase != VisitorAttractionPhase.EnteringPetting,
            access = petting.Footprint.AccessCell, interior = petting.Footprint.RearAccessCell, remaining = 5, quotedPrice = 6, interest = 1
        };
        VisitorAttractionSave Mounted(VisitorAttractionPhase phase = VisitorAttractionPhase.Visiting)
        {
            var data = Petting(phase); data.building = 4;
            data.access = mounted.Footprint.AccessCell; data.interior = mounted.Footprint.RearAccessCell;
            return data;
        }
        VisitorAttractionSave Room(VisitorAttractionPhase phase = VisitorAttractionPhase.Visiting) => new VisitorAttractionSave
        {
            building = 5, phase = phase, hasViewingRoom = true,
            insideViewingRoom = phase == VisitorAttractionPhase.Visiting || phase == VisitorAttractionPhase.ExitingViewingRoom,
            access = room.Footprint.AccessCell, interior = room.Footprint.RearAccessCell, remaining = 5, quotedPrice = 6, interest = 1
        };
        Valid(null); Valid(new VisitorAttractionSave()); Valid(Visit());
        foreach (VisitorAttractionPhase phase in Enum.GetValues(typeof(VisitorAttractionPhase)))
            if (phase != VisitorAttractionPhase.None && phase != VisitorAttractionPhase.EnteringViewingRoom
                && phase != VisitorAttractionPhase.ExitingViewingRoom && phase != VisitorAttractionPhase.EnteringAttraction
                && phase != VisitorAttractionPhase.WaitingForRide && phase != VisitorAttractionPhase.LeavingAttraction)
                { Valid(Petting(phase)); Valid(Mounted(phase)); }
        foreach (var phase in new[] { VisitorAttractionPhase.Approaching, VisitorAttractionPhase.EnteringAttraction,
            VisitorAttractionPhase.WaitingForRide, VisitorAttractionPhase.Visiting, VisitorAttractionPhase.LeavingAttraction }) Valid(Interior(phase));
        var paidInterior = Interior(VisitorAttractionPhase.LeavingAttraction); paidInterior.completed = true; Valid(paidInterior);
        paidInterior.building = -1; paidInterior.interrupted = paidInterior.orphanExit = true; Valid(paidInterior);
        var badInterior = Interior(); badInterior.seat = -1; Invalid(badInterior);
        badInterior = Interior(); badInterior.seat = badInterior.seatCapacity; Invalid(badInterior);
        badInterior = Interior(); badInterior.seatCapacity = 0; Invalid(badInterior);
        badInterior = Interior(); badInterior.seatCapacity = 10001; Invalid(badInterior);
        badInterior = Interior(); badInterior.interior += Vector2Int.right; Invalid(badInterior);
        badInterior = Interior(); badInterior.access += Vector2Int.right; Invalid(badInterior);
        badInterior = Interior(); badInterior.insideInterior = false; Invalid(badInterior);
        badInterior = Interior(); badInterior.hasPettingPortal = true; Invalid(badInterior);
        badInterior = Interior(); badInterior.hasViewingRoom = true; Invalid(badInterior);
        badInterior = Interior(); badInterior.hasWindow = true; Invalid(badInterior);
        badInterior = Interior(); badInterior.completed = true; Invalid(badInterior);
        badInterior = Interior(); badInterior.building = 2; Invalid(badInterior);
        badInterior = Interior(); badInterior.phase = VisitorAttractionPhase.EnteringPetting; Invalid(badInterior);
        badInterior = Interior(); badInterior.transitionOrigin = new Vector3(float.NaN, 0, 0); Invalid(badInterior);
        foreach (var phase in new[] { VisitorAttractionPhase.Approaching, VisitorAttractionPhase.EnteringViewingRoom,
            VisitorAttractionPhase.Visiting, VisitorAttractionPhase.ExitingViewingRoom }) Valid(Room(phase));
        var departedRoom = Room(VisitorAttractionPhase.ExitingViewingRoom); departedRoom.completed = true; Valid(departedRoom);
        departedRoom.building = -1; departedRoom.interrupted = true; Valid(departedRoom);
        var badRoom = Room(); badRoom.access += Vector2Int.right; Invalid(badRoom);
        badRoom = Room(); badRoom.interior += Vector2Int.right; Invalid(badRoom);
        badRoom = Room(); badRoom.building = 2; Invalid(badRoom);
        badRoom = Room(); badRoom.hasViewingRoom = false; Invalid(badRoom);
        badRoom = Room(); badRoom.insideViewingRoom = false; Invalid(badRoom);
        badRoom = Room(); badRoom.hasPettingPortal = true; Invalid(badRoom);
        badRoom = Room(); badRoom.hasWindow = true; Invalid(badRoom);
        badRoom = Room(); badRoom.completed = true; Invalid(badRoom);
        badRoom = Room(); badRoom.phase = VisitorAttractionPhase.EnteringPetting; Invalid(badRoom);
        badRoom = Room(VisitorAttractionPhase.EnteringViewingRoom); badRoom.insideViewingRoom = true; Invalid(badRoom);
        badRoom = Room(VisitorAttractionPhase.Approaching); badRoom.insideViewingRoom = true; Invalid(badRoom);
        badRoom = Visit(); badRoom.insideViewingRoom = true; Invalid(badRoom);
        var gone = Petting(VisitorAttractionPhase.ReturningPetting); gone.building = -1; gone.interrupted = true; Valid(gone);
        gone.completed = true; Valid(gone);
        gone = Mounted(VisitorAttractionPhase.ReturningPetting); gone.building = -1; gone.interrupted = true; Valid(gone);
        gone.completed = true; Valid(gone);
        Check(ConstructionRouteGraph.CellDistance(Mounted().access, Mounted().interior) == 1,
            "Mounted gateway saves retain their adjacent public and enclosure access cells");
        var shifted = Petting(); shifted.access = petting.Footprint.Anchor; Invalid(shifted);
        shifted = Mounted(); shifted.access += Vector2Int.down; Invalid(shifted);
        shifted = Mounted(VisitorAttractionPhase.ReturningPetting); shifted.building = -1;
        shifted.access += Vector2Int.down * 2; Invalid(shifted);
        foreach (float bad in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            var data = Visit(); data.remaining = bad; Invalid(data);
            data = Visit(); data.quotedPrice = bad; Invalid(data);
            data = Visit(); data.interest = bad; Invalid(data);
            data = Visit(); data.choiceCooldown = bad; Invalid(data);
            data = Visit(); data.history.Add(new VisitorAttractionMemory { id = "b:15:4:4:0", remaining = bad }); Invalid(data);
        }
        foreach (string bad in new[] { null, "", " ", "bad\nkey", new string('a', 129) })
        { var data = Visit(); data.history.Add(new VisitorAttractionMemory { id = bad, remaining = 2 }); Invalid(data); }
        var duplicate = Visit(); duplicate.history.Add(new VisitorAttractionMemory { id = "same", remaining = 1 });
        duplicate.history.Add(new VisitorAttractionMemory { id = "same", remaining = 2 }); Invalid(duplicate);
        var state = Visit(); state.history.Add(null); Invalid(state);
        state = Visit(); state.building = 99; Invalid(state);
        state = Visit(); state.building = -2; Invalid(state);
        state = Visit(); state.building = 3; Invalid(state);
        state = Visit(); state.interest = 1.01f; Invalid(state);
        state = Visit(); state.phase = (VisitorAttractionPhase)999; Invalid(state);
        state = Visit(); state.access = new Vector2Int(-1, 4); Invalid(state);
        state = Visit(); state.completed = true; Invalid(state);
        state = Visit(); state.insidePetting = true; Invalid(state);
        state = Visit(); state.phase = VisitorAttractionPhase.ReturningPetting; Invalid(state);
        state = new VisitorAttractionSave { building = 1 }; Invalid(state);
        state = Petting(); state.interior = state.access; Invalid(state);
        state = Petting(); state.access += Vector2Int.right; state.interior += Vector2Int.right; Invalid(state);
        state = Petting(); state.insidePetting = false; Invalid(state);
        state = Petting(VisitorAttractionPhase.EnteringPetting); state.insidePetting = true; Invalid(state);
        state = Petting(VisitorAttractionPhase.Approaching); state.insidePetting = true; Invalid(state);
        state = Petting(); state.building = 1; Invalid(state);
        state = Visit(); state.hasWindow = true; state.windowCell = new Vector2Int(6, 6); Invalid(state);
        state = Visit(); state.building = -1; Invalid(state);
        var history = new VisitorAttractionSave { choiceCooldown = 12 };
        history.history.Add(new VisitorAttractionMemory { id = "b:15:4:4:0", remaining = 60 });
        history.history.Add(new VisitorAttractionMemory { id = "w:6:6:1", remaining = 20 }); Valid(history);
        Check(history.history[0].remaining == 60 && history.choiceCooldown == 12, "Validation leaves relative cooldowns and quoted state intact");
        Console.WriteLine("PASS: " + checks + " attraction save phase, portal, quote and cooldown validation checks.");
    }
}

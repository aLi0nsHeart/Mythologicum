using System;
using System.Linq;
using Mythologicum;
using UnityEngine;

public static class FenceWindowChecks
{
    private static int checks;
    private static void Check(bool value, string reason) { if (!value) throw new Exception(reason); checks++; }
    public static void Main()
    {
        foreach (FenceKind kind in Enum.GetValues(typeof(FenceKind)))
        {
            var grid = new SquareBuildGrid(Vector3.zero, 2, 7, 7);
            var fences = new FenceLayout(grid); var edge = new FenceEdge(new Vector2Int(3, 3), true);
            Check(!fences.TrySetWindow(edge, 5), "A window requires an existing fence");
            Check(fences.TryAdd(edge, kind), "The underlying fence can be installed");
            float health = fences.HealthAt(edge); bool climb = fences.CanClimb(edge), phase = fences.CanPhase(edge),
                damage = fences.CanBreak(edge), burrow = fences.CanBurrow(edge);
            int opened = 0; fences.Opened += _ => opened++;
            int revision = fences.Revision;
            Check(fences.TrySetWindow(edge, 5), "A window upgrades each available fence kind");
            Check(fences.Revision == revision + 1 && opened == 0, "Window installation invalidates derived visuals without opening containment");
            var record = fences.WindowAt(edge);
            Check(fences.HasWindow(edge) && fences.Windows.Single().Equals(edge) && record.Edge.Equals(edge), "A window is recorded by its exact fence edge");
            Check(!fences.IsGate(edge) && fences.BlocksPath(edge), "A viewing window is a closed barrier, not an entry gate");
            Check(fences.KindAt(edge) == kind && fences.HealthAt(edge) == health && fences.MaximumHealthAt(edge) == health,
                "Installing glass preserves the fence's type and health");
            Check(fences.CanClimb(edge) == climb && fences.CanPhase(edge) == phase && fences.CanBreak(edge) == damage
                && fences.CanBurrow(edge) == burrow, "All creature resistance and traversal rules stay with the underlying fence");
            Check(!new CreatureNavigation(grid, fences).CanCross(edge.CellA, edge.CellB)
                && !new VisitorNavigation(grid, fences).CanCross(edge.CellA, edge.CellB, true), "Neither animals nor frightened visitors can cross glass");
            Check(!fences.TrySetGate(edge) && !fences.TrySetWindow(edge, 9) && ReferenceEquals(record, fences.WindowAt(edge)),
                "A duplicate upgrade or gate cannot replace live window accounts");
            Check(record.SetPrice(7) && record.Price == 7, "The player can change a window ticket price");
            foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                Check(!record.SetPrice(invalid) && record.Price == 7, "Invalid ticket values cannot poison window accounts");
            record.RecordRevenue(5); record.RecordRevenue(7); record.RecordExpense(3);
            record.RecordRevenue(float.NaN); record.RecordExpense(-5);
            Check(record.Revenue == 12 && record.Expenses == 3 && record.Profit == 9, "Window revenue and running costs remain independent");
            record.RestoreAccounts(4, 20, 6);
            Check(record.Price == 4 && record.Revenue == 20 && record.Expenses == 6, "Saved window prices and accounts can be restored");
            Check(fences.TryRemove(edge) && !fences.HasWindow(edge) && fences.WindowAt(edge) == null && !fences.Windows.Any() && opened == 1,
                "Removing or breaking a fence removes its attached window and emits one opening event");
            Check(fences.TryAdd(edge, kind) && fences.TrySetGate(edge) && !fences.TrySetWindow(edge, 5), "Staff gates cannot be converted to viewing windows");
            var other = new FenceEdge(new Vector2Int(4, 4), false);
            Check(fences.TryAdd(other, kind) && !fences.TrySetWindow(other, -1) && !fences.TrySetWindow(other, float.NaN), "Invalid installation fees cannot create a malformed window");
        }
        var live = new FenceWindowRecord(new FenceEdge(new Vector2Int(1, 1), true), 1);
        live.RecordRevenue(float.MaxValue); live.RecordRevenue(float.MaxValue);
        live.RecordExpense(float.MaxValue); live.RecordExpense(float.MaxValue);
        Check(live.Revenue == float.MaxValue && live.Expenses == float.MaxValue && live.Profit == 0, "Large valid accounts remain finite");
        Console.WriteLine("PASS: " + checks + " viewing window accounting, containment, upgrade and lifetime checks.");
    }
}

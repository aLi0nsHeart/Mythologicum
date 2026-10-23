using System;
using Mythologicum;
using UnityEngine;

public static class GrazingFieldChecks
{
    static int checks;
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    public static void Main()
    {
        var grid = new SquareBuildGrid(Vector3.zero, 2f, 6, 6);
        var grass = new GrazingField(grid, 20f);
        var grazed = new Vector2Int(1, 1);
        Check(grass.HasFood(grazed, 0f), "New natural ground feeds herbivores");
        Check(grass.TryConsume(grazed, 2f), "Available patch consumed");
        Check(!grass.HasFood(grazed, 2f), "Consumed patch depleted immediately");
        Check(!grass.TryConsume(grazed, 3f), "Depleted patch cannot feed twice");
        Check(!grass.HasFood(grazed, 21.999f), "Regrowth waits full interval");
        Check(grass.HasFood(grazed, 22f), "Food returns at regrowth boundary");
        Check(grass.TryConsume(grazed, 22f), "Regrown food can be consumed");
        Check(!grass.HasFood(grazed, 41f), "Second meal resets regrowth");
        Check(grass.HasFood(new Vector2Int(2, 2), 3f), "Independent cells retain food");
        Check(!grass.HasFood(new Vector2Int(-1, 0), 50f), "Outside negative map rejected");
        Check(!grass.TryConsume(new Vector2Int(6, 0), 50f), "Outside positive map cannot feed");
        var path = new Vector2Int(3, 3);
        var building = new Vector2Int(4, 4);
        grid.TryOccupy(path, BuildCellContent.Path);
        grid.TryOccupy(building, BuildCellContent.Building);
        Check(!grass.HasFood(path, 0f), "Paths have no grass");
        Check(!grass.TryConsume(path, 0f), "Paths cannot be grazed");
        Check(!grass.HasFood(building, 0f), "Buildings have no grass");
        Check(!grass.TryConsume(building, 0f), "Buildings cannot be grazed");
        grid.TryRemove(path, BuildCellContent.Path);
        Check(grass.HasFood(path, 0f), "Demolished path returns to natural ground");
        grid.TryOccupy(grazed, BuildCellContent.Path);
        grid.TryRemove(grazed, BuildCellContent.Path);
        Check(!grass.HasFood(grazed, 30f), "Building and removing path does not reset depleted patch");
        Check(grass.HasFood(grazed, 42f), "Temporarily covered ground still regrows on schedule");
        Console.WriteLine("PASS: " + checks + " grass depletion, regrowth and ground exclusion checks.");
    }
}

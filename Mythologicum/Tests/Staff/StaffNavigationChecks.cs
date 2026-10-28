using System;
using Mythologicum;
using UnityEngine;

public static class StaffNavigationChecks
{
    public static void Main()
    {
        int checks = 0;
        void Check(bool condition,string message) { checks++; if (!condition) throw new Exception(message); }
        Vector2Int V(int x,int y) => new Vector2Int(x,y);
        var grid = new SquareBuildGrid(Vector3.zero,2,6,4);
        var fences = new FenceLayout(grid);
        var nav = new StaffNavigation(grid,fences);
        Check(nav.FindPath(V(0,1),V(5,1)).Count == 6,"Staff may travel on clear ground for work.");
        Check(nav.FindPath(V(0,1),V(5,1),true).Count == 0,"Path patrols cannot use bare ground.");
        for (int x=0;x<6;x++) grid.TryOccupy(V(x,1),BuildCellContent.Path);
        Check(nav.FindPath(V(0,1),V(5,1),true).Count == 6,"Patrol uses connected public paths.");
        foreach (FenceKind kind in new[] { FenceKind.Wooden,FenceKind.Spiked,FenceKind.Heavy,FenceKind.Sanctified })
        {
            for (int y=0;y<4;y++) fences.TryAdd(new FenceEdge(V(3,y),false),kind);
            Check(nav.FindPath(V(0,1),V(5,1)).Count == 0,"Solid barriers block ground work routes.");
            Check(nav.FindPath(V(0,1),V(5,1),true).Count == 0,"Solid barriers also block patrol routes.");
            Check(nav.HasLineOfSight(V(2,1),V(3,1)),"Fence allows observation without allowing passage.");
            Check(!nav.HasClearShot(V(2,1),V(3,1)),"Shots cannot cross a fence even when the animal is visible.");
            for (int y=0;y<4;y++) fences.TryRemove(new FenceEdge(V(3,y),false));
        }
        for (int y=0;y<4;y++) fences.TryAdd(new FenceEdge(V(3,y),false),FenceKind.Heavy);
        Check(fences.TrySetGate(new FenceEdge(V(3,1),false)),"Protected staff gate can be installed.");
        Check(nav.CanCross(V(2,1),V(3,1)) && nav.CanCross(V(3,1),V(2,1)),"Authorized gate permits staff in both directions.");
        Check(!nav.HasClearShot(V(2,1),V(3,1)),"An authorized gate is a passage, not permission to fire through it.");
        Check(nav.HasClearShot(V(3,1),V(4,1)),"A mercenary inside the enclosure has a clear shot on the same side.");
        Check(nav.FindPath(V(0,1),V(5,1),true).Count == 6,"A protected gate supports path patrols.");
        grid.TryRemove(V(2,1),BuildCellContent.Path);
        Check(nav.FindPath(V(0,1),V(5,1),true).Count == 0,"Removing public path stops patrol routing.");
        Check(nav.FindPath(V(0,1),V(5,1)).Count == 6,"Workers can still approach a job over clear ground.");
        grid.TryRemove(V(1,1),BuildCellContent.Path);
        grid.TryOccupy(V(1,1),BuildCellContent.Building);
        Check(!nav.IsWalkable(V(1,1)) && !nav.HasLineOfSight(V(0,1),V(3,1)),"Buildings prevent movement and observation through them.");
        Check(!nav.HasClearShot(V(0,1),V(3,1)),"Buildings also block weapon fire.");
        Check(nav.FindPath(V(0,1),V(5,1)).Count > 6,"Staff must detour around buildings.");
        var blocked = new StaffNavigation(grid,fences,cell => cell != V(3,1));
        Check(blocked.FindPath(V(0,1),V(5,1)).Count == 0,"Static clearance can close the only authorized gate.");
        Check(!nav.CanCross(V(0,0),V(1,1)) && !nav.CanCross(V(0,0),V(-1,0)),"Routes never move diagonally or off the map.");
        Check(nav.FindPath(V(0,0),V(0,0)).Count == 1,"A job already at the employee's cell requires no movement.");
        Check(nav.Reachable(V(0,1),true).Count < nav.Reachable(V(0,1)).Count,"Patrol reachability remains narrower than work travel.");
        Console.WriteLine("PASS: " + checks + " employee navigation checks.");
    }
}

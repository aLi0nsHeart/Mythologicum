using System;
using System.Collections.Generic;
using System.IO;
using Mythologicum;
using UnityEngine;
public static class TerrainChecks
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static bool Near(float a,float b)=>Math.Abs(a-b)<.002f;
    static Vector2Int V(int x,int z)=>new Vector2Int(x,z);
    public static void Main()
    {
        Check(Near(SquareTileHeightSampler.Bilinear(0,2,4,6,.5f,.5f),3),"Imported four-corner interpolation samples centre");
        Check(Near(SquareTileHeightSampler.Bilinear(0,2,4,6,.25f,.75f),3.5f),"Imported sampler preserves both axes");
        var field=new GridHeightField(12,12,2);var heights=new float[13*13];
        for(int z=0;z<=12;z++)for(int x=0;x<=12;x++)heights[z*13+x]=x*.2f+z*.1f;field.ImportHeights(heights);
        var grid=new SquareBuildGrid(new Vector3(-10,3,-10),2,12,12,field);var fences=new FenceLayout(grid);
        Check(Near(grid.CellCenter(V(4,4)).y,4.35f),"Grid centre uses world origin plus terrain height");
        Check(Near(grid.GroundHeight(new Vector3(-1.5f,0,-1)),4.3f),"World sampling works between cell centres");
        Check(Near(field.Sample(0,0),0)&&Near(field.Sample(12,12),3.6f),"Heightfield includes its boundary nodes");
        Check(Near(field.Sample(-2,-3),0),"Sampling outside map clamps safely");
        Check(field.Walkable(V(4,4))&&field.Walkable(V(4,4),true),"Gentle slopes support actors and paths");
        var creature=new CreatureNavigation(grid,fences);var visitor=new VisitorNavigation(grid,fences);var staff=new StaffNavigation(grid,fences);
        Check(creature.FindPath(V(1,1),V(6,1)).Count==6&&staff.FindPath(V(1,1),V(6,1)).Count==6,"Ground A* still works on hills");
        for(int x=1;x<=6;x++)grid.TryOccupy(V(x,1),BuildCellContent.Path);
        Check(visitor.FindPath(V(1,1),V(6,1)).Count==6,"Guest routing follows elevated paths");
        Check(grid.MovementCost(V(1,1),V(2,1))>1&&grid.MovementCost(V(1,1),V(2,1))>grid.MovementCost(V(2,1),V(1,1)),"Uphill movement has an expandable extra traversal cost");
        var shape=new BuildingFootprint(new BuildingDefinition{kind=BuildingKind.Kiosk,width=2,depth=2},V(4,4),0);
        Check(field.TryPlanFoundation(shape,grid,out var plan),"Plan modest foundation earthworks");
        float before=field.Node(4,4);int revision=grid.Revision;
        Check(Near(field.Node(4,4),before),"Preview planning never mutates terrain");
        field.Apply(plan);Check(grid.Revision>revision,"Height changes invalidate navigation and placement revisions");
        foreach(var cell in shape.Cells())
            Check(Near(grid.CellCenter(cell).y,grid.Origin.y+plan.Level),"Every occupied building tile is level");
        Check(Near(grid.CellCenter(shape.AccessCell).y,grid.Origin.y+plan.Level),"Dedicated entrance has the same foundation level");
        var owner=new object();Check(field.RegisterFoundation(owner,shape),"Reserve level foundation vertices");
        var nearby=new BuildingFootprint(new BuildingDefinition{width=1,depth=1},V(3,4),0);
        if(field.TryPlanFoundation(nearby,grid,out var adjacent))
        {Check(Near(adjacent.Level,plan.Level),"Touching foundation respects already built level");field.Apply(adjacent);}
        Check(Near(grid.CellCenter(V(4,4)).y,grid.Origin.y+plan.Level),"Nearby placement never moves an existing building");
        field.ReleaseFoundation(owner);Check(field.TryPlanFoundation(shape,grid,out _),"Demolition releases foundation reservations");
        var save=field.Capture();var copy=new GridHeightField(12,12,2);copy.Restore(save);
        Check(Near(copy.Sample(4.3f,5.7f),field.Sample(4.3f,5.7f)),"Flattened terrain survives save restoration exactly");
        save.heights[0]+=1;Check(!Near(copy.Node(0,0),save.heights[0]),"Terrain snapshots do not alias live data");
        bool rejected=false;try{copy.ImportHeights(new float[3]);}catch(InvalidDataException){rejected=true;}Check(rejected,"Wrong height count rejected");
        var bad=copy.Capture();bad.heights[0]=float.NaN;rejected=false;try{copy.Restore(bad);}catch(InvalidDataException){rejected=true;}Check(rejected,"Invalid terrain heights rejected");
        var generatedA=new GridHeightField(12,12,2);var generatedB=new GridHeightField(12,12,2);
        generatedA.Generate(2471,7,28);generatedB.Generate(2471,7,28);
        Check(Near(generatedA.Sample(3.25f,7.3f),generatedB.Sample(3.25f,7.3f)),"Seeded terrain generation is reproducible");
        Check(Math.Abs(generatedA.Node(0,0)-generatedA.Node(12,12))>.01f,"Generated landscape has actual relief");
        var steep=new GridHeightField(8,5,2);var steepHeights=new float[9*6];
        for(int z=0;z<=5;z++)for(int x=0;x<=8;x++)steepHeights[z*9+x]=x>=4?8:0;steep.ImportHeights(steepHeights);
        var steepGrid=new SquareBuildGrid(Vector3.zero,2,8,5,steep);var steepFences=new FenceLayout(steepGrid);
        Check(!steep.Walkable(V(3,2))&&!steep.Walkable(V(3,2),true),"Cliffs block both path construction and walking");
        Check(new CreatureNavigation(steepGrid,steepFences).FindPath(V(1,2),V(6,2)).Count==0,"Creatures cannot walk through a cliff");
        Check(new StaffNavigation(steepGrid,steepFences).FindPath(V(1,2),V(6,2)).Count==0,"Staff share cliff restrictions");
        for(int x=0;x<8;x++)steepGrid.TryOccupy(V(x,2),BuildCellContent.Path);
        Check(new VisitorNavigation(steepGrid,steepFences).FindPath(V(1,2),V(6,2)).Count==0,"Guest connectivity cannot cross an over-steep path");
        var cut=new BuildingFootprint(new BuildingDefinition{width=2,depth=2},V(3,2),0);
        Check(!steep.TryPlanFoundation(cut,steepGrid,out _),"Large earthwork or broken neighboring path rejects building safely");
        var weighted=ConstructionRouteFinder.Find(0,3,node=>node==0?new[]{1,2}:node==1||node==2?new[]{3}:Array.Empty<int>(),node=>true,(a,b)=>0,(a,b)=>a==0&&b==1?8f:1f);
        Check(weighted.Count==3&&weighted[1]==2,"Shared A* chooses cheaper weighted route");
        var blocked=ConstructionRouteFinder.Find(0,1,node=>new[]{1},node=>true,(a,b)=>0,(a,b)=>float.NaN);
        Check(blocked.Count==0,"Invalid future movement costs are not traversed");
        var ridge=new GridHeightField(12,4,2);var ridgeHeights=new float[13*5];
        for(int z=0;z<5;z++)for(int x=0;x<13;x++)ridgeHeights[z*13+x]=Math.Max(0,3-Math.Abs(x-6)*.5f);
        ridge.ImportHeights(ridgeHeights);var ridgeGrid=new SquareBuildGrid(Vector3.zero,2,12,4,ridge);var ridgeFences=new FenceLayout(ridgeGrid);
        Check(new CreatureNavigation(ridgeGrid,ridgeFences).FindPath(V(0,2),V(11,2)).Count>0,"Gentle ridge remains traversable");
        Check(!new StaffNavigation(ridgeGrid,ridgeFences).HasClearShot(V(0,2),V(11,2)),"Terrain ridge blocks shooting through a hill");
        Console.WriteLine("PASS: "+checks+" terrain sampling, foundations, slopes, weighted navigation and save checks.");
    }
}

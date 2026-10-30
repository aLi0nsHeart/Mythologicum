using System;
using System.Linq;
using Mythologicum;
using UnityEngine;
public static class TerrainBrushChecks
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static bool Near(float a,float b)=>Math.Abs(a-b)<.0001f;
    static Vector2Int V(int x,int z)=>new Vector2Int(x,z);
    static void Same(float[] a,float[] b,string why)=>Check(a.SequenceEqual(b),why);
    static SquareBuildGrid Grid(GridHeightField field)=>new SquareBuildGrid(new Vector3(-20,5,-20),field.CellSize,field.Width,field.Depth,field);
    public static void Main()
    {
        Check(Near(TerrainBrushRules.Falloff(0,3),1)&&Near(TerrainBrushRules.Falloff(3,3),0),"Circular brush is full strength at its centre and zero at its boundary");
        Check(TerrainBrushRules.Falloff(1,3)>TerrainBrushRules.Falloff(2,3),"Brush influence decreases smoothly with distance");
        Check(TerrainBrushRules.Falloff(4,3)==0&&TerrainBrushRules.Falloff(1,float.NaN)==0,"Invalid and outside falloff samples are harmless");
        var field=new GridHeightField(12,12,2);var grid=Grid(field);int events=0;field.Changed+=()=>events++;
        int revision=field.Revision,gridRevision=grid.Revision;
        Check(field.TryEdit(grid,new Vector2(6,6),2.5f,.25f,TerrainBrushMode.Raise,0,out var reason)&&reason=="","Raise applies a valid brush");
        Check(Near(field.Node(6,6),.25f),"Centre height rises by the supplied metre amount");
        Check(field.Node(7,6)>0&&field.Node(7,6)<field.Node(6,6),"Falloff feathers the brush edge");
        Check(field.Node(9,6)==0&&field.Node(0,0)==0,"Raise does not modify nodes outside the brush");
        Check(events==1&&field.Revision==revision+1&&grid.Revision==gridRevision+1,"One accepted edit emits one change and navigation invalidation");
        Check(field.TryEdit(grid,new Vector2(6,6),2.5f,.25f,TerrainBrushMode.Lower,0,out reason),"Lower accepts the reverse operation");
        Check(Near(field.Node(6,6),0)&&events==2,"Lower reverses the earlier centre change");
        Check(field.TryEdit(grid,Vector2.zero,2,.2f,TerrainBrushMode.Lower,0,out reason),"Brush clips safely at the map boundary");
        Check(Near(field.Node(0,0),-.2f)&&field.Node(12,12)==0,"Boundary edits support negative relative elevations without wrapping");
        var flat=new GridHeightField(12,12,2);var flatGrid=Grid(flat);var flatHeights=Enumerable.Repeat(2f,169).ToArray();flat.ImportHeights(flatHeights);
        revision=flat.Revision;events=0;flat.Changed+=()=>events++;
        Check(!flat.TryEdit(flatGrid,new Vector2(6,6),3,1,TerrainBrushMode.Flatten,2,out reason)&&reason=="","Flatten on an already level surface is a no-op");
        Check(!flat.TryEdit(flatGrid,new Vector2(6,6),3,1,TerrainBrushMode.Smooth,0,out reason)&&reason=="","Smoothing a flat field is a no-op");
        Check(!flat.TryEdit(flatGrid,new Vector2(6,6),3,0,TerrainBrushMode.Raise,0,out reason),"Zero-strength edit is a no-op");
        Check(flat.Revision==revision&&events==0,"No-op brushes do not rebuild meshes or invalidate navigation");
        Check(flat.TryEdit(flatGrid,new Vector2(6,6),3,.1f,TerrainBrushMode.Flatten,1.5f,out reason),"Flatten moves toward the captured target");
        Check(Near(flat.Node(6,6),1.9f),"Flatten changes are bounded by brush strength");
        var smooth=new GridHeightField(12,12,2);var smoothGrid=Grid(smooth);var smoothHeights=new float[169];smoothHeights[6+6*13]=.8f;smooth.ImportHeights(smoothHeights);
        Check(smooth.TryEdit(smoothGrid,new Vector2(6,6),3,.2f,TerrainBrushMode.Smooth,0,out reason),"Smooth accepts a modest peak");
        Check(Near(smooth.Node(6,6),.6f)&&smooth.Node(7,6)>0,"Smoothing lowers a peak and raises the surrounding depression");
        var pathField=new GridHeightField(12,12,2);var pathGrid=Grid(pathField);pathGrid.TryOccupy(V(6,6),BuildCellContent.Path);var unchanged=pathField.Capture().heights;revision=pathField.Revision;
        Check(!pathField.TryEdit(pathGrid,new Vector2(6,6),1.1f,1.2f,TerrainBrushMode.Raise,0,out reason)&&reason.Contains("path"),"Brush cannot make a built path steeper than its limit");
        Same(pathField.Capture().heights,unchanged,"Rejected path edit leaves every node unchanged");
        Check(pathField.Revision==revision,"Rejected edit does not rebuild terrain");
        pathGrid.TryRemove(V(6,6),BuildCellContent.Path);
        Check(pathField.TryEdit(pathGrid,new Vector2(6,6),1.1f,1.2f,TerrainBrushMode.Raise,0,out reason),"The same moderate slope is allowed without a path");
        var cliff=new GridHeightField(12,12,2);var cliffGrid=Grid(cliff);unchanged=cliff.Capture().heights;
        Check(!cliff.TryEdit(cliffGrid,new Vector2(6,6),1.1f,5,TerrainBrushMode.Raise,0,out reason)&&reason.Contains("walkable"),"Brush cannot turn currently walkable ground into a cliff");
        Same(cliff.Capture().heights,unchanged,"Excessive slope rejection is atomic");
        var steepHeights=new float[169];for(int z=0;z<=12;z++)for(int x=0;x<=12;x++)steepHeights[z*13+x]=x>=6?8:0;
        cliff.ImportHeights(steepHeights);float oldSlope=cliff.MaximumSlope(V(5,5));
        Check(cliff.TryEdit(cliffGrid,new Vector2(5.5f,6),3,.5f,TerrainBrushMode.Smooth,0,out reason),"Already steep ground can be repaired gradually");
        Check(cliff.MaximumSlope(V(5,5))<oldSlope,"Repairing steep ground reduces its slope");
        var locked=new GridHeightField(12,12,2);var lockedGrid=Grid(locked);var footprint=new BuildingFootprint(new BuildingDefinition{width=2,depth=2},V(5,5),0);var owner=new object();
        Check(locked.RegisterFoundation(owner,footprint),"Fixture registers a level building foundation");
        Check(locked.BrushTouchesFoundation(new Vector2(6,6),2)&&!locked.BrushTouchesFoundation(new Vector2(1,1),1),"Brush cursor detects positive influence on foundations");
        unchanged=locked.Capture().heights;
        Check(!locked.TryEdit(lockedGrid,new Vector2(6,6),2,.1f,TerrainBrushMode.Raise,0,out reason)&&reason.Contains("foundations"),"Registered foundation intersects reject the whole brush");
        Same(locked.Capture().heights,unchanged,"A foundation rejection does not partially edit nearby nodes");
        Check(!locked.TryEdit(lockedGrid,new Vector2(6,4),.75f,.1f,TerrainBrushMode.Lower,0,out reason),"Dedicated entrance foundation is protected too");
        Check(locked.TryEdit(lockedGrid,new Vector2(2,2),1.5f,.1f,TerrainBrushMode.Raise,0,out reason),"Unrelated terrain remains editable");
        Check(!locked.TryEdit(lockedGrid,new Vector2(6,6),2,.1f,TerrainBrushMode.Raise,0,out reason),"Successful edits preserve existing foundation reservations");
        locked.ReleaseFoundation(owner);
        Check(!locked.BrushTouchesFoundation(new Vector2(6,6),2),"Foundation cursor feedback clears after demolition");
        Check(locked.TryEdit(lockedGrid,new Vector2(6,6),2,.1f,TerrainBrushMode.Raise,0,out reason),"Demolition-released foundations can be edited again");
        var logical=new GridHeightField(12,12,2);var logicalGrid=Grid(logical);logicalGrid.TryOccupy(V(6,6),BuildCellContent.Building);
        Check(!logical.TryEdit(logicalGrid,new Vector2(6,6),2,.1f,TerrainBrushMode.Lower,0,out reason)&&reason.Contains("building"),"Logical building occupancy is also protected");
        revision=logical.Revision;
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1f})
            Check(!logical.TryEdit(logicalGrid,new Vector2(2,2),invalid,1,TerrainBrushMode.Raise,0,out reason)&&reason!="","Invalid radius is rejected");
        Check(!logical.TryEdit(logicalGrid,new Vector2(-1,2),2,.1f,TerrainBrushMode.Raise,0,out reason),"Brush centre outside map is rejected");
        Check(!logical.TryEdit(logicalGrid,new Vector2(2,2),2,float.PositiveInfinity,TerrainBrushMode.Raise,0,out reason),"Invalid strength is rejected");
        Check(!logical.TryEdit(logicalGrid,new Vector2(2,2),2,float.MaxValue,TerrainBrushMode.Raise,0,out reason),"Extreme strength cannot corrupt saved heights");
        Check(!logical.TryEdit(logicalGrid,new Vector2(2,2),2,1,(TerrainBrushMode)100,0,out reason),"Unknown terrain tool is rejected");
        Check(logical.Revision==revision,"Invalid requests leave revision unchanged");
        foreach(int size in new[]{32,50,72})
        {
            var sized=new GridHeightField(size,size,2);var sizedGrid=Grid(sized);
            Check(sized.TryEdit(sizedGrid,new Vector2(size-1,size-1),3,.1f,TerrainBrushMode.Raise,0,out reason),"Brush supports each new map size");
            var restored=new GridHeightField(size,size,2);restored.Restore(sized.Capture());
            Same(sized.Capture().heights,restored.Capture().heights,"Edited corner heights survive existing terrain save format");
        }
        Console.WriteLine("PASS: "+checks+" terrain brush falloff, editing, foundations, path protection and save checks.");
    }
}

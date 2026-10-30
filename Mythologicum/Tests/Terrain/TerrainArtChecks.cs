using System;
using System.Collections.Generic;
using System.Linq;
using Mythologicum;
using UnityEngine;
public static class TerrainArtChecks
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    public static void Main()
    {
        foreach(var axis in new[]{Vector3.up,Vector3.right,Vector3.forward,new Vector3(1,1,0),new Vector3(1,2,3)})
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();var a=new Vector3(2,3,-1);var b=a+axis*3;
            GroundMeshBuilder.Beam(vertices,triangles,a,b,.2f,.3f);var centre=(a+b)*.5f;
            Check(vertices.Count==24&&triangles.Count==36,"Beam is a closed six-sided mesh");
            for(int i=0;i<triangles.Count;i+=3)
            {
                var x=vertices[triangles[i]];var y=vertices[triangles[i+1]];var z=vertices[triangles[i+2]];
                var normal=Vector3.Cross(y-x,z-x);
                Check(Vector3.Dot(normal,(x+y+z)/3-centre)>0,"Fence beam triangle faces outside for every orientation");
            }
        }
        var band=ForestBorderLayout.Cells(10,12,2,1,123);
        Check(band.Count==72,"Forest band covers exactly the requested border at full density");
        Check(band.All(c=>c.x<2||c.x>=8||c.y<2||c.y>=10),"Border forest leaves the park centre clear");
        Check(ForestBorderLayout.Cells(10,12,2,0,123).Count==0,"Forest density zero disables scattering");
        Check(ForestBorderLayout.Cells(10,12,0,1,123).Count==0,"Forest width zero leaves the map empty");
        Check(ForestBorderLayout.Cells(2,2,8,1,123).Count==0,"Tiny maps retain central playable space");
        var first=ForestBorderLayout.Cells(50,50,3,.88f,819);var second=ForestBorderLayout.Cells(50,50,3,.88f,819);
        Check(first.SequenceEqual(second)&&first.Count>400,"Forest scatter is deterministic and sufficiently dense");
        Check(!first.SequenceEqual(ForestBorderLayout.Cells(50,50,3,.88f,820)),"Different seeds change the forest pattern");
        var grid=new SquareBuildGrid(Vector3.zero,2,4,4);var fences=new FenceLayout(grid);var edge=new FenceEdge(new Vector2Int(2,1),false);
        fences.TryAdd(edge,FenceKind.Spiked);fences.TryDamage(edge,30);
        Check(fences.TrySetGate(edge)&&fences.IsGate(edge)&&fences.KindAt(edge)==FenceKind.Spiked,"Spiked fence directly accepts a protected gate");
        Check(fences.HealthAt(edge)==190&&!fences.CanClimb(edge),"Spiked gate retains damage and blocks climbing");
        Check(new StaffNavigation(grid,fences).CanCross(edge.CellA,edge.CellB),"Authorized staff can cross a spiked gate");
        Check(!new CreatureNavigation(grid,fences,null,true).CanCross(edge.CellA,edge.CellB),"Climbers cannot exploit a spiked gate");
        Console.WriteLine("PASS: "+checks+" fence winding, forest border and spiked-gate checks.");
    }
}

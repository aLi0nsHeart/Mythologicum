using System;
using System.Linq;
using Mythologicum;
using UnityEngine;
public static class NatureGenerationChecks
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    static int Edge(Vector2Int cell,int width,int depth)=>Math.Min(Math.Min(cell.x,width-1-cell.x),Math.Min(cell.y,depth-1-cell.y));
    public static void Main()
    {
        const int seed=2471^819;
        var first=NatureGeneration.Plan(50,50,3,.88f,seed);var same=NatureGeneration.Plan(50,50,3,.88f,seed);
        Check(first.Trees.SequenceEqual(same.Trees)&&first.Rocks.SequenceEqual(same.Rocks),"Nature layout is deterministic for map seed and settings");
        Check(first.Trees.Count>250&&first.Trees.Count<1000,"Border remains substantially forested without consuming the entire map");
        Check(first.Trees.Distinct().Count()==first.Trees.Count&&first.Rocks.Distinct().Count()==first.Rocks.Count,"Natural objects never duplicate a cell");
        Check(!first.Trees.Intersect(first.Rocks).Any(),"Rocks and trees use separate non-overlapping locations");
        Check(first.Trees.Concat(first.Rocks).All(c=>c.x>=0&&c.y>=0&&c.x<50&&c.y<50),"Generated nature stays inside map bounds");
        int interior=first.Trees.Count(c=>Edge(c,50,50)>=6);
        Check(interior>0&&interior<150,"Interior has sparse woodland pockets and ample clear space");
        Check(first.Trees.Any(c=>Edge(c,50,50)>=3)&&first.Trees.Any(c=>Edge(c,50,50)==0),"Forest edges vary in depth rather than forming a rigid rectangle");
        var fullBand=ForestBorderLayout.Cells(50,50,3,1,seed);
        Check(fullBand.Any(c=>!first.Trees.Contains(c)),"The forest contains natural gaps and clearings");
        Check(first.Rocks.Count>0&&first.Rocks.Count<100,"Rock outcrops are sparse");
        var different=NatureGeneration.Plan(50,50,3,.88f,seed+1);
        Check(!first.Trees.SequenceEqual(different.Trees)&&!first.Rocks.SequenceEqual(different.Rocks),"A different seed changes both trees and rocks");
        Check(NatureGeneration.Plan(50,50,3,0,seed).Trees.Count==0,"Zero forest density removes trees");
        Check(NatureGeneration.Plan(50,50,3,.2f,seed).Trees.Count<first.Trees.Count,"Lower forest density yields fewer trees");
        Check(NatureGeneration.Plan(50,50,0,.88f,seed).Trees.Count==0,"Disabling forest width removes forest generation");
        Check(NatureGeneration.Plan(2,2,8,.88f,seed).Trees.Count==0,"Tiny maps retain central playable space");
        int soil=0,grass=0;bool repeatable=true;
        for(int z=0;z<50;z++)for(int x=0;x<50;x++)
        {
            bool dirt=NatureGeneration.GroundIsSoil(x+.5f,z+.5f,2471);
            if(dirt)soil++;else grass++;
            repeatable&=dirt==NatureGeneration.GroundIsSoil(x+.5f,z+.5f,2471);
        }
        Check(repeatable,"Ground patches are repeatable");
        Check(soil>50&&grass>500,"Map includes broad soil patches and predominantly grassy ground");
        bool changed=false;for(int z=0;z<50;z++)for(int x=0;x<50;x++)if(NatureGeneration.GroundIsSoil(x,z,1)!=NatureGeneration.GroundIsSoil(x,z,2))changed=true;
        Check(changed,"Ground cover patterns vary with the map seed");
        Console.WriteLine("PASS: "+checks+" deterministic forest, clearing, rock and ground-cover checks. Default layout: "+first.Trees.Count+" trees ("+interior+" interior), "+first.Rocks.Count+" rocks; "+soil+" soil cells.");
    }
}

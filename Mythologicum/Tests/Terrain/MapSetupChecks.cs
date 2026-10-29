using System;
using Mythologicum;
public static class MapSetupChecks
{
    static int checks;
    static void Check(bool value,string why){checks++;if(!value)throw new Exception(why);}
    public static void Main()
    {
        var options=new MapGenerationOptions();
        Check(options.Validate(out _)&&options.width==50&&options.depth==50&&options.seed==2471,"Defaults preserve standard map identity");
        Check(options.relief==7&&options.featureSize==28&&options.forestDensity==.88f,"Defaults preserve terrain and forest tuning");
        var clone=options.Clone();clone.width=clone.depth=32;clone.seed=-24;clone.relief=0;
        Check(options.width==50&&options.seed==2471&&options.relief==7,"Editing cloned setup leaves previous choices independent");
        var flat=clone.CreateHeightField();Check(flat.Seed==-24&&flat.Sample(5.2f,7.3f)==0,"Flat maps retain seed for forest and soil identity");
        foreach(int size in new[]{32,50,72})
        {
            var selected=new MapGenerationOptions{width=size,depth=size,seed=int.MinValue};
            Check(selected.Validate(out _),"Supported map size accepts signed seed");
            var field=selected.CreateHeightField();Check(field.Width==size&&field.Depth==size&&field.CellSize==2,"Generation uses selected size and shared 2 metre cells");
        }
        var a=options.CreateHeightField();var b=options.Clone().CreateHeightField();
        Check(a.Sample(4.3f,9.2f)==b.Sample(4.3f,9.2f),"Preview and actual map share deterministic height generation");
        var wrong=options.Clone();wrong.width=48;Check(!wrong.Validate(out _),"Unsupported sizes are rejected before scene loading");
        wrong=options.Clone();wrong.depth=32;Check(!wrong.Validate(out _),"Setup uses square maps");
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1,21})
        {wrong=options.Clone();wrong.relief=invalid;Check(!wrong.Validate(out _),"Invalid hill strength rejected");}
        foreach(float invalid in new[]{float.NaN,float.PositiveInfinity,-1,2})
        {wrong=options.Clone();wrong.forestDensity=invalid;Check(!wrong.Validate(out _),"Invalid forest density rejected");}
        wrong=options.Clone();wrong.featureSize=0;Check(!wrong.Validate(out _),"Invalid hill width rejected");
        wrong=options.Clone();wrong.seed=int.MaxValue;Check(wrong.Validate(out _)&&wrong.CreateHeightField().Seed==int.MaxValue,"Largest signed seed stays reproducible");
        Console.WriteLine("PASS: "+checks+" map setup validation, cloning and deterministic generation checks.");
    }
}

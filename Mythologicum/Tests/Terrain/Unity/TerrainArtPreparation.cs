using System;
using System.Collections.Generic;
using Mythologicum;
using UnityEditor;
using UnityEngine;
public static class TerrainArtPreparation
{
    const string Root="Assets/Mythologicum/";
    public static void Prepare()
    {
        var lit=Shader.Find("Universal Render Pipeline/Lit");if(lit==null)throw new Exception("URP Lit shader not available");
        Material Convert(string name,string texture,bool cutout,Vector2 scale,Vector2 offset)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"Materials/Terrain/SquareTileTerrain/"+name+".mat");
            var source=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/Terrain/SquareTileTerrain/"+texture+".png");
            if(material==null||source==null)throw new Exception("Missing terrain art: "+name);
            material.shader=lit;material.SetTexture("_BaseMap",source);material.SetColor("_BaseColor",Color.white);
            material.SetTextureScale("_BaseMap",scale);material.SetTextureOffset("_BaseMap",offset);material.SetFloat("_Smoothness",0);
            material.SetFloat("_Surface",0);material.SetFloat("_ZWrite",1);material.SetFloat("_Cull",cutout?0:2);
            material.SetFloat("_AlphaClip",cutout?1:0);material.SetFloat("_Cutoff",.4f);
            material.shaderKeywords=Array.Empty<string>();
            if(cutout)material.EnableKeyword("_ALPHATEST_ON");
            material.SetOverrideTag("RenderType",cutout?"TransparentCutout":"Opaque");material.renderQueue=cutout?2450:2000;
            material.doubleSidedGI=cutout;material.enableInstancing=true;
            EditorUtility.SetDirty(material);return material;
        }
        var grass=Convert("GroundGrass","GroundGrass",false,new Vector2(.25f,.25f),Vector2.zero);
        // The atlas contains unused black and magenta cells. Sample only a cliff tile.
        var cliff=Convert("TerrainCliff","TerrainAtlas",false,new Vector2(1f/6,1f/6),new Vector2(1f/6,4f/6));
        var treeMaterial=Convert("TreeMat","TreeTex",true,Vector2.one,Vector2.zero);
        var tree=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"Prefabs/Environment/SquareTileTerrain/TreePrefab.prefab");if(tree==null)throw new Exception("Missing imported tree prefab");
        foreach(var renderer in tree.GetComponentsInChildren<Renderer>(true))
            foreach(var material in renderer.sharedMaterials)if(material!=treeMaterial)throw new Exception("Tree prefab material reference was not preserved");
        var vegetation=AssetDatabase.LoadAssetAtPath<VegetationDefinition>(Root+"Resources/Environment/Vegetation.asset");
        var choices=new List<GameObject>(vegetation.trees);if(!choices.Contains(tree))choices.Add(tree);vegetation.trees=choices.ToArray();EditorUtility.SetDirty(vegetation);
        string artPath=Root+"Resources/Terrain/TerrainArt.asset";
        var art=AssetDatabase.LoadAssetAtPath<TerrainArtDefinition>(artPath);
        if(art==null){art=ScriptableObject.CreateInstance<TerrainArtDefinition>();AssetDatabase.CreateAsset(art,artPath);}
        art.grass=grass;art.cliff=cliff;art.borderTrees=new[]{tree};EditorUtility.SetDirty(art);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();Debug.Log("TERRAIN ART PREPARED: URP grass/cliff materials, cutout pine, vegetation catalogue and forest settings.");
    }
    public static void PrepareAndExit(){Prepare();EditorApplication.Exit(0);}
}

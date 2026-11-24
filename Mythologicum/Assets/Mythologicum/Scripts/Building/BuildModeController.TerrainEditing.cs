using UnityEngine;
using UnityEngine.InputSystem;
namespace Mythologicum
{
    public sealed partial class BuildModeController
    {
        private TerrainEditingPresentation terrainEditing;
        private LineRenderer terrainCursor;
        private Material terrainCursorMaterial;
        private bool terrainStroke;
        private float terrainBudget,flattenTarget,waterStrokeLevel;
        private string terrainMessage="";
        private void CreateTerrainEditingUI(Canvas canvas)
        {
            var settings=Resources.Load<TerrainDefinition>("Terrain/DefaultTerrain");
            terrainEditing=new TerrainEditingPresentation(canvas,SelectTerrainBrush,()=>SelectTool(Tool.None),settings!=null?settings.brush:null);
            var root=new GameObject("Terrain brush outline",typeof(LineRenderer));terrainCursor=root.GetComponent<LineRenderer>();
            terrainCursorMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")??gridMaterial.shader);
            terrainCursor.sharedMaterial=terrainCursorMaterial;terrainCursor.useWorldSpace=true;terrainCursor.loop=true;terrainCursor.widthMultiplier=.06f;
            terrainCursor.positionCount=64;terrainCursor.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;root.SetActive(false);
        }
        private void SelectTerrainBrush(TerrainBrushMode mode)
        {SelectTool(Tool.Terraform);terrainEditing.SetMode(mode);ResetTerrainStroke();}
        private void ResetTerrainStroke(){if(terrainStroke)CommitConstructionAction();terrainStroke=false;terrainBudget=0;terrainMessage="";}
        private void ApplyTerrainBrush(Mouse mouse,Vector3 point)
        {
            var center=new Vector2((point.x-grid.Origin.x)/grid.CellSize,(point.z-grid.Origin.z)/grid.CellSize);
            float radius=terrainEditing.Radius;
            bool locked=(int)terrainEditing.Mode<5 && grid.Terrain.BrushTouchesFoundation(center,radius);
            terrainCursor.gameObject.SetActive(true);
            for(int i=0;i<64;i++)
            {float angle=i*Mathf.PI*2/64;var p=point+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius*grid.CellSize;
                var c=grid.WorldToCell(p);p.y=Mathf.Max(grid.GroundHeight(p),grid.Habitat.IsWater(c)?grid.Habitat.WaterLevel(c):float.NegativeInfinity)+.14f;terrainCursor.SetPosition(i,p);}
            terrainCursorMaterial.color=locked?new Color(.95f,.18f,.1f):new Color(.15f,.9f,.95f);
            status.text=locked?Loc.Text("park.build_mode_controller__terrain_editing.this_brush_touches_a_building_foundation_move_it_or_reduce"):terrainMessage!=""?terrainMessage
                :Loc.Format("park.build_mode_controller__terrain_editing.ground_hold_left_mouse_radius_and_rate_below_buildings_and", Loc.Enum(terrainEditing.Mode));
            if(!mouse.leftButton.isPressed){ResetTerrainStroke();return;}
            if(mouse.leftButton.wasPressedThisFrame)
            {BeginConstructionAction("park.build_mode_controller__terrain_editing.edit_terrain");terrainStroke=true;flattenTarget=grid.GroundHeight(point)-grid.Origin.y;
                var clicked=grid.WorldToCell(point);waterStrokeLevel=grid.Habitat.IsWater(clicked)?grid.Habitat.WaterLevel(clicked):grid.GroundHeight(point);terrainBudget=.12f;}
            if(!terrainStroke)return;
            terrainBudget+=Time.unscaledDeltaTime;if(terrainBudget<.12f)return;
            float amount=terrainEditing.Strength*Mathf.Min(terrainBudget,.2f);terrainBudget=0;
            string reason;bool changed;
            if(terrainEditing.Mode==TerrainBrushMode.Water)
                changed=grid.Habitat.AddWater(center,radius,waterStrokeLevel,terrainEditing.WaterDepth,HabitatWaterEditable,out reason);
            else if(terrainEditing.Mode==TerrainBrushMode.RemoveWater)
                changed=grid.Habitat.RemoveWater(center,radius,grid.Contains,out reason);
            else if((int)terrainEditing.Mode>=6)
                changed=grid.Habitat.PaintBiome(center,radius,(HabitatBiome)((int)terrainEditing.Mode-6),HabitatGroundEditable,out reason);
            else if(!HabitatSculptSafe(center,radius)) {changed=false;reason=Loc.Text("habitat.terrain.protected_shore");}
            else changed=grid.Terrain.TryEdit(grid,center,radius,amount,terrainEditing.Mode,flattenTarget,out reason);
            if(changed)terrainMessage="";
            else terrainMessage=reason;
        }
    }
}

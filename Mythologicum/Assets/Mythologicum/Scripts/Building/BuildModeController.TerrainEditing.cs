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
        private float terrainBudget,flattenTarget;
        private string terrainMessage="";
        private void CreateTerrainEditingUI(Canvas canvas)
        {
            terrainEditing=new TerrainEditingPresentation(canvas,SelectTerrainBrush,()=>SelectTool(Tool.None));
            var root=new GameObject("Terrain brush outline",typeof(LineRenderer));terrainCursor=root.GetComponent<LineRenderer>();
            terrainCursorMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")??gridMaterial.shader);
            terrainCursor.sharedMaterial=terrainCursorMaterial;terrainCursor.useWorldSpace=true;terrainCursor.loop=true;terrainCursor.widthMultiplier=.06f;
            terrainCursor.positionCount=64;terrainCursor.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;root.SetActive(false);
        }
        private void SelectTerrainBrush(TerrainBrushMode mode)
        {SelectTool(Tool.Terraform);terrainEditing.SetMode(mode);ResetTerrainStroke();}
        private void ResetTerrainStroke(){terrainStroke=false;terrainBudget=0;terrainMessage="";}
        private void ApplyTerrainBrush(Mouse mouse,Vector3 point)
        {
            var center=new Vector2((point.x-grid.Origin.x)/grid.CellSize,(point.z-grid.Origin.z)/grid.CellSize);
            float radius=terrainEditing.Radius;
            bool locked=grid.Terrain.BrushTouchesFoundation(center,radius);
            terrainCursor.gameObject.SetActive(true);
            for(int i=0;i<64;i++)
            {float angle=i*Mathf.PI*2/64;var p=point+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*radius*grid.CellSize;p.y=grid.GroundHeight(p)+.14f;terrainCursor.SetPosition(i,p);}
            terrainCursorMaterial.color=locked?new Color(.95f,.18f,.1f):new Color(.15f,.9f,.95f);
            status.text=locked?"This brush touches a building foundation. Move it or reduce its radius.":terrainMessage!=""?terrainMessage
                :terrainEditing.Mode+" ground | Hold left mouse | Radius and rate below | Buildings and paths are protected";
            if(!mouse.leftButton.isPressed){ResetTerrainStroke();return;}
            if(mouse.leftButton.wasPressedThisFrame)
            {terrainStroke=true;flattenTarget=grid.GroundHeight(point)-grid.Origin.y;terrainBudget=.12f;}
            if(!terrainStroke)return;
            terrainBudget+=Time.deltaTime;if(terrainBudget<.12f)return;
            float amount=terrainEditing.Strength*Mathf.Min(terrainBudget,.2f);terrainBudget=0;
            if(grid.Terrain.TryEdit(grid,center,radius,amount,terrainEditing.Mode,flattenTarget,out string reason))terrainMessage="";
            else terrainMessage=reason;
        }
    }
}

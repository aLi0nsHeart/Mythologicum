using System;
using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    public sealed class TerrainEditingPresentation : IDisposable
    {
        private readonly GameObject root;
        private readonly Text radiusText,strengthText,waterDepthText,hint;
        private readonly Action<TerrainBrushMode> select;
        private readonly TerrainBrushSettings settings;
        public float Radius {get;private set;}
        public float Strength {get;private set;}
        public float WaterDepth {get;private set;}
        public TerrainBrushMode Mode {get;private set;}
        public TerrainEditingPresentation(Canvas canvas,Action<TerrainBrushMode> select,Action close,TerrainBrushSettings settings=null)
        {
            this.select=select;
            this.settings=settings??new TerrainBrushSettings();this.settings.Validate();
            Radius=this.settings.initialRadius;Strength=this.settings.initialStrength;WaterDepth=this.settings.initialWaterDepth;
            root=ManagementWidgets.Box(canvas.transform,"Terrain editing controls",Vector2.zero,Vector2.one);
            var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-24,24);
            var frame=UIFramedPanel.Attach(root,false,850);
            var controls=new GameObject("Terrain tools",typeof(RectTransform));var body=(RectTransform)controls.transform;body.SetParent(frame.Content,false);
            int index=0;foreach(TerrainBrushMode mode in Enum.GetValues(typeof(TerrainBrushMode)))
            {var m=mode;ManagementWidgets.Button(body,"enum.TerrainBrushMode."+mode,new Vector2((index%4)*120,(index/4)*44),new Vector2(112,36),()=>select(m),16);index++;}
            hint=ManagementWidgets.Label(body,"park.terrain_editing_presentation.hold_left_mouse_to_sculpt",new Vector2(0,144),new Vector2(472,128),16);
            radiusText=ManagementWidgets.Label(body,"",new Vector2(500,0),new Vector2(220,28),17);
            strengthText=ManagementWidgets.Label(body,"",new Vector2(500,78),new Vector2(220,28),17);
            waterDepthText=ManagementWidgets.Label(body,"",new Vector2(500,156),new Vector2(220,28),17);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.radius",new Vector2(500,32),new Vector2(100,36),()=>{Radius=Mathf.Max(this.settings.minimumRadius,Radius-this.settings.radiusStep);Refresh();},14);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.radius_2",new Vector2(612,32),new Vector2(100,36),()=>{Radius=Mathf.Min(this.settings.maximumRadius,Radius+this.settings.radiusStep);Refresh();},14);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.rate",new Vector2(500,110),new Vector2(100,36),()=>{Strength=Mathf.Max(this.settings.minimumStrength,Strength-this.settings.strengthStep);Refresh();},14);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.rate_2",new Vector2(612,110),new Vector2(100,36),()=>{Strength=Mathf.Min(this.settings.maximumStrength,Strength+this.settings.strengthStep);Refresh();},14);
            ManagementWidgets.Button(body,"habitat.terrain.depth_less",new Vector2(500,188),new Vector2(100,36),()=>{WaterDepth=Mathf.Max(this.settings.minimumWaterDepth,WaterDepth-this.settings.waterDepthStep);Refresh();},14);
            ManagementWidgets.Button(body,"habitat.terrain.depth_more",new Vector2(612,188),new Vector2(100,36),()=>{WaterDepth=Mathf.Min(this.settings.maximumWaterDepth,WaterDepth+this.settings.waterDepthStep);Refresh();},14);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.done",new Vector2(500,238),new Vector2(212,38),close,19);
            frame.SetLogicalContent(body,new Vector2(720,280));
            Refresh();root.SetActive(false);
        }
        private void Refresh(){radiusText.text=Loc.Format("park.terrain_editing_presentation.radius_tiles", Radius);strengthText.text=Loc.Format("park.terrain_editing_presentation.rate_m_sec", Strength);waterDepthText.text=Loc.Format("habitat.terrain.depth_value", WaterDepth);}
        public void SetActive(bool value){root.SetActive(value);if(value)root.transform.SetAsLastSibling();}
        public void SetMode(TerrainBrushMode mode)
        {Mode=mode;hint.text=mode==TerrainBrushMode.Water?Loc.Text("habitat.terrain.water_hint"):mode==TerrainBrushMode.RemoveWater?Loc.Text("habitat.terrain.drain_hint")
            :mode>=TerrainBrushMode.Grass?Loc.Format("habitat.terrain.biome_hint",Loc.Enum(mode))
            :mode==TerrainBrushMode.Flatten?Loc.Text("park.terrain_editing_presentation.hold_and_drag_level_to_the_first_click_s_height"):Loc.Format("park.terrain_editing_presentation.hold_left_mouse_ground_esc_finish", Loc.Enum(mode));}
        public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);}
    }
}

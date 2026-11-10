using System;
using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    public sealed class TerrainEditingPresentation : IDisposable
    {
        private readonly GameObject root;
        private readonly Text radiusText,strengthText,hint;
        private readonly Action<TerrainBrushMode> select;
        private readonly TerrainBrushSettings settings;
        public float Radius {get;private set;}
        public float Strength {get;private set;}
        public TerrainBrushMode Mode {get;private set;}
        public TerrainEditingPresentation(Canvas canvas,Action<TerrainBrushMode> select,Action close,TerrainBrushSettings settings=null)
        {
            this.select=select;
            this.settings=settings??new TerrainBrushSettings();this.settings.Validate();
            Radius=this.settings.initialRadius;Strength=this.settings.initialStrength;
            root=ManagementWidgets.Box(canvas.transform,"Terrain editing controls",Vector2.zero,Vector2.one);
            var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-24,24);
            var body=UIFramedPanel.Attach(root,false,640).Content;
            int index=0;foreach(TerrainBrushMode mode in Enum.GetValues(typeof(TerrainBrushMode)))
            {var m=mode;ManagementWidgets.Button(body,"enum.TerrainBrushMode."+mode,new Vector2((index%2)*130,(index/2)*44),new Vector2(120,36),()=>select(m),17);index++;}
            hint=ManagementWidgets.Label(body,"park.terrain_editing_presentation.hold_left_mouse_to_sculpt",new Vector2(0,100),new Vector2(250,100),17);
            radiusText=ManagementWidgets.Label(body,"",new Vector2(280,0),new Vector2(250,28),18);
            strengthText=ManagementWidgets.Label(body,"",new Vector2(280,88),new Vector2(250,28),18);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.radius",new Vector2(280,32),new Vector2(110,36),()=>{Radius=Mathf.Max(this.settings.minimumRadius,Radius-this.settings.radiusStep);Refresh();},14);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.radius_2",new Vector2(400,32),new Vector2(110,36),()=>{Radius=Mathf.Min(this.settings.maximumRadius,Radius+this.settings.radiusStep);Refresh();},14);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.rate",new Vector2(280,120),new Vector2(110,36),()=>{Strength=Mathf.Max(this.settings.minimumStrength,Strength-this.settings.strengthStep);Refresh();},14);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.rate_2",new Vector2(400,120),new Vector2(110,36),()=>{Strength=Mathf.Min(this.settings.maximumStrength,Strength+this.settings.strengthStep);Refresh();},14);
            ManagementWidgets.Button(body,"park.terrain_editing_presentation.done",new Vector2(280,184),new Vector2(230,38),close,19);
            Refresh();root.SetActive(false);
        }
        private void Refresh(){radiusText.text=Loc.Format("park.terrain_editing_presentation.radius_tiles", Radius);strengthText.text=Loc.Format("park.terrain_editing_presentation.rate_m_sec", Strength);}
        public void SetActive(bool value){root.SetActive(value);if(value)root.transform.SetAsLastSibling();}
        public void SetMode(TerrainBrushMode mode)
        {Mode=mode;hint.text=mode==TerrainBrushMode.Flatten?Loc.Text("park.terrain_editing_presentation.hold_and_drag_level_to_the_first_click_s_height"):Loc.Format("park.terrain_editing_presentation.hold_left_mouse_ground_esc_finish", Loc.Enum(mode));}
        public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);}
    }
}

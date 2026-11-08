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
        public float Radius {get;private set;}=3f;
        public float Strength {get;private set;}=1f;
        public TerrainBrushMode Mode {get;private set;}
        public TerrainEditingPresentation(Canvas canvas,Action<TerrainBrushMode> select,Action close)
        {
            this.select=select;
            root=ManagementWidgets.Box(canvas.transform,"Terrain editing controls",Vector2.zero,Vector2.one);
            var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(1,0);rect.anchoredPosition=new Vector2(-24,24);
            var body=UIFramedPanel.Attach(root,false,640).Content;
            int index=0;foreach(TerrainBrushMode mode in Enum.GetValues(typeof(TerrainBrushMode)))
            {var m=mode;ManagementWidgets.Button(body,mode.ToString(),new Vector2((index%2)*130,(index/2)*44),new Vector2(120,36),()=>select(m),17);index++;}
            hint=ManagementWidgets.Label(body,"Hold left mouse to sculpt.",new Vector2(0,100),new Vector2(250,100),17);
            radiusText=ManagementWidgets.Label(body,"",new Vector2(280,0),new Vector2(250,28),18);
            strengthText=ManagementWidgets.Label(body,"",new Vector2(280,88),new Vector2(250,28),18);
            ManagementWidgets.Button(body,"Radius -",new Vector2(280,32),new Vector2(110,36),()=>{Radius=Mathf.Max(1,Radius-1);Refresh();},14);
            ManagementWidgets.Button(body,"Radius +",new Vector2(400,32),new Vector2(110,36),()=>{Radius=Mathf.Min(8,Radius+1);Refresh();},14);
            ManagementWidgets.Button(body,"Rate -",new Vector2(280,120),new Vector2(110,36),()=>{Strength=Mathf.Max(.25f,Strength-.25f);Refresh();},14);
            ManagementWidgets.Button(body,"Rate +",new Vector2(400,120),new Vector2(110,36),()=>{Strength=Mathf.Min(4,Strength+.25f);Refresh();},14);
            ManagementWidgets.Button(body,"Done",new Vector2(280,184),new Vector2(230,38),close,19);
            Refresh();root.SetActive(false);
        }
        private void Refresh(){radiusText.text=$"Radius: {Radius:0} tiles";strengthText.text=$"Rate: {Strength:0.##} m / sec";}
        public void SetActive(bool value){root.SetActive(value);if(value)root.transform.SetAsLastSibling();}
        public void SetMode(TerrainBrushMode mode)
        {Mode=mode;hint.text=mode==TerrainBrushMode.Flatten?"Hold and drag: level to the first click's height.":"Hold left mouse: "+mode.ToString().ToLowerInvariant()+" ground. Esc: finish.";}
        public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);}
    }
}

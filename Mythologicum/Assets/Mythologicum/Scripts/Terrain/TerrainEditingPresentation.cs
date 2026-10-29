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
            root=ManagementWidgets.Box(canvas.transform,"Terrain editing controls",Vector2.zero,new Vector2(980,88));
            var rect=(RectTransform)root.transform;rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,0);rect.anchoredPosition=new Vector2(0,86);
            int index=0;foreach(TerrainBrushMode mode in Enum.GetValues(typeof(TerrainBrushMode)))
            {var m=mode;ManagementWidgets.Button(root.transform,mode.ToString(),new Vector2(10+index++*125,8),new Vector2(120,34),()=>select(m),17);}
            hint=ManagementWidgets.Label(root.transform,"Hold left mouse to sculpt.",new Vector2(12,48),new Vector2(485,32),17);
            radiusText=ManagementWidgets.Label(root.transform,"",new Vector2(520,12),new Vector2(195,30),18);
            strengthText=ManagementWidgets.Label(root.transform,"",new Vector2(520,52),new Vector2(195,30),18);
            ManagementWidgets.Button(root.transform,"Radius -",new Vector2(715,8),new Vector2(43,34),()=>{Radius=Mathf.Max(1,Radius-1);Refresh();},14);
            ManagementWidgets.Button(root.transform,"Radius +",new Vector2(765,8),new Vector2(43,34),()=>{Radius=Mathf.Min(8,Radius+1);Refresh();},14);
            ManagementWidgets.Button(root.transform,"Rate -",new Vector2(715,48),new Vector2(43,34),()=>{Strength=Mathf.Max(.25f,Strength-.25f);Refresh();},14);
            ManagementWidgets.Button(root.transform,"Rate +",new Vector2(765,48),new Vector2(43,34),()=>{Strength=Mathf.Min(4,Strength+.25f);Refresh();},14);
            ManagementWidgets.Button(root.transform,"Done",new Vector2(825,8),new Vector2(143,74),close,19);
            Refresh();root.SetActive(false);
        }
        private void Refresh(){radiusText.text=$"Radius: {Radius:0} tiles";strengthText.text=$"Rate: {Strength:0.##} m / sec";}
        public void SetActive(bool value){root.SetActive(value);if(value)root.transform.SetAsLastSibling();}
        public void SetMode(TerrainBrushMode mode)
        {Mode=mode;hint.text=mode==TerrainBrushMode.Flatten?"Hold and drag: level to the first click's height.":"Hold left mouse: "+mode.ToString().ToLowerInvariant()+" ground. Esc: finish.";}
        public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);}
    }
}

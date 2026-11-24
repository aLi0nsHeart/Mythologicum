using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    /// <summary>Continent silhouettes and expedition pins share a normalized world viewport.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ExpeditionMapGraphic : MaskableGraphic
    {
        private Rect viewport=new Rect(0,0,1,1);
        public Rect Viewport=>viewport;
        public void SetViewport(Rect normalizedWorldBounds)
        {
            normalizedWorldBounds.width=Mathf.Max(.001f,normalizedWorldBounds.width);
            normalizedWorldBounds.height=Mathf.Max(.001f,normalizedWorldBounds.height);
            if(viewport==normalizedWorldBounds)return;
            viewport=normalizedWorldBounds;SetVerticesDirty();
        }
        public Vector2 Project(Vector2 worldPosition)=>(worldPosition-viewport.center)
            /Mathf.Max(viewport.width,viewport.height)+new Vector2(.5f,.5f);
        private static readonly Vector2[][] Land = {
            new[]{new Vector2(.06f,.8f),new Vector2(.14f,.94f),new Vector2(.3f,.89f),new Vector2(.35f,.73f),new Vector2(.28f,.56f),new Vector2(.25f,.44f),new Vector2(.17f,.56f),new Vector2(.09f,.67f)},
            new[]{new Vector2(.25f,.47f),new Vector2(.36f,.43f),new Vector2(.4f,.31f),new Vector2(.36f,.14f),new Vector2(.3f,.02f),new Vector2(.28f,.24f)},
            new[]{new Vector2(.35f,.92f),new Vector2(.4f,.98f),new Vector2(.44f,.92f),new Vector2(.4f,.81f)},
            new[]{new Vector2(.45f,.69f),new Vector2(.52f,.83f),new Vector2(.56f,.96f),new Vector2(.6f,.88f),new Vector2(.57f,.69f),new Vector2(.52f,.6f)},
            new[]{new Vector2(.45f,.6f),new Vector2(.53f,.64f),new Vector2(.63f,.55f),new Vector2(.6f,.37f),new Vector2(.55f,.13f),new Vector2(.49f,.32f),new Vector2(.44f,.47f)},
            new[]{new Vector2(.56f,.83f),new Vector2(.7f,.94f),new Vector2(.9f,.87f),new Vector2(.95f,.77f),new Vector2(.86f,.61f),new Vector2(.82f,.4f),new Vector2(.76f,.5f),new Vector2(.72f,.33f),new Vector2(.66f,.51f),new Vector2(.6f,.64f)},
            new[]{new Vector2(.81f,.23f),new Vector2(.9f,.25f),new Vector2(.94f,.14f),new Vector2(.9f,.08f),new Vector2(.8f,.11f)},
            new[]{new Vector2(.9f,.62f),new Vector2(.915f,.67f),new Vector2(.92f,.57f),new Vector2(.9f,.53f)},
            new[]{new Vector2(.06f,.015f),new Vector2(.12f,.045f),new Vector2(.3f,.035f),new Vector2(.45f,.055f),new Vector2(.62f,.04f),new Vector2(.8f,.055f),new Vector2(.95f,.025f),new Vector2(.92f,0),new Vector2(.1f,0)}
        };
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            foreach(var polygon in Land)
            {
                // Clip in world coordinates before projection so a narrow continent's
                // letterbox margins stay empty and the silhouette keeps its proportions.
                var points=ClipToViewport(polygon);if(points.Count<3)continue;
                int start=vh.currentVertCount;Vector2 center=Vector2.zero;foreach(var p in points)center+=p;center/=points.Count;
                Vector2 projected=Project(center);
                vh.AddVert(new Vector3(r.xMin+projected.x*r.width,r.yMin+projected.y*r.height),color,Vector2.zero);
                foreach(var p in points)
                {
                    projected=Project(p);
                    vh.AddVert(new Vector3(r.xMin+projected.x*r.width,r.yMin+projected.y*r.height),color,Vector2.zero);
                }
                for(int i=0;i<points.Count;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Count);
            }
        }
        private List<Vector2> ClipToViewport(Vector2[] polygon)
        {
            var points=new List<Vector2>(polygon);
            for(int edge=0;edge<4&&points.Count>0;edge++)
            {
                var input=points;points=new List<Vector2>(input.Count+4);
                Vector2 previous=input[input.Count-1];bool previousInside=Inside(previous,edge);
                foreach(var current in input)
                {
                    bool currentInside=Inside(current,edge);
                    if(currentInside!=previousInside)
                    {
                        bool xEdge=edge<2;
                        float border=edge==0?viewport.xMin:edge==1?viewport.xMax:edge==2?viewport.yMin:viewport.yMax;
                        float fraction=xEdge?(border-previous.x)/(current.x-previous.x):(border-previous.y)/(current.y-previous.y);
                        points.Add(Vector2.LerpUnclamped(previous,current,fraction));
                    }
                    if(currentInside)points.Add(current);
                    previous=current;previousInside=currentInside;
                }
            }
            return points;
        }
        private bool Inside(Vector2 point,int edge)
        {
            switch(edge)
            {
                case 0:return point.x>=viewport.xMin;
                case 1:return point.x<=viewport.xMax;
                case 2:return point.y>=viewport.yMin;
                default:return point.y<=viewport.yMax;
            }
        }
    }
}

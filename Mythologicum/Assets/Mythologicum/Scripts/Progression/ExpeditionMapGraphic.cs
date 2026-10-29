using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    /// <summary>Simple continent silhouettes; expedition pins carry the actual region definitions.</summary>
    public sealed class ExpeditionMapGraphic : Graphic
    {
        private static readonly Vector2[][] Land = {
            new[]{new Vector2(.06f,.8f),new Vector2(.14f,.94f),new Vector2(.3f,.89f),new Vector2(.35f,.73f),new Vector2(.28f,.56f),new Vector2(.25f,.44f),new Vector2(.17f,.56f),new Vector2(.09f,.67f)},
            new[]{new Vector2(.25f,.47f),new Vector2(.36f,.43f),new Vector2(.4f,.31f),new Vector2(.36f,.14f),new Vector2(.3f,.02f),new Vector2(.28f,.24f)},
            new[]{new Vector2(.35f,.92f),new Vector2(.4f,.98f),new Vector2(.44f,.92f),new Vector2(.4f,.81f)},
            new[]{new Vector2(.45f,.69f),new Vector2(.52f,.83f),new Vector2(.56f,.96f),new Vector2(.6f,.88f),new Vector2(.57f,.69f),new Vector2(.52f,.6f)},
            new[]{new Vector2(.45f,.6f),new Vector2(.53f,.64f),new Vector2(.63f,.55f),new Vector2(.6f,.37f),new Vector2(.55f,.13f),new Vector2(.49f,.32f),new Vector2(.44f,.47f)},
            new[]{new Vector2(.56f,.83f),new Vector2(.7f,.94f),new Vector2(.9f,.87f),new Vector2(.95f,.77f),new Vector2(.86f,.61f),new Vector2(.82f,.4f),new Vector2(.76f,.5f),new Vector2(.72f,.33f),new Vector2(.66f,.51f),new Vector2(.6f,.64f)},
            new[]{new Vector2(.81f,.23f),new Vector2(.9f,.25f),new Vector2(.94f,.14f),new Vector2(.9f,.08f),new Vector2(.8f,.11f)},
            new[]{new Vector2(.9f,.62f),new Vector2(.915f,.67f),new Vector2(.92f,.57f),new Vector2(.9f,.53f)}
        };
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            foreach(var points in Land)
            {
                int start=vh.currentVertCount;Vector2 center=Vector2.zero;foreach(var p in points)center+=p;center/=points.Length;
                vh.AddVert(new Vector3(r.xMin+center.x*r.width,r.yMin+center.y*r.height),color,Vector2.zero);
                foreach(var p in points)vh.AddVert(new Vector3(r.xMin+p.x*r.width,r.yMin+p.y*r.height),color,Vector2.zero);
                for(int i=0;i<points.Length;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Length);
            }
        }
    }
}

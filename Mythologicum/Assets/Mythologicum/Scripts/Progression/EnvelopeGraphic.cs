using UnityEngine;
using UnityEngine.UI;
namespace Mythologicum
{
    public sealed class EnvelopeGraphic : Graphic
    {
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            Line(vh,new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin));Line(vh,new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax));
            Line(vh,new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax));Line(vh,new Vector2(r.xMin,r.yMax),new Vector2(r.xMin,r.yMin));
            Line(vh,new Vector2(r.xMin,r.yMax),new Vector2(r.center.x,r.center.y));Line(vh,new Vector2(r.center.x,r.center.y),new Vector2(r.xMax,r.yMax));
        }
        private void Line(VertexHelper vh,Vector2 a,Vector2 b)
        {
            var n=(b-a).normalized;var side=new Vector2(-n.y,n.x)*1.2f;int i=vh.currentVertCount;
            vh.AddVert(a-side,color,Vector2.zero);vh.AddVert(a+side,color,Vector2.zero);vh.AddVert(b+side,color,Vector2.zero);vh.AddVert(b-side,color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}

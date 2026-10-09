using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Draw the simulation's ground projection in map coordinates, beneath its landing markers.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GolfMapPath : MaskableGraphic
    {
        readonly List<Vector2> points = new();
        bool dotted;
        public void SetPath(Camera map, IReadOnlyList<Vector3> path, Color tint, bool dots)
        {
            points.Clear();
            foreach (var world in path)
            {
                var v = map.WorldToViewportPoint(world);
                points.Add(new Vector2(v.x, v.y));
            }
            color = tint; dotted = dots; SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var rect = rectTransform.rect;
            Vector2 Local(Vector2 v) => rect.min + Vector2.Scale(v, rect.size);
            void Quad(Vector2 a, Vector2 b, float width, Color tint)
            {
                var n = (b-a).normalized; n = new Vector2(-n.y,n.x)*width/2;
                int start = mesh.currentVertCount;
                mesh.AddVert(a-n,tint,Vector2.zero);mesh.AddVert(a+n,tint,Vector2.zero);
                mesh.AddVert(b+n,tint,Vector2.zero);mesh.AddVert(b-n,tint,Vector2.zero);
                mesh.AddTriangle(start,start+1,start+2);mesh.AddTriangle(start,start+2,start+3);
            }
            void Dot(Vector2 centre)
            {
                int start=mesh.currentVertCount; mesh.AddVert(centre,Color.white,Vector2.zero);
                for(int j=0;j<12;j++){float t=j*Mathf.PI*2/12;mesh.AddVert(centre+new Vector2(Mathf.Cos(t),Mathf.Sin(t))*3.3f,Color.white,Vector2.zero);}
                for(int j=0;j<12;j++)mesh.AddTriangle(start,start+1+j,start+1+(j+1)%12);
            }
            float travelled=0, nextDot=0;
            for(int i=1;i<points.Count;i++)
            {
                var a=Local(points[i-1]);var b=Local(points[i]);float length=Vector2.Distance(a,b);
                if(length<.001f)continue;
                Quad(a,b,7,new Color(0,0,0,.65f));Quad(a,b,3.5f,color);
                if(dotted)while(nextDot<=travelled+length){Dot(Vector2.Lerp(a,b,(nextDot-travelled)/length));nextDot+=15;}
                travelled+=length;
            }
        }
    }
}

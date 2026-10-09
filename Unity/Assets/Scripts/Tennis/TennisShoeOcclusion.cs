using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Two bounded occlusion footprints follow evaluated sole geometry. The
    /// samples use the same bone/bind matrices as skinning without baking the
    /// whole garment every frame. No actor/root or collision transform changes.
    [DefaultExecutionOrder(1175)]
    public sealed class TennisShoeOcclusion : MonoBehaviour
    {
        public ContactShadow shadow;
        public Bounds LeftBounds {get;private set;}
        public Bounds RightBounds {get;private set;}
        public float LeftFade {get;private set;}
        public float RightFade {get;private set;}
        public int SampleCount => (left?.samples.Count??0)+(right?.samples.Count??0);
        sealed class Sole
        {
            public SkinnedMeshRenderer renderer;public Mesh source;
            public readonly List<int> samples=new();public Vector3[] vertices;public BoneWeight[] weights;
            public Matrix4x4[] binds,matrices;public Vector3[] posed;
            public Transform foot,toe;
            public void Prepare()
            {
                var mesh=renderer.sharedMesh;if(mesh==source)return;source=mesh;
                vertices=mesh.vertices;weights=mesh.boneWeights;binds=mesh.bindposes;matrices=new Matrix4x4[binds.Length];samples.Clear();
                var candidates=new HashSet<int>();var materials=renderer.sharedMaterials;
                for(int s=0;s<mesh.subMeshCount&&s<materials.Length;s++)if(materials[s]&&materials[s].name.StartsWith(MatchHeroLook.RoleSole))foreach(int id in mesh.GetIndices(s))candidates.Add(id);
                // The exact sole envelope, including heel/toe/extreme perimeter.
                // Sixteen horizontal support directions plus vertical extrema.
                for(int k=0;k<18;k++)
                {
                    var direction=k==16?Vector3.down:k==17?Vector3.up:new Vector3(Mathf.Cos(k*Mathf.PI/8),0,Mathf.Sin(k*Mathf.PI/8));
                    float best=float.NegativeInfinity;int chosen=-1;
                    foreach(int id in candidates){float dot=Vector3.Dot(vertices[id],direction);if(dot>best){best=dot;chosen=id;}}
                    if(chosen>=0&&!samples.Contains(chosen))samples.Add(chosen);
                }
                posed=new Vector3[samples.Count];
            }
            public Bounds Evaluate()
            {
                Prepare();var bones=renderer.bones;
                for(int b=0;b<matrices.Length;b++)matrices[b]=bones[b].localToWorldMatrix*binds[b];
                var bounds=new Bounds();
                for(int k=0;k<samples.Count;k++)
                {
                    int id=samples[k];var w=weights[id];var v=vertices[id];var p=Vector3.zero;
                    if(w.weight0>0)p+=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0;
                    if(w.weight1>0)p+=matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1;
                    if(w.weight2>0)p+=matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2;
                    if(w.weight3>0)p+=matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                    posed[k]=p;if(k==0)bounds=new Bounds(p,Vector3.zero);else bounds.Encapsulate(p);
                }
                return bounds;
            }
        }
        MatchHeroLook look;Sole left,right;Mesh mesh;
        readonly Vector3[] positions=new Vector3[8];readonly Color[] colors=new Color[8];
        bool Bind()
        {
            if(!shadow||!shadow.Follow)return false;
            if(!look)
            {
                look=shadow.Follow.GetComponentInChildren<MatchHeroLook>();if(!look||look.kit==null)return false;
                left=null;right=null;
                foreach(var shoe in look.kit)
                {
                    if(!shoe)continue;
                    if(shoe.name=="Kit_Shoe_L")left=new Sole{renderer=shoe,foot=look.Bone(HumanBodyBones.LeftFoot),toe=look.Bone(HumanBodyBones.LeftToes)};
                    if(shoe.name=="Kit_Shoe_R")right=new Sole{renderer=shoe,foot=look.Bone(HumanBodyBones.RightFoot),toe=look.Bone(HumanBodyBones.RightToes)};
                }
            }
            return left!=null&&right!=null&&left.renderer&&right.renderer;
        }
        void LateUpdate()
        {
            if(!shadow||!shadow.enabled||(!shadow.GolfGround&&!TennisVenue.HasRenderedCourtHeight)||!Bind())return;
            if(!mesh)
            {
                mesh=new Mesh{name="Evaluated shoe contact footprints"};mesh.MarkDynamic();
                mesh.vertices=positions;mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up,Vector2.zero,Vector2.right,Vector2.one,Vector2.up};
                mesh.triangles=new[]{0,2,1,0,3,2,4,6,5,4,7,6};GetComponent<MeshFilter>().sharedMesh=mesh;
            }
            LeftBounds=left.Evaluate();RightBounds=right.Evaluate();
            LeftFade=Write(left,LeftBounds,0);RightFade=Write(right,RightBounds,4);
            transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);transform.localScale=Vector3.one;
            mesh.vertices=positions;mesh.colors=colors;mesh.RecalculateBounds();
            if(shadow.Material){shadow.Material.SetFloat("_UseVertexFade",1);shadow.Material.SetFloat("_Strength",Mathf.Min(.68f,shadow.Strength*1.4f));}
        }
        float Write(Sole sole,Bounds bounds,int start)
        {
            float court=shadow.GolfGround ? (float)GolfArcade.Course.HoleView.GroundHeight(new GolfArcade.Course.CoursePoint(bounds.center.x,bounds.center.z)) : TennisVenue.RenderedCourtHeight;
            float fade=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.15f,bounds.min.y-court));
            var forward=sole.foot&&sole.toe?sole.toe.position-sole.foot.position:shadow.Follow.forward;forward.y=0;
            if(forward.sqrMagnitude<.0001f)forward=shadow.Follow.forward;forward.Normalize();var across=Vector3.Cross(Vector3.up,forward);
            var centre=new Vector3(bounds.center.x,court+.004f,bounds.center.z);
            float width=0,length=0;
            foreach(var p in sole.posed){width=Mathf.Max(width,Mathf.Abs(Vector3.Dot(p-centre,across)));length=Mathf.Max(length,Mathf.Abs(Vector3.Dot(p-centre,forward)));}
            width=Mathf.Clamp(width+.065f,.14f,.22f);length=Mathf.Clamp(length+.08f,.19f,.30f);
            positions[start]=centre-across*width-forward*length;positions[start+1]=centre+across*width-forward*length;
            positions[start+2]=centre+across*width+forward*length;positions[start+3]=centre-across*width+forward*length;
            for(int i=start;i<start+4;i++)colors[i]=new Color(1,1,1,fade);
            return fade;
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}

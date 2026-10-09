using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Shared near meshes and a 16-triangle distant crown. Fixed lists avoid
    /// allocations and bounded culling keeps added plant density inexpensive.
    public sealed class GolfBotanicalInstances : MonoBehaviour
    {
        sealed class Group
        {
            public Mesh mesh;public int sub;
            public readonly List<Matrix4x4> source=new(),near=new(1023),far=new(1023);
        }
        readonly List<Group> groups=new();
        Material material;Mesh proxy;
        readonly Plane[] planes=new Plane[6];
        public int LastDrawCalls {get;private set;}
        public int LastInstances {get;private set;}
        public void Initialize(Material source,List<CombineInstance> parts)
        {
            material=source;material.enableInstancing=true;
            var lookup=new Dictionary<(Mesh,int),Group>();
            foreach(var part in parts){
                var key=(part.mesh,part.subMeshIndex);
                if(!lookup.TryGetValue(key,out var group)){group=new Group{mesh=part.mesh,sub=part.subMeshIndex};lookup.Add(key,group);groups.Add(group);}
                group.source.Add(part.transform);
            }
            proxy=Proxy();
        }
        static Mesh Proxy()
        {
            var v=new List<Vector3>();var t=new List<int>();var c=new List<Color>();
            for(int side=0;side<8;side++){
                float a=side*Mathf.PI/4,b=(side+1)*Mathf.PI/4;int k=v.Count;
                v.Add(new Vector3(Mathf.Cos(a)*.5f,.3f,Mathf.Sin(a)*.5f));
                v.Add(new Vector3(Mathf.Cos(b)*.5f,.3f,Mathf.Sin(b)*.5f));
                v.Add(new Vector3(0,.85f,0));v.Add(Vector3.zero);
                c.Add(new Color(.19f,.35f,.085f,.65f));c.Add(new Color(.19f,.35f,.085f,.65f));c.Add(new Color(.32f,.49f,.15f,1));c.Add(new Color(.075f,.16f,.055f,0));
                t.AddRange(new[]{k,k+2,k+1,k,k+1,k+3});
            }
            var mesh=new Mesh{name="Distant botanical crown"};mesh.SetVertices(v);mesh.SetColors(c);mesh.SetTriangles(t,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        void LateUpdate()
        {
            var camera=Camera.main;LastDrawCalls=0;LastInstances=0;if(!camera||!material)return;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);var root=transform.localToWorldMatrix;
            foreach(var group in groups){
                group.near.Clear();group.far.Clear();
                foreach(var local in group.source){
                    var world=root*local;var centre=world.MultiplyPoint3x4(group.mesh.bounds.center);
                    float distance=(camera.transform.position-centre).sqrMagnitude;if(distance>300*300)continue;
                    var size=group.mesh.bounds.size;float radius=Mathf.Max(world.MultiplyVector(Vector3.right*size.x).magnitude,Mathf.Max(world.MultiplyVector(Vector3.up*size.y).magnitude,world.MultiplyVector(Vector3.forward*size.z).magnitude));
                    if(!GeometryUtility.TestPlanesAABB(planes,new Bounds(centre,Vector3.one*(radius*2+1))))continue;
                    if(distance<85*85||!SystemInfo.supportsInstancing){
                        group.near.Add(world);if(group.near.Count==1023)Draw(group.mesh,group.sub,group.near);
                    }else if(group.sub==0){
                        // Bounds-fit proxy keeps each source plant's footprint/height.
                        var fit=world*Matrix4x4.TRS(group.mesh.bounds.center-Vector3.up*size.y*.5f,Quaternion.identity,new Vector3(size.x,size.y/.85f,size.z));
                        group.far.Add(fit);if(group.far.Count==1023)Draw(proxy,0,group.far);
                    }
                }
                Draw(group.mesh,group.sub,group.near);Draw(proxy,0,group.far);
            }
        }
        void Draw(Mesh mesh,int sub,List<Matrix4x4> matrices)
        {
            if(matrices.Count==0)return;
            if(SystemInfo.supportsInstancing){Graphics.DrawMeshInstanced(mesh,sub,material,matrices,null,ShadowCastingMode.Off,true,gameObject.layer,null,LightProbeUsage.Off);LastDrawCalls++;}
            else foreach(var matrix in matrices){Graphics.DrawMesh(mesh,matrix,material,gameObject.layer,null,sub,null,ShadowCastingMode.Off,true);LastDrawCalls++;}
            LastInstances+=matrices.Count;matrices.Clear();
        }
        void OnDestroy(){if(proxy)Destroy(proxy);}
    }
}

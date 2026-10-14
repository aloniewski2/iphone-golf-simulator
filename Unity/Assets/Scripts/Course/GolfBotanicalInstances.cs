using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Shared near meshes, instanced. Fixed lists avoid allocations; only the view frustum culls.
    public sealed class GolfBotanicalInstances : MonoBehaviour
    {
        sealed class Group
        {
            public Mesh mesh;public int sub;
            public readonly List<Matrix4x4> source=new(),near=new(1023);
        }
        readonly List<Group> groups=new();
        Material material;
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
        }
        void LateUpdate()
        {
            var camera=Camera.main;LastDrawCalls=0;LastInstances=0;if(!camera||!material)return;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);var root=transform.localToWorldMatrix;
            foreach(var group in groups){
                group.near.Clear();
                foreach(var local in group.source){
                    var world=root*local;var centre=world.MultiplyPoint3x4(group.mesh.bounds.center);
                    var size=group.mesh.bounds.size;float radius=Mathf.Max(world.MultiplyVector(Vector3.right*size.x).magnitude,Mathf.Max(world.MultiplyVector(Vector3.up*size.y).magnitude,world.MultiplyVector(Vector3.forward*size.z).magnitude));
                    if(!GeometryUtility.TestPlanesAABB(planes,new Bounds(centre,Vector3.one*(radius*2+1))))continue;
                    // Every plant in view draws its real mesh at every distance: no cut-off and no
                    // distant stand-in, so nothing appears or changes as the camera flies toward it.
                    group.near.Add(world);if(group.near.Count==1023)Draw(group.mesh,group.sub,group.near);
                }
                Draw(group.mesh,group.sub,group.near);
            }
        }
        void Draw(Mesh mesh,int sub,List<Matrix4x4> matrices)
        {
            if(matrices.Count==0)return;
            if(SystemInfo.supportsInstancing){Graphics.DrawMeshInstanced(mesh,sub,material,matrices,null,ShadowCastingMode.Off,true,gameObject.layer,null,LightProbeUsage.Off);LastDrawCalls++;}
            else foreach(var matrix in matrices){Graphics.DrawMesh(mesh,matrix,material,gameObject.layer,null,sub,null,ShadowCastingMode.Off,true);LastDrawCalls++;}
            LastInstances+=matrices.Count;matrices.Clear();
        }
    }
}

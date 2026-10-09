using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Course
{
    /// Artist near meshes and baked distant foliage share instanced submissions.
    /// Only rendering changes: the authored placements and gameplay obstacles stay intact.
    public sealed class GolfCoastalInstances : MonoBehaviour
    {
        sealed class Group
        {
            public Mesh nearMesh,farMesh;public Material[] nearMaterials,farMaterials;public bool tree;
            public readonly List<Matrix4x4> source=new(),near=new(1023),far=new(1023);
        }
        readonly Dictionary<Mesh,Group> groups=new();
        readonly Plane[] planes=new Plane[6];
        public int LastDrawCalls {get;private set;}
        public int LastInstances {get;private set;}
        public void Add(Mesh near,Mesh far,Material[] nearMaterials,Material[] farMaterials,Matrix4x4 local,bool tree)
        {
            if(!groups.TryGetValue(near,out var group)){
                group=new Group{nearMesh=near,farMesh=far,nearMaterials=nearMaterials,farMaterials=farMaterials,tree=tree};groups.Add(near,group);
                foreach(var material in nearMaterials)material.enableInstancing=true;
                foreach(var material in farMaterials)material.enableInstancing=true;
            }
            group.source.Add(local);
        }
        void LateUpdate()
        {
            LastDrawCalls=LastInstances=0;var camera=Camera.main;if(!camera)return;
            GeometryUtility.CalculateFrustumPlanes(camera,planes);
            float lens=2*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            foreach(var group in groups.Values){
                group.near.Clear();group.far.Clear();
                foreach(var local in group.source){
                    var world=transform.localToWorldMatrix*local;var bounds=group.nearMesh.bounds;
                    var centre=world.MultiplyPoint3x4(bounds.center);
                    float radius=world.MultiplyVector(bounds.extents).magnitude;
                    float distance=Vector3.Distance(camera.transform.position,centre);
                    float relative=radius*2/Mathf.Max(1,distance*lens)*QualitySettings.lodBias;
                    if(relative<(group.tree?.008f:.003f)||!GeometryUtility.TestPlanesAABB(planes,new Bounds(centre,Vector3.one*(radius*2+1))))continue;
                    bool near=relative>(group.tree?.105f:.14f);
                    var batch=near?group.near:group.far;batch.Add(world);
                    if(batch.Count==1023)Draw(group,near,batch);
                }
                Draw(group,true,group.near);Draw(group,false,group.far);
            }
        }
        void Draw(Group group,bool near,List<Matrix4x4> matrices)
        {
            if(matrices.Count==0)return;
            var mesh=near?group.nearMesh:group.farMesh;var materials=near?group.nearMaterials:group.farMaterials;
            var shadow=near&&group.tree?ShadowCastingMode.On:ShadowCastingMode.Off;
            for(int sub=0;sub<mesh.subMeshCount;sub++){
                var material=materials[Mathf.Min(sub,materials.Length-1)];
                if(SystemInfo.supportsInstancing){Graphics.DrawMeshInstanced(mesh,sub,material,matrices,null,shadow,true,gameObject.layer,null,LightProbeUsage.Off);LastDrawCalls++;}
                else foreach(var matrix in matrices){Graphics.DrawMesh(mesh,matrix,material,gameObject.layer,null,sub,null,shadow,true);LastDrawCalls++;}
            }
            LastInstances+=matrices.Count;matrices.Clear();
        }
    }
}

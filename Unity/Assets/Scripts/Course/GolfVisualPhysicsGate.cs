#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Editor-only, opt-in synchronous contract around visual dressing. Captures actual
    /// original collision objects/data; no build assets or gameplay values are changed.
    public static class GolfVisualPhysicsGate
    {
        public static bool Enabled;
        public static readonly Dictionary<int,Report> Reports=new();
        [Serializable] public sealed class ColliderRecord
        {
            public string path,type,meshHash,transformHash,shapeHash;public int componentId,meshId,vertices,triangles;public bool enabled,active,trigger,readable;
        }
        [Serializable] public sealed class State
        {
            public ColliderRecord[] colliders;public string obstacleHash,surfaceHash,groundSampleHash,windmillHash;public int obstacleCount,groundSamples;
        }
        [Serializable] public sealed class Report
        {
            public int hole;public string scope="Synchronous editor capture immediately before/after visual dressers; exact source geometry plus source HEAD preservation are separate evidence";
            public string gate;public State before,after;public bool colliderCountExact,collidersExact,obstaclesExact,surfaceExact,groundSamplesExact,sourceReferencesExact,collisionMeshesReadable,windmillExact;
            public int originalGroundMeshFilters,newVisualMeshFilters;public string[] changedOriginalSourceFilters,visualMeshesReusingCollisionSource;
        }
        public sealed class Token
        {
            internal State before;internal Collider[] originalColliders;internal MeshFilter[] groundFilters;internal Mesh[] sourceMeshes;internal ISurface surface;internal Func<CoursePoint,double> ground;internal Obstacle[] obstacles;internal SpinningSails windmill;internal int rootId;
        }
        public static Token Begin(GameObject root,Hole hole)
        {
            if(!Enabled&&Environment.GetEnvironmentVariable("GOLF_VISUAL_PHYSICS_GATE")!="1")return null;
            var colliders=root.GetComponentsInChildren<Collider>(true);var filters=new List<MeshFilter>();var sources=new List<Mesh>();
            foreach(var c in colliders)if(c is MeshCollider mc&&mc.sharedMesh&&c.TryGetComponent<MeshFilter>(out var mf)){filters.Add(mf);sources.Add(mf.sharedMesh);}
            return new Token{before=Capture(root,hole),originalColliders=colliders,groundFilters=filters.ToArray(),sourceMeshes=sources.ToArray(),surface=hole.Surface,ground=hole.Ground,obstacles=hole.Obstacles,windmill=hole.Windmill,rootId=root.GetInstanceID()};
        }
        public static void End(Token token,GameObject root,Hole hole)
        {
            if(token==null)return;
            var after=Capture(root,hole);var report=new Report{hole=hole.Number,before=token.before,after=after,originalGroundMeshFilters=token.groundFilters.Length};
            report.colliderCountExact=token.before.colliders.Length==after.colliders.Length;
            report.collidersExact=report.colliderCountExact&&JsonUtility.ToJson(new ColliderList{items=token.before.colliders})==JsonUtility.ToJson(new ColliderList{items=after.colliders});
            report.obstaclesExact=token.before.obstacleHash==after.obstacleHash;
            report.surfaceExact=token.before.surfaceHash==after.surfaceHash;report.windmillExact=token.before.windmillHash==after.windmillHash;report.collisionMeshesReadable=true;foreach(var c in after.colliders)if(c.meshId!=0&&!c.readable)report.collisionMeshesReadable=false;
            report.groundSamplesExact=token.before.groundSampleHash==after.groundSampleHash&&token.before.groundSamples==after.groundSamples;
            var changed=new List<string>();var reused=new List<string>();var ids=new HashSet<int>();
            for(int i=0;i<token.groundFilters.Length;i++){
                var mf=token.groundFilters[i];if(token.sourceMeshes[i])ids.Add(token.sourceMeshes[i].GetInstanceID());
                if(!mf||mf.sharedMesh!=token.sourceMeshes[i])changed.Add(mf?PathOf(root.transform,mf.transform):"Destroyed original ground MeshFilter");
            }
            foreach(var mf in root.GetComponentsInChildren<MeshFilter>(true)){
                bool visual=mf.name.StartsWith("RESORT_")||mf.name.StartsWith("GOLF_SURFACE_BATCH_")||mf.name.StartsWith("GOLF_PLANT_BATCH_");
                if(!visual||!mf.sharedMesh)continue;report.newVisualMeshFilters++;
                if(ids.Contains(mf.sharedMesh.GetInstanceID()))reused.Add(PathOf(root.transform,mf.transform));
            }
            report.changedOriginalSourceFilters=changed.ToArray();report.visualMeshesReusingCollisionSource=reused.ToArray();
            report.sourceReferencesExact=changed.Count==0&&reused.Count==0&&ReferenceEquals(hole.Surface,token.surface)&&hole.Ground==token.ground&&ReferenceEquals(hole.Obstacles,token.obstacles)&&ReferenceEquals(hole.Windmill,token.windmill)&&root.GetInstanceID()==token.rootId;
            report.gate=report.colliderCountExact&&report.collidersExact&&report.obstaclesExact&&report.surfaceExact&&report.groundSamplesExact&&report.sourceReferencesExact&&report.collisionMeshesReadable&&report.windmillExact?"PASS":"FAIL";
            Reports[hole.Number]=report;Debug.Log($"[GolfVisualPhysicsGate] hole {hole.Number}: {report.gate}; {after.colliders.Length} source colliders, {after.obstacleCount} obstacles, {after.groundSamples} actual ground samples; {report.newVisualMeshFilters} distinct visual meshes");
        }
        [Serializable] sealed class ColliderList {public ColliderRecord[] items;}
        static State Capture(GameObject root,Hole hole)
        {
            Physics.SyncTransforms();var records=new List<ColliderRecord>();
            foreach(var c in root.GetComponentsInChildren<Collider>(true)){
                var record=new ColliderRecord{path=PathOf(root.transform,c.transform),type=c.GetType().Name,componentId=c.GetInstanceID(),enabled=c.enabled,active=c.gameObject.activeInHierarchy,trigger=c.isTrigger};
                var matrix=c.transform.localToWorldMatrix;record.transformHash=Hash(w=>{for(int i=0;i<16;i++)w.Write(matrix[i]);});
                record.shapeHash=Hash(w=>{w.Write(c.contactOffset);w.Write(c.sharedMaterial?c.sharedMaterial.GetInstanceID():0);if(c.sharedMaterial){w.Write(c.sharedMaterial.dynamicFriction);w.Write(c.sharedMaterial.staticFriction);w.Write(c.sharedMaterial.bounciness);w.Write((int)c.sharedMaterial.frictionCombine);w.Write((int)c.sharedMaterial.bounceCombine);}
                    if(c is MeshCollider mc){w.Write(mc.convex);w.Write((int)mc.cookingOptions);}else if(c is BoxCollider b){Vector(w,b.center);Vector(w,b.size);}else if(c is SphereCollider s){Vector(w,s.center);w.Write(s.radius);}else if(c is CapsuleCollider cp){Vector(w,cp.center);w.Write(cp.radius);w.Write(cp.height);w.Write(cp.direction);}});
                if(c is MeshCollider collider&&collider.sharedMesh){var m=collider.sharedMesh;record.meshId=m.GetInstanceID();record.vertices=m.vertexCount;for(int sub=0;sub<m.subMeshCount;sub++)record.triangles+=(int)m.GetIndexCount(sub)/3;record.readable=m.isReadable;record.meshHash=MeshHash(m);}
                records.Add(record);
            }
            records.Sort((a,b)=>a.componentId.CompareTo(b.componentId));int samples=0;
            var ground=Hash(w=>{
                void sample(CoursePoint p){w.Write(p.X);w.Write(p.D);w.Write(hole.Ground!=null?hole.Ground(p):0);samples++;}
                sample(hole.Tee);sample(hole.Pin);foreach(var p in hole.Centerline){sample(p);sample(new CoursePoint(p.X-hole.FairwayWidth*.45,p.D));sample(new CoursePoint(p.X+hole.FairwayWidth*.45,p.D));}
                double reach=hole.GreenRadius+12;for(int y=0;y<=16;y++)for(int x=0;x<=16;x++)sample(new CoursePoint(hole.Pin.X-reach+2*reach*x/16,hole.Pin.D-reach+2*reach*y/16));
            });
            return new State{colliders=records.ToArray(),obstacleCount=hole.Obstacles?.Length??0,obstacleHash=Hash(w=>{var obs=hole.Obstacles??Array.Empty<Obstacle>();w.Write(obs.Length);foreach(var o in obs){w.Write((int)o.Kind);w.Write(o.X);w.Write(o.D);w.Write(o.Base);w.Write(o.Top);w.Write(o.Radius);w.Write(o.TrunkRadius);w.Write(o.CrownBase);w.Write(o.Cone);}}),surfaceHash=SurfaceHash(hole.Surface,hole),windmillHash=Hash(w=>{var sails=hole.Windmill;w.Write(sails!=null);if(sails!=null){foreach(var field in typeof(SpinningSails).GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)){var value=field.GetValue(sails);if(value is double d){w.Write(field.Name);w.Write(d);}else if(value is double[] values){w.Write(field.Name);w.Write(values.Length);foreach(var v in values)w.Write(v);}}}}),groundSampleHash=ground,groundSamples=samples};
        }
        static string SurfaceHash(ISurface surface,Hole hole)=>Hash(w=>{
            w.Write(surface?.GetType().FullName??"null");
            if(surface is HeightGrid){
                var fields=typeof(HeightGrid).GetFields(BindingFlags.Instance|BindingFlags.NonPublic);Array.Sort(fields,(a,b)=>string.CompareOrdinal(a.Name,b.Name));
                foreach(var field in fields){w.Write(field.Name);var value=field.GetValue(surface);if(value is double d)w.Write(d);else if(value is int i)w.Write(i);else if(value is double[] values){w.Write(values.Length);foreach(var v in values)w.Write(v);}else throw new InvalidOperationException("Unexpected HeightGrid field "+field.Name);}
            }else if(surface is Contours c){w.Write(c.TiltX);w.Write(c.TiltD);w.Write(c.Features.Count);foreach(var f in c.Features){w.Write(f.Center.X);w.Write(f.Center.D);w.Write(f.Radius);w.Write(f.Height);w.Write(f.End.HasValue);if(f.End is CoursePoint p){w.Write(p.X);w.Write(p.D);}}}
            if(surface!=null)for(int y=-8;y<=8;y++)for(int x=-8;x<=8;x++){var p=new CoursePoint(hole.Pin.X+x*3,hole.Pin.D+y*3);w.Write(surface.Height(p));var g=surface.Gradient(p);w.Write(g.dx);w.Write(g.dd);}
        });
        static string MeshHash(Mesh mesh)=>Hash(w=>{w.Write(mesh.vertexCount);w.Write(mesh.subMeshCount);if(!mesh.isReadable){w.Write("UNREADABLE");return;}foreach(var p in mesh.vertices)Vector(w,p);for(int sub=0;sub<mesh.subMeshCount;sub++){w.Write((int)mesh.GetTopology(sub));var indices=mesh.GetIndices(sub);w.Write(indices.Length);foreach(int index in indices)w.Write(index);}});
        static void Vector(BinaryWriter writer,Vector3 value){writer.Write(value.x);writer.Write(value.y);writer.Write(value.z);}
        static string Hash(Action<BinaryWriter> write){using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))write(writer);using var hash=SHA256.Create();return BitConverter.ToString(hash.ComputeHash(stream.ToArray())).Replace("-","").ToLowerInvariant();}
        static string PathOf(Transform root,Transform at){var names=new List<string>();while(at&&at!=root){names.Add(at.name);at=at.parent;}names.Reverse();return string.Join("/",names);}
    }
}
#endif

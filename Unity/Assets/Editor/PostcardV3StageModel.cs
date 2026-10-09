// Proof-only replacement of the currently loaded HoleView's Course model.
// Uses unchanged scoring/marker alignment; never saves a model or scene asset.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using GolfArcade.Course;
namespace GolfArcade.EditorTools
{
    public static class PostcardV3StageModel
    {
        [Serializable] public sealed class Provenance
        {
            public string status="FAIL",reason,asset,sha256,alignment="Unchanged HoleView.BuildFromModel after actual GolfGame.StartHole; only original Course model removed";
            public int hole,holeViews,courseModels,sourceMeshes,staleInstalledMeshes;public string[] meshAssetPaths;
            public float[] tee,pin,up,rootPosition,rootScale;public float teeResidualXZ,pinResidualXZ,upTiltDegrees;
        }
        static float[] A(Vector3 v)=>new[]{v.x,v.y,v.z};
        public static Provenance SwapCurrent(int number,string assets,string outDir,string shot)
        {
            var p=new Provenance{hole=number};
            try {
                if(string.IsNullOrEmpty(assets)||!assets.StartsWith("Assets/Editor/PostcardV3Scratch/",StringComparison.Ordinal))throw new InvalidOperationException("GOLF_PROOF_STAGE_ASSETS must name the approved scratch import asset folder");
                var view=HoleView.Current;if(!view||view.Hole.Number!=number)throw new InvalidOperationException("Actual requested HoleView is not current");
                if(UnityEngine.Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length!=1)throw new InvalidOperationException("Exactly one settled HoleView required before staged swap");
                p.asset=assets.TrimEnd('/')+"/hole_"+number.ToString("D2")+".fbx";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(p.asset);if(!prefab)throw new InvalidOperationException("Already imported staged FBX missing: "+p.asset);
                foreach(var name in new[]{"MARKER_TEE","MARKER_PIN","MARKER_UP"})if(prefab.GetComponentsInChildren<Transform>(true).Count(t=>t.name==name)!=1)throw new InvalidOperationException("Staged model requires exactly one "+name);
                var build=typeof(HoleView).GetMethod("BuildFromModel",BindingFlags.Instance|BindingFlags.NonPublic,null,new[]{typeof(GameObject)},null);if(build==null||build.ReturnType!=typeof(void))throw new InvalidOperationException("Unchanged HoleView.BuildFromModel API missing/version mismatch");
                var baseline=view.transform.Find("Course model");if(!baseline)throw new InvalidOperationException("Original Course model missing; no primitive fallback permitted");UnityEngine.Object.DestroyImmediate(baseline.gameObject);
                try{build.Invoke(view,new object[]{prefab});}catch(TargetInvocationException e){throw e.InnerException??e;}
                var model=view.transform.Find("Course model");if(!model)throw new InvalidOperationException("BuildFromModel did not produce staged Course model");var transforms=model.GetComponentsInChildren<Transform>(true);Vector3 Marker(string name)=>transforms.Single(t=>t.name==name).position;
                Vector3 tee=Marker("MARKER_TEE"),pin=Marker("MARKER_PIN"),up=Marker("MARKER_UP");var meshes=model.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.sharedMesh).ToArray();
                p.holeViews=UnityEngine.Object.FindObjectsByType<HoleView>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length;p.courseModels=view.transform.Cast<Transform>().Count(t=>t.name=="Course model");p.sourceMeshes=meshes.Count(m=>AssetDatabase.GetAssetPath(m.sharedMesh)==p.asset);
                p.staleInstalledMeshes=UnityEngine.Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(m=>m.sharedMesh&&AssetDatabase.GetAssetPath(m.sharedMesh).StartsWith("Assets/Resources/Course/hole_",StringComparison.Ordinal));
                p.meshAssetPaths=meshes.Select(m=>AssetDatabase.GetAssetPath(m.sharedMesh)).Distinct().OrderBy(x=>x).ToArray();p.tee=A(tee);p.pin=A(pin);p.up=A(up);p.rootPosition=A(model.position);p.rootScale=A(model.lossyScale);
                p.teeResidualXZ=Vector2.Distance(new Vector2(tee.x,tee.z),new Vector2((float)view.Hole.Tee.X,(float)view.Hole.Tee.D));p.pinResidualXZ=Vector2.Distance(new Vector2(pin.x,pin.z),new Vector2((float)view.Hole.Pin.X,(float)view.Hole.Pin.D));p.upTiltDegrees=Vector3.Angle(up-tee,Vector3.up);
                using(var sha=SHA256.Create())p.sha256=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,p.asset)))).Replace("-","").ToLowerInvariant();
                bool valid=p.holeViews==1&&p.courseModels==1&&p.sourceMeshes>0&&p.staleInstalledMeshes==0;p.status=valid?"PASS":"FAIL";p.reason="Source/isolation only. Actual marker alignment residuals retained; exact frozen scoring/markers are checked separately, with no invented residual tolerance.";
                if(!valid)throw new InvalidOperationException("Staged source/isolation FAIL");return p;
            }catch(Exception e){p.status="FAIL";p.reason=e.ToString();throw;}
            finally{File.WriteAllText(Path.Combine(outDir,shot+".stage_provenance.json"),JsonUtility.ToJson(p,true)+"\n");}
        }
    }
}

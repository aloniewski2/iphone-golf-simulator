using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GolfArcade.EditorTools
{
    [InitializeOnLoad] public static class TropicalArenaReview
    {
        static double started; static Vector3 position;
        static TropicalArenaReview(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            AssetDatabase.Refresh();
            TropicalArenaImporter.Prepare();
            if(Directory.Exists("Assets/Resources/Tennis/KitsV3")) foreach(var path in Directory.GetFiles("Assets/Resources/Tennis/KitsV3","*.fbx")) AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/Resources/Tennis/TropicalV3/TropicalTennisResort.fbx",ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetInt("TropicalReview",1);EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            int stage=SessionState.GetInt("TropicalReview",0);
            if(stage==0 || !EditorApplication.isPlaying) return;
            var game=Object.FindFirstObjectByType<TennisGame>();
            if(!game || !game.Initialized) return;
            var walker=GameObject.Find("Resort walker 1");
            if(stage==1){started=EditorApplication.timeSinceStartup;position=walker.transform.position;SessionState.SetInt("TropicalReview",2);return;}
            if(EditorApplication.timeSinceStartup-started<3) return;
            Directory.CreateDirectory("Library/TropicalReview");
            var arena=GameObject.Find("Tropical tennis resort v3 — live arena");
            int textured=arena.GetComponentsInChildren<Renderer>().Count(r=>r.sharedMaterials.Any(m=>m && m.mainTexture));
            bool moved=Vector3.Distance(position,walker.transform.position)>.05f;
            int count=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Count(t=>t.name.StartsWith("Resort walker "));
            File.WriteAllText("Library/TropicalReview/result.txt",$"scene={game.gameObject.scene.name}\narena={arena.name}\ntexturedRenderers={textured}\nwalkers={count}\nwalkerMoved={moved}\n");
            Capture(game.GameplayCamera,"gameplay.png");
            game.GameplayCamera.transform.position=new Vector3(-39,28,-43);
            game.GameplayCamera.transform.LookAt(new Vector3(2,0,4));
            Capture(game.GameplayCamera,"overview.png");
            bool kitPass=true;
            game.ManualSimulation=true;
            foreach(var actor in new[]{game.Player,game.Opponent})
            {
                bool female=actor==game.Opponent;
                actor.Tick(2,0);
                var c=game.GameplayCamera;
                c.transform.position=actor.transform.TransformPoint(new Vector3(1.5f,1.5f,3));
                c.transform.LookAt(actor.transform.TransformPoint(new Vector3(0,1,0)));c.fieldOfView=38;
                Capture(c,(female?"female":"male")+"-kit-ready.png");
                foreach(var view in new[]{(label:"front",offset:new Vector3(0,1.65f,1.7f)),(label:"side",offset:new Vector3(1.7f,1.65f,0)),(label:"back",offset:new Vector3(0,1.65f,-1.7f))}) {
                    c.transform.position=actor.transform.TransformPoint(view.offset);c.transform.LookAt(actor.transform.TransformPoint(new Vector3(0,1.55f,0)));
                    Capture(c,(female?"female":"male")+"-visor-"+view.label+".png");
                }
                c.transform.position=actor.transform.TransformPoint(new Vector3(1.5f,1.5f,3));
                c.transform.LookAt(actor.transform.TransformPoint(new Vector3(0,1,0)));
                actor.Swing(.65f,false);actor.Tick(.25f,0);
                Capture(c,(female?"female":"male")+"-kit-forehand.png");
                actor.Tick(2,0);actor.Swing(.8f,true);actor.Tick(.3f,0);
                Capture(c,(female?"female":"male")+"-kit-backhand.png");
                var kit=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="Fitted Tripo tennis kit v3");
                foreach(var r in kit.GetComponentsInChildren<Renderer>()) File.AppendAllText("Library/TropicalReview/kit-bounds.txt",$"{r.name} scale={r.transform.lossyScale} bounds={r.bounds} parent={r.transform.parent.name}\n");
                File.AppendAllText("Library/TropicalReview/result.txt",$"{(female?"female":"male")} kitSkinnedMeshes={kit.GetComponentsInChildren<SkinnedMeshRenderer>().Length}\n");
                kitPass &= actor.RacketGripError<.005f;
            }
            var leftProperty=typeof(GolfArcade.Game.NativeSportsSession).GetProperty("Left");
            bool originalLeft=GolfArcade.Game.NativeSportsSession.Left;
            foreach(bool left in new[]{false,true}) foreach(bool female in new[]{false,true}) {
                leftProperty.SetValue(null,left);
                var actor=new GameObject("Kit handedness verification").AddComponent<TennisActor>();
                actor.Build(female,Color.white);
                float maxGrip=0;
                foreach(int pose in new[]{0,1,2,3,4}) {
                    actor.Tick(2,0);
                    if(pose==1)actor.Swing(.3f,false);if(pose==2)actor.Swing(.9f,true);if(pose==3)actor.Serve(.7f);
                    for(int frame=0;frame<60;frame++){actor.Tick(1f/60,pose==4?3:0);maxGrip=Mathf.Max(maxGrip,actor.RacketGripError);}
                }
                kitPass &= maxGrip<.005f;
                File.AppendAllText("Library/TropicalReview/result.txt",$"female={female} left={left} maxGripErrorMetres={maxGrip}\n");
                Object.DestroyImmediate(actor.gameObject);
            }
            leftProperty.SetValue(null,originalLeft);
            SessionState.SetInt("TropicalReview",0);
            EditorApplication.Exit(textured>0 && moved && kitPass?0:1);
        }
        static void Capture(Camera c,string name)
        {
            // Several poses are evaluated in this editor tick. Bake the current CPU pose
            // for capture, rather than reusing Unity's GPU skinning from the previous frame.
            var baked=new System.Collections.Generic.List<(SkinnedMeshRenderer original,GameObject copy,Mesh mesh)>();
            foreach(var r in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) if(r.enabled && r.gameObject.activeInHierarchy) {
                var mesh=new Mesh();r.BakeMesh(mesh);
                var copy=new GameObject("Current pose capture");copy.transform.SetParent(r.transform,false);
                var scale=r.transform.lossyScale;
                copy.transform.localScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z));
                copy.AddComponent<MeshFilter>().sharedMesh=mesh;copy.AddComponent<MeshRenderer>().sharedMaterials=r.sharedMaterials;
                r.enabled=false;baked.Add((r,copy,mesh));
            }
            var rt=new RenderTexture(1600,900,24);c.targetTexture=rt;c.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();
            File.WriteAllBytes("Library/TropicalReview/"+name,tex.EncodeToPNG());c.targetTexture=null;RenderTexture.active=null;
            Object.DestroyImmediate(tex);Object.DestroyImmediate(rt);
            foreach(var b in baked){b.original.enabled=true;Object.DestroyImmediate(b.copy);Object.DestroyImmediate(b.mesh);}
        }
    }
}

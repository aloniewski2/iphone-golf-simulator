#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    [InitializeOnLoad] public static class TennisAudienceProof
    {
        const string Flag="TennisAudienceProof";static readonly Stack<IEnumerator> steps=new();static int last=-1;
        static TennisAudienceProof(){EditorApplication.update+=Tick;}
        public static void Run(){SessionState.SetBool(Flag,true);TennisVenue.Selected=TennisVenueKind.Resort;EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");EditorApplication.isPlaying=true;}
        static void Finish(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;steps.Clear();if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||last==Time.frameCount)return;
            var game=Object.FindFirstObjectByType<TennisGame>();if(!game||!game.Initialized)return;last=Time.frameCount;
            try
            {
                if(steps.Count==0)steps.Push(Go(game));
                while(steps.Count>0){var next=steps.Peek();if(!next.MoveNext()){steps.Pop();continue;}if(next.Current is IEnumerator child){steps.Push(child);continue;}return;}
                Finish(0);
            }
            catch(Exception e){Debug.LogException(e);Finish(1);}
        }
        static Bounds BoundsOf(Transform t)
        {
            var renderers=t.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject.activeInHierarchy).ToArray();
            if(renderers.Length==0)throw new InvalidOperationException("No evaluated audience meshes on "+t.name);
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;
        }
        static IEnumerator Shot(Camera camera,string path,Bounds bounds,bool seated,Transform subject)
        {
            var at=bounds.center;float distance=bounds.size.y/(2*Mathf.Tan(17.5f*Mathf.Deg2Rad))*1.22f;
            var from=at+(seated?new Vector3(distance,.12f,distance*.08f):(subject.forward+subject.right*.12f)*distance+Vector3.up*.14f);
            var isolated=Environment.GetEnvironmentVariable("TAP_ISOLATE")=="1";
            int oldMask=camera.cullingMask;var oldFlags=camera.clearFlags;var oldColour=camera.backgroundColor;
            var pieces=subject.GetComponentsInChildren<Renderer>(true);var layers=new int[pieces.Length];
            if(isolated)
            {
                camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.29f,.33f);
                for(int i=0;i<pieces.Length;i++){layers[i]=pieces[i].gameObject.layer;pieces[i].gameObject.layer=30;}
            }
            camera.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from));camera.fieldOfView=35;
            for(int k=0;k<3;k++)yield return null;
            var rt=RenderTexture.GetTemporary(800,800,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var old=camera.targetTexture;var active=RenderTexture.active;camera.targetTexture=rt;camera.Render();camera.targetTexture=old;RenderTexture.active=rt;
            var tex=new Texture2D(800,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,800,800),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
            if(isolated){for(int i=0;i<pieces.Length;i++)pieces[i].gameObject.layer=layers[i];camera.cullingMask=oldMask;camera.clearFlags=oldFlags;camera.backgroundColor=oldColour;}
        }
        static IEnumerator Go(TennisGame game)
        {
            var output=Path.GetFullPath(Environment.GetEnvironmentVariable("TAP_OUT")??"../proof/full-visual-overhaul/tennis-audience-proof");Directory.CreateDirectory(output);Time.captureFramerate=60;
            var presentation=game.GetComponent<TennisPresentation>();if(presentation)presentation.Finish();game.enabled=false;
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
            for(int k=0;k<15;k++)yield return null;
            var crowd=game.GetComponent<TennisStandsCrowd>();var walk=game.GetComponent<TennisResortCrowd>();if(!crowd||!walk)throw new InvalidOperationException("Resort audience components missing");
            crowd.FreezePerformance=true;walk.FreezePerformance=true;
            bool instanced=crowd.InstancedSubmission;if(Environment.GetEnvironmentVariable("TAP_ISOLATE")=="1")crowd.SetInstancedSubmission(false);
            var selected=new Transform[6];
            foreach(var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if(!t.name.StartsWith("Seated sporting fan "))continue;
                var variant=t.GetComponentsInChildren<Transform>().FirstOrDefault(v=>v.name.StartsWith("FAN_"));if(!variant)continue;
                int id=int.Parse(variant.name.Substring(4));if(selected[id]==null||t.position.y<selected[id].position.y)selected[id]=t;
            }
            var camera=game.GameplayCamera;
            for(int detail=0;detail<2;detail++)
            {
                crowd.DetailOverride=detail;walk.DetailOverride=detail;
                for(int i=0;i<6;i++)
                {
                    if(!selected[i])throw new InvalidOperationException("Seated variant missing "+i);
                    yield return Shot(camera,Path.Combine(output,"seated_"+i+"_"+(detail==0?"near":"far")+"_idle.png"),BoundsOf(selected[i]),true,selected[i]);
                    var visitor=GameObject.Find("Premium promenade visitor "+i);if(!visitor)throw new InvalidOperationException("Visitor missing "+i);
                    yield return Shot(camera,Path.Combine(output,"visitor_"+i+"_"+(detail==0?"near":"far")+"_walk.png"),BoundsOf(visitor.transform),false,visitor.transform);
                }
            }
            crowd.FreezePerformance=false;walk.FreezePerformance=false;crowd.Cheer(1);walk.Cheer(1);
            for(int k=0;k<62;k++)yield return null;
            crowd.FreezePerformance=true;walk.FreezePerformance=true;
            for(int detail=0;detail<2;detail++)
            {
                crowd.DetailOverride=detail;walk.DetailOverride=detail;
                for(int i=0;i<6;i++)
                {
                    yield return Shot(camera,Path.Combine(output,"seated_"+i+"_"+(detail==0?"near":"far")+"_cheer.png"),BoundsOf(selected[i]),true,selected[i]);
                    var visitor=GameObject.Find("Premium promenade visitor "+i);
                    yield return Shot(camera,Path.Combine(output,"visitor_"+i+"_"+(detail==0?"near":"far")+"_cheer.png"),BoundsOf(visitor.transform),false,visitor.transform);
                }
            }
            crowd.SetInstancedSubmission(instanced);crowd.DetailOverride=-1;walk.DetailOverride=-1;crowd.FreezePerformance=false;walk.FreezePerformance=false;
            var inventory=TennisNpcInventory.Save(output);if(inventory.status!="PASS")throw new InvalidOperationException("Actual audience replacement inventory FAIL");
            File.WriteAllText(Path.Combine(output,"scope.txt"),"Evaluated production36seated +6walkers; each of six real variants at forced near/far, idle/moving and actual raised-arm cheering after62real60fpsframes. Camera waits3actualframes before capture. Detail overrides are diagnostics only; ordinary rendering uses actual screen-size/frustum selection. Original legacy audience draw inventory stored separately. TAP_ISOLATE=1 excludes other geometry only for the close diagnostic images, with unchanged production source pose/material/lights and real front-facing garment views; full-world composition/costs are evaluated separately. Visual judgement pending.\n");
            Debug.Log("[TennisAudienceProof] actual near/far/raised-arm captures complete "+output);
        }
    }
}
#endif

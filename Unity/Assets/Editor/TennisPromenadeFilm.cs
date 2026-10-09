#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    [InitializeOnLoad] public static class TennisPromenadeFilm
    {
        const string Flag="TennisPromenadeFilm";static IEnumerator script;static int lastFrame=-1;
        static TennisPromenadeFilm(){EditorApplication.update+=Tick;}
        public static void Run(){TennisVenue.Selected=TennisVenueKind.Resort;EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");SessionState.SetBool(Flag,true);EditorApplication.isPlaying=true;}
        static void Tick(){if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||lastFrame==Time.frameCount)return;lastFrame=Time.frameCount;var g=Object.FindFirstObjectByType<TennisGame>();if(!g||!g.Initialized)return;if(script==null)script=Go(g);try{if(!script.MoveNext())Done(0);}catch(Exception e){Debug.LogException(e);Done(1);}}
        static void Done(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;if(Application.isBatchMode)EditorApplication.Exit(code);else EditorApplication.isPlaying=false;}
        static Transform Part(Transform root,string prefix){foreach(var t in root.GetComponentsInChildren<Transform>())if(t.name.StartsWith(prefix,StringComparison.Ordinal))return t;return null;}
        static string P(Vector3 p)=>$"{p.x:F5};{p.y:F5};{p.z:F5}";
        static void Shot(Camera cam,string file,Transform actor)
        {
            var pos=cam.transform.position;var rot=cam.transform.rotation;var fov=cam.fieldOfView;
            var from=actor.TransformPoint(2.4f,1.35f,3.0f);var at=actor.TransformPoint(0,.88f,0);cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from));cam.fieldOfView=37;
            var rt=RenderTexture.GetTemporary(640,640,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var old=cam.targetTexture;var active=RenderTexture.active;cam.targetTexture=rt;cam.Render();cam.targetTexture=old;RenderTexture.active=rt;
            var tex=new Texture2D(640,640,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,640,640),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());Object.DestroyImmediate(tex);RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);cam.transform.SetPositionAndRotation(pos,rot);cam.fieldOfView=fov;
        }
        static void Grid(Camera camera,string path,Transform[] actors)
        {
            const int width=320,height=480;
            var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView,aspect=camera.aspect;
            var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var target=camera.targetTexture;var active=RenderTexture.active;
            var image=new Texture2D(width*3,height*2,TextureFormat.RGB24,false);
            camera.targetTexture=rt;camera.fieldOfView=37;camera.aspect=(float)width/height;
            bool isolated=Environment.GetEnvironmentVariable("TWALK_ISOLATE")=="1";int oldMask=camera.cullingMask;var flags=camera.clearFlags;var colour=camera.backgroundColor;
            if(isolated){camera.cullingMask=1<<30;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.22f,.29f,.33f);}
            for(int i=0;i<actors.Length;i++)
            {
                var pieces=actors[i].GetComponentsInChildren<Renderer>(true);var layers=new int[pieces.Length];
                if(isolated)for(int k=0;k<pieces.Length;k++){layers[k]=pieces[k].gameObject.layer;pieces[k].gameObject.layer=30;}
                var from=actors[i].TransformPoint(.65f,1.35f,3.6f);var at=actors[i].TransformPoint(0,.88f,0);
                camera.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from));camera.Render();RenderTexture.active=rt;
                image.ReadPixels(new Rect(0,0,width,height),(i%3)*width,(1-i/3)*height,false);
                if(isolated)for(int k=0;k<pieces.Length;k++)pieces[k].gameObject.layer=layers[k];
            }
            image.Apply(false);File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);
            camera.targetTexture=target;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.aspect=aspect;camera.cullingMask=oldMask;camera.clearFlags=flags;camera.backgroundColor=colour;
        }
        static IEnumerator Go(TennisGame game)
        {
            var output=Path.GetFullPath(Environment.GetEnvironmentVariable("TWALK_OUT")??"../proof/full-visual-overhaul/tennis-promenade-film");Directory.CreateDirectory(output);Time.captureFramerate=60;
            var presentation=game.GetComponent<TennisPresentation>();if(presentation)presentation.Finish();
            typeof(TennisGame).GetProperty("Flow").GetSetMethod(true).Invoke(game,new object[]{TennisGame.Phase.PointOver});typeof(TennisGame).GetField("resetTimer",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,1e6f);
            int start=Time.frameCount;while(Time.frameCount-start<8)yield return null;
            var walkers=game.GetComponent<TennisResortCrowd>();walkers.DetailOverride=0;
            // Diagnostic near-detail film evaluates the actual eleven-joint continuous skinned visitors.
            // Ordinary automatic detail is measured separately by FrameCosts.
            int detailFrame=Time.frameCount;while(Time.frameCount-detailFrame<3)yield return null;
            var inventory=TennisNpcInventory.Save(output);if(inventory.status!="PASS")throw new InvalidOperationException("Promenade production inventory failed");
            var actors=new Transform[6];for(int i=0;i<6;i++){actors[i]=GameObject.Find("Premium promenade visitor "+i)?.transform;if(!actors[i])throw new InvalidOperationException("Visitor "+i+" missing");Shot(game.GameplayCamera,Path.Combine(output,"visitor_"+i+"_standing.png"),actors[i]);}
            var trace=new List<string>{"frame,unityFrame,visitor,actor,forward,leftAnkle,rightAnkle,leftKnee,rightKnee"};
            // Eight seconds of the deterministic natural crowd path. The trace records
            // exact actor/ankle/knee/facing movement and absolute Time.frameCount; no root or clock is set here.
            for(int frame=0;frame<480;frame++)
            {
                int before=Time.frameCount;do{yield return null;}while(Time.frameCount==before);
                for(int i=0;i<6;i++)trace.Add($"{frame},{Time.frameCount},{i},{P(actors[i].position)},{P(actors[i].forward)},{P(Part(actors[i],"FOOT_L").position)},{P(Part(actors[i],"FOOT_R").position)},{P(Part(actors[i],"SHIN_L").position)},{P(Part(actors[i],"SHIN_R").position)}");
                if(frame%2==0){string id=(frame/2).ToString("00000");GameCapture.Save(Path.Combine(output,"game_"+id+".png"),960,540);Grid(game.GameplayCamera,Path.Combine(output,"visitors_"+id+".png"),actors);}
            }
            walkers.DetailOverride=-1;
            File.WriteAllLines(Path.Combine(output,"trace.csv"),trace);File.WriteAllText(Path.Combine(output,"audit.txt"),"Actual six independent fitted continuous11-bone visitors; natural path,60fps evaluated and30fps captured. Grid row1 variants0/1/2, row2 variants3/4/5; allsix moving joints are filmed concurrently. Gameplay capture retains the ordinary world and HUD. Near detail is forced only in this close diagnostic film. No actor/pose/clock override. TWALK_ISOLATE=1 gives unobstructed actual cloth/joint views only in the grid, retaining actual material/light/pose and complete normal gameplay frames. Actual gait/garment visual review required; automatic near/far selection is evaluated separately in FrameCosts.\n");
        }
    }
}
#endif

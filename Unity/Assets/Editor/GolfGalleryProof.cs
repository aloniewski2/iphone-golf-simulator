#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GolfArcade.Course;
using GolfArcade.Game;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    [InitializeOnLoad] public static class GolfGalleryProof
    {
        const string Flag="GolfGalleryProof";static IEnumerator script;static int lastFrame=-1;
        static GolfGalleryProof(){EditorApplication.update+=Tick;}
        public static void Run(){SessionState.SetBool(Flag,true);EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");EditorApplication.isPlaying=true;}
        static void Tick(){
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying||Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
            var game=Object.FindFirstObjectByType<GolfGame>();if(!game||game.Swing==null)return;
            try{if(script==null)script=Capture(game);if(!script.MoveNext())Done(0);}catch(Exception e){Debug.LogException(e);Done(1);}
        }
        static void Done(int code){SessionState.SetBool(Flag,false);Time.captureFramerate=0;EditorApplication.Exit(code);}
        static void Shot(Camera cam,string file,Vector3 from,Vector3 at,int width=1440,int height=810)
        {
            var pos=cam.transform.position;var rotation=cam.transform.rotation;float fov=cam.fieldOfView,aspect=cam.aspect;
            var old=cam.targetTexture;var active=RenderTexture.active;var rt=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
            var tex=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(at-from));cam.fieldOfView=38;cam.aspect=width/(float)height;cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();File.WriteAllBytes(file,tex.EncodeToPNG());}
            finally{cam.targetTexture=old;RenderTexture.active=active;Object.DestroyImmediate(tex);RenderTexture.ReleaseTemporary(rt);cam.transform.SetPositionAndRotation(pos,rotation);cam.fieldOfView=fov;cam.aspect=aspect;}
        }
        static IEnumerator Capture(GolfGame game)
        {
            var output=Path.GetFullPath(Environment.GetEnvironmentVariable("GGP_OUT")??"../proof/full-visual-overhaul/golf-gallery-runtime");Directory.CreateDirectory(output);Time.captureFramerate=60;Time.timeScale=1;
            game.ChooseHoles(0);game.Play();game.JumpToHole(12);
            game.enabled=false;var rig=Object.FindFirstObjectByType<CameraRig>();if(rig)rig.enabled=false;
            var gallery=Object.FindFirstObjectByType<Gallery>();if(!gallery)throw new Exception("Production Gallery missing.");gallery.gameObject.SetActive(true);
            for(int frame=0;frame<45;frame++)yield return null;
            var fans=gallery.GetComponentsInChildren<GolfGalleryPresentation>();if(fans.Length==0||fans.Length!=gallery.Count)throw new Exception("Standing gallery count invalid.");
            var facing=fans[0].transform.forward;var at=gallery.Centre+Vector3.up*.94f;var from=at+facing*8.4f+Vector3.up*1.1f;
            var cam=Camera.main;Shot(cam,Path.Combine(output,"gallery_idle.png"),from,at);
            var audit=new List<string>{"Actual production standing Golf gallery. No hero skin rig or legacy V4 crowd. Parent visual review and completed-frame cost remain separate.","Count="+gallery.Count+" RopeYards="+Gallery.RopeYards+" Centre="+gallery.Centre};
            int skins=0,animators=0,colliders=0;
            for(int i=0;i<fans.Length;i++){
                var fan=fans[i];skins+=fan.GetComponentsInChildren<SkinnedMeshRenderer>().Length;animators+=fan.GetComponentsInChildren<Animator>().Length;colliders+=fan.GetComponentsInChildren<Collider>().Length;
                audit.Add("fan="+i+" foot="+fan.transform.position+" ground="+HoleView.GroundHeight(HoleView.ToCourse(fan.transform.position))+" pieces="+fan.RigidPieceCount+" nearTriangles="+fan.MeshTriangles+" farTriangles="+fan.DistantTriangles);
                Shot(cam,Path.Combine(output,"fan_"+i+"_front.png"),fan.transform.position+fan.transform.forward*3.4f+Vector3.up*1.12f,fan.transform.position+Vector3.up*.95f,640,900);
                Shot(cam,Path.Combine(output,"fan_"+i+"_face.png"),fan.Head.position+fan.transform.forward*1.05f+Vector3.up*.11f,fan.Head.position+Vector3.up*.14f,640,640);
                foreach(float distance in new[]{3.4f,8f,18f})Shot(cam,Path.Combine(output,"fan_"+i+"_distance_"+distance.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png"),fan.transform.position+fan.transform.forward*distance+Vector3.up*1.12f,fan.transform.position+Vector3.up*.95f,640,900);
            }
            audit.Add("SkinnedMeshRenderers="+skins+" Animators="+animators+" SpectatorColliders="+colliders);if(skins!=0||animators!=0||colliders!=0)throw new Exception("Standing gallery must be rigid visual-only.");
            gallery.Cheer(2.4f);
            var film=Path.Combine(output,"cheer_frames");Directory.CreateDirectory(film);
            for(int frame=0;frame<210;frame++){
                yield return null;
                if(frame%2==0)Shot(cam,Path.Combine(film,"frame_"+(frame/2).ToString("00000")+".png"),from,at,960,540);
                if(frame==15||frame==55||frame==135||frame==185)Shot(cam,Path.Combine(output,"gallery_cheer_"+frame+".png"),from,at);
                if(frame==55)for(int i=0;i<fans.Length;i++){
                    var fan=fans[i];
                    Shot(cam,Path.Combine(output,"fan_"+i+"_cheer_face.png"),fan.Head.position+fan.transform.forward*1.05f+Vector3.up*.11f,fan.Head.position+Vector3.up*.14f,640,640);
                    foreach(float distance in new[]{3.4f,8f,18f})Shot(cam,Path.Combine(output,"fan_"+i+"_cheer_distance_"+distance.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png"),fan.transform.position+fan.transform.forward*distance+Vector3.up*1.12f,fan.transform.position+Vector3.up*.95f,640,900);
                }
            }
            foreach(var fan in fans)fan.Perform("FistPump",.1f);
            for(int frame=0;frame<60;frame++)yield return null;
            Shot(cam,Path.Combine(output,"gallery_fistpump.png"),from,at);
            foreach(var fan in fans)fan.Perform("Idle",.3f);
            for(int frame=0;frame<45;frame++)yield return null;
            Shot(cam,Path.Combine(output,"gallery_recovered.png"),from,at);
            File.WriteAllLines(Path.Combine(output,"audit.txt"),audit);
        }
    }
}
#endif

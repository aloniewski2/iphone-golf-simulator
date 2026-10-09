using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Shot;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace GolfArcade.EditorTools {
    /// Actual production golf figure, source clip landmarks and corrective layers.
    /// Each sampled pose advances across rendered player frames before capture.
    [InitializeOnLoad] public static class VisualOverhaulGolfCharacterCapture {
        const string Key="VisualOverhaulGolfCharacterCapture";
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static readonly Stack<IEnumerator> steps=new();
        static string output;
        static bool started;
        static int warm;
        static readonly List<string> audit=new();
        static VisualOverhaulGolfCharacterCapture(){EditorApplication.update+=Tick;}
        public static void Run(){
            output=Path.GetFullPath(Environment.GetEnvironmentVariable("VISUAL_GOLF_CHARACTER_OUT")??"../proof/full-visual-overhaul/golf-characters-pass-1");
            Directory.CreateDirectory(output);SessionState.SetString(Key+"out",output);SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");EditorApplication.isPlaying=true;
        }
        static void Tick(){
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||++warm<40)return;
            try{
                if(!started){output=SessionState.GetString(Key+"out","");steps.Push(Capture());started=true;}
                while(steps.Count>0){var step=steps.Peek();if(!step.MoveNext()){steps.Pop();continue;}if(step.Current is IEnumerator nested){steps.Push(nested);continue;}return;}
                Finish(0);
            }catch(Exception e){Debug.LogException(e);audit.Add("FAIL: "+e);Finish(1);}
        }
        static void Finish(int result){File.WriteAllLines(Path.Combine(output,"audit.txt"),audit);SessionState.SetBool(Key,false);Time.captureFramerate=0;EditorApplication.Exit(result);}
        static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        static string WristStamp(HeroGolfer hero) {
            var text = new System.Text.StringBuilder();
            foreach (string name in new[]{"LeftUpperArm","LeftLowerArm","LeftHand","LeftMiddleProximal","RightUpperArm","RightLowerArm","RightHand","RightMiddleProximal","Club"}) {
                var b=hero.Bones[name];text.Append(name+" p="+b.position.ToString("F9")+" q="+b.rotation.ToString("F9")+"; ");
            }
            return text.ToString();
        }
        static IEnumerator Capture(){
            Time.captureFramerate=30;QualitySettings.skinWeights=SkinWeights.FourBones;
            var game=Object.FindFirstObjectByType<GolfGame>();if(game)game.enabled=false;
            var rig=Object.FindFirstObjectByType<CameraRig>();if(rig)rig.enabled=false;
            foreach(var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))renderer.enabled=false;
            foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))canvas.enabled=false;
            var cam=Camera.main;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.83f,.89f,.88f);cam.nearClipPlane=.025f;cam.cullingMask=~0;cam.farClipPlane=200;
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.enabled=false;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.61f,.66f);RenderSettings.fog=false;
            void LightAt(string name,Vector3 p,Color c,float power){var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Directional;l.intensity=power;l.color=c;l.transform.position=p;l.transform.LookAt(Vector3.up);}
            LightAt("Golf review key",new Vector3(-3,5,4),new Color(1,.96f,.9f),1.25f);
            LightAt("Golf review fill",new Vector3(3,2,2),new Color(.76f,.88f,1),.55f);
            foreach(bool female in new[]{false,true}){
                string sex=female?"female":"male";
                var golfer=new GameObject("Golf figure review").AddComponent<GolferView>();
                var heroLook=new HeroLook {Female=female,Skin=HeroKit.Hex("C47A4C"),HairColor=Color.black,Haircut=(int)HeroGolfer.Haircut.Bald,Shirt=Color.white,Shorts=HeroKit.Hex("10243D"),Shoes=Color.white};
                typeof(GolferView).GetField("own",Private).SetValue(golfer,new GolferView.Look{Hero=heroLook});
                golfer.ApplyStyle();golfer.Stand(Vector3.zero,Vector3.forward);golfer.enabled=false;
                yield return Frames(3);
                if(!golfer.IsHero)throw new InvalidOperationException("Production golf hero missing "+sex);
                var hero=golfer.Hero;var look=hero.Root.GetComponent<MatchHeroLook>();if(look.GetComponent<MatchHeroFacePerformance>() is MatchHeroFacePerformance face)face.ForcedBlink=0;
                foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.updateWhenOffscreen=true;skin.forceMatrixRecalculationPerRender=true;}
                if(look.GetComponent<MatchHeroFacePerformance>() is MatchHeroFacePerformance golfFace) {
                    var head=look.Bone(HumanBodyBones.Head);
                    foreach(float blink in new[]{0f,.5f,1f}) {
                        golfFace.ForcedBlink=blink;yield return Frames(2);
                        Shot(sex+"_golf_blink_"+Mathf.RoundToInt(blink*100),head.position+hero.Root.transform.TransformDirection(new Vector3(.32f,.06f,1)),head.position+Vector3.up*.04f,24,900,900);
                    }
                    golfFace.ForcedBlink=0;yield return Frames(2);
                }
                audit.Add($"{sex}: root active={hero.Root.activeInHierarchy}, camera mask={cam.cullingMask}, camera active={cam.gameObject.activeInHierarchy}");
                foreach(var renderer in hero.Root.GetComponentsInChildren<Renderer>())audit.Add($"{renderer.name}: enabled={renderer.enabled} active={renderer.gameObject.activeInHierarchy} layer={renderer.gameObject.layer} bounds={renderer.bounds}");
                foreach(var club in new[]{GolfClub.Driver,GolfClub.Iron,GolfClub.PitchingWedge,GolfClub.Wedge,GolfClub.Putter}){
                    var clubs=Environment.GetEnvironmentVariable("VISUAL_GOLF_CHARACTER_CLUBS");
                    if(!string.IsNullOrEmpty(clubs)&&Array.IndexOf(clubs.Split(','),club.ToString())<0)continue;
                    bool shortShot=club==GolfClub.Wedge;golfer.SetClub(club,shortShot);
                    // Review the settled clip; disabling Update also freezes crossfade.
                    typeof(GolferView).GetField("fade",Private).SetValue(golfer,0f);
                    typeof(GolferView).GetMethod("Weigh",Private).Invoke(golfer,null);
                    float Read(string key)=>(float)typeof(GolferView).GetField(key,Private).GetValue(golfer);
                    float top=Read("TopTime"),contact=Read("ImpactTime"),end=Read("EndTime");
                    var times=new[]{0,top,contact,end*.92f};var labels=new[]{"address","top","contact","follow"};
                    for(int i=0;i<times.Length;i++){
                        var stages=Environment.GetEnvironmentVariable("VISUAL_GOLF_CHARACTER_STAGES");
                        if(!string.IsNullOrEmpty(stages)&&Array.IndexOf(stages.Split(','),labels[i])<0)continue;
                        typeof(GolferView).GetMethod("Show",Private).Invoke(golfer,new object[]{times[i]});
                        if(Environment.GetEnvironmentVariable("VISUAL_GOLF_WRIST_AUDIT")=="1")audit.Add(sex+"_"+club+"_"+labels[i]+" before: "+WristStamp(hero));
                        yield return Frames(3);
                        if(Environment.GetEnvironmentVariable("VISUAL_GOLF_WRIST_AUDIT")=="1")audit.Add(sex+"_"+club+"_"+labels[i]+" after: "+WristStamp(hero));
                        foreach(var skin in hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.bounds.size.magnitude>8||float.IsNaN(skin.bounds.center.x))throw new InvalidOperationException("Bad bounds "+skin.name);
                        string stem=sex+"_"+club+"_"+labels[i];
                        if(Environment.GetEnvironmentVariable("VISUAL_GOLF_CHARACTER_POSE_DUMPS")=="1")
                            HeroGarmentPoseSnapshot.Save(look,stem,Path.Combine(output,"poses"));
                        var centre=hero.Root.GetComponent<MatchHeroLook>().body.bounds.center;
                        Shot(stem+"_front34",centre+new Vector3(2.5f,.3f,3.8f),centre,34,900,1200);
                        Shot(stem+"_back34",centre+new Vector3(-2.5f,.3f,-3.8f),centre,34,900,1200);
                        var left=hero.Bones["LeftHand"].position;var right=hero.Bones["RightHand"].position;var hands=(left+right)*.5f;
                        Shot(stem+"_grip",hands+new Vector3(.75f,.3f,1),hands,28,900,900);
                        audit.Add($"{stem}: time={times[i]:F4} top={top:F4} contact={contact:F4} end={end:F4} clubhead={golfer.ClubHeadWorld()}");
                    }
                    if(int.TryParse(Environment.GetEnvironmentVariable("VISUAL_GOLF_CHARACTER_MATRIX_SAMPLES"),out int matrixSamples)){
                        matrixSamples=Mathf.Clamp(matrixSamples,3,121);
                        for(int sample=0;sample<matrixSamples;sample++){
                            float t=end*sample/(matrixSamples-1);
                            typeof(GolferView).GetMethod("Show",Private).Invoke(golfer,new object[]{t});yield return Frames(2);
                            HeroGarmentPoseSnapshot.SaveMatrices(look,$"{sex}_{club}_{sample:000}",t,Path.Combine(output,"matrices"));
                        }
                    }
                }
                Object.Destroy(golfer.gameObject);yield return Frames(2);
            }
            audit.Add("CAPTURE PASS: both production bodies, five stroke families, four actual clip landmarks, front/back/grip; visual review required.");
            void Shot(string name,Vector3 from,Vector3 target,float fov,int w,int h){
                cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(target-from));cam.fieldOfView=fov;cam.aspect=w/(float)h;
                var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);var previous=cam.targetTexture;var active=RenderTexture.active;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
                try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());
                    var pixels=tex.GetPixels32();var corner=pixels[0];int occupied=0;foreach(var p in pixels)if(Math.Abs(p.r-corner.r)+Math.Abs(p.g-corner.g)+Math.Abs(p.b-corner.b)>40)occupied++;
                    if(occupied<1000)throw new InvalidOperationException("Blank or unframed hero capture "+name);
                }
                finally{cam.targetTexture=previous;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
            }
        }
    }
}

#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools
{
    // Diagnostic only: freeze Ready + the actual win reach, compare both anatomical
    // hinge signs, measure the rendered weighted mesh, and save close hand views.
    public static class TennisFistProbe
    {
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string output=Environment.GetEnvironmentVariable("TFP_OUT") ?? Path.GetFullPath(Path.Combine(Application.dataPath,"../../proof/full-visual-overhaul/fist-probe-1"));
            Directory.CreateDirectory(output);var audit=new System.Text.StringBuilder();
            var cam=new GameObject("Fist proof camera").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.20f,.25f,.32f);cam.fieldOfView=30;
            var sun=new GameObject("Fist proof key").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.5f;sun.color=Color.white;sun.transform.rotation=Quaternion.Euler(35,-25,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.55f,.62f,.72f);RenderSettings.ambientEquatorColor=new Color(.38f,.42f,.47f);RenderSettings.ambientGroundColor=new Color(.22f,.24f,.26f);RenderSettings.fog=false;
            foreach(string sex in new[]{"Male","Female"})foreach(int sign in new[]{0,1,-1})
            {
                var root=Object.Instantiate(Resources.Load<GameObject>("Tennis/Customization/Player"+sex));
                var hero=root.GetComponent<MatchHeroLook>();var driver=root.GetComponent<HeroTennisDriver>();driver.enabled=false;
                var ready=driver.slots.First(x=>x.id==HeroTennisDriver.Clip.Ready).clip;
                ready.SampleAnimation(root,.6f);TennisPerformanceBuild.Pose(root.transform,hero,HeroTennisDriver.Clip.MatchWin,1,false);
                var hand=hero.Bone(HumanBodyBones.LeftHand);
                var bones=hero.body.bones;var left=hero.bones.Where(b=>b && b.name.StartsWith("Left") && new[]{"Index","Middle","Ring","Little","Thumb"}.Any(f=>b.name.Contains(f))).ToArray();
                hero.body.sharedMesh=HeroHandWeights.Prepare(hero.body,hero.female);
                var heads=left.Select(b=>b.position).ToArray();var before=new Mesh();hero.body.BakeMesh(before);
                if(sign==1)HeroGripPolish.ApplyFist(hero,true,1);
                else if(sign==-1)SignedFist(hero,sign);
                var after=new Mesh();hero.body.BakeMesh(after);var points0=before.vertices;var points1=after.vertices;
                var weights=hero.body.sharedMesh.boneWeights;float sum=0,max=0;int count=0;
                for(int n=0;n<weights.Length;n++)
                {
                    var w=weights[n];float Finger(int id)=>id>=0&&id<bones.Length&&left.Contains(bones[id])?1:0;
                    float fw=w.weight0*Finger(w.boneIndex0)+w.weight1*Finger(w.boneIndex1)+w.weight2*Finger(w.boneIndex2)+w.weight3*Finger(w.boneIndex3);
                    if(fw<.05f)continue;float displacement=Vector3.Distance(points0[n],points1[n]);sum+=displacement;max=Mathf.Max(max,displacement);count++;
                }
                audit.AppendLine(sex+" pose="+(sign==1?"CURRENT_HELPER":sign==-1?"OLD_NEGATIVE_SIGN":"READY_REACH_BASELINE")+" sign="+sign+" fingerWeightedVertices="+count+" meanMeshDisplacement="+(sum/Mathf.Max(1,count)).ToString("F5")+" max="+max.ToString("F5"));
                for(int n=0;n<left.Length;n++)audit.AppendLine(left[n].name+" bodyBoneIndex="+Array.IndexOf(bones,left[n])+" path="+AnimationUtility.CalculateTransformPath(left[n],root.transform)+" headTravel="+Vector3.Distance(heads[n],left[n].position).ToString("F5")+" rotation="+left[n].localRotation);
                Object.DestroyImmediate(before);Object.DestroyImmediate(after);
                // Force actual evaluated geometry into ordinary mesh renderers. This
                // close-up does not depend on offscreen GPU skinning or editor waits.
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if(!skin.enabled)continue;var mesh=new Mesh();skin.BakeMesh(mesh);
                    var baked=new GameObject(skin.name+" evaluated");baked.transform.SetParent(skin.transform,false);
                    baked.AddComponent<MeshFilter>().sharedMesh=mesh;baked.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;
                }
                var centre=hand.position+hand.up*.04f;
                Shot(cam,Path.Combine(output,sex+"_sign_"+sign+"_front.png"),centre+new Vector3(-.35f,.04f,.65f),centre);
                Shot(cam,Path.Combine(output,sex+"_sign_"+sign+"_side.png"),centre+new Vector3(-.65f,.08f,.12f),centre);
                Object.DestroyImmediate(root);
            }
            File.WriteAllText(Path.Combine(output,"fist-binding-audit.txt"),audit.ToString());AssetDatabase.Refresh();
            Debug.Log("TENNIS_FIST_PROBE_COMPLETE "+output);
        }
        static void SignedFist(MatchHeroLook h,int sign)
        {
            var hand=h.Bone(HumanBodyBones.LeftHand);var index=h.Bone(HumanBodyBones.LeftIndexProximal);var little=h.Bone(HumanBodyBones.LeftLittleProximal);var middle=h.Bone(HumanBodyBones.LeftMiddleProximal);
            var palm=Vector3.Cross((index.position-little.position).normalized,(middle.position-hand.position).normalized).normalized * -sign;
            foreach(string name in new[]{"Index","Middle","Ring","Little"})
            {
                Transform B(string joint)=>h.Bone((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),"Left"+name+joint));
                var p=B("Proximal");var m=B("Intermediate");var d=B("Distal");
                Turn(p,m.position-p.position,palm,35);Turn(m,d.position-m.position,palm,50);Turn(d,d.up,palm,25);
            }
        }
        static void Turn(Transform b,Vector3 d,Vector3 palm,float angle){var axis=Vector3.Cross(d.normalized,palm).normalized;if(axis.sqrMagnitude>.5f)b.rotation=Quaternion.AngleAxis(angle,axis)*b.rotation;}
        static void Shot(Camera c,string path,Vector3 position,Vector3 target)
        {
            c.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
            var rt=RenderTexture.GetTemporary(768,768,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);c.targetTexture=rt;c.Render();RenderTexture.active=rt;
            var tex=new Texture2D(768,768,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,768,768),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());
            c.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(tex);RenderTexture.ReleaseTemporary(rt);
        }
    }
}
#endif

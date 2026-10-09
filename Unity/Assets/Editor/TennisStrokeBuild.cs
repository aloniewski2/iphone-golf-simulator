#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
namespace GolfArcade.EditorTools
{
    /// Reduces the imported free-arm release snap without touching the hitting
    /// arm, contact pose, source rig, prop curves, or any approved emote.
    public static class TennisStrokeBuild
    {
        const string Output="Assets/Resources/Tennis/Performance";
        public static void Run()
        {
            Directory.CreateDirectory(Output);var report=new StringBuilder("FREE ARM STROKE SOURCE POLISH\n");
            foreach(string sex in new[]{"Male","Female"})
            {
                string path=$"Assets/Characters/MatchHeroes/{sex}/{sex}_Forehand.fbx";
                var source=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"));
                var clone=UnityEngine.Object.Instantiate(source);clone.name=sex+"_ForehandPolished";clone.frameRate=60;
                var map=new AnimationCurve(new Keyframe(0,0),new Keyframe(.12f,.2333f),new Keyframe(.18f,.2667f),new Keyframe(.42f,.3333f),new Keyframe(.55f,.55f));
                for(int i=0;i<map.length;i++){AnimationUtility.SetKeyLeftTangentMode(map,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(map,i,AnimationUtility.TangentMode.ClampedAuto);}
                int changed=0;
                foreach(var binding in AnimationUtility.GetCurveBindings(source))
                {
                    string bone=binding.path.Split('/').Last();
                    if(binding.type!=typeof(Transform)||!(bone=="LeftShoulder"||bone=="LeftUpperArm"||bone=="LeftLowerArm"||bone=="LeftHand"))continue;
                    var old=AnimationUtility.GetEditorCurve(source,binding);var polished=new AnimationCurve();
                    int frames=Mathf.RoundToInt(source.length*60);
                    for(int frame=0;frame<=frames;frame++)
                    {
                        float t=frame/60f;float sample=t>=.55f?t:Mathf.Clamp(map.Evaluate(t),0,.55f);
                        polished.AddKey(t,old.Evaluate(sample));
                    }
                    for(int i=0;i<polished.length;i++){AnimationUtility.SetKeyLeftTangentMode(polished,i,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(polished,i,AnimationUtility.TangentMode.ClampedAuto);}
                    AnimationUtility.SetEditorCurve(clone,binding,polished);changed++;
                }
                clone.EnsureQuaternionContinuity();
                var root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Tennis/Customization/Player"+sex));
                try
                {
                    var h=root.GetComponent<MatchHeroLook>();var d=root.GetComponent<HeroTennisDriver>();
                    float contact=d.slots.First(s=>s.id==HeroTennisDriver.Clip.Forehand).contact;
                    source.SampleAnimation(root,contact);var right=h.Bone(HumanBodyBones.RightHand).position;var left=h.Bone(HumanBodyBones.LeftHand).position;var hip=h.Bone(HumanBodyBones.Hips).position;
                    clone.SampleAnimation(root,contact);float rd=Vector3.Distance(right,h.Bone(HumanBodyBones.RightHand).position),ld=Vector3.Distance(left,h.Bone(HumanBodyBones.LeftHand).position),hd=Vector3.Distance(hip,h.Bone(HumanBodyBones.Hips).position);
                    if(Mathf.Max(rd,ld,hd)>.00005f)throw new InvalidOperationException(sex+" contact pose changed: "+rd+" / "+ld+" / "+hd);
                    if(Mathf.Abs(clone.length-source.length)>.0001f)throw new InvalidOperationException(sex+" clip duration changed");
                    report.AppendLine($"{sex}: {changed} free-arm curves | source={source.length:F4}s contact={contact:F4}s | right delta={rd:F7}m left delta={ld:F7}m hips delta={hd:F7}m");
                }
                finally{UnityEngine.Object.DestroyImmediate(root);}
                string target=Output+"/"+clone.name+".anim";var current=AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
                if(current){EditorUtility.CopySerialized(clone,current);UnityEngine.Object.DestroyImmediate(clone);}else AssetDatabase.CreateAsset(clone,target);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            string proof=Path.GetFullPath(Path.Combine(Application.dataPath,"../../proof/full-visual-overhaul/tennis"));Directory.CreateDirectory(proof);File.WriteAllText(Path.Combine(proof,"stroke-polish-build.txt"),report.ToString());Debug.Log(report);
        }
    }
}
#endif

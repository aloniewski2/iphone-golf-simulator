using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// Deterministic review of the production Generic bodies, clothes, props and all 33 slots.
    /// Each pose advances on a separate player frame so GPU skinning cannot reuse a prior pose.
    [InitializeOnLoad]
    public static class VisualOverhaulCharacterCapture
    {
        static readonly List<ScriptableRendererFeature> disabledFeatures=new();
        const string Flag="VisualOverhaulCharacterCapture";
        static IEnumerator script;
        static readonly Stack<IEnumerator> steps=new();
        static string output;
        static int steppedFrame=-1;
        static readonly List<string> audit=new();
        static VisualOverhaulCharacterCapture(){EditorApplication.update+=Tick;}
        public static void Run()
        {
            output=Path.GetFullPath(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_OUT")??"../proof/full-visual-overhaul/character-pass-1");
            Directory.CreateDirectory(output);
            if(Environment.GetEnvironmentVariable("VISUAL_HERO_FILMIC")=="1"){
                var renderer=Resources.Load<UniversalRendererData>("Tennis/Rendering/TennisURP_Renderer");
                renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                if(!renderer.postProcessData)throw new InvalidOperationException("Missing URP post-processing resources");
                renderer.SetDirty();
            }
            SessionState.SetString(Flag+"out",output);SessionState.SetBool(Flag,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");EditorApplication.isPlaying=true;
        }
        static void Tick()
        {
            if(!SessionState.GetBool(Flag,false)||!EditorApplication.isPlaying)return;
            if(steppedFrame==Time.frameCount)return;steppedFrame=Time.frameCount;
            var game=Object.FindFirstObjectByType<TennisGame>();if(!game||!game.Initialized)return;
            try
            {
                if(script==null){output=SessionState.GetString(Flag+"out","");script=Capture(game);steps.Push(script);}
                while(steps.Count>0){
                    var step=steps.Peek();
                    if(!step.MoveNext()){steps.Pop();continue;}
                    if(step.Current is IEnumerator nested){steps.Push(nested);continue;}
                    return;
                }
                Finish(0);
            }
            catch(Exception e){Debug.LogException(e);audit.Add("FAIL: "+e);Finish(1);}
        }
        static void Finish(int code)
        {
            File.WriteAllText(Path.Combine(output,"audit.txt"),string.Join("\n",audit));
            if(Environment.GetEnvironmentVariable("VISUAL_UNDERARM_GUSSET_STATS")=="1"){
                File.WriteAllText(Path.Combine(output,"gusset-stats.json"),JsonUtility.ToJson(HeroGarmentGussetLifetime.GetCaptureStats(),true));
                HeroGarmentGussetLifetime.EndCaptureStats();
            }
            foreach(var feature in disabledFeatures)if(feature)feature.SetActive(true);disabledFeatures.Clear();
            if(Environment.GetEnvironmentVariable("VISUAL_HERO_FILMIC")=="1"){
                var renderer=Resources.Load<UniversalRendererData>("Tennis/Rendering/TennisURP_Renderer");
                renderer.postProcessData=null;renderer.SetDirty();Shader.SetGlobalFloat("_HeroFilmResponse",0);
            }
            SessionState.SetBool(Flag,false);Time.captureFramerate=0;EditorApplication.Exit(code);
        }
        static string GarmentShapes(MatchHeroLook hero) {
            var top=hero.kit.FirstOrDefault(r=>r&&r.name=="Kit_Top");if(!top)return "noTop";
            return string.Join(";",Enumerable.Range(0,top.sharedMesh.blendShapeCount).Select(i=>top.sharedMesh.GetBlendShapeName(i)+":"+top.GetBlendShapeWeight(i).ToString("F3")));
        }
        static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        static IEnumerator Capture(TennisGame game)
        {
            int renderedSlots=0;
            HeroGarmentGussetLifetime.BeginCaptureStats();
            if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_DISABLE_SSAO")=="1") {
                var data=Resources.Load<UniversalRendererData>("Tennis/Rendering/TennisURP_Renderer");
                if(data)foreach(var feature in data.rendererFeatures)
                    if(feature is ScreenSpaceAmbientOcclusion && feature.isActive){disabledFeatures.Add(feature);feature.SetActive(false);}
                audit.Add("Diagnostic only: SSAO temporarily disabled; restored on exit.");
            }
            Time.captureFramerate=30;
            HeroGarmentLOD.ForceMatchDetailForReview = Environment.GetEnvironmentVariable("VISUAL_CHARACTER_MATCH_LOD") == "1";
            HeroGarmentLOD.ForceFullDetail = !HeroGarmentLOD.ForceMatchDetailForReview;
            var presentation=game.GetComponent<TennisPresentation>();if(presentation)presentation.Finish();
            game.ManualSimulation=true;game.enabled=false;
            foreach(var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))c.enabled=false;
            var cam=game.GameplayCamera;
            bool filmic=Environment.GetEnvironmentVariable("VISUAL_HERO_FILMIC")=="1";
            Shader.SetGlobalFloat("_HeroFilmResponse",filmic?1:0);
            if(filmic){
                cam.allowHDR=true;cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                foreach(var volume in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)){
                    if(!volume.profile)continue;
                    if(volume.profile.TryGet(out Tonemapping tone))tone.mode.Override(TonemappingMode.ACES);
                    if(volume.profile.TryGet(out ColorAdjustments grade)){grade.postExposure.Override(.8f);grade.contrast.Override(0);grade.saturation.Override(0);grade.colorFilter.Override(Color.white);}
                    if(volume.profile.TryGet(out Vignette vignette))vignette.active=false;
                    if(volume.profile.TryGet(out DepthOfField depth))depth.active=false;
                    if(volume.profile.TryGet(out Bloom bloom))bloom.active=false;
                }
                audit.Add("Filmic diagnostic: URP PostProcessData connected; ACES active; shader highlight rolls bypassed.");
            }
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.83f,.89f,.88f);
            cam.fieldOfView=35;cam.nearClipPlane=.025f;
            foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))light.enabled=false;
            bool warmStudio=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_STUDIO")=="warm";
            RenderSettings.ambientMode=warmStudio?UnityEngine.Rendering.AmbientMode.Trilight:UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.55f,.61f,.66f);RenderSettings.fog=false;
            if(warmStudio){
                RenderSettings.ambientSkyColor=new Color(.43f,.53f,.65f);
                RenderSettings.ambientEquatorColor=new Color(.46f,.35f,.28f);
                RenderSettings.ambientGroundColor=new Color(.24f,.18f,.14f);
                cam.backgroundColor=new Color(.93f,.91f,.86f);
                if(filmic){
                    RenderSettings.ambientSkyColor=new Color(.30f,.37f,.44f);
                    RenderSettings.ambientEquatorColor=new Color(.35f,.29f,.25f);
                    RenderSettings.ambientGroundColor=new Color(.16f,.13f,.11f);
                }
            }
            Light LightAt(string name,Vector3 position,Color color,float intensity)
            {
                var l=new GameObject(name).AddComponent<Light>();l.type=LightType.Directional;
                l.color=color;l.intensity=intensity;l.transform.position=position;l.transform.LookAt(Vector3.up);
                return l;
            }
            var key=LightAt("Review key",new Vector3(warmStudio?3:-3,5,4),new Color(1,.96f,.9f),warmStudio?1.75f:1.25f);key.shadows=LightShadows.Soft;
            if(warmStudio){key.shadowStrength=.65f;key.shadowBias=.02f;key.shadowNormalBias=.05f;}
            LightAt("Review fill",new Vector3(warmStudio?-3:3,2,2),warmStudio?new Color(1,.87f,.76f):new Color(.76f,.88f,1),warmStudio?.22f:.55f);
            LightAt("Review rim",new Vector3(1,4,-3),new Color(1,.91f,.74f),warmStudio?.65f:.5f);
            if(filmic){key.intensity=1.5f;GameObject.Find("Review fill").GetComponent<Light>().color=new Color(.88f,.94f,1);GameObject.Find("Review fill").GetComponent<Light>().intensity=.35f;GameObject.Find("Review rim").GetComponent<Light>().intensity=.35f;}
            foreach(bool female in new[]{false,true})
            {
                string selectedSex=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_SEX");
                if(selectedSex=="female"&&!female||selectedSex=="male"&&female)continue;
                game.SelectCharacter(female);yield return Frames(3);
                var actor=game.Player;actor.enabled=false;actor.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
                var driver=actor.GetComponentInChildren<HeroTennisDriver>();driver.enabled=false;
                var hero=driver.matchLook;
                var facePerformance=hero.GetComponent<MatchHeroFacePerformance>();
                if(facePerformance)facePerformance.ForcedBlink=0;
                foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                    if(!r.transform.IsChildOf(actor.transform))r.enabled=false;
                // Audience components can create their late detail renderers after the enumeration.
                // Isolate the unchanged hero graph by layer so later world updates cannot enter a plate.
                foreach(var child in actor.GetComponentsInChildren<Transform>(true))child.gameObject.layer=30;
                cam.cullingMask=1<<30;
                bool starter=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_STARTER")=="1";
                var kit=starter?default:TennisLook.Kit.From("FFFFFF","10243D","FFFFFF","10243D",2);
                string skinHex=Environment.GetEnvironmentVariable(female?"VISUAL_CHARACTER_SKIN_FEMALE":"VISUAL_CHARACTER_SKIN_MALE")??Environment.GetEnvironmentVariable("VISUAL_CHARACTER_SKIN")??"C47A4C";
                var style=HeroKit.Style.From(2,0,0,kit,0,female);style.SkinTint=HeroKit.Hex(skinHex);
                game.SetPlayerLook(style);
                string surfaceReview=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_SURFACE");
                if(surfaceReview=="lit"||surfaceReview=="lit-all"){
                    var targets=surfaceReview=="lit-all"?hero.body.sharedMaterials.Concat(hero.face.sharedMaterials):hero.body.sharedMaterials;
                    foreach(var material in targets)if(material){
                        material.shader=Shader.Find("Universal Render Pipeline/Lit");
                        material.SetFloat("_Smoothness",material.name.Contains("Face_")?.55f:.35f);material.SetFloat("_Metallic",0);
                        material.DisableKeyword("_NORMALMAP");
                    }
                }
                driver.Sample(HeroTennisDriver.Clip.Ready,.5f);yield return Frames(2);
                string sex=female?"female":"male";
                var reviewLOD = hero.GetComponent<HeroGarmentLOD>();
                audit.Add($"{sex} garment detail: match={reviewLOD && reviewLOD.UsingMatchDetail}, fullTriangles={reviewLOD?.FullTriangles}, matchTriangles={reviewLOD?.MatchTriangles}");
                if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_SWATCHES")=="1"){
                    var reviewHead=hero.Bone(HumanBodyBones.Head).position;
                    Vector3 reviewTarget=reviewHead+Vector3.up*.04f;
                    Vector3 reviewUp=Vector3.up,reviewForward=hero.transform.forward,reviewRight=hero.transform.right;
                    bool faceAligned=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_FACE_ALIGNED")=="1";
                    if(faceAligned){
                        var headBone=hero.Bone(HumanBodyBones.Head);int headIndex=Array.IndexOf(hero.body.bones,headBone);
                        var material=hero.body.sharedMaterial;
                        if(headIndex<0||!material||material.GetVector("_HeadOrigin").w<.5f)throw new InvalidOperationException("Missing anatomical frame for face review");
                        var bindToWorld=headBone.localToWorldMatrix*hero.body.sharedMesh.bindposes[headIndex];
                        Vector3 r=material.GetVector("_HeadRight"),u=material.GetVector("_HeadUp"),f=material.GetVector("_HeadForward"),e=material.GetVector("_HeadEye");
                        Vector3 eyeBind=(Vector3)material.GetVector("_HeadOrigin")+r*e.x+u*e.y+f*e.z;
                        reviewUp=bindToWorld.MultiplyVector(u).normalized;reviewForward=bindToWorld.MultiplyVector(f).normalized;reviewRight=bindToWorld.MultiplyVector(r).normalized;
                        reviewTarget=bindToWorld.MultiplyPoint3x4(eyeBind)-reviewUp*.02f;
                        audit.Add($"{sex} face-aligned review: target={reviewTarget}, up={reviewUp}, forward={reviewForward}");
                    }
                    var variants=new[]{
                        (name:"baseline",tone:TonemappingMode.ACES,exposure:.8f,male:"E58D64",woman:"F5AB7F",response:0f),
                        (name:"pigment",tone:TonemappingMode.ACES,exposure:.70f,male:"EA8D71",woman:"FBAE8F",response:1f),
                        (name:"neutral",tone:TonemappingMode.Neutral,exposure:.30f,male:"E58D64",woman:"F5AB7F",response:1f)
                    };
                    bool twoFrontOnly=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_TWO_FRONT_ONLY")=="1";
                    foreach(var variant in variants){
                        bool blinkReview=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_BLINK_REVIEW")=="1";
                        bool fullReview=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_FULL_REVIEW")=="1";
                        if((blinkReview||fullReview||twoFrontOnly)&&variant.name!="pigment")continue;
                        foreach(var volume in Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)){
                            if(!volume.profile)continue;
                            if(volume.profile.TryGet(out Tonemapping tone))tone.mode.Override(variant.tone);
                            if(volume.profile.TryGet(out ColorAdjustments grade))grade.postExposure.Override(variant.exposure);
                        }
                        if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_USE_MATERIAL_RESPONSE")!="1")foreach(var material in hero.body.sharedMaterials)if(material&&material.HasProperty("_SkinResponse"))material.SetFloat("_SkinResponse",variant.response*(female?2:1));
                        style.SkinTint=HeroKit.Hex(female?variant.woman:variant.male);game.SetPlayerLook(style);yield return Frames(2);
                        var front=faceAligned?reviewTarget+reviewForward*.90f+reviewUp*.01f:reviewHead+hero.transform.TransformDirection(new Vector3(0,.06f,1));
                        Shot(sex+"_"+variant.name+"_front",front,reviewTarget,24,900,900,reviewUp);
                        if(twoFrontOnly){
                            audit.Add($"Two-front normal review {sex}: mesh={hero.body.sharedMesh.name}; eye, skin finish and open blink retained");
                            continue;
                        }
                        Shot(sex+"_"+variant.name+"_threequarter",front+reviewRight*.30f,reviewTarget,24,900,900,reviewUp);
                        if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_FACE_PAIR_ONLY")=="1")continue;
                        if(fullReview){
                            Vector3 fullReviewTarget=hero.transform.TransformPoint(new Vector3(0,.88f,0));
                            Shot(sex+"_current_full",hero.transform.TransformPoint(new Vector3(0,.94f,3.6f)),fullReviewTarget,30,900,1200);
                            driver.Sample(HeroTennisDriver.Clip.IntroWave,driver.LengthOf(HeroTennisDriver.Clip.IntroWave)*.45f);yield return Frames(2);
                            Shot(sex+"_current_intro",hero.transform.TransformPoint(new Vector3(0,.94f,3.6f)),fullReviewTarget,30,900,1200);
                            if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_TORSO_REVIEW")=="1"){
                                Shot(sex+"_polo_front",hero.transform.TransformPoint(new Vector3(0,1.32f,1.85f)),hero.transform.TransformPoint(new Vector3(0,1.30f,0)),24,900,900);
                                Shot(sex+"_polo_threequarter",hero.transform.TransformPoint(new Vector3(.7f,1.38f,1.75f)),hero.transform.TransformPoint(new Vector3(0,1.30f,0)),24,900,900);
                            }
                            if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_GARMENT_SWATCHES")=="1"){
                                foreach(string swatch in new[]{"F4F0E4","162032","37B7A6"}){
                                    hero.SetKit(swatch,null,null);yield return Frames(2);
                                    Shot(sex+"_polo_swatch_"+swatch,hero.transform.TransformPoint(new Vector3(0,.94f,3.6f)),fullReviewTarget,30,900,1200);
                                    if(!hero.TryGetKitColour(MatchHeroLook.RoleShirt,out var applied))throw new InvalidOperationException("Shirt palette role missing");
                                    audit.Add($"Garment swatch {sex}: requested={swatch}, material={applied}");
                                }
                                game.SetPlayerLook(style);yield return Frames(2);
                            }
                            if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLOTH_REVIEW")=="1"){
                                HeroGarmentPoseSnapshot.Save(hero,sex+"_intro",Path.Combine(output,"poses"));
                                string clothSelection=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLOTH_CLIPS");
                                var clothClips=string.IsNullOrWhiteSpace(clothSelection)
                                    ?new[]{HeroTennisDriver.Clip.Serve,HeroTennisDriver.Clip.Forehand}
                                    :clothSelection=="all"?Enum.GetValues(typeof(HeroTennisDriver.Clip)).Cast<HeroTennisDriver.Clip>().ToArray()
                                    :clothSelection.Split(',').Select(name=>Enum.Parse<HeroTennisDriver.Clip>(name.Trim())).ToArray();
                                foreach(var clip in clothClips){
                                    float length=driver.LengthOf(clip),contact=driver.ContactOf(clip);
                                    if(length<=0)throw new InvalidOperationException(sex+" missing "+clip);
                                    bool motionReview=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLOTH_MOTION_REVIEW")=="1";
                                    int samples=motionReview?10:3;
                                    if(int.TryParse(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLOTH_SAMPLES"),out int requestedSamples))
                                        samples=Mathf.Clamp(requestedSamples,3,33);
                                    for(int sample=0;sample<samples;sample++){
                                        float t=motionReview||samples!=3?(sample==samples-1&&contact>0?contact:length*Mathf.Min((float)sample/(samples-2),1)):
                                            sample==1&&contact>0?contact:length*new[]{.18f,.5f,.82f}[sample];
                                        string reviewWindow=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLOTH_WINDOW");
                                        if(!string.IsNullOrWhiteSpace(reviewWindow)){
                                            var ends=reviewWindow.Split(',');
                                            if(ends.Length!=2)throw new InvalidOperationException("Cloth review window needs start,end seconds");
                                            float from=float.Parse(ends[0],System.Globalization.CultureInfo.InvariantCulture);
                                            float to=float.Parse(ends[1],System.Globalization.CultureInfo.InvariantCulture);
                                            t=sample==samples-1&&contact>0?contact:Mathf.Lerp(from,to,Mathf.Min((float)sample/(samples-2),1));
                                        }
                                        driver.Sample(clip,t);yield return Frames(2);
                                        foreach(var mesh in hero.GetComponentsInChildren<SkinnedMeshRenderer>())
                                            if(mesh.bounds.size.magnitude>8||float.IsNaN(mesh.bounds.center.x)){
                                                string badPose=$"{sex}_{clip}_{sample}_invalid_bounds";
                                                HeroGarmentPoseSnapshot.SaveMatrices(hero,badPose,t,Path.Combine(output,"matrices"));
                                                HeroGarmentPoseSnapshot.Save(hero,badPose,Path.Combine(output,"poses"));
                                                var baked=new Mesh();mesh.BakeMesh(baked);
                                                string detail=$"renderer={mesh.bounds}; local={mesh.localBounds}; shared={mesh.sharedMesh.bounds}; baked={baked.bounds}; rootScale={hero.transform.lossyScale}; rendererScale={mesh.transform.lossyScale}";
                                                Object.Destroy(baked);File.WriteAllText(Path.Combine(output,badPose+".txt"),detail);
                                                throw new InvalidOperationException("Invalid bounds "+sex+" "+clip+" "+mesh.name+" "+detail);
                                            }
                                        string pose=$"{sex}_{clip}_{sample}";
                                        TailoredPoloPoseProbe.Apply(hero,pose);
                                        Shot(pose,hero.transform.TransformPoint(new Vector3(2.4f,1.3f,4.1f)),hero.transform.TransformPoint(new Vector3(0,.83f,.1f)),34,900,1200);
                                        if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLOTH_REAR")=="1")
                                            Shot(pose+"_rear",hero.transform.TransformPoint(new Vector3(-2.4f,1.3f,-4.1f)),hero.transform.TransformPoint(new Vector3(0,.83f,.1f)),34,900,1200);
                                        if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLOTH_NO_DUMPS")!="1")
                                            HeroGarmentPoseSnapshot.Save(hero,pose,Path.Combine(output,"poses"));
                                        if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLOTH_MATRIX_DUMPS")=="1")
                                            HeroGarmentPoseSnapshot.SaveMatrices(hero,pose,t,Path.Combine(output,"matrices"));
                                        audit.Add($"Cloth sample {pose}: time={t:F5}s");
                                    }
                                    renderedSlots++;
                                    audit.Add($"Cloth review {sex} {clip}: length={length:F3} contact={contact:F4}; {samples} actual poses under pigment lighting");
                                }
                            }
                            driver.Sample(HeroTennisDriver.Clip.Ready,.5f);yield return Frames(2);
                        }
                        if(blinkReview&&facePerformance){
                            foreach(float blink in new[]{0f,.25f,.5f,.75f,1f}){
                                facePerformance.ForcedBlink=blink;yield return Frames(2);
                                if(blink==1f && Environment.GetEnvironmentVariable("VISUAL_CHARACTER_FACE_POSE_DUMPS")=="1")HeroGarmentPoseSnapshot.SaveFace(hero,sex+"_closed",Path.Combine(output,"poses"));
                                Shot(sex+"_blink_"+Mathf.RoundToInt(blink*100)+"_front",front,reviewTarget,24,900,900,reviewUp);
                                Shot(sex+"_blink_"+Mathf.RoundToInt(blink*100)+"_threequarter",front+reviewRight*.30f,reviewTarget,24,900,900,reviewUp);
                                audit.Add($"Blink {sex} {blink}: "+string.Join(";",new[]{hero.body,hero.face}.OfType<SkinnedMeshRenderer>().Select(r=>r.name+":"+string.Join(",",Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(i=>r.sharedMesh.GetBlendShapeName(i)+"="+r.GetBlendShapeWeight(i))))));
                            }
                            facePerformance.ForcedBlink=0;yield return Frames(2);
                            if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_GAZE_REVIEW")=="1"){
                                bool performanceEnabled=facePerformance.enabled;facePerformance.enabled=false;
                                foreach(var gaze in new[]{new Vector2(-.0018f,0),new Vector2(.0018f,0),new Vector2(0,.0018f),new Vector2(0,-.0018f)}){
                                    hero.SetFacePerformance(0,gaze);
                                    Shot(sex+"_gaze_"+gaze.x.ToString("F4",System.Globalization.CultureInfo.InvariantCulture)+"_"+gaze.y.ToString("F4",System.Globalization.CultureInfo.InvariantCulture),front,reviewTarget,24,900,900,reviewUp);
                                }
                                hero.SetFacePerformance(0,Vector2.zero);facePerformance.enabled=performanceEnabled;
                            }
                        }
                        string blinkPoseNames=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_BLINK_POSES");
                        if(!string.IsNullOrWhiteSpace(blinkPoseNames)){
                            if(!facePerformance)throw new InvalidOperationException("Pose blink review requires MatchHeroFacePerformance");
                            var clips=blinkPoseNames.Split(',').Select(name=>Enum.Parse<HeroTennisDriver.Clip>(name.Trim())).Distinct().ToArray();
                            foreach(var clip in clips){
                                float length=driver.LengthOf(clip),contact=driver.ContactOf(clip);
                                if(length<=0)throw new InvalidOperationException(sex+" missing blink pose "+clip);
                                var samples=new System.Collections.Generic.List<(string name,float time)>{("mid",length*.5f)};
                                if(contact>0&&Mathf.Abs(contact-length*.5f)>.0001f)samples.Add(("contact",contact));
                                foreach(var sample in samples){
                                    driver.Sample(clip,sample.time);facePerformance.ForcedBlink=0;yield return Frames(2);
                                    foreach(float blink in new[]{0f,1f}){
                                        facePerformance.ForcedBlink=blink;yield return Frames(2);
                                        // Register the actual animated head, rather than a fixed
                                        // Ready camera. Source frame is authored in Body bind space.
                                        var headBone=hero.Bone(HumanBodyBones.Head);
                                        int headIndex=Array.IndexOf(hero.body.bones,headBone);
                                        var frameMaterial=hero.body.sharedMaterials.FirstOrDefault(m=>m&&m.HasProperty("_HeadOrigin")&&m.GetVector("_HeadOrigin").w>.5f);
                                        if(!headBone||headIndex<0||!frameMaterial)throw new InvalidOperationException("Missing registered frame for pose blink review");
                                        var bindToWorld=headBone.localToWorldMatrix*hero.body.sharedMesh.bindposes[headIndex];
                                        Vector3 sourceRight=frameMaterial.GetVector("_HeadRight"),sourceUp=frameMaterial.GetVector("_HeadUp"),sourceForward=frameMaterial.GetVector("_HeadForward"),sourceEye=frameMaterial.GetVector("_HeadEye");
                                        Vector3 eyeBind=(Vector3)frameMaterial.GetVector("_HeadOrigin")+sourceRight*sourceEye.x+sourceUp*sourceEye.y+sourceForward*sourceEye.z;
                                        var poseUp=bindToWorld.MultiplyVector(sourceUp).normalized;
                                        var poseForward=bindToWorld.MultiplyVector(sourceForward).normalized;
                                        var poseRight=bindToWorld.MultiplyVector(sourceRight).normalized;
                                        var poseTarget=bindToWorld.MultiplyPoint3x4(eyeBind)-poseUp*.02f;
                                        var poseFront=poseTarget+poseForward*.90f+poseUp*.01f;
                                        string label=sex+"_"+clip+"_"+sample.name+"_blink_"+Mathf.RoundToInt(blink*100);
                                        Shot(label+"_front",poseFront,poseTarget,24,900,900,poseUp);
                                        Shot(label+"_threequarter",poseFront+poseRight*.30f,poseTarget,24,900,900,poseUp);
                                        audit.Add($"Pose blink {label}: time={sample.time:F5}s, length={length:F5}s, contact={contact:F5}s; target={poseTarget}; head={headBone.position}; "+string.Join(";",new[]{hero.body,hero.face}.OfType<SkinnedMeshRenderer>().Select(r=>r.name+":"+string.Join(",",Enumerable.Range(0,r.sharedMesh.blendShapeCount).Select(i=>r.sharedMesh.GetBlendShapeName(i)+"="+r.GetBlendShapeWeight(i))))));
                                    }
                                }
                            }
                            facePerformance.ForcedBlink=0;driver.Sample(HeroTennisDriver.Clip.Ready,.5f);yield return Frames(2);
                        }
                        audit.Add($"Skin swatch {sex}/{variant.name}: tone={variant.tone}, exposure={variant.exposure}, colour={(female?variant.woman:variant.male)}, regionalResponse={variant.response}");
                    }
                    continue;
                }
                if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_FEATURE_DUMPS")=="1")HeroFeaturePoseAudit.Save(hero,sex+"_ready",Path.Combine(output,"features"));
                audit.Add(sex+" actual body="+hero.body.sharedMesh.name+" bones="+hero.body.bones.Length);
                audit.Add(driver.SlotReport());
                foreach(var material in hero.body.sharedMaterials)if(material){
                    float Property(string name)=>material.HasProperty(name)?material.GetFloat(name):-1;
                    audit.Add($"{sex} body material={material.name} shader={material.shader.name} albedo={(material.HasProperty("_BaseMap")&&material.GetTexture("_BaseMap") ? material.GetTexture("_BaseMap").name : "none")} normalKeyword={material.IsKeywordEnabled("_NORMALMAP")} bump={Property("_BumpScale")} triplanar={Property("_BumpTriplanar")}");
                }
                var bodyBounds=hero.body.bounds;
                var centre=bodyBounds.center;float floor=bodyBounds.min.y;
                var fullTarget=new Vector3(centre.x,floor+.88f,centre.z);
                Vector3 Point(float x,float y,float z)=>hero.transform.TransformPoint(new Vector3(x,y,z));
                audit.Add(sex+" ready garment shapes="+GarmentShapes(hero));
                Shot(sex+"_front",Point(0,.94f,3.6f),fullTarget,30,900,1200);
                Shot(sex+"_front34",Point(1.45f,1.07f,3.4f),fullTarget,30,900,1200);
                Shot(sex+"_back34",Point(-1.45f,1.07f,-3.4f),fullTarget,30,900,1200);
                Shot(sex+"_profile",Point(3.8f,1.05f,0),fullTarget,30,900,1200);
                // A second real production pose makes the outfit and face readable without the
                // Ready racket obscuring the torso. This samples the approved wave clip as shipped.
                driver.Sample(HeroTennisDriver.Clip.IntroWave,driver.LengthOf(HeroTennisDriver.Clip.IntroWave)*.45f);
                yield return Frames(2);
                audit.Add(sex+" intro garment shapes="+GarmentShapes(hero));
                Shot(sex+"_intro",Point(0,.94f,3.6f),fullTarget,30,900,1200);
                if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_POSE_DUMPS")=="1")
                    HeroGarmentPoseSnapshot.Save(hero,sex+"_intro",Path.Combine(output,"poses"));
                driver.Sample(HeroTennisDriver.Clip.Ready,.5f);yield return Frames(2);
                var head=hero.Bone(HumanBodyBones.Head).position;
                Shot(sex+"_face_front",head+hero.transform.TransformDirection(new Vector3(0,.06f,1.0f)),head+Vector3.up*.04f,24,900,900);
                Shot(sex+"_face34",head+hero.transform.TransformDirection(new Vector3(.32f,.06f,1.0f)),head+Vector3.up*.04f,24,900,900);
                if(facePerformance)
                {
                    foreach(float blink in new[]{.5f,1f})
                    {
                        facePerformance.ForcedBlink=blink;yield return Frames(2);
                        Shot(sex+"_blink_"+Mathf.RoundToInt(blink*100),head+hero.transform.TransformDirection(new Vector3(.32f,.06f,1)),head+Vector3.up*.04f,24,900,900);
                    }
                    facePerformance.ForcedBlink=0;yield return Frames(2);
                }
                Shot(sex+"_shirt_detail",Point(.4f,1.15f,1.5f),Point(0,1.17f,0),28,900,900);
                foreach(string tone in new[]{"F1CBA5","78462D"})
                {
                    style.SkinTint=HeroKit.Hex(tone);game.SetPlayerLook(style);yield return Frames(2);
                    Shot(sex+"_face_"+tone,head+hero.transform.TransformDirection(new Vector3(.32f,.06f,1)),head+Vector3.up*.04f,24,900,900);
                }
                style.SkinTint=HeroKit.Hex(skinHex);game.SetPlayerLook(style);
                foreach(HeroTennisDriver.Clip clip in Enum.GetValues(typeof(HeroTennisDriver.Clip)))
                {
                    string selected=Environment.GetEnvironmentVariable("VISUAL_CHARACTER_CLIPS");
                    if(!string.IsNullOrWhiteSpace(selected)&&!selected.Split(',').Contains(clip.ToString()))continue;
                    float length=driver.LengthOf(clip);if(length<=0)throw new InvalidOperationException(sex+" missing "+clip);
                    float contact=driver.ContactOf(clip);
                    for(int sample=0;sample<3;sample++)
                    {
                        float t=sample==1&&contact>0?contact:length*new[]{.18f,.5f,.82f}[sample];
                        driver.Sample(clip,t);yield return null;
                        foreach(var mesh in hero.GetComponentsInChildren<SkinnedMeshRenderer>())
                            if(mesh.bounds.size.magnitude>8||float.IsNaN(mesh.bounds.center.x))throw new InvalidOperationException("Invalid bounds "+sex+" "+clip+" "+mesh.name);
                        Shot($"{sex}_{clip}_{sample}",Point(2.4f,1.3f,4.1f),Point(0,.83f,.1f),34,640,800);
                        if(Environment.GetEnvironmentVariable("VISUAL_CHARACTER_POSE_DUMPS")=="1")
                            HeroGarmentPoseSnapshot.Save(hero,$"{sex}_{clip}_{sample}",Path.Combine(output,"poses"));
                    }
                    audit.Add($"{sex} {clip}: length={length:F3} contact={contact:F4} rendered 3 poses");
                    renderedSlots++;
                }
            }
            if(Environment.GetEnvironmentVariable("VISUAL_GUSSET_OWNERSHIP_GATE")=="1")yield return HeroGarmentGussetRuntimeGate.RunGate(game,output);
            audit.Add($"CAPTURE PASS: selected production bodies and {renderedSlots} selected clip slots rendered; final appearance requires visual review.");
            void Shot(string name,Vector3 from,Vector3 target,float fov,int w,int h,Vector3? up=null)
            {
                cam.transform.SetPositionAndRotation(from,Quaternion.LookRotation(target-from,up??Vector3.up));cam.fieldOfView=fov;
                var rt=RenderTexture.GetTemporary(w,h,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Default,4);
                var old=cam.targetTexture;var active=RenderTexture.active;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);
                try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name+".png"),tex.EncodeToPNG());}
                finally{cam.targetTexture=old;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
            }
        }
    }
}

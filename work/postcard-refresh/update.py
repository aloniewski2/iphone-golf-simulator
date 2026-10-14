from pathlib import Path
import shutil
root=Path.cwd(); backup=root/'work/postcard-refresh/original'
def edit(name,changes):
 p=root/name; b=backup/name;b.parent.mkdir(parents=True,exist_ok=True)
 if not b.exists():shutil.copy2(p,b)
 s=p.read_text()
 for a,b in changes:
  assert a in s,(name,a[:100]);s=s.replace(a,b)
 p.write_text(s)
edit('Unity/Assets/Resources/Tennis/Rendering/TennisURP_Renderer.asset', [('postProcessData: {fileID: 0}','postProcessData: {fileID: 11400000, guid: 41439944d30ece34e96484bdb6645b55, type: 2}')])
edit('Unity/Assets/Editor/UrpSetup.cs',[('            AddSsao(renderer);','            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");\n            EditorUtility.SetDirty(renderer);\n            AddSsao(renderer);')])
edit('Unity/Assets/Scripts/Tennis/TennisLook.cs',[
 ('            var data = camera.GetUniversalAdditionalCameraData();\n            bool on = TennisQuality.Current != TennisQuality.Tier.Low;','            var existing = camera.GetComponent<SportsPostProcessing>();\n            if (existing) return existing.Volume;\n            var data = camera.GetUniversalAdditionalCameraData();\n            bool on = true;\n            camera.allowHDR = true;'),
 ('            var go = new GameObject("Tennis post-processing");','            var go = new GameObject("Sports post-processing");\n            go.transform.SetParent(camera.transform, false);\n            data.volumeLayerMask = 1 << go.layer;'),
 ('bloom.threshold.Override(1.35f); bloom.intensity.Override(.16f);','bloom.threshold.Override(1.05f); bloom.intensity.Override(.28f);'),
 ('grade.postExposure.Override(-.1f); grade.contrast.Override(4); grade.saturation.Override(3);','grade.postExposure.Override(0); grade.contrast.Override(5); grade.saturation.Override(8);'),
 ('            return volume;\n        }','            if (volume.profile.TryGet(out Vignette vignette)) vignette.intensity.Override(.12f);\n            if (volume.profile.TryGet(out LiftGammaGain lift)) lift.lift.Override(new Vector4(1,1,1,0));\n            camera.gameObject.AddComponent<SportsPostProcessing>().Initialize(volume);\n            return volume;\n        }'),
 ('SunIntensity = 1.75f, FillIntensity = .55f','SunIntensity = 1.65f, FillIntensity = .18f'),
 ('FillColor = new Color(.76f, .87f, 1f)','FillColor = new Color(.94f, .97f, 1f)'),
 ('RenderSettings.ambientGroundColor = new Color(.16f, .20f, .19f);','RenderSettings.ambientGroundColor = new Color(.26f, .30f, .24f);'),
 ('sun.shadowBias = .05f; sun.shadowNormalBias = .4f;','sun.shadowBias = .025f; sun.shadowNormalBias = .18f;')])
edit('Unity/Assets/Scripts/Tennis/TennisQuality.cs',[('urp.supportsHDR = tier != Tier.Low;','urp.supportsHDR = true; // Tone mapping stays active; Low drops bloom and AO.')])
edit('Unity/Assets/Scripts/Tennis/HeroLightingProfile.cs',[
 ('? 0f : .65f','? 0f : .12f'),
 ('            Shader.SetGlobalFloat("_HeroSkinPolish"','            Shader.SetGlobalFloat("_HeroFilmResponse",1);\n            Shader.SetGlobalFloat("_HeroSkinPolish"'),
 ('new Vector4(peak/Mathf.Max(.1f,white.r),peak/Mathf.Max(.1f,white.g),peak/Mathf.Max(.1f,white.b),1)','Vector4.one')])
edit('Unity/Assets/Scripts/Tennis/HeroRimLight.cs',[
 ('public const float Intensity = 1.8f;','public const float Intensity = .38f;'),
 ('            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;','            var sun = RenderSettings.sun;\n            if (sun) { transform.rotation = sun.transform.rotation; if (lit) lit.color = sun.color; return; }\n            if (!cam || !cam.isActiveAndEnabled) cam = Camera.main;')])
edit('Unity/Assets/Resources/Tennis/Shaders/HeroLighting.hlsl',[
 ('    if (_HeroProfile < .5) return c;','    if (_HeroFilmResponse > .5 || _HeroProfile < .5) return c;')])
edit('Unity/Assets/Resources/Tennis/Shaders/TennisCharacter.shader',[
 ('half3 rimLight = _RimColor.rgb * rim *','half3 rimLight = _RimColor.rgb * rim * saturate(dot(n,main.direction)*.7h+.3h) *')])
edit('Unity/Assets/Resources/Tennis/Shaders/TennisCloth.shader',[
 ('_RimColor.rgb * rim * (0.35','_RimColor.rgb * rim * saturate(dot(n,main.direction)*.7h+.3h) * (0.35'),
 ('colour = max(colour, albedo * lerp(.35h,4.0h,mask.b));','if (_HeroFilmResponse < .5h) colour = max(colour, albedo * lerp(.35h,4.0h,mask.b));')])
edit('Unity/Assets/Scripts/Course/GolfAtmosphere.cs',[
 ('SunIntensity = 2.3f','SunIntensity = 1.8f'),('FillIntensity = .8f','FillIntensity = .18f'),
 ('FillIntensity = .55f, FillColor = new(.62f, .55f, .90f)','FillIntensity = .14f, FillColor = new(.90f, .93f, 1f)'),
 ('SunColor = new(1f, .80f, .58f)','SunColor = new(1f, .92f, .80f)'),
 ('FigureExposure = new(.76f, .90f, 1.20f)','FigureExposure = Color.white'),
 ('BallExposure = new(.75f, .92f, 1.25f), BallSelfLight = .14f','BallExposure = Color.white, BallSelfLight = .04f'),
 ('AmbientSky = new(.34f, .24f, .40f), AmbientEquator = new(.33f, .36f, .31f), AmbientGround = new(.48f, .34f, .18f)','AmbientSky = new(.34f, .39f, .49f), AmbientEquator = new(.33f, .35f, .36f), AmbientGround = new(.38f, .29f, .20f)'),
 ('FogStart = 200','FogStart = 150'),('FogStart = 220','FogStart = 150'),('FogStart = 120','FogStart = 150'),
 ('sun.shadowBias = .05f; sun.shadowNormalBias = .4f;','sun.shadowBias = .025f; sun.shadowNormalBias = .18f;'),
 ('            Shader.SetGlobalFloat("_HeroRim",.35f);\n            Shader.SetGlobalFloat("_HeroExposure",.9f);','            Shader.SetGlobalFloat("_HeroRim",.65f);\n            Shader.SetGlobalFloat("_HeroExposure",1f);\n            GolfArcade.Tennis.HeroRimLight.Ensure();'),
 ('            data.renderPostProcessing = false;','            GolfArcade.Tennis.TennisLook.SetupPost(camera);\n            data.renderPostProcessing = true;')])
edit('Unity/Assets/Scripts/Course/GolfCourseAtmosphere.cs',[
 ('look.FillIntensity = volcanic ? .35f : .28f','look.FillIntensity = volcanic ? .14f : .18f'),
 ('look.FogStart = 550; look.FogEnd = volcanic ? 2600 : 2400','look.FogStart = 150; look.FogEnd = volcanic ? 1500 : 1800'),
 ('look.FogStart = 650; look.FogEnd = 2300','look.FogStart = 150; look.FogEnd = 1500'),
 ('look.FillIntensity=.30f','look.FillIntensity=.18f'),('look.FillIntensity = .30f','look.FillIntensity = .18f')])
edit('Unity/Assets/Scripts/Course/GolfTurfPalette.cs',[
 ('low=new(.355f,.440f,.285f); high=new(.425f,.505f,.350f)','low=new(.40f,.60f,.19f); high=new(.61f,.77f,.30f)'),
 ('low=new(.300f,.415f,.220f); high=new(.405f,.515f,.310f)','low=new(.30f,.54f,.12f); high=new(.55f,.73f,.23f)'),
 ('low=new(.245f,.395f,.215f); high=new(.370f,.505f,.290f)','low=new(.25f,.51f,.11f); high=new(.51f,.71f,.23f)'),
 ('low=new(.165f,.345f,.205f); high=new(.255f,.445f,.260f)','low=new(.17f,.40f,.14f); high=new(.32f,.57f,.20f)'),
 ('low=new(.170f,.315f,.180f); high=new(.370f,.455f,.245f)','low=new(.16f,.35f,.12f); high=new(.37f,.54f,.20f)'),
 ('new Color(.91f,.89f,.91f); high*=new Color(.93f,.91f,.90f)','new Color(.97f,1.02f,.95f); high*=new Color(.98f,1.02f,.95f)'),
 ('role==GolfCourseLook.Surface.Fairway?.045f:role==GolfCourseLook.Surface.Tee?.010f:role==GolfCourseLook.Surface.Green?.004f:0','role==GolfCourseLook.Surface.Fairway?.22f:role==GolfCourseLook.Surface.Tee?.16f:role==GolfCourseLook.Surface.Green?.10f:0'),
 ('SetFloat("_Bands",.045f)','SetFloat("_Bands",.22f)')])
edit('Unity/Assets/Scripts/Course/GolfCoastalTurf.cs',[
 ('new(){7,8,9,12,13,14,15,19,20}','new(){7,8,9,12,13,14,15,16,17,18,19,20,21,22,23}'),
 ('number is 7 or 8 or 9 or 12 or 13 or 14 or 15 or 19 or 20 || CoverageCandidate(number)','number is 7 or 8 or 9 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21 or 22 or 23 || CoverageCandidate(number)')])
edit('Unity/Assets/Scripts/Game/CameraRig.cs',[
 ('(putting ? 4.8f : 5.5f) : (putting ? 3.3f : 4.3f)','(putting ? 4.4f : 4.65f) : (putting ? 3.3f : 3.85f)'),
 ('Vector3.up * (portrait ? 2.3f : 1.85f)','Vector3.up * (portrait ? 2.45f : 2.05f)'),
 ('course.Hole.Number == 12','course.Hole.Number is 9 or 12')])
edit('Unity/Assets/Scripts/Tennis/TennisShoulderCamera.cs',[
 ('5.8f, player.z - 5.8f','7.0f, player.z - 7.8f'),('aspect < 1 ? 58 : 54','aspect < 1 ? 49 : 46')])
edit('Unity/Assets/Resources/Course/Shaders/GolfLava.shader',[
 ('                half fog = InitializeInputDataFog','                // Keep the cooling crust dark while hot channels cross the HDR bloom threshold.\n                colour *= lerp(1.0h, 4.2h, smoothstep(.06h,.55h,max(colour.r,max(colour.g,colour.b))));\n                half fog = InitializeInputDataFog')])
print('Updated rendering, lighting, palettes, camera and six accepted grass routes.')

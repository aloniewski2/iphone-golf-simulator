using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Tennis;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GolfArcade.EditorTools
{
    /// Screenshots of a tennis venue from the gameplay camera, the drone path and a few outside angles:
    ///   HERO_VENUE=skyscraper Unity -projectPath Unity -executeMethod GolfArcade.EditorTools.TennisVenueCapture.Run
    /// Writes ArtDir/screenshots/venues/<venue>_<shot>.png, or into HERO_VENUE_OUT when that is set
    /// (proof runs use it so the shipped captures are never overwritten).
    /// HERO_VENUE_DUMP=1 also logs the arena kit's props and the venue's triangle counts (read-only).
    [InitializeOnLoad]
    public static class TennisVenueCapture
    {
        const string Flag = "TennisVenueCapture";
        static int frame, lastFrame=-1;
        static System.Collections.IEnumerator capture;
        static readonly System.Collections.Generic.Stack<System.Collections.IEnumerator> steps=new();
        static string Out
        {
            get
            {
                var o = System.Environment.GetEnvironmentVariable("HERO_VENUE_OUT");
                return Path.GetFullPath(string.IsNullOrEmpty(o) ? "../ArtDir/screenshots/venues" : o);
            }
        }

        static TennisVenueCapture() { EditorApplication.update += Tick; }

        public static void Run()
        {
            frame=0;lastFrame=-1;capture=null;steps.Clear();
            if(System.Environment.GetEnvironmentVariable("HERO_VENUE_POST_REVIEW")=="1"){
                var renderer=Resources.Load<UniversalRendererData>("Tennis/Rendering/TennisURP_Renderer");
                renderer.postProcessData=AssetDatabase.LoadAssetAtPath<PostProcessData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset");
                renderer.SetDirty();
            }
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            if(Time.frameCount==lastFrame)return;lastFrame=Time.frameCount;
            var game = Object.FindFirstObjectByType<TennisGame>();
            if (!game || !game.Initialized) return;
            if (++frame < 45) return;   // let particles prewarm and Start()s run
            try {
                if(capture==null){capture=Capture(game);steps.Push(capture);}
                while(steps.Count>0){
                    var step=steps.Peek();
                    if(!step.MoveNext()){steps.Pop();continue;}
                    if(step.Current is System.Collections.IEnumerator nested){steps.Push(nested);continue;}
                    return;
                }
                Finish(0);
            }
            catch (System.Exception e) { Debug.LogError("[VenueCapture] " + e); Finish(1); }
        }

        static void Finish(int result) {
            if(System.Environment.GetEnvironmentVariable("HERO_VENUE_POST_REVIEW")=="1"){
                var renderer=Resources.Load<UniversalRendererData>("Tennis/Rendering/TennisURP_Renderer");
                renderer.postProcessData=null;renderer.SetDirty();Shader.SetGlobalFloat("_HeroFilmResponse",0);
            }
            SessionState.SetBool(Flag,false);
            if(Application.isBatchMode)EditorApplication.Exit(result);else EditorApplication.isPlaying=false;
        }

        static System.Collections.IEnumerator Capture(TennisGame game)
        {
            var reviewSex=System.Environment.GetEnvironmentVariable("HERO_VENUE_CHARACTER_SEX");
            if(reviewSex=="male" || reviewSex=="female"){
                game.SelectCharacter(reviewSex=="female");
                for(int settle=0;settle<3;settle++)yield return null;
            }
            var captureHero=game.Player.GetComponentInChildren<MatchHeroLook>();
            var authoredCaptureTone=captureHero && captureHero.body && captureHero.body.sharedMaterial
                ? ColorUtility.ToHtmlStringRGB(captureHero.body.sharedMaterial.GetColor("_BaseColor")) : "missing";
            var reviewSkin=System.Environment.GetEnvironmentVariable("HERO_VENUE_CHARACTER_SKIN");
            if(!string.IsNullOrEmpty(reviewSkin)){
                if(!ColorUtility.TryParseHtmlString("#"+reviewSkin.TrimStart('#'),out var tone))
                    throw new System.ArgumentException("Invalid HERO_VENUE_CHARACTER_SKIN: "+reviewSkin);
                var captureStyle=game.PlayerLook ?? HeroKit.Style.From(HeroKit.DefaultSkin,HeroKit.DefaultHair,0,default,5,game.FemalePlayer);
                captureStyle.SkinTint=tone;game.SetPlayerLook(captureStyle);
                for(int settle=0;settle<2;settle++)yield return null;
            }
            var activeCaptureTone=captureHero && captureHero.body && captureHero.body.sharedMaterial
                ? ColorUtility.ToHtmlStringRGB(captureHero.body.sharedMaterial.GetColor("_BaseColor")) : "missing";
            var toneAudit=$"sex={(game.FemalePlayer ? "female":"male")}; sourceDefault={authoredCaptureTone}; selectedOverride={reviewSkin ?? "none"}; activeBaseColor={activeCaptureTone}; playerLook={game.PlayerLook.HasValue}; skinPolish={Shader.GetGlobalFloat("_HeroSkinPolish")}; presentationFill={Shader.GetGlobalFloat("_HeroPresentationFill")}; filmResponse={Shader.GetGlobalFloat("_HeroFilmResponse")}";
            Debug.Log("[VenueCapture skin] "+toneAudit);
            File.WriteAllText(Path.Combine(Out,"skin-light-audit.txt"),toneAudit+"\n");
            HeroGarmentLOD.ForceFullDetail=true;
            foreach(var lod in Object.FindObjectsByType<HeroGarmentLOD>(FindObjectsSortMode.None))lod.Prepare(game.GameplayCamera);
            foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))skin.updateWhenOffscreen=true;
            var presentation=game.GetComponent<TennisPresentation>();if(presentation)presentation.Finish();
            var name = TennisVenue.Current.ToString().ToLowerInvariant();
            game.enabled = false;
            var venueRoot = GameObject.Find(TennisVenue.Current + " venue");
            if (venueRoot) foreach (Transform c in venueRoot.transform) { var r = c.GetComponent<Renderer>(); var mf = c.GetComponent<MeshFilter>(); if (r && (c.name.Contains("ground") || c.name.Contains("Grass") || c.name.Contains("Volcano") || c.name.Contains("Sea")))
                Debug.Log($"[VenueCapture] {c.name}: enabled={r.enabled} verts={(mf && mf.sharedMesh ? mf.sharedMesh.vertexCount : 0)} bounds={r.bounds.center}/{r.bounds.size} mat={(r.sharedMaterial ? r.sharedMaterial.name + " " + r.sharedMaterial.shader.name : "null")}"); }
            // debug aids: HERO_VENUE_HIDE=name,name hides venue groups whose name matches; HERO_VENUE_ONLY=3_drone5,1_gameplay captures only those shots
            var hide = System.Environment.GetEnvironmentVariable("HERO_VENUE_HIDE");
            if (!string.IsNullOrEmpty(hide) && venueRoot)
                foreach (var rr in venueRoot.GetComponentsInChildren<Renderer>(true))
                    foreach (var h in hide.Split(',')) if (rr.name.Contains(h) || (rr.transform.parent && rr.transform.parent.name.Contains(h))) rr.enabled = false;
            var only = System.Environment.GetEnvironmentVariable("HERO_VENUE_ONLY");
            if (System.Environment.GetEnvironmentVariable("HERO_VENUE_DUMP") == "1") Dump(name, venueRoot);
            var cam = game.GameplayCamera;
            bool reviewPost=System.Environment.GetEnvironmentVariable("HERO_VENUE_POST_REVIEW")=="1";
            if(reviewPost){cam.allowHDR=true;cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;}
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            System.Collections.IEnumerator Shot(string label, Vector3 pos, Vector3 look, float fov)
            {
                if (!string.IsNullOrEmpty(only) && System.Array.IndexOf(only.Split(','), label) < 0) yield break;
                cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation(look - pos); cam.fieldOfView = fov;
                // A real player frame must update skinning after culling/lens changes. Repeated
                // synchronous Camera.Render calls can omit the body's first Metal draw.
                for(int warm=0;warm<3;warm++)yield return null;
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
                File.WriteAllBytes($"{Out}/{name}_{label}.png", tex.EncodeToPNG());
                if(reviewPost){
                    bool brightReview=System.Environment.GetEnvironmentVariable("HERO_VENUE_POST_BRIGHT")=="1";
                    foreach(var mode in new[]{TonemappingMode.Neutral,TonemappingMode.ACES}){
                        foreach(var volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None)){
                            if(!volume.profile)continue;
                            if(volume.profile.TryGet(out Tonemapping tone))tone.mode.Override(mode);
                            if(volume.profile.TryGet(out ColorAdjustments grade)){grade.postExposure.Override(brightReview?.80f:.25f);grade.contrast.Override(brightReview?6:0);grade.saturation.Override(brightReview?6:0);grade.colorFilter.Override(Color.white);}
                            if(volume.profile.TryGet(out Bloom bloom))bloom.active=false;
                            if(volume.profile.TryGet(out Vignette vignette))vignette.active=false;
                            if(volume.profile.TryGet(out DepthOfField dof))dof.active=false;
                        }
                        Shader.SetGlobalFloat("_HeroFilmResponse",1);cam.GetUniversalAdditionalCameraData().renderPostProcessing=true;
                        yield return null;
                        cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();RenderTexture.active=null;cam.targetTexture=null;
                        File.WriteAllBytes($"{Out}/{name}_{label}_{mode}.png",tex.EncodeToPNG());
                    }
                    cam.GetUniversalAdditionalCameraData().renderPostProcessing=false;Shader.SetGlobalFloat("_HeroFilmResponse",0);
                }
            }
            TennisShoulderCamera.Frame(game.Player.transform.position,false,Vector3.zero,false,16f/9f,
                out var gameplayPosition,out var gameplayLook,out var gameplayFov);
            yield return Shot("1_gameplay",gameplayPosition,gameplayLook,gameplayFov);
            yield return Shot("2_postcard", new Vector3(0, 9f, -30f), new Vector3(0, 1f, 6f), 46);
            for (int i = 0; i <= 5; i++)
            {
                TennisVenue.Drone(i / 5f, out var p, out var l, out var f);
                yield return Shot($"3_drone{i}", p, l, f);
            }
            yield return Shot("4_side", new Vector3(58, 10, -46), new Vector3(0, -4, 0), 50);
            yield return Shot("5_below", new Vector3(34, -26, -70), new Vector3(0, -6, 0), 55);
            yield return Shot("6_edge", new Vector3(-9.5f, 2.2f, -18f), new Vector3(-19, -6, 2), 62);
            // review extras (not part of the shipped set): the view from the far end back over the near end, and the rival's intro
            yield return Shot("7_rear", new Vector3(0, 3.5f, 15.6f), new Vector3(0, 1.15f, 3.2f), 56);
            yield return Shot("8_rivalintro", new Vector3(-1.6f, 1.25f, 6.2f), new Vector3(0, 1f, 11.2f), 40);
            if(System.Environment.GetEnvironmentVariable("HERO_VENUE_CHARACTER")=="1")
            {
                yield return Shot("15_face",new Vector3(-.65f,1.57f,9.05f),new Vector3(0,1.48f,11.2f),26);
                var playerLook=game.Player.GetComponentInChildren<MatchHeroLook>();
                var playerHead=playerLook ? playerLook.Bone(HumanBodyBones.Head):null;
                if(playerHead){
                    var target=playerHead.position+Vector3.up*.06f;
                    yield return Shot("16_player_face",target+game.Player.transform.forward*1.65f+game.Player.transform.right*.25f+Vector3.up*.04f,target,24);
                    var centre=game.Player.transform.position+Vector3.up*.88f;
                    yield return Shot("17_player_intro",centre+game.Player.transform.forward*3.6f+game.Player.transform.right*1.1f+Vector3.up*.08f,centre,34);
                }
            }
            // Supplemental implemented camera, alongside unchanged gameplay/diagnostic views.
            yield return Shot("9_hero_postcard", new Vector3(2.6f,2.2f,-17.2f), new Vector3(0,.85f,3.5f),52);
            if(System.Environment.GetEnvironmentVariable("HERO_VENUE_REFERENCE")=="1")
                yield return Shot("14_reference_camera",new Vector3(2,1.80f,-15.3f),new Vector3(0,1,7),54);
            if(System.Environment.GetEnvironmentVariable("HERO_VENUE_CAMERA_GRID")=="1")
            {
                // Review-only physical camera choices. Each is rendered with the
                // exact production net/court/hero; generated-image proportions
                // are not presumed to describe a physically consistent lens.
                var views=new (string label,Vector3 from,Vector3 at,float fov)[] {
                    ("15_reference_a",new Vector3(2.2f,2.4f,-16.2f),new Vector3(0,.35f,1),40),
                    ("15_reference_b",new Vector3(2.2f,2.8f,-16.7f),new Vector3(0,.35f,1),40),
                    ("15_reference_c",new Vector3(2.2f,3.4f,-17.5f),new Vector3(0,.35f,1),36),
                    ("15_reference_d",new Vector3(2.2f,3.6f,-18.0f),new Vector3(0,.35f,1),38),
                    ("15_reference_e",new Vector3(1.5f,2.8f,-16.7f),new Vector3(0,.35f,1),36),
                    ("15_reference_f",new Vector3(2.6f,3.2f,-17.3f),new Vector3(0,.55f,2),40),
                    ("15_reference_g",new Vector3(2.2f,3.4f,-16.7f),new Vector3(0,.40f,1.5f),44),
                    ("15_reference_h",new Vector3(1.6f,2.6f,-16.2f),new Vector3(0,.25f,.5f),38) };
                foreach(var view in views)yield return Shot(view.label,view.from,view.at,view.fov);
            }
            if(System.Environment.GetEnvironmentVariable("HERO_VENUE_CAMERA_GRID")=="1")
            {
                yield return Shot("16_reference_a",new Vector3(2.2f,2.8f,-16.3f),new Vector3(0,-.28f,1),40);
                yield return Shot("16_reference_b",new Vector3(2.2f,2.8f,-16.3f),new Vector3(0,-.58f,1),40);
                yield return Shot("16_reference_c",new Vector3(1.8f,2.7f,-16.0f),new Vector3(0,-.35f,1),42);
                yield return Shot("16_reference_d",new Vector3(2.2f,3.0f,-16.3f),new Vector3(0,-.15f,1),40);
            }
            if(System.Environment.GetEnvironmentVariable("HERO_VENUE_SURFACES")=="1")
            {
                yield return Shot("11_surface_close",new Vector3(2.8f,.82f,-10),new Vector3(0,.05f,-6),52);
                if(TennisVenue.IsResort)
                {
                    yield return Shot("12_terrace_close",new Vector3(18,.80f,-13),new Vector3(22,.15f,-8),52);
                    var clubhouse=GameObject.Find("EnvV4_BeachHouse");
                    if(clubhouse)
                    {
                        bool valid=false;var houseBounds=new Bounds();
                        foreach(var renderer in clubhouse.GetComponentsInChildren<MeshRenderer>())
                        {
                            if(!renderer.enabled)continue;
                            if(!valid){houseBounds=renderer.bounds;valid=true;}else houseBounds.Encapsulate(renderer.bounds);
                        }
                        if(valid)
                        {
                            // The actual facade faces the court (-world x). Frame
                            // from outside the complete source bounds; no camera
                            // is placed within the gallery, roof or landscaping.
                            var target=new Vector3(houseBounds.min.x+houseBounds.size.x*.25f,
                                houseBounds.min.y+houseBounds.size.y*.56f,houseBounds.center.z);
                            float nearDistance=Mathf.Max(10,houseBounds.size.z*.85f);
                            yield return Shot("17_clubhouse_close",new Vector3(houseBounds.min.x-nearDistance,
                                target.y+1.1f,houseBounds.center.z-nearDistance*.52f),target,52);
                            yield return Shot("18_clubhouse_roof",new Vector3(houseBounds.min.x-nearDistance,
                                houseBounds.max.y+3,houseBounds.center.z-nearDistance*.55f),target,52);
                            Debug.Log("[VenueCapture] clubhouse actual bounds="+houseBounds+" target="+target);
                        }
                    }
                }
                else if(TennisVenue.Current==TennisVenueKind.Skyscraper)
                    yield return Shot("12_facade_close",new Vector3(-53,17,82),new Vector3(-84,20,128),52);
                else
                    yield return Shot("12_basalt_close",new Vector3(65,14,100),new Vector3(45,24,170),52);
            }
            if(TennisVenue.IsResort && System.Environment.GetEnvironmentVariable("HERO_VENUE_FIXTURES")=="1")
            {
                // The original source remains as a hidden render-only reference.
                // Frame each replaced family from two actual close camera angles.
                var sources=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
                foreach(string key in new[]{"017","027","031","033","032","020","024"})
                {
                    MeshRenderer source=null;float nearest=float.PositiveInfinity;
                    foreach(var renderer in sources)
                    {
                        if(!renderer.sharedMaterial || !renderer.sharedMaterial.name.StartsWith("TropicalV3_"+key))continue;
                        float sourceDistance=Vector3.Distance(renderer.bounds.center,new Vector3(0,0,-11));
                        if(sourceDistance<nearest){source=renderer;nearest=sourceDistance;}
                    }
                    if(!source){Debug.LogWarning("No source bounds for fixture "+key);continue;}
                    var bounds=source.bounds;var centre=bounds.center;
                    if(key=="027"||key=="031")centre.y=Mathf.Min(bounds.max.y-.12f,bounds.min.y+.75f);
                    float distance=key=="027"?5.5f:key=="032"?6.5f:key=="020"?4.5f:key=="024"?2.1f:3.5f;
                    float height=key=="024"?2.2f:.7f;
                    if(key=="017")
                    {
                        distance=Mathf.Max(3.5f,Mathf.Max(bounds.size.x,bounds.size.z)*1.7f+1.2f);
                        // Approach the pot from the open court, outside its entire
                        // original foliage envelope and away from the promenade lamp.
                        float courtSide=centre.x>0?-1:1;
                        yield return Shot("10_fixture_"+key+"_front",centre+new Vector3(courtSide*distance,.6f,-distance*.12f),centre,46);
                        yield return Shot("10_fixture_"+key+"_side",centre+new Vector3(courtSide*distance*.78f,.65f,-distance*.72f),centre,46);
                    }
                    else
                    {
                        yield return Shot("10_fixture_"+key+"_front",centre+new Vector3(0,height,-distance),centre,46);
                        yield return Shot("10_fixture_"+key+"_side",centre+new Vector3(distance*.8f,height*.8f,-distance*.6f),centre,46);
                    }
                    Debug.Log("[VenueCapture] fixture "+key+" original "+source.name+" centre="+bounds.center+" size="+bounds.size+" enabled="+source.enabled);
                }
            }
            Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
            Debug.Log("[VenueCapture] wrote " + Out);
        }

        static int Tris(Mesh m) { if (!m) return 0; int t = 0; for (int i = 0; i < m.subMeshCount; i++) t += (int)(m.GetIndexCount(i) / 3); return t; }

        /// Read-only report: which arena kit renderers are visible, where, and how many triangles the
        /// venue's own geometry costs (static meshes, plus particle caps counted as 2 triangles each).
        static void Dump(string name, GameObject venueRoot)
        {
            int venue = 0, kit = 0, other = 0, particles = 0, venueParts = 0;
            var perMat = new System.Collections.Generic.Dictionary<string, (int n, int tris)>();
            var byGroup = new System.Collections.Generic.Dictionary<string, (int n, int tris)>();
            var batches = new System.Collections.Generic.HashSet<Mesh>();
            foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                var r = mf.GetComponent<Renderer>(); if (!r || !r.enabled || !mf.sharedMesh) continue;
                // Static batching points every member's sharedMesh at the combined batch mesh: count each batch once.
                if (r.isPartOfStaticBatch && !batches.Add(mf.sharedMesh)) continue;
                int t = Tris(mf.sharedMesh);
                bool inVenue = venueRoot && mf.transform.IsChildOf(venueRoot.transform);
                string mat = r.sharedMaterial ? r.sharedMaterial.name.Replace(" (styled)", "").Replace(" (Instance)", "") : "none";
                if (inVenue)
                {
                    venue += t; venueParts++;
                    var top = mf.transform; while (top.parent && top.parent != venueRoot.transform) top = top.parent;
                    (int n, int tris) g = byGroup.TryGetValue(top.name, out var gv) ? gv : (0, 0); byGroup[top.name] = (g.n + 1, g.tris + t);
                }
                else if (mat.StartsWith("TropicalV3_")) { kit += t; (int n, int tris) e = perMat.TryGetValue(mat, out var v) ? v : (0, 0); perMat[mat] = (e.n + 1, e.tris + t);
                    if (System.Environment.GetEnvironmentVariable("HERO_VENUE_DUMP_KIT") == "1")
                        Debug.Log($"[VenueDump] kit {mat} '{mf.name}' tris={t} centre={r.bounds.center:F2} size={r.bounds.size:F2}"); }
                else other += t;
            }
            foreach (var ps in Object.FindObjectsByType<ParticleSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                particles += ps.main.maxParticles * 2;
                if (venueRoot && ps.transform.IsChildOf(venueRoot.transform))
                    Debug.Log($"[VenueDump] particles '{ps.name}' alive={ps.particleCount} playing={ps.isPlaying} max={ps.main.maxParticles} at={ps.transform.position:F0} bounds={ps.GetComponent<ParticleSystemRenderer>().bounds.size:F0}");
            }
            foreach (var kv in perMat) Debug.Log($"[VenueDump] kit material {kv.Key}: {kv.Value.n} renderers, {kv.Value.tris} tris");
            foreach (var kv in byGroup) Debug.Log($"[VenueDump] venue group '{kv.Key}': {kv.Value.n} mesh renderers, {kv.Value.tris} tris");
            int cams = 0; foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) if (l.enabled) cams++;
            Debug.Log($"[VenueDump] {name}: venue-owned static tris={venue} in {venueParts} mesh renderers | arena kit tris={kit} | other static tris={other} | particle cap tris~{particles} | active lights={cams}");
        }
    }
}

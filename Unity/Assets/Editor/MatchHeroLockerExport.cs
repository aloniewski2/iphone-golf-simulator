using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// HERO_MAINSTAY locker export: the Swift locker mirror (CharacterModelPreview.swift) shows the same two bodies the match plays.
    /// For each sex it poses the real prefab (Resources/Tennis/Customization/PlayerMale|PlayerFemale) on its own skeleton, skins the body on the CPU,
    /// and writes   GolfArcade/Unity/CharacterAssets/MatchHero_<Sex>.json         manifest (parts, submeshes, materials + the LOOK of every submesh, bounds, head centre, rig)
    ///              GolfArcade/Unity/CharacterAssets/MatchHero_<Sex>.lzfse         base pose = frame 0 of Male_ReadyIdle / Female_ReadyIdle (positions, normals, indices; UVs of the kit; bind-pose positions of the body)
    ///              GolfArcade/Unity/CharacterAssets/MatchHero_<Sex>_Swing_NN.lzfse   the match Forehand clip sampled at SwingTimes (positions + normals, same part order)
    ///              GolfArcade/Unity/CharacterAssets/MatchHero_<Sex>_Rig.lzfse     LOCKER_MIRROR: the bind-pose mesh of every skinned part with its bone weights and inverse bind poses, and the clips
    ///                                                                             <Sex>_ReadyIdle and <Sex>_Serve as bone tracks sampled at 60 Hz (the Swift side skins them on the GPU)
    ///              GolfArcade/Unity/CharacterAssets/MatchHero_<map>.png           the look maps the runtime materials sample (cloth weave, kit seam maps, soft skin normal), copied from Resources/Tennis/HeroDetail
    /// Parts: Body (skin), Face (painted features, one flat colour per submesh), the six worn-kit meshes Kit_Top, Kit_Bottom, Kit_Sock_L/R, Kit_Shoe_L/R (DRESS_MATCH_HEROES; kind "kit", one flat
    /// colour per role material: Kit_Shirt, Kit_ShirtTrim, Kit_Shorts, Kit_ShortsBand, Kit_Shoe, Kit_Sole, Kit_Sock - the Swift side recolours them from the locker picks), the racket meshes
    /// (frame / strings / grip / butt cap), at the size the court draws them (TennisRules.HeroRacketScale about the grip).
    /// There is no hair and no headwear in the file: the heroes are bald.
    /// LOCKER_MIRROR: the look numbers in the manifest are read off the hero's RUNTIME materials (MatchHeroLook.OwnMaterials is run on the instance, exactly what Awake does in the game),
    /// so the mirror samples what the court draws: TennisCloth / TennisCharacter parameters, the per-piece weave tile, the kit's seam maps and the skin's soft normal.
    /// Coordinates are Unity metres with z negated and the winding flipped (the hero faces -z in the file, like the old locker export); the Swift side turns it to the camera.
    ///   Unity -batchmode -nographics -quit -projectPath Unity -executeMethod GolfArcade.EditorTools.MatchHeroLockerExport.Run
    public static class MatchHeroLockerExport
    {
        const string PrefabDir = "Assets/Resources/Tennis/Customization/";
        static string OutDir => Path.GetFullPath(Environment.GetEnvironmentVariable("MH_LOCKER_OUT") ?? "../GolfArcade/Unity/CharacterAssets");
        static string Report => Path.GetFullPath(Environment.GetEnvironmentVariable("MH_LOCKER_REPORT") ?? "../work/hero-mainstay/logs/locker_export.txt");
        /// Where the golden samples for the Swift tests go (positions of a few hundred vertices at known clip times, straight from the CPU skinning).
        static string GoldenDir => Environment.GetEnvironmentVariable("MH_LOCKER_GOLDEN");

        /// The match Forehand (1.2 s, contact at 0.667 s) sampled densely around the strike, where the racket moves fastest (a morph blends vertex positions linearly).
        public static readonly float[] SwingTimes = { 0.00f, 0.15f, 0.30f, 0.42f, 0.50f, 0.56f, 0.62f, 0.667f, 0.72f, 0.78f, 0.90f, 1.20f };

        /// Bone tracks are sampled at this rate (the clips are authored at 30 fps; 60 Hz keeps the curves' own smoothness and the Swift side interpolates between samples).
        const float ClipRate = 60f;
        /// The clips the menus play, with the id the Swift side looks them up by.
        static readonly (HeroTennisDriver.Clip slot, string id, bool loop)[] RigClips = {
            (HeroTennisDriver.Clip.Ready, "ready", true), (HeroTennisDriver.Clip.Serve, "serve", false),
            (HeroTennisDriver.Clip.EmoteScuba, "scuba", false), (HeroTennisDriver.Clip.EmoteThrust, "thrust", false),
            (HeroTennisDriver.Clip.EmoteSpike, "spike", false), (HeroTennisDriver.Clip.IntroWave, "wave", false),
            (HeroTennisDriver.Clip.IntroBringIt, "bringIt", false), (HeroTennisDriver.Clip.IntroPushups, "pushups", false)
        };
        static readonly Matrix4x4 FlipZ = Matrix4x4.Scale(new Vector3(1, 1, -1));

        [Serializable]
        class Look
        {
            /// "TennisCloth", "TennisCharacter", or "Lit" (a hero material that is not on one of the hero shaders: the export refuses it).
            public string shader;
            public float[] color, rimColor, subsurface;
            public float smoothness, wrap, rimStrength, rimPower, exposure, knee, weaveTile, weaveAngle, weaveNormal, weaveThread, sheenStrength, sheenPower, saturation, bumpScale, bumpTriplanar, bumpTile;
            /// Cloth only: the size in metres of one weave tile (MatchHeroLook.ClothFor(role).tileMetres): weaveTile = metres per UV unit / tileMetres.
            public float tileMetres;
            /// Shipped file names (no extension; "" = none): the kit seam map, the weave map, the skin's soft normal.
            public string baseMap = "", weaveMap = "", bumpMap = "", normalMap = "", maskMap = "";
            public float[] trimColor; public float normalStrength, useGarmentMaps, fabricVersion;
        }
        [Serializable] class Sub { public string material; public int indexOffset, indexCount; public Look look; }
        [Serializable] class PartInfo { public string name, kind; public int vertexCount, positionOffset, normalOffset, swingPositionOffset, swingNormalOffset, uvOffset = -1, bindPositionOffset = -1; public Sub[] submeshes; }
        [Serializable] class MatInfo { public string name; public float[] color; public float smoothness; }
        [Serializable] class RigPart { public string part, kind; public int vertexCount, bindPositionOffset, bindNormalOffset, weightOffset, indexOffset, inverseBindOffset, track = -1; public int[] bones; }
        [Serializable] class ClipInfo { public string id, name; public bool loop; public float length, fps, contact; public int frames, offset; public float[] boundsMin, boundsMax, times; }
        [Serializable] class Rig { public string file; public string[] bones, rigid; public int trackCount, floatsPerTrack = 10; public RigPart[] parts; public ClipInfo[] clips; }
        [Serializable]
        class Manifest
        {
            public int version = 2; public string sex, source, baseClip, swingClip; public float baseTime, swingLength, swingContact;
            public PartInfo[] parts; public MatInfo[] materials; public string[] swingFrames; public float[] swingTimes;
            public float[] boundsMin, boundsMax; public float height, headCentreY, headTopY, headRadius;
            public Rig rig;
            public string coordinates = "Unity metres, z negated, winding flipped (hero faces -z)";
        }

        /// One exported renderer: the vertices in hero-root space for a pose.
        class PartSource
        {
            public string name, kind; public Mesh mesh; public Renderer renderer; public SkinnedMeshRenderer skinned; public Material[] materials;
            public Vector3[] pos, nrm;
        }

        [MenuItem("Golf Arcade/Match Heroes/Export Locker Assets")]
        public static void Run()
        {
            var log = new StringBuilder();
            int code = 0;
            try
            {
                Directory.CreateDirectory(OutDir);
                foreach (var sex in new[] { "Male", "Female" }) log.AppendLine(ExportSex(sex));
            }
            catch (Exception e) { Debug.LogException(e); log.AppendLine("EXCEPTION " + e); code = 1; }
            Directory.CreateDirectory(Path.GetDirectoryName(Report));
            File.WriteAllText(Report, log.ToString());
            Debug.Log("[MatchHeroLockerExport]\n" + log);
            if (Application.isBatchMode) EditorApplication.Exit(code);
        }

        /// Live Unity frames for the side-by-side SceneKit parity recorder. Run with graphics enabled.
        public static void CaptureEmoteParity()
        {
            int code=0;
            try {
                string output=Environment.GetEnvironmentVariable("MH_EMOTE_FRAMES"); if(string.IsNullOrEmpty(output))throw new ArgumentException("MH_EMOTE_FRAMES required");
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
                var camera=new GameObject("ParityCamera").AddComponent<Camera>(); camera.fieldOfView=32; camera.nearClipPlane=.05f;
                camera.transform.position=new Vector3(0,.9f,4.6f);camera.transform.LookAt(new Vector3(0,.8f,0));camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.98f,.97f,.95f);
                RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.86f,.88f,.96f)*.21f;
                void Light(float intensity,Color color,float elevation,float azimuth) {
                    var l=new GameObject("ParityLight").AddComponent<Light>(); l.type=LightType.Directional;l.intensity=intensity;l.color=color;
                    float e=elevation*Mathf.Deg2Rad,a=azimuth*Mathf.Deg2Rad;l.transform.position=new Vector3(-Mathf.Sin(a)*Mathf.Cos(e)*6,Mathf.Sin(e)*6,Mathf.Cos(a)*Mathf.Cos(e)*6);l.transform.LookAt(new Vector3(0,.8f,0));
                }
                Light(1.05f,new Color(1,.92f,.82f),40,38);Light(.37f,new Color(.84f,.9f,1),18,-55);Light(.52f,new Color(1,.96f,.9f),35,160);
                var rt=new RenderTexture(480,640,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.antiAliasing=4;rt.Create();camera.targetTexture=rt;
                var tex=new Texture2D(480,640,TextureFormat.RGB24,false);var previous=RenderTexture.active;
                foreach(string sex in new[]{"Male","Female"}) {
                    var root=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir+"Player"+sex+".prefab"));
                    var driver=root.GetComponent<HeroTennisDriver>();var look=root.GetComponent<MatchHeroLook>();typeof(MatchHeroLook).GetMethod("OwnMaterials",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(look,null);
                    root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Racket_Classic").localScale*=TennisRules.HeroRacketScale;
                    foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>()){skin.quality=SkinQuality.Bone4;skin.updateWhenOffscreen=true;}
                    var ready=driver.slots.First(s=>s.id==HeroTennisDriver.Clip.Ready).clip;ready.SampleAnimation(root,0);
                    // Manual SampleAnimation in a batch loop does not advance Unity's GPU skinning frame cache.
                    // Draw the live clip's CPU skinning in Unity, using exactly the same Pose path independently checked against BakeMesh.
                    var sources=Collect(root);foreach(var source in sources)Pose(root,source);
                    var bounds=new Bounds();bool has=false;foreach(var source in sources.Where(p=>p.kind=="body"||p.kind=="face"))foreach(var v in source.pos){if(!has){bounds=new Bounds(v,Vector3.zero);has=true;}else bounds.Encapsulate(v);}
                    var drawMeshes=new List<Mesh>();
                    foreach(var source in sources){source.renderer.enabled=false;var node=new GameObject("UnityCPUSkin_"+source.name);node.transform.SetParent(root.transform,false);var mesh=Object.Instantiate(source.mesh);node.AddComponent<MeshFilter>().sharedMesh=mesh;node.AddComponent<MeshRenderer>().sharedMaterials=source.materials;drawMeshes.Add(mesh);}
                    root.transform.localScale=Vector3.one*(1.387f/bounds.size.y);root.transform.rotation=Quaternion.Euler(0,-.35f*Mathf.Rad2Deg,0);
                    foreach(var entry in RigClips.Where(e=>e.id=="scuba"||e.id=="pushups")) {
                        var clip=driver.slots.First(s=>s.id==entry.slot).clip;string dir=Path.Combine(output,sex.ToLower()+"_"+entry.id);Directory.CreateDirectory(dir);
                        int frames=Mathf.CeilToInt((clip.length+1.3f)*30)+1;
                        for(int frame=0;frame<frames;frame++) {
                            float time=frame/30f-.3f;ready.SampleAnimation(root,0);if(time>=0&&time<=clip.length+.2f)clip.SampleAnimation(root,Mathf.Min(clip.length,time));
                            for(int part=0;part<sources.Count;part++){Pose(root,sources[part]);drawMeshes[part].vertices=sources[part].pos;drawMeshes[part].normals=sources[part].nrm;drawMeshes[part].RecalculateBounds();}
                            camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,480,640),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(dir,frame.ToString("D5")+".png"),tex.EncodeToPNG());
                        }
                    }
                    Object.DestroyImmediate(root);
                }
                RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
            }catch(Exception e){Debug.LogException(e);code=1;}
            if(Application.isBatchMode)EditorApplication.Exit(code);
        }

        public static void RunGolf() { RunVariant(true); }
        static void RunVariant(bool golf) {
            try { Directory.CreateDirectory(OutDir); foreach(string sex in new[]{"Male","Female"}) Debug.Log(ExportSex(sex,golf)); if(Application.isBatchMode)EditorApplication.Exit(0); }
            catch(Exception e){Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);else throw;}
        }

        static string ExportSex(string sex, bool golf = false)
        {
            var sb = new StringBuilder("== " + sex + "\n");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Player" + sex + (golf ? "GolfKit" : "") + ".prefab");
            if (!prefab) throw new FileNotFoundException("prefab " + sex);
            var root = (GameObject)Object.Instantiate(prefab);
            try
            {
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); root.transform.localScale = Vector3.one;
                var driver = root.GetComponent<HeroTennisDriver>();
                // What Awake does in the game: the hero's own material copies move onto TennisCharacter / TennisCloth with their maps. Edit mode does not run Awake, so it is called here.
                // Everything below reads the materials AFTER this, so the manifest carries the numbers the court draws.
                var look = root.GetComponent<MatchHeroLook>();
                var own = typeof(MatchHeroLook).GetMethod("OwnMaterials", BindingFlags.Instance | BindingFlags.NonPublic);
                if (!look || own == null) throw new InvalidOperationException("MatchHeroLook.OwnMaterials not found");
                own.Invoke(look, null);
                // the court draws the racket TennisRules.HeroRacketScale bigger about its grip (HeroTennisDriver.Build; the arcade string bed is wider than a real one):
                // the locker mirror shows the same racket the match plays with
                var racketRoot = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Racket_Classic");
                racketRoot.localScale *= TennisRules.HeroRacketScale;
                var ready = driver.slots.First(s => s.id == HeroTennisDriver.Clip.Ready).clip;
                var fore = driver.slots.First(s => s.id == HeroTennisDriver.Clip.Forehand);
                var sources = Collect(root);
                sb.AppendLine("  parts: " + string.Join(", ", sources.Select(p => $"{p.name}[{p.kind}] v={p.mesh.vertexCount} subs={p.mesh.subMeshCount}")));
                foreach (var p in sources) if (!p.mesh.isReadable) throw new InvalidOperationException(p.name + " mesh is not readable");

                // ---- base pose
                ready.SampleAnimation(root, 0f);
                if(golf)root.GetComponent<GolfGarmentCorrectives>()?.Apply();
                foreach (var p in sources) Pose(root, p);
                var bounds = new Bounds(); bool first = true;
                foreach (var p in sources.Where(s => s.kind == "body")) foreach (var v in p.pos) { if (first) { bounds = new Bounds(v, Vector3.zero); first = false; } else bounds.Encapsulate(v); }
                // head: the vertices of the body above the neck bone; the centre of their box is the locker's head target
                var neck = FindBone(root.transform, "Neck"); var head = FindBone(root.transform, "Head");
                float neckY = neck.position.y;
                var headBounds = new Bounds(); bool hf = true;
                foreach (var p in sources.Where(s => s.kind == "body" || s.kind == "face")) foreach (var v in p.pos) if (v.y > neckY + .02f) { if (hf) { headBounds = new Bounds(v, Vector3.zero); hf = false; } else headBounds.Encapsulate(v); }
                sb.AppendLine($"  base pose {ready.name} t=0: body bounds min={bounds.min:F3} max={bounds.max:F3} height={bounds.size.y:F3} m; neck y={neckY:F3} head bone y={head.position.y:F3}; head box centre y={headBounds.center.y:F3} top={headBounds.max.y:F3} size={headBounds.size:F3}");

                // CPU skin check against Unity's own BakeMesh (SkinnedMeshRenderer local space -> world). BakeMesh obeys the quality tier's bone limit, so the check runs at four bones
                var keepWeights = QualitySettings.skinWeights; QualitySettings.skinWeights = SkinWeights.FourBones;
                foreach (var p in sources.Where(s => s.skinned))
                {
                    var baked = new Mesh(); p.skinned.BakeMesh(baked, true); var bv = baked.vertices; var l2w = p.skinned.transform.localToWorldMatrix; var inv = root.transform.worldToLocalMatrix;
                    float worst = 0, sum = 0; int over1 = 0;
                    for (int i = 0; i < bv.Length; i++) { float d = Vector3.Distance(inv.MultiplyPoint3x4(l2w.MultiplyPoint3x4(bv[i])), p.pos[i]); worst = Mathf.Max(worst, d); sum += d; if (d > .001f) over1++; }
                    sb.AppendLine($"  CPU skin vs Unity BakeMesh (tier skinWeights was {keepWeights}): {p.name} max deviation {worst * 1000f:F3} mm, mean {sum / bv.Length * 1000f:F4} mm, {over1} verts over 1 mm, of {bv.Length}");
                    Object.DestroyImmediate(baked);
                    if (worst > .002f) throw new InvalidOperationException(p.name + " CPU skinning deviates from BakeMesh by " + worst * 1000f + " mm");
                }
                QualitySettings.skinWeights = keepWeights;

                // ---- materials (flat colours; the body skin is tinted at run time by the locker's skin pick, the racket frame by the racket colour)
                var mats = new Dictionary<string, MatInfo>();
                string Key(Material m)
                {
                    string n = m ? m.name.Replace(" (Instance)", "").Replace(" (hero)", "").Replace("MatchHero_" + sex + "_", "") : "Missing";
                    if (m && !mats.ContainsKey(n))
                    {
                        var c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                        mats[n] = new MatInfo { name = n, color = new[] { c.r, c.g, c.b, c.a }, smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : .3f };
                    }
                    return n;
                }
                var copiedMaps = new SortedSet<string>();

                // ---- base file: per part positions | normals | indices (| UVs of the kit | bind-pose positions of the body)
                var bin = new List<byte>(); var infos = new List<PartInfo>();
                foreach (var p in sources)
                {
                    var info = new PartInfo { name = p.name, kind = p.kind, vertexCount = p.pos.Length };
                    info.positionOffset = bin.Count; AddV3(bin, p.pos, true, p.kind == "kit" ? 10 : 0);
                    info.normalOffset = bin.Count; AddV3(bin, p.nrm, true, p.kind == "kit" ? 12 : 0);
                    var subs = new List<Sub>();
                    for (int s = 0; s < p.mesh.subMeshCount; s++)
                    {
                        var idx = p.mesh.GetIndices(s); if (idx.Length == 0) continue;
                        var mat = p.materials[Mathf.Min(s, p.materials.Length - 1)];
                        subs.Add(new Sub { material = Key(mat), indexOffset = bin.Count, indexCount = idx.Length, look = ReadLook(mat, copiedMaps, golf) });
                        for (int i = 0; i + 2 < idx.Length; i += 3) { AddInt(bin, idx[i]); AddInt(bin, idx[i + 2]); AddInt(bin, idx[i + 1]); }   // winding flipped with the z mirror
                    }
                    if (p.kind == "kit")
                    {
                        // the kit parts have UVs in Unity (seam / collar / hem maps, the weave tiles per UV unit); the body, the painted face and the racket do not
                        var uv = p.mesh.uv; if (uv.Length != p.pos.Length) throw new InvalidOperationException(p.name + ": kit mesh has no UV0");
                        info.uvOffset = bin.Count; AddV2(bin, uv);
                    }
                    else if (p.kind == "body")
                    {
                        // the soft skin normal is projected from the BIND-pose position (the body has no UVs): MatchHeroLook.WithBindPose puts mesh.vertices in channel 1
                        info.bindPositionOffset = bin.Count; AddV3(bin, p.mesh.vertices, true);
                    }
                    info.submeshes = subs.ToArray(); infos.Add(info);
                }
                int tris = infos.Sum(i => i.submeshes.Sum(s => s.indexCount)) / 3;
                var unfit = infos.SelectMany(i => i.submeshes.Select(s => (i.name, s))).Where(x => x.s.look.shader != "TennisCloth" && x.s.look.shader != "TennisCharacter" && !(golf && x.name == "Hair_F" && x.s.look.shader == "Universal Render Pipeline/Lit")).ToList();
                if (unfit.Count > 0) throw new InvalidOperationException("hero materials not on a hero shader (OwnMaterials did not run?): " + string.Join(", ", unfit.Select(x => x.name + ":" + x.s.material + "=" + x.s.look.shader)));
                // swing frames use their own offsets (positions then normals, part order)
                int swingCursor = 0;
                foreach (var info in infos) { info.swingPositionOffset = swingCursor; swingCursor += info.vertexCount * 12; info.swingNormalOffset = swingCursor; swingCursor += info.vertexCount * 12; }

                string baseName = (golf ? "GolfKitHero_" : "MatchHero_") + sex;
                var man = new Manifest
                {
                    sex = sex, source = golf ? "Resources/Tennis/Customization/Player" + sex + "GolfKit.prefab" : "Resources/Tennis/Customization/Player" + sex + ".prefab (work/match-anim-set/export/HeroBase_" + sex + "_MatchAnims.blend)",
                    baseClip = ready.name, baseTime = 0, swingClip = fore.clip.name, swingLength = fore.clip.length, swingContact = fore.contact,
                    parts = infos.ToArray(), swingTimes = SwingTimes,
                    boundsMin = new[] { bounds.min.x, bounds.min.y, -bounds.max.z }, boundsMax = new[] { bounds.max.x, bounds.max.y, -bounds.min.z },
                    height = bounds.size.y, headCentreY = headBounds.center.y, headTopY = headBounds.max.y, headRadius = Mathf.Max(headBounds.extents.x, headBounds.extents.z),
                };
                var frames = new List<string>();
                // ---- swing frames
                for (int k = 0; !golf && k < SwingTimes.Length; k++)
                {
                    fore.clip.SampleAnimation(root, Mathf.Min(SwingTimes[k], fore.clip.length));
                    foreach (var p in sources) Pose(root, p);
                    var fb = new List<byte>(swingCursor);
                    foreach (var p in sources) { AddV3(fb, p.pos, true, p.kind == "kit" ? 10 : 0); AddV3(fb, p.nrm, true, p.kind == "kit" ? 12 : 0); }
                    string name = $"{baseName}_Swing_{k:00}"; frames.Add(name);
                    WriteCompressed(name, fb.ToArray());
                }
                man.swingFrames = frames.ToArray();
                if(golf){man.swingTimes=Array.Empty<float>();man.swingClip="";man.swingLength=0;man.swingContact=0;}
                man.materials = mats.Values.OrderBy(m => m.name).ToArray();

                // ---- the rig: bind-pose skin data + the ReadyIdle and Serve bone tracks
                man.rig = golf ? null : ExportRig(root, driver, sources, ready, sb, baseName);

                File.WriteAllText(Path.Combine(OutDir, baseName + ".json"), JsonUtility.ToJson(man, true));
                WriteCompressed(baseName, bin.ToArray());
                foreach (var map in copiedMaps) File.Copy(map, Path.Combine(OutDir, MapResourceName(map, golf) + Path.GetExtension(map)), true);
                long total = new[] { baseName }.Concat(frames).Sum(n => new FileInfo(Path.Combine(OutDir, n + ".lzfse")).Length);
                sb.AppendLine($"  wrote {baseName}.json + .lzfse + {frames.Count} swing frames ({total / 1024 / 1024.0:F1} MB compressed); triangles={tris / 3}; materials={string.Join(",", man.materials.Select(m => m.name))}");
                sb.AppendLine($"  maps copied: {string.Join(", ", copiedMaps.Select(Path.GetFileName))}");
                sb.AppendLine($"  swing clip {fore.clip.name} len={fore.clip.length:F3}s contact={fore.contact:F3}s sampled at [{string.Join(", ", SwingTimes.Select(t => t.ToString("0.000")))}]");
                int kitVerts = infos.Where(i => i.kind == "kit").Sum(i => i.vertexCount), allVerts = infos.Sum(i => i.vertexCount);
                int kitTris = infos.Where(i => i.kind == "kit").Sum(i => i.submeshes.Sum(q => q.indexCount)) / 3, allTris = infos.Sum(i => i.submeshes.Sum(q => q.indexCount)) / 3;
                sb.AppendLine($"  BUDGET {sex}: vertices {allVerts} (kit {kitVerts}), triangles {allTris} (kit {kitTris}); one swing frame = {swingCursor / 1024.0 / 1024.0:F2} MB decompressed (positions + normals), base file {bin.Count / 1024.0 / 1024.0:F2} MB; 12 morph targets resident = {swingCursor * 12 / 1024.0 / 1024.0:F1} MB");
                sb.AppendLine("  looks: " + string.Join("; ", infos.SelectMany(i => i.submeshes.Select(s => i.name + "/" + s.material + "=" + s.look.shader.Replace("Tennis", ""))).Distinct()));
            }
            finally
            {
                // the instance's own material copies are normally freed by OnDestroy (edit mode does not call it): free them so repeated exports in an open editor do not pile them up
                var look = root ? root.GetComponent<MatchHeroLook>() : null;
                typeof(MatchHeroLook).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(look, null);
                Object.DestroyImmediate(root);
            }
            return sb.ToString();
        }

        // ================================================================== look

        /// The numbers of one runtime material, read off the material itself. Properties a shader does not have stay 0 (the Swift side picks the family by `shader`).
        // Static golf kit resources have their own namespace beside the existing
        // GolfHero motion resources. Use it for both manifest references and files.
        static string MapResourceName(string path, bool golf)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            if (golf && name.StartsWith("Golf_", StringComparison.Ordinal))
                name = "GolfKit_" + name.Substring("Golf_".Length);
            return "MatchHero_" + name;
        }

        static Look ReadLook(Material m, SortedSet<string> maps, bool golf)
        {
            var look = new Look { shader = m && m.shader ? m.shader.name.Replace("GolfArcade/", "") : "Missing" };
            if (!m) return look;
            float F(string n) => m.HasProperty(n) ? m.GetFloat(n) : 0f;
            float[] C(string n) { if (!m.HasProperty(n)) return new float[0]; var c = m.GetColor(n); return new[] { c.r, c.g, c.b, c.a }; }
            string Map(string n)
            {
                if (!m.HasProperty(n)) return "";
                var tex = m.GetTexture(n); if (!tex) return "";
                string path = AssetDatabase.GetAssetPath(tex);
                if (string.IsNullOrEmpty(path)) return "";   // a built-in default (the shader's "bump" / "white"), nothing to ship
                if (!path.EndsWith(".png")) throw new InvalidOperationException($"{m.name}.{n}: texture '{tex.name}' is not a PNG asset ({path})");
                maps.Add(path);
                return MapResourceName(path, golf);
            }
            look.color = C("_BaseColor"); look.rimColor = C("_RimColor"); look.subsurface = C("_Subsurface");
            look.smoothness = F("_Smoothness"); look.wrap = F("_Wrap"); look.rimStrength = F("_RimStrength"); look.rimPower = F("_RimPower"); look.exposure = F("_Exposure"); look.knee = F("_Knee");
            look.weaveTile = F("_WeaveTile"); look.weaveAngle = F("_WeaveAngle"); look.weaveNormal = F("_WeaveNormal"); look.weaveThread = F("_WeaveThread"); look.sheenStrength = F("_SheenStrength"); look.sheenPower = F("_SheenPower");
            look.saturation = F("_Saturation"); look.bumpScale = F("_BumpScale"); look.bumpTriplanar = F("_BumpTriplanar"); look.bumpTile = F("_BumpTile");
            look.baseMap = Map("_BaseMap"); look.weaveMap = Map("_WeaveMap"); look.bumpMap = Map("_BumpMap");
            look.normalMap = Map("_NormalMap"); look.maskMap = Map("_MaskMap"); look.trimColor = C("_TrimColor"); look.normalStrength = F("_NormalStrength"); look.useGarmentMaps = F("_UseGarmentMaps"); look.fabricVersion = F("_FabricVersion");
            if (look.shader == "TennisCloth")
            {
                // the role's cloth numbers live in MatchHeroLook.ClothFor (private): read them off it, so the tile size the Swift side fades the weave by is the one Unity tiles with
                var clothFor = typeof(MatchHeroLook).GetMethod("ClothFor", BindingFlags.Static | BindingFlags.NonPublic);
                var entry = clothFor?.Invoke(null, new object[] { m.name.Replace(" (hero)", "").Replace(" (Instance)", "") });
                var field = entry?.GetType().GetField("tileMetres");
                if (field == null) throw new InvalidOperationException("MatchHeroLook.ClothFor(...).tileMetres not found");
                look.tileMetres = (float)field.GetValue(entry);
            }
            return look;
        }

        // ================================================================== rig

        static Transform FindBone(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);

        /// Bind-pose mesh + bone weights + inverse binds of every skinned part, and the clip tracks. Returns the manifest block; writes MatchHero_<Sex>_Rig.lzfse.
        static Rig ExportRig(GameObject root, HeroTennisDriver driver, List<PartSource> sources, AnimationClip ready, StringBuilder sb, string baseName)
        {
            // the skeleton: every transform a skinned part is bound to (flat list; the Swift side keeps the bones as a flat set of nodes carrying hero-space transforms)
            var bones = new List<Transform>(); var boneIndex = new Dictionary<Transform, int>();
            int Bone(Transform t) { if (!boneIndex.TryGetValue(t, out int i)) { i = bones.Count; bones.Add(t); boneIndex[t] = i; } return i; }
            var skinned = sources.Where(s => s.skinned).ToList();
            var rigid = sources.Where(s => !s.skinned).ToList();
            var rig = new Rig { file = baseName + "_Rig", parts = new RigPart[skinned.Count + rigid.Count] };
            var blob = new List<byte>();
            int partNo = 0;
            foreach (var p in skinned)
            {
                var mesh = p.mesh; var weights = mesh.boneWeights; var bind = mesh.bindposes; var smrBones = p.skinned.bones;
                if (weights.Length != mesh.vertexCount) throw new InvalidOperationException(p.name + ": no bone weights");
                if (smrBones.Length != bind.Length) throw new InvalidOperationException(p.name + ": bones / bindposes mismatch");
                var rp = new RigPart { part = p.name, kind = "skin", vertexCount = mesh.vertexCount, bones = new int[smrBones.Length] };
                for (int i = 0; i < smrBones.Length; i++) { if (!smrBones[i]) throw new InvalidOperationException(p.name + ": null bone " + i); rp.bones[i] = Bone(smrBones[i]); }
                rp.bindPositionOffset = blob.Count; AddV3(blob, mesh.vertices, true);
                rp.bindNormalOffset = blob.Count; AddV3(blob, mesh.normals, true);
                // weights as 4 floats per vertex (normalised: the CPU skin divides by the sum), indices as 4 bytes (mesh-local bone indices)
                rp.weightOffset = blob.Count; var wb = new byte[mesh.vertexCount * 16];
                rp.indexOffset = blob.Count + wb.Length; var ib = new byte[mesh.vertexCount * 4];
                int zero = 0, over255 = 0;
                for (int i = 0; i < weights.Length; i++)
                {
                    var w = weights[i]; float sum = w.weight0 + w.weight1 + w.weight2 + w.weight3;
                    if (sum <= 0) { zero++; sum = 1; w.weight0 = 1; w.boneIndex0 = 0; w.weight1 = w.weight2 = w.weight3 = 0; }
                    int[] bi = { w.boneIndex0, w.boneIndex1, w.boneIndex2, w.boneIndex3 }; float[] bw = { w.weight0 / sum, w.weight1 / sum, w.weight2 / sum, w.weight3 / sum };
                    for (int k = 0; k < 4; k++)
                    {
                        if (bi[k] > 255) over255++;
                        Buffer.BlockCopy(BitConverter.GetBytes(bw[k]), 0, wb, i * 16 + k * 4, 4);
                        ib[i * 4 + k] = (byte)Mathf.Clamp(bw[k] > 0 ? bi[k] : 0, 0, 255);
                    }
                }
                if (zero > 0) sb.AppendLine($"  WARNING {p.name}: {zero} vertices without bone weights (bound to bone 0)");
                if (over255 > 0) throw new InvalidOperationException(p.name + ": bone index above 255");
                blob.AddRange(wb); blob.AddRange(ib);
                rp.inverseBindOffset = blob.Count;
                foreach (var bp in bind) AddMatrix(blob, FlipZ * bp * FlipZ);
                rig.parts[partNo++] = rp;
            }
            foreach (var p in rigid)
            {
                var rp = new RigPart { part = p.name, kind = "rigid", vertexCount = p.mesh.vertexCount };
                rig.parts[partNo++] = rp;
            }
            rig.bones = bones.Select(b => b.name).ToArray();
            rig.rigid = rigid.Select(r => r.name).ToArray();
            rig.trackCount = bones.Count + rigid.Count;
            for (int i = 0; i < rigid.Count; i++) rig.parts.First(r => r.part == rigid[i].name).track = bones.Count + i;
            sb.AppendLine($"  rig: {bones.Count} bones, {rigid.Count} rigid parts ({string.Join(", ", rig.rigid)}), tracks {rig.trackCount}; skinned parts {skinned.Count}");

            // ---- clips: the hero-space transform of every track at ClipRate. Bones are world TRS (z mirrored); a rigid part's track is its transform relative to frame 0 of ReadyIdle (the base pose its geometry is in)
            var inv = root.transform.worldToLocalMatrix;
            ready.SampleAnimation(root, 0f);
            var rigidRef = rigid.Select(r => FlipZ * (inv * r.renderer.transform.localToWorldMatrix) * FlipZ).Select(m => m.inverse).ToArray();
            var clipInfos = new List<ClipInfo>(); var clipFrames = new Dictionary<string, float[]>();
            foreach (var (slotId, id, loop) in RigClips)
            {
                var slot = driver.slots.First(s => s.id == slotId); var clip = slot.clip;
                if (!clip) throw new InvalidOperationException("Missing lobby clip: " + slotId);
                ready.SampleAnimation(root, 0f);
                int n = Mathf.Max(1, Mathf.RoundToInt(clip.length * ClipRate)), frames = n + 1;
                var data = new float[frames * rig.trackCount * 10];
                var prevQ = new Quaternion[rig.trackCount];
                float worstRecompose = 0, worstScale = 0;
                var bmin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue); var bmax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
                for (int k = 0; k < frames; k++)
                {
                    float t = Mathf.Min(clip.length, k * clip.length / n);
                    clip.SampleAnimation(root, t);
                    for (int tr = 0; tr < rig.trackCount; tr++)
                    {
                        Matrix4x4 m = tr < bones.Count
                            ? FlipZ * (inv * bones[tr].localToWorldMatrix) * FlipZ
                            : FlipZ * (inv * rigid[tr - bones.Count].renderer.transform.localToWorldMatrix) * FlipZ * rigidRef[tr - bones.Count];
                        Decompose(m, out var tv, out var q, out var sv);
                        if (k > 0 && Quaternion.Dot(q, prevQ[tr]) < 0) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                        prevQ[tr] = q;
                        worstRecompose = Mathf.Max(worstRecompose, MaxDiff(Matrix4x4.TRS(tv, q, sv), m));
                        worstScale = Mathf.Max(worstScale, Mathf.Abs(Mathf.Abs(sv.x) - 1), Mathf.Abs(Mathf.Abs(sv.y) - 1), Mathf.Abs(Mathf.Abs(sv.z) - 1));
                        int o = (k * rig.trackCount + tr) * 10;
                        data[o] = tv.x; data[o + 1] = tv.y; data[o + 2] = tv.z; data[o + 3] = q.x; data[o + 4] = q.y; data[o + 5] = q.z; data[o + 6] = q.w; data[o + 7] = sv.x; data[o + 8] = sv.y; data[o + 9] = sv.z;
                    }
                    if (k % 4 == 0 || k == n) foreach (var p in sources) { Pose(root, p); foreach (var v in p.pos) { var f = new Vector3(v.x, v.y, -v.z); bmin = Vector3.Min(bmin, f); bmax = Vector3.Max(bmax, f); } }
                }
                // Keep the 60 Hz base samples. Abrupt authored cuts need extra guard samples so interpolation
                // does not pull the racket off its socket early or smear a one-frame hand transition.
                float baseFps = n / clip.length; float[] sampleTimes = null;
                if (!loop && id != "serve") {
                    var times = new List<float>(); var refined = new List<float>();
                    float[] Sample(float time) {
                        clip.SampleAnimation(root, time); var pose = new float[rig.trackCount * 10];
                        for (int tr = 0; tr < rig.trackCount; tr++) {
                            var m = tr < bones.Count ? FlipZ * (inv * bones[tr].localToWorldMatrix) * FlipZ
                                : FlipZ * (inv * rigid[tr - bones.Count].renderer.transform.localToWorldMatrix) * FlipZ * rigidRef[tr - bones.Count];
                            Decompose(m, out var t, out var q, out var scale); int o = tr * 10;
                            pose[o]=t.x; pose[o+1]=t.y; pose[o+2]=t.z; pose[o+3]=q.x; pose[o+4]=q.y; pose[o+5]=q.z; pose[o+6]=q.w; pose[o+7]=scale.x; pose[o+8]=scale.y; pose[o+9]=scale.z;
                        }
                        return pose;
                    }
                    bool Accurate(float[] a, float[] b, float[] actual, float u) {
                        for (int tr = 0; tr < rig.trackCount; tr++) { int o=tr*10;
                            var ta=new Vector3(a[o],a[o+1],a[o+2]); var tb=new Vector3(b[o],b[o+1],b[o+2]); var t=new Vector3(actual[o],actual[o+1],actual[o+2]);
                            var qa=new Quaternion(a[o+3],a[o+4],a[o+5],a[o+6]); var qb=new Quaternion(b[o+3],b[o+4],b[o+5],b[o+6]); var q=new Quaternion(actual[o+3],actual[o+4],actual[o+5],actual[o+6]);
                            if (Vector3.Distance(Vector3.Lerp(ta,tb,u),t)>.001f || Quaternion.Angle(Quaternion.Slerp(qa,qb,u),q)>.7f) return false;
                        }
                        return true;
                    }
                    void Append(float time, float[] pose) { times.Add(time); refined.AddRange(pose); }
                    void Refine(float aTime, float[] aPose, float bTime, float[] bPose, int depth) {
                        float mid=(aTime+bTime)*.5f; var m=Sample(mid);
                        bool accurate=Accurate(aPose,bPose,m,.5f) && Accurate(aPose,bPose,Sample(Mathf.Lerp(aTime,bTime,.25f)),.25f) && Accurate(aPose,bPose,Sample(Mathf.Lerp(aTime,bTime,.75f)),.75f);
                        if (accurate || depth >= 8) { Append(bTime,bPose); return; }
                        Refine(aTime,aPose,mid,m,depth+1); Refine(mid,m,bTime,bPose,depth+1);
                    }
                    var previous=Sample(0); Append(0,previous);
                    for(int k=1;k<frames;k++) { float at=k*clip.length/n; var next=Sample(at); Refine((k-1)*clip.length/n,previous,at,next,0); previous=next; }
                    sampleTimes=times.ToArray(); data=refined.ToArray(); frames=sampleTimes.Length; n=frames-1;
                    sb.AppendLine($"  clip {id}: {frames} total samples including 60 Hz base + cut guards");
                }
                // loop seam: how far the last frame is from the first (positions in mm, rotation in degrees)
                float seamPos = 0, seamRot = 0;
                for (int tr = 0; tr < rig.trackCount; tr++)
                {
                    int a = tr * 10, b = (n * rig.trackCount + tr) * 10;
                    seamPos = Mathf.Max(seamPos, new Vector3(data[a] - data[b], data[a + 1] - data[b + 1], data[a + 2] - data[b + 2]).magnitude);
                    seamRot = Mathf.Max(seamRot, Quaternion.Angle(new Quaternion(data[a + 3], data[a + 4], data[a + 5], data[a + 6]), new Quaternion(data[b + 3], data[b + 4], data[b + 5], data[b + 6])));
                }
                var ci = new ClipInfo { id = id, name = clip.name, loop = loop, length = clip.length, fps = baseFps, contact = slot.contact, frames = frames, offset = blob.Count, times = sampleTimes,
                                        boundsMin = new[] { bmin.x, bmin.y, bmin.z }, boundsMax = new[] { bmax.x, bmax.y, bmax.z } };
                var cb = new byte[data.Length * 4]; Buffer.BlockCopy(data, 0, cb, 0, cb.Length); blob.AddRange(cb);
                clipInfos.Add(ci); clipFrames[id] = data;
                sb.AppendLine($"  clip {id}: {clip.name} length {clip.length:F3}s, {frames} frames at {ci.fps:F2} Hz, loop seam {seamPos * 1000f:F3} mm / {seamRot:F3} deg (last frame vs first), TRS recompose error {worstRecompose:E2}, bone scale error {worstScale:E2}; mesh bounds (file space) min={bmin:F3} max={bmax:F3}");
            }
            rig.clips = clipInfos.ToArray();
            WriteCompressed(rig.file, blob.ToArray());
            sb.AppendLine($"  rig file {rig.file}: {blob.Count / 1024.0 / 1024.0:F2} MB decompressed");

            // ---- self check: skin the exported bind mesh with the exported tracks and compare with the CPU skinning of the live rig, at a few clip times
            foreach (var ci in clipInfos)
            {
                var clip = driver.slots.First(s => s.id == RigClips.First(c => c.id == ci.id).slot).clip; var data = clipFrames[ci.id];
                float worst = 0; string where = "";
                foreach (float u in new[] { 0f, .21f, .47f, .83f, 1f })
                {
                    float fk = u * (ci.frames - 1); int k = Mathf.RoundToInt(fk); float t = ci.times != null ? ci.times[k] : Mathf.Min(clip.length, k * clip.length / (ci.frames - 1));
                    clip.SampleAnimation(root, t);
                    foreach (var p in sources) Pose(root, p);
                    foreach (var rp in rig.parts.Where(r => r.kind == "skin"))
                    {
                        var src = sources.First(s => s.name == rp.part); var mesh = src.mesh; var w = mesh.boneWeights; var bind = mesh.bindposes; var verts = mesh.vertices;
                        var m = new Matrix4x4[rp.bones.Length];
                        for (int b = 0; b < m.Length; b++) { int tr = rp.bones[b]; int o = (k * rig.trackCount + tr) * 10; m[b] = Matrix4x4.TRS(new Vector3(data[o], data[o + 1], data[o + 2]), new Quaternion(data[o + 3], data[o + 4], data[o + 5], data[o + 6]), new Vector3(data[o + 7], data[o + 8], data[o + 9])) * (FlipZ * bind[b] * FlipZ); }
                        for (int i = 0; i < verts.Length; i += 7)
                        {
                            var bw = w[i]; Vector3 pp = Vector3.zero; float sum = 0; var vz = new Vector3(verts[i].x, verts[i].y, -verts[i].z);
                            void Add(int bi, float wt) { if (wt <= 0) return; pp += wt * (Vector3)m[bi].MultiplyPoint3x4(vz); sum += wt; }
                            Add(bw.boneIndex0, bw.weight0); Add(bw.boneIndex1, bw.weight1); Add(bw.boneIndex2, bw.weight2); Add(bw.boneIndex3, bw.weight3);
                            if (sum <= 0) continue; pp /= sum;
                            float d = Vector3.Distance(new Vector3(pp.x, pp.y, -pp.z), src.pos[i]);   // Pose() is in Unity space; the file space negates z
                            if (d > worst) { worst = d; where = $"{ci.id} frame {k} {rp.part} v{i}"; }
                        }
                    }
                }
                sb.AppendLine($"  rig self check {ci.id}: exported tracks + bind mesh vs live CPU skinning, max deviation {worst * 1000f:F4} mm ({where})");
                if (worst > .0005f) throw new InvalidOperationException($"rig data does not reproduce the live skinning: {worst * 1000f} mm at {where}");
            }

            // ---- golden samples for the Swift tests (what the live rig gives at known clip times), when asked for
            if (!string.IsNullOrEmpty(GoldenDir)) {
                WriteGolden(root, driver, sources, rig, clipInfos, baseName);
                WriteEmoteParity(root, driver, bones, rigid, rigidRef, baseName);
            }
            return rig;
        }

        /// Independent Unity samples at ten times BETWEEN the 60 Hz samples. Swift checks bone rotation and rigid racket playback against these.
        static void WriteEmoteParity(GameObject root, HeroTennisDriver driver, List<Transform> bones, List<PartSource> rigid, Matrix4x4[] rigidRef, string baseName)
        {
            var inv = root.transform.worldToLocalMatrix;
            var sb = new StringBuilder("{\"samples\":["); bool first = true;
            foreach (var entry in RigClips.Where(e => !e.loop && e.id != "serve")) {
                var clip = driver.slots.First(s => s.id == entry.slot).clip;
                for (int frame = 0; frame < 10; frame++) {
                    float time = clip.length * frame / 9f;
                    clip.SampleAnimation(root, time);
                    if (!first) sb.Append(','); first = false;
                    sb.Append($"{{\"clip\":\"{entry.id}\",\"time\":{time:R},\"tracks\":[");
                    for (int tr = 0; tr < bones.Count + rigid.Count; tr++) {
                        if (tr > 0) sb.Append(',');
                        var m = tr < bones.Count ? FlipZ * (inv * bones[tr].localToWorldMatrix) * FlipZ
                            : FlipZ * (inv * rigid[tr - bones.Count].renderer.transform.localToWorldMatrix) * FlipZ * rigidRef[tr - bones.Count];
                        Decompose(m, out var t, out var q, out var s);
                        sb.Append($"[{t.x:R},{t.y:R},{t.z:R},{q.x:R},{q.y:R},{q.z:R},{q.w:R},{s.x:R},{s.y:R},{s.z:R}]");
                    }
                    sb.Append("]}");
                }
            }
            sb.Append("]}"); File.WriteAllText(Path.Combine(GoldenDir, baseName + "_emote_parity.json"), sb.ToString());
        }

        /// A few hundred vertices per part at 6 clip times per clip, straight from the CPU skinning of the live rig, so the Swift tests can check the shipped rig file against Unity itself.
        static void WriteGolden(GameObject root, HeroTennisDriver driver, List<PartSource> sources, Rig rig, List<ClipInfo> clips, string baseName)
        {
            Directory.CreateDirectory(GoldenDir);
            var sb = new StringBuilder("{\n  \"samples\": [\n"); bool firstSample = true;
            foreach (var ci in clips)
            {
                var clip = driver.slots.First(s => s.id == RigClips.First(c => c.id == ci.id).slot).clip;
                foreach (float u in new[] { 0f, .17f, .38f, .55f, .8f, 1f })
                {
                    int k = Mathf.RoundToInt(u * (ci.frames - 1)); float t = ci.times != null ? ci.times[k] : Mathf.Min(clip.length, k * clip.length / (ci.frames - 1));
                    clip.SampleAnimation(root, t);
                    foreach (var p in sources) Pose(root, p);
                    foreach (var p in sources)
                    {
                        if (!firstSample) sb.Append(",\n"); firstSample = false;
                        sb.Append($"    {{\"clip\":\"{ci.id}\",\"frame\":{k},\"part\":\"{p.name}\",\"vertices\":[");
                        int step = Mathf.Max(1, p.pos.Length / 160), count = 0;
                        for (int i = 0; i < p.pos.Length; i += step)
                        {
                            if (count++ > 0) sb.Append(',');
                            sb.Append($"[{i},{p.pos[i].x:R},{p.pos[i].y:R},{-p.pos[i].z:R}]");
                        }
                        sb.Append("]}");
                    }
                }
            }
            sb.Append("\n  ]\n}\n");
            File.WriteAllText(Path.Combine(GoldenDir, baseName + "_golden.json"), sb.ToString());
        }

        static float MaxDiff(Matrix4x4 a, Matrix4x4 b) { float w = 0; for (int i = 0; i < 16; i++) w = Mathf.Max(w, Mathf.Abs(a[i] - b[i])); return w; }

        /// Translation, rotation and (signed) scale of an affine matrix with orthogonal axes (a bone, or the product of two such).
        static void Decompose(Matrix4x4 m, out Vector3 t, out Quaternion q, out Vector3 s)
        {
            t = new Vector3(m.m03, m.m13, m.m23);
            var c0 = new Vector3(m.m00, m.m10, m.m20); var c1 = new Vector3(m.m01, m.m11, m.m21); var c2 = new Vector3(m.m02, m.m12, m.m22);
            s = new Vector3(c0.magnitude, c1.magnitude, c2.magnitude);
            if (Vector3.Dot(Vector3.Cross(c0, c1), c2) < 0) s.x = -s.x;
            q = Quaternion.LookRotation(c2 / s.z, c1 / s.y);
        }

        static List<PartSource> Collect(GameObject root)
        {
            var list = new List<PartSource>();
            var body = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name.StartsWith("Body_"));
            list.Add(new PartSource { name = "Body", kind = "body", mesh = body.sharedMesh, renderer = body, skinned = body, materials = body.sharedMaterials });
            var face = root.GetComponentsInChildren<MeshRenderer>(true).First(r => r.name.StartsWith("Face_"));
            list.Add(new PartSource { name = "Face", kind = "face", mesh = face.GetComponent<MeshFilter>().sharedMesh, renderer = face, materials = face.sharedMaterials });
            // the worn kit: skinned to the same bones, posed by the same CPU skinning as the body (checked against BakeMesh below)
            foreach (var k in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("Kit_")).OrderBy(r => r.name, StringComparer.Ordinal))
                list.Add(new PartSource { name = k.name, kind = "kit", mesh = k.sharedMesh, renderer = k, skinned = k, materials = k.sharedMaterials });
            var hair = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Hair_F");
            if (hair) list.Add(new PartSource { name=hair.name,kind="hair",mesh=hair.sharedMesh,renderer=hair,skinned=hair,materials=hair.sharedMaterials });
            var rigidHair = root.GetComponentsInChildren<MeshRenderer>(true).FirstOrDefault(r => r.name == "Hair_F");
            if (rigidHair) list.Add(new PartSource {name=rigidHair.name,kind="hair",mesh=rigidHair.GetComponent<MeshFilter>().sharedMesh,renderer=rigidHair,materials=rigidHair.sharedMaterials});
            if (root.GetComponent<MatchHeroLook>().golfKit) return list;
            var racket = root.GetComponentsInChildren<Transform>(true).First(t => t.name == "Racket_Classic");
            foreach (var r in racket.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (r.name == "Collider_Racket") continue;
                var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue;
                list.Add(new PartSource { name = "Racket_" + r.name, kind = "racket", mesh = mf.sharedMesh, renderer = r, materials = r.sharedMaterials });
            }
            return list;
        }

        /// The part's vertices and normals in hero-root space for the pose the hierarchy is in now (CPU skinning for the body, the renderer's transform for the rest).
        static void Pose(GameObject root, PartSource p)
        {
            var inv = root.transform.worldToLocalMatrix;
            var verts = p.mesh.vertices; var norms = p.mesh.normals;
            if(p.skinned && p.mesh.blendShapeCount>0) {
                var delta=new Vector3[verts.Length];var normals=new Vector3[verts.Length];var tangents=new Vector3[verts.Length];
                for(int shape=0;shape<p.mesh.blendShapeCount;shape++) {
                    float weight=p.skinned.GetBlendShapeWeight(shape);if(Mathf.Abs(weight)<.00001f)continue;
                    int frame=p.mesh.GetBlendShapeFrameCount(shape)-1;float factor=weight/p.mesh.GetBlendShapeFrameWeight(shape,frame);
                    p.mesh.GetBlendShapeFrameVertices(shape,frame,delta,normals,tangents);
                    for(int i=0;i<verts.Length;i++){verts[i]+=factor*delta[i];norms[i]+=factor*normals[i];}
                }
            }
            if (norms == null || norms.Length != verts.Length) throw new InvalidOperationException(p.name + " has no normals");
            if (p.pos == null) { p.pos = new Vector3[verts.Length]; p.nrm = new Vector3[verts.Length]; }
            if (p.skinned)
            {
                var bones = p.skinned.bones; var bind = p.mesh.bindposes; var w = p.mesh.boneWeights;
                var m = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) m[i] = inv * bones[i].localToWorldMatrix * bind[i];
                for (int i = 0; i < verts.Length; i++)
                {
                    var bw = w[i]; Vector3 pp = Vector3.zero, nn = Vector3.zero; float sum = 0;
                    void Add(int bi, float wt) { if (wt <= 0) return; pp += wt * (Vector3)m[bi].MultiplyPoint3x4(verts[i]); nn += wt * (Vector3)m[bi].MultiplyVector(norms[i]); sum += wt; }
                    Add(bw.boneIndex0, bw.weight0); Add(bw.boneIndex1, bw.weight1); Add(bw.boneIndex2, bw.weight2); Add(bw.boneIndex3, bw.weight3);
                    if (sum <= 0) { pp = inv.MultiplyPoint3x4(p.skinned.transform.TransformPoint(verts[i])); nn = norms[i]; } else pp /= sum;
                    p.pos[i] = pp; p.nrm[i] = nn.normalized;
                }
            }
            else
            {
                var m = inv * p.renderer.transform.localToWorldMatrix;
                for (int i = 0; i < verts.Length; i++) { p.pos[i] = m.MultiplyPoint3x4(verts[i]); p.nrm[i] = m.MultiplyVector(norms[i]).normalized; }
            }
        }

        // Only kit pose data uses mantissa rounding for LZFSE: <0.15 mm position error,
        // <0.0005 normal-vector error. Non-kit geometry and all rig/clip data stay exact.
        static float Packed(float value, int clear)
        {
            if (clear == 0) return value;
            uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(value));
            bits = (bits + (1u << (clear - 1))) & ~((1u << clear) - 1u);
            return BitConverter.Int32BitsToSingle(unchecked((int)bits));
        }
        static void AddV3(List<byte> bin, Vector3[] v, bool mirrorZ, int clear = 0)
        {
            var buf = new byte[v.Length * 12];
            for (int i = 0; i < v.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(Packed(v[i].x, clear)), 0, buf, i * 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(Packed(v[i].y, clear)), 0, buf, i * 12 + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(Packed(mirrorZ ? -v[i].z : v[i].z, clear)), 0, buf, i * 12 + 8, 4);
            }
            bin.AddRange(buf);
        }
        static void AddV2(List<byte> bin, Vector2[] v)
        {
            var buf = new byte[v.Length * 8];
            for (int i = 0; i < v.Length; i++) { Buffer.BlockCopy(BitConverter.GetBytes(v[i].x), 0, buf, i * 8, 4); Buffer.BlockCopy(BitConverter.GetBytes(v[i].y), 0, buf, i * 8 + 4, 4); }
            bin.AddRange(buf);
        }
        /// 16 floats, row-major (m00 m01 m02 m03 m10 ...).
        static void AddMatrix(List<byte> bin, Matrix4x4 m)
        {
            var buf = new byte[64];
            for (int r = 0; r < 4; r++) for (int c = 0; c < 4; c++) Buffer.BlockCopy(BitConverter.GetBytes(m[r, c]), 0, buf, (r * 4 + c) * 4, 4);
            bin.AddRange(buf);
        }
        static void AddInt(List<byte> bin, int v) => bin.AddRange(BitConverter.GetBytes(v));

        /// <name>.lzfse next to the manifest (compression_tool ships with macOS; the Swift side reads .lzfse first, then .bin).
        static void WriteCompressed(string name, byte[] data)
        {
            string raw = Path.Combine(OutDir, name + ".bin"), lz = Path.Combine(OutDir, name + ".lzfse");
            File.WriteAllBytes(raw, data);
            try
            {
                var psi = new ProcessStartInfo("/usr/bin/compression_tool", $"-encode -a lzfse -i \"{raw}\" -o \"{lz}\"") { UseShellExecute = false, RedirectStandardError = true };
                using var proc = Process.Start(psi); proc.WaitForExit();
                if (proc.ExitCode != 0 || !File.Exists(lz)) throw new IOException("compression_tool failed: " + proc.StandardError.ReadToEnd());
                File.Delete(raw);
            }
            catch (Exception e) { Debug.LogWarning("lzfse not written for " + name + " (" + e.Message + "); the raw .bin stays"); if (File.Exists(lz)) File.Delete(lz); }
        }
    }
}

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
        static readonly (HeroTennisDriver.Clip slot, string id, bool loop)[] RigClips = { (HeroTennisDriver.Clip.Ready, "ready", true), (HeroTennisDriver.Clip.Serve, "serve", false) };
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
            public string baseMap = "", weaveMap = "", bumpMap = "";
        }
        [Serializable] class Sub { public string material; public int indexOffset, indexCount; public Look look; }
        [Serializable] class PartInfo { public string name, kind; public int vertexCount, positionOffset, normalOffset, swingPositionOffset, swingNormalOffset, uvOffset = -1, bindPositionOffset = -1; public Sub[] submeshes; }
        [Serializable] class MatInfo { public string name; public float[] color; public float smoothness; }
        [Serializable] class RigPart { public string part, kind; public int vertexCount, bindPositionOffset, bindNormalOffset, weightOffset, indexOffset, inverseBindOffset, track = -1; public int[] bones; }
        [Serializable] class ClipInfo { public string id, name; public bool loop; public float length, fps, contact; public int frames, offset; public float[] boundsMin, boundsMax; }
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

        static string ExportSex(string sex)
        {
            var sb = new StringBuilder("== " + sex + "\n");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Player" + sex + ".prefab");
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
                    info.positionOffset = bin.Count; AddV3(bin, p.pos, true);
                    info.normalOffset = bin.Count; AddV3(bin, p.nrm, true);
                    var subs = new List<Sub>();
                    for (int s = 0; s < p.mesh.subMeshCount; s++)
                    {
                        var idx = p.mesh.GetIndices(s); if (idx.Length == 0) continue;
                        var mat = p.materials[Mathf.Min(s, p.materials.Length - 1)];
                        subs.Add(new Sub { material = Key(mat), indexOffset = bin.Count, indexCount = idx.Length, look = ReadLook(mat, copiedMaps) });
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
                var unfit = infos.SelectMany(i => i.submeshes.Select(s => (i.name, s))).Where(x => x.s.look.shader != "TennisCloth" && x.s.look.shader != "TennisCharacter").ToList();
                if (unfit.Count > 0) throw new InvalidOperationException("hero materials not on a hero shader (OwnMaterials did not run?): " + string.Join(", ", unfit.Select(x => x.name + ":" + x.s.material + "=" + x.s.look.shader)));
                // swing frames use their own offsets (positions then normals, part order)
                int swingCursor = 0;
                foreach (var info in infos) { info.swingPositionOffset = swingCursor; swingCursor += info.vertexCount * 12; info.swingNormalOffset = swingCursor; swingCursor += info.vertexCount * 12; }

                string baseName = "MatchHero_" + sex;
                var man = new Manifest
                {
                    sex = sex, source = "Resources/Tennis/Customization/Player" + sex + ".prefab (work/match-anim-set/export/HeroBase_" + sex + "_MatchAnims.blend)",
                    baseClip = ready.name, baseTime = 0, swingClip = fore.clip.name, swingLength = fore.clip.length, swingContact = fore.contact,
                    parts = infos.ToArray(), swingTimes = SwingTimes,
                    boundsMin = new[] { bounds.min.x, bounds.min.y, -bounds.max.z }, boundsMax = new[] { bounds.max.x, bounds.max.y, -bounds.min.z },
                    height = bounds.size.y, headCentreY = headBounds.center.y, headTopY = headBounds.max.y, headRadius = Mathf.Max(headBounds.extents.x, headBounds.extents.z),
                };
                var frames = new List<string>();
                // ---- swing frames
                for (int k = 0; k < SwingTimes.Length; k++)
                {
                    fore.clip.SampleAnimation(root, Mathf.Min(SwingTimes[k], fore.clip.length));
                    foreach (var p in sources) Pose(root, p);
                    var fb = new List<byte>(swingCursor);
                    foreach (var p in sources) { AddV3(fb, p.pos, true); AddV3(fb, p.nrm, true); }
                    string name = $"{baseName}_Swing_{k:00}"; frames.Add(name);
                    WriteCompressed(name, fb.ToArray());
                }
                man.swingFrames = frames.ToArray();
                man.materials = mats.Values.OrderBy(m => m.name).ToArray();

                // ---- the rig: bind-pose skin data + the ReadyIdle and Serve bone tracks
                man.rig = ExportRig(root, driver, sources, ready, sb, baseName);

                File.WriteAllText(Path.Combine(OutDir, baseName + ".json"), JsonUtility.ToJson(man, true));
                WriteCompressed(baseName, bin.ToArray());
                foreach (var map in copiedMaps) File.Copy(map, Path.Combine(OutDir, "MatchHero_" + Path.GetFileName(map)), true);
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
        static Look ReadLook(Material m, SortedSet<string> maps)
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
                return "MatchHero_" + Path.GetFileNameWithoutExtension(path);
            }
            look.color = C("_BaseColor"); look.rimColor = C("_RimColor"); look.subsurface = C("_Subsurface");
            look.smoothness = F("_Smoothness"); look.wrap = F("_Wrap"); look.rimStrength = F("_RimStrength"); look.rimPower = F("_RimPower"); look.exposure = F("_Exposure"); look.knee = F("_Knee");
            look.weaveTile = F("_WeaveTile"); look.weaveAngle = F("_WeaveAngle"); look.weaveNormal = F("_WeaveNormal"); look.weaveThread = F("_WeaveThread"); look.sheenStrength = F("_SheenStrength"); look.sheenPower = F("_SheenPower");
            look.saturation = F("_Saturation"); look.bumpScale = F("_BumpScale"); look.bumpTriplanar = F("_BumpTriplanar"); look.bumpTile = F("_BumpTile");
            look.baseMap = Map("_BaseMap"); look.weaveMap = Map("_WeaveMap"); look.bumpMap = Map("_BumpMap");
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
                // loop seam: how far the last frame is from the first (positions in mm, rotation in degrees)
                float seamPos = 0, seamRot = 0;
                for (int tr = 0; tr < rig.trackCount; tr++)
                {
                    int a = tr * 10, b = (n * rig.trackCount + tr) * 10;
                    seamPos = Mathf.Max(seamPos, new Vector3(data[a] - data[b], data[a + 1] - data[b + 1], data[a + 2] - data[b + 2]).magnitude);
                    seamRot = Mathf.Max(seamRot, Quaternion.Angle(new Quaternion(data[a + 3], data[a + 4], data[a + 5], data[a + 6]), new Quaternion(data[b + 3], data[b + 4], data[b + 5], data[b + 6])));
                }
                var ci = new ClipInfo { id = id, name = clip.name, loop = loop, length = clip.length, fps = n / clip.length, contact = slot.contact, frames = frames, offset = blob.Count,
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
                    float fk = u * (ci.frames - 1); int k = Mathf.RoundToInt(fk); float t = Mathf.Min(clip.length, k * clip.length / (ci.frames - 1));
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
            if (!string.IsNullOrEmpty(GoldenDir)) WriteGolden(root, driver, sources, rig, clipInfos, baseName);
            return rig;
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
                    int k = Mathf.RoundToInt(u * (ci.frames - 1)); float t = Mathf.Min(clip.length, k * clip.length / (ci.frames - 1));
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

        static void AddV3(List<byte> bin, Vector3[] v, bool mirrorZ)
        {
            var buf = new byte[v.Length * 12];
            for (int i = 0; i < v.Length; i++)
            {
                Buffer.BlockCopy(BitConverter.GetBytes(v[i].x), 0, buf, i * 12, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(v[i].y), 0, buf, i * 12 + 4, 4);
                Buffer.BlockCopy(BitConverter.GetBytes(mirrorZ ? -v[i].z : v[i].z), 0, buf, i * 12 + 8, 4);
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

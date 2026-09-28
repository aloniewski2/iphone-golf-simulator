using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Tennis;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    /// Exports the exact in-game Hero V4 (the Hero_01_Tennis prefab with its runtime fixes: sealed hair, head
    /// occluder, face) to the iOS locker, so the locker mirror is the character the game plays:
    ///   GolfArcade/Unity/CharacterAssets/HeroV4.json + HeroV4.bin   baked Ready pose, racket, three hats
    ///   GolfArcade/Unity/CharacterAssets/HeroV4_Atlas.png, _KitMask.png, _Iris.png
    ///   Unity/Assets/Resources/Tennis/Hero/HeroV4_KitMask.png + HeroV4_KitRef.json   (HeroKit, in game)
    /// Coordinates are converted to SceneKit (right-handed: z negated, winding reversed, v flipped).
    [InitializeOnLoad]
    public static class HeroLockerExport
    {
        static string AppAssets => Path.GetFullPath("../GolfArcade/Unity/CharacterAssets");
        const string ResDir = "Assets/Resources/Tennis/Hero/";
        const int MaskSize = 1024;
        static int step; static GameObject hero;
        static HeroLockerExport() { EditorApplication.update += Tick; }

        public static void Run()
        {
            HeroGameplayBuild.Build();
            BuildMask();
            EditorSceneManager.OpenScene("Assets/Scenes/Hero01RestoreMotion.unity");
            SessionState.SetInt("HeroLocker", 1); step = 0; EditorApplication.isPlaying = true;
        }

        // ------------------------------------------------------------------ region mask
        [Serializable] class Ref { public float shirt, shorts, hair, skin; }
        static void BuildMask()
        {
            var prefab = Resources.Load<GameObject>(TennisHeroSetup.PrefabPath);
            var look = prefab.GetComponent<ModularHeroLook>();
            var atlasPath = AssetDatabase.GetAssetPath(look.skinAtlas);
            var atlas = Readable(look.skinAtlas, MaskSize); var skinMask = Readable(look.skinMask, MaskSize);
            var ap = atlas.GetPixels(); var sp = skinMask.GetPixels();
            bool[] shirt = new bool[MaskSize * MaskSize], shorts = new bool[MaskSize * MaskSize], hair = new bool[MaskSize * MaskSize];
            foreach (var r in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = r.sharedMesh; if (!mesh) continue; var uv = mesh.uv; var mats = r.sharedMaterials;
                for (int sm = 0; sm < mesh.subMeshCount && sm < mats.Length; sm++)
                {
                    var m = mats[sm]; if (!m) continue;
                    bool[] target = r.name.StartsWith("Shirt") && m.mainTexture == look.skinAtlas ? shirt
                        : r.name.StartsWith("Shorts") && m.mainTexture == look.skinAtlas ? shorts
                        : r.name == "Body_Skin" && m.name.StartsWith("skin_BlondHair") ? hair : null;
                    if (target == null) continue;
                    var t = mesh.GetTriangles(sm);
                    for (int i = 0; i < t.Length; i += 3) Raster(target, uv[t[i]], uv[t[i + 1]], uv[t[i + 2]]);
                }
            }
            // Texture filtering samples a few texels past each UV island: grow each garment region into its padding,
            // or the island seams read as white 'crackle' lines on a recoloured shirt.
            shirt = Dilate(shirt, 4); shorts = Dilate(shorts, 4);
            var o = new Color[MaskSize * MaskSize]; double[] sum = new double[4]; int[] cnt = new int[4];
            for (int i = 0; i < o.Length; i++)
            {
                var c = ap[i]; Color.RGBToHSV(c, out float h, out float s, out float v);
                // shirt body: every low-saturation texel (whites and their shaded creases / seam greys)
                float R = shirt[i] && s < .3f && v > .25f ? 1 : 0;
                float G = (shorts[i] || shirt[i]) && h > .52f && h < .8f && s > .2f && v < .7f ? 1 : 0;
                float B = hair[i] ? 1 : 0;
                float A = sp[i].a;
                o[i] = new Color(R, G, B, A);
                var lin = c.linear; float l = .2126f * lin.r + .7152f * lin.g + .0722f * lin.b;
                if (R > .5f) { sum[0] += l; cnt[0]++; } if (G > .5f) { sum[1] += l; cnt[1]++; } if (B > .5f) { sum[2] += l; cnt[2]++; } if (A > .5f) { sum[3] += l; cnt[3]++; }
            }
            var tex = new Texture2D(MaskSize, MaskSize, TextureFormat.RGBA32, false); tex.SetPixels(o); tex.Apply();
            File.WriteAllBytes(ResDir + "HeroV4_KitMask.png", tex.EncodeToPNG());
            var rf = new Ref { shirt = (float)(sum[0] / Math.Max(1, cnt[0])), shorts = (float)(sum[1] / Math.Max(1, cnt[1])), hair = (float)(sum[2] / Math.Max(1, cnt[2])), skin = look.skinReference };
            File.WriteAllText(ResDir + "HeroV4_KitRef.json", JsonUtility.ToJson(rf));
            AssetDatabase.ImportAsset(ResDir + "HeroV4_KitMask.png");
            var ti = (TextureImporter)AssetImporter.GetAtPath(ResDir + "HeroV4_KitMask.png");
            ti.sRGBTexture = false; ti.mipmapEnabled = false; ti.alphaIsTransparency = false; ti.textureCompression = TextureImporterCompression.Uncompressed; ti.alphaSource = TextureImporterAlphaSource.FromInput; ti.SaveAndReimport();
            AssetDatabase.ImportAsset(ResDir + "HeroV4_KitRef.json");
            Directory.CreateDirectory(AppAssets);
            // iOS: opaque PNGs (a straight-alpha mask gets premultiplied away by CoreGraphics)
            var rgb = new Color[o.Length]; var sk = new Color[o.Length];
            for (int i = 0; i < o.Length; i++) { rgb[i] = new Color(o[i].r, o[i].g, o[i].b, 1); sk[i] = new Color(o[i].a, o[i].a, o[i].a, 1); }
            var t1 = new Texture2D(MaskSize, MaskSize, TextureFormat.RGB24, false); t1.SetPixels(rgb); t1.Apply();
            var t2 = new Texture2D(MaskSize, MaskSize, TextureFormat.RGB24, false); t2.SetPixels(sk); t2.Apply();
            File.WriteAllBytes(AppAssets + "/HeroV4_KitMask.png", t1.EncodeToPNG());
            File.WriteAllBytes(AppAssets + "/HeroV4_SkinMask.png", t2.EncodeToPNG());
            File.WriteAllText(AppAssets + "/HeroV4_KitRef.json", JsonUtility.ToJson(rf));
            File.WriteAllBytes(AppAssets + "/HeroV4_Atlas.png", atlas.EncodeToPNG());
            var iris = prefab.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).FirstOrDefault(m => m && m.name.Contains("EyeCornea"));
            if (iris && iris.mainTexture) File.WriteAllBytes(AppAssets + "/HeroV4_Iris.png", Readable(iris.mainTexture, 512).EncodeToPNG());
            Debug.Log($"HERO_LOCKER_MASK shirt={cnt[0]} shorts={cnt[1]} hair={cnt[2]} skin={cnt[3]} ref={JsonUtility.ToJson(rf)} atlas={atlasPath}");
        }

        static void Raster(bool[] m, Vector2 a, Vector2 b, Vector2 c)
        {
            a *= MaskSize; b *= MaskSize; c *= MaskSize;
            int x0 = Mathf.Max(0, (int)Mathf.Floor(Mathf.Min(a.x, Mathf.Min(b.x, c.x))) - 1), x1 = Mathf.Min(MaskSize - 1, (int)Mathf.Ceil(Mathf.Max(a.x, Mathf.Max(b.x, c.x))) + 1);
            int y0 = Mathf.Max(0, (int)Mathf.Floor(Mathf.Min(a.y, Mathf.Min(b.y, c.y))) - 1), y1 = Mathf.Min(MaskSize - 1, (int)Mathf.Ceil(Mathf.Max(a.y, Mathf.Max(b.y, c.y))) + 1);
            float E(Vector2 p, Vector2 q, Vector2 r) => (q.x - p.x) * (r.y - p.y) - (q.y - p.y) * (r.x - p.x);
            float area = E(a, b, c); if (Mathf.Abs(area) < 1e-6f) return;
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + .5f, y + .5f);
                    float w0 = E(b, c, p) / area, w1 = E(c, a, p) / area, w2 = E(a, b, p) / area;
                    if (w0 >= -.02f && w1 >= -.02f && w2 >= -.02f) m[y * MaskSize + x] = true;
                }
        }

        static bool[] Dilate(bool[] m, int r)
        {
            var o = (bool[])m.Clone();
            for (int y = 0; y < MaskSize; y++)
                for (int x = 0; x < MaskSize; x++)
                {
                    if (!m[y * MaskSize + x]) continue;
                    for (int dy = -r; dy <= r; dy++) for (int dx = -r; dx <= r; dx++)
                    {
                        int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= MaskSize || yy >= MaskSize) continue;
                        o[yy * MaskSize + xx] = true;
                    }
                }
            return o;
        }
        static Texture2D Readable(Texture src, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(src, rt); var prev = RenderTexture.active; RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false); t.ReadPixels(new Rect(0, 0, size, size), 0, 0); t.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(rt); return t;
        }

        // ------------------------------------------------------------------ mesh bake (play mode: the runtime fixes are built there)
        [Serializable] class Sub { public string material; public int indexOffset, indexCount; }
        [Serializable] class Part { public string name, hat, hair, body; public int vertexCount, positionOffset, normalOffset, uvOffset; public List<Sub> submeshes = new List<Sub>(); }
        [Serializable] class Mat { public string name, texture; public float[] color; public bool transparent; public float smoothness; }
        [Serializable] class Manifest { public int version = 1; public string pose = "Ready"; public List<Part> parts = new List<Part>(); public List<Mat> materials = new List<Mat>(); public float[] bounds; }

        static void Tick()
        {
            if (SessionState.GetInt("HeroLocker", 0) == 0 || !EditorApplication.isPlaying) return;
            try
            {
                if (step == 0)
                {
                    var old = Object.FindFirstObjectByType<ModularHeroLook>(); if (old) old.gameObject.SetActive(false);
                    hero = Object.Instantiate(Resources.Load<GameObject>(TennisHeroSetup.PrefabPath), Vector3.zero, Quaternion.identity);
                    hero.GetComponent<HeroTennisDriver>().Build(); step = 1; return;
                }
                if (step == 1) { step = 2; return; }   // let the face / occluder components run once
                var d = hero.GetComponent<HeroTennisDriver>(); var cos = hero.GetComponent<HeroCosmetics>();
                var man = new Manifest(); var bin = new MemoryStream(); var w = new BinaryWriter(bin);
                var mats = new Dictionary<string, Mat>();
                string bodyTag = null, hatHair = null;
                bool Shaped(Renderer r) { var sm = (r as SkinnedMeshRenderer)?.sharedMesh; if (!sm) return false; for (int i = 0; i < sm.blendShapeCount; i++) if (sm.GetBlendShapeName(i).EndsWith("Female")) return true; return false; }
                void Bake(string hatTag, Func<Renderer, bool> want)
                {
                    d.Sample(HeroTennisDriver.Clip.Ready, .6f);
                    foreach (var r in hero.GetComponentsInChildren<Renderer>(false))
                    {
                        if (!r.enabled || r is TrailRenderer || r is LineRenderer || r is ParticleSystemRenderer || !want(r)) continue;
                        Mesh mesh; Matrix4x4 toWorld;
                        if (r is SkinnedMeshRenderer smr) { mesh = new Mesh(); smr.BakeMesh(mesh, false); toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one); }
                        else { var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh) continue; mesh = mf.sharedMesh; toWorld = r.transform.localToWorldMatrix; }
                        var toHero = hero.transform.worldToLocalMatrix * toWorld;
                        var v = mesh.vertices; var n = mesh.normals; var uv = mesh.uv; if (uv.Length != v.Length) uv = new Vector2[v.Length];
                        var part = new Part { name = r.name, hat = hatTag, hair = hatHair ?? HairTagOf(r.name), body = bodyTag, vertexCount = v.Length };
                        part.positionOffset = (int)bin.Position; foreach (var p in v) { var q = toHero.MultiplyPoint3x4(p); w.Write(q.x); w.Write(q.y); w.Write(-q.z); }
                        part.normalOffset = (int)bin.Position;
                        for (int i = 0; i < v.Length; i++) { var q = (n.Length == v.Length ? toHero.MultiplyVector(n[i]) : Vector3.up).normalized; w.Write(q.x); w.Write(q.y); w.Write(-q.z); }
                        part.uvOffset = (int)bin.Position; foreach (var t in uv) { w.Write(t.x); w.Write(1 - t.y); }
                        var sm = r.sharedMaterials;
                        for (int s = 0; s < mesh.subMeshCount && s < sm.Length; s++)
                        {
                            var m = sm[s]; if (!m) continue; var key = m.name.Replace(" (Instance)", "");
                            if (!mats.ContainsKey(key))
                            {
                                var tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
                                // Iris, the hair detail maps (buzz / waves, shipped as HeroV4_<name>.png), else the shared atlas
                                string tname = !tex ? null : tex.name.Contains("Iris") ? "iris" : tex.name.StartsWith("Hair_") ? tex.name : "atlas";
                                // hair detail maps are keyed by the material name (Hero_01_HairTuft_<Style> -> Textures/Hair_<Style>.png)
                                if (key.StartsWith("Hero_01_HairTuft_"))
                                {
                                    string style = key.Substring("Hero_01_HairTuft_".Length).Split(' ')[0], src = "Assets/ArtDirection/Hero01/Textures/Hair_" + style + ".png";
                                    if (File.Exists(src)) { tname = "Hair_" + style; Directory.CreateDirectory(AppAssets); File.Copy(src, AppAssets + "/HeroV4_" + tname + ".png", true); }
                                }
                                var col = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.color;
                                mats[key] = new Mat { name = key, texture = tname, color = new[] { col.r, col.g, col.b, col.a }, transparent = m.renderQueue >= 2900, smoothness = m.HasProperty("_Smoothness") ? m.GetFloat("_Smoothness") : .3f };
                            }
                            var tri = mesh.GetTriangles(s);
                            var sub = new Sub { material = key, indexOffset = (int)bin.Position, indexCount = tri.Length };
                            for (int i = 0; i < tri.Length; i += 3) { w.Write((uint)tri[i]); w.Write((uint)tri[i + 2]); w.Write((uint)tri[i + 1]); }   // winding flip
                            part.submeshes.Add(sub);
                        }
                        man.parts.Add(part);
                    }
                }
                string HairTagOf(string n)
                {
                    if (n.EndsWith("_Free")) n = n.Substring(0, n.Length - 5);
                    return n == "Hair_Default" || n == "Hair_BandFill" ? "Swept" : n.StartsWith("Hair_BandFill_") ? n.Substring(14) : n.StartsWith("Hair_") ? n.Substring(5) : null;
                }
                bool InHat(Renderer r) { for (var t = r.transform; t; t = t.parent) if (t.name == "Slot_Hat" || t.name == "HatLiner") return true; return false; }
                bool Fill(Renderer r) => r.name.StartsWith("Hair_BandFill");
                bool Free(Renderer r) => r.name.StartsWith("Hair_") && r.name.EndsWith("_Free");
                bool Cut(Renderer r) => r.name.StartsWith("Hair_") && !Fill(r) && !Free(r);
                bool anyFree = hero.GetComponentsInChildren<Renderer>(true).Any(Free);
                cos.EquipHat(HeroCosmetics.Hat.Visor); Bake(null, r => !InHat(r) && !Fill(r) && !Cut(r) && !Free(r) && !Shaped(r)); Bake("Visor", InHat);
                // Hero V5: body + clothes twice, boy and girl ('Female' blend shape), tagged body=Boy/Girl
                foreach (var g in new[] { false, true })
                {
                    foreach (var r in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true)) HeroKit.SetFemale(r, g);
                    bodyTag = g ? "Girl" : "Boy"; Bake(null, Shaped); bodyTag = null;
                }
                foreach (var r in hero.GetComponentsInChildren<SkinnedMeshRenderer>(true)) HeroKit.SetFemale(r, false);
                // Hero V5: every haircut (tagged hair=<name>), and its natural band strip shown only with no headwear
                var hairRs = hero.GetComponentsInChildren<Renderer>(true).Where(r => Cut(r) || Fill(r) || Free(r)).ToList();
                var was = hairRs.ToDictionary(r => r, r => r.enabled);
                foreach (var r in hairRs) r.enabled = true;
                // with un-pressed variants: pressed cuts are tagged "Worn" (any headwear), _Free ones "None"; no band fills
                // a cut with an un-pressed twin is "Worn" (any headwear) + its _Free twin "None"; cuts with no twin (bald,
                // buzz, waves: nothing to press) show with every headwear
                bool HasFree(Renderer r) => hero.GetComponentsInChildren<Renderer>(true).Any(x => x.name == r.name + "_Free");
                if (anyFree) { Bake("Worn", r => Cut(r) && HasFree(r)); Bake(null, r => Cut(r) && !HasFree(r)); Bake("None", Free); } else { Bake(null, Cut); Bake("None", Fill); }
                foreach (var r in hairRs) r.enabled = was[r];
                cos.EquipHat(HeroCosmetics.Hat.Cap); Bake("Cap", InHat);
                cos.EquipHat(HeroCosmetics.Hat.Sweatband); Bake("Sweatband", InHat);
                // short cuts (bald / buzz / waves) wear the hats fitted to the bare skull: hair tag "Short"
                cos.shortHair = true; hatHair = "Short";
                foreach (var (hh, tag) in new[] { (HeroCosmetics.Hat.Visor, "Visor"), (HeroCosmetics.Hat.Cap, "Cap"), (HeroCosmetics.Hat.Sweatband, "Sweatband") })
                { cos.EquipHat(hh); Bake(tag, InHat); }
                cos.shortHair = false; hatHair = null;
                man.materials = mats.Values.ToList();
                Directory.CreateDirectory(AppAssets);
                File.WriteAllBytes(AppAssets + "/HeroV4.bin", bin.ToArray());
                File.WriteAllText(AppAssets + "/HeroV4.json", JsonUtility.ToJson(man, true));
                Debug.Log($"HERO_LOCKER_EXPORT parts={man.parts.Count} materials={man.materials.Count} bytes={bin.Length}");
                SessionState.SetInt("HeroLocker", 0);
                if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.isPlaying = false;
            }
            catch (Exception e) { Debug.LogException(e); SessionState.SetInt("HeroLocker", 0); if (Application.isBatchMode) EditorApplication.Exit(1); }
        }
    }
}

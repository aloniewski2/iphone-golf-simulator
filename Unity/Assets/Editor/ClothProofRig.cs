using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Tennis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GolfArcade.EditorTools
{
    // Fixed proof cameras, real prefabs/runtime materials, and the unmodified Tennis court/light rig.
    // CLOTH_OUT=<absolute dir> CLOTH_FILMS=1 Unity -batchmode -executeMethod GolfArcade.EditorTools.ClothProofRig.Run
    [InitializeOnLoad]
    public static class ClothProofRig
    {
        const string Flag = "ClothProofRig";
        static IEnumerator routine;
        static string Out => Environment.GetEnvironmentVariable("CLOTH_OUT") ?? "../work/cloth-overhaul/before";
        static readonly List<HeroAudit> audits = new List<HeroAudit>();
        [Serializable] class PaletteRow {public string sex,group,pick,role;public Color main,trim;}
        [Serializable] class PaletteReport {public PaletteRow[] rows;}
        static readonly List<PaletteRow> paletteRows = new List<PaletteRow>();
        [Serializable] class Piece { public string name; public int verts, tris, bones; public float areaM2, uvArea, pxPerCm; public string[] materials, maps; }
        [Serializable] class Socket { public string pose, name; public Vector3 world; }
        [Serializable] class HeroAudit { public string sex; public Piece[] kit; public Socket[] sockets; public int totalTris; }
        [Serializable] class Report { public string unity, gpu, pipeline; public HeroAudit[] heroes; }
        static ClothProofRig() { EditorApplication.update += Tick; }
        public static void Run()
        {
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true); EditorApplication.isPlaying = true;
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var g = Object.FindFirstObjectByType<TennisGame>();
            if (!g || !g.Initialized) return;
            routine ??= Capture(g);
            try { if (!routine.MoveNext()) Finish(0); }
            catch (Exception e) { Debug.LogException(e); Finish(1); }
        }
        static void Finish(int code)
        {
            SessionState.SetBool(Flag, false);
            File.WriteAllText(Path.Combine(Out, "audit.json"), JsonUtility.ToJson(new Report { unity = Application.unityVersion,
                gpu = SystemInfo.graphicsDeviceName, pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline.name, heroes = audits.ToArray() }, true));
            var shader=Shader.Find("GolfArcade/TennisCloth");
            var compat=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            var reason=typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic);
            int batch=(int)compat.Invoke(null,new object[]{shader,0});
            File.WriteAllText(Path.Combine(Out,"srp_batcher.txt"),batch+" "+reason.Invoke(null,new object[]{shader,0,batch}));
            if(paletteRows.Count>0)File.WriteAllText(Path.Combine(Out,"palette_uniforms.json"),JsonUtility.ToJson(new PaletteReport{rows=paletteRows.ToArray()},true));
            Time.captureFramerate = 0;
            if (Application.isBatchMode) EditorApplication.Exit(code); else EditorApplication.isPlaying = false;
        }
        static void Save(Camera cam, string file, int w = 1080, int h = 1080)
        {
            file = Path.Combine(Out, file + ".png"); Directory.CreateDirectory(Path.GetDirectoryName(file));
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var prev = cam.targetTexture; var active = RenderTexture.active;
            try {
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); File.WriteAllBytes(file, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            } finally { cam.targetTexture = prev; RenderTexture.active = active; RenderTexture.ReleaseTemporary(rt); }
        }
        static void View(Camera cam, Transform hero, Vector3 target, Vector3 offset, float fov)
        {
            var to = hero.TransformPoint(target); var from = hero.TransformPoint(target + offset);
            cam.transform.SetPositionAndRotation(from, Quaternion.LookRotation(to - from)); cam.fieldOfView = fov;
            var rim = Object.FindFirstObjectByType<HeroRimLight>(); if (rim) rim.Aim();
        }
        static Piece Audit(SkinnedMeshRenderer r)
        {
            var m = r.sharedMesh; var v = m.vertices; var uv = m.uv; double a = 0, b = 0;
            var tri = m.triangles;
            for (int i = 0; i < tri.Length; i += 3) {
                int x = tri[i], y = tri[i + 1], z = tri[i + 2];
                a += Vector3.Cross(v[y] - v[x], v[z] - v[x]).magnitude * .5;
                if (uv.Length == v.Length) { var e = uv[y] - uv[x]; var f = uv[z] - uv[x]; b += Mathf.Abs(e.x * f.y - e.y * f.x) * .5; }
            }
            int size = r.name == "Kit_Top" ? 2048 : r.name.Contains("Sock") ? 512 : 1024;
            return new Piece { name = r.name, verts = m.vertexCount, tris = tri.Length / 3, bones = r.bones.Length,
                areaM2 = (float)a, uvArea = (float)b, pxPerCm = b > 0 ? size / (float)Math.Sqrt(a / b) / 100 / r.transform.lossyScale.x : 0,
                materials = r.sharedMaterials.Select(x => x.name).ToArray(),
                maps = r.sharedMaterials.SelectMany(x => new[] { "_BaseMap", "_NormalMap", "_MaskMap", "_WeaveMap" }.Select(p => p + "=" + (x.HasProperty(p) && x.GetTexture(p) ? x.GetTexture(p).name : "-"))).ToArray() };
        }
        static void Pose(HeroTennisDriver d, HeroTennisDriver.Clip slot, float seconds)
        {
            var s = d.slots.FirstOrDefault(x => x.id == slot);
            if (!s.clip) throw new InvalidOperationException("Missing clip: " + slot);
            s.clip.SampleAnimation(d.gameObject, Mathf.Clamp(seconds, 0, s.clip.length));
        }
        static int Tris(Renderer r)
        {
            var m = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
            return m ? (int)(Enumerable.Range(0, m.subMeshCount).Sum(i => (long)m.GetIndexCount(i)) / 3) : 0;
        }
        static void Mask(Camera cam, MatchHeroLook look, string name, int w=1080, int h=1080, bool dataAO=false)
        {
            var saved = look.kit.Select(r => (r, r.gameObject.layer, r.sharedMaterials)).ToArray();
            int cull=cam.cullingMask;var flags=cam.clearFlags;var bg=cam.backgroundColor;
            var temporary=new List<Material>();
            try {
                foreach(var row in saved) {
                    row.r.gameObject.layer=31;
                    row.r.sharedMaterials=row.Item3.Select(src=>{
                        string role=src.name.Replace(" (hero)", "");Color col=Color.black;
                        if(role==MatchHeroLook.RoleShirt)col=Color.red;
                        if(role==MatchHeroLook.RoleShorts)col=Color.green;
                        if(role==MatchHeroLook.RoleShirtTrim||role==MatchHeroLook.RoleShortsBand)col=Color.blue;
                        var m=new Material(Shader.Find("Hidden/ClothProofMask"));m.SetColor("_ClassColour",col);m.SetFloat("_ShowAO",dataAO?1:0);
                        if(src.HasProperty("_MaskMap") && src.HasProperty("_UseGarmentMaps") && src.GetFloat("_UseGarmentMaps")>.5f) {
                            m.SetTexture("_MaskMap",src.GetTexture("_MaskMap"));m.SetFloat("_UseMaps",1);
                        }
                        temporary.Add(m);return m;
                    }).ToArray();
                }
                cam.cullingMask=unchecked((int)0x80000000);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
                Save(cam,name+(dataAO?"_data_mask":"_mask"),w,h);
            } finally {
                foreach(var row in saved){row.r.gameObject.layer=row.Item2;row.r.sharedMaterials=row.Item3;}
                cam.cullingMask=cull;cam.clearFlags=flags;cam.backgroundColor=bg;foreach(var m in temporary)Object.DestroyImmediate(m);
            }
        }
        static void FoldPair(Camera cam, Transform root, MatchHeroLook look, string name, Vector3 target, Vector3 offset)
        {
            View(cam,root,target,offset,28);Save(cam,name);
            foreach(var m in look.kit.SelectMany(r=>r.sharedMaterials))if(m.HasProperty("_AOStrength"))m.SetFloat("_AOStrength",0);
            Save(cam,name+"_noao");Mask(cam,look,name);Mask(cam,look,name,1080,1080,true);
            foreach(var m in look.kit.SelectMany(r=>r.sharedMaterials))if(m.HasProperty("_AOStrength"))m.SetFloat("_AOStrength",1);
        }
        static void ConstructionDetails(Camera cam,HeroTennisDriver driver,MatchHeroLook look,string sex)
        {
            var root=driver.transform;var shoe=look.kit.First(r=>r.name=="Kit_Shoe_R");var bind=shoe.sharedMesh.vertices;var baked=new Mesh();shoe.BakeMesh(baked);var pose=baked.vertices;
            bool female=sex=="female";
            var points=new[]{("shoe_stitch_detail",female?new Vector3(-.19f,.035f,.10f):new Vector3(-.276f,.055f,.168f),new Vector3(-.18f,.065f,.025f)),
                ("shoe_lace_detail",female?new Vector3(-.125f,.067f,.06f):new Vector3(-.195f,.102f,.125f),new Vector3(0,.23f,.075f))};
            foreach(var point in points) {
                var bindTarget=shoe.transform.InverseTransformPoint(root.TransformPoint(point.Item2));
                int index=Enumerable.Range(0,bind.Length).OrderBy(i=>(bind[i]-bindTarget).sqrMagnitude).First();
                var target=root.InverseTransformPoint(shoe.transform.TransformPoint(pose[index]));
                View(cam,root,target,point.Item3,30);Save(cam,sex+"_"+point.Item1);
            }
            Object.DestroyImmediate(baked);
            var bottom=look.kit.First(r=>r.name=="Kit_Bottom");var bbind=bottom.sharedMesh.vertices;var bm=new Mesh();bottom.BakeMesh(bm);
            var bt=bottom.transform.InverseTransformPoint(root.TransformPoint(new Vector3(-.12f,female?.644f:.600f,.06f)));
            int bi=Enumerable.Range(0,bbind.Length).OrderBy(i=>(bbind[i]-bt).sqrMagnitude).First();
            var hemTarget=root.InverseTransformPoint(bottom.transform.TransformPoint(bm.vertices[bi]));
            View(cam,root,hemTarget,new Vector3(-.18f,.015f,.20f),28);Save(cam,sex+"_hem_detail");Object.DestroyImmediate(bm);
        }
        static void ShoeDetails(Camera cam,HeroTennisDriver driver,MatchHeroLook look,string sex)
        {
            var root=driver.transform;var shoe=look.kit.First(r=>r.name=="Kit_Shoe_R");var mesh=new Mesh();shoe.BakeMesh(mesh);mesh.RecalculateBounds();
            var target=root.InverseTransformPoint(shoe.transform.TransformPoint(mesh.bounds.center));Object.DestroyImmediate(mesh);
            // Close-up inspection needs the sole visible. Remove only the occluding court floor, identically in both variants.
            var floor=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.bounds.size.x>6&&r.bounds.size.z>6&&r.bounds.size.y<.2f).ToArray();
            foreach(var r in floor)r.enabled=false;
            foreach(var entry in new[]{("shoe",new Vector3(-.42f,.32f,.48f),38f),("shoe_side",new Vector3(-.65f,.04f,.08f),34f),("sole",new Vector3(-.18f,-.45f,.25f),38f)}) {
                View(cam,root,target,entry.Item2,entry.Item3);Save(cam,sex+"_"+entry.Item1);
            }
            foreach(var r in floor)r.enabled=true;
        }
        static IEnumerator Shimmer(Camera cam, HeroTennisDriver d, MatchHeroLook look, string sex)
        {
            var root=d.transform;Pose(d,HeroTennisDriver.Clip.Ready,0);
            var anchor=root.position;var direction=root.right;
            // Keep the whole shirt inside the match proof camera's portrait frustum.
            View(cam,root,new Vector3(0,.95f,7.2f),new Vector3(0,4.85f,-13),54);
            var from=cam.transform.position;var rotation=cam.transform.rotation;
            for(int i=0;i<180;i++) {
                Pose(d,HeroTennisDriver.Clip.RunRight,(i/60f)%d.slots.First(s=>s.id==HeroTennisDriver.Clip.RunRight).clip.length);
                root.position=anchor+direction*Mathf.Lerp(-.6f,.6f,i/179f);
                cam.transform.SetPositionAndRotation(from,rotation);
                var rim=Object.FindFirstObjectByType<HeroRimLight>();if(rim)rim.Aim();
                Save(cam,sex+"_shimmer/"+i.ToString("D4"),1170,2532);
                Mask(cam,look,sex+"_shimmer_mask/"+i.ToString("D4"),1170,2532);
                if(i%12==0)yield return null;
            }
            root.position=anchor;Pose(d,HeroTennisDriver.Clip.Ready,0);
        }
        static IEnumerator Capture(TennisGame g)
        {
            Time.captureFramerate = 60;
            for (int i = 0; i < 40; i++) yield return null;
            var presentation = g.GetComponent<TennisPresentation>(); if (presentation) presentation.Finish();
            var cam = g.GameplayCamera ? g.GameplayCamera : Camera.main;
            foreach (var b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
                if (b is TennisGame || b is TennisActor || b is HeroTennisDriver || b.GetType().Name.Contains("Camera")) b.enabled = false;
            foreach (bool female in new[] { false, true }) {
                g.SelectCharacter(female);
                for (int i = 0; i < 3; i++) yield return null;
                foreach (var drv in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None)) { drv.enabled = false; var an = drv.GetComponent<Animator>(); if (an) an.enabled = false; }
                var d = g.Player.GetComponentInChildren<HeroTennisDriver>(); var root = d.transform; var look = d.matchLook;
                look.SetKit(null, null, null); look.SetSkin(HeroKit.Hex("C47A4C")); Pose(d, HeroTennisDriver.Clip.Ready, 0);
                string sex = female ? "female" : "male";
                var sock = new List<Socket>();
                foreach (var pair in new[] { (HeroTennisDriver.Clip.Ready, 0f), (HeroTennisDriver.Clip.Serve, d.slots.First(s => s.id == HeroTennisDriver.Clip.Serve).contact) }) {
                    Pose(d, pair.Item1, pair.Item2);
                    foreach (var t in root.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Socket") || t.name == "Hand_Racket" || t.name.Contains("_Shoe") || t.name == "Chest_Top" || t.name == "Hip_Bottom"))
                        sock.Add(new Socket { pose = pair.Item1.ToString(), name = t.name, world = t.position });
                }
                Pose(d, HeroTennisDriver.Clip.Ready, 0);
                audits.Add(new HeroAudit { sex = sex, kit = look.kit.Select(Audit).ToArray(), sockets = sock.ToArray(),
                    totalTris = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is TrailRenderer)).Sum(Tris) });
                if(Environment.GetEnvironmentVariable("CLOTH_SHOEONLY")=="1") {ShoeDetails(cam,d,look,sex);continue;}
                if(Environment.GetEnvironmentVariable("CLOTH_DETAILONLY")=="1") {ConstructionDetails(cam,d,look,sex);continue;}
                if(Environment.GetEnvironmentVariable("CLOTH_SHIMMERONLY")=="1") {var film=Shimmer(cam,d,look,sex);while(film.MoveNext())yield return film.Current;continue;}
                if(Environment.GetEnvironmentVariable("CLOTH_G2ONLY")=="1") {
                    FoldPair(cam,root,look,sex+"_fold_armpit",new Vector3(-.18f,1.16f,0),new Vector3(-.75f,.04f,-.65f));
                    FoldPair(cam,root,look,sex+"_fold_waist",new Vector3(0,.98f,.08f),new Vector3(0,.02f,-1));
                    FoldPair(cam,root,look,sex+"_fold_crotch",new Vector3(0,.76f,.04f),new Vector3(.16f,.06f,1));
                    continue;
                }
                // Match cameras include both heroes and the racket, exactly under court lighting.
                View(cam, root, new Vector3(0, .95f, 7.2f), new Vector3(0, 4.85f, -13f), 54);
                Save(cam, sex + "_match_behind", 1280, 720); Save(cam, sex + "_match_phone", 1170, 2532);
                Mask(cam,look,sex+"_match_phone",1170,2532);
                View(cam, root, new Vector3(0, 1, 14), new Vector3(0, 4.8f, -20), 38); Save(cam, sex + "_opponent", 1170, 2532);
                var racket = look.racketGrip; if (racket) racket.gameObject.SetActive(false);
                // Same authored targets and offsets are reused for every iteration.
                var views = new[] {
                    ("vent_detail", new Vector3(-.17f,.964f,-.035f), new Vector3(-.38f,.025f,.14f),24f),
                    ("pocket_detail", new Vector3(-.16f,.865f,.06f), new Vector3(-.38f,.025f,.32f),24f),
                    ("collar", new Vector3(0, 1.35f, .07f), new Vector3(.13f, .13f, .85f), 24f),
                    ("sleeve", new Vector3(-.24f, 1.22f, 0), new Vector3(-.65f, .2f, .75f), 25f),
                    ("waist", new Vector3(0, .92f, .06f), new Vector3(.15f, .06f, 1), 28f),
                    ("bottom_front", new Vector3(0, .72f, .03f), new Vector3(.2f, .08f, 1.15f), 28f),
                    ("bottom_side", new Vector3(-.16f, .73f, 0), new Vector3(-1f, .06f, .25f), 30f),
                    ("bottom_back", new Vector3(0, .75f, 0), new Vector3(.12f, .04f, -1.15f), 30f),
                    ("bottom_other", new Vector3(.16f, .73f, 0), new Vector3(1f, .06f, .25f), 30f),
                    ("sock", new Vector3(-.12f, .23f, 0), new Vector3(-.15f, .09f, .72f), 23f),
                    ("shoe", new Vector3(-.14f, .075f, .03f), new Vector3(-.4f, .3f, .5f), 27f),
                    ("shoe_side", new Vector3(-.14f, .075f, .03f), new Vector3(-.7f, .08f, 0), 27f),
                    ("sole", new Vector3(-.14f, .01f, .03f), new Vector3(-.3f, -.4f, .35f), 27f)
                };
                foreach (var v in views) { if(v.Item1.StartsWith("shoe")||v.Item1=="sole") {
                        var shoe=look.kit.First(r=>r.name=="Kit_Shoe_R");var mesh=new Mesh();shoe.BakeMesh(mesh);mesh.RecalculateBounds();
                        var target=root.InverseTransformPoint(shoe.transform.TransformPoint(mesh.bounds.center));Object.DestroyImmediate(mesh);
                        // Foot construction camera correction is identical for BEFORE and AFTER (shoe positions unchanged).
                        var offset=v.Item1=="shoe"?new Vector3(-.42f,.32f,.48f):v.Item1=="sole"?new Vector3(-.18f,-.45f,.25f):new Vector3(-.65f,.04f,.08f);
                        View(cam,root,target,offset,v.Item1=="shoe_side"?34:38);
                        if(v.Item1=="sole" || v.Item1=="shoe_side") {
                            // Hide only the court floor for the underside proof. Lighting and shadow settings remain unchanged.
                            var floor=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.bounds.size.x>6&&r.bounds.size.z>6&&r.bounds.size.y<.2f).ToArray();
                            foreach(var r in floor)r.enabled=false;Save(cam,sex+"_"+v.Item1);foreach(var r in floor)r.enabled=true;continue;
                        }
                    } else View(cam, root, v.Item2, v.Item3, v.Item4);
                    Save(cam, sex + "_" + v.Item1); }
                foreach (var v in new[] { ("front", new Vector3(0, .1f, 3.5f)), ("back", new Vector3(0, .1f, -3.5f)), ("threequarter", new Vector3(-2.5f, .15f, 2.5f)) }) {
                    View(cam, root, new Vector3(0, .85f, 0), v.Item2, 32); Save(cam, sex + "_full_" + v.Item1);
                }
                View(cam,root,new Vector3(0,.91f,.07f),new Vector3(0,-.18f,1),24);Save(cam,sex+"_drawcord");
                FoldPair(cam,root,look,sex+"_fold_armpit",new Vector3(-.18f,1.16f,0),new Vector3(-.75f,.04f,-.65f));
                FoldPair(cam,root,look,sex+"_fold_waist",new Vector3(0,.98f,.08f),new Vector3(0,.02f,-1));
                FoldPair(cam,root,look,sex+"_fold_crotch",new Vector3(0,.76f,.04f),new Vector3(.16f,.06f,1));
                string[] palette={"F2F2F0","3FA9F5","1E2A6E","9EE63A","FFC233","FF6B4A","8A4FFF","2E3138","000000","FFFFFF"};
                foreach(string group in new[]{"shirt","shorts","shoes"})foreach(string pick in palette) {
                    look.SetKit(group=="shirt"?pick:null,group=="shorts"?pick:null,group=="shoes"?pick:null);
                    foreach(var mat in look.kit.SelectMany(r=>r.sharedMaterials).GroupBy(m=>m.name).Select(x=>x.First()))paletteRows.Add(new PaletteRow{sex=sex,group=group,pick=pick,role=mat.name.Replace(" (hero)",""),main=mat.GetColor("_BaseColor"),trim=mat.HasProperty("_TrimColor")?mat.GetColor("_TrimColor"):Color.clear});
                    View(cam,root,new Vector3(0,.85f,0),new Vector3(-1.5f,.1f,3.2f),32);Save(cam,"palette/"+sex+"_"+group+"_"+pick,540,900);
                }
                look.SetKit(null,null,null);
                if (racket) racket.gameObject.SetActive(true);
                if (Environment.GetEnvironmentVariable("CLOTH_FILMS") != "0") {
                    foreach (var v in new[] { ("ready", HeroTennisDriver.Clip.Ready), ("forehand", HeroTennisDriver.Clip.Forehand), ("trophy", HeroTennisDriver.Clip.Serve), ("run", HeroTennisDriver.Clip.RunForward), ("pushups", HeroTennisDriver.Clip.IntroPushups) }) {
                        for (int i = 0; i < 120; i++) { Pose(d, v.Item2, (i / 60f) % d.slots.First(s => s.id == v.Item2).clip.length); View(cam, root, new Vector3(0, .95f, 0), new Vector3(-2.5f, .6f, 2.7f), 40); Save(cam, sex + "_" + v.Item1 + "/" + i.ToString("D4"), 960, 960); if (i % 12 == 0) yield return null; }
                    }
                    if(Environment.GetEnvironmentVariable("CLOTH_CAPTURE_SHIMMER")=="1") {var shimmer=Shimmer(cam,d,look,sex);while(shimmer.MoveNext())yield return shimmer.Current;}
                    var start = root.position;
                    root.position = start; Pose(d, HeroTennisDriver.Clip.Ready, 0);
                    for (int i = 0; i < 120; i++) { float a = i * Mathf.PI / 60; View(cam, root, new Vector3(0, .85f, 0), new Vector3(Mathf.Sin(a) * 3.5f, .15f, Mathf.Cos(a) * 3.5f), 32); Save(cam, sex + "_turntable/" + i.ToString("D4"), 960, 960); if (i % 12 == 0) yield return null; }
                }
                Debug.Log("[ClothProofRig] captured " + sex);
            }
        }
    }
}

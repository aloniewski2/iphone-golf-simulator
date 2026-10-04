using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Tennis;

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
        static int frame;
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
            Directory.CreateDirectory(Out);
            EditorSceneManager.OpenScene("Assets/Scenes/Tennis.unity");
            SessionState.SetBool(Flag, true);
            EditorApplication.isPlaying = true;
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Flag, false) || !EditorApplication.isPlaying) return;
            var game = Object.FindFirstObjectByType<TennisGame>();
            if (!game || !game.Initialized) return;
            if (++frame < 45) return;   // let particles prewarm and Start()s run
            SessionState.SetBool(Flag, false);
            try { Capture(game); }
            catch (System.Exception e) { Debug.LogError("[VenueCapture] " + e); }
            if (Application.isBatchMode) EditorApplication.Exit(0); else EditorApplication.isPlaying = false;
        }

        static void Capture(TennisGame game)
        {
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
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            void Shot(string label, Vector3 pos, Vector3 look, float fov)
            {
                if (!string.IsNullOrEmpty(only) && System.Array.IndexOf(only.Split(','), label) < 0) return;
                cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation(look - pos); cam.fieldOfView = fov;
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
                File.WriteAllBytes($"{Out}/{name}_{label}.png", tex.EncodeToPNG());
            }
            Shot("1_gameplay", new Vector3(0, 3.5f, -15.6f), new Vector3(0, 1.15f, -3.2f), 56);
            Shot("2_postcard", new Vector3(0, 9f, -30f), new Vector3(0, 1f, 6f), 46);
            for (int i = 0; i <= 5; i++)
            {
                TennisVenue.Drone(i / 5f, out var p, out var l, out var f);
                Shot($"3_drone{i}", p, l, f);
            }
            Shot("4_side", new Vector3(58, 10, -46), new Vector3(0, -4, 0), 50);
            Shot("5_below", new Vector3(34, -26, -70), new Vector3(0, -6, 0), 55);
            Shot("6_edge", new Vector3(-9.5f, 2.2f, -18f), new Vector3(-19, -6, 2), 62);
            // review extras (not part of the shipped set): the view from the far end back over the near end, and the rival's intro
            Shot("7_rear", new Vector3(0, 3.5f, 15.6f), new Vector3(0, 1.15f, 3.2f), 56);
            Shot("8_rivalintro", new Vector3(-1.6f, 1.25f, 6.2f), new Vector3(0, 1f, 11.2f), 40);
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

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GolfArcade.Tennis;

namespace GolfArcade.EditorTools
{
    /// Screenshots of a tennis venue from the gameplay camera, the drone path and a few outside angles:
    ///   HERO_VENUE=skyscraper Unity -projectPath Unity -executeMethod GolfArcade.EditorTools.TennisVenueCapture.Run
    /// Writes ArtDir/screenshots/venues/<venue>_<shot>.png
    [InitializeOnLoad]
    public static class TennisVenueCapture
    {
        const string Flag = "TennisVenueCapture";
        static int frame;
        static string Out => Path.GetFullPath("../ArtDir/screenshots/venues");

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
            var cam = game.GameplayCamera;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var tex = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            void Shot(string label, Vector3 pos, Vector3 look, float fov)
            {
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
            Debug.Log("[VenueCapture] wrote " + Out);
        }
    }
}

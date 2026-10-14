using System;
using System.Collections;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.Tests
{
    /// Captures the Plaza's bay-screen previews from our own game (PLAN_MenuHub_WalkableWorld §3): a few seconds of a self-playing
    /// rally on each tennis venue and of each golf course's opening, 640x360 at 30 fps, H.264 (Unity's MediaEncoder). Runs only with
    /// HUB_CAPTURE_OUT set (an output folder); the clips are then copied to Assets/Resources/Hub/Previews.
    public class HubPreviewCapture
    {
        const int W = 640, H = 360, Fps = 30;
        static string Out => Environment.GetEnvironmentVariable("HUB_CAPTURE_OUT");

        [UnityTest, Timeout(1800000)] public IEnumerator CaptureBayPreviews()
        {
            if (string.IsNullOrEmpty(Out)) { Assert.Ignore("HUB_CAPTURE_OUT not set"); yield break; }
            Directory.CreateDirectory(Out);
            var host = new GameObject("NativeSportsSession"); Object.DontDestroyOnLoad(host);
            var bridge = host.AddComponent<NativeSportsSession>();
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("HUB_CAPTURE_SECONDS"), out var s) ? s : 6f;
            try
            {
                foreach (var (sport, place, name) in new[] { ("tennis", "resort", "tennis_resort"), ("tennis", "skyscraper", "tennis_skyscraper"), ("tennis", "volcano", "tennis_volcano"),
                                                            ("golf", "cliffside", "golf_cliffside"), ("golf", "wildisles", "golf_wildisles"), ("golf", "magma", "golf_magma") })
                {
                    var only = Environment.GetEnvironmentVariable("HUB_CAPTURE_ONLY");
                    if (!string.IsNullOrEmpty(only) && !name.StartsWith(only)) continue;
                    string id = "capture-" + name;
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = id, action = "start", sport = sport, venue = sport == "tennis" ? place : "resort", course = place,
                        bench = true, touch = true, intros = sport == "golf" ? "full" : "off", mode = sport == "tennis" ? "exhibition" : "round", holeFlyover = sport == "golf", sets = 3, games = 6 }));
                    for (int i = 0; i < 2400 && !bridge.Ready; i++) yield return null;
                    Assert.IsTrue(bridge.Ready, name + " loaded");
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = id, action = "resume" }));
                    yield return new WaitForSecondsRealtime(sport == "tennis" ? 3.5f : .5f);
                    var cam = bridge.GameplayCamera; Assert.IsNotNull(cam);
                    var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                    int frames = Mathf.RoundToInt(seconds * Fps);
                    Time.captureFramerate = Fps;
#if UNITY_EDITOR
                    var path = Path.Combine(Out, name + ".mp4");
                    var attrs = new UnityEditor.Media.VideoTrackAttributes { frameRate = new UnityEditor.Media.MediaRational(Fps), width = W, height = H, includeAlpha = false, bitRateMode = UnityEditor.VideoBitrateMode.High };
                    using (var encoder = new UnityEditor.Media.MediaEncoder(path, attrs))
                    {
                        for (int f = 0; f < frames; f++)
                        {
                            yield return new WaitForEndOfFrame();
                            // the picture only: HUD canvases off for the shot
                            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None); var was = new bool[canvases.Length];
                            for (int c = 0; c < canvases.Length; c++) { was[c] = canvases[c].enabled; canvases[c].enabled = false; }
                            var keep = cam.targetTexture; float aspect = cam.aspect;
                            cam.targetTexture = rt; cam.aspect = W / (float)H; cam.Render(); cam.targetTexture = keep; cam.aspect = aspect;
                            for (int c = 0; c < canvases.Length; c++) if (canvases[c]) canvases[c].enabled = was[c];
                            var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply(); RenderTexture.active = prev;
                            encoder.AddFrame(tex);
                            if (f == frames / 2) File.WriteAllBytes(Path.Combine(Out, name + ".png"), tex.EncodeToPNG());
                        }
                    }
#endif
                    Time.captureFramerate = 0;
                    Object.Destroy(rt); Object.Destroy(tex);
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message { version = 1, session = id, action = "end" }));
                    yield return null;
                }
            }
            finally { Time.captureFramerate = 0; Object.DestroyImmediate(host); Time.timeScale = 1; }
        }
    }
}

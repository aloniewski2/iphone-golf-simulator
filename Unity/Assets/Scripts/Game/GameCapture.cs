using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Renders what the phone would show — the main camera plus the overlay HUD — into a PNG at
    /// phone resolution, without depending on a Game view being visible or even present. Used
    /// by the editor's capture menu and by the review-capture PlayMode test.
    public static class GameCapture
    {
        public const int PhoneWidth = 1080, PhoneHeight = 2340;

        public static string Save(string path, int width = PhoneWidth, int height = PhoneHeight)
        {
            var camera = Camera.main;
            if (!camera) return null;
            var rt = RenderTexture.GetTemporary(width, height, 24);
            Graphics.SetRenderTarget(rt); GL.Clear(true, true, Color.black); Graphics.SetRenderTarget(null);
            // The phone's picture: every camera that draws to the phone (display 0), in its own
            // rect, in depth order — the course view may keep to the top of the screen.
            var cameras = new List<Camera>();
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                if (c.enabled && c.gameObject.activeInHierarchy && c.targetDisplay == 0 && !c.targetTexture) cameras.Add(c);
            cameras.Sort((a, b) => a.depth.CompareTo(b.depth));
            foreach (var c in cameras)
            {
                var prev = c.targetTexture; c.targetTexture = rt; c.Render(); c.targetTexture = prev;
            }
            // An overlay canvas only draws to the screen; it comes through Render in camera space,
            // drawn by the main camera over the full frame (its own rect may be partial).
            // The canvases are built in code on the default layer; for this pass alone they go on
            // the UI layer so only they draw.
            var overlays = new List<Canvas>();
            var relayered = new List<(GameObject go, int layer)>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    overlays.Add(c);
                    c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 0.5f;
                    foreach (var t in c.GetComponentsInChildren<Transform>(true)) { relayered.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = 5; }
                }
            var rect = camera.rect; var clear = camera.clearFlags; var mask = camera.cullingMask; var previous = camera.targetTexture;
            camera.rect = new Rect(0, 0, 1, 1); camera.clearFlags = CameraClearFlags.Nothing; camera.cullingMask = 1 << 5; camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            camera.rect = rect; camera.clearFlags = clear; camera.cullingMask = mask; camera.targetTexture = previous;
            foreach (var (go, layer) in relayered) go.layer = layer;
            foreach (var c in overlays) c.renderMode = RenderMode.ScreenSpaceOverlay;

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            // a .jpg is for the frames of a video (a PNG a frame is gigabytes a minute)
            bool jpg = path.EndsWith(".jpg", System.StringComparison.OrdinalIgnoreCase);
            File.WriteAllBytes(path, jpg ? tex.EncodeToJPG(92) : tex.EncodeToPNG());
            Object.Destroy(tex);
            return path;
        }
    }
}

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
            // URP RenderGraph does not preserve the colour attachment across a second
            // Camera.Render invocation with ClearFlags.Depth. Draw the world and its HUD
            // together, instead of replacing the world with a UI-only second render.
            var overlays = new List<(Canvas canvas, Camera previousCamera, float distance)>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    overlays.Add((c,c.worldCamera,c.planeDistance));
                    c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 0.5f;
                }
            // the full-screen cards size themselves to the surface being drawn to, not to the editor window
            GolfArcade.UI.OffscreenSize.Override = new Vector2Int(width, height);
            foreach (var card in Object.FindObjectsByType<GolfArcade.UI.HoleOutCard>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) card.RefreshLayout();
            foreach (var cover in Object.FindObjectsByType<GolfArcade.UI.HoleLoadingCover>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)) cover.RefreshLayout();
            try
            {
                Canvas.ForceUpdateCanvases();
                foreach (var c in cameras)
                {
                    var previous = c.targetTexture;
                    try { c.targetTexture = rt; c.Render(); }
                    finally { c.targetTexture = previous; }
                }
            }
            finally
            {
                GolfArcade.UI.OffscreenSize.Override = null;
                foreach (var state in overlays)
                {
                    state.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    state.canvas.worldCamera = state.previousCamera;
                    state.canvas.planeDistance = state.distance;
                }
                Canvas.ForceUpdateCanvases();
            }

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

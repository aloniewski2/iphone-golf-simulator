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
            // An overlay canvas only draws to the screen; in camera space it comes through Render.
            var overlays = new List<Canvas>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    overlays.Add(c);
                    c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = camera; c.planeDistance = 0.5f;
                }
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            camera.targetTexture = previous;
            foreach (var c in overlays) c.renderMode = RenderMode.ScreenSpaceOverlay;

            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            var active = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            // JPEG for film frames: a minute at 60fps as PNG runs to gigabytes.
            File.WriteAllBytes(path, path.EndsWith(".jpg") ? tex.EncodeToJPG(92) : tex.EncodeToPNG());
            Object.Destroy(tex);
            return path;
        }
    }
}

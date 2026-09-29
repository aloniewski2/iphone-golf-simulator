using System.Collections;
using System.IO;
using System.Reflection;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// Stills of the player waiting to return a serve, from the side, the front and behind, while the
    /// opponent goes through the serve. Runs only with HERO_CAPTURE_STANCE=<tag>; output in ArtDir/screenshots/stance/<tag>.
    public class TennisStanceCaptureTests
    {
        [UnityTest, Timeout(600000)] public IEnumerator FilmReceivingStance()
        {
            var tag = System.Environment.GetEnvironmentVariable("HERO_CAPTURE_STANCE");
            Assume.That(tag, Is.Not.Null.And.Not.Empty, "set HERO_CAPTURE_STANCE to capture");
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.ConfigureMatch(TennisGame.Mode.Training, "", "", "", 1, 3);   // skips the broadcast intro
            // The opponent serves this point: the player is the receiver.
            var t = typeof(TennisGame);
            t.GetField("match", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, TennisMatch.New(false, 1, 3));
            t.GetMethod("BeginPoint", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            string dir = Path.GetFullPath("../ArtDir/screenshots/stance/" + tag); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            Time.captureFramerate = 30;
            var cam = game.GameplayCamera;
            var rt = new RenderTexture(960, 540, 24) { antiAliasing = 4 };
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            void Shot(string name, Vector3 pos, Vector3 look, float fov)
            {
                var keepPos = cam.transform.position; var keepRot = cam.transform.rotation; var keepFov = cam.fieldOfView;
                cam.transform.position = pos; cam.transform.rotation = Quaternion.LookRotation(look - pos); cam.fieldOfView = fov;
                cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = null; cam.targetTexture = null;
                File.WriteAllBytes($"{dir}/{name}.png", tex.EncodeToPNG());
                cam.transform.position = keepPos; cam.transform.rotation = keepRot; cam.fieldOfView = keepFov;
            }
            try
            {
                for (int frame = 0; frame < 150; frame++)
                {
                    yield return null;
                    if (frame % 15 != 0) continue;
                    var p = game.Player.transform; Vector3 chest = p.position + Vector3.up * .95f;
                    string n = (frame / 15).ToString("00");
                    Shot($"{n}_side", chest + p.right * 3.4f + Vector3.up * .1f, chest, 34);
                    Shot($"{n}_front", chest + p.forward * 3.2f + Vector3.up * .3f, chest, 34);
                    Shot($"{n}_back", chest - p.forward * 3.0f + p.right * -1.4f + Vector3.up * .6f, chest, 40);
                }
            }
            finally { Time.captureFramerate = 0; }
        }
    }
}

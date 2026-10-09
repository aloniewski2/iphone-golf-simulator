using System.Collections;
using System.IO;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// Score-80 Phase E proof: the court set from the angles the spectacle cams use (reaction / ultimate orbit
    /// heights all round both players, the serve toss look-up, a high aerial) plus the opening flyover, rendered
    /// with the real game lighting. Output: ArtDir/score80/phaseE/<SCORE80_ENV_TAG>/.
    public class Score80EnvAuditTests
    {
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator RenderWideAngles()
        {
            string tag = System.Environment.GetEnvironmentVariable("SCORE80_ENV_TAG"); if (string.IsNullOrEmpty(tag)) tag = "after";
            string dir = Path.GetFullPath("../ArtDir/score80/phaseE/" + tag); Directory.CreateDirectory(dir);
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            int oldRate = Time.captureFramerate; Time.captureFramerate = 30;
            var cam = game.GameplayCamera ? game.GameplayCamera : Camera.main;
            // opening flyover: 8 frames across it
            float len = TennisPresentation.Length; int shots = 0;
            for (int f = 0; f < len * 30; f++)
            {
                yield return null;
                if (f % Mathf.Max(1, (int)(len * 30 / 8)) == 0) Save(cam, $"{dir}/flyover_{shots++:00}.png");
            }
            game.ManualSimulation = true;
            for (int f = 0; f < 5; f++) yield return null;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            var audit = new GameObject("Env audit cam").AddComponent<Camera>(); audit.enabled = false;
            audit.CopyFrom(cam); audit.fieldOfView = 44;
            Vector3 p = game.Player.transform.position, o = game.Opponent.transform.position;
            int k = 0;
            foreach (var (who, name) in new[] { (p, "player"), (o, "rival") })
                for (int yaw = 0; yaw < 360; yaw += 45)
                {
                    var d = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
                    audit.transform.position = who + d * 3.2f + Vector3.up * 1.1f; audit.transform.LookAt(who + Vector3.up * 1.0f);
                    Save(audit, $"{dir}/react_{name}_{yaw:000}.png"); k++;
                    audit.transform.position = who + d * 2.4f + Vector3.up * .45f; audit.transform.LookAt(who + Vector3.up * 1.4f);
                    Save(audit, $"{dir}/low_{name}_{yaw:000}.png"); k++;
                }
            foreach (var yaw in new[] { 0, 90, 180, 270 })
            {
                var d = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
                audit.transform.position = d * 34 + Vector3.up * 22; audit.transform.LookAt(Vector3.zero); audit.fieldOfView = 50;
                Save(audit, $"{dir}/aerial_{yaw:000}.png"); k++;
            }
            Debug.Log($"[Score80Env] flyover={shots} angles={k}");
            Object.Destroy(audit.gameObject); Time.captureFramerate = oldRate;
        }
        static void Save(Camera cam, string path)
        {
            var rt = RenderTexture.GetTemporary(1280, 720, 24); var prev = cam.targetTexture; cam.targetTexture = rt; cam.Render(); cam.targetTexture = prev;
            var t = new Texture2D(1280, 720, TextureFormat.RGB24, false); var a = RenderTexture.active; RenderTexture.active = rt; t.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); t.Apply(); RenderTexture.active = a;
            File.WriteAllBytes(path, t.EncodeToPNG()); Object.Destroy(t); RenderTexture.ReleaseTemporary(rt);
        }
    }
}

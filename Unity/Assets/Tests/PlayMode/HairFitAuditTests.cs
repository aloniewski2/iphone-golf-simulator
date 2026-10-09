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
    /// In-game hair fit: the gameplay hero with every haircut x headwear, boy and girl, deep skin + black hair, in
    /// Ready and mid-forehand, head shots from the side / 3/4 / back. Output: ArtDir/hero/v5_proof/hairfit_unity/.
    /// Also logs, per haircut, the gap between the hair and the scalp (hair verts vs the head bone) for the report.
    public class HairFitAuditTests
    {
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator EveryHaircutSitsOnTheHead()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation = true;
            for (int f = 0; f < 10; f++) yield return null;
            HeroTennisDriver hero = null;
            foreach (var d in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None)) if (d.isPlayer) hero = d;
            Assert.IsNotNull(hero);
            string dir = Path.GetFullPath("../ArtDir/hero/v5_proof/hairfit_unity"); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (!r.transform.IsChildOf(hero.transform)) r.enabled = false;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            hero.enabled = false;
            var cam = new GameObject("Fit cam").AddComponent<Camera>(); cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.93f, .62f, .48f, 1); cam.nearClipPlane = .05f;
            var rt = new RenderTexture(300, 300, 24); var tex = new Texture2D(300, 300, TextureFormat.RGB24, false); cam.targetTexture = rt;
            var head = hero.look.animator.GetBoneTransform(HumanBodyBones.Head);
            var kit = TennisLook.Kit.From("FFFFFF", "1E2A5A", "F28C28", "2E6BD6", 5);
            var log = new System.Text.StringBuilder();
            foreach (bool girl in new[] { false, true })
                for (int cut = 0; cut < HeroKit.HaircutNames.Length; cut++)
                    for (int hw = 0; hw < 4; hw++)
                    {
                        var st = HeroKit.Style.From(5, 0, hw, kit, cut, girl); st.SkinTint = HeroKit.Hex("3E2317"); st.HairTint = HeroKit.Hex("141010");
                        HeroKit.Apply(hero, st); yield return null;
                        foreach (var (clip, t, pose) in new[] { (HeroTennisDriver.Clip.Ready, .4f, "ready"), (HeroTennisDriver.Clip.Forehand, -1f, "fh") })
                        {
                            float tt = t >= 0 ? t : hero.ContactOf(clip);
                            hero.Sample(clip, tt); yield return null; hero.Sample(clip, tt);
                            if (pose == "ready" && hw == 0)
                            {
                                // how far the visible hair sits from the head bone vs the Swept baseline (a floating cut shows a big min distance)
                                foreach (var r in hero.look.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                                    if (r.enabled && r.name.StartsWith("Hair_"))
                                    {
                                        var m = new Mesh(); r.BakeMesh(m, true); float lo = float.MaxValue, hiY = float.MinValue; Vector3 mean = Vector3.zero;
                                        foreach (var v in m.vertices) { var w = r.transform.TransformPoint(v); lo = Mathf.Min(lo, (w - head.position).magnitude); mean += w; hiY = Mathf.Max(hiY, w.y); }
                                        mean /= Mathf.Max(1, m.vertexCount);
                                        log.AppendLine($"{(girl ? "girl" : "boy")} {HeroKit.HaircutNames[cut]} {r.name}: minDistToHeadBone={lo:F3} meanOffsetFromHead={(mean - head.position):F3} top={hiY - head.position.y:F3} bones={r.bones.Length} root={(r.rootBone ? r.rootBone.name : "-")}");
                                        Object.Destroy(m);
                                    }
                            }
                            foreach (var rr in hero.look.racketGrip.GetComponentsInChildren<Renderer>()) rr.enabled = false;
                            foreach (var (yaw, lab) in new[] { (35, "34"), (90, "side"), (170, "back") })
                            {
                                var hc = head.position + hero.transform.up * .06f;
                                var d = Quaternion.AngleAxis(yaw, Vector3.up) * hero.transform.forward;
                                cam.transform.position = hc + d * 1.4f + Vector3.up * .1f; cam.transform.LookAt(hc); cam.fieldOfView = 24; cam.Render();
                                var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 300, 300), 0, 0); tex.Apply(); RenderTexture.active = prev;
                                File.WriteAllBytes($"{dir}/{(girl ? "girl" : "boy")}_{HeroKit.HaircutNames[cut]}_{HeroKit.HeadwearNames[hw]}_{pose}_{lab}.png", tex.EncodeToPNG());
                            }
                            foreach (var rr in hero.look.racketGrip.GetComponentsInChildren<Renderer>()) rr.enabled = true;
                        }
                    }
            File.WriteAllText(dir + "/fit.txt", log.ToString());
        }
    }
}

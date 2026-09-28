using System;
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
    /// Score-80 Phase D proof: the gameplay hero, isolated on magenta, in the poses that open the head (ready, run,
    /// forehand / serve / smash contact, whiff) x every headwear x two hair colours, orbited at head height and from
    /// above / below, plus the customize framing (full body, 3/4). Images feed score80_holes.py, which counts
    /// background pixels ENCLOSED by the hero silhouette (see-through holes), per image.
    public class Score80WardrobeAuditTests
    {
        [UnityTest, Explicit, Timeout(1800000)]
        public IEnumerator OrbitHeadAndSlots()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation = true;
            for (int f = 0; f < 10; f++) yield return null;
            HeroTennisDriver hero = null;
            foreach (var d in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None)) if (d.isPlayer) hero = d;
            Assert.IsNotNull(hero);
            string dir = Path.GetFullPath("../ArtDir/score80/phaseD/orbit"); Directory.CreateDirectory(dir);
            foreach (var f in Directory.GetFiles(dir, "*.png")) File.Delete(f);
            // isolate the hero
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (!r.transform.IsChildOf(hero.transform)) r.enabled = false;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            hero.enabled = false;   // pose by Sample() only
            var cam = new GameObject("Audit cam").AddComponent<Camera>(); cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(1, 0, 1, 1); cam.nearClipPlane = .05f; cam.farClipPlane = 30;
            var rt = new RenderTexture(320, 320, 24) { antiAliasing = 1 }; var tex = new Texture2D(320, 320, TextureFormat.RGB24, false);
            var bodyRt = new RenderTexture(360, 540, 24) { antiAliasing = 1 }; var bodyTex = new Texture2D(360, 540, TextureFormat.RGB24, false);
            cam.targetTexture = rt;
            var head = hero.look.animator.GetBoneTransform(HumanBodyBones.Head);
            var kit = TennisLook.Kit.From("FFFFFF", "1E2A5A", "F28C28", "2E6BD6", 0);
            var poses = new (HeroTennisDriver.Clip clip, float t, string name)[]
            {
                (HeroTennisDriver.Clip.Ready, .4f, "ready"), (HeroTennisDriver.Clip.RunForward, .25f, "run"),
                (HeroTennisDriver.Clip.Forehand, -1, "fh"), (HeroTennisDriver.Clip.Serve, -1, "serve"),
                (HeroTennisDriver.Clip.Smash, -1, "smash"), (HeroTennisDriver.Clip.MissWhiff, .45f, "whiff"),
            };
            int n = 0;
            foreach (int hairC in new[] { 0, 3 })
                foreach (int hw in new[] { 0, 1, 2, 3 })
                {
                    HeroKit.Apply(hero, HeroKit.Style.From(0, hairC, hw, kit));
                    yield return null;
                    foreach (var p in poses)
                    {
                        float t = p.t >= 0 ? p.t : hero.ContactOf(p.clip);
                        hero.Sample(p.clip, t); yield return null; hero.Sample(p.clip, t);   // let a freshly equipped hat skin once
                        // racket (hoop + strings) off for the head orbit: its hoop encloses background by design
                        var racketRs = hero.look.racketGrip ? hero.look.racketGrip.GetComponentsInChildren<Renderer>() : new Renderer[0];
                        foreach (var rr in racketRs) rr.enabled = false;
                        foreach (float pitch in new[] { -12f, 10f, 35f })
                            for (int yaw = 0; yaw < 360; yaw += 30)
                            {
                                var hc = head.position + hero.transform.up * .08f;
                                var dirV = Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right) * Vector3.forward;
                                cam.transform.position = hc + dirV * 1.5f; cam.transform.LookAt(hc); cam.fieldOfView = 22;
                                cam.targetTexture = rt; cam.Render();
                                var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 320, 320), 0, 0); tex.Apply(); RenderTexture.active = prev;
                                File.WriteAllBytes($"{dir}/h{hairC}_w{hw}_{p.name}_p{pitch:+0;-0}_y{yaw:000}.png", tex.EncodeToPNG()); n++;
                            }
                        foreach (var rr in racketRs) rr.enabled = true;
                        // customize framing: full body, front 3/4 and back 3/4
                        foreach (int yaw in new[] { 30, 150, 210 })
                        {
                            var c0 = hero.transform.position + Vector3.up * .8f;
                            var dirV = Quaternion.AngleAxis(yaw, Vector3.up) * Vector3.forward;
                            cam.transform.position = c0 + dirV * 3.4f + Vector3.up * .2f; cam.transform.LookAt(c0); cam.fieldOfView = 30;
                            cam.targetTexture = bodyRt; cam.Render();
                            var prev = RenderTexture.active; RenderTexture.active = bodyRt; bodyTex.ReadPixels(new Rect(0, 0, 360, 540), 0, 0); bodyTex.Apply(); RenderTexture.active = prev;
                            File.WriteAllBytes($"{dir}/body_h{hairC}_w{hw}_{p.name}_y{yaw:000}.png", bodyTex.EncodeToPNG()); n++;
                        }
                    }
                }
            Debug.Log("[Score80Wardrobe] images=" + n);
            cam.targetTexture = null; Object.Destroy(cam.gameObject);
        }
    }
}

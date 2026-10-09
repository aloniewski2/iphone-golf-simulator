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
    /// Score-80 Phase D A/B: the head views that exposed hair gaps, rendered for the current build; run with
    /// HERO_HAIRFIX=0 for the untouched prefab materials. Output ArtDir/score80/phaseD/ab_<SCORE80_AB_TAG>/.
    public class Score80HairABTests
    {
        [UnityTest, Explicit, Timeout(600000)]
        public IEnumerator HeadViews()
        {
            string tag = System.Environment.GetEnvironmentVariable("SCORE80_AB_TAG"); if (string.IsNullOrEmpty(tag)) tag = "fix";
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation = true;
            for (int f = 0; f < 5; f++) yield return null;
            HeroTennisDriver hero = null;
            foreach (var d in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None)) if (d.isPlayer) hero = d;
            string dir = Path.GetFullPath("../ArtDir/score80/phaseD/ab_" + tag); Directory.CreateDirectory(dir);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (!r.transform.IsChildOf(hero.transform)) r.enabled = false;
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false;
            hero.enabled = false;
            var cam = new GameObject("AB cam").AddComponent<Camera>(); cam.enabled = false; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(1, 0, 1, 1); cam.nearClipPlane = .05f;
            var rt = new RenderTexture(400, 400, 24); cam.targetTexture = rt; var tex = new Texture2D(400, 400, TextureFormat.RGB24, false);
            var head = hero.look.animator.GetBoneTransform(HumanBodyBones.Head);
            var kit = TennisLook.Kit.From("FFFFFF", "1E2A5A", "F28C28", "2E6BD6", 0);
            var log = new System.Text.StringBuilder();
            foreach (int hw in new[] { 0, 1, 3 })
            {
                HeroKit.Apply(hero, HeroKit.Style.From(0, 0, hw, kit)); yield return null;
                var liner = head.Find("HatLiner");
                log.Append($"hw={hw} liner={(liner ? liner.gameObject.activeInHierarchy + " verts=" + liner.GetComponent<MeshFilter>().sharedMesh.vertexCount + " mat=" + liner.GetComponent<MeshRenderer>().sharedMaterial.name + " cull=" + liner.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_Cull") : "none")}\n");
                foreach (var r in hero.look.GetComponentsInChildren<Renderer>(true))
                    if (r.name.StartsWith("Hair") || r.name == "Body_Skin" || r.name.StartsWith("Hat"))
                        log.Append($"   {r.name} enabled={r.enabled} mesh={(r is SkinnedMeshRenderer s ? s.sharedMesh.name : "")} mats={string.Join("|", System.Array.ConvertAll(r.sharedMaterials, x => x ? x.name + "(cull " + (x.HasProperty("_Cull") ? x.GetFloat("_Cull") : -1) + ")" : "null"))}\n");
                foreach (var (clip, name) in new[] { (HeroTennisDriver.Clip.Ready, "ready"), (HeroTennisDriver.Clip.Serve, "serve") })
                {
                    hero.Sample(clip, clip == HeroTennisDriver.Clip.Ready ? .4f : hero.ContactOf(clip)); yield return null;   // let a freshly equipped hat skin once
                    hero.Sample(clip, clip == HeroTennisDriver.Clip.Ready ? .4f : hero.ContactOf(clip));
                    foreach (var (pitch, yaw) in new[] { (10f, 180), (35f, 300), (-12f, 180), (10f, 90) })
                    {
                        var hc = head.position + hero.transform.up * .08f;
                        cam.transform.position = hc + Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right) * Vector3.forward * 1.3f; cam.transform.LookAt(hc); cam.fieldOfView = 24;
                        cam.Render(); var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 400, 400), 0, 0); tex.Apply(); RenderTexture.active = prev;
                        File.WriteAllBytes($"{dir}/w{hw}_{name}_p{pitch:+0;-0}_y{yaw:000}.png", tex.EncodeToPNG());
                    }
                }
            }
            File.WriteAllText(dir + "/materials.txt", log.ToString());
        }
    }
}

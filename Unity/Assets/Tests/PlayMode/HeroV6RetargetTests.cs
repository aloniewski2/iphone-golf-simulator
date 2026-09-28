#if UNITY_EDITOR
using System.Collections;
using System.IO;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// Plays the approved Humanoid tennis clips on the current hero AND on the generated V6 hero, side by side, same
    /// camera, same lights: ArtDir/review/v6_retarget/<clip>_<t>.png (left = current, right = generated).
    public class HeroV6RetargetTests
    {
        [UnityTest, Explicit, Timeout(900000)]
        public IEnumerator ClipsPlayOnTheGeneratedHero()
        {
            var old = Object.Instantiate(Resources.Load<GameObject>(TennisHeroSetup.PrefabPath), new Vector3(-.75f, 0, 0), Quaternion.identity);
            var drv = old.GetComponent<HeroTennisDriver>(); drv.enabled = false;
            foreach (var mb in old.GetComponentsInChildren<MonoBehaviour>()) mb.enabled = false;
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArtDirection/HeroV6/Hero_V6.fbx");
            Assert.IsNotNull(src, "V6 model");
            var v6 = Object.Instantiate(src, new Vector3(.75f, 0, 0), Quaternion.identity);
            var a6 = v6.GetComponent<Animator>() ?? v6.AddComponent<Animator>();
            a6.avatar = AssetDatabase.LoadAllAssetsAtPath("Assets/ArtDirection/HeroV6/Hero_V6.fbx").OfTypeAvatar();
            Assert.IsTrue(a6.avatar && a6.avatar.isHuman, "V6 humanoid avatar");
            var aOld = old.GetComponentInChildren<Animator>();
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/ArtDirection/HeroV6/Hero_V6.mat");
            foreach (var r in v6.GetComponentsInChildren<SkinnedMeshRenderer>()) { r.sharedMaterial = mat; r.updateWhenOffscreen = true; }
            // scale V6 to the current hero's height
            yield return null;
            float hOld = Height(old), hNew = Height(v6); v6.transform.localScale *= hOld / Mathf.Max(.01f, hNew);
            var cam = new GameObject("cam").AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.93f, .62f, .48f); cam.fieldOfView = 30;
            var sun = new GameObject("sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.transform.rotation = Quaternion.Euler(40, 150, 0); sun.intensity = 1.6f;
            RenderSettings.ambientLight = new Color(.55f, .55f, .6f);
            var rt = new RenderTexture(960, 540, 24); var tex = new Texture2D(960, 540, TextureFormat.RGB24, false); cam.targetTexture = rt;
            string dir = Path.GetFullPath("../ArtDir/review/v6_retarget"); Directory.CreateDirectory(dir);
            var gOld = PlayableGraph.Create("old"); var gNew = PlayableGraph.Create("new");
            var oOld = AnimationPlayableOutput.Create(gOld, "o", aOld); var oNew = AnimationPlayableOutput.Create(gNew, "o", a6);
            // racket: the current hero's racket, moved onto the V6 right hand with the same world grip in Ready
            Transform racket = null;
            foreach (var s in drv.slots) if (s.id == HeroTennisDriver.Clip.Ready && s.clip)
            {
                var p0 = AnimationClipPlayable.Create(gOld, s.clip); var p1 = AnimationClipPlayable.Create(gNew, s.clip);
                oOld.SetSourcePlayable(p0); oNew.SetSourcePlayable(p1); p0.SetTime(.4f); p1.SetTime(.4f); gOld.Evaluate(0); gNew.Evaluate(0);
                var hO = aOld.GetBoneTransform(HumanBodyBones.RightHand); var hN = a6.GetBoneTransform(HumanBodyBones.RightHand);
                var grip = old.GetComponent<ModularHeroLook>().racketGrip;
                racket = Object.Instantiate(grip.gameObject).transform; racket.name = "V6 racket";
                racket.SetPositionAndRotation(hN.position + (grip.position - hO.position), hN.rotation * (Quaternion.Inverse(hO.rotation) * grip.rotation));
                racket.localScale = grip.lossyScale; racket.SetParent(hN, true);
                p0.Destroy(); p1.Destroy();
            }
            // fluidity: the swings side by side at 30 fps (frames -> ArtDir/review/v6_retarget/video/)
            string vdir = dir + "/video"; Directory.CreateDirectory(vdir); int vf = 0;
            foreach (var id in new[] { HeroTennisDriver.Clip.Ready, HeroTennisDriver.Clip.Forehand, HeroTennisDriver.Clip.Backhand, HeroTennisDriver.Clip.Serve, HeroTennisDriver.Clip.RunForward, HeroTennisDriver.Clip.RunRight, HeroTennisDriver.Clip.Smash, HeroTennisDriver.Clip.CelebratePoint, HeroTennisDriver.Clip.MissWhiff })
                foreach (var s in drv.slots) if (s.id == id && s.clip)
                {
                    var p0 = AnimationClipPlayable.Create(gOld, s.clip); var p1 = AnimationClipPlayable.Create(gNew, s.clip);
                    oOld.SetSourcePlayable(p0); oNew.SetSourcePlayable(p1);
                    int n = Mathf.Clamp(Mathf.RoundToInt(s.clip.length * 30), 15, 120);
                    for (int k = 0; k < n; k++)
                    {
                        double t = k / 30.0; p0.SetTime(t); p1.SetTime(t); gOld.Evaluate(0); gNew.Evaluate(0);
                        var mid = (old.transform.position + v6.transform.position) / 2 + Vector3.up * .7f;
                        cam.transform.position = mid + new Vector3(0, .5f, 5.4f); cam.transform.LookAt(mid); cam.Render();
                        var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = prev;
                        File.WriteAllBytes($"{vdir}/v{vf++:0000}.png", tex.EncodeToPNG());
                    }
                    p0.Destroy(); p1.Destroy();
                }
            foreach (var s in drv.slots)
            {
                if (!s.clip) continue;
                var pOld = AnimationClipPlayable.Create(gOld, s.clip); var pNew = AnimationClipPlayable.Create(gNew, s.clip);
                oOld.SetSourcePlayable(pOld); oNew.SetSourcePlayable(pNew);
                foreach (float f in new[] { .15f, .35f, .5f, .65f, .85f })
                {
                    double t = f * s.clip.length; pOld.SetTime(t); pNew.SetTime(t); gOld.Evaluate(0); gNew.Evaluate(0);
                    yield return null;
                    var mid = (old.transform.position + v6.transform.position) / 2 + Vector3.up * hOld * .55f;
                    foreach (var (yaw, lab) in new[] { (0f, "front"), (70f, "side") })
                    {
                        var d = Quaternion.Euler(8, yaw + 180, 0) * Vector3.back;
                        cam.transform.position = mid - d * 5.2f; cam.transform.LookAt(mid); cam.Render();
                        var prev = RenderTexture.active; RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply(); RenderTexture.active = prev;
                        File.WriteAllBytes($"{dir}/{s.id}_{f:0.00}_{lab}.png", tex.EncodeToPNG());
                    }
                }
                pOld.Destroy(); pNew.Destroy();
            }
            gOld.Destroy(); gNew.Destroy();
        }
        static float Height(GameObject g)
        {
            var b = new Bounds(g.transform.position, Vector3.zero); bool any = false;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!r.enabled) continue; if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds); }
            return b.size.y;
        }
    }
    static class AvatarExt
    {
        public static Avatar OfTypeAvatar(this Object[] all) { foreach (var o in all) if (o is Avatar a) return a; return null; }
    }
}
#endif

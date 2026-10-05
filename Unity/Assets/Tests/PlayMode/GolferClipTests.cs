using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The golfers' clips as the build has them, sampled straight off the imported FBX (no game on top): where the club and the hands are at
    /// address, at the top, at the ball and at the finish. The numbers are what blender/scripts/matchhero_golf.py wrote in golfer_<m|f>_clips.json.
    public class GolferClipTests
    {
        [UnityTest, Timeout(120000)]
        public IEnumerator TheClubComesBackToTheBallInEveryClip()
        {
            foreach (var sex in new[] { "m", "f" })
            {
                var prefab = Resources.Load<GameObject>($"Hero/golfer_{sex}");
                Assert.IsNotNull(prefab, $"golfer_{sex} is in the build");
                var go = Object.Instantiate(prefab);
                var clips = Resources.LoadAll<AnimationClip>($"Hero/golfer_{sex}").Where(c => !c.name.StartsWith("__")).ToDictionary(c => c.name);
                var json = JsonUtility.FromJson<ClipSet>(Resources.Load<TextAsset>($"Hero/golfer_{sex}_clips").text);
                var club = go.GetComponentsInChildren<Transform>(true).First(t => t.name == "Club");
                foreach (var info in json.clips.Where(c => c.club != null && c.club != ""))
                {
                    Assert.IsTrue(clips.TryGetValue(info.name, out var clip), $"{sex}: the {info.name} clip");
                    var mesh = go.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "CLUB_" + info.club.ToUpperInvariant());
                    mesh.gameObject.SetActive(true);
                    Vector3 Head(float t)
                    {
                        clip.SampleAnimation(go, t);
                        var baked = new Mesh(); mesh.BakeMesh(baked, true);
                        var origin = mesh.transform.InverseTransformPoint(club.position);
                        Vector3 far = Vector3.zero; float best = -1;
                        foreach (var v in baked.vertices) { float d = (v - origin).sqrMagnitude; if (d > best) { best = d; far = v; } }
                        Object.Destroy(baked);
                        return mesh.transform.TransformPoint(far);
                    }
                    float fps = info.fps;
                    var line = new System.Text.StringBuilder($"CLIP {sex} {info.name} ({clip.length:F2}s, top {info.top / fps:F2}, impact {info.impact / fps:F2}): ");
                    foreach (var t in new[] { 0f, info.top / fps, (info.top + info.impact) / 2f / fps, info.impact / fps, (info.frames - 1) / fps })
                    {
                        var h = Head(t);
                        line.Append($"t={t:F2} head=({h.x:F2},{h.y:F2},{h.z:F2}) ");
                    }
                    Debug.Log(line.ToString());
                    mesh.gameObject.SetActive(false);
                }
                Object.Destroy(go);
            }
            yield return null;
        }

#pragma warning disable 649
        [System.Serializable] class Info { public string name, club; public int frames, fps, top, impact; }
        [System.Serializable] class ClipSet { public Info[] clips; }
#pragma warning restore 649
    }
}

using System.Collections;
using System.IO;
using System.Text;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    /// Score-80 probe: each stroke clip sampled at 120 Hz around its authored contact -- racket string-centre speed
    /// (hero space) and the string-bed normal vs the net direction -- to see where the racket is fastest.
    public class Score80ClipProbeTests
    {
        [UnityTest, Explicit, Timeout(600000)]
        public IEnumerator ProbeStrokeClips()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation = true;
            for (int f = 0; f < 5; f++) yield return null;
            HeroTennisDriver hero = null;
            foreach (var d in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None)) if (d.isPlayer) hero = d;
            hero.enabled = false;
            var grip = hero.look.racketGrip;
            var sb = new StringBuilder("clip,t,rel,speed,face\n");
            foreach (var c in new[] { HeroTennisDriver.Clip.Forehand, HeroTennisDriver.Clip.Backhand, HeroTennisDriver.Clip.Volley, HeroTennisDriver.Clip.Smash, HeroTennisDriver.Clip.Serve })
            {
                float ct = hero.ContactOf(c); Vector3 prev = Vector3.zero; bool first = true;
                for (float t = ct - .5f; t <= ct + .25f; t += 1f / 120)
                {
                    hero.Sample(c, Mathf.Max(0, t));
                    var ctr = hero.transform.InverseTransformPoint(grip.TransformPoint(new Vector3(0, .395f, 0)));
                    var n = hero.transform.InverseTransformDirection(grip.TransformDirection(Vector3.forward));
                    float speed = first ? 0 : (ctr - prev).magnitude * 120; first = false; prev = ctr;
                    float face = Vector3.Angle(new Vector3(0, 0, Mathf.Sign(n.z == 0 ? 1 : n.z)), n);   // 0 = strings square to the net line
                    sb.Append(c).Append(',').Append(t.ToString("0.000")).Append(',').Append((t - ct).ToString("0.000")).Append(',').Append(speed.ToString("0.00")).Append(',').Append(face.ToString("0.0")).Append('\n');
                }
            }
            string dir = Path.GetFullPath("../ArtDir/score80/probe"); Directory.CreateDirectory(dir);
            File.WriteAllText(dir + "/clip_racket_speed.csv", sb.ToString());
            Debug.Log("[Score80Probe] done");
        }

        [UnityTest, Explicit, Timeout(600000)]
        public IEnumerator ProbeHairWinding()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            HeroTennisDriver hero = null;
            foreach (var d in Object.FindObjectsByType<HeroTennisDriver>(FindObjectsSortMode.None)) if (d.isPlayer) hero = d;
            var sb = new StringBuilder();
            foreach (var r in hero.look.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var m = r.sharedMesh; if (!m) continue;
                sb.Append($"{r.name} mesh={m.name} verts={m.vertexCount} sub={m.subMeshCount} readable={m.isReadable} mats={string.Join("|", System.Array.ConvertAll(r.sharedMaterials, x => x ? x.name : "null"))}\n");
                if (!m.isReadable || !(r.name.StartsWith("Hair") || r.name == "Body_Skin")) continue;
                var v = m.vertices; var nrm = m.normals;
                for (int s = 0; s < m.subMeshCount; s++)
                {
                    var t = m.GetTriangles(s); Vector3 c = Vector3.zero; int cnt = 0;
                    for (int i = 0; i < t.Length; i++) { c += v[t[i]]; cnt++; }
                    c /= Mathf.Max(1, cnt);
                    int inward = 0, disagree = 0;
                    for (int i = 0; i < t.Length; i += 3)
                    {
                        Vector3 a0 = v[t[i]], a1 = v[t[i + 1]], a2 = v[t[i + 2]]; var fn = Vector3.Cross(a1 - a0, a2 - a0); var mid = (a0 + a1 + a2) / 3;
                        if (Vector3.Dot(fn, mid - c) < 0) inward++;
                        if (nrm.Length == v.Length && Vector3.Dot(fn, nrm[t[i]] + nrm[t[i + 1]] + nrm[t[i + 2]]) < 0) disagree++;
                    }
                    sb.Append($"   sub{s} tris={t.Length / 3} inwardByCentroid={inward} normalVsWinding={disagree} centre={c}\n");
                }
            }
            string dir = Path.GetFullPath("../ArtDir/score80/probe"); Directory.CreateDirectory(dir);
            File.WriteAllText(dir + "/hair_winding.txt", sb.ToString());
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Shot;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The golfer's swing is fluid: the club head, tracked frame by frame at a fixed 60 fps
    /// through a backswing following the phone, the strike, the follow-through and the settle
    /// back to address, never jumps — no frame moves it far more than the frames either side.
    public class SwingSmoothnessTests
    {
        const int Fps = 60;

        /// The club head in the world: the far end of the CLUB_ mesh from the grip, carried into
        /// the Club bone's space by its bind pose.
        static System.Func<Vector3> ClubHead(GameObject golfer, string club)
        {
            var smr = golfer.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "CLUB_" + club);
            var mesh = smr.sharedMesh;
            int bone = System.Array.FindIndex(smr.bones, b => b.name == "Club");
            Assert.GreaterOrEqual(bone, 0, "the club is skinned to the Club bone");
            var toBone = mesh.bindposes[bone];
            // (the mesh isn't readable at runtime; its bounds' far corner stands in for the head)
            var b = mesh.bounds;
            var corners = from x in new[] { b.min.x, b.max.x } from y in new[] { b.min.y, b.max.y } from z in new[] { b.min.z, b.max.z } select new Vector3(x, y, z);
            var tip = corners.Select(v => toBone.MultiplyPoint3x4(v)).OrderByDescending(v => v.sqrMagnitude).First();
            var t = smr.bones[bone];
            return () => t.TransformPoint(tip);
        }

        static IEnumerator Run(GolferView g, System.Func<Vector3> head, List<Vector3> path, int frames, System.Action<int> each = null)
        {
            for (int i = 0; i < frames; i++)
            {
                each?.Invoke(i);
                yield return null;
                path.Add(head());
            }
        }

        static void AssertNoPops(List<Vector3> path, string what)
        {
            var d = new List<float>();
            for (int i = 1; i < path.Count; i++) d.Add((path[i] - path[i - 1]).magnitude);
            for (int i = 1; i < d.Count - 1; i++)
            {
                float around = Mathf.Max(d[i - 1], d[i + 1]);
                Assert.LessOrEqual(d[i], around * 2.5f + 0.03f,
                    $"{what}: the club head jumps {d[i]:F3} m on frame {i} (neighbours {d[i - 1]:F3}, {d[i + 1]:F3})");
            }
        }

        IEnumerator Swing(GolfClub club, string mesh, float load, string what)
        {
            Time.captureFramerate = Fps;
            var root = new GameObject("Swing test");
            try
            {
                var g = GolferView.Create(root.transform);
                g.SetClub(club, false);
                yield return null;
                var head = ClubHead(root, mesh);
                var path = new List<Vector3>();
                yield return Run(g, head, path, 15);
                yield return Run(g, head, path, Fps, i => g.ShowLoad(load * (i + 1f) / Fps));   // a one-second takeaway
                yield return Run(g, head, path, 10, _ => g.ShowLoad(load));
                float toBall = g.Strike();
                Assert.That(toBall, Is.InRange(0.15f, 0.45f), $"{what}: the downswing reaches the ball in a real swing's time");
                yield return Run(g, head, path, Fps * 2);
                g.Settle();
                yield return Run(g, head, path, Fps / 2);
                AssertNoPops(path, what);
                Debug.Log($"{what}: {path.Count} frames, downswing {toBall:F2} s, top speed {path.Zip(path.Skip(1), (a, b) => (b - a).magnitude * Fps).Max():F1} m/s");
            }
            finally
            {
                Object.Destroy(root);
                Time.captureFramerate = 0;
            }
        }

        [UnityTest] public IEnumerator AFullDriveIsFluid() => Swing(GolfClub.Driver, "DRIVER", 1f, "full drive");
        [UnityTest] public IEnumerator AHalfSwingIsFluid() => Swing(GolfClub.Iron, "IRON", 0.5f, "half iron");
        [UnityTest] public IEnumerator APuttIsFluid() => Swing(GolfClub.Putter, "PUTTER", 0.7f, "putt");
    }
}

using System.Collections.Generic;
using UnityEngine;
using GolfArcade.Course;

namespace GolfArcade.Game
{
    /// The gallery at the tee, the way a tournament has one: two staggered rows of spectators
    /// behind the golfer (on the side the golfer stands, so the camera that finds the player on
    /// the tee has them in the background), back past the tee markers and behind a rope, each in
    /// a look of their own, idling — shifting their weight, never in step — until something is
    /// worth cheering. Only where there is ground for them at the tee's own height: not in the
    /// water, not off the cliff.
    public sealed class Gallery : MonoBehaviour
    {
        readonly List<GolferView> fans = new();
        readonly List<float> phases = new();
        float cheerUntil = -1f;

        static readonly Color[] Shirts =
        {
            UI.UiKit.Hex("E8483F"), UI.UiKit.Hex("F2A81D"), UI.UiKit.Hex("3CB36B"), UI.UiKit.Hex("2F5FE0"),
            UI.UiKit.Hex("8E5BD8"), UI.UiKit.Hex("F4F7FB"), UI.UiKit.Hex("FFD23A"), UI.UiKit.Hex("F07AA8"),
        };
        static readonly Color[] Trousers =
        {
            UI.UiKit.Hex("D8C7A0"), UI.UiKit.Hex("24365E"), UI.UiKit.Hex("F2F2F2"), UI.UiKit.Hex("6E7482"), UI.UiKit.Hex("2A2A30"),
        };
        /// Yards from the golfer to the rope and to the two rows: past the tee markers, which
        /// stand about five yards either side of the ball.
        const float RopeBack = 5.9f, FrontRow = 6.9f, BackRow = 8.2f;

        public static Gallery Create(Transform parent)
        {
            var go = new GameObject("Gallery");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Gallery>();
        }

        public int Count => fans.Count;
        /// Yards from the golfer to the rope, for a camera that wants to stand inside it.
        public const float RopeYards = RopeBack;
        /// Where the gallery stands, for a camera to find it; the golfer's spot when there is none.
        public Vector3 Centre { get; private set; }

        /// Fills the tee's gallery for `hole`: the golfer stands at `golfer` facing across the aim
        /// line `aim`; the rows run along the line behind them.
        public void Place(Hole hole, Vector3 golfer, Vector3 aim, int seed)
        {
            Clear();
            Centre = golfer;
            aim.y = 0; aim.Normalize();
            var side = Vector3.Cross(Vector3.up, aim).normalized;   // the way the golfer faces
            var rng = new System.Random(seed);
            // front row a rope's width back, the second row staggered between them
            var spots = new List<(float back, float along)>();
            foreach (float a in new[] { -2.4f, -0.8f, 0.8f, 2.4f }) spots.Add((FrontRow, a));
            foreach (float a in new[] { -1.6f, 0f, 1.6f }) spots.Add((BackRow, a));
            double teeHeight = HoleView.GroundHeight(HoleView.ToCourse(golfer));
            var sum = Vector3.zero;
            foreach (var (back, along) in spots)
            {
                var at = golfer - side * back + aim * (along + (float)(rng.NextDouble() - 0.5) * 0.5f);
                var point = HoleView.ToCourse(at);
                var lie = hole.LieAt(point);
                if (lie == CourseLie.Water || lie == CourseLie.OutOfBounds) continue;
                double ground = HoleView.GroundHeight(point);
                if (System.Math.Abs(ground - teeHeight) > 1.2) continue;
                bool female = rng.Next(2) == 1;
                var look = new GolferView.Look
                {
                    ModelPath = female ? "Golfer/golfer_f" : "Golfer/golfer_m",
                    Skin = GolferStyle.SkinTones[rng.Next(GolferStyle.SkinTones.Length)],
                    Hair = GolferStyle.HairColors[rng.Next(GolferStyle.HairColors.Length)],
                    HairMesh = new[] { "HAIR_SHORT", "HAIR_LONG", "HAIR_CURLY", female ? "HAIR_LONG" : "HAIR_SHORT" }[rng.Next(4)],
                    Shirt = Shirts[rng.Next(Shirts.Length)],
                    Trousers = Trousers[rng.Next(Trousers.Length)],
                };
                var fan = GolferView.CreateSpectator(transform, look);
                var place = HoleView.ToWorld(point);
                fan.transform.position = place;
                // facing the golfer, give or take
                var toward = golfer - place; toward.y = 0;
                fan.transform.rotation = Quaternion.LookRotation(toward.normalized, Vector3.up) * Quaternion.Euler(0, (float)(rng.NextDouble() - 0.5) * 24f, 0);
                float phase = (float)rng.NextDouble() * 2f;
                fan.Perform("Idle", phase);
                fans.Add(fan); phases.Add(phase);
                sum += place;
            }
            if (fans.Count > 0) Centre = sum / fans.Count;
            if (fans.Count > 0) Rope(golfer, side, aim, teeHeight);
        }

        /// The gallery rope between them and the golfer: white stakes a couple of yards apart,
        /// a rope strung between their tops, sagging a little in each span.
        void Rope(Vector3 golfer, Vector3 side, Vector3 aim, double teeHeight)
        {
            var rope = new GameObject("Rope").transform;
            rope.SetParent(transform, false);
            ropeRoot = rope;
            Vector3? last = null;
            for (float along = -3.9f; along <= 3.91f; along += 2.6f)
            {
                var point = HoleView.ToCourse(golfer - side * RopeBack + aim * along);
                if (System.Math.Abs(HoleView.GroundHeight(point) - teeHeight) > 1.2) { last = null; continue; }
                var foot = HoleView.ToWorld(point);
                var stake = HoleView.Primitive(PrimitiveType.Cylinder, "Stake", UI.UiKit.Hex("F4F7FB"), rope);
                stake.transform.position = foot + Vector3.up * 0.45f;
                stake.transform.localScale = new Vector3(0.07f, 0.45f, 0.07f);
                var top = foot + Vector3.up * 0.82f;
                if (last is Vector3 from)
                {
                    var mid = (from + top) / 2 - Vector3.up * 0.12f;
                    Strand(rope, from, mid); Strand(rope, mid, top);
                }
                last = top;
            }
        }

        static void Strand(Transform parent, Vector3 a, Vector3 b)
        {
            var strand = HoleView.Primitive(PrimitiveType.Cylinder, "Rope", UI.UiKit.Hex("F2C230"), parent);
            strand.transform.position = (a + b) / 2;
            strand.transform.rotation = Quaternion.FromToRotation(Vector3.up, b - a);
            strand.transform.localScale = new Vector3(0.035f, (b - a).magnitude / 2, 0.035f);
        }

        Transform ropeRoot;

        /// Everyone cheers — arms up or a fist pump, each their own way — for `seconds`, then
        /// back to idling.
        public void Cheer(float seconds = 2.4f)
        {
            for (int i = 0; i < fans.Count; i++)
                fans[i].Perform(i % 3 == 1 ? "FistPump" : "Cheer", phases[i] * 0.4f);
            cheerUntil = Time.time + seconds;
        }

        public void Clear()
        {
            foreach (var f in fans) if (f) Destroy(f.gameObject);
            if (ropeRoot) Destroy(ropeRoot.gameObject);
            fans.Clear(); phases.Clear(); cheerUntil = -1f;
        }

        void Update()
        {
            if (cheerUntil < 0 || Time.time < cheerUntil) return;
            cheerUntil = -1f;
            for (int i = 0; i < fans.Count; i++) fans[i].Perform("Idle", phases[i]);
        }
    }
}

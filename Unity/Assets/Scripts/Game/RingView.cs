using System.Collections.Generic;
using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The golden rings over a hole, drawn: a chunky toy hoop standing in the air facing the tee,
    /// a coin spinning in its middle — gold for a gold ring, turquoise for the glide ring that
    /// slides to and fro, bands of every colour for the rainbow one. Posed each frame from the
    /// movers' clock (CourseMovers.Now), so a sliding ring is where the ball will meet it. Flown
    /// through, it bursts outward and is gone (Take). Lives under the hole, and goes with it.
    public sealed class RingView : MonoBehaviour
    {
        CourseMovers movers;
        /// No balloon, cart or gull on the hole to keep the clock running: the rings do.
        bool drivesClock;
        bool paused;
        readonly List<Transform> rings = new();
        readonly List<Transform> coins = new();
        readonly Dictionary<int, float> takenAt = new();

        public static readonly Color Gold = new(1f, 0.79f, 0.2f), Glide = new(0.2f, 0.82f, 0.9f);
        static readonly Color[] Rainbow =
        {
            new(0.95f, 0.26f, 0.3f), new(1f, 0.58f, 0.2f), new(1f, 0.86f, 0.25f),
            new(0.36f, 0.85f, 0.42f), new(0.25f, 0.62f, 0.98f), new(0.6f, 0.4f, 0.95f),
        };

        public static RingView Build(Transform parent, CourseMovers movers, bool drivesClock)
        {
            var go = new GameObject("Rings");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<RingView>();
            view.movers = movers; view.drivesClock = drivesClock;
            view.Refresh();
            return view;
        }

        /// Draw any rings the hole has that aren't drawn yet (the rainbow one, when it comes).
        public void Refresh()
        {
            var all = movers?.Rings;
            if (all == null) return;
            for (int i = rings.Count; i < all.Length; i++)
            {
                var r = all[i];
                var root = new GameObject($"{r.Kind} ring {i}").transform;
                root.SetParent(transform, false);
                float radius = (float)r.Radius;
                if (r.Kind == RingKind.Rainbow)
                {
                    // the tube in six bands, outside in
                    for (int b = 0; b < Rainbow.Length; b++)
                        Hoop(root, radius + 0.55f - b * 0.2f, 0.15f, Rainbow[b]);
                }
                else Hoop(root, radius + 0.25f, 0.46f, r.Kind == RingKind.Glide ? Glide : Gold);
                // the coin in the middle, standing up and spinning
                var coin = HoleView.Primitive(PrimitiveType.Cylinder, "Coin", Gold, root);
                coin.GetComponent<Renderer>().sharedMaterial = MoverView.Toy(Gold);
                coin.transform.localScale = new Vector3(1.7f, 0.08f, 1.7f);
                var spin = new GameObject("Spin").transform; spin.SetParent(root, false);
                coin.transform.SetParent(spin, false); coin.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var face = HoleView.Primitive(PrimitiveType.Cylinder, "Face", new Color(1f, 0.92f, 0.5f), coin.transform);
                face.GetComponent<Renderer>().sharedMaterial = MoverView.Toy(new Color(1f, 0.92f, 0.55f));
                face.transform.localScale = new Vector3(0.66f, 1.25f, 0.66f);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = BallLook.OverlayLayer;
                foreach (var rend in root.GetComponentsInChildren<Renderer>(true)) { rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
                rings.Add(root); coins.Add(spin);
            }
            Pose();
        }

        /// A toy hoop of `radius` (to the middle of its tube) and `tube`, in the ring's plane (facing +Z).
        static void Hoop(Transform parent, float radius, float tube, Color colour)
        {
            var go = new GameObject("Hoop");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Torus(radius, tube);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MoverView.Toy(colour);
        }

        static readonly Dictionary<(float, float), Mesh> tori = new();
        static Mesh Torus(float radius, float tube)
        {
            if (tori.TryGetValue((radius, tube), out var cached) && cached) return cached;
            const int around = 56, sides = 14;
            var verts = new Vector3[(around + 1) * (sides + 1)];
            var normals = new Vector3[verts.Length];
            var tris = new int[around * sides * 6];
            for (int i = 0; i <= around; i++)
            {
                float a = i * Mathf.PI * 2 / around;
                var centre = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int j = 0; j <= sides; j++)
                {
                    float b = j * Mathf.PI * 2 / sides;
                    var n = centre * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    verts[i * (sides + 1) + j] = centre * radius + n * tube;
                    normals[i * (sides + 1) + j] = n;
                }
            }
            int k = 0;
            for (int i = 0; i < around; i++)
                for (int j = 0; j < sides; j++)
                {
                    int a0 = i * (sides + 1) + j, a1 = a0 + 1, b0 = a0 + sides + 1, b1 = b0 + 1;
                    // (wound so the outside faces out: Unity's front faces are clockwise)
                    tris[k++] = a0; tris[k++] = b0; tris[k++] = a1;
                    tris[k++] = b0; tris[k++] = b1; tris[k++] = a1;
                }
            var mesh = new Mesh { name = $"Hoop {radius:F2}", vertices = verts, normals = normals, triangles = tris };
            mesh.RecalculateBounds();
            return tori[(radius, tube)] = mesh;
        }

        public void SetPaused(bool on) => paused = on;

        /// Ring `index` flown through: it bursts outward and is gone.
        public void Take(int index) { if (index >= 0 && index < rings.Count && !takenAt.ContainsKey(index)) takenAt[index] = Time.time; }

        public bool Taken(int index) => takenAt.ContainsKey(index);

        void Update()
        {
            if (movers == null) return;
            if (drivesClock && !paused) movers.Advance(Time.deltaTime);
            Pose();
        }

        void Pose()
        {
            var all = movers?.Rings;
            if (all == null) return;
            double t = movers.Now;
            for (int i = 0; i < rings.Count && i < all.Length; i++)
            {
                var root = rings[i];
                if (!root) continue;
                var r = all[i];
                var (x, y, d) = r.At(t);
                root.position = new Vector3((float)x, (float)y, (float)d);
                root.rotation = Quaternion.LookRotation(new Vector3((float)r.NormalX, 0, (float)r.NormalD), Vector3.up);
                coins[i].localRotation = Quaternion.Euler(0, Time.time * 140f + i * 50f, 0);
                if (takenAt.TryGetValue(i, out var at))
                {
                    // out it bursts, spinning the coin away, and gone
                    float u = (Time.time - at) / 0.45f;
                    if (u >= 1) { if (root.gameObject.activeSelf) root.gameObject.SetActive(false); continue; }
                    root.localScale = Vector3.one * (1 + 0.7f * u) * (1 - u * u);
                    coins[i].localPosition = Vector3.up * u * 2.5f;
                    coins[i].localRotation = Quaternion.Euler(0, Time.time * 900f, 0);
                }
                else
                {
                    // a slow breathing pulse so it catches the eye
                    root.localScale = Vector3.one * (1 + 0.04f * Mathf.Sin(Time.time * 3f + i));
                }
            }
        }
    }
}

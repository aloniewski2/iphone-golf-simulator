using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Hub
{
    /// A little life for the country-club plaza, all of it cheap: the wind that sways the trees and roses (the global _HubWind
    /// GolfArcade/HubSurface reads), gulls circling high over the island and the sport grounds, and flags that flutter (a few
    /// dozen vertices moved per frame). Lives beside the static-batched world (under HubWorld itself), so it can move.
    public sealed class HubLife : MonoBehaviour
    {
        static readonly int WindId = Shader.PropertyToID("_HubWind");
        /// The wind's direction on the ground (it blows from the sea, over the plaza, toward the north-east).
        public static readonly Vector3 WindDir = new Vector3(.8f, 0, .6f);

        sealed class Bird { public Transform t; public Vector3 centre; public float radius, height, speed, phase, flap; }
        sealed class Flutter { public Mesh mesh; public Vector3[] rest, work; public float length, phase; }
        readonly List<Bird> birds = new List<Bird>();
        readonly List<Flutter> flags = new List<Flutter>();
        public int Birds => birds.Count;
        public int Flags => flags.Count;

        /// The life container of the hub that `part` belongs to (created on first use, beside the batched world).
        public static HubLife Of(Transform part)
        {
            var root = part.root;
            var life = root.GetComponentInChildren<HubLife>(true);
            if (life) return life;
            var go = new GameObject("Hub life"); go.transform.SetParent(root, false);
            return go.AddComponent<HubLife>();
        }

        void Update()
        {
            float t = Time.unscaledTime;
            float amp = .5f + .22f * Mathf.Sin(t * .31f) + .1f * Mathf.Sin(t * .93f + 1.3f);
            Shader.SetGlobalVector(WindId, new Vector4(WindDir.x * amp, 0, WindDir.z * amp, t % 600f));
            foreach (var b in birds)
            {
                float a = b.phase + t * b.speed / b.radius;
                var pos = b.centre + new Vector3(Mathf.Cos(a) * b.radius, b.height + Mathf.Sin(t * .4f + b.phase) * .8f, Mathf.Sin(a) * b.radius);
                var tangent = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a)) * Mathf.Sign(b.speed);
                b.t.SetPositionAndRotation(pos, Quaternion.LookRotation(tangent, Vector3.up) * Quaternion.Euler(0, 0, -Mathf.Sign(b.speed) * 14));
                // a few beats, then a glide
                float beat = Mathf.Repeat(t * .35f + b.phase, 1f) < .45f ? Mathf.Sin(t * b.flap) : .25f;
                b.t.localScale = new Vector3(1, Mathf.Lerp(-.55f, 1f, beat * .5f + .5f), 1);
            }
            foreach (var f in flags)
            {
                for (int i = 0; i < f.rest.Length; i++)
                {
                    var r = f.rest[i]; float k = r.x / f.length;
                    float wave = Mathf.Sin(r.x * 4.2f - t * 7.5f + f.phase) * .09f + Mathf.Sin(r.x * 9f - t * 11f + f.phase * 2) * .03f;
                    f.work[i] = new Vector3(r.x * (1 - .04f * k), r.y - k * k * .05f, wave * Mathf.Pow(k, .8f) * f.length);
                }
                f.mesh.vertices = f.work; f.mesh.RecalculateNormals();
            }
        }
        void OnDestroy() => Shader.SetGlobalVector(WindId, Vector4.zero);

        /// `n` gulls circling `centre` at about `height` metres, `radius` wide.
        public void Flock(Vector3 centre, int n, float radius, float height, int seed)
        {
            var mesh = GullMesh(); var mat = HubClub.Lit(HubClub.C("F4F1EA"), .2f);
            for (int i = 0; i < n; i++)
            {
                float h = Mathf.Abs(Mathf.Sin(seed * 12.9898f + i * 78.233f) * 43758.5453f) % 1f;
                var go = HubClub.Put("Gull", transform, mesh, mat, false);
                go.GetComponent<MeshRenderer>().receiveShadows = false;
                go.transform.localScale = Vector3.one;
                birds.Add(new Bird { t = go.transform, centre = centre, radius = radius * (.75f + h * .5f), height = height + (h - .5f) * 4, speed = (i % 3 == 0 ? -1 : 1) * (3.2f + h * 1.6f), phase = i * 1.7f + seed, flap = 9 + h * 4 });
            }
        }

        /// A flag on a pole: `top` is where its top edge meets the pole; it streams downwind. Size in metres.
        public void Flag(Vector3 top, float length, float height, Color colour)
        {
            const int nx = 9, ny = 3;
            var rest = new Vector3[nx * ny * 2]; var tris = new List<int>();
            for (int side = 0; side < 2; side++)
                for (int y = 0; y < ny; y++) for (int x = 0; x < nx; x++)
                    rest[side * nx * ny + y * nx + x] = new Vector3(length * x / (nx - 1), -height * y / (ny - 1), 0);
            for (int side = 0; side < 2; side++)
                for (int y = 0; y < ny - 1; y++) for (int x = 0; x < nx - 1; x++)
                {
                    int o = side * nx * ny, a = o + y * nx + x, b = a + 1, c = a + nx, d = c + 1;
                    if (side == 0) tris.AddRange(new[] { a, b, c, b, d, c }); else tris.AddRange(new[] { a, c, b, b, c, d });
                }
            var mesh = new Mesh { name = "Flag" }; mesh.MarkDynamic();
            mesh.vertices = rest; mesh.SetTriangles(tris, 0); mesh.RecalculateNormals();
            mesh.bounds = new Bounds(new Vector3(length / 2, -height / 2, 0), new Vector3(length * 1.4f, height * 1.6f, length));
            var go = HubClub.Put("Flag", transform, mesh, HubClub.Lit(colour, .2f), true);
            go.transform.SetPositionAndRotation(top, Quaternion.LookRotation(Vector3.Cross(WindDir, Vector3.up), Vector3.up));
            flags.Add(new Flutter { mesh = mesh, rest = rest, work = (Vector3[])rest.Clone(), length = length, phase = top.x * .7f + top.z * .3f });
        }

        static Mesh gull;
        static Mesh GullMesh()
        {
            if (gull) return gull;
            var v = new List<Vector3>(); var t = new List<int>();
            void Tri(Vector3 a, Vector3 b, Vector3 c) { int i = v.Count; v.Add(a); v.Add(b); v.Add(c); t.AddRange(new[] { i, i + 1, i + 2 }); i = v.Count; v.Add(a); v.Add(c); v.Add(b); t.AddRange(new[] { i, i + 1, i + 2 }); }
            foreach (int s in new[] { -1, 1 })
            {
                Tri(new Vector3(0, 0, .14f), new Vector3(s * .32f, .1f, .02f), new Vector3(0, 0, -.12f));
                Tri(new Vector3(s * .32f, .1f, .02f), new Vector3(s * .62f, .02f, -.1f), new Vector3(0, 0, -.12f));
            }
            Tri(new Vector3(-.05f, 0, .2f), new Vector3(.05f, 0, .2f), new Vector3(0, -.02f, -.25f));
            gull = new Mesh { name = "Gull" }; gull.SetVertices(v); gull.SetTriangles(t, 0); gull.RecalculateNormals(); gull.RecalculateBounds();
            return gull;
        }
    }
}

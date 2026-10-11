using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Hub
{
    /// Greybox building blocks for the Plaza: flat-colour URP materials, drums, discs, rings, boxes and world-space signs.
    /// Simple shapes on purpose (PLAN §8 phase 1); the evening-plaza art pass replaces them piece by piece.
    public static class HubKit
    {
        static readonly Dictionary<(Color, bool), Material> cache = new Dictionary<(Color, bool), Material>();
        static Font font;

        public static Material Mat(Color c, bool glow = false)
        {
            if (cache.TryGetValue((c, glow), out var m) && m) return m;
            var shader = Shader.Find(glow ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            m = new Material(shader) { name = (glow ? "Hub glow " : "Hub ") + ColorUtility.ToHtmlStringRGB(c) };
            m.SetColor("_BaseColor", c);
            if (!glow) { m.SetFloat("_Smoothness", .18f); m.SetFloat("_Metallic", 0); }
            m.enableInstancing = true;
            cache[(c, glow)] = m; return m;
        }
        /// A material with its own texture (sky gradient, signs): never cached.
        public static Material Textured(Texture tex, bool unlit = true)
        {
            var m = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); return m;
        }

        public static GameObject Obj(string name, Transform parent, Mesh mesh, Material mat, Vector3 position, Quaternion rotation, bool collide = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false); go.transform.SetPositionAndRotation(position, rotation);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = mat;
            if (collide) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 centre, Vector3 size, Color c, bool collide = true, Quaternion? rot = null, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, rot ?? Quaternion.identity); go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mat(c, glow);
            if (!collide) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
        /// An invisible wall.
        public static GameObject Blocker(string name, Transform parent, Vector3 centre, Vector3 size, Quaternion? rot = null)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(centre, rot ?? Quaternion.identity);
            go.AddComponent<BoxCollider>().size = size; return go;
        }

        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();

        /// A closed cylinder (drum) of `radius` and `height`, base at y = 0. `segments` around.
        public static Mesh Drum(float radius, float height, int segments = 40, bool caps = true)
        {
            string key = $"drum {radius} {height} {segments} {caps}";
            if (meshes.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v.Add(d * radius); n.Add(d); v.Add(d * radius + Vector3.up * height); n.Add(d);
            }
            for (int i = 0; i < segments; i++) { int b = i * 2; t.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 }); }
            if (caps)
            {
                foreach (var top in new[] { true, false })
                {
                    int c = v.Count; float y = top ? height : 0; v.Add(new Vector3(0, y, 0)); n.Add(top ? Vector3.up : Vector3.down);
                    for (int i = 0; i <= segments; i++) { float a = i * Mathf.PI * 2 / segments; v.Add(new Vector3(Mathf.Sin(a) * radius, y, Mathf.Cos(a) * radius)); n.Add(top ? Vector3.up : Vector3.down); }
                    for (int i = 0; i < segments; i++) { if (top) t.AddRange(new[] { c, c + 1 + i, c + 2 + i }); else t.AddRange(new[] { c, c + 2 + i, c + 1 + i }); }
                }
            }
            var m = new Mesh { name = key }; m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
            meshes[key] = m; return m;
        }

        /// A flat ring (annulus) lying on y = 0, facing up.
        public static Mesh Ring(float inner, float outer, int segments = 64)
        {
            string key = $"ring {inner} {outer} {segments}";
            if (meshes.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                v.Add(d * inner); v.Add(d * outer);
            }
            for (int i = 0; i < segments; i++) { int b = i * 2; t.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 }); }
            var m = new Mesh { name = key }; m.SetVertices(v); m.SetTriangles(t, 0);
            var normals = new Vector3[v.Count]; for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up; m.normals = normals; m.RecalculateBounds();
            meshes[key] = m; return m;
        }

        /// A thick ring wall (tube section), base at y = 0: outer and inner faces plus the top.
        public static Mesh RingWall(float inner, float outer, float height, int segments = 64)
        {
            string key = $"ringwall {inner} {outer} {height} {segments}";
            if (meshes.TryGetValue(key, out var cached)) return cached;
            var v = new List<Vector3>(); var n = new List<Vector3>(); var t = new List<int>();
            void Strip(float r0, float y0, float r1, float y1, Vector3 normalOf, bool radial, bool flip)
            {
                int start = v.Count;
                for (int i = 0; i <= segments; i++)
                {
                    float a = i * Mathf.PI * 2 / segments; var d = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));
                    var nn = radial ? d * normalOf.x : normalOf;
                    v.Add(d * r0 + Vector3.up * y0); n.Add(nn); v.Add(d * r1 + Vector3.up * y1); n.Add(nn);
                }
                for (int i = 0; i < segments; i++)
                {
                    int b = start + i * 2;
                    if (flip) t.AddRange(new[] { b, b + 1, b + 2, b + 2, b + 1, b + 3 }); else t.AddRange(new[] { b, b + 2, b + 1, b + 2, b + 3, b + 1 });
                }
            }
            Strip(outer, 0, outer, height, new Vector3(1, 0, 0), true, false);
            Strip(inner, 0, inner, height, new Vector3(-1, 0, 0), true, true);
            Strip(inner, height, outer, height, Vector3.up, false, true);
            var m = new Mesh { name = key }; m.SetVertices(v); m.SetNormals(n); m.SetTriangles(t, 0); m.RecalculateBounds();
            meshes[key] = m; return m;
        }

        /// A low-poly ball (subdivided octahedron), radius 1, centred on the origin.
        public static Mesh Ball(int subdivisions = 2)
        {
            string key = $"ball {subdivisions}";
            if (meshes.TryGetValue(key, out var cached)) return cached;
            var verts = new List<Vector3> { Vector3.up, Vector3.forward, Vector3.right, Vector3.back, Vector3.left, Vector3.down };
            var tris = new List<int> { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
            for (int s = 0; s < subdivisions; s++)
            {
                var next = new List<int>(); var mid = new Dictionary<long, int>();
                int Mid(int a, int b)
                {
                    long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(k, out var i)) return i;
                    verts.Add(((verts[a] + verts[b]) * .5f).normalized); mid[k] = verts.Count - 1; return verts.Count - 1;
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], c = tris[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    next.AddRange(new[] { a, ab, ca, ab, b, bc, ca, bc, c, ab, bc, ca });
                }
                tris = next;
            }
            // flat shading: split every triangle (the faceted low-poly tree look of the concept)
            var fv = new List<Vector3>(); var fn = new List<Vector3>(); var ft = new List<int>();
            for (int i = 0; i < tris.Count; i += 3)
            {
                var a = verts[tris[i]]; var b = verts[tris[i + 1]]; var c = verts[tris[i + 2]];
                var nn = Vector3.Cross(b - a, c - a).normalized;
                ft.Add(fv.Count); fv.Add(a); fn.Add(nn); ft.Add(fv.Count); fv.Add(b); fn.Add(nn); ft.Add(fv.Count); fv.Add(c); fn.Add(nn);
            }
            var m = new Mesh { name = key }; m.SetVertices(fv); m.SetNormals(fn); m.SetTriangles(ft, 0); m.RecalculateBounds();
            meshes[key] = m; return m;
        }

        /// A vertical sky dome (inverted sphere) carrying a top-to-horizon gradient in its UVs (v = elevation).
        public static Mesh SkyDome(int segments = 32, int rings = 16)
        {
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float el = Mathf.Lerp(-.25f, 1f, r / (float)rings) * Mathf.PI / 2;
                for (int s = 0; s <= segments; s++)
                {
                    float az = s * Mathf.PI * 2 / segments;
                    v.Add(new Vector3(Mathf.Cos(el) * Mathf.Sin(az), Mathf.Sin(el), Mathf.Cos(el) * Mathf.Cos(az)));
                    uv.Add(new Vector2(s / (float)segments, Mathf.Clamp01(Mathf.Sin(el) * .5f + .5f)));
                }
            }
            for (int r = 0; r < rings; r++) for (int s = 0; s < segments; s++)
            {
                int a = r * (segments + 1) + s, b = a + segments + 1;
                t.AddRange(new[] { a, b, a + 1, b, b + 1, a + 1 });   // front faces toward the centre
            }
            var m = new Mesh { name = "Hub sky dome" }; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 2); return m;
        }

        public static Texture2D Gradient(params (float at, Color c)[] stops)
        {
            var tex = new Texture2D(1, 256, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Hub gradient" };
            for (int i = 0; i < 256; i++)
            {
                float y = i / 255f; Color c = stops[0].c;
                for (int k = 1; k < stops.Length; k++) if (y >= stops[k - 1].at) c = Color.Lerp(stops[k - 1].c, stops[k].c, Mathf.InverseLerp(stops[k - 1].at, stops[k].at, y));
                tex.SetPixel(0, i, c);
            }
            tex.Apply(false, true); return tex;
        }

        public static Font Font => font ??= Resources.Load<Font>("Tennis/UI/Fonts/Rubik-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        /// A world-space sign: text on an optional plate, `height` metres tall, facing `facing` (the way a reader looks at it is -facing).
        public static Text Sign(string name, Transform parent, string text, Vector3 position, Vector3 facing, float height, Color color, Color? plate = null, float width = 0)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(-facing, Vector3.up));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; float px = 100f;
            float w = width > 0 ? width : Mathf.Max(1.2f, text.Length * height * .62f + height * .8f);
            rt.sizeDelta = new Vector2(w * px, height * 1.25f * px); rt.localScale = Vector3.one / px;
            if (plate.HasValue)
            {
                var bg = new GameObject("Plate").AddComponent<Image>(); bg.transform.SetParent(go.transform, false); bg.color = plate.Value;
                var brt = bg.rectTransform; brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one; brt.offsetMin = brt.offsetMax = Vector2.zero; bg.raycastTarget = false;
            }
            var label = new GameObject("Text").AddComponent<Text>(); label.transform.SetParent(go.transform, false);
            label.font = Font; label.text = text; label.color = color; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.fontSize = Mathf.RoundToInt(height * px * .82f); label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
            var lrt = label.rectTransform; lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.offsetMin = lrt.offsetMax = Vector2.zero;
            return label;
        }
    }
}

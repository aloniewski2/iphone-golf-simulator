using UnityEngine;

namespace GolfArcade.Course
{
    /// The world beyond the hole, the way Golf Dreams frames its holes: low-poly islands with
    /// green peaks out on the horizon and a few sailboats riding the swell between them and the
    /// course. The models are Higgsfield generations (an image, then Tripo image-to-3D), slimmed
    /// in Blender by blender/scripts/backdrop_import.py to Resources/Course/Backdrop. They sit on
    /// the sea (the course models keep their water at y = 0), out past everything the ball can
    /// reach, where the ball cam and the aerials look.
    public static class Backdrop
    {
        /// Around `course` (its world bounds), with `along` the direction down the hole; the open
        /// sea they sit on is `sea`'s material.
        public static void Place(Transform parent, Bounds course, Vector3 along, Material sea)
        {
            var island = Resources.Load<GameObject>("Course/Backdrop/island");
            var boat = Resources.Load<GameObject>("Course/Backdrop/sailboat");
            along.y = 0; along.Normalize();
            var right = Vector3.Cross(Vector3.up, along);
            var centre = course.center; centre.y = 0;
            float reach = Mathf.Max(course.extents.x, course.extents.z);
            var root = new GameObject("Backdrop").transform;
            root.SetParent(parent, false);
            // The open sea to the horizon, a hand's breadth under the hole's own water so that
            // (with its swell and its shallows) shows where it reaches and this beyond it.
            // It is rings, close together by the island and wider apart out to the horizon, not one
            // big quad: the fog is worked out at the corners, and a quad whose corners are all
            // kilometres off comes out the colour of the sky however near its middle is.
            var ocean = new GameObject("Open sea");
            ocean.transform.SetParent(root, false);
            ocean.transform.position = new Vector3(centre.x, -0.3f, centre.z);
            ocean.AddComponent<MeshFilter>().sharedMesh = SeaMesh();
            var oceanRenderer = ocean.AddComponent<MeshRenderer>();
            oceanRenderer.sharedMaterial = sea;
            oceanRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (!island && !boat) return;
            if (island)
            {
                // beyond the green, off to one side of it, and away on the far flank
                Put(island, root, centre + along * (reach + 260) + right * 110, 230, 20, -4);
                Put(island, root, centre + along * (reach + 150) - right * (reach + 240), 160, 145, -3);
                Put(island, root, centre + right * (reach + 300) - along * 60, 190, 260, -4);
            }
            if (boat)
            {
                Bob(Put(boat, root, centre + along * (reach + 70) - right * 70, 14, 60, 0), 0f);
                Bob(Put(boat, root, centre + right * (reach + 110) + along * 40, 12, 200, 0), 1.7f);
                Bob(Put(boat, root, centre - right * (reach + 90) + along * (reach * 0.3f), 13, 310, 0), 3.1f);
            }
        }

        static Mesh seaMesh;

        /// A disc of sea 5 km across, flat, in rings from a few yards out to its edge.
        static Mesh SeaMesh()
        {
            if (seaMesh) return seaMesh;
            const int segments = 72;
            var radii = new System.Collections.Generic.List<float> { 0 };
            for (float r = 12; r < 2500; r *= 1.22f) radii.Add(r);
            radii.Add(2500);
            var verts = new Vector3[1 + (radii.Count - 1) * segments];
            for (int k = 1; k < radii.Count; k++)
                for (int j = 0; j < segments; j++)
                {
                    float a = j * Mathf.PI * 2 / segments;
                    verts[1 + (k - 1) * segments + j] = new Vector3(Mathf.Cos(a) * radii[k], 0, Mathf.Sin(a) * radii[k]);
                }
            var tris = new System.Collections.Generic.List<int>();
            for (int j = 0; j < segments; j++) { tris.Add(0); tris.Add(1 + (j + 1) % segments); tris.Add(1 + j); }
            for (int k = 1; k < radii.Count - 1; k++)
                for (int j = 0; j < segments; j++)
                {
                    int a = 1 + (k - 1) * segments + j, b = 1 + (k - 1) * segments + (j + 1) % segments;
                    int c = a + segments, d = b + segments;
                    tris.Add(a); tris.Add(b); tris.Add(c);
                    tris.Add(b); tris.Add(d); tris.Add(c);
                }
            var normals = new Vector3[verts.Length];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            seaMesh = new Mesh { name = "Open sea", vertices = verts, normals = normals, triangles = tris.ToArray() };
            seaMesh.RecalculateBounds();
            return seaMesh;
        }

        static Transform Put(GameObject prefab, Transform root, Vector3 at, float size, float yaw, float sink)
        {
            var go = Object.Instantiate(prefab, root);
            go.name = prefab.name;
            var t = go.transform;
            t.position = new Vector3(at.x, sink, at.z);
            t.rotation = Quaternion.Euler(0, yaw, 0) * t.rotation;
            t.localScale = t.localScale * size;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return t;
        }

        static void Bob(Transform boat, float phase) => boat.gameObject.AddComponent<Swell>().Begin(phase);

        /// A boat riding the swell: a slow rise and fall and a gentle roll and pitch, each on
        /// its own period so it never looks like a loop.
        sealed class Swell : MonoBehaviour
        {
            Vector3 home; Quaternion rest; float phase;
            public void Begin(float p) { phase = p; home = transform.position; rest = transform.rotation; }
            void Update()
            {
                float t = Time.time + phase;
                transform.position = home + Vector3.up * (0.35f * Mathf.Sin(t * 0.9f));
                transform.rotation = rest * Quaternion.Euler(3.5f * Mathf.Sin(t * 0.7f), 0, 4.5f * Mathf.Sin(t * 1.1f + 0.6f));
            }
        }
    }
}

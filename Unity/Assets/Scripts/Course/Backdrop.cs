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
            var ocean = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ocean.name = "Open sea";
            Object.Destroy(ocean.GetComponent<Collider>());
            ocean.transform.SetParent(root, false);
            ocean.transform.position = new Vector3(centre.x, -0.3f, centre.z);
            ocean.transform.localScale = new Vector3(400, 1, 400);            // 4 km a side
            var oceanRenderer = ocean.GetComponent<Renderer>();
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

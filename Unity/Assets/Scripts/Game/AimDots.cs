using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Wii Sports Golf's aim: a row of white dots along the arc a full swing would fly from
    /// here, out to the landing ring — and, as the backswing loads, one amber dot sliding along
    /// that arc to where a swing with this much load would come down, so you gauge the shot
    /// before you commit. Soft discs facing the camera, sized against the view so they read
    /// from behind the ball and out at the landing alike.
    public sealed class AimDots : MonoBehaviour
    {
        const int Max = 40;
        readonly List<Transform> dots = new();
        readonly List<Vector3> path = new();
        Transform mark;
        Camera view;
        float markAt = -1f;

        public static AimDots Create(Transform parent, Camera view)
        {
            var go = new GameObject("Aim dots");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AimDots>();
            a.view = view;
            var white = new Material(ShotEffects.ParticleMaterial()) { color = new Color(1, 1, 1, 0.92f) };
            for (int i = 0; i < Max; i++) a.dots.Add(Disc(go.transform, "Dot", white));
            var amber = new Material(ShotEffects.ParticleMaterial()) { color = new Color(LandingZone.Amber.r, LandingZone.Amber.g, LandingZone.Amber.b, 0.98f) };
            a.mark = Disc(go.transform, "Load mark", amber);
            a.Hide();
            return a;
        }

        static Transform Disc(Transform parent, string name, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            q.SetActive(false);
            return q.transform;
        }

        /// Dots along `arc` (world points, in order), one every `spacing` yards of it.
        public void Lay(List<Vector3> arc, float spacing = 4f)
        {
            path.Clear(); path.AddRange(arc);
            gameObject.SetActive(true);
            int n = 0;
            float since = spacing;
            for (int i = 0; i < arc.Count && n < Max; i++)
            {
                if (i > 0) since += Vector3.Distance(arc[i - 1], arc[i]);
                if (since < spacing) continue;
                since = 0;
                dots[n].position = arc[i];
                dots[n].gameObject.SetActive(true);
                n++;
            }
            for (int i = n; i < Max; i++) dots[i].gameObject.SetActive(false);
            Mark(markAt);
        }

        /// The amber dot at this fraction of the arc's length (below 0 hides it).
        public void Mark(float fraction)
        {
            markAt = fraction;
            if (fraction < 0 || path.Count < 2) { mark.gameObject.SetActive(false); return; }
            float total = 0;
            for (int i = 1; i < path.Count; i++) total += Vector3.Distance(path[i - 1], path[i]);
            float want = Mathf.Clamp01(fraction) * total, run = 0;
            for (int i = 1; i < path.Count; i++)
            {
                float seg = Vector3.Distance(path[i - 1], path[i]);
                if (run + seg >= want || i == path.Count - 1)
                {
                    mark.position = Vector3.Lerp(path[i - 1], path[i], seg > 1e-4f ? (want - run) / seg : 1f) + Vector3.up * 0.05f;
                    break;
                }
                run += seg;
            }
            mark.gameObject.SetActive(true);
        }

        public void Hide() { gameObject.SetActive(false); markAt = -1f; }

        void LateUpdate()
        {
            if (!view) return;
            var rot = view.transform.rotation;
            foreach (var d in dots) if (d.gameObject.activeSelf) Face(d, rot, 0.014f, 0.35f);
            if (mark.gameObject.activeSelf) Face(mark, rot, 0.024f, 0.55f);
        }

        void Face(Transform t, Quaternion rot, float share, float least)
        {
            t.rotation = rot;
            float d = Vector3.Distance(view.transform.position, t.position);
            float viewHeight = 2f * d * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad / 2f);
            t.localScale = Vector3.one * Mathf.Max(least, share * viewHeight);
        }
    }
}

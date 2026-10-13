using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The aim: a row of white dots from the ball along the start of the shot — which way it
    /// leaves and how it climbs, fading out about half way, so the player sees the line it
    /// starts on but not where it comes down — and, as the backswing loads, one amber dot on the
    /// ground sliding out to where a swing with this much load would come down in still air, so
    /// you gauge the shot before you commit. Soft discs facing the camera, sized against the view.
    public sealed class AimDots : MonoBehaviour
    {
        const int Max = 40;
        const float DotAlpha = 0.74f;
        static readonly Color Shade = new(0.04f, 0.1f, 0.16f, 0.5f);
        readonly List<Transform> dots = new();
        readonly List<float> fades = new();
        readonly List<Vector3> path = new();
        MaterialPropertyBlock tint;
        Transform mark;
        Camera view;
        float markAt = -1f, least = 0.16f;

        public static AimDots Create(Transform parent, Camera view)
        {
            var go = new GameObject("Aim dots") { layer = BallLook.OverlayLayer };
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<AimDots>();
            a.view = view;
            var white = new Material(ShotEffects.ParticleMaterial()) { color = new Color(1, 1, 1, DotAlpha) };
            // each dot sits on a faint dark halo, so it still reads against pale sand, rock or cloud
            var shade = new Material(ShotEffects.ParticleMaterial()) { color = Shade };
            a.tint = new MaterialPropertyBlock();
            for (int i = 0; i < Max; i++)
            {
                var dot = Disc(go.transform, "Dot", white);
                var halo = Disc(dot, "Halo", shade);
                halo.localPosition = new Vector3(0, 0, 0.02f);   // just behind it, as the dot faces the camera
                halo.localScale = Vector3.one * 1.8f;
                halo.gameObject.SetActive(true);
                a.dots.Add(dot); a.fades.Add(1f);
            }
            var amber = new Material(ShotEffects.ParticleMaterial()) { color = new Color(LandingZone.Amber.r, LandingZone.Amber.g, LandingZone.Amber.b, 0.98f) };
            a.mark = Disc(go.transform, "Load mark", amber);
            a.Hide();
            return a;
        }

        static Transform Disc(Transform parent, string name, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name; q.layer = BallLook.OverlayLayer;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            q.SetActive(false);
            return q.transform;
        }

        /// Dots along `arc` (world points, in order), one every `spacing` yards of it: whole to
        /// `fadeFrom` of its length, then fading and shrinking away to nothing at its end.
        /// `smallest` is the least a dot shrinks to with distance, yards (a putt's are finer).
        public void Lay(List<Vector3> arc, float spacing = 4f, float fadeFrom = 0.45f, float smallest = 0.16f)
        {
            path.Clear(); path.AddRange(arc);
            gameObject.SetActive(true);
            least = smallest;
            float length = 0;
            for (int i = 1; i < arc.Count; i++) length += Vector3.Distance(arc[i - 1], arc[i]);
            int n = 0;
            float since = 0, along = 0; // Leave the actual ball and club head free of an overlapping glow.
            for (int i = 0; i < arc.Count && n < Max; i++)
            {
                if (i > 0) { float step = Vector3.Distance(arc[i - 1], arc[i]); since += step; along += step; }
                if (since < spacing) continue;
                since = 0;
                float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fadeFrom, 1f, length > 0 ? along / length : 1f));
                if (fade < 0.04f) break;
                fades[n] = fade;
                dots[n].position = arc[i];
                tint.SetColor("_Color", new Color(1, 1, 1, DotAlpha * fade));
                dots[n].GetComponent<Renderer>().SetPropertyBlock(tint);
                tint.SetColor("_Color", new Color(Shade.r, Shade.g, Shade.b, Shade.a * fade));
                dots[n].GetChild(0).GetComponent<Renderer>().SetPropertyBlock(tint);
                dots[n].gameObject.SetActive(true);
                n++;
            }
            for (int i = n; i < Max; i++) dots[i].gameObject.SetActive(false);
            if (markAt < 0) mark.gameObject.SetActive(false);
        }

        /// The amber dot on the ground where the loaded swing comes down; null hides it.
        public void MarkAt(Vector3? spot)
        {
            markAt = spot.HasValue ? 1f : -1f;
            mark.gameObject.SetActive(spot.HasValue);
            if (spot.HasValue) mark.position = spot.Value + Vector3.up * 0.3f;
        }

        public void Hide() { gameObject.SetActive(false); markAt = -1f; }

        /// Where the amber dot is, when it shows — for the minimap's copy of it.
        public bool MarkShown => gameObject.activeSelf && mark.gameObject.activeSelf;
        public Vector3 MarkPosition => mark.position;

        void LateUpdate()
        {
            if (!view) return;
            var rot = view.transform.rotation;
            for (int i = 0; i < dots.Count; i++)
                if (dots[i].gameObject.activeSelf) Face(dots[i], rot, 0.006f, least, Mathf.Lerp(0.55f, 1f, fades[i]));
            if (mark.gameObject.activeSelf) Face(mark, rot, 0.014f, 0.35f);
        }

        void Face(Transform t, Quaternion rot, float share, float smallest, float scale = 1f)
        {
            t.rotation = rot;
            float d = Vector3.Distance(view.transform.position, t.position);
            float viewHeight = 2f * d * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad / 2f);
            t.localScale = Vector3.one * Mathf.Max(smallest, share * viewHeight) * scale;
        }
    }
}

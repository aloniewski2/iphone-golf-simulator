using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Keeps the ball readable in flight, the way the golf games draw it: never smaller than a
    /// fingertip-sized dot on the screen however far it has flown (it swells with distance from
    /// the camera, sitting on the ground rather than sinking into it), a soft glow round it so
    /// it reads against sky and grass alike, and a soft shadow on the ground straight below it
    /// that shrinks and darkens as it comes down — the eye judges the landing from the two
    /// closing on each other. At address and on the green it is left at its true size.
    public sealed class BallLook : MonoBehaviour
    {
        /// Everything drawn for the player rather than part of the course (aim dots, the landing
        /// ring, the tracer, this): the course view shows it, the minimap's camera leaves it out.
        public const int OverlayLayer = 31;
        /// The ball's least height on the screen, as a share of the view's height (~1 %, about
        /// 25 px on a phone); in the ball POV, Codex's presentation ball, as a share of the width.
        const float MinShare = 0.0105f, PovWidthShare = 0.08f;
        float share = MinShare, wantShare = MinShare;
        /// The drawn ball's diameter this frame, yards (its true size, or swollen to be seen),
        /// and its centre.
        public float DrawnDiameter { get; private set; }
        public Vector3 Centre => ball.position + Vector3.up * (DrawnDiameter - size) * 0.5f;

        Transform ball, body, halo, shadow;
        Camera view;
        float size;
        Material haloMaterial, shadowMaterial;
        bool readable;

        /// `body` is the ball's visible part (under the `ball` root, which carries its position
        /// and true size `size`); it is swollen and lifted, never the root.
        public static BallLook Create(Transform parent, Transform ball, Transform body, Camera view, float size)
        {
            var go = new GameObject("Ball look");
            go.transform.SetParent(parent, false);
            var look = go.AddComponent<BallLook>();
            look.ball = ball; look.body = body; look.view = view; look.size = size;
            look.haloMaterial = new Material(ShotEffects.ParticleMaterial()) { color = new Color(1f, 1f, 0.94f, 0.5f) };
            look.shadowMaterial = new Material(ShotEffects.ParticleMaterial()) { color = new Color(0.05f, 0.09f, 0.04f, 0.5f) };
            look.halo = Quad(go.transform, "Halo", look.haloMaterial);
            look.shadow = Quad(go.transform, "Shadow", look.shadowMaterial);
            look.Readable(false);
            return look;
        }

        static Transform Quad(Transform parent, string name, Material m)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name; q.layer = OverlayLayer;
            Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            var r = q.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return q.transform;
        }

        /// Codex's big POV ball — about 8 % of the frame's width, whatever the screen's shape —
        /// eased in and out over half a second; or the plain readable one.
        public void Pov(bool on) => wantShare = on ? Mathf.Clamp(PovWidthShare * (view ? view.aspect : 0.46f), MinShare, 0.15f) : MinShare;

        /// On for the shot (in the air, bouncing, rolling out, at rest after it); off for
        /// address, putts and the showcase, where the ball is its true size and casts its own
        /// shadow.
        public void Readable(bool on)
        {
            readable = on;
            halo.gameObject.SetActive(on);
            shadow.gameObject.SetActive(on);
            foreach (var r in body.GetComponentsInChildren<Renderer>(true))
                r.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            if (!on) { body.localScale = Vector3.one; body.localPosition = Vector3.zero; share = wantShare = MinShare; DrawnDiameter = size; }
        }

        float ViewHeightAt(Vector3 p)
        {
            float d = Vector3.Distance(view.transform.position, p);
            return 2f * d * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad / 2f);
        }

        void LateUpdate()
        {
            if (!readable || !view || !ball.gameObject.activeInHierarchy)
            {
                if (readable) { halo.gameObject.SetActive(false); shadow.gameObject.SetActive(false); }
                return;
            }
            halo.gameObject.SetActive(true);
            // swell to the least screen size, the bottom staying where the real ball's is
            share = Mathf.MoveTowards(share, wantShare, Time.deltaTime * 0.06f);
            float k = Mathf.Max(1f, share * ViewHeightAt(ball.position) / size);
            body.localScale = Vector3.one * k;
            body.localPosition = Vector3.up * (k - 1f) * 0.5f;
            var centre = ball.position + Vector3.up * (k - 1f) * 0.5f * size;
            float drawn = k * size;
            DrawnDiameter = drawn;
            halo.position = centre;
            halo.rotation = view.transform.rotation;
            halo.localScale = Vector3.one * drawn * 2.6f;
            // the shadow: straight down, soft, smaller and darker the lower the ball
            double ground = HoleView.GroundHeight(HoleView.ToCourse(ball.position));
            float height = Mathf.Max(0f, ball.position.y - (float)ground - size * 0.5f);
            float near = Mathf.Clamp01(1f - height / 45f);
            bool show = height < 60f;
            shadow.gameObject.SetActive(show);
            if (show)
            {
                shadow.position = new Vector3(ball.position.x, (float)ground + 0.04f, ball.position.z);
                shadow.rotation = Quaternion.Euler(90, 0, 0);
                float least = MinShare * 1.2f * ViewHeightAt(shadow.position);
                shadow.localScale = Vector3.one * Mathf.Max(least, drawn * (1.4f + height * 0.03f));
                var c = shadowMaterial.color; c.a = 0.18f + 0.4f * near * near; shadowMaterial.color = c;
            }
        }
    }
}

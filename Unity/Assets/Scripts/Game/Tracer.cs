using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Game
{
    /// The ball tracer, the way Golf Dreams (and TV's TopTracer) draws a shot: one thin luminous
    /// line laid along the ball's path from the moment it leaves the club, growing with the
    /// flight and staying in the air over the hole until the next shot is set up. Kept a few
    /// pixels wide whatever the distance — it is a line on the picture, not a thing in the world —
    /// and coloured by the swing's rating, brightest at the ball.
    public sealed class Tracer : MonoBehaviour
    {
        const float Step = 0.6f;            // yards between points along the line
        readonly List<Vector3> points = new();
        readonly List<Keyframe> widths = new();
        LineRenderer line;
        Camera view;
        Color color = Color.green;
        float fade = 1f, fadeFrom = -1f;

        public static Tracer Create(Transform parent, Camera view)
        {
            var go = new GameObject("Tracer");
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Tracer>();
            t.view = view;
            t.line = go.AddComponent<LineRenderer>();
            t.line.useWorldSpace = true;
            t.line.material = ShotEffects.ParticleMaterial();
            t.line.material.mainTexture = ShotEffects.Band();
            t.line.textureMode = LineTextureMode.Stretch;
            t.line.alignment = LineAlignment.View;
            t.line.numCornerVertices = 3; t.line.numCapVertices = 3;
            t.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.line.receiveShadows = false;
            t.line.positionCount = 0;
            go.layer = BallLook.OverlayLayer;
            go.SetActive(false);
            return t;
        }

        public void Begin(Color c)
        {
            color = c; points.Clear(); fade = 1f; fadeFrom = -1f;
            line.positionCount = 0;
            gameObject.SetActive(true);
        }

        /// The ball is here now; the line reaches it.
        public void Push(Vector3 at)
        {
            if (points.Count == 0 || (at - points[points.Count - 1]).sqrMagnitude >= Step * Step) points.Add(at);
            else if (points.Count > 1) points[points.Count - 1] = at;
            if (points.Count == 1) points.Add(at);
        }

        /// Leave the line in the air for a while, then let it go.
        public void FadeOut(float seconds) { fadeFrom = Time.time; fadeSeconds = seconds; }
        float fadeSeconds = 1.5f;
        public void Clear() { points.Clear(); line.positionCount = 0; gameObject.SetActive(false); }

        float PixelWidth(Vector3 at)
        {
            float d = view ? Vector3.Distance(view.transform.position, at) : 30f;
            float viewHeight = view ? 2f * d * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad / 2f) : 30f;
            return Mathf.Max(0.004f, 0.003f * viewHeight);
        }

        void LateUpdate()
        {
            if (points.Count < 2) return;
            if (fadeFrom >= 0)
            {
                fade = 1f - Mathf.Clamp01((Time.time - fadeFrom) / fadeSeconds);
                if (fade <= 0f) { Clear(); return; }
            }
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            // A few pixels wide all along: each stretch sized against its own distance from the
            // view, so the end nearest the camera is not a fat smear and the far end not lost.
            int n = points.Count, stride = Mathf.Max(1, n / 40);
            widths.Clear();
            for (int i = 0; i < n - 1; i += stride) widths.Add(new Keyframe((float)i / (n - 1), PixelWidth(points[i])));
            widths.Add(new Keyframe(1f, PixelWidth(points[n - 1])));
            line.widthMultiplier = 1f;
            line.widthCurve = new AnimationCurve(widths.ToArray());
            var g = new Gradient();
            var tail = Color.Lerp(color, Color.white, 0.1f); tail.a = 0.55f * fade;
            var head = Color.Lerp(color, Color.white, 0.45f); head.a = 0.95f * fade;
            g.SetKeys(new[] { new GradientColorKey(tail, 0), new GradientColorKey(head, 1) },
                      new[] { new GradientAlphaKey(tail.a, 0), new GradientAlphaKey(0.75f * fade, 0.6f), new GradientAlphaKey(head.a, 1) });
            line.colorGradient = g;
        }
    }
}

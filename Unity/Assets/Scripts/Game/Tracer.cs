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
            // a few pixels wide: sized against the view at the line's middle
            var mid = points[points.Count / 2];
            float d = view ? Vector3.Distance(view.transform.position, mid) : 30f;
            float viewHeight = view ? 2f * d * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad / 2f) : 30f;
            line.widthMultiplier = Mathf.Max(0.05f, 0.0035f * viewHeight);
            var g = new Gradient();
            var tail = Color.Lerp(color, Color.white, 0.1f); tail.a = 0.55f * fade;
            var head = Color.Lerp(color, Color.white, 0.45f); head.a = 0.95f * fade;
            g.SetKeys(new[] { new GradientColorKey(tail, 0), new GradientColorKey(head, 1) },
                      new[] { new GradientAlphaKey(tail.a, 0), new GradientAlphaKey(0.75f * fade, 0.6f), new GradientAlphaKey(head.a, 1) });
            line.colorGradient = g;
        }
    }
}

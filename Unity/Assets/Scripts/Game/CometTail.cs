using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Codex's trail, the way his ball POV shows it (Hole_12_Cinematic.blend, Trail_Green /
    /// Yellow / Red): one short translucent tube right behind the ball — the last third of a
    /// second of its path — nearly as wide as the ball at its head and a hair at its tail,
    /// brightest at the ball, in the swing's colour; after the landing it narrows and fades away
    /// over half a second. Its numbers are his recipe (ShotEffects.TrailRecipe, from
    /// Resources/Course/swing_trail.json). Sized off the drawn ball, so it matches the big POV
    /// ball rather than the true one.
    public sealed class CometTail : MonoBehaviour
    {
        readonly List<Vector3> points = new();
        readonly List<float> times = new();
        LineRenderer line;
        BallLook look;
        Color color = Color.green;
        bool live;
        float fadeFrom = -1f;

        public static CometTail Create(Transform parent, BallLook look)
        {
            var go = new GameObject("Comet tail") { layer = BallLook.OverlayLayer };
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<CometTail>();
            c.look = look;
            c.line = go.AddComponent<LineRenderer>();
            c.line.useWorldSpace = true;
            c.line.material = ShotEffects.ParticleMaterial();
            c.line.material.mainTexture = ShotEffects.Band();
            c.line.textureMode = LineTextureMode.Stretch;
            c.line.alignment = LineAlignment.View;
            c.line.numCapVertices = 4;
            c.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            c.line.receiveShadows = false;
            c.line.positionCount = 0;
            go.SetActive(false);
            return c;
        }

        public void Begin(Color c)
        {
            color = c; points.Clear(); times.Clear(); live = true; fadeFrom = -1f;
            line.positionCount = 0;
            gameObject.SetActive(true);
        }

        /// Down: it narrows and fades away behind the bouncing ball.
        public void Land() { if (live && fadeFrom < 0) fadeFrom = Time.time; }

        public void Clear() { live = false; points.Clear(); times.Clear(); line.positionCount = 0; gameObject.SetActive(false); }

        void LateUpdate()
        {
            if (!live || look == null) return;
            var r = ShotEffects.TrailRecipe();
            float now = Time.time;
            float fade = fadeFrom < 0 ? 1f : 1f - Mathf.Clamp01((now - fadeFrom) / Mathf.Max(0.05f, r.fadeSeconds));
            if (fade <= 0f) { Clear(); return; }
            points.Add(look.Centre); times.Add(now);
            // keep the last `lengthSeconds` of the path, and one point older to reach back to it
            while (times.Count > 2 && times[1] < now - r.lengthSeconds) { points.RemoveAt(0); times.RemoveAt(0); }
            if (points.Count < 2) { line.positionCount = 0; return; }
            line.positionCount = points.Count;
            line.SetPositions(points.ToArray());
            float d = look.DrawnDiameter;
            line.widthMultiplier = 1f;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, r.tailRadiusBalls * d * fade), new Keyframe(1f, r.headRadiusBalls * d * fade));
            var body = Color.Lerp(color, Color.white, 0.25f);
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(color, 0f), new GradientColorKey(body, 1f) },
                      new[] { new GradientAlphaKey(r.tailAlpha * fade, 0f), new GradientAlphaKey(r.headAlpha * fade, 1f) });
            line.colorGradient = g;
        }
    }
}

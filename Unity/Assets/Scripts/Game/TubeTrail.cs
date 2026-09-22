using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Codex's trail for a live shot: the same tapered tube his Blender scene follows the
    /// animated ball with — twenty rings over the last third of a second of flight, radius
    /// 0.025 + 0.28·t^1.2 of the head's (t = 1 at the ball), alpha 0.04 + 0.38·t², unlit, in the
    /// swing's colour — rebuilt every frame around the ball's real path, and drained away over
    /// half a second once the ball has landed. The head radius is set from outside against the
    /// view, so the tube keeps its size on screen whatever the lens does.
    public sealed class TubeTrail : MonoBehaviour
    {
        const int Rings = 20, Sides = 8;
        public float LengthSeconds = 0.333f, FadeSeconds = 0.533f;
        public float HeadRadius = 0.3f;
        public float HeadAlpha = 0.42f, TailAlpha = 0.04f;

        readonly List<(Vector3 p, float t)> path = new();
        Mesh mesh;
        Color color = Color.green;
        float fade = 1f, fadeFrom = -1f;
        bool laying;
        readonly Vector3[] verts = new Vector3[Rings * Sides];
        readonly Color[] colors = new Color[Rings * Sides];
        int[] tris;

        public static TubeTrail Create(Transform parent)
        {
            var go = new GameObject("Trail tube");
            go.transform.SetParent(parent, false);
            var tube = go.AddComponent<TubeTrail>();
            var mf = go.AddComponent<MeshFilter>();
            tube.mesh = new Mesh { name = "Trail tube" }; tube.mesh.MarkDynamic();
            mf.mesh = tube.mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = new Material(ShotEffects.ParticleMaterial()) { mainTexture = Texture2D.whiteTexture };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
            var t = new List<int>();
            for (int i = 0; i < Rings - 1; i++)
                for (int j = 0; j < Sides; j++)
                {
                    int a = i * Sides + j, b = i * Sides + (j + 1) % Sides, c = (i + 1) * Sides + (j + 1) % Sides, d = (i + 1) * Sides + j;
                    t.AddRange(new[] { a, b, c, a, c, d });
                }
            tube.tris = t.ToArray();
            go.SetActive(false);
            return tube;
        }

        public void Begin(Color c)
        {
            color = c; path.Clear(); fade = 1f; fadeFrom = -1f; laying = true;
            mesh.Clear();
            gameObject.SetActive(true);
        }

        /// The ball is here now.
        public void Push(Vector3 at)
        {
            if (!laying) return;
            path.Add((at, Time.time));
            while (path.Count > 2 && Time.time - path[0].t > LengthSeconds + 0.1f) path.RemoveAt(0);
        }

        /// Landed: nothing new is laid; what is there narrows away.
        public void Land() { if (laying && fadeFrom < 0) fadeFrom = Time.time; }
        public void End() { laying = false; gameObject.SetActive(false); }

        Vector3 At(float time)
        {
            if (path.Count == 0) return Vector3.zero;
            if (time <= path[0].t) return path[0].p;
            for (int i = 1; i < path.Count; i++)
                if (path[i].t >= time)
                {
                    float span = path[i].t - path[i - 1].t;
                    return span > 1e-5f ? Vector3.Lerp(path[i - 1].p, path[i].p, (time - path[i - 1].t) / span) : path[i].p;
                }
            return path[path.Count - 1].p;
        }

        void LateUpdate()
        {
            if (path.Count < 2) return;
            if (fadeFrom >= 0)
            {
                fade = 1f - Mathf.Clamp01((Time.time - fadeFrom) / FadeSeconds);
                if (fade <= 0f) { End(); return; }
            }
            float now = fadeFrom >= 0 ? fadeFrom : Time.time;     // once landed the tube stops at the landing
            var head = At(now);
            for (int i = 0; i < Rings; i++)
            {
                float t = i / (Rings - 1f);                        // 0 at the tail, 1 at the ball
                float time = now - LengthSeconds * (1 - t);
                var p = At(time);
                var v = At(Mathf.Min(now, time + 0.02f)) - At(Mathf.Max(path[0].t, time - 0.02f));
                if (v.sqrMagnitude < 1e-6f) v = head - p;
                if (v.sqrMagnitude < 1e-6f) v = Vector3.forward;
                v.Normalize();
                var side = Vector3.Cross(v, Vector3.up);
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right; else side.Normalize();
                var up = Vector3.Cross(side, v).normalized;
                float radius = HeadRadius * (0.025f + 0.28f * Mathf.Pow(t, 1.2f)) / 0.305f * Mathf.Lerp(0.6f, 1f, fade);
                var c = color; c.a = Mathf.Lerp(TailAlpha, HeadAlpha, t * t) * fade;
                for (int j = 0; j < Sides; j++)
                {
                    float a = j * Mathf.PI * 2 / Sides;
                    verts[i * Sides + j] = p + (Mathf.Cos(a) * side + Mathf.Sin(a) * up) * radius;
                    colors[i * Sides + j] = c;
                }
            }
            mesh.Clear();
            mesh.vertices = verts; mesh.colors = colors; mesh.triangles = tris;
            mesh.RecalculateBounds();
        }
    }
}

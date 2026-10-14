using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Bald-base pass 2 (2026-10-12): how the racket sits in the hand, so the fingers can close round the handle with the thumb over it.
    ///
    /// The clips key Hand_Racket and Racket_Classic (and the racket's parts) every frame, so the seat is not a transform: the racket's own
    /// meshes (frame, grip, butt cap, strings, collider) move by Offset inside Racket_Classic, and the string-bed centre the gameplay uses
    /// (MatchHeroLook.stringCentreLocal, read by HeroTennisDriver's contact model) moves with them. The visible racket stays the gameplay
    /// racket; the hand, arm, swings and their timing are untouched. Offsets are racket-local metres, before the 1.3x match scale.
    public static class HeroRacketSeat
    {
        public static Vector3 Offset(bool female) => female ? new Vector3(-.005f, 0, .005f) : new Vector3(.015f, 0, -.015f);
        static readonly Dictionary<(Mesh, Vector3), Mesh> cache = new();

        public static void Apply(MatchHeroLook hero)
        {
            if (!hero || hero.golfKit || !hero.racketGrip) return;
            var offset = Offset(hero.female);
            if (offset.sqrMagnitude < 1e-12f) return;
            bool frameMoved = false;
            foreach (var filter in hero.racketGrip.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh; if (!mesh || !mesh.isReadable) continue;
                // the offset in this part's own space (its transform under the racket is keyed constant by the clips)
                var local = filter.transform.InverseTransformVector(hero.racketGrip.TransformVector(offset));
                var key = (mesh, new Vector3(Mathf.Round(local.x * 1e5f), Mathf.Round(local.y * 1e5f), Mathf.Round(local.z * 1e5f)));
                if (!cache.TryGetValue(key, out var seated) || !seated)
                {
                    seated = Object.Instantiate(mesh); seated.name = mesh.name + " (seated)"; seated.hideFlags = HideFlags.DontSave;
                    var v = mesh.vertices; for (int i = 0; i < v.Length; i++) v[i] += local;
                    seated.vertices = v; seated.RecalculateBounds(); cache[key] = seated;
                }
                filter.sharedMesh = seated; if (filter.transform == hero.racketGrip) frameMoved = true;
                if (filter.TryGetComponent<MeshCollider>(out var collider) && collider.sharedMesh == mesh) collider.sharedMesh = seated;
            }
            if (frameMoved) hero.stringCentreLocal += offset;   // only if the strings really moved: visible racket = gameplay racket
        }
    }
}

using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Small behaviours for the venues' scenery: things that follow the camera, face it, drift,
    /// bob, pulse or blink. Nothing here touches play.
    public static class TennisVenueFx
    {
        static Camera cam;
        static Camera Cam { get { if (!cam || !cam.isActiveAndEnabled) cam = Camera.main; return cam; } }

        /// Keeps a sky element centred on the camera so it never gets closer (or runs out).
        public sealed class FollowCamera : MonoBehaviour
        {
            public Vector3 Offset;
            void LateUpdate() { var c = Cam; if (c) transform.position = c.transform.position + Offset; }
        }

        /// A sprite that turns to the camera; yaw only for clouds that stand on the sea.
        public sealed class FaceCamera : MonoBehaviour
        {
            public bool YawOnly = true;
            void LateUpdate()
            {
                var c = Cam; if (!c) return;
                Vector3 d = transform.position - c.transform.position;
                if (YawOnly) d.y = 0;
                if (d.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(d.normalized);
            }
        }

        /// A wisp of cloud drifting past the deck, wrapping round so there are always some.
        public sealed class Drift : MonoBehaviour
        {
            public Vector3 Velocity; public float Wrap = 400;
            void Update()
            {
                var p = transform.localPosition + Velocity * Time.deltaTime;
                if (p.x < -Wrap) p.x = Wrap; else if (p.x > Wrap) p.x = -Wrap;
                if (p.z < -Wrap) p.z = Wrap; else if (p.z > Wrap) p.z = -Wrap;
                transform.localPosition = p;
            }
        }

        /// The aircraft-warning light on the mast.
        public sealed class Blink : MonoBehaviour
        {
            Material mat; Color glow;
            void Start() { mat = GetComponent<Renderer>().material; glow = mat.GetColor("_EmissionColor"); }
            void Update() { if (mat) mat.SetColor("_EmissionColor", glow * (Mathf.Repeat(Time.time, 1.6f) < .35f ? 1 : .08f)); }
        }

        /// Cracks that breathe.
        public sealed class Pulse : MonoBehaviour
        {
            public Material Material; Color glow;
            void Start() { if (Material) glow = Material.GetColor("_EmissionColor"); }
            void Update() { if (Material) Material.SetColor("_EmissionColor", glow * (.82f + .28f * Mathf.Sin(Time.time * 1.7f) * Mathf.Sin(Time.time * .63f + 1))); }
        }

        /// The lava lake: the crust crawls and the fire under it flares.
        public sealed class LavaFlow : MonoBehaviour
        {
            public Material Material; Color glow;
            void Start() { if (Material) glow = Material.GetColor("_EmissionColor"); }
            void Update()
            {
                if (!Material) return;
                var o = new Vector2(Time.time * .006f, Time.time * .0035f);
                Material.SetTextureOffset("_BaseMap", o); Material.SetTextureOffset("_EmissionMap", o);
                Material.SetColor("_EmissionColor", glow * (.9f + .18f * Mathf.Sin(Time.time * .9f)));
            }
        }

        /// A lavafall: the molten stream runs down its ribbon and the fire under it flares.
        public sealed class FallFlow : MonoBehaviour
        {
            public Material Material; Color glow;
            void Start() { if (Material) glow = Material.GetColor("_EmissionColor"); }
            void Update()
            {
                if (!Material) return;
                var o = new Vector2(0, Time.time * .22f);       // texture content slides toward lower v: down the slope
                Material.SetTextureOffset("_BaseMap", o); Material.SetTextureOffset("_EmissionMap", o);
                Material.SetColor("_EmissionColor", glow * (.92f + .16f * Mathf.Sin(Time.time * 1.3f)));
            }
        }

        /// A rock hanging in the air round the slab: it bobs and turns slowly.
        public sealed class Bob : MonoBehaviour
        {
            public float Amplitude = .5f, Speed = .4f, Spin = 6, Phase;
            Vector3 home;
            void Start() { home = transform.localPosition; }
            void Update()
            {
                transform.localPosition = home + Vector3.up * (Mathf.Sin(Time.time * Speed * 2 + Phase) * Amplitude);
                transform.Rotate(Vector3.up, Spin * Time.deltaTime, Space.World);
            }
        }

        /// A windsock streaming in a shifting breeze (swings about the pole, a little droop).
        public sealed class Sway : MonoBehaviour
        {
            Quaternion home;
            void Start() { home = transform.localRotation; }
            void Update()
            {
                float t = Time.time;
                float yaw = Mathf.Sin(t * .55f) * 24f + Mathf.Sin(t * 1.7f + 1f) * 7f, droop = 10f + Mathf.Sin(t * 2.3f) * 5f;
                transform.localRotation = Quaternion.Euler(0, yaw, 0) * home * Quaternion.Euler(0, 0, droop);
            }
        }

        /// Brazier fire.
        public sealed class Flicker : MonoBehaviour
        {
            Vector3 home; float seed;
            void Start() { home = transform.localScale; seed = Random.value * 10; }
            void Update()
            {
                float n = Mathf.PerlinNoise(Time.time * 6, seed);
                transform.localScale = new Vector3(home.x * (.9f + .2f * n), home.y * (.8f + .45f * n), 1);
            }
        }
    }
}

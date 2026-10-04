using UnityEngine;

namespace GolfArcade.Game
{
    /// The menus' stage (Adnan's clubhouse, his ClubBackdrop and the lit disc his player stands on): while it
    /// is up the game camera draws only the golfer, on a lit disc at their feet, in front of a painted scene
    /// (Resources/Club/scene-*.jpg) on a quad far behind them, fitted to the screen. The course is left where
    /// it is, unseen. The golfer stays the live figure: every look shows at once, and nothing is rendered twice.
    public sealed class ClubStage : MonoBehaviour
    {
        /// The layer the golfer is moved to while on stage; the camera draws only it.
        public const int Layer = 30;

        Camera cam;
        Transform subject;
        /// Anything else to show on the stage (the ball on the locker's GEAR tab), or null.
        public Transform Extra;
        readonly System.Collections.Generic.Dictionary<GameObject, int> savedLayers = new();
        GameObject quad, disc;
        Material quadMaterial, discMaterial, studioMaterial;
        int savedMask; CameraClearFlags savedClear; Color savedBackground;
        float focus = 0.5f;
        public bool Active { get; private set; }
        /// The scene on show ("home", "locker"), or null.
        public string Scene { get; private set; }

        /// Up (or changed): `scene` behind `golfer`, `tint` of lagoon over it, the crop of a wide scene centred
        /// at `focus` across it (0 its left edge, 1 its right).
        public void Show(Camera camera, Transform golfer, string scene, float tint = 0.35f, float focusX = 0.5f)
        {
            if (!Active)
            {
                cam = camera;
                savedMask = cam.cullingMask; savedClear = cam.clearFlags; savedBackground = cam.backgroundColor;
            }
            subject = golfer; focus = focusX; Scene = scene;
            Build();
            // "studio": a plain soft gradient and a soft shadow under the feet (the golfer picker); anything else is one of the
            // clubhouse's painted scenes with the lit disc
            bool studio = scene == "studio";
            var quadRenderer = quad.GetComponent<MeshRenderer>();
            if (studio)
            {
                studioMaterial ??= new Material(Shader.Find("Unlit/Transparent")) { mainTexture = StudioTexture() };
                quadRenderer.sharedMaterial = studioMaterial;
            }
            else
            {
                quadRenderer.sharedMaterial = quadMaterial;
                quadMaterial.mainTexture = UI.Club.Scene(scene);
                quadMaterial.SetFloat("_Tint", tint);
            }
            discMaterial.mainTexture = studio ? ShadowTexture() : DiscTexture();
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = UI.Club.LagoonDeep;
            quad.SetActive(true); disc.SetActive(true);
            Active = true;
            LateUpdate();
        }

        public void Hide()
        {
            if (!Active) return;
            Active = false; Scene = null;
            if (cam) { cam.cullingMask = savedMask; cam.clearFlags = savedClear; cam.backgroundColor = savedBackground; }
            if (quad) quad.SetActive(false);
            if (disc) disc.SetActive(false);
            foreach (var (go, layer) in savedLayers) if (go) go.layer = layer;
            savedLayers.Clear();
            Extra = null;
        }

        void Build()
        {
            if (!quad)
            {
                quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Club backdrop";
                Destroy(quad.GetComponent<Collider>());
                quad.layer = Layer;
                quadMaterial = new Material(Shader.Find("GolfArcade/ClubBackdrop"));
                var r = quad.GetComponent<MeshRenderer>();
                r.sharedMaterial = quadMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            if (!disc)
            {
                disc = GameObject.CreatePrimitive(PrimitiveType.Quad);
                disc.name = "Club disc";
                Destroy(disc.GetComponent<Collider>());
                disc.layer = Layer;
                discMaterial = new Material(Shader.Find("GolfArcade/ClubDisc")) { mainTexture = DiscTexture() };
                var r = disc.GetComponent<MeshRenderer>();
                r.sharedMaterial = discMaterial; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
            quad.transform.SetParent(cam.transform, false);
        }

        void LateUpdate()
        {
            if (!Active || !cam) return;
            // the painting: square to the camera, far behind the golfer, filling the screen
            float d = Mathf.Min(cam.farClipPlane * 0.8f, 400f);
            float h = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            quad.transform.localPosition = new Vector3(0, 0, d);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(h * cam.aspect, h, 1);
            bool studio = Scene == "studio";
            if (!studio)
            {
                // cover: a wide scene cropped to the screen, about `focus`
                var tex = quadMaterial.mainTexture;
                float ta = tex ? tex.width / (float)tex.height : 16f / 9f, a = cam.aspect;
                var cover = a < ta ? new Vector4(a / ta, 1, 0, 0) : new Vector4(1, ta / a, 0, 0);
                float room = (1 - cover.x) / 2;
                cover.z = Mathf.Clamp(focus - 0.5f, -room, room);
                quadMaterial.SetVector("_Cover", cover);
            }
            if (!subject) return;
            OnStage(subject);
            if (Extra) OnStage(Extra);
            // the lit disc at their feet
            disc.transform.position = subject.position + Vector3.up * 0.02f;
            disc.transform.rotation = Quaternion.Euler(90, 0, 0);
            disc.transform.localScale = Vector3.one * (studio ? 1.7f : 2.4f);
        }

        /// Onto the stage's layer, remembering each object's own to put back.
        void OnStage(Transform t)
        {
            foreach (var c in t.GetComponentsInChildren<Transform>(true))
            {
                var go = c.gameObject;
                if (go.layer == Layer) continue;
                savedLayers[go] = go.layer; go.layer = Layer;
            }
        }

        static Texture2D studioTexture, shadowTexture;
        /// The studio: a soft gradient, light at the top.
        static Texture2D StudioTexture()
        {
            if (studioTexture) return studioTexture;
            const int n = 256;
            studioTexture = new Texture2D(4, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var top = new Color(0.969f, 0.961f, 0.992f); var bottom = new Color(0.875f, 0.863f, 0.953f);
            for (int y = 0; y < n; y++)
            {
                var c = Color.Lerp(bottom, top, Mathf.SmoothStep(0, 1, y / (n - 1f)));
                for (int x = 0; x < 4; x++) studioTexture.SetPixel(x, y, c);
            }
            studioTexture.Apply();
            return studioTexture;
        }

        /// A soft shadow under the feet: dark in the middle, fading out.
        static Texture2D ShadowTexture()
        {
            if (shadowTexture) return shadowTexture;
            const int n = 128;
            shadowTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1 - r); a = a * a * (3 - 2 * a) * 0.38f;
                    px[y * n + x] = new Color32(52, 44, 96, (byte)(255 * a));
                }
            shadowTexture.SetPixels32(px); shadowTexture.Apply();
            return shadowTexture;
        }

        static Texture2D discTexture;
        /// His stage: a sun glow fading out, with a white ring round the feet.
        static Texture2D DiscTexture()
        {
            if (discTexture) return discTexture;
            const int n = 256;
            discTexture = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            var sun = UI.Club.Sun;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
                    float glow = Mathf.Clamp01(1 - r); glow *= glow * 0.8f;
                    float ring = Mathf.Clamp01(1 - Mathf.Abs(r - 0.6f) / 0.02f) * 0.7f;
                    var c = Color.Lerp(new Color(sun.r, sun.g, sun.b, glow), new Color(1, 1, 1, 1), ring / Mathf.Max(0.001f, ring + glow));
                    c.a = Mathf.Clamp01(glow + ring);
                    px[y * n + x] = c;
                }
            discTexture.SetPixels32(px); discTexture.Apply();
            return discTexture;
        }

        void OnDestroy() { if (quadMaterial) Destroy(quadMaterial); if (discMaterial) Destroy(discMaterial); if (studioMaterial) Destroy(studioMaterial); }
    }
}

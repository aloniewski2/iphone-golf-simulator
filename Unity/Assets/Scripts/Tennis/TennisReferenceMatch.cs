using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GolfArcade.Tennis
{
    /// Reference-matching stage, used only by Assets/Scenes/TennisReference.unity: the real
    /// game (TennisGame builds the same resort, characters and lighting as in play) frozen in
    /// one static moment and framed like frame 87 (t = 3.58 s) of the approved concept video
    /// (SportsLibrary/ArtDirection/Tennis/Generated/video). Everything here is applied to this
    /// scene's own instances -- no shared asset, material or gameplay value is changed -- so
    /// the look can be tuned against the target before anything is carried into the game.
    [DefaultExecutionOrder(30000)]
    public sealed class TennisReferenceMatch : MonoBehaviour
    {
        [Header("Camera (solved from the court lines in the reference frame)")]
        // Solved from the reference frame's court lines against a regulation court.
        public Vector3 CameraPosition = new Vector3(0, 4.3f, -23f);
        public float Pitch = 10.5f;                  // degrees down
        public float Yaw = 0f;
        public float VerticalFieldOfView = 32f;

        [Header("Staging")]
        public Vector3 PlayerPosition = new Vector3(-.3f, .035f, -13.5f);
        public Vector3 OpponentPosition = new Vector3(-2.6f, .035f, 12f);
        public float PlayerScale = 1f;               // the video's characters read larger than ours
        public float IdlePhase = .3f;                // seconds of Ready idle to settle into
        public bool HideHud = true;
        public bool HideBall = true;

        [System.Serializable]
        public struct Recolour { public string Material; public Color BaseColor; }

        [Header("Surfaces (pass 2) -- scene-owned material copies; the assets are untouched")]
        public Recolour[] Surfaces =
        {
            // "Court | sapphire acrylic": the reference court is a pale cornflower blue.
            new Recolour { Material = "TropicalV3_002", BaseColor = new Color32(101, 137, 180, 255) },
            // "Runoff | ocean teal": the reference surround is warm tan decking.
            new Recolour { Material = "TropicalV3_011", BaseColor = new Color32(176, 126, 92, 255) },
        };

        [Header("Light (pass 3) -- estimated from the reference: high, front-right, soft, pastel")]
        public Vector3 SunToward = new Vector3(.45f, .75f, .48f);   // direction to the sun
        public Color SunColor = new Color(1f, .96f, .90f);
        public float SunIntensity = 2.0f;
        public float ShadowStrength = .55f;
        public Color AmbientSky = new Color(.72f, .84f, 1f), AmbientEquator = new Color(.92f, .86f, .78f), AmbientGround = new Color(.62f, .52f, .42f);
        public float PostExposure = .35f, Contrast = -12f, Saturation = 0f, BloomIntensity = .55f;

        TennisGame game;
        bool staged;

        void Awake() { game = GetComponent<TennisGame>(); if (game) game.ManualSimulation = true; }

        void LateUpdate()
        {
            if (!game || !game.Player || !game.GameplayCamera) return;
            if (!staged)
            {
                ApplySurfaces();
                ApplyLight();
                // Let the idle cycle settle into a natural ready stance once.
                for (float t = 0; t < IdlePhase; t += 1f / 60) { Stage(); game.Player.Tick(1f / 60, 0); game.Opponent.Tick(1f / 60, 0); }
                staged = true;
            }
            Stage();
            var cam = game.GameplayCamera;
            cam.transform.SetPositionAndRotation(CameraPosition, Quaternion.Euler(Pitch, Yaw, 0));
            cam.fieldOfView = VerticalFieldOfView;
            if (HideHud)
                foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.enabled = false;
            if (HideBall)
            {
                var ball = GameObject.Find("Tennis ball");
                if (ball) foreach (var r in ball.GetComponentsInChildren<Renderer>()) r.enabled = false;
            }
        }

        void ApplySurfaces()
        {
            var copies = new System.Collections.Generic.Dictionary<Material, Material>();
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                var shared = renderer.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < shared.Length; i++)
                {
                    var m = shared[i];
                    if (!m) continue;
                    foreach (var r in Surfaces)
                    {
                        if (!m.name.StartsWith(r.Material)) continue;
                        if (!copies.TryGetValue(m, out var copy))
                        {
                            copy = new Material(m) { name = m.name + " (reference)" };
                            if (copy.HasProperty("_BaseColor")) copy.SetColor("_BaseColor", r.BaseColor);
                            if (copy.HasProperty("_Color")) copy.SetColor("_Color", r.BaseColor);
                            copies[m] = copy;
                        }
                        shared[i] = copy; changed = true;
                    }
                }
                if (changed) renderer.sharedMaterials = shared;
            }
            Debug.Log($"REFERENCE recoloured {copies.Count} materials");
        }

        void ApplyLight()
        {
            var sun = RenderSettings.sun;
            Vector3 toward = SunToward.normalized;
            if (sun)
            {
                sun.transform.rotation = Quaternion.LookRotation(-toward);
                sun.color = SunColor; sun.intensity = SunIntensity; sun.shadowStrength = ShadowStrength;
            }
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = AmbientSky; RenderSettings.ambientEquatorColor = AmbientEquator; RenderSettings.ambientGroundColor = AmbientGround;
            if (RenderSettings.skybox && RenderSettings.skybox.HasProperty("_SunDir"))
            {
                var sky = new Material(RenderSettings.skybox) { name = "Painted sky (reference)" };
                sky.SetVector("_SunDir", toward); RenderSettings.skybox = sky;
            }
            // The game's post volume is already a private copy; adjust it here.
            foreach (var volume in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (!volume.profile) continue;
                if (volume.profile.TryGet(out ColorAdjustments colour))
                {
                    colour.postExposure.Override(colour.postExposure.value + PostExposure);
                    colour.contrast.Override(Contrast); colour.saturation.Override(Saturation);
                }
                if (volume.profile.TryGet(out Bloom bloom)) bloom.intensity.Override(BloomIntensity);
            }
            DynamicGI.UpdateEnvironment();
        }

        void Stage()
        {
            game.Player.transform.SetPositionAndRotation(PlayerPosition, Quaternion.identity);
            game.Player.transform.localScale = Vector3.one * PlayerScale;
            game.Opponent.transform.SetPositionAndRotation(OpponentPosition, Quaternion.Euler(0, 180, 0));
            game.Opponent.transform.localScale = Vector3.one * PlayerScale;
        }
    }
}

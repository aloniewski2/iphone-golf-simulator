using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// Bottom-right coaching panel: where on the string bed each ball actually struck.
    ///
    /// The point is to make a miss diagnosable. "OFF CENTER" as text tells the player nothing
    /// actionable; a dot low and to the left of the sweet spot tells them exactly what to fix.
    /// Recent hits persist as a fading trail so a pattern is visible rather than one sample.
    ///
    /// All artwork is generated at runtime, so the panel needs no imported sprites.
    public sealed class TennisHitMap : MonoBehaviour
    {
        const int History = 12;
        const float PanelSize = 190f, DotSize = 13f;

        readonly List<Image> dots = new();
        readonly List<float> stamps = new();
        RectTransform face;
        Text caption;
        static Sprite ring, disc;

        /// A hollow ellipse for the string bed and a filled circle for each impact.
        static Sprite Ellipse(bool filled, int size = 128)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float r = size * .5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    // Slightly taller than wide, like a real racket head.
                    float dx = (x + .5f - r) / (r * .82f), dy = (y + .5f - r) / r;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = filled
                        ? Mathf.Clamp01((1f - d) * size * .25f)
                        : Mathf.Clamp01((1f - Mathf.Abs(d - .94f) * 26f));
                    pixels[y * size + x] = new Color(1, 1, 1, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
        }

        public static Color GradeColour(Timing grade) => grade switch
        {
            Timing.Perfect => new Color(.35f, 1f, .55f),
            Timing.Excellent => new Color(.55f, 1f, .4f),
            Timing.Great => new Color(1f, .93f, .35f),
            Timing.Good => new Color(1f, .68f, .25f),
            _ => new Color(1f, .38f, .3f),
        };

        public void Build(Canvas canvas)
        {
            ring ??= Ellipse(false);
            disc ??= Ellipse(true, 64);

            var panel = new GameObject("Impact map").AddComponent<RectTransform>();
            panel.SetParent(canvas.transform, false);
            panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
            panel.sizeDelta = new Vector2(PanelSize, PanelSize + 34);
            panel.anchoredPosition = new Vector2(-22, 18);

            // Solid dark backing: the court is bright and the panel was washing out against
            // it, which defeats the point of a diagnostic you glance at mid-rally.
            var backing = new GameObject("Backing").AddComponent<Image>();
            backing.transform.SetParent(panel, false);
            backing.color = new Color(.04f, .07f, .11f, .55f);
            var backRect = backing.rectTransform;
            backRect.anchorMin = Vector2.zero; backRect.anchorMax = Vector2.one;
            backRect.offsetMin = new Vector2(-10, -8); backRect.offsetMax = new Vector2(10, 4);

            var strings = new GameObject("String bed").AddComponent<Image>();
            strings.transform.SetParent(panel, false);
            strings.sprite = ring;
            strings.color = new Color(1, 1, 1, .9f);
            var stringRect = strings.rectTransform;
            stringRect.anchorMin = stringRect.anchorMax = stringRect.pivot = new Vector2(.5f, 0);
            stringRect.sizeDelta = new Vector2(PanelSize, PanelSize);
            stringRect.anchoredPosition = Vector2.zero;
            face = stringRect;

            // A small marker for the dead centre the player is aiming to find.
            var centre = new GameObject("Sweet spot").AddComponent<Image>();
            centre.transform.SetParent(face, false);
            centre.sprite = disc;
            centre.color = new Color(1, 1, 1, .4f);
            centre.rectTransform.sizeDelta = new Vector2(DotSize * 2.6f, DotSize * 2.6f);
            centre.rectTransform.anchoredPosition = Vector2.zero;

            caption = new GameObject("Caption").AddComponent<Text>();
            caption.transform.SetParent(panel, false);
            caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 16;
            caption.alignment = TextAnchor.LowerCenter;
            caption.color = Color.white;
            caption.text = "CONTACT MAP";
            var capRect = caption.rectTransform;
            capRect.anchorMin = capRect.anchorMax = capRect.pivot = new Vector2(.5f, 1);
            capRect.sizeDelta = new Vector2(PanelSize + 40, 32);
            capRect.anchoredPosition = Vector2.zero;
            caption.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1, -1);

            for (int i = 0; i < History; i++)
            {
                var dot = new GameObject($"Impact {i}").AddComponent<Image>();
                dot.transform.SetParent(face, false);
                dot.sprite = disc;
                dot.rectTransform.sizeDelta = new Vector2(DotSize, DotSize);
                dot.enabled = false;
                dots.Add(dot);
                stamps.Add(-99);
            }
        }

        /// Plot one contact. `offset` is metres across and up the string bed from its centre.
        public void Record(Vector2 offset, Timing grade, bool supercharged)
        {
            if (!face) return;
            // Oldest slot gets reused, so the trail is always the most recent History hits.
            int slot = 0;
            for (int i = 1; i < stamps.Count; i++) if (stamps[i] < stamps[slot]) slot = i;
            stamps[slot] = Time.unscaledTime;

            float half = PanelSize * .5f;
            var dot = dots[slot];
            dot.enabled = true;
            dot.color = supercharged ? new Color(.45f, .95f, 1f) : GradeColour(grade);
            dot.canvasRenderer.SetAlpha(1);
            dot.rectTransform.sizeDelta = new Vector2(DotSize, DotSize) * (supercharged ? 1.5f : 1f);
            // Clamp to the panel so an edge-of-frame mishit is still visible at the rim.
            dot.rectTransform.anchoredPosition = new Vector2(
                Mathf.Clamp(offset.x / TennisRules.StringHalfWidth, -1.08f, 1.08f) * half * .82f,
                Mathf.Clamp(offset.y / TennisRules.StringHalfHeight, -1.08f, 1.08f) * half);

            float error = TennisRules.FaceError(offset);
            caption.text = error < .32f ? "CONTACT MAP · middle of the strings"
                : $"CONTACT MAP · {(Mathf.Abs(offset.y) > Mathf.Abs(offset.x) ? (offset.y > 0 ? "high" : "low") : (offset.x > 0 ? "outside" : "inside"))} by {error:P0}";
        }

        void Update()
        {
            // Fade the trail so the newest contact reads strongest. Alpha goes through the
            // canvas renderer, which does not rebuild the UI mesh; setting `color` every frame
            // forced a full canvas rebuild every frame.
            for (int i = 0; i < dots.Count; i++)
            {
                if (!dots[i].enabled) continue;
                float alpha = Mathf.Clamp01(1 - (Time.unscaledTime - stamps[i]) / 9f);
                if (alpha <= 0) dots[i].enabled = false; else dots[i].canvasRenderer.SetAlpha(alpha);
            }
        }
    }
}

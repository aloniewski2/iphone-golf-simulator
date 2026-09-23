using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// Bottom-right coaching instrument: a racket, and where on its strings each ball struck.
    ///
    /// The point is to make a miss diagnosable. "OFF CENTER" as text tells the player nothing
    /// actionable; a dot low and to the left of the sweet spot tells them exactly what to fix.
    /// Recent hits persist as a fading trail so a pattern is visible rather than one sample.
    ///
    /// The racket is Higgsfield artwork (Resources/Tennis/UI/racket-map.png). Its string bed
    /// was measured from the image: centred at 25.5% from the top, 43% of the width and 23.2%
    /// of the height from centre to the inside of the frame.
    public sealed class TennisHitMap : MonoBehaviour
    {
        const int History = 12;
        const float RacketHeight = 250f, DotSize = 11f, Tilt = -14f;
        const float BedCentre = .255f, BedHalfWidth = .43f, BedHalfHeight = .232f;

        readonly List<Image> dots = new();
        readonly List<float> stamps = new();
        RectTransform face;
        Text caption;
        Vector2 half;
        static Sprite disc;

        static Sprite Disc(int size = 64)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float r = size * .5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Sqrt((x + .5f - r) * (x + .5f - r) + (y + .5f - r) * (y + .5f - r)) / r;
                    pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01((1f - d) * size * .25f));
                }
            texture.SetPixels(pixels); texture.Apply();
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

        public void Build(Canvas canvas, RectTransform parent = null)
        {
            var into = parent ? parent : (RectTransform)canvas.transform;
            disc ??= Disc();
            var art = Resources.Load<Texture2D>("Tennis/UI/racket-map");
            float aspect = art ? art.width / (float)art.height : .386f;

            var racket = new GameObject("Contact racket").AddComponent<RectTransform>();
            racket.SetParent(into, false);
            racket.anchorMin = racket.anchorMax = new Vector2(1, 0);
            racket.pivot = new Vector2(.5f, 0);
            racket.sizeDelta = new Vector2(RacketHeight * aspect, RacketHeight);
            racket.anchoredPosition = new Vector2(-84, 22);
            racket.localRotation = Quaternion.Euler(0, 0, Tilt);
            half = new Vector2(RacketHeight * aspect * BedHalfWidth, RacketHeight * BedHalfHeight);
            var bedCentre = new Vector2(0, RacketHeight * (1 - BedCentre));

            // A dark string bed behind the (translucent) strings, so dots read on a bright court.
            var bed = new GameObject("String bed").AddComponent<Image>();
            bed.transform.SetParent(racket, false);
            bed.sprite = disc; bed.color = new Color(.05f, .10f, .20f, .62f); bed.raycastTarget = false;
            var bedRect = bed.rectTransform;
            bedRect.anchorMin = bedRect.anchorMax = new Vector2(.5f, 0);
            bedRect.sizeDelta = half * 2.02f; bedRect.anchoredPosition = bedCentre;

            if (art)
            {
                var frame = new GameObject("Racket art").AddComponent<Image>();
                frame.transform.SetParent(racket, false); frame.raycastTarget = false;
                frame.sprite = Sprite.Create(art, new Rect(0, 0, art.width, art.height), new Vector2(.5f, .5f), 100);
                var fr = frame.rectTransform; fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            }

            var faceRect = new GameObject("Face").AddComponent<RectTransform>();
            faceRect.SetParent(racket, false);
            faceRect.anchorMin = faceRect.anchorMax = new Vector2(.5f, 0);
            faceRect.sizeDelta = half * 2; faceRect.anchoredPosition = bedCentre;
            face = faceRect;

            // The dead centre the player is looking for.
            var centre = new GameObject("Sweet spot").AddComponent<Image>();
            centre.transform.SetParent(face, false);
            centre.sprite = disc; centre.color = new Color(1f, .84f, .25f, .35f); centre.raycastTarget = false;
            centre.rectTransform.sizeDelta = new Vector2(DotSize * 2.8f, DotSize * 2.8f);

            caption = new GameObject("Caption").AddComponent<Text>();
            caption.transform.SetParent(into, false);
            caption.font = Resources.Load<Font>("Tennis/UI/Fonts/Rubik-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 14; caption.raycastTarget = false;
            caption.alignment = TextAnchor.LowerRight;
            caption.color = new Color(1, 1, 1, .9f);
            caption.text = "CONTACT";
            var capRect = caption.rectTransform;
            capRect.anchorMin = capRect.anchorMax = capRect.pivot = new Vector2(1, 0);
            capRect.sizeDelta = new Vector2(260, 20);
            capRect.anchoredPosition = new Vector2(-24, 6);
            foreach (var d in new[] { new Vector2(1.6f, -1.6f), new Vector2(-1.6f, 1.6f) }) { var o = caption.gameObject.AddComponent<Outline>(); o.effectColor = TennisHud.Navy; o.effectDistance = d; }

            for (int i = 0; i < History; i++)
            {
                var dot = new GameObject($"Impact {i}").AddComponent<Image>();
                dot.transform.SetParent(face, false);
                dot.sprite = disc; dot.raycastTarget = false;
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
            stamps[slot] = HudClock.Now;

            var dot = dots[slot];
            dot.enabled = true;
            dot.color = supercharged ? new Color(.45f, .95f, 1f) : GradeColour(grade);
            dot.canvasRenderer.SetAlpha(1);
            dot.rectTransform.sizeDelta = new Vector2(DotSize, DotSize) * (supercharged ? 1.5f : 1f);
            // Clamp to the panel so an edge-of-frame mishit is still visible at the rim.
            dot.rectTransform.anchoredPosition = new Vector2(
                Mathf.Clamp(offset.x / TennisRules.StringHalfWidth, -1.08f, 1.08f) * half.x,
                Mathf.Clamp(offset.y / TennisRules.StringHalfHeight, -1.08f, 1.08f) * half.y);

            float error = TennisRules.FaceError(offset);
            caption.text = error < .32f ? "CONTACT · SWEET SPOT"
                : $"CONTACT · {(Mathf.Abs(offset.y) > Mathf.Abs(offset.x) ? (offset.y > 0 ? "HIGH" : "LOW") : (offset.x > 0 ? "OUTSIDE" : "INSIDE"))}";
        }

        void Update()
        {
            // Fade the trail so the newest contact reads strongest. Alpha goes through the
            // canvas renderer, which does not rebuild the UI mesh; setting `color` every frame
            // forced a full canvas rebuild every frame.
            for (int i = 0; i < dots.Count; i++)
            {
                if (!dots[i].enabled) continue;
                float alpha = Mathf.Clamp01(1 - (HudClock.Now - stamps[i]) / 9f);
                if (alpha <= 0) dots[i].enabled = false; else dots[i].canvasRenderer.SetAlpha(alpha);
            }
        }
    }
}

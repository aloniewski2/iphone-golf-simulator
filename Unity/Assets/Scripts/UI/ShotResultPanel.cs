using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The swing HUD continues into the result, with a small strip of emote/continue controls.
    public sealed class ShotResultPanel : MonoBehaviour
    {
        RectTransform summary;
        Text quality, distance, carry;
        RectTransform footer, remaining;
        Text detail;
        Camera view;
        CanvasScaler scaler;
        HoldButton next;
        readonly HoldButton[] emotes = new HoldButton[3];

        void LateUpdate()
        {
            bool portrait = view.aspect < 1.2f;
            scaler.referenceResolution = portrait ? new Vector2(1080, 1920) : new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = portrait ? 0 : 1;
            float scale = portrait ? .8f : 1;
            summary.localScale = Vector3.one * scale;
            remaining.localScale = Vector3.one * scale;
            remaining.anchoredPosition = new Vector2(-36, -36 - (292 + 16) * scale);
            footer.anchorMin = footer.anchorMax = portrait ? new Vector2(.5f, 0) : new Vector2(1, 0);
            footer.pivot = portrait ? new Vector2(.5f, 0) : new Vector2(1, 0);
            footer.anchoredPosition = portrait ? new Vector2(0, 52) : new Vector2(-36, 32);
            footer.localScale = Vector3.one * (portrait ? 1.9f : 1);
        }

        public static ShotResultPanel Create(Transform parent, Camera camera)
        {
            var go = new GameObject("Shot result", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = UiKit.Canvas(go);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera; canvas.planeDistance = .5f;
            canvas.sortingOrder = 50; canvas.targetDisplay = camera.targetDisplay;
            var c = go.AddComponent<ShotResultPanel>();
            c.view = camera; c.scaler = go.GetComponent<CanvasScaler>();
            c.summary = GolfShotHud.Panel(go.transform, "Shot summary", new Vector2(1,1), new Vector2(-36,-36), new Vector2(440,292));
            c.quality = GolfShotHud.Label(c.summary,"Contact",28,new Vector2(28,-26),new Vector2(384,42));
            c.quality.color = GolfShotHud.Mint;
            c.distance = GolfShotHud.Label(c.summary,"Distance",66,new Vector2(28,-84),new Vector2(384,88));
            c.carry = GolfShotHud.Label(c.summary,"Carry",26,new Vector2(28,-204),new Vector2(384,62));
            c.remaining = UiKit.Pill(go.transform, "Remaining distance", GolfShotHud.Ink, new Vector2(1, 1), Vector2.zero,
                new Vector2(440, 68), out var detailFill, 4).GetComponent<RectTransform>();
            c.remaining.pivot = new Vector2(1, 1);
            c.detail = UiKit.Label(detailFill.transform, "Remaining", 26, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, UiKit.Strong, false);
            c.detail.color = Color.white;
            c.detail.rectTransform.offsetMin = new Vector2(14, 0); c.detail.rectTransform.offsetMax = new Vector2(-14, 0);
            Icons.Fit(c.detail, 18, 26);
            c.footer = new GameObject("Shot actions", typeof(RectTransform)).GetComponent<RectTransform>();
            c.footer.SetParent(go.transform, false); c.footer.sizeDelta = new Vector2(440, 216);
            var caption = UiKit.Label(c.footer, "Emote prompt", 20, TextAnchor.MiddleCenter, new Vector2(.5f, 1), new Vector2(.5f, 1),
                Vector2.zero, new Vector2(440, 30), UiKit.Strong);
            caption.text = "CHOOSE AN EMOTE";
            for (int i = 0; i < 3; i++)
            {
                c.emotes[i] = UiKit.Button(c.footer, "EMOTE", new Vector2(.5f, 0), new Vector2((i - 1) * 150, 132), new Vector2(140, 72), 21, GolfShotHud.Mint);
                c.emotes[i].GetComponentInChildren<Text>().color = UiKit.ArcadeInk;
            }
            c.next = UiKit.Button(c.footer, "NEXT SHOT", new Vector2(.5f, 0), new Vector2(0, 40), new Vector2(440, 80), 28, GolfShotHud.Ink);
            go.SetActive(false);
            return c;
        }

        public void Show(string grade, Color gradeColor, string outcome, (string icon, string title, string value)[] tiles,
            string remainingText, IList<Vector2> path, float reach, float target, string nextLabel, string[] emoteLabels, Action proceed, Func<int, bool> emote)
        {
            gameObject.SetActive(true); LateUpdate();
            quality.text = grade.TrimEnd('!') + " CONTACT";
            distance.text = tiles.Length > 5 ? tiles[5].value : "—";
            carry.text = $"CARRY  {(tiles.Length > 4 ? tiles[4].value : "—")}\n{outcome.ToUpperInvariant()}";
            detail.text = remainingText;
            next.GetComponentInChildren<Text>().text = nextLabel; next.Pressed = proceed;
            for (int i = 0; i < emotes.Length; i++)
            {
                int choice = i;
                emotes[i].GetComponentInChildren<Text>().text = emoteLabels[i].ToUpperInvariant();
                emotes[i].Pressed = () => emote(choice);
            }
        }
    }
}

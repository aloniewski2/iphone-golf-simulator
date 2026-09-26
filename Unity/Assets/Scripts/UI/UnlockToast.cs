using System.Collections.Generic;
using GolfArcade.Game;
using GolfArcade.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Something earned, announced over the round's card: a yellow medallion with the reward's
    /// icon, UNLOCKED! on a tab, the reward on a navy pill and how it was earned under it. Each
    /// pops in with a bounce, holds, and makes way for the next.
    public sealed class UnlockToast : MonoBehaviour
    {
        RectTransform root;
        Image icon;
        Text tab, title, how;
        readonly Queue<(string who, Reward reward)> waiting = new();
        float shownAt = -99f;
        const float Hold = 2.6f;

        /// What is on show now, for the tests.
        public string Showing => root && root.gameObject.activeSelf ? title.text : null;

        public static UnlockToast Create(Transform parent)
        {
            var go = new GameObject("Unlock toast", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<UnlockToast>();
            t.root = (RectTransform)go.transform;
            t.root.anchorMin = t.root.anchorMax = new Vector2(0.5f, 1f); t.root.pivot = new Vector2(0.5f, 1f);
            t.root.anchoredPosition = new Vector2(0, -230); t.root.sizeDelta = new Vector2(900, 250);

            var pill = UiKit.Pill(t.root, "Pill", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0.5f), new Vector2(50, -10), new Vector2(800, 170), out var fill, 6f, false);
            foreach (var img in pill.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;
            t.title = UiKit.Chunky(fill.transform, "Title", 56, Color.white, UiKit.ArcadeInk, 4f);
            t.title.rectTransform.offsetMin = new Vector2(150, 62); t.title.rectTransform.offsetMax = new Vector2(-24, -14);
            Icons.Fit(t.title, 30, 56);
            t.how = UiKit.Label(fill.transform, "How", 30, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1, 0), new Vector2(0, 38), new Vector2(0, 44), UiKit.Display, false);
            t.how.rectTransform.offsetMin = new Vector2(150, 16); t.how.rectTransform.offsetMax = new Vector2(-24, 60);
            t.how.color = UiKit.ArcadeYellow;
            Icons.Fit(t.how, 18, 30);

            var medallion = UiKit.Pill(t.root, "Medallion", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(-330, -10), new Vector2(190, 190), out var disc, 7f);
            foreach (var img in medallion.GetComponentsInChildren<Image>()) img.type = Image.Type.Simple;
            t.icon = Icons.Place(disc.transform, "star", UiKit.ArcadeInk, new Vector2(0.5f, 0.5f), Vector2.zero, 104);

            var tabPill = UiKit.Pill(t.root, "Tab", UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(90, 84), new Vector2(360, 62), out var tabFill, 3f);
            t.tab = UiKit.Chunky(tabFill.transform, "Word", 36, UiKit.ArcadeInk, new Color(1, 1, 1, 0), 0f);
            Icons.Fit(t.tab, 20, 36);

            foreach (var g in go.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            go.SetActive(false);
            return t;
        }

        /// Adds rewards to announce, each with whose it is (null on a phone with one player).
        public void Announce(IEnumerable<(string who, Reward reward)> rewards)
        {
            foreach (var r in rewards) waiting.Enqueue(r);
            if (!root.gameObject.activeSelf) Next();
        }

        void Next()
        {
            if (waiting.Count == 0) { root.gameObject.SetActive(false); return; }
            var (who, r) = waiting.Dequeue();
            root.gameObject.SetActive(true);
            transform.SetAsLastSibling();
            tab.text = (who == null ? "UNLOCKED!" : $"{who} UNLOCKED").ToUpperInvariant();
            title.text = Gear.Title(r).ToUpperInvariant();
            how.text = r.How.ToUpperInvariant();
            icon.sprite = Icons.Get(Gear.Icon(r));
            shownAt = Time.unscaledTime;
        }

        void Update()
        {
            float t = Time.unscaledTime - shownAt;
            if (t > Hold + 0.3f) { Next(); return; }
            float s;
            if (t < 0.32f) { float u = t / 0.32f; s = 1f - Mathf.Pow(1f - u, 3f) + 0.2f * Mathf.Sin(u * Mathf.PI); }
            else if (t < Hold) s = 1f;
            else s = Mathf.Lerp(1f, 0f, (t - Hold) / 0.3f);
            root.localScale = Vector3.one * Mathf.Max(0.01f, s);
        }
    }
}

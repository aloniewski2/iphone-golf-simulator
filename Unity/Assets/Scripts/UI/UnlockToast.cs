using System.Collections.Generic;
using GolfArcade.Game;
using GolfArcade.Profile;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Something earned, announced over the round's card, as one of Adnan's club cards: a cream slab with a
    /// sun strip, the reward's rendered object (a trophy for a course, the kit for an outfit, the driver for a
    /// ball or clubs, the bolt for a trail), UNLOCKED! on a sun badge, the reward and how it was earned. Each
    /// pops in with a bounce, holds, and makes way for the next.
    public sealed class UnlockToast : MonoBehaviour
    {
        RectTransform root;
        Club.Card card;
        Text title => card.Title;
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
            t.root.anchoredPosition = new Vector2(0, -230); t.root.sizeDelta = new Vector2(960, 250);
            t.card = new Club.Card(t.root, "Card", "trophy", "", "", Club.Sun, true, 0.95f);
            var rt = t.card.Root;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            foreach (var g in go.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            go.SetActive(false);
            return t;
        }

        static string ArtFor(Reward r) => r.Kind switch
        {
            RewardKind.Course => "trophy",
            RewardKind.Outfit => "kit",
            RewardKind.Trail => "quick",
            _ => "golf",
        };

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
            card.SetArt(ArtFor(r)).Set(Gear.Title(r), r.How).SetBadge(Club.BadgeKind.New, who == null ? "UNLOCKED!" : $"{who.ToUpperInvariant()} UNLOCKED");
            ClubSound.Play("pop", 0.55f);
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

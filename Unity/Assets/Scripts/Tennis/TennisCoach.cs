using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// First-match coaching and the end-of-match card.
    ///
    /// Coaching: short prompts at the moment each skill is first needed -- the serve, the
    /// first incoming ball, the first backhand-side ball, the first whiff -- shown once per
    /// device, then never again. Nothing blocks play; the tip sits above the court and fades.
    ///
    /// Results: the match used to restart on its own six seconds after the last point. Now
    /// a card shows the score and the rally stats, and the next match starts when the player
    /// swings (or after a long pause, so a benchmark run keeps going).
    public sealed class TennisCoach : MonoBehaviour
    {
        public enum Tip { Serve, FirstBall, Backhand, Whiff, Timing }
        const string Key = "tennis.coach.v1.";
        Text tip, resultTitle, resultBody;
        Image card;
        float tipUntil;
        public bool ShowingResults => card && card.gameObject.activeSelf;
        public int[] GradeCounts { get; } = new int[6];

        public static string TextFor(Tip t) => t switch
        {
            Tip.Serve => "SERVE: the ball is tossed for you. Swing down hard from above your head as it peaks.",
            Tip.FirstBall => "You run to the ball on your own. Just swing as it reaches you — screen toward the TV for a forehand.",
            Tip.Backhand => "Ball on your other side: turn the phone so the camera faces the TV, and swing across.",
            Tip.Whiff => "Too early or too late. Start your swing as the ball crosses the ring at your feet.",
            Tip.Timing => "Swing early to hit cross-court, late to go down the line.",
            _ => "",
        };

        public void Build(Canvas canvas)
        {
            tip = MakeText(canvas.transform, "Coaching tip", new Vector2(.5f, .78f), new Vector2(900, 90), 26);
            tip.alignment = TextAnchor.MiddleCenter; tip.color = new Color(1, .96f, .8f);
            tip.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, .75f);
            card = new GameObject("Match results").AddComponent<Image>();
            card.transform.SetParent(canvas.transform, false);
            // Same family as the score plaque: deep blue gradient, navy frame.
            card.color = Color.white; card.raycastTarget = false;
            UiGradient.On(card, new Color(.22f, .50f, .98f, .96f), new Color(.05f, .17f, .60f, .96f));
            var frame = card.gameObject.AddComponent<Outline>(); frame.effectColor = TennisHud.Navy; frame.effectDistance = new Vector2(5, -5);
            var r = card.rectTransform; r.anchorMin = r.anchorMax = new Vector2(.5f, .5f); r.sizeDelta = new Vector2(620, 330);
            resultTitle = MakeText(card.transform, "Result", new Vector2(.5f, .78f), new Vector2(600, 70), 44);
            resultTitle.alignment = TextAnchor.MiddleCenter;
            resultBody = MakeText(card.transform, "Stats", new Vector2(.5f, .38f), new Vector2(560, 200), 24);
            resultBody.alignment = TextAnchor.MiddleCenter;
            card.gameObject.SetActive(false);
        }

        static Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 size, int fontSize)
        {
            var text = new GameObject(name).AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = Resources.Load<Font>("Tennis/UI/Fonts/Rubik-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = Color.white;
            foreach (var d in new[] { new Vector2(2, -2), new Vector2(-2, 2) }) { var o = text.gameObject.AddComponent<Outline>(); o.effectColor = TennisHud.Navy; o.effectDistance = d; }
            text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -3.5f);
            text.raycastTarget = false; text.text = "";
            var rect = text.rectTransform; rect.anchorMin = rect.anchorMax = anchor; rect.sizeDelta = size;
            return text;
        }

        /// Show a tip the first time its moment comes up on this device.
        public void Offer(Tip t)
        {
            string key = Key + t;
            bool seen;
            try { seen = PlayerPrefs.GetInt(key, 0) == 1; } catch { seen = true; }
            if (seen || HudClock.Now < tipUntil - 2) return;
            try { PlayerPrefs.SetInt(key, 1); PlayerPrefs.Save(); } catch { }
            tip.text = TextFor(t); tipUntil = HudClock.Now + 5;
        }

        public static void ResetTips() { foreach (Tip t in System.Enum.GetValues(typeof(Tip))) PlayerPrefs.DeleteKey(Key + t); }

        public void Record(Timing grade) => GradeCounts[(int)grade]++;

        public void ShowResults(TennisMatch match, int hits, int longestRally)
        {
            resultTitle.text = match.PlayerWonMatch ? "YOU WIN THE SET" : "OPPONENT WINS THE SET";
            resultTitle.color = match.PlayerWonMatch ? new Color(.5f, 1, .6f) : new Color(1, .7f, .5f);
            int clean = GradeCounts[(int)Timing.Great] + GradeCounts[(int)Timing.Excellent] + GradeCounts[(int)Timing.Perfect];
            resultBody.text = $"Games {match.PlayerGames}–{match.OpponentGames}\n" +
                $"Balls returned {hits}   ·   Longest rally {longestRally}\n" +
                $"Perfect {GradeCounts[(int)Timing.Perfect]}   ·   Clean hits {clean}\n\nSwing to play again";
            card.gameObject.SetActive(true);
        }

        public void HideResults()
        {
            card.gameObject.SetActive(false);
            System.Array.Clear(GradeCounts, 0, GradeCounts.Length);
        }

        void Update()
        {
            if (tip.text.Length == 0) return;
            float left = tipUntil - HudClock.Now;
            if (left <= 0) tip.text = "";
            else tip.canvasRenderer.SetAlpha(Mathf.Clamp01(left / .6f));
        }
    }
}

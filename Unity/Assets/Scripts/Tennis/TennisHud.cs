using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// Rally Pop match HUD. Native text and panels; legacy serve-meter visuals are preserved.
    public sealed class TennisHud : MonoBehaviour
    {
        // Palette: tropical daylight, one navy for every outline so it all reads as one set.
        public static readonly Color Navy = new(.05f, .10f, .30f), NavyDeep = new(.03f, .06f, .20f);
        internal static readonly Color SkyTop = new(.30f, .62f, 1f), SkyBottom = new(.07f, .24f, .78f);
        internal static readonly Color SunTop = new(1f, .86f, .30f), SunBottom = new(1f, .47f, .10f);
        internal static readonly Color SeaTop = new(.35f, .92f, 1f), SeaBottom = new(.12f, .50f, 1f);
        internal static readonly Color GoldTop = new(1f, .93f, .52f), GoldBottom = new(.98f, .66f, .12f);
        static readonly Color PearlTop = Color.white, PearlBottom = new(.82f, .88f, 1f);
        static readonly Color CaptionTop = new(.16f, .24f, .55f);

        bool scoreOnly = true;
        Font font;
        Sprite rounded, disc, star, ball, rallyRounded;
        static readonly Color RallyNavy = new(.055f,.12f,.21f), RallyCream = new(.98f,.965f,.90f), RallyLime = new(.80f,.94f,.30f);
        RectTransform feedbackPanel, callPanel; Text gradeText, setsText;
        RectTransform root, plaque, ballIcon;
        readonly Row[] rows = new Row[2];
        Image noticeBack;
        Text notice;
        string shownNotice;
        int shownServer = -1, shownStreak = -1;
        float introAt;
        readonly Image[] pips = new Image[TennisRules.SuperchargeStreak];

        // Grade badge.
        Text badgeCaption;
        float badgeAt = -9;

        // Word-art calls, queued so two never overlap.
        Text callFallback, callSub;
        readonly Queue<(string key, string sub, bool good)> calls = new();
        float callAt = -9, callDuration = CallHold;
        const float CallHold = 1.55f;

        // Sparkle bursts.
        sealed class Spark { public RectTransform rt; public Image img; public Vector2 from, velocity; public float at, spin, size; }
        readonly List<Spark> sparks = new();

        sealed class Row
        {
            public RectTransform tag, coin, cell;
            public Text name, games, points;
            public Image flash;
            public string shownName, shownGames, shownPoints;
            public float popAt = -9, wiggleAt = -9;
        }

        public string PlayerName = "YOU", OpponentName = "KAI";
        Text eventText;
        /// Event title above the score rows.
        public string EventLabel { set { if (eventText) eventText.text = value; } }
        internal RectTransform Root => root;
        /// Everything that belongs to the match (hidden during the presentation).
        public RectTransform MatchLayer => matchGroup ? (RectTransform)matchGroup.transform : root;
        CanvasGroup matchGroup;

        /// Hidden during the presentation; the plaque swings in when play starts.
        public bool MatchVisible
        {
            set
            {
                if (!matchGroup) return;
                if (value && matchGroup.alpha < 1) introAt = HudClock.Now;
                matchGroup.alpha = value ? 1 : 0;
            }
        }

        // ---------------------------------------------------------------- art

        static Sprite RoundedSprite(int r)
        {
            int n = r * 2 + 4;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "HUD rounded" };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(r - x, x - (n - 1 - r))), dy = Mathf.Max(0, Mathf.Max(r - y, y - (n - 1 - r)));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r + .5f - Mathf.Sqrt(dx * dx + dy * dy))));
            }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
        }

        static Texture2D Blank(int n, string name) => new(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
        static Sprite Whole(Texture2D t) { t.Apply(); return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f)); }

        static Sprite DiscSprite(int n = 64)
        {
            var t = Blank(n, "HUD disc"); float r = n * .5f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float d = Mathf.Sqrt((x + .5f - r) * (x + .5f - r) + (y + .5f - r) * (y + .5f - r));
                t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(r - d)));
            }
            return Whole(t);
        }

        /// Four-point sparkle, like the stars around the PERFECT! badge.
        static Sprite StarSprite(int n = 64)
        {
            var t = Blank(n, "HUD star"); float c = n * .5f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + .5f - c) / c, dy = Mathf.Abs(y + .5f - c) / c;
                float a = Mathf.Clamp01((1 - Mathf.Pow(Mathf.Sqrt(dx) + Mathf.Sqrt(dy), 2)) * 3f);
                t.SetPixel(x, y, new Color(1, 1, 1, a));
            }
            return Whole(t);
        }

        /// A tennis ball: optic yellow, darker toward the rim, the white seam, a navy outline.
        static Sprite BallSprite(int n = 64)
        {
            var t = Blank(n, "HUD ball"); float r = n * .5f;
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float px = x + .5f - r, py = y + .5f - r, d = Mathf.Sqrt(px * px + py * py) / r;
                if (d > 1) { t.SetPixel(x, y, Color.clear); continue; }
                Color c = Color.Lerp(new Color(.93f, 1f, .35f), new Color(.62f, .82f, .08f), Mathf.Clamp01((d - .2f) * 1.1f - py / r * .25f));
                float seam = Mathf.Min(Mathf.Abs(Vector2.Distance(new Vector2(px, py), new Vector2(-r * 1.25f, 0)) - r * .95f),
                                       Mathf.Abs(Vector2.Distance(new Vector2(px, py), new Vector2(r * 1.25f, 0)) - r * .95f)) / r;
                if (seam < .07f) c = Color.Lerp(Color.white, c, seam / .07f);
                if (d > .86f) c = Color.Lerp(c, Navy, (d - .86f) / .14f);
                c.a = Mathf.Clamp01((1 - d) * r);
                t.SetPixel(x, y, c);
            }
            return Whole(t);
        }

        // ---------------------------------------------------------------- building blocks

        static Image Box(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 pos, Color color, Sprite sprite, bool sliced = true)
        {
            var img = new GameObject(name).AddComponent<Image>();
            img.transform.SetParent(parent, false);
            img.sprite = sprite; img.type = sliced && sprite ? Image.Type.Sliced : Image.Type.Simple; img.color = color; img.raycastTarget = false;
            var rt = img.rectTransform; rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = size; rt.anchoredPosition = pos;
            return img;
        }

        /// A glossy chunky shape: navy outline, gradient body, soft highlight on the top half.
        /// Returns the body; its parent is the outline, which is what to move and scale.
        internal Image Chunky(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 pos, Color top, Color bottom, float outline = 4)
        {
            var edge = Box(name, parent, anchor, size + Vector2.one * outline * 2, pos, Navy, rounded);
            var body = Box("Body", edge.rectTransform, new Vector2(.5f, .5f), size, Vector2.zero, Color.white, rounded);
            UiGradient.On(body, top, bottom);
            var gloss = Box("Gloss", body.rectTransform, new Vector2(.5f, 1f), new Vector2(size.x - 8, size.y * .42f), new Vector2(0, -size.y * .23f), new Color(1, 1, 1, .3f), rounded);
            UiGradient.On(gloss, Color.white, new Color(1, 1, 1, 0));
            return body;
        }

        internal Text Label(RectTransform parent, string text, int size, Vector2 pos, Vector2 box, Color color, TextAnchor align, float outline = 2.2f)
        {
            var t = new GameObject("Label").AddComponent<Text>();
            t.transform.SetParent(parent, false);
            t.font = font; t.fontSize = size; t.color = color; t.alignment = align; t.text = text; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform; rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(.5f, .5f); rt.sizeDelta = box; rt.anchoredPosition = pos;
            if (outline > 0)
            {
                var o = t.gameObject.AddComponent<Outline>(); o.effectColor = Navy; o.effectDistance = new Vector2(outline, -outline);
                var o2 = t.gameObject.AddComponent<Outline>(); o2.effectColor = Navy; o2.effectDistance = new Vector2(-outline, outline);
                var s = t.gameObject.AddComponent<Shadow>(); s.effectColor = new Color(.03f, .06f, .2f, .7f); s.effectDistance = new Vector2(0, -outline * 1.8f);
            }
            return t;
        }

        // ---------------------------------------------------------------- build

        public void Build(Canvas canvas)
        {
            font = Resources.Load<Font>("Tennis/UI/Fonts/Rubik-Bold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            rounded = RoundedSprite(20); disc = DiscSprite(); star = StarSprite(); ball = BallSprite();
            root = (RectTransform)canvas.transform;
            var group = new GameObject("Match HUD").AddComponent<RectTransform>();
            group.SetParent(root, false); group.anchorMin = Vector2.zero; group.anchorMax = Vector2.one; group.offsetMin = group.offsetMax = Vector2.zero;
            matchGroup = group.gameObject.AddComponent<CanvasGroup>(); matchGroup.blocksRaycasts = false;

            rallyRounded = RoundedSprite(7);
            var body = RallyPanel("Rally Pop score", group, new Vector2(0,1), new Vector2(352,126), new Vector2(200,-87), RallyNavy);
            plaque = body.rectTransform;
            eventText = RallyLabel(plaque, TennisVenue.Title.ToUpperInvariant(), 12, new Vector2(-60,48), new Vector2(200,20), RallyCream, TextAnchor.MiddleLeft);
            setsText = RallyLabel(plaque, "SET 1", 11, new Vector2(-76,-51), new Vector2(174,16), RallyCream, TextAnchor.MiddleLeft);
            RallyLabel(plaque, "GAMES", 10, new Vector2(70,48), new Vector2(48,20), RallyCream, TextAnchor.MiddleCenter);
            RallyLabel(plaque, "PTS", 10, new Vector2(130,48), new Vector2(62,20), RallyCream, TextAnchor.MiddleCenter);
            rows[0] = BuildRow(plaque, 17, RallyCream, RallyCream);
            rows[1] = BuildRow(plaque, -23, RallyCream, RallyCream);
            ballIcon = Box("Server ball", plaque, new Vector2(0,.5f), new Vector2(17,17), new Vector2(27,17), Color.white, ball, false).rectTransform;
            BuildServeMeter(group);
            BuildSwingCue(group);
            BuildTimingCheck();
            noticeBack = RallyPanel("Match state", plaque, new Vector2(.5f,.5f), new Vector2(138,22), new Vector2(87,-51), RallyLime);
            notice = RallyLabel(noticeBack.rectTransform, "", 9, Vector2.zero, new Vector2(132,18), RallyNavy, TextAnchor.MiddleCenter);
            noticeBack.gameObject.SetActive(false);

            var feedback = RallyPanel("Shot feedback", group, Vector2.zero, new Vector2(226,94), new Vector2(137,88), RallyNavy);
            feedbackPanel = feedback.rectTransform;
            var quality = Box("Quality", feedbackPanel, new Vector2(.5f,.5f), new Vector2(202,26), new Vector2(0,25), RallyLime, rallyRounded);
            gradeText = RallyLabel(quality.rectTransform, "PERFECT", 17, Vector2.zero, new Vector2(192,26), RallyNavy, TextAnchor.MiddleLeft);
            badgeCaption = RallyLabel(feedbackPanel, "", 16, new Vector2(0,-9), new Vector2(202,40), RallyCream, TextAnchor.MiddleLeft);
            feedbackPanel.gameObject.SetActive(false);

            var call = RallyPanel("Point call", group, new Vector2(.5f,1), new Vector2(320,90), new Vector2(0,-118), RallyNavy);
            callPanel = call.rectTransform;
            Box("Lime accent", callPanel, new Vector2(0,.5f), new Vector2(4,62), new Vector2(8,0), RallyLime, rallyRounded);
            callFallback = RallyLabel(callPanel, "", 30, new Vector2(0,13), new Vector2(284,38), RallyLime, TextAnchor.MiddleCenter);
            callSub = RallyLabel(callPanel, "", 13, new Vector2(0,-22), new Vector2(284,26), RallyCream, TextAnchor.MiddleCenter);
            callPanel.gameObject.SetActive(false);

            // Supercharge pips beside the contact racket.
            for (int i = 0; i < pips.Length; i++)
            {
                Box("Pip outline", group, new Vector2(1, 0), new Vector2(26, 26), new Vector2(-176, 44 + i * 30), Navy, disc, false);
                pips[i] = Box("Supercharge pip", group, new Vector2(1, 0), new Vector2(20, 20), new Vector2(-176, 44 + i * 30), Color.white, disc, false);
            }

            for (int i = 0; i < 28; i++)
            {
                var img = Box("Sparkle", root, new Vector2(.5f, .5f), new Vector2(24, 24), Vector2.zero, Color.white, star, false);
                img.enabled = false; sparks.Add(new Spark { rt = img.rectTransform, img = img, at = -9 });
            }
            introAt = HudClock.Now;
        }

        // These helpers are deliberately independent of Chunky/Label: serve power stays pixel-identical.
        Image RallyPanel(string name, RectTransform parent, Vector2 anchor, Vector2 size, Vector2 pos, Color color)
        {
            var panel = Box(name, parent, anchor, size, pos, color, rallyRounded);
            var shadow = panel.gameObject.AddComponent<Shadow>(); shadow.effectColor = new Color(.015f,.03f,.06f,.35f); shadow.effectDistance = new Vector2(0,-4);
            Box("Top highlight", panel.rectTransform, new Vector2(.5f,1), new Vector2(size.x-16,1), new Vector2(0,-1), new Color(1,1,1,.14f), null);
            return panel;
        }

        Text RallyLabel(RectTransform parent, string text, int size, Vector2 pos, Vector2 box, Color color, TextAnchor align)
        {
            var label = Label(parent,text,size * 2,pos,box * 2,color,align,0);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = Mathf.Max(10,size-5) * 2; label.resizeTextMaxSize = size * 2;
            // Higher atlas resolution for couch-readable type; no change to legacy meter labels.
            label.rectTransform.localScale = Vector3.one * .5f;
            return label;
        }

        Row BuildRow(RectTransform body, float y, Color top, Color bottom)
        {
            var row = new Row();
            row.tag = Box("Player name", body, new Vector2(0,.5f), new Vector2(212,36), new Vector2(116,y), RallyCream, rallyRounded).rectTransform;
            row.name = RallyLabel(row.tag,"",19,new Vector2(12,0),new Vector2(160,34),RallyNavy,TextAnchor.MiddleLeft);
            row.coin = Box("Games",body,new Vector2(0,.5f),new Vector2(44,36),new Vector2(246,y),RallyCream,rallyRounded).rectTransform;
            row.games = RallyLabel(row.coin,"0",22,Vector2.zero,new Vector2(42,34),RallyNavy,TextAnchor.MiddleCenter);
            row.cell = Box("Points",body,new Vector2(0,.5f),new Vector2(68,36),new Vector2(306,y),RallyLime,rallyRounded).rectTransform;
            row.points = RallyLabel(row.cell,"0",28,Vector2.zero,new Vector2(64,34),RallyNavy,TextAnchor.MiddleCenter);
            row.flash = Box("Score flash",row.cell,new Vector2(.5f,.5f),new Vector2(68,36),Vector2.zero,Color.white,rallyRounded);
            row.flash.canvasRenderer.SetAlpha(0);
            return row;
        }

        // ---------------------------------------------------------------- state

        static string Call(int p) => p switch { 0 => "0", 1 => "15", 2 => "30", _ => "40" };

        static void Points(TennisMatch m, out string mine, out string theirs)
        {
            if (m.Tiebreak) { mine = Digits(m.PlayerPoints); theirs = Digits(m.OpponentPoints); return; }
            if (m.PlayerPoints >= 3 && m.OpponentPoints >= 3)
            {
                if (m.PlayerPoints == m.OpponentPoints) { mine = theirs = "40"; return; }
                bool ahead = m.PlayerPoints > m.OpponentPoints;
                mine = ahead ? "AD" : "40"; theirs = ahead ? "40" : "AD"; return;
            }
            mine = Call(m.PlayerPoints); theirs = Call(m.OpponentPoints);
        }

        static readonly string[] digits = { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        static string Digits(int v) => v >= 0 && v < 10 ? digits[v] : v.ToString();

        void SetRow(Row row, string name, string games, string points)
        {
            if (row.shownName != name) { row.shownName = name; row.name.text = name; }
            bool gameChanged = row.shownGames != null && row.shownGames != games;
            bool pointChanged = row.shownPoints != null && row.shownPoints != points;
            if (row.shownGames != games) { row.shownGames = games; row.games.text = games; }
            if (row.shownPoints != points) { row.shownPoints = points; row.points.text = points; }
            // Only celebrate a score that went up (a new game resets points to 0).
            if ((pointChanged && points != "0") || gameChanged)
            {
                row.popAt = row.wiggleAt = HudClock.Now;

            }
        }

        Vector2 Centre(RectTransform rt) => root.InverseTransformPoint(rt.TransformPoint(rt.rect.center));

        public void Refresh(TennisGame game)
        {
            scoreOnly = game.ScoreOnlyText;
            if (scoreOnly) { badgeAt = -99; feedbackPanel.gameObject.SetActive(false); }
            meterLabel.enabled = tossLabel.enabled = !scoreOnly;
            var m = game.Match;
            Points(m, out string pa, out string pb);
            // Multi-set matches show sets won ahead of the games in the current set.
            SetRow(rows[0], PlayerName, Digits(m.PlayerGames), pa);
            SetRow(rows[1], OpponentName, Digits(m.OpponentGames), pb);
            setsText.text = m.Tiebreak ? "TIEBREAK" : m.MultiSet ? $"SET {m.PlayerSets + m.OpponentSets + (m.Complete ? 0 : 1)}  ·  SETS {m.PlayerSets}–{m.OpponentSets}" : "SET 1";
            shownServer = m.PlayerServes ? 0 : 1;
            string n = m.Complete ? null : TennisGame.IsMatchPoint(m) ? "MATCH POINT" : game.SecondServe ? "2ND SERVE"
                : m.PlayerPoints == m.OpponentPoints && m.PlayerPoints >= 3 ? "DEUCE" : null;
            if (n != shownNotice)
            {
                shownNotice = n;
                noticeBack.gameObject.SetActive(n != null);
                if (n != null)
                {
                    notice.text = n;
                    // Second serves are already called by the fault; announce the others.
                    if (n != "2ND SERVE") ShowCall(n, null, n == "MATCH POINT" && m.PlayerPoints > m.OpponentPoints);
                }
            }
            if (game.Streak != shownStreak)
            {
                shownStreak = game.Streak;
                for (int i = 0; i < pips.Length; i++) pips[i].color = i < game.Streak ? SeaTop : new Color(1, 1, 1, .3f);
            }
            Animate();
        }

        // ---------------------------------------------------------------- events

        /// A clean hit: quality and shot detail settle into the lower-left panel.
        public void ShowGrade(Timing grade, bool supercharged, string detail)
        {
            if (scoreOnly) return;
            gradeText.text = supercharged ? "SUPER SHOT" : grade.ToString().ToUpperInvariant();
            badgeCaption.text = (detail ?? "").Replace("KM/H", "km/h").Replace("  ·  ", " · ");
            feedbackPanel.gameObject.SetActive(true);
            badgeAt = HudClock.Now;
        }

        /// A point, game or state call. Queued behind whatever is showing.
        public void ShowCall(string key, string sub, bool good)
        {
            if (scoreOnly) { calls.Clear(); return; }
            if (calls.Count >= 3) calls.Dequeue();
            calls.Enqueue((key, sub, good));
            if (HudClock.Now - callAt > callDuration) NextCall();
        }

        /// Point winners remain visible even with the uncluttered rally HUD.
        public void ShowPointResult(string winnerName, string detail, bool good) {
            calls.Clear();
            callFallback.text = winnerName.ToUpperInvariant() + " SCORED";
            callFallback.resizeTextForBestFit = true; callFallback.resizeTextMinSize = 28; callFallback.resizeTextMaxSize = 60;
            callSub.text = detail;
            callPanel.gameObject.SetActive(true);
            callAt = HudClock.Now; callDuration = 3f;
        }
        public string PointResultText => callPanel && callPanel.gameObject.activeSelf ? callFallback.text : "";

        void NextCall()
        {
            if (calls.Count == 0) return;
            var (key, sub, good) = calls.Dequeue();
            callFallback.text = key.Replace("!", "").ToUpperInvariant();
            callSub.text = sub ?? "";
            callPanel.gameObject.SetActive(true);
            callAt = HudClock.Now; callDuration = CallHold;
        }

        void Burst(Vector2 at, int count, Color tint, float speed)
        {
            int started = 0;
            foreach (var s in sparks)
            {
                if (started >= count) break;
                if (s.img.enabled) continue;
                float angle = started / (float)count * Mathf.PI * 2 + Random.value * .5f;
                s.at = HudClock.Now; s.from = at;
                s.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * Random.Range(.7f, 1.2f);
                s.spin = Random.Range(-360f, 360f); s.size = Random.Range(16f, 30f);
                s.img.color = Color.Lerp(tint, Color.white, Random.value * .5f); s.img.enabled = true;
                started++;
            }
        }

        // ---------------------------------------------------------------- motion

        /// Springy settle: overshoots once or twice, then rests at 1.
        static float Elastic(float t, float amplitude, float frequency = 18f, float damping = 7f) =>
            1 + amplitude * Mathf.Exp(-t * damping) * Mathf.Cos(t * frequency);

        /// Age of an animation started at `at`; a start "in the future" (the HUD clock can jump
        /// when a capture rate is switched) counts as long finished.
        static float Age(float now, float at) { float a = now - at; return a < 0 ? 99 : a; }

        // ---------------------------------------------------------------- swing cue
        //
        // When to swing, as a rhythm game shows it: along the bottom of the screen two
        // brackets close on a target as the ball comes in, meeting it at the moment to START
        // the swing (the stroke then reaches the ball as it arrives). "SWING!" flashes as they
        // meet. It reads at a glance without looking away from the ball, and it closes at a
        // steady speed so the beat can be felt as well as seen.
        const float CueTravel = 190;
        RectTransform cue, cueLeft, cueRight, cueTarget; Image cueFill, cueLeftImage, cueRightImage; Text cueText;
        float cueClosing = 1, cueNowAt = -10, cueShownAt = -10; bool cueShown, cueWasNow;
        static readonly Color CueGreen = new(.35f, 1f, .45f);

        void BuildSwingCue(RectTransform group)
        {
            var body = Chunky("Swing cue", group, new Vector2(.5f, 0), new Vector2(460, 30), new Vector2(0, 44), new Color(.10f, .16f, .40f, .92f), NavyDeep, 3);
            cue = (RectTransform)body.transform.parent;
            // Track marks: the closer to the middle, the brighter.
            for (int i = 1; i <= 4; i++)
                foreach (float side in new[] { -1f, 1f })
                    Box("Tick", body.rectTransform, new Vector2(.5f, .5f), new Vector2(4, 14), new Vector2(side * (34 + i * CueTravel / 4.5f), 0), new Color(1, 1, 1, .12f + .06f * (4 - i)), rounded);
            cueTarget = Box("Target ring", cue, new Vector2(.5f, .5f), new Vector2(64, 64), Vector2.zero, Color.white, disc, false).rectTransform;
            Box("Target hole", cueTarget, new Vector2(.5f, .5f), new Vector2(50, 50), Vector2.zero, NavyDeep, disc, false);
            cueFill = Box("Target fill", cueTarget, new Vector2(.5f, .5f), new Vector2(40, 40), Vector2.zero, Color.white, ball, false);
            cueLeftImage = Box("Bracket left", cue, new Vector2(.5f, .5f), new Vector2(16, 54), Vector2.zero, Color.white, rounded);
            cueRightImage = Box("Bracket right", cue, new Vector2(.5f, .5f), new Vector2(16, 54), Vector2.zero, Color.white, rounded);
            cueLeft = cueLeftImage.rectTransform; cueRight = cueRightImage.rectTransform;
            cueText = Label(cue, "SWING!", 34, new Vector2(0, 58), new Vector2(240, 44), CueGreen, TextAnchor.MiddleCenter, 2.6f);
            cue.gameObject.SetActive(false);
        }

        /// Show the swing cue closing: 1 = the ball has just been struck, 0 = swing now.
        public void SwingCue(bool show, float closing)
        {
            if (!cue) return;
            float now = HudClock.Now;
            if (show)
            {
                if (!cueShown) { cueShownAt = now; cueWasNow = false; }
                cueShown = true; cueClosing = Mathf.Clamp01(closing);
                if (!cue.gameObject.activeSelf) cue.gameObject.SetActive(true);
                if (cueClosing <= .02f && !cueWasNow) { cueWasNow = true; cueNowAt = now; Burst(Centre(cue), 8, CueGreen, 220); }
            }
            else if (cueShown)
            {
                // Linger a moment after the swing so "SWING!" is seen, then go.
                cueShown = false;
                if (!cueWasNow) cue.gameObject.SetActive(false);
            }
        }

        void AnimateSwingCue(float now)
        {
            if (!cue || !cue.gameObject.activeSelf) return;
            float sinceNow = Age(now, cueNowAt);
            if (!cueShown && (!cueWasNow || sinceNow > .45f)) { cue.gameObject.SetActive(false); return; }
            float c = cueWasNow ? 0 : cueClosing;
            float x = 40 + c * CueTravel;
            cueLeft.anchoredPosition = new Vector2(-x, 0); cueRight.anchoredPosition = new Vector2(x, 0);
            // White far out, warming through yellow to green as the moment arrives.
            Color tint = c > .5f ? Color.Lerp(SunTop, Color.white, (c - .5f) * 2) : Color.Lerp(CueGreen, SunTop, c * 2);
            cueLeftImage.color = cueRightImage.color = tint;
            cueFill.color = cueWasNow ? CueGreen : Color.Lerp(Color.white, new Color(1, 1, 1, .35f), c);
            cueTarget.localScale = Vector3.one * (cueWasNow && sinceNow < .35f ? 1 + .35f * Mathf.Exp(-sinceNow * 7) * Mathf.Cos(sinceNow * 18) : 1 + (1 - c) * .12f);
            bool flash = cueWasNow && sinceNow < .45f;
            cueText.enabled = flash && !scoreOnly;
            if (flash) cueText.transform.localScale = Vector3.one * (sinceNow < .1f ? Mathf.Lerp(1.8f, 1f, sinceNow / .1f) : 1);
            float fadeIn = Mathf.Clamp01(Age(now, cueShownAt) / .12f), fadeOut = cueShown ? 1 : Mathf.Clamp01(1 - (sinceNow - .3f) / .15f);
            SetGroupAlpha(cue, fadeIn * fadeOut);
        }

        // ---------------------------------------------------------------- timing check
        //
        // A ball bouncing on a line to a steady beat, centre screen; the player swings on
        // every bounce. Dots fill as swings are counted, and nothing says early or late (see
        // TennisBeatCalibration).
        RectTransform check, checkBall, checkRing; Text checkTitle, checkHint; Image[] checkDots;
        float checkRingAt = -10, checkDoneAt = -10; bool checkDone;
        const float CheckFloor = -56, CheckBounce = 118;

        void BuildTimingCheck()
        {
            var shade = Box("Timing check", root, new Vector2(.5f, .5f), new Vector2(4000, 4000), Vector2.zero, new Color(.02f, .04f, .14f, .78f), null, false);
            check = shade.rectTransform;
            var body = Chunky("Timing panel", check, new Vector2(.5f, .5f), new Vector2(640, 380), new Vector2(0, 10), new Color(.10f, .16f, .40f), NavyDeep, 5);
            var rt = body.rectTransform;
            checkTitle = Label(rt, "TIMING CHECK", 40, new Vector2(0, 148), new Vector2(600, 50), GoldTop, TextAnchor.MiddleCenter, 2.6f);
            checkHint = Label(rt, "Get the rhythm…", 24, new Vector2(0, 104), new Vector2(600, 36), Color.white, TextAnchor.MiddleCenter, 1.8f);
            var line = Box("Line", rt, new Vector2(.5f, .5f), new Vector2(440, 10), new Vector2(0, CheckFloor - 26), Color.white, rounded);
            UiGradient.On(line, Color.white, PearlBottom);
            checkRing = Box("Bounce ring", rt, new Vector2(.5f, .5f), new Vector2(70, 22), new Vector2(0, CheckFloor - 26), CueGreen, disc, false).rectTransform;
            checkBall = Box("Ball", rt, new Vector2(.5f, .5f), new Vector2(46, 46), new Vector2(0, CheckFloor), Color.white, ball, false).rectTransform;
            int scored = TennisBeatCalibration.Beats - TennisBeatCalibration.WarmUp;
            checkDots = new Image[scored];
            for (int i = 0; i < scored; i++)
            {
                float x = (i - (scored - 1) / 2f) * 44;
                Box("Dot outline", rt, new Vector2(.5f, .5f), new Vector2(28, 28), new Vector2(x, -140), Navy, disc, false);
                checkDots[i] = Box("Dot", rt, new Vector2(.5f, .5f), new Vector2(20, 20), new Vector2(x, -140), new Color(1, 1, 1, .18f), disc, false);
            }
            check.gameObject.SetActive(false);
        }

        public void ShowTimingCheck(bool visible)
        {
            if (!check) return;
            check.gameObject.SetActive(visible); checkDone = false;
            if (!visible) return;
            // Over everything, the intro's title cards included.
            check.SetAsLastSibling();
            checkTitle.text = "TIMING CHECK"; checkTitle.color = GoldTop; checkHint.text = "Watch the ball…";
            foreach (var d in checkDots) d.color = new Color(1, 1, 1, .18f);
            checkBall.gameObject.SetActive(true);
        }

        /// The ball's height (0 on the line .. 1 at the top) and the beat it is on.
        public void SetTimingCheck(float height, int beat, int scored, float countdown = 0)
        {
            if (!check) return;
            if (countdown > 0)
            {
                // Get ready: what to do, then 5..1 before the ball starts to drop.
                checkTitle.text = $"GET READY  ·  {Mathf.CeilToInt(countdown)}";
                checkHint.text = "Swing every time the ball drops onto the line";
                checkBall.anchoredPosition = new Vector2(0, CheckFloor + CheckBounce);
                return;
            }
            if (checkTitle.text != "TIMING CHECK") checkTitle.text = "TIMING CHECK";
            checkBall.anchoredPosition = new Vector2(0, CheckFloor + height * CheckBounce);
            // Squash on the line.
            float squash = height < .06f ? 1 - height / .06f : 0;
            checkBall.localScale = new Vector3(1 + .25f * squash, 1 - .25f * squash, 1);
            checkHint.text = beat < 0 ? "Watch the ball…" : beat < TennisBeatCalibration.WarmUp ? "Get the rhythm…" :  $"Swing on every bounce!  {scored}/{TennisBeatCalibration.Beats - TennisBeatCalibration.WarmUp} recorded";
            if (height < .02f && Age(HudClock.Now, checkRingAt) > .3f) checkRingAt = HudClock.Now;
        }

        /// A swing counted for `beat`.
        public void TimingCheckSwing(int beat)
        {
            int i = beat - TennisBeatCalibration.WarmUp;
            if (checkDots == null || i < 0 || i >= checkDots.Length) return;
            checkDots[i].color = CueGreen;
            checkDots[i].rectTransform.localScale = Vector3.one * 1.5f;
        }

        public void TimingCheckDone(bool ok)
        {
            if (!check) return;
            checkDone = true; checkDoneAt = HudClock.Now;
            checkBall.gameObject.SetActive(false);
            checkTitle.text = ok ? "TIMING SET!" : "NO STEADY RHYTHM";
            checkTitle.color = ok ? CueGreen : Color.white;
            checkHint.text = ok ? "Your swings are now timed to what you see" : "No problem — the game learns your timing as you play";
            if (ok) Burst(Vector2.zero, 14, CueGreen, 300);
        }

        void AnimateTimingCheck(float now)
        {
            if (!check || !check.gameObject.activeSelf) return;
            float r = Age(now, checkRingAt);
            checkRing.localScale = Vector3.one * (1 + r * 2.2f);
            checkRing.GetComponent<Image>().canvasRenderer.SetAlpha(checkDone ? 0 : Mathf.Clamp01(1 - r / .35f));
            foreach (var d in checkDots) d.rectTransform.localScale = Vector3.Lerp(d.rectTransform.localScale, Vector3.one, Time.unscaledDeltaTime * 10);
            if (checkDone && Age(now, checkDoneAt) > 2.2f) check.gameObject.SetActive(false);
        }

        // ---------------------------------------------------------------- serve meter
        //
        // The serve's power bar, on the right of the screen: it fills as the toss rises and is
        // full at the top, where a small gold band marks the perfect window. It locks where
        // the player swung. The toss meter itself lives on the phone (no display lag there);
        // its grade shows above the bar.
        const float MeterHeight = 300;
        RectTransform meter, meterFill; Image meterFillImage; Text meterLabel, tossLabel;
        float meterValue, meterLockedAt = -10, tossAt = -10; bool meterLocked, meterPerfect;

        void BuildServeMeter(RectTransform group)
        {
            var body = Chunky("Serve power", group, new Vector2(1, .5f), new Vector2(48, MeterHeight), new Vector2(-96, 10), new Color(.10f, .16f, .40f), NavyDeep, 4);
            meter = (RectTransform)body.transform.parent;
            float band = MeterHeight * (1 - TennisRules.ServePowerAt(TennisRules.ServePerfectWindow));
            var perfect = Box("Perfect window", body.rectTransform, new Vector2(.5f, 1), new Vector2(48, Mathf.Max(4, band)), new Vector2(0, -Mathf.Max(4, band) / 2), Color.white, rounded);
            UiGradient.On(perfect, GoldTop, GoldBottom);
            meterFillImage = Box("Fill", body.rectTransform, new Vector2(.5f, 0), new Vector2(36, 0), Vector2.zero, Color.white, rounded);
            UiGradient.On(meterFillImage, SunTop, SunBottom);
            meterFill = meterFillImage.rectTransform; meterFill.pivot = new Vector2(.5f, 0); meterFill.anchoredPosition = new Vector2(0, 6);
            meterLabel = Label(meter, "POWER", 20, new Vector2(0, -MeterHeight / 2 - 26), new Vector2(160, 30), Color.white, TextAnchor.MiddleCenter, 2f);
            tossLabel = Label(meter, "", 22, new Vector2(-10, MeterHeight / 2 + 30), new Vector2(260, 30), GoldTop, TextAnchor.MiddleCenter, 2.2f);
            meter.gameObject.SetActive(false);
        }

        public void ShowServeMeter(bool visible)
        {
            if (!meter || meter.gameObject.activeSelf == visible) return;
            meter.gameObject.SetActive(visible);
            if (visible) { meterLocked = false; meterPerfect = false; meterValue = 0; meterLabel.text = "POWER"; }
        }

        /// The toss is in the air: the bar shows what a swing now would give.
        public void SetServeMeter(float value, bool rising) { if (!meterLocked) meterValue = value; }

        /// The player swung: freeze the bar there.
        public void LockServeMeter(float value, bool perfect)
        {
            if (!meter) return;
            if (!meter.gameObject.activeSelf) ShowServeMeter(true);
            meterValue = value; meterLocked = true; meterPerfect = perfect; meterLockedAt = HudClock.Now;
            meterLabel.text = perfect ? "PERFECT!" : value > .75f ? "BIG SERVE" : value > .4f ? "GOOD" : "WEAK";
            if (perfect) Burst(Centre(meter), 10, GoldTop, 200);
        }

        /// The phone's toss meter reading, shown above the bar.
        public void ShowTossGrade(float accuracy)
        {
            if (!tossLabel) return;
            tossLabel.text = accuracy >= TennisRules.ServePerfectToss ? "PERFECT TOSS!" : accuracy > .6f ? "GOOD TOSS" : accuracy > .3f ? "LOOSE TOSS" : "WILD TOSS";
            tossLabel.color = accuracy >= TennisRules.ServePerfectToss ? GoldTop : Color.white;
            tossAt = HudClock.Now;
        }

        void AnimateServeMeter(float now)
        {
            if (!meter || !meter.gameObject.activeSelf) return;
            float h = (MeterHeight - 12) * Mathf.Clamp01(meterValue);
            meterFill.sizeDelta = new Vector2(36, Mathf.Lerp(meterFill.sizeDelta.y, h, meterLocked ? 1 : .6f));
            float since = Age(now, meterLockedAt);
            meter.localScale = Vector3.one * (meterLocked && since < .4f ? 1 + .18f * Mathf.Exp(-since * 8) * Mathf.Cos(since * 20) : 1);
            meterFillImage.color = meterLocked && meterPerfect ? Color.Lerp(Color.white, new Color(1, .95f, .6f), Mathf.PingPong(now * 6, 1)) : Color.white;
            tossLabel.transform.localScale = Vector3.one * (Age(now, tossAt) < .3f ? 1.25f - Age(now, tossAt) : 1);
        }

        void Animate()
        {
            float now = HudClock.Now;
            AnimateServeMeter(now);
            AnimateSwingCue(now);
            AnimateTimingCheck(now);
            // Safe-area offsets affect the new panels only; the serve meter never moves.
            float left = Screen.width > 0 ? Screen.safeArea.xMin / Screen.width * root.rect.width : 0;
            float top = Screen.height > 0 ? (Screen.height-Screen.safeArea.yMax) / Screen.height * root.rect.height : 0;
            float bottom = Screen.height > 0 ? Screen.safeArea.yMin / Screen.height * root.rect.height : 0;
            float intro = Age(now,introAt);
            plaque.anchoredPosition = new Vector2(200+left-18*Mathf.Exp(-intro*10),-87-top);
            noticeBack.rectTransform.anchoredPosition = new Vector2(87,-51);
            ballIcon.anchoredPosition = new Vector2(27,rows[Mathf.Max(0,shownServer)].tag.anchoredPosition.y);
            foreach (var row in rows)
            {
                float p = Age(now,row.popAt);
                row.cell.localScale = row.coin.localScale = Vector3.one*(p < .6f ? Elastic(p,.07f,18,10) : 1);
                row.flash.canvasRenderer.SetAlpha(p < .2f ? .22f*(1-p/.2f) : 0);
            }
            for (int i=0;i<pips.Length;i++) pips[i].rectTransform.localScale = Vector3.one;
            float since = Age(now,badgeAt);
            if (since < 1.8f)
            {
                feedbackPanel.anchoredPosition = new Vector2(137+left,88+bottom-6*Mathf.Exp(-since*15));
                SetGroupAlpha(feedbackPanel,Mathf.Min(Mathf.Clamp01(since/.08f),Mathf.Clamp01((1.8f-since)/.25f)));
            }
            else feedbackPanel.gameObject.SetActive(false);
            float t = Age(now,callAt);
            if (t < callDuration)
            {
                callPanel.anchoredPosition = new Vector2(0,-118-top+8*Mathf.Exp(-t*15));
                callPanel.localScale = Vector3.one*(1+.035f*Mathf.Exp(-t*12));
                SetGroupAlpha(callPanel,Mathf.Min(Mathf.Clamp01(t/.09f),Mathf.Clamp01((callDuration-t)/.2f)));
            }
            else { callPanel.gameObject.SetActive(false); if (calls.Count > 0) NextCall(); }

            // Sparkles fly out, spin, shrink and fade.
            foreach (var sp in sparks)
            {
                if (!sp.img.enabled) continue;
                float a = Age(now, sp.at);
                if (a > .65f) { sp.img.enabled = false; continue; }
                sp.rt.anchoredPosition = sp.from + sp.velocity * a * (1 - a * .6f);
                sp.rt.localRotation = Quaternion.Euler(0, 0, sp.spin * a);
                float grow = a < .12f ? a / .12f : 1 - (a - .12f) / .53f;
                sp.rt.sizeDelta = Vector2.one * sp.size * Mathf.Max(0, grow);
                sp.img.canvasRenderer.SetAlpha(Mathf.Clamp01(grow * 1.4f));
            }
        }

        static void SetGroupAlpha(RectTransform rt, float alpha)
        {
            foreach (var g in rt.GetComponentsInChildren<Graphic>()) g.canvasRenderer.SetAlpha(alpha);
        }
    }
}

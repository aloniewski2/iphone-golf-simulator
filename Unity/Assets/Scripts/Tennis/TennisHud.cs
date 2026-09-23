using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.Tennis
{
    /// The match HUD, in the same arcade language as the Higgsfield grade badges: chunky
    /// rounded type (Rubik Bold) with thick navy outlines, glossy gradients, and everything
    /// that changes moving -- points pop and sparkle, calls slam in, the server's ball bounces.
    ///
    ///   * score plaque top-left: gold header ribbon, a tag per player (orange = you, cyan =
    ///     rival), games in gold coins, points in white pills, a bouncing ball on the server
    ///   * grade badge top-right after every clean hit, with the shot caption under it
    ///   * word-art calls mid-screen for the moments (ACE!, OUT!, DEUCE!, GAME!...)
    ///   * the racket contact map and supercharge pips bottom-right (TennisHitMap)
    ///
    /// Built once; text is rewritten only when its value changes, and all motion is done with
    /// transforms and canvas-renderer alpha so the canvas is not rebuilt every frame.
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

        Font font;
        Sprite rounded, disc, star, ball;
        RectTransform root, plaque, ballIcon, shimmerRt;
        readonly Row[] rows = new Row[2];
        Image noticeBack;
        Text notice;
        UiGradient noticeGradient;
        string shownNotice;
        int shownServer = -1, shownStreak = -1;
        float introAt;
        readonly Image[] pips = new Image[TennisRules.SuperchargeStreak];

        // Grade badge.
        readonly Sprite[] grades = new Sprite[6]; Sprite super;
        Image badge; RectTransform badgeCaptionBack; Text badgeCaption;
        float badgeAt = -9; bool badgeSuper;

        // Word-art calls, queued so two never overlap.
        Image callArt; Text callFallback, callSub; RectTransform callSubBack;
        readonly Dictionary<string, Sprite> callSprites = new();
        readonly Queue<(string key, string sub, bool good)> calls = new();
        float callAt = -9; bool callGood;
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

        static Sprite LoadSprite(string path)
        {
            var t = Resources.Load<Texture2D>(path);
            return t ? Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(.5f, .5f), 100) : null;
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

            // Score plaque.
            var body = Chunky("Score plaque", group, new Vector2(0, 1), new Vector2(400, 112), new Vector2(24 + 204, -30 - 60), SkyTop, SkyBottom, 5);
            plaque = (RectTransform)body.transform.parent;
            var clip = new GameObject("Shimmer mask").AddComponent<RectMask2D>();
            clip.transform.SetParent(body.transform, false);
            var clipRt = clip.rectTransform; clipRt.anchorMin = Vector2.zero; clipRt.anchorMax = Vector2.one; clipRt.offsetMin = clipRt.offsetMax = Vector2.zero;
            var shimmer = Box("Shimmer", clipRt, new Vector2(0, .5f), new Vector2(46, 220), new Vector2(-80, 0), new Color(1, 1, 1, .2f), null, false);
            shimmer.rectTransform.localRotation = Quaternion.Euler(0, 0, -22);
            shimmerRt = shimmer.rectTransform;

            var ribbon = Chunky("Header ribbon", plaque, new Vector2(0, 1), new Vector2(210, 26), new Vector2(124, 4), GoldTop, GoldBottom, 3);
            Label(ribbon.rectTransform, "TROPICAL OPEN  ·  SET 1", 14, new Vector2(0, 1), new Vector2(210, 26), Navy, TextAnchor.MiddleCenter, 0);

            rows[0] = BuildRow(body.rectTransform, 22, SunTop, SunBottom);
            rows[1] = BuildRow(body.rectTransform, -24, SeaTop, SeaBottom);
            ballIcon = Box("Serve ball", body.rectTransform, new Vector2(0, .5f), new Vector2(26, 26), new Vector2(20, 22), Color.white, ball, false).rectTransform;

            noticeBack = Chunky("Notice", group, new Vector2(0, 1), new Vector2(190, 32), new Vector2(24 + 110, -30 - 136), SunTop, SunBottom, 3);
            noticeGradient = noticeBack.GetComponent<UiGradient>();
            notice = Label(noticeBack.rectTransform, "", 18, new Vector2(0, 1), new Vector2(190, 32), Color.white, TextAnchor.MiddleCenter, 1.6f);
            noticeBack.transform.parent.gameObject.SetActive(false);

            // Grade badge (Higgsfield word-art), top-right.
            grades[(int)Timing.Ok] = LoadSprite("Tennis/UI/grade-ok"); grades[(int)Timing.Good] = LoadSprite("Tennis/UI/grade-good");
            grades[(int)Timing.Great] = LoadSprite("Tennis/UI/grade-great"); grades[(int)Timing.Excellent] = LoadSprite("Tennis/UI/grade-excellent");
            grades[(int)Timing.Perfect] = LoadSprite("Tennis/UI/grade-perfect"); super = LoadSprite("Tennis/UI/grade-super");
            badge = Box("Grade badge", root, new Vector2(1, 1), new Vector2(250, 100), new Vector2(-24 - 125, -18 - 50), Color.white, null, false);
            badge.preserveAspect = true; badge.enabled = false;
            var captionBody = Chunky("Badge caption", root, new Vector2(1, 1), new Vector2(200, 30), new Vector2(-24 - 125, -18 - 118), CaptionTop, NavyDeep, 3);
            badgeCaptionBack = (RectTransform)captionBody.transform.parent;
            badgeCaption = Label(captionBody.rectTransform, "", 16, new Vector2(0, 1), new Vector2(200, 30), Color.white, TextAnchor.MiddleCenter, 1.2f);
            badgeCaptionBack.gameObject.SetActive(false);

            // Calls, upper middle.
            foreach (var key in new[] { "ace", "winner", "out", "net", "fault", "double-fault", "deuce", "game", "match-point", "missed", "point", "second-serve", "play" })
            {
                var s = LoadSprite("Tennis/UI/call-" + key); if (s) callSprites[key] = s;
            }
            callArt = Box("Call", root, new Vector2(.5f, .5f), new Vector2(360, 200), new Vector2(0, 110), Color.white, null, false);
            callArt.preserveAspect = true; callArt.enabled = false;
            callFallback = Label(root, "", 64, new Vector2(0, 110), new Vector2(900, 90), Color.white, TextAnchor.MiddleCenter, 3.5f);
            // Gradient before the outlines so the outline copies stay navy.
            DestroyImmediate(callFallback.GetComponent<Shadow>());
            foreach (var o in callFallback.GetComponents<Outline>()) DestroyImmediate(o);
            UiGradient.On(callFallback, SunTop, SunBottom);
            foreach (var d in new[] { new Vector2(3.5f, -3.5f), new Vector2(-3.5f, 3.5f) }) { var o = callFallback.gameObject.AddComponent<Outline>(); o.effectColor = Navy; o.effectDistance = d; }
            var subBody = Chunky("Call caption", root, new Vector2(.5f, .5f), new Vector2(320, 34), new Vector2(0, 8), CaptionTop, NavyDeep, 3);
            callSubBack = (RectTransform)subBody.transform.parent;
            callSub = Label(subBody.rectTransform, "", 17, new Vector2(0, 1), new Vector2(320, 34), Color.white, TextAnchor.MiddleCenter, 1.2f);
            callSubBack.gameObject.SetActive(false);

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

        Row BuildRow(RectTransform body, float y, Color top, Color bottom)
        {
            var row = new Row();
            var tag = Chunky("Name tag", body, new Vector2(0, .5f), new Vector2(170, 36), new Vector2(40 + 85, y), top, bottom, 3);
            row.tag = (RectTransform)tag.transform.parent;
            row.name = Label(tag.rectTransform, "", 24, new Vector2(0, 1), new Vector2(170, 36), Color.white, TextAnchor.MiddleCenter, 2f);
            var coin = Chunky("Games coin", body, new Vector2(0, .5f), new Vector2(40, 40), new Vector2(262, y), GoldTop, GoldBottom, 3);
            row.coin = (RectTransform)coin.transform.parent;
            row.games = Label(coin.rectTransform, "0", 24, new Vector2(0, 1), new Vector2(40, 40), Navy, TextAnchor.MiddleCenter, 0);
            var cell = Chunky("Points", body, new Vector2(0, .5f), new Vector2(76, 40), new Vector2(334, y), PearlTop, PearlBottom, 3);
            row.cell = (RectTransform)cell.transform.parent;
            row.points = Label(cell.rectTransform, "0", 28, new Vector2(0, 1), new Vector2(76, 40), Navy, TextAnchor.MiddleCenter, 0);
            row.flash = Box("Flash", cell.rectTransform, new Vector2(.5f, .5f), new Vector2(76, 40), Vector2.zero, Color.white, rounded);
            row.flash.canvasRenderer.SetAlpha(0);
            return row;
        }

        // ---------------------------------------------------------------- state

        static string Call(int p) => p switch { 0 => "0", 1 => "15", 2 => "30", _ => "40" };

        static void Points(TennisMatch m, out string mine, out string theirs)
        {
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
                Burst(Centre(gameChanged ? row.coin : row.cell), 7, gameChanged ? GoldTop : Color.white, 170);
            }
        }

        Vector2 Centre(RectTransform rt) => root.InverseTransformPoint(rt.TransformPoint(rt.rect.center));

        public void Refresh(TennisGame game)
        {
            var m = game.Match;
            Points(m, out string pa, out string pb);
            SetRow(rows[0], PlayerName, Digits(m.PlayerGames), pa);
            SetRow(rows[1], OpponentName, Digits(m.OpponentGames), pb);
            shownServer = m.PlayerServes ? 0 : 1;
            string n = m.Complete ? null : TennisGame.IsMatchPoint(m) ? "MATCH POINT" : game.SecondServe ? "2ND SERVE"
                : m.PlayerPoints == m.OpponentPoints && m.PlayerPoints >= 3 ? "DEUCE" : null;
            if (n != shownNotice)
            {
                shownNotice = n;
                noticeBack.transform.parent.gameObject.SetActive(n != null);
                if (n != null)
                {
                    notice.text = n;
                    var (top, bottom) = n == "DEUCE" ? (new Color(.95f, .6f, 1f), new Color(.55f, .2f, .85f)) : n == "2ND SERVE" ? (SeaTop, SeaBottom) : (SunTop, new Color(1f, .3f, .15f));
                    noticeGradient.Top = top; noticeGradient.Bottom = bottom; noticeBack.SetVerticesDirty();
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

        /// A clean hit: the grade badge pops in top-right with the shot under it.
        public void ShowGrade(Timing grade, bool supercharged, string detail)
        {
            var sprite = supercharged ? super : grades[Mathf.Clamp((int)grade, 1, 5)];
            if (!sprite) return;
            badge.sprite = sprite; badge.enabled = true; badgeSuper = supercharged;
            badge.rectTransform.sizeDelta = supercharged ? new Vector2(260, 120) : new Vector2(250, 100);
            badgeCaption.text = detail;
            badgeCaptionBack.gameObject.SetActive(true);
            badgeAt = HudClock.Now;
            if (grade >= Timing.Excellent || supercharged)
                Burst(Centre(badge.rectTransform), supercharged ? 12 : 8, supercharged ? SeaTop : GoldTop, 230);
        }

        /// A point, game or state call. Queued behind whatever is showing.
        public void ShowCall(string key, string sub, bool good)
        {
            if (calls.Count >= 3) calls.Dequeue();
            calls.Enqueue((key, sub, good));
            if (HudClock.Now - callAt > CallHold) NextCall();
        }

        void NextCall()
        {
            if (calls.Count == 0) return;
            var (key, sub, good) = calls.Dequeue();
            callGood = good;
            if (callSprites.TryGetValue(key.ToLowerInvariant().Replace(' ', '-'), out var sprite))
            {
                callArt.sprite = sprite; callArt.enabled = true; callFallback.text = "";
            }
            else { callArt.enabled = false; callFallback.text = key + "!"; }
            bool hasSub = !string.IsNullOrEmpty(sub);
            callSubBack.gameObject.SetActive(hasSub);
            if (hasSub) callSub.text = sub;
            callAt = HudClock.Now;
            if (good) Burst(new Vector2(0, 110), 12, GoldTop, 320);
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

        void Animate()
        {
            float now = HudClock.Now;
            // The plaque swings in from the left with a bounce when the HUD first appears.
            float intro = Age(now, introAt);
            plaque.anchoredPosition = new Vector2(24 + 204 - (intro < 1.2f ? 420 * Mathf.Exp(-intro * 6) * Mathf.Cos(intro * 9) : 0), -30 - 60);
            // A gloss sweep every few seconds.
            shimmerRt.anchoredPosition = new Vector2(-60 + Mathf.Repeat(now, 5.5f) * 520, 0);
            // The server's ball bounces beside their tag.
            var server = rows[Mathf.Max(0, shownServer)];
            ballIcon.anchoredPosition = new Vector2(20, server.tag.anchoredPosition.y + Mathf.Abs(Mathf.Sin(now * 4.2f)) * 7);
            ballIcon.localRotation = Quaternion.Euler(0, 0, now * -140);
            foreach (var row in rows)
            {
                float p = Age(now, row.popAt);
                float scale = p < 1.2f ? Elastic(p, .45f) : 1;
                row.cell.localScale = row.coin.localScale = Vector3.one * scale;
                row.flash.canvasRenderer.SetAlpha(p < .35f ? 1 - p / .35f : 0);
                float w = Age(now, row.wiggleAt);
                row.tag.localRotation = Quaternion.Euler(0, 0, w < 1f ? Mathf.Sin(w * 26) * 7 * Mathf.Exp(-w * 5) : 0);
            }
            // The notice pill breathes so it is noticed without shouting.
            if (shownNotice != null) noticeBack.transform.parent.localScale = Vector3.one * (1 + Mathf.Sin(now * 5) * .04f);
            for (int i = 0; i < pips.Length; i++)
                pips[i].rectTransform.localScale = Vector3.one * (i < shownStreak ? 1 + Mathf.Sin(now * 8 + i) * .12f : 1);

            // Grade badge: pop with a tilt that settles, hold, then lift and fade.
            float since = Age(now, badgeAt);
            if (since < 1.5f)
            {
                float s = since < .08f ? Mathf.Lerp(.2f, 1.25f, since / .08f) : Elastic(since - .08f, .25f, 20, 8);
                float exit = Mathf.Clamp01((since - 1.2f) / .3f);
                badge.rectTransform.localScale = Vector3.one * s * (1 + exit * .15f);
                badge.rectTransform.localRotation = Quaternion.Euler(0, 0, (since < .5f ? -12 * Mathf.Exp(-since * 6) * Mathf.Cos(since * 20) : 0) + (badgeSuper ? Mathf.Sin(now * 30) * 1.5f : 0));
                badge.canvasRenderer.SetAlpha(1 - exit);
                // The caption drops in just after the word and leaves with it.
                float c = Mathf.Clamp01((since - .06f) / .1f);
                badgeCaptionBack.localScale = Vector3.one * (c < 1 ? Mathf.Lerp(.5f, 1.1f, c) : Elastic(since - .16f, .1f));
                SetGroupAlpha(badgeCaptionBack, c > 0 ? 1 - exit : 0);
            }
            else if (badge.enabled) { badge.enabled = false; badgeCaptionBack.gameObject.SetActive(false); }

            // Calls: slam in from big, settle with a shake, hold, then zip up and away.
            float t = Age(now, callAt);
            if (t < CallHold)
            {
                float land = Mathf.Clamp01(t / .13f);
                float s = t < .13f ? Mathf.Lerp(2.3f, .9f, land * land) : Elastic(t - .13f, -.1f, 22, 9);
                float exit = Mathf.Clamp01((t - (CallHold - .25f)) / .25f);
                var shake = t < .45f ? new Vector2(Mathf.Sin(t * 90) * 9, Mathf.Cos(t * 77) * 6) * Mathf.Exp(-t * 9) : Vector2.zero;
                Graphic art = callArt.enabled ? callArt : callFallback;
                art.rectTransform.anchoredPosition = new Vector2(0, 110 + exit * 40) + shake;
                art.rectTransform.localScale = Vector3.one * s * (1 - exit * .2f);
                art.rectTransform.localRotation = Quaternion.Euler(0, 0, (callGood ? -4 : 3) * Mathf.Exp(-t * 4));
                art.canvasRenderer.SetAlpha(Mathf.Min(land * 1.5f, 1 - exit));
                if (callSubBack.gameObject.activeSelf)
                {
                    float c = Mathf.Clamp01((t - .15f) / .15f);
                    callSubBack.anchoredPosition = new Vector2(0, 8 + exit * 40 - (1 - c) * 20);
                    SetGroupAlpha(callSubBack, Mathf.Min(c, 1 - exit));
                }
            }
            else
            {
                if (callArt.enabled || callFallback.text.Length > 0) { callArt.enabled = false; callFallback.text = ""; callSubBack.gameObject.SetActive(false); }
                if (calls.Count > 0) NextCall();
            }

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

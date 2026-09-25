using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The first screen (the user's pick, option C "On the Tee", blender/previews/home_screen):
    /// the game itself behind it, the golfer at address on the first tee with the hole
    /// stretching away; the GOLF ARCADE logo over the sky with a ball bouncing off it; a small
    /// round TV button for the big screen; and a dock at the foot with a big round yellow PLAY
    /// between COURSE (which hole, or the round) and GOLFER (their face, live). Over the dock,
    /// who plays: SOLO, 2 PLAYERS on this phone, or ONLINE; top left, the PROFILE. The game wires
    /// the buttons and calls Refresh.
    public sealed class HomeScreen
    {
        public readonly RectTransform Root;
        public readonly HoldButton Play, Course, Golfer, BigScreen, Profile;
        /// SOLO, 2 PLAYERS, ONLINE (GameSetup.PlayMode order).
        public readonly HoldButton[] Modes = new HoldButton[3];
        readonly Image[] modeFills = new Image[3];
        readonly Text[] modeWords = new Text[3];
        readonly Text profileName;
        /// The golfer's face in the GOLFER button: the game points a camera at it.
        public readonly RawImage Avatar;
        public readonly Text Build;

        readonly Text courseTag, golferTag;
        readonly Image tvDot;

        public HomeScreen(Transform parent)
        {
            // (named "Menu": it is the menu; nothing on it but the buttons takes a touch)
            Root = new GameObject("Menu").AddComponent<RectTransform>();
            Root.SetParent(parent, false);
            Root.anchorMin = Vector2.zero; Root.anchorMax = Vector2.one; Root.offsetMin = Root.offsetMax = Vector2.zero;

            BuildLogo();

            // the big screen: a small round TV button in the corner, a dot lit while it's on
            var tv = UiKit.Pill(Root, "Big screen", UiKit.ArcadeBlue, new Vector2(1, 1), new Vector2(-96, -86), new Vector2(116, 116), out var tvFill, 5f);
            var screen = UiKit.Panel(tvFill.transform, "Screen", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(58, 42));
            screen.rectTransform.pivot = new Vector2(0.5f, 0.5f); screen.raycastTarget = false;
            var glass = UiKit.Panel(screen.transform, "Glass", UiKit.ArcadeBlueDeep, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            glass.rectTransform.offsetMin = new Vector2(6, 6); glass.rectTransform.offsetMax = new Vector2(-6, -6); glass.raycastTarget = false;
            var stand = UiKit.Panel(tvFill.transform, "Stand", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -24), new Vector2(26, 7));
            stand.rectTransform.pivot = new Vector2(0.5f, 0.5f); stand.raycastTarget = false;
            tvDot = UiKit.Panel(tv, "On", UiKit.Hex("5CD65C"), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -14), new Vector2(30, 30));
            tvDot.sprite = UiKit.Circle; tvDot.type = Image.Type.Simple; tvDot.raycastTarget = false; tvDot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            BigScreen = Hold(tv, tvFill, UiKit.ArcadeBlue);

            // the profile: the same round button, top left, and the player's name under it
            var me = UiKit.Pill(Root, "Profile", UiKit.ArcadeBlue, new Vector2(0, 1), new Vector2(96, -86), new Vector2(116, 116), out var meFill, 5f);
            foreach (var img in me.GetComponentsInChildren<Image>()) img.type = Image.Type.Simple;
            Icons.Place(meFill.transform, "face", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, 64);
            Profile = Hold(me, meFill, UiKit.ArcadeBlue);
            profileName = UiKit.Label(Root, "Profile name", 26, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(38, -168), new Vector2(360, 40), UiKit.Display, false);
            profileName.color = Color.white; profileName.raycastTarget = false;
            profileName.gameObject.AddComponent<Shadow>().effectColor = new Color(0.05f, 0.1f, 0.3f, 0.6f);

            // who plays: three halves of one pill over the dock, the chosen one lit yellow
            var modes = UiKit.Pill(Root, "Players", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0), new Vector2(0, 520), new Vector2(930, 100), out var modesFill, 5f);
            string[] words = { "SOLO", "2 PLAYERS", "ONLINE" };
            for (int i = 0; i < 3; i++)
            {
                var third = UiKit.Panel(modesFill.transform, words[i], UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 300, 0), new Vector2(292, 78));
                third.sprite = UiKit.Circle; third.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var w = UiKit.Label(third.transform, "Word", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                w.text = words[i]; w.raycastTarget = false;
                var hold = third.gameObject.AddComponent<HoldButton>();
                hold.Fill = third;
                Modes[i] = hold; modeFills[i] = third; modeWords[i] = w;
            }

            // the dock: COURSE, the big PLAY, GOLFER
            var dock = UiKit.Pill(Root, "Dock", UiKit.ArcadeBlue, new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(1010, 250), out _, 5f, false);
            foreach (var img in dock.GetComponentsInChildren<Image>()) img.sprite = UiKit.RoundedLarge;

            var course = Round("Course", new Vector2(-330, 170), 212, UiKit.ArcadeBlueDeep, out var courseFill);
            Icons.Place(courseFill.transform, "flag", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 30), 86);
            Word(courseFill.transform, "COURSE", -50, 30);
            courseTag = Tag(course, "Hole", "HOLE 12");
            Course = Hold(course, courseFill, UiKit.ArcadeBlueDeep);

            var golfer = Round("Golfer", new Vector2(330, 170), 212, UiKit.ArcadeBlueDeep, out var golferFill);
            var face = UiKit.Panel(golferFill.transform, "Face", UiKit.Hex("9CD4FF"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 28), new Vector2(112, 112));
            face.sprite = UiKit.Circle; face.type = Image.Type.Simple; face.raycastTarget = false; face.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            face.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Avatar = new GameObject("Avatar").AddComponent<RawImage>();
            Avatar.transform.SetParent(face.transform, false);
            Avatar.rectTransform.anchorMin = Vector2.zero; Avatar.rectTransform.anchorMax = Vector2.one; Avatar.rectTransform.offsetMin = Avatar.rectTransform.offsetMax = Vector2.zero;
            Avatar.raycastTarget = false; Avatar.enabled = false;
            Word(golferFill.transform, "GOLFER", -50, 30);
            golferTag = Tag(golfer, "Kit", "NAVY KIT");
            Golfer = Hold(golfer, golferFill, UiKit.ArcadeBlueDeep);

            var play = Round("Play", new Vector2(0, 250), 330, UiKit.ArcadeYellow, out var playFill, 10f);
            var shine = UiKit.Panel(playFill.transform, "Shine", new Color(1, 1, 1, 0.22f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 62), new Vector2(210, 110));
            shine.sprite = UiKit.Circle; shine.type = Image.Type.Simple; shine.raycastTarget = false; shine.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var go = UiKit.Chunky(playFill.transform, "Word", 88, UiKit.ArcadeInk, new Color(1, 1, 1, 0), 0f);
            go.text = "PLAY";
            play.gameObject.AddComponent<Breathe>();
            Play = Hold(play, playFill, UiKit.ArcadeYellow);

            // which build this is, for checking what a phone is running, under the dock
            Build = UiKit.Label(Root, "Build", 20, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(900, 26), UiKit.Body, false);
            Build.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            Build.color = new Color(1, 1, 1, 0.6f); Build.raycastTarget = false;
        }

        /// GOLF in white over ARCADE in yellow, each a navy-outlined word standing on a deeper
        /// navy extrusion, and a ball bouncing off the end of GOLF.
        void BuildLogo()
        {
            var logo = new GameObject("Logo").AddComponent<RectTransform>();
            logo.SetParent(Root, false);
            logo.anchorMin = logo.anchorMax = new Vector2(0.5f, 1); logo.pivot = new Vector2(0.5f, 0.5f);
            logo.anchoredPosition = new Vector2(0, -330); logo.sizeDelta = new Vector2(1000, 400);
            float Word3D(string text, int size, Color fill, float y)
            {
                var holder = new GameObject(text).AddComponent<RectTransform>();
                holder.SetParent(logo, false);
                holder.anchorMin = holder.anchorMax = new Vector2(0.5f, 0.5f); holder.pivot = new Vector2(0.5f, 0.5f);
                holder.anchoredPosition = new Vector2(0, y); holder.sizeDelta = new Vector2(1000, size * 1.25f);
                var depth = UiKit.Chunky(holder, "Depth", size, UiKit.ArcadeBlueDeep, UiKit.ArcadeInk, 9f);
                depth.text = text; depth.rectTransform.anchoredPosition = new Vector2(0, -16);
                var face = UiKit.Chunky(holder, "Face", size, fill, UiKit.ArcadeInk, 9f);
                face.text = text;
                return face.preferredWidth;
            }
            float golf = Word3D("GOLF", 200, Color.white, 92);
            Word3D("ARCADE", 168, UiKit.ArcadeYellow, -96);

            // the ball, off the F, with its streak
            var ball = new GameObject("Ball").AddComponent<RectTransform>();
            ball.SetParent(logo, false);
            ball.anchorMin = ball.anchorMax = new Vector2(0.5f, 0.5f); ball.pivot = new Vector2(0.5f, 0.5f);
            ball.anchoredPosition = new Vector2(golf / 2 + 70, 128); ball.sizeDelta = new Vector2(100, 100);
            var rim = UiKit.Panel(ball, "Rim", UiKit.ArcadeInk, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            rim.sprite = UiKit.Circle; rim.type = Image.Type.Simple; rim.raycastTarget = false;
            var white = UiKit.Panel(rim.transform, "Ball", Color.white, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            white.sprite = UiKit.Circle; white.type = Image.Type.Simple; white.raycastTarget = false;
            white.rectTransform.offsetMin = new Vector2(8, 8); white.rectTransform.offsetMax = new Vector2(-8, -8);
            foreach (var (x, y) in new[] { (-14f, 14f), (10f, 18f), (18f, -6f), (-4f, -2f), (-18f, -16f), (4f, -20f) })
            {
                var dimple = UiKit.Panel(white.transform, "Dimple", UiKit.Hex("C9D6EA"), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(12, 12));
                dimple.sprite = UiKit.Circle; dimple.type = Image.Type.Simple; dimple.raycastTarget = false; dimple.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            }
            ball.gameObject.AddComponent<Bounce>();
        }

        /// A round button: white rim, `fill` inside, a soft shadow.
        RectTransform Round(string name, Vector2 pos, float size, Color fill, out Image inner, float rim = 7f)
        {
            var r = UiKit.Pill(Root, name, fill, new Vector2(0.5f, 0), pos, new Vector2(size, size), out inner, rim);
            foreach (var img in r.GetComponentsInChildren<Image>()) img.type = Image.Type.Simple;
            return r;
        }

        static Text Word(Transform parent, string text, float y, int size)
        {
            var t = UiKit.Label(parent, text, size, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(190, 40), UiKit.Display, false);
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            t.text = text; t.color = Color.white; t.raycastTarget = false;
            return t;
        }

        /// A small yellow tag hanging off the bottom of a round button: what it's set to.
        static Text Tag(RectTransform button, string name, string text)
        {
            var tag = UiKit.Pill(button, name, UiKit.ArcadeYellow, new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(190, 50), out var fill, 3f);
            var t = UiKit.Label(fill.transform, "Text", 24, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            t.rectTransform.offsetMin = new Vector2(12, 0); t.rectTransform.offsetMax = new Vector2(-12, 0);
            t.text = text; t.color = UiKit.ArcadeInk; t.raycastTarget = false;
            Icons.Fit(t, 16, 24);
            return t;
        }

        static HoldButton Hold(RectTransform button, Image fill, Color rest)
        {
            var hit = button.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            hit.sprite = UiKit.Circle;
            var hold = button.gameObject.AddComponent<HoldButton>();
            hold.Fill = fill; hold.RestColor = rest;
            return hold;
        }

        /// The course (a hole, or the round), the golfer's kit, and whether the big screen is on.
        public void Refresh(string course, string golfer, bool tvOn)
        {
            courseTag.text = course.ToUpperInvariant();
            golferTag.text = golfer.ToUpperInvariant();
            tvDot.color = tvOn ? UiKit.Hex("5CD65C") : new Color(0.75f, 0.8f, 0.9f, 0.9f);
        }

        /// Lights the chosen mode (0 solo, 1 two players on this phone, 2 online) and names the
        /// profile playing.
        public void ShowMode(int mode, string player)
        {
            for (int i = 0; i < 3; i++)
            {
                var c = i == mode ? UiKit.ArcadeYellow : new Color(1, 1, 1, 0);
                modeFills[i].color = c; Modes[i].RestColor = c;
                modeWords[i].color = i == mode ? UiKit.ArcadeInk : Color.white;
            }
            profileName.text = (player ?? "").ToUpperInvariant();
        }

        public void Destroy() { if (Root) Object.Destroy(Root.gameObject); }

        /// The logo's ball: hopping off the end of GOLF, squashing a little as it lands.
        sealed class Bounce : MonoBehaviour
        {
            Vector2 home;
            void Start() { home = ((RectTransform)transform).anchoredPosition; }
            void Update()
            {
                float t = Mathf.Repeat(Time.unscaledTime / 1.4f, 1f);
                float hop = 4f * t * (1f - t);                       // a parabola, 0 → 1 → 0
                ((RectTransform)transform).anchoredPosition = home + new Vector2(0, 34f * hop);
                float squash = t < 0.08f || t > 0.92f ? 0.1f : 0f;
                transform.localScale = new Vector3(1 + squash, 1 - squash, 1);
            }
        }

        /// PLAY breathing: a slow swell, so the eye goes to it.
        sealed class Breathe : MonoBehaviour
        {
            void Update() => transform.localScale = Vector3.one * (1f + 0.035f * Mathf.Sin(Time.unscaledTime * 2.6f));
        }
    }

    /// The course screen, opened from COURSE on the home screen: the hole itself filling the
    /// picture while the camera circles it high up (the game does that), its name big across the
    /// top on a yellow tab with the number, par and yards, small see-through white arrows at the
    /// sides to go between the holes, a dot for each, THIS HOLE or FULL ROUND, and SELECT.
    public sealed class CourseScreen
    {
        public readonly RectTransform Root;
        public readonly HoldButton Back, Previous, Next, ThisHole, FullRound, Select;

        readonly Text title, info;
        readonly RectTransform infoPill, titleHolder;
        readonly Image[] dots;
        readonly Image thisFill, fullFill;
        readonly Text thisWord, fullWord;
        readonly CardPop pop;

        static readonly Color Frost = new(1f, 1f, 1f, 0.26f), FrostRim = new(1f, 1f, 1f, 0.75f);

        public CourseScreen(Transform parent, int holes)
        {
            Root = new GameObject("Course select").AddComponent<RectTransform>();
            Root.SetParent(parent, false);
            Root.anchorMin = Vector2.zero; Root.anchorMax = Vector2.one; Root.offsetMin = Root.offsetMax = Vector2.zero;

            // back: solid, so it reads over the sky
            var back = UiKit.Pill(Root, "Back", UiKit.ArcadeBlue, new Vector2(0, 1), new Vector2(96, -86), new Vector2(112, 112), out var backFill, 5f);
            foreach (var img in back.GetComponentsInChildren<Image>()) img.type = Image.Type.Simple;
            Arrow(backFill.transform, false, 44);
            var backHit = back.gameObject.AddComponent<Image>(); backHit.color = Color.clear; backHit.sprite = UiKit.Circle;
            Back = back.gameObject.AddComponent<HoldButton>();
            Back.Fill = backFill; Back.RestColor = UiKit.ArcadeBlue;

            // the hole's name, big, and the tab under it
            titleHolder = new GameObject("Title").AddComponent<RectTransform>();
            titleHolder.SetParent(Root, false);
            titleHolder.anchorMin = titleHolder.anchorMax = new Vector2(0.5f, 1); titleHolder.pivot = new Vector2(0.5f, 0.5f);
            titleHolder.anchoredPosition = new Vector2(0, -240); titleHolder.sizeDelta = new Vector2(1000, 150);
            title = UiKit.Chunky(titleHolder, "Name", 112, Color.white, UiKit.ArcadeInk, 7f);
            Icons.Fit(title, 60, 112);
            pop = titleHolder.gameObject.AddComponent<CardPop>();
            infoPill = UiKit.Pill(Root, "Info", UiKit.ArcadeYellow, new Vector2(0.5f, 1), new Vector2(0, -360), new Vector2(560, 72), out var infoFill, 4f);
            info = UiKit.Label(infoFill.transform, "Text", 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            info.color = UiKit.ArcadeInk; info.raycastTarget = false;

            Previous = Frosted("Previous hole", "◀", new Vector2(0, 0.52f), new Vector2(84, 0), 124, 54);
            Next = Frosted("Next hole", "▶", new Vector2(1, 0.52f), new Vector2(-84, 0), 124, 54);

            // a dot for each hole
            dots = new Image[holes];
            for (int i = 0; i < holes; i++)
            {
                var d = UiKit.Panel(Root, $"Dot {i}", Color.white, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2((i - (holes - 1) / 2f) * 46, 470), new Vector2(24, 24));
                d.sprite = UiKit.Circle; d.type = Image.Type.Simple; d.raycastTarget = false; d.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                dots[i] = d;
            }

            // THIS HOLE | FULL ROUND
            var toggle = UiKit.Pill(Root, "Round or hole", UiKit.ArcadeBlueDeep, new Vector2(0.5f, 0), new Vector2(0, 350), new Vector2(780, 104), out var toggleFill, 5f);
            (HoldButton, Image, Text) Half(string name, string text, float x)
            {
                var half = UiKit.Panel(toggleFill.transform, name, UiKit.ArcadeYellow, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 0), new Vector2(372, 80));
                half.sprite = UiKit.Circle; half.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var w = UiKit.Label(half.transform, "Word", 32, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
                w.text = text; w.raycastTarget = false;
                var hold = half.gameObject.AddComponent<HoldButton>();
                hold.Fill = half;
                return (hold, half, w);
            }
            (ThisHole, thisFill, thisWord) = Half("This hole", "THIS HOLE", -188);
            (FullRound, fullFill, fullWord) = Half("Full round", "FULL ROUND", 188);

            // SELECT
            var select = UiKit.Pill(Root, "Select", UiKit.ArcadeYellow, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(900, 150), out var selectFill, 6f);
            var sw = UiKit.Chunky(selectFill.transform, "Word", 66, UiKit.ArcadeInk, new Color(1, 1, 1, 0), 0f);
            sw.text = "SELECT";
            var hit = select.gameObject.AddComponent<Image>(); hit.color = Color.clear;
            Select = select.gameObject.AddComponent<HoldButton>();
            Select.Fill = selectFill; Select.RestColor = UiKit.ArcadeYellow;
        }

        /// A small see-through white round button with a white arrow: over the course, not on it.
        HoldButton Frosted(string name, string glyph, Vector2 anchor, Vector2 pos, float size, int glyphSize)
        {
            var b = UiKit.Pill(Root, name, Frost, anchor, pos, new Vector2(size, size), out var fill, 4f);
            foreach (var img in b.GetComponentsInChildren<Image>()) img.type = Image.Type.Simple;
            b.Find("Rim").GetComponent<Image>().color = FrostRim;
            b.Find("Shadow").GetComponent<Image>().color = new Color(0.03f, 0.08f, 0.25f, 0.12f);
            // the rim is a ring: the see-through fill is cut from the inside of an opaque disc
            fill.color = Frost;
            var rimImage = b.Find("Rim").GetComponent<Image>();
            rimImage.sprite = UiKit.RingOf(128, 8);
            var g = Arrow(fill.transform, glyph == "▶", glyphSize);
            var sh = g.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0.05f, 0.1f, 0.3f, 0.35f); sh.effectDistance = new Vector2(0, -2);
            var hit = b.gameObject.AddComponent<Image>(); hit.color = Color.clear; hit.sprite = UiKit.Circle;
            var hold = b.gameObject.AddComponent<HoldButton>();
            hold.Fill = fill; hold.RestColor = Frost;
            return hold;
        }

        /// A white arrow, the play icon pointing the way (left turned round), a touch off centre
        /// the way it points so it sits in the middle to the eye.
        static Image Arrow(Transform parent, bool right, float size)
        {
            var icon = Icons.Place(parent, "play", Color.white, new Vector2(0.5f, 0.5f), new Vector2(right ? 3 : -3, 0), size);
            if (!right) icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            return icon;
        }

        /// The hole on show (`index` of `count`), and whether the choice is the whole round.
        public void Show(string name, int number, int par, double yards, int index, bool fullRound)
        {
            if (title.text != name.ToUpperInvariant()) { pop.enabled = false; pop.enabled = true; }
            title.text = name.ToUpperInvariant();
            info.text = $"HOLE {number}  ·  PAR {par}  ·  {yards:F0} YD";
            infoPill.sizeDelta = new Vector2(info.preferredWidth + 70, infoPill.sizeDelta.y);
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].color = i == index ? UiKit.ArcadeYellow : new Color(1, 1, 1, 0.7f);
                dots[i].rectTransform.sizeDelta = Vector2.one * (i == index ? 32 : 22);
            }
            void Light(HoldButton b, Image fill, Text word, bool on)
            {
                var c = on ? UiKit.ArcadeYellow : new Color(1, 1, 1, 0);
                fill.color = c; b.RestColor = c;
                word.color = on ? UiKit.ArcadeInk : Color.white;
            }
            Light(ThisHole, thisFill, thisWord, !fullRound);
            Light(FullRound, fullFill, fullWord, fullRound);
        }

        public void Destroy() { if (Root) Object.Destroy(Root.gameObject); }
    }
}

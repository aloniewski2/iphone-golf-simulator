using UnityEngine;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The first screen: Adnan's Clubhouse (his ClubHomeScreen, the phone layout), on the Island Sports Club
    /// system (UI/Club.cs). The painted clubhouse behind (Game/ClubStage.cs), the crest and where you are
    /// along the top with your player's chip, your golfer standing on a lit disc, a greeting, who plays (solo,
    /// two on this phone, online, the Open), the big sun-yellow PLAY slab, the course card, and the cards for
    /// your look and your records. The game wires the buttons and calls Refresh.
    public sealed class HomeScreen
    {
        public readonly RectTransform Root;
        public readonly HoldButton Play, Course, Golfer, BigScreen, Profile, Records;
        /// SOLO, 2 PLAYERS, ONLINE, OPEN (GameSetup.PlayMode order).
        public readonly HoldButton[] Modes = new HoldButton[4];
        readonly Club.Pill[] modePills = new Club.Pill[4];
        public readonly Text Build;
        readonly Club.Header header;
        readonly Club.Card course, look;
        readonly Text playSub, hello;
        readonly Image tvDot;
        readonly RectTransform stage;

        public HomeScreen(Transform parent)
        {
            // (named "Menu": it is the menu; nothing on it but the buttons takes a touch)
            Root = new GameObject("Menu").AddComponent<RectTransform>();
            Root.SetParent(parent, false);
            Root.anchorMin = Vector2.zero; Root.anchorMax = Vector2.one; Root.offsetMin = Root.offsetMax = Vector2.zero;

            header = new Club.Header(Root, new[] { "Clubhouse" });
            Profile = header.Chip;

            // where the golfer stands (the camera frames them in it), with the big screen in its corner
            stage = Club.Box(Root, "Stage", new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            stage.offsetMin = new Vector2(54, Bottom); stage.offsetMax = new Vector2(-54, -196);
            var tv = new Club.Pill(stage, "Big screen", "Big screen", 96);
            tv.Root.anchorMin = tv.Root.anchorMax = new Vector2(1, 1); tv.Root.pivot = new Vector2(1, 1); tv.Root.anchoredPosition = Vector2.zero;
            tvDot = Club.Box(tv.Root, "On", new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -10), new Vector2(28, 28)).gameObject.AddComponent<Image>();
            tvDot.sprite = UiKit.Circle; tvDot.raycastTarget = false;
            BigScreen = tv.Button;

            // the greeting, the players, PLAY, the course, your look and your records, up from the foot
            float y = 34;
            var look = new Club.Card(Root, "Your look", "kit", "Your look", "Style your golfer", Club.Coral, true, 0.82f);
            Place(look.Root, 0, 0.5f, 54, 12, y, 218);
            this.look = look; Golfer = look.Button;
            var records = new Club.Card(Root, "Records", "trophy", "Records", "Bests & unlocks", Club.Violet, true, 0.82f);
            Place(records.Root, 0.5f, 1, 12, 54, y, 218);
            Records = records.Button;
            y += 218 + 42;
            course = new Club.Card(Root, "Course", "golf", "Cliffside", "Full round · change course or hole", Club.Green, true, 0.92f);
            Place(course.Root, 0, 1, 54, 54, y, 236);
            Course = course.Button;
            y += 236 + 42;
            Play = PlaySlab(y, out playSub);
            y += 280 + 44;
            string[] words = { "Solo", "2 players", "Online", "The Open" };
            float x = 54;
            for (int i = 0; i < words.Length; i++)
            {
                var p = modePills[i] = new Club.Pill(Root, words[i], words[i], 100);
                p.Root.anchorMin = p.Root.anchorMax = new Vector2(0, 0); p.Root.pivot = new Vector2(0, 0);
                p.Root.anchoredPosition = new Vector2(x, y);
                x += p.Root.sizeDelta.x + 18;
                Modes[i] = p.Button;
            }
            y += 100 + 30;
            var greet = Club.Box(Root, "Greeting", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, y), new Vector2(-108, 170));
            hello = Club.Words(greet, "Hello", "WELCOME BACK", Club.Caps, 40, Club.Sun, TextAnchor.UpperLeft);
            var what = Club.Words(greet, "Question", "Ready for a round?", Club.Display, 118, Color.white, TextAnchor.LowerLeft, 12);
            what.horizontalOverflow = HorizontalWrapMode.Overflow;
            Bottom = y + 170;
            stage.offsetMin = new Vector2(54, Bottom);

            int n = 0;
            foreach (var c in new Component[] { greet, modePills[0].Root, Play, course.Root, look.Root, records.Root }) Club.Enter(c, n++);

            // which build this is, for checking what a phone is running, at the very foot
            Build = UiKit.Label(Root, "Build", 22, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 2), new Vector2(900, 28), Club.UiMedium, false);
            Build.rectTransform.pivot = new Vector2(0.5f, 0);
            Build.color = Club.White(0.5f); Build.raycastTarget = false;
        }

        /// How high the stack at the foot reaches (canvas units above the safe area's foot).
        float Bottom = 1100;

        /// Along the foot: between `x0` and `x1` of the width (inset `left` and `right`), `y` up, `h` tall.
        static void Place(RectTransform rt, float x0, float x1, float left, float right, float y, float h)
        {
            rt.anchorMin = new Vector2(x0, 0); rt.anchorMax = new Vector2(x1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(left, y); rt.offsetMax = new Vector2(-right, y + h);
        }

        /// His PLAY: the sun slab with the play object, PLAY huge, and what it plays under it.
        HoldButton PlaySlab(float y, out Text sub)
        {
            var hold = Club.Slab(Root, "Play", Club.Sun, Club.SunDeep, 90, out var face, 21);
            var rt = (RectTransform)hold.transform;
            rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(1, 0); rt.pivot = new Vector2(0.5f, 0);
            rt.offsetMin = new Vector2(54, y); rt.offsetMax = new Vector2(-54, y + 280);
            var shine = Club.Paint(face, "Shine", Club.White(0.35f), 90);
            shine.sprite = Club.Ramp; shine.type = Image.Type.Simple;
            shine.rectTransform.anchorMin = new Vector2(0, 0.5f);
            var obj = Club.Object(face, "Object", "play", 210);
            var ort = (RectTransform)obj.transform.parent;
            ort.anchorMin = ort.anchorMax = new Vector2(0, 0.5f); ort.anchoredPosition = new Vector2(48 + 105, 0);
            var word = Club.Words(face, "Word", "PLAY", Club.Display, 162, Club.Ink, TextAnchor.MiddleLeft);
            word.rectTransform.offsetMin = new Vector2(300, 46); word.rectTransform.offsetMax = new Vector2(-40, 0);
            word.horizontalOverflow = HorizontalWrapMode.Overflow;
            sub = Club.Words(face, "What", "Cliffside · full round", Club.Ui, 42, new Color(Club.Ink.r, Club.Ink.g, Club.Ink.b, 0.7f), TextAnchor.MiddleLeft);
            sub.rectTransform.offsetMin = new Vector2(304, 0); sub.rectTransform.offsetMax = new Vector2(-40, -150);
            face.gameObject.AddComponent<Breathe>();
            return hold;
        }

        /// The part of the screen the golfer stands in, as fractions of the screen's height from its foot, for
        /// a picture `aspect` wide to its height: the canvas is laid out against a 1080 × 2340 phone (width and
        /// height matched half and half), inside the safe area.
        public Vector2 StageBand(float aspect)
        {
            float canvasH = Mathf.Sqrt(UiKit.PhoneReference.x * UiKit.PhoneReference.y / Mathf.Max(0.2f, aspect));
            var safe = Screen.safeArea;
            float foot = Screen.height > 0 ? safe.yMin / Screen.height : 0, head = Screen.height > 0 ? safe.yMax / Screen.height : 1;
            float lo = foot + (Bottom + 20) / canvasH, hi = head - (StageTop + 10) / canvasH;
            return new Vector2(lo, Mathf.Max(lo + 0.15f, hi));
        }

        /// How far down from the top the stage starts (under the header), in canvas units.
        const float StageTop = 196;

        /// The course (a hole, or the round), what PLAY plays, and whether the big screen is on.
        public void Refresh(string courseName, string what, string golfer, bool tvOn)
        {
            course.Set(courseName, what + " · change course or hole");
            playSub.text = $"{courseName} · {what}";
            look.Set("Your look", golfer);
            tvDot.color = tvOn ? Club.Green : Club.White(0.35f);
        }

        /// Lights the chosen mode (0 solo, 1 two players on this phone, 2 online, 3 the Open) and names the
        /// player on the chip and in the greeting.
        public void ShowMode(int mode, string player, Color skin, Color shirt)
        {
            for (int i = 0; i < modePills.Length; i++) modePills[i].Select(i == mode);
            header.Player(player, skin, shirt);
            int hour = System.DateTime.Now.Hour;
            string hi = hour < 12 ? "GOOD MORNING" : hour < 18 ? "WELCOME BACK" : "GOOD EVENING";
            hello.text = string.IsNullOrEmpty(player) ? hi : $"{hi}, {player.ToUpperInvariant()}";
        }

        public void Destroy() { if (Root) Object.Destroy(Root.gameObject); }

        /// PLAY breathing: a slow swell, so the eye goes to it.
        sealed class Breathe : MonoBehaviour
        {
            void Update() => transform.localScale = Vector3.one * (1f + 0.012f * Mathf.Sin(Time.unscaledTime * 2.6f));
        }
    }

    /// The course screen, opened from the course card: Adnan's golf hub over the hole itself (the camera
    /// circles it high up; the game does that) — the crest and "Clubhouse › Cliffside" along the top, the
    /// hole's name big with its number, par and yards on a sun capsule, round cream arrows at the sides to go
    /// between the holes, a dot for each, THIS HOLE or FULL ROUND, and a sun SELECT slab beside a quiet BACK.
    /// A course still to be earned shows its padlock card.
    public sealed class CourseScreen
    {
        public readonly RectTransform Root;
        public readonly HoldButton Back, Previous, Next, ThisHole, FullRound, Select;

        readonly Club.Header header;
        readonly Text title, info;
        readonly RectTransform infoPill, titleHolder;
        readonly Image[] dots;
        readonly Club.Pill thisHole, fullRound;
        readonly CardPop pop;
        readonly Club.Card lockCard;
        readonly RectTransform lockRoot, selectRoot;
        readonly Text selectWord;
        readonly Image selectFace, selectBase;
        float shakeAt = -9f;

        public CourseScreen(Transform parent, int holes)
        {
            Root = new GameObject("Course select").AddComponent<RectTransform>();
            Root.SetParent(parent, false);
            Root.anchorMin = Vector2.zero; Root.anchorMax = Vector2.one; Root.offsetMin = Root.offsetMax = Vector2.zero;

            // the lagoon over the top and the foot of the live picture, so the type reads
            Club.Shade(Root, "Shade top", true, 760, 0.85f);
            Club.Shade(Root, "Shade foot", false, 1100, 0.92f);
            header = new Club.Header(Root, new[] { "Clubhouse", "Cliffside" }, chip: false);

            // the hole's name, big, and the capsule under it
            titleHolder = Club.Box(Root, "Title", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(1000, 170));
            title = Club.Words(titleHolder, "Name", "", Club.Display, 150, Color.white, TextAnchor.MiddleCenter, 15);
            Icons.Fit(title, 80, 150);
            pop = titleHolder.gameObject.AddComponent<CardPop>();
            infoPill = Club.Box(Root, "Info", new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -452), new Vector2(560, 72));
            var infoFill = infoPill.gameObject.AddComponent<Image>(); infoFill.color = Club.Sun; infoFill.raycastTarget = false;
            infoFill.sprite = UiKit.Circle; infoFill.type = Image.Type.Sliced; infoFill.pixelsPerUnitMultiplier = 32f / 36f;
            info = Club.Words(infoPill, "Text", "", Club.Caps, 34, Club.Ink, TextAnchor.MiddleCenter);
            info.horizontalOverflow = HorizontalWrapMode.Overflow;

            Previous = Arrow("Previous hole", false);
            Next = Arrow("Next hole", true);

            // a dot for each hole
            dots = new Image[holes];
            for (int i = 0; i < holes; i++)
            {
                var d = Club.Box(Root, $"Dot {i}", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2((i - (holes - 1) / 2f) * 40, 600), new Vector2(22, 22)).gameObject.AddComponent<Image>();
                d.sprite = UiKit.Circle; d.raycastTarget = false;
                dots[i] = d;
            }

            // THIS HOLE | FULL ROUND
            thisHole = new Club.Pill(Root, "This hole", "This hole", 108);
            fullRound = new Club.Pill(Root, "Full round", "Full round", 108);
            float gap = 24, w = thisHole.Root.sizeDelta.x + fullRound.Root.sizeDelta.x + gap;
            thisHole.Root.anchorMin = thisHole.Root.anchorMax = new Vector2(0.5f, 0); thisHole.Root.pivot = new Vector2(0, 0.5f);
            thisHole.Root.anchoredPosition = new Vector2(-w / 2, 450);
            fullRound.Root.anchorMin = fullRound.Root.anchorMax = new Vector2(0.5f, 0); fullRound.Root.pivot = new Vector2(0, 0.5f);
            fullRound.Root.anchoredPosition = new Vector2(-w / 2 + thisHole.Root.sizeDelta.x + gap, 450);
            ThisHole = thisHole.Button; FullRound = fullRound.Button;

            // BACK (quiet) and SELECT (sun)
            Back = Club.Button(Root, "Back", "Back", Club.Style.Quiet, 22);
            var brt = (RectTransform)Back.transform;
            brt.anchorMin = brt.anchorMax = new Vector2(0, 0); brt.pivot = new Vector2(0, 0); brt.anchoredPosition = new Vector2(54, 150); brt.sizeDelta = new Vector2(300, 170);
            Select = Club.Button(Root, "Select", "Select", Club.Style.Primary, 30);
            selectRoot = (RectTransform)Select.transform;
            selectRoot.anchorMin = new Vector2(0, 0); selectRoot.anchorMax = new Vector2(1, 0); selectRoot.pivot = new Vector2(0.5f, 0);
            selectRoot.offsetMin = new Vector2(54 + 300 + 30, 150); selectRoot.offsetMax = new Vector2(-54, 150 + 170);
            selectWord = selectRoot.Find("Face/Title").GetComponent<Text>();
            selectFace = selectRoot.Find("Face").GetComponent<Image>(); selectBase = selectRoot.Find("Base").GetComponent<Image>();

            // a course still to be earned: its padlock card over the dots
            lockCard = new Club.Card(Root, "Locked", "lock", "Locked", "", Club.Violet, true, 0.85f);
            lockCard.SetLocked(true).SetBadge(Club.BadgeKind.Locked);
            lockRoot = lockCard.Root;
            lockRoot.anchorMin = new Vector2(0, 0); lockRoot.anchorMax = new Vector2(1, 0); lockRoot.pivot = new Vector2(0.5f, 0);
            lockRoot.offsetMin = new Vector2(54, 660); lockRoot.offsetMax = new Vector2(-54, 660 + 230);
            lockRoot.gameObject.SetActive(false);
            Root.gameObject.AddComponent<Ticker>().Tick = () =>
            {
                float t = Time.unscaledTime - shakeAt;
                float dx = t < 0.4f ? Mathf.Sin(t * 60f) * 18f * (1 - t / 0.4f) : 0;
                lockRoot.offsetMin = new Vector2(54 + dx, 660); lockRoot.offsetMax = new Vector2(-54 + dx, 660 + 230);
            };
        }

        /// A course still to be earned: its padlock card with `requirement`, and SELECT greyed to LOCKED. Null
        /// opens it.
        public void SetLocked(string requirement)
        {
            bool locked = requirement != null;
            lockRoot.gameObject.SetActive(locked);
            if (locked) lockCard.Set("Locked", $"{requirement} to unlock");
            selectWord.text = locked ? "Locked" : "Select";
            selectFace.color = locked ? UiKit.Hex("C9C5CF") : Club.Sun; selectBase.color = locked ? UiKit.Hex("8E8A96") : Club.SunDeep;
            Select.RestColor = selectFace.color; Select.PressedColor = selectFace.color;
        }

        /// SELECT pressed on a locked course: the padlock shakes its head.
        public void ShakeLock() => shakeAt = Time.unscaledTime;

        sealed class Ticker : MonoBehaviour
        {
            public System.Action Tick;
            void Update() => Tick?.Invoke();
        }

        /// A round cream button at the side, half-way up, with an ink arrow.
        HoldButton Arrow(string name, bool right)
        {
            var hold = Club.Slab(Root, name, Club.Cream, Club.CreamDeep, 64, out var face, 12);
            var rt = (RectTransform)hold.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(right ? 1 : 0, 0.55f); rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(right ? -110 : 110, 0); rt.sizeDelta = new Vector2(128, 128);
            var icon = Icons.Place(face, "play", Club.Ink, new Vector2(0.5f, 0.5f), new Vector2(right ? 4 : -4, 0), 52);
            if (!right) icon.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
            hold.Sound = "tick";
            return hold;
        }

        /// The hole on show (`index` of `count`), the course it is on, and whether the choice is the whole round.
        public void Show(string name, int number, int par, double yards, int index, bool fullRound, string course = null)
        {
            if (title.text != name) { pop.enabled = false; pop.enabled = true; }
            title.text = name;
            info.text = $"HOLE {number}  ·  PAR {par}  ·  {yards:F0} YD";
            infoPill.sizeDelta = new Vector2(info.preferredWidth + 70, infoPill.sizeDelta.y);
            header.Where("Clubhouse", course ?? name);
            for (int i = 0; i < dots.Length; i++)
            {
                dots[i].color = i == index ? Club.Sun : Club.White(0.55f);
                dots[i].rectTransform.sizeDelta = Vector2.one * (i == index ? 30 : 20);
            }
            thisHole.Select(!fullRound);
            this.fullRound.Select(fullRound);
        }

        public void Destroy() { if (Root) Object.Destroy(Root.gameObject); }
    }
}

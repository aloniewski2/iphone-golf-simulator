using System;
using System.Collections.Generic;
using GolfArcade.Game;
using GolfArcade.Profile;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// The locker: Adnan's character screen (his LockerStudio.swift) in Unity. The golfer stands large in
    /// the top of the screen (the game's 3-D figure, turned by dragging), a row of tabs — BODY, HAIR,
    /// HEADWEAR, OUTFIT, GEAR — and under it the choices for the tab: cards for styles, swatch rows for
    /// quick colours, and range sliders (the gradient is the track) for any skin tone, hair colour or
    /// outfit colour. The HAIR and HEADWEAR tabs bring the camera in to the head. The game supplies the
    /// look and receives edits through the members below; this class only draws and reports.
    public sealed class Locker
    {
        public enum Tab { Body, Hair, Headwear, Outfit, Gear }
        public enum Rows { Ball, Trail, Club, Shirt, Shorts, Shoes, Haircut, Headwear, Mixer }

        public readonly RectTransform Root;
        public readonly HoldButton Go, Shuffle;
        /// Dragging across the golfer: screen pixels this frame, and the let-go.
        public Action<float> Spin;
        public Action SpinDone;
        public Action<Tab> TabChanged;

        // ---- Supplied by the game
        /// The look on screen.
        public Func<CharacterLook> Look;
        /// Apply an edit to it; `commit` is false while a slider is still being dragged.
        public Action<Action<CharacterLook>, bool> Change;
        /// Whether the outfit quick-pick colour n is open, and whether the colour mixer is.
        public Func<int, bool> OutfitColourOpen = _ => true;
        public Func<bool> MixerOpen = () => true;
        /// A padlocked choice pressed: what it is, to be told what it takes.
        public Action<Rows, string> LockedPressed;

        public Tab Current { get; private set; } = Tab.Body;

        // ---- Layout (reference pixels, 1080 wide)
        const float PanelW = 1000, PanelH = 920, PanelBottom = 258;
        static readonly Color Lagoon = UiKit.Hex("0E2A47"), LagoonDeep = UiKit.Hex("081A2E"), Sun = UiKit.Hex("FFD21F"), SunDeep = UiKit.Hex("D99A00"),
            Cream = UiKit.Hex("F7F4EC"), Ink = UiKit.Hex("16123A"), Soft = new Color(1, 1, 1, 0.12f), SoftStrong = new Color(1, 1, 1, 0.2f);

        readonly RectTransform[] panels = new RectTransform[5];
        readonly Image[] tabFills = new Image[5];
        readonly Text[] tabWords = new Text[5];
        readonly Image[] tabIcons = new Image[5];
        readonly Text nameWord;

        // BODY
        readonly HoldButton[] bodyCards = new HoldButton[2];
        Slider skinSlider; SwatchRow skinSwatches; Image skinChip;
        // HAIR
        HoldButton[] haircutCards; Text haircutNames;
        Slider hairSlider, dyeSlider; SwatchRow hairSwatches; Image hairChip, dyeChip;
        // HEADWEAR
        HoldButton[] hatCards; Slider hatHue, hatShade; Image hatChip; Text hatNote;
        // OUTFIT
        readonly HoldButton[] slotChips = new HoldButton[3];
        readonly string[] slots = { "shirt", "shorts", "shoes" };
        int slot;
        SwatchRow outfitSwatches; HoldButton outfitReset; Slider outfitHue, outfitShade; Text outfitName; Image outfitChip; Text mixerNote;
        // GEAR
        public HoldButton[] Balls = new HoldButton[0], Trails = new HoldButton[0], Clubs = new HoldButton[0];
        readonly RowParts[] gear = new RowParts[3];
        RectTransform gearPanel;

        public Locker(Transform parent)
        {
            Root = Rect(parent, "Locker", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // the golfer's ground: drag to turn them round
            var spinArea = UiKit.Panel(Root, "Spin area", Color.clear, new Vector2(0, 0.42f), new Vector2(1, 1), Vector2.zero, Vector2.zero, false);
            spinArea.rectTransform.offsetMin = spinArea.rectTransform.offsetMax = Vector2.zero;
            var drag = spinArea.gameObject.AddComponent<SpinDrag>();
            drag.Moved = dx => Spin?.Invoke(dx);
            drag.Released = () => SpinDone?.Invoke();
            // the club's header: the crest, and where you are
            new Club.Header(Root, new[] { "Clubhouse", "Your look" }, chip: false);
            var hint = UiKit.Pill(Root, "Drag hint", new Color(0, 0, 0, 0.32f), new Vector2(0, 1), new Vector2(170, -222), new Vector2(250, 56), out var hintFill, 0f);
            foreach (var img in hint.GetComponentsInChildren<Image>()) if (img.name != "Shadow") img.color = img.name == "Rim" ? new Color(0, 0, 0, 0) : new Color(0, 0, 0, 0.32f);
            var hintWord = UiKit.Label(hintFill.transform, "Word", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Club.Caps, false);
            hintWord.text = "↔ Drag to spin"; hintWord.color = new Color(1, 1, 1, 0.85f); hintWord.raycastTarget = false;

            // the name tag, over the tabs
            var tag = Club.Box(Root, "Name", new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0.5f), new Vector2(0, PanelBottom + PanelH + 196), new Vector2(520, 84));
            var tagFill = tag.gameObject.AddComponent<Image>(); tagFill.color = Sun; tagFill.raycastTarget = false;
            tagFill.sprite = UiKit.Circle; tagFill.type = Image.Type.Sliced; tagFill.pixelsPerUnitMultiplier = 32f / 42f;
            nameWord = Club.Words(tag, "Word", "Your golfer", Club.Title, 54, Ink, TextAnchor.MiddleCenter);
            nameWord.rectTransform.offsetMin = new Vector2(30, 0); nameWord.rectTransform.offsetMax = new Vector2(-30, 0);
            Icons.Fit(nameWord, 28, 54);

            // the tabs
            string[] titles = { "BODY", "HAIR", "HEADWEAR", "OUTFIT", "GEAR" };
            string[] icons = { "face", "sparkle", "star", "shirt", "iron" };
            float tabW = (PanelW - 4 * 12) / 5f;
            for (int i = 0; i < 5; i++)
            {
                int tabIndex = i;
                var t = UiKit.Panel(Root, "Tab " + titles[i], Soft, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-PanelW / 2 + i * (tabW + 12), PanelBottom + PanelH + 20), new Vector2(tabW, 120));
                t.sprite = UiKit.RoundedLarge; t.type = Image.Type.Sliced; t.rectTransform.pivot = Vector2.zero;
                var hold = t.gameObject.AddComponent<HoldButton>();
                hold.Fill = t; hold.RestColor = Soft;
                hold.Pressed = () => Show((Tab)tabIndex);
                tabFills[i] = t;
                tabIcons[i] = Icons.Place(t.transform, icons[i], Color.white, new Vector2(0.5f, 1), new Vector2(tabW / 2, -34), 44);
                tabIcons[i].rectTransform.anchorMin = tabIcons[i].rectTransform.anchorMax = new Vector2(0, 1);
                tabWords[i] = UiKit.Label(t.transform, "Word", 24, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 30), new Vector2(0, 36), UiKit.Strong, false);
                tabWords[i].rectTransform.pivot = new Vector2(0.5f, 0.5f); tabWords[i].text = titles[i]; tabWords[i].color = Color.white; tabWords[i].raycastTarget = false;
                tabWords[i].rectTransform.sizeDelta = new Vector2(0, 36);
                Icons.Fit(tabWords[i], 16, 24);
            }

            // the panel the choices sit on
            var panel = UiKit.Panel(Root, "Panel", new Color(LagoonDeep.r, LagoonDeep.g, LagoonDeep.b, 0.86f), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-PanelW / 2, PanelBottom), new Vector2(PanelW, PanelH));
            panel.sprite = UiKit.RoundedLarge; panel.type = Image.Type.Sliced; panel.rectTransform.pivot = Vector2.zero;
            var edge = UiKit.Panel(panel.transform, "Edge", new Color(1, 1, 1, 0.12f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            edge.sprite = UiKit.RoundedLarge; edge.type = Image.Type.Sliced; edge.rectTransform.offsetMin = new Vector2(-2, -2); edge.rectTransform.offsetMax = new Vector2(2, 2);
            edge.transform.SetAsFirstSibling(); edge.raycastTarget = false;
            for (int i = 0; i < 5; i++)
            {
                panels[i] = Rect(panel.transform, "Panel " + titles[i], Vector2.zero, Vector2.one, new Vector2(30, 24), new Vector2(-30, -24));
                panels[i].gameObject.SetActive(false);
            }
            BuildBody(panels[(int)Tab.Body]);
            BuildHair(panels[(int)Tab.Hair]);
            BuildHeadwear(panels[(int)Tab.Headwear]);
            BuildOutfit(panels[(int)Tab.Outfit]);
            gearPanel = panels[(int)Tab.Gear];

            // SHUFFLE (his secondary cream slab) and LET'S GO (the sun one)
            Shuffle = Club.Button(Root, "Shuffle", "Shuffle", Club.Style.Secondary, 19, null, "quick");
            var srt = (RectTransform)Shuffle.transform;
            srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0); srt.pivot = new Vector2(0, 0.5f);
            srt.anchoredPosition = new Vector2(-PanelW / 2, 128); srt.sizeDelta = new Vector2(400, 150);
            Go = Club.Button(Root, "Lets go", "Let's go", Club.Style.Primary, 28, null, "play");
            var grt = (RectTransform)Go.transform;
            grt.anchorMin = grt.anchorMax = new Vector2(0.5f, 0); grt.pivot = new Vector2(1, 0.5f);
            grt.anchoredPosition = new Vector2(PanelW / 2, 128); grt.sizeDelta = new Vector2(PanelW - 400 - 26, 150);

            Show(Tab.Body, notify: false);
        }

        // ================================================================= tabs

        public void Show(Tab tab, bool notify = true)
        {
            Current = tab;
            for (int i = 0; i < 5; i++)
            {
                bool on = i == (int)tab;
                panels[i].gameObject.SetActive(on);
                tabFills[i].color = on ? Sun : Soft;
                if (tabFills[i].TryGetComponent<HoldButton>(out var h)) h.RestColor = on ? Sun : Soft;
                tabWords[i].color = on ? Ink : Color.white;
                tabIcons[i].color = on ? Ink : Color.white;
            }
            Refresh();
            if (notify) TabChanged?.Invoke(tab);
        }

        /// Whose golfer is being made ("YOUR GOLFER", or a player's name).
        public void SetName(string text) => nameWord.text = text;

        /// Re-reads the look and lights every choice on the tab shown.
        public void Refresh()
        {
            var look = Look?.Invoke();
            if (look == null) return;
            switch (Current)
            {
                case Tab.Body:
                    Mark(bodyCards[0], look.Body == CharacterLook.Boy); Mark(bodyCards[1], look.Body == CharacterLook.Girl);
                    var skin = GolferStyle.ColorOf(look.Skin) ?? GolferStyle.SkinTones[GolferStyle.DefaultSkin];
                    skinSlider.Set(LockerColor.RampPosition(LockerColor.SkinStops, skin)); skinChip.color = skin;
                    skinSwatches.Choose(NearestSwatch(LockerColor.SkinStops, LockerColor.SkinPresets, skin));
                    break;
                case Tab.Hair:
                    for (int i = 0; i < haircutCards.Length; i++) Mark(haircutCards[i], look.HaircutId == i);
                    haircutNames.text = HeroGolfer.HaircutNames[Mathf.Clamp(look.HaircutId, 0, HeroGolfer.HaircutNames.Length - 1)].ToUpperInvariant();
                    var hair = GolferStyle.ColorOf(look.Hair) ?? GolferStyle.HairColors[GolferStyle.DefaultHairTone];
                    hairChip.color = hair; dyeChip.color = hair;
                    hairSlider.Set(LockerColor.RampPosition(LockerColor.HairStops, hair));
                    var hairPresetT = new float[LockerColor.HairPresets.Length];
                    for (int i = 0; i < hairPresetT.Length; i++) hairPresetT[i] = LockerColor.HairPresets[i].t;
                    hairSwatches.Choose(NearestSwatch(LockerColor.HairStops, hairPresetT, hair));
                    var (dh, _) = LockerColor.HueShadeOf(hair);
                    dyeSlider.Set(dh);
                    break;
                case Tab.Headwear:
                    for (int i = 0; i < hatCards.Length; i++) Mark(hatCards[i], look.Headwear == i);
                    var hat = GolferStyle.ColorOf(look.Hat) ?? GolferStyle.HatAsDesigned;
                    hatChip.color = hat;
                    var (hh, hs) = LockerColor.HueShadeOf(hat);
                    hatHue.Set(hh); hatShade.Set((hs + 1) / 2);
                    bool coloured = look.Headwear >= 2;
                    hatNote.text = look.Headwear == 0 ? "NO HAT: THE HAIR SHOWS IN FULL" : look.Headwear == 1 ? "THE VISOR COMES IN ITS OWN COLOURS" : "PICK THE CAP'S COLOUR";
                    hatHue.SetDimmed(!coloured); hatShade.SetDimmed(!coloured);
                    break;
                case Tab.Outfit:
                    for (int i = 0; i < 3; i++) Mark(slotChips[i], slot == i);
                    string hex = slot == 0 ? look.Shirt : slot == 1 ? look.Shorts : look.Shoes;
                    var asDesigned = string.IsNullOrEmpty(hex);
                    var fallback = slot == 0 ? Cream : slot == 1 ? GolferStyle.KitColors[0] : GolferStyle.ShoesAsDesigned;
                    var col = GolferStyle.ColorOf(hex) ?? fallback;
                    outfitChip.color = col;
                    outfitName.text = asDesigned ? "AS DESIGNED" : LockerColor.Describe(col).ToUpperInvariant();
                    var (oh, os) = LockerColor.HueShadeOf(col);
                    outfitHue.Set(oh); outfitShade.Set((os + 1) / 2);
                    Mark(outfitReset, asDesigned);
                    var palette = slot == 1 ? GolferStyle.KitColors : GolferStyle.ShirtColors;
                    outfitSwatches.Choose(GolferStyle.IndexOf(palette, hex, -1));
                    bool mixer = MixerOpen();
                    outfitHue.SetDimmed(!mixer || asDesigned); outfitShade.SetDimmed(!mixer || asDesigned);
                    mixerNote.gameObject.SetActive(!mixer);
                    var open = new bool[palette.Length];
                    for (int i = 0; i < open.Length; i++) open[i] = OutfitColourOpen(i);
                    outfitSwatches.SetOpen(open);
                    break;
            }
        }

        static int NearestSwatch(string[] stops, float[] presets, Color c)
        {
            int best = -1; float bestD = 0.02f * 0.02f * 3;
            for (int i = 0; i < presets.Length; i++)
            {
                var r = LockerColor.Ramp(stops, presets[i]);
                float d = (r.r - c.r) * (r.r - c.r) + (r.g - c.g) * (r.g - c.g) + (r.b - c.b) * (r.b - c.b);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        // ================================================================= panels

        void BuildBody(RectTransform p)
        {
            Section(p, "PLAYER", 0, out _);
            string[] names = { "BOY", "GIRL" };
            for (int i = 0; i < 2; i++)
            {
                int body = i;
                bodyCards[i] = Card(p, names[i], 0 + i * 486, 56, 470, 150, i == 0 ? "face" : "sparkle");
                bodyCards[i].Pressed = () => Change?.Invoke(l => { l.Body = body; }, true);
            }
            Section(p, "SKIN TONE", 240, out skinChip);
            skinSlider = Slider.Make(p, 0, 300, 940, LockerColor.SkinStops, null);
            skinSlider.ColorAt = t => LockerColor.Ramp(LockerColor.SkinStops, t);
            skinSlider.Changed = (t, done) => Change?.Invoke(l => l.Skin = GolferStyle.HexOf(LockerColor.Ramp(LockerColor.SkinStops, t)), done);
            var skinColours = new Color[LockerColor.SkinPresets.Length];
            for (int i = 0; i < skinColours.Length; i++) skinColours[i] = LockerColor.Ramp(LockerColor.SkinStops, LockerColor.SkinPresets[i]);
            skinSwatches = SwatchRow.Make(p, 0, 380, 940, skinColours, 92);
            for (int i = 0; i < skinColours.Length; i++)
            {
                var c = skinColours[i];
                skinSwatches.Holds[i].Pressed = () => Change?.Invoke(l => l.Skin = GolferStyle.HexOf(c), true);
            }
            var note = Label(p, "Note", "SKIN, HAIR AND CLOTHES ARE SEPARATE PARTS: CHANGE ONE AND THE REST STAY AS THEY ARE.", 24, 0, 560, 940, 80, Color.white * 0.7f, TextAnchor.UpperLeft);
            note.color = new Color(1, 1, 1, 0.55f);
        }

        void BuildHair(RectTransform p)
        {
            Section(p, "HAIRSTYLE", 0, out _);
            haircutNames = Label(p, "Chosen", "", 26, 250, 4, 690, 40, Sun, TextAnchor.MiddleRight);
            int cuts = HeroGolfer.OfferedHaircuts;
            haircutCards = new HoldButton[cuts];
            const float cw = 220, gap = 20;
            for (int i = 0; i < cuts; i++)
            {
                int cut = i;
                haircutCards[i] = Card(p, HeroGolfer.HaircutNames[i].ToUpperInvariant(), (i % 4) * (cw + gap), 56 + (i / 4) * 128, cw, 116, null);
                haircutCards[i].Pressed = () =>
                {
                    if (!LockedOk(Rows.Haircut, cut.ToString())) return;
                    Change?.Invoke(l => l.Haircut = cut, true);
                };
            }
            Section(p, "NATURAL COLOUR", 330, out hairChip);
            hairSlider = Slider.Make(p, 0, 390, 940, LockerColor.HairStops, null);
            hairSlider.ColorAt = t => LockerColor.Ramp(LockerColor.HairStops, t);
            hairSlider.Changed = (t, done) => Change?.Invoke(l => l.Hair = GolferStyle.HexOf(LockerColor.Ramp(LockerColor.HairStops, t)), done);
            var hairColours = new Color[LockerColor.HairPresets.Length];
            for (int i = 0; i < hairColours.Length; i++) hairColours[i] = LockerColor.Ramp(LockerColor.HairStops, LockerColor.HairPresets[i].t);
            hairSwatches = SwatchRow.Make(p, 0, 470, 940, hairColours, 84);
            for (int i = 0; i < hairColours.Length; i++)
            {
                var c = hairColours[i];
                hairSwatches.Holds[i].Pressed = () => Change?.Invoke(l => l.Hair = GolferStyle.HexOf(c), true);
            }
            Section(p, "DYE", 590, out dyeChip);
            dyeSlider = Slider.Make(p, 0, 650, 940, null, null);
            dyeSlider.Rainbow = true;
            dyeSlider.ColorAt = t => LockerColor.HueShade(t, 0);
            dyeSlider.Changed = (t, done) => Change?.Invoke(l => l.Hair = GolferStyle.HexOf(LockerColor.HueShade(t, 0.1f)), done);
        }

        void BuildHeadwear(RectTransform p)
        {
            Section(p, "HEADWEAR", 0, out _);
            hatCards = new HoldButton[HeroGolfer.HeadwearNames.Length];
            for (int i = 0; i < hatCards.Length; i++)
            {
                int wear = i;
                hatCards[i] = Card(p, HeroGolfer.HeadwearNames[i].ToUpperInvariant(), (i % 2) * 486, 56 + (i / 2) * 150, 470, 134, null);
                hatCards[i].Pressed = () =>
                {
                    if (!LockedOk(Rows.Headwear, wear.ToString())) return;
                    Change?.Invoke(l => l.Headwear = wear, true);
                };
            }
            Section(p, "CAP AND BAND COLOUR", 400, out hatChip);
            hatNote = Label(p, "Note", "", 24, 0, 456, 940, 36, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
            hatHue = Slider.Make(p, 0, 510, 940, null, null); hatHue.Rainbow = true;
            hatHue.ColorAt = t => LockerColor.HueShade(t, 0);
            hatHue.Changed = (t, done) => Change?.Invoke(l => l.Hat = GolferStyle.HexOf(LockerColor.HueShade(t, ShadeOf(l.Hat))), done);
            hatShade = Slider.Make(p, 0, 590, 940, null, null);
            hatShade.ColorAt = t => LockerColor.HueShade(0.0f, t * 2 - 1) ;
            hatShade.Changed = (t, done) => Change?.Invoke(l => l.Hat = GolferStyle.HexOf(LockerColor.HueShade(HueOf(l.Hat), t * 2 - 1)), done);
        }

        static float ShadeOf(string hex) => LockerColor.HueShadeOf(GolferStyle.ColorOf(hex) ?? GolferStyle.HatAsDesigned).shade;
        static float HueOf(string hex) => LockerColor.HueShadeOf(GolferStyle.ColorOf(hex) ?? GolferStyle.HatAsDesigned).hue;

        void BuildOutfit(RectTransform p)
        {
            string[] titles = { "SHIRT", "SHORTS", "SHOES" };
            for (int i = 0; i < 3; i++)
            {
                int which = i;
                slotChips[i] = Card(p, titles[i], i * 320, 0, 300, 96, null);
                slotChips[i].Pressed = () => { slot = which; Refresh(); };
            }
            Section(p, "COLOUR", 130, out outfitChip);
            outfitName = Label(p, "Name", "", 26, 250, 134, 690, 40, Sun, TextAnchor.MiddleRight);
            outfitHue = Slider.Make(p, 0, 190, 940, null, null); outfitHue.Rainbow = true;
            outfitHue.ColorAt = t => LockerColor.HueShade(t, 0);
            outfitHue.Changed = (t, done) => ChangeOutfit(l => LockerColor.HueShade(t, ShadeNow(l)), done);
            outfitShade = Slider.Make(p, 0, 270, 940, null, null);
            outfitShade.ColorAt = t => LockerColor.HueShade(0.58f, t * 2 - 1);
            outfitShade.Changed = (t, done) => ChangeOutfit(l => LockerColor.HueShade(HueNow(l), t * 2 - 1), done);
            mixerNote = Label(p, "Mixer note", "THE COLOUR MIXER IS EARNED — THE QUICK PICKS BELOW ARE OPEN", 24, 0, 350, 940, 34, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft);
            Section(p, "QUICK PICKS", 400, out _);
            outfitReset = Card(p, "AS DESIGNED", 0, 456, 290, 100, null);
            outfitReset.Pressed = () => Change?.Invoke(l => SetSlot(l, ""), true);
            var colours = GolferStyle.ShirtColors;
            outfitSwatches = SwatchRow.Make(p, 320, 462, 620, colours, 84, right: true);
            for (int i = 0; i < colours.Length; i++)
            {
                int index = i;
                outfitSwatches.Holds[i].Pressed = () =>
                {
                    if (!OutfitColourOpen(index)) { LockedPressed?.Invoke(slot == 1 ? Rows.Shorts : slot == 0 ? Rows.Shirt : Rows.Shoes, $"outfit.{index}"); return; }
                    var palette = slot == 1 ? GolferStyle.KitColors : GolferStyle.ShirtColors;
                    Change?.Invoke(l => SetSlot(l, GolferStyle.HexOf(palette[index])), true);
                };
            }
        }

        void ChangeOutfit(Func<CharacterLook, Color> pick, bool done)
        {
            if (!MixerOpen()) { LockedPressed?.Invoke(Rows.Mixer, "outfit.mixer"); Refresh(); return; }
            Change?.Invoke(l => SetSlot(l, GolferStyle.HexOf(pick(l))), done);
        }

        string SlotHex(CharacterLook l) => slot == 0 ? l.Shirt : slot == 1 ? l.Shorts : l.Shoes;
        float ShadeNow(CharacterLook l) => LockerColor.HueShadeOf(GolferStyle.ColorOf(SlotHex(l)) ?? OutfitFallback()).shade;
        float HueNow(CharacterLook l) => LockerColor.HueShadeOf(GolferStyle.ColorOf(SlotHex(l)) ?? OutfitFallback()).hue;
        Color OutfitFallback() => slot == 0 ? Cream : slot == 1 ? GolferStyle.KitColors[0] : GolferStyle.ShoesAsDesigned;

        void SetSlot(CharacterLook l, string hex)
        {
            if (slot == 0) l.Shirt = hex; else if (slot == 1) l.Shorts = hex; else l.Shoes = hex;
        }

        bool LockedOk(Rows row, string id)
        {
            // hooks for rewards gating a haircut or headwear; open unless the game says otherwise
            return true;
        }

        // ================================================================= GEAR

        sealed class RowParts { public HoldButton[] Holds; public RectTransform Ring; public Text Chosen; public Image[] Locks, Fills; public string[] Names; }

        /// The GEAR tab's rows: the balls, the trails and the clubs' finishes, by name and swatch (a trail's may be a face of its own).
        public void AddGear(string[] balls, Color[] ballColors, string[] trails, Color[] trailColors, Sprite[] trailFaces, string[] clubs, Color[] clubColors)
        {
            const float rowH = 200, gearGap = 22;
            Balls = GearRow(gearPanel, "BALL", 0, rowH, balls, ballColors, out gear[0], null);
            Trails = GearRow(gearPanel, "TRAIL", rowH + gearGap, rowH, trails, trailColors, out gear[1], trailFaces);
            Clubs = GearRow(gearPanel, "CLUBS", 2 * (rowH + gearGap), rowH, clubs, clubColors, out gear[2], null);
        }

        HoldButton[] GearRow(RectTransform page, string label, float y, float rowH, string[] names, Color[] colors, out RowParts parts, Sprite[] faces)
        {
            var bar = UiKit.Panel(page, label, new Color(1, 1, 1, 0.08f), new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y), new Vector2(940, rowH));
            bar.sprite = UiKit.RoundedLarge; bar.type = Image.Type.Sliced; bar.rectTransform.pivot = new Vector2(0, 1); bar.raycastTarget = false;
            var l = UiKit.Label(bar.transform, "Label", 32, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(26, -20), new Vector2(300, 40), UiKit.Display, false);
            l.text = label; l.color = Color.white;
            var chosen = UiKit.Label(bar.transform, "Chosen", 24, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -24), new Vector2(560, 34), UiKit.Display, false);
            chosen.rectTransform.pivot = new Vector2(1, 1); chosen.color = Sun; Icons.Fit(chosen, 14, 24);
            const float swatch = 84;
            float step = Mathf.Min(120f, (940 - 60 - swatch) / Mathf.Max(1, colors.Length - 1));
            float x0 = 30 + swatch / 2;
            var ring = UiKit.Panel(bar.transform, "Ring", Sun, new Vector2(0, 0), new Vector2(0, 0), Vector2.zero, new Vector2(swatch + 22, swatch + 22), false).rectTransform;
            ring.GetComponent<Image>().sprite = UiKit.Circle; ring.GetComponent<Image>().raycastTarget = false; ring.pivot = new Vector2(0.5f, 0.5f);
            var holds = new HoldButton[colors.Length]; var locks = new Image[colors.Length]; var fills = new Image[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                var rim = UiKit.Panel(bar.transform, $"{label} {names[i]}", Color.white, new Vector2(0, 0), new Vector2(0, 0), new Vector2(x0 + step * i, 62), new Vector2(swatch, swatch), false);
                rim.sprite = UiKit.Circle; rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                var fill = UiKit.Panel(rim.transform, "Colour", colors[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(swatch - 14, swatch - 14), false);
                fill.sprite = faces != null && faces[i] ? faces[i] : UiKit.Circle; fill.rectTransform.pivot = new Vector2(0.5f, 0.5f); fill.raycastTarget = false;
                if (faces != null && faces[i]) fill.color = Color.white;
                var badge = UiKit.Panel(rim.transform, "Padlock", LagoonDeep, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(swatch * 0.32f, -swatch * 0.32f), new Vector2(swatch * 0.5f, swatch * 0.5f), false);
                badge.sprite = UiKit.Circle; badge.rectTransform.pivot = new Vector2(0.5f, 0.5f); badge.raycastTarget = false;
                Icons.Place(badge.transform, "lock", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, swatch * 0.3f).raycastTarget = false;
                badge.gameObject.SetActive(false);
                var hold = rim.gameObject.AddComponent<HoldButton>(); hold.Fill = rim; hold.RestColor = Color.white;
                holds[i] = hold; locks[i] = badge; fills[i] = fill;
            }
            ring.SetAsFirstSibling();
            parts = new RowParts { Holds = holds, Ring = ring, Chosen = chosen, Locks = locks, Fills = fills, Names = names };
            return holds;
        }

        /// Padlocks on the swatches still to be earned (true is open), dimming them.
        public void SetOpen(Rows row, bool[] open)
        {
            var r = row switch { Rows.Ball => gear[0], Rows.Trail => gear[1], Rows.Club => gear[2], _ => null };
            if (r == null) return;
            for (int i = 0; i < r.Locks.Length && i < open.Length; i++)
            {
                r.Locks[i].gameObject.SetActive(!open[i]);
                var c = r.Fills[i].color; c.a = open[i] ? 1f : 0.35f; r.Fills[i].color = c;
            }
        }

        /// Rings the chosen ball, trail and club.
        public void RefreshGear(int ball, int trail, int club) { ChooseGear(gear[0], ball); ChooseGear(gear[1], trail); ChooseGear(gear[2], club); }

        static void ChooseGear(RowParts row, int index)
        {
            if (row == null || index < 0 || index >= row.Holds.Length) return;
            row.Ring.anchoredPosition = ((RectTransform)row.Holds[index].transform).anchoredPosition;
            row.Chosen.text = row.Names[index].ToUpperInvariant();
        }

        /// A padlocked swatch pressed: what it takes, on the row's label.
        public void SayLocked(Rows row, string requirement)
        {
            var text = $"LOCKED: {requirement}".ToUpperInvariant();
            var r = row switch { Rows.Ball => gear[0], Rows.Trail => gear[1], Rows.Club => gear[2], _ => null };
            if (r != null) { r.Chosen.text = text; return; }
            if (row == Rows.Mixer) mixerNote.text = text;
            else outfitName.text = text;
            if (row == Rows.Mixer) mixerNote.gameObject.SetActive(true);
        }

        public bool ShowingGear => Current == Tab.Gear;
        public void ShowGear(bool on) => Show(on ? Tab.Gear : Tab.Body);

        public void Destroy() { if (Root) UnityEngine.Object.Destroy(Root.gameObject); }

        // ================================================================= building blocks

        static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = new GameObject(name).AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            return rt;
        }

        /// A heading on the panel, with a colour chip at its right end when `chip` is wanted.
        static void Section(RectTransform p, string title, float y, out Image chip)
        {
            var t = UiKit.Label(p, "Heading " + title, 34, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -y), new Vector2(600, 44), UiKit.Display, false);
            t.text = title; t.color = Color.white; t.rectTransform.pivot = new Vector2(0, 1);
            var rim = UiKit.Panel(p, "Chip", Color.white, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -y - 4), new Vector2(40, 40), false);
            rim.sprite = UiKit.Circle; rim.rectTransform.pivot = new Vector2(1, 1); rim.raycastTarget = false;
            chip = UiKit.Panel(rim.transform, "Colour", Color.gray, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30), false);
            chip.sprite = UiKit.Circle; chip.rectTransform.pivot = new Vector2(0.5f, 0.5f); chip.raycastTarget = false;
            if (title == "PLAYER" || title == "HAIRSTYLE" || title == "HEADWEAR") rim.gameObject.SetActive(false);
        }

        static Text Label(RectTransform p, string name, string text, int size, float x, float y, float w, float h, Color color, TextAnchor anchor)
        {
            var t = UiKit.Label(p, name, size, anchor, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h), UiKit.Strong, false);
            t.rectTransform.pivot = new Vector2(0, 1);
            t.text = text; t.color = color; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }

        /// A card: a rounded light box with a word (and an icon), lit sun-yellow when chosen.
        static HoldButton Card(RectTransform p, string word, float x, float y, float w, float h, string icon)
        {
            var img = UiKit.Panel(p, "Card " + word, Soft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));
            img.sprite = UiKit.RoundedLarge; img.type = Image.Type.Sliced; img.rectTransform.pivot = new Vector2(0, 1);
            var hold = img.gameObject.AddComponent<HoldButton>();
            hold.Fill = img; hold.RestColor = Soft;
            var t = UiKit.Label(img.transform, "Word", 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Display, false);
            t.text = word; t.color = Color.white; t.raycastTarget = false;
            Icons.Fit(t, 18, 34);
            if (icon != null)
            {
                t.rectTransform.offsetMin = new Vector2(0, 0); t.rectTransform.offsetMax = new Vector2(0, -h * 0.34f);
                Icons.Place(img.transform, icon, Color.white, new Vector2(0.5f, 1), new Vector2(0, -h * 0.30f), h * 0.32f);
                t.alignment = TextAnchor.LowerCenter; t.rectTransform.offsetMin = new Vector2(0, 10);
            }
            return hold;
        }

        /// Lights a card (chosen) or rests it.
        static void Mark(HoldButton card, bool on)
        {
            if (!card) return;
            card.RestColor = on ? Sun : Soft;
            if (card.Fill && !card.IsHeld) card.Fill.color = card.RestColor;
            var word = card.GetComponentInChildren<Text>(true);
            if (word) word.color = on ? Ink : Color.white;
            foreach (var ic in card.GetComponentsInChildren<Image>(true)) if (ic.name.StartsWith("Icon")) ic.color = on ? Ink : Color.white;
        }

        // ---- a row of colour swatches
        sealed class SwatchRow
        {
            public HoldButton[] Holds; RectTransform ring; Image[] locks; Image[] fills;

            public static SwatchRow Make(RectTransform p, float x, float y, float width, Color[] colors, float size, bool right = false)
            {
                var row = new SwatchRow { Holds = new HoldButton[colors.Length], locks = new Image[colors.Length], fills = new Image[colors.Length] };
                float step = Mathf.Min(size + 26, (width - size) / Mathf.Max(1, colors.Length - 1));
                var holder = UiKit.Panel(p, "Swatches", Color.clear, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, size + 20), false);
                holder.rectTransform.pivot = new Vector2(0, 1); holder.raycastTarget = false;
                row.ring = UiKit.Panel(holder.transform, "Ring", Sun, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(size + 20, size + 20), false).rectTransform;
                row.ring.GetComponent<Image>().sprite = UiKit.Circle; row.ring.GetComponent<Image>().raycastTarget = false; row.ring.pivot = new Vector2(0.5f, 0.5f);
                row.ring.gameObject.SetActive(false);
                float x0 = right ? width - size / 2 - step * (colors.Length - 1) : size / 2;
                for (int i = 0; i < colors.Length; i++)
                {
                    var rim = UiKit.Panel(holder.transform, "Swatch " + i, Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(x0 + step * i, 0), new Vector2(size, size), false);
                    rim.sprite = UiKit.Circle; rim.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    var fill = UiKit.Panel(rim.transform, "Colour", colors[i], new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size - 14, size - 14), false);
                    fill.sprite = UiKit.Circle; fill.rectTransform.pivot = new Vector2(0.5f, 0.5f); fill.raycastTarget = false;
                    var badge = UiKit.Panel(rim.transform, "Padlock", LagoonDeep, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(size * 0.32f, -size * 0.32f), new Vector2(size * 0.5f, size * 0.5f), false);
                    badge.sprite = UiKit.Circle; badge.rectTransform.pivot = new Vector2(0.5f, 0.5f); badge.raycastTarget = false;
                    Icons.Place(badge.transform, "lock", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.3f).raycastTarget = false;
                    badge.gameObject.SetActive(false);
                    var hold = rim.gameObject.AddComponent<HoldButton>(); hold.Fill = rim; hold.RestColor = Color.white;
                    row.Holds[i] = hold; row.locks[i] = badge; row.fills[i] = fill;
                }
                row.ring.SetAsFirstSibling();
                return row;
            }

            public void Choose(int index)
            {
                ring.gameObject.SetActive(index >= 0 && index < Holds.Length);
                if (index < 0 || index >= Holds.Length) return;
                ring.anchoredPosition = ((RectTransform)Holds[index].transform).anchoredPosition;
            }

            public void SetOpen(bool[] open)
            {
                for (int i = 0; i < locks.Length && i < open.Length; i++)
                {
                    locks[i].gameObject.SetActive(!open[i]);
                    var c = fills[i].color; c.a = open[i] ? 1f : 0.35f; fills[i].color = c;
                }
            }
        }

        // ---- a range slider: the gradient is the track, a big knob sits on the pick
        sealed class Slider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public Func<float, Color> ColorAt;
            public Action<float, bool> Changed;
            public bool Rainbow;
            RectTransform track, knob; Image knobFill; RawImage gradient; CanvasGroup group;
            float value; bool dragging, ready;
            const float Pad = 30;

            public static Slider Make(RectTransform p, float x, float y, float width, string[] stops, Func<float, Color> colorAt)
            {
                var root = UiKit.Panel(p, "Slider", Color.clear, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, 70), false);
                root.rectTransform.pivot = new Vector2(0, 1);
                var s = root.gameObject.AddComponent<Slider>();
                s.group = root.gameObject.AddComponent<CanvasGroup>();
                s.ColorAt = colorAt;
                // the track: a capsule, its gradient a texture on a mask
                var capsule = UiKit.Panel(root.transform, "Track", Color.white, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-2 * Pad, 34), false);
                capsule.rectTransform.pivot = new Vector2(0.5f, 0.5f); capsule.rectTransform.offsetMin = new Vector2(Pad, -17); capsule.rectTransform.offsetMax = new Vector2(-Pad, 17);
                capsule.sprite = UiKit.Circle; capsule.type = Image.Type.Sliced; capsule.pixelsPerUnitMultiplier = 1f;
                capsule.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var tex = new GameObject("Gradient").AddComponent<RawImage>();
                tex.transform.SetParent(capsule.transform, false);
                tex.rectTransform.anchorMin = Vector2.zero; tex.rectTransform.anchorMax = Vector2.one; tex.rectTransform.offsetMin = tex.rectTransform.offsetMax = Vector2.zero;
                tex.raycastTarget = false;
                s.gradient = tex; s.track = capsule.rectTransform;
                var outline = UiKit.Panel(root.transform, "Outline", new Color(1, 1, 1, 0.3f), new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero, false);
                outline.sprite = UiKit.Circle; outline.type = Image.Type.Sliced; outline.pixelsPerUnitMultiplier = 1f; outline.raycastTarget = false;
                outline.rectTransform.pivot = new Vector2(0.5f, 0.5f); outline.rectTransform.offsetMin = new Vector2(Pad - 2, -19); outline.rectTransform.offsetMax = new Vector2(-Pad + 2, 19);
                outline.transform.SetAsFirstSibling();
                var k = UiKit.Panel(root.transform, "Knob", Color.white, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(52, 52), false);
                k.sprite = UiKit.Circle; k.rectTransform.pivot = new Vector2(0.5f, 0.5f); k.raycastTarget = false;
                var kf = UiKit.Panel(k.transform, "Colour", Color.gray, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36, 36), false);
                kf.sprite = UiKit.Circle; kf.rectTransform.pivot = new Vector2(0.5f, 0.5f); kf.raycastTarget = false;
                s.knob = k.rectTransform; s.knobFill = kf;
                if (stops != null) s.ColorAt = t => LockerColor.Ramp(stops, t);
                return s;
            }

            void EnsureTexture()
            {
                if (ready || ColorAt == null) return;
                const int n = 128;
                var tex = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                for (int i = 0; i < n; i++) tex.SetPixel(i, 0, ColorAt(i / (n - 1f)));
                tex.Apply();
                gradient.texture = tex;
                ready = true;
            }

            public void SetDimmed(bool dim) => group.alpha = dim ? 0.5f : 1f;

            public void Set(float t)
            {
                if (dragging) return;
                EnsureTexture();
                value = Mathf.Clamp01(t);
                Place();
            }

            void Place()
            {
                float w = ((RectTransform)transform).rect.width - 2 * Pad;
                if (w <= 0) w = 940 - 2 * Pad;
                knob.anchoredPosition = new Vector2(Pad + value * w, 0);
                knobFill.color = ColorAt != null ? ColorAt(value) : Color.gray;
            }

            void Move(PointerEventData e, bool done)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(track, e.position, e.pressEventCamera, out var local);
                float w = track.rect.width;
                value = Mathf.Clamp01((local.x + w / 2) / Mathf.Max(1, w));
                Place();
                Changed?.Invoke(value, done);
            }

            public void OnPointerDown(PointerEventData e) { dragging = true; Move(e, false); }
            public void OnDrag(PointerEventData e) => Move(e, false);
            public void OnPointerUp(PointerEventData e) { if (dragging) { Move(e, true); dragging = false; } }
        }

        sealed class SpinDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            public Action<float> Moved;
            public Action Released;
            public void OnBeginDrag(PointerEventData e) { }
            public void OnDrag(PointerEventData e) => Moved?.Invoke(e.delta.x);
            public void OnEndDrag(PointerEventData e) => Released?.Invoke();
        }
    }
}

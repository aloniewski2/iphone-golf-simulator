using System;
using System.Collections.Generic;
using GolfArcade.Game;
using GolfArcade.Profile;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GolfArcade.UI
{
    /// Where a golfer is made. The golfer stands large above a plain studio (Game/ClubStage.cs, the "studio" scene),
    /// turned by dragging; a white sheet below holds the tabs (Skin, Hair, Face, Hats, Outfit, Gear: Face and Hats only
    /// while the golfer has glasses, facial hair or hats to choose) and, under them,
    /// the choices: a picture tile for every style (renders in Resources/UI/Look), colour swatches for the
    /// quick picks and a quiet slider for any colour. Each tab scrolls, so a new style is one more tile and no layout
    /// work. The Hair, Face and Hats tabs bring the camera in to the head. The game supplies the look and receives
    /// edits through the members below; this class only draws and reports.
    public sealed class Locker
    {
        public enum Tab { Body, Hair, Face, Headwear, Outfit, Gear }
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

        // ---- Look (reference pixels, 1080 wide)
        const float W = 1080, SheetH = 1180, Bleed = 220, TabH = 150, FooterH = 230, Margin = 48, Inner = W - 2 * Margin;
        const int Tabs = 6;
        static readonly Color Paper = Color.white, Ink = UiKit.Hex("1B1938"), Muted = UiKit.Hex("8C89A6"), Line = UiKit.Hex("E9E7F3"),
            Tile = UiKit.Hex("F3F2F9"), TileOn = UiKit.Hex("E8E5FB"), Accent = UiKit.Hex("5A4BE0"), Cream = UiKit.Hex("F7F4EC");
        static readonly Vector2 TL = new(0, 1), TR = new(1, 1);

        readonly RectTransform[] pages = new RectTransform[Tabs];
        readonly Image[] tabBars = new Image[Tabs];
        readonly Graphic[] tabIcons = new Graphic[Tabs];
        readonly Text[] tabWords = new Text[Tabs];
        readonly Text nameWord, hintWord;
        Image skinTabDot;

        // SKIN
        Segments playerSegments; Swatches skinSwatches; Slider skinSlider; Image skinChip;
        // HAIR
        TileGrid haircutTiles; Text haircutNames; Swatches hairSwatches; Slider hairSlider, dyeSlider; Image hairChip;
        // FACE
        TileGrid glassesTiles, facialTiles; Text glassesNames, facialNames;
        // HATS
        TileGrid hatTiles; Text hatNames, hatNote; Slider hatHue, hatShade; Image hatChip;
        // OUTFIT
        Segments slotSegments; TileGrid topTiles, bottomTiles, shoeTiles; Text styleNames;
        readonly string[] slots = { "shirt", "shorts", "shoes" };
        int slot;
        Swatches outfitSwatches; Choice outfitReset; Slider outfitHue, outfitShade; Text outfitName, mixerNote; Image outfitChip; Text outfitResetWord;
        // GEAR
        public HoldButton[] Balls = new HoldButton[0], Trails = new HoldButton[0], Clubs = new HoldButton[0];
        readonly RowParts[] gear = new RowParts[3];

        public Locker(Transform parent)
        {
            Root = Rect(parent, "Locker", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // the golfer's ground: drag to turn them round
            var spinArea = UiKit.Panel(Root, "Spin area", Color.clear, new Vector2(0, 0.38f), Vector2.one, Vector2.zero, Vector2.zero, false);
            spinArea.rectTransform.offsetMin = spinArea.rectTransform.offsetMax = Vector2.zero;
            var drag = spinArea.gameObject.AddComponent<SpinDrag>();
            drag.Moved = dx => Spin?.Invoke(dx);
            drag.Released = () => SpinDone?.Invoke();

            // the header: the title, whose golfer, and SHUFFLE
            var title = UiKit.Label(Root, "Title", 64, TextAnchor.UpperLeft, TL, TL, new Vector2(56, -64), new Vector2(640, 84), UiKit.Strong, false);
            title.text = "Your look"; title.color = Ink; title.raycastTarget = false;
            nameWord = UiKit.Label(Root, "Name", 32, TextAnchor.UpperLeft, TL, TL, new Vector2(58, -146), new Vector2(640, 44), UiKit.Body, false);
            nameWord.color = Muted; nameWord.text = "Your golfer"; nameWord.raycastTarget = false;
            Shuffle = Pill(Root, "Shuffle", "Shuffle", TR, new Vector2(-48, -70), new Vector2(246, 92), Paper, Ink, 34, outline: true);
            var hint = hintWord = UiKit.Label(Root, "Drag hint", 28, TextAnchor.MiddleRight, TR, TR, new Vector2(-50, -176), new Vector2(400, 40), UiKit.Body, false);
            hint.text = "Drag to turn"; hint.color = Muted; hint.raycastTarget = false;

            // the sheet, with a soft shadow along its top edge
            var shadow = UiKit.Panel(Root, "Sheet shadow", new Color(0.12f, 0.09f, 0.32f, 0.10f), Vector2.zero, new Vector2(1, 0), new Vector2(0, SheetH), new Vector2(0, 90), false);
            shadow.sprite = ShadowRamp(); shadow.raycastTarget = false;
            var sheet = UiKit.Panel(Root, "Sheet", Paper, Vector2.zero, new Vector2(1, 0), new Vector2(0, -Bleed), new Vector2(0, SheetH + Bleed));
            sheet.sprite = UiKit.RoundedLarge; sheet.type = Image.Type.Sliced; sheet.pixelsPerUnitMultiplier = 0.9f;

            BuildTabs(sheet.rectTransform);
            var divider = UiKit.Panel(sheet.transform, "Divider", Line, TL, new Vector2(1, 1), new Vector2(0, -TabH), new Vector2(0, 2), false); divider.raycastTarget = false;
            for (int i = 0; i < Tabs; i++) pages[i] = NewPage(sheet.rectTransform, (Tab)i);
            BuildBody(pages[(int)Tab.Body]);
            BuildHair(pages[(int)Tab.Hair]);
            BuildFace(pages[(int)Tab.Face]);
            BuildHeadwear(pages[(int)Tab.Headwear]);
            BuildOutfit(pages[(int)Tab.Outfit]);

            // LET'S GO, at the foot of the sheet
            Go = Pill(sheet.rectTransform, "Lets go", "Let's go", new Vector2(0.5f, 0), new Vector2(0, 76), new Vector2(Inner, 124), Ink, Color.white, 44, outline: false);
            Show(Tab.Body, notify: false);
        }

        // ================================================================= the tabs

        /// A tab is there when the golfer has something to choose on it (Face: glasses or facial hair; Hats: headwear).
        public static bool TabAvailable(Tab tab) => tab switch
        {
            Tab.Face => HeroGolfer.GlassesNames.Length > 1 || HeroGolfer.FacialNames.Length > 1,
            Tab.Headwear => HeroGolfer.HeadwearNames.Length > 1,
            _ => true,
        };

        void BuildTabs(RectTransform sheet)
        {
            string[] names = { "BODY", "HAIR", "FACE", "HEADWEAR", "OUTFIT", "GEAR" };      // (the objects' names)
            string[] words = { "Skin", "Hair", "Face", "Hats", "Outfit", "Gear" };
            string[] pictures = { null, "hair_classic_m", "glasses_round", "hat_cap", "tab_outfit", null };
            int shown = 0;
            for (int i = 0; i < Tabs; i++) if (TabAvailable((Tab)i)) shown++;
            float w = (W - 2 * 24) / shown;
            int slotAt = 0;
            for (int i = 0; i < Tabs; i++)
            {
                if (!TabAvailable((Tab)i)) continue;
                int index = i;
                var tab = UiKit.Panel(sheet, "Tab " + names[i], Color.clear, TL, TL, new Vector2(24 + slotAt++ * w, 0), new Vector2(w, TabH), false);
                tab.rectTransform.pivot = TL;
                var hold = tab.gameObject.AddComponent<HoldButton>();
                hold.Fill = tab; hold.RestColor = Color.clear; hold.PressedColor = Color.clear;
                hold.Pressed = () => Show((Tab)index);
                // the picture: the kit's own render (a dot of the skin colour for SKIN, the iron for GEAR)
                if (pictures[i] != null)
                {
                    var raw = Picture(tab.transform, "Icon", pictures[i], 66);
                    raw.rectTransform.anchorMin = raw.rectTransform.anchorMax = new Vector2(0.5f, 1); raw.rectTransform.anchoredPosition = new Vector2(0, -52);
                    tabIcons[i] = raw;
                }
                else if (i == (int)Tab.Body)
                {
                    var dot = UiKit.Panel(tab.transform, "Icon", Color.gray, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(52, 52));
                    dot.sprite = UiKit.Circle; dot.rectTransform.pivot = new Vector2(0.5f, 0.5f); dot.raycastTarget = false;
                    skinTabDot = dot; tabIcons[i] = dot;
                }
                else tabIcons[i] = Icons.Place(tab.transform, "iron", Ink, new Vector2(0.5f, 1), new Vector2(0, -52), 52);
                var word = UiKit.Label(tab.transform, "Word", 26, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(1, 0), new Vector2(0, 18), new Vector2(0, 36), UiKit.Ui, false);
                word.text = words[i]; word.raycastTarget = false; tabWords[i] = word;
                var bar = UiKit.Panel(tab.transform, "Underline", Accent, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 0), new Vector2(76, 6));
                bar.rectTransform.pivot = new Vector2(0.5f, 0); bar.raycastTarget = false; tabBars[i] = bar;
            }
        }

        public void Show(Tab tab, bool notify = true)
        {
            Current = tab;
            for (int i = 0; i < Tabs; i++)
            {
                bool on = i == (int)tab;
                pages[i].parent.gameObject.SetActive(on);
                if (!tabBars[i]) continue;      // (a tab the golfer has nothing for)
                tabBars[i].gameObject.SetActive(on);
                tabWords[i].color = on ? Ink : Muted;
                var c = tabIcons[i].color; c.a = on ? 1f : 0.5f; if (tabIcons[i] is RawImage) c = new Color(1, 1, 1, c.a); tabIcons[i].color = c;
            }
            hintWord.gameObject.SetActive(tab is Tab.Body or Tab.Outfit or Tab.Gear);      // (turning the head's close-up is not what it is for)
            Refresh();
            if (notify) TabChanged?.Invoke(tab);
        }

        /// Whose golfer is being made ("Your golfer", or a player's name).
        public void SetName(string text) => nameWord.text = text;

        /// Re-reads the look and lights every choice on the tab shown.
        public void Refresh()
        {
            var look = Look?.Invoke();
            if (look == null) return;
            var skin = GolferStyle.ColorOf(look.Skin) ?? GolferStyle.SkinTones[GolferStyle.DefaultSkin];
            if (skinTabDot) { var c = skin; c.a = Current == Tab.Body ? 1f : 0.55f; skinTabDot.color = c; }
            switch (Current)
            {
                case Tab.Body:
                    playerSegments.Choose(look.Body == CharacterLook.Girl ? 1 : 0);
                    skinSlider.Set(LockerColor.RampPosition(LockerColor.SkinStops, skin)); skinChip.color = skin;
                    skinSwatches.Choose(NearestSwatch(LockerColor.SkinStops, LockerColor.SkinPresets, skin));
                    break;
                case Tab.Hair:
                    haircutTiles.Retexture(look.Female ? "_f" : "_m");
                    haircutTiles.Choose(look.HaircutId);
                    haircutNames.text = HeroGolfer.Pretty(HeroGolfer.HaircutNames[Mathf.Clamp(look.HaircutId, 0, HeroGolfer.HaircutNames.Length - 1)]);
                    var hair = GolferStyle.ColorOf(look.Hair) ?? GolferStyle.HairColors[GolferStyle.DefaultHairTone];
                    hairChip.color = hair;
                    hairSlider.Set(LockerColor.RampPosition(LockerColor.HairStops, hair));
                    var hairPresetT = new float[LockerColor.HairPresets.Length];
                    for (int i = 0; i < hairPresetT.Length; i++) hairPresetT[i] = LockerColor.HairPresets[i].t;
                    hairSwatches.Choose(NearestSwatch(LockerColor.HairStops, hairPresetT, hair));
                    var (dh, _) = LockerColor.HueShadeOf(hair);
                    dyeSlider.Set(dh);
                    break;
                case Tab.Face:
                    glassesTiles.Choose(look.Glasses); facialTiles.Choose(look.Facial);
                    glassesNames.text = HeroGolfer.Pretty(HeroGolfer.GlassesNames[Mathf.Clamp(look.Glasses, 0, HeroGolfer.GlassesNames.Length - 1)]);
                    facialNames.text = HeroGolfer.Pretty(HeroGolfer.FacialNames[Mathf.Clamp(look.Facial, 0, HeroGolfer.FacialNames.Length - 1)]);
                    break;
                case Tab.Headwear:
                    hatTiles.Choose(look.Headwear);
                    string hatName = HeroGolfer.HeadwearNames[Mathf.Clamp(look.Headwear, 0, HeroGolfer.HeadwearNames.Length - 1)];
                    hatNames.text = HeroGolfer.Pretty(hatName);
                    var hat = GolferStyle.ColorOf(look.Hat) ?? HeroGolfer.HatDefault(look.Headwear);
                    hatChip.color = hat;
                    var (hh, hs) = LockerColor.HueShadeOf(hat);
                    hatHue.Set(hh); hatShade.Set((hs + 1) / 2);
                    bool coloured = look.Headwear >= 1;
                    hatNote.text = coloured ? $"Pick the colour of the {HeroGolfer.Pretty(hatName).ToLowerInvariant()}." : "No hat: your hair shows in full.";
                    hatHue.SetDimmed(!coloured); hatShade.SetDimmed(!coloured);
                    break;
                case Tab.Outfit:
                    slotSegments.Choose(slot);
                    if (topTiles != null)
                    {
                        topTiles.Root.gameObject.SetActive(slot == 0); bottomTiles.Root.gameObject.SetActive(slot == 1); shoeTiles.Root.gameObject.SetActive(slot == 2);
                        topTiles.Choose(look.Top); bottomTiles.Choose(look.Bottom); shoeTiles.Choose(0);
                        styleNames.text = slot == 0 ? HeroGolfer.Pretty(HeroGolfer.TopNames[Mathf.Clamp(look.Top, 0, HeroGolfer.TopNames.Length - 1)])
                            : slot == 1 ? HeroGolfer.Pretty(HeroGolfer.BottomNames[Mathf.Clamp(look.Bottom, 0, HeroGolfer.BottomNames.Length - 1)]) : "Golf shoes";
                    }
                    string hex = slot == 0 ? look.Shirt : slot == 1 ? look.Shorts : look.Shoes;
                    var asDesigned = string.IsNullOrEmpty(hex);
                    var fallback = slot == 0 ? Cream : slot == 1 ? GolferStyle.KitColors[0] : GolferStyle.ShoesAsDesigned;
                    var col = GolferStyle.ColorOf(hex) ?? fallback;
                    outfitChip.color = col;
                    outfitName.text = asDesigned ? "As designed" : LockerColor.Describe(col);
                    var (oh, os) = LockerColor.HueShadeOf(col);
                    outfitHue.Set(oh); outfitShade.Set((os + 1) / 2);
                    outfitReset.GetComponent<Image>().color = asDesigned ? TileOn : Tile;
                    outfitResetWord.color = asDesigned ? Accent : Ink;
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

        // ================================================================= the pages

        void BuildBody(RectTransform p)
        {
            float y = 8;
            Section(p, ref y, "Player", null);
            playerSegments = Segment(p, ref y, new[] { "Boy", "Girl" }, i => Change?.Invoke(l => l.Body = i, true));
            y += 36;
            Section(p, ref y, "Skin tone", out skinChip);
            var skinColours = new Color[LockerColor.SkinPresets.Length];
            for (int i = 0; i < skinColours.Length; i++) skinColours[i] = LockerColor.Ramp(LockerColor.SkinStops, LockerColor.SkinPresets[i]);
            skinSwatches = Swatches.Make(p, ref y, skinColours, 78, false);
            for (int i = 0; i < skinColours.Length; i++)
            {
                var c = skinColours[i];
                skinSwatches.Choices[i].Clicked = () => Change?.Invoke(l => l.Skin = GolferStyle.HexOf(c), true);
            }
            y += 14;
            Caption(p, ref y, "Fine tune");
            skinSlider = Slider.Make(p, 0, y, Inner, LockerColor.SkinStops, null);
            skinSlider.ColorAt = t => LockerColor.Ramp(LockerColor.SkinStops, t);
            skinSlider.Changed = (t, done) => Change?.Invoke(l => l.Skin = GolferStyle.HexOf(LockerColor.Ramp(LockerColor.SkinStops, t)), done);
            y += 90;
            Finish(p, y);
        }

        void BuildHair(RectTransform p)
        {
            float y = 8;
            haircutNames = Section(p, ref y, "Hairstyle", null);
            var textures = new string[HeroGolfer.HaircutNames.Length];
            for (int i = 0; i < textures.Length; i++) textures[i] = "hair_" + HeroGolfer.HaircutNames[i].ToLowerInvariant();
            haircutTiles = Grid(p, ref y, textures, cut =>
            {
                if (!LockedOk(Rows.Haircut, cut.ToString())) return;
                Change?.Invoke(l => l.Haircut = cut, true);
            }, 5, "_m");      // (his hair and hers are not the same: each cut has a picture for the boy and the girl)
            y += 24;
            Section(p, ref y, "Hair colour", out hairChip);
            var hairColours = new Color[LockerColor.HairPresets.Length];
            for (int i = 0; i < hairColours.Length; i++) hairColours[i] = LockerColor.Ramp(LockerColor.HairStops, LockerColor.HairPresets[i].t);
            hairSwatches = Swatches.Make(p, ref y, hairColours, 70, false);
            for (int i = 0; i < hairColours.Length; i++)
            {
                var c = hairColours[i];
                hairSwatches.Choices[i].Clicked = () => Change?.Invoke(l => l.Hair = GolferStyle.HexOf(c), true);
            }
            y += 14;
            Caption(p, ref y, "Natural shades");
            hairSlider = Slider.Make(p, 0, y, Inner, LockerColor.HairStops, null);
            hairSlider.ColorAt = t => LockerColor.Ramp(LockerColor.HairStops, t);
            hairSlider.Changed = (t, done) => Change?.Invoke(l => l.Hair = GolferStyle.HexOf(LockerColor.Ramp(LockerColor.HairStops, t)), done);
            y += 84;
            Caption(p, ref y, "Dye");
            dyeSlider = Slider.Make(p, 0, y, Inner, null, null);
            dyeSlider.Rainbow = true;
            dyeSlider.ColorAt = t => LockerColor.HueShade(t, 0);
            dyeSlider.Changed = (t, done) => Change?.Invoke(l => l.Hair = GolferStyle.HexOf(LockerColor.HueShade(t, 0.1f)), done);
            y += 90;
            Finish(p, y);
        }

        void BuildFace(RectTransform p)
        {
            float y = 8;
            glassesNames = Section(p, ref y, "Glasses", null);
            var g = new string[HeroGolfer.GlassesNames.Length];
            for (int i = 0; i < g.Length; i++) g[i] = i == 0 ? null : "glasses_" + HeroGolfer.GlassesNames[i].ToLowerInvariant();
            glassesTiles = Grid(p, ref y, g, i => Change?.Invoke(l => l.Glasses = i, true));
            y += 24;
            facialNames = Section(p, ref y, "Facial hair", null);
            var f = new string[HeroGolfer.FacialNames.Length];
            for (int i = 0; i < f.Length; i++) f[i] = i == 0 ? null : "facial_" + HeroGolfer.FacialNames[i].ToLowerInvariant();
            facialTiles = Grid(p, ref y, f, i => Change?.Invoke(l => l.Facial = i, true));
            y += 6;
            Caption(p, ref y, "Facial hair takes your hair colour.");
            Finish(p, y);
        }

        void BuildHeadwear(RectTransform p)
        {
            float y = 8;
            hatNames = Section(p, ref y, "Hat", null);
            var h = new string[HeroGolfer.HeadwearNames.Length];
            for (int i = 0; i < h.Length; i++) h[i] = i == 0 ? null : "hat_" + HeroGolfer.HeadwearNames[i].ToLowerInvariant();
            hatTiles = Grid(p, ref y, h, wear =>
            {
                if (!LockedOk(Rows.Headwear, wear.ToString())) return;
                Change?.Invoke(l => l.Headwear = wear, true);
            });
            y += 24;
            Section(p, ref y, "Hat colour", out hatChip);
            hatNote = Caption(p, ref y, "");
            hatHue = Slider.Make(p, 0, y, Inner, null, null); hatHue.Rainbow = true;
            hatHue.ColorAt = t => LockerColor.HueShade(t, 0);
            hatHue.Changed = (t, done) => Change?.Invoke(l => l.Hat = GolferStyle.HexOf(LockerColor.HueShade(t, ShadeOf(l))), done);
            y += 84;
            Caption(p, ref y, "Light to dark");
            hatShade = Slider.Make(p, 0, y, Inner, null, null);
            hatShade.ColorAt = t => LockerColor.HueShade(0.0f, t * 2 - 1);
            hatShade.Changed = (t, done) => Change?.Invoke(l => l.Hat = GolferStyle.HexOf(LockerColor.HueShade(HueOf(l), t * 2 - 1)), done);
            y += 90;
            Finish(p, y);
        }

        static float ShadeOf(CharacterLook l) => LockerColor.HueShadeOf(GolferStyle.ColorOf(l.Hat) ?? HeroGolfer.HatDefault(l.Headwear)).shade;
        static float HueOf(CharacterLook l) => LockerColor.HueShadeOf(GolferStyle.ColorOf(l.Hat) ?? HeroGolfer.HatDefault(l.Headwear)).hue;

        void BuildOutfit(RectTransform p)
        {
            float y = 8;
            slotSegments = Segment(p, ref y, new[] { "Shirt", "Pants", "Shoes" }, i => { slot = i; Refresh(); });
            y += 34;
            if (HeroGolfer.TopNames.Length > 1 || HeroGolfer.BottomNames.Length > 1)
            {
                // the styles of top and bottom, once there is more than the one kit
                styleNames = Section(p, ref y, "Style", null);
                float gridY = y;
                var tops = new string[HeroGolfer.TopNames.Length];
                for (int i = 0; i < tops.Length; i++) tops[i] = "top_" + HeroGolfer.TopNames[i].ToLowerInvariant();
                topTiles = Grid(p, ref y, tops, i => Change?.Invoke(l => l.Top = i, true));
                y = gridY;
                var bottoms = new string[HeroGolfer.BottomNames.Length];
                for (int i = 0; i < bottoms.Length; i++) bottoms[i] = "bottom_" + HeroGolfer.BottomNames[i].ToLowerInvariant();
                bottomTiles = Grid(p, ref y, bottoms, i => Change?.Invoke(l => l.Bottom = i, true));
                y = gridY;
                shoeTiles = Grid(p, ref y, new[] { "shoes_golf" }, _ => { });
                y += 24;
            }
            Section(p, ref y, "Colour", out outfitChip);
            outfitName = Caption(p, ref y, "");
            outfitHue = Slider.Make(p, 0, y, Inner, null, null); outfitHue.Rainbow = true;
            outfitHue.ColorAt = t => LockerColor.HueShade(t, 0);
            outfitHue.Changed = (t, done) => ChangeOutfit(l => LockerColor.HueShade(t, ShadeNow(l)), done);
            y += 84;
            outfitShade = Slider.Make(p, 0, y, Inner, null, null);
            outfitShade.ColorAt = t => LockerColor.HueShade(0.58f, t * 2 - 1);
            outfitShade.Changed = (t, done) => ChangeOutfit(l => LockerColor.HueShade(HueNow(l), t * 2 - 1), done);
            y += 84;
            mixerNote = Caption(p, ref y, "The colour mixer is earned; the quick picks below are open.");
            y += 4;
            Section(p, ref y, "Quick picks", null);
            var colours = GolferStyle.ShirtColors;
            outfitSwatches = Swatches.Make(p, ref y, colours, 70, false);
            for (int i = 0; i < colours.Length; i++)
            {
                int index = i;
                outfitSwatches.Choices[i].Clicked = () =>
                {
                    if (!OutfitColourOpen(index)) { LockedPressed?.Invoke(slot == 1 ? Rows.Shorts : slot == 0 ? Rows.Shirt : Rows.Shoes, $"outfit.{index}"); return; }
                    var palette = slot == 1 ? GolferStyle.KitColors : GolferStyle.ShirtColors;
                    Change?.Invoke(l => SetSlot(l, GolferStyle.HexOf(palette[index])), true);
                };
            }
            y += 8;
            var reset = Box(p, "As designed", Tile, Inner, 84, 0, y);
            outfitReset = reset.gameObject.AddComponent<Choice>();
            outfitReset.Clicked = () => Change?.Invoke(l => SetSlot(l, ""), true);
            outfitResetWord = UiKit.Label(reset.transform, "Word", 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Strong, false);
            outfitResetWord.text = "Back to the kit as designed"; outfitResetWord.raycastTarget = false;
            y += 100;
            Finish(p, y);
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

        sealed class RowParts { public HoldButton[] Holds; public Swatches Row; public Text Chosen; public string[] Names; }

        /// The GEAR tab's rows: the balls, the trails and the clubs' finishes, by name and swatch (a trail's may be a face of its own).
        public void AddGear(string[] balls, Color[] ballColors, string[] trails, Color[] trailColors, Sprite[] trailFaces, string[] clubs, Color[] clubColors)
        {
            var page = pages[(int)Tab.Gear];
            float y = 8;
            Balls = GearRow(page, ref y, "Ball", balls, ballColors, out gear[0], null); y += 30;
            Trails = GearRow(page, ref y, "Trail", trails, trailColors, out gear[1], trailFaces); y += 30;
            Clubs = GearRow(page, ref y, "Clubs", clubs, clubColors, out gear[2], null);
            Finish(page, y);
        }

        HoldButton[] GearRow(RectTransform page, ref float y, string label, string[] names, Color[] colors, out RowParts parts, Sprite[] faces)
        {
            var chosen = Section(page, ref y, label, null);
            var row = Swatches.Make(page, ref y, colors, 78, true, faces);
            // each is findable by what it is ("BALL Gold", "TRAIL Fire", "CLUBS Classic"): the tests press them, and so would anything reading the screen
            for (int i = 0; i < row.Holds.Length && i < names.Length; i++) if (row.Holds[i]) row.Holds[i].gameObject.name = $"{label.ToUpperInvariant()} {names[i]}";
            parts = new RowParts { Holds = row.Holds, Row = row, Chosen = chosen, Names = names };
            return row.Holds;
        }

        /// Padlocks on the swatches still to be earned (true is open), dimming them.
        public void SetOpen(Rows row, bool[] open)
        {
            var r = row switch { Rows.Ball => gear[0], Rows.Trail => gear[1], Rows.Club => gear[2], _ => null };
            r?.Row.SetOpen(open);
        }

        /// Rings the chosen ball, trail and club.
        public void RefreshGear(int ball, int trail, int club) { ChooseGear(gear[0], ball); ChooseGear(gear[1], trail); ChooseGear(gear[2], club); }

        static void ChooseGear(RowParts row, int index)
        {
            if (row == null || index < 0 || index >= row.Holds.Length) return;
            row.Row.Choose(index);
            row.Chosen.text = row.Names[index];
        }

        /// A padlocked swatch pressed: what it takes, on the row's label.
        public void SayLocked(Rows row, string requirement)
        {
            var text = $"Locked: {requirement}";
            var r = row switch { Rows.Ball => gear[0], Rows.Trail => gear[1], Rows.Club => gear[2], _ => null };
            if (r != null) { r.Chosen.text = text; return; }
            if (row == Rows.Mixer) { mixerNote.text = text; mixerNote.gameObject.SetActive(true); }
            else outfitName.text = text;
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

        /// A tab's page: a scrolling window between the tabs and the foot of the sheet; returns what scrolls.
        RectTransform NewPage(RectTransform sheet, Tab tab)
        {
            var view = Rect(sheet, "Page " + tab, Vector2.zero, Vector2.one, new Vector2(Margin, FooterH), new Vector2(-Margin, -(TabH + 2)));
            view.gameObject.AddComponent<RectMask2D>();
            var catcher = view.gameObject.AddComponent<Image>(); catcher.color = new Color(0, 0, 0, 0);
            var content = Rect(view, "Content", TL, TR, new Vector2(0, -100), Vector2.zero);
            content.pivot = new Vector2(0.5f, 1);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = view; scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic; scroll.scrollSensitivity = 40f; scroll.decelerationRate = 0.12f;
            view.gameObject.SetActive(false);
            return content;
        }

        /// Sets how tall a page's content is (so it scrolls when it is taller than the window).
        static void Finish(RectTransform content, float height)
        {
            content.sizeDelta = new Vector2(0, height + 40);
            content.anchoredPosition = Vector2.zero;
        }

        /// A heading, with its chosen word at the right; or (chip form) a colour chip there.
        static Text Section(RectTransform p, ref float y, string title, Text unused)
        {
            var t = UiKit.Label(p, "Heading " + title, 36, TextAnchor.MiddleLeft, TL, TL, new Vector2(0, -y), new Vector2(560, 50), UiKit.Strong, false);
            t.text = title; t.color = Ink; t.raycastTarget = false;
            var v = UiKit.Label(p, "Chosen", 30, TextAnchor.MiddleRight, TR, TR, new Vector2(0, -y), new Vector2(440, 50), UiKit.Body, false);
            v.color = Muted; v.raycastTarget = false;
            y += 68;
            return v;
        }

        static void Section(RectTransform p, ref float y, string title, out Image chip)
        {
            Section(p, ref y, title, (Text)null).gameObject.SetActive(false);
            var rim = UiKit.Panel(p, "Chip", Line, TR, TR, new Vector2(0, -(y - 62)), new Vector2(46, 46));
            rim.sprite = UiKit.Circle; rim.rectTransform.pivot = TR; rim.raycastTarget = false;
            chip = UiKit.Panel(rim.transform, "Colour", Color.gray, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 38));
            chip.sprite = UiKit.Circle; chip.rectTransform.pivot = new Vector2(0.5f, 0.5f); chip.raycastTarget = false;
        }

        /// A line of small muted text.
        static Text Caption(RectTransform p, ref float y, string text)
        {
            var t = UiKit.Label(p, "Caption", 27, TextAnchor.MiddleLeft, TL, TL, new Vector2(0, -y), new Vector2(Inner, 38), UiKit.Body, false);
            t.text = text; t.color = Muted; t.raycastTarget = false;
            y += 46;
            return t;
        }

        static Image Box(RectTransform p, string name, Color color, float w, float h, float x, float y)
        {
            var img = UiKit.Panel(p, name, color, TL, TL, new Vector2(x, -y), new Vector2(w, h));
            img.sprite = UiKit.RoundedLarge; img.type = Image.Type.Sliced; img.pixelsPerUnitMultiplier = 1.2f;
            return img;
        }

        /// A rounded button on the sheet or the header: a solid face (Go), or a white one with a hairline (Shuffle).
        static HoldButton Pill(Transform parent, string name, string word, Vector2 anchor, Vector2 pos, Vector2 size, Color face, Color ink, int fontSize, bool outline)
        {
            var edge = UiKit.Panel(parent, name, outline ? Line : face, anchor, anchor, pos, size);
            edge.sprite = UiKit.RoundedLarge; edge.type = Image.Type.Sliced; edge.pixelsPerUnitMultiplier = 1.2f;
            edge.rectTransform.pivot = anchor.y > 0.5f && anchor.x > 0.5f ? new Vector2(1, 1) : new Vector2(0.5f, 0);
            Image fill = edge;
            if (outline)
            {
                fill = UiKit.Panel(edge.transform, "Fill", face, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                fill.sprite = UiKit.RoundedLarge; fill.type = Image.Type.Sliced; fill.pixelsPerUnitMultiplier = 1.2f;
                fill.rectTransform.offsetMin = new Vector2(3, 3); fill.rectTransform.offsetMax = new Vector2(-3, -3); fill.raycastTarget = false;
            }
            var t = UiKit.Label(edge.transform, "Word", fontSize, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Strong, false);
            t.text = word; t.color = ink; t.raycastTarget = false;
            var hold = edge.gameObject.AddComponent<HoldButton>();
            hold.Fill = fill; hold.RestColor = face; hold.PressedColor = outline ? Tile : Accent;
            return hold;
        }

        static RawImage Picture(Transform parent, string name, string texture, float size)
        {
            var raw = new GameObject(name).AddComponent<RawImage>();
            raw.transform.SetParent(parent, false);
            raw.texture = Resources.Load<Texture2D>("UI/Look/" + texture); raw.raycastTarget = false;
            raw.color = raw.texture ? Color.white : new Color(1, 1, 1, 0);
            raw.rectTransform.sizeDelta = new Vector2(size, size);
            return raw;
        }

        static Sprite shadowRamp;
        /// A soft shadow: dark at the bottom, clear at the top.
        static Sprite ShadowRamp()
        {
            if (shadowRamp) return shadowRamp;
            var tex = new Texture2D(2, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < 64; y++) { float a = Mathf.Pow(y / 63f, 2.2f); a = 1f - a; for (int x = 0; x < 2; x++) tex.SetPixel(x, y, new Color(1, 1, 1, a * a)); }
            tex.Apply();
            return shadowRamp = Sprite.Create(tex, new Rect(0, 0, 2, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        // ---- a click that is not a scroll: pointer-up on the same thing (the ScrollRect keeps the drags)
        public sealed class Choice : MonoBehaviour, IPointerClickHandler
        {
            public Action Clicked;
            public void OnPointerClick(PointerEventData e) => Clicked?.Invoke();
        }

        // ---- a grid of picture tiles; the chosen one is ringed
        sealed class TileGrid
        {
            public RectTransform Root; Image[] rings, fills; RawImage[] pics; string[] bases;
            public void Choose(int index)
            {
                for (int i = 0; i < rings.Length; i++) { rings[i].enabled = i == index; fills[i].color = i == index ? TileOn : Tile; }
            }
            /// The pictures again for another golfer ("_m", "_f": a style that looks different on the boy and the girl, like hair).
            public void Retexture(string suffix)
            {
                for (int i = 0; i < pics.Length; i++)
                {
                    if (!pics[i]) continue;
                    pics[i].texture = Resources.Load<Texture2D>("UI/Look/" + bases[i] + suffix);
                    pics[i].color = pics[i].texture ? Color.white : new Color(1, 1, 1, 0);
                }
            }
            public static TileGrid Make(RectTransform p, ref float y, string[] textures, Action<int> pick, int cols, string suffix = "")
            {
                const float gap = 20;
                float size = (Inner - (cols - 1) * gap) / cols;
                var g = new TileGrid { rings = new Image[textures.Length], fills = new Image[textures.Length], pics = new RawImage[textures.Length], bases = textures };
                g.Root = Rect(p, "Tiles", TL, TL, Vector2.zero, Vector2.zero); g.Root.pivot = TL;
                int rows = (textures.Length + cols - 1) / cols;
                g.Root.anchoredPosition = new Vector2(0, -y); g.Root.sizeDelta = new Vector2(Inner, rows * (size + gap) - gap);
                for (int i = 0; i < textures.Length; i++)
                {
                    int index = i;
                    float x = i % cols * (size + gap), yy = i / cols * (size + gap);
                    var ring = Box(g.Root, "Ring " + i, Accent, size + 10, size + 10, x - 5, yy - 5); ring.raycastTarget = false; ring.enabled = false; g.rings[i] = ring;
                    var tile = Box(g.Root, "Tile " + i + " " + (textures[i] ?? "none"), Tile, size, size, x, yy); g.fills[i] = tile;
                    tile.gameObject.AddComponent<Choice>().Clicked = () => pick(index);
                    if (textures[i] != null)
                    {
                        var pic = Picture(tile.transform, "Picture", textures[i] + suffix, 0);
                        pic.rectTransform.anchorMin = Vector2.zero; pic.rectTransform.anchorMax = Vector2.one;
                        pic.rectTransform.offsetMin = new Vector2(8, 8); pic.rectTransform.offsetMax = new Vector2(-8, -8);
                        g.pics[i] = pic;
                    }
                    else DrawNone(tile.transform, size);
                }
                y += rows * (size + gap) - gap + 4;
                return g;
            }
            /// "None": a ring with a slash through it.
            static void DrawNone(Transform parent, float size)
            {
                var ring = UiKit.Panel(parent, "None", new Color(Muted.r, Muted.g, Muted.b, 0.7f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.42f, size * 0.42f), false);
                ring.sprite = UiKit.RingOf(128, 12); ring.rectTransform.pivot = new Vector2(0.5f, 0.5f); ring.raycastTarget = false;
                var bar = UiKit.Panel(ring.transform, "Slash", new Color(Muted.r, Muted.g, Muted.b, 0.7f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.42f, 8), false);
                bar.rectTransform.pivot = new Vector2(0.5f, 0.5f); bar.rectTransform.localRotation = Quaternion.Euler(0, 0, -45); bar.raycastTarget = false;
            }
        }

        static TileGrid Grid(RectTransform p, ref float y, string[] textures, Action<int> pick, int cols = 5, string suffix = "") => TileGrid.Make(p, ref y, textures, pick, cols, suffix);

        // ---- a segmented control: a few words in one rounded bar, the chosen one filled
        sealed class Segments
        {
            Image[] fills; Text[] words;
            public void Choose(int index)
            {
                for (int i = 0; i < fills.Length; i++) { fills[i].color = i == index ? Ink : Color.clear; words[i].color = i == index ? Color.white : Ink; }
            }
            public static Segments Make(RectTransform p, ref float y, string[] names, Action<int> pick)
            {
                var s = new Segments { fills = new Image[names.Length], words = new Text[names.Length] };
                var bar = Box(p, "Segments", Tile, Inner, 100, 0, y); bar.raycastTarget = false;
                float w = (Inner - 12) / names.Length;
                for (int i = 0; i < names.Length; i++)
                {
                    int index = i;
                    var seg = UiKit.Panel(bar.transform, "Segment " + names[i], Color.clear, TL, TL, new Vector2(6 + i * w, -6), new Vector2(w, 88));
                    seg.sprite = UiKit.RoundedLarge; seg.type = Image.Type.Sliced; seg.pixelsPerUnitMultiplier = 1.2f;
                    seg.gameObject.AddComponent<Choice>().Clicked = () => pick(index);
                    var word = UiKit.Label(seg.transform, "Word", 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UiKit.Strong, false);
                    word.text = names[i]; word.raycastTarget = false;
                    s.fills[i] = seg; s.words[i] = word;
                }
                y += 100;
                return s;
            }
        }

        static Segments Segment(RectTransform p, ref float y, string[] names, Action<int> pick) => Segments.Make(p, ref y, names, pick);

        // ---- a row of colour swatches; the chosen one is ringed. `hold` makes them HoldButtons (the gear rows the game wires).
        sealed class Swatches
        {
            public Choice[] Choices; public HoldButton[] Holds;
            Image[] rings, gaps, locks, fills;

            public static Swatches Make(RectTransform p, ref float y, Color[] colors, float size, bool hold, Sprite[] faces = null)
            {
                int n = colors.Length;
                var s = new Swatches { Choices = new Choice[n], Holds = new HoldButton[n], rings = new Image[n], gaps = new Image[n], locks = new Image[n], fills = new Image[n] };
                float step = Mathf.Min(size + 26, (Inner - size - 16) / Mathf.Max(1, n - 1));
                float rowH = size + 22;
                for (int i = 0; i < n; i++)
                {
                    float cx = 8 + size / 2 + step * i, cy = y + rowH / 2;
                    Image Disc(string name, Color c, float d)
                    {
                        var img = UiKit.Panel(p, name + i, c, TL, TL, new Vector2(cx - d / 2, -(cy - d / 2)), new Vector2(d, d), false);
                        img.sprite = UiKit.Circle; img.rectTransform.pivot = TL; img.raycastTarget = false;
                        return img;
                    }
                    s.rings[i] = Disc("Ring ", Ink, size + 20); s.rings[i].enabled = false;
                    s.gaps[i] = Disc("Gap ", Paper, size + 10); s.gaps[i].enabled = false;
                    var rim = Disc("Rim ", Line, size + 4);
                    var fill = Disc("Swatch ", colors[i], size); fill.raycastTarget = true; s.fills[i] = fill;
                    if (faces != null && faces[i]) { fill.sprite = faces[i]; fill.color = Color.white; }
                    var badge = UiKit.Panel(fill.transform, "Padlock", Ink, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-4, 4), new Vector2(size * 0.42f, size * 0.42f), false);
                    badge.sprite = UiKit.Circle; badge.rectTransform.pivot = new Vector2(1, 0); badge.raycastTarget = false;
                    Icons.Place(badge.transform, "lock", Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, size * 0.26f).raycastTarget = false;
                    badge.gameObject.SetActive(false);
                    s.locks[i] = badge;
                    if (hold)
                    {
                        var h = fill.gameObject.AddComponent<HoldButton>(); h.Fill = null; h.RestColor = fill.color; h.PressedColor = fill.color;
                        s.Holds[i] = h;
                    }
                    else s.Choices[i] = fill.gameObject.AddComponent<Choice>();
                }
                y += rowH;
                return s;
            }

            public void Choose(int index)
            {
                for (int i = 0; i < rings.Length; i++) { rings[i].enabled = i == index; gaps[i].enabled = i == index; }
            }

            public void SetOpen(bool[] open)
            {
                for (int i = 0; i < locks.Length && i < open.Length; i++)
                {
                    locks[i].gameObject.SetActive(!open[i]);
                    var c = fills[i].color; c.a = open[i] ? 1f : 0.4f; fills[i].color = c;
                }
            }
        }

        // ---- a range slider: a thin gradient track and a round knob in the colour picked
        sealed class Slider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public Func<float, Color> ColorAt;
            public Action<float, bool> Changed;
            public bool Rainbow;
            RectTransform track, knob; Image knobFill; RawImage gradient; CanvasGroup group;
            float value; bool dragging, ready;
            const float Pad = 26;

            public static Slider Make(RectTransform p, float x, float y, float width, string[] stops, Func<float, Color> colorAt)
            {
                var root = UiKit.Panel(p, "Slider", Color.clear, TL, TL, new Vector2(x, -y), new Vector2(width, 64), false);
                root.rectTransform.pivot = TL;
                var s = root.gameObject.AddComponent<Slider>();
                s.group = root.gameObject.AddComponent<CanvasGroup>();
                s.ColorAt = colorAt;
                var capsule = UiKit.Panel(root.transform, "Track", Color.white, new Vector2(0, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(-2 * Pad, 18), false);
                capsule.rectTransform.pivot = new Vector2(0.5f, 0.5f); capsule.rectTransform.offsetMin = new Vector2(Pad, -9); capsule.rectTransform.offsetMax = new Vector2(-Pad, 9);
                capsule.sprite = UiKit.Circle; capsule.type = Image.Type.Sliced; capsule.pixelsPerUnitMultiplier = 1f;
                capsule.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var tex = new GameObject("Gradient").AddComponent<RawImage>();
                tex.transform.SetParent(capsule.transform, false);
                tex.rectTransform.anchorMin = Vector2.zero; tex.rectTransform.anchorMax = Vector2.one; tex.rectTransform.offsetMin = tex.rectTransform.offsetMax = Vector2.zero;
                tex.raycastTarget = false;
                s.gradient = tex; s.track = capsule.rectTransform;
                var k = UiKit.Panel(root.transform, "Knob", Paper, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(46, 46), false);
                k.sprite = UiKit.Circle; k.rectTransform.pivot = new Vector2(0.5f, 0.5f); k.raycastTarget = false;
                var rim = UiKit.Panel(k.transform, "Rim", Line, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46), false);
                rim.sprite = UiKit.Circle; rim.rectTransform.pivot = new Vector2(0.5f, 0.5f); rim.raycastTarget = false; rim.transform.SetAsFirstSibling();
                rim.rectTransform.sizeDelta = new Vector2(50, 50);
                var kf = UiKit.Panel(k.transform, "Colour", Color.gray, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(32, 32), false);
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

            public void SetDimmed(bool dim) => group.alpha = dim ? 0.45f : 1f;

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
                if (w <= 0) w = Inner - 2 * Pad;
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

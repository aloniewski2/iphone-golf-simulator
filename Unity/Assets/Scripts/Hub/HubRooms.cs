using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GolfArcade.Hub
{
    /// The rooms behind the Plaza's doors, in the plaza's art language (rounded cream and royal blue, lime trim, warm light):
    /// the Locker Room (lockers, clothes rails, a lit vanity mirror), the Clubhouse (lounge, desk, trophies, mailbox, invite board),
    /// the PLAY Hall (one archway per sport), and the Tennis and Golf rooms with their queue bays. Each room is cut away on the
    /// south side (a low front wall) so the follow camera can sit behind it; a ceiling with cove lights closes the top.
    public static class HubRooms
    {
        const float H = 4.6f;
        static Material Lit(Color c, float s = .3f) => HubArt.Lit(c, s);
        static Material Glow(Color c) => HubArt.Glow(c);
        static Color C(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out var c); return c; }
        static GameObject Box(string n, Transform p, Vector3 at, Vector3 size, Material m, float r = .06f, Quaternion? rot = null, bool collide = false)
        {
            var go = new GameObject(n); go.transform.SetParent(p, false); go.transform.SetPositionAndRotation(at, rot ?? Quaternion.identity);
            var mesh = HubShapes.RoundedBox(size, r, 3); go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collide) go.AddComponent<BoxCollider>().size = size;
            return go;
        }
        static GameObject Mesh(string n, Transform p, Mesh mesh, Material m, Vector3 at, Quaternion? rot = null, Vector3? scale = null)
        {
            var go = new GameObject(n); go.transform.SetParent(p, false); go.transform.SetPositionAndRotation(at, rot ?? Quaternion.identity); if (scale.HasValue) go.transform.localScale = scale.Value;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = m; return go;
        }
        static Mesh Lathe(string key, Vector2[] profile, float fillet, int seg = 32) { var m = HubShapes.Lathe(key, HubShapes.Fillet(profile, fillet), seg); HubShapes.FixWinding(m); return m; }
        static void AddLight(Transform p, Vector3 at, Color c, float intensity, float range)
        {
            var l = new GameObject("Light").AddComponent<Light>(); l.transform.SetParent(p, false); l.transform.position = at;
            l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.None;
        }
        /// A flat sign: a rounded royal plate with an optional icon and white text, facing `facing` (toward the reader).
        static void Plate(Transform p, string text, string icon, Vector3 at, Vector3 facing, float height, Color? plate = null, float width = 0)
        {
            var rot = Quaternion.LookRotation(-facing, Vector3.up);
            float w = width > 0 ? width : Mathf.Max(1.2f, text.Length * height * .62f + (icon != null ? height * 1.6f : 0) + height * 1.2f);
            Box("Sign plate", p, at + facing * .02f, new Vector3(w, height * 1.6f, .08f), Lit(plate ?? HubArt.Royal, .4f), .05f, rot);
            var go = new GameObject("Sign " + text); go.transform.SetParent(p, false); go.transform.SetPositionAndRotation(at + facing * .075f, rot);
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(w * 100, height * 1.6f * 100); rt.localScale = Vector3.one / 100;
            float x = icon != null ? height * .55f * 100 : 0;
            if (icon != null)
            {
                var img = new GameObject("Icon").AddComponent<RawImage>(); img.transform.SetParent(go.transform, false); img.texture = HubIcons.Get(icon); img.raycastTarget = false;
                var irt = img.rectTransform; irt.sizeDelta = new Vector2(height * 1.2f * 100, height * 1.2f * 100); irt.anchoredPosition = new Vector2(-(text.Length * height * .62f * 100) / 2 - height * 30, 0);
            }
            var t = new GameObject("Text").AddComponent<Text>(); t.transform.SetParent(go.transform, false);
            t.font = HubKit.Font; t.text = text; t.fontSize = Mathf.RoundToInt(height * 100 * 1.05f); t.color = Color.white; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero; trt.anchoredPosition = new Vector2(x, 0);
        }

        // ================================================================== shell
        public static void Room(Transform root, HubLayout.Place place)
        {
            var o = place.origin; float w = place.size.x, d = place.size.y;
            Color wainscot, floorA, floorB; string floorKind;
            switch (place.id)
            {
                case HubLayout.Locker: wainscot = HubArt.Royal; floorA = C("E2C49C"); floorB = C("D4B288"); floorKind = "planks"; break;
                case HubLayout.Clubhouse: wainscot = C("3E6A55"); floorA = C("CFA57A"); floorB = C("C09669"); floorKind = "planks"; break;
                case HubLayout.TennisRoom: wainscot = HubArt.Royal; floorA = C("4C78C8"); floorB = C("4C78C8"); floorKind = "court"; break;
                case HubLayout.GolfRoom: wainscot = C("3F7A48"); floorA = C("79B35A"); floorB = C("6EA650"); floorKind = "turf"; break;
                default: wainscot = HubArt.Royal; floorA = C("F2E3D6"); floorB = C("E9D6C6"); floorKind = "stone"; break;
            }
            // floor (painted), with collider
            Mesh(place.title + " floor", root, HubShapes.Quad(new Vector2(w + .6f, d + .6f)), FloorMaterial(floorKind, floorA, floorB, w + .6f, d + .6f), o, Quaternion.Euler(90, 0, 0));
            HubKit.Blocker(place.title + " floor collider", root, o + new Vector3(0, -.1f, 0), new Vector3(w + .6f, .2f, d + .6f));
            // walls: cream upper, coloured wainscot with a rounded rail, a royal cornice with a lime lip
            var cream = Lit(HubArt.Cream, .3f);
            // one wall: cream, with a coloured wainscot + rail along its foot and a royal cornice at the top (all on its inner face)
            void Wall(string n, Vector3 innerFaceCentre, float length, Vector3 inward)
            {
                var along = Vector3.Cross(Vector3.up, inward); var rot = Quaternion.LookRotation(inward, Vector3.up);
                Box(n, root, innerFaceCentre - inward * .15f + Vector3.up * H / 2, new Vector3(length + .6f, H, .3f), cream, .05f, rot, true);
                Box(n + " wainscot", root, innerFaceCentre + inward * .06f + Vector3.up * .575f, new Vector3(length, 1.15f, .12f), Lit(wainscot, .35f), .04f, rot);
                Box(n + " rail", root, innerFaceCentre + inward * .1f + Vector3.up * 1.18f, new Vector3(length, .1f, .2f), Lit(HubArt.Cream, .4f), .04f, rot);
                Box(n + " cornice", root, innerFaceCentre + inward * .06f + Vector3.up * (H - .35f), new Vector3(length, .55f, .12f), Lit(HubArt.Royal, .4f), .05f, rot);
            }
            Wall(place.title + " north wall", o + new Vector3(0, 0, d / 2), w, Vector3.back);
            Wall(place.title + " west wall", o + new Vector3(-w / 2, 0, 0), d, Vector3.right);
            Wall(place.title + " east wall", o + new Vector3(w / 2, 0, 0), d, Vector3.left);
            // the cut-away front: a low wall either side of the exit, an invisible full-height collider
            foreach (int s in new[] { -1, 1 })
            {
                Box(place.title + " front", root, o + new Vector3(s * (w / 4 + .7f), .4f, -d / 2 - .15f), new Vector3(w / 2 - 1.4f, .8f, .3f), cream, .1f);
                Box(place.title + " front cap", root, o + new Vector3(s * (w / 4 + .7f), .84f, -d / 2 - .15f), new Vector3(w / 2 - 1.3f, .1f, .42f), Lit(HubArt.CreamShade, .35f), .04f);
            }
            HubKit.Blocker(place.title + " south wall", root, o + new Vector3(0, H / 2, -d / 2 - .15f), new Vector3(w + .6f, H, .3f));
            // ceiling with a glowing cove all round, and warm lights
            Box(place.title + " ceiling", root, o + new Vector3(0, H + .1f, 1.2f), new Vector3(w + .6f, .2f, d - 1.8f), Lit(C("F4E7DA"), .2f), .04f);
            Box(place.title + " cove", root, o + new Vector3(0, H - .02f, 1.2f), new Vector3(w - 1.4f, .04f, d - 3.6f), Glow(new Color(1.6f, 1.25f, .85f)), .02f);
            AddLight(root, o + new Vector3(0, H - .6f, 2.2f), C("FFE4C4"), 2.2f, Mathf.Max(w, d) * 1.05f);
            AddLight(root, o + new Vector3(0, H - .8f, -2.4f), C("F2E8FF"), 1.4f, Mathf.Max(w, d) * .9f);
            // the room's name on the north wall
            Plate(root, place.title.ToUpperInvariant(), RoomIcon(place.id), o + new Vector3(0, H - 1.25f, d / 2 - .02f), Vector3.back, .36f);
            switch (place.id)
            {
                case HubLayout.Locker: LockerRoom(root, o, w, d); break;
                case HubLayout.Clubhouse: Clubhouse(root, o, w, d); break;
                case HubLayout.PlayHall: PlayHall(root, o, w, d); break;
                case HubLayout.TennisRoom: SportRoom(root, o, w, d, false); break;
                case HubLayout.GolfRoom: SportRoom(root, o, w, d, true); break;
            }
        }
        static string RoomIcon(string id) => id switch { HubLayout.Locker => "hanger", HubLayout.Clubhouse => "crown", HubLayout.TennisRoom => "racket", HubLayout.GolfRoom => "flag", _ => "crest" };

        static Material FloorMaterial(string kind, Color a, Color b, float w, float d)
        {
            const int px = 512; var tex = new Texture2D(px, px, TextureFormat.RGBA32, true) { name = "room floor " + kind, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
            var c = new Color[px * px];
            for (int y = 0; y < px; y++) for (int x = 0; x < px; x++)
            {
                float u = x / (px - 1f) * w, v = y / (px - 1f) * d;   // metres across the floor
                Color col = a;
                if (kind == "planks")
                {
                    int row = Mathf.FloorToInt(v / .32f); float off = (row * 1.37f) % 2.4f; float along = (u + off) % 2.4f;
                    col = (row % 2 == 0) ? a : Color.Lerp(a, b, .6f);
                    col = Color.Lerp(col, b, Mathf.PerlinNoise(u * 3 + row * 7, v * 9) * .35f);
                    if (v % .32f < .012f || along < .012f) col *= .8f;
                }
                else if (kind == "court")
                {
                    float cx = u - w / 2, cz = v - d / 2;
                    bool line = Mathf.Abs(Mathf.Abs(cx) - 4.2f) < .05f && Mathf.Abs(cz) < 6.2f || Mathf.Abs(Mathf.Abs(cz) - 6.2f) < .05f && Mathf.Abs(cx) < 4.2f || Mathf.Abs(cx) < .03f && Mathf.Abs(cz) < 3.2f || Mathf.Abs(Mathf.Abs(cz) - 3.2f) < .05f && Mathf.Abs(cx) < 4.2f;
                    col = Mathf.Abs(cx) > 4.2f || Mathf.Abs(cz) > 6.2f ? C("3E9A5E") : a;
                    if (line) col = new Color(.96f, .96f, .94f);
                    col *= .97f + Mathf.PerlinNoise(u * 6, v * 6) * .06f;
                }
                else if (kind == "turf")
                {
                    col = Color.Lerp(a, b, (Mathf.FloorToInt((u + v) / 1.2f) % 2 == 0) ? 0 : 1);
                    col *= .94f + Mathf.PerlinNoise(u * 14, v * 14) * .12f;
                }
                else
                {
                    float cx = u - w / 2, cz = v - d / 2; float r = Mathf.Sqrt(cx * cx + cz * cz);
                    col = Color.Lerp(a, b, Mathf.PerlinNoise(u * .8f, v * .8f) * .5f);
                    if (Mathf.Abs(r - 3.2f) < .18f) col = HubArt.Royal; else if (r < 3.0f && r > 2.6f) col = Color.Lerp(col, HubArt.Gold, .7f);
                    if ((Mathf.Abs(u % 1.5f) < .015f || Mathf.Abs(v % 1.5f) < .015f) && r > 3.4f) col *= .93f;
                }
                col.a = 1; c[y * px + x] = col;
            }
            tex.SetPixels(c); tex.Apply(true, true);
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "room floor " + kind };
            m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", Color.white); m.SetFloat("_Smoothness", kind == "stone" ? .55f : kind == "court" ? .35f : .25f);
            return m;
        }

        // ================================================================== rooms
        static void LockerRoom(Transform r, Vector3 o, float w, float d)
        {
            // a bank of lockers along the back of each side wall
            var palette = new[] { HubArt.Royal, C("4A70D8"), HubArt.Royal };
            foreach (int s in new[] { -1, 1 })
                for (int i = 0; i < 4; i++)
                {
                    var at = o + new Vector3(s * (w / 2 - .32f), 1.05f, d / 2 - .55f - i * .62f);
                    Box("Locker", r, at, new Vector3(.6f, 2.1f, .58f), Lit(palette[i % 3], .45f), .05f, null, true);
                    Box("Locker vent", r, at + new Vector3(-s * .31f, .6f, 0), new Vector3(.02f, .22f, .36f), Lit(C("2C3F86"), .3f), .01f);
                    Box("Locker handle", r, at + new Vector3(-s * .31f, 0, .18f), new Vector3(.04f, .18f, .04f), Lit(C("E8E2D8"), .7f), .015f);
                }
            // benches down the middle
            foreach (float z in new[] { .2f, -2.4f })
            {
                Box("Bench top", r, o + new Vector3(0, .46f, z), new Vector3(2.8f, .1f, .5f), Lit(C("C9925E"), .35f), .04f, null, true);
                foreach (int s in new[] { -1, 1 }) Box("Bench leg", r, o + new Vector3(s * 1.15f, .21f, z), new Vector3(.12f, .42f, .42f), Lit(HubArt.Cream, .3f), .03f);
            }
            // the vanity mirror on the north wall: a lit frame of bulbs
            var m = o + new Vector3(0, 1.75f, d / 2 - .06f);
            Box("Mirror frame", r, m, new Vector3(3.3f, 2.5f, .1f), Lit(HubArt.Cream, .4f), .08f);
            Box("Mirror", r, m + Vector3.back * .055f, new Vector3(3.0f, 2.2f, .02f), Lit(C("BFD7EA"), .95f), .02f);
            for (int i = 0; i < 7; i++)
            {
                float x = -1.5f + i * .5f;
                Mesh("Bulb", r, HubKit.Ball(1), Glow(HubArt.LampGlow * .8f), m + new Vector3(x, 1.33f, -.08f), null, Vector3.one * .1f);
            }
            AddLight(r, m + new Vector3(0, 0, -1.2f), C("FFE3BE"), 1.4f, 4.5f);
            Plant(r, o + new Vector3(-w / 2 + .7f, 0, -d / 2 + 1.3f), 1f, 7); Plant(r, o + new Vector3(w / 2 - .7f, 0, -d / 2 + 1.3f), .9f, 8);
        }

        static void Clubhouse(Transform r, Vector3 o, float w, float d)
        {
            // rug, sofas and a coffee table in the middle
            Mesh("Rug", r, HubShapes.Disc(64), Lit(C("C7584B"), .1f), o + new Vector3(0, .012f, .2f), null, new Vector3(2.5f, 1, 1.9f));
            Mesh("Rug ring", r, HubKit.Ring(.86f, .93f, 64), Lit(C("F2D3A2"), .1f), o + new Vector3(0, .016f, .2f), null, new Vector3(2.5f, 1, 1.9f));
            foreach (int s in new[] { -1, 1 })
            {
                var at = o + new Vector3(s * 1.9f, 0, .2f); var rot = Quaternion.Euler(0, s * 90, 0);
                Box("Sofa seat", r, at + Vector3.up * .3f, new Vector3(2.0f, .4f, .9f), Lit(C("3F62CC"), .25f), .14f, rot, true);
                Box("Sofa back", r, at + Vector3.up * .72f + rot * Vector3.back * .36f, new Vector3(2.0f, .55f, .22f), Lit(C("3A5ABF"), .25f), .1f, rot);
                foreach (int a in new[] { -1, 1 }) Box("Sofa arm", r, at + Vector3.up * .5f + rot * Vector3.right * a * .95f, new Vector3(.22f, .55f, .9f), Lit(C("3A5ABF"), .25f), .1f, rot);
            }
            Mesh("Coffee table", r, Lathe("coffee table", new[] { new Vector2(0, 0), new Vector2(.12f, 0), new Vector2(.1f, .38f), new Vector2(.6f, .38f), new Vector2(.6f, .44f), new Vector2(0, .44f) }, .03f), Lit(C("C9925E"), .5f), o + new Vector3(0, 0, .2f));
            Plant(r, o + new Vector3(-w / 2 + .7f, 0, -d / 2 + 1.3f), 1.05f, 17); Plant(r, o + new Vector3(w / 2 - .7f, 0, -d / 2 + 1.3f), .95f, 18);
            Plant(r, o + new Vector3(-1.9f, 0, d / 2 - .8f), .8f, 19); Plant(r, o + new Vector3(1.9f, 0, d / 2 - .8f), .8f, 20);
            // pendant lamps
            foreach (float x in new[] { -1.2f, 1.2f })
            {
                var at = o + new Vector3(x, H - 1.1f, .2f);
                Box("Pendant cord", r, at + Vector3.up * .6f, new Vector3(.02f, 1.1f, .02f), Lit(C("3A3036"), .3f), .005f);
                Mesh("Pendant shade", r, Lathe("pendant", new[] { new Vector2(0, .25f), new Vector2(.08f, .25f), new Vector2(.36f, 0), new Vector2(.34f, -.02f), new Vector2(0, -.02f) }, .02f), Lit(HubArt.Lime, .4f), at);
                Mesh("Pendant bulb", r, HubKit.Ball(1), Glow(HubArt.LampGlow), at + Vector3.down * .05f, null, Vector3.one * .12f);
            }
        }

        static void PlayHall(Transform r, Vector3 o, float w, float d)
        {
            // three archways on the north wall: TENNIS (blue), coming soon (grey), GOLF (green)
            foreach (var door in HubLayout.Doors)
            {
                if (door.place != HubLayout.PlayHall || Vector3.Dot(door.inward, Vector3.forward) < .7f) continue;
                bool golf = door.id.Contains("golf"), soon = door.locked;
                var tone = soon ? C("9A97A8") : golf ? C("3F8A4C") : HubArt.Royal;
                var at = door.position + door.inward * .3f;
                foreach (int k in new[] { -1, 1 }) Box("Arch pillar", r, at + new Vector3(k * (door.width / 2 + .22f), 1.6f, 0), new Vector3(.44f, 3.2f, .6f), Lit(tone, .4f), .1f);
                Box("Arch header", r, at + Vector3.up * 3.42f, new Vector3(door.width + 1.0f, .5f, .7f), Lit(tone, .4f), .16f);
                Box("Arch opening", r, at + Vector3.up * 1.6f + Vector3.forward * .05f, new Vector3(door.width, 3.2f, .04f), Glow(soon ? new Color(.55f, .55f, .6f) : new Color(1.9f, 1.4f, .78f)), .02f);
                Plate(r, door.label.ToUpperInvariant(), soon ? null : golf ? "flag" : "racket", at + Vector3.up * 4.05f + Vector3.back * .15f, Vector3.back, .24f, tone);
                if (!soon) Mesh("Arch runner", r, HubShapes.Disc(32), Lit(tone, .3f), door.position + Vector3.up * .012f + Vector3.back * .9f, null, new Vector3(door.width * .45f, 1, 1.4f));
            }
            // a directory board and greenery
            Plate(r, "TENNIS  ◂   ▸  GOLF", null, o + new Vector3(-w / 2 + .05f, 2.0f, -1.5f), Vector3.right, .28f);
            Plant(r, o + new Vector3(-w / 2 + .8f, 0, d / 2 - 1.2f), 1.1f, 27); Plant(r, o + new Vector3(w / 2 - .8f, 0, d / 2 - 1.2f), 1.1f, 28);
            Plant(r, o + new Vector3(-w / 2 + .8f, 0, -d / 2 + 1.4f), .9f, 29); Plant(r, o + new Vector3(w / 2 - .8f, 0, -d / 2 + 1.4f), .9f, 30);
        }

        static void SportRoom(Transform r, Vector3 o, float w, float d, bool golf)
        {
            // a big window onto the sport's view
            var win = o + new Vector3(0, 2.3f, d / 2 - .06f);
            Box("Window frame", r, win, new Vector3(6.2f, 2.6f, .14f), Lit(HubArt.Cream, .4f), .08f);
            var view = Mesh("Window view", r, HubShapes.Quad(new Vector2(5.8f, 2.2f)), ViewMaterial(golf), win + Vector3.back * .08f, Quaternion.identity);
            view.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            Box("Window mullion", r, win + Vector3.back * .1f, new Vector3(.08f, 2.2f, .06f), Lit(HubArt.Cream, .4f), .02f);
            Plant(r, o + new Vector3(-w / 2 + .8f, 0, -d / 2 + 1.4f), 1f, golf ? 41 : 31); Plant(r, o + new Vector3(w / 2 - .8f, 0, -d / 2 + 1.4f), 1f, golf ? 42 : 32);
        }

        static Material ViewMaterial(bool golf)
        {
            const int W = 512, Hh = 192; var tex = new Texture2D(W, Hh, TextureFormat.RGBA32, true) { name = golf ? "fairway view" : "court view", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[W * Hh];
            for (int y = 0; y < Hh; y++) for (int x = 0; x < W; x++)
            {
                float u = x / (W - 1f), v = y / (Hh - 1f);
                var sky = HubArt.SkyColor(new Vector3((u - .5f) * .8f, Mathf.Lerp(-.02f, .55f, (v - .45f) / .55f), 1).normalized);
                Color c = sky;
                float horizon = .45f;
                if (v < horizon)
                {
                    float depth = (horizon - v) / horizon;   // 0 at the horizon, 1 at the bottom
                    c = Color.Lerp(HubArt.SeaFar, HubArt.SeaNear, depth * 2);
                    if (golf)
                    {
                        float hill = .42f - .06f * Mathf.Sin(u * 6.5f) - .03f * Mathf.Sin(u * 17);
                        if (v < hill) c = Color.Lerp(C("8CC063"), C("6AA34B"), depth);
                        float fair = Mathf.Abs(u - .5f - (horizon - v) * .4f);
                        if (v < hill && fair < .12f + depth * .25f) c = Color.Lerp(c, C("A6D477"), .7f);
                        if (Mathf.Abs(u - .62f) < .003f && v > .3f && v < .38f) c = Color.white;
                        if (u > .62f && u < .66f && v > .35f && v < .38f) c = C("E8483C");
                    }
                    else
                    {
                        float half = .18f + depth * .32f, cx = u - .5f;
                        if (v < .38f)
                        {
                            c = Mathf.Abs(cx) < half ? C("4C78C8") : C("3E9A5E");
                            float dd = (.38f - v) / .38f;
                            bool line = Mathf.Abs(Mathf.Abs(cx) - half) < .004f + dd * .004f || Mathf.Abs(v - .3f) < .004f || Mathf.Abs(cx) < .003f && v < .3f;
                            if (line && Mathf.Abs(cx) <= half + .01f) c = Color.white;
                            if (Mathf.Abs(v - .2f) < .025f && Mathf.Abs(cx) < half) c = Color.Lerp(c, new Color(.15f, .15f, .2f), .55f);   // the net
                        }
                    }
                }
                c.a = 1; px[y * W + x] = c;
            }
            tex.SetPixels(px); tex.Apply(true, true);
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = tex.name }; m.SetTexture("_BaseMap", tex); m.SetColor("_BaseColor", new Color(1.1f, 1.1f, 1.1f));
            return m;
        }

        static void Plant(Transform r, Vector3 at, float s, int seed)
        {
            Mesh("Pot", r, Lathe("pot", new[] { new Vector2(0, 0), new Vector2(.3f, 0), new Vector2(.38f, .55f), new Vector2(.42f, .6f), new Vector2(.36f, .6f), new Vector2(0, .58f) }, .03f), Lit(HubArt.Cream, .35f), at, null, Vector3.one * s);
            Mesh("Leaves", r, HubShapes.Crown(seed, 3, .2f), Lit(Color.Lerp(HubArt.Leaf, HubArt.LeafDark, (seed % 5) / 6f), .2f), at + Vector3.up * .95f * s, Quaternion.Euler(0, seed * 41, 0), new Vector3(.45f, .6f, .45f) * s);
        }

        // ================================================================== stations and bays
        public static void Spot(Transform root, HubLayout.Spot s)
        {
            var face = Quaternion.LookRotation(s.facing, Vector3.up);
            var place = HubLayout.PlaceOf(s.place);
            // the spot you stand on: a soft glowing ring
            Mesh("Spot ring " + s.id, root, HubKit.Ring(.52f, .66f, 48), Glow(s.kind == HubLayout.Kind.Bay ? new Color(.7f, 1.1f, 2.0f) : new Color(1.9f, 1.5f, .7f)), s.position + Vector3.up * .014f);
            var wallAt = s.position + s.facing * 1.15f;
            switch (s.id)
            {
                case "rack-shirt": case "rack-shorts": Rail(root, s, s.id == "rack-shirt"); break;
                case "rack-shoes": Shelf(root, s, "shoes"); break;
                case "rack-racket": Shelf(root, s, "racket"); break;
                case "rack-club": Shelf(root, s, "club"); break;
                case "look-mirror": break;   // the vanity mirror is the station
                case "emote-mirror":
                    Box("Dance mirror", root, wallAt + Vector3.up * 1.4f + s.facing * .1f, new Vector3(1.6f, 2.2f, .08f), Lit(C("BFD7EA"), .95f), .04f, face);
                    break;
                case "desk-settings":
                    Box("Desk", root, wallAt + Vector3.up * .5f - s.facing * .1f, new Vector3(1.8f, 1.0f, .7f), Lit(C("C9925E"), .45f), .08f, face, true);
                    Box("Desk top", root, wallAt + Vector3.up * 1.03f - s.facing * .1f, new Vector3(1.95f, .08f, .8f), Lit(HubArt.Cream, .5f), .03f, face);
                    Box("Desk screen", root, wallAt + Vector3.up * 1.45f + s.facing * .1f, new Vector3(.9f, .6f, .06f), Glow(new Color(.45f, .7f, 1.4f)), .03f, face);
                    break;
                case "screen-howto":
                    Box("How-to frame", root, wallAt + Vector3.up * 2.0f + s.facing * .2f, new Vector3(3.0f, 1.7f, .1f), Lit(C("2E3448"), .5f), .06f, face);
                    Box("How-to screen", root, wallAt + Vector3.up * 2.0f + s.facing * .14f, new Vector3(2.8f, 1.5f, .02f), Glow(new Color(.55f, .75f, 1.5f)), .02f, face);
                    break;
                case "shelf-trophies":
                    Box("Trophy shelf", root, wallAt + Vector3.up * 1.0f, new Vector3(1.8f, 2.0f, .5f), Lit(C("C9925E"), .45f), .05f, face, true);
                    for (int i = 0; i < 3; i++)
                        Mesh("Trophy", root, Lathe("trophy", new[] { new Vector2(0, 0), new Vector2(.12f, 0), new Vector2(.1f, .06f), new Vector2(.03f, .1f), new Vector2(.03f, .2f), new Vector2(.15f, .3f), new Vector2(.14f, .42f), new Vector2(0, .4f) }, .015f),
                             Lit(HubArt.Gold, .8f), wallAt + Vector3.up * 2.02f - s.facing * .05f + Vector3.Cross(Vector3.up, s.facing) * (i - 1) * .55f);
                    break;
                case "mailbox-feedback":
                    Mesh("Mailbox", root, Lathe("mailbox", new[] { new Vector2(0, 0), new Vector2(.26f, 0), new Vector2(.26f, 1.05f), new Vector2(.2f, 1.2f), new Vector2(0, 1.24f) }, .05f), Lit(C("D9493D"), .45f), wallAt - s.facing * .2f);
                    Box("Mailbox slot", root, wallAt - s.facing * .45f + Vector3.up * .95f, new Vector3(.24f, .04f, .04f), Lit(C("3A3036"), .3f), .01f, face);
                    break;
                case "board-invite":
                    Box("Invite board", root, wallAt + Vector3.up * 1.7f + s.facing * .1f, new Vector3(2.0f, 1.3f, .08f), Lit(C("C9925E"), .3f), .05f, face);
                    for (int i = 0; i < 4; i++)
                        Box("Photo", root, wallAt + Vector3.up * (1.5f + (i / 2) * .5f) + s.facing * .05f + Vector3.Cross(Vector3.up, s.facing) * ((i % 2) - .5f) * .8f, new Vector3(.55f, .4f, .02f), Lit(new[] { C("FF8FB8"), C("8FC8FF"), C("A6E38F"), C("FFD36B") }[i], .4f), .02f, face);
                    break;
                default:
                    if (s.kind == HubLayout.Kind.Bay) Bay(root, s, place);
                    break;
            }
            if (s.kind == HubLayout.Kind.Station && s.id != "look-mirror")
                Plate(root, s.label.ToUpperInvariant(), null, wallAt + Vector3.up * 2.75f + s.facing * .05f, -s.facing, .2f);
        }

        static void Rail(Transform root, HubLayout.Spot s, bool shirts)
        {
            var at = s.position + s.facing * 1.0f; var side = Vector3.Cross(Vector3.up, s.facing); var face = Quaternion.LookRotation(s.facing);
            Box("Rail bar", root, at + Vector3.up * 1.75f, new Vector3(1.8f, .05f, .05f), Lit(C("D8D2C8"), .8f), .02f, face);
            foreach (int k in new[] { -1, 1 }) Box("Rail post", root, at + side * k * .9f + Vector3.up * .88f, new Vector3(.06f, 1.76f, .06f), Lit(C("D8D2C8"), .8f), .02f, face, true);
            var colours = new[] { C("E8505B"), C("3F62CC"), C("F2F2EE"), C("7BDA4A"), C("FFB13B"), C("A970FF") };
            for (int i = 0; i < 6; i++)
            {
                var hang = at + side * (-.72f + i * .29f) + Vector3.up * (shirts ? 1.3f : 1.42f);
                var rot = Quaternion.LookRotation(-s.facing, Vector3.up) * Quaternion.Euler(0, (i - 2.5f) * 7f, 0);
                Box(shirts ? "Shirt" : "Shorts", root, hang - s.facing * (.04f * (i % 2)), shirts ? new Vector3(.62f, .78f, .06f) : new Vector3(.5f, .52f, .06f), Lit(colours[i], .25f), .08f, rot);
            }
        }

        static void Shelf(Transform root, HubLayout.Spot s, string what)
        {
            var at = s.position + s.facing * 1.05f; var side = Vector3.Cross(Vector3.up, s.facing); var face = Quaternion.LookRotation(s.facing);
            Box("Shelf back", root, at + Vector3.up * 1.1f + s.facing * .2f, new Vector3(1.8f, 2.2f, .08f), Lit(C("C9925E"), .35f), .04f, face, true);
            for (int k = 0; k < 3; k++) Box("Shelf", root, at + Vector3.up * (.5f + k * .6f), new Vector3(1.8f, .06f, .4f), Lit(HubArt.Cream, .4f), .02f, face);
            var colours = new[] { C("E8505B"), C("3F62CC"), C("F2F2EE"), C("7BDA4A"), C("FFB13B"), C("A970FF") };
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < 3; i++)
                {
                    var spot = at + side * ((i - 1) * .55f) + Vector3.up * (.53f + k * .6f);
                    var c = colours[(k * 3 + i) % colours.Length];
                    if (what == "shoes") { Box("Shoe", root, spot + Vector3.up * .07f + side * .07f, new Vector3(.12f, .12f, .3f), Lit(c, .35f), .05f, face); Box("Shoe", root, spot + Vector3.up * .07f - side * .07f, new Vector3(.12f, .12f, .3f), Lit(c, .35f), .05f, face); }
                    else if (what == "racket") { Mesh("Racket head", root, HubKit.Ring(.11f, .15f, 24), Lit(c, .5f), spot + Vector3.up * .34f, Quaternion.FromToRotation(Vector3.up, -s.facing)); Box("Racket handle", root, spot + Vector3.up * .1f, new Vector3(.04f, .2f, .04f), Lit(C("3A3036"), .3f), .015f, face); }
                    else { Box("Club shaft", root, spot + Vector3.up * .3f, new Vector3(.02f, .55f, .02f), Lit(C("D8D2C8"), .8f), .008f, face); Box("Club head", root, spot + Vector3.up * .04f + side * .04f, new Vector3(.14f, .06f, .05f), Lit(c, .6f), .02f, face); }
                }
        }

        static void Bay(Transform root, HubLayout.Spot s, HubLayout.Place place)
        {
            var face = Quaternion.LookRotation(s.facing); var side = Vector3.Cross(Vector3.up, s.facing);
            bool golf = s.place == HubLayout.GolfRoom;
            var tone = golf ? C("3F8A4C") : HubArt.Royal;
            // the bay floor: a rounded mat with a glowing ring that fills while a match loads
            Mesh("Bay mat " + s.id, root, Lathe("bay mat", new[] { new Vector2(1.55f, 0), new Vector2(1.5f, .04f), new Vector2(0, .04f) }, .02f, 48), Lit(Color.Lerp(tone, Color.black, .25f), .4f), s.position + s.facing * .1f);
            Mesh("Bay glow " + s.id, root, HubKit.Ring(1.28f, 1.42f, 64), Glow(golf ? new Color(.8f, 2.0f, .7f) : new Color(.7f, 1.2f, 2.4f)), s.position + s.facing * .1f + Vector3.up * .045f);
            // bench behind you, one seat per player
            float bw = Mathf.Max(1.4f, s.seats * .7f);
            Box("Bench " + s.id, root, s.position - s.facing * .95f + Vector3.up * .45f, new Vector3(bw, .12f, .5f), Lit(C("F2EEE6"), .4f), .05f, face, true);
            foreach (int k in new[] { -1, 1 }) Box("Bench leg", root, s.position - s.facing * .95f + side * k * (bw / 2 - .15f) + Vector3.up * .2f, new Vector3(.14f, .4f, .4f), Lit(tone, .4f), .04f, face);
            // the big screen ahead (phase 3 plays the mode's glitch preview here)
            var scr = s.position + s.facing * 1.75f + Vector3.up * 1.95f;
            Box("Screen frame " + s.id, root, scr, new Vector3(2.9f, 1.75f, .14f), Lit(C("2A2F42"), .55f), .08f, face);
            var screen = Mesh("Screen " + s.id, root, HubShapes.Quad(new Vector2(2.66f, 1.5f)), Glow(golf ? new Color(.35f, .8f, .45f) : new Color(.35f, .55f, 1.1f)), scr - s.facing * .075f, Quaternion.LookRotation(s.facing, Vector3.up));
            screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            HubBayScreen.Attach(screen.GetComponent<MeshRenderer>(), s);
            Box("Screen stand", root, s.position + s.facing * 1.8f + Vector3.up * .55f, new Vector3(.2f, 1.1f, .12f), Lit(C("2A2F42"), .5f), .04f, face);
            Plate(root, s.label.ToUpperInvariant(), golf ? "flag" : "racket", scr + Vector3.up * 1.12f - s.facing * .02f, -s.facing, .22f, tone);
        }

        // ================================================================== exit
        public static void Exit(Transform root, HubLayout.Door d)
        {
            var face = Quaternion.LookRotation(-d.inward, Vector3.up);
            var mat = HubShapes.RoundedBox(new Vector3(d.width + .4f, .03f, 1.2f), .015f, 2);
            Mesh("Exit mat", root, mat, Lit(HubArt.Lime, .3f), d.position + Vector3.up * .015f + d.inward * .1f, face);
            Mesh("Exit glow", root, HubShapes.RoundedBox(new Vector3(d.width, .01f, .25f), .005f, 2), Glow(new Color(2.0f, 1.6f, .8f)), d.position + Vector3.up * .03f + d.inward * .55f, face);
            var go = new GameObject("Exit label"); go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(d.position + Vector3.up * .04f - d.inward * .05f, Quaternion.Euler(90, 0, 0));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(260, 60); rt.localScale = Vector3.one / 100;
            var t = new GameObject("Text").AddComponent<Text>(); t.transform.SetParent(go.transform, false);
            t.font = HubKit.Font; t.text = "▼  " + d.label.ToUpperInvariant(); t.fontSize = 34; t.color = HubArt.RoyalDeep; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        }
    }
}

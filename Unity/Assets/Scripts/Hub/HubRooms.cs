using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace GolfArcade.Hub
{
    /// The places behind the Plaza's doors, in the country club's language (cream, navy, gold, warm light): the Locker Room
    /// (lockers, clothes rails, a lit vanity mirror), the Clubhouse (lounge, desk, trophies, mailbox, invite board) and the PLAY
    /// Hall (one archway per sport) are rooms, cut away on the south side (a low front wall) so the follow camera can sit behind
    /// it, a ceiling with cove lights on top. Tennis and Golf are outdoors (ArtDir/ui/hub-targets-2026-10-12/country-club 06, 07):
    /// a grass court with its net, umpire's chair and windscreens, and a practice range with low stone walls, a putting green
    /// and the course beyond, each under its own sky; the walking space keeps the room's invisible walls, the bays stay put.
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
        /// A flat sign, the club's: a navy board with a thin gold frame and gold lettering (optional icon), facing `facing`
        /// (toward the reader); `height` is the old plate's text size. (The plate colour is always navy now.)
        static void Plate(Transform p, string text, string icon, Vector3 at, Vector3 facing, float height, Color? plate = null, float width = 0)
            => HubClub.SignBoard(p, text, icon, at + facing * .1f, facing, height * .78f, width, 2.05f);

        // ================================================================== shell
        public static void Room(Transform root, HubLayout.Place place)
        {
            if (place.id == HubLayout.TennisRoom || place.id == HubLayout.GolfRoom) { Outdoors(root, place); return; }
            var o = place.origin; float w = place.size.x, d = place.size.y;
            bool hall = place.id == HubLayout.PlayHall;
            // the club's interiors: cream plaster over panelling (navy; walnut in the Clubhouse), a coffered navy ceiling, warm lanterns
            var panel = place.id == HubLayout.Clubhouse ? C("5A3820") : HubClub.Navy;
            // the hall's shell casts no shadow: the sun reaches the court and the range seen through its arches
            var b = new HubClub.Batch { shadows = !hall };
            float fw = w + .6f, fd = d + .6f;
            if (hall) b[HubClub.LitTex(HubClub.MarbleTex, Color.white, .5f, 1.8f)].Quad(o + new Vector3(-fw / 2, 0, -fd / 2), o + new Vector3(-fw / 2, 0, fd / 2), o + new Vector3(fw / 2, 0, fd / 2), o + new Vector3(fw / 2, 0, -fd / 2), Vector3.up, new Vector2(-fw / 2, -fd / 2));
            else
            {
                bool lounge = place.id == HubLayout.Clubhouse;
                Mesh(place.title + " floor", root, HubShapes.Quad(new Vector2(fw, fd)), FloorMaterial("planks", lounge ? C("8E5F3A") : C("C08A57"), lounge ? C("74492B") : C("A87443"), fw, fd), o, Quaternion.Euler(90, 0, 0));
            }
            HubKit.Blocker(place.title + " floor collider", root, o + new Vector3(0, -.1f, 0), new Vector3(fw, .2f, fd));
            var plaster = HubClub.LitTex(HubClub.PlasterTex, C("F1E6CF"), .2f, 2f);
            // each wall keeps its old collider; the hall's north wall is drawn with its three arches (PlayHall)
            void Wall(string n, Vector3 innerFaceCentre, float length, Vector3 inward, bool draw = true)
            {
                HubKit.Blocker(n, root, innerFaceCentre - inward * .15f + Vector3.up * H / 2, new Vector3(length + .6f, H, .3f), Quaternion.LookRotation(inward, Vector3.up));
                if (!draw) return;
                var f = new HubClub.Frame(innerFaceCentre, inward);
                b[plaster].Box(f, new Vector3(0, H / 2, -.15f), new Vector3(length + .6f, H, .3f));
                HubClub.Panelling(b, f, -length / 2, length / 2, panel);
                HubClub.Crown(b, f, -length / 2, length / 2, H);
            }
            Wall(place.title + " north wall", o + new Vector3(0, 0, d / 2), w, Vector3.back, !hall);
            Wall(place.title + " west wall", o + new Vector3(-w / 2, 0, 0), d, Vector3.right);
            Wall(place.title + " east wall", o + new Vector3(w / 2, 0, 0), d, Vector3.left);
            // the cut-away front: a low panelled wall either side of the exit, an invisible full-height collider
            foreach (int s in new[] { -1, 1 })
            {
                var f = new HubClub.Frame(o + new Vector3(s * (w / 4 + .7f), 0, -d / 2), Vector3.forward);
                b[plaster].Box(f, new Vector3(0, .4f, -.15f), new Vector3(w / 2 - 1.4f, .8f, .3f));
                b[HubClub.Lit(panel, .42f)].Box(f, new Vector3(0, .34f, .01f), new Vector3(w / 2 - 1.42f, .62f, .04f));
                b[HubClub.Lit(HubClub.StoneLight, .35f)].Box(f, new Vector3(0, .84f, -.15f), new Vector3(w / 2 - 1.3f, .1f, .42f));
            }
            HubKit.Blocker(place.title + " south wall", root, o + new Vector3(0, H / 2, -d / 2 - .15f), new Vector3(w + .6f, H, .3f));
            HubClub.Coffers(b, o + new Vector3(0, H, 1.2f), w + .6f, d - 1.8f);
            AddLight(root, o + new Vector3(0, H - .6f, 2.2f), C("FFD9A8"), 2.0f, Mathf.Max(w, d) * 1.05f);
            AddLight(root, o + new Vector3(0, H - .8f, -2.4f), C("FFE2BC"), 1.3f, Mathf.Max(w, d) * .9f);
            b.Flush(root, place.title + " shell");
            // the room's name (the hall's goes on its west wall: its north wall is the three arches)
            if (hall) Plate(root, place.title.ToUpperInvariant(), RoomIcon(place.id), o + new Vector3(-w / 2 + .05f, 3.25f, -1.5f), Vector3.right, .3f);
            else Plate(root, place.title.ToUpperInvariant(), RoomIcon(place.id), o + new Vector3(0, H - 1.0f, d / 2 - .02f), Vector3.back, .36f);
            switch (place.id)
            {
                case HubLayout.Locker: LockerRoom(root, o, w, d); break;
                case HubLayout.Clubhouse: Clubhouse(root, o, w, d); break;
                case HubLayout.PlayHall: PlayHall(root, o, w, d, plaster); break;
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
            var b = new HubClub.Batch();
            // a bank of wooden lockers along the back of each side wall (the same boxes and colliders as before)
            var woods = new[] { C("8A5532"), C("7A4A2A"), C("8A5532") };
            foreach (int s in new[] { -1, 1 })
                for (int i = 0; i < 4; i++)
                {
                    var at = o + new Vector3(s * (w / 2 - .32f), 1.05f, d / 2 - .55f - i * .62f);
                    Box("Locker", r, at, new Vector3(.6f, 2.1f, .58f), Lit(woods[i % 3], .4f), .04f, null, true);
                    Box("Locker vent", r, at + new Vector3(-s * .31f, .6f, 0), new Vector3(.02f, .22f, .36f), Lit(C("3E2414"), .3f), .01f);
                    Box("Locker handle", r, at + new Vector3(-s * .31f, 0, .18f), new Vector3(.04f, .18f, .04f), Lit(HubClub.Brass, .8f), .015f);
                    Box("Locker plate", r, at + new Vector3(-s * .305f, .82f, 0), new Vector3(.02f, .07f, .16f), Lit(HubClub.Brass, .8f), .01f);
                }
            // wooden benches down the middle on a navy runner
            foreach (float z in new[] { .2f, -2.4f })
            {
                Box("Bench top", r, o + new Vector3(0, .46f, z), new Vector3(2.8f, .1f, .5f), Lit(C("9A6238"), .4f), .03f, null, true);
                foreach (int s in new[] { -1, 1 }) Box("Bench leg", r, o + new Vector3(s * 1.15f, .21f, z), new Vector3(.12f, .42f, .42f), Lit(HubClub.Navy, .45f), .03f);
            }
            HubClub.Rug(b, o + new Vector3(0, 0, -1.1f), Vector3.forward, 5.8f, 1.7f, HubClub.RugTex("locker runner", HubClub.Navy, C("15204A"), HubClub.Gold));
            // the vanity mirror on the north wall: a gold frame lit by a row of bulbs
            var m = o + new Vector3(0, 1.75f, d / 2 - .06f);
            Box("Mirror frame", r, m, new Vector3(3.3f, 2.5f, .1f), Lit(HubClub.Gold, .7f), .06f);
            Box("Mirror", r, m + Vector3.back * .055f, new Vector3(3.0f, 2.2f, .02f), Lit(C("BFD7EA"), .95f), .02f);
            for (int i = 0; i < 7; i++) Mesh("Bulb", r, HubKit.Ball(1), Glow(HubArt.LampGlow * .8f), m + new Vector3(-1.5f + i * .5f, 1.33f, -.08f), null, Vector3.one * .1f);
            AddLight(r, m + new Vector3(0, 0, -1.2f), C("FFE3BE"), 1.4f, 4.5f);
            foreach (int s in new[] { -1, 1 }) HubClub.HangingLantern(b, o + new Vector3(s * 1.9f, H - .05f, .6f), .65f);
            Topiary(r, b, o + new Vector3(-w / 2 + .7f, 0, -d / 2 + 1.3f), .95f); Topiary(r, b, o + new Vector3(w / 2 - .7f, 0, -d / 2 + 1.3f), .9f);
            b.Flush(r, "Locker dressing");
        }

        static void Clubhouse(Transform r, Vector3 o, float w, float d)
        {
            var b = new HubClub.Batch();
            // the lounge: an old rug, two leather sofas and a walnut table (same places and colliders as before)
            HubClub.Rug(b, o + new Vector3(0, 0, .2f), Vector3.right, 4.6f, 3.1f, HubClub.RugTex("lounge", C("8E2F2A"), HubClub.Navy, C("D9B25A")));
            foreach (int s in new[] { -1, 1 })
            {
                var at = o + new Vector3(s * 1.9f, 0, .2f); var rot = Quaternion.Euler(0, s * 90, 0);
                Box("Sofa seat", r, at + Vector3.up * .3f, new Vector3(2.0f, .4f, .9f), Lit(C("6B3A22"), .45f), .12f, rot, true);
                Box("Sofa back", r, at + Vector3.up * .72f + rot * Vector3.back * .36f, new Vector3(2.0f, .55f, .22f), Lit(C("5A2F1B"), .45f), .1f, rot);
                foreach (int a in new[] { -1, 1 }) Box("Sofa arm", r, at + Vector3.up * .5f + rot * Vector3.right * a * .95f, new Vector3(.22f, .55f, .9f), Lit(C("5A2F1B"), .45f), .1f, rot);
            }
            Mesh("Coffee table", r, Lathe("coffee table", new[] { new Vector2(0, 0), new Vector2(.12f, 0), new Vector2(.1f, .38f), new Vector2(.6f, .38f), new Vector2(.6f, .44f), new Vector2(0, .44f) }, .03f), Lit(C("5A3820"), .55f), o + new Vector3(0, 0, .2f));
            HubClub.Trophy(b, o + new Vector3(0, .5f, .2f), .45f);
            Topiary(r, b, o + new Vector3(-w / 2 + .7f, 0, -d / 2 + 1.3f), 1.0f); Topiary(r, b, o + new Vector3(w / 2 - .7f, 0, -d / 2 + 1.3f), .95f);
            Topiary(r, b, o + new Vector3(-1.9f, 0, d / 2 - .8f), .8f); Topiary(r, b, o + new Vector3(1.9f, 0, d / 2 - .8f), .8f);
            // brass lanterns where the pendants hung, the club's two paintings on the side walls
            foreach (float x in new[] { -1.2f, 1.2f }) HubClub.HangingLantern(b, o + new Vector3(x, H - .05f, .2f), 1.05f);
            HubClub.Picture(b, new HubClub.Frame(o + new Vector3(-w / 2 + .02f, 1.75f, .3f), Vector3.right), 1.0f, 1.3f, HubClub.PaintingTex(false));
            HubClub.Picture(b, new HubClub.Frame(o + new Vector3(w / 2 - .02f, 1.75f, .3f), Vector3.left), 1.0f, 1.3f, HubClub.PaintingTex(true));
            b.Flush(r, "Clubhouse dressing");
        }

        /// The PLAY Hall (ArtDir/ui/hub-targets-2026-10-12/country-club/08): checkered marble with a brass compass in the floor
        /// circle, three round arches in the north wall (TENNIS onto a sunny grass court, GOLF onto the range, a closed wooden
        /// "Coming soon" door between them), navy-and-gold signs over them, topiaries, trophy cabinets, paintings, lanterns, a rug.
        static void PlayHall(Transform r, Vector3 o, float w, float d, Material plaster)
        {
            var b = new HubClub.Batch(); var shell = new HubClub.Batch { shadows = false };
            var north = new HubClub.Frame(o + new Vector3(0, 0, d / 2), Vector3.back);   // x = world x, O into the hall
            const float hw = 1.2f, spring = 2.4f, rise = 1.2f, band = .26f;
            var arches = new List<(HubLayout.Door door, float cx)>();
            foreach (var door in HubLayout.Doors)
                if (door.place == HubLayout.PlayHall && Vector3.Dot(door.inward, Vector3.forward) > .7f) arches.Add((door, door.position.x - o.x));
            arches.Sort((p, q) => p.cx.CompareTo(q.cx));
            var openings = new (float, float, float, float)[arches.Count];
            for (int i = 0; i < arches.Count; i++) openings[i] = (arches[i].cx, hw, spring, rise);
            HubClub.ArchedWall(shell, north, -w / 2 - .3f, w / 2 + .3f, H, .5f, plaster, openings);
            // panelling on the piers between the arches, the crown along the top
            float x0 = -w / 2;
            foreach (var (_, cx) in arches) { HubClub.Panelling(shell, north, x0, cx - hw - band, HubClub.Navy); x0 = cx + hw + band; }
            HubClub.Panelling(shell, north, x0, w / 2, HubClub.Navy);
            HubClub.Crown(shell, north, -w / 2, w / 2, H);
            foreach (var (door, cx) in arches)
            {
                HubClub.ArchSurround(shell, north, cx, hw, spring, rise, band, HubClub.Navy);
                string icon = door.locked ? "crest" : door.id.Contains("golf") ? "flag" : "racket";
                HubClub.SignBoardStacked(r, door.label.ToUpperInvariant(), icon, north.P(cx, 4.08f, .12f), north.O, .17f, 1.9f);
                if (door.locked)
                {
                    HubClub.ArchDoors(b, new HubClub.Frame(north.P(cx, 0, -.32f), north.O), hw, spring, rise);
                    HubClub.SignBoard(r, door.label.ToUpperInvariant(), null, north.P(cx, 1.72f, -.2f), north.O, .085f, 0, 2.3f);
                }
            }
            // topiaries in urns either side of each arch
            foreach (float x in new[] { -5.35f, -1.85f, 1.85f, 5.35f }) Topiary(r, b, north.P(x, 0, .55f), .82f, false);
            // the floor circle: a navy disc ringed in gold with a brass compass rose
            Mesh("Compass disc", r, HubShapes.Disc(96), Lit(HubClub.Navy, .55f), o + Vector3.up * .006f, null, new Vector3(2.78f, 1, 2.78f));
            Mesh("Compass rim", r, HubKit.Ring(2.78f, 2.98f, 96), Lit(HubClub.Gold, .75f), o + Vector3.up * .009f);
            Mesh("Compass ring", r, HubKit.Ring(2.2f, 2.27f, 96), Lit(HubClub.Gold, .75f), o + Vector3.up * .009f);
            Mesh("Compass rose", r, HubArt.StarMesh(4, 2.5f, .42f, .03f), Lit(HubClub.Brass, .75f), o + Vector3.up * .008f);
            Mesh("Compass rose (diagonals)", r, HubArt.StarMesh(4, 1.55f, .36f, .025f), Lit(HubClub.Gold, .75f), o + Vector3.up * .009f, Quaternion.Euler(0, 45, 0));
            Mesh("Compass hub", r, HubShapes.Disc(32), Lit(HubClub.Gold, .8f), o + Vector3.up * .072f, null, new Vector3(.16f, 1, .16f));
            // trophy cabinets and paintings on the side walls, the directory board (as before), lanterns, a rug by the door
            HubClub.Cabinet(b, r, new HubClub.Frame(o + new Vector3(-w / 2, 0, 2.3f), Vector3.right), 1.5f, 2.3f, 1);
            HubClub.Cabinet(b, r, new HubClub.Frame(o + new Vector3(w / 2, 0, 2.3f), Vector3.left), 1.5f, 2.3f, 2);
            HubClub.Cabinet(b, r, new HubClub.Frame(o + new Vector3(w / 2, 0, -1.5f), Vector3.left), 1.5f, 2.3f, 3);
            HubClub.Picture(b, new HubClub.Frame(o + new Vector3(-w / 2 + .02f, 1.8f, 4.75f), Vector3.right), 1.0f, 1.35f, HubClub.PaintingTex(false));
            HubClub.Picture(b, new HubClub.Frame(o + new Vector3(w / 2 - .02f, 1.8f, 4.75f), Vector3.left), 1.0f, 1.35f, HubClub.PaintingTex(true));
            Plate(r, "TENNIS  ◂   ▸  GOLF", null, o + new Vector3(-w / 2 + .05f, 2.0f, -1.5f), Vector3.right, .28f);
            foreach (int s in new[] { -1, 1 }) { HubClub.HangingLantern(b, o + new Vector3(s * 1.85f, H - .05f, 5.3f), .5f); HubClub.HangingLantern(b, o + new Vector3(s * 3.7f, H - .05f, .3f), .7f); }
            HubClub.Rug(b, o + new Vector3(0, 0, -5.25f), Vector3.right, 4.6f, 1.35f, HubClub.RugTex("hall", C("B89A6E"), C("7E5E3C"), C("E2CFA4")));
            Topiary(r, b, o + new Vector3(-w / 2 + .8f, 0, -d / 2 + 1.4f), .8f); Topiary(r, b, o + new Vector3(w / 2 - .8f, 0, -d / 2 + 1.4f), .8f);
            PlayViews(r, o);
            shell.Flush(r, "PLAY Hall arches"); b.Flush(r, "PLAY Hall");
        }

        /// What the PLAY Hall's arches look out on: a lawn court with its net and windscreen (west) and the practice range with
        /// its flags (east), a tall hedge between them, trees and sky beyond. Outside the hall's walls (never walked).
        static void PlayViews(Transform root, Vector3 o)
        {
            var b = new HubClub.Batch(); var life = HubLife.Of(root);
            HubArt.SkyDome(root, o + new Vector3(0, 0, 10), 34);
            Mesh("Lawn", root, Lathe("hall lawn", new[] { new Vector2(34, 0), new Vector2(0, 0) }, 0, 64), HubClub.Surface("lawn", HubClub.Lawn, HubClub.GrassTex, 1, new Vector2(2.2f, 2.2f), .5f, .16f, .35f, .05f), o + new Vector3(0, -.03f, 10));
            var court = HubClub.Surface("court turf", C("4E9033"), HubClub.StripeTexZ, 0, new Vector2(1, 2.6f), .15f, .4f, .35f, .05f);
            b[court].Quad(o + new Vector3(-14, .004f, 7.6f), o + new Vector3(-14, .004f, 33), o + new Vector3(.8f, .004f, 33), o + new Vector3(.8f, .004f, 7.6f), Vector3.up);
            var cc = o + new Vector3(-5.5f, 0, 21);
            HubClub.CourtLines(b, cc + Vector3.up * .008f, Vector3.forward, .75f);
            HubClub.Net(b, cc, Vector3.forward, 3.5f);
            HubClub.Windscreen(b, o + new Vector3(-14, 0, 31.5f), o + new Vector3(.4f, 0, 31.5f));
            HubClub.Windscreen(b, o + new Vector3(-12.5f, 0, 9), o + new Vector3(-12.5f, 0, 31.5f));
            HubClub.Hedge(b, o + new Vector3(.8f, 0, 7.8f), o + new Vector3(.8f, 0, 32), 2.4f, 1f);
            var range = HubClub.Surface("range turf", C("4F9236"), HubClub.StripeTexD, 1, new Vector2(4.2f, 4.2f), .18f, .3f, .35f, .05f);
            b[range].Quad(o + new Vector3(.8f, .004f, 7.6f), o + new Vector3(.8f, .004f, 33), o + new Vector3(15, .004f, 33), o + new Vector3(15, .004f, 7.6f), Vector3.up);
            b.noShadow.Add(court); b.noShadow.Add(range);
            foreach (var (x, z, h, c) in new[] { (4.4f, 15.5f, 1.9f, HubClub.White), (8.6f, 25f, 1.5f, C("E8433A")), (3.2f, 28f, 1.4f, C("E8433A")) })
            {
                var p = o + new Vector3(x, .01f, z); HubClub.Pin(b, p, h); life.Flag(p + Vector3.up * h, h * .3f, h * .2f, c);
            }
            b[HubClub.Surface("range bunker", HubClub.Sand, HubClub.GravelTex, 1, new Vector2(1.2f, 1.2f), .15f, .8f, .4f, .03f)].Lathe(o + new Vector3(6.6f, .02f, 19), new[] { new Vector2(1, 0), new Vector2(0, 0) }, 24, new Vector3(1.8f, 1, 1));
            HubClub.Hedge(b, o + new Vector3(.8f, 0, 31.5f), o + new Vector3(15, 0, 31.5f), 1.2f, .9f);
            int seed = 0;
            for (float x = -18; x <= 18; x += 4.6f) HubClub.Tree(b, o + new Vector3(x, 0, 35 + Mathf.Abs(Mathf.Sin(x)) * 2), 1.5f + Mathf.Abs(Mathf.Cos(x)) * .3f, 140 + seed++);
            foreach (int s in new[] { -1, 1 }) for (float z = 12; z <= 30; z += 6) HubClub.Tree(b, o + new Vector3(s * (16 + Mathf.Abs(Mathf.Sin(z)) * 2), 0, z), 1.4f, 160 + seed++);
            b.Flush(root, "PLAY Hall views");
        }

        // ================================================================== outdoors: the grass court and the practice range
        static void Outdoors(Transform root, HubLayout.Place place)
        {
            var o = place.origin; float w = place.size.x, d = place.size.y; bool golf = place.id == HubLayout.GolfRoom;
            // the room's invisible walls, so the walking space is exactly what it was
            HubKit.Blocker(place.title + " floor collider", root, o + new Vector3(0, -.1f, 0), new Vector3(w + .6f, .2f, d + .6f));
            void Wall(string n, Vector3 innerFaceCentre, float length, Vector3 inward) =>
                HubKit.Blocker(n, root, innerFaceCentre - inward * .15f + Vector3.up * H / 2, new Vector3(length + .6f, H, .3f), Quaternion.LookRotation(inward, Vector3.up));
            Wall(place.title + " north wall", o + new Vector3(0, 0, d / 2), w, Vector3.back);
            Wall(place.title + " west wall", o + new Vector3(-w / 2, 0, 0), d, Vector3.right);
            Wall(place.title + " east wall", o + new Vector3(w / 2, 0, 0), d, Vector3.left);
            HubKit.Blocker(place.title + " south wall", root, o + new Vector3(0, H / 2, -d / 2 - .15f), new Vector3(w + .6f, H, .3f));
            // a sky of its own (inside the camera's 60 m), a lawn out to it
            HubArt.SkyDome(root, o, 42);
            Mesh("Lawn", root, Lathe("grounds lawn", new[] { new Vector2(44, 0), new Vector2(0, 0) }, 0, 64), HubClub.Surface("lawn", HubClub.Lawn, HubClub.GrassTex, 1, new Vector2(2.2f, 2.2f), .45f, .18f, .4f, .05f), o + Vector3.down * .03f);
            var b = new HubClub.Batch();
            if (golf) Range(root, b, o); else Court(root, b, o);
            b.Flush(root, place.title + " grounds");
            HubLife.Of(root).Flock(o + new Vector3(2, 0, 26), 4, 13, 16, golf ? 4 : 3);
        }

        /// Tennis: you stand on the near half of a grass court; the net runs along the far edge of the walking space with the far
        /// half beyond it, green windscreens round the court, the umpire's chair at the net, trees and the clubhouse behind.
        static void Court(Transform root, HubClub.Batch b, Vector3 o)
        {
            var turf = HubClub.Surface("court turf", C("4E9033"), HubClub.StripeTexZ, 0, new Vector2(1, 2.6f), .15f, .4f, .35f, .05f);
            b[turf].Quad(o + new Vector3(-10, .004f, -16), o + new Vector3(-10, .004f, 27), o + new Vector3(10, .004f, 27), o + new Vector3(10, .004f, -16), Vector3.up);
            b.noShadow.Add(turf);
            var net = o + new Vector3(0, 0, 8);
            HubClub.CourtLines(b, net + Vector3.up * .008f, Vector3.forward, 1f);
            HubClub.Net(b, net, Vector3.forward, 6.4f);
            HubClub.UmpireChair(b, o + new Vector3(-7.7f, 0, 8.3f), Vector3.right);
            HubClub.Windscreen(b, o + new Vector3(-11, 0, 26.6f), o + new Vector3(11, 0, 26.6f));
            foreach (int s in new[] { -1, 1 }) HubClub.Windscreen(b, o + new Vector3(s * 11, 0, -7), o + new Vector3(s * 11, 0, 26.6f));
            foreach (int s in new[] { -1, 1 }) foreach (float z in new[] { 9f, 25.6f }) HubClub.LampPost(b, o + new Vector3(s * 10.3f, 0, z), 4.4f);
            foreach (int s in new[] { -1, 1 }) HubClub.Hedge(b, o + new Vector3(s * 12.2f, 0, -8), o + new Vector3(s * 12.2f, 0, 27.5f), 1.4f, 1f);
            HubClub.Hedge(b, o + new Vector3(-12.2f, 0, 27.6f), o + new Vector3(12.2f, 0, 27.6f), 1.4f, 1f);
            Plate(root, "TENNIS", "racket", o + new Vector3(0, 1.7f, 26.5f), Vector3.back, .4f);
            Backdrop(b, o, 30.5f);
            HubClub.Mansion(b, o + new Vector3(9.5f, 0, 32.5f), new Vector3(-.25f, 0, -1).normalized, .82f);
        }

        /// Golf: a striped practice tee with low stone walls (navy band, lamp posts on the pillars) and hedges either side, a
        /// putting green front right, and the course beyond: fairways, bunkers, a pond with a footbridge, trees, the clubhouse.
        static void Range(Transform root, HubClub.Batch b, Vector3 o)
        {
            var turf = HubClub.Surface("range turf", C("4F9236"), HubClub.StripeTexD, 1, new Vector2(4.2f, 4.2f), .18f, .3f, .35f, .05f);
            b[turf].Quad(o + new Vector3(-13, .004f, -16), o + new Vector3(-13, .004f, 37), o + new Vector3(13, .004f, 37), o + new Vector3(13, .004f, -16), Vector3.up);
            b.noShadow.Add(turf);
            foreach (int s in new[] { -1, 1 })
            {
                HubClub.StoneWall(b, o + new Vector3(s * 7.6f, 0, -9.5f), o + new Vector3(s * 7.6f, 0, 8.7f));
                HubClub.StoneWall(b, o + new Vector3(s * 7.6f, 0, 8.7f), o + new Vector3(s * 3.4f, 0, 8.7f));
                HubClub.Hedge(b, o + new Vector3(s * 8.7f, 0, -9.5f), o + new Vector3(s * 8.7f, 0, 9.6f), 1.5f, 1f, 10, s > 0 ? 61 : 62);
                foreach (float z in new[] { -5.86f, 5.06f }) HubClub.LampPost(b, o + new Vector3(s * 7.6f, 1.33f, z), 2.6f);
            }
            // the putting green, front right, with a flag in the wind
            var gc = o + new Vector3(3.6f, 0, -4.3f);
            b[HubClub.Surface("range collar", C("519437"), null, 0, default, .2f, .5f, .35f, .05f)].Lathe(gc + Vector3.up * .012f, new[] { new Vector2(2.9f, 0), new Vector2(0, 0) }, 40, new Vector3(1.22f, 1, 1));
            b[HubClub.Surface("range green", C("78B84C"), HubClub.StripeTexZ, 1, new Vector2(1.4f, 1.4f), .1f, .5f, .35f, .06f)].Lathe(gc + Vector3.up * .02f, new[] { new Vector2(2.5f, 0), new Vector2(0, 0) }, 40, new Vector3(1.22f, 1, 1));
            var pin = gc + new Vector3(.5f, .02f, -.3f); HubClub.Pin(b, pin, 1.9f);
            HubLife.Of(root).Flag(pin + Vector3.up * 1.9f, .62f, .42f, C("F2C230"));
            var pole = new GameObject("Pin collider"); pole.transform.SetParent(root, false); pole.transform.position = pin + Vector3.up * .9f;
            var cap = pole.AddComponent<CapsuleCollider>(); cap.radius = .08f; cap.height = 1.8f;
            // the course beyond, on a terrace a little above the tee (so the pond and its bridge show between the screens)
            const float lift = 1.2f, bank0 = 9.6f, bank1 = 12f;
            var hill = b[HubClub.Surface("lawn", HubClub.Lawn, HubClub.GrassTex, 1, new Vector2(2.2f, 2.2f), .5f, .16f, .35f, .05f)];
            hill.Quad(o + new Vector3(-32, 0, bank0), o + new Vector3(-32, lift, bank1), o + new Vector3(32, lift, bank1), o + new Vector3(32, 0, bank0), new Vector3(0, 1, -.5f).normalized);
            hill.Quad(o + new Vector3(-32, lift, bank1), o + new Vector3(-32, lift, 44), o + new Vector3(32, lift, 44), o + new Vector3(32, lift, bank1), Vector3.up);
            var top = o + Vector3.up * lift;
            var fair = HubClub.Surface("fairway", C("7CBB4E"), HubClub.StripeTexD, 1, new Vector2(3f, 3f), .12f, .3f, .35f, .05f);
            foreach (var (x, z, sx, sz) in new[] { (5.5f, 18f, 3.5f, 7f), (-4f, 29f, 6f, 4f), (9f, 27f, 3.5f, 6f) }) b[fair].Lathe(top + new Vector3(x, .012f, z), new[] { new Vector2(1, 0), new Vector2(0, 0) }, 32, new Vector3(sx, 1, sz));
            var sand = HubClub.Surface("range bunker", HubClub.Sand, HubClub.GravelTex, 1, new Vector2(1.2f, 1.2f), .15f, .8f, .4f, .03f);
            foreach (var (x, z, sx, sz) in new[] { (6f, 14f, 1.8f, 1.1f), (-7f, 24.5f, 1.6f, 1f), (6.5f, 31f, 2f, 1.1f), (-.5f, 33f, 1.4f, .8f) }) b[sand].Lathe(top + new Vector3(x, .02f, z), new[] { new Vector2(1, 0), new Vector2(0, 0) }, 24, new Vector3(sx, 1, sz));
            // the pond and its footbridge sit right of the range, in front of the clubhouse: from the range camera everything in the
            // middle is behind the screens, and this is where the line of sight clears the hedge
            var pond = top + new Vector3(12.2f, .03f, 16.5f);
            b[HubClub.Water("range pond", C("3F8BA6"), C("C8DCE6"), null, .35f, 1.2f, 1.6f, 5f, .6f)].Lathe(pond, new[] { new Vector2(1, 0), new Vector2(0, 0) }, 40, new Vector3(3.4f, 1, 2.6f));
            b[Lit(HubClub.StoneShade, .2f)].Lathe(pond + Vector3.down * .05f, new[] { new Vector2(1.06f, .02f), new Vector2(1.0f, .16f), new Vector2(.97f, .06f) }, 40, new Vector3(3.4f, 1, 2.6f));
            HubClub.Bridge(b, pond + new Vector3(-3.9f, .12f, .2f), pond + new Vector3(3.9f, .12f, .2f), 1.5f, .6f);
            for (int i = 0; i < 9; i++) { float a = i * .7f + 1.1f; HubClub.RoseBush(b, pond + new Vector3(Mathf.Cos(a) * 3.8f, .05f, Mathf.Sin(a) * 3.0f), .55f + (i % 3) * .1f, 90 + i); }
            foreach (var (x, z) in new[] { (-2.5f, 28.5f), (9.5f, 27.5f) }) { var p = top + new Vector3(x, .02f, z); HubClub.Pin(b, p, 1.6f); HubLife.Of(root).Flag(p + Vector3.up * 1.6f, .5f, .34f, C("E8433A")); }
            Plate(root, "GOLF", "flag", o + new Vector3(0, 2.3f, 9.6f), Vector3.back, .34f);
            foreach (int s in new[] { -1, 1 }) b[Lit(HubClub.Iron, .5f)].Box(new HubClub.Frame(o + new Vector3(s * .8f, 1.0f, 9.75f), Vector3.back), Vector3.zero, new Vector3(.08f, 2f, .08f));
            Backdrop(b, o, 34f, .78f, bank1, lift, (x, z) => x > 8 && z > 9 && z < 23);   // (clear of the pond)
            // rolling course hills on the horizon, a far tree line on them
            for (int i = 0; i < 7; i++)
            {
                float x = -27 + i * 9f, z = Mathf.Sqrt(Mathf.Max(0, 39.5f * 39.5f - x * x)) - 1.5f;
                b[HubClub.Surface("course hills", C("4E8A33"), null, 0, default, .35f, .3f, .35f, .03f)].Mesh(HubShapes.Crown(500 + i, 3, .12f), Matrix4x4.TRS(o + new Vector3(x, -1.6f + lift, z), Quaternion.Euler(0, i * 40, 0), new Vector3(9, 3.2f, 5)));
            }
            HubClub.Mansion(b, top + new Vector3(13f, 0, 30.5f), new Vector3(-.4f, 0, -1).normalized, .78f);
        }

        /// Trees round a sport ground: a thick belt along the sides and behind, kept inside the ground's sky.
        static void Backdrop(HubClub.Batch b, Vector3 o, float farZ, float backScale = 1f, float terraceZ = 999, float terraceY = 0, System.Func<float, float, bool> skip = null)
        {
            Vector3 At(float x, float z) => o + new Vector3(x, z > terraceZ ? terraceY : 0, z);
            int seed = 0;
            for (float x = -30; x <= 30; x += 5.2f)
            {
                float z = farZ + 2.5f + Mathf.Abs(Mathf.Sin(x * 1.7f)) * 3.5f;
                if (new Vector2(x, z).magnitude > 38.5f) z = Mathf.Sqrt(38.5f * 38.5f - x * x);
                if (x > 4 && x < 17) continue;   // the clubhouse
                HubClub.Tree(b, At(x, z), (1.45f + Mathf.Abs(Mathf.Sin(x)) * .4f) * backScale, 70 + seed++);
            }
            foreach (int s in new[] { -1, 1 })
                for (float z = -6; z <= farZ; z += 5.5f)
                {
                    float x = s * (15.5f + Mathf.Abs(Mathf.Sin(z * 2.3f)) * 3f);
                    if (skip == null || !skip(x, z)) HubClub.Tree(b, At(x, z), 1.35f + Mathf.Abs(Mathf.Cos(z)) * .35f, 90 + seed++); else seed++;
                    if (z > 4 && (skip == null || !skip(x + s * 7f, z + 2.5f))) HubClub.Tree(b, At(x + s * 7f, z + 2.5f), 1.6f, 110 + seed++); else seed++;
                }
        }

        /// A clipped topiary in a stone urn (where the rooms had potted plants), with a small collider.
        static void Topiary(Transform r, HubClub.Batch b, Vector3 at, float s, bool cone = false)
        {
            HubClub.Topiary(b, at, s, cone);
            var go = new GameObject("Topiary collider"); go.transform.SetParent(r, false); go.transform.position = at + Vector3.up * .5f;
            var c = go.AddComponent<CapsuleCollider>(); c.radius = .34f * s; c.height = 1f;
        }

        // ================================================================== stations and bays
        public static void Spot(Transform root, HubLayout.Spot s)
        {
            var face = Quaternion.LookRotation(s.facing, Vector3.up);
            var place = HubLayout.PlaceOf(s.place);
            // the spot you stand on: a soft glowing ring
            Mesh("Spot ring " + s.id, root, HubKit.Ring(.52f, .66f, 48), Glow(s.kind == HubLayout.Kind.Bay ? new Color(2.1f, 1.55f, .55f) : new Color(1.9f, 1.5f, .7f)), s.position + Vector3.up * .014f);
            var wallAt = s.position + s.facing * 1.15f;
            var trophies = new HubClub.Batch();
            switch (s.id)
            {
                case "rack-shirt": case "rack-shorts": Rail(root, s, s.id == "rack-shirt"); break;
                case "rack-shoes": Shelf(root, s, "shoes"); break;
                case "rack-racket": Shelf(root, s, "racket"); break;
                case "rack-club": Shelf(root, s, "club"); break;
                case "look-mirror": break;   // the vanity mirror is the station
                case "emote-mirror":
                    Box("Dance mirror frame", root, wallAt + Vector3.up * 1.4f + s.facing * .14f, new Vector3(1.76f, 2.36f, .06f), Lit(HubClub.Gold, .7f), .03f, face);
                    Box("Dance mirror", root, wallAt + Vector3.up * 1.4f + s.facing * .1f, new Vector3(1.6f, 2.2f, .08f), Lit(C("BFD7EA"), .95f), .04f, face);
                    break;
                case "desk-settings":
                    Box("Desk", root, wallAt + Vector3.up * .5f - s.facing * .1f, new Vector3(1.8f, 1.0f, .7f), Lit(C("6B4024"), .5f), .05f, face, true);
                    Box("Desk top", root, wallAt + Vector3.up * 1.03f - s.facing * .1f, new Vector3(1.95f, .08f, .8f), Lit(C("5A3820"), .55f), .03f, face);
                    Box("Desk leather", root, wallAt + Vector3.up * 1.075f - s.facing * .1f, new Vector3(1.5f, .01f, .5f), Lit(C("2F5A3A"), .35f), .005f, face);
                    Box("Desk screen", root, wallAt + Vector3.up * 1.45f + s.facing * .1f, new Vector3(.9f, .6f, .06f), Glow(new Color(.45f, .7f, 1.4f)), .03f, face);
                    break;
                case "screen-howto":
                    Box("How-to trim", root, wallAt + Vector3.up * 2.0f + s.facing * .24f, new Vector3(3.12f, 1.82f, .08f), Lit(HubClub.Gold, .7f), .05f, face);
                    Box("How-to frame", root, wallAt + Vector3.up * 2.0f + s.facing * .2f, new Vector3(3.0f, 1.7f, .1f), Lit(C("1C202C"), .5f), .06f, face);
                    Box("How-to screen", root, wallAt + Vector3.up * 2.0f + s.facing * .14f, new Vector3(2.8f, 1.5f, .02f), Glow(new Color(.55f, .75f, 1.5f)), .02f, face);
                    break;
                case "shelf-trophies":
                    Box("Trophy shelf", root, wallAt + Vector3.up * 1.0f, new Vector3(1.8f, 2.0f, .5f), Lit(C("6B4024"), .5f), .04f, face, true);
                    for (int k = 0; k < 2; k++) Box("Trophy ledge", root, wallAt + Vector3.up * (.43f + k * .62f) - s.facing * .38f, new Vector3(1.7f, .04f, .28f), Lit(C("8A5532"), .45f), .01f, face);
                    for (int k = 0; k < 2; k++) for (int i = 0; i < 3; i++)
                        HubClub.Trophy(trophies, wallAt + Vector3.up * (.45f + k * .62f) - s.facing * .26f + Vector3.Cross(Vector3.up, s.facing) * (i - 1) * .5f, .5f + ((i + k) % 2) * .15f);
                    for (int i = 0; i < 3; i++)
                        Mesh("Trophy", root, Lathe("trophy", new[] { new Vector2(0, 0), new Vector2(.12f, 0), new Vector2(.1f, .06f), new Vector2(.03f, .1f), new Vector2(.03f, .2f), new Vector2(.15f, .3f), new Vector2(.14f, .42f), new Vector2(0, .4f) }, .015f),
                             Lit(HubArt.Gold, .8f), wallAt + Vector3.up * 2.02f - s.facing * .05f + Vector3.Cross(Vector3.up, s.facing) * (i - 1) * .55f);
                    break;
                case "mailbox-feedback":
                    Mesh("Mailbox", root, Lathe("mailbox", new[] { new Vector2(0, 0), new Vector2(.26f, 0), new Vector2(.26f, 1.05f), new Vector2(.2f, 1.2f), new Vector2(0, 1.24f) }, .05f), Lit(HubClub.Navy, .5f), wallAt - s.facing * .2f);
                    Box("Mailbox slot", root, wallAt - s.facing * .45f + Vector3.up * .95f, new Vector3(.26f, .05f, .04f), Lit(HubClub.Gold, .75f), .01f, face);
                    break;
                case "board-invite":
                    Box("Invite board frame", root, wallAt + Vector3.up * 1.7f + s.facing * .14f, new Vector3(2.14f, 1.44f, .06f), Lit(HubClub.Gold, .7f), .03f, face);
                    Box("Invite board", root, wallAt + Vector3.up * 1.7f + s.facing * .1f, new Vector3(2.0f, 1.3f, .08f), Lit(C("B88B5A"), .2f), .05f, face);
                    for (int i = 0; i < 4; i++)
                        Box("Photo", root, wallAt + Vector3.up * (1.5f + (i / 2) * .5f) + s.facing * .05f + Vector3.Cross(Vector3.up, s.facing) * ((i % 2) - .5f) * .8f, new Vector3(.55f, .4f, .02f), Lit(new[] { C("FF8FB8"), C("8FC8FF"), C("A6E38F"), C("FFD36B") }[i], .4f), .02f, face);
                    break;
                default:
                    if (s.kind == HubLayout.Kind.Bay) Bay(root, s, place);
                    break;
            }
            trophies.Flush(root, "Trophies " + s.id);
            if (s.kind == HubLayout.Kind.Station && s.id != "look-mirror")
                Plate(root, s.label.ToUpperInvariant(), null, wallAt + Vector3.up * 2.75f + s.facing * .05f, -s.facing, .2f);
        }

        static void Rail(Transform root, HubLayout.Spot s, bool shirts)
        {
            var at = s.position + s.facing * 1.0f; var side = Vector3.Cross(Vector3.up, s.facing); var face = Quaternion.LookRotation(s.facing);
            Box("Rail bar", root, at + Vector3.up * 1.75f, new Vector3(1.8f, .05f, .05f), Lit(HubClub.Brass, .8f), .02f, face);
            foreach (int k in new[] { -1, 1 }) Box("Rail post", root, at + side * k * .9f + Vector3.up * .88f, new Vector3(.06f, 1.76f, .06f), Lit(HubClub.Brass, .8f), .02f, face, true);
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
            Box("Shelf back", root, at + Vector3.up * 1.1f + s.facing * .2f, new Vector3(1.8f, 2.2f, .08f), Lit(C("6B4024"), .45f), .03f, face, true);
            for (int k = 0; k < 3; k++) Box("Shelf", root, at + Vector3.up * (.5f + k * .6f), new Vector3(1.8f, .06f, .4f), Lit(C("8A5532"), .45f), .02f, face);
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
            var b = new HubClub.Batch();
            // the pad: navy with a brass rim and a warm glowing ring (the ring that fills while a match loads sits on it)
            Mesh("Bay mat " + s.id, root, Lathe("bay mat", new[] { new Vector2(1.55f, 0), new Vector2(1.5f, .04f), new Vector2(0, .04f) }, .02f, 48), Lit(HubClub.Navy, .5f), s.position + s.facing * .1f);
            Mesh("Bay rim " + s.id, root, HubKit.Ring(1.47f, 1.56f, 64), Lit(HubClub.Brass, .7f), s.position + s.facing * .1f + Vector3.up * .043f);
            Mesh("Bay glow " + s.id, root, HubKit.Ring(1.28f, 1.42f, 64), Glow(new Color(2.2f, 1.35f, .35f)), s.position + s.facing * .1f + Vector3.up * .045f);
            // a wooden park bench behind you, one seat per player (the same collider as the old bench)
            float bw = Mathf.Max(1.4f, s.seats * .7f);
            var benchAt = s.position - s.facing * .95f;
            var bench = new GameObject("Bench " + s.id); bench.transform.SetParent(root, false); bench.transform.SetPositionAndRotation(benchAt + Vector3.up * .45f, face);
            bench.AddComponent<BoxCollider>().size = new Vector3(bw, .12f, .5f);
            HubClub.Bench(b, benchAt, s.facing, bw + .2f, .49f);
            if (golf)
            {
                HubClub.GolfBag(b, benchAt - side * (bw / 2 + .5f) - s.facing * .1f);
                HubClub.BallBucket(b, benchAt + side * (bw / 2 + .42f) + s.facing * .25f);
            }
            // the big screen ahead (the mode's live preview), a dark frame with a thin gold trim, its navy-and-gold label
            var scr = s.position + s.facing * 1.75f + Vector3.up * 1.95f;
            Box("Screen trim " + s.id, root, scr + s.facing * .03f, new Vector3(2.99f, 1.84f, .12f), Lit(HubClub.Gold, .7f), .06f, face);
            Box("Screen frame " + s.id, root, scr, new Vector3(2.9f, 1.75f, .14f), Lit(C("1C202C"), .55f), .08f, face);
            var screen = Mesh("Screen " + s.id, root, HubShapes.Quad(new Vector2(2.66f, 1.5f)), Glow(golf ? new Color(.35f, .8f, .45f) : new Color(.35f, .55f, 1.1f)), scr - s.facing * .075f, Quaternion.LookRotation(s.facing, Vector3.up));
            screen.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            HubBayScreen.Attach(screen.GetComponent<MeshRenderer>(), s);
            Box("Screen stand", root, s.position + s.facing * 1.8f + Vector3.up * .55f, new Vector3(.2f, 1.1f, .12f), Lit(HubClub.Iron, .5f), .04f, face);
            Box("Screen foot", root, s.position + s.facing * 1.8f + Vector3.up * .03f, new Vector3(.9f, .06f, .5f), Lit(HubClub.Iron, .5f), .03f, face);
            Plate(root, s.label.ToUpperInvariant(), golf ? "flag" : "racket", scr + Vector3.up * 1.12f - s.facing * .02f, -s.facing, .22f);
            b.Flush(root, "Bay " + s.id);
        }

        // ================================================================== exit
        public static void Exit(Transform root, HubLayout.Door d)
        {
            var face = Quaternion.LookRotation(-d.inward, Vector3.up);
            var mat = HubShapes.RoundedBox(new Vector3(d.width + .4f, .03f, 1.2f), .015f, 2);
            Mesh("Exit mat", root, mat, Lit(HubClub.StoneLight, .3f), d.position + Vector3.up * .015f + d.inward * .1f, face);
            Mesh("Exit glow", root, HubShapes.RoundedBox(new Vector3(d.width, .01f, .25f), .005f, 2), Glow(new Color(2.2f, 1.6f, .6f)), d.position + Vector3.up * .03f + d.inward * .55f, face);
            var go = new GameObject("Exit label"); go.transform.SetParent(root, false);
            go.transform.SetPositionAndRotation(d.position + Vector3.up * .04f - d.inward * .05f, Quaternion.Euler(90, 0, 0));
            var canvas = go.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(260, 60); rt.localScale = Vector3.one / 100;
            var t = new GameObject("Text").AddComponent<Text>(); t.transform.SetParent(go.transform, false);
            t.font = HubKit.Font; t.text = "▼  " + d.label.ToUpperInvariant(); t.fontSize = 34; t.color = HubClub.Navy; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            var trt = t.rectTransform; trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        }
    }
}

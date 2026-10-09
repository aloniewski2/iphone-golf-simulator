using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    public static partial class TennisVenueBuilder
    {
        // =====================================================================================
        // SKYSCRAPER: a rooftop above a sea of cloud, among towers that are not all the same tower
        //
        //   near   the deck: stone margin with rooftop kit (tank, air handlers, vents, floodlight masts)
        //   mid    a skyline of ~30 towers in six facade families and nine silhouettes, one landmark
        //   far    a solid cloud sea 58 m under the deck, banks of cumulus standing on it, a banded sky
        //
        // Geometry is opaque and lit; the only alpha is the sky dome and the sun glow.
        // =====================================================================================
        static class Skyscraper
        {
            public static readonly Vector3 SunDir = new Vector3(-.80f, .30f, .52f).normalized;
            /// The cloud surface sits this far under the deck so it is seen from play (see AUDIT: the deck edge hides
            /// anything steeper than 5.4 degrees below the eye).
            const float SeaY = -58f;
            static readonly Vector3 HazeColor = new Vector3(.48f, .48f, .60f);   // cool dusk depth, below the felt-yellow ball

            // tower tops
            const int Flat = 0, Ziggurat = 1, Crown = 2, Dome = 3, Slant = 4, Needle = 5, Helipad = 6, Garden = 7, Drum = 9;

            struct Tw { public float x, z, roof, w, d; public int fam, top; }

            public static void Build(Transform root)
            {
                float hx = TennisVenue.DeckHalfX, hz = TennisVenue.DeckHalfZ;
                Deck(root, hx, hz);
                var fams = Families();
                var seaH = CloudSea(root);
                // The authored atmosphere provides cloud volume; no opaque pebble wisps.
                MainTower(root, hx, hz, fams);
                Rooftop(root, hx, hz);
                Mast(root);
                Skyline(root, fams, seaH);
                Sky(root);
            }

            // ----------------------------------------------------------------- deck

            static void Deck(Transform root, float hx, float hz)
            {
                var stone = Lit(new Color(.60f, .62f, .64f), .28f);
                var coping = Lit(new Color(.86f, .87f, .89f), .35f);
                var coral = Lit(new Color(.95f, .45f, .40f), .4f);
                Slab(root, "Rooftop deck", new Vector3(0, -.5f - .005f, 0), new Vector3(hx * 2, 1f, hz * 2), stone);
                float e = .34f;
                Slab(root, "Coping N", new Vector3(0, -.06f, hz - e / 2), new Vector3(hx * 2, .12f, e), coping);
                Slab(root, "Coping S", new Vector3(0, -.06f, -hz + e / 2), new Vector3(hx * 2, .12f, e), coping);
                Slab(root, "Coping E", new Vector3(hx - e / 2, -.06f, 0), new Vector3(e, .12f, hz * 2 - e * 2), coping);
                Slab(root, "Coping W", new Vector3(-hx + e / 2, -.06f, 0), new Vector3(e, .12f, hz * 2 - e * 2), coping);
                // a coral band round the slab's outer faces: reads from the drone, side and below views
                float t = .14f, y = -.42f, h = .34f;
                Slab(root, "Band N", new Vector3(0, y, hz + t / 2), new Vector3(hx * 2 + t * 2, h, t), coral);
                Slab(root, "Band S", new Vector3(0, y, -hz - t / 2), new Vector3(hx * 2 + t * 2, h, t), coral);
                Slab(root, "Band E", new Vector3(hx + t / 2, y, 0), new Vector3(t, h, hz * 2), coral);
                Slab(root, "Band W", new Vector3(-hx - t / 2, y, 0), new Vector3(t, h, hz * 2), coral);
            }

            // ----------------------------------------------------------------- facades

            sealed class Fam { public Material mat; public Vector2 uvM; public MB mb = new MB(); public string name; }

            static Fam[] Families()
            {
                Fam Make(string name, int cols, int rows, float winM, float floorM, float smooth, Color glow, System.Func<float, float, int, int, (Color a, Color e)> cell)
                {
                    var (alb, emi) = TexPair(128, 256, (u, v) =>
                    {
                        float cu = u * cols, cv = v * rows; int cx = Mathf.FloorToInt(cu), cy = Mathf.FloorToInt(cv);
                        var result=cell(cu - cx, cv - cy, cx, cy);
                        var a=result.a;
                        // Facade alpha stores glazing membership, not transparency.
                        bool glazed=(a.b>a.r*1.08f || a.g>a.r*1.20f) && Mathf.Max(a.r,Mathf.Max(a.g,a.b))<.91f;
                        a.a=glazed?1:0;return (a,result.e);
                    }, "Facade " + name);
                    var m = Glowing(Color.white, smooth, alb, emi, glow, Vector2.one);
                    var atmosphere=Resources.Load<Shader>("Tennis/Shaders/TennisTowerAtmosphere");
                    if(atmosphere){var wrapped=new Material(atmosphere){name=name+" cloud-immersed facade"};wrapped.SetTexture("_BaseMap",alb);wrapped.SetTexture("_EmissionMap",emi);wrapped.SetColor("_BaseColor",Color.white);wrapped.SetColor("_EmissionColor",glow);wrapped.SetFloat("_Smoothness",smooth);wrapped.SetTexture("_SkyPanorama",Resources.Load<Texture2D>("Tennis/Premium/SkyRooftop"));wrapped.SetFloat("_SkyRotation",-90);wrapped.SetTexture("_CloudTops",Resources.Load<Texture2D>("Tennis/Premium/CloudTops"));m=wrapped;}
                    return new Fam { mat = m, uvM = new Vector2(cols * winM, rows * floorM), name = name };
                }
                var warm = new Color(.95f, .66f, .34f); Color black = Color.black;
                var fams = new Fam[6];
                // 0 blue glass curtain wall with spandrel bands
                fams[0] = Make("blue glass", 4, 6, 3.2f, 3.6f, .62f, new Color(.85f, .62f, .34f), (fu, fv, cx, cy) =>
                {
                    bool mull = fu < .035f || fu > .965f, span = fv < .17f; float h1 = Hash(cx, cy, 9), h2 = Hash(cx, cy, 5);
                    var pane = Color.Lerp(new Color(.18f, .28f, .44f), new Color(.46f, .62f, .80f), Mathf.Clamp01(h1*.35f+(1-fv)*.45f+.20f*Mathf.Sin((fu+cx*.07f)*6.28f)));
                    var a = mull ? new Color(.70f, .74f, .80f) : span ? new Color(.44f, .50f, .58f) : pane;
                    bool lit = h2 > .935f && !mull && !span;
                    return (lit ? Color.Lerp(a, new Color(.92f, .70f, .42f), .22f) : a, lit ? warm*.30f : black);
                });
                // 1 cream stone with punched windows
                fams[1] = Make("cream stone", 3, 5, 3.6f, 4.0f, .18f, new Color(.8f, .58f, .3f), (fu, fv, cx, cy) =>
                {
                    float n = Hash(cx, cy, 31); bool win = fu > .20f && fu < .80f && fv > .22f && fv < .80f, sill = fu > .16f && fu < .84f && fv > .16f && fv <= .22f, lintel = fu > .16f && fu < .84f && fv >= .80f && fv < .86f;
                    var wall = new Color(.72f, .64f, .52f) * (.97f + .03f * Mathf.Sin(fu * 6.28f));
                    if (sill || lintel) return (new Color(.56f, .50f, .40f), black);
                    if (!win) return (wall, black);
                    bool lit = n > .82f;
                    return (lit ? new Color(.90f, .70f, .42f) : Color.Lerp(new Color(.16f, .20f, .30f), new Color(.30f, .38f, .50f), fv), lit ? warm : black);
                });
                // 2 terracotta brick with white frames
                fams[2] = Make("terracotta brick", 4, 5, 3.0f, 3.6f, .12f, new Color(.8f, .58f, .3f), (fu, fv, cx, cy) =>
                {
                    float n = Hash(cx, cy, 17); bool frame = fu > .16f && fu < .84f && fv > .20f && fv < .80f; bool inner = fu > .22f && fu < .78f && fv > .26f && fv < .74f;
                    float row = Mathf.Repeat(fv * 5 * 8, 1); bool mortar = row < .08f;
                    var brick = Color.Lerp(new Color(.66f, .33f, .22f), new Color(.78f, .42f, .28f), Hash(cx * 7 + Mathf.FloorToInt(fv * 40), cy, 4));
                    if (mortar && !frame) brick = new Color(.84f, .76f, .66f);
                    if (!frame) return (brick, black);
                    if (!inner) return (new Color(.78f, .76f, .72f), black);
                    bool lit = n > .80f;
                    return (lit ? new Color(.92f, .72f, .44f) : new Color(.20f, .26f, .38f), lit ? warm : black);
                });
                // 3 mint ribbon windows
                fams[3] = Make("mint ribbon", 6, 6, 2.6f, 3.4f, .35f, new Color(.8f, .6f, .32f), (fu, fv, cx, cy) =>
                {
                    bool mull = fu < .06f, ribbon = fv > .28f && fv < .74f, span = fv < .12f;
                    float n = Hash(cx, cy, 23);
                    if (span) return (new Color(.78f, .84f, .82f), black);
                    if (!ribbon || mull) return (new Color(.46f, .66f, .60f), black);
                    bool lit = n > .93f;
                    return (lit ? new Color(.92f, .74f, .46f) : Color.Lerp(new Color(.18f, .38f, .44f), new Color(.34f, .58f, .62f), fv), lit ? warm : black);
                });
                // 4 charcoal glass with gold fins
                fams[4] = Make("charcoal gold", 4, 6, 3.0f, 3.6f, .7f, new Color(.9f, .64f, .34f), (fu, fv, cx, cy) =>
                {
                    bool fin = fu < .025f || fu > .98f, span = fv < .14f; float n = Hash(cx, cy, 41);
                    if (fin) return (new Color(.90f, .70f, .30f), black);
                    if (span) return (new Color(.10f, .12f, .16f), black);
                    bool lit = n > .955f;
                    return (lit ? new Color(.92f, .72f, .44f) : Color.Lerp(new Color(.16f, .30f, .38f), new Color(.30f, .50f, .58f), n * .6f + fv * .4f), lit ? warm : black);
                });
                // 5 white and lavender with coral bands
                fams[5] = Make("lavender coral", 4, 6, 3.0f, 3.6f, .45f, new Color(.8f, .58f, .32f), (fu, fv, cx, cy) =>
                {
                    bool band = cy % 3 == 0 && fv < .26f; bool win = fu > .12f && fu < .88f && fv > .32f && fv < .82f; float n = Hash(cx, cy, 53);
                    if (band) return (new Color(.88f, .46f, .42f), black);
                    if (!win) return (new Color(.70f, .66f, .82f), black);
                    bool lit = n > .92f;
                    return (lit ? new Color(.92f, .74f, .48f) : Color.Lerp(new Color(.50f, .48f, .80f), new Color(.72f, .70f, .92f), fv), lit ? warm : black);
                });
                return fams;
            }

            // ----------------------------------------------------------------- the tower the deck sits on

            static void MainTower(Transform root, float hx, float hz, Fam[] fams)
            {
                var f = fams[4]; var gold = Lit(new Color(.92f, .72f, .28f), .5f, null, null, .5f); var coral = Lit(new Color(.95f, .45f, .40f), .4f);
                var concrete = Lit(new Color(.62f, .60f, .62f), .3f);
                // the slim overhang under the deck, with a warm light strip along its lower edge
                Slab(root, "Deck underside", new Vector3(0, -1.5f, 0), new Vector3(hx * 2 - .6f, 1f, hz * 2 - .6f), Lit(new Color(.55f, .58f, .62f), .4f));
                var strip = Glowing(new Color(.3f, .25f, .2f), .3f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(2.2f, 1.0f, .38f), Vector2.one);
                Slab(root, "Underglow N", new Vector3(0, -2.05f, hz - .55f), new Vector3(hx * 2 - 1.2f, .12f, .12f), strip);
                Slab(root, "Underglow S", new Vector3(0, -2.05f, -hz + .55f), new Vector3(hx * 2 - 1.2f, .12f, .12f), strip);
                Slab(root, "Underglow E", new Vector3(hx - .55f, -2.05f, 0), new Vector3(.12f, .12f, hz * 2 - 1.2f), strip);
                Slab(root, "Underglow W", new Vector3(-hx + .55f, -2.05f, 0), new Vector3(.12f, .12f, hz * 2 - 1.2f), strip);
                // shaft in two setbacks above the sea, widening downward, then a long base the clouds swallow
                float[][] tiers = { new[] { 1.0f, 1.0f, -2f, -30f }, new[] { 1.18f, 1.12f, -30f, -62f }, new[] { 1.5f, 1.4f, -62f, -140f } };
                var m = new MB(); var trim = new MB(); var accent = new MB();
                foreach (var t in tiers)
                {
                    float w = hx * 2 * t[0] * .82f, d = hz * 2 * t[1] * .70f, y0 = t[3], y1 = t[2];
                    m.Box(new Vector3(0, (y0 + y1) / 2, 0), new Vector3(w, y1 - y0, d), f.uvM, Vector2.zero);
                    trim.Solid(new Vector3(0, y0 + .6f, 0), new Vector3(w + .8f, 1.2f, d + .8f));                 // a ledge at each setback
                    foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })                          // gold corner fins
                        accent.Solid(new Vector3(sx * (w / 2 + .3f), (y0 + y1) / 2, sz * (d / 2 + .3f)), new Vector3(.7f, y1 - y0, .7f));
                }
                MeshObject(root, "Deck tower", m.ToMesh("Deck tower"), f.mat, false);
                MeshObject(root, "Deck tower ledges", trim.ToMesh("Deck tower ledges"), concrete, false);
                MeshObject(root, "Deck tower fins", accent.ToMesh("Deck tower fins"), gold, false);
                // coral bands under the deck and at the first setback
                var bands = new MB();
                bands.Solid(new Vector3(0, -3.2f, 0), new Vector3(hx * 2 * .82f + 1.0f, 1.0f, hz * 2 * .70f + 1.0f));
                bands.Solid(new Vector3(0, -31f, 0), new Vector3(hx * 2 * 1.18f * .82f + 1.0f, 1.2f, hz * 2 * 1.12f * .70f + 1.0f));
                MeshObject(root, "Deck tower bands", bands.ToMesh("Deck tower bands"), coral, false);
            }

            static void Mast(Transform root)
            {
                var steel = Lit(new Color(.82f, .84f, .86f), .5f, null, null, .6f);
                var m = new GameObject("Antenna mast").transform; m.SetParent(root, false); m.localPosition = new Vector3(8.2f, 0, 19.4f);
                Prim(PrimitiveType.Cube, m, "Plant room", new Vector3(0, .8f, 0), new Vector3(2.4f, 1.6f, 2.0f), Lit(new Color(.72f, .74f, .76f), .3f));
                for (int i = 0; i < 4; i++)
                {
                    float s = i < 2 ? -1 : 1, t = i % 2 == 0 ? -1 : 1;
                    var leg = Prim(PrimitiveType.Cylinder, m, "Leg", new Vector3(s * .45f, 8.5f, t * .45f), new Vector3(.09f, 7f, .09f), steel);
                    leg.transform.localRotation = Quaternion.Euler(-t * 1.6f, 0, s * 1.6f);
                }
                Prim(PrimitiveType.Cylinder, m, "Pole", new Vector3(0, 22f, 0), new Vector3(.22f, 8f, .22f), steel);
                for (int i = 0; i < 3; i++)
                    Prim(PrimitiveType.Cube, m, "Panel", new Vector3(0, 9.5f + i * 3.4f, .35f), new Vector3(.5f, 1.5f, .18f), Lit(new Color(.92f, .93f, .95f), .4f)).transform.localRotation = Quaternion.Euler(0, i * 120, 0);
                var beacon = Prim(PrimitiveType.Sphere, m, "Beacon", new Vector3(0, 30.4f, 0), Vector3.one * .6f, Glowing(new Color(.5f, .05f, .05f), .3f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(3.2f, .2f, .15f), Vector2.one), false);
                beacon.AddComponent<TennisVenueFx.Blink>();
            }

            // ----------------------------------------------------------------- rooftop kit (replaces the resort's lanterns, sofas and planters)

            static void Rooftop(Transform root, float hx, float hz)
            {
                var concrete = new MB(); var steel = new MB(); var wood = new MB(); var dark = new MB(); var lamp = new MB(); var glass = new MB();
                // water tank on legs, far left corner: the classic rooftop landmark
                {
                    var c = new Vector3(-11.3f, 0, 20.4f);
                    foreach (var a in new[] { 0f, 90f, 180f, 270f }) { float r = a * Mathf.Deg2Rad; steel.Cyl(c + new Vector3(Mathf.Cos(r) * .78f, 0, Mathf.Sin(r) * .78f), .07f, .07f, 1.3f, 6, Vector2.zero, Vector2.zero); }
                    wood.Cyl(c + Vector3.up * 1.3f, .95f, .95f, 2.2f, 20, Vector2.zero, Vector2.zero, true, true);
                    for (int i = 0; i < 3; i++) steel.Cyl(c + Vector3.up * (1.6f + i * .8f), .99f, .99f, .08f, 20, Vector2.zero, Vector2.zero, false);
                    wood.Cyl(c + Vector3.up * 3.5f, 1.05f, .05f, .9f, 20, Vector2.zero, Vector2.zero, false);
                }
                // air handlers, near right corner
                foreach (var z in new[] { -19.9f, -17.2f })
                {
                    TennisVenueArt.BevelBox(concrete, new Vector3(11.4f, .65f, z), new Vector3(1.9f, 1.3f, 1.9f), .065f);
                    steel.Cyl(new Vector3(11.4f, 1.3f, z), .62f, .62f, .16f, 14, Vector2.zero, Vector2.zero, true, false);
                    dark.Solid(new Vector3(10.42f, .6f, z), new Vector3(.06f, .45f, .9f));
                    for (int i = 0; i < 7; i++)
                        steel.Solid(new Vector3(10.375f, .41f + i * .064f, z), new Vector3(.055f, .018f, .82f));
                }
                // vent stacks, right margin
                foreach (var z in new[] { 6f, 9.5f, 13f })
                {
                    steel.Cyl(new Vector3(11.5f, 0, z), .2f, .2f, 1.5f, 10, Vector2.zero, Vector2.zero, false);
                    steel.Cyl(new Vector3(11.5f, 1.5f, z), .36f, .08f, .22f, 10, Vector2.zero, Vector2.zero, true);
                }
                // skylight, left margin
                concrete.Solid(new Vector3(-11.4f, .12f, -8f), new Vector3(1.5f, .24f, 2.6f));
                glass.Solid(new Vector3(-11.4f, .30f, -8f), new Vector3(1.15f, .16f, 2.2f));
                // stair bulkhead, right far
                concrete.Solid(new Vector3(11.3f, 1.25f, 17.6f), new Vector3(2.1f, 2.5f, 2.8f));
                concrete.Solid(new Vector3(11.3f, 2.58f, 17.6f), new Vector3(2.4f, .16f, 3.1f));
                dark.Solid(new Vector3(10.22f, .95f, 17.6f), new Vector3(.06f, 1.9f, 1.1f));
                // floodlight masts at the four corners, warm lamp heads (emission, not lights)
                foreach (var sx in new[] { -1, 1 })   // far end only: the near corners belong to the drone's landing and stay clear
                {
                    var p = new Vector3(sx * 11.55f, 0, 20.6f);
                    steel.Cyl(p, .17f, .09f, 8.2f, 8, Vector2.zero, Vector2.zero, false);
                    concrete.Cyl(p, .3f, .3f, .35f, 8, Vector2.zero, Vector2.zero, true);
                    lamp.Solid(p + new Vector3(-sx * .5f, 8.5f, 0), new Vector3(.25f, .8f, 1.7f));
                    steel.Solid(p + new Vector3(-sx * .26f, 8.5f, 0), new Vector3(.12f, .9f, 1.8f));
                    steel.Solid(p + new Vector3(0, 8.2f, 0), new Vector3(.3f, .15f, .3f));
                }
                MeshObject(root, "Rooftop concrete", concrete.ToMesh("Rooftop concrete"), Lit(new Color(.54f, .55f, .62f), .3f), true);   // a cool mid-grey: pale boxes against the horizon are as bright as the ball
                MeshObject(root, "Rooftop steel", steel.ToMesh("Rooftop steel"), Lit(new Color(.52f, .56f, .60f), .55f, null, null, .5f), true);
                MeshObject(root, "Rooftop tank", wood.ToMesh("Rooftop tank"), Lit(new Color(.58f, .38f, .24f), .2f), true);
                MeshObject(root, "Rooftop dark", dark.ToMesh("Rooftop dark"), Lit(new Color(.10f, .14f, .20f), .6f), false);
                MeshObject(root, "Rooftop glass", glass.ToMesh("Rooftop glass"), Lit(new Color(.30f, .52f, .62f), .85f, null, null, .2f), false);
                MeshObject(root, "Floodlight heads", lamp.ToMesh("Floodlight heads"), Glowing(new Color(.35f, .32f, .26f), .3f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(2.4f, 1.9f, 1.2f), Vector2.one), false);
                // windsock on the near left corner
                var pole = new GameObject("Windsock").transform; pole.SetParent(root, false); pole.localPosition = new Vector3(11.5f, 0, -3.5f);
                Prim(PrimitiveType.Cylinder, pole, "Pole", new Vector3(0, 2.1f, 0), new Vector3(.1f, 2.1f, .1f), Lit(new Color(.85f, .86f, .88f), .5f));
                var sockMb = new MB();
                sockMb.Cyl(Vector3.zero, .34f, .12f, 1.7f, 10, Vector2.zero, Vector2.zero, false);
                var sock = MeshObject(pole, "Sock", sockMb.ToMesh("Windsock"), Lit(new Color(.98f, .50f, .22f), .3f), false);
                sock.transform.localPosition = new Vector3(0, 4.15f, 0); sock.transform.localRotation = Quaternion.Euler(0, 0, -86);
                sock.AddComponent<TennisVenueFx.Sway>();
            }

            // ----------------------------------------------------------------- the cloud sea

            /// Pillow colours, one definition for every cloud: shaded valleys indigo-mauve, shoulders rose, only the lit tops cream.
            /// Deliberately a notch darker than daylight cloud: against a felt-yellow ball, bright cloud is the hardest thing to see through.
            static Texture2D CloudRamp(string name) => Ramp(name, new[] {
                (0f, new Color(.24f, .32f, .47f)), (.30f, new Color(.39f, .47f, .62f)), (.62f, new Color(.62f, .66f, .74f)), (1f, new Color(.86f, .81f, .76f)) });

            /// Height of the cloud surface at (x, z): rounded pillows with creases between them, a slow swell and fine puffs.
            static float SeaHeight(float x, float z)
            {
                float wx = x + (Mathf.PerlinNoise(x * .004f + 3, z * .004f + 9) - .5f) * 90f, wz = z + (Mathf.PerlinNoise(x * .004f + 17, z * .004f + 5) - .5f) * 90f;   // warp: no regular cells
                Worley(wx / 95f, wz / 95f, 11, out float f1, out float f2);
                float dome = Mathf.Sqrt(Mathf.Max(0, 1 - f1 * f1 * 1.15f)), crease = Sm(0f, .42f, f2 - f1);
                Worley(x / 42f, z / 42f, 29, out float g1, out float g2);
                float puff = Mathf.Sqrt(Mathf.Max(0, 1 - g1 * g1 * 1.4f)) * Sm(0f, .22f, g2 - g1);
                float swell = (Mathf.PerlinNoise(x * .0028f + 7, z * .0028f + 3) - .5f) * 2;
                return SeaY + dome * crease * 3f + puff * .8f + swell * 1.4f;
            }

            static System.Func<float, float, float> CloudSea(Transform root)
            {
                var group = new GameObject("Cloud sea").transform; group.SetParent(root, false);
                // pillow colour ramp: valleys lavender-indigo, shoulders pink, tops cream
                var shader=Resources.Load<Shader>("Tennis/Shaders/TennisCloudAtmosphere");
                var mat=shader ? new Material(shader) {name="Authored cloud-top atmosphere"} : Matte(Color.white,CloudRamp("Cloud ramp"));
                if(shader){mat.SetTexture("_CloudTops",Resources.Load<Texture2D>("Tennis/Premium/CloudTops"));mat.SetFloat("_WorldScale",.0018f);mat.SetFloat("_Exposure",.78f);}
                // polar grid: fine near the deck, coarse far out
                var radii = new List<float>(); float rr = 6;
                while (rr < 7000) { radii.Add(rr); rr += rr < 400 ? 10 : rr < 1200 ? 25 : rr < 3500 ? 80 : 250; }
                const int around = 192;
                var mb = new MB();
                for (int i = 0; i < radii.Count; i++)
                    for (int a = 0; a <= around; a++)
                    {
                        float th = a / (float)around * Mathf.PI * 2, x = Mathf.Cos(th) * radii[i], z = Mathf.Sin(th) * radii[i], h = SeaHeight(x, z);
                        mb.Add(new Vector3(x, h, z), new Vector2(.5f, Mathf.Clamp01((h - SeaY + 4f) / 36f)));
                    }
                for (int i = 0; i < radii.Count - 1; i++)
                    for (int a = 0; a < around; a++)
                    {
                        int i0 = i * (around + 1) + a, i1 = i0 + 1, i2 = i0 + around + 1, i3 = i2 + 1;
                        mb.T.AddRange(new[] { i0, i1, i2, i1, i3, i2 });     // clockwise from above (Unity is left-handed): faces up
                    }
                MeshObject(group, "Cloud sea surface", mb.ToMesh("Cloud sea"), mat, false);
                Log("sky: cloud sea surface", mb.Tris);

                // The panorama carries richly shaped distant cloud banks, continuously into the horizon.
                return SeaHeight;
            }

            /// Puffs of cloud drifting between the deck and the sea: a depth cue for the open edge (they stay under the deck).
            static void Wisps(Transform root)
            {
                var group = new GameObject("Wisps").transform; group.SetParent(root, false);
                var mat = Matte(Color.white, CloudRamp("Wisp ramp")); var rng = new System.Random(305);
                for (int i = 0, n = TennisQuality.Current == TennisQuality.Tier.Low ? 8 : 18; i < n; i++)
                {
                    float ang = Rand(rng, 0, Mathf.PI * 2), r = Rand(rng, 34, 150);
                    float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                    if (Mathf.Abs(x) < 26 && Mathf.Abs(z) < 34) { i--; continue; }   // not through the deck's own tower
                    var mb = new MB(); float s0 = Rand(rng, 7, 15);
                    for (int k = 0; k < 2; k++)
                        mb.Lump(new Vector3(Rand(rng, -1, 1) * s0 * 1.1f, Rand(rng, -.2f, .4f) * s0, Rand(rng, -1, 1) * s0 * .8f), new Vector3(s0 * Rand(rng, .8f, 1.5f), s0 * .38f, s0 * Rand(rng, .8f, 1.3f)), 2, Rand(rng, 0, 90), Rand(rng, 0, 1), .06f);
                    var go = MeshObject(group, "Wisp", mb.ToMesh("Wisp"), mat, false);
                    go.transform.localPosition = new Vector3(x, Rand(rng, -48, -12), z);
                    var d = go.AddComponent<TennisVenueFx.Drift>(); d.Velocity = new Vector3(Rand(rng, -2.4f, -.8f), 0, Rand(rng, -.9f, .9f)); d.Wrap = 170;
                }
            }

            // ----------------------------------------------------------------- skyline

            /// Inside the view cone behind the far baseline (about +-22 degrees from the gameplay camera): keep facades dark so a lobbed ball reads against them.
            static bool Central(float x, float z) => z > 0 && Mathf.Abs(x) < .40f * (z + 15.6f);

            static List<Tw> Layout(List<Vector3> path)
            {
                var L = new List<Tw>();
                void A(float x, float z, float roof, float w, float d, int fam, int top) => L.Add(new Tw { x = x, z = z, roof = roof, w = w, d = d, fam = fam, top = top });
                // anchors: frame the +z gameplay view, the postcard and the rival intro
                A(-84, 128, 44, 26, 24, 0, Ziggurat);
                A(96, 150, 52, 30, 26, 4, Crown);
                A(18, 250, 36, 22, 22, 4, Dome);
                A(-34, 205, 18, 18, 18, 2, Garden);
                A(160, 330, 82, 40, 34, 4, Slant);
                A(-185, 215, 70, 34, 30, 5, Needle);
                A(238, 160, 40, 28, 26, 0, Helipad);
                A(-260, 360, 24, 30, 28, 3, Flat);
                // player-intro side (-z): a drum tower and a pair a little further out
                A(-120, -250, 30, 24, 24, 1, Drum);
                A(120, -300, 58, 28, 26, 5, Crown);
                A(-300, -90, 48, 30, 28, 2, Ziggurat);
                A(290, -40, 34, 26, 26, 4, Flat);
                // far skyline: deterministic scatter
                var rng = new System.Random(2031); int guard = 0;
                while (L.Count < 34 && guard++ < 600)
                {
                    float ang = Rand(rng, 0, Mathf.PI * 2), r = 420 + Mathf.Sqrt(Rand(rng, 0, 1)) * 1500;
                    float roof = rng.NextDouble() < .16 ? Rand(rng, 80, 140) : Rand(rng, -14, 70), w = Rand(rng, 24, 60), d = w * Rand(rng, .8f, 1.25f);
                    float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                    bool bad = false;
                    foreach (var o in L) { float dx = o.x - x, dz = o.z - z, min = (Mathf.Max(w, d) + Mathf.Max(o.w, o.d)) * .5f + 18; if (dx * dx + dz * dz < min * min) { bad = true; break; } }
                    if (bad) continue;
                    L.Add(new Tw { x = x, z = z, roof = roof, w = w, d = d, fam = Central(x, z) ? new[] { 0, 2, 4 }[rng.Next(3)] : rng.Next(6), top = new[] { Flat, Ziggurat, Crown, Dome, Slant, Needle, Helipad, Garden, Drum }[rng.Next(9)] });
                }
                // keep clear of the drone: no tower reaches the camera's height within 16 m of the path
                int kept = 0, dropped = 0;
                for (int i = L.Count - 1; i >= 0; i--)
                {
                    var t = L[i]; float half = Mathf.Max(t.w, t.d) * .72f; bool hit = false;
                    foreach (var p in path) if (p.y < t.roof + 14f) { float dx = p.x - t.x, dz = p.z - t.z; if (dx * dx + dz * dz < (half + 16) * (half + 16)) { hit = true; break; } }
                    if (hit || (Mathf.Abs(t.x) < 70 && Mathf.Abs(t.z) < 70)) { L.RemoveAt(i); dropped++; } else kept++;
                }
                Log("sky: towers kept " + kept + ", dropped for drone clearance " + dropped, 0);
                return L;
            }

            static void Skyline(Transform root, Fam[] fams, System.Func<float, float, float> seaH)
            {
                var group = new GameObject("Skyline").transform; group.SetParent(root, false);
                var path = new List<Vector3>(); for (int i = 0; i <= 80; i++) { TennisVenue.Drone(i / 80f, out var p, out var l, out var f); path.Add(p); }
                var layout = Layout(path);
                var trim = new MB(); var gold = new MB(); var coral = new MB(); var teal = new MB(); var green = new MB(); var beacons = new List<Vector3>();
                var used = new HashSet<int>(); var tops = new HashSet<int>();
                var collars = new MB(); var rng = new System.Random(5);
                foreach (var t in layout)
                {
                    var fam = fams[t.fam]; used.Add(t.fam); tops.Add(t.top);
                    float distance=Mathf.Sqrt(t.x*t.x+t.z*t.z);
                    float hierarchy=Mathf.Lerp(1,.70f,Mathf.InverseLerp(220,1250,distance));
                    float w=t.w*hierarchy,d=t.d*hierarchy,roof=Mathf.Lerp(SeaY+9,t.roof,hierarchy); var off = new Vector2(Rand(rng, 0, 1), Rand(rng, 0, 1));
                    float baseY = -140f, sea = seaH(t.x, t.z);
                    // Two sculpted anchors establish a designed skyline. Their
                    // curved glazing, deep vertical fins and planted setbacks
                    // share the exact existing family materials and cloud depth.
                    if(t.x==-84 && t.z==128 || t.x==96 && t.z==150)
                    {
                        SignatureTower(fam.mb,trim,gold,green,t.x,t.z,w,d,roof,fam.uvM,off,t.x<0);
                        beacons.Add(new Vector3(t.x,roof+(t.x<0?21.6f:13.6f),t.z));
                        continue;
                    }
                    bool drum = t.top == Drum, slant = t.top == Slant;
                    float tierH = Rand(rng, 14, 24), w2 = slant || drum ? w : w * .78f, d2 = slant || drum ? d : d * .78f;
                    var c = new Vector3(t.x, 0, t.z);
                    if (drum)
                    {
                        float r = Mathf.Min(w, d) * .46f;
                        fam.mb.Cyl(new Vector3(t.x, baseY, t.z), r, r, roof - baseY, 28, fam.uvM, off, false);
                        for (float by = roof - 26; by > roof - 70; by -= 26) trim.Cyl(new Vector3(t.x, by, t.z), r + 1.6f, r + 1.6f, .7f, 28, Vector2.zero, Vector2.zero, true);   // ring balconies
                        trim.Cyl(new Vector3(t.x, roof, t.z), r + 1.2f, r + 1.2f, .8f, 28, Vector2.zero, Vector2.zero, true);
                        w2 = d2 = r * 1.7f;
                    }
                    else if (slant)
                    {
                        fam.mb.Box(new Vector3(t.x, (baseY + roof - tierH) / 2, t.z), new Vector3(w, roof - tierH - baseY, d), fam.uvM, off);
                        float rise = w * .55f; float x0 = t.x - w / 2, x1 = t.x + w / 2, z0 = t.z - d / 2, z1 = t.z + d / 2, yb = roof - tierH, yl = roof, yr = roof + rise;
                        float vm = fam.uvM.y, um = fam.uvM.x, ox = off.x, oy = off.y;
                        Vector2 UV(float u, float y) => new Vector2(u / um + ox, y / vm + oy);
                        fam.mb.Face4(new Vector3(x0, yb, z0), new Vector3(x1, yb, z0), new Vector3(x1, yr, z0), new Vector3(x0, yl, z0), UV(x0, yb), UV(x1, yb), UV(x1, yr), UV(x0, yl));        // -z
                        fam.mb.Face4(new Vector3(x1, yb, z0), new Vector3(x1, yb, z1), new Vector3(x1, yr, z1), new Vector3(x1, yr, z0), UV(z0, yb), UV(z1, yb), UV(z1, yr), UV(z0, yr));        // +x
                        fam.mb.Face4(new Vector3(x1, yb, z1), new Vector3(x0, yb, z1), new Vector3(x0, yl, z1), new Vector3(x1, yr, z1), UV(-x1, yb), UV(-x0, yb), UV(-x0, yl), UV(-x1, yr));      // +z
                        fam.mb.Face4(new Vector3(x0, yb, z1), new Vector3(x0, yb, z0), new Vector3(x0, yl, z0), new Vector3(x0, yl, z1), UV(-z1, yb), UV(-z0, yb), UV(-z0, yl), UV(-z1, yl));      // -x
                        { int a = trim.Add(new Vector3(x0, yl, z0), Vector2.zero), b = trim.Add(new Vector3(x1, yr, z0), Vector2.zero), cc = trim.Add(new Vector3(x1, yr, z1), Vector2.zero), dd = trim.Add(new Vector3(x0, yl, z1), Vector2.zero); trim.Quad(a, b, cc, dd); }
                        coral.Solid(new Vector3(t.x, roof - 2, t.z), new Vector3(w + .8f, .9f, d + .8f));
                        w2 = d2 = 0;
                    }
                    else
                    {
                        fam.mb.Box(new Vector3(t.x, (baseY + roof - tierH) / 2, t.z), new Vector3(w, roof - tierH - baseY, d), fam.uvM, off);
                        trim.Solid(new Vector3(t.x, roof - tierH + .5f, t.z), new Vector3(w + .9f, 1f, d + .9f));                                   // ledge at the setback
                        fam.mb.Box(new Vector3(t.x, roof - tierH / 2, t.z), new Vector3(w2, tierH, d2), fam.uvM, off);
                        trim.Solid(new Vector3(t.x, roof + .4f, t.z), new Vector3(w2 + .8f, .8f, d2 + .8f));
                    }
                    if(!drum && distance<380)
                    {
                        for(int edge=-1;edge<=1;edge+=2)
                        {
                            gold.Solid(new Vector3(t.x+edge*(w*.5f+.12f),(baseY+roof-tierH)/2,t.z-d*.5f-.12f),new Vector3(.35f,roof-tierH-baseY,.35f));
                            gold.Solid(new Vector3(t.x+edge*(w*.5f+.12f),(baseY+roof-tierH)/2,t.z+d*.5f+.12f),new Vector3(.35f,roof-tierH-baseY,.35f));
                        }
                    }
                    if (!slant)
                        switch (t.top)
                        {
                            case Ziggurat:
                                fam.mb.Box(new Vector3(t.x, roof + 5.5f, t.z), new Vector3(w2 * .7f, 9f, d2 * .7f), fam.uvM, off);
                                trim.Solid(new Vector3(t.x, roof + 10.3f, t.z), new Vector3(w2 * .7f + .6f, .7f, d2 * .7f + .6f));
                                fam.mb.Box(new Vector3(t.x, roof + 14.3f, t.z), new Vector3(w2 * .42f, 7f, d2 * .42f), fam.uvM, off);
                                gold.Solid(new Vector3(t.x, roof + 18.1f, t.z), new Vector3(w2 * .42f + .5f, .8f, d2 * .42f + .5f));
                                gold.Cyl(new Vector3(t.x, roof + 18.5f, t.z), .35f, .1f, 12f, 6, Vector2.zero, Vector2.zero, false);
                                break;
                            case Crown:
                                foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 })
                                    gold.Solid(new Vector3(t.x + sx * (w2 / 2 - .5f), roof + 5f, t.z + sz * (d2 / 2 - .5f)), new Vector3(1.1f, 9f, 1.1f));
                                coral.Solid(new Vector3(t.x, roof + 1.6f, t.z), new Vector3(w2 + .5f, 1.6f, d2 + .5f));
                                gold.Cyl(new Vector3(t.x, roof + .8f, t.z), .45f, .12f, 18f, 6, Vector2.zero, Vector2.zero, false);
                                break;
                            case Dome:
                                {
                                    float r = Mathf.Min(w2, d2) * .38f;
                                    fam.mb.Cyl(new Vector3(t.x, roof + .8f, t.z), r, r, 6f, 22, fam.uvM, off, false);
                                    trim.Cyl(new Vector3(t.x, roof + 6.8f, t.z), r + .6f, r + .6f, .6f, 22, Vector2.zero, Vector2.zero, true);
                                    teal.Dome(new Vector3(t.x, roof + 7.3f, t.z), r, 22, 6, .9f);
                                    gold.Cyl(new Vector3(t.x, roof + 7.3f + r * .9f - .2f, t.z), .3f, .08f, 5f, 6, Vector2.zero, Vector2.zero, false);
                                    break;
                                }
                            case Needle:
                                gold.Cyl(new Vector3(t.x, roof + .8f, t.z), .9f, .12f, 36f, 8, Vector2.zero, Vector2.zero, false);
                                coral.Cyl(new Vector3(t.x, roof + 20f, t.z), 3.2f, 3.2f, .6f, 14, Vector2.zero, Vector2.zero, true);
                                break;
                            case Helipad:
                                {
                                    float r = Mathf.Min(w2, d2) * .42f;
                                    coral.Cyl(new Vector3(t.x, roof + .8f, t.z), r, r, .3f, 24, Vector2.zero, Vector2.zero, true);
                                    trim.Cyl(new Vector3(t.x, roof + 1.1f, t.z), r * .62f, r * .62f, .06f, 24, Vector2.zero, Vector2.zero, true);
                                    coral.Cyl(new Vector3(t.x, roof + 1.16f, t.z), r * .3f, r * .3f, .04f, 24, Vector2.zero, Vector2.zero, true);
                                    break;
                                }
                            case Garden:
                                green.Solid(new Vector3(t.x, roof + 1.1f, t.z), new Vector3(w2 * .86f, .6f, d2 * .86f));
                                for (int k = 0; k < 7; k++)
                                {
                                    float tx = t.x + Rand(rng, -.38f, .38f) * w2, tz = t.z + Rand(rng, -.38f, .38f) * d2, th = Rand(rng, 2.4f, 4.2f);
                                    green.Cyl(new Vector3(tx, roof + 1.4f, tz), 1.3f, .1f, th, 7, Vector2.zero, Vector2.zero, false);
                                }
                                break;
                            default:   // Flat: a plant room and an aerial
                                trim.Solid(new Vector3(t.x + w2 * .15f, roof + 2f, t.z - d2 * .1f), new Vector3(w2 * .3f, 3.2f, d2 * .3f));
                                trim.Cyl(new Vector3(t.x - w2 * .2f, roof + .8f, t.z + d2 * .15f), .12f, .08f, 9f, 5, Vector2.zero, Vector2.zero, false);
                                break;
                        }
                    float topY = roof + (t.top == Needle ? 37f : t.top == Ziggurat ? 31f : t.top == Crown ? 18f : slant ? w * .55f : 9f);
                    if (topY > 60) beacons.Add(new Vector3(t.x + (t.top == Flat ? -w2 * .2f : 0), topY + .5f, t.z + (t.top == Flat ? d2 * .15f : 0)));

                }
                // skybridge twins on the player-intro side
                {
                    float x0 = -62, z0 = -330, h = 66, w = 22, d = 24, gap = 17; var fam = fams[3];
                    foreach (var sx in new[] { -1, 1 })
                    {
                        float x = x0 + sx * (w + gap) / 2; var off = new Vector2(sx * .31f + .5f, .2f);
                        fam.mb.Box(new Vector3(x, (-140 + h) / 2, z0), new Vector3(w, h + 140, d), fam.uvM, off);
                        trim.Solid(new Vector3(x, h + .4f, z0), new Vector3(w + .8f, .8f, d + .8f));
                        gold.Solid(new Vector3(x, h + 3f, z0), new Vector3(w * .5f, 5f, d * .5f));
                        gold.Cyl(new Vector3(x, h + 5.4f, z0), .5f, .12f, 16f, 6, Vector2.zero, Vector2.zero, false);
                        beacons.Add(new Vector3(x, h + 21.8f, z0));
                    }
                    var bf = fams[4]; bf.mb.Box(new Vector3(x0, h - 30, z0), new Vector3(gap + 2f, 6f, d * .5f), bf.uvM, new Vector2(.1f, .1f), false, false);
                    trim.Solid(new Vector3(x0, h - 26.9f, z0), new Vector3(gap + 2.6f, .6f, d * .5f + .6f));
                    trim.Solid(new Vector3(x0, h - 33.4f, z0), new Vector3(gap + 2.6f, .6f, d * .5f + .6f));
                }
                SaucerTower(root, trim, gold, coral, teal, beacons, seaH);
                foreach (var f in fams) if (f.mb.Count > 0) { MeshObject(group, "Towers " + f.name, f.mb.ToMesh("Towers " + f.name), f.mat, false); Log("sky: towers " + f.name, f.mb.Tris); }

                MeshObject(group, "Tower trim", trim.ToMesh("Tower trim"), Lit(new Color(.80f, .78f, .80f), .3f), false);
                MeshObject(group, "Tower gold", gold.ToMesh("Tower gold"), Lit(new Color(.92f, .72f, .28f), .5f, null, null, .5f), false);
                MeshObject(group, "Tower coral", coral.ToMesh("Tower coral"), Lit(new Color(.95f, .45f, .40f), .4f), false);
                MeshObject(group, "Tower teal", teal.ToMesh("Tower teal"), Lit(new Color(.10f, .58f, .54f), .5f), false);
                MeshObject(group, "Tower gardens", green.ToMesh("Tower gardens"), Lit(new Color(.28f, .56f, .30f), .15f), false);
                Log("sky: trim/gold/coral/teal/garden", trim.Tris + gold.Tris + coral.Tris + teal.Tris + green.Tris + collars.Tris);
                var beaconMat = Glowing(new Color(.5f, .05f, .05f), .3f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(3.2f, .2f, .15f), Vector2.one);
                int bn = 0;
                foreach (var b in beacons) { var go = Prim(PrimitiveType.Sphere, group, "Beacon", b, Vector3.one * 1.6f, beaconMat, false); if (bn++ % 2 == 1) go.AddComponent<TennisVenueFx.Blink>(); }
                Log("sky: facade families used " + used.Count + ", tower tops used " + tops.Count + ", beacons " + beacons.Count, 0);
            }

            /// Rounded rectangular curtain wall and crafted terrace profiles.
            /// All tiers are closed render-only meshes; no court/deck changes.
            static void SignatureTower(MB glass,MB stone,MB bronze,MB garden,float x,float z,
                                       float w,float d,float roof,Vector2 uv,Vector2 off,bool pearl)
            {
                float[] levels=pearl?new[]{-140f,roof-29,roof-9,roof+9,roof+20}:new[]{-140f,roof-35,roof-17,roof-1,roof+12};
                float[] scale=pearl?new[]{1.08f,1.04f,.91f,.72f,.52f}:new[]{1.07f,1.05f,.89f,.75f,.61f};
                for(int tier=0;tier<levels.Length-1;tier++)
                {
                    float y0=levels[tier],y1=levels[tier+1];
                    float centreX=x+(pearl?-1:1)*tier*.52f;
                    float wi=w*scale[tier],di=d*scale[tier];
                    RoundedPrism(glass,new Vector3(centreX,y0,z),wi,di,wi*.965f,di*.985f,y1-y0,Mathf.Min(wi,di)*.23f,uv,off);
                    // Rounded cornices project beyond glazing and shade the reveal.
                    RoundedPrism(stone,new Vector3(centreX,y1-.18f,z),wi*.99f+1.3f,di+1.3f,wi*.97f+1.3f,di*.985f+1.3f,.72f,Mathf.Min(wi,di)*.235f,Vector2.zero,Vector2.zero);
                    RoundedPrism(bronze,new Vector3(centreX,y1-.45f,z),wi+.12f,di+.12f,wi+.12f,di+.12f,.17f,Mathf.Min(wi,di)*.23f,Vector2.zero,Vector2.zero);
                    // Recessed facade lanes terminate at every actual terrace.
                    int fins=pearl?9:7;
                    for(int n=0;n<fins;n++)
                    {
                        float t=(n+.5f)/fins-.5f,fx=centreX+t*wi*.66f;
                        bronze.Solid(new Vector3(fx,(y0+y1)*.5f,z-di*.5f-.16f),new Vector3(.22f,y1-y0-.55f,.43f));
                        bronze.Solid(new Vector3(fx,(y0+y1)*.5f,z+di*.5f+.16f),new Vector3(.22f,y1-y0-.55f,.43f));
                    }
                    for(int edge=-1;edge<=1;edge+=2)for(int n=0;n<4;n++)
                        bronze.Solid(new Vector3(centreX+edge*(wi*.5f+.12f),(y0+y1)*.5f,z+(n/3f-.5f)*di*.60f),new Vector3(.36f,y1-y0-.55f,.22f));
                    if(tier>0)
                    {
                        foreach(int edge in new[]{-1,1})
                        {
                            float px=centreX+edge*(wi*.5f-.50f);
                            for(int n=0;n<4;n++)
                                TennisVenueArt.QueuePlant(n%2==0?"SHRUB_0":"SHRUB_1",new Vector3(px,y1+.50f,z+(n/3f-.5f)*di*.65f),tier*37+n*67,1.35f,1.10f);
                        }
                    }
                }
                // An inhabited planted roof, framed by a low sculpted parapet.
                float top=levels[levels.Length-1],tw=w*scale[scale.Length-2]*.965f,td=d*scale[scale.Length-2]*.985f;
                RoundedPrism(garden,new Vector3(x+(pearl?-1:1)*1.56f,top+.51f,z),tw*.72f,td*.72f,tw*.72f,td*.72f,.14f,Mathf.Min(tw,td)*.16f,Vector2.zero,Vector2.zero);
            }

            static void RoundedPrism(MB mesh,Vector3 baseAt,float w0,float d0,float w1,float d1,float height,
                                     float radius,Vector2 metres,Vector2 offset)
            {
                const int cornerSteps=12,sides=cornerSteps*4;
                Vector3 Point(int index,float w,float d,float y)
                {
                    int corner=(index%sides)/cornerSteps;float t=(index%cornerSteps)/(float)cornerSteps;
                    float angle=(corner*90+t*90)*Mathf.Deg2Rad;
                    float r=Mathf.Min(radius,Mathf.Min(w,d)*.48f);
                    float cx=(corner==0||corner==3?1:-1)*(w*.5f-r);
                    float cz=(corner<2?1:-1)*(d*.5f-r);
                    return baseAt+new Vector3(cx+Mathf.Cos(angle)*r,y,cz+Mathf.Sin(angle)*r);
                }
                float perimeter=0;var bottom=new int[sides+1];var top=new int[sides+1];
                for(int n=0;n<=sides;n++)
                {
                    var a=Point(n,w0,d0,0);var b=Point(n,w1,d1,height);
                    if(n>0)perimeter+=Vector3.Distance(a,Point(n-1,w0,d0,0));
                    float u=metres.x>0?perimeter/metres.x+offset.x:.5f;
                    bottom[n]=mesh.Add(a,new Vector2(u,metres.y>0?baseAt.y/metres.y+offset.y:.5f));
                    top[n]=mesh.Add(b,new Vector2(u,metres.y>0?(baseAt.y+height)/metres.y+offset.y:.5f));
                }
                for(int n=0;n<sides;n++)mesh.Quad(bottom[n],bottom[n+1],top[n+1],top[n]);
                int centre=mesh.Add(baseAt+Vector3.up*height,new Vector2(.5f,.5f));
                for(int n=0;n<sides;n++)mesh.Tri(centre,mesh.Add(Point(n,w1,d1,height),new Vector2(.5f,.5f)),mesh.Add(Point(n+1,w1,d1,height),new Vector2(.5f,.5f)));
            }

            /// The landmark: a needle shaft, a lit saucer with a window ring, a spire and a blinking beacon.
            static void SaucerTower(Transform root, MB trim, MB gold, MB coral, MB teal, List<Vector3> beacons, System.Func<float, float, float> seaH)
            {
                var c = new Vector3(-135f, 0, 312f); var ivory = new MB(); var ring = new MB();
                ivory.Cyl(new Vector3(c.x, -140f, c.z), 4.6f, 4.0f, 190f, 20, Vector2.zero, Vector2.zero, false);
                coral.Cyl(new Vector3(c.x, 8f, c.z), 4.7f, 4.7f, 1.2f, 20, Vector2.zero, Vector2.zero, true);
                coral.Cyl(new Vector3(c.x, 28f, c.z), 4.5f, 4.5f, 1.2f, 20, Vector2.zero, Vector2.zero, true);
                coral.Cyl(new Vector3(c.x, 50f, c.z), 4.0f, 15.5f, 5.5f, 28, Vector2.zero, Vector2.zero, true);                       // lower saucer cone
                ring.Cyl(new Vector3(c.x, 55.5f, c.z), 15.5f, 15.5f, 4.4f, 28, new Vector2(24f, 4.4f), new Vector2(0, -55.5f / 4.4f), false);          // window ring (own material)
                trim.Cyl(new Vector3(c.x, 59.9f, c.z), 15.5f, 10.5f, 3.4f, 28, Vector2.zero, Vector2.zero, true);                        // upper cone
                teal.Cyl(new Vector3(c.x, 63.3f, c.z), 10.5f, 7.0f, 2.2f, 28, Vector2.zero, Vector2.zero, true);
                gold.Cyl(new Vector3(c.x, 65.5f, c.z), 1.0f, .2f, 36f, 8, Vector2.zero, Vector2.zero, false);
                MeshObject(root, "Saucer shaft", ivory.ToMesh("Saucer shaft"), Lit(new Color(.93f, .91f, .86f), .35f), false);
                var (alb, emi) = TexPair(64, 16, (u, v) =>
                {
                    bool mull = Mathf.Repeat(u * 24, 1) < .12f || v < .1f || v > .9f;
                    return mull ? (new Color(.55f, .40f, .22f), Color.black) : (new Color(.95f, .80f, .50f), new Color(1f, .78f, .42f));
                }, "Saucer windows");
                MeshObject(root, "Saucer window ring", ring.ToMesh("Saucer ring"), Glowing(Color.white, .3f, alb, emi, new Color(2.4f, 1.7f, .9f), Vector2.one), false);
                beacons.Add(new Vector3(c.x, 102f, c.z));
            }

            // ----------------------------------------------------------------- sky

            static Color SkyBand(float el)
            {
                // Broad, softly blended dusk colour. The warm rim stays close to the
                // horizon so the city has depth and the play corridor stays cool.
                float[] edge = { 0f, 1.0f, 2.4f, 4.6f, 8.5f, 14f, 22f, 34f };
                Color[] col = {
                    new Color(.92f, .68f, .43f), new Color(.76f, .49f, .49f), new Color(.52f, .41f, .58f), new Color(.37f, .37f, .57f),
                    new Color(.28f, .34f, .56f), new Color(.20f, .29f, .51f), new Color(.14f, .23f, .43f), new Color(.09f, .16f, .32f) };
                Color c = new Color(HazeColor.x, HazeColor.y, HazeColor.z);                                       // below the horizon
                c = Color.Lerp(c, col[0], Sm(-1.2f, .4f, el));
                for (int i = 1; i < edge.Length; i++) c = Color.Lerp(c, col[i], Sm(edge[i] - 1.8f, edge[i] + 2.8f, el));
                return c;
            }

            static void Sky(Transform root)
            {
                // Real distant cloud shape and an uninterrupted dusk gradient come from the
                // production latitude-longitude atmosphere, shared with the cloud surface below.
                // The painted sun supplies the warm horizon; old giant ring sprites are removed.
            }

            static Texture2D SunRings() => Tex(512, 512, (u, v) =>
            {
                float dx = u * 2 - 1, dy = v * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
                float m3 = 1 - Sm(.975f, 1f, r), m2 = 1 - Sm(.615f, .64f, r), m1 = 1 - Sm(.385f, .41f, r), m0 = 1 - Sm(.262f, .278f, r);
                var c = new Color(1f, .78f, .78f); float a = .20f * m3;
                c = Color.Lerp(c, new Color(1f, .74f, .72f), m2); a = Mathf.Lerp(a, .34f, m2);
                c = Color.Lerp(c, new Color(1f, .84f, .74f), m1); a = Mathf.Lerp(a, .55f, m1);
                c = Color.Lerp(c, new Color(1f, .97f, .86f), m0); a = Mathf.Lerp(a, 1f, m0);
                return new Color(c.r, c.g, c.b, a);
            }, false, "Sun rings");

            public static void Light(Light sun, Camera camera)
            {
                sun.transform.rotation = Quaternion.LookRotation(-SunDir);
                sun.color = new Color(1f, .87f, .73f); sun.intensity = 1.75f;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.30f, .43f, .67f);
                RenderSettings.ambientEquatorColor = new Color(.30f, .35f, .46f);
                RenderSettings.ambientGroundColor = new Color(.24f, .28f, .36f);   // cloud bounce retains the cool underside
                var haze = new Color(HazeColor.x, HazeColor.y, HazeColor.z);
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = haze;
                RenderSettings.fogStartDistance = 150; RenderSettings.fogEndDistance = 2300;
                var skyShader=Resources.Load<Shader>("Tennis/Shaders/TennisRooftopSky");
                var panorama=Resources.Load<Texture2D>("Tennis/Premium/SkyRooftop");
                if(skyShader && panorama)
                {
                    var sky=new Material(skyShader){name="Rooftop dusk atmosphere"};
                    sky.SetTexture("_Panorama",panorama);sky.SetFloat("_Rotation",-90);sky.SetFloat("_Exposure",.90f);
                    RenderSettings.skybox=sky;camera.clearFlags=CameraClearFlags.Skybox;
                }
                else {RenderSettings.skybox=null;camera.clearFlags=CameraClearFlags.SolidColor;}
                camera.backgroundColor=haze;
            }
        }

        /// Triangle counts for the proof runs (HERO_VENUE_DUMP=1 only).
        static void Log(string what, int tris)
        {
            if (System.Environment.GetEnvironmentVariable("HERO_VENUE_DUMP") != "1") return;
            Debug.Log("[VenueBuild] " + what + (tris > 0 ? ": " + tris + " tris" : ""));
        }
    }
}

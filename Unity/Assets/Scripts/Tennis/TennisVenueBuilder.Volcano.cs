using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    public static partial class TennisVenueBuilder
    {
        // =====================================================================================
        // VOLCANO: a slab of obsidian over a lava lake, in a terraced bowl
        //
        //   near   the slab: a molten channel round its margin, the apron lit from the edge, a rock keel
        //   mid    basalt columns standing in the lake, glowing at the base and rimmed on top
        //   far    four stepped terraces with overhanging lips, lavafalls spilling over them, a toothed crest
        //
        // Lava is a light here: it is emission (bloomed), glow cards and gradients, never a flat orange wall.
        // The middle of the far wall behind the baseline stays dark and quiet so a lobbed ball reads.
        // =====================================================================================
        static class Volcano
        {
            public static readonly Vector3 SunDir = new Vector3(.66f, .10f, .70f).normalized;
            const float LakeY = -46f;
            public static readonly Vector3 Lake = new Vector3(0, LakeY, 0);   // the court hovers dead centre over the lava

            static Texture2D rock, rockNormal, cracks, crust, crustGlow, fallTex, fallGlow, basalt, basaltGlow;

            static void Textures()
            {
                if (rock) return;
                rock = Tex(512, 512, (u, v) =>
                {
                    // weathered basalt: dark strata, ash-dusted ledges, rust staining (no yellow: nothing here should look like a ball)
                    float n = Fbm(u, v, 6, 5, 21), band = TileNoiseXY(u, v, 3, 22, 8), b2 = TileNoiseXY(u, v, 2, 7, 15);
                    float ash = Sm(.55f, .70f, Fbm(u, v, 4, 4, 44)), rust = Sm(.58f, .72f, Fbm(u, v, 5, 3, 61));
                    var c = Color.Lerp(new Color(.07f, .06f, .07f), new Color(.26f, .20f, .20f), Mathf.Clamp01(n * .9f + (band - .5f) * .4f));
                    c = Color.Lerp(c, new Color(.34f, .30f, .30f), ash * .45f * Sm(.35f, .6f, b2));
                    c = Color.Lerp(c, new Color(.36f, .15f, .08f), rust * .35f);
                    float seam = .74f + .46f * Sm(.42f, .58f, b2);
                    return new Color(c.r * seam, c.g * seam, c.b * seam, 1);
                }, true, "Basalt");
                rockNormal = Tex(256, 256, (u, v) =>
                {
                    float H(float x, float y) => Fbm(x, y, 12, 4, 7) * .65f + TileNoiseXY(x, y, 4, 30, 2) * .35f;
                    const float e = 1f / 256f, k = 7f;
                    float dx = H(u + e, v) - H(u - e, v), dy = H(u, v + e) - H(u, v - e);
                    var nrm = new Vector3(-dx * k, -dy * k, 1).normalized;
                    return new Color(nrm.x * .5f + .5f, nrm.y * .5f + .5f, nrm.z * .5f + .5f, 1);
                }, true, "Rock relief", true);
                cracks = Tex(256, 256, (u, v) =>
                {
                    // thin wandering cracks, glowing in patches
                    float n = Mathf.Abs(Fbm(u, v, 5, 4, 41) - .5f) * 2;
                    float line = 1 - Sm(0f, .035f, n), pulse = Sm(.50f, .66f, Fbm(u, v, 3, 3, 9));
                    float m = line * pulse;
                    return new Color(m, m * .42f, m * .08f, 1);
                }, true, "Basalt cracks");
                // the lake: dark crust plates drifting on molten rock, glowing in the seams between them
                (crust, crustGlow) = TexPair(256, 256, (u, v) =>
                {
                    float wu = u + (Fbm(u, v, 4, 3, 71) - .5f) * .10f, wv = v + (Fbm(u, v, 4, 3, 93) - .5f) * .10f;     // warp: plates are not a honeycomb
                    WorleyTile(wu, wv, 6, 12, out float f1, out float f2);
                    float edge = 1 - Sm(0f, .11f, f2 - f1), mott = Fbm(u, v, 9, 3, 5);
                    var plate = Color.Lerp(new Color(.17f, .07f, .05f), new Color(.36f, .15f, .08f), mott);
                    var hot = Color.Lerp(new Color(1f, .34f, .06f), new Color(1f, .70f, .24f), edge * edge);
                    var under = new Color(.30f, .08f, .01f) * (.5f + mott);                 // a faint glow through the crust itself
                    return (Color.Lerp(plate, hot, edge), Color.Lerp(under, hot, edge));
                }, "Lava crust");
                // a lavafall: bright core, dark banks, long streaks down the flow (u across, v along)
                (fallTex, fallGlow) = TexPair(128, 256, (u, v) =>
                {
                    float w = 1 - Mathf.Abs(u * 2 - 1), streak = TileNoiseXY(u, v, 7, 3, 3);
                    float core = Sm(.10f, .55f, w + (streak - .5f) * .35f);
                    var hot = Color.Lerp(new Color(1f, .32f, .06f), new Color(1f, .62f, .20f), Sm(.4f, .9f, w) * (.6f + .4f * streak));
                    return (Color.Lerp(new Color(.14f, .06f, .05f), hot, core), Color.Lerp(Color.black, hot, core));
                }, "Lavafall");
                fallTex.wrapModeU = TextureWrapMode.Clamp; fallGlow.wrapModeU = TextureWrapMode.Clamp;
                // basalt columns: dark grain with heat that falls off with height above the lake (v = height, clamped; u repeats)
                (basalt, basaltGlow) = TexPair(64, 128, (u, v) =>
                {
                    float g = .07f + .08f * Fbm(u, v * 2, 4, 3, 7), heat = Mathf.Pow(1 - v, 2.1f);
                    var rockc = new Color(g * 1.1f, g, g * 1.05f);
                    var hotc = Color.Lerp(new Color(.95f, .30f, .06f), new Color(1f, .62f, .16f), heat);
                    return (Color.Lerp(rockc, hotc, Mathf.Clamp01(heat * .7f)), hotc * heat);
                }, "Basalt column");
                basalt.wrapModeV = TextureWrapMode.Clamp; basaltGlow.wrapModeV = TextureWrapMode.Clamp;
            }

            public static void Build(Transform root)
            {
                Textures();
                float hx = TennisVenue.DeckHalfX, hz = TennisVenue.DeckHalfZ;
                Deck(root, hx, hz);
                Keel(root, hx, hz);
                Bowl(root);
                LakeSurface(root);
                Islands(root);
                Braziers(root, hx, hz);
                Pylons(root);
                Sky(root);
                Plume(root);
                Embers(root);
            }

            // ----------------------------------------------------------------- the slab

            static void Deck(Transform root, float hx, float hz)
            {
                var deck = Lit(new Color(.075f, .07f, .078f), .35f);
                Slab(root, "Obsidian deck", new Vector3(0, -.5f - .005f, 0), new Vector3(hx * 2, 1f, hz * 2), deck);

                // a molten channel in the exposed margin all the way round (apron edge at x 10, z 19.5)
                const float co = .45f, cw = 1.0f, y = .012f;
                float xa = hx - co - cw, xb = hx - co, za = hz - co - cw, zb = hz - co;
                var ch = new MB();
                void Strip(float x0, float x1, float z0, float z1)
                    => ch.Face(new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1), x0 / 9f, x1 / 9f, z0 / 9f, z1 / 9f);
                Strip(-xb, xb, za, zb); Strip(-xb, xb, -zb, -za); Strip(xb - cw, xb, -za, za); Strip(-xb, -xb + cw, -za, za);
                var lava = Glowing(new Color(.3f, .12f, .06f), .35f, crust, crustGlow, new Color(2.6f, .9f, .16f), Vector2.one);
                MeshObject(root, "Lava channel", ch.ToMesh("Lava channel"), lava, false).AddComponent<TennisVenueFx.LavaFlow>().Material = lava;
                // kerbs either side of the channel keep it crisp
                var kb = new MB(); const float kw = .12f, kh = .08f;
                void Kerb(float cx, float cz, float sx, float sz) => kb.Solid(new Vector3(cx, kh / 2 - .02f, cz), new Vector3(sx, kh, sz));
                foreach (var s in new[] { -1, 1 })
                {
                    Kerb(s * (xa - kw / 2), 0, kw, za * 2);            // inner side of the channel, along z
                    Kerb(0, s * (za - kw / 2), xa * 2, kw);            // inner side, along x
                    Kerb(s * (xb + kw / 2), 0, kw, (zb + kw) * 2);     // outer side, along z
                    Kerb(0, s * (zb + kw / 2), (xb + kw) * 2, kw);     // outer side, along x
                }
                MeshObject(root, "Channel kerbs", kb.ToMesh("Channel kerbs"), Lit(new Color(.05f, .045f, .05f), .5f), false);

                // the lava's light on the apron: an orange gradient on its outer 3.4 m, clear of every line (lines end at x 5.5, z 11.9)
                var gl = new MB { Colors = true }; const float gd = 2.2f, gy = .07f; var hot = new Color32(255, 54, 8, 70); var none = new Color32(255, 54, 8, 0);
                void Glow(float x0, float x1, float z0, float z1, bool alongX, bool innerFirst)
                {
                    // alongX: gradient runs across x (side strips), otherwise across z (end strips)
                    Color32 c00 = innerFirst ? none : hot, c10 = innerFirst ? hot : none;
                    int a, b, c, d;
                    if (alongX) { a = gl.Add(new Vector3(x0, gy, z0), Vector2.zero, c00); b = gl.Add(new Vector3(x1, gy, z0), Vector2.zero, c10); c = gl.Add(new Vector3(x1, gy, z1), Vector2.zero, c10); d = gl.Add(new Vector3(x0, gy, z1), Vector2.zero, c00); }
                    else { a = gl.Add(new Vector3(x0, gy, z0), Vector2.zero, c00); b = gl.Add(new Vector3(x1, gy, z0), Vector2.zero, c00); c = gl.Add(new Vector3(x1, gy, z1), Vector2.zero, c10); d = gl.Add(new Vector3(x0, gy, z1), Vector2.zero, c10); }
                    gl.Quad(a, b, c, d);
                }
                float ax = 10f, az = 19.5f;
                Glow(ax - gd, ax, -az, az, true, true); Glow(-ax, -ax + gd, -az, az, true, false);
                Glow(-(ax - gd), ax - gd, az - gd, az, false, true); Glow(-(ax - gd), ax - gd, -az, -az + gd, false, false);
                MeshObject(root, "Apron edge glow", gl.ToMesh("Apron edge glow", false), Sprite(Additive, Texture2D.whiteTexture, Color.white, 2990, null, 1.2f), false);

                // a melt-line round the slab's outer faces, seen from the side and below
                var side = Glowing(new Color(.1f, .03f, .01f), .3f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(2.4f, .7f, .1f), Vector2.one);
                Slab(root, "Melt N", new Vector3(0, -.38f, hz + .03f), new Vector3(hx * 2 + .06f, .16f, .06f), side);
                Slab(root, "Melt S", new Vector3(0, -.38f, -hz - .03f), new Vector3(hx * 2 + .06f, .16f, .06f), side);
                Slab(root, "Melt E", new Vector3(hx + .03f, -.38f, 0), new Vector3(.06f, .16f, hz * 2), side);
                Slab(root, "Melt W", new Vector3(-hx - .03f, -.38f, 0), new Vector3(.06f, .16f, hz * 2), side);
                var under = new GameObject("Slab glow").AddComponent<Light>(); under.type = LightType.Point; under.transform.SetParent(root, false);
                under.transform.localPosition = new Vector3(0, -9f, 0); under.color = new Color(1f, .6f, .28f); under.range = 130; under.intensity = 14; under.shadows = LightShadows.None;
            }

            /// The rock the slab hangs from: a keel that tapers to a point far below, glowing in its cracks.
            static void Keel(Transform root, float hx, float hz)
            {
                const int around = 120, rings = 22; const float depth = 34f, margin = .25f;   // flush with the slab: the drone lands 1.4 m outside its edge, and a wider lip is seen edge-on as slivers
                var mb = new MB();
                Vector3 Rim(float t01, float k)
                {
                    float w = (hx + margin) * 2, d = (hz + margin) * 2, per = 2 * (w + d), sp = t01 * per, x, z;
                    if (sp < w) { x = -w / 2 + sp; z = -d / 2; }
                    else if (sp < w + d) { x = w / 2; z = -d / 2 + (sp - w); }
                    else if (sp < 2 * w + d) { x = w / 2 - (sp - w - d); z = d / 2; }
                    else { x = -w / 2; z = d / 2 - (sp - 2 * w - d); }
                    return new Vector3(x * k, 0, z * k);
                }
                for (int r = 0; r <= rings; r++)
                {
                    float t = r / (float)rings;
                    float k = t < .06f ? 1f : Mathf.Lerp(1.02f, .04f, Mathf.Pow((t - .06f) / .94f, .8f));
                    float y = -.9f - (t < .06f ? t / .06f * 2.4f : 2.4f + Mathf.Pow((t - .06f) / .94f, 1.1f) * (depth - 2.4f));
                    for (int a = 0; a <= around; a++)
                    {
                        float u = a / (float)around, cu = Mathf.Cos(u * Mathf.PI * 2), su = Mathf.Sin(u * Mathf.PI * 2);   // noise on the circle, so the seam closes
                        float n = (Noise(cu * 1.9f + t * 7, su * 1.9f + 3) - .5f) * 2 * 3.2f * Mathf.Sin(Mathf.Min(1, t * 2.5f) * Mathf.PI * .5f);
                        var p = Rim(u % 1f, k); p += new Vector3(p.x, 0, p.z).normalized * (n * Mathf.Pow(k, .8f));
                        p.y = y + (Noise(cu * 2.6f + 3, su * 2.6f + t * 9) - .5f) * 2 * 2.4f * t + Mathf.Sin(t * 22f + u * Mathf.PI * 2 * 1f) * .5f * t;
                        mb.Add(p, new Vector2(u * 9, t * 3));
                    }
                }
                for (int r = 0; r < rings; r++)
                    for (int a = 0; a < around; a++)
                    {
                        int i0 = r * (around + 1) + a, i1 = i0 + 1, i2 = i0 + around + 1, i3 = i2 + 1;
                        mb.T.AddRange(new[] { i0, i1, i2, i1, i3, i2 });
                    }
                var mat = Glowing(Color.white, .16f, rock, cracks, new Color(3.6f, 1.2f, .2f), Vector2.one);
                MeshObject(root, "Rock keel", mb.ToMesh("Rock keel"), mat, false).AddComponent<TennisVenueFx.Pulse>().Material = mat;
                var kb = mb.V[0]; Vector3 lo = kb, hi = kb; foreach (var q in mb.V) { lo = Vector3.Min(lo, q); hi = Vector3.Max(hi, q); }
                Log("volcano: rock keel x " + lo.x.ToString("F1") + ".." + hi.x.ToString("F1") + " y " + lo.y.ToString("F1") + ".." + hi.y.ToString("F1") + " z " + lo.z.ToString("F1") + ".." + hi.z.ToString("F1"), mb.Tris);
            }

            // ----------------------------------------------------------------- the bowl

            const int Around = 256;
            static readonly float[] Foot = { 121f, 150f, 182f, 222f, 262f };
            static readonly float[] Rise = { 24f, 21f, 19f, 17f, 15f };

            struct Row { public float r, y, nx, ny, arc; public byte kind; public int terrace; }
            static Row[] rows;
            static Vector3[][] grid;
            static readonly int[] riserStart = new int[5], treadStart = new int[5];

            /// A particle system starts playing the moment it is created, before its settings exist, so prewarm never happens.
            /// Stop, clear and play again once configured and the stream is already established.
            static void Restart(ParticleSystem ps) { ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); ps.Play(true); }

            static float Periodic(float th, float scale, float seed) => Noise(Mathf.Cos(th) * scale + seed, Mathf.Sin(th) * scale + seed * 1.7f);

            static void Profile()
            {
                var pts = new List<(float r, float y, byte kind, int t)>();
                float y = LakeY;
                pts.Add((112f, LakeY - 5f, 1, 0));
                for (int i = 0; i < Foot.Length; i++)
                {
                    float rf = Foot[i], h = Rise[i];
                    pts.Add((rf, y, 0, i));
                    pts.Add((rf + 1.2f, y + .18f * h, 0, i));
                    pts.Add((rf + 2.0f, y + .46f * h, 0, i));
                    pts.Add((rf + 1.4f, y + .76f * h, 0, i));
                    pts.Add((rf - .6f, y + .95f * h, 0, i));
                    pts.Add((rf - 1.8f, y + h, 0, i));          // the lip leans out over the riser: its underside catches the lava's up-light
                    y += h;
                    float next = i + 1 < Foot.Length ? Foot[i + 1] : 292f;
                    pts.Add((rf + 3f, y + .5f, 1, i));
                    pts.Add(((rf + next) / 2, y + 1.2f, 1, i));
                    pts.Add((next - 5f, y + 1.7f, 1, i));
                    y += 1.8f;
                }
                pts.Add((312f, y + 3f, 2, 5)); pts.Add((345f, y - 14f, 2, 5)); pts.Add((400f, y - 48f, 2, 5)); pts.Add((470f, -50f, 2, 5)); pts.Add((540f, -54f, 2, 5));

                var list = new List<Row>(); float arc = 0;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (i == 0) { list.Add(new Row { r = pts[0].r, y = pts[0].y, kind = pts[0].kind, terrace = pts[0].t }); continue; }
                    var a = pts[i - 1]; var b = pts[i]; float len = Mathf.Sqrt((b.r - a.r) * (b.r - a.r) + (b.y - a.y) * (b.y - a.y));
                    float step = b.kind == 0 ? 2.4f : b.kind == 1 ? 4f : 14f; int n = Mathf.Max(1, Mathf.CeilToInt(len / step));
                    for (int k = 1; k <= n; k++)
                    {
                        float f = k / (float)n; arc += len / n;
                        list.Add(new Row { r = Mathf.Lerp(a.r, b.r, f), y = Mathf.Lerp(a.y, b.y, f), kind = b.kind, terrace = b.t, arc = arc });
                    }
                }
                rows = list.ToArray();
                for (int i = 0; i < rows.Length; i++)
                {
                    var p0 = rows[Mathf.Max(0, i - 1)]; var p1 = rows[Mathf.Min(rows.Length - 1, i + 1)];
                    float tx = p1.r - p0.r, ty = p1.y - p0.y, l = Mathf.Max(.0001f, Mathf.Sqrt(tx * tx + ty * ty));
                    rows[i].nx = -ty / l; rows[i].ny = tx / l;                    // toward the crater's air: smaller r, higher y
                }
                for (int t = 0; t < 5; t++) { riserStart[t] = -1; treadStart[t] = -1; }
                for (int i = 0; i < rows.Length; i++)
                {
                    int t = rows[i].terrace; if (t > 4) continue;
                    if (rows[i].kind == 0 && riserStart[t] < 0) riserStart[t] = i;
                    if (rows[i].kind == 1 && treadStart[t] < 0 && rows[i].terrace == t && riserStart[t] >= 0) treadStart[t] = i;
                }
            }

            static void Bowl(Transform root)
            {
                Profile();
                grid = new Vector3[rows.Length][];
                for (int i = 0; i < rows.Length; i++)
                {
                    grid[i] = new Vector3[Around + 1]; var rw = rows[i];
                    float amp = rw.kind == 0 ? 1.7f : rw.kind == 1 ? .8f : 2.6f;
                    for (int j = 0; j <= Around; j++)
                    {
                        float th = (j % Around) / (float)Around * Mathf.PI * 2, c = Mathf.Cos(th), s = Mathf.Sin(th);
                        float nz = (Noise(c * 3.1f + rw.r * .02f, s * 3.1f + rw.y * .05f) - .5f) * 2 * amp + (Noise(c * 9f + rw.y * .11f, s * 9f + rw.r * .03f) - .5f) * amp;
                        float warp = rw.r > 118 ? (Periodic(th, 5f, 2f + rw.r * .01f) - .5f) * 14f + (Periodic(th, 14f, 7f) - .5f) * 6f : 0;
                        float rr = rw.r + rw.nx * nz + warp, yy = rw.y + rw.ny * nz;
                        if (rw.r > 255 && rw.r < 335) yy += (Periodic(th, 30f, 2f) - .5f) * 2 * 8f + Mathf.Max(0, Periodic(th, 34f, 4f) - .56f) * 55f;   // a toothed crest with a few horns
                        grid[i][j] = new Vector3(Lake.x + c * rr, yy, Lake.z + s * rr);
                    }
                }
                // three bands so the glow dies away with height (emission cannot vary per vertex on a lit material)
                int b1 = riserStart[2], b2 = riserStart[4];
                var normalMap = rockNormal; var bands = new[] { (0, b1, 2.6f, "Bowl low"), (b1, b2, 1.0f, "Bowl middle"), (b2, rows.Length - 1, .5f, "Bowl high") };
                int total = 0;
                foreach (var (a, b, glow, name) in bands)
                {
                    var mb = new MB(); int n = b - a + 1;
                    for (int i = a; i <= b; i++)
                        for (int j = 0; j <= Around; j++) mb.Add(grid[i][j], new Vector2(j / (float)Around * 40f, rows[i].arc / 34f));
                    for (int i = 0; i < n - 1; i++)
                        for (int j = 0; j < Around; j++)
                        {
                            int i0 = i * (Around + 1) + j, i1 = i0 + 1, i2 = i0 + Around + 1, i3 = i2 + 1;
                            // the wall behind the far baseline (+-24 degrees about +z) is the lob band: it gets a much quieter glow
                            var dst = a > 0 && Mathf.Abs(Mathf.DeltaAngle(j / (float)Around * 360f, 90f)) < 24f ? mb.T2 : mb.T;
                            dst.AddRange(new[] { i0, i1, i2, i1, i3, i2 });
                        }
                    Material Rock(float g) => WithRelief(Glowing(Color.white, .10f, rock, cracks, new Color(g, g * .34f, g * .06f), Vector2.one), normalMap, 1.8f, Vector2.one);
                    var mat = Rock(glow); var go = MeshObject(root, name, mb.ToMesh(name), mat, false);   // far geometry: no shadow pass (its bounds span the crater)
                    if (mb.T2.Count > 0) go.GetComponent<MeshRenderer>().sharedMaterials = new[] { mat, Rock(glow * .16f) };
                    if (glow > 2) go.AddComponent<TennisVenueFx.Pulse>().Material = mat;
                    total += mb.Tris + mb.T2.Count / 3;
                }
                Log("volcano: bowl", total);
                Falls(root);
                Ledges(root);
                Steam(root);
            }

            static Vector3 Grid(int row, float colF)
            {
                colF = Mathf.Repeat(colF, Around); int c0 = Mathf.FloorToInt(colF) % Around, c1 = (c0 + 1) % Around; float f = colF - Mathf.Floor(colF);
                return Vector3.Lerp(grid[row][c0], grid[row][c1], f);
            }

            /// A lavafall: a glowing ribbon laid on the bowl from a terrace tread down to the lake.
            static void Fall(MB mb, float thetaDeg, int topRow, int bottomRow, float widthTop, float widthRiser, float phase)
            {
                float colF = thetaDeg / 360f * Around; int a = mb.Count;
                int n = topRow - bottomRow + 1;
                for (int k = 0; k < n; k++)
                {
                    int row = bottomRow + k; var rw = rows[row];
                    float wobble = Mathf.Sin(row * .07f + phase) * .9f + Mathf.Sin(row * .19f + phase * 2) * .3f;
                    float cf = colF + wobble;
                    float width = rw.kind == 0 ? widthRiser : widthTop;
                    float half = width * .5f / (2 * Mathf.PI * Mathf.Max(rw.r, 60f) / Around);
                    Vector3 P(float c) => Grid(row, c);
                    Vector3 tcol = P(cf + 1) - P(cf - 1), trow = Grid(Mathf.Min(rows.Length - 1, row + 1), cf) - Grid(Mathf.Max(0, row - 1), cf);
                    Vector3 nrm = Vector3.Cross(tcol, trow).normalized;
                    var l = P(cf - half) + nrm * .45f; var r = P(cf + half) + nrm * .45f; float v = rw.arc / 12f;
                    mb.Add(l, new Vector2(0, v)); mb.Add(r, new Vector2(1, v));
                }
                for (int k = 0; k < n - 1; k++)
                {
                    int i0 = a + k * 2, i1 = i0 + 1, i2 = i0 + 2, i3 = i0 + 3;
                    mb.T.AddRange(new[] { i0, i1, i2, i1, i3, i2 });
                }
            }

            static void Falls(Transform root)
            {
                var mb = new MB();
                // (angle from +x toward +z, terrace the fall starts on). The pair flanking +z (90) sit at +-30 deg: in frame, clear of the lob band.
                var falls = new[] { (58f, 4), (122f, 4), (16f, 3), (164f, 3), (238f, 4), (292f, 4), (200f, 2), (336f, 3) };
                int i = 0;
                foreach (var (th, t) in falls) { Fall(mb, th, treadStart[t], riserStart[0], 10f, 13f, i * 1.7f); i++; }
                var mat = Glowing(new Color(.3f, .12f, .06f), .3f, fallTex, fallGlow, new Color(3.0f, .98f, .17f), Vector2.one);
                var go = MeshObject(root, "Lavafalls", mb.ToMesh("Lavafalls"), mat, false);
                var flow = go.AddComponent<TennisVenueFx.FallFlow>(); flow.Material = mat;
                Log("volcano: lavafalls", mb.Tris);
            }

            /// Low-poly rocks on the ledges: silhouette on the lips and something for the light to catch.
            static void Ledges(Transform root)
            {
                var rng = new System.Random(64); var mb = new MB(); int count = TennisQuality.Current == TennisQuality.Tier.Low ? 70 : 150;
                for (int k = 0; k < count; k++)
                {
                    int t = rng.Next(1, 5); int i0 = treadStart[t], i1 = Mathf.Min(rows.Length - 1, i0 + 6);
                    int row = rng.Next(i0, i1 + 1); float col = Rand(rng, 0, Around - 1);
                    var p = Grid(row, col); float size = Rand(rng, 1.6f, 5.2f);
                    mb.FlatLump(p + Vector3.up * size * .22f, new Vector3(size * Rand(rng, .8f, 1.4f), size * Rand(rng, .5f, .9f), size * Rand(rng, .8f, 1.4f)), Rand(rng, 0, 100), .32f);
                }
                MeshObject(root, "Ledge rocks", mb.ToMesh("Ledge rocks"), WithRelief(Lit(new Color(.9f, .84f, .8f), .12f, rock, Vector2.one * 1f), rockNormal, 1.4f, Vector2.one), false);
                Log("volcano: ledge rocks", mb.Tris);
            }

            /// Steam curling off the terrace treads.
            static void Steam(Transform root)
            {
                bool low = TennisQuality.Current == TennisQuality.Tier.Low;
                // theta runs from +x toward +z, so 90 is straight behind the far baseline: keep the corridor 54..126 clear of smoke
                float[] angles = low ? new[] { 20f, 200f, 300f } : new[] { 20f, 140f, 175f, 215f, 255f, 300f, 340f };
                int n = angles.Length;
                for (int i = 0; i < n; i++)
                {
                    float th = angles[i]; int t = 1 + i % 3;
                    var p = Grid(treadStart[t] + 2, th / 360f * Around);
                    var go = new GameObject("Steam vent").AddComponent<ParticleSystem>(); go.transform.SetParent(root, false);
                    go.transform.position = p; go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    var m = go.main; m.loop = true; m.prewarm = true; m.simulationSpace = ParticleSystemSimulationSpace.World;
                    m.startLifetime = new ParticleSystem.MinMaxCurve(6, 10); m.startSpeed = new ParticleSystem.MinMaxCurve(4, 9); m.startSize = new ParticleSystem.MinMaxCurve(8, 16);
                    m.maxParticles = 50; m.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                    var e = go.emission; e.rateOverTime = 6;
                    var sh = go.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 12; sh.radius = 3;
                    var c = go.colorOverLifetime; c.enabled = true;
                    var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(1f, .6f, .35f), 0), new GradientColorKey(new Color(.5f, .38f, .38f), 1) },
                        new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.40f, .2f), new GradientAlphaKey(0, 1) });
                    c.color = g;
                    var s2 = go.sizeOverLifetime; s2.enabled = true; s2.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .4f), new Keyframe(1, 1.8f)));
                    go.GetComponent<ParticleSystemRenderer>().sharedMaterial = Sprite(Alpha, Puff, Color.white, 3014);
                    Restart(go);
                }
            }

            // ----------------------------------------------------------------- the lake and the islands in it

            static void LakeSurface(Transform root)
            {
                var lakeMat = Glowing(new Color(.5f, .25f, .12f), .3f, crust, crustGlow, new Color(2.6f, .95f, .18f), new Vector2(2.2f, 2.2f));
                var lake = Prim(PrimitiveType.Cylinder, root, "Lava lake", Lake + Vector3.up * .3f, new Vector3(246, .2f, 246), lakeMat, false);
                lake.AddComponent<TennisVenueFx.LavaFlow>().Material = lakeMat;
            }

            /// Basalt columns standing in the lake: glowing at the foot, a bright rim on every top.
            static void Islands(Transform root)
            {
                var path = new List<Vector3>(); for (int i = 0; i <= 80; i++) { TennisVenue.Drone(i / 80f, out var p, out var l, out var f); path.Add(p); }
                // (x, z, radius, top height). Nothing within 10 m of the deck; none in the +-15 degree corridor behind the far baseline.
                var spec = new[] {
                    new Vector4(-34,-2,5,-4), new Vector4(-46,22,6,-2), new Vector4(-34,46,5,-5), new Vector4(-64,-26,5,-7), new Vector4(-62,56,6,-1), new Vector4(-88,12,7,-3),
                    new Vector4(35,0,5,-5), new Vector4(48,26,6,-1), new Vector4(36,52,5,-4), new Vector4(66,-24,5,-6), new Vector4(64,54,6,-2), new Vector4(90,8,7,-4),
                    new Vector4(-26,76,5,-2), new Vector4(30,84,5,1), new Vector4(-58,98,6,-3), new Vector4(58,102,6,-2),
                    new Vector4(-26,-52,5,-6), new Vector4(4,-86,6,-7), new Vector4(-62,-80,6,-8), new Vector4(70,-84,6,-7) };
                // cameras that must never end up inside rock: gameplay, postcard, edge, below, side, rear, rival intro
                var cams = new[] { new Vector3(0, 3.5f, -15.6f), new Vector3(0, 9, -30), new Vector3(-9.5f, 2.2f, -18), new Vector3(34, -26, -70), new Vector3(58, 10, -46), new Vector3(0, 3.5f, 15.6f), new Vector3(-1.6f, 1.25f, 6.2f) };
                var rng = new System.Random(8); var mb = new MB(); int islands = 0, dropped = 0;
                foreach (var s in spec)
                {
                    bool hit = false; foreach (var cam in cams) { float dx = cam.x - s.x, dz = cam.z - s.y; if (cam.y < s.w + 6 && dx * dx + dz * dz < (s.z + 16) * (s.z + 16)) { hit = true; break; } } if (!hit) foreach (var p in path) { float dx = p.x - s.x, dz = p.z - s.y; if (p.y < s.w + 14 && dx * dx + dz * dz < (s.z + 14) * (s.z + 14)) { hit = true; break; } }
                    if (hit) { dropped++; continue; }
                    Island(mb, new Vector3(s.x, 0, s.y), s.z, s.w, rng); islands++;
                }
                var mat = Glowing(Color.white, .22f, basalt, basaltGlow, new Color(2.4f, .8f, .15f), Vector2.one);
                MeshObject(root, "Basalt islands", mb.ToMesh("Basalt islands"), mat, false).AddComponent<TennisVenueFx.Pulse>().Material = mat;
                Log("volcano: basalt islands (" + islands + ", dropped " + dropped + ")", mb.Tris);
            }

            static void Island(MB mb, Vector3 c, float radius, float top, System.Random rng)
            {
                const float sp = 3.5f, R = 1.88f, bevel = .34f;
                int span = Mathf.CeilToInt(radius / sp) + 1;
                for (int q = -span; q <= span; q++)
                    for (int r = -span; r <= span; r++)
                    {
                        float x = sp * (q + r * .5f), z = sp * r * .866f, d = Mathf.Sqrt(x * x + z * z);
                        if (d > radius + Rand(rng, -1.2f, 1.2f)) continue;
                        float h = top + (1 - d / radius) * 1.6f + Rand(rng, -1.0f, 1.0f) + Mathf.Floor(Rand(rng, 0, 3)) * .5f;
                        var p = c + new Vector3(x, 0, z); float yb = LakeY - 3f;
                        var lo = new int[7]; var hi = new int[7];
                        for (int k = 0; k <= 6; k++)
                        {
                            float a = (k % 6) / 6f * Mathf.PI * 2 + Mathf.PI / 6, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                            float u = k / 6f; float v0 = 0, v1 = Mathf.Clamp01((h - bevel - LakeY) / 56f);
                            lo[k] = mb.Add(new Vector3(p.x + ca * R, yb, p.z + sa * R), new Vector2(u, v0));
                            hi[k] = mb.Add(new Vector3(p.x + ca * R, h - bevel, p.z + sa * R), new Vector2(u, v1));
                        }
                        for (int k = 0; k < 6; k++) mb.Quad(lo[k], lo[k + 1], hi[k + 1], hi[k]);
                        // bevel ring (hot rim) and cap (dark)
                        var rim = new int[7]; var cap = new int[7];
                        for (int k = 0; k <= 6; k++)
                        {
                            float a = (k % 6) / 6f * Mathf.PI * 2 + Mathf.PI / 6, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                            rim[k] = mb.Add(new Vector3(p.x + ca * R, h - bevel, p.z + sa * R), new Vector2(k / 6f, .02f));
                            cap[k] = mb.Add(new Vector3(p.x + ca * (R - bevel), h, p.z + sa * (R - bevel)), new Vector2(k / 6f, .03f));
                        }
                        for (int k = 0; k < 6; k++) mb.Quad(rim[k], rim[k + 1], cap[k + 1], cap[k]);
                        int ctr = mb.Add(new Vector3(p.x, h, p.z), new Vector2(.5f, .98f)); var top6 = new int[7];
                        for (int k = 0; k <= 6; k++) { float a = (k % 6) / 6f * Mathf.PI * 2 + Mathf.PI / 6; top6[k] = mb.Add(new Vector3(p.x + Mathf.Cos(a) * (R - bevel), h, p.z + Mathf.Sin(a) * (R - bevel)), new Vector2(k / 6f, .98f)); }
                        for (int k = 0; k < 6; k++) mb.Tri(ctr, top6[k], top6[k + 1]);
                    }
            }

            // ----------------------------------------------------------------- fire, sky, light

            /// Six low obsidian pylons stand where the resort's lanterns did, in the margin between the apron and the lava channel:
            /// a glowing slit on the inward face and a hot cap, so the slab's edge is lit from points, not only from the channel.
            static void Pylons(Transform root)
            {
                var body = new MB(); var glow = new MB();
                foreach (var sx in new[] { -1, 1 })
                    foreach (var z in new[] { -8f, 8f, 18f })   // none at z -17: that corner is inside the postcard and edge cameras' view
                    {
                        var p = new Vector3(sx * 10.62f, 0, z);
                        body.Cyl(p, .34f, .22f, 1.45f, 6, Vector2.zero, Vector2.zero, true);
                        glow.Cyl(p + Vector3.up * 1.45f, .22f, .22f, .08f, 6, Vector2.zero, Vector2.zero, true);                // hot cap
                        glow.Solid(p + new Vector3(-sx * .275f, .75f, 0), new Vector3(.05f, .82f, .09f));                       // slit on the court-facing side
                    }
                MeshObject(root, "Obsidian pylons", body.ToMesh("Obsidian pylons"), Lit(new Color(.06f, .055f, .07f), .45f), true);
                MeshObject(root, "Pylon glow", glow.ToMesh("Pylon glow"), Glowing(new Color(.3f, .1f, .04f), .3f, Texture2D.whiteTexture, Texture2D.whiteTexture, new Color(2.3f, .36f, .05f), Vector2.one), false);   // red-orange: tone-mapped yellow would read as a second ball
                Log("volcano: pylons", body.Tris + glow.Tris);
            }

            static void Braziers(Transform root, float hx, float hz)
            {
                var bowl = Lit(new Color(.09f, .08f, .09f), .3f);
                var flame = Sprite(Additive, Soft, new Color(1f, .55f, .16f, .9f), 3008);
                bool light = TennisQuality.Current != TennisQuality.Tier.Low;
                foreach (var c in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
                {
                    var p = new Vector3(c.x * (hx - .9f), 0, c.y * (hz - .9f));
                    Prim(PrimitiveType.Cylinder, root, "Brazier stand", p + Vector3.up * .55f, new Vector3(.32f, .55f, .32f), bowl);
                    Prim(PrimitiveType.Cylinder, root, "Brazier bowl", p + Vector3.up * 1.15f, new Vector3(.9f, .16f, .9f), bowl);
                    var f = Billboard(root, "Flame", flame, p + Vector3.up * 1.9f, new Vector2(1.5f, 2.4f));
                    f.AddComponent<TennisVenueFx.Flicker>();
                    if (light)
                    {
                        var l = new GameObject("Brazier light").AddComponent<Light>(); l.type = LightType.Point; l.transform.SetParent(root, false);
                        l.transform.localPosition = p + Vector3.up * 2f; l.color = new Color(1f, .5f, .2f); l.range = 14; l.intensity = 2.2f; l.shadows = LightShadows.None;
                    }
                }
            }

            static float Smoke(float u, float el) => Fbm(u, el / 90f * 1.8f + .3f, 9, 4, 17);

            static void Sky(Transform root)
            {
                float[] edge = { 0f, 3f, 7f, 13f, 22f, 36f };
                Color[] col = { new Color(1f, .46f, .14f), new Color(.92f, .27f, .15f), new Color(.66f, .17f, .33f), new Color(.40f, .13f, .38f), new Color(.21f, .09f, .31f), new Color(.10f, .06f, .20f) };
                var warpCol = new float[1025]; for (int i = 0; i < warpCol.Length; i++) warpCol[i] = float.NaN;
                TexDome(root, "volcano", (az, el) =>
                {
                    float u01 = az / (Mathf.PI * 2);
                    int column = Mathf.Min(1024, Mathf.FloorToInt(u01 * 1024f));
                    if (float.IsNaN(warpCol[column])) warpCol[column] = (Fbm((column + .5f) / 1024f, .5f, 7, 3, 61) - .5f) * 4f;
                    float warp = warpCol[column], e = el + warp * Sm(-1, 8, el);
                    Color c = new Color(.30f, .10f, .08f);
                    c = Color.Lerp(c, col[0], Sm(-1.2f, .4f, e));
                    for (int i = 1; i < edge.Length; i++) c = Color.Lerp(c, col[i], Sm(edge[i] - .6f, edge[i] + .6f, e));
                    // billows of smoke: dark bodies, glowing undersides lit by the crater
                    float body = 0, lower = 0;   // the smoke term is exactly zero at or below 1.5 degrees, and its under-lit edge only matters where there is smoke
                    if (el > 1.5f)
                    {
                        body = Sm(.54f, .60f, Smoke(u01, el)) * Sm(1.5f, 9f, el);
                        if (body > 0) lower = body * (1 - Sm(.54f, .60f, Smoke(u01, el - 1.5f)));
                    }
                    c = Color.Lerp(c, new Color(.16f, .07f, .17f), body * .85f);
                    c = Color.Lerp(c, new Color(1f, .50f, .16f), lower * (1 - Sm(22f, 40f, el)) * .9f);
                    c.a = 1; return c;
                });
                var rings = Sprite(Alpha, VolcanoSun(), Color.white, 2998);
                Billboard(root, "Sun", rings, SunDir * 2790, new Vector2(900, 900), false).AddComponent<TennisVenueFx.FollowCamera>().Offset = SunDir * 2790;
                var glow = Sprite(Additive, Soft, new Color(1f, .5f, .2f, .55f), 2999, null, .7f);
                Billboard(root, "Sun glow", glow, SunDir * 2800, new Vector2(1500, 1500), false).AddComponent<TennisVenueFx.FollowCamera>().Offset = SunDir * 2800;
            }

            static Texture2D VolcanoSun() => Tex(512, 512, (u, v) =>
            {
                float dx = u * 2 - 1, dy = v * 2 - 1, r = Mathf.Sqrt(dx * dx + dy * dy);
                float m3 = 1 - Sm(.975f, 1f, r), m2 = 1 - Sm(.615f, .64f, r), m1 = 1 - Sm(.385f, .41f, r), m0 = 1 - Sm(.262f, .278f, r);
                var c = new Color(.9f, .22f, .16f); float a = .18f * m3;
                c = Color.Lerp(c, new Color(1f, .34f, .16f), m2); a = Mathf.Lerp(a, .30f, m2);
                c = Color.Lerp(c, new Color(1f, .52f, .20f), m1); a = Mathf.Lerp(a, .50f, m1);
                c = Color.Lerp(c, new Color(1f, .78f, .42f), m0); a = Mathf.Lerp(a, .95f, m0);
                return new Color(c.r, c.g, c.b, a);
            }, false, "Volcano sun");

            /// The eruption: a tall column of ash from a vent in the far rim, with fire fountains in the lake.
            static void Plume(Transform root)
            {
                bool low = TennisQuality.Current == TennisQuality.Tier.Low;
                var vent = Grid(treadStart[4] + 2, 142f / 360f * Around) + Vector3.up * 2;   // 52 degrees off the lob corridor: seen from the drone and the side, never behind the baseline
                var smoke = new GameObject("Plume").AddComponent<ParticleSystem>(); smoke.transform.SetParent(root, false); smoke.transform.position = vent;
                smoke.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                var main = smoke.main; main.loop = true; main.prewarm = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(12, 20); main.startSpeed = new ParticleSystem.MinMaxCurve(18, 30);
                main.startSize = new ParticleSystem.MinMaxCurve(34, 70); main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                main.maxParticles = low ? 320 : 700; main.gravityModifier = -.02f;
                var em = smoke.emission; em.rateOverTime = low ? 16 : 36;
                var sh = smoke.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 7; sh.radius = 12;
                var col = smoke.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(new Color(1f, .62f, .24f), 0), new GradientColorKey(new Color(.62f, .30f, .26f), .2f), new GradientColorKey(new Color(.30f, .18f, .26f), .55f), new GradientColorKey(new Color(.20f, .14f, .24f), 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(.85f, .08f), new GradientAlphaKey(.75f, .7f), new GradientAlphaKey(0, 1) });
                col.color = g;
                var szl = smoke.sizeOverLifetime; szl.enabled = true; szl.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .3f), new Keyframe(1, 1.7f)));
                var rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-.15f, .15f);
                var pr = smoke.GetComponent<ParticleSystemRenderer>(); pr.sharedMaterial = Sprite(Alpha, Puff, Color.white, 3012); pr.shadowCastingMode = ShadowCastingMode.Off;
                Restart(smoke);
                for (int i = 0; i < (low ? 2 : 4); i++)
                {
                    var f = new GameObject("Lava fountain").AddComponent<ParticleSystem>(); f.transform.SetParent(root, false);
                    f.transform.localPosition = Lake + new Vector3(Mathf.Sin(i * 1.9f) * 55, 1, 50 + Mathf.Cos(i * 2.3f) * 55); f.transform.localRotation = Quaternion.Euler(-90, 0, 0);
                    var fm = f.main; fm.loop = true; fm.prewarm = true; fm.startLifetime = 3.6f; fm.startSpeed = new ParticleSystem.MinMaxCurve(22, 42); fm.startSize = new ParticleSystem.MinMaxCurve(1.6f, 4.2f);
                    fm.gravityModifier = 2.4f; fm.simulationSpace = ParticleSystemSimulationSpace.World; fm.maxParticles = 160;
                    var fe = f.emission; fe.rateOverTime = 36;
                    var fs = f.shape; fs.shapeType = ParticleSystemShapeType.Cone; fs.angle = 14; fs.radius = 3;
                    var fc = f.colorOverLifetime; fc.enabled = true;
                    var fg = new Gradient(); fg.SetKeys(new[] { new GradientColorKey(new Color(1f, .8f, .3f), 0), new GradientColorKey(new Color(1f, .3f, .08f), 1) }, new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
                    fc.color = fg;
                    var fr = f.GetComponent<ParticleSystemRenderer>(); fr.sharedMaterial = Sprite(Additive, Soft, Color.white, 3011); fr.shadowCastingMode = ShadowCastingMode.Off;
                    Restart(f);
                }
            }

            /// Sparks and ash drifting up past the court.
            static void Embers(Transform root)
            {
                var e = new GameObject("Embers").AddComponent<ParticleSystem>(); e.transform.SetParent(root, false); e.transform.localPosition = new Vector3(0, -12, 4);
                var m = e.main; m.loop = true; m.prewarm = true; m.startLifetime = new ParticleSystem.MinMaxCurve(6, 11); m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 4.5f);
                m.startSize = new ParticleSystem.MinMaxCurve(.05f, .13f); m.simulationSpace = ParticleSystemSimulationSpace.World; m.maxParticles = TennisQuality.Current == TennisQuality.Tier.Low ? 90 : 200;
                var em = e.emission; em.rateOverTime = TennisQuality.Current == TennisQuality.Tier.Low ? 10 : 20;
                var s = e.shape; s.shapeType = ParticleSystemShapeType.Box; s.scale = new Vector3(90, 4, 130); s.rotation = new Vector3(-90, 0, 0);
                var v = e.velocityOverLifetime; v.enabled = true; v.x = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f); v.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f); v.y = 0;
                var c = e.colorOverLifetime; c.enabled = true;
                var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(new Color(1f, .56f, .18f), 0), new GradientColorKey(new Color(1f, .3f, .08f), .6f), new GradientColorKey(new Color(.6f, .1f, .05f), 1) },   // starts orange: a yellow spark is ball-coloured
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .1f), new GradientAlphaKey(.7f, .7f), new GradientAlphaKey(0, 1) });
                c.color = g;
                var r = e.GetComponent<ParticleSystemRenderer>(); r.sharedMaterial = Sprite(Additive, Soft, Color.white, 3013); r.shadowCastingMode = ShadowCastingMode.Off;
                Restart(e);
            }

            public static void Light(Light sun, Camera camera)
            {
                sun.transform.rotation = Quaternion.LookRotation(-SunDir);
                sun.color = new Color(1f, .52f, .26f); sun.intensity = 2.3f;
                RenderSettings.ambientMode = AmbientMode.Trilight;
                RenderSettings.ambientSkyColor = new Color(.34f, .24f, .40f);
                RenderSettings.ambientEquatorColor = new Color(.55f, .30f, .22f);
                RenderSettings.ambientGroundColor = new Color(.60f, .22f, .08f);   // the lava lights everything from below
                RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = new Color(.42f, .19f, .14f);
                RenderSettings.fogStartDistance = 350; RenderSettings.fogEndDistance = 3800;
                RenderSettings.skybox = null;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.42f, .19f, .14f);
                // an orange up-light from the crater, on the players' undersides and the racket
                var wallGlow = new GameObject("Lava wall glow").AddComponent<Light>(); wallGlow.type = LightType.Directional; wallGlow.shadows = LightShadows.None;
                wallGlow.transform.rotation = Quaternion.LookRotation(new Vector3(0f, .30f, .95f)); wallGlow.color = new Color(1f, .52f, .22f); wallGlow.intensity = .38f;
                var up = new GameObject("Lava up-light").AddComponent<Light>(); up.type = LightType.Directional; up.shadows = LightShadows.None;
                up.transform.rotation = Quaternion.LookRotation(new Vector3(.05f, .95f, .25f)); up.color = new Color(1f, .42f, .12f); up.intensity = .8f;
            }
        }
    }
}

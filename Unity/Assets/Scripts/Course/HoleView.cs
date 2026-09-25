using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Course
{
    /// Builds a hole at runtime. With a modelled course in Resources/Course/hole_NN (a Blender
    /// island, exported as FBX) it places that model so its tee and pin markers land on the
    /// hole's, and the ground under the ball is read off its meshes; otherwise it draws the hole
    /// out of primitives: rough everywhere, a fairway ribbon along the centerline, a round green,
    /// bunkers, a tee box. World units are yards: course X is world X, course D is world Z, up is Y.
    public sealed class HoleView : MonoBehaviour
    {
        public Hole Hole { get; private set; }
        public Transform Flag { get; private set; }
        /// The hole on screen, so `ToWorld` can put things on its ground.
        public static HoleView Current { get; private set; }
        bool hasGround;

        static readonly Color RoughColor = new(0.30f, 0.52f, 0.20f);
        static readonly Color FairwayColor = new(0.45f, 0.72f, 0.28f);
        static readonly Color GreenColor = new(0.55f, 0.83f, 0.36f);
        static readonly Color FringeColor = new(0.50f, 0.78f, 0.32f);
        static readonly Color SandColor = new(0.93f, 0.86f, 0.62f);
        static readonly Color TeeColor = new(0.42f, 0.70f, 0.30f);
        static readonly Color CupColor = new(0.08f, 0.08f, 0.06f);
        static readonly Color TreeColor = new(0.16f, 0.36f, 0.14f);

        /// A course point `height` yards above the ground there. Flat holes have their ground at
        /// y = 0; a modelled hole's terrain rises and falls under the ball.
        public static Vector3 ToWorld(CoursePoint p, double height = 0) => new((float)p.X, (float)(GroundHeight(p) + height), (float)p.D);
        public static CoursePoint ToCourse(Vector3 w) => new(w.x, w.z);

        public static double GroundHeight(CoursePoint p)
        {
            if (!Current || !Current.hasGround) return 0;
            var from = new Vector3((float)p.X, 400, (float)p.D);
            return Physics.Raycast(from, Vector3.down, out var hit, 800, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0;
        }

        public static HoleView Build(Hole hole, Transform parent)
        {
            var root = new GameObject($"Hole {hole.Number}");
            root.transform.SetParent(parent, false);
            var view = root.AddComponent<HoleView>();
            view.Hole = hole;
            Current = view;
            var model = Resources.Load<GameObject>($"Course/hole_{hole.Number:00}");
            if (model) view.BuildFromModel(model); else view.BuildGeometry();
            view.BuildPin();
            return view;
        }

        void OnDestroy() { if (Current == this) Current = null; }

        // ----- Modelled course -----

        /// A course's own look over the palette, by the hole's Theme: Maple Bay's autumn, the
        /// same colours as blender/scripts/maple_bay_palette.py.
        static readonly Dictionary<string, Dictionary<string, Color>> Themes = new()
        {
            ["autumn"] = new()
            {
                ["MAT_ROUGH"] = Rgb(150, 142, 60), ["MAT_FAIRWAY"] = Rgb(140, 196, 70), ["MAT_FAIRWAY_STRIPE"] = Rgb(124, 182, 60),
                ["MAT_FIRSTCUT"] = Rgb(118, 170, 56), ["MAT_GREEN"] = Rgb(150, 218, 86), ["MAT_BUNKER_LIP"] = Rgb(164, 192, 82),
                ["MAT_SAND"] = Rgb(246, 232, 200), ["MAT_CLIFF"] = Rgb(178, 108, 72), ["MAT_CLIFF_DARK"] = Rgb(138, 80, 56),
                ["MAT_TREE_DARK"] = Rgb(178, 58, 34), ["MAT_TREE_MID"] = Rgb(222, 110, 40), ["MAT_TREE_LIGHT"] = Rgb(242, 178, 60),
                ["MAT_ROCK"] = Rgb(170, 140, 116), ["MAT_ROCK_DARK"] = Rgb(128, 100, 84),
                ["MAT_WATER"] = Rgb(22, 96, 150), ["MAT_WATER_SHALLOW"] = Rgb(62, 160, 188),
            },
        };

        /// A material's colour on this hole: its course's theme first, then the palette.
        Color? Colour(string name)
        {
            if (Hole != null && Themes.TryGetValue(Hole.Theme ?? "", out var theme) && theme.TryGetValue(name, out var themed)) return themed;
            return Palette.TryGetValue(name, out var c) ? c : null;
        }

        /// Blender material name → the flat game colour. The FBX carries the same names, so the
        /// look is set here rather than by whatever the importer made of them.
        static readonly Dictionary<string, Color> Palette = new()
        {
            ["MAT_FAIRWAY"] = Rgb(118, 208, 56), ["MAT_FAIRWAY_STRIPE"] = Rgb(100, 192, 48), ["MAT_FIRSTCUT"] = Rgb(84, 176, 44),
            ["MAT_ROUGH"] = Rgb(58, 148, 38), ["MAT_GREEN"] = Rgb(156, 228, 72), ["MAT_BUNKER_LIP"] = Rgb(166, 228, 90),
            ["MAT_SAND"] = Rgb(240, 218, 160), ["MAT_WATER"] = Rgb(16, 70, 170), ["MAT_WATER_SHALLOW"] = Rgb(40, 146, 222),
            ["MAT_FOAM"] = Rgb(226, 244, 252), ["MAT_CLIFF"] = Rgb(118, 122, 130), ["MAT_CLIFF_DARK"] = Rgb(84, 90, 100),
            ["MAT_ROCK"] = Rgb(138, 140, 146), ["MAT_ROCK_DARK"] = Rgb(98, 102, 110), ["MAT_TREE_DARK"] = Rgb(32, 104, 54),
            ["MAT_TREE_MID"] = Rgb(50, 140, 62), ["MAT_TREE_LIGHT"] = Rgb(94, 178, 70), ["MAT_PATH"] = Rgb(200, 202, 204),
            ["MAT_PATH_EDGE"] = Rgb(152, 156, 158), ["MAT_WOOD"] = Rgb(112, 74, 46), ["MAT_ROOF"] = Rgb(104, 84, 74),
            ["MAT_WALL"] = Rgb(224, 208, 178), ["MAT_GLASS"] = Rgb(150, 205, 235), ["MAT_STONE"] = Rgb(196, 188, 176),
            ["MAT_FLAG"] = Rgb(232, 40, 40), ["MAT_POLE"] = Rgb(240, 240, 240), ["MAT_CUP"] = Rgb(28, 28, 28), ["MAT_BALL"] = Rgb(250, 250, 250),
            // Hole 12's own: blue-grey basalt facets, cedar, the lighthouse and the flower beds.
            ["MAT_BASALT_0"] = Rgb(137, 147, 158), ["MAT_BASALT_1"] = Rgb(158, 164, 170), ["MAT_BASALT_2"] = Rgb(170, 174, 176),
            ["MAT_BASALT_3"] = Rgb(147, 158, 170), ["MAT_BASALT_4"] = Rgb(182, 180, 171), ["MAT_BARK"] = Rgb(130, 96, 64),
            ["MAT_WOOD_LIGHT"] = Rgb(199, 151, 94), ["MAT_CHALK"] = Rgb(240, 239, 220), ["MAT_SLATE"] = Rgb(66, 97, 112),
            ["MAT_FLOWER_CORAL"] = Rgb(243, 132, 147), ["MAT_FLOWER_GOLD"] = Rgb(251, 205, 80), ["MAT_FLOWER_LAVENDER"] = Rgb(184, 135, 213),
            // The pin (blender/pin.blend): the cup's liner and the band on the stick.
            ["MAT_CUP_EDGE"] = Rgb(92, 150, 58), ["MAT_POLE_BAND"] = Rgb(250, 200, 40),
        };
        static Color Rgb(int r, int g, int b) => new(r / 255f, g / 255f, b / 255f);

        /// Surfaces the ball rests on: everything the raycast should see. Trees, rocks, water and
        /// buildings are scenery.
        static readonly string[] GroundPrefixes = { "TERRAIN", "FAIRWAY", "GREEN", "TEE_BOX", "BUNKER", "CART_PATH" };
        /// The model's stand-ins for things the game draws itself at the exact pin and tee.
        static readonly string[] GameplayPlaceholders = { "FLAG", "FLAG_POLE", "HOLE_CUP", "BALL_START", "MARKER_TEE", "MARKER_PIN", "MARKER_UP" };

        GameObject model;

        /// A named node of the course model (empties included), placed in the world — null on
        /// a primitive hole or when the model has no such node.
        public Transform ModelNode(string name) => model ? FindDeep(model.transform, name) : null;

        /// The tee markers (Hole 7's pair, Hole 12's one mesh), off for a shot they would stand
        /// in front of — the crowd shot in the introductions — and back on after.
        public void ShowTeeMarkers(bool on)
        {
            foreach (var marker in teeMarkers) if (marker) marker.SetActive(on);
        }

        readonly List<GameObject> teeMarkers = new();
        static readonly Color TeeMarkerColor = new(1f, 0.8f, 0.16f), TeeMarkerBand = new(0.12f, 0.24f, 0.62f);

        /// The tee's two markers, the same on every hole: a yellow ball either side of the teeing
        /// line with a navy band round it, a little ahead of the ball and well wide of the golfer.
        /// (The models' own were white blocks the size of a suitcase, and three holes had none.)
        void PlaceTeeMarkers()
        {
            foreach (var name in new[] { "TEE_MARKER_1", "TEE_MARKER_2", "TEE_MARKERS" })
                if (ModelNode(name) is Transform node) node.gameObject.SetActive(false);
            var tee = Hole.Tee;
            var towards = Hole.RecommendedTarget(tee);
            double heading = tee.HeadingTo(towards) * System.Math.PI / 180;
            double ax = System.Math.Sin(heading), ad = System.Math.Cos(heading);
            foreach (int side in new[] { -1, 1 })
            {
                var at = new CoursePoint(tee.X + ad * 3.4 * side + ax * 1.2, tee.D - ax * 3.4 * side + ad * 1.2);
                var ball = Primitive(PrimitiveType.Sphere, $"Tee marker {(side < 0 ? "L" : "R")}", TeeMarkerColor, transform);
                ball.transform.localScale = Vector3.one * 0.46f;
                ball.transform.position = ToWorld(at, 0.2);
                var band = Primitive(PrimitiveType.Cylinder, "Band", TeeMarkerBand, ball.transform);
                band.transform.localScale = new Vector3(1.04f, 0.09f, 1.04f);
                band.transform.localPosition = Vector3.zero;
                teeMarkers.Add(ball);
            }
        }
        /// The placed course model's root: anything exported from the same Blender scene sits
        /// on the course when parented here with no transform of its own.
        public Transform ModelRoot => model ? model.transform : null;

        void BuildFromModel(GameObject prefab)
        {
            model = Instantiate(prefab, transform);
            model.name = "Course model";
            var tee = FindDeep(model.transform, "MARKER_TEE");
            var pin = FindDeep(model.transform, "MARKER_PIN");
            var up = FindDeep(model.transform, "MARKER_UP");
            if (!tee || !pin || !up)
            {
                Debug.LogError("Course model needs MARKER_TEE, MARKER_PIN and MARKER_UP; drawing the hole from primitives instead");
                Destroy(model);
                BuildGeometry();
                return;
            }

            // Place the model so its markers land on the hole's: the frame from the markers
            // (tee→pin along the hole, tee→up straight up) is mapped onto the course frame, which
            // makes the FBX axis convention irrelevant; a uniform scale takes metres to yards;
            // the slide puts the tee marker over the tee. The model's water is at its own
            // height 0 and stays at y = 0, so the island top rises above it.
            // Everything below is a world-space correction applied on top of whatever transform
            // the importer gave the root, so it composes with it rather than replacing it.
            var upM = (up.position - tee.position).normalized;
            var alongM = pin.position - tee.position;
            alongM -= upM * Vector3.Dot(alongM, upM);
            var teeC = new Vector3((float)Hole.Tee.X, 0, (float)Hole.Tee.D);
            var pinC = new Vector3((float)Hole.Pin.X, 0, (float)Hole.Pin.D);
            float scale = (pinC - teeC).magnitude / Mathf.Max(0.001f, alongM.magnitude);
            var rotation = Quaternion.LookRotation(pinC - teeC, Vector3.up) * Quaternion.Inverse(Quaternion.LookRotation(alongM, upM));
            var offset = teeC - rotation * (tee.position * scale);
            var origin = rotation * (model.transform.position * scale) + offset; // where the model's own origin (its water level) lands
            offset.y -= origin.y;
            model.transform.localScale = model.transform.localScale * scale;
            model.transform.rotation = rotation * model.transform.rotation;
            model.transform.position = rotation * (model.transform.position * scale) + offset;
            float residual = (Flat(pin.position) - pinC).magnitude;
            if (residual > 0.5f) Debug.LogWarning($"Course model pin is {residual:F2} yd off the hole's pin after alignment");
            float tilt = Vector3.Angle((up.position - tee.position), Vector3.up);
            if (tilt > 1f) Debug.LogWarning($"Course model up is {tilt:F1}° off vertical after alignment");

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    string name = mats[i].name.Replace(" (Instance)", "");
                    if (Colour(name) is Color color)
                        mats[i] = name.StartsWith("MAT_WATER") ? WaterMat(color) : Turf.TryGetValue(name, out var turf) ? TurfMat(color, turf) : Mat(color);
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = r.name.StartsWith("WATER") ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
            }
            // The model's own open sea is one quad kilometres across, which the fog paints the
            // colour of the sky; the backdrop's sea (Backdrop.Place) replaces it.
            if (FindDeep(model.transform, "WATER_OCEAN") is Transform ocean)
                foreach (var r in ocean.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            // The shallows and the surf round the shore came out of Blender facing down (it draws
            // both sides; Unity only the front), so from above they were not there: turn them up.
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
                if (mf.name.StartsWith("WATER_") && mf.sharedMesh && mf.sharedMesh.isReadable && FacesDown(mf)) FlipUp(mf);
            // Hole 12's sea comes with its swell as blendshapes and a sheet of glints; drive them.
            WaterMotion.Attach(FindDeep(model.transform, "WATER_WAVES"), FindDeep(model.transform, "WATER_GLINTS"));
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!mf.sharedMesh || !StartsWithAny(mf.name, GroundPrefixes)) continue;
                var collider = mf.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mf.sharedMesh;
                hasGround = true;
            }
            foreach (var placeholder in GameplayPlaceholders)
            {
                var t = FindDeep(model.transform, placeholder);
                if (t) t.gameObject.SetActive(false);
            }
            Physics.SyncTransforms(); // the ball is placed on this ground in the same frame
            if (hasGround) { Hole.Surface = SampleSurface(); Hole.Ground = GroundHeight; }
            // what stands on it, for the ball to run into
            Hole.Obstacles = ObstacleScan.From(model.transform);
            PlaceTeeMarkers();
            // the islands and boats out on the sea round it, past the playable ground
            var land = new Bounds(); bool any = false;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!StartsWithAny(mf.name, GroundPrefixes) || !mf.TryGetComponent(out Renderer lr)) continue;
                if (any) land.Encapsulate(lr.bounds); else { land = lr.bounds; any = true; }
            }
            if (any) Backdrop.Place(transform, land, pinC - teeC, WaterMat(Colour("MAT_WATER").Value));
        }

        /// The ground around the green as the ball will roll over it, read off the meshes the
        /// player sees (half-yard samples, well past the fringe) so the read and the break come
        /// from the same shape that was sculpted in Blender.
        HeightGrid SampleSurface()
        {
            double reach = Hole.GreenRadius + 12;
            return HeightGrid.Sample(Hole.Pin.X - reach, Hole.Pin.D - reach, 2 * reach, 2 * reach, 0.5, GroundHeight);
        }

        /// Most of the mesh's faces point at the ground.
        static bool FacesDown(MeshFilter mf)
        {
            var mesh = mf.sharedMesh; var v = mesh.vertices; var t = mesh.triangles;
            double up = 0;
            for (int i = 0; i + 2 < t.Length; i += 3)
            {
                var n = Vector3.Cross(mf.transform.TransformPoint(v[t[i + 1]]) - mf.transform.TransformPoint(v[t[i]]), mf.transform.TransformPoint(v[t[i + 2]]) - mf.transform.TransformPoint(v[t[i]]));
                up += n.y;
            }
            return up < 0;
        }

        static void FlipUp(MeshFilter mf)
        {
            var mesh = Instantiate(mf.sharedMesh);
            var t = mesh.triangles;
            for (int i = 0; i + 2 < t.Length; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);
            mesh.triangles = t;
            var n = mesh.normals;
            for (int i = 0; i < n.Length; i++) n[i] = -n[i];
            mesh.normals = n;
            mf.sharedMesh = mesh;
        }

        static Vector3 Flat(Vector3 v) => new(v.x, 0, v.z);

        static bool StartsWithAny(string name, string[] prefixes)
        {
            foreach (var p in prefixes) if (name.StartsWith(p)) return true;
            return false;
        }

        static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t;
            return null;
        }

        // ----- Primitive course -----

        void BuildGeometry()
        {
            // Rough: a big ground plane, shifted so the whole hole sits well inside it.
            var pin = ToWorld(Hole.Pin);
            var ground = Primitive(PrimitiveType.Plane, "Rough", RoughColor, transform);
            ground.transform.position = new Vector3(pin.x / 2, -0.02f, pin.z / 2);
            ground.transform.localScale = new Vector3(120, 1, 120);

            // Fairway ribbon and its fringe of lighter rough.
            AddRibbon("Fairway fringe", Hole.Centerline, Hole.FairwayWidth + 6, 0.0f, FringeColor * 0.92f);
            AddRibbon("Fairway", Hole.Centerline, Hole.FairwayWidth, 0.005f, FairwayColor);

            foreach (var h in Hole.Hazards)
            {
                var kind = h.Kind == HazardKind.Water ? "Water" : "Bunker";
                var color = h.Kind == HazardKind.Water ? new Color(0.25f, 0.55f, 0.85f) : SandColor;
                var disc = Disc(kind, (float)h.Width / 2, (float)h.Length / 2, color, 0.01f);
                disc.transform.position = new Vector3((float)h.X, 0.01f, (float)h.Distance);
            }

            var green = Disc("Green", (float)Hole.GreenRadius, (float)Hole.GreenRadius, GreenColor, 0.012f);
            green.transform.position = pin + Vector3.up * 0.012f;

            var tee = Primitive(PrimitiveType.Cube, "Tee box", TeeColor, transform);
            tee.transform.position = ToWorld(Hole.Tee) + new Vector3(0, 0.008f, 1.5f);
            tee.transform.localScale = new Vector3(7, 0.02f, 5);

            PlantTrees();
        }

        /// The flagstick and its flag, which come out while the player putts.
        Transform pinRoot, flagstick, flag;

        /// Cup and flag at the pin, on whatever ground is there: the modelled pin from
        /// Resources/Course/pin (blender/pin.blend, metres) when it is there, primitives otherwise.
        void BuildPin()
        {
            var pin = ToWorld(Hole.Pin);
            var prefab = Resources.Load<GameObject>("Course/pin");
            if (prefab)
            {
                var model = Instantiate(prefab, transform);
                model.name = "Pin";
                model.transform.position = pin;
                model.transform.localScale *= MetresToYards;
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] && Palette.TryGetValue(mats[i].name.Replace(" (Instance)", ""), out var color)) mats[i] = Mat(color);
                    r.sharedMaterials = mats;
                }
                pinRoot = model.transform;
                // The cup (blender/scripts/pin_build.py): its mouth marks the stencil and its inside
                // draws through the green there, so the hole has real depth in an uncut green. All
                // of it at the ball's scale (Hole.CupScale) and lying with the green's slope; the
                // pole and the flag stay life-size and upright.
                var slope = Physics.Raycast(pin + Vector3.up * 5f, Vector3.down, out var at, 20f, ~0, QueryTriggerInteraction.Ignore)
                    ? Quaternion.FromToRotation(Vector3.up, at.normal) : Quaternion.identity;
                foreach (var name in new[] { "CUP", "CUP_MOUTH", "CUP_EDGE" })
                {
                    var part = FindDeep(pinRoot, name);
                    if (!part) continue;
                    part.SetParent(transform, true);   // the flag turns with the wind; the hole doesn't
                    part.localScale *= (float)Hole.CupScale;
                    part.rotation = slope * part.rotation;
                    var r = part.GetComponent<Renderer>();
                    if (!r) continue;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    if (name == "CUP") r.sharedMaterial = CupMaterial("GolfArcade/HoleInside");
                    else if (name == "CUP_MOUTH") r.sharedMaterial = CupMaterial("GolfArcade/HoleMask");
                }
                flagstick = FindDeep(pinRoot, "FLAG_POLE");
                flag = FindDeep(pinRoot, "FLAG");
                if (flag) DressFlag(flag, Hole.Number);
                // the flag flutters: its four shape keys cross-faded round a loop, a ripple running
                // out to the fly a little faster than once a second
                if (flag) WaterMotion.Attach(flag, null, 1.1f, true);
                Flag = flagstick ? flagstick : pinRoot;
                return;
            }
            var cup = Disc("Cup", 0.15f, 0.15f, CupColor, 0.02f);
            cup.transform.position = pin + Vector3.up * 0.02f;
            var stick = Primitive(PrimitiveType.Cylinder, "Flagstick", Color.white, transform);
            stick.transform.position = pin + Vector3.up * 1.2f;
            stick.transform.localScale = new Vector3(0.05f, 1.2f, 0.05f);
            var cloth = Primitive(PrimitiveType.Cube, "Flag", new Color(0.9f, 0.15f, 0.15f), transform);
            cloth.transform.position = pin + new Vector3(0.5f, 2.2f, 0);
            cloth.transform.localScale = new Vector3(1f, 0.3f, 0.03f);
            flagstick = stick.transform; flag = cloth.transform;
            Flag = stick.transform;
        }

        const float MetresToYards = 1.0936f;

        static readonly Dictionary<string, Material> cupMaterials = new();
        static Material CupMaterial(string shader)
        {
            if (cupMaterials.TryGetValue(shader, out var m) && m) return m;
            var s = Shader.Find(shader);
            if (!s) { Debug.LogError($"{shader} missing from the build — run Golf Arcade → Set Up Project"); s = Shader.Find("Unlit/Color"); }
            return cupMaterials[shader] = new Material(s);
        }

        /// Tend the flag: it comes out for a putt and goes back for the next player.
        public void ShowFlag(bool on)
        {
            if (flagstick) flagstick.gameObject.SetActive(on);
            if (flag) flag.gameObject.SetActive(on);
        }

        /// Turn the flag to fly with the wind (`towardDegrees` is the heading it blows toward).
        public void SetFlagWind(double towardDegrees)
        {
            if (!pinRoot || !flag || !flagstick) return;
            var cloth = flag.GetComponentInChildren<Renderer>(true);
            if (!cloth) return;
            var flying = cloth.bounds.center - flagstick.position; flying.y = 0;
            if (flying.sqrMagnitude < 1e-6f) return;
            var wanted = new Vector3(Mathf.Sin((float)towardDegrees * Mathf.Deg2Rad), 0, Mathf.Cos((float)towardDegrees * Mathf.Deg2Rad));
            pinRoot.rotation = Quaternion.AngleAxis(Vector3.SignedAngle(flying, wanted, Vector3.up), Vector3.up) * pinRoot.rotation;
        }

        /// Tree line at the edge of the rough, so out of bounds reads at a glance.
        void PlantTrees()
        {
            var rng = new System.Random(Hole.Number * 7919);
            double edge = Hole.FairwayWidth / 2 + Hole.RoughWidth;
            for (int i = 1; i < Hole.Centerline.Length; i++)
            {
                var a = Hole.Centerline[i - 1]; var b = Hole.Centerline[i];
                double dx = b.X - a.X, dd = b.D - a.D, len = a.DistanceTo(b);
                if (len < 1) continue;
                double nx = -dd / len, nd = dx / len;
                for (double s = 0; s <= len; s += 14)
                {
                    foreach (int side in new[] { -1, 1 })
                    {
                        double jitter = rng.NextDouble() * 6;
                        var p = new CoursePoint(a.X + dx * s / len + nx * side * (edge + 4 + jitter), a.D + dd * s / len + nd * side * (edge + 4 + jitter));
                        float h = 5 + (float)rng.NextDouble() * 4;
                        var trunk = Primitive(PrimitiveType.Cylinder, "Trunk", new Color(0.35f, 0.24f, 0.12f), transform);
                        trunk.transform.position = ToWorld(p, h * 0.25);
                        trunk.transform.localScale = new Vector3(0.4f, h * 0.25f, 0.4f);
                        var crown = Primitive(PrimitiveType.Sphere, "Crown", TreeColor, transform);
                        crown.transform.position = ToWorld(p, h * 0.5 + 1.5);
                        crown.transform.localScale = Vector3.one * (h * 0.7f);
                    }
                }
            }
        }

        void AddRibbon(string name, CoursePoint[] line, double width, float y, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = new Vector3(0, y, 0);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mat(color);
            mf.sharedMesh = RibbonMesh(line, (float)width);
        }

        /// A flat strip of constant width along a polyline, with round caps and joins.
        static Mesh RibbonMesh(CoursePoint[] line, float width)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float r = width / 2;
            // Round discs at every station cover the joins and the ends.
            foreach (var p in line) AddDisc(verts, tris, ToWorld(p), r, r);
            for (int i = 1; i < line.Length; i++)
            {
                var a = ToWorld(line[i - 1]); var b = ToWorld(line[i]);
                var dir = (b - a).normalized;
                var n = new Vector3(-dir.z, 0, dir.x) * r;
                int v = verts.Count;
                verts.Add(a - n); verts.Add(a + n); verts.Add(b + n); verts.Add(b - n);
                tris.AddRange(new[] { v, v + 1, v + 2, v, v + 2, v + 3 });
            }
            var mesh = new Mesh { name = "Ribbon" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void AddDisc(List<Vector3> verts, List<int> tris, Vector3 center, float rx, float rz, int segments = 40)
        {
            int c = verts.Count;
            verts.Add(center);
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                verts.Add(center + new Vector3(Mathf.Cos(a) * rx, 0, Mathf.Sin(a) * rz));
            }
            for (int i = 1; i <= segments; i++) tris.AddRange(new[] { c, c + i + 1, c + i });
        }

        GameObject Disc(string name, float rx, float rz, Color color, float y)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var verts = new List<Vector3>(); var tris = new List<int>();
            AddDisc(verts, tris, Vector3.zero, rx, rz);
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mat(color);
            return go;
        }

        public static GameObject Primitive(PrimitiveType type, string name, Color color, Transform parent)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            var collider = go.GetComponent<Collider>();
            if (collider) Destroy(collider);
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return go;
        }

        /// The flag wears the hole's number (Resources/Course/flag_<n>, Higgsfield). The modelled flag
        /// has no UVs, so they're laid on flat across its width and height.
        static void DressFlag(Transform flag, int number)
        {
            var tex = Resources.Load<Texture2D>($"Course/flag_{number}");
            var r = flag.GetComponent<Renderer>();
            if (!tex || !r) return;
            Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : flag.GetComponent<MeshFilter>()?.sharedMesh;
            if (!mesh) return;
            mesh = Instantiate(mesh);
            var b = mesh.bounds; var v = mesh.vertices; var uv = new Vector2[v.Length];
            // across: the longest horizontal extent (pole to fly); up: y
            bool alongX = b.size.x >= b.size.z;
            for (int i = 0; i < v.Length; i++)
            {
                float across = alongX ? (v[i].x - b.min.x) / b.size.x : (v[i].z - b.min.z) / b.size.z;
                uv[i] = new Vector2(across, (v[i].y - b.min.y) / b.size.y);
            }
            mesh.uv = uv;
            if (r is SkinnedMeshRenderer sk) sk.sharedMesh = mesh; else flag.GetComponent<MeshFilter>().sharedMesh = mesh;
            tex.wrapMode = TextureWrapMode.Clamp;
            var shader = Shader.Find("Standard");
            var m = new Material(shader) { mainTexture = tex, color = Color.white, name = $"Flag {number}" };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            r.sharedMaterial = m;
        }

        /// Which of the Blender-baked turf tiles (Resources/Course/Turf, turf_textures.py) a
        /// surface wears: the tile, how many yards one covers, how strongly it shows.
        static readonly Dictionary<string, (string tile, float yards, float strength)> Turf = new()
        {
            ["MAT_FAIRWAY"] = ("fairway", 10f, 0.28f), ["MAT_FAIRWAY_STRIPE"] = ("fairway", 10f, 0.28f),
            ["MAT_FIRSTCUT"] = ("fairway", 8f, 0.32f), ["MAT_GREEN"] = ("green", 4f, 0.9f),
            ["MAT_ROUGH"] = ("rough", 6f, 0.4f), ["MAT_BUNKER_LIP"] = ("rough", 6f, 0.35f),
            ["MAT_SAND"] = ("sand", 3f, 0.45f),
        };
        static readonly Dictionary<(Color, string), Material> turfMaterials = new();

        /// Grass or sand: its palette colour with its detail tile over it (GolfArcade/Turf).
        public static Material TurfMat(Color color, (string tile, float yards, float strength) turf)
        {
            if (turfMaterials.TryGetValue((color, turf.tile), out var m) && m) return m;
            var shader = Shader.Find("GolfArcade/Turf");
            var tex = Resources.Load<Texture2D>("Course/Turf/" + turf.tile);
            if (!shader || !tex) return Mat(color);
            tex.wrapMode = TextureWrapMode.Repeat; tex.filterMode = FilterMode.Trilinear; tex.anisoLevel = 8;
            m = new Material(shader) { color = color, name = "Turf " + turf.tile };
            m.SetTexture("_Detail", tex);
            m.SetFloat("_Tile", turf.yards);
            m.SetFloat("_Strength", turf.strength);
            turfMaterials[(color, turf.tile)] = m;
            return m;
        }

        static readonly Dictionary<Color, Material> materials = new();
        static readonly Dictionary<Color, Material> waterMaterials = new();

        /// Water is the one flat colour that wants a sheen: the swell only reads where it catches
        /// the sun.
        public static Material WaterMat(Color color)
        {
            if (waterMaterials.TryGetValue(color, out var m) && m) return m;
            m = new Material(Mat(color));
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.62f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.62f);
            waterMaterials[color] = m;
            return m;
        }
        static readonly Dictionary<Color, Material> unlitMaterials = new();

        /// A flat, unshaded colour — for markers and lines that should read the same from any angle.
        public static Material UnlitMat(Color color)
        {
            if (unlitMaterials.TryGetValue(color, out var m) && m) return m;
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            unlitMaterials[color] = m;
            return m;
        }

        public static Material Mat(Color color)
        {
            if (materials.TryGetValue(color, out var m) && m) return m;
            // Both are listed in Always Included Shaders by ProjectSetup; a player build strips
            // shaders nothing references, and no asset references these.
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (!shader) Debug.LogError("Lit shader missing from the build — run Golf Arcade → Set Up Project");
            m = new Material(shader) { color = color };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            materials[color] = m;
            return m;
        }
    }
}

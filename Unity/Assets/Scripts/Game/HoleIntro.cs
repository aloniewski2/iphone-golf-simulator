using System;
using System.Collections.Generic;
using GolfArcade.Course;
using UnityEngine;

namespace GolfArcade.Game
{
    /// A hole's opening flyover: a smooth high glide down the hole under its name, then what matters on it — chosen from the
    /// hole itself — each framed so it reads, and always the green last. The highlights are scored by kind (a carry over water
    /// or lava, the windmill, a pool beside the line, the bunkers round the green or in the landing area, rocks in play, the
    /// cliff edge); a new kind of obstacle joins by giving it a score, a label and a shot here. Every camera spot is checked:
    /// nothing in the lens, nothing between it and its subject, not lifted over a tree, not on another bunker's lip.
    public sealed class HoleIntro
    {
        public const float Opening = 2.8f, OpeningShort = 2.2f, ShotSeconds = 1.7f;
        /// The flyover's length: the full cut has the opening and three highlights (two and the green), the short the green only.
        public static float Length(bool full) => full ? Opening + 3 * ShotSeconds : OpeningShort + ShotSeconds;

        public sealed class Feature { public string Kind, Label, Sub; public Vector3 At; public float Size, Score; }
        public IReadOnlyList<Feature> Highlights => highlights;

        readonly Hole hole;
        readonly List<Feature> highlights = new();
        readonly List<Vector3> path = new(); readonly List<float> dist = new();
        readonly Dictionary<Feature, Vector3> views = new();
        float length; int sea = 1; Vector3 tee, pin;

        public HoleIntro(Hole hole)
        {
            this.hole = hole;
            tee = HoleView.ToWorld(hole.Tee); pin = HoleView.ToWorld(hole.Pin);
            var pts = new List<Vector3>(); foreach (var p in hole.Centerline) pts.Add(HoleView.ToWorld(p));
            Vector3 last = pts[0];
            for (int i = 0; i <= 120; i++)
            {
                var p = Catmull(pts, i / 120f); p.y = G(p);
                if (i > 0) { var d = p - last; d.y = 0; length += d.magnitude; }
                path.Add(p); dist.Add(length); last = p;
            }
            length = Mathf.Max(1, length);
            float left = 0, right = 0;
            for (float u = .3f; u <= .8f; u += .1f)
            {
                var at = At(u); var r = Right(u);
                for (float d = 15; d <= 150; d += 15) if (Wet(at + r * d)) { right += 150 - d; break; }
                for (float d = 15; d <= 150; d += 15) if (Wet(at - r * d)) { left += 150 - d; break; }
            }
            sea = right >= left ? 1 : -1;
            Pick();
        }

        // ---------------------------------------------------------------- the line of play
        static float G(Vector3 p) => (float)HoleView.GroundHeight(HoleView.ToCourse(p));
        static CoursePoint P(Vector3 v) => new(v.x, v.z);
        bool Wet(Vector3 p) => hole.LieAt(P(p)) is CourseLie.Water or CourseLie.OutOfBounds;
        static Vector3 Catmull(List<Vector3> pts, float u)
        {
            int n = pts.Count - 1; float s = Mathf.Clamp01(u) * n; int i = Mathf.Min((int)s, n - 1); float f = s - i;
            Vector3 Q(int k) => pts[Mathf.Clamp(k, 0, n)];
            Vector3 p0 = Q(i - 1), p1 = Q(i), p2 = Q(i + 1), p3 = Q(i + 2);
            return .5f * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (-p0 + 3 * p1 - 3 * p2 + p3) * f * f * f);
        }
        Vector3 OnPath(float u)
        {
            float want = Mathf.Clamp01(u) * length; int n = 1;
            while (n < dist.Count - 1 && dist[n] < want) n++;
            return Vector3.Lerp(path[n - 1], path[n], Mathf.InverseLerp(dist[n - 1], dist[n], want));
        }
        Vector3 Fwd(float u) { var a = OnPath(u + .04f) - OnPath(u - .04f); a.y = 0; return a.sqrMagnitude > 1e-4f ? a.normalized : Vector3.forward; }
        Vector3 At(float u) => u < 0 ? path[0] - Fwd(0) * (-u * length) : u > 1 ? path[^1] + Fwd(1) * ((u - 1) * length) : OnPath(u);
        Vector3 Right(float u) => Vector3.Cross(Vector3.up, Fwd(u));
        Vector3 AtYards(float yards) => At(yards / length);
        float AlongOf(Vector3 p) { float best = 0, bd = float.MaxValue; for (int i = 0; i < path.Count; i++) { var d = path[i] - p; d.y = 0; if (d.sqrMagnitude < bd) { bd = d.sqrMagnitude; best = dist[i]; } } return best; }

        // ---------------------------------------------------------------- what matters, scored
        void Pick()
        {
            var all = new List<Feature>();
            float wetFrom = -1, longestFrom = 0, longest = 0;
            for (float d = 4; d < length - 4; d += 2)
            {
                bool wet = hole.LieAt(P(AtYards(d))) == CourseLie.Water;
                if (wet && wetFrom < 0) wetFrom = d;
                if (!wet && wetFrom >= 0) { if (d - wetFrom > longest) { longest = d - wetFrom; longestFrom = wetFrom; } wetFrom = -1; }
            }
            if (longest > 15)
            {
                var mid = AtYards(longestFrom + longest / 2); bool lava = hole.HazardAt(P(mid)) == HazardKind.Lava || hole.SeaIsLava;
                all.Add(new Feature { Kind = lava ? "lavacarry" : "carry", Label = lava ? "LAVA CARRY" : "WATER CARRY", Sub = $"{longest:F0} YD TO CLEAR", At = mid, Size = longest, Score = 100 });
            }
            if (hole.Windmill != null)
                all.Add(new Feature { Kind = "windmill", Label = "THE WINDMILL", Sub = "TIME THE SAILS", At = new Vector3((float)hole.Windmill.HubX, (float)hole.Windmill.HubY, (float)hole.Windmill.HubD), Size = (float)hole.Windmill.Reach, Score = 95 });
            var greenside = new List<Vector3>(); var fairway = new List<Vector3>(); float fairwayAlong = 0;
            foreach (var h in hole.Hazards)
            {
                var c = new Vector3((float)h.X, 0, (float)h.Distance); c.y = G(c);
                float toPin = Vector2.Distance(new Vector2(c.x, c.z), new Vector2(pin.x, pin.z)); float off = (float)hole.DistanceFromCenterline(P(c)); float along = AlongOf(c);
                if (h.Kind == HazardKind.Bunker)
                {
                    if (toPin < hole.GreenRadius + 34) greenside.Add(c);
                    else if (off < hole.FairwayWidth / 2 + 22 && along > 110) { fairway.Add(c); fairwayAlong = fairway.Count == 1 ? along : Mathf.Min(fairwayAlong, along); }
                }
                else if (h.Kind is HazardKind.Water or HazardKind.Lava && off < h.Width / 2 + 30)
                    all.Add(new Feature { Kind = h.Kind == HazardKind.Lava ? "lava" : "pool", Label = h.Kind == HazardKind.Lava ? "LAVA" : "WATER", Sub = $"{along:F0} YD  ·  {(Vector3.Dot(c - AtYards(along), Right(along / length)) > 0 ? "RIGHT" : "LEFT")}", At = c, Size = (float)Math.Max(h.Width, h.Length), Score = 80 });
            }
            Vector3 Nearest(List<Vector3> v) { var best = v[0]; double bd = double.MaxValue; foreach (var x in v) { double d = hole.DistanceFromCenterline(P(x)); if (d < bd) { bd = d; best = x; } } return best; }
            if (greenside.Count > 0) all.Add(new Feature { Kind = "greenside", Label = greenside.Count > 1 ? "GREENSIDE BUNKERS" : "GREENSIDE BUNKER", Sub = greenside.Count > 1 ? $"{greenside.Count} GUARD THE GREEN" : "GUARDS THE GREEN", At = Nearest(greenside), Size = 18, Score = 55 + 5 * greenside.Count });
            if (fairway.Count > 0) all.Add(new Feature { Kind = "fairway", Label = fairway.Count > 1 ? "FAIRWAY BUNKERS" : "FAIRWAY BUNKER", Sub = $"FROM {fairwayAlong:F0} YD", At = Nearest(fairway), Size = 22, Score = 50 + 5 * fairway.Count });
            int rocks = 0; var rockAt = Vector3.zero;
            foreach (var o in hole.Obstacles)
                if (o.Kind is ObstacleKind.Rock or ObstacleKind.Wall && hole.DistanceFromCenterline(new CoursePoint(o.X, o.D)) < hole.FairwayWidth / 2 + 8) { rocks++; rockAt += new Vector3((float)o.X, (float)o.Base, (float)o.D); }
            if (rocks > 0) all.Add(new Feature { Kind = "rocks", Label = "ROCKS IN PLAY", Sub = rocks > 1 ? $"{rocks} ON THE LINE" : "ON THE LINE", At = rockAt / rocks, Size = 12, Score = 45 });
            if (longest <= 15)
            {
                float closest = float.MaxValue; Vector3 edge = Vector3.zero; float edgeAlong = 0; int edgeSide = 1;
                for (float d = length * .35f; d < length * .95f; d += 8)
                    foreach (int side in new[] { -1, 1 })
                        for (float o = 10; o < 60; o += 5)
                        {
                            var q = AtYards(d) + Right(d / length) * side * o;
                            if (hole.LieAt(P(q)) == CourseLie.Water) { if (o < closest) { closest = o; edge = q; edgeAlong = d; edgeSide = side; } break; }
                        }
                if (closest < 50) { edge.y = G(AtYards(edgeAlong)); all.Add(new Feature { Kind = "edge", Label = hole.SeaIsLava ? "LAVA EDGE" : "CLIFF EDGE", Sub = $"{(hole.SeaIsLava ? "LAVA" : "THE SEA")} {(edgeSide > 0 ? "RIGHT" : "LEFT")}", At = edge, Size = 30, Score = 45 }); }
            }
            all.Sort((a, b) => b.Score.CompareTo(a.Score));
            foreach (var f in all)
            {
                if (highlights.Count == 2) break;
                bool near = Vector3.Distance(f.At, pin) < 22 && f.Kind != "greenside";
                foreach (var g in highlights) near |= Vector3.Distance(f.At, g.At) < 40;
                if (!near) highlights.Add(f);
            }
            // a hole with less in play still gets its view: out over the scenery beside the landing area
            while (highlights.Count < 2)
            {
                float d = length * (highlights.Count == 0 ? .5f : .75f);
                highlights.Add(new Feature { Kind = "vista", Label = hole.Name.ToUpperInvariant(), Sub = $"{d:F0} YD OUT", At = AtYards(d), Size = 30, Score = 0 });
            }
            highlights.Sort((a, b) => AlongOf(a.At).CompareTo(AlongOf(b.At)));
            float rise = G(pin) - G(tee);
            highlights.Add(new Feature { Kind = "green", Label = "THE GREEN", Sub = rise > 4 ? $"UPHILL  ·  +{rise:F0} YD" : rise < -4 ? $"DOWNHILL  ·  {rise:F0} YD" : $"PAR {hole.Par}  ·  {hole.Length:F0} YD", At = pin, Size = (float)hole.GreenRadius, Score = 0 });
        }

        // ---------------------------------------------------------------- the shots
        /// The flyover's shot at `t`: 0 is the opening, then the highlights in order (the short cut: the opening, the green).
        public int ShotAt(bool full, float t, out float local, out float len)
        {
            float open = full ? Opening : OpeningShort;
            if (t < open) { local = t; len = open; return 0; }
            int i = Mathf.Min((int)((t - open) / ShotSeconds), full ? highlights.Count - 1 : 0);
            local = t - open - i * ShotSeconds; len = ShotSeconds;
            return full ? i + 1 : highlights.Count;   // (the short cut's one highlight is the green)
        }
        /// The highlight a shot frames (null for the opening).
        public Feature FeatureOf(int shot) => shot <= 0 ? null : highlights[Mathf.Clamp(shot - 1, 0, highlights.Count - 1)];

        /// The lens `t` into the flyover. `aspect` widens the lens on a portrait screen.
        public void Pose(bool full, float t, float aspect, out Vector3 position, out Vector3 look, out float fov)
        {
            int shot = ShotAt(full, t, out float s, out float len);
            float k = Mathf.SmoothStep(0, 1, s / len);
            if (shot == 0)
            {
                // high and smooth down the hole from behind the tee, the scenery wide on one side
                float e = Smoother(s / len); float u = Mathf.Lerp(-.05f, full ? .55f : .7f, e);
                position = Above(At(u) - Fwd(Mathf.Clamp01(u)) * 30 + Right(Mathf.Clamp01(u)) * -sea * 26 + Vector3.up * Mathf.Lerp(46, 34, e), 20);
                look = At(Mathf.Min(1, u + .35f)) + Vector3.up * 2; fov = 44;
            }
            else FeaturePose(FeatureOf(shot), k, out position, out look, out fov);
            Handheld(ref look, position, t);
            if (aspect < 1) fov = Mathf.Min(78, fov * Mathf.Lerp(1.6f, 1, aspect));
        }
        static float Smoother(float x) { x = Mathf.Clamp01(x); return x * x * x * (x * (x * 6 - 15) + 10); }
        static Vector3 Above(Vector3 p, float clear) { p.y = Mathf.Max(p.y, G(p) + clear); return p; }
        /// A camera operator's hold: a fraction of a degree of drift.
        static void Handheld(ref Vector3 look, Vector3 from, float t)
        {
            var to = look - from; float d = to.magnitude; if (d < .01f) return;
            var right = Vector3.Cross(Vector3.up, to).normalized; var up = Vector3.Cross(to, right).normalized;
            look += right * d * Mathf.Tan((Mathf.PerlinNoise(t * .55f, 1.3f) - .5f) * .6f * Mathf.Deg2Rad) + up * d * Mathf.Tan((Mathf.PerlinNoise(2.1f, t * .5f) - .5f) * .4f * Mathf.Deg2Rad);
        }

        void FeaturePose(Feature f, float k, out Vector3 position, out Vector3 look, out float fov)
        {
            var toPin = pin - f.At; toPin.y = 0; toPin = toPin.sqrMagnitude > 1 ? toPin.normalized : Fwd(.9f);
            var side = Vector3.Cross(Vector3.up, toPin);
            float onFairway = Mathf.Sign(Vector3.Dot(AtYards(AlongOf(f.At)) - f.At, side)); if (onFairway == 0) onFairway = 1;
            switch (f.Kind)
            {
                case "carry":
                case "lavacarry":
                {
                    // skimming low over the water toward the far bank and the green on it
                    float from = AlongOf(f.At) - f.Size / 2;
                    position = Above(AtYards(from + f.Size * Mathf.Lerp(-.15f, .25f, k)) + Right(.5f) * 10 + Vector3.up * 3.2f, 2.5f);
                    look = AtYards(from + f.Size) + Vector3.up * 2; fov = 40; return;
                }
                case "windmill":
                {
                    float a = Mathf.Lerp(-20, 15, k) * Mathf.Deg2Rad; var back = -toPin;
                    position = f.At + (back * Mathf.Cos(a) + side * Mathf.Sin(a)) * f.Size * 3.2f + Vector3.up * .5f; look = f.At; fov = 38; return;
                }
                case "greenside":
                {
                    // raised behind the sand: it sits low in the frame with the green and its flag beyond
                    var away = f.At - pin; away.y = 0; away = away.sqrMagnitude > 1 ? away.normalized : -toPin;
                    look = Vector3.Lerp(f.At, pin, .7f) + Vector3.up * .6f;
                    position = Placed(f, look, (away + Vector3.Cross(Vector3.up, away) * .35f).normalized, 19, k, 5.5f, 3.5f); fov = 36; return;
                }
                case "edge":
                case "vista":
                {
                    // from out over the drop (or the rough), the fairway along the top and the view beyond
                    var land = AtYards(AlongOf(f.At)); var outward = f.Kind == "edge" ? f.At - land : Right(AlongOf(f.At) / length) * sea; outward.y = 0; outward = outward.normalized;
                    position = (f.Kind == "edge" ? f.At : land) + outward * Mathf.Lerp(34, 28, k) - toPin * 10; position.y = G(land) + 6;
                    position = Above(position, 4); look = land + toPin * 30 + Vector3.up; fov = 42; return;
                }
                case "green":
                {
                    // low, long lens, a touch down onto the flag with the view behind it
                    var approach = -Fwd(.97f); var across = Vector3.Cross(Vector3.up, approach);
                    look = pin + Vector3.up * .9f;
                    position = Placed(f, look, (approach * .7f + across * -sea * .7f).normalized, 12, k, 2.1f, 1.6f); fov = 28; return;
                }
                default:
                {
                    // fairway bunkers, pools, lava, rocks: low on the fairway side, past them toward the green
                    look = Vector3.Lerp(f.At, pin, .2f) + Vector3.up * .8f;
                    position = Placed(f, look, (-toPin + side * onFairway * .45f).normalized, f.Size * 1.1f + 16, k, 3.4f, 2.4f); fov = 40; return;
                }
            }
        }
        Vector3 Placed(Feature f, Vector3 look, Vector3 dir, float distance, float k, float height, float clear)
        {
            if (!views.TryGetValue(f, out var d)) views[f] = d = ClearView(f.At, look, dir, distance, height, clear);
            var p = f.At + d * distance * Mathf.Lerp(1, .85f, k); p.y = Mathf.Max(f.At.y + height, G(p) + clear);
            return p;
        }
        /// Of the directions round `subject`, the first (nearest the preferred one) from which the lens stays clear all through
        /// its push and nothing stands between it and what it looks at.
        Vector3 ClearView(Vector3 subject, Vector3 look, Vector3 dir, float distance, float height, float clear)
        {
            foreach (float a in new[] { 0f, 30, -30, 55, -55, 80, -80, 120, -120, 180 })
            {
                var d = Quaternion.AngleAxis(a, Vector3.up) * dir; bool ok = true;
                for (float k = 0; k <= 1.001f && ok; k += .5f)
                {
                    var p = subject + d * distance * Mathf.Lerp(1, .85f, k); p.y = Mathf.Max(subject.y + height, G(p) + clear);
                    if (p.y - Mathf.Max(subject.y, G(p)) > height + 6) { ok = false; break; }   // lifted over a tree: elsewhere
                    if (Physics.CheckSphere(p, .7f, ~0, QueryTriggerInteraction.Ignore) || NearSand(p, subject)) { ok = false; break; }
                    var to = look - p;
                    if (Physics.Raycast(p, to.normalized, to.magnitude - 2.5f, ~0, QueryTriggerInteraction.Ignore)) ok = false;
                }
                if (ok) return d;
            }
            return dir;
        }
        /// The lens over or beside another bunker's lip (bunker faces have no colliders).
        bool NearSand(Vector3 p, Vector3 subject)
        {
            var flat = new Vector2(subject.x, subject.z);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4; var q = p + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 5;
                if (hole.HazardAt(P(q)) == HazardKind.Bunker && Vector2.Distance(new Vector2(q.x, q.z), flat) > 8) return true;
            }
            return false;
        }

        /// The tag's colour for each kind.
        public static Color ColourOf(string kind) => kind switch
        {
            "carry" or "pool" or "edge" or "vista" => GolfArcade.UI.IntroGraphics.Sky,
            "lava" or "lavacarry" => GolfArcade.UI.IntroGraphics.Lava,
            "greenside" or "fairway" => GolfArcade.UI.IntroGraphics.Sun,
            "windmill" => GolfArcade.UI.IntroGraphics.Coral,
            "rocks" => GolfArcade.UI.IntroGraphics.Grape,
            _ => GolfArcade.UI.IntroGraphics.Grass,
        };
    }
}

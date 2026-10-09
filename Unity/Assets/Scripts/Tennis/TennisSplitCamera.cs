using System;
using System.Collections.Generic;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Where the two cameras of the split screen go. Each half of the TV shows the whole court from behind one player's baseline; the
    /// far player's camera is the near player's turned half way round the middle of the court, so both players see their own end in
    /// front of them and the same court. The placement is solved from the half's own shape (width over height): a half is nearly
    /// square, and it must keep the court's width and both players in view, which a TV-wide camera never had to.
    ///
    /// Pure math (no Camera), so the fit is tested for every shape a half can have: see SplitScreenFramingTests.
    public static class TennisSplitCamera
    {
        public struct Placement { public Vector3 position, look; public float fov; public float courtArea; }

        /// The court and both players stay this far (a fraction of the half) inside its edge.
        public const float Margin = .04f;
        /// What must stay in view: the court with its alleys and the room a player runs and serves in beyond the baselines
        /// (HalfWidth, HalfLength), and a player's head (HeadHeight) at the widest a player runs (PlayerReach).
        public const float HalfWidth = 5.5f, HalfLength = 12.9f, HeadHeight = 2.3f, PlayerReach = 4.9f;
        /// The singles court itself: its projected size is what the placement tries to make large.
        public const float SinglesHalfWidth = TennisRules.CourtHalfWidth, SinglesHalfLength = TennisRules.CourtHalfLength;
        public const float MinHeight = 7f, MaxHeight = 11f;

        /// Points that must be inside the picture, in the near player's frame (the court is symmetrical, so the far player's
        /// frame needs the same set).
        public static IReadOnlyList<Vector3> MustFit { get; } = new[]
        {
            new Vector3(-HalfWidth, 0, -HalfLength), new Vector3(HalfWidth, 0, -HalfLength),
            new Vector3(-HalfWidth, 0, HalfLength), new Vector3(HalfWidth, 0, HalfLength),
            new Vector3(-PlayerReach, HeadHeight, -HalfLength), new Vector3(PlayerReach, HeadHeight, -HalfLength),
            new Vector3(-PlayerReach, HeadHeight, HalfLength), new Vector3(PlayerReach, HeadHeight, HalfLength),
        };

        static readonly Dictionary<int, Placement> cache = new Dictionary<int, Placement>();

        /// The camera behind the near baseline (seat 0's end).
        public static Placement Near(float aspect)
        {
            int key = (int)Math.Round(aspect * 1000);
            if (cache.TryGetValue(key, out var found)) return found;
            return cache[key] = Solve(key / 1000.0);
        }

        /// The camera for a seat: seat 1's is seat 0's turned half way round the middle of the court.
        public static Placement For(int seat, float aspect)
        {
            var near = Near(aspect);
            return seat == 0 ? near : Turn(near);
        }

        public static Placement Turn(Placement p) => new Placement
        {
            position = new Vector3(-p.position.x, p.position.y, -p.position.z),
            look = new Vector3(-p.look.x, p.look.y, -p.look.z),
            fov = p.fov, courtArea = p.courtArea,
        };

        /// Where a world point lands in a camera's picture: x and y in -1..1 across the picture, depth along the view. False when behind it.
        public static bool Project(Placement camera, float aspect, Vector3 point, out float x, out float y, out float depth)
        {
            double px = camera.position.x, py = camera.position.y, pz = camera.position.z;
            double fx = camera.look.x - px, fy = camera.look.y - py, fz = camera.look.z - pz;
            double fl = Math.Sqrt(fx * fx + fy * fy + fz * fz); fx /= fl; fy /= fl; fz /= fl;
            // Unity is left-handed: right = up x forward.
            double rx = fz, ry = 0, rz = -fx; double rl = Math.Sqrt(rx * rx + rz * rz); rx /= rl; rz /= rl;
            double ux = fy * rz - fz * ry, uy = fz * rx - fx * rz, uz = fx * ry - fy * rx;
            double vx = point.x - px, vy = point.y - py, vz = point.z - pz;
            double d = vx * fx + vy * fy + vz * fz;
            depth = (float)d;
            double tanV = Math.Tan(camera.fov * Math.PI / 360), tanH = tanV * aspect;
            x = y = 0;
            if (d < .1) return false;
            x = (float)((vx * rx + vz * rz) / (d * tanH));
            y = (float)((vx * ux + vy * uy + vz * uz) / (d * tanV));
            return true;
        }

        /// True when every point that must be in view is inside the picture with the margin to spare.
        public static bool Fits(Placement camera, float aspect)
        {
            foreach (var point in MustFit)
                if (!Project(camera, aspect, point, out float x, out float y, out _) || Math.Abs(x) > 1 - Margin || Math.Abs(y) > 1 - Margin) return false;
            return true;
        }

        /// Size of the singles court in the picture (the area it covers, 0..4), the quantity the placement maximizes.
        public static float CourtArea(Placement camera, float aspect)
        {
            var corners = new[] { new Vector3(-SinglesHalfWidth, 0, -SinglesHalfLength), new Vector3(SinglesHalfWidth, 0, -SinglesHalfLength),
                                  new Vector3(SinglesHalfWidth, 0, SinglesHalfLength), new Vector3(-SinglesHalfWidth, 0, SinglesHalfLength) };
            var xs = new float[4]; var ys = new float[4];
            for (int i = 0; i < 4; i++) if (!Project(camera, aspect, corners[i], out xs[i], out ys[i], out _)) return 0;
            float twice = 0;
            for (int i = 0; i < 4; i++) { int j = (i + 1) % 4; twice += xs[i] * ys[j] - xs[j] * ys[i]; }
            return Math.Abs(twice) / 2;
        }

        // A few hundred metres of searching, once per shape of half: the biggest court that still fits, within sensible heights.
        static Placement Solve(double aspect)
        {
            Placement best = default; float bestArea = -1;
            foreach (float fov in new[] { 46f, 50f, 54f, 58f })
                for (float height = MinHeight; height <= MaxHeight; height += 1)
                    for (float back = 0; back <= 45; back += .5f)
                        for (float aim = -4; aim <= 6; aim += 2)
                        {
                            var candidate = new Placement { position = new Vector3(0, height, -(HalfLength + back)), look = new Vector3(0, 0, aim), fov = fov };
                            if (!Fits(candidate, (float)aspect)) continue;
                            float area = CourtArea(candidate, (float)aspect);
                            if (area > bestArea) { bestArea = area; best = candidate; best.courtArea = area; }
                        }
            if (bestArea < 0)   // no shape this thin or wide is expected; stay safe rather than nowhere
                best = new Placement { position = new Vector3(0, MaxHeight, -(HalfLength + 45)), look = Vector3.zero, fov = 58f };
            return best;
        }
    }
}

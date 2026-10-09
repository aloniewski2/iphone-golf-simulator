using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using GolfArcade.Tennis;

namespace GolfArcade.Tests {
    /// The split screen shows the whole court to each player in half of the TV. A half is nearly square, so the camera has to be placed
    /// to keep the court's width and both players in view; these tests check that for every shape of half a display can give, with no
    /// Unity (they use only the pure framing math).
    public class SplitScreenFramingTests {
        static float[] Shapes() { var list = new System.Collections.Generic.List<float>(); for (float a = .60f; a <= 1.2001f; a += .02f) list.Add(a); return list.ToArray(); }

        [Test] public void EveryShapeOfHalfKeepsTheWholeCourtAndBothPlayersInViewForBothPlayers() {
            foreach (float aspect in Shapes())
                for (int seat = 0; seat < 2; seat++) {
                    var camera = TennisSplitCamera.For(seat, aspect);
                    foreach (var near in TennisSplitCamera.MustFit) {
                        // Seat 1's picture must hold the same set as seen from its own end: the set is turned with the camera.
                        var point = seat == 0 ? near : new Vector3(-near.x, near.y, -near.z);
                        Assert.IsTrue(TennisSplitCamera.Project(camera, aspect, point, out float x, out float y, out float depth), $"seat {seat}, shape {aspect:F2}: {point.x},{point.y},{point.z} is behind the camera");
                        Assert.LessOrEqual(Math.Abs(x), 1 - TennisSplitCamera.Margin + 1e-4f, $"seat {seat}, shape {aspect:F2}: x {x:F3} for {point.x},{point.y},{point.z}");
                        Assert.LessOrEqual(Math.Abs(y), 1 - TennisSplitCamera.Margin + 1e-4f, $"seat {seat}, shape {aspect:F2}: y {y:F3} for {point.x},{point.y},{point.z}");
                    }
                }
        }

        [Test] public void TheFarPlayersCameraIsTheNearOnesTurnedHalfWayRound() {
            foreach (float aspect in Shapes()) {
                var a = TennisSplitCamera.For(0, aspect); var b = TennisSplitCamera.For(1, aspect);
                Assert.AreEqual(-a.position.z, b.position.z, 1e-4); Assert.AreEqual(-a.position.x, b.position.x, 1e-4); Assert.AreEqual(a.position.y, b.position.y, 1e-4);
                Assert.AreEqual(a.fov, b.fov, 1e-4);
                // A point seen from one end, turned half way round, lands at the same place in the other camera's picture.
                foreach (var p in TennisSplitCamera.MustFit) {
                    TennisSplitCamera.Project(a, aspect, p, out float x0, out float y0, out _);
                    TennisSplitCamera.Project(b, aspect, new Vector3(-p.x, p.y, -p.z), out float x1, out float y1, out _);
                    Assert.AreEqual(x0, x1, 1e-3); Assert.AreEqual(y0, y1, 1e-3);
                }
            }
        }

        [Test] public void TheCourtIsBigEnoughToPlayOnAndTheCameraIsNeverLowOrSilly() {
            foreach (float aspect in Shapes().Where(a => a >= .75f && a <= 1.0f)) {
                var c = TennisSplitCamera.For(0, aspect);
                Assert.GreaterOrEqual(c.position.y, TennisSplitCamera.MinHeight - 1e-4f); Assert.LessOrEqual(c.position.y, TennisSplitCamera.MaxHeight + 1e-4f);
                // The near baseline of the singles court should span more than half of the picture's width...
                TennisSplitCamera.Project(c, aspect, new Vector3(TennisRules.CourtHalfWidth, 0, -TennisRules.CourtHalfLength), out float nearX, out float nearY, out _);
                Assert.GreaterOrEqual(nearX, .5f, $"shape {aspect:F2}: the court's near edge is only {nearX:F2} of half the width");
                // ...and the court should use at least a third of the picture's height.
                TennisSplitCamera.Project(c, aspect, new Vector3(TennisRules.CourtHalfWidth, 0, TennisRules.CourtHalfLength), out _, out float farY, out _);
                Assert.GreaterOrEqual(farY - nearY, .6f, $"shape {aspect:F2}: the court is only {(farY - nearY) / 2:P0} of the height");
            }
        }

        [Test] public void TheNumbersForTheShapesATVActuallyGives() {
            // 16:9 TV, a half is 960x1080 (0.89); 16:10 (a MacBook over AirPlay) 720x900 (0.8); a window nearly square (1.0).
            foreach (var (name, aspect) in new[] { ("16:9 half", 960f / 1080), ("16:10 half", 720f / 900), ("square", 1f) }) {
                var c = TennisSplitCamera.Near(aspect);
                TestContext.Progress.WriteLine($"{name} ({aspect:F3}): height {c.position.y:F1} m, {(-c.position.z - TennisSplitCamera.HalfLength):F1} m behind the baseline, looking at z {c.look.z:F0}, fov {c.fov:F0}, court area {c.courtArea:F2}");
                Assert.IsTrue(TennisSplitCamera.Fits(c, aspect));
            }
        }
    }
}

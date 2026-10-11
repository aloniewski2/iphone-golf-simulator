using System;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;

namespace GolfArcade.Tests
{
    /// Rally length for a phone player (auto-run, no steering input) against the rival's real decision code.
    ///
    /// The rally used to end after two shots against the stronger rivals: the character reacted a quarter second late, took
    /// most of a second to get up to speed, and the sprint boost only came with a lean that a phone-motion player never
    /// gives. These tests keep the opening of a point playable at every rung of the ladder. The model is deliberately
    /// plain (a human who is a little late now and then and misses one ball in twelve): it checks the reach and pace balance,
    /// not the feel, which still needs a phone and a TV.
    public class TennisRallyBalanceTests
    {
        const float StrikeZ = 10.55f;

        static float TimeToPlane(Vector3 position, Vector3 velocity, float spin, float planeZ, bool towardsNegative, out Vector3 at)
        {
            float t = 0;
            for (int i = 0; i < 1200; i++)
            {
                TennisBall.Integrate(ref position, ref velocity, spin, TennisBall.Step);
                if (position.y < TennisRules.BallRadius && velocity.y < 0)
                { position.y = TennisRules.BallRadius; TennisBall.Bounce(ref velocity, ref spin, .75f); }
                t += TennisBall.Step;
                if (towardsNegative ? position.z <= planeZ : position.z >= planeZ) break;
            }
            at = position; return t;
        }

        // The same arc TennisAbilities.LobVelocity flies (kept here so this file needs nothing from the game's Unity classes).
        static Vector3 Lob(Vector3 start, Vector3 target, float apex)
        {
            apex = Mathf.Max(apex, start.y + .5f);
            float up = Mathf.Sqrt(2 * 9.81f * (apex - start.y));
            float duration = up / 9.81f + Mathf.Sqrt(2 * (apex - target.y) / 9.81f);
            Vector3 v = (target - start) / duration; v.y = up; return v;
        }

        public struct Summary { public float MeanRally, ShareAtLeastSix; }

        /// Points are opened by a rival rally ball; `shots` counts every ball struck by either side.
        static Summary Simulate(OpponentProfile rival, bool autoJump, int seed, int points = 1500)
        {
            var rng = new System.Random(seed);
            float R() => (float)rng.NextDouble();
            float Normal() { double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble(); return (float)(Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2)); }
            long total = 0; int atLeastSix = 0;
            for (int point = 0; point < points; point++)
            {
                int shots = 0; float humanX = (R() - .5f) * 1.2f, rivalX = 0, humanSpeedAcross = 0;
                float incomingPace = .3f, incomingQuality = .4f, crossingX = 0; bool lastLob = false;
                while (true)
                {
                    float ballX = shots == 0 ? (R() * 2 - 1) * 1.5f : crossingX;
                    var reply = TennisOpponent.Decide(rivalX, ballX, humanX, humanSpeedAcross, 0, rival, R(), R(), R(),
                        incomingPace, rival.Reach, 0, incomingQuality, false, TennisRules.BaselineZ, lastLob, TennisOpponent.RallyEase(shots + 1));
                    if (!reply.Reached || reply.Error) break;                       // the rival loses the point
                    var start = new Vector3(ballX, 1.1f, StrikeZ);
                    Vector3 velocity = reply.Kind == TennisReturnKind.Lob ? Lob(start, reply.Landing, 4.4f)
                        : TennisRules.RallyArcVelocity(start, reply.Landing, reply.Speed, reply.Spin, reply.Kind == TennisReturnKind.Drop ? .18f : .45f);
                    lastLob = reply.Kind == TennisReturnKind.Lob;
                    shots++;
                    // The human reads it from where they stand: a plain run first, and the sprint boost if that cannot get there.
                    var me = new Vector2(humanX, TennisRules.BaselineZ);
                    var plan = TennisRules.PlanIntercept(start, velocity, reply.Spin, .75f, me, TennisRules.ReactionTime, TennisRules.RunSpeed, false);
                    if (autoJump && TennisRules.EarnsAutoJump(plan))
                        plan = TennisRules.PlanIntercept(start, velocity, reply.Spin, .75f, me, TennisRules.JumpReaction, TennisRules.SprintSpeed, false);
                    bool whiff = R() < .08f;                                         // a swing that simply misses
                    if (!plan.Found || !plan.Reachable || whiff) break;              // the human loses the point
                    humanSpeedAcross = (plan.Point.x - humanX) / Mathf.Max(.3f, plan.Time);
                    humanX = plan.Point.x;
                    // How late the swing meets the ball (signed: early is negative), with the odd badly mistimed one.
                    float late = Normal() * .075f;
                    if (R() < .08f) late = (R() < .5f ? -1 : 1) * Mathf.Lerp(.15f, .33f, R());
                    var hit = TennisRules.AssistedHit(Mathf.Abs(late), Mathf.Lerp(.3f, .65f, R()), 1, .65f, 1);
                    incomingQuality = hit.Quality;
                    var target = TennisRules.TimingPlacement(late, .65f, hit.Quality, R() * 2 - 1, R() * 2 - 1);
                    var strikeAt = new Vector3(humanX, 1.1f, -StrikeZ);
                    var shot = TennisRules.RallyVelocity(strikeAt, target, hit.Speed, .35f, hit.Quality);
                    incomingPace = Mathf.InverseLerp(16, 32, new Vector2(shot.x, shot.z).magnitude);
                    float flight = TimeToPlane(strikeAt, shot, .35f, StrikeZ, false, out var atRival);
                    crossingX = atRival.x;
                    // The rival waits for its reaction, then runs toward the ball's crossing point.
                    float goal = Mathf.Clamp(crossingX, -TennisRules.CourtHalfWidth - .8f, TennisRules.CourtHalfWidth + .8f);
                    rivalX = Mathf.MoveTowards(0, goal, Mathf.Max(0, flight - rival.Reaction) * rival.Speed * TennisRules.CourtMovementScale);
                    shots++;
                    // The human drifts back toward the middle while the ball is in the air.
                    humanX = Mathf.MoveTowards(humanX, 0, flight * TennisRules.RunSpeed * .75f);
                }
                total += shots; if (shots >= 6) atLeastSix++;
            }
            return new Summary { MeanRally = total / (float)points, ShareAtLeastSix = atLeastSix / (float)points };
        }

        static readonly (string name, OpponentProfile profile)[] Ladder =
        {
            ("club", OpponentProfile.FromDifficulty(0f)),
            ("standard", OpponentProfile.FromDifficulty(TennisOpponent.DefaultDifficulty)),
            ("hard", OpponentProfile.FromDifficulty(.8f)),
            ("pro", OpponentProfile.FromDifficulty(1f)),
            ("boss", OpponentProfile.Rival("boss", 1f)),
        };

        [Test] public void APhonePlayerGetsARallyGoingAgainstEveryRung()
        {
            var report = "";
            foreach (var (name, profile) in Ladder)
            {
                var s = Simulate(profile, autoJump: true, seed: 17);
                report += $"{name}: mean {s.MeanRally:0.0}, {s.ShareAtLeastSix:P0} reach six shots   ";
                Assert.GreaterOrEqual(s.MeanRally, 8f, $"{name} rally length: {report}");
                Assert.GreaterOrEqual(s.ShareAtLeastSix, .5f, $"{name} share of rallies that reach six shots: {report}");
            }
            TestContext.WriteLine(report);
        }

        [Test] public void TheAutoJumpIsWhatKeepsTheHardRivalsPlayable()
        {
            // Without the rescue the sprint boost never reaches a phone player; the hardest rivals then end most points on
            // the first ball. This pins down why the assist exists, so it is not removed as "free help".
            var boss = OpponentProfile.Rival("boss", 1f);
            var assisted = Simulate(boss, true, 23);
            var unassisted = Simulate(boss, false, 23);
            TestContext.WriteLine($"boss: with the auto-jump {assisted.MeanRally:0.0} shots, without {unassisted.MeanRally:0.0}");
            Assert.Greater(assisted.MeanRally, unassisted.MeanRally + 2f);
        }
    }
}

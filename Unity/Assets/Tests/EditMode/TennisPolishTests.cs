using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;

/// Tests for the quality pass: spin, serve variety, frame pacing, quality tiers, match
/// moments and the rally balance the opponent is tuned to.
public class TennisPolishTests
{
    // --- Spin ---------------------------------------------------------------------------

    [Test] public void TopspinDipsAndSliceFloats()
    {
        Vector3 start = new Vector3(0, 1, -10), launch = new Vector3(0, 4, 22);
        Assert.IsTrue(TennisBall.Landing(start, launch, 0, out var flat, out _));
        Assert.IsTrue(TennisBall.Landing(start, launch, 1, out var top, out _));
        Assert.IsTrue(TennisBall.Landing(start, launch, -1, out var slice, out _));
        Assert.Less(top.z, flat.z - 1, "topspin must pull the ball down into the court");
        Assert.Greater(slice.z, flat.z + .5f, "slice must hold the ball up");
    }

    [Test] public void SpinChangesTheBounce()
    {
        Vector3 incoming = new Vector3(0, -8, 20);
        Vector3 top = incoming, flat = incoming, slice = incoming;
        float s1 = 1, s0 = 0, s2 = -1;
        TennisBall.Bounce(ref top, ref s1, .75f); TennisBall.Bounce(ref flat, ref s0, .75f); TennisBall.Bounce(ref slice, ref s2, .75f);
        Assert.Greater(top.y, flat.y, "topspin kicks up"); Assert.Less(slice.y, flat.y, "slice stays low");
        Assert.Greater(top.z, flat.z, "topspin jumps forward"); Assert.Less(slice.z, flat.z, "slice skids and slows");
        Assert.Less(Mathf.Abs(s1), 1, "spin is partly used up by the bounce");
    }

    [Test] public void SpunShotsStillLandOnTheirTarget()
    {
        Vector3 start = new Vector3(.5f, 1.1f, -10.5f);
        foreach (float spin in new[] { -.8f, .35f, 1f })
            foreach (float aim in new[] { -1f, 0f, 1f })
            {
                Vector3 target = TennisRules.ShotTarget(aim, .7f);
                Vector3 v = TennisBall.Solve(start, target, 24, spin);
                Assert.IsTrue(TennisBall.Landing(start, v, spin, out var landing, out _));
                Assert.Less(Vector2.Distance(new Vector2(landing.x, landing.z), new Vector2(target.x, target.z)), .03f,
                    $"spin {spin} aim {aim} landed at {landing} for {target}");
            }
    }

    [Test] public void FlatPredictionMatchesTheOldBallistics()
    {
        // Spin 0 must reproduce the plain ballistic flight every existing test was built on.
        Assert.IsTrue(TennisRules.PredictLanding(new Vector3(0, 1.2f, -8), new Vector3(0, 4, 14), out var landing));
        float t = (4 + Mathf.Sqrt(16 + 2 * 9.81f * (1.2f - TennisRules.BallRadius))) / 9.81f;
        Assert.AreEqual(-8 + 14 * t, landing.z, .15f);
    }

    [Test] public void SpunServesClearTheNetAndLandInTheBox()
    {
        foreach (bool deuce in new[] { true, false })
        {
            var box = TennisRules.ServeTargetCentre(true, deuce);
            Vector3 start = new Vector3(TennisRules.ServerStanceX(true, deuce), TennisRules.ServeContactHeight, -11.2f);
            foreach (float spin in new[] { .12f, .75f })
            {
                Vector3 v = TennisRules.ServeVelocity(start, box, 36, spin);
                Assert.Greater(TennisRules.NetClearance(start, v, spin), TennisRules.NetHeight, $"spin {spin} deuce {deuce}");
                Assert.IsTrue(TennisBall.Landing(start, v, spin, out var landing, out _));
                Assert.IsTrue(TennisRules.ServeIsIn(landing, true, deuce), $"spin {spin} deuce {deuce} landed {landing}");
            }
        }
    }

    // --- Opponent serve -----------------------------------------------------------------

    [Test] public void OpponentServesAreVariedAndLegalUnlessFaulted()
    {
        var lanes = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 40; i++)
        {
            float placement = i / 40f;
            var plan = TennisOpponent.PlanServe(i % 2 == 0, false, .5f, placement, .99f, (i * 7 % 10) / 10f);
            lanes.Add(plan.Kind);
            Assert.IsFalse(plan.Fault);
            Assert.IsTrue(TennisRules.ServeIsIn(plan.Landing, false, i % 2 == 0), $"{plan.Kind} serve landed {plan.Landing}");
        }
        Assert.AreEqual(3, lanes.Count, "wide, body and T");
        var fault = TennisOpponent.PlanServe(true, false, .5f, .5f, 0, .5f);
        Assert.IsTrue(fault.Fault); Assert.IsFalse(TennisRules.ServeIsIn(fault.Landing, false, true));
        var first = TennisOpponent.PlanServe(true, false, .5f, .5f, .99f, .5f);
        var second = TennisOpponent.PlanServe(true, true, .5f, .5f, .99f, .5f);
        Assert.Less(second.Speed, first.Speed, "the second serve is the safer one");
        Assert.Greater(second.Spin, first.Spin, "and carries kick");
    }

    // --- Rally balance ------------------------------------------------------------------

    /// Plays thousands of simulated rallies against the real opponent decision logic. A
    /// casual player -- misses one ball in eight, aims loosely -- should get proper rallies
    /// and win about half the points at the default difficulty.
    static void Simulate(float difficulty, float playerMiss, out float meanShots, out float playerWinShare, out float longShare)
    {
        var rng = new System.Random(11);
        const int rallies = 6000;
        int total = 0, wins = 0, longOnes = 0;
        for (int r = 0; r < rallies; r++)
        {
            int shots = 0; float opp = 0, player = 0; bool won;
            while (true)
            {
                if (rng.NextDouble() < playerMiss) { won = false; break; }
                shots++;
                float aim = (float)(rng.NextDouble() * 1.4 - .7), power = (float)(.4 + rng.NextDouble() * .5);
                Vector3 target = TennisRules.ShotTarget(aim, power);
                target.x += Mathf.Tan((float)(rng.NextDouble() * 2 - 1) * 8 * Mathf.Deg2Rad) * (target.z + 10);
                if (Mathf.Abs(target.x) > TennisRules.CourtHalfWidth) { won = false; break; }
                float move = TennisOpponent.Speed * (1f - TennisOpponent.Reaction);
                opp += Mathf.Clamp(target.x - opp, -move, move);
                var reply = TennisOpponent.Decide(opp, target.x, player, difficulty,
                    (float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble(), (power - .4f) / .5f);
                if (!reply.Reached || reply.Error) { won = true; break; }
                shots++;
                player = reply.Landing.x; opp *= .3f;
            }
            total += shots; if (won) wins++; if (shots >= 8) longOnes++;
        }
        meanShots = total / (float)rallies; playerWinShare = wins / (float)rallies; longShare = longOnes / (float)rallies;
    }

    [Test] public void DefaultOpponentGivesLongRalliesAndIsBeatable()
    {
        Simulate(TennisOpponent.DefaultDifficulty, .12f, out float mean, out float win, out float longShare);
        Assert.That(mean, Is.InRange(4.5f, 9f), "rally length");
        Assert.That(win, Is.InRange(.40f, .60f), "a casual player wins about half the points");
        Assert.Greater(longShare, .2f, "a fair share of rallies run to eight shots or more");
        Simulate(1f, .12f, out _, out float hardWin, out _);
        Simulate(0f, .12f, out _, out float easyWin, out _);
        Assert.Less(hardWin, win); Assert.Greater(easyWin, win);
    }

    // --- Frame pacing and quality --------------------------------------------------------

    [Test] public void FrameProbeReportsLowsAndHitches()
    {
        var frames = new float[100]; var scratch = new float[100];
        for (int i = 0; i < 100; i++) frames[i] = 1f / 60;
        frames[10] = frames[50] = .05f; // two badly dropped frames: 2% of the run
        var s = FrameProbe.Summarise(frames, scratch, 100, 60, 0, 0);
        Assert.AreEqual(2, s.Hitches);
        Assert.AreEqual(50, s.WorstMs, .01f);
        Assert.Less(s.OnePercentLowFps, 25);
        Assert.Greater(s.AverageFps, 55);
    }

    [Test] public void FrameRateOnlyGoesTo120WhenAskedAndSupported()
    {
        Assert.AreEqual(60, FrameRate.Target(60, 120));
        Assert.AreEqual(60, FrameRate.Target(120, 60));
        Assert.AreEqual(120, FrameRate.Target(120, 120));
    }

    [Test] public void QualityTierFollowsTheChip()
    {
        Assert.AreEqual(TennisQuality.Tier.Standard, TennisQuality.ForModel("iPhone15,4")); // iPhone 15, A16
        Assert.AreEqual(TennisQuality.Tier.High, TennisQuality.ForModel("iPhone16,1"));     // 15 Pro
        Assert.AreEqual(TennisQuality.Tier.High, TennisQuality.ForModel("iPhone18,2"));     // 17 Pro Max
        Assert.AreEqual(TennisQuality.Tier.Low, TennisQuality.ForModel("iPhone13,2"));
        foreach (var tier in new[] { TennisQuality.Tier.Standard, TennisQuality.Tier.High })
            Assert.GreaterOrEqual(TennisQuality.For(tier).Msaa, 4, "every phone in the target range gets 4x MSAA");
    }

    [Test] public void GovernorStepsDownOnlyOnSustainedLateFrames()
    {
        Assert.IsFalse(TennisFrameGovernor.ShouldStepDown(120, 3));
        Assert.IsTrue(TennisFrameGovernor.ShouldStepDown(120, 20));
    }

    // --- Moments ------------------------------------------------------------------------

    [Test] public void MatchPointIsRecognisedForEitherPlayer()
    {
        var m = TennisMatch.New();
        Assert.IsFalse(TennisGame.IsMatchPoint(m));
        m.PlayerGames = 2; m.PlayerPoints = 3;
        Assert.IsTrue(TennisGame.IsMatchPoint(m));
        m = TennisMatch.New(); m.OpponentGames = 2; m.OpponentPoints = 3; m.PlayerPoints = 1;
        Assert.IsTrue(TennisGame.IsMatchPoint(m));
    }

    [Test] public void PlayheadLandsOnEachClipsOwnContactFrame()
    {
        const float duration = .45f;
        float toContact = TennisRules.SweetTime * duration / TennisRules.StrokeDuration;
        foreach (float contact in new[] { .35f, .44f, .5f })
            Assert.AreEqual(contact, TennisRules.StrokePlayhead(toContact, duration, contact), .001f);
    }

    [Test] public void BallsOutOfReachAreDived()
    {
        Assert.AreEqual("Dive", TennisRules.StrokeFor(1f, -9f, -11.2f, TennisRules.DiveGap + .2f, false));
        Vector3 ball = new Vector3(2.0f, 1.1f, .65f);
        Assert.IsFalse(TennisRules.AssistedContact(ball, ball, Vector3.zero, TennisRules.SweetTime, false, out _, .5f));
        Assert.IsTrue(TennisRules.AssistedContact(ball, ball, Vector3.zero, TennisRules.SweetTime, false, out _, .5f, true));
    }

    [Test] public void ArmSurfaceFollowsAnAnatomicalProfile()
    {
        float shoulder = StandardCharacterArms.ProfileRadius(0, out _);
        float elbow = StandardCharacterArms.ProfileRadius(9, out _);
        float forearm = StandardCharacterArms.ProfileRadius(12, out _);
        float wrist = StandardCharacterArms.ProfileRadius(16, out float flat);
        Assert.Greater(shoulder, elbow); Assert.Greater(forearm, elbow, "forearm swells below the elbow");
        Assert.Less(wrist, elbow); Assert.Less(flat, 1, "the wrist is flatter than round");
    }
}

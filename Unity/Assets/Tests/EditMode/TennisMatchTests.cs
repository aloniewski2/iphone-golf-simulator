using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;

public class TennisMatchTests
{
    [Test] public void LoveGameCountsUpAndHandsOverServe()
    {
        var m = TennisMatch.New();
        Assert.AreEqual("0–0", m.PointCall);
        m.AwardPoint(true); Assert.AreEqual("15–0", m.PointCall);
        m.AwardPoint(true); Assert.AreEqual("30–0", m.PointCall);
        m.AwardPoint(true); Assert.AreEqual("40–0", m.PointCall);
        Assert.IsTrue(m.AwardPoint(true), "fourth point takes the game");
        Assert.AreEqual(1, m.PlayerGames);
        Assert.IsFalse(m.PlayerServes, "serve changes hands with the game");
    }

    [Test] public void DeuceNeedsTwoClearPoints()
    {
        var m = TennisMatch.New();
        for (int i = 0; i < 3; i++) { m.AwardPoint(true); m.AwardPoint(false); }
        Assert.AreEqual("DEUCE", m.PointCall);
        m.AwardPoint(true); Assert.AreEqual("AD IN", m.PointCall);
        m.AwardPoint(false); Assert.AreEqual("DEUCE", m.PointCall);
        m.AwardPoint(false); Assert.AreEqual("AD OUT", m.PointCall);
        Assert.AreEqual(0, m.PlayerGames + m.OpponentGames, "no game awarded from advantage alone");
        m.AwardPoint(false);
        Assert.AreEqual(1, m.OpponentGames);
    }

    [Test] public void ServerScoreIsCalledFirst()
    {
        var m = TennisMatch.New();
        m.AwardPoint(true); m.AwardPoint(true); m.AwardPoint(true);
        m.AwardPoint(false); m.AwardPoint(false);
        Assert.AreEqual("40–30", m.PointCall);
    }

    [Test] public void SetEndsAtThreeGamesAndNotBefore()
    {
        var m = TennisMatch.New();
        int games = 0;
        while (!m.Complete) { if (m.AwardPoint(true)) games++; Assert.LessOrEqual(games, 3); }
        Assert.AreEqual(3, games);
        Assert.IsTrue(m.PlayerWonMatch);
        Assert.IsFalse(m.AwardPoint(false), "a finished match accepts no more points");
    }

    [Test] public void CourtSideAlternatesEveryPoint()
    {
        var m = TennisMatch.New();
        Assert.IsTrue(m.DeuceCourt, "games start on the deuce court");
        m.AwardPoint(true); Assert.IsFalse(m.DeuceCourt);
        m.AwardPoint(false); Assert.IsTrue(m.DeuceCourt);
    }

    [Test] public void ServeMustCrossToTheOppositeBox()
    {
        // Player serves from the near end on the deuce court: the ball must land beyond the
        // net, inside the service line, and on the far side of the centre line.
        Assert.IsTrue(TennisRules.ServeIsIn(new Vector3(-2f, .1f, 3f), true, true));
        Assert.IsFalse(TennisRules.ServeIsIn(new Vector3(2f, .1f, 3f), true, true), "wrong box");
        Assert.IsFalse(TennisRules.ServeIsIn(new Vector3(-2f, .1f, 7.5f), true, true), "past the service line");
        Assert.IsFalse(TennisRules.ServeIsIn(new Vector3(-2f, .1f, -3f), true, true), "own half");
        Assert.IsFalse(TennisRules.ServeIsIn(new Vector3(-4.9f, .1f, 3f), true, true), "wide");
        // Ad court sends it the other way, and the far-side server mirrors both.
        Assert.IsTrue(TennisRules.ServeIsIn(new Vector3(2f, .1f, 3f), true, false));
        Assert.IsTrue(TennisRules.ServeIsIn(new Vector3(2f, .1f, -3f), false, true));
        Assert.IsFalse(TennisRules.ServeIsIn(new Vector3(-2f, .1f, -3f), false, true));
    }

    [Test] public void ServeTargetCentreIsAlwaysALegalTarget()
    {
        foreach (bool near in new[] { true, false })
            foreach (bool deuce in new[] { true, false })
                Assert.IsTrue(TennisRules.ServeIsIn(TennisRules.ServeTargetCentre(near, deuce), near, deuce),
                    $"near={near} deuce={deuce}");
    }

    // --- The controller serve -----------------------------------------------------------

    [Test] public void PowerBarPeaksAtTheTopOfTheToss()
    {
        Assert.AreEqual(1f, TennisRules.ServePowerAt(0), 1e-5f);
        Assert.Greater(TennisRules.ServePowerAt(.1f), TennisRules.ServePowerAt(.25f));
        Assert.AreEqual(TennisRules.ServePowerAt(-.2f), TennisRules.ServePowerAt(.2f), 1e-5f, "early and late cost the same");
        Assert.AreEqual(0f, TennisRules.ServePowerAt(TennisRules.ServePowerWindow), 1e-5f);
        Assert.IsTrue(TennisRules.ServePerfectTiming(TennisRules.ServePerfectWindow * .9f));
        Assert.IsFalse(TennisRules.ServePerfectTiming(TennisRules.ServePerfectWindow * 1.5f));
    }

    [Test] public void APerfectServeNeedsAPerfectTossAndAPerfectSwing()
    {
        var aim = new Vector2(.8f, .9f);
        var perfect = TennisRules.JudgeServeStrike(0, 1, aim, true, true, false, .9f, .1f);
        Assert.IsTrue(perfect.Perfect); Assert.IsTrue(perfect.Legal);
        Assert.AreEqual(TennisRules.ServeTopSpeed, perfect.Speed, 1e-4f, "as fast as a serve goes");
        var spot = TennisRules.IntoServiceBox(TennisRules.ServeAimPoint(aim, true, true), true, true);
        Assert.Less(Vector3.Distance(spot, perfect.Landing), .01f, "exactly where it was aimed");
        var looseToss = TennisRules.JudgeServeStrike(0, .5f, aim, true, true, false, .9f, .1f);
        var lateSwing = TennisRules.JudgeServeStrike(.2f, 1, aim, true, true, false, .9f, .1f);
        Assert.IsFalse(looseToss.Perfect); Assert.IsFalse(lateSwing.Perfect);
        Assert.Less(looseToss.Speed, perfect.Speed); Assert.Less(lateSwing.Speed, perfect.Speed);
        Assert.Less(TennisRules.JudgeServeStrike(0, 1, aim, true, true, true, .5f, .5f).Speed, perfect.Speed, "a second serve is safer");
    }

    [Test] public void TheTossPlacesTheServeAndTheSwingPowersIt()
    {
        var aim = new Vector2(0, .7f);
        var spot = TennisRules.IntoServiceBox(TennisRules.ServeAimPoint(aim, true, true), true, true);
        // Same swing, different tosses: same pace, but a looser toss lands further off the aim.
        var clean = TennisRules.JudgeServeStrike(.15f, .95f, aim, true, true, false, .95f, .95f);
        var loose = TennisRules.JudgeServeStrike(.15f, .4f, aim, true, true, false, .95f, .95f);
        var wild = TennisRules.JudgeServeStrike(.15f, .05f, aim, true, true, false, .95f, .95f);
        Assert.AreEqual(clean.Speed, loose.Speed, 1e-4f, "the toss does not change the pace");
        Assert.Less(Vector3.Distance(clean.Landing, spot), .01f, "a centred toss lands on the aim");
        Assert.Greater(Vector3.Distance(loose.Landing, spot), Vector3.Distance(clean.Landing, spot));
        Assert.Greater(Vector3.Distance(wild.Landing, spot), Vector3.Distance(loose.Landing, spot));
        // Same toss, different swings: same spot, different pace.
        var early = TennisRules.JudgeServeStrike(-.3f, .95f, aim, true, true, false, .95f, .95f);
        Assert.Less(early.Speed, clean.Speed);
        Assert.Less(Vector3.Distance(early.Landing, spot), .01f);
    }

    [Test] public void AGoodTossLandsWhereAimedAndALooseOneRisksAFault()
    {
        // A centred toss lands exactly on the aim, anywhere in the box, at any legal timing.
        foreach (bool deuce in new[] { true, false })
            for (float across = -1; across <= 1; across += .5f)
                for (float depth = 0; depth <= 1; depth += .5f)
                    for (float fromApex = -.4f; fromApex <= .4f; fromApex += .1f)
                    {
                        var v = TennisRules.JudgeServeStrike(fromApex, 1, new Vector2(across, depth), true, deuce, false, .99f, .01f);
                        if (TennisRules.ServePowerAt(fromApex) >= TennisRules.ServeFaultPower)
                            Assert.IsTrue(v.Legal && TennisRules.ServeIsIn(v.Landing, true, deuce), $"aim {across},{depth} at {fromApex}: {v.Landing}");
                    }
        // Aimed at the lines with a loose toss, the wander can carry it out: a fault.
        int outs = 0;
        for (float r = 0; r <= 1; r += .1f)
        {
            var v = TennisRules.JudgeServeStrike(0, .2f, new Vector2(1, 1), true, true, false, r, 1 - r);
            if (!TennisRules.ServeIsIn(v.Landing, true, true)) outs++;
        }
        Assert.Greater(outs, 2, "going for the lines with a bad toss must be a real risk");
        // Aimed safely with a middling toss it still lands in.
        Assert.IsTrue(TennisRules.ServeIsIn(TennisRules.JudgeServeStrike(0, .7f, new Vector2(0, .5f), true, true, false, .9f, .1f).Landing, true, true));
        Assert.IsFalse(TennisRules.JudgeServeStrike(.6f, 1, Vector2.zero, true, true, false, .5f, .5f).Legal, "far too late hits the net");
        Assert.IsFalse(TennisRules.JudgeServeStrike(-.6f, 1, Vector2.zero, true, true, false, .5f, .5f).Legal, "far too early too");
        // Aim moves the ball: wide lands further out than the T.
        var t = TennisRules.JudgeServeStrike(0, 1, new Vector2(-1, .8f), true, true, false, .5f, .5f);
        var wide = TennisRules.JudgeServeStrike(0, 1, new Vector2(1, .8f), true, true, false, .5f, .5f);
        Assert.Greater(Mathf.Abs(wide.Landing.x), Mathf.Abs(t.Landing.x) + 2);
    }

    [Test] public void ServesTheReceiverCanReachAreToldApart()
    {
        // Straight at the receiver: always reachable. Top pace into the far corner with the
        // receiver stood on the other side: not.
        Vector3 start = new Vector3(-2.35f, 2.45f, 12.35f);
        var atThem = TennisRules.ServeAimPoint(new Vector2(0, .9f), false, true);
        Assert.IsTrue(TennisRules.ServeReachable(start, TennisRules.ServeVelocity(start, atThem, 30, .1f), .1f, .6f, atThem.x, -11.2f));
        var corner = TennisRules.ServeAimPoint(new Vector2(1, .95f), false, true);
        Assert.IsFalse(TennisRules.ServeReachable(start, TennisRules.ServeVelocity(start, corner, TennisRules.ServeTopSpeed, .1f), .1f, .6f, -corner.x, -11.2f));
    }

    [Test] public void ServerAndReceiverStandOnMatchingSides()
    {
        // Serving from your right sends the ball to the receiver's right, so that is where
        // they wait. Player is at the near end facing +z (right = +x); the opponent faces
        // the other way, so their right is -x.
        Assert.Greater(TennisRules.ServerStanceX(true, true), 0, "player's deuce court is +x");
        Assert.Less(TennisRules.ReceiverStanceX(true, true), 0, "which is the opponent's right");
        Assert.Less(TennisRules.ServerStanceX(true, false), 0, "player's ad court is -x");
        Assert.Greater(TennisRules.ReceiverStanceX(true, false), 0);
        // Mirrored when the opponent serves.
        Assert.Less(TennisRules.ServerStanceX(false, true), 0, "opponent's deuce court is -x");
        Assert.Greater(TennisRules.ReceiverStanceX(false, true), 0);
        // The receiver always waits on the same side of the centre line as the target box.
        foreach (bool near in new[] { true, false })
            foreach (bool deuce in new[] { true, false })
                Assert.AreEqual(TennisRules.ServeTargetCentre(near, deuce).x >= 0,
                                TennisRules.ReceiverStanceX(near, deuce) >= 0, $"near={near} deuce={deuce}");
    }

    [Test] public void EveryLegalServeActuallyClearsTheNet()
    {
        // The original bug: the flattest arc that reaches the target passes UNDER the net, so
        // no serve could ever be legal however well it was timed. Now at up to top pace.
        foreach (bool deuce in new[] { true, false })
            foreach (float contact in new[] { 1.2f, 2.0f, 2.45f, 2.8f })
            {
                Vector3 start = new Vector3(TennisRules.ServerStanceX(true, deuce), contact, -11.2f);
                foreach (float fromApex in new[] { 0f, .2f, .35f })
                    foreach (float across in new[] { -1f, 0f, 1f })
                    {
                        var v = TennisRules.JudgeServeStrike(fromApex, 1, new Vector2(across, .9f), true, deuce, false, .5f, .5f);
                        Vector3 velocity = TennisRules.ServeVelocity(start, v.Landing, v.Speed);
                        float flight = Mathf.Abs(velocity.z) < .001f ? 1 : (v.Landing.z - start.z) / velocity.z;
                        Assert.Greater(TennisRules.NetCrossingHeight(start, v.Landing, velocity, flight),
                            TennisRules.NetHeight, $"contact {contact} timing {fromApex} aim {across} deuce {deuce} must clear the net");
                        Assert.IsTrue(TennisRules.ServeIsIn(v.Landing, true, deuce));
                    }
            }
    }

    [Test] public void OnlyTheServeHasToGoDiagonally()
    {
        // A serve into the wrong box is a fault...
        Assert.IsFalse(TennisRules.ServeIsIn(new Vector3(2f, .1f, 3f), true, true));
        // ...but in a rally the very same bounce is perfectly good, and so is a deep ball
        // well past the service line, which serve rules would have rejected.
        Assert.IsTrue(TennisRules.BounceIsIn(new Vector3(2f, .1f, 3f)));
        Assert.IsTrue(TennisRules.BounceIsIn(new Vector3(2f, .1f, 10.5f)), "deep rally ball is in");
        Assert.IsTrue(TennisRules.BounceIsIn(new Vector3(-4f, .1f, -11f)), "so is a deep ball to the other corner");
        // Only the true court lines bound a rally.
        Assert.IsFalse(TennisRules.BounceIsIn(new Vector3(4.5f, .1f, 3f)), "wide of the singles line");
        Assert.IsFalse(TennisRules.BounceIsIn(new Vector3(0f, .1f, 12.5f)), "past the baseline");
    }

    [Test] public void OpponentIsBeatenByWidthNotByLuck()
    {
        // A ball straight at it comes back; one outside its reach is a clean winner.
        var easy = TennisOpponent.Decide(0, 0, 0, .5f, .5f, .5f, .5f);
        Assert.IsTrue(easy.Reached); Assert.IsFalse(easy.Error);
        var past = TennisOpponent.Decide(0, TennisOpponent.Reach + .5f, 0, .5f, .5f, .5f, .5f);
        Assert.IsFalse(past.Reached, "wide of its reach must win the point outright");

        // Error chance grows with stretch rather than being a flat coin-flip.
        Assert.Greater(TennisOpponent.ErrorChance(1f, .5f), TennisOpponent.ErrorChance(0f, .5f) * 4);
        Assert.Less(TennisOpponent.ErrorChance(0f, .5f), .05f, "comfortable balls come back");
        Assert.Less(TennisOpponent.ErrorChance(.4f, .5f), .07f, "and so do ordinary ones: points come from reach, not gifts");
        Assert.Greater(TennisOpponent.ErrorChance(1f, .5f), .35f, "a ball at full stretch often does not");
        // And a harder setting errs less at the same stretch.
        Assert.Less(TennisOpponent.ErrorChance(.8f, 1f), TennisOpponent.ErrorChance(.8f, 0f));
    }

    [Test] public void OpponentOnlyMissesBallsThatAreHardToPlay()
    {
        // A comfortable ball -- at the body, moderate pace, waist high -- comes back unless the
        // opponent makes an unforced error, which only weak players do with any regularity.
        for (float d = 0; d <= 1f; d += .25f)
        {
            float unforced = OpponentProfile.FromDifficulty(d).UnforcedError;
            var easy = TennisOpponent.Decide(0, .4f, 0, d, unforced + .01f, .5f, .5f, .4f, TennisOpponent.Reach, 0, .5f);
            Assert.IsTrue(easy.Reached && !easy.Error, $"comfortable ball at difficulty {d}");
            Assert.LessOrEqual(unforced, .1f, "unforced errors stay occasional");
        }
        Assert.IsTrue(TennisOpponent.Decide(0, .4f, 0, 0, .02f, .5f, .5f, .4f, TennisOpponent.Reach, 0, .5f).Error,
            "a club player can shank an easy ball");
        Assert.Less(OpponentProfile.Rival("boss", 1).UnforcedError, .01f, "the champion almost never gives one away");
        // Each threat alone makes a ball harder; together they compound.
        float calm = TennisOpponent.ShotDifficulty(0, 0);
        Assert.Greater(TennisOpponent.ShotDifficulty(.9f, 0), calm);
        Assert.Greater(TennisOpponent.ShotDifficulty(0, 1), calm);
        Assert.Greater(TennisOpponent.ShotDifficulty(0, 0, 1), calm);
        Assert.Greater(TennisOpponent.ShotDifficulty(0, 0, 0, 1), calm);
        Assert.Greater(TennisOpponent.ShotDifficulty(.9f, 1, 1, 1), TennisOpponent.ShotDifficulty(.9f, 0));

        // A miss says why, and lands where that kind of miss goes.
        var wide = TennisOpponent.Decide(0, TennisOpponent.Reach * .97f, 0, .5f, .9f * TennisOpponent.ErrorChance(.97f, .5f), .5f, .5f);
        Assert.IsTrue(wide.Error); Assert.AreEqual("STRETCHED WIDE", wide.Reason);
        Assert.Greater(Mathf.Abs(wide.Landing.x), TennisRules.CourtHalfWidth, "sprayed wide of the sideline");
        Assert.Less(wide.Landing.z, 0, "on the player's side");
        var low = TennisOpponent.Decide(0, .3f, 0, .5f, 0, .5f, .5f, .2f, TennisOpponent.Reach, -1, 0);
        var lowChance = TennisOpponent.ErrorChance(.3f / TennisOpponent.Reach, .5f, .2f, 1, 0);
        if (lowChance > 0) { Assert.IsTrue(low.Error); StringAssert.Contains("LOW", low.Reason); Assert.Greater(low.Landing.z, 0, "into the net"); }
        var pace = TennisOpponent.Decide(0, 1.2f, 0, .2f, .99f * TennisOpponent.ErrorChance(1.2f / TennisOpponent.Reach, .2f, 1, 0, 1), .5f, .5f, 1, TennisOpponent.Reach, 0, 1);
        Assert.IsTrue(pace.Error);
        Assert.Less(pace.Landing.z, -TennisRules.CourtHalfLength, "late on a heavy ball, it flies long");
        StringAssert.DoesNotContain("WIDE", pace.Reason);
    }

    [Test] public void OpponentHitsIntoTheOpenCourtAndInsideTheLines()
    {
        // Player parked out wide right: the reply should go left, and vice versa.
        Assert.Less(TennisOpponent.OpenCourtTarget(3f, .5f, .5f).x, 0);
        Assert.Greater(TennisOpponent.OpenCourtTarget(-3f, .5f, .5f).x, 0);
        // Every placement it can choose must be a legal, in-court target on the player's side.
        for (float w = 0; w <= 1f; w += .1f)
            for (float d = 0; d <= 1f; d += .1f)
                foreach (float px in new[] { -3f, 0f, 3f })
                {
                    var t = TennisOpponent.OpenCourtTarget(px, w, d);
                    Assert.IsTrue(TennisRules.BounceIsIn(t), $"target {t} must be in court");
                    Assert.Less(t.z, 0, "and on the player's side of the net");
                }
    }

    [Test] public void AStretchedOpponentHitsWeaker()
    {
        var comfortable = TennisOpponent.Decide(0, .2f, 0, .5f, .99f, .5f, .5f);
        var stretched = TennisOpponent.Decide(0, TennisOpponent.Reach * .95f, 0, .5f, .99f, .5f, .5f);
        Assert.IsTrue(comfortable.Reached && !comfortable.Error);
        Assert.IsTrue(stretched.Reached && !stretched.Error);
        Assert.Greater(comfortable.Speed, stretched.Speed, "moving it around must yield weaker replies");
    }

    [Test] public void OpponentRecoversTowardTheMiddleWhenNothingIsComing()
    {
        float x = 3f;
        for (int i = 0; i < 200; i++) x = TennisOpponent.Reposition(x, 0, false, 1f / 60);
        Assert.Less(Mathf.Abs(x - TennisOpponent.RestX), .05f);
    }

    [Test] public void SwingTimingSteersTheBall()
    {
        // Early opens the cross-court corner, late goes down the line, on time goes straight.
        Assert.Greater(TennisRules.AimFromTiming(-TennisRules.AimWindow, false), .9f);
        Assert.Less(TennisRules.AimFromTiming(TennisRules.AimWindow, false), -.9f);
        Assert.AreEqual(0, TennisRules.AimFromTiming(0, false), .001f);
        // The backhand mirrors it, so the same early swing opens the other corner.
        Assert.Less(TennisRules.AimFromTiming(-TennisRules.AimWindow, true), -.9f);
        // And it never steers further than the aim range allows.
        foreach (float offset in new[] { -3f, -1f, 0f, 1f, 3f })
            foreach (bool back in new[] { false, true })
                Assert.LessOrEqual(Mathf.Abs(TennisRules.AimFromTiming(offset, back)), 1f);
    }

    // --- Gameplay depth: contact decides pace and placement; reaching the ball is a skill ---

    [Test] public void ShotPaceComesFromContactNotEffort()
    {
        float clean = TennisRules.ShotSpeed(.95f, .5f, 1), poor = TennisRules.ShotSpeed(.35f, .5f, 1);
        Assert.Greater(clean, poor + 8, "a clean strike must be much faster than a poor one");
        float hard = TennisRules.ShotSpeed(.5f, 1, 1), soft = TennisRules.ShotSpeed(.5f, .1f, 1);
        Assert.Less(hard - soft, 3.5f, "swinging harder only nudges the pace");
    }

    [Test] public void CleanContactOpensTheLinesAndDepth()
    {
        var clean = TennisRules.AimedTarget(1, .95f, 0);
        var poor = TennisRules.AimedTarget(1, .3f, 0);
        Assert.Greater(clean.x, 3.4f, "full aim off a clean hit goes near the sideline");
        Assert.Less(clean.x, TennisRules.CourtHalfWidth, "but inside it before error");
        Assert.Greater(poor.x, 2.5f, "a poor contact still goes the way the face pointed");
        Assert.Less(poor.x, clean.x, "just not as close to the line");
        Assert.Greater(clean.z, poor.z + 2, "clean contact lands deep, poor contact sits up short");
        Assert.AreEqual(0, TennisRules.AimedTarget(0, .9f, 0).x, 1e-4, "neutral face goes through the middle");
        Assert.Less(TennisRules.AimedTarget(-1, .9f, 0).x, -3f, "face turned left goes left");
    }

    [Test] public void MovementIsHumanAndConsistent()
    {
        foreach (float d in new[] { .5f, 2f, 5f })
            Assert.AreEqual(d, TennisRules.Coverable(TennisRules.TimeToCover(d, TennisRules.RunSpeed), TennisRules.RunSpeed), 1e-3f);
        Assert.Greater(TennisRules.TimeToCover(4f, TennisRules.RunSpeed), .9f, "four metres takes most of a second");
        Assert.Less(TennisRules.TimeToCover(4f, TennisRules.SprintSpeed), TennisRules.TimeToCover(4f, TennisRules.RunSpeed));
    }

    static Vector3 Fly(Vector3 from, Vector3 to, float speed) => TennisRules.RallyVelocity(from, to, speed, 0, .9f);

    [Test] public void WideWinnersNeedAGoodJump()
    {
        // A fast, clean ball to the far corner from a player waiting in the middle.
        Vector3 from = new Vector3(-2, 1, 10.5f), corner = new Vector3(3.9f, TennisRules.BallRadius, -10.2f);
        Vector3 v = Fly(from, corner, 30);
        var late = TennisRules.PlanIntercept(from, v, 0, .75f, new Vector2(0, TennisRules.BaselineZ), TennisRules.ReactionTime, TennisRules.RunSpeed, false);
        var early = TennisRules.PlanIntercept(from, v, 0, .75f, new Vector2(0, TennisRules.BaselineZ), TennisRules.JumpReaction, TennisRules.SprintSpeed, false);
        Assert.IsFalse(late.Reachable, $"reacting late, a clean corner winner stays a winner (gap {late.Gap:0.00})");
        Assert.IsTrue(early.Reachable || early.Diveable, $"a good jump gets there, or close enough to dive (gap {early.Gap:0.00})");
    }

    [Test] public void AnOrdinaryBallIsReachable()
    {
        Vector3 from = new Vector3(0, 1, 10.5f), to = new Vector3(1.2f, TennisRules.BallRadius, -9.5f);
        var plan = TennisRules.PlanIntercept(from, Fly(from, to, 22), 0, .75f, new Vector2(0, TennisRules.BaselineZ), TennisRules.ReactionTime, TennisRules.RunSpeed, false);
        Assert.IsTrue(plan.Reachable);
    }

    [Test] public void AShortBallPullsThePlayerForward()
    {
        Vector3 from = new Vector3(0, 1, 10.5f), to = new Vector3(.5f, TennisRules.BallRadius, -4.5f);
        var plan = TennisRules.PlanIntercept(from, Fly(from, to, 17), 0, .75f, new Vector2(0, TennisRules.BaselineZ), TennisRules.ReactionTime, TennisRules.RunSpeed, false);
        Assert.IsTrue(plan.Found);
        Assert.Greater(plan.Point.z, -8.5f, $"a short ball is met inside the court (z {plan.Point.z:0.0})");
    }

    [Test] public void BallHeightAndDistanceChooseTheStroke()
    {
        // Overhead, low, at the net, stretched wide, and the ordinary case.
        Assert.AreEqual("Smash", TennisRules.StrokeFor(2.2f, -6f, -11.2f, .2f, false));
        Assert.AreEqual("Lob", TennisRules.StrokeFor(1.2f, -9f, -11.2f, .2f, true));
        // At the net (player z near zero), not merely "ball has arrived".
        Assert.AreEqual("Volley", TennisRules.StrokeFor(1.2f, -2f, -3f, .2f, false));
        Assert.AreNotEqual("Volley", TennisRules.StrokeFor(1.2f, -9f, -11.2f, .2f, false),
            "a baseline rally ball is not a volley");
        Assert.AreEqual("LowPickup", TennisRules.StrokeFor(.3f, -9f, -11.2f, .2f, false));
        Assert.AreEqual("Running", TennisRules.StrokeFor(1.0f, -9f, -11.2f, 1.4f, false));
        // Beyond normal reach the only way to the ball is to throw yourself at it.
        Assert.AreEqual("Dive", TennisRules.StrokeFor(1.0f, -9f, -11.2f, 2.2f, false));
        Assert.AreEqual("Drive", TennisRules.StrokeFor(1.0f, -9f, -11.2f, .2f, false));
        // A smash outranks everything else: height wins.
        Assert.AreEqual("Smash", TennisRules.StrokeFor(2.2f, -9f, -11.2f, 3f, true));
    }

    [Test] public void ContactGradesRunFromMissToPerfect()
    {
        Assert.AreEqual(Timing.Missed, TennisRules.Grade(0));
        Assert.AreEqual(Timing.Ok, TennisRules.Grade(.3f));
        Assert.AreEqual(Timing.Good, TennisRules.Grade(.6f));
        Assert.AreEqual(Timing.Great, TennisRules.Grade(.8f));
        Assert.AreEqual(Timing.Excellent, TennisRules.Grade(.9f));
        Assert.AreEqual(Timing.Perfect, TennisRules.Grade(1f));
        // Grades must be monotonic in timing, or the feedback would contradict itself.
        for (float t = 0; t <= 1f; t += .02f)
            Assert.LessOrEqual((int)TennisRules.Grade(t), (int)TennisRules.Grade(Mathf.Min(1f, t + .02f)));
        // Only clean strikes keep a streak alive.
        Assert.IsTrue(TennisRules.Extends(Timing.Great));
        Assert.IsTrue(TennisRules.Extends(Timing.Perfect));
        Assert.IsFalse(TennisRules.Extends(Timing.Good));
        Assert.IsFalse(TennisRules.Extends(Timing.Missed));
    }

    [Test] public void ThreeCleanStrikesSuperchargeTheThird()
    {
        // Mirrors the streak bookkeeping in TennisGame.ReturnBall.
        int streak = 0; int supercharged = 0;
        foreach (var grade in new[] { Timing.Great, Timing.Perfect, Timing.Excellent, Timing.Great })
        {
            streak = TennisRules.Extends(grade) ? streak + 1 : 0;
            if (streak >= TennisRules.SuperchargeStreak) { supercharged++; streak = 0; }
        }
        Assert.AreEqual(1, supercharged, "third clean strike fires, then the streak resets");

        // A sloppy hit in the middle breaks it.
        streak = 0; supercharged = 0;
        foreach (var grade in new[] { Timing.Great, Timing.Good, Timing.Great, Timing.Great })
        {
            streak = TennisRules.Extends(grade) ? streak + 1 : 0;
            if (streak >= TennisRules.SuperchargeStreak) { supercharged++; streak = 0; }
        }
        Assert.AreEqual(0, supercharged, "a scruffy ball resets the run");
    }

    [Test] public void SuperchargeIsFasterAndStraighter()
    {
        var plain = TennisRules.Evaluate(TennisRules.SweetTime, Vector2.zero, 1, 1, .8f, 1);
        var boosted = TennisRules.Supercharge(plain);
        Assert.Greater(boosted.Speed, plain.Speed * 1.3f);
        Assert.Less(boosted.ErrorDegrees, plain.ErrorDegrees);
        Assert.AreEqual("SUPERCHARGED", boosted.Label);
    }

    [Test] public void ImpactOffsetIsMeasuredOnTheStringBed()
    {
        Vector3 sweet = new Vector3(1, 1.1f, -9);
        Vector3 right = Vector3.right, up = Vector3.up;
        // Dead centre reads as zero, and error grows toward the rim.
        Assert.AreEqual(0, TennisRules.FaceError(TennisRules.FaceOffset(sweet, sweet, right, up)), .001f);
        var high = TennisRules.FaceOffset(sweet + Vector3.up * .1f, sweet, right, up);
        Assert.AreEqual(.1f, high.y, .001f);
        Assert.AreEqual(0, high.x, .001f);
        // An edge contact reads as roughly a full face-width of error.
        var edge = TennisRules.FaceOffset(sweet + Vector3.right * TennisRules.StringHalfWidth, sweet, right, up);
        Assert.AreEqual(1f, TennisRules.FaceError(edge), .01f);
    }

    [Test] public void StrokePlayheadAcceleratesIntoContactThenRelaxes()
    {
        const float duration = .45f;
        float toContact = TennisRules.SweetTime * duration / TennisRules.StrokeDuration;
        // Starts partway in, so the swing does not have to crush a slow ready-in.
        Assert.AreEqual(TennisRules.StrokeEntry, TennisRules.StrokePlayhead(0, duration), .001f);
        // Reaches the clip's contact frame exactly when the ball is struck.
        Assert.AreEqual(TennisRules.StrokeContact, TennisRules.StrokePlayhead(toContact, duration), .001f);
        // And resolves by the end of the swing.
        Assert.AreEqual(TennisRules.StrokeExit, TennisRules.StrokePlayhead(duration, duration), .001f);
        // Monotonic throughout: the clip must never run backwards mid-stroke.
        float previous = -1;
        for (float t = 0; t <= duration; t += duration / 60)
        {
            float head = TennisRules.StrokePlayhead(t, duration);
            Assert.GreaterOrEqual(head, previous); previous = head;
        }
        // The run into contact is faster than the follow-through, which is what makes a hard
        // stroke read as powerful rather than uniformly sped up.
        float intoContact = (TennisRules.StrokeContact - TennisRules.StrokeEntry) / toContact;
        float after = (TennisRules.StrokeExit - TennisRules.StrokeContact) / (duration - toContact);
        Assert.Greater(intoContact, after);
    }

    [Test] public void RunCycleTracksGroundSpeedWithNoFloor()
    {
        // The old scale had a floor, so a stationary character kept shuffling their feet.
        Assert.AreEqual(0, TennisRules.CycleRate(0), .0001f);
        Assert.Greater(TennisRules.CycleRate(6.2f), TennisRules.CycleRate(3f));
        // Twice the speed turns the legs over twice as fast.
        Assert.AreEqual(2 * TennisRules.CycleRate(3f), TennisRules.CycleRate(6f), .0001f);
        // Direction does not change how fast the legs cycle.
        Assert.AreEqual(TennisRules.CycleRate(4f), TennisRules.CycleRate(-4f), .0001f);
    }

    [Test] public void ShortMovementStaysSquareAndWideMovementTurnsTheBody()
    {
        // Tennis coaching: shuffle square to the net for short distances, cross over and turn
        // the hips when you have to cover real ground.
        Assert.AreEqual(0, TennisRules.CrossoverBlend(0), .001f);
        Assert.AreEqual(0, TennisRules.CrossoverBlend(TennisRules.ShuffleSpeed), .001f, "a shuffle stays square");
        Assert.Greater(TennisRules.CrossoverBlend(4f), 0);
        Assert.AreEqual(1, TennisRules.CrossoverBlend(TennisRules.RunSpeed), .001f, "a sprint is a full crossover");
        Assert.AreEqual(1, TennisRules.CrossoverBlend(TennisRules.SprintSpeed), .001f, "and never more than full");
        // Monotonic, so the body never snaps between styles.
        float previous = -1;
        for (float v = 0; v <= TennisRules.SprintSpeed; v += .2f)
        {
            float blend = TennisRules.CrossoverBlend(v);
            Assert.GreaterOrEqual(blend, previous); previous = blend;
        }
        // The body turns toward travel, and mirrors for the other direction.
        Assert.Greater(TennisRules.BodyYaw(TennisRules.RunSpeed), 40f);
        Assert.AreEqual(-TennisRules.BodyYaw(5f), TennisRules.BodyYaw(-5f), .001f);
        Assert.AreEqual(0, TennisRules.BodyYaw(1f), .001f, "no turn while shuffling");
        // Never past the crossover limit, whatever speed is thrown at it.
        foreach (float v in new[] { 12f, 40f, -40f })
            Assert.LessOrEqual(Mathf.Abs(TennisRules.BodyYaw(v)), TennisRules.CrossoverYaw + .001f);
    }

    [Test] public void LandingPredictionFindsTheBounceAndJudgesItOut()
    {
        Assert.IsTrue(TennisRules.PredictLanding(new Vector3(0, 1.2f, -8), new Vector3(0, 4, 14), out var inBall));
        Assert.IsTrue(TennisRules.BounceIsIn(inBall));
        Assert.IsTrue(TennisRules.PredictLanding(new Vector3(0, 1.2f, -8), new Vector3(9, 4, 14), out var wide));
        Assert.IsFalse(TennisRules.BounceIsIn(wide), "a ball sent this wide must read as out");
    }

    // --- Timing: fair windows, learned lag, honest contact map --------------------------

    [Test] public void TimingGradesAreSetInMillisecondsAPersonCanHit()
    {
        Assert.AreEqual(Timing.Perfect, TennisRules.Grade(TennisRules.TimingScore(.03f)), "30ms late is still perfect");
        Assert.AreEqual(Timing.Perfect, TennisRules.Grade(TennisRules.TimingScore(-.03f)), "and so is 30ms early");
        Assert.AreEqual(Timing.Excellent, TennisRules.Grade(TennisRules.TimingScore(.05f)));
        Assert.AreEqual(Timing.Great, TennisRules.Grade(TennisRules.TimingScore(.08f)));
        Assert.AreEqual(Timing.Good, TennisRules.Grade(TennisRules.TimingScore(-.12f)));
        Assert.AreEqual(Timing.Ok, TennisRules.Grade(TennisRules.TimingScore(.18f)));
        Assert.AreEqual(Timing.Missed, TennisRules.Grade(TennisRules.TimingScore(.3f)));
        Assert.AreEqual("LATE", TennisRules.TimingWord(.06f));
        Assert.AreEqual("EARLY", TennisRules.TimingWord(-.06f));
        Assert.AreEqual("", TennisRules.TimingWord(.02f));
    }

    [Test] public void LagLearnerCorrectsASteadyBiasAndIgnoresFlukes()
    {
        var lag = new TennisLagLearner { Prior = .10f };
        Assert.AreEqual(.10f, lag.Estimate, 1e-5f, "starts at the measured TV delay");
        // The player's timing actually runs ~210ms behind (TV plus habit).
        foreach (float late in new[] { .20f, .23f, .21f, .19f }) lag.Observe(late);
        Assert.AreEqual(.20f, lag.Estimate, .02f, "a steady bias is learned within a few swings");
        lag.Observe(.9f); lag.Observe(-.6f);
        Assert.AreEqual(.20f, lag.Estimate, .02f, "a panicked swing is not a trend");
        Assert.LessOrEqual(new TennisLagLearner { Prior = 2 }.Estimate, TennisLagLearner.Max);
        // An unmeasured TV (prior 0) is still learned, from play alone.
        var unmeasured = new TennisLagLearner();
        foreach (float late in new[] { .15f, .16f, .14f, .15f }) unmeasured.Observe(late);
        Assert.AreEqual(.15f, unmeasured.Estimate, .02f);
    }

    [Test] public void AssistedHitsLandOnTheStringsByHowWellTheyWereMade()
    {
        // Clean and on time: the sweet spot, not the frame.
        Vector2 clean = TennisRules.AssistedFace(1f, .1f, -.1f);
        Assert.Less(TennisRules.FaceError(clean), .1f);
        // Scrambled: out toward the frame, but still on the strings.
        Vector2 poor = TennisRules.AssistedFace(0f, 1f, 0f);
        Assert.That(TennisRules.FaceError(poor), Is.InRange(.8f, 1f));
        // Direction follows the timing and the height.
        Assert.Greater(TennisRules.AssistedFace(.5f, 1, 0).x, 0, "early goes one way across the face");
        Assert.Less(TennisRules.AssistedFace(.5f, -1, 0).x, 0, "late the other");
        Assert.Greater(TennisRules.AssistedFace(.5f, 0, 1).y, 0, "a high ball meets the upper strings");
        // Quality alone decides how far out: a middling contact sits in between.
        float mid = TennisRules.FaceError(TennisRules.AssistedFace(.5f, .3f, .3f));
        Assert.Less(mid, TennisRules.FaceError(poor)); Assert.Greater(mid, TennisRules.FaceError(clean));
    }

    [Test] public void TimingCheckMeasuresHowLateSwingsLandOnTheBeat()
    {
        // A player whose swings register 190ms after each bounce they see (+/- a little).
        var check = new TennisBeatCalibration(10);
        float[] wobble = { .01f, -.02f, .015f, 0, -.01f, .02f, -.015f, .005f, 0 };
        for (int b = 0; b < TennisBeatCalibration.Beats; b++)
            Assert.AreEqual(b, check.Swing(check.BeatTime(b) + .19f + wobble[b]));
        Assert.IsTrue(check.Finished(check.End));
        Assert.AreEqual(TennisBeatCalibration.Beats - TennisBeatCalibration.WarmUp, check.Scored, "warm-up beats only set the rhythm");
        Assert.IsTrue(check.TryResult(out float lag));
        Assert.AreEqual(.19f, lag, .015f);

        // A second swing on the same bounce, or one between bounces, does not count twice.
        var again = new TennisBeatCalibration(0);
        Assert.AreEqual(3, again.Swing(again.BeatTime(3) + .1f));
        Assert.AreEqual(-1, again.Swing(again.BeatTime(3) + .15f));
        Assert.AreEqual(-1, again.Swing(again.BeatTime(4) - TennisBeatCalibration.Interval / 2));

        // Flailing is not a rhythm: no result rather than a wrong one.
        var wild = new TennisBeatCalibration(0);
        float[] scatter = { 0, 0, -.3f, .3f, -.25f, .28f, .0f, -.3f, .3f };
        for (int b = 0; b < TennisBeatCalibration.Beats; b++) wild.Swing(wild.BeatTime(b) + scatter[b]);
        Assert.IsFalse(wild.TryResult(out _));
        // Nor is standing still.
        Assert.IsFalse(new TennisBeatCalibration(0).TryResult(out _));

        // The ball is on the line at every beat and up in between.
        Assert.Less(check.Height(check.BeatTime(4) + .001f, out int onBeat), .01f); Assert.AreEqual(4, onBeat);
        Assert.Greater(check.Height(check.BeatTime(4) + TennisBeatCalibration.Interval / 2, out _), .95f);
    }
}

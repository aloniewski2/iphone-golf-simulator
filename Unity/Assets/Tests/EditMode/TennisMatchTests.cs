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

    [Test] public void MistimedServeGoesIntoTheNetAndGoodOnesLandIn()
    {
        var box = TennisRules.ServeTargetCentre(true, true);
        var good = TennisRules.JudgeServe(0, .8f, .2f, box, true);
        Assert.IsTrue(good.Struck); Assert.IsTrue(good.Legal);
        Assert.IsTrue(TennisRules.ServeIsIn(good.Landing, true, true));

        var late = TennisRules.JudgeServe(.75f, .8f, .2f, box, true);
        Assert.IsTrue(late.Struck); Assert.IsFalse(late.Legal);

        var early = TennisRules.JudgeServe(-.8f, .8f, .2f, box, true);
        Assert.IsFalse(early.Legal);

        // Every legal serve, anywhere in the window, must actually be a legal serve.
        for (float offset = -TennisRules.ServeLegalWindow; offset <= TennisRules.ServeLegalWindow; offset += .02f)
        {
            var v = TennisRules.JudgeServe(offset, .8f, .2f, box, true);
            if (v.Legal) Assert.IsTrue(TennisRules.ServeIsIn(v.Landing, true, true), $"offset {offset}");
        }
    }

    [Test] public void APatOrAFlatHandIsNotAServe()
    {
        var box = TennisRules.ServeTargetCentre(true, true);
        Assert.IsFalse(TennisRules.JudgeServe(0, .05f, .2f, box, true).Struck, "too weak");
        Assert.IsFalse(TennisRules.JudgeServe(0, .8f, 0f, box, true).Struck, "arm never raised");
    }

    [Test] public void TimingOnlyDecidesNetOrIn()
    {
        var box = TennisRules.ServeTargetCentre(true, true);
        // Placement is automatic: any legal serve goes to the same correct box, whatever the
        // timing. Timing decides only whether it clears the net.
        var sharp = TennisRules.JudgeServe(0, .8f, .2f, box, true);
        var scruffy = TennisRules.JudgeServe(.17f, .8f, .2f, box, true);
        Assert.IsTrue(sharp.Legal); Assert.IsTrue(scruffy.Legal);
        Assert.AreEqual(sharp.Landing, scruffy.Landing);
        Assert.AreEqual(sharp.Speed, scruffy.Speed, .001f, "speed comes from power, not timing");
        Assert.IsFalse(TennisRules.JudgeServe(.8f, .8f, .2f, box, true).Legal, "wildly mistimed hits the net");
        Assert.IsTrue(TennisRules.JudgeServe(.35f, .8f, .2f, box, true).Legal, "ordinary sloppy timing still goes in");
        // Power still decides pace.
        Assert.Greater(TennisRules.JudgeServe(0, 1f, .2f, box, true).Speed,
                       TennisRules.JudgeServe(0, .3f, .2f, box, true).Speed);
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
        // no serve could ever be legal however well it was timed.
        foreach (bool deuce in new[] { true, false })
        {
            var box = TennisRules.ServeTargetCentre(true, deuce);
            foreach (float contact in new[] { 1.2f, 2.0f, 2.45f, 2.8f })
            {
                Vector3 start = new Vector3(TennisRules.ServerStanceX(true, deuce), contact, -11.2f);
                foreach (float power in new[] { 0f, .5f, 1f })
                {
                    var v = TennisRules.JudgeServe(0, Mathf.Max(power, TennisRules.ServeMinPower), .2f, box, true);
                    Vector3 velocity = TennisRules.ServeVelocity(start, v.Landing, v.Speed);
                    float flight = Mathf.Abs(velocity.z) < .001f ? 1 : (v.Landing.z - start.z) / velocity.z;
                    Assert.Greater(TennisRules.NetCrossingHeight(start, v.Landing, velocity, flight),
                        TennisRules.NetHeight, $"contact {contact} power {power} deuce {deuce} must clear the net");
                    // And it must still land in the box it is required to hit.
                    Assert.IsTrue(TennisRules.ServeIsIn(v.Landing, true, deuce));
                }
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
        Assert.Less(TennisOpponent.ErrorChance(0f, .5f), .12f, "comfortable balls come back");
        // And a harder setting errs less at the same stretch.
        Assert.Less(TennisOpponent.ErrorChance(.8f, 1f), TennisOpponent.ErrorChance(.8f, 0f));
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
        Assert.AreEqual("Running", TennisRules.StrokeFor(1.0f, -9f, -11.2f, 2.4f, false));
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
}

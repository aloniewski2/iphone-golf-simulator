using NUnit.Framework;
using UnityEngine;
using GolfArcade.Tennis;

/// The campaign's difficulty curve, match formats and serve balance, checked without playing.
public class TennisCampaignBalanceTests
{
    static TennisMatch Play(TennisMatch m, params bool[] points) { foreach (var p in points) m.AwardPoint(p); return m; }
    static bool[] Game(bool toPlayer) => new[] { toPlayer, toPlayer, toPlayer, toPlayer };

    [Test] public void ShortSetIsFirstToThreeGames()
    {
        var m = TennisMatch.New(true);
        for (int g = 0; g < 2; g++) m = Play(m, Game(true));
        m = Play(m, Game(false));
        Assert.IsFalse(m.Complete);
        m = Play(m, Game(true));
        Assert.IsTrue(m.Complete); Assert.IsTrue(m.PlayerWonMatch); Assert.AreEqual("3–1", m.FinalScore);
    }

    [Test] public void SixGameSetsNeedTwoClearAndGoToATiebreakAtSixAll()
    {
        var m = TennisMatch.New(true, 2, 6);
        for (int g = 0; g < 5; g++) { m = Play(m, Game(true)); m = Play(m, Game(false)); }
        Assert.AreEqual(0, m.PlayerSets, "5–5 is not a set");
        m = Play(m, Game(true)); Assert.AreEqual(0, m.PlayerSets, "6–5 is not a set either");
        m = Play(m, Game(false));
        Assert.IsTrue(m.Tiebreak, "6–6 goes to a tiebreak");
        StringAssert.Contains("TIEBREAK", m.Scoreboard);
        // 6–6 in the tiebreak, then two clear points.
        for (int i = 0; i < 6; i++) { m.AwardPoint(true); m.AwardPoint(false); }
        Assert.IsTrue(m.Tiebreak, "a tiebreak needs two clear points");
        m.AwardPoint(true); m.AwardPoint(true);
        Assert.AreEqual(1, m.PlayerSets); Assert.AreEqual("7–6", m.SetScores[0]);
        Assert.IsFalse(m.Tiebreak); Assert.AreEqual(0, m.PlayerGames);
    }

    [Test] public void BestOfFiveNeedsThreeSets()
    {
        var m = TennisMatch.New(true, 3, 6);
        for (int set = 0; set < 5; set++)
        {
            bool player = set % 2 == 0;   // player, opponent, player, opponent, player
            for (int g = 0; g < 6; g++) m = Play(m, Game(player));
            if (set < 4) Assert.IsFalse(m.Complete, $"after set {set + 1}");
        }
        Assert.IsTrue(m.Complete); Assert.IsTrue(m.PlayerWonMatch);
        Assert.AreEqual("6–0 0–6 6–0 0–6 6–0", m.FinalScore);
    }

    [Test] public void EveryRivalIsTougherThanTheLast()
    {
        OpponentProfile prev = default;
        for (int i = 0; i < TennisRoster.All.Length; i++)
        {
            var r = TennisRoster.All[i];
            var p = r.Profile;
            if (i > 0)
            {
                Assert.Greater(r.Rung, TennisRoster.All[i - 1].Rung, r.Key);
            }
            prev = p;
        }
        var boss = TennisRoster.Find("Viktor").Profile;
        foreach (var r in TennisRoster.All)
        {
            if (r.Boss) continue;
            Assert.GreaterOrEqual(boss.Reach, r.Profile.Reach, r.Key);
            Assert.LessOrEqual(boss.Reaction, r.Profile.Reaction, r.Key);
            Assert.LessOrEqual(boss.UnforcedError, r.Profile.UnforcedError, r.Key);
        }
    }

    /// A simplified rally: a strong player's shots (good contact, aimed into the corners) against
    /// each rival's legs and hands, and the rival's shots against the player. The point-win rate
    /// should fall steadily from the first round to the final.
    static float PointWinRate(OpponentProfile p, int seed, int points = 4000)
    {
        var rng = new System.Random(seed);
        float R() => (float)rng.NextDouble();
        int won = 0;
        for (int n = 0; n < points; n++)
        {
            for (int shot = 0; ; shot++)
            {
                // The rival starts wherever its own last shot left it; a strong player goes for
                // the open court most of the time, with mostly good contact.
                float opponentX = (R() - .5f) * 3f;
                float aim = R() < .7f ? -Mathf.Sign(opponentX) * Mathf.Lerp(.4f, 1f, R()) : R() * 2 - 1;
                float q = Mathf.Lerp(.5f, 1f, R());
                var target = TennisRules.AimedTarget(aim, q, 0);
                float pace = Mathf.Lerp(18, 34, q);
                float flight = 22f / pace;
                // As in the game: after its reaction it runs toward where the ball will cross its
                // contact plane (a little wider than the bounce), then has its reach from there.
                float crossing = target.x * 1.25f;
                float goal = Mathf.Lerp(opponentX, crossing, .85f);
                float at = Mathf.MoveTowards(opponentX, goal, Mathf.Max(0, flight - p.Reaction) * p.Speed);
                var ret = TennisOpponent.Decide(at, crossing, 0, 0, 0, p, R(), R(), R(), Mathf.InverseLerp(14, 34, pace), p.Reach, 0, q);
                if (!ret.Reached || ret.Error) { won++; break; }
                // The rival's reply: width, depth and pace make the player miss more often.
                float threat = .45f * p.Width + .25f * p.Depth + .3f * Mathf.InverseLerp(14, 36, p.PaceMax) + .3f * p.WrongFoot + .2f * p.Hunt;
                if (R() < .05f + .16f * threat) break;
            }
        }
        return won / (float)points;
    }

    [Test] public void TheLadderGetsSteeplyHarderUpToAnAlmostImpossibleFinal()
    {
        int count = TennisRoster.All.Length;
        var rates = new float[count];
        var report = "";
        for (int i = 0; i < count; i++)
        {
            rates[i] = PointWinRate(TennisRoster.All[i].Profile, 11 + i);
            report += $"{TennisRoster.All[i].Key} {rates[i]:P0}  ";
        }
        TestContext.WriteLine(report);
        for (int i = 1; i < count; i++)
            Assert.Less(rates[i], rates[i - 1] + .03f, $"{TennisRoster.All[i].Key} is not easier than the rival before: " + report);
        float first = rates[0], last = rates[count - 1];
        Assert.Greater(first, .8f, "round one is winnable for a decent player: " + report);
        Assert.Less(last, .35f, "a strong player loses most points against Viktor: " + report);
        Assert.Greater(last, .15f, "…but he can be beaten with enough skill: " + report);
        Assert.Less(last, rates[count - 2] - .1f, "the final is a wall above the semifinal: " + report);
    }

    [Test] public void AGreenServeIsNotAFreeAceAgainstStrongReturners()
    {
        // A perfect serve aimed wide: the club player rarely gets it back, the champion usually does.
        float Returned(OpponentProfile p)
        {
            var rng = new System.Random(5); int back = 0, n = 2000;
            for (int i = 0; i < n; i++)
            {
                var s = TennisRules.JudgeServeStrike(0, 1, new Vector2(1, .9f), true, true, false, (float)rng.NextDouble() * 2 - 1, (float)rng.NextDouble() * 2 - 1);
                float flight = 21f / s.Speed;
                float stance = 1.2f;
                float x = s.Landing.x * 1.45f;   // it keeps travelling wide after the bounce
                float reach = p.Reach + p.ReturnReach + Mathf.Max(0, flight + .35f - p.ReturnReaction) * p.Speed * .6f;
                var r = TennisOpponent.Decide(stance * Mathf.Sign(x), x, 0, 0, 0, p, (float)rng.NextDouble(), .5f, .5f,
                    Mathf.InverseLerp(22, 52, s.Speed), reach, 0, .6f);
                if (r.Reached && !r.Error) back++;
            }
            return back / (float)n;
        }
        float club = Returned(TennisRoster.Find("Milo").Profile), boss = Returned(TennisRoster.Find("Viktor").Profile);
        TestContext.WriteLine($"perfect wide serve returned: Milo {club:P0}, Viktor {boss:P0}");
        Assert.Greater(boss, .6f, "the champion returns most perfect serves");
        Assert.Less(club, boss, "weaker returners get aced more");
    }

    [Test] public void FullAimLandsOnTheAimedSide()
    {
        var rng = new System.Random(3);
        int right = 0, n = 1000;
        for (int i = 0; i < n; i++)
        {
            var t = TennisRules.AimedTarget(1, .5f, 0);
            // The worst contacts scatter by up to ~20 degrees; a third of that is sideways.
            float error = ((float)rng.NextDouble() * 2 - 1) * 20f;
            float x = t.x + Mathf.Tan(error * .35f * Mathf.Deg2Rad) * (t.z + TennisRules.CourtHalfLength);
            if (x > 0) right++;
        }
        Assert.Greater(right / (float)n, .9f);
    }
}

using System.Collections;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    /// The phone turns a match into XP from these numbers, so they have to be right: sent once, before the
    /// result, and consistent with the score.
    public class TennisMatchStatsTests
    {
        [UnityTest, Timeout(600000)] public IEnumerator FinishedMatchReportsItsStatsOnce()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            int reports = 0; string line = null; int finishedAfterStats = 0;
            System.Action<string> onStats = s => { reports++; line = s; };
            System.Action<bool, string> onFinish = (won, score) => { if (reports > 0) finishedAfterStats++; };
            TennisGame.MatchStatsReady += onStats; TennisGame.MatchFinished += onFinish;
            try
            {
                // A one-game set (first to four points) played by the autoplayer.
                game.ConfigureMatch(TennisGame.Mode.Exhibition, "", "", "", 1, 1);
                game.AutoPlay = true; game.AutoPlayLean = true;
                float deadline = Time.realtimeSinceStartup + 240;
                while (!game.Match.Complete && Time.realtimeSinceStartup < deadline) yield return null;
                yield return new WaitForSeconds(.5f);
                Assert.IsTrue(game.Match.Complete, "the match should finish");
                Assert.AreEqual(1, reports, "stats are sent exactly once");
                Assert.AreEqual(1, finishedAfterStats, "and before the final result");
                StringAssert.Contains("pointsWon=", line);
                int Get(string key) { foreach (var kv in line.Split(';')) { var p = kv.Split('='); if (p[0] == key) return (int)float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture); } return -1; }
                Assert.AreEqual(game.Match.PlayerWonMatch ? 1 : 0, Get("won"));
                Assert.GreaterOrEqual(Get("pointsWon") + Get("pointsLost"), 4, "a game is at least four points");
                Assert.GreaterOrEqual(Get("longest"), 0);
            }
            finally { TennisGame.MatchStatsReady -= onStats; TennisGame.MatchFinished -= onFinish; game.AutoPlay = false; }
        }
    }
}

using System.Collections;
using System.IO;
using System.Linq;
using GolfArcade.Game;
using GolfArcade.Net.Online;
using GolfArcade.Profile;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GolfArcade.PlayTests
{
    /// When things go wrong the game carries on: a fault in the middle of a shot, a shot stuck in
    /// the air, faults again and again, an online server that isn't there, a dropped connection.
    public class ErrorHandlingTests
    {
        static IEnumerator WaitFor(System.Func<bool> done, float seconds, string what)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (!done())
            {
                if (Time.realtimeSinceStartup > until) Assert.Fail($"timed out waiting for {what}");
                yield return null;
            }
        }

        static IEnumerator Start(System.Action<GolfGame> ready)
        {
            Time.timeScale = 1f;
            yield return SceneManager.LoadSceneAsync("Golf", LoadSceneMode.Single);
            var game = Object.FindFirstObjectByType<GolfGame>();
            game.InstantReplays = false;
            yield return null;
            ready(game);
        }

        static string Words() => string.Join("\n", Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Where(t => t.isActiveAndEnabled).Select(t => t.text));

        [UnityTest]
        public IEnumerator AFaultInTheMiddleOfAShotIsGotOver()
        {
            LogAssert.ignoreFailingMessages = true;   // (the faults are logged, on purpose)
            GolfGame game = null;
            yield return Start(g => game = g);
            game.ChooseHoles(0); game.Play();
            yield return null;
            game.JumpToHole(12);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
            game.StrikeToward(game.CurrentHole.Pin);
            yield return WaitFor(() => game.Current == GolfGame.State.Flight && game.FlightTime > 0.3, 10, "the ball in the air");
            int before = ErrorGuard.Count;
            game.InjectFault = true;
            yield return null; yield return null;
            Assert.AreNotEqual(GolfGame.State.Flight, game.Current, "the shot was settled where it lay");
            StringAssert.Contains("CARRYING ON", Object.FindFirstObjectByType<Hud>().NoticeShowing ?? "");
            Assert.Greater(ErrorGuard.Count, before, "the fault was caught");
            StringAssert.Contains("a fault injected for the tests", File.ReadAllText(ErrorGuard.LogPath), "and written down");
            yield return WaitFor(() => game.Current is GolfGame.State.Aim or GolfGame.State.HoleDone or GolfGame.State.RoundDone, 20, "the round to go on");
        }

        [UnityTest]
        public IEnumerator AShotStuckInTheAirIsBroughtDown()
        {
            LogAssert.ignoreFailingMessages = true;
            GolfGame game = null;
            yield return Start(g => game = g);
            game.ChooseHoles(0); game.Play();
            yield return null;
            game.JumpToHole(7);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
            game.StrikeToward(game.CurrentHole.RecommendedTarget(game.CurrentHole.Tee));
            yield return WaitFor(() => game.Current == GolfGame.State.Flight, 10, "the ball in the air");
            game.StallForTests(120);   // as if it had been in the air two minutes
            yield return null; yield return null;
            Assert.AreNotEqual(GolfGame.State.Flight, game.Current, "the watchdog settled it");
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 20, "the next shot");
        }

        [UnityTest]
        public IEnumerator FaultAfterFaultGoesHome()
        {
            LogAssert.ignoreFailingMessages = true;
            GolfGame game = null;
            yield return Start(g => game = g);
            game.ChooseHoles(0); game.Play();
            yield return null;
            game.JumpToHole(12);
            yield return WaitFor(() => game.Current == GolfGame.State.Aim, 45, "the tee");
            for (int i = 0; i < 6; i++) { game.InjectFault = true; yield return null; }
            Assert.AreEqual(GolfGame.State.Menu, game.Current, "after fault on fault, the home screen");
            StringAssert.Contains("BACK TO THE MENU", Object.FindFirstObjectByType<Hud>().NoticeShowing ?? "");
            yield return new WaitForSecondsRealtime(11f);   // (and it settles: the next fault is a fresh start)
            game.Play();
            yield return WaitFor(() => game.Current is GolfGame.State.Intro or GolfGame.State.Aim, 10, "a new round");
        }

        [UnityTest]
        public IEnumerator AServerThatIsNotThereIsSaidPlainly()
        {
            LogAssert.ignoreFailingMessages = true;
            GolfGame game = null;
            yield return Start(g => game = g);
            var me = ProfileStore.Active;
            string url = BackendConfig.ServerUrl, id = me.ServerId, token = me.ServerToken;
            try
            {
                BackendConfig.ServerUrl = "http://127.0.0.1:9";   // nothing listens there
                me.ServerId = ""; me.ServerToken = "";
                game.ChooseHoles(0);
                yield return new WaitForSecondsRealtime(0.5f);
                int before = BackendClient.Requests;
                game.OpenLobby(Lobby.Page.Online);
                yield return new WaitForSecondsRealtime(6f);
                int made = BackendClient.Requests - before;
                string words = Words();
                Debug.Log($"ERRORS offline page: {made} requests; {words.Replace("\n", " | ")}");
                Assert.LessOrEqual(made, 4, "signing in isn't retried in a loop");
                StringAssert.Contains("TRY AGAIN", words);
                StringAssert.Contains("GAME SERVER", words, "the trouble in words");
                Assert.IsFalse(words.Contains("CANNOT CONNECT") || words.Contains("DESTINATION HOST"), "not the network's own jargon");
            }
            finally
            {
                OnlineSession.Instance?.Disconnect();
                BackendConfig.ServerUrl = url; me.ServerId = id; me.ServerToken = token; ProfileStore.Save();
            }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator OnlineAtHomeCopes()
        {
            LogAssert.ignoreFailingMessages = true;
            using (var probe = UnityWebRequest.Get("http://localhost:8080/healthz"))
            {
                probe.timeout = 3;
                yield return probe.SendWebRequest();
                if (probe.result != UnityWebRequest.Result.Success) Assert.Ignore("no game server running on this Mac");
            }
            GolfGame game = null;
            yield return Start(g => game = g);
            var me = ProfileStore.Active;
            string url = BackendConfig.ServerUrl, id = me.ServerId, token = me.ServerToken;
            try
            {
                BackendConfig.ServerUrl = "http://localhost:8080";
                me.ServerId = ""; me.ServerToken = "";
                game.ChooseHoles(0);
                yield return new WaitForSecondsRealtime(0.5f);
                game.OpenLobby(Lobby.Page.Online);
                yield return WaitFor(() => OnlineSession.Instance && OnlineSession.Instance.State == OnlineSession.Status.Online, 15, "online");
                var session = OnlineSession.Instance;
                // a code nobody has: said in words
                session.Send(OnlineMessage.Join("ZZZZ"));
                yield return WaitFor(() => Words().Contains("NO ROOM WITH THAT CODE"), 10, "the wrong code to be explained");
                // the Wi-Fi blips: back on its own
                session.DropForTests();
                yield return WaitFor(() => session.State != OnlineSession.Status.Online, 5, "the drop to be noticed");
                yield return WaitFor(() => session.State == OnlineSession.Status.Online, 20, "the connection to come back");
            }
            finally
            {
                OnlineSession.Instance?.Disconnect();
                BackendConfig.ServerUrl = url; me.ServerId = id; me.ServerToken = token; ProfileStore.Save();
            }
        }
    }
}

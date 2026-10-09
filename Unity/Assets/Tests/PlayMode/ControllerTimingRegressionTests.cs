using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public sealed class ControllerTimingRegressionTests
    {
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static readonly MethodInfo RenderHero = typeof(HeroTennisDriver).GetMethod("LateUpdate", Private);
        static readonly FieldInfo TossClock = typeof(TennisGame).GetField("phaseTimer", Private);
        static readonly FieldInfo Hand = typeof(NativeSportsSession).GetField("<Left>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        [TearDown] public void RestoreHand() => Hand.SetValue(null, false);
        static void Step(TennisGame game)
        {
            game.Step(TennisBall.Step);
            var hero = game.Player.GetComponentInChildren<HeroTennisDriver>();
            if (hero) RenderHero.Invoke(hero, null);
        }

        [UnityTest]
        public IEnumerator OnTimePhoneServeMakesRealContactAcrossScreenDelaysAndCharacters()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.NativeControlled = true;
            // Advance manually, but retain the production conversion from real controller
            // time to the slower serve clock. Disable Update to avoid a second simulation.
            game.ManualSimulation = false; game.enabled = false;
            game.GetComponent<TennisPresentation>().Finish();
            var failures = new List<string>();
            foreach (bool female in new[] { false, true })
            foreach (bool left in new[] { false, true })
            foreach (float delay in new[] { 0f, .18f, .4f, .7f })
            foreach (float toss in new[] { 1f, .85f })
            {
                Hand.SetValue(null, left);
                game.SelectCharacter(female);
                game.ConfigureMatch(TennisGame.Mode.Exhibition, null, null, null);
                game.SetControllerSetup(true); game.SetControllerSetup(false);
                game.StartPlayerServe(); game.DisplayLatency = delay;
                game.Toss(toss);
                for (int i = 0; i < 600 && game.Flow != TennisGame.Phase.PlayerServeToss; i++) Step(game);
                Assert.That(game.Flow, Is.EqualTo(TennisGame.Phase.PlayerServeToss));
                int misses = game.HonestMisses;
                const float deliveryAge = .02f;
                float onset = TennisRules.ServeApex + (delay + TennisRules.ServeOnsetLatency + deliveryAge) * TennisRules.ServePace;
                while ((float)TossClock.GetValue(game) < onset && game.Flow == TennisGame.Phase.PlayerServeToss) Step(game);
                game.BeginSwing(0, .3f, 1, deliveryAge);
                Assert.That(game.Player.Swinging, Is.True, "The detected stroke must animate immediately");
                for (int i = 0; i < 8; i++) Step(game);
                game.RequestSwing(.8f, 0, .3f, 1, deliveryAge);
                for (int i = 0; i < 240 && game.Flow == TennisGame.Phase.PlayerServeToss; i++) Step(game);
                string result = $"female={female} left={left} delay={delay:F2} toss={toss:F2} shots={game.RallyShots} misses={game.HonestMisses - misses} gap={game.LastContactGap:F3} {game.Feedback}";
                Debug.Log("[ControllerTimingRegression] " + result);
                if (game.RallyShots != 1 || game.HonestMisses != misses || game.LastContactGap > TennisGame.HonestContactGap)
                    failures.Add(result);
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        [UnityTest] public IEnumerator SwingCheckCanCorrectAnIncorrectFlashMeasurement()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.enabled=false;
            game.SetControllerSetup(true); game.DisplayLatency=.72f;
            var check=new TennisBeatCalibration(Time.unscaledTime-20,0,.72f);
            for(int i=0;i<TennisBeatCalibration.Beats;i++) check.Swing(check.BeatTime(i)+.18f);
            typeof(TennisGame).GetField("calibration",Private).SetValue(game,check);
            typeof(TennisGame).GetMethod("TickTimingCheck",Private).Invoke(game,null);
            Assert.That(game.CheckingTiming,Is.False);
            Assert.That(game.Lag,Is.EqualTo(.18f).Within(.001f),"A consistent swing measurement must replace a bad flash result");
            Assert.That(game.ControllerSetup,Is.True);
        }

        [UnityTest] public IEnumerator EmptySwingCheckCannotReportSuccessFromAnOldFlash()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>(); game.enabled=false;
            game.SetControllerSetup(true); game.DisplayLatency=.72f;
            var check=new TennisBeatCalibration(Time.unscaledTime-20,0,.72f);
            typeof(TennisGame).GetField("calibration",Private).SetValue(game,check);
            float reported=0;
            System.Action<float> receive=result=>reported=result;
            TennisGame.TimingChecked+=receive;
            try { typeof(TennisGame).GetMethod("TickTimingCheck",Private).Invoke(game,null); }
            finally { TennisGame.TimingChecked-=receive; }
            Assert.That(reported,Is.EqualTo(-1));
            Assert.That(game.Lag,Is.EqualTo(.72f).Within(.001f),"A failed check preserves the previous usable measurement");
        }

        [UnityTest] public IEnumerator LatestPacketRetainsStrokeAgeAndIgnoresAnOlderAbort()
        {
            yield return SceneManager.LoadSceneAsync("Tennis", LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>();
            game.enabled=false; game.NativeControlled=true; game.ManualSimulation=true;
            game.SetControllerSetup(true); game.SetControllerSetup(false); game.DisplayLatency=0;
            game.InjectBall(game.Player.transform.position+new Vector3(.6f,1.3f,2),new Vector3(0,0,-8));
            var root=new GameObject("Controller packet test");
            var adapter=root.AddComponent<NativeSportsSession>(); adapter.enabled=false;
            typeof(NativeSportsSession).GetField("tennis",Private).SetValue(adapter,game);
            typeof(NativeSportsSession).GetField("paused",Private).SetValue(adapter,false);
            typeof(NativeSportsSession).GetField("resumedAt",Private).SetValue(adapter,100d);
            var apply=typeof(NativeSportsSession).GetMethod("ApplySample",Private);
            var packet=new NativeSportsSession.Sample { version=NativeSportsSession.Sample.Version,session=1,time=100.2,flags=1,
                swingStart=2,swing=1,swingAbort=1,power=.8f,onsetTime=100.05,confirmationTime=100.13,abortTime=100.04 };
            apply.Invoke(adapter,new object[]{packet,100.21d});
            Assert.That(game.Player.Swinging,Is.True);
            Assert.That(game.Player.Provisional,Is.False,"An abort belonging to the previous candidate cannot cancel a newer confirmed stroke");
            Assert.That(game.Player.SwingAge,Is.GreaterThan(.1f),"The newest packet must compensate the original onset age, rather than restamping it as zero");
            game.AbortSwing(); game.Player.CancelSwing();
            packet.time=100.24; packet.swingStart=3; packet.swing=2; packet.onsetTime=99.8; packet.confirmationTime=99.9;
            apply.Invoke(adapter,new object[]{packet,100.25d});
            Assert.That(game.Player.Swinging,Is.False,"Fresh controller state cannot revive a stroke from before Ready");
            Object.Destroy(root);
        }
    }
}

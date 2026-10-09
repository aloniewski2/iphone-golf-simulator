using System.Collections;
using GolfArcade.Game;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Q = System.Numerics.Quaternion;
using V = System.Numerics.Vector3;

namespace GolfArcade.PlayTests {
    public class GolfMotionReadyTests {
        static void Sample(GolfGame game, ref double time, float angle, float rate, Q grip) {
            time += .01;
            Q q = Q.Normalize(Q.CreateFromAxisAngle(V.UnitX, angle) * grip);
            game.NativeMotion(new NativeSportsSession.Sample { time=time,qx=q.X,qy=q.Y,qz=q.Z,qw=q.W,rx=rate });
        }
        static void Stroke(GolfGame game, ref double time, Q grip) {
            float angle=0;
            foreach (var leg in new[]{(.2f,0f),(.8f,1.8f),(.15f,1.8f),(.25f,-.5f),(.2f,-.5f)}) {
                int n=Mathf.RoundToInt(leg.Item1*100); float delta=(leg.Item2-angle)/n;
                for(int i=0;i<n;i++) { angle+=delta; Sample(game,ref time,angle,delta/.01f,grip); }
            }
        }
        [UnityTest, Timeout(120000)] public IEnumerator EveryShotNeedsStartSwingAndImpactHasNoExtraDownswingDelay() {
            yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<GolfGame>(); game.PrepareNativeAddress();
            double time=1;
            foreach(var grip in new[]{Q.Identity,Q.CreateFromAxisAngle(V.UnitY,.8f)}) {
                game.DropBall(game.CurrentHole.Tee);
                game.RequireNativeReady();
                Assert.IsFalse(game.NativeShotReady);
                Stroke(game,ref time,grip);
                Assert.AreEqual(GolfGame.State.Aim,game.Current,"Motion without Ready must not spend a stroke");
                game.NativeReady();
                Assert.IsTrue(game.NativeShotReady);
                Assert.IsFalse(game.NativeSwingTracking,"Ready lines the shot up; it does not start reading the swing");
                Assert.AreEqual(0,game.NativeSwingState);
                Stroke(game,ref time,grip);
                Assert.AreEqual(GolfGame.State.Aim,game.Current,"Handling the phone before Start Swing must not spend a stroke");
                game.NativeStartSwing(grip);
                Assert.AreEqual(2,game.NativeSwingState,"A supplied grip is ready to swing at once");
                Stroke(game,ref time,grip);
                Assert.AreEqual(GolfGame.State.Flight,game.Current,"Each fresh grip must accept a complete stroke");
                Assert.IsFalse(game.NativeShotReady);
                Assert.IsFalse(game.NativeSwingTracking,"A shot ends the swing: the next one needs Start Swing again");
                Assert.GreaterOrEqual(game.FlightTime,0,"Physical impact must not schedule another animated downswing");
            }
        }
        [UnityTest, Timeout(120000)] public IEnumerator ChangingClubOrAimEndsAStartedSwing() {
            yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<GolfGame>(); game.PrepareNativeAddress();
            double time=1;
            game.DropBall(game.CurrentHole.Tee); game.NativeReady(); game.NativeStartSwing(Q.Identity);
            Assert.AreEqual(2,game.NativeSwingState);
            game.NativeClub(1);
            Assert.IsTrue(game.NativeShotReady,"Changing club must not strand the player behind Ready");
            Assert.IsFalse(game.NativeSwingTracking,"Changing club ends a started swing");
            Stroke(game,ref time,Q.Identity);
            Assert.AreEqual(GolfGame.State.Aim,game.Current,"A swing started before the club change must not count");
            game.NativeStartSwing(Q.Identity);
            game.NativeAimDirection(1,0);
            Assert.IsFalse(game.NativeSwingTracking,"A new aim ends a started swing");
            game.NativeStartSwing(Q.Identity);
            Stroke(game,ref time,Q.Identity);
            Assert.AreEqual(GolfGame.State.Flight,game.Current,"Start Swing after lining up must accept the stroke");
        }
        [UnityTest, Timeout(120000)] public IEnumerator StartSwingTakesTheGripOnlyOnceThePhoneIsHeldStill() {
            yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<GolfGame>(); game.PrepareNativeAddress();
            double time=1;
            game.DropBall(game.CurrentHole.Tee); game.NativeReady(); game.NativeStartSwing();
            Assert.AreEqual(1,game.NativeSwingState,"Without a grip the phone is asked to hold still");
            // lifting the phone from the tap into the stance is not a grip
            for(int i=0;i<100;i++) Sample(game,ref time,i*.02f,3f,Q.Identity);
            Assert.AreEqual(1,game.NativeSwingState,"A moving phone is not a grip");
            for(int i=0;i<30;i++) Sample(game,ref time,2f,0f,Q.Identity);
            Assert.AreEqual(1,game.NativeSwingState,"A moment's stillness is not yet a grip");
            for(int i=0;i<40;i++) Sample(game,ref time,2f,0f,Q.Identity);
            Assert.AreEqual(2,game.NativeSwingState,"Held still in the stance, the grip is set");
        }
    }
}

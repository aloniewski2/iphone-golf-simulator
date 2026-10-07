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
        [UnityTest, Timeout(120000)] public IEnumerator EveryShotNeedsReadyAndImpactHasNoExtraDownswingDelay() {
            yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);
            yield return null;
            var game=Object.FindFirstObjectByType<GolfGame>(); game.PrepareNativeAddress();
            double time=1;
            foreach(var grip in new[]{Q.Identity,Q.CreateFromAxisAngle(V.UnitY,.8f)}) {
                game.DropBall(game.CurrentHole.Tee);
                Assert.IsFalse(game.NativeShotReady);
                Stroke(game,ref time,grip);
                Assert.AreEqual(GolfGame.State.Aim,game.Current,"Motion without Ready must not spend a stroke");
                game.NativeReady(grip);
                Stroke(game,ref time,grip);
                Assert.AreEqual(GolfGame.State.Flight,game.Current,"Each fresh grip must accept a complete stroke");
                Assert.IsFalse(game.NativeShotReady);
                Assert.GreaterOrEqual(game.FlightTime,0,"Physical impact must not schedule another animated downswing");
            }
            game.DropBall(game.CurrentHole.Tee); game.NativeReady(Q.Identity); game.NativeClub(1);
            Assert.IsFalse(game.NativeShotReady,"Changing club requires Ready again");
            game.StartGolfCalibration(Q.Identity);
            for(int i=0;i<3;i++) {
                if(i>0) game.NativeReady(Q.Identity);
                Stroke(game,ref time,Q.Identity);
                Assert.AreEqual(i+1,game.NativeCalibrationCount);
                Assert.AreEqual(GolfGame.State.Aim,game.Current,"Practice swings cannot launch a ball");
                Assert.IsFalse(game.NativeShotReady,"Practice also needs Ready for every swing");
            }
            game.FinishGolfCalibration();
            Assert.IsFalse(game.NativeShotReady);
            Assert.Less(game.Swing.Detector.FullBackswing,2.0,"The comfortable practice range is learned");
        }
    }
}

using System.Collections;
using System.Linq;
using System.Reflection;
using GolfArcade.Course;
using GolfArcade.Game;
using GolfArcade.Shot;
using GolfArcade.Tennis;
using GolfArcade.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace GolfArcade.PlayTests
{
    public class GolfBroadcastTests
    {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Select(GolfGame game,GolfClub club)=>typeof(GolfGame).GetMethod("SelectClub",Private).Invoke(game,new object[]{club});
        static void InMap(Camera map,Vector3 point)
        {
            var v=map.WorldToViewportPoint(point);
            Assert.That(v.x,Is.InRange(.04f,.96f),"projection outside map horizontally");
            Assert.That(v.y,Is.InRange(.04f,.96f),"projection outside map vertically");
        }
        [UnityTest,Timeout(180000)]
        public IEnumerator OffCourseProjectionsAndAllClubChangesStayVisible()
        {
            Time.timeScale=1; TennisQuality.Apply();
            yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);
            var game=Object.FindFirstObjectByType<GolfGame>();yield return null;
            game.PrepareNativeAddress();
            foreach(var hole in new[]{7,12})
            {
                game.JumpToHole(hole);game.DropBall(game.CurrentHole.Tee);
                game.SetWind(new Wind(30,90));
                for(int i=0;i<30;i++)game.NativeAim(1);
                foreach(float aspect in new[]{16f/9f,9f/16f})
                {
                    game.GameplayCamera.aspect=aspect;
                    foreach(var club in GolfClubs.All)
                    {
                        Select(game,club);yield return null;yield return null;
                        var map=game.GetComponentsInChildren<Camera>().First(c=>c.orthographic);
                        var plan=game.TutorialHud.Map;
                        InMap(map,plan.Ball);InMap(map,plan.Pin);
                        foreach(var point in plan.Path)InMap(map,point);
                        foreach(var point in plan.Targets)InMap(map,point);
                        var broadcast=Object.FindFirstObjectByType<GolfShotHud>();
                        var marks=broadcast.GetComponentsInChildren<RectTransform>().Count(t=>new[]{"Landing 1","Landing 2","Landing 3","Landing 4"}.Contains(t.name));
                        Assert.That(marks,Is.EqualTo(plan.Targets.Count));
                        Assert.That(broadcast.GetComponentsInChildren<Text>().Any(t=>t.name=="Club name"&&t.text.Contains(club.DisplayName())),Is.True);
                        Assert.That(broadcast.GetComponentInChildren<GolfMapPath>(),Is.Not.Null);
                    }
                }
            }
            Select(game,GolfClub.Driver);yield return null;Select(game,GolfClub.Iron);yield return null;
            var change=Object.FindFirstObjectByType<GolfShotHud>().GetComponentsInChildren<Text>().First(t=>t.name=="Club change");
            Assert.That(change.text,Is.EqualTo("Driver → 7 Iron"));
            Select(game,GolfClub.Driver);game.NativeClub(-1);yield return null;
            Assert.That(change.text,Is.EqualTo("Driver → Putter"),"bag must wrap in both directions");
        }
        /// After a full swing, once the ball is down: a replay of the strike from the tee box (behind the golfer, looking up the
        /// line, the ball followed all the way), the phone offering to skip it, then the result card. A putt goes straight to its result.
        [UnityTest,Timeout(240000)]
        public IEnumerator AFullSwingIsReplayedFromTheTeeBoxBeforeItsResult()
        {
            int rate=Time.captureFramerate;Time.captureFramerate=30;Time.timeScale=1;
            yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);
            var game=Object.FindFirstObjectByType<GolfGame>();yield return null;
            try
            {
                game.PrepareNativeAddress();
                game.JumpToHole(7);game.DropBall(game.CurrentHole.Tee);Select(game,GolfClub.Iron);
                var cam=game.GameplayCamera;cam.aspect=16f/9f;yield return null;yield return null;
                game.NativeReady();game.NativeSwing(.8f);
                Assert.That(game.Current,Is.EqualTo(GolfGame.State.Flight));
                var origin=HoleView.ToWorld(game.LastShot.Origin);
                Vector3 line=HoleView.ToWorld(game.LastShot.Landing)-origin;line.y=0;line.Normalize();
                int frames=0;
                while(game.Current!=GolfGame.State.Replay&&frames++<1500){yield return null;Assert.That(game.ShowingShotResult,Is.False,"the result waits for the replay");}
                Assert.That(game.Current,Is.EqualTo(GolfGame.State.Replay),"a full swing is replayed");
                bool sawLanding=false;frames=0;
                while(game.Current==GolfGame.State.Replay&&frames++<1500)
                {
                    yield return null;if(game.Current!=GolfGame.State.Replay)break;
                    var behind=Vector3.Dot(cam.transform.position-origin,line);
                    Assert.That(behind,Is.LessThan(0),"the camera stays at the tee box, behind the ball");
                    Assert.That(Vector3.Dot(cam.transform.forward,line),Is.GreaterThan(0),"looking up the line");
                    var p=cam.WorldToViewportPoint(game.BallPosition);
                    if(Vector3.Distance(game.BallPosition,HoleView.ToWorld(game.LastShot.Landing))<3f&&p.z>0&&p.x>0&&p.x<1&&p.y>0&&p.y<1)sawLanding=true;
                }
                Assert.That(sawLanding,Is.True,"the ball is followed down to its landing");
                Assert.That(game.Current,Is.EqualTo(GolfGame.State.Result));Assert.That(game.ShowingShotResult,Is.True,"then the result");
                Assert.That(Time.timeScale,Is.EqualTo(1f),"slow motion ends with the replay");
            }
            finally{Time.captureFramerate=rate;Time.timeScale=1;}
        }
        [UnityTest,Timeout(240000)]
        public IEnumerator FlightCutsOnceAtLandingThenBothGolfersCanEmoteAndContinue()
        {
            int rate=Time.captureFramerate;var look=GolferStyle.Current.Clone();
            Time.captureFramerate=30;Time.timeScale=1;TennisQuality.Apply();
            yield return SceneManager.LoadSceneAsync("Golf",LoadSceneMode.Single);
            var game=Object.FindFirstObjectByType<GolfGame>();yield return null;
            try
            {
                game.PrepareNativeAddress();game.InstantReplays=false;
                foreach(bool female in new[]{false,true})
                {
                    GolferStyle.Body=female?GolferStyle.BodyKind.Female:GolferStyle.BodyKind.Male;
                    var golfer=game.GetComponentInChildren<GolferView>(true);golfer.ApplyStyle();
                    game.JumpToHole(7);game.DropBall(game.CurrentHole.Tee);Select(game,GolfClub.Iron);
                    var cam=game.GameplayCamera;cam.aspect=16f/9f;yield return null;yield return null;
                    var rig=cam.GetComponent<CameraRig>();game.NativeReady();game.NativeSwing(.76f);
                    Assert.That(game.Current,Is.EqualTo(GolfGame.State.Flight));
                    Vector3 line=HoleView.ToWorld(game.LastShot.Landing)-HoleView.ToWorld(game.LastShot.Origin);line.y=0;line.Normalize();
                    int frames=0, outside=0, airFrames=0,cuts=0;bool landed=false;
                    while(game.Current==GolfGame.State.Flight&&frames++<1500)
                    {
                        yield return null;if(game.Current!=GolfGame.State.Flight)break;
                        if(rig.LandingView!=landed){Assert.That(rig.LandingView,Is.True);landed=true;cuts++;}
                        Assert.That(Vector3.Dot(cam.transform.forward,line),Is.GreaterThan(0),"shot heading reversed");
                        Assert.That(Mathf.Abs(Vector3.Dot(cam.transform.right,Vector3.up)),Is.LessThan(.015f),"camera rolled");
                        if(game.FlightTime>CameraRig.LaunchHoldSeconds+.5f&&game.FlightTime<game.LastShot.LandingTime)
                        {
                            airFrames++;var p=cam.WorldToViewportPoint(game.BallPosition);
                            if(p.z<=0||p.x<.03||p.x>.97||p.y<.03||p.y>.97)outside++;
                        }
                    }
                    Assert.That(game.ShowingShotResult,Is.True);Assert.That(cuts,Is.EqualTo(1));
                    Assert.That(outside,Is.LessThanOrEqualTo(Mathf.Max(3,airFrames/20)),"airborne ball left view");
                    var position=cam.transform.position;var rotation=cam.transform.rotation;var shot=game.LastShot;
                    Assert.That(Vector3.Dot((position-golfer.transform.position).normalized,golfer.transform.forward),Is.GreaterThan(.5f),"result must show the face");
                    foreach(int emote in new[]{0,1,2})
                    {
                        Assert.That(game.PlayResultEmote(emote),Is.True);
                        for(int i=0,n=Mathf.CeilToInt(golfer.PerformanceDuration*30)+15;i<n;i++)
                        {
                            yield return null;Assert.That(Vector3.Distance(cam.transform.position,position),Is.LessThan(.01f));
                            Assert.That(Quaternion.Angle(cam.transform.rotation,rotation),Is.LessThan(.1f));
                        }
                        Assert.That(game.ResultEmote,Is.EqualTo("Idle"));
                    }
                    cam.aspect=9f/16f;
                    for(int i=0;i<45;i++)yield return null;
                    Vector3 feet=cam.WorldToViewportPoint(golfer.transform.position),head=cam.WorldToViewportPoint(golfer.transform.position+Vector3.up*2.16f);
                    Assert.That(feet.y,Is.GreaterThan(.23f),"phone emote footer overlaps feet");Assert.That(head.y,Is.LessThan(.85f));
                    Assert.That(feet.x,Is.InRange(.1f,.8f));Assert.That(game.LastShot,Is.SameAs(shot));
                    game.ContinueShotResult();yield return null;
                    Assert.That(game.Current,Is.EqualTo(GolfGame.State.Aim));Assert.That(game.ShowingShotResult,Is.False);
                }
                game.DropBall(new CoursePoint(game.CurrentHole.Pin.X,game.CurrentHole.Pin.D-3));
                Assert.That(game.StrikeHolingPutt(),Is.True);
                var puttRig=game.GameplayCamera.GetComponent<CameraRig>();int puttFrames=0;
                while(game.Current==GolfGame.State.Flight&&puttFrames++<1500){yield return null;Assert.That(puttRig.LandingView,Is.False,"putting should preserve the green read");}
                Assert.That(game.ShowingShotResult,Is.True);Assert.That(game.LastShot.IsHoled,Is.True);
            }
            finally{Time.captureFramerate=rate;Time.timeScale=1;GolferStyle.SaveDevice(look);}
        }
    }
}

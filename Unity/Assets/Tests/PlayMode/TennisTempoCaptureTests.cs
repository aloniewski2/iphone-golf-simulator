using System.Collections;
using System.IO;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public class TennisTempoCaptureTests
    {
        [UnityTest, Explicit, Timeout(1800000)]
        public IEnumerator FilmTempoComparison60()
        {
            int oldRate = Time.captureFramerate;
            float oldTempo = HeroTennisDriver.AnimationTempo;
            Time.captureFramerate = 60;
            try {
                foreach (float tempo in new[] { 1f, 1.2f }) {
                    HeroTennisDriver.AnimationTempo = tempo;
                    yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
                    var game = Object.FindFirstObjectByType<TennisGame>(); game.ManualSimulation = true;
                    game.ConfigureMatch(TennisGame.Mode.Training, null, null, null);
                    game.InjectBall(new Vector3(0,20,30), Vector3.zero);
                    foreach(var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) canvas.gameObject.SetActive(false);
                    var driver = game.Player.GetComponentInChildren<HeroTennisDriver>();
                    Assert.IsNotNull(driver);
                    var sample = typeof(HeroTennisDriver).GetMethod("StrokeTime",BindingFlags.Instance|BindingFlags.NonPublic);
                    foreach (var slot in driver.slots) if (slot.clip && slot.id >= HeroTennisDriver.Clip.Forehand && slot.id <= HeroTennisDriver.Clip.Smash)
                        Assert.That((float)sample.Invoke(driver,new object[]{(int)slot.id,0f}),Is.EqualTo(slot.contact).Within(.0001f),"tempo must preserve contact frame");
                    string dir = "Library/Captures/tempo-" + (tempo == 1 ? "100" : "120"); Directory.CreateDirectory(dir);
                    var strokes = new[] { TennisActor.Stroke.Drive,TennisActor.Stroke.Drive,TennisActor.Stroke.Serve,TennisActor.Stroke.Volley,TennisActor.Stroke.Smash };
                    int frame = 0;
                    for (int shot=0;shot<strokes.Length;shot++) {
                        game.Player.CancelSwing(); game.Player.Tick(3,0);
                        game.Player.transform.position = new Vector3(0,.035f,-10);
                        for (int f=0; f<150; f++) {
                            if (f==24) game.Player.Swing(.7f,shot==1,strokes[shot]);
                            game.Player.Tick(1f/60,0); game.Player.Pose();
                            yield return null;
                            var cam=game.GameplayCamera;
                            cam.transform.position=game.Player.transform.position+new Vector3(3.2f,2.1f,5);
                            cam.transform.LookAt(game.Player.transform.position+Vector3.up*1.05f); cam.fieldOfView=36;
                            GameCapture.Save($"{dir}/frame-{frame++:D4}.jpg",960,540);
                        }
                    }
                }
            } finally { HeroTennisDriver.AnimationTempo=oldTempo; Time.captureFramerate=oldRate; }
        }
    }
}

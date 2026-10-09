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
    public class TennisReceiveCameraTests
    {
        [UnityTest, Timeout(180000)]
        public IEnumerator ReceiverAndPointKeepShoulderGameplayCamera()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game = Object.FindFirstObjectByType<TennisGame>();
            game.ConfigureMatch(TennisGame.Mode.Exhibition, null, null, null);
            game.ManualSimulation = true;
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(TennisGame).GetField("match", flags).SetValue(game, TennisMatch.New(false, 1, 3));
            typeof(TennisGame).GetMethod("BeginPoint", flags).Invoke(game, null);
            string dir = Path.GetFullPath("../ArtDir/review/receive-camera"); Directory.CreateDirectory(dir);
            bool before = System.Environment.GetEnvironmentVariable("RECEIVE_BEFORE") == "1";
            var ffmpeg = System.Environment.GetEnvironmentVariable("RECEIVE_FFMPEG");
            System.Diagnostics.Process encoder = null;
            if (!string.IsNullOrEmpty(ffmpeg)) encoder = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                FileName=ffmpeg, Arguments="-y -hide_banner -loglevel error -f image2pipe -framerate 60 -vcodec mjpeg -i pipe:0 -an -c:v libx264 -preset veryfast -crf 20 -pix_fmt yuv420p -movflags +faststart \""+dir+(before?"/before.mp4":"/after.mp4")+"\"",
                UseShellExecute=false, RedirectStandardInput=true, CreateNoWindow=true });
            void Frame() { if(encoder==null)return; var path=dir+"/frame.jpg"; GameCapture.Save(path,960,540); var bytes=File.ReadAllBytes(path);encoder.StandardInput.BaseStream.Write(bytes,0,bytes.Length); }
            int oldRate = Time.captureFramerate; Time.captureFramerate = 60;
            try
            {
                for (int f=0; f<420; f++) {
                    typeof(TennisGame).GetField("serveTimer", flags).SetValue(game, .5f);
                    game.Step(1f/60); yield return null;
                    var reviewCam=game.GameplayCamera;
                    if(f>=120 && f<240) { reviewCam.transform.position=game.Player.transform.position+new Vector3(2.6f,1.4f,3);reviewCam.transform.LookAt(game.Player.transform.position+Vector3.up*.9f);reviewCam.fieldOfView=35; }
                    Frame();
                }
                Assert.That(game.Flow, Is.EqualTo(TennisGame.Phase.OpponentServe));
                var cam = game.GameplayCamera;
                if(!before) Assert.That(cam.transform.position.y - game.Player.transform.position.y, Is.InRange(2f, 2.5f));
                if(!before) Assert.Less(cam.transform.position.z, game.Player.transform.position.z - 3.5f);
                GameCapture.Save(dir + "/receiver-gameplay.png",1280,720);
                var pos=cam.transform.position; var rot=cam.transform.rotation; float fov=cam.fieldOfView;
                cam.transform.position=game.Player.transform.position + new Vector3(2.6f,1.4f,3);
                cam.transform.LookAt(game.Player.transform.position+Vector3.up*.9f); cam.fieldOfView=35;
                GameCapture.Save(dir + "/receiver-front.png",960,720);
                cam.transform.SetPositionAndRotation(pos,rot); cam.fieldOfView=fov;
                typeof(TennisGame).GetMethod("AwardPoint", flags).Invoke(game,new object[]{true,false});
                for(int f=0; f<120; f++) {
                    game.Step(1f/60); yield return null;
                    Frame();
                    if(!before) Assert.That(cam.transform.position.y-game.Player.transform.position.y,Is.InRange(2f,2.5f),"point result keeps the shoulder view");
                    if(!before) Assert.That(cam.fieldOfView, Is.EqualTo(fov), "point result does not switch lenses");
                    if(!before) Assert.Less(cam.transform.position.z,game.Player.transform.position.z-3.5f,"point result stays behind the player");
                }
                GameCapture.Save(dir + "/point-gameplay.png",1280,720);
                // All point phases retain the same shoulder framing.
                var flow = typeof(TennisGame).GetProperty("Flow");
                var updateCamera = typeof(TennisGame).GetMethod("UpdateCamera", flags);
                foreach (var phase in new[] { TennisGame.Phase.PlayerServeHold, TennisGame.Phase.PlayerServeToss }) {
                    flow.SetValue(game, phase);
                    updateCamera.Invoke(game, new object[] { true });
                    Assert.That(cam.transform.position.y - game.Player.transform.position.y, Is.InRange(2f,2.5f));
                    Assert.That(cam.fieldOfView, Is.InRange(60f,66f));
                }
                flow.SetValue(game, TennisGame.Phase.Rally);
                updateCamera.Invoke(game, new object[] { true });
                Assert.That(cam.transform.position.y-game.Player.transform.position.y, Is.InRange(2f,2.5f));

            }
            finally { Time.captureFramerate=oldRate; if(encoder!=null) { encoder.StandardInput.Close();encoder.WaitForExit(); Assert.That(encoder.ExitCode,Is.Zero);encoder.Dispose(); } }
        }
    }
}

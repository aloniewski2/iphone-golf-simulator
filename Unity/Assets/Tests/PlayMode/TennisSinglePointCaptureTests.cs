using System.Collections;
using System.Diagnostics;
using System.IO;
using GolfArcade.Game;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GolfArcade.PlayTests
{
    public class TennisSinglePointCaptureTests
    {
        [UnityTest, Explicit, Timeout(1800000)]
        public IEnumerator FilmOnePoint60()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>();
            int oldRate=Time.captureFramerate; Time.captureFramerate=60;
            Process encoder=null;
            string output=Path.GetFullPath(System.Environment.GetEnvironmentVariable("POINT_CAPTURE_OUTPUT") ?? "../ArtDir/anims/captures/OnePoint_PlayerPOV_60fps.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string temp="Library/Captures/onepoint-current.jpg";
            int frame=0,finishedFrames=0; bool complete=false; string score="";
            try {
                // Let the ordinary opening camera finish, then start a fresh standard match.
                for(int f=0;f<(TennisPresentation.Length+2)*60;f++) yield return null;
                game.ConfigureMatch(TennisGame.Mode.Exhibition,null,"Rival","CASUAL MATCH");
                game.AutoPlay=true; game.AutoPlayLean=true;
                encoder=Process.Start(new ProcessStartInfo {
                    FileName=System.Environment.GetEnvironmentVariable("POINT_CAPTURE_FFMPEG") ?? "/private/tmp/tennis-preview-runtime/lib/python3.14/site-packages/imageio_ffmpeg/binaries/ffmpeg-macos-aarch64-v7.1",
                    Arguments="-y -hide_banner -loglevel error -f image2pipe -framerate 60 -vcodec mjpeg -i pipe:0 -an -c:v libx264 -preset veryfast -crf 19 -pix_fmt yuv420p -r 60 -movflags +faststart \""+output+"\"",
                    UseShellExecute=false,RedirectStandardInput=true,CreateNoWindow=true
                });
                for(;frame<60*120;frame++) {
                    if(game.Flow == TennisGame.Phase.PointOver || game.Flow == TennisGame.Phase.MatchOver) {
                        if(!complete) { complete=true; score=game.Match.Scoreboard; game.AutoPlay=false; }
                        if(++finishedFrames>=90) break;
                    }
                    yield return null;
                    GameCapture.Save(temp,1280,720);
                    byte[] jpg=File.ReadAllBytes(temp); encoder.StandardInput.BaseStream.Write(jpg,0,jpg.Length);
                    if(frame%600==0) UnityEngine.Debug.Log($"[OnePointCapture] seconds={frame/60} score={game.Match.Scoreboard} hits={game.Hits}");
                }
                encoder.StandardInput.Close(); encoder.WaitForExit(); Assert.That(encoder.ExitCode,Is.Zero);
                File.WriteAllText(Path.ChangeExtension(output,".txt"),$"Unity gameplay camera; autoplay inputs; 60 fps; {frame} frames; complete={complete}; score={score}; hits={game.Hits}; longest rally={game.LongestRally}; visual tempo={HeroTennisDriver.AnimationTempo}. Silent capture.\n");
                Assert.IsTrue(complete,"must film the completed point, not a time-limited excerpt");
            } finally {
                game.AutoPlay=false; Time.captureFramerate=oldRate;
                if(encoder!=null && !encoder.HasExited) { encoder.StandardInput.Close(); encoder.WaitForExit(10000); }
                encoder?.Dispose();
            }
        }

        [UnityTest, Explicit, Timeout(1800000)]
        public IEnumerator FilmTwoPoints60()
        {
            yield return SceneManager.LoadSceneAsync("Tennis"); yield return null;
            var game=Object.FindFirstObjectByType<TennisGame>();
            int oldRate=Time.captureFramerate; Time.captureFramerate=60;
            Process encoder=null;
            string output=Path.GetFullPath(System.Environment.GetEnvironmentVariable("POINT_CAPTURE_OUTPUT") ?? "../work/gameplay-capture/two_points.mp4");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            string temp="Library/Captures/twopoints-current.jpg";
            int frame=0,points=0,tail=0; bool wasOver=false;
            try {
                for(int f=0;f<(TennisPresentation.Length+2)*60;f++) yield return null;
                game.ConfigureMatch(TennisGame.Mode.Exhibition,null,"Rival","CASUAL MATCH");
                game.AutoPlay=true; game.AutoPlayLean=true;
                encoder=Process.Start(new ProcessStartInfo {
                    FileName=System.Environment.GetEnvironmentVariable("POINT_CAPTURE_FFMPEG"),
                    Arguments="-y -hide_banner -loglevel error -f image2pipe -framerate 60 -vcodec mjpeg -i pipe:0 -an -c:v libx264 -preset veryfast -crf 19 -pix_fmt yuv420p -r 60 -movflags +faststart \""+output+"\"",
                    UseShellExecute=false,RedirectStandardInput=true,CreateNoWindow=true
                });
                for(;frame<60*120;frame++) {
                    bool over=game.Flow == TennisGame.Phase.PointOver || game.Flow == TennisGame.Phase.MatchOver;
                    if(over && !wasOver) points++;
                    wasOver=over;
                    if(points>=2 && ++tail>=90) break;
                    yield return null;
                    GameCapture.Save(temp,1280,720);
                    byte[] jpg=File.ReadAllBytes(temp); encoder.StandardInput.BaseStream.Write(jpg,0,jpg.Length);
                    if(frame%600==0) UnityEngine.Debug.Log($"[TwoPointCapture] seconds={frame/60} points={points} score={game.Match.Scoreboard} hits={game.Hits}");
                }
                encoder.StandardInput.Close(); encoder.WaitForExit(); Assert.That(encoder.ExitCode,Is.Zero);
                File.WriteAllText(Path.ChangeExtension(output,".txt"),$"Unity gameplay camera; autoplay; 60 fps; {frame} frames; points={points}; score={game.Match.Scoreboard}; venue={TennisVenue.Current}\n");
                Assert.GreaterOrEqual(points,2);
            } finally {
                game.AutoPlay=false; Time.captureFramerate=oldRate;
                if(encoder!=null && !encoder.HasExited) { encoder.StandardInput.Close(); encoder.WaitForExit(10000); }
                encoder?.Dispose();
            }
        }
    }
}

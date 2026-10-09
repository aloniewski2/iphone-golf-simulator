using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Multiplayer;
using GolfArcade.Course;
using GolfArcade.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace GolfArcade.EditorTools {
    [InitializeOnLoad] public static class GolfPartyProof {
        const string Key="GolfPartyProof";
        static int stage,frames;static double began;static GolfGame game;static SportsMultiplayer net;static Camera cam;
        static string output;static bool reactions;
        static GolfPartyProof(){EditorApplication.update+=Tick;}
        public static void Run(){
            var path=Environment.GetEnvironmentVariable("GOLF_PARTY_OUT");if(string.IsNullOrEmpty(path))throw new Exception("GOLF_PARTY_OUT missing");
            Directory.CreateDirectory(path);SessionState.SetString(Key+"out",Path.GetFullPath(path));SessionState.SetBool(Key,true);
            EditorSceneManager.OpenScene("Assets/Scenes/Golf.unity");EditorApplication.isPlaying=true;
        }
        static void Tick(){
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            try {
                if(!game){
                    if(++frames<50||!HoleView.Current)return;
                    game=Object.FindFirstObjectByType<GolfGame>();if(!game)return;
                    output=SessionState.GetString(Key+"out","");
                    PresentationPolicy.Configure("off",false,false,true);game.PrepareNativeAddress("cliffside");
                    var c=new NetworkConfiguration {lobbyID="proof",matchID="proof",hostID="host",localID="host",sport="golf",venue="cliffside",seed=27,participants=new[]{
                        P("host","ADNAN",0,false,"79C8EA"),P("guest1","GUEST 1",1,true,"F2A661"),P("guest2","GUEST 2",2,false,"A0CE89"),P("guest3","GUEST 3",3,true,"D7ACE8")}};
                    SportsMultiplayer.Configure(JsonUtility.ToJson(c));net=SportsMultiplayer.Instance;cam=game.GameplayCamera;cam.aspect=16f/9f;
                    net.Receive(JsonUtility.ToJson(new NetworkPacket {lobbyID="proof",matchID="proof",sender="host",kind="run",payload="0"}));
                    began=EditorApplication.timeSinceStartup;frames=0;return;
                }
                if(EditorApplication.timeSinceStartup-began>120)throw new Exception("Party proof timed out at "+net.GolfState.phase);
                frames++;
                if(stage==0&&net.GolfState.phase=="aim"&&frames>40){
                    Capture("party-address.png");net.Submit(new NetworkInput {action="swing",power=.65f});stage=1;return;
                }
                if(stage==1&&net.GolfState.phase=="result"){
                    if(!reactions){
                        if(game.Current!=GolfGame.State.Result)return;
                        Capture("shot-result-start.png");
                        foreach(var p in net.GolfState.golfers)if(!game.PlayNetworkEmote(p.seat,p.seat%3))throw new Exception("Emote rejected seat "+p.seat);
                        reactions=true;
                    }
                    if(net.GolfState.resultUntil-net.GolfState.time<2.2){
                        Capture("shot-result-emotes.png");File.WriteAllText(Path.Combine(output,"result-state.json"),JsonUtility.ToJson(net.GolfState,true));
                        stage=2;
                    }
                    return;
                }
                if(stage==2&&net.GolfState.phase=="aim"){
                    if(net.GolfState.turn!=1)throw new Exception("Wrong next seat");
                    if(frames%10!=0)return;
                    Capture("next-player.png");
                    var roster=game.NativeControllerReading().party;
                    if(!roster.myTurn||roster.players.Count(p=>p.controlled)!=4)throw new Exception("Shared phone did not hand over");
                    File.WriteAllText(Path.Combine(output,"GATE_RESULTS.txt"),"PASS: four golfers; four accepted emotes; result display; automatic handoff to guest; host controls all local seats.\n");
                    SessionState.SetBool(Key,false);EditorApplication.Exit(0);
                }
            }catch(Exception e){Debug.LogException(e);if(output!=null)File.WriteAllText(Path.Combine(output,"GATE_RESULTS.txt"),"FAIL: "+e);SessionState.SetBool(Key,false);EditorApplication.Exit(1);}
        }
        static NetworkParticipant P(string id,string name,int seat,bool female,string shirt)=>new NetworkParticipant {
            id=id,name=name,seat=seat,female=female,controllerID=seat==0?null:"host",loadout=new NetworkEmoteLoadout {skinHex=female?"D6A17C":"F2D0AF",shirtHex=shirt,shortsHex="23344D",emotes=new[]{"wave","scuba","spike"}}};
        static void Capture(string name){
            var rt=new RenderTexture(1600,900,24){antiAliasing=4};rt.Create();var tex=new Texture2D(1600,900,TextureFormat.RGB24,false);
            var old=cam.targetTexture;var active=RenderTexture.active;
            try{cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,900),0,0);tex.Apply();File.WriteAllBytes(Path.Combine(output,name),tex.EncodeToPNG());}
            finally{cam.targetTexture=old;RenderTexture.active=active;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);}
        }
    }
}

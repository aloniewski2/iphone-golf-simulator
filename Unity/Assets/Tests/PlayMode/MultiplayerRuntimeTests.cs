using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GolfArcade.Game;
using GolfArcade.Multiplayer;
using GolfArcade.Tennis;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace GolfArcade.Tests {
    public class MultiplayerRuntimeTests {
        static NetworkConfiguration Config(string sport="tennis",string local="a")=>new() {lobbyID="party",matchID="contest",hostID="a",localID=local,sport=sport,venue="resort",sets=1,games=3,seed=123,
            participants=new[] {new NetworkParticipant{id="a",name="Alice",seat=0},new NetworkParticipant{id="b",name="Bob",seat=1},new NetworkParticipant{id="c",name="Cora",seat=sport=="golf"?2:-1},new NetworkParticipant{id="d",name="Dan",seat=sport=="golf"?3:-1}}};
        static string Packet(NetworkConfiguration c,string kind,string payload="",string sender="a",string match=null)=>JsonUtility.ToJson(new NetworkPacket {lobbyID=c.lobbyID,matchID=match??c.matchID,sender=sender,kind=kind,payload=payload});
        static void Clean(){SportsMultiplayer.TestOutput=null;SportsMultiplayer.Shutdown();if(SportsMultiplayer.Instance)Object.DestroyImmediate(SportsMultiplayer.Instance.gameObject);Time.timeScale=1;}

        [UnityTest] public IEnumerator EquippedIntroChoiceDoesNotExtendThePresentationBudget() {
            Clean();Time.captureFramerate=10;
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Tennis");
            var game=Object.FindFirstObjectByType<TennisGame>();
            for(int i=0;i<120 && (!game || !game.Initialized);i++)yield return null;
            Assert.True(game && game.Initialized);
            game.NativeControlled=true;game.AutoPlay=false;game.ManualSimulation=false;
            game.EquipEmotes(new[]{"scuba","wave","pushups"});
            var presentation=game.GetComponent<TennisPresentation>();

            var driver=game.Player.GetComponentInChildren<HeroTennisDriver>();int before=driver.EmotesPlayed;
            try {
                Assert.AreEqual("intro",game.EmoteWindow);
                Assert.True(game.RequestEquippedEmote(2));Assert.False(game.RequestEquippedEmote(0));
                for(int i=0;i<40 && driver.EmotesPlayed==before;i++)yield return null;
                Assert.AreEqual(before+1,driver.EmotesPlayed);Assert.AreEqual("IntroPushups",driver.LastEmotePlayed);

                for(int i=0;i<100 && game.IntroPlaying;i++)yield return null;
                Assert.False(game.IntroPlaying,"A long equipped animation cannot extend the intro cap");
                Assert.AreNotEqual("intro",game.EmoteWindow);
            } finally {Time.captureFramerate=0;Clean();}
        }

        [UnityTest] public IEnumerator EquippedOnlineEmotePlaysOnRemoteActorAndControllerSendsSlot() {
            Clean();Time.captureFramerate=10;
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Tennis");
            var game=Object.FindFirstObjectByType<TennisGame>();
            for(int i=0;i<120 && (!game || !game.Initialized);i++)yield return null;
            Assert.True(game && game.Initialized);
            var c=Config(local:"b");var sent=new List<string>();SportsMultiplayer.TestOutput=sent.Add;
            SportsMultiplayer.Configure(JsonUtility.ToJson(c));var net=SportsMultiplayer.Instance;
            var state=new NetworkTennisMatch(intro:true).State;
            state.players[0].emoteID="scuba";state.players[0].emoteSequence=1;state.players[0].emoteUntil=5.2;
            try {
                var driver=game.Opponent.GetComponentInChildren<HeroTennisDriver>();int before=driver.EmotesPlayed;
                net.Receive(Packet(c,"snapshot",JsonUtility.ToJson(state)));net.Receive(Packet(c,"run"));
                for(int i=0;i<40 && driver.EmotesPlayed==before;i++)yield return null;
                Assert.AreEqual(before+1,driver.EmotesPlayed);Assert.AreEqual("EmoteScuba",driver.LastEmotePlayed);
                Assert.AreEqual("intro",game.EmoteWindow);
                SportsMultiplayer.Command(new NativeSportsSession.Message{action="emote",value=2});
                var packet=sent.ConvertAll(JsonUtility.FromJson<NetworkPacket>).Find(p=>p.kind=="input");
                Assert.NotNull(packet);var input=JsonUtility.FromJson<NetworkInput>(packet.payload);
                Assert.AreEqual("emote",input.action);Assert.AreEqual(2,input.value);
                Assert.AreEqual(state.point,input.point);Assert.AreEqual(state.contact,input.contact);
            } finally {Time.captureFramerate=0;Clean();}
        }

        [UnityTest] public IEnumerator EquippedEmotePlaysRequestedClipOnBothBodies() {
            Clean();Time.captureFramerate=10;
            yield return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Tennis");
            var game=Object.FindFirstObjectByType<TennisGame>();
            for(int i=0;i<120 && (!game || !game.Initialized);i++) yield return null;
            Assert.True(game && game.Initialized);
            game.NativeControlled=true;game.AutoPlay=false;game.ManualSimulation=true;
            game.GetComponent<TennisPresentation>().Finish();
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            try {
                foreach(bool female in new[]{false,true}) {
                    game.SelectCharacter(female);
                    typeof(TennisGame).GetProperty("Flow").GetSetMethod(true).Invoke(game,new object[]{TennisGame.Phase.PointOver});
                    typeof(TennisGame).GetField("resetTimer",flags).SetValue(game,100f);
                    for(int i=0;i<15;i++) yield return null;
                    var driver=game.Player.GetComponentInChildren<HeroTennisDriver>();Assert.True(driver);
                    foreach(string id in TennisEmotes.IDs) {
                        game.EquipEmotes(new[]{id,"wave","spike"});
                        typeof(TennisGame).GetField("playerWonPoint",flags).SetValue(game,false);
                        typeof(TennisGame).GetField("pointEmoteChosen",flags).SetValue(game,false);
                        Assert.False(game.RequestEquippedEmote(0),"Cannot celebrate an opponent point");
                        typeof(TennisGame).GetField("playerWonPoint",flags).SetValue(game,true);
                        int before=driver.EmotesPlayed;
                        Assert.True(game.RequestEquippedEmote(0),id+" equipped slot must be accepted");
                        Assert.False(game.RequestEquippedEmote(1),"Only one selection per point");
                        for(int i=0;i<40 && driver.EmotesPlayed==before;i++)yield return null;
                        Assert.AreEqual(before+1,driver.EmotesPlayed);
                        TennisEmotes.TryClip(id,out var clip);Assert.AreEqual(clip.ToString(),driver.LastEmotePlayed);
                        Assert.GreaterOrEqual((float)typeof(TennisGame).GetField("resetTimer",flags).GetValue(game),driver.EmoteDuration(clip)*TennisGame.GameSpeed);
                        for(int i=0;i<70 && driver.EmoteActive;i++)yield return null;
                        Assert.False(driver.EmoteActive,id+" completes rather than looping");
                    }
                }
            } finally {Time.captureFramerate=0;Clean();}
        }

        [UnityTest] public IEnumerator AuthorityRejectsObserverInputAndOldContest() {
            Clean();var c=Config();SportsMultiplayer.Configure(JsonUtility.ToJson(c));var net=SportsMultiplayer.Instance;
            try {
                net.Receive(Packet(c,"run",sender:"c"));Assert.False(net.Running);net.Receive(Packet(c,"run"));Assert.True(net.Running);
                var toss=new NetworkInput {action="toss",point=net.TennisState.point,contact=net.TennisState.contact,eventID=1,time=Time.realtimeSinceStartupAsDouble};
                net.Receive(Packet(c,"input",JsonUtility.ToJson(toss),"c"));Assert.AreEqual("intro",net.TennisState.phase);
                net.Receive(Packet(c,"input",JsonUtility.ToJson(toss),"a","older"));Assert.AreEqual("intro",net.TennisState.phase);
                net.TennisState.phase="serve";net.Receive(Packet(c,"input",JsonUtility.ToJson(toss)));Assert.AreEqual("toss",net.TennisState.phase);yield return null;
            } finally {Clean();}
        }
        [UnityTest] public IEnumerator ObserverCannotEmitControlsAndClientRejectsOldSnapshots() {
            Clean();var c=Config(local:"c");var sent=new List<string>();SportsMultiplayer.TestOutput=sent.Add;SportsMultiplayer.Configure(JsonUtility.ToJson(c));var net=SportsMultiplayer.Instance;
            try {
                net.Receive(Packet(c,"run"));net.Submit(new NetworkInput {action="toss"});Assert.AreEqual(0,sent.Count);
                var state=new NetworkTennisMatch().State;state.tick=20;net.Receive(Packet(c,"snapshot",JsonUtility.ToJson(state)));state.tick=19;net.Receive(Packet(c,"snapshot",JsonUtility.ToJson(state)));Assert.AreEqual(20,net.TennisState.tick);
                state.tick=30;net.Receive(Packet(c,"snapshot",JsonUtility.ToJson(state),"b"));Assert.AreEqual(20,net.TennisState.tick);yield return null;
            } finally {Clean();}
        }
        [UnityTest] public IEnumerator FarSideInputUsesCanonicalCoordinatesAndSameSampleOrder() {
            Clean();var c=Config(local:"b");var sent=new List<string>();SportsMultiplayer.TestOutput=sent.Add;SportsMultiplayer.Configure(JsonUtility.ToJson(c));var net=SportsMultiplayer.Instance;
            try {
                var state=new NetworkTennisMatch().State;state.phase="rally";state.receiver=1;net.Receive(Packet(c,"snapshot",JsonUtility.ToJson(state)));net.Receive(Packet(c,"run"));
                SportsMultiplayer.Sample(new NativeSportsSession.Sample {flags=1,target=.5f,aim=.25f,power=.7f,swingStart=1,swing=1,time=Time.realtimeSinceStartupAsDouble},false);
                var inputs=new List<NetworkInput>();foreach(var json in sent){var p=JsonUtility.FromJson<NetworkPacket>(json);if(p.kind=="input")inputs.Add(JsonUtility.FromJson<NetworkInput>(p.payload));}
                Assert.AreEqual("move",inputs[0].action);Assert.AreEqual(-.5f,inputs[0].target);Assert.AreEqual(-.25f,inputs[0].aim);
                Assert.AreEqual("beginSwing",inputs[1].action);Assert.AreEqual("swing",inputs[2].action);Assert.AreEqual(state.point,inputs[2].point);yield return null;
            } finally {Clean();}
        }
        [UnityTest] public IEnumerator ReliableGolfCheckpointIncludesFlightPathForLateViewer() {
            Clean();var c=Config("golf");var sent=new List<string>();SportsMultiplayer.TestOutput=sent.Add;SportsMultiplayer.Configure(JsonUtility.ToJson(c));var net=SportsMultiplayer.Instance;
            try {
                net.Receive(Packet(c,"run"));net.Submit(new NetworkInput {action="swing",power=.5f});net.Receive(Packet(c,"snapshotRequest"));
                bool shot=false,checkpoint=false;
                foreach(var json in sent){var p=JsonUtility.FromJson<NetworkPacket>(json);if(p.kind=="golfShot")shot=JsonUtility.FromJson<NetworkGolfShot>(p.payload).path.Length>1;if(p.kind=="snapshot"&&p.reliable){var s=JsonUtility.FromJson<NetworkGolfState>(p.payload);if(s.shot!=null)checkpoint=s.shot.path.Length>1;}}
                Assert.True(shot);Assert.True(checkpoint);yield return null;
            } finally {Clean();}
        }
        /// Pass the phone (two golfers on the host's phone) plays as single player: the shot set up with single player's line and
        /// club and told to the host, the view looked up the hole to the pin and shown on the TV, the backswing on the TV, the
        /// strike's grade kept, and the shot flown with single player's flight (the host's ruling, to the yard), then the next
        /// golfer's shot lined up the same way.
        [UnityTest] public IEnumerator PassThePhoneGolfPlaysAsSinglePlayer() {
            Clean();var root=new GameObject("Native multiplayer golf");Object.DontDestroyOnLoad(root);var bridge=root.AddComponent<NativeSportsSession>();
            var c=new NetworkConfiguration {lobbyID="party",matchID="pass",hostID="a",localID="a",sport="golf",venue="cliffside",seed=7,
                participants=new[] {new NetworkParticipant{id="a",name="Alice",seat=0},new NetworkParticipant{id="g",name="Gus",seat=1,controllerID="a"}}};
            Assert.True(c.Valid);
            try {
                bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session="pass",action="start",sport="golf",course="cliffside",touch=true,network=JsonUtility.ToJson(c)}));
                for(int frame=0;frame<600&&!bridge.Ready;frame++)yield return null;
                Assert.True(bridge.Ready);
                bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session="pass",action="resume"}));
                var net=SportsMultiplayer.Instance;net.Receive(Packet(c,"run"));
                for(int i=0;i<30&&!net.Running;i++)yield return null;   // (the start is scheduled on the shared clock)
                Assert.True(net.Running);
                var game=Object.FindFirstObjectByType<GolfGame>();
                for(int i=0;i<900&&(net.GolfState.phase!="aim"||game.Current!=GolfGame.State.Aim);i++)yield return null;
                Assert.AreEqual("aim",net.GolfState.phase);Assert.AreEqual(GolfGame.State.Aim,game.Current);
                for(int i=0;i<5;i++)yield return null;
                var state=net.GolfState;var up=state.golfers[0];
                // single player's set-up, and the host has it
                Assert.True(game.NativeShotReady,"the player up can swing");
                Assert.AreEqual((int)game.ClubInHand,up.club,"the host has the club the phone set up");
                Assert.AreEqual(0,Mathf.DeltaAngle((float)game.AimHeading,(float)up.heading),.1,"the host has the line the phone set up");
                // the joystick up: the view climbs to look along the hole to the pin, and the TV is told
                var camera=game.GameplayCamera.transform;float pitchBefore=camera.eulerAngles.x;
                game.LookHeld=1;for(int i=0;i<60;i++)yield return null;
                Assert.Greater(game.AimLook,.9f);Assert.Greater(state.look,.8f,"the TV is told where the view looks");
                game.LookHeld=0;for(int i=0;i<60;i++)yield return null;
                Assert.Less(game.AimLook,.1f);
                // the backswing goes to the TV, and the swing is struck as single player strikes it
                game.ShowBackswing(.7);yield return null;
                Assert.AreEqual(.7f,state.load,.03f);
                game.NativeSwing(.8f);yield return null;
                Assert.AreEqual("flight",net.GolfState.phase);
                var shot=net.GolfState.shot;
                Assert.AreEqual(GolfGame.State.Flight,game.Current,"flown with single player's flight");
                Assert.NotNull(game.LastShot);
                Assert.AreEqual(shot.restX,game.LastShot.Rest.X,1e-6);Assert.AreEqual(shot.restD,game.LastShot.Rest.D,1e-6);
                Assert.AreNotEqual(0,shot.impact.speedBonus,"the strike's own speed is kept");
                // the result, then Gus's shot, lined up on this phone the same way
                for(int i=0;i<3000&&net.GolfState.phase!="result";i++)yield return null;
                for(int i=0;i<3000&&game.Current!=GolfGame.State.Result;i++)yield return null;
                Assert.AreEqual(GolfGame.State.Result,game.Current);
                for(int i=0;i<3000&&!(net.GolfState.phase=="aim"&&net.GolfState.turn==1);i++)yield return null;
                for(int i=0;i<5;i++)yield return null;
                Assert.AreEqual(GolfGame.State.Aim,game.Current);Assert.True(game.NativeShotReady,"the next golfer on this phone can swing");
                Assert.AreEqual(net.GolfState.golfers[1].x,game.BallAt.X,1e-6);Assert.AreEqual((int)game.ClubInHand,net.GolfState.golfers[1].club);
                bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session="pass",action="end"}));
            } finally {Object.DestroyImmediate(root);Clean();}
        }
        [UnityTest] public IEnumerator NativeMultiplayerLoadsBothSportsAndResumesItsInputReader() {
            Clean();var root=new GameObject("Native multiplayer test");Object.DontDestroyOnLoad(root);var bridge=root.AddComponent<NativeSportsSession>();
            try {
                foreach(var sport in new[]{"tennis","golf"}) {
                    var c=Config(sport);string session="network-"+sport;
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session=session,action="start",sport=sport,touch=true,network=JsonUtility.ToJson(c)}));
                    for(int frame=0;frame<600&&!bridge.Ready;frame++){
                        // Batch-mode has no live Game view; explicitly render the real camera.
                        if(bridge.GameplayCamera){
                            string dir=Environment.GetEnvironmentVariable("GAMEPLAY_PROOF_DIR")??"Library/Captures/multiplayer";
                            GameCapture.Save($"{dir}/network-{sport}-ready.png",640,360);
                        }
                        yield return null;
                    }
                    Assert.True(bridge.Ready,"Real gameplay camera must render before launch finishes.");Assert.True(SportsMultiplayer.Active);Assert.False(SportsMultiplayer.Instance.Running);
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session=session,action="resume"}));
                    Assert.False((bool)typeof(NativeSportsSession).GetField("paused",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(bridge));
                    SportsMultiplayer.Instance.Receive(Packet(c,"run"));Assert.True(SportsMultiplayer.Instance.Running);
                    if(sport=="tennis"){
                        for(int i=0;i<900 && SportsMultiplayer.Instance.TennisState.phase=="intro";i++) yield return null;
                        Assert.AreEqual("serve",SportsMultiplayer.Instance.TennisState.phase);
                        yield return null;
                        var meter=Object.FindFirstObjectByType<TennisTossMeter>();Assert.True(meter&&meter.gameObject.activeInHierarchy,"The server must see the host-clock toss meter.");
                        SportsMultiplayer.Command(new NativeSportsSession.Message {action="toss",value=1});Assert.AreEqual("toss",SportsMultiplayer.Instance.TennisState.phase);
                        Assert.Less(SportsMultiplayer.Instance.TennisState.tossAccuracy,TennisRules.ServePerfectToss,"A claimed perfect value cannot override an early press.");
                        Assert.False(Object.FindFirstObjectByType<TennisGame>().AutoPlay);
                        yield return null;Assert.False(meter.gameObject.activeSelf);
                    }
                    else {SportsMultiplayer.Instance.Submit(new NetworkInput {action="swing",power=.5f});Assert.AreEqual("flight",SportsMultiplayer.Instance.GolfState.phase);}
                    for(int i=0;i<10;i++)yield return null;
                    bridge.Receive(JsonUtility.ToJson(new NativeSportsSession.Message {version=1,session=session,action="end"}));Assert.False(SportsMultiplayer.Active);
                }
            } finally {Object.DestroyImmediate(root);Clean();}
        }
    }
}

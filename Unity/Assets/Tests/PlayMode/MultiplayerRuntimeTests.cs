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

        [UnityTest] public IEnumerator AuthorityRejectsObserverInputAndOldContest() {
            Clean();var c=Config();SportsMultiplayer.Configure(JsonUtility.ToJson(c));var net=SportsMultiplayer.Instance;
            try {
                net.Receive(Packet(c,"run",sender:"c"));Assert.False(net.Running);net.Receive(Packet(c,"run"));Assert.True(net.Running);
                var toss=new NetworkInput {action="toss",point=net.TennisState.point,contact=net.TennisState.contact,eventID=1,time=Time.realtimeSinceStartupAsDouble};
                net.Receive(Packet(c,"input",JsonUtility.ToJson(toss),"c"));Assert.AreEqual("serve",net.TennisState.phase);
                net.Receive(Packet(c,"input",JsonUtility.ToJson(toss),"a","older"));Assert.AreEqual("serve",net.TennisState.phase);
                net.Receive(Packet(c,"input",JsonUtility.ToJson(toss)));Assert.AreEqual("toss",net.TennisState.phase);yield return null;
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

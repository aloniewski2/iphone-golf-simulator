using System;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using GolfArcade.Game;
using GolfArcade.Tennis;
using UnityEngine;

namespace GolfArcade.Multiplayer {
    /// Apple transports remain native. This component runs/replicates the sport on Unity's main thread.
    [DefaultExecutionOrder(-950)]
    public sealed class SportsMultiplayer : MonoBehaviour {
        public static SportsMultiplayer Instance {get;private set;}
        public static bool Active=>Instance && Instance.Configuration!=null;
        public NetworkConfiguration Configuration {get;private set;}
        public bool IsHost=>Configuration?.hostID==Configuration?.localID;
        public int LocalSeat=>Configuration?.Seat(Configuration.localID)??-1;
        public bool Running {get;private set;}
        public NetworkTennisState TennisState {get;private set;}
        public NetworkGolfState GolfState {get;private set;}
        public NetworkGolfShot GolfShot {get;private set;}
        public float LocalTarget {get;private set;}
        NetworkTennisMatch tennis;NetworkGolfRound golf;
        TennisGame tennisView;GolfGame golfView;
        double clockOffset,lastSnapshot,nextSnapshot,lastMove,packetAt;long eventID,lastTick=-1,lastRevision=-1;
        // IL2CPP shrinks a marshalled StringBuilder to the returned text length (one char on an empty poll).
        // A fixed blittable buffer preserves capacity across every poll and keeps UTF-8 packet boundaries.
        readonly byte[] inputBuffer=new byte[65536];
        readonly System.Collections.Generic.HashSet<string> suspended=new();
        static double Clock=>NativeClock();
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int SportsNetworkPoll([Out] byte[] output,int capacity);
        [DllImport("__Internal")] static extern int SportsNetworkEmit(string json);
        [DllImport("__Internal",EntryPoint="SportsClock")] static extern double NativeClock();
#else
        public static Action<string> TestOutput;
        static int SportsNetworkPoll([Out] byte[] output,int capacity)=>0;
        static int SportsNetworkEmit(string json){TestOutput?.Invoke(json);return 1;}
        static double NativeClock()=>Time.realtimeSinceStartupAsDouble;
#endif
        public static void Configure(string json) {
            var c=JsonUtility.FromJson<NetworkConfiguration>(json);if(c==null||!c.Valid)throw new ArgumentException("Invalid multiplayer configuration.");
            if(!Instance){var go=new GameObject("SportsMultiplayer");DontDestroyOnLoad(go);Instance=go.AddComponent<SportsMultiplayer>();}
            Instance.Initialize(c);
        }
        void Initialize(NetworkConfiguration c) {
            Configuration=c;Running=false;LocalTarget=0;lastSwing=lastStart=lastAbort=0;eventID=0;lastTick=lastRevision=-1;nextSnapshot=lastMove=0;lastSnapshot=packetAt=Clock;clockOffset=0;suspended.Clear();GolfShot=null;TennisState=null;GolfState=null;
            tennisView=FindFirstObjectByType<TennisGame>();golfView=FindFirstObjectByType<GolfGame>();
            if(IsHost) {
                if(c.sport=="tennis") {tennis=new(c.sets,c.games);TennisState=tennis.State;golf=null;tennis.Result=Result;}
                else {golf=new(c.participants.Where(p=>p.seat>=0).Select(p=>p.seat).ToArray(),c.seed);GolfState=golf.State;tennis=null;golf.Result=Result;golf.Shot=shot=>{GolfShot=shot;Send("golfShot",JsonUtility.ToJson(shot));};}
            } else {tennis=null;golf=null;TennisState=null;GolfState=null;}
            tennisView?.ConfigureNetwork(c);golfView?.ConfigureNetwork(c);
            Time.timeScale=1;
        }
        void OnDestroy(){if(Instance==this)Instance=null;}
        void Update() {
            if(!Active)return;
            for(int i=0;i<64;i++){int bytes=SportsNetworkPoll(inputBuffer,inputBuffer.Length);if(bytes<=0)break;if(bytes<=60000)Receive(Encoding.UTF8.GetString(inputBuffer,0,bytes));if(!Active)break;}
            if(!Running)return;
            if(IsHost) {
                if(tennis!=null){tennis.State.paused=suspended.Count>0;tennis.Step(Time.unscaledDeltaTime);TennisState=tennis.State;}
                if(golf!=null){golf.State.paused=suspended.Count>0;golf.Step(Time.unscaledDeltaTime);GolfState=golf.State;}
                if(Clock>=nextSnapshot){nextSnapshot=Clock+1.0/30;SendSnapshot(false);}
            }
        }
        public void Receive(string json) {
            if(!Active)return;
            NetworkPacket p;try{p=JsonUtility.FromJson<NetworkPacket>(json);}catch{return;}
            if(p==null||p.version!=NetworkPacket.Version||p.lobbyID!=Configuration.lobbyID||p.matchID!=Configuration.matchID)return;
            if(p.kind=="clock") {if(p.sender!=Configuration.localID)return;}
            else if(p.kind!="input"&&p.sender!=Configuration.hostID)return;
            switch(p.kind) {
                case "run":Running=true;Time.timeScale=1;SendSnapshot(true);break;
                case "clock":if(double.TryParse(p.payload,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var offset))clockOffset=offset;break;
                case "input":
                    if(!IsHost||!Running)return;var seat=Configuration.Seat(p.sender);if(seat<0)return;
                    NetworkInput value;try{value=JsonUtility.FromJson<NetworkInput>(p.payload);}catch{return;}
                    ApplyInput(seat,value);break;
                case "snapshot":
                    if(IsHost||p.sender!=Configuration.hostID)return;
                    if(Configuration.sport=="tennis") {
                        var s=JsonUtility.FromJson<NetworkTennisState>(p.payload);if(s==null||s.tick<lastTick)return;lastTick=s.tick;TennisState=s;if(s.complete)Running=false;
                    } else {
                        var s=JsonUtility.FromJson<NetworkGolfState>(p.payload);if(s==null||s.revision<lastRevision)return;lastRevision=s.revision;
                        if(s.shot?.path!=null&&s.shot.path.Length>1)GolfShot=s.shot;GolfState=s;if(s.complete)Running=false;
                    }
                    lastSnapshot=Clock;packetAt=p.sentAt;break;
                case "golfShot":
                    if(p.sender!=Configuration.hostID)return;var shot=JsonUtility.FromJson<NetworkGolfShot>(p.payload);
                    if(shot!=null&&(GolfShot==null||shot.id>GolfShot.id))GolfShot=shot;break;
                case "suspend":suspended.Add(p.payload);if(TennisState!=null)TennisState.paused=true;if(GolfState!=null)GolfState.paused=true;break;
                case "resumePeer":suspended.Remove(p.payload);break;
                case "drop":suspended.Remove(p.payload);if(IsHost){tennis?.Drop(Configuration.Seat(p.payload));golf?.Drop(Configuration.Seat(p.payload));SendSnapshot(true);}break;
                case "snapshotRequest":if(IsHost)SendSnapshot(true);break;
                case "result":Running=false;break;
                case "stop":Shutdown();break;
            }
        }
        void ApplyInput(int seat,NetworkInput input) {
            if(input==null||!input.Valid)return;
            if(tennis!=null)tennis.Input(seat,input,Clock);
            if(golf!=null)golf.Input(seat,input,Clock);
        }
        public void Submit(NetworkInput input,bool reliable=true) {
            if(!Running||LocalSeat<0||input==null||!input.Valid)return;
            if(Configuration.sport=="tennis") {if(TennisState==null)return;input.point=TennisState.point;input.contact=TennisState.contact;}
            if(Configuration.sport=="tennis"&&LocalSeat==1) {
                input.target=-input.target;input.aim=-input.aim;
                if(input.action=="aim"||input.action=="nudge")input.value=-input.value;
            }
            if(input.action=="move")LocalTarget=input.target;
            input.time=Clock+(IsHost?0:clockOffset);if(input.eventID==0)input.eventID=++eventID;
            if(IsHost){ApplyInput(LocalSeat,input);return;}
            Send("input",JsonUtility.ToJson(input),reliable);
        }
        public static bool Command(NativeSportsSession.Message m) {
            if(!Active)return false;
            switch(m.action) {
                case "start":case "end":return false;
                case "pause":Instance.Send("availability","false");return true;
                case "resume":Instance.Send("availability","true");Time.timeScale=1;return true;
                case "touch":case "motion":case "recalibrate":case "latency":return false;
                case "toss":
                    Instance.Submit(new NetworkInput {action="toss",age=Instance.tennisView?.TossSeenAgo??0});return true;
                case "serveAim":case "nudge":case "aim":case "rallyAim":case "club":case "dive":
                    Instance.Submit(new NetworkInput {action=m.action=="rallyAim"?"aim":m.action,value=m.value,value2=m.value2});return true;
                case "sound":case "haptics":case "display":case "flash":return false;
                default:return true; // Solo tutorials, campaign cheats and resets cannot change a network match.
            }
        }
        public static void Sample(in NativeSportsSession.Sample s,bool touch) {
            if(!Active||Instance.LocalSeat<0||!s.valid)return;
            var self=Instance;
            float aim=touch&&self.TennisState!=null?self.TennisState.players[self.LocalSeat].aim*(self.LocalSeat==1?-1:1):s.aim;
            if(self.Configuration.sport=="tennis"&&Clock-self.lastMove>=1.0/30) {
                self.lastMove=Clock;self.Submit(new NetworkInput {action="move",target=s.target,aim=aim},false);
            }
            if(s.swingStart>self.lastStart){self.lastStart=s.swingStart;self.Submit(new NetworkInput {action="beginSwing",power=s.power});self.tennisView?.PredictNetworkSwing(s.power);}
            if(s.swing>self.lastSwing) {self.lastSwing=s.swing;self.Submit(new NetworkInput {action="swing",power=s.power,aim=aim,handSide=s.handSide,lift=s.lift,facing=s.strokeFacing,age=Math.Min(.25,Math.Max(0,Clock-s.time))});self.tennisView?.PredictNetworkSwing(s.power);}
            if(s.swingAbort>self.lastAbort){self.lastAbort=s.swingAbort;self.Submit(new NetworkInput {action="abortSwing"});}
        }
        int lastSwing,lastStart,lastAbort;
        public double RenderAdvance=>IsHost?0:Math.Min(.1,Math.Max(0,Clock+clockOffset-packetAt));
        public bool Stale=>Running&&!IsHost&&Clock-lastSnapshot>2;
        void SendSnapshot(bool full) {
            if(!IsHost)return;
            if(tennis!=null)Send("snapshot",JsonUtility.ToJson(tennis.State),full);
            if(golf!=null) {
                var path=golf.State.shot?.path;
                if(!full&&golf.State.shot!=null)golf.State.shot.path=null;
                var json=JsonUtility.ToJson(golf.State);
                if(golf.State.shot!=null)golf.State.shot.path=path;
                Send("snapshot",json,full);
            }
        }
        [Serializable] public sealed class MatchResult {public string sport,reason,score;public int winner=-1;public NetworkGolfer[] golfers;}
        void Result(string score){
            var r=new MatchResult {sport=Configuration.sport,reason=score=="interrupted"?"interrupted":"complete",score=score};
            if(tennis!=null&&tennis.State.score.Complete)r.winner=tennis.State.score.PlayerWonMatch?0:1;
            if(golf!=null)r.golfers=golf.State.golfers;
            SendSnapshot(true);Send("result",JsonUtility.ToJson(r));Running=false;
        }
        void Send(string kind,string payload,bool reliable=true) {
            if(Configuration==null)return;
            var p=new NetworkPacket {lobbyID=Configuration.lobbyID,matchID=Configuration.matchID,sender=Configuration.localID,kind=kind,payload=payload,reliable=reliable,sentAt=Clock};
            var json=JsonUtility.ToJson(p);if(Encoding.UTF8.GetByteCount(json)<=60000)if(SportsNetworkEmit(json)==0){Running=false;Debug.LogError("Multiplayer bridge backpressure: match paused.");}
        }
        public static void Shutdown(){if(!Instance)return;Instance.Running=false;Instance.Configuration=null;Instance.tennis=null;Instance.golf=null;Time.timeScale=1;}
    }
}

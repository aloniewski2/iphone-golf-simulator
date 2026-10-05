using System;
using System.Collections.Generic;
using GolfArcade.Tennis;

namespace GolfArcade.Multiplayer {
    [Serializable] public sealed class NetworkTennisPlayer {
        public float x,z,target,aim,depth=.75f,power=.65f,stamina=1;
        public int swings,serves,dives; public double swingAt=-100,diveUntil; public bool confirmedSwing;
    }
    [Serializable] public sealed class NetworkTennisState {
        public long tick,point,contact; public double time,phaseAt;
        public string phase="serve",reason="";
        public int server,receiver=1,bounces,winner=-1; public bool secondServe,serveFlight,paused,complete;
        public NetworkVector ball,velocity;
        public float tossAccuracy=1,tossRollX=.5f,tossRollZ=.5f;
        public NetworkTennisPlayer[] players={new(),new()};
        public TennisMatch score;
    }
    /// Symmetric authority: both competitors use the same movement, serve, hit and score rules.
    /// Presentation is separate; a client never advances the official score or ball.
    public sealed class NetworkTennisMatch {
        public readonly NetworkTennisState State;
        readonly Dictionary<string,long> events=new();
        readonly Queue<History> history=new();
        readonly Random serveRandom=new(2701);
        struct History { public double time; public long point,contact; public NetworkVector ball,velocity; public float ax,az,bx,bz; public int receiver,bounces; }
        public const double MaximumRewind=.15;
        const float Radius=.034f, HalfWidth=4.115f, HalfLength=11.885f, NetHeight=.97f, Gravity=9.81f;
        double accumulator; bool hasResult;
        public Action<string> Result;
        public NetworkTennisMatch(int sets=1,int games=3) {
            State=new NetworkTennisState {score=TennisMatch.New(true,sets,games)};
            BeginPoint();
        }
        void BeginPoint() {
            State.point++; State.phase="serve"; State.phaseAt=State.time; State.server=State.score.PlayerServes?0:1;
            State.receiver=1-State.server; State.bounces=0; State.serveFlight=false; State.secondServe=false;
            float side=State.score.DeuceCourt?1:-1;
            State.players[0].x=side*1.6f;State.players[0].z=-12.2f;
            State.players[1].x=-side*1.6f;State.players[1].z=12.2f;
            foreach(var p in State.players) {p.swingAt=-100;p.confirmedSwing=false;p.target=p.x/3.6f;}
            SetHeldBall(); history.Clear();
        }
        void SetHeldBall() {var p=State.players[State.server];State.ball=new(p.x,1.1f,p.z);State.velocity=default;}
        public bool Input(int seat,NetworkInput input,double hostTime) {
            if(seat<0||seat>1||input==null||!input.Valid||State.complete||State.paused) return false;
            if(input.time>hostTime+.05||input.time<hostTime-.5) return false;
            if((input.action=="swing"||input.action=="beginSwing"||input.action=="abortSwing"||input.action=="toss")&&(input.point!=State.point||input.contact!=State.contact))return false;
            string key=seat+":"+input.action;
            if(input.action!="move" && input.action!="aim" && input.action!="serveAim" && input.action!="nudge") {
                if(input.eventID<=0 || events.TryGetValue(key,out long last)&&input.eventID<=last) return false;
                events[key]=input.eventID;
            }
            var p=State.players[seat];
            switch(input.action) {
                case "move": p.target=NetworkMath.Clamp(input.target,-1,1);p.aim=NetworkMath.Clamp(input.aim,-1,1);return true;
                case "aim":p.aim=NetworkMath.Clamp(input.value,-1,1);p.depth=NetworkMath.Clamp(input.value2,0,1);return true;
                case "serveAim":p.aim=NetworkMath.Clamp(input.value,-1,1);p.depth=NetworkMath.Clamp(input.value2,0,1);return true;
                case "nudge":p.target=NetworkMath.Clamp(p.target+input.value*.02f,-1,1);return true;
                case "toss":
                    if(seat!=State.server||State.phase!="serve")return false;
                    // Judge the host's sweep at the compensated press time, never a client score.
                    double tossAge=Math.Max(0,Math.Min(MaximumRewind+.25,hostTime-input.time+input.age));
                    State.tossAccuracy=TennisTossMeter.AccuracyAt((float)Math.Max(0,State.time-State.phaseAt-tossAge));
                    State.tossRollX=(float)serveRandom.NextDouble();State.tossRollZ=(float)serveRandom.NextDouble();
                    State.phase="toss";State.phaseAt=State.time;return true;
                case "beginSwing": if(State.phase=="rally") {p.swingAt=State.time;p.confirmedSwing=false;p.swings++;} return true;
                case "abortSwing":p.swingAt=-100;p.confirmedSwing=false;return true;
                case "swing":
                    p.power=NetworkMath.Clamp(input.power,.1f,1);
                    double age=Math.Max(0,Math.Min(MaximumRewind,hostTime-input.time+input.age));
                    if(State.phase=="toss"&&seat==State.server) {
                        double timing=State.time-State.phaseAt-age;
                        if(timing<.25 || timing>1.55) {Fault();return true;}
                        Serve(seat);return true;
                    }
                    if(State.phase!="rally"||State.receiver!=seat)return false;
                    p.aim=NetworkMath.Clamp(input.aim,-1,1);p.swingAt=State.time;p.confirmedSwing=true;p.swings++;
                    // A late packet may meet a historical ball, but never one from a previous
                    // point or before a newer confirmed contact.
                    foreach(var h in history) {
                        if(h.time<State.time-age-.025||h.point!=State.point||h.contact!=State.contact||h.receiver!=seat||(State.serveFlight&&h.bounces==0))continue;
                        float px=seat==0?h.ax:h.bx,pz=seat==0?h.az:h.bz;
                        if(CanReach(h.ball,px,pz,p.diveUntil>State.time)) {
                            State.ball=h.ball;State.velocity=h.velocity;State.bounces=h.bounces;
                            Return(seat,p); AdvanceBall((float)(State.time-h.time));return true;
                        }
                    }
                    return true;
                case "dive":
                    if(State.phase!="rally"||p.stamina<.25f||State.time<p.diveUntil+2)return false;
                    p.diveUntil=State.time+.55;p.dives++;p.stamina-=.25f;return true;
                default:return false;
            }
        }
        void Serve(int seat) {
            var p=State.players[seat];float sign=seat==0?1:-1;
            State.ball=new(p.x,2.85f,p.z+sign*.4f);
            float targetX=-Math.Sign(p.x)*(1.2f+p.aim*.8f), targetZ=sign*(2.4f+p.depth*3.4f);
            var scatter=TennisRules.ServeTossScatter(State.tossAccuracy,State.tossRollX,State.tossRollZ);
            Launch(new(targetX+scatter.x,Radius,targetZ+scatter.y),25+p.power*14);
            State.phase="rally";State.receiver=1-seat;State.bounces=0;State.serveFlight=true;State.contact++;p.serves++;
        }
        static bool CanReach(NetworkVector b,float x,float z,bool dive)=>b.y>.1f&&b.y<3.6f&&Math.Abs(b.x-x)<(dive?2.15f:1.55f)&&Math.Abs(b.z-z)<2f;
        void Return(int seat,NetworkTennisPlayer p) {
            float sign=seat==0?1:-1;
            State.ball.y=Math.Max(.45f,State.ball.y);
            float depth=5f+p.depth*6.4f;
            Launch(new(p.aim*3.6f,Radius,sign*depth),18+p.power*15);
            State.receiver=1-seat;State.bounces=0;State.serveFlight=false;State.contact++;p.swingAt=-100;p.confirmedSwing=false;
        }
        void Launch(NetworkVector target,float speed) {
            float dx=target.x-State.ball.x,dz=target.z-State.ball.z;
            float flight=Math.Max(.25f,(float)Math.Sqrt(dx*dx+dz*dz)/speed);
            // Preserve a playable arc from low bounced contacts. Horizontal power cannot
            // flatten an otherwise legal aim into an unavoidable net fault.
            if(State.ball.z*target.z<0) {
                float fraction=Math.Abs(State.ball.z/dz),linear=State.ball.y+(target.y-State.ball.y)*fraction;
                float clearance=NetHeight+.16f-linear;
                if(clearance>0&&fraction>0&&fraction<1)flight=Math.Max(flight,(float)Math.Sqrt(2*clearance/(Gravity*fraction*(1-fraction))));
            }
            State.velocity=new(dx/flight,(target.y-State.ball.y)/flight+.5f*Gravity*flight,dz/flight);
        }
        public void Step(double elapsed) {
            if(State.paused||State.complete)return;
            accumulator+=Math.Min(.1,Math.Max(0,elapsed));
            while(accumulator>=1.0/120) {accumulator-=1.0/120;Tick(1f/120);}
        }
        void Tick(float dt) {
            State.time+=dt;State.tick++;
            if(State.phase=="point") {if(State.time-State.phaseAt>2)BeginPoint();return;}
            for(int i=0;i<2;i++) {
                var p=State.players[i];float desired=p.target*3.6f;
                if(State.phase=="rally"&&State.receiver==i) {
                    // Same bounded assist for both humans, with a deliberate swing still required.
                    float dz=p.z-State.ball.z;
                    float arrival=Math.Abs(State.velocity.z)>.5f?dz/State.velocity.z:0;
                    float intercept=State.ball.x+State.velocity.x*Math.Max(0,arrival);
                    desired=NetworkMath.Clamp(desired*.35f+intercept*.65f,-4.8f,4.8f);
                }
                p.x=NetworkMath.Toward(p.x,desired,dt*(p.diveUntil>State.time?9:5.6f));p.stamina=Math.Min(1,p.stamina+dt*.04f);
            }
            if(State.phase=="serve"){SetHeldBall();return;}
            if(State.phase=="toss") {
                var p=State.players[State.server];float t=(float)(State.time-State.phaseAt);
                float error=TennisRules.TossError(State.tossAccuracy);
                State.ball=new(p.x+(State.tossRollX*2-1)*error*t,1.1f+6*t-3*t*t,p.z+(State.tossRollZ*2-1)*error*.6f*t);
                if(t>1.65f)Fault();return;
            }
            history.Enqueue(new History {time=State.time,point=State.point,contact=State.contact,ball=State.ball,velocity=State.velocity,ax=State.players[0].x,az=State.players[0].z,bx=State.players[1].x,bz=State.players[1].z,receiver=State.receiver,bounces=State.bounces});
            while(history.Count>0&&State.time-history.Peek().time>MaximumRewind+.05)history.Dequeue();
            var receiver=State.players[State.receiver];
            if(receiver.confirmedSwing && State.time-receiver.swingAt>=.09&&State.time-receiver.swingAt<=.32 && (!State.serveFlight||State.bounces>0)&&CanReach(State.ball,receiver.x,receiver.z,receiver.diveUntil>State.time))Return(State.receiver,receiver);
            AdvanceBall(dt);
        }
        void AdvanceBall(float elapsed) {
            // Catch up a rewound contact in fixed substeps, retaining net/bounce rulings.
            while(elapsed>0&&State.phase=="rally") {
                float dt=Math.Min(elapsed,1f/120);elapsed-=dt;
                var old=State.ball;var b=old;var v=State.velocity;
                b.x+=v.x*dt;b.z+=v.z*dt;b.y+=v.y*dt-.5f*Gravity*dt*dt;v.y-=Gravity*dt;
                State.ball=b;State.velocity=v;
                if(old.z*b.z<0) {
                    float f=Math.Abs(old.z)/(Math.Abs(old.z)+Math.Abs(b.z));float y=old.y+(b.y-old.y)*f;
                    if(y<NetHeight) {if(State.serveFlight)Fault();else Point(State.receiver);return;}
                }
                if(b.y<=Radius&&v.y<0) {
                    State.ball.y=Radius;State.velocity.y=-v.y*.75f;State.bounces++;
                    bool inCourt=Math.Abs(b.x)<=HalfWidth&&Math.Abs(b.z)<=HalfLength;
                    bool correctSide=State.receiver==0?b.z<0:b.z>0;
                    if(State.bounces==1&&State.serveFlight) {
                        bool diagonal=Math.Sign(b.x)!=Math.Sign(State.players[State.server].x);
                        if(!correctSide||!diagonal||Math.Abs(b.z)>6.4f||!inCourt) {Fault();return;}
                        // Keep serveFlight until return to forbid volleying a serve.
                    } else if(State.bounces==1&&(!inCourt||!correctSide)) {Point(State.receiver);return;}
                    else if(State.bounces>=2) {Point(1-State.receiver);return;}
                }
                if(Math.Abs(b.z)>18||Math.Abs(b.x)>15||b.y<-2){Point(State.receiver);return;}
            }
        }
        void Fault() {
            if(State.secondServe){Point(1-State.server);return;}
            State.secondServe=true;State.phase="serve";State.phaseAt=State.time;State.serveFlight=false;State.bounces=0;SetHeldBall();history.Clear();
        }
        void Point(int winner) {
            if(State.phase!="rally"&&State.phase!="toss"&&State.phase!="serve")return;
            State.score.AwardPoint(winner==0);State.winner=winner;State.phaseAt=State.time;State.velocity=default;State.complete=State.score.Complete;
            State.phase=State.complete?"complete":"point";history.Clear();
            if(State.complete&&!hasResult){hasResult=true;Result?.Invoke(State.score.FinalScore);}
        }
        public void Drop(int seat) {if(seat<0||seat>1||State.complete)return;State.complete=true;State.phase="interrupted";State.reason="A competitor disconnected.";Result?.Invoke("interrupted");}
    }
}

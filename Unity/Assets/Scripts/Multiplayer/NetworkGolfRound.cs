using System;
using System.Linq;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
namespace GolfArcade.Multiplayer {
    [Serializable] public sealed class NetworkGolfer {
        public int seat,strokes,club,total; public int emoteSlot=-1; public long emoteID; public double emoteAt=-100; public double x,d,heading; public bool holed,dnf; public int[] card;
    }
    [Serializable] public sealed class NetworkGolfState {
        public long revision,shotID;public int hole,turn;public double time,turnAt,windSpeed,windDirection,presentationUntil,resultUntil;
        public string phase="aim";public bool paused,complete;
        public NetworkGolfer[] golfers;public NetworkGolfShot shot;
    }
    [Serializable] public sealed class NetworkGolfShot {
        public long id;public int seat,hole,club,strokes,penalty;public double start,duration,carry,roll,apex,restX,restD,nextX,nextD;
        public bool holed;public string lie; public NetworkVector[] path;
    }
    /// The existing CourseShot engine runs once, on the authority. Every peer receives its path and ruling.
    public sealed class NetworkGolfRound {
        public const double ResultSeconds=3;
        public readonly NetworkGolfState State;
        readonly bool intros; readonly Course.Course course;readonly Random random;readonly long[] lastEvent=new long[4];
        public Action<NetworkGolfShot> Shot;public Action<string> Result;
        public NetworkGolfRound(int[] seats,int seed,bool intro=false,string courseKey=null) {
            if(seats==null||seats.Length<2||seats.Length>4||seats.Distinct().Count()!=seats.Length||seats.Any(s=>s<0||s>3))throw new ArgumentException("Invalid golf seats.");
            intros=intro; course=Course.Course.ByKey(courseKey??"postcards")??Course.Course.Postcards();random=new Random(seed);
            State=new NetworkGolfState {golfers=seats.OrderBy(s=>s).Select(s=>new NetworkGolfer {seat=s,card=new int[course.Holes.Length]}).ToArray()};
            BeginHole(0);
        }
        void BeginHole(int index) {
            State.hole=index;State.shot=null;State.phase="aim";State.revision++;
            var hole=course.Holes[index];var wind=Wind.Random(random);State.windSpeed=wind.SpeedMPH;State.windDirection=wind.DirectionDegrees;
            foreach(var p in State.golfers) {p.x=hole.Tee.X;p.d=hole.Tee.D;p.strokes=0;p.holed=false;p.club=0;p.heading=hole.Tee.HeadingTo(hole.Pin);}
            var eligible=State.golfers.Where(p=>!p.dnf).ToArray();
            if(eligible.Length<2){Complete("interrupted");return;}
            State.turn=eligible[index%eligible.Length].seat;State.turnAt=State.time;
            if(intros) { State.phase="intro";State.presentationUntil=State.time+2.9; }
        }
        public bool Input(int seat,NetworkInput input,double hostTime) {
            var p=State.golfers.FirstOrDefault(p=>p.seat==seat);
            if(p==null||p.dnf||State.complete||State.paused||input==null||!input.Valid||input.time>hostTime+.05||input.time<hostTime-.5)return false;
            if(input.action=="emote") {
                if(State.phase=="intro" || (State.turn==seat && State.phase!="result") || input.value<0 || input.value>2 || input.value!=Math.Floor(input.value) || State.time-p.emoteAt<1.5 || input.eventID<=lastEvent[seat])return false;
                lastEvent[seat]=input.eventID;p.emoteSlot=(int)input.value;p.emoteID++;p.emoteAt=State.time;State.revision++;return true;
            }
            if(p.holed || State.turn!=seat || State.phase!="aim")return false;
            switch(input.action) {
                case "aim":p.heading+=NetworkMath.Clamp(input.value,-1,1)*1.5;State.revision++;return true;
                case "golfAimHeading":p.heading=(input.value%360+360)%360;State.revision++;return true;
                case "club":
                    if(input.value==0)return false;
                    int index=Array.IndexOf(GolfClubs.All,(GolfClub)p.club);
                    p.club=(int)GolfClubs.All[(index+(input.value>0?1:-1)+GolfClubs.All.Length)%GolfClubs.All.Length];
                    State.revision++;return true;
                case "swing":
                    if(input.eventID<=lastEvent[seat])return false;lastEvent[seat]=input.eventID;
                    Play(p,new SwingImpact {Power=NetworkMath.Clamp(input.power,0,1),StartLineDegrees=NetworkMath.Clamp(input.aim,-1,1)*8,CurveDegrees=NetworkMath.Clamp(input.facing,-1,1)*10});return true;
                default:return false;
            }
        }
        void Play(NetworkGolfer p,SwingImpact impact) {
            var hole=course.Holes[State.hole];var origin=new CoursePoint(p.x,p.d);var club=(GolfClub)p.club;
            var shot=new CourseShot(club,impact,p.heading,origin,hole,hole.LieAt(origin).PowerFactor(),new Wind(State.windSpeed,State.windDirection));
            p.strokes+=1+shot.PenaltyStrokes;State.shotID++;
            int samples=(int)Math.Ceiling(shot.Duration/.05)+1;
            // Bound data below GameKit/bridge packet size. Long paths are sampled more sparsely.
            samples=Math.Max(2,Math.Min(360,samples));var path=new NetworkVector[samples];
            for(int i=0;i<samples;i++){var v=shot.PositionAt(shot.Duration*i/(samples-1));path[i]=new((float)v.x,(float)v.h,(float)v.d);}
            State.shot=new NetworkGolfShot {id=State.shotID,seat=p.seat,hole=State.hole,club=p.club,strokes=p.strokes,penalty=shot.PenaltyStrokes,start=State.time,duration=shot.Duration,
                carry=shot.Carry,roll=shot.Roll,apex=shot.Apex,restX=shot.Rest.X,restD=shot.Rest.D,nextX=shot.NextPosition.X,nextD=shot.NextPosition.D,holed=shot.IsHoled,lie=shot.Lie.ToString(),path=path};
            State.phase="flight";State.revision++;Shot?.Invoke(State.shot);
        }
        public void Step(double elapsed) {
            if(State.paused||State.complete)return;State.time+=Math.Max(0,Math.Min(.1,elapsed));
            if(State.phase=="intro") { if(State.time>=State.presentationUntil) {State.phase="aim";State.turnAt=State.time;State.revision++;} return; }
            if(State.phase=="flight" && State.time>=State.shot.start+State.shot.duration) {
                State.phase="result";State.resultUntil=State.time+ResultSeconds;State.revision++;
            } else if(State.phase=="result" && State.time>=State.resultUntil) {
                var p=State.golfers.First(g=>g.seat==State.shot.seat);p.x=State.shot.nextX;p.d=State.shot.nextD;
                if(State.shot.holed||p.strokes>=course.Holes[State.hole].Par+5)FinishGolfer(p);
                NextTurn();
            } else if(State.phase=="aim"&&State.time-State.turnAt>=60) {
                var p=State.golfers.First(g=>g.seat==State.turn);p.strokes++;if(p.strokes>=course.Holes[State.hole].Par+5)FinishGolfer(p);NextTurn();
            }
        }
        void FinishGolfer(NetworkGolfer p) {p.holed=true;p.card[State.hole]=p.strokes;p.total=p.card.Sum();}
        void NextTurn() {
            var eligible=State.golfers.Where(g=>!g.dnf&&!g.holed).ToArray();State.shot=null;State.revision++;
            if(eligible.Length==0) {
                if(State.hole+1==course.Holes.Length){Complete("complete");return;}BeginHole(State.hole+1);return;
            }
            // Everyone tees off before anyone's second shot. Rotate tee order by hole.
            var unplayed=eligible.Where(g=>g.strokes==0).OrderBy(g=>(g.seat-State.hole%4+4)%4).ToArray();
            State.turn=(unplayed.Length>0?unplayed[0]:eligible.OrderByDescending(g=>new CoursePoint(g.x,g.d).DistanceTo(course.Holes[State.hole].Pin)).ThenBy(g=>g.seat).First()).seat;
            var next=eligible.First(p=>p.seat==State.turn);var hole=course.Holes[State.hole];var at=new CoursePoint(next.x,next.d);var lie=hole.LieAt(at);var distance=at.DistanceTo(hole.Pin);
            next.heading=at.HeadingTo(hole.Pin);next.club=(int)(lie==CourseLie.Green?GolfClub.Putter:lie==CourseLie.Bunker||distance<=100?GolfClub.Wedge:distance<=185?GolfClub.Iron:GolfClub.Driver);
            State.phase="aim";State.turnAt=State.time;
        }
        public void Drop(int seat) {
            var p=State.golfers.FirstOrDefault(g=>g.seat==seat);if(p==null||p.dnf)return;p.dnf=true;State.revision++;
            if(State.golfers.Count(g=>!g.dnf)<2){Complete("interrupted");return;}
            if(State.phase=="aim"&&State.turn==seat)NextTurn();
        }
        void Complete(string reason){if(State.complete)return;State.complete=true;State.phase=reason;State.revision++;Result?.Invoke(reason);}
    }
}

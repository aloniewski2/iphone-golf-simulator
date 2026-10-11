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
        /// The player up, as their phone shows it, for the TV: how far the aiming view looks up the hole to the pin (1) or down at the
        /// ball (-1), and the backswing's load (0–1).
        public float look,load;
        public NetworkGolfer[] golfers;public NetworkGolfShot shot;
    }
    [Serializable] public sealed class NetworkGolfShot {
        public long id;public int seat,hole,club,strokes,penalty;public double start,duration,carry,roll,apex,restX,restD,nextX,nextD;
        /// Where it was struck from, along which line, and the swing itself: every phone flies this same shot from them.
        public double originX,originD,heading;public NetworkImpact impact;
        /// How long the phones take to show the flight: the club coming down to the ball first, a ball in the water a moment to sink.
        public double shown;
        public bool holed;public string lie; public NetworkVector[] path;
    }
    /// The swing as the player's phone read it, the strike's own speed and spin already in (Strikes.Pure), as single player plays it.
    [Serializable] public sealed class NetworkImpact {
        public double power,startLine,curve,face,tempo,peakSpeed,backswing,downswing,commit,thin,speedBonus,spinScatter;
        public static NetworkImpact From(SwingImpact i)=>new() {power=i.Power,startLine=i.StartLineDegrees,curve=i.CurveDegrees,face=i.FaceDegrees,tempo=i.TempoSeconds,
            peakSpeed=i.PeakSpeed,backswing=i.Backswing,downswing=i.DownswingSeconds,commit=i.Commit,thin=i.Thin,speedBonus=i.SpeedBonus,spinScatter=i.SpinScatter};
        public SwingImpact ToImpact()=>new() {Power=power,StartLineDegrees=startLine,CurveDegrees=curve,FaceDegrees=face,TempoSeconds=tempo,PeakSpeed=peakSpeed,
            Backswing=backswing,DownswingSeconds=downswing,Commit=commit,Thin=thin,SpeedBonus=speedBonus,SpinScatter=spinScatter};
        public bool Valid=>new[]{power,startLine,curve,face,tempo,peakSpeed,backswing,downswing,commit,thin,speedBonus,spinScatter}.All(NetworkInput.Finite);
        /// Within what a phone's swing can read: the detector's caps on the line and the curve, the strike's on speed and spin.
        public NetworkImpact Clamped()=>new() {power=Math.Max(0,Math.Min(1,power)),startLine=Math.Max(-8,Math.Min(8,startLine)),curve=Math.Max(-10,Math.Min(10,curve)),
            face=face,tempo=tempo,peakSpeed=peakSpeed,backswing=Math.Max(0,Math.Min(1,backswing)),downswing=downswing,commit=commit,
            thin=Math.Max(0,Math.Min(1,thin)),speedBonus=Math.Max(-.2,Math.Min(.2,speedBonus)),spinScatter=Math.Max(-.3,Math.Min(.3,spinScatter))};
    }
    /// The existing CourseShot engine runs once, on the authority. Every peer receives its path and ruling.
    public sealed class NetworkGolfRound {
        public const double ResultSeconds=3;
        /// The club coming down to the ball before it leaves, as the phones draw the strike.
        public const double StrikeLead=.5;
        /// A ball in the water sinks and splashes before the result.
        public const double SplashSeconds=1.2;
        /// A holed shot's result runs on for the cheer.
        public const double HoledExtraSeconds=2;
        /// Every full swing is replayed from the tee box before its result (GolfGame), and the replay is given its time: the
        /// beat to see where the ball finished, the slow-motion strike, the flight and the landing (a holed ball to the cup).
        public static bool ReplaysShot(GolfClub club)=>club!=GolfClub.Putter;
        public static double ReplaySeconds(CourseShot shot)=>1.2+4.1+(shot.IsHoled?shot.Duration:Math.Min(shot.Duration,shot.LandingTime+2.0));
        public readonly NetworkGolfState State;
        readonly bool intros; readonly Course.Course course;readonly Random random;readonly long[] lastEvent=new long[4];
        public Action<NetworkGolfShot> Shot;public Action<string> Result;
        /// `playing`: the course as the game has it loaded, its holes' ground, greens, trees and windmill read off their models
        /// (Course.ByKey makes a bare copy without them: flat, slopeless greens, nothing to hit).
        public NetworkGolfRound(int[] seats,int seed,bool intro=false,string courseKey=null,Course.Course playing=null) {
            if(seats==null||seats.Length<2||seats.Length>4||seats.Distinct().Count()!=seats.Length||seats.Any(s=>s<0||s>3))throw new ArgumentException("Invalid golf seats.");
            intros=intro; random=new Random(seed);
            var listed=Course.Course.ByKey(courseKey??"cliffside")??Course.Course.Cliffside();
            course=playing!=null&&playing.Key==listed.Key&&playing.Holes.Length>0?playing:listed;
            State=new NetworkGolfState {golfers=seats.OrderBy(s=>s).Select(s=>new NetworkGolfer {seat=s,card=new int[course.Holes.Length]}).ToArray()};
            BeginHole(0);
        }
        void BeginHole(int index) {
            State.hole=index;State.shot=null;State.phase="aim";State.revision++;
            var hole=course.Holes[index];var wind=Wind.Random(random);State.windSpeed=wind.SpeedMPH;State.windDirection=wind.DirectionDegrees;
            foreach(var p in State.golfers) {p.x=hole.Tee.X;p.d=hole.Tee.D;p.strokes=0;p.holed=false;LineUp(p,hole);}
            State.look=0;State.load=0;
            var eligible=State.golfers.Where(p=>!p.dnf).ToArray();
            if(eligible.Length<2){Complete("interrupted");return;}
            State.turn=eligible[index%eligible.Length].seat;State.turnAt=State.time;
            if(intros) { State.phase="intro";State.presentationUntil=State.time+2.9; }
        }
        public bool Input(int seat,NetworkInput input,double hostTime) {
            var p=State.golfers.FirstOrDefault(p=>p.seat==seat);
            if(p==null||p.dnf||State.complete||State.paused||input==null||!input.Valid||input.time>hostTime+NetworkTuning.FutureTolerance||input.time<hostTime-NetworkTuning.PastTolerance)return false;
            if(input.action=="emote") {
                if(State.phase=="intro" || (State.turn==seat && State.phase!="result") || input.value<0 || input.value>2 || input.value!=Math.Floor(input.value) || State.time-p.emoteAt<1.5 || input.eventID<=lastEvent[seat])return false;
                lastEvent[seat]=input.eventID;p.emoteSlot=(int)input.value;p.emoteID++;p.emoteAt=State.time;State.revision++;return true;
            }
            if(p.holed || State.turn!=seat || State.phase!="aim")return false;
            switch(input.action) {
                case "aim":p.heading+=NetworkMath.Clamp(input.value,-1,1)*1.5;State.revision++;return true;
                case "golfAimHeading":p.heading=(input.value%360+360)%360;State.revision++;return true;
                case "golfClub":
                    if(Array.IndexOf(GolfClubs.All,(GolfClub)(int)input.value)<0||input.value!=Math.Floor(input.value))return false;
                    p.club=(int)input.value;State.revision++;return true;
                // (what the TV shows of the player's phone: the view and the backswing; not the shot itself)
                case "golfLook":State.look=NetworkMath.Clamp(input.value,-1,1);return true;
                case "golfLoad":State.load=NetworkMath.Clamp(input.value,0,1);return true;
                case "club":
                    if(input.value==0)return false;
                    int index=Array.IndexOf(GolfClubs.All,(GolfClub)p.club);
                    p.club=(int)GolfClubs.All[(index+(input.value>0?1:-1)+GolfClubs.All.Length)%GolfClubs.All.Length];
                    State.revision++;return true;
                case "swing":
                    if(input.eventID<=lastEvent[seat])return false;
                    // The phone says which club and line it swung on, so a club or aim change still on its way can't change the shot.
                    if(input.shotClub>=0) {
                        if(Array.IndexOf(GolfClubs.All,(GolfClub)input.shotClub)<0||!NetworkInput.Finite(input.heading))return false;
                        p.club=input.shotClub;p.heading=(input.heading%360+360)%360;
                    }
                    lastEvent[seat]=input.eventID;
                    // (JsonUtility writes an empty impact for an input that has none: only a phone's swing names its club)
                    var impact=input.shotClub>=0&&input.impact!=null?input.impact.Clamped():new NetworkImpact {power=NetworkMath.Clamp(input.power,0,1),
                        startLine=NetworkMath.Clamp(input.aim,-1,1)*8,curve=NetworkMath.Clamp(input.facing,-1,1)*10};
                    Play(p,impact);return true;
                default:return false;
            }
        }
        void Play(NetworkGolfer p,NetworkImpact impact) {
            var hole=course.Holes[State.hole];var origin=new CoursePoint(p.x,p.d);var club=(GolfClub)p.club;
            // As single player strikes it: CourseShot takes the lie's toll itself (passing it in too took it twice).
            var shot=new CourseShot(club,impact.ToImpact(),p.heading,origin,hole,1,new Wind(State.windSpeed,State.windDirection));
            p.strokes+=1+shot.PenaltyStrokes;State.shotID++;
            int samples=(int)Math.Ceiling(shot.Duration/.05)+1;
            // Bound data below GameKit/bridge packet size. Long paths are sampled more sparsely.
            samples=Math.Max(2,Math.Min(360,samples));var path=new NetworkVector[samples];
            for(int i=0;i<samples;i++){var v=shot.PositionAt(shot.Duration*i/(samples-1));path[i]=new((float)v.x,(float)v.h,(float)v.d);}
            State.shot=new NetworkGolfShot {id=State.shotID,seat=p.seat,hole=State.hole,club=p.club,strokes=p.strokes,penalty=shot.PenaltyStrokes,start=State.time,duration=shot.Duration,
                carry=shot.Carry,roll=shot.Roll,apex=shot.Apex,restX=shot.Rest.X,restD=shot.Rest.D,nextX=shot.NextPosition.X,nextD=shot.NextPosition.D,holed=shot.IsHoled,lie=shot.Lie.ToString(),path=path,
                originX=origin.X,originD=origin.D,heading=p.heading,impact=impact,
                shown=StrikeLead+shot.Duration+(shot.Lie==CourseLie.Water?SplashSeconds:0)+(ReplaysShot(club)?ReplaySeconds(shot):0)};
            State.phase="flight";State.load=0;State.look=0;State.revision++;Shot?.Invoke(State.shot);
        }
        public void Step(double elapsed) {
            if(State.paused||State.complete)return;State.time+=Math.Max(0,Math.Min(.1,elapsed));
            if(State.phase=="intro") { if(State.time>=State.presentationUntil) {State.phase="aim";State.turnAt=State.time;State.revision++;} return; }
            if(State.phase=="flight" && State.time>=State.shot.start+Math.Max(State.shot.duration,State.shot.shown)) {
                State.phase="result";State.resultUntil=State.time+ResultSeconds+(State.shot.holed?HoledExtraSeconds:0);State.revision++;
            } else if(State.phase=="result" && State.time>=State.resultUntil) {
                var p=State.golfers.First(g=>g.seat==State.shot.seat);p.x=State.shot.nextX;p.d=State.shot.nextD;
                if(State.shot.holed||p.strokes>=course.Holes[State.hole].Par+5)FinishGolfer(p);
                NextTurn();
            } else if(State.phase=="aim"&&State.time-State.turnAt>=60) {
                var p=State.golfers.First(g=>g.seat==State.turn);p.strokes++;if(p.strokes>=course.Holes[State.hole].Par+5)FinishGolfer(p);NextTurn();
            }
        }
        /// The shot set up as single player sets it up: along the hole to its next landing (the cup on the green), with the shortest
        /// club in the bag that gets there from this lie. (The player's phone then adds the climb to a raised green, which needs the
        /// course's ground, and says so with "golfClub".)
        public static void LineUp(NetworkGolfer p,Hole hole) {
            var at=new CoursePoint(p.x,p.d);var lie=hole.LieAt(at);bool putting=lie.IsPuttingSurface();
            var target=putting?hole.Pin:hole.RecommendedTarget(at);
            p.heading=(at.HeadingTo(target)%360+360)%360;
            p.club=(int)(putting?GolfClub.Putter:GolfClubs.ForDistance(at.DistanceTo(target),c=>lie.PowerFactor(c)));
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
            LineUp(eligible.First(p=>p.seat==State.turn),course.Holes[State.hole]);
            State.phase="aim";State.turnAt=State.time;State.look=0;State.load=0;
        }
        public void Drop(int seat) {
            var p=State.golfers.FirstOrDefault(g=>g.seat==seat);if(p==null||p.dnf)return;p.dnf=true;State.revision++;
            if(State.golfers.Count(g=>!g.dnf)<2){Complete("interrupted");return;}
            if(State.phase=="aim"&&State.turn==seat)NextTurn();
        }
        void Complete(string reason){if(State.complete)return;State.complete=true;State.phase=reason;State.revision++;Result?.Invoke(reason);}
    }
}

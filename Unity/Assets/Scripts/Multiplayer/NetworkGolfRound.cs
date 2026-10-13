using System;
using System.Linq;
using GolfArcade.Course;
using GolfArcade.Shot;
using GolfArcade.Swing;
namespace GolfArcade.Multiplayer {
    [Serializable] public sealed class NetworkGolfer {
        public int seat,strokes,club,total; public int emoteSlot=-1; /** The hole this golfer's phone has built (a new hole waits for every phone). */ public int ready=-1; /** The hole whose flyover this golfer's phone asked to skip. */ public int skipVote=-1; public long emoteID; public double emoteAt=-100; public double x,d,heading; public bool holed,dnf; public int[] card;
    }
    [Serializable] public sealed class NetworkGolfState {
        public long revision,shotID;public int hole,turn;public double time,turnAt,windSpeed,windDirection,presentationUntil,resultUntil;
        public string phase="aim";public bool paused,complete;
        /// The player up, as their phone shows it, for the TV: how far the aiming view looks up the hole to the pin (1) or down at the
        /// ball (-1), and the backswing's load (0–1).
        public float look,load;
        /// How fast the player up is turning their line (degrees a second, from their phone): the TV runs the line on between updates.
        public float aimRate;
        /// The hole's intro as single player would play it on the host's phone (full the first time, short after): every phone plays this cut.
        public bool introFull;
        /// The hole's intro beat by beat (host seconds): it began at introAt; the golfer up's own intro (their emote) begins at
        /// introWalkAt, the flyover's end, or sooner when every golfer asked to skip it (introSkip is then the hole). introSlot is
        /// the emote the golfer up picked for it (-1: their first equipped), at introSlotAt.
        public double introAt,introWalkAt,introSlotAt; public int introSkip=-1,introSlot=-1;
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
        /// The most a new hole waits for a phone that has not said it has built it.
        public const double HoleLoadTimeout=20;
        /// The phone that swung ends its result as single player does (3 s, the reaction, an emote played out, or the player's tap) and
        /// says so (golfResultDone); this is only the most a result waits if that word never comes.
        public const double ResultFallbackSeconds=14,HoledFallbackSeconds=18;
        /// Longest a turn may sit unplayed before it passes (online only; the TV counts the last TurnWarnSeconds down).
        public const double TurnLimitSeconds=180,TurnWarnSeconds=20;
        /// Everyone has holed out: the hole's card (a row each, as single player's card with others) before the next hole loads.
        public const double HoleCardSeconds=7;
        /// Every full swing is replayed from the tee box before its result (GolfGame), and the replay is given its time: the
        /// beat to see where the ball finished, the slow-motion strike, the flight and the landing (a holed ball to the cup).
        public static bool ReplaysShot(GolfClub club)=>club!=GolfClub.Putter;
        public static double ReplaySeconds(CourseShot shot)=>1.2+4.1+(shot.IsHoled?shot.Duration:Math.Min(shot.Duration,shot.LandingTime+2.0));
        public readonly NetworkGolfState State;
        readonly bool intros; readonly Course.Course course;readonly Random random;readonly long[] lastEvent=new long[4];
        /// Each phone builds every hole after the first behind its loading screen; the hole waits ("loading") until every golfer's phone
        /// has said so (golfHoleReady), so nobody plays while someone is still loading. Off for the bare rules (tests).
        readonly bool waitForLoad;
        /// Everyone is on one phone (pass the phone): turns just pass, with no time limit on a turn.
        readonly bool sharedPhone;
        double holeShownAt;
        public Action<NetworkGolfShot> Shot;public Action<string> Result;
        /// The host's game: whether a hole's intro is the full cut, and how long each part runs as single player plays it: the
        /// flyover (hole, full), the address (full); the golfer up's introduction, `walkOnWindow` long unless their player picks
        /// an emote (they may until then), which plays from `emoteFrom` into it (or when picked) and is played out.
        Func<int,bool> fullIntro; Func<int,bool,double> venueLength; Func<int,int,double> emoteLength; Func<bool,double> startLength;
        double walkOnWindow=.8, emoteFrom;
        const double EmoteSettle=.35;
        /// The seats played from the same phone as this one (pass-the-phone guests on the host's): one vote speaks for them all.
        public Func<int,int[]> SamePhone;
        public void ConfigureIntro(Func<int,bool> full,Func<int,bool,double> venue,Func<bool,double> start,double walkOnWindow,double emoteFrom,Func<int,int,double> emoteLength) {
            fullIntro=full; venueLength=venue; startLength=start; this.walkOnWindow=walkOnWindow; this.emoteFrom=emoteFrom; this.emoteLength=emoteLength;
            if(State.phase=="intro") { State.introFull=fullIntro?.Invoke(State.hole)??false; BeginIntro(); }
        }
        double VenueSeconds=>venueLength?.Invoke(State.hole,State.introFull)??1.5;
        double StartSeconds=>startLength?.Invoke(State.introFull)??.6;
        void BeginIntro() {
            State.phase="intro";State.introAt=State.time;State.introWalkAt=State.time+VenueSeconds;State.introSkip=State.introSlot=-1;State.introSlotAt=0;
            foreach(var g in State.golfers)g.skipVote=-1;
            TimeIntro();
        }
        /// The intro ends once the golfer's own introduction has had its window and any emote picked for it has played out, then the address.
        void TimeIntro() {
            double end=State.introWalkAt+walkOnWindow;
            if(State.introSlot>=0) end=Math.Max(end,Math.Max(State.introSlotAt,State.introWalkAt+emoteFrom)+(emoteLength?.Invoke(State.turn,State.introSlot)??0)+EmoteSettle);
            State.presentationUntil=end+StartSeconds;State.revision++;
        }
        /// `playing`: the course as the game has it loaded, its holes' ground, greens, trees and windmill read off their models
        /// (Course.ByKey makes a bare copy without them: flat, slopeless greens, nothing to hit).
        public NetworkGolfRound(int[] seats,int seed,bool intro=false,string courseKey=null,Course.Course playing=null,bool waitForLoad=false,bool sharedPhone=false) {
            if(seats==null||seats.Length<2||seats.Length>4||seats.Distinct().Count()!=seats.Length||seats.Any(s=>s<0||s>3))throw new ArgumentException("Invalid golf seats.");
            intros=intro; random=new Random(seed); this.waitForLoad=waitForLoad; this.sharedPhone=sharedPhone;
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
            // (the first hole was built while the match loaded: the match start already waited for every phone)
            if(waitForLoad&&index>0) { State.phase="loading";holeShownAt=State.time;return; }
            State.introFull=fullIntro?.Invoke(index)??false;
            if(intros) BeginIntro();
        }
        public bool Input(int seat,NetworkInput input,double hostTime) {
            var p=State.golfers.FirstOrDefault(p=>p.seat==seat);
            if(p==null||p.dnf||State.complete||State.paused||input==null||!input.Valid||input.time>hostTime+NetworkTuning.FutureTolerance||input.time<hostTime-NetworkTuning.PastTolerance)return false;
            if(input.action=="golfHoleReady") {
                if(input.value!=State.hole||p.ready==State.hole)return false;
                p.ready=State.hole;State.revision++;return true;
            }
            if(input.action=="golfResultDone") {
                // the player whose shot it was has seen its result through
                if(State.shot==null||input.value!=State.shot.id||State.shot.seat!=seat||(State.phase!="flight"&&State.phase!="result"))return false;
                if(State.phase=="flight"){State.phase="result";}
                State.resultUntil=State.time;State.revision++;return true;
            }
            if(input.action=="golfSkipIntro") {
                // a golfer's phone asks to skip the hole's flyover (as single player's tap does); it is skipped once every golfer still
                // playing has asked, and only the flyover: the golfer up's intro, their emote, always plays
                if(State.phase!="intro"||input.value!=State.hole||State.time>=State.introWalkAt)return false;
                foreach(int s in SamePhone?.Invoke(seat)??new[]{seat}) { var g=State.golfers.FirstOrDefault(x=>x.seat==s); if(g!=null)g.skipVote=State.hole; }
                if(State.golfers.Where(g=>!g.dnf).All(g=>g.skipVote==State.hole)) { State.introSkip=State.hole;State.introWalkAt=State.time;TimeIntro(); }
                State.revision++;return true;
            }
            if(input.action=="golfIntroEmote") {
                // the golfer up picks the emote they greet the gallery with: during the flyover or their own introduction's window
                if(State.phase!="intro"||State.turn!=seat||State.introSlot>=0||input.value<0||input.value>2||input.value!=Math.Floor(input.value)||State.time>=State.introWalkAt+walkOnWindow)return false;
                State.introSlot=(int)input.value;State.introSlotAt=State.time;TimeIntro();return true;
            }
            if(input.action=="golfHoleCardDone") {
                // anyone may move on from the hole's card (as a tap on single player's card does)
                if(State.phase!="holeDone"||input.value!=State.hole)return false;
                State.resultUntil=Math.Min(State.resultUntil,State.time+.2);State.revision++;return true;
            }
            if(input.action=="emote") {
                if(State.phase=="intro" || State.phase=="loading" || (State.turn==seat && State.phase!="result") || input.value<0 || input.value>2 || input.value!=Math.Floor(input.value) || State.time-p.emoteAt<1.5 || input.eventID<=lastEvent[seat])return false;
                lastEvent[seat]=input.eventID;p.emoteSlot=(int)input.value;p.emoteID++;p.emoteAt=State.time;State.revision++;return true;
            }
            if(p.holed || State.turn!=seat || State.phase!="aim")return false;
            switch(input.action) {
                case "aim":p.heading+=NetworkMath.Clamp(input.value,-1,1)*1.5;State.revision++;return true;
                case "golfAimHeading":p.heading=(input.value%360+360)%360;State.aimRate=NetworkMath.Clamp(input.value2,-120,120);State.revision++;return true;
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
            if(State.phase=="loading") {
                if(State.golfers.All(g=>g.dnf||g.ready==State.hole)||State.time-holeShownAt>=HoleLoadTimeout) {
                    if(intros){State.introFull=fullIntro?.Invoke(State.hole)??false;BeginIntro();} else State.phase="aim";
                    State.turnAt=State.time;State.revision++;
                }
                return;
            }
            if(State.phase=="intro") { if(State.time>=State.presentationUntil) {State.phase="aim";State.turnAt=State.time;State.revision++;} return; }
            if(State.phase=="holeDone") { if(State.time>=State.resultUntil) BeginHole(State.hole+1); return; }
            if(State.phase=="flight" && State.time>=State.shot.start+Math.Max(State.shot.duration,State.shot.shown)) {
                State.phase="result";State.resultUntil=State.time+(State.shot.holed?HoledFallbackSeconds:ResultFallbackSeconds);State.revision++;
            } else if(State.phase=="result" && State.time>=State.resultUntil) {
                var p=State.golfers.First(g=>g.seat==State.shot.seat);p.x=State.shot.nextX;p.d=State.shot.nextD;
                if(State.shot.holed||p.strokes>=course.Holes[State.hole].Par+5)FinishGolfer(p);
                NextTurn();
            } else if(State.phase=="aim"&&!sharedPhone&&State.time-State.turnAt>=TurnLimitSeconds) {
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
                if(State.hole+1==course.Holes.Length){Complete("complete");return;}
                State.phase="holeDone";State.resultUntil=State.time+HoleCardSeconds;return;
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

using System;
using System.Linq;
namespace GolfArcade.Multiplayer {
    [Serializable] public sealed class NetworkEmoteLoadout { public string[] emotes; public string skinHex,shirtHex,shortsHex; }
    [Serializable] public sealed class NetworkParticipant { public string id,name,controllerID,view; public int seat=-1; public bool ready,loaded,connected=true,female,left,invited=true; public NetworkEmoteLoadout loadout; }
    [Serializable] public sealed class NetworkConfiguration {
        public string lobbyID,matchID,hostID,localID,sport,venue; public int sets=1,games=3,seed;
        public NetworkParticipant[] participants;
        public bool Controls(string device, int seat) => participants != null && participants.Any(p=>p.seat==seat && p.connected && (p.id==device || (sport=="golf" && device==hostID && p.controllerID==device)));
        public int Seat(string id)=>participants?.FirstOrDefault(p=>p.id==id)?.seat ?? -1;
        /// How a phone shows a tennis match: "near" (its own TV, its player in front), "split" (the one TV in the room shows both players)
        /// or "none" (no screen: the phone is only a controller). Empty for golf and for spectators.
        public const string ViewNear="near", ViewSplit="split", ViewNone="none";
        public string ViewOf(string id)=>participants?.FirstOrDefault(p=>p.id==id)?.view ?? "";
        public string LocalView=>ViewOf(localID);
        /// Whose end of the court is drawn in front on this phone. A phone with its own TV puts its own player in front (as before); a phone
        /// whose TV shows both players, a controller-only phone and a spectator all use seat 0's end.
        public static int NearSide(int localSeat,string view)=>localSeat>=0 && view!=ViewSplit && view!=ViewNone ? localSeat : 0;
        /// Views exist only in tennis. At most one phone is "split", and when one is, the other competitor is "none".
        public bool ViewsValid {
            get {
                if(participants==null) return false;
                if(participants.Any(p=>!string.IsNullOrEmpty(p.view) && p.view!=ViewNear && p.view!=ViewSplit && p.view!=ViewNone)) return false;
                if(sport!="tennis") return participants.All(p=>string.IsNullOrEmpty(p.view));
                int split=participants.Count(p=>p.view==ViewSplit);
                return split<=1 && (split==0 || participants.Where(p=>p.seat>=0 && p.view!=ViewSplit).All(p=>p.view==ViewNone));
            }
        }
        public bool Valid => !string.IsNullOrEmpty(matchID) && !string.IsNullOrEmpty(lobbyID) && !string.IsNullOrEmpty(localID)
            && (sport=="tennis" || sport=="golf") && participants!=null && participants.Length>=2 && participants.Length<=4
            && participants.Any(p=>p.id==hostID) && participants.Any(p=>p.id==localID)
            && participants.Select(p=>p.id).Distinct().Count()==participants.Length
            && participants.All(p=>string.IsNullOrEmpty(p.controllerID) || (sport=="golf" && p.controllerID==hostID && p.id!=hostID))
            && participants.All(p=>p.seat>=-1 && p.seat<(sport=="tennis"?2:4))
            && participants.All(p=>GolfArcade.Tennis.TennisEmotes.Valid(p.loadout?.emotes)) && ViewsValid
            && participants.Where(p=>p.seat>=0).Select(p=>p.seat).Distinct().Count()==participants.Count(p=>p.seat>=0)
            && participants.Count(p=>p.seat>=0)>=2 && sets>=1 && sets<=3 && (games==1 || games==3 || games==6);
    }
    [Serializable] public sealed class NetworkPacket {
        public const int Version=4;
        public int version=Version; public string lobbyID,matchID,sender,kind,payload; public long sequence; public bool reliable=true; public double sentAt;
    }
    [Serializable] public sealed class NetworkInput {
        public string action; public double time,age; public float target,power,aim,depth=.75f,handSide,lift,facing,value,value2;
        public long eventID,point,contact; public int club; public int actorSeat=-1;
        /// A golf swing: the whole strike, and the club and line it was swung on (shotClub -1: none of these, the host's club and line
        /// and the power, aim and facing above).
        public NetworkImpact impact; public int shotClub=-1; public double heading;
        public bool Valid => !string.IsNullOrEmpty(action) && Finite(time) && Finite(age) && age>=0 && age<=NetworkTuning.MaxInputAge
            && Finite(target) && Finite(power) && Finite(aim) && Finite(depth) && Finite(value) && Finite(value2)
            && Finite(handSide) && Finite(lift) && Finite(facing) && Finite(heading) && (impact==null || impact.Valid);
        public static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
    }
    [Serializable] public struct NetworkVector {
        public float x,y,z;
        public NetworkVector(float x,float y,float z) { this.x=x;this.y=y;this.z=z; }
        public static NetworkVector Lerp(NetworkVector a,NetworkVector b,float t)=>new(a.x+(b.x-a.x)*t,a.y+(b.y-a.y)*t,a.z+(b.z-a.z)*t);
    }
    public static class NetworkMath {
        public static float Clamp(float v,float a,float b)=>Math.Max(a,Math.Min(b,v));
        public static float Toward(float a,float b,float step)=>a+Clamp(b-a,-step,step);
    }
}

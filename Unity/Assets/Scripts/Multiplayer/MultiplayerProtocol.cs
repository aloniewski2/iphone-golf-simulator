using System;
using System.Linq;
namespace GolfArcade.Multiplayer {
    [Serializable] public sealed class NetworkParticipant { public string id,name; public int seat=-1; public bool ready,loaded,connected=true,female,left,invited=true; }
    [Serializable] public sealed class NetworkConfiguration {
        public string lobbyID,matchID,hostID,localID,sport,venue; public int sets=1,games=3,seed;
        public NetworkParticipant[] participants;
        public int Seat(string id)=>participants?.FirstOrDefault(p=>p.id==id)?.seat ?? -1;
        public bool Valid => !string.IsNullOrEmpty(matchID) && !string.IsNullOrEmpty(lobbyID) && !string.IsNullOrEmpty(localID)
            && (sport=="tennis" || sport=="golf") && participants!=null && participants.Length>=2 && participants.Length<=4
            && participants.Any(p=>p.id==hostID) && participants.Any(p=>p.id==localID)
            && participants.Select(p=>p.id).Distinct().Count()==participants.Length
            && participants.All(p=>p.seat>=-1 && p.seat<(sport=="tennis"?2:4))
            && participants.Where(p=>p.seat>=0).Select(p=>p.seat).Distinct().Count()==participants.Count(p=>p.seat>=0)
            && participants.Count(p=>p.seat>=0)>=2 && sets>=1 && sets<=3 && (games==1 || games==3 || games==6);
    }
    [Serializable] public sealed class NetworkPacket {
        public const int Version=2;
        public int version=Version; public string lobbyID,matchID,sender,kind,payload; public long sequence; public bool reliable=true; public double sentAt;
    }
    [Serializable] public sealed class NetworkInput {
        public string action; public double time,age; public float target,power,aim,depth=.75f,handSide,lift,facing,value,value2;
        public long eventID,point,contact; public int club;
        public bool Valid => !string.IsNullOrEmpty(action) && Finite(time) && Finite(age) && age>=0 && age<=.25
            && Finite(target) && Finite(power) && Finite(aim) && Finite(depth) && Finite(value) && Finite(value2)
            && Finite(handSide) && Finite(lift) && Finite(facing);
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

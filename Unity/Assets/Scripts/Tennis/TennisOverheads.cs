using UnityEngine;

namespace GolfArcade.Tennis
{
    public static class TennisOverheads
    {
        // Pick a descending, pre-bounce overhead rather than waiting for a waist-high return.
        public static TennisRules.InterceptPlan Plan(Vector3 p, Vector3 v, float spin, float curve,
            Vector2 player, Vector3 racketOffset, float speed, float reaction, float currentSpeed = 0)
        {
            if (v.z >= 0 || p.y + v.y*v.y/(2*9.81f) < 3.3f) return default;
            for(float t=0; t<3; t+=TennisBall.Step*2) {
                TennisBall.Integrate(ref p,ref v,spin,TennisBall.Step*2,curve);
                if(p.y<TennisRules.BallRadius) break;
                if(v.y>=0 || p.y>Mathf.Clamp(racketOffset.y + .08f,2f,2.55f) || p.y<1.9f || p.z> -2 || p.z< -13.5f) continue;
                var goal=new Vector2(p.x-racketOffset.x,p.z-racketOffset.z);
                if(Mathf.Abs(goal.x)>TennisRules.CourtHalfWidth+1.5f || goal.y< -14.3f || goal.y> -2.6f) continue;
                float available=Mathf.Max(0,t-reaction);
                float cover=Mathf.Max(TennisRules.Coverable(available,speed),Mathf.Min(speed,currentSpeed)*available);
                float gap=Vector2.Distance(player,goal)-cover;
                if(gap>.25f) continue;
                return new TennisRules.InterceptPlan { Found=true,Reachable=true,Point=p,Time=t,Gap=gap };
            }
            return default;
        }
    }
    public sealed partial class TennisGame
    {
        public bool TrackingOverhead { get; private set; }
        public int AutoSmashAttempts { get; private set; }
        bool overheadAttempted;
        Vector2 overheadStand;
        TennisRules.InterceptPlan overheadPlan;
        void TryTrackedOverhead()
        {
            if(!TrackingOverhead || overheadAttempted || Player.Swinging || Player.GroundRecovering || DiveActive
                || !incoming || Flow!=Phase.Rally || resetTimer>0 || bounces>0) return;
            if(overheadPlan.Time<.06f || overheadPlan.Time>.32f) return;
            Vector3 cp=Player.ContactPoint(TennisActor.Stroke.Smash,false)-Player.transform.position;
            Vector2 goal=new Vector2(overheadPlan.Point.x-cp.x,overheadPlan.Point.z-cp.z);
            if(Vector2.Distance(goal,new Vector2(Player.transform.position.x,Player.transform.position.z))>.55f) return;
            overheadAttempted=true; overheadStand=goal; AutoSmashAttempts++;
            Player.Swing(.85f,false,TennisActor.Stroke.Smash);
            Player.PaceToContact(overheadPlan.Time); Player.GuideContact(overheadPlan.Point,overheadPlan.Time);
            consumedStroke=false; honestRejected=false;
            Feedback="OVERHEAD!";
        }
    }
}

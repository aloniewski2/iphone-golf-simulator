using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The clock HUD animations run on: real (unscaled) time in play, so pops and slams keep
    /// their speed through hit-stop and slow motion; game time while a fixed capture rate is
    /// set, so filmed frames show the animations at the speed they really play.
    public static class HudClock
    {
        public static float Now => Time.captureDeltaTime > 0 ? Time.time : Time.unscaledTime;
    }
}

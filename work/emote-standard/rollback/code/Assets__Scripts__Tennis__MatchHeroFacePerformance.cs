using UnityEngine;
namespace GolfArcade.Tennis
{
    /// Cosmetic blink, gaze and brow performance on the active face.
    /// Preserves rest bones, body animation and gameplay contacts.
    [DefaultExecutionOrder(1150), DisallowMultipleComponent]
    public sealed class MatchHeroFacePerformance : MonoBehaviour
    {
        public MatchHeroLook look;
        public float ForcedBlink = -1;
        public Vector2 ForcedGaze;
        HeroTennisDriver driver;
        float clock, blinkClock = -1, nextBlink = 2.7f, blink, brow;
        void LateUpdate()
        {
            if (!look) look = GetComponent<MatchHeroLook>(); if (!look || !look.face) return;
            if (!driver) driver = GetComponentInParent<HeroTennisDriver>();
            float dt = Mathf.Min(.05f, Time.timeScale > 0 ? Time.deltaTime : Time.unscaledDeltaTime); clock += dt;
            nextBlink -= dt;
            if (nextBlink <= 0) { blinkClock = 0; nextBlink = 3.1f + Mathf.Sin(clock * .73f) * .7f; }
            float pulse = 0;
            if (blinkClock >= 0) { blinkClock += dt; pulse = Mathf.Sin(Mathf.Clamp01(blinkClock / .16f) * Mathf.PI); if (blinkClock >= .16f) blinkClock = -1; }
            var c = driver ? driver.CurrentAction : HeroTennisDriver.Clip.Ready;
            bool happy = c == HeroTennisDriver.Clip.CelebratePoint || c == HeroTennisDriver.Clip.MatchWin || c == HeroTennisDriver.Clip.HitPerfect;
            bool sad = c == HeroTennisDriver.Clip.SadPointLost || c == HeroTennisDriver.Clip.MatchLose;
            bool effort = c == HeroTennisDriver.Clip.Serve || c == HeroTennisDriver.Clip.Smash;
            blink = Mathf.MoveTowards(blink, happy ? .16f : effort ? .12f : 0, dt * 3);
            brow = Mathf.MoveTowards(brow, happy ? .00065f : sad ? -.0006f : effort ? -.0004f : 0, dt * .006f);
            Vector2 gaze = ForcedGaze;
            if (driver && driver.game) {
                var head = look.Bone(HumanBodyBones.Head);
                if (head) { var to = transform.InverseTransformDirection((driver.game.BallPosition - head.position).normalized); gaze = new Vector2(to.x, to.y) * .0018f; }
            }
            float closure=ForcedBlink >= 0 ? ForcedBlink : Mathf.Max(blink,pulse);
            look.SetFacePerformance(closure, gaze, brow);
            HeroFaceReferenceFit.ApplyExpression(look,closure,gaze,brow);
        }
        void OnDisable() { if (look) { look.SetFacePerformance(0, Vector2.zero); HeroFaceReferenceFit.ApplyExpression(look,0,Vector2.zero,0); } }
    }
}

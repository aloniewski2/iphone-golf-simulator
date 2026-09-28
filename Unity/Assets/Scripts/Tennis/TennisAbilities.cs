using UnityEngine;

namespace GolfArcade.Tennis
{
    // Wire IDs are shared with TennisUltimate in SportsSession.swift. No paid tiers or perks.
    public enum TennisUltimate { Skybreaker = 0, RescueLob = 1, Curveball = 2 }

    public static class TennisAbilities
    {
        public const float DiveCooldown = 4f, DiveTravel = .42f, DiveDistance = 1.65f;
        public static string Name(TennisUltimate ability) => ability switch {
            TennisUltimate.RescueLob => "Rescue Lob", TennisUltimate.Curveball => "Curveball", _ => "Skybreaker"
        };
        public static Vector3 LobVelocity(Vector3 start, Vector3 target, float apex)
        {
            apex = Mathf.Max(apex, start.y + .5f);
            float up = Mathf.Sqrt(2 * 9.81f * (apex - start.y));
            float duration = up / 9.81f + Mathf.Sqrt(2 * (apex - target.y) / 9.81f);
            Vector3 v = (target - start) / duration; v.y = up; return v;
        }
        // Spin is zero here: constant lateral acceleration makes the landing exactly solvable.
        public static Vector3 CurveVelocity(Vector3 start, Vector3 target, float curve)
        {
            float duration = Mathf.Max(1.1f, Mathf.Abs(target.z - start.z) / 17f);
            return (target - start) / duration + new Vector3(-.5f * curve * duration, .5f * 9.81f * duration, 0);
        }
    }

    public sealed partial class TennisGame
    {
        public TennisUltimate SelectedUltimate { get; private set; }
        public string UltimateName => TennisAbilities.Name(SelectedUltimate);
        public bool UltimateArmed { get; private set; }
        public bool LoadoutLocked { get; private set; }
        public float DiveCooldownLeft { get; private set; }
        public bool DiveActive => diveTime > 0;
        public float DivePose => DiveActive ? Mathf.Sin(Mathf.PI * Mathf.Clamp01(1 - diveTime / .9f)) : 0;
        public float DiveSide => diveDirection.x < 0 ? -1 : 1;
        public int DiveAttempts { get; private set; }
        float diveTime, diveTravelled, ballCurve;
        Vector3 diveDirection;
        bool LiveAbilityInput => Player && Flow == Phase.Rally && resetTimer <= 0 && faultDelay <= 0
            && !CheckingTiming && !IntroPlaying && !ReplayPlaying && !(juice && juice.UltimateActive)
            && (ManualSimulation || Time.timeScale > 0);
        public bool CanDive => LiveAbilityInput && incoming && !Player.Swinging && !Player.GroundRecovering
            && DiveCooldownLeft <= 0 && !DiveActive;
        public bool CanArmUltimate => LiveAbilityInput && PlayerUltimate >= 1;
        /// Armed ultimates fire on the player's very next real contact, whatever the stroke (Skybreaker included).
        bool EligibleUltimateContact => Player.Kind != TennisActor.Stroke.Dive;

        public bool SelectUltimate(int value)
        {
            if (LoadoutLocked || value < 0 || value > 2) return false;
            SelectedUltimate = (TennisUltimate)value; UltimateArmed = false; return true;
        }
        public void LockLoadout() => LoadoutLocked = true;
        public bool ToggleUltimate()
        {
            if (!LiveAbilityInput) return false;
            if (UltimateArmed) { UltimateArmed = false; playerUltCharged = false; return true; }
            if (!CanArmUltimate) return false;
            UltimateArmed = true;
            Feedback = UltimateName + " armed — your next hit fires it";
            return true;
        }
        public bool RequestDive()
        {
            if (!CanDive) return false;
            Vector3 delta = PredictBall(.18f) - Player.transform.position; delta.y = 0;
            diveDirection = delta.sqrMagnitude > .01f ? delta.normalized : Vector3.right;
            diveTime = .9f; diveTravelled = 0; DiveCooldownLeft = TennisAbilities.DiveCooldown;
            DiveAttempts++; Stamina = Mathf.Max(0, Stamina - TennisRules.DiveStamina);
            Player.Swing(.4f, (delta.x < 0) != Player.LeftHanded, TennisActor.Stroke.Dive);
            consumedStroke = false; honestRejected = false; CompensateDisplayDelay();
            Feedback = "DIVE — time the reach";
            return true;
        }
        void MoveDive(float dt)
        {
            float travel = Mathf.Min(dt, Mathf.Max(0, TennisAbilities.DiveTravel - diveTravelled));
            diveTravelled += travel;
            Vector3 p = Player.transform.position + diveDirection * (travel * TennisAbilities.DiveDistance / TennisAbilities.DiveTravel);
            p.x = Mathf.Clamp(p.x, -TennisRules.CourtHalfWidth - 2.4f, TennisRules.CourtHalfWidth + 2.4f);
            p.z = Mathf.Clamp(p.z, -14.3f, -2.6f);
            Player.transform.position = p; moveVelocity = Vector2.zero; LateralSpeed = 0;
        }
        void TickAbilities(float dt)
        {
            DiveCooldownLeft = Mathf.Max(0, DiveCooldownLeft - dt);
            diveTime = Mathf.Max(0, diveTime - dt);
            if (Flow == Phase.Rally) LoadoutLocked = true;
        }
        void ResetPointAbilities()
        {
            UltimateArmed = false; playerUltCharged = rivalUltCharged = false;
            diveTime = 0; ballCurve = 0; TrackingOverhead = false; overheadAttempted = false;
        }
        void ResetMatchAbilities()
        {
            ResetPointAbilities(); PlayerUltimate = RivalUltimate = 0;
            DiveCooldownLeft = 0; DiveAttempts = 0; LoadoutLocked = false;
        }
        void ApplySelectedUltimate(Vector3 target, float speed)
        {
            switch (SelectedUltimate)
            {
                case TennisUltimate.RescueLob:
                    BallSpin = 0; target.z = 10; target.y = TennisRules.BallRadius;
                    BallVelocity = TennisAbilities.LobVelocity(BallPosition, target, 6f);
                    break;
                case TennisUltimate.Curveball:
                    BallSpin = 0; ballCurve = AimInput < 0 ? -4f : 4f;
                    target.x = Mathf.Clamp(target.x, -3.4f, 3.4f); target.y = TennisRules.BallRadius;
                    BallVelocity = TennisAbilities.CurveVelocity(BallPosition, target, ballCurve);
                    break;
                default:
                    BallVelocity = TennisRules.RallyVelocity(BallPosition, target, Mathf.Min(34, speed * UltimateSpeed), BallSpin, 1);
                    break;
            }
            Feedback = UltimateName.ToUpperInvariant() + "!";
        }
    }
}

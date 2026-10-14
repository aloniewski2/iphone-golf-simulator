using System;
using GolfArcade.Swing;
using UnityEngine;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;

namespace GolfArcade.Tennis
{
    /// This phone's gyro outside the app (the standalone build). Tilt is a prototype movement control, not tracking.
    public sealed class TennisPhoneInput : MonoBehaviour
    {
        TennisGame game;
        readonly PhoneMotionSource local = new();
        NQuaternion neutral;
        bool calibrated, armed = true;
        double clock, peakStarted, lastSampleAt = double.NegativeInfinity;
        float peak, lateral;
        public bool HasMotionControl => clock - lastSampleAt < .3;

        void Start()
        {
            game = GetComponent<TennisGame>();
            if (GolfArcade.Game.NativeSportsSession.Active) { enabled=false; return; }
            clock = Time.unscaledTimeAsDouble; local.Start();
        }

        void Update()
        {
            clock = Time.unscaledTimeAsDouble;
            if (!game || game.ManualSimulation) return;
            if (Input.GetKeyDown(KeyCode.C)) calibrated = false;
            IMotionSource source = local;
            while (source.TryRead(out var sample))
            {
                lastSampleAt = clock;
                if (!calibrated) { neutral = sample.Attitude; calibrated = true; }
                var relative = NQuaternion.Normalize(NQuaternion.Inverse(neutral) * sample.Attitude);
                lateral = Mathf.Clamp(-NVector3.Transform(NVector3.UnitY, relative).X / .45f, -1, 1);
                float rate = sample.RotationRate.Length();
                if (rate < 1.5f) { armed = true; peak = 0; }
                if (armed && rate > 4.5f)
                {
                    if (peak == 0) peakStarted = clock;
                    peak = Mathf.Max(peak, rate);
                    if (clock - peakStarted > .04) { game.RequestSwing(Mathf.Clamp01(peak / 14)); armed = false; }
                }
            }
            if (HasMotionControl)
            {
                // Suppress tilt steering during a stroke: swing rotation is not a side-step.
                float movement = game.Player && game.Player.Swinging ? 0 : lateral;
                game.SetLateralInput(movement, Mathf.Abs(movement) > .8f);
                game.InputStatus = "PHONE · calibrated tilt moves · swing to hit";
            }
            else game.InputStatus = "Keyboard · A/D move · Shift sprint · hold/release Space · arrows aim";
        }

        void OnDestroy() { local.Stop(); }
    }
}

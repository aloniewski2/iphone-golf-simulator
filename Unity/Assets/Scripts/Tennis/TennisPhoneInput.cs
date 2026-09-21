using System;
using GolfArcade.Net;
using GolfArcade.Swing;
using UnityEngine;
using NQuaternion = System.Numerics.Quaternion;
using NVector3 = System.Numerics.Vector3;

namespace GolfArcade.Tennis
{
    /// Existing gyro/LAN protocol adapter. Tilt is a prototype movement control, not tracking.
    public sealed class TennisPhoneInput : MonoBehaviour
    {
        TennisGame game;
        NetworkMotionSource network;
        readonly PhoneMotionSource local = new();
        NQuaternion neutral;
        bool calibrated, armed = true;
        double clock, peakStarted, lastSampleAt = double.NegativeInfinity;
        float peak, lateral;
        public bool HasMotionControl => clock - lastSampleAt < .3;

        void Start()
        {
            game = GetComponent<TennisGame>(); clock = Time.unscaledTimeAsDouble; local.Start();
            try { network = new NetworkMotionSource(() => clock); network.Start(); }
            catch (Exception e) { Debug.LogWarning("Tennis phone connection unavailable: " + e.Message); network?.Dispose(); network = null; }
        }

        void Update()
        {
            clock = Time.unscaledTimeAsDouble;
            if (!game || game.ManualSimulation) return;
            if (Input.GetKeyDown(KeyCode.C)) calibrated = false;
            IMotionSource source = network != null && network.IsConnected ? network : local;
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
                if (network != null && network.IsConnected)
                {
                    int buttons = (network.Buttons.HasFlag(ControllerButtons.AimRight) ? 1 : 0) - (network.Buttons.HasFlag(ControllerButtons.AimLeft) ? 1 : 0);
                    if (buttons != 0) movement = buttons;
                }
                game.SetLateralInput(movement, Mathf.Abs(movement) > .8f);
                game.InputStatus = "PHONE · calibrated tilt/buttons move · swing to hit (physical-position tracking pending)";
                network?.SendAck(0, game.LastHit.Quality, game.Feedback);
            }
            else game.InputStatus = "Keyboard · A/D move · Shift sprint · hold/release Space · arrows aim";
        }

        void OnDestroy() { network?.Dispose(); local.Stop(); }
    }
}

using UnityEngine;

namespace GolfArcade.Game
{
    /// Aim footwork for the Hero golfer. While the golfer stands at address and the aim turns, GolfGame re-places the whole
    /// golfer on a circle round the ball every frame (GolferView.Stand), so the address pose used to slide its shoes along that
    /// arc. Now the body still turns with the aim, but each shoe stays planted where it is until it is a few centimetres (or
    /// degrees) off its place in the stance, then takes a quick small side step to it: the foot on the side the stance is moving
    /// steps first and the other follows, so a long turn reads as a little shuffle round the ball, one foot after the other.
    /// Runs after the clip pose (LateUpdate); only at address, never in a swing or a move; a jump to a new ball snaps the feet.
    public sealed class GolferFootwork
    {
        sealed class Leg
        {
            public Transform up, low, foot; public bool right;
            public Vector3 planted; public Quaternion plantedRot;
            public bool stepping; public float u, dur; public Vector3 from; public Quaternion fromRot;
            public Vector3 shown; public Quaternion shownRot;
        }

        /// A foot steps once it is this far from its place in the stance (metres) or turned this much (degrees).
        public const float StepDistance = .035f, StepAngle = 6f;
        /// One step: seconds for a short side step (longer for a longer one) and how high the shoe lifts.
        const float StepSeconds = .17f, StepSecondsPerMetre = .9f, Lift = .045f;
        /// The longest single step; a bigger turn takes more of them. Past SnapDistance (a joystick flick to a new line) the shoes are re-placed.
        const float MaxStep = .30f, SnapDistance = .45f;
        /// Off: the old behaviour (the whole stance turns, shoes and all). For before/after films.
        public static bool Disabled;
        readonly Transform root;
        readonly Leg[] legs;
        bool reset = true; float weight; Vector3 lastRoot; int lastLead = -1;
        public int Steps { get; private set; }
        public bool Stepping => legs[0].stepping || legs[1].stepping;

        GolferFootwork(Transform root, Leg[] legs) { this.root = root; this.legs = legs; }

        public static GolferFootwork Build(Transform root, System.Collections.Generic.IReadOnlyDictionary<string, Transform> bones)
        {
            Leg Make(string side, bool right)
            {
                if (!bones.TryGetValue(side + "UpperLeg", out var up) || !bones.TryGetValue(side + "LowerLeg", out var low) || !bones.TryGetValue(side + "Foot", out var foot)) return null;
                return new Leg { up = up, low = low, foot = foot, right = right };
            }
            var l = Make("Left", false); var r = Make("Right", true);
            return l == null || r == null ? null : new GolferFootwork(root, new[] { l, r });
        }

        /// Forget the planted feet (a new ball, a move, a swing): they take the pose's places again.
        public void Reset() { reset = true; }

        /// `atAddress`: the golfer is standing over the ball and not swinging. Call after the pose has been evaluated.
        public void Tick(bool atAddress, float dt)
        {
            weight = Mathf.MoveTowards(weight, atAddress && !Disabled ? 1 : 0, dt / .1f);
            if (!atAddress) reset = true;
            if (weight <= 0) { reset = true; return; }
            // a new ball (or a big jump): put the shoes where the pose has them, no steps
            if ((root.position - lastRoot).sqrMagnitude > .25f * .25f) reset = true;
            lastRoot = root.position;
            foreach (var g in legs)
            {
                if (!reset) continue;
                g.planted = g.foot.position; g.plantedRot = g.foot.rotation; g.stepping = false; g.shown = g.planted; g.shownRot = g.plantedRot;
            }
            reset = false;

            // where the stance wants each shoe now (the clip's pose under the turned golfer); "right" = along the stance, left shoe to right shoe
            Vector3 right = legs[1].planted - legs[0].planted; right.y = 0;
            if (right.sqrMagnitude < 1e-6f) right = root.right;
            right.Normalize();
            for (int i = 0; i < 2; i++) { var off = legs[i].foot.position - legs[i].planted; off.y = 0; if (off.magnitude > SnapDistance) { reset = true; } }
            if (reset) { foreach (var g in legs) { g.planted = g.foot.position; g.plantedRot = g.foot.rotation; g.stepping = false; } reset = false; lastLead = -1; return; }
            var want = new Vector3[2]; var wantRot = new Quaternion[2]; var err = new float[2];
            for (int i = 0; i < 2; i++)
            {
                want[i] = legs[i].foot.position; wantRot[i] = legs[i].foot.rotation;
                var d = want[i] - legs[i].planted; d.y = 0;
                err[i] = Mathf.Max(d.magnitude / StepDistance, Quaternion.Angle(legs[i].plantedRot, wantRot[i]) / StepAngle);
            }
            // start a step: only one shoe in the air at a time; the side the stance is moving leads, the other follows it in
            if (!legs[0].stepping && !legs[1].stepping)
            {
                int lead = -1;
                if (err[0] >= 1 || err[1] >= 1)
                {
                    var drift = (want[0] - legs[0].planted) + (want[1] - legs[1].planted);
                    float side = Vector3.Dot(drift, right);
                    lead = Mathf.Abs(side) > .01f ? (side > 0 ? 1 : 0) : (err[1] > err[0] ? 1 : 0);
                    // a follow-up: the shoe that did not just step goes next, so the feet alternate
                    if (lastLead >= 0 && err[1 - lastLead] >= .45f) lead = 1 - lastLead;
                }
                else if (lastLead >= 0 && err[1 - lastLead] >= .45f) lead = 1 - lastLead;    // close the stance after a turn
                if (lead >= 0)
                {
                    var g = legs[lead];
                    g.stepping = true; g.u = 0; g.from = g.planted; g.fromRot = g.plantedRot;
                    var span = want[lead] - g.planted; span.y = 0;
                    g.dur = StepSeconds + span.magnitude * StepSecondsPerMetre * .5f;
                    lastLead = lead; Steps++;
                }
            }
            // the shoes: planted, or on their way (the step keeps aiming at where the stance wants it while the aim still turns)
            for (int i = 0; i < 2; i++)
            {
                var g = legs[i];
                if (g.stepping)
                {
                    g.u = Mathf.Min(1, g.u + dt / g.dur);
                    float s = g.u * g.u * (3 - 2 * g.u);
                    var to = want[i]; var span = to - g.from; span.y = 0;
                    if (span.magnitude > MaxStep) to = g.from + span.normalized * MaxStep + Vector3.up * (want[i].y - g.from.y);
                    float turn = Quaternion.Angle(g.fromRot, wantRot[i]);
                    var toRot = turn > 25f ? Quaternion.Slerp(g.fromRot, wantRot[i], 25f / turn) : wantRot[i];
                    var p = Vector3.Lerp(g.from, to, s);
                    p.y = Mathf.Lerp(g.from.y, to.y, s) + Lift * Mathf.Sin(Mathf.PI * g.u);
                    // the heel comes up a touch more than the toe in the air
                    var r = Quaternion.Slerp(g.fromRot, toRot, s);
                    r = Quaternion.AngleAxis(-8f * Mathf.Sin(Mathf.PI * g.u), right) * r;
                    g.shown = p; g.shownRot = r;
                    if (g.u >= 1) { g.stepping = false; g.planted = to; g.plantedRot = toRot; g.shown = g.planted; g.shownRot = g.plantedRot; }
                }
                else { g.shown = g.planted; g.shownRot = g.plantedRot; }
            }
            // pose the legs onto the shoes (the hips, hands and club stay as the clip has them)
            foreach (var g in legs)
            {
                var target = Vector3.Lerp(g.foot.position, g.shown, weight);
                var rot = Quaternion.Slerp(g.foot.rotation, g.shownRot, weight);
                if ((target - g.foot.position).sqrMagnitude < 1e-8f && Quaternion.Angle(rot, g.foot.rotation) < .01f) continue;
                TwoBone(g.up, g.low, g.foot, target, root.forward);
                g.foot.rotation = rot;
            }
        }

        /// Two-bone IK keeping the knee in the plane it already bends in.
        static void TwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 poleHint)
        {
            Vector3 A = a.position, B = b.position, C = c.position;
            float l1 = (B - A).magnitude, l2 = (C - B).magnitude;
            var toT = target - A;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(l1 - l2) + 1e-4f, l1 + l2 - 1e-4f);
            var dir = toT.normalized;
            var bend = Vector3.ProjectOnPlane(B - A, dir);
            if (bend.sqrMagnitude < 1e-8f) bend = Vector3.ProjectOnPlane(poleHint, dir);
            bend.Normalize();
            float along = (l1 * l1 - l2 * l2 + d * d) / (2 * d);
            var knee = A + dir * along + bend * Mathf.Sqrt(Mathf.Max(0, l1 * l1 - along * along));
            a.rotation = Quaternion.FromToRotation(B - A, knee - A) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
        }
    }
}

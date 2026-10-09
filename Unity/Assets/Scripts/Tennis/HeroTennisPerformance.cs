using System;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Cosmetic performance on the actual Generic match rig. Contact, authored emote
    /// choreography, finger grips and prop transforms remain owned by their clips.
    public sealed class HeroTennisPerformance
    {
        readonly MatchHeroLook hero;
        readonly Transform head, neck, chest, spine;
        Vector2 lastVelocity, lean, leanVelocity;
        Vector2 gaze, gazeVelocity;
        float nextGlance = 3.7f, glanceRemaining, glanceYaw;
        uint random;

        public Vector2 LeanDegrees => lean;
        public Vector2 GazeDegrees => gaze;

        public HeroTennisPerformance(MatchHeroLook look)
        {
            hero = look;
            head = look.Bone(HumanBodyBones.Head); neck = look.Bone(HumanBodyBones.Neck);
            chest = look.Bone(HumanBodyBones.Chest); spine = look.Bone(HumanBodyBones.Spine);
            random = look.female ? 179u : 113u;
        }

        float Rand()
        {
            // Per hero, deterministic, and independent of gameplay's random state.
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return (random & 0xffffff) / 16777215f;
        }

        public void Apply(TennisActor actor, TennisGame game, Transform frame,
                          bool action, bool emote, bool reaction, float dt)
        {
            if (!actor || dt <= 0 || !hero) return;
            dt = Mathf.Min(dt, .05f);
            var velocity = new Vector2(actor.Speed, actor.ForwardSpeed);
            var acceleration = (velocity - lastVelocity) / Mathf.Max(.008f, dt);
            lastVelocity = velocity;
            bool contact = actor.Swinging || actor.PrepareAmount > .02f || actor.PrepareServe;
            bool free = !action && !emote && !reaction && !contact && !(game && game.DiveActive);
            bool moving = velocity.magnitude > .22f;

            // A player's planted run carries weight into a turn and settles into a stop.
            // The feet and root are never translated, and this fades out before a stroke.
            var wantLean = free && moving
                ? new Vector2(Mathf.Clamp(-velocity.x * .65f - acceleration.x * .035f, -4.5f, 4.5f),
                              Mathf.Clamp(velocity.y * .4f + acceleration.y * .025f, -3.5f, 3.5f))
                : Vector2.zero;
            lean = Vector2.SmoothDamp(lean, wantLean, ref leanVelocity, .11f, 80, dt);
            if (free && spine && chest)
            {
                var q = Quaternion.AngleAxis(lean.x, frame.forward) * Quaternion.AngleAxis(lean.y, frame.right);
                spine.rotation = Quaternion.Slerp(Quaternion.identity, q, .35f) * spine.rotation;
                chest.rotation = Quaternion.Slerp(Quaternion.identity, q, .65f) * chest.rotation;
            }

            bool idle = free && !moving;
            if (idle)
            {
                nextGlance -= dt;
                if (nextGlance <= 0)
                {
                    glanceYaw = (Rand() < .5f ? -1 : 1) * Mathf.Lerp(6, 11, Rand());
                    glanceRemaining = Mathf.Lerp(.65f, 1.05f, Rand());
                    nextGlance = Mathf.Lerp(4.1f, 7.6f, Rand());
                }
                glanceRemaining = Mathf.Max(0, glanceRemaining - dt);
            }
            else glanceRemaining = 0;

            // Read the ball with the head rather than a mechanically forward stare.
            // Authored intros, taunts and reactions own the head completely.
            Vector2 wantedGaze = Vector2.zero;
            if (!emote && !reaction && head && game)
            {
                var to = frame.InverseTransformDirection(game.BallPosition - head.position);
                if (to.sqrMagnitude > .04f && to.z > .1f)
                {
                    wantedGaze.x = Mathf.Clamp(Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, -18, 18);
                    wantedGaze.y = Mathf.Clamp(-Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg, -10, 8);
                }
                if (idle && glanceRemaining > 0) wantedGaze.x += glanceYaw;
                if (contact) wantedGaze *= .18f;
            }
            gaze = Vector2.SmoothDamp(gaze, wantedGaze, ref gazeVelocity, .13f, 140, dt);
            if (!emote && !reaction && head)
            {
                var q = Quaternion.AngleAxis(gaze.x, frame.up) * Quaternion.AngleAxis(gaze.y, frame.right);
                if (neck) neck.rotation = Quaternion.Slerp(Quaternion.identity, q, .32f) * neck.rotation;
                head.rotation = Quaternion.Slerp(Quaternion.identity, q, .68f) * head.rotation;
            }
        }
    }
}

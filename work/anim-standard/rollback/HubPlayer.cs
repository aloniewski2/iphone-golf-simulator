using System;
using GolfArcade.Tennis;
using UnityEngine;

namespace GolfArcade.Hub
{
    /// The player's hero in the plaza: phone stick -> camera-relative ground velocity -> CharacterController, the hero turns to face
    /// where it goes, and HubHeroAnimator matches the legs to the speed.
    ///
    /// The stick is analog (PLAN §6): a short push walks, a full push runs, holding it full sprints; letting go stops within 0.25 s.
    ///   deflection < Dead            stand
    ///   Dead .. RunAt                walk, 0.6 .. 1.6 m/s with the deflection
    ///   >= RunAt                     run 3.25 m/s (the run clip's own speed)
    ///   >= SprintAt for SprintAfter  sprint 4.3 m/s
    public sealed class HubPlayer : MonoBehaviour
    {
        public const float Dead = .12f, RunAt = .7f, SprintAt = .92f, SprintAfter = .9f;
        public const float WalkMin = .6f, WalkMax = 1.6f, RunSpeed = 3.25f, SprintSpeed = 4.3f;
        public const float Accel = 14f, Brake = 22f, TurnRate = 720f;

        public CharacterController body;
        public HubHeroAnimator hero;
        public MatchHeroLook look;
        public bool female;
        /// Camera yaw the stick is relative to (stick up = away from the camera).
        public float cameraYaw;
        /// Input is ignored (door walk-through, seated, station panel open).
        public bool locked;

        Vector2 stick; float stickMag, fullHeld; double stickTime;
        Vector3 velocity; float yaw;
        public Vector3 Velocity => velocity;
        public float Speed => new Vector2(velocity.x, velocity.z).magnitude;
        public float Yaw => yaw;
        public bool Sprinting => fullHeld >= SprintAfter && stickMag >= SprintAt;
        public Vector2 Stick => stick;
        public float StickMagnitude => stickMag;
        /// When the newest stick reading was taken (phone clock; 0 for editor input).
        public double StickTime => stickTime;
        public event Action<float> Moved;

        public static HubPlayer Spawn(Transform parent, bool female, Vector3 at, float facingYaw)
        {
            var root = new GameObject("Hub player");
            root.transform.SetParent(parent, false);
            root.transform.position = at;
            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.7f; cc.radius = .32f; cc.center = new Vector3(0, .85f, 0); cc.stepOffset = .3f; cc.skinWidth = .03f; cc.minMoveDistance = 0;
            var p = root.AddComponent<HubPlayer>(); p.body = cc; p.yaw = facingYaw;
            p.SetHero(female);
            return p;
        }

        /// Puts the male or female match hero under this root (again, when the locker changes the body).
        public void SetHero(bool female)
        {
            if (hero && this.female == female) return;
            if (hero) Destroy(hero.gameObject);
            this.female = female;
            var prefab = TennisCustomization.HeroBase(female);
            if (!prefab) { Debug.LogError("[Hub] match hero prefab missing: " + TennisCustomization.HeroPath(female)); return; }
            var go = Instantiate(prefab, transform, false);
            go.name = female ? "Plaza hero Female" : "Plaza hero Male";
            go.transform.localPosition = Vector3.zero; go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            HubWorld.SetLayer(go.transform);
            look = go.GetComponent<MatchHeroLook>();
            // the plaza hero walks empty-handed: the racket stays in the bag until a match
            if (look && look.racketGrip) foreach (var r in look.racketGrip.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            hero = HubHeroAnimator.Attach(go);
        }

        /// Newest phone stick reading: direction (-1..1 each) and deflection 0..1.
        public void SetStick(float x, float y, float magnitude, double time = 0)
        {
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(magnitude) || float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(magnitude)) return;
            var v = new Vector2(Mathf.Clamp(x, -1, 1), Mathf.Clamp(y, -1, 1));
            float m = Mathf.Clamp01(magnitude);
            stick = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero; stickMag = stick == Vector2.zero ? 0 : m; stickTime = time;
        }

        /// Ground speed the stick asks for right now.
        public float WantedSpeed()
        {
            if (locked || stickMag < Dead) return 0;
            if (stickMag < RunAt) return Mathf.Lerp(WalkMin, WalkMax, Mathf.InverseLerp(Dead, RunAt, stickMag));
            return Sprinting ? SprintSpeed : RunSpeed;
        }

        public void Teleport(Vector3 position, float facingYaw)
        {
            body.enabled = false; transform.position = position; body.enabled = true;
            velocity = Vector3.zero; yaw = facingYaw;
            if (hero) { hero.transform.localRotation = Quaternion.Euler(0, yaw, 0); hero.ResetFootTracking(); }
        }

        /// Walks the hero along `direction` at `speed` without the stick (door walk-throughs).
        public Vector3? Autopilot;
        public float AutopilotSpeed = 2.4f;

        void Update()
        {
            float dt = Time.unscaledDeltaTime; if (dt <= 0) return;   // unscaled: a match loading behind the plaza pauses Time.timeScale
            fullHeld = stickMag >= SprintAt && !locked ? fullHeld + dt : 0;
            Vector3 want;
            if (Autopilot.HasValue) want = Autopilot.Value.normalized * AutopilotSpeed;
            else
            {
                float speed = WantedSpeed();
                var dir = Quaternion.Euler(0, cameraYaw, 0) * new Vector3(stick.x, 0, stick.y);
                want = speed > 0 ? dir.normalized * speed : Vector3.zero;
            }
            if (locked && !Autopilot.HasValue)
            {
                // seated or at a station: no physics (a bench's collider would push the capsule off it)
                velocity = Vector3.zero; if (hero) { hero.transform.localRotation = Quaternion.Euler(0, yaw, 0); hero.Tick(0, dt); } Moved?.Invoke(0); return;
            }
            var flat = new Vector3(velocity.x, 0, velocity.z);
            // accelerate toward the wanted velocity; braking (shorter or reversing) is quicker than speeding up
            float rate = want.sqrMagnitude < flat.sqrMagnitude || Vector3.Dot(want, flat) < 0 ? Brake : Accel;
            flat = Vector3.MoveTowards(flat, want, rate * dt);
            velocity = new Vector3(flat.x, body.isGrounded ? -1f : velocity.y - 9.81f * dt, flat.z);
            var before = transform.position;
            body.Move(velocity * dt);
            // what really happened (walls slow you): the legs follow the actual ground speed, not the wish
            var moved = transform.position - before; moved.y = 0;
            var actual = moved / dt; velocity.x = actual.x; velocity.z = actual.z;
            if (body.isGrounded) velocity.y = -1f;
            float ground = actual.magnitude;
            if (ground > .2f || (want.sqrMagnitude > .01f))
            {
                var face = ground > .2f ? actual : want;
                float target = Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg;
                yaw = Mathf.MoveTowardsAngle(yaw, target, TurnRate * dt);
            }
            if (hero) { hero.transform.localRotation = Quaternion.Euler(0, yaw, 0); hero.Tick(ground, dt); }
            Moved?.Invoke(ground);
        }

        public bool PlayEmote(string clip) => hero && Speed < .4f && hero.Play(clip);
    }
}

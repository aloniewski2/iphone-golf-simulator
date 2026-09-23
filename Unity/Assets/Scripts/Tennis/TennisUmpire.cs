using UnityEngine;

namespace GolfArcade.Tennis
{
    /// The chair umpire (Higgsfield model rigged by blender/scripts/prepare_umpire.py), seated
    /// on the arena's umpire chair at the net.
    ///
    /// No authored clips: he follows the ball with his head (clamped, eased, the way a real
    /// umpire tracks a rally without swivelling), breathes, leans in a touch while the ball is
    /// live, and nods when he calls a point.
    public sealed class TennisUmpire : MonoBehaviour
    {
        /// The seat of the arena's umpire chair, measured from the arena mesh (the platform
        /// sits 2.1 m up at the left net post).
        public static readonly Vector3 Seat = new(-7.45f, 2.22f, 0f);
        const float MaxYaw = 62f, MaxPitch = 22f;

        Transform head, spine;
        Quaternion headRest, spineRest;
        Vector3 forward;                  // his rest facing, in world space
        Quaternion look = Quaternion.identity;
        float nodAt = -9, lean, breath;

        public static TennisUmpire Spawn(Transform parent)
        {
            var prefab = Resources.Load<GameObject>("Tennis/Umpire/TennisUmpire");
            if (!prefab) return null;
            var model = Instantiate(prefab, parent);
            model.name = "Chair umpire";
            TennisLook.PrepareCharacter(model);
            var umpire = model.AddComponent<TennisUmpire>();
            umpire.Place();
            return umpire;
        }

        void Place()
        {
            transform.position = Seat;
            var bones = GetComponentsInChildren<Transform>(true);
            head = System.Array.Find(bones, t => t.name == "Head");
            spine = System.Array.Find(bones, t => t.name == "Spine");
            var marker = System.Array.Find(bones, t => t.name == "UmpireFacing");
            // Turn him to face the court (+X from the left-hand chair), whatever the export axes.
            if (marker)
            {
                Vector3 facing = Vector3.ProjectOnPlane(marker.position - transform.position, Vector3.up);
                if (facing.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.FromToRotation(facing.normalized, Vector3.right) * transform.rotation;
            }
            forward = Vector3.right;
            if (head) headRest = head.rotation;
            if (spine) spineRest = spine.rotation;
        }

        /// Called when a point is decided.
        public void Call() => nodAt = Time.time;

        /// Once a frame from the game: where the ball is and whether it is in play.
        public void Follow(Vector3 ball, bool live, float dt)
        {
            if (!head) return;
            // In Unity a positive turn about the character's right-hand axis tips forward.
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 to = ball - head.position;
            Vector3 flat = Vector3.ProjectOnPlane(to, Vector3.up);
            float yaw = Mathf.Clamp(Vector3.SignedAngle(forward, flat, Vector3.up), -MaxYaw, MaxYaw);
            float pitch = Mathf.Clamp(Mathf.Atan2(-to.y, Mathf.Max(.5f, flat.magnitude)) * Mathf.Rad2Deg, -MaxPitch, MaxPitch);
            var turn = Quaternion.AngleAxis(yaw, Vector3.up);
            var target = turn * Quaternion.AngleAxis(pitch, right);
            // The head catches up with the ball quickly but never snaps.
            look = Quaternion.Slerp(look, target, 1 - Mathf.Exp(-dt * 7f));
            if (spine)
            {
                // Shoulders take a little of the turn; he leans in while the ball is live.
                lean = Mathf.MoveTowards(lean, live ? 4f : 0f, dt * 8f);
                breath += dt;
                float rise = Mathf.Sin(breath * 1.6f) * 1.1f;
                var body = Quaternion.Slerp(Quaternion.identity, look, .18f);
                spine.rotation = body * Quaternion.AngleAxis(lean + rise, right) * spineRest;
            }
            float since = Time.time - nodAt;
            float nod = since < .6f ? Mathf.Sin(since / .6f * Mathf.PI) * 14f : 0;
            head.rotation = look * Quaternion.AngleAxis(nod, right) * headRest;
        }
    }
}

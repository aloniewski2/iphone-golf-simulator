using System.Linq;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Two-handed driver finish and shaft clearance around the measured head envelope.
    /// Both arms solve to one shared grip. Feet, torso turn and impact stay authored.
    public sealed class GolfSwingClearance
    {
        readonly HeroGolfer hero;
        readonly Transform head, grip;
        readonly Vector3 centre;
        readonly float radius;
        readonly Vector3 driveReleaseShaft;
        readonly (GameObject node, Vector3 end)[] clubs;
        public GolfSwingClearance(HeroGolfer hero)
        {
            this.hero = hero; head = hero.Bones["Head"]; grip = hero.Bones["Club"];
            // Authored Drive shaft at F076 (2.50 s), in model space. Anchoring the
            // direction here prevents the changing source pose from flipping the blend.
            bool female = hero.Root.GetComponent<GolfArcade.Tennis.MatchHeroLook>().female;
            driveReleaseShaft = female ? new Vector3(-.177348f, .9043239f, .38826f)
                                      : new Vector3(-.1848222f, .8964227f, .4028237f);
            var body = hero.Part("Body"); var mesh = body.sharedMesh;
            int h = System.Array.FindIndex(body.bones, b => b == head);
            var vertices = mesh.vertices; var weights = mesh.boneWeights;
            var bounds = new Bounds(); bool first = true;
            for (int i = 0; i < vertices.Length; i++)
            {
                var w = weights[i];
                if (!(w.boneIndex0 == h && w.weight0 > .6f || w.boneIndex1 == h && w.weight1 > .6f)) continue;
                var p = mesh.bindposes[h].MultiplyPoint3x4(vertices[i]);
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
            centre = bounds.center;
            radius = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z) + .075f;
            clubs = hero.Root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r => r.name.StartsWith("CLUB_")).Select(r =>
            {
                int b = System.Array.FindIndex(r.bones, t => t == grip);
                Vector3 far = Vector3.zero;
                foreach (var v in r.sharedMesh.vertices)
                {
                    var p = r.sharedMesh.bindposes[b].MultiplyPoint3x4(v);
                    if (p.sqrMagnitude > far.sqrMagnitude) far = p;
                }
                return (r.gameObject, far);
            }).ToArray();
        }
        /// Continue the driver's release into a wrap behind the head. The authored
        /// impact is untouched; a smooth blend begins only after the arms extend through it.
        public void ApplyDriveFollowThrough(float clipTime)
        {
            float progress = Mathf.InverseLerp(2.50f, 3.02f, clipTime);
            if (progress <= 0) return;
            // Continue with momentum, then decelerate into the held finish.
            float travel = progress * (2 - progress);
            var club = clubs.FirstOrDefault(c => c.node.activeSelf);
            if (!club.node) return;
            var left = hero.Bones["LeftHand"]; var rightHand = hero.Bones["RightHand"];
            var leftUpper = hero.Bones["LeftUpperArm"]; var rightUpper = hero.Bones["RightUpperArm"];
            var across = (rightUpper.position - leftUpper.position).normalized;
            var up = hero.Root.transform.up;
            var forward = Vector3.ProjectOnPlane(head.forward, up).normalized;
            across = Vector3.ProjectOnPlane(across, forward).normalized;
            float scale = Mathf.Abs(hero.Root.transform.lossyScale.x);
            var headCentre = head.TransformPoint(centre);
            var originalGrip = grip.position; var originalRotation = grip.rotation;
            var shaft = grip.TransformVector(club.end).normalized;
            var finishShaft = (across - up * .65f - forward * .12f).normalized;
            // A spherical Bezier travels up and behind the head before the shaft
            // lowers. A shortest-rotation blend to the finish cuts through the torso.
            var releaseShaft = hero.Root.transform.TransformVector(driveReleaseShaft).normalized;
            var overheadShaft = (up * .9f + across * .35f - forward * .65f).normalized;
            var desiredShaft = SphericalArc(releaseShaft, overheadShaft, finishShaft, travel);
            var turn = Quaternion.FromToRotation(shaft, desiredShaft);

            // Carry the hands around the lead side at their radial distance from
            // the head, rather than translating them through the shoulder/chest.
            var fromHead = originalGrip - headCentre;
            var startRadial = Vector3.ProjectOnPlane(fromHead, up);
            var finishRadial = -across * (.24f * scale) - forward * ((radius + .035f) * scale);
            var radialDirection = SphericalArc(startRadial.normalized, -across, finishRadial.normalized, travel);
            float radialDistance = Mathf.Lerp(startRadial.magnitude, finishRadial.magnitude, travel);
            float height = Mathf.Lerp(Vector3.Dot(fromHead, up), .065f * scale, travel);
            var targetGrip = headCentre + radialDirection * radialDistance + up * height;
            var leftOffset = turn * (left.position - originalGrip);
            var rightOffset = turn * (rightHand.position - originalGrip);
            var leftRotation = turn * left.rotation; var rightRotation = turn * rightHand.rotation;
            float Reach(string side) => Vector3.Distance(hero.Bones[side + "UpperArm"].position, hero.Bones[side + "LowerArm"].position) + Vector3.Distance(hero.Bones[side + "LowerArm"].position, hero.Bones[side + "Hand"].position) - .002f * scale;
            float elbowBlend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.50f, 2.63f, clipTime));
            float FlexedReach(string side)
            {
                float a = Vector3.Distance(hero.Bones[side + "UpperArm"].position, hero.Bones[side + "LowerArm"].position);
                float b = Vector3.Distance(hero.Bones[side + "LowerArm"].position, hero.Bones[side + "Hand"].position);
                // Reserve enough reach for a bent elbow instead of locking the arm straight.
                float bent = Mathf.Sqrt(a*a + b*b + 2*a*b*Mathf.Cos(38*Mathf.Deg2Rad));
                return Mathf.Lerp(Reach(side), bent, elbowBlend);
            }
            float leftReach = FlexedReach("Left"), rightReach = FlexedReach("Right");
            // Keep one common grip inside both arm reach spheres.
            for (int i = 0; i < 8; i++)
            {
                var delta = targetGrip + leftOffset - leftUpper.position;
                if (delta.magnitude > leftReach) targetGrip -= delta.normalized * (delta.magnitude - leftReach);
                delta = targetGrip + rightOffset - rightUpper.position;
                if (delta.magnitude > rightReach) targetGrip -= delta.normalized * (delta.magnitude - rightReach);
            }
            SolveTo("Left", targetGrip + leftOffset, leftRotation, elbowBlend);
            SolveTo("Right", targetGrip + rightOffset, rightRotation, elbowBlend);
            grip.SetPositionAndRotation(targetGrip, turn * originalRotation);
        }

        static Vector3 SphericalArc(Vector3 start, Vector3 control, Vector3 end, float t)
        {
            return Vector3.Slerp(Vector3.Slerp(start, control, t), Vector3.Slerp(control, end, t), t).normalized;
        }

        public void Apply()
        {
            foreach (var club in clubs)
            {
                if (!club.node.activeSelf) continue;
                Vector3 shaft = grip.TransformVector(club.end), toHead = head.TransformPoint(centre) - grip.position;
                float length = toHead.magnitude, safe = radius * Mathf.Abs(head.lossyScale.x);
                if (length <= safe || length > shaft.magnitude + safe) return;
                float angle = Vector3.Angle(toHead, shaft), cone = Mathf.Asin(safe / length) * Mathf.Rad2Deg;
                const float feather = 12;
                if (angle >= cone + feather) return;
                // C1-continuous approach to the exclusion cone, with positive clearance inside it.
                float target = angle < cone - feather ? cone : angle + Mathf.Pow(cone + feather - angle, 2) / (4 * feather);
                var axis = Vector3.Cross(toHead, shaft);
                if (axis.sqrMagnitude < .00001f) axis = Vector3.Cross(toHead, head.forward);
                var turn = Quaternion.AngleAxis(target - angle, axis.normalized);
                Solve("Left", turn); Solve("Right", turn);
                grip.rotation = turn * grip.rotation;
                return;
            }
        }
        void Solve(string side, Quaternion turn)
        {
            var hand = hero.Bones[side + "Hand"];
            var target = grip.position + turn * (hand.position - grip.position);
            var rotation = turn * hand.rotation;
            SolveTo(side, target, rotation);
        }
        Vector3 BendAroundTorso(string side, Vector3 shoulder, Vector3 wrist, Vector3 elbowCentre,
            float elbowRadius, Vector3 wristAxis, Vector3 authoredBend, float blend)
        {
            var left = hero.Bones["LeftUpperArm"].position;
            var right = hero.Bones["RightUpperArm"].position;
            var shoulderMid = (left + right) * .5f;
            var pelvis = hero.Bones["Hips"].position;
            var up = (shoulderMid - pelvis).normalized;
            var across = Vector3.ProjectOnPlane(right - left, up).normalized;
            var front = Vector3.Cross(across, up).normalized;
            float scale = Mathf.Abs(hero.Root.transform.lossyScale.x);
            var torsoCentre = (pelvis + shoulderMid) * .5f;
            float width = Vector3.Distance(left, right) * .52f + .025f * scale;
            float depth = .155f * scale;
            float height = Vector3.Distance(pelvis, shoulderMid) * .5f + .075f * scale;
            var outward = side == "Left" ? -across : across;
            var preferred = Vector3.ProjectOnPlane(front + outward * .3f + up * .3f, wristAxis).normalized;
            if (preferred.sqrMagnitude < .1f) preferred = authoredBend;
            var reference = Vector3.Slerp(authoredBend, preferred, blend * .8f).normalized;

            float Penetration(Vector3 point)
            {
                var p = point - torsoCentre;
                float x = Vector3.Dot(p, across) / width;
                float y = Vector3.Dot(p, up) / height;
                float z = Vector3.Dot(p, front) / depth;
                float inside = Mathf.Max(0, 1 - x*x - y*y - z*z);
                return inside*inside;
            }
            float Cost(float degrees)
            {
                var direction = Quaternion.AngleAxis(degrees, wristAxis) * reference;
                var elbow = elbowCentre + direction * elbowRadius;
                float penetration = 0;
                // Both segments matter: an elbow outside the chest can still leave
                // the upper arm or forearm cutting through it.
                for (int i = 1; i <= 4; i++)
                {
                    penetration += Penetration(Vector3.Lerp(shoulder, elbow, i / 4f));
                    penetration += Penetration(Vector3.Lerp(elbow, wrist, i / 5f));
                }
                return penetration * 100 + (1-Vector3.Dot(direction, preferred))*.25f + degrees*degrees*.000006f;
            }
            float bestAngle = 0, bestCost = Cost(0);
            for (int angle = -170; angle <= 170; angle += 10)
            {
                float cost = Cost(angle);
                if (cost < bestCost) { bestCost = cost; bestAngle = angle; }
            }
            // Refine within the selected interval so elbows do not move in 10-degree steps.
            float lower = bestAngle-10, upper = bestAngle+10;
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.Lerp(lower, upper, 1f/3), b = Mathf.Lerp(lower, upper, 2f/3);
                if (Cost(a) < Cost(b)) upper = b; else lower = a;
            }
            var clearBend = Quaternion.AngleAxis((lower+upper)*.5f, wristAxis) * reference;
            return Vector3.Slerp(authoredBend, clearBend, blend).normalized;
        }

        void SolveTo(string side, Vector3 target, Quaternion rotation, float elbowBlend = 0)
        {
            var upper = hero.Bones[side + "UpperArm"]; var lower = hero.Bones[side + "LowerArm"]; var hand = hero.Bones[side + "Hand"];
            Vector3 root = upper.position, delta = target - root;
            float a = Vector3.Distance(root, lower.position), b = Vector3.Distance(lower.position, hand.position);
            float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(a-b)+.0001f, a+b-.0001f);
            var forward = delta.normalized;
            var bend = Vector3.ProjectOnPlane(lower.position-root, forward).normalized;
            if (bend.sqrMagnitude < .1f) bend = Vector3.ProjectOnPlane(upper.forward, forward).normalized;
            float along = (a*a-b*b+d*d)/(2*d);
            float elbowRadius = Mathf.Sqrt(Mathf.Max(0, a*a-along*along));
            var elbowCentre = root + forward*along;
            if (elbowBlend > 0 && elbowRadius > .0001f)
                bend = BendAroundTorso(side, root, target, elbowCentre, elbowRadius, forward, bend, elbowBlend);
            var elbow = elbowCentre + bend*elbowRadius;
            upper.rotation = Quaternion.FromToRotation(lower.position-root, elbow-root)*upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position-lower.position, target-lower.position)*lower.rotation;
            hand.rotation = rotation;
        }
    }
}

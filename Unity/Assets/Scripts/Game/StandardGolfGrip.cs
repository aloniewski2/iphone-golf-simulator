using System;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Golf-only constraint evaluated after the authored clip and before shared arm IK.
    /// Both fixed-grip hand meshes rotate around the same handle, then travel with the club.
    /// This is a hand-authored reference adaptation, not motion capture.
    public sealed class StandardGolfGrip : MonoBehaviour
    {
        Transform lead, trail, leadShoulder, trailShoulder, chest, handle, contact;
        public bool IsReady => lead && trail && handle && contact && chest && leadShoulder && trailShoulder;
        public float MaximumHandleError { get; private set; }

        public void Initialize()
        {
            var bones = GetComponentsInChildren<Transform>(true);
            Transform Find(string name) => Array.Find(bones, t => t.name == name);
            lead = Find("Hand.L"); trail = Find("Hand.R");
            leadShoulder = Find("UpperArm.L"); trailShoulder = Find("UpperArm.R");
            chest = Find("Chest"); contact = Find("StandardClubContact");
            handle = contact ? contact.parent : null;
        }

        public void Apply(float phase)
        {
            if (!IsReady) Initialize();
            if (!IsReady) return;
            float scale = Mathf.Abs(transform.lossyScale.x);
            // The contact marker includes a small club-head offset. Use the actual shaft axis.
            Vector3 local = contact.localPosition;
            Vector3 shaft = Mathf.Abs(local.z) >= Mathf.Abs(local.y) && Mathf.Abs(local.z) >= Mathf.Abs(local.x)
                ? new Vector3(0, 0, local.z) : Mathf.Abs(local.y) >= Mathf.Abs(local.x)
                    ? new Vector3(0, local.y, 0) : new Vector3(local.x, 0, 0);
            Vector3 axis = handle.TransformDirection(shaft).normalized;
            Vector3 towardBody = Vector3.ProjectOnPlane(chest.position - handle.position, axis).normalized;
            if (towardBody.sqrMagnitude < .5f) return;
            // Wrists approach the handle from the body side, not diametrically opposite sides.
            // Rotate the whole fixed hand around its shaft contact: fingers stay curled on it.
            Wrap(lead, axis, towardBody, 25f);
            Wrap(trail, axis, towardBody, -25f);
            Vector3 beforeLead = handle.InverseTransformPoint(lead.position);
            Vector3 beforeTrail = handle.InverseTransformPoint(trail.position);
            float fold = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.84f, .95f, phase));
            float reach = Mathf.Lerp(.530f * scale, Vector3.Distance(lead.position, leadShoulder.position), fold);
            Vector3 offset = leadShoulder.position + (lead.position - leadShoulder.position).normalized * reach - lead.position;
            float maximum = (StandardCharacterArms.UpperLengthMetres + StandardCharacterArms.ForearmLengthMetres - .006f) * scale;
            // Project the shared grip, never the individual hands, into both reach spheres.
            for (int i = 0; i < 24; i++)
            {
                offset = Limit(offset, lead.position, leadShoulder.position, maximum);
                offset = Limit(offset, trail.position, trailShoulder.position, maximum);
            }
            // At address/contact, pivot the whole grip around the club head instead of pushing
            // the head underground and compensating by lifting the character off the course.
            float contactWeight = Mathf.Max(1 - Mathf.SmoothStep(0, 1, phase / .22f),
                1 - Mathf.SmoothStep(0, 1, Mathf.Abs(phase - .8f) / .09f));
            Vector3 pivot = contact.position;
            Quaternion rotation = Quaternion.Slerp(Quaternion.identity,
                Quaternion.FromToRotation(handle.position - pivot, handle.position + offset - pivot), contactWeight);
            lead.SetPositionAndRotation(pivot + rotation * (lead.position - pivot), rotation * lead.rotation);
            trail.SetPositionAndRotation(pivot + rotation * (trail.position - pivot), rotation * trail.rotation);
            handle.SetPositionAndRotation(pivot + rotation * (handle.position - pivot), rotation * handle.rotation);
            offset *= 1 - contactWeight;
            lead.position += offset; trail.position += offset; handle.position += offset;
            MaximumHandleError = Mathf.Max((handle.InverseTransformPoint(lead.position) - beforeLead).magnitude,
                (handle.InverseTransformPoint(trail.position) - beforeTrail).magnitude);
        }

        void Wrap(Transform hand, Vector3 axis, Vector3 towardBody, float fan)
        {
            Vector3 relative = hand.position - handle.position;
            Vector3 axial = Vector3.Project(relative, axis);
            Vector3 radial = relative - axial;
            Vector3 desired = Quaternion.AngleAxis(fan, axis) * towardBody;
            Quaternion turn = Quaternion.AngleAxis(Vector3.SignedAngle(radial, desired, axis), axis);
            hand.SetPositionAndRotation(handle.position + axial + turn * radial, turn * hand.rotation);
        }

        static Vector3 Limit(Vector3 offset, Vector3 wrist, Vector3 shoulder, float maximum)
        {
            Vector3 delta = wrist + offset - shoulder;
            return delta.magnitude > maximum ? offset - delta.normalized * (delta.magnitude - maximum) : offset;
        }
    }
}

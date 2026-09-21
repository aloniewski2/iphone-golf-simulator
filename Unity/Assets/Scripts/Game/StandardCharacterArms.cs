using System;
using UnityEngine;

namespace GolfArcade.Game
{
    /// Shared visual correction for every permanent standard-character rig, independent of sport.
    /// Evaluate after animation: preserve authored hands/grips, solve longer arms, and draw one
    /// continuous tapered surface per arm instead of the source's disconnected rigid cylinders.
    [DefaultExecutionOrder(10000)]
    public sealed class StandardCharacterArms : MonoBehaviour
    {
        public const float UpperLengthMetres = .285f;
        public const float ForearmLengthMetres = .265f;
        public bool ManualEvaluation { get; set; }
        public bool IsReady => arms != null && arms.Length == 2;
        public float MaximumGripError { get; private set; }
        public float MaximumSegmentError { get; private set; }
        Arm[] arms;
        Renderer[] floatingHandsHidden;
        bool[] previousVisibility;
        public bool FloatingHandsPreview { get; private set; }

        /// Reversible visual experiment: leave all bones, hands and equipment active.
        public void SetFloatingHandsPreview(bool enabled)
        {
            Initialize();
            if (enabled == FloatingHandsPreview) return;
            if (enabled)
            {
                floatingHandsHidden = Array.FindAll(GetComponentsInChildren<Renderer>(true), r =>
                    r.name.StartsWith("Standard continuous arm ") || r.name.StartsWith("Shoulder fabric ") ||
                    r.name.StartsWith("Short sleeve ") || r.name.StartsWith("Sleeve piping "));
                previousVisibility = Array.ConvertAll(floatingHandsHidden, r => r.enabled);
                foreach (var renderer in floatingHandsHidden) renderer.enabled = false;
            }
            else if (floatingHandsHidden != null)
                for (int i = 0; i < floatingHandsHidden.Length; i++)
                    if (floatingHandsHidden[i]) floatingHandsHidden[i].enabled = previousVisibility[i];
            FloatingHandsPreview = enabled;
        }
        const int Sides = 16, Rings = 17;

        sealed class Arm
        {
            public Transform upper, lower, hand, chest;
            public Mesh mesh;
            public Vector3[] vertices = new Vector3[Rings * Sides + 2];
            public Vector3[] centers = new Vector3[Rings];
            public Vector3 restPole;
        }

        void Awake() => Initialize();

        public void Initialize()
        {
            if (arms != null) return;
            var bones = GetComponentsInChildren<Transform>(true);
            Transform Find(string name) => Array.Find(bones, t => t.name == name);
            var built = new Arm[2];
            for (int index = 0; index < 2; index++)
            {
                string side = index == 0 ? "L" : "R";
                var arm = new Arm { upper = Find("UpperArm." + side), lower = Find("LowerArm." + side), hand = Find("Hand." + side), chest = Find("Chest") };
                if (!arm.upper || !arm.lower || !arm.hand || !arm.chest) return;
                // Pole in chest-local space gives stable outward/downward elbows, not wrist twist.
                // Chest bone local up follows the skeleton; derive directions from the bind pose.
                Vector3 down = (arm.lower.position - arm.upper.position).normalized;
                Vector3 outward = (arm.upper.position - arm.chest.position).normalized;
                arm.restPole = arm.chest.InverseTransformDirection((down + outward * .35f).normalized);
                built[index] = arm;
            }
            arms = built;
            for (int index = 0; index < 2; index++)
            {
                string side = index == 0 ? "L" : "R";
                Material skin = null;
                foreach (var renderer in GetComponentsInChildren<Renderer>(true))
                {
                    string n = renderer.name;
                    if (n.StartsWith("Upper arm " + side) || n.StartsWith("Forearm " + side) || n.StartsWith("Elbow " + side))
                    {
                        skin = renderer.sharedMaterial;
                        renderer.enabled = false;
                    }
                }
                var arm = arms[index];
                var go = new GameObject("Standard continuous arm " + side);
                go.transform.SetParent(transform, false);
                arm.mesh = new Mesh { name = "Standard arm surface " + side };
                arm.mesh.MarkDynamic();
                var triangles = new int[(Rings - 1) * Sides * 6 + Sides * 6];
                int k = 0;
                for (int r = 0; r < Rings - 1; r++)
                    for (int j = 0; j < Sides; j++)
                    {
                        int a = r * Sides + j, b = r * Sides + (j + 1) % Sides, c = a + Sides, d = b + Sides;
                        triangles[k++] = a; triangles[k++] = b; triangles[k++] = c;
                        triangles[k++] = b; triangles[k++] = d; triangles[k++] = c;
                    }
                for (int j = 0; j < Sides; j++)
                {
                    triangles[k++] = Rings * Sides; triangles[k++] = (j + 1) % Sides; triangles[k++] = j;
                    triangles[k++] = Rings * Sides + 1; triangles[k++] = (Rings - 1) * Sides + j; triangles[k++] = (Rings - 1) * Sides + (j + 1) % Sides;
                }
                arm.mesh.vertices = arm.vertices;
                arm.mesh.triangles = triangles;
                go.AddComponent<MeshFilter>().sharedMesh = arm.mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = skin;
            }
            // Permanent multi-sport visual standard. Retain the hidden rig for attachments.
            SetFloatingHandsPreview(true);
        }

        void LateUpdate() { if (!ManualEvaluation) ApplyAfterAnimation(); }

        public void ApplyAfterAnimation()
        {
            Initialize();
            if (!IsReady) return;
            MaximumGripError = MaximumSegmentError = 0;
            float scale = Mathf.Abs(transform.lossyScale.x);
            foreach (var arm in arms)
            {
                Vector3 shoulder = arm.upper.position, wrist = arm.hand.position;
                Quaternion wristRotation = arm.hand.rotation;
                Vector3 oldUpper = arm.lower.position - shoulder, oldLower = wrist - arm.lower.position;
                Quaternion upperRotation = arm.upper.rotation, lowerRotation = arm.lower.rotation;
                float upper = UpperLengthMetres * scale, lower = ForearmLengthMetres * scale;
                Vector3 pole = arm.chest.TransformDirection(arm.restPole);
                Vector3 elbow = SolveElbow(shoulder, wrist, pole, upper, lower);
                arm.upper.rotation = Quaternion.FromToRotation(oldUpper, elbow - shoulder) * upperRotation;
                arm.lower.position = elbow;
                arm.lower.rotation = Quaternion.FromToRotation(oldLower, wrist - elbow) * lowerRotation;
                arm.hand.SetPositionAndRotation(wrist, wristRotation);
                MaximumGripError = Mathf.Max(MaximumGripError, Vector3.Distance(wrist, arm.hand.position));
                MaximumSegmentError = Mathf.Max(MaximumSegmentError, Mathf.Abs(Vector3.Distance(shoulder, elbow) - upper), Mathf.Abs(Vector3.Distance(elbow, wrist) - lower));
                if (!FloatingHandsPreview)
                    UpdateSurface(arm, transform.InverseTransformPoint(shoulder), transform.InverseTransformPoint(elbow), transform.InverseTransformPoint(wrist));
            }
        }

        public static Vector3 SolveElbow(Vector3 shoulder, Vector3 wrist, Vector3 pole, float upper, float lower)
        {
            Vector3 delta = wrist - shoulder;
            float actual = delta.magnitude;
            Vector3 axis = actual > .00001f ? delta / actual : Vector3.down;
            // Maintain the grip even for out-of-range legacy poses; extend both segments equally.
            if (actual > upper + lower - .0001f)
            {
                float extension = (actual + .0001f) / (upper + lower);
                upper *= extension; lower *= extension;
            }
            float distance = Mathf.Max(actual, .0001f);
            float along = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            along = Mathf.Clamp(along, -upper, upper);
            Vector3 bend = Vector3.ProjectOnPlane(pole, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.Cross(axis, Mathf.Abs(axis.y) < .9f ? Vector3.up : Vector3.right);
            return shoulder + axis * along + bend.normalized * Mathf.Sqrt(Mathf.Max(0, upper * upper - along * along));
        }

        static void UpdateSurface(Arm arm, Vector3 shoulder, Vector3 elbow, Vector3 wrist)
        {
            Vector3 enter = Vector3.Lerp(shoulder, elbow, .82f), leave = Vector3.Lerp(elbow, wrist, .18f);
            for (int i = 0; i < Rings; i++)
            {
                if (i <= 6) arm.centers[i] = Vector3.Lerp(shoulder, enter, i / 6f);
                else if (i <= 10)
                {
                    float t = (i - 6) / 4f;
                    arm.centers[i] = (1 - t) * (1 - t) * enter + 2 * t * (1 - t) * elbow + t * t * leave;
                }
                else arm.centers[i] = Vector3.Lerp(leave, wrist, (i - 10) / 6f);
            }
            Vector3 previousNormal = Vector3.zero;
            for (int i = 0; i < Rings; i++)
            {
                Vector3 tangent = (arm.centers[Mathf.Min(Rings - 1, i + 1)] - arm.centers[Mathf.Max(0, i - 1)]).normalized;
                Vector3 normal = i == 0 ? Vector3.Cross(tangent, Mathf.Abs(tangent.z) < .9f ? Vector3.forward : Vector3.right).normalized : Vector3.ProjectOnPlane(previousNormal, tangent).normalized;
                if (normal.sqrMagnitude < .5f) normal = Vector3.Cross(tangent, Vector3.right).normalized;
                Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
                previousNormal = normal;
                float radius = i <= 8 ? Mathf.Lerp(.058f, .041f, i / 8f) : Mathf.Lerp(.041f, .030f, (i - 8) / 8f);
                for (int j = 0; j < Sides; j++)
                {
                    float angle = j * Mathf.PI * 2 / Sides;
                    arm.vertices[i * Sides + j] = arm.centers[i] + radius * (normal * Mathf.Cos(angle) + binormal * Mathf.Sin(angle));
                }
            }
            arm.vertices[Rings * Sides] = shoulder;
            arm.vertices[Rings * Sides + 1] = wrist;
            arm.mesh.vertices = arm.vertices;
            arm.mesh.RecalculateNormals();
            arm.mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (arms == null) return;
            foreach (var arm in arms)
                if (arm.mesh)
                {
                    if (Application.isPlaying) Destroy(arm.mesh); else DestroyImmediate(arm.mesh);
                }
        }
    }
}

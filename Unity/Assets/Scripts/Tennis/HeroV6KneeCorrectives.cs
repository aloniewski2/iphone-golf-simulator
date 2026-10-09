using System;
using UnityEngine;

namespace GolfArcade.Tennis
{
    /// Standard pose-space morphs authored in Blender; retains the shared Humanoid and gameplay clips.
    [DefaultExecutionOrder(1500)]
    public sealed class HeroV6KneeCorrectives : MonoBehaviour
    {
        public bool applyCorrectives = true;
        public bool HasCorrectives => indices[0] >= 0 && indices[1] >= 0 && indices[2] >= 0 && indices[3] >= 0;
        public float LeftBend { get; private set; }
        public float RightBend { get; private set; }
        Animator animator;
        Transform leftUpper, leftKnee, leftFoot, rightUpper, rightKnee, rightFoot;
        SkinnedMeshRenderer skin;
        Mesh boundMesh;
        readonly int[] indices = { -1, -1, -1, -1 };

        void Start() => Bind();
        void Bind()
        {
            var look = GetComponent<ModularHeroLook>();
            animator = look ? look.animator : GetComponentInChildren<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman) return;
            leftUpper = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
            leftKnee = animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightUpper = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
            rightKnee = animator.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name == "Body_Skin") { skin = r; break; }
            BindMesh();
        }
        void BindMesh()
        {
            if (!skin || !skin.sharedMesh) return;
            boundMesh = skin.sharedMesh;
            for (int k = 0; k < indices.Length; k++) indices[k] = -1;
            string[] names = { "Knee_L_60", "Knee_L_110", "Knee_R_60", "Knee_R_110" };
            for (int i = 0; i < boundMesh.blendShapeCount; i++)
                for (int k = 0; k < names.Length; k++)
                    if (boundMesh.GetBlendShapeName(i).EndsWith(names[k], StringComparison.Ordinal)) indices[k] = i;
        }
        static float Bend(Transform upper, Transform knee, Transform foot) =>
            upper && knee && foot ? 180f - Vector3.Angle(upper.position - knee.position, foot.position - knee.position) : 0;
        void LateUpdate() => Apply();
        public void Apply()
        {
            if (!skin || !leftKnee) Bind();
            if (!skin || !leftKnee) return;
            if (skin.sharedMesh != boundMesh) BindMesh();
            LeftBend = Bend(leftUpper, leftKnee, leftFoot);
            RightBend = Bend(rightUpper, rightKnee, rightFoot);
            Set(0, applyCorrectives ? LeftBend : 0);
            Set(2, applyCorrectives ? RightBend : 0);
        }
        void Set(int offset, float angle)
        {
            float low = angle <= 60 ? Mathf.Clamp01(angle / 60) : Mathf.Clamp01((110 - angle) / 50);
            float high = Mathf.Clamp01((angle - 60) / 50);
            if (indices[offset] >= 0) skin.SetBlendShapeWeight(indices[offset], low * 100);
            if (indices[offset + 1] >= 0) skin.SetBlendShapeWeight(indices[offset + 1], high * 100);
        }
    }
}

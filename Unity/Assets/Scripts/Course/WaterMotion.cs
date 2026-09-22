using UnityEngine;

namespace GolfArcade.Course
{
    /// The sea moving, the way the Blender scene has it: WATER_WAVES carries four morph targets a
    /// quarter wave apart (Unity blendshapes, from Codex's animated Hole 12 via
    /// hole12_animate_finish.py and hole12_prepare.py). Cross-fading them one after another makes
    /// the swell travel, and the loop closes every Period seconds; WATER_GLINTS, the sheet of
    /// highlight cards, drifts with it and back. The shapes are already calm along every shore.
    public sealed class WaterMotion : MonoBehaviour
    {
        public float Period = 8f;

        SkinnedMeshRenderer waves;
        Transform glints;
        Vector3 glintsHome;
        int shapes;

        /// Attach to a course model that has the animated water; null when it has none.
        public static WaterMotion Attach(Transform waves, Transform glints)
        {
            if (!waves) return null;
            // The importer gives an unskinned mesh a MeshRenderer even when it has blendshapes;
            // only a SkinnedMeshRenderer can weight them, so the grid moves over to one.
            if (!waves.TryGetComponent(out SkinnedMeshRenderer smr) && waves.TryGetComponent(out MeshFilter mf) && mf.sharedMesh && mf.sharedMesh.blendShapeCount > 0)
            {
                var mr = waves.GetComponent<MeshRenderer>();
                var mesh = mf.sharedMesh;
                var mats = mr ? mr.sharedMaterials : System.Array.Empty<Material>();
                if (mr) Destroy(mr);
                Destroy(mf);
                smr = waves.gameObject.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.sharedMaterials = mats;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                smr.receiveShadows = false;
            }
            if (!smr || !smr.sharedMesh || smr.sharedMesh.blendShapeCount == 0) return null;
            var motion = waves.gameObject.AddComponent<WaterMotion>();
            motion.waves = smr;
            motion.shapes = smr.sharedMesh.blendShapeCount;
            smr.updateWhenOffscreen = true; // the grid's bounds are its flat basis; a crest must not be culled
            if (glints) { motion.glints = glints; motion.glintsHome = glints.localPosition; }
            return motion;
        }

        void Update()
        {
            float phase = Time.time / Period * shapes;              // one shape per quarter of the loop
            for (int k = 0; k < shapes; k++)
            {
                float d = Mathf.Abs(Mathf.Repeat(phase - k + shapes / 2f, shapes) - shapes / 2f); // circular distance to shape k
                waves.SetBlendShapeWeight(k, 100f * Mathf.Clamp01(1f - d));
            }
            if (glints) glints.localPosition = glintsHome + new Vector3(1.8f, 0, 1f) * Mathf.PingPong(Time.time / Period * 2f, 1f);
        }
    }
}

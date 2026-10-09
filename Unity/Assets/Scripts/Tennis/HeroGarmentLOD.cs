using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace GolfArcade.Tennis
{
    /// Preserve complete tailoring in the locker/intros; use the measured 1.5mm-envelope
    /// garment mesh when the whole athlete occupies less than 24% of the screen height.
    /// Materials, UVs, semantic roles and animation bones are shared; the body/face never change.
    [DefaultExecutionOrder(900), DisallowMultipleComponent]
    public sealed class HeroGarmentLOD : MonoBehaviour
    {
        public static bool ForceFullDetail;
#if UNITY_EDITOR
        // Inspect the actual match mesh at a close proof camera without
        // changing the screen-height thresholds used by the player build.
        public static bool ForceMatchDetailForReview;
#endif
        public const float MatchScreenHeight = .24f;
        public MatchHeroLook look;
        sealed class Piece
        {
            public SkinnedMeshRenderer renderer;
            public Mesh full, match;
            public Transform[] fullBones, matchBones;
        }
        readonly List<Piece> pieces = new();
        readonly List<Mesh> owned = new();
        void OnDestroy() { foreach (var mesh in owned) if (mesh) Destroy(mesh); }
        bool initialized, matchActive;
        public bool UsingMatchDetail => matchActive;
        public int FullTriangles { get; private set; }
        public int MatchTriangles { get; private set; }

        void OnDisable() => Select(false);
        void LateUpdate() => Prepare(Camera.main);
        /// Call before the frame's animation/skinning evaluation for proof cameras.
        /// Mesh palettes never change from a beginCameraRendering callback.
        public void Prepare(Camera camera)
        {
            if (!camera || !isActiveAndEnabled || !Initialize()) { Select(false); return; }
            var bounds = look.body ? look.body.bounds : pieces[0].renderer.bounds;
            float relativeHeight = camera.orthographic ? bounds.size.y / (2 * camera.orthographicSize) :
                bounds.size.y / (2 * Mathf.Max(.1f, Vector3.Distance(camera.transform.position, bounds.center)) * Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad));
            bool match = !ForceFullDetail && relativeHeight < MatchScreenHeight + (matchActive ? .025f : -.025f);
#if UNITY_EDITOR
            if (ForceMatchDetailForReview && !ForceFullDetail) match = true;
#endif
            Select(match);
        }
        bool Initialize()
        {
            if (initialized) return pieces.Count > 0;
            if (!look) look = GetComponent<MatchHeroLook>();
            if (!look || look.golfKit || look.kit == null) return false;
            var source = Resources.Load<GameObject>("Tennis/KitsLOD/" + (look.female ? "Female" : "Male"));
            if (!source) return false;
            var byName = look.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
            foreach (var low in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var live = look.kit.FirstOrDefault(r => r && r.name == low.name);
                if (!live || !live.sharedMesh || !low.sharedMesh) continue;
                bool tailored = live.sharedMesh.name.StartsWith("TailoredPolo");
                bool skirt = live.sharedMesh.name.StartsWith("TailoredSkirt");
                var selected = low;
                if (skirt) selected = Resources.Load<GameObject>("Tennis/KitsTailored/FemaleSkirtDistance").GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r => r.name == "Kit_Bottom");
                if (tailored)
                {
                    var asset = Resources.Load<GameObject>("Tennis/KitsTailored/" + (look.female ? "Female" : "Male") + "Distance");
                    selected = asset ? asset.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == "Kit_Top") : null;
                    if (!selected) throw new System.InvalidOperationException("Tailored polo distance mesh missing");
                }
                if (selected.bones.Any(b => !b || !byName.ContainsKey(b.name))) continue;
                var matchBones = selected.bones.Select(b => byName[b.name]).ToArray();
                Mesh matchMesh;
                if (tailored)
                {
                    matchMesh = Instantiate(selected.sharedMesh); matchMesh.name = "TailoredPolo32 "+(look.female?"Female":"Male")+" distance";
                    matchMesh.hideFlags = HideFlags.DontSave; owned.Add(matchMesh);
                    matchMesh = TailoredPoloDeformation.Prepare(look, live, matchMesh, matchBones);
                }
                else if (skirt) matchMesh = selected.sharedMesh;
                else matchMesh = HeroGarmentHemWeights.Prepare(look, live, selected.sharedMesh, matchBones);
                pieces.Add(new Piece { renderer = live, full = HeroGarmentGusset.CanonicalSource(live.sharedMesh), match = HeroGarmentGusset.CanonicalSource(matchMesh),
                    fullBones = live.bones, matchBones = matchBones });
                FullTriangles += CountTriangles(HeroGarmentGusset.CanonicalSource(live.sharedMesh)); MatchTriangles += CountTriangles(matchMesh);
            }
            initialized = true;
            return pieces.Count > 0;
        }
        static int CountTriangles(Mesh m)
        {
            int count = 0; for (int i = 0; i < m.subMeshCount; i++) count += (int)m.GetIndexCount(i) / 3; return count;
        }
        void Select(bool match)
        {
            if (matchActive == match) return;
            foreach (var p in pieces) if (p.renderer)
            {
                p.renderer.sharedMesh = match ? p.match : p.full;
                p.renderer.bones = match ? p.matchBones : p.fullBones;
            }
            matchActive = match;
        }
    }
}

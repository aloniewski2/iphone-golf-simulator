# Unity integration handoff

Update: the first golf slice is now integrated and tested. See [golf runtime integration](GOLF-RUNTIME-INTEGRATION.md); the checklist below records the original broader source-library handoff and still applies to unintegrated assets.

Target: **Unity 6000.3.24f1**, Built-in render pipeline, iOS. Project: `../Unity`. Keep the pinned version; do not upgrade the project while importing this library.

## Readiness

| Asset | Current state | Required before shipping |
|---|---|---|
| Standard male/female | Identity fixed; V4 rigged Blender prototypes | Single-character FBX export, Avatar setup, deformation and scale validation |
| 236 animation variants | Named Blender actions/NLA clips, 59 families | Export, Unity clip import, blending, contacts, event timing, gameplay tests |
| 16 clothing kits | Separate skinned geometry in masters | Extreme-pose fit, UV/material work, skinning/LOD validation |
| 16 equipment variants | FBX and GLB exports; Blender round-trip checked | Built-in materials, attachments, collider/physics and triangle-budget review |
| Tennis/bowling/boxing arenas | Editable Blender scenes; bowling/boxing also have FBX | Material baking, optimization, colliders, Unity validation; tennis export |
| Crowds | Standard-derived static posed geometry | Crowd batching/instancing, LODs, culling; animated crowd system if wanted |
| Preview videos | 10/60 fps, plus 120 fps tennis | Visual references only; not device benchmarks |

There are **no V4 character/animation FBX exports or production Animator controllers** in this commit. No prefab or scene migration has been performed. Do not treat the studio's ball trajectories as gameplay physics or its hard cuts between studies as completed state transitions.

## Integration sequence

1. Read the [character standard](CHARACTER-STANDARD.md). Start with one standard tennis character, one kit, racket, and ready/run/forehand clips in an isolated Unity review scene.
2. Export at consistent meter scale with clean root transforms, no extra turnaround geometry, and no unrelated studio cameras/lights. Preserve a neutral rest pose and stable names. Export each clip's actual range from the manifest; do not bake the whole concatenated review timeline as one action. Preserve skinned garment bindings and avoid conflicting action/NLA export settings.
3. Validate the custom rig as Generic first if no retargeting is needed. If adopting Humanoid, explicitly validate Avatar bone mapping/rest pose for both standards; automatic mapping is not assumed valid. V4 has no individually rigged finger articulation.
4. Recreate simple color materials for the Built-in pipeline. Bake procedural venue textures when needed; many new garments do not have production UVs. Do not assume Blender material parity.
5. Validate root-motion policy. Phone-driven tennis locomotion generally needs in-place cycles and runtime displacement; do not accidentally apply both clip movement and input movement. Keep bowling approach, ball retention/release, and golf swing phases separable.
6. Build character prefabs with modular kits and explicit equipment attachment transforms. Add blending, deliberate off-hand poses, sport-specific IK/contact refinement, and event-driven ball release/contact. Preserve RH/LH behavior independently of character identity.
7. Only after the sample passes, bring in both characters, all kits and actions, arenas, and standard-derived crowds. Replace the procedural runtime golfer with the validated standard while preserving existing swing/shot behavior.
8. Run existing repository tests, play-mode visual checks, and an iPhone build. Benchmark the real scene at 60 fps and evaluate 120 fps only on supported devices; include input latency and thermal stability.

## Current control and camera contract

- Tennis: player/phone movement drives lateral movement; swing gesture drives racket. Close third-person camera.
- Bowling: player movement drives approach; holding a button retains the ball and releasing it triggers release. Behind-player third person.
- Golf: phone controls the swing. Third person.
- Boxing: phone movement and punch gestures, with buttons to select punch types. **First person**. One-phone/two-fist interpretation still needs design and implementation.
- UI, rules, networking, and additional customization systems are separate tasks, not implemented by this library.

## Known limits and acceptance criteria

The V4 structural report checks sampled geometry and weights, not all-frame visual fidelity. Motion is hand-authored/parameterized, not motion capture. High frame rate interpolates existing curves; it does not repair awkward posing, sparse authored detail, cloth intersections, or fixed hand grips. Dives, foot locking, natural timing, finger contacts, and extreme-pose clothing still need animator review.

Bowling and boxing FBXs predate crowd additions. Their exports must not be labeled as crowd-inclusive. First-person boxing preview and older external review cameras are distinct; historical external-ring concepts are not the current gameplay camera specification. Golf currently has an animation stage, not a finished standalone resort course.

Accept an import only after checking scale/orientation, both standards, each handedness, no exploded vertices, material slots, clothing seams, equipment grip, contact/release timing, correct clip duration, locomotion direction and blending. Unity/device testing remains pending in this asset-packaging commit.

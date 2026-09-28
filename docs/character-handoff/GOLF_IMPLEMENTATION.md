# Golf implementation sequence

## 0. Baseline and receiving project inventory

Work in an isolated receiving-project branch. Record Unity version, render pipeline, world units, player controller, shot authority, animation system and input API. If it is not Unity, port the data/rig contracts rather than Unity scripts. Capture the current golf swing and target Hero from front/side/rear before changing either. The audited source project is Unity 6000.3.24f1 with URP; do not upgrade as part of character work.

## 1. Preserve golf’s existing control contract

`Unity/Assets/Scripts/Game/GolferView.cs` currently:

- Loads `StandardGolfDrive` from the selected standard golfer FBX.
- Uses manual PlayableGraph evaluation, no root motion, always-animate culling.
- Scrubs backswing from phone load via `ShowLoad`.
- Uses top=1.77 s, impact=2.4 s, end=3.0 s. These are **legacy clip-specific timestamps**, not universal golf timing.
- Returns impact delay from the swing trigger; shot logic must stay aligned with visual impact.
- Converts FBX metres to course yards using 1.0936 and rotates model -90 degrees around Y.
- Aligns `StandardClubContact` to the expected ball position by sampling impact, then returns to address.
- Applies `StandardGolfGrip` and `StandardCharacterArms` after animation. `FloatingHandsPreview` defaults true: explicitly review/disable the legacy preview mode when shipping a full-body Hero.

Read the rest of `GolferView` and its call sites before swapping visuals. The receiving game may have different units or authority; adapt the contract rather than blindly copying the 1.0936 scale or -90-degree rotation.

## 2. Retarget one golf drive to the Hero

Use the current Hero bind skeleton and default wardrobe. Keep tennis prefab/FBXs untouched. The older standard golf skeleton is Generic; its drive does not automatically become usable as a Hero Humanoid clip.

1. Extract the source drive action from the golf master/runtime export and verify rest orientation, limb lengths, club/hand relationship and actual contact frame.
2. Retarget/bake in Blender or an isolated, explicit Humanoid conversion; correct rest-pose offsets and preserve hip/shoulder sequencing. Do not change the global legacy importer.
3. Export a separately named Hero golf clip and scoped Humanoid import; confirm Avatar valid/isHuman, one armature, consistent scale, in-place XZ, intentional vertical movement, no extra animated duplicate root.
4. Sample complete clip in Unity, not just three favorable poses. Short limbs need explicit elbow and wrist review.
5. Record measured address/top/contact/finish timestamps in a golf clip manifest. Align the shot to the new contact time. Do not retain 2.4 s if the new clip’s contact differs.

Existing source library: `SportsLibrary/Animations/V4/Golf/actions.json` and README document 24 variants across Chip, Drive, HalfSwing, IronSwing, Putt, SwingAbort. These are authored Blender actions, not proof of 24 runtime-ready Hero clips. Start with Drive; then retarget Putt and Chip. The legacy master is `SportsLibrary/Blender/sports-animation-studio-v4.blend`; `blender/scripts/export_standard_golf.py` exports its drive without saving changes to that master. See `SportsLibrary/GOLF-RUNTIME-INTEGRATION.md` and source manifests.

## 3. Golf-specific motion/equipment adapter

Proposed new component (not implemented): `HeroGolfDriver` with address, load/scrub, unwind/abort, downswing, impact, finish and recovery. Use PlayableGraph to match existing project conventions. Keep gameplay position/ball velocity in the game controller.

Separate shared assembly/tints from `HeroTennisDriver`; do not attach a driver expecting TennisActor just to animate golf. `ModularHeroLook` currently includes tennis grip/stroke fields, so extract only the shared responsibilities deliberately.

Equipment contract:

- Club attached to the lead-hand socket with an authored local grip frame.
- Second hand tracks an upper/lower grip target according to golf handedness; fingers visibly wrap, wrists remain natural.
- Explicit markers: grip centre, clubhead contact, face normal, shaft axis. Ball launch/contact originates from the authoritative contact marker.
- Preserve body unit scale. Compensate socket bone scale as needed; do not stretch a whole armature to fit a club.
- Apply animation first, then constrained hand/club corrections, then contact measurement. One owner for each correction; avoid two scripts fighting over wrists.
- Different clubs use explicit grip/contact metadata, not hardcoded tennis racket coordinates.
- Recheck RH and LH independently. Mirroring preview geometry alone does not implement left-handed clubface/grip/shot logic.

## 4. Appearance unification

Proposed serializable `CharacterAppearance` versioned schema:

`version`, `bodyVariantId`, `bodySize01`, `skinColor`, `haircutId`, `hairColor`, `headwearId`, `shirtId`, `shirtColor`, `shortsId`, `shortsColor`, `shoesId`, `shoesColor`, `equipmentSkinId`, `handedness`.

These names are a proposal, not existing APIs. Write a compatibility adapter from current Player/native JSON. Do not discard old saves or reinterpret `hairStyle` (currently headwear) as haircut. Unknown IDs fall back deterministically. Keep colors in one documented color space.

Use stable mesh IDs and a shared catalog with supported body sizes, headwear fits and skeleton version. Implement size using coordinated body AND garment blend shapes with unchanged gameplay root/hitbox/reach; never a root-scale slider. Test slim/mid/broad plus intermediate values. Update native payload, Unity parser, both sport adapters and locker export together. UI should expose only implemented options.

For boy/girl, keep one skeleton and apply corresponding garment shapes with the body. Hair/headwear fit should follow the actual head shape and a stable head socket. Preserve coverage at neck, cuffs and ankles; document body regions intentionally hidden under outfits.

## 5. Character repairs in a controlled order

1. Head/scalp coverage and hair roots; use clean topology/normals and correct weights. Verify hat-off, not only visor-on.
2. Hat/hair combination fitting; readable opaque clumps and safe brim/eyebrow clearance.
3. Shoulder/knee/wrist volume across golf swing extrema. Wider weight falloffs or targeted corrective shapes, no wholesale remesh.
4. Clothes/body-size correspondence, then new swappable garments.
5. Skin/eye/material parity and native preview parity. Match existing target design; no photoreal style shift.

Do not gate progress on an arbitrary “80% match” claim. Provide identical framing before/after and measured grip/contact/clearance errors.

## 6. Export and review

Keep `.blend` authoring sources, FBXs, textures, shape metadata and Unity `.meta` files. `HeroV5Rebind.Run` is a useful reference for bone-order-correct rebind. `HeroLockerExport.Run` exports the current tennis preview; adapt it for golf display poses/club only after defining shared appearance data. Its baked SceneKit output is not a runtime skeletal animation package.

Deliver default and extreme customization captures, a full swing video, manifest with contact timestamps and bone mapping, prefab dependencies, and known failures. Verify a fresh checkout with LFS assets. Then integrate the next golf motion family.

## 7. Apply the same character to other sports

| Sport | Shared | Sport-specific adapter and verification |
|---|---|---|
| Tennis | Body, wardrobe, colors, skeleton | Preserve current 17 slots, strings/contact, serve toss, two-hand backhand, locomotion and runtime corrections |
| Golf | Same appearance and bind | Load scrubbing, club grip/face, planted stance, impact scheduling; Drive→Putt→Chip→Iron/Half/Abort |
| Bowling | Same appearance and bind | Approach/root policy, hand-ball retention, exact release event, follow-through; no tennis racket solver |
| Boxing | Same appearance and bind | First-person arm framing if following current design, two fist targets, glove sockets, guard/punch recovery, no visible body penetration |
| Other sports | Same catalog/schema | Define locomotion, equipment, contact/release and camera requirements before importing clips |

A common Humanoid Avatar enables reuse; it does not guarantee correct props, contacts, proportions or handedness. Maintain separate sport clip catalogs and event metadata. Avoid a giant conditional controller that mixes all sports.

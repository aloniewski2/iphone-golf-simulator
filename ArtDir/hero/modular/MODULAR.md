# Hero_01 — Modular handoff

**PHASE: Modularize. Approved hero preserved; await Adnan before Mixamo.**

## Default assembly

Open `Hero_01_Assembled.blend`. All eight renderable objects share **one `Hero_01_Rig` armature**. The five attachment empties remain bone-parented to that rig. Studio camera/lights/floor are in `REVIEW_ONLY` and are excluded from FBX.

`modular_preview.png` is the assembled default loadout, rendered with the approved camera and studio lighting. `skin_tint_examples.png` shows the three tested skin colours. Original `../Hero_01.blend` was not overwritten.

## Slots and exports

| Slot | File | Mesh name(s) | Behavior |
|---|---|---|---|
| Fixed skin body / bind input | `Hero_01_Body.fbx` | `Body_Skin`, `Body_EyeSphere_L`, `Body_EyeSphere_R` | Full skin foundation plus approved exposed skin and fixed eyes; A-pose input for the next binding phase |
| Default body coverage | `Hero_01_Body_DefaultCoverage.fbx` | Same fixed body/eye names | Covered foundation culled for the default outfit; use this variant in the default mesh assembly |
| Hair | `Hero_01_Hair_Default.fbx` | `Hair_Default` | Swappable hair surface; Head weights |
| Hat | `Hero_01_Hat_Visor.fbx` | `Hat_Visor` | Swappable visor/headwear; Head weights |
| Shirt | `Hero_01_Shirt_Default.fbx` | `Shirt_Default` | Swappable polo including collar, placket and sleeve trim |
| Shorts | `Hero_01_Shorts_Default.fbx` | `Shorts_Default` | Swappable shorts including pocket/hem accents |
| Shoes | `Hero_01_Shoes_Default.fbx` | `Shoes_Default` | Both shoes **and socks** bundled in one slot |
| Racket / hand prop | Not created | — | Optional output omitted; use Hand_R / Hand_L for later equipment |

Face shape, ears, brows, smile, eye surfaces, fingers and body proportions are fixed in this handoff. Skin colour changes through a mask; it does not swap the head or affect reach/gameplay. There are no alternate bodies or new cosmetic designs.

## One shared skeleton

All FBXs serialize the same armature/rest matrices; this does **not** mean instantiating a separate skeleton per cosmetic. During assembly, retain one target rig and rebind each `SkinnedMeshRenderer.bones` entry by name. Discard the imported cosmetic's duplicate skeleton container. The assembled Blender file already follows this single-rig arrangement.

Preserved bone names:

- `Root`, `Hips`, `Spine`, `Chest`, `Neck`, `Head`.
- `Shoulder.L/R`, `UpperArm.L/R`, `LowerArm.L/R`, `Hand.L/R`.
- `UpperLeg.L/R`, `LowerLeg.L/R`, `Foot.L/R`, `Toes.L/R`.

No loose cosmetic root transforms are used in the assembled scene. Mesh transforms are identity, source origin remains at the feet, metric scale remains 1.70 m. FBX is −Z forward / Y up, no leaf bones and no animation.

### Sockets

Blender rest coordinates: X right, −Y forward, Z up; units meters.

| Socket | Parent bone | Position |
|---|---|---|
| Hat | Head | (0, 0, 1.665) |
| Hand_R | Hand.R | (−0.477, −0.023, 0.695) |
| Hand_L | Hand.L | (0.477, −0.023, 0.695) |
| Back | Chest | (0, 0.150, 1.060) |
| FaceExtra | Head | (0, −0.150, 1.405) |

Sockets are included in the body FBXs, not duplicated in the individual cosmetic FBXs. A future Mixamo bind must transfer all pieces to its resulting skeleton and preserve/reparent these sockets together.

## Skin tint

Files:

- `Hero_01_BaseColor.png`: approved 2K sRGB atlas.
- `Hero_01_SkinMask.png`: 2K mask, **skin in alpha**, RGB zero. Import as linear/non-colour data.
- `Hero_01_SkinMask.json`: measured mean linear skin luminance (`skin`), colour-space notes and example tones.
- `Hero_01_Normal.png`: 1K normal map; `Hero_01_Iris.png`: fixed 256px iris texture.

The mask protects eye whites, pupils and facial details, and has padding across UV seams. Neck/ear skin belongs to the fixed body, rather than disappearing with the hat/hair. Tint materials are separate from the outfit material instances.

### Blender controls

In the Shader Editor, open the shared node group **`Hero_01_SkinTint_Controls`**:

- **Skin Tone**: choose target RGB colour once; all skin material instances use it.
- **Tint Strength**: `0` preserves the approved original texture exactly; `1` applies the chosen tone.

The assembled file is saved at strength **0**. The three example renders use strength **1**:

| Example | sRGB hex |
|---|---|
| Light warm | `#F2C093` |
| Medium warm | `#B77950` |
| Deep warm | `#6A4030` |

### Existing runtime mapping

The project already has the required approach:

- `Unity/Assets/Scripts/Tennis/TennisCustomization.cs`: `AttachBase` rebinds bones by name; `Apply` uses a source texture and skin mask.
- `Unity/Assets/Resources/Tennis/Shaders/KitRecolor.shader`: skin uses mask alpha, `_Skin`, and `_Ref.w`.

The Blender preview reproduces that existing recolour formula:

`shaded = targetLinearRGB × clamp(sourceLinearLuminance / referenceSkinLuminance, 0.25, 1.35)`

`result = lerp(original, shaded, maskAlpha × strength)`

Use the JSON `skin` value for `_Ref.w`. `_Skin.a = 0` retains the approved texture; `_Skin.a = 1` applies a tone. Clothing and eye textures remain unchanged.

FBX cannot carry this Blender node graph as a Unity shader. The FBXs therefore export standard default atlas materials; the adjacent mask/JSON provide the input for the **existing** tint path. No new runtime shader, customization system, resource registration or Unity import was added. Existing hard-coded Player resource/renderer filters still need mapping to these names in a later authorized integration phase.

## Coverage and topology limits

The approved source was a clothed surface, with no complete skin body underneath. This phase preserves its exposed surfaces and adds a smooth inset anatomical foundation under the clothes/scalp. That foundation is a binding/coverage aid, not a newly sculpted unclothed character.

The default outfit uses the Blender **`Default outfit coverage` Mask modifier** and `Default_VisibleSkin` vertex group. Its exported equivalent is `Hero_01_Body_DefaultCoverage.fbx`. This culls hidden foundation triangles and prevents skin appearing through the default garment seams. `Hero_01_Body.fbx` exports the full foundation with that mask disabled.

**Choose one body export, not both.** Default assembled loadout = coverage body + hair + visor + shirt + shorts + shoes. The preview uses precisely this arrangement.

Replacement pieces must cover at least the default seam footprints:

- Polo: torso, shoulder joins, upper-arm cut boundaries and lower neck/collar junction.
- Shorts: hips/crotch and upper thighs down to the default hems.
- Shoes slot: ankles and feet; socks are part of this slot.
- Hair/headwear: scalp and the existing hairline/headband boundary. Bald, buzz-cut, or hatless coverage needs its own coverage check; it is not implied by the default QA.

The default loadout has continuous visible neck, wrist and ankle coverage. Wrist/hand surfaces stay in the fixed body. Default garment boundary positions and source normals were preserved. Rest-pose checks do not guarantee gaps remain closed in an extreme swing; test those after binding.

The full body is a **layered skin surface plus a closed underlying foundation**, with separate fixed eyes. It is not a new all-quad, single watertight anatomical shell. Existing cleaned triangle topology and provisional weights are retained on visible skin; the foundation is remeshed and weighted. It is prepared as the human-shaped input for Mixamo, but actual Mixamo acceptance and deformation quality remain untested because that phase is prohibited here. Do not expose the covered foundation as a finished shirtless/barefoot cosmetic.

## Verification

`verification.json` records:

- One armature in the assembled file; all skin/hair/clothes point to it.
- Identical bone names, parent hierarchy and rest-matrix signature across every FBX.
- No unweighted vertices or invalid bone-weight sums; coverage groups are excluded from bone-weight accounting.
- All five sockets retained.
- Every approved exterior triangle present in the assembled default, with none added or missing: **46,838 visible triangles**.
- Full-body export includes the foundation; the default-coverage export omits it.

`render-comparison.json` measures the assembled default against `../Hero_01_threequarter.png`. Matching camera, lighting and geometry are used; this is a render comparison, not a likeness claim. `modular_manifest.json` contains current per-slot mesh counts and names.

No Mixamo, Unity import, animation change, Tripo/Higgsfield generation or trailer work occurred.

**STOP — wait for Adnan before PHASE Mixamo.**

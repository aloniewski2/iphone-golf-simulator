# Art Bible — Party Tennis

## North star
**Switch Sports / Wii clarity + soft plastic beauty.** Readable on iPhone. Not Roblox default, not photoreal, not film-only Pixar.

## Locked character bases
Authority images live in `ArtDir/hero/base_lock/`:
- Soft plastic stylized **young-adult** male + female
- Grey mannequin body (placeholder for clothes Adnan will create)
- Expressive faces, solid opaque hair clumps
- Stocky-friendly athletic toy proportions

Any new hero work must overlay-match these plates before polish.

## Shape language
- Rounded soft forms, clear limb separation
- Head readable; hair as **sculpted opaque mass** (not strands/cards)
- Body mass: athletic soft-toy, not stick limbs, not hyper-muscled sim

## Materials (URP Lit)
| Part | Notes |
|------|--------|
| Skin | Warm matte soft plastic; roughness ~0.55–0.70 |
| Hair | **Opaque** only on solid locks; roughness ~0.55–0.65 |
| Body suit (temp) | Flat grey soft plastic until clothes exist |
| Eyes | Small specular exception OK |

No transparent hair queues. No metallic skin.

## Color
Bases: natural skin + dark hair as in plates. Outfit colors come later from Adnan’s clothes — do not invent a permanent kit on the body.

## Camera / presentation
Customize and proof shots: neutral grey studio, soft key+fill, same framing as plates when comparing. Gameplay: keep hero readable vs court (see env plans separately).

## Customization (future)
Slots: Hair · Headwear · Top · Bottom · Shoes · Racket.  
Clothes must plug into sockets without rewiring the humanoid. Default outfit must never be the only body geometry.

## Quality bar
If it looks worse in Unity than the plate → it is not shipping. Fix export, normals, color space, compression, or mesh — don’t “accept Blender loss.”

## Bald reference update — 2026-09-30

Adnan supplied replacement bald male and female references. The current base_lock male_body_plain.jpg and female_body_plain.jpg are those unchanged files; earlier hair-on plates are archived under base_lock/archive/with_hair_20260930/. This explicit update supersedes earlier hair-on identity statements. Match the complete bald head, face and grey body. Main HeroBase_Male/Female prefabs are bald; hair stays a separate optional asset and must never supply missing scalp, ears, face or neck. Optional hairstyle previews are separate from the base comparison. No clothes, rig, Mixamo or Animator.

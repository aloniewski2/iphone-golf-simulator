# PHASE: HeroV4_HairAndCustomizeSafe (Plan 3B — hero detail)

Paste into Claude Opus. One PHASE only. Stop when done — then Adnan reviews.

Motion fluidity (3A) and env (2D) stay out of scope unless a wardrobe swap breaks a socket — then fix the socket only.

---

## Project
`/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator`
Read `AGENTS.md` + `art-bible.md`.

**Locks:** Hero POLISH V4 **proportions + Mixamo bind** stay. Do **not** full-body Tripo regen. This is a **surgical mesh / material / socket** pass on the locked hero so hair reads solid and cosmetics can swap without breaking the body.

---

## Why (Adnan + screenshot)

Customize / locker preview: hair looks **transparent / not modeled correctly**. Clear **horizontal gap / slice** through mid-hair — locker shelves visible through the gap. Silhouette feels broken vs plate energy. Need V4 details honed again, and **fully customizable without issues** (wardrobe slots swap clean: hair, hat, top, bottom, racket — no poke-through, no missing geo, no alpha holes).

Reference plate: locked plate_01 / art-bible hero look. Cosmetics = identity only (no P2W).

---

## Hard rules

1. **No full regen** of body. Prefer Blender edit of existing hair mesh + Unity materials/LODs.
2. **Hair must be opaque where it should be solid** — no see-through mid-scalp gaps. Soft edge cards OK only at fringe if sorted correctly; chunky locks = solid mesh preferred for party-chibi.
3. **Customize-safe** — every wardrobe slot: equip / unequip / swap A↔B without clipping head, neck, visor, collar, or racket grip.
4. Keep Mixamo humanoid + existing animator bindings. Don’t break Plan 1 hit sockets / racket hand sockets.
5. Mobile URP: no expensive transparent sorting spaghetti; prefer opaque / alpha-clip with correct cull.

---

## Do

### P0 — Hair
1. Open current hair asset (mesh + materials). Reproduce the horizontal gap in Scene / Customize UI.
2. Fix root cause (whichever is true — verify, don’t guess forever):
   - Missing / separated geo mid-hair → **rebuild or weld** lock volumes so silhouette is continuous
   - Double-sided / backface cull hole → fix normals + cull mode
   - Alpha / dither / transparent queue sorting → move solid locks to **opaque or alpha-clip**; reserve true transparency for thin fringe only; fix render queue / depth write
   - LOD dropping mid-cards → keep close-up LOD solid
3. Re-import; verify in **gameplay cam + customize close-up + ultimate close-up** (same angles that exposed faces before).
4. Match art-bible: chunky toy hair, readable silhouette, world quieter than character.

### P1 — Customize-safe wardrobe
5. Audit slots: Hair · Headwear (visor) · Top · Bottom · Socks/shoes if slotted · Racket.
6. For each slot: swap 2 variants (or hide/show). Check:
   - No hair through visor / visor through hair
   - No neck / collar poke
   - Scalp covered when hair unequipped (cap mesh or scalp material — no bald hole unless intentional bald cosmetic)
   - Racket still seats in hand socket; two-hand BH grip still valid
7. Fix skinning weights on hair/hat near ears and nape so run/swing doesn’t open new gaps.
8. Shirt dither/speckle: if accidental shader artifact (not intentional fabric), clean material to soft plastic / matte URP Lit per art-bible.

### P2 — Small body debt only if cheap
9. Known armpit / shoulder pinch if still obvious in customize — micro weight or mesh fix only. No proportion reopen.
10. Document slot list + how to add a new hair/hat without breaking V4.

---

## Don’t
- New body proportions, new Tripo hero, Mixamo proxy as final.
- Rewrite anims / cameras / ultimate / env.
- Shop economy / P2W stats on cosmetics.
- Leave “temp” transparent hair in shipping path.

---

## Done when
1. Customize close-up: **no background visible through hair**; silhouette solid and plate-adjacent.
2. Swap hair + hat + top + bottom + racket with **zero** poke-through / missing geo.
3. Short note: root cause of the gap, assets touched, remaining cosmetic debt for later skins.

Reply paths + what changed. **Stop for Adnan.**


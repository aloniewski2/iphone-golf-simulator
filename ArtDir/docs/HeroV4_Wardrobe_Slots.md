# Hero V4 wardrobe slots: how to add a cosmetic without breaking the hero

Cosmetics are identity only. They never change stats, timing, reach or hitboxes.

## Slots
| Slot | What it is | Parent | Code |
|---|---|---|---|
| Hair | one skinned mesh (`Hair_Default`) plus a baked side-hair layer inside `Body_Skin` | Head bones | `ModularHeroLook.Equip(Slot.Hair)` |
| Headwear | None / Visor / Cap / Sweatband, skinned to Head. Band hats get an auto-fitted hair liner | `Slot_Hat` under the skeleton | `HeroCosmetics.EquipHat`, `BuildHatLiner` |
| Shirt / Shorts | recolour regions of the shared atlas (mask R = shirt body, G = shorts + navy trim) | body | `HeroKit.Apply` |
| Shoes | flat material `Hero_01_ShoeCleanWhite` | body | `HeroKit.Apply` |
| Racket | `Hero_Racket_Blue` frame colour. The mesh stays on the `Hero_Racket_GripSocket` under Hand_R | Hand_R socket | `HeroKit.Apply` |
| Prop | trophy on the Hand_L socket (celebrations only) | Hand_L | `HeroCosmetics.SetTrophy` |

## Rules for a new hair or hat mesh
1. **Rig:** build on the V4 bind (`Hero_01_Mixamo_Bind.fbx`). Skin 100% to Head (hats) or to Head/Neck (hair). No new bones.
2. **Solid:** closed, opaque volumes. Chunky toy locks, not alpha cards. The material is URP Lit, opaque, `_Cull 0` (two-sided) if the shell is thin.
3. **Coverage:** the hair must cover the ears' top edge and the nape down to the collar line. The scalp material shows as shadowed hair colour through any gap.
4. **Hats:** keep at least 3 mm off the hair everywhere. A band or visor opening is closed automatically by the hat liner, and a closed hat (cap) needs no liner.
5. **Clearance:** nothing may enter the racket-hand socket volume or the collar.
6. **Verify:**
   - Run the editor orbit audit: `HERO_STRIP_ISOLATE=1` with `orb_*` views and `headholes.py`, in 5 poses. It must show 0 head-hole pixels outside raised-arm gaps.
   - Re-run `HeroLockerExport.Run` so the locker mirror matches, and add the part's hat tag.
7. **Palettes:** add colours to `Outfit.palette` (Swift). The recolour maths is shared: `HeroKit.cs` in the game and `HeroV4.tintedAtlas` in the locker. Change both or neither.

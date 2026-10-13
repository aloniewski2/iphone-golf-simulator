# Country-club hub restyle (2026-10-12)

Brief: `ArtDir/ui/hub-targets-2026-10-12/country-club/COUNTRY_CLUB_HUB_BRIEF.md` and its 7 before/after sheets.
Result and gates: `proof/GATE_RESULTS.md`. Side-by-sides: `proof/*_before_after.png` (and `_before_after_target.png`
with the brief's target as a third panel).

## Pass 2 (2026-10-12, later): interiors + two fixes

- **PLAY Hall** (sheet `08_play_hall_before_after.png`): checkered marble floor, a brass compass in the floor circle, cream plaster
  over navy panelling, a coffered navy ceiling with brass lanterns, trophy cabinets and paintings on the side walls, topiaries
  by the arches, a rug by the door. The north wall now has three real round arches: TENNIS looks out on a sunlit grass court,
  GOLF on the range, the middle one is a closed wooden door with a COMING SOON plaque. Navy arched signs with gold icon and word
  over each arch. The scenery outside is built behind the wall (never walked: the wall's old collider is unchanged), lit by
  the sun; the hall's shell casts no shadow so the sun reaches it. The room's own PLAY HALL plate moved to the west wall (it was
  hidden behind the old middle arch).
- **Locker / Clubhouse** interiors: the same shell (plaster, panelling — navy in the Locker, walnut in the Clubhouse — crown
  moulding, coffered ceiling, lanterns), wooden lockers with brass, brass rails, wood shelves, a navy runner, a gold vanity
  mirror; leather sofas, an old rug, walnut table, green-leather desk, navy mailbox, gold-framed boards, a trophy cabinet,
  paintings, topiaries. Every station, collider and plate stays where it was.
- **PLAY on the clock tower** (target 01): a vertical PLAY sign down the tower face under the clock; the portico roof is lower so
  the tower shows from the plaza; the belfry is gone (the spire sits on the clock stage); MOTION CLUB moved to the portico's
  entablature.
- **Golf pond + bridge** visible from the range camera: the course beyond the range sits on a terrace 1.2 m up; the pond and its
  footbridge moved to the right of the range in front of the clubhouse (in the middle they are always behind the screens).

Proof: `proof/pass2/` (before = pass 1, after = pass 2, same cameras; `08_play_hall_before_after_target.png` has sheet 08's
target as a third panel; `after/x_hall_near.png` a few steps closer to the arches).

## What changed (Unity only; no Swift, no characters, no animation)

| File | Change |
|---|---|
| `Unity/Assets/Scripts/Hub/HubArt.cs` | The plaza: golden-hour sky/sun/grade, the sea (polar disc, glitter toward the visible sun), the island (lawn, cliffs, boulders), gravel plaza with a cobbled circle and flagstone paths, the star medallion (the emote stage), hedge ring with roses, LOCKER / CLUBHOUSE as stone houses, PLAY as the clock-tower hall, dressing, the grass court / putting green / pond outside the hedge. |
| `Unity/Assets/Scripts/Hub/HubRooms.cs` | Signs are navy boards with gold frames and gold text; TENNIS and GOLF are outdoors (grass court, practice range) inside the old rooms' invisible walls; bays restyled (navy/brass pads, gold glow, wooden benches, gold screen trim); exits restyled. Interiors: the club shell and the PLAY Hall / Locker / Clubhouse dressing (pass 2). |
| `Unity/Assets/Scripts/Hub/HubClub.cs` (new) | The kit: palette, cached materials, small painted textures, a mesh builder, the props. |
| `Unity/Assets/Scripts/Hub/HubLife.cs` (new) | Wind for the swaying trees and roses (global `_HubWind`), gulls, fluttering flags. |
| `Unity/Assets/Scripts/Hub/HubIcons.cs` | Two banner icons: `rackets`, `shield`. |
| `Unity/Assets/Resources/Hub/HubSurface.shader` (new) | Lawns, hedges, trees, gravel, cobbles: one main light + SH, wrap lighting, world-space noise, wind sway. No keywords of its own. |
| `Unity/Assets/Resources/Hub/HubWater.shader` (new) | Sea, fountain, ponds: opaque, sine ripples, fresnel, sun glitter. |

Kept exactly: `HubLayout` (places, doors, spots, spawn), `HubWorld` (cameras, build steps, doors, prompts), every
collider that shaped the walking space (floors, plaza wall ring, room walls, benches, planters, crest), the bay screens'
live previews (`HubBayScreen`), labels and ids. New small colliders: house facades (box instead of drum), planter boxes
against the LOCKER front, the CLUBHOUSE topiaries and second parasol, the PLAY lamps and banner poles, two plaza benches,
the garden borders on the front half of the plaza edge (0.9 m deep), the range's flag pin; pass 2: the topiaries in the
rooms (small capsules, where the old pots had none) and the PLAY Hall's three trophy cabinets against its side walls.

## Re-running the proof

```
SP=<scratch dir>
work/hub-country-club/tools/run.sh $SP/U $SP/after sync      # clone of Unity/ (made on first use), synced, 7 shots + render costs
work/hub-country-club/tools/suite.sh $SP/U $SP/suite_after    # the hub PlayMode suites in the clone
python3 work/hub-country-club/tools/sheet.py <before dir> <after dir> work/hub-country-club/proof ArtDir/ui/hub-targets-2026-10-12/country-club
```

`HubCountryClubProof.cs` (copied into the clone by `run.sh`, not a repo test) uses the same cameras as
`HubPlazaTests.LookStills`, but first skips the arrival fly-in: a fresh boot now opens on the fly-in's high shot, so
`LookStills`' spawn still shows that shot, not the spawn (it predates the fly-in). The BEFORE shots were rendered from a
second clone with the original `HubArt.cs` / `HubRooms.cs` / `HubIcons.cs` restored from git.

Windowed Unity runs are muted (`GOLF_MUTE=1`) and serialised with `work/postcard-look/heavy.sh`.

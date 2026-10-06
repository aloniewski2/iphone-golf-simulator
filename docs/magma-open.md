# The Magma Open: three holes on a lake of lava

A third course, three holes (21–23), whose islands float on the molten lake of a volcano crater, under a
red-purple ash sky. The look is Adnan's Volcano tennis venue (`origin/newmapsandmenus`), rebuilt for our
Built-in render pipeline (his is URP, so the shaders are re-written, not copied).

| Hole | Name | Par | Shape |
|---|---|---|---|
| 21 | Obsidian Slab | 3 | a slab of black rock for a tee; carry ~100 yd of lava to a bigger slab with a raised green |
| 22 | Ember Causeway | 4 | a winding causeway across the lake: a wide landing, a neck where the lava bites in, a second landing, a plateau green |
| 23 | Caldera Crown | 5 | a horseshoe of rock round a lagoon with a geyser in it; tee and green ~145 yd apart across the lagoon: play round (three shots) or gamble on the carry |

The sea is lava (`Hole.SeaIsLava`): a ball that leaves the rock is a penalty with the result "Lava", the same
rule as water. Two lava ponds (hole 22, 23) are hazards inside the islands.

Unlock: finish a round on Wild Isles (`Unlocks.Magma`, `Unlocks.CourseReward`). The server mirrors the pars
(`COURSE_HOLES` in `server/src/game.js`).

## Building a hole (Blender)

Design modules in `blender/scripts/`: `hole21_slab_design.py`, `hole22_causeway_design.py`,
`hole23_caldera_design.py`, using `magma_shapes.py` (pure geometry: `ribbon` for an island that follows a curve,
`frame`/`place`/`polar` to put features on it by fraction-along and fraction-of-width). Run them through
`course_builder.py` like every other hole; it writes `Resources/Course/hole_2N.fbx`, the card, the flag and
`blender/holes_mock/hole_2N_numbers.txt` (the C# numbers pasted into `Course.Magma()` in `Hole.cs`).

`THEME = "magma"` and `LAVA_SEA = True` switch the builder to `PALETTES["magma"]` (black basalt, pumice, ember
turf), a glowing emissive sea, a dark card, and the extras in `course_extras.py`: `OBSIDIAN_SPIRE`, `BRAZIER`,
`STACKS` (basalt columns standing in the lava), `GEYSER` (an empty named `GEYSER_n`; the game puts a fire fountain
on it).

## The crater (Unity)

* `Course/LavaWorld.cs` builds the surroundings when a magma hole is built: the lava lake at y = -0.3
  (`LavaLake.shader`, two crawling layers in world space), Adnan's crater profile (his metres, scaled to the
  course: lake radius = max(240, half-diagonal + 110), heights x 0.62) as a stratified basalt wall with lava streams
  (`CraterRock.shader`), boulders, an eruption plume, fire fountains at each `GEYSER_` and drifting embers
  (`ParticleAdd.shader`).
* `Course/HoleAtmosphere.cs` sets the light for the hole: an ember sun, ambient lit orange from below, a reddened
  haze, a far clip of 2600 and the sky (`SkyMagma.shader`: gradient, sun glow, ash cloud). The next hole puts the
  resort's look back (the course screen browses between them).
* Textures are baked, not imported: `Golf Arcade > Bake Lava Textures` (`Editor/LavaTextureBaker.cs`) writes
  `Resources/Course/Lava/*.png`.
* The four shaders are looked up at runtime, so `ProjectSetup.RuntimeShaders` lists them (Golf Arcade > Set Up
  Project puts them in the Always Included list).

## Tests

* EditMode `MagmaOpenTests`: the course, playable tee/pin, the sea is lava, the caldera's tee-to-pin distance,
  the unlock.
* PlayMode `MagmaOpenTests`: the holes float in the crater (captures in `Library/Captures/magma`), a long drive
  into the lake burns and the atmosphere reverts on a resort hole.
* `PlaythroughTests.TheMagmaOpenPlaysOut`, and holes 21–23 in `CourseReviewTests`.

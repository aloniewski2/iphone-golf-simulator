# Accessories clip check (read this first)

Runs: tennis heroes (Male, Female) with the tennis cap: 64 clips (every emote, intro, Walk, Run, stroke); golf heroes with golf cap + glove: 50 clips; golf bag on the golf heroes: Walk, Run, Idle, Wave, Cheer, FistPump, every emote and intro (40 clips). Tool: `AccessoriesClipCheck` (Unity skinning, 10 samples/s). Caps were checked with the previous (flatter) brim; the crown, band and fit are unchanged, the brim only moved out of the hands' way.

**Cap (tennis + golf): clean.** Walk, Run, Idle, Cheer and every stroke/swing show no contact. Contact only where the animation puts the hands on the head: `Emote_Shock` (= `MissWhiff`): fingers into the cap 1.8-1.9 mm, cap into the hands up to 10 mm; Female `Emote_Spike`: arm through the cap (2 mm / 19.6 mm at t 0.5). Same places where the emote audit already found hands in the scalp.
**Glove:** skin cannot show through (the covered hand triangles are not drawn). Its "glove inside body" numbers equal the same hand without a glove (`BareHandL` rows in the CSV): the hand's own clipping into hips / thigh / the other hand, not something the glove adds.
**Bag (plaza + between shots only):**
- Idle, Wave, Cheer, Walk (Female), RunForward (Female): skin never enters the bag; the strap pad presses 4.7-4.9 mm into the polo (the strap sits ~10 mm off the skin, a polo is ~4 mm).
- Male Walk / Run and Female Run: trouser / skirt cloth enters the bag's base by up to ~20 mm (the legs swing back into the boot).
- `Emote_FistPump`, `FistPump`, `Emote_Victory`, `Emote_JumpJoy`, arms-overhead emotes: the raised arm passes through the strap span (skin 5-20 mm), strap 28 mm into the arm in FistPump (offline pose check, `out/*/golf_bag/pose_check.json`). Swings: bag through the arms (Drive -53/-66 mm), which is why the bag is never carried through a swing.
- Not fixed: hide the strap / bag during overhead emotes at runtime if it shows.

Columns: SKIN = skin vertices inside the accessory wall; CLOTH = garment vertices inside it; ACC-IN-BODY = accessory vertices inside the body (own hand excluded for the glove). CSVs: `run2_tennis`, `run2_golf`, `run5_golf` (this folder).


Poses of every emote / intro / Walk / Run / swing clip (10 samples a second) with the accessories on; Unity skinning (BakeMesh). Depths in mm, search radius 20 mm.
Columns: SKIN = skin vertices inside the accessory wall; CLOTH = garment vertices inside it; ACC-IN-BODY = accessory vertices inside the body (a glove's own hand excluded).

## Acc_Bag

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 26 | 26 | 20.0 mm (GolfKitHero_Male Emote_Defeat) |
| idle / celebrate / other | 8 | 8 | 20.0 mm (GolfKitHero_Male FistPump) |
| walk / run | 6 | 6 | 20.0 mm (GolfKitHero_Male RunForward) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| GolfKitHero_Male | Emote_Defeat | 115 / 19.7 | 7503 / 20.0 | 847 / 20.0 | 2.90 |
| GolfKitHero_Male | Emote_JumpJoy | 80 / 20.0 | 4942 / 19.8 | 735 / 19.9 | 1.90 |
| GolfKitHero_Male | Emote_Slump | 190 / 20.0 | 5873 / 19.9 | 1558 / 20.0 | 0.10 |
| GolfKitHero_Male | Emote_Stomp | 101 / 20.0 | 5280 / 20.0 | 656 / 19.9 | 1.90 |
| GolfKitHero_Male | Emote_Thrust | 63 / 19.8 | 5198 / 20.0 | 719 / 20.0 | 1.80 |
| GolfKitHero_Male | Emote_Victory | 132 / 19.9 | 7728 / 20.0 | 644 / 19.8 | 3.00 |
| GolfKitHero_Male | FistPump | 285 / 4.8 | 8219 / 20.0 | 230 / 20.0 | 2.30 |
| GolfKitHero_Male | Intro_Pushups | 224 / 20.0 | 8541 / 19.9 | 1300 / 20.0 | 2.50 |
| GolfKitHero_Male | RunForward | 109 / 20.0 | 1647 / 19.9 | 224 / 19.8 | 0.30 |
| GolfKitHero_Female | Emote_Defeat | 90 / 20.0 | 12063 / 20.0 | 1014 / 20.0 | 0.10 |
| GolfKitHero_Female | Emote_FistPump | 31 / 19.8 | 6289 / 19.4 | 812 / 20.0 | 1.40 |
| GolfKitHero_Female | Emote_JumpJoy | 34 / 19.1 | 8059 / 19.3 | 789 / 20.0 | 1.90 |
| GolfKitHero_Female | Emote_Slump | 93 / 17.6 | 8915 / 19.7 | 1022 / 20.0 | 2.50 |
| GolfKitHero_Female | Emote_Stomp | 39 / 19.9 | 8776 / 20.0 | 736 / 19.9 | 2.00 |
| GolfKitHero_Female | FistPump | 144 / 4.2 | 13796 / 4.9 | 1307 / 20.0 | 1.00 |
| GolfKitHero_Female | Intro_Pushups | 95 / 19.8 | 12166 / 20.0 | 1175 / 20.0 | 0.20 |
| GolfKitHero_Female | Run | 0 / 0.0 | 3106 / 20.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_FistPump | 86 / 19.7 | 3992 / 19.9 | 657 / 19.9 | 1.40 |
| GolfKitHero_Male | Intro_BringIt | 139 / 19.9 | 7508 / 19.5 | 643 / 19.8 | 0.00 |
| GolfKitHero_Male | Run | 107 / 19.9 | 1919 / 19.9 | 241 / 19.9 | 0.50 |
| GolfKitHero_Female | Emote_Scuba | 4 / 1.3 | 8143 / 19.6 | 262 / 19.9 | 0.00 |
| GolfKitHero_Female | Emote_Shock | 10 / 1.5 | 8710 / 19.7 | 680 / 19.9 | 1.90 |
| GolfKitHero_Female | Emote_Spike | 26 / 19.9 | 7720 / 19.5 | 290 / 19.9 | 0.00 |
| GolfKitHero_Female | Emote_Thrust | 20 / 17.6 | 8612 / 19.9 | 550 / 19.9 | 1.90 |
| GolfKitHero_Female | Intro_BringIt | 6 / 1.3 | 12555 / 19.3 | 393 / 19.9 | 0.00 |
| GolfKitHero_Female | Intro_Wave | 11 / 1.4 | 8319 / 19.4 | 434 / 19.9 | 0.00 |
| GolfKitHero_Male | Emote_Scuba | 113 / 19.7 | 4920 / 19.5 | 264 / 19.8 | 0.00 |
| GolfKitHero_Male | Emote_Shock | 65 / 19.8 | 5380 / 19.3 | 640 / 19.8 | 2.00 |
| GolfKitHero_Male | Emote_Spike | 77 / 19.7 | 4887 / 19.8 | 192 / 19.8 | 0.00 |
| GolfKitHero_Male | Intro_Wave | 90 / 19.1 | 5029 / 19.4 | 310 / 19.8 | 0.00 |
| GolfKitHero_Female | Emote_Victory | 19 / 16.5 | 12443 / 19.4 | 683 / 19.8 | 3.00 |
| GolfKitHero_Male | Walk | 0 / 0.0 | 3025 / 19.6 | 2 / 0.0 | 0.90 |
| GolfKitHero_Male | Cheer | 0 / 0.0 | 4813 / 4.9 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Idle | 0 / 0.0 | 5124 / 4.9 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Wave | 0 / 0.0 | 4813 / 4.9 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Cheer | 0 / 0.0 | 8533 / 4.8 | 31 / 3.0 | 0.50 |
| GolfKitHero_Female | Wave | 0 / 0.0 | 7843 / 4.8 | 31 / 3.0 | 0.50 |
| GolfKitHero_Female | Idle | 0 / 0.0 | 8169 / 4.7 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Walk | 0 / 0.0 | 4720 / 4.7 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | RunForward | 0 / 0.0 | 2653 / 4.6 | 0 / 0.0 | -1.00 |

## Acc_Cap

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 66 | 27 | 20.0 mm (GolfKitHero_Male Emote_FistPump) |
| idle / celebrate / other | 18 | 4 | 20.0 mm (GolfKitHero_Male Cheer) |
| swing / stroke | 14 | 2 | 10.3 mm (MatchHero_Male MissWhiff) |
| walk / run | 16 | 3 | 20.0 mm (GolfKitHero_Male Run) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| GolfKitHero_Male | Cheer | 672 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_FistPump | 320 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_JumpJoy | 661 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_Scuba | 796 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_Shock | 1906 / 20.0 | 0 / 0.0 | 95 / 7.9 | 0.70 |
| GolfKitHero_Male | Emote_Spike | 682 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_Stomp | 429 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_Thrust | 1390 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Idle | 672 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Intro_BringIt | 1293 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Run | 304 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | RunForward | 214 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Walk | 411 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Wave | 672 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Emote_Shock | 1373 / 20.0 | 0 / 0.0 | 863 / 9.5 | 0.60 |
| GolfKitHero_Female | Emote_Victory | 1009 / 20.0 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_Defeat | 220 / 19.9 | 0 / 0.0 | 100 / 0.2 | 1.70 |
| GolfKitHero_Male | Emote_Slump | 133 / 19.9 | 0 / 0.0 | 90 / 0.2 | 1.70 |
| GolfKitHero_Male | Emote_Victory | 1786 / 19.9 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Intro_Pushups | 133 / 19.9 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Intro_Wave | 785 / 19.9 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Emote_FistPump | 71 / 19.9 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Emote_Stomp | 164 / 19.9 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Emote_Thrust | 718 / 19.9 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Intro_BringIt | 15 / 19.8 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | FistPump | 79 / 19.6 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| MatchHero_Female | EmoteSpike | 21 / 2.2 | 0 / 0.0 | 112 / 19.6 | 0.50 |
| GolfKitHero_Female | Emote_JumpJoy | 236 / 18.9 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Emote_Spike | 200 / 18.9 | 0 / 0.0 | 0 / 0.0 | -1.00 |
| MatchHero_Male | EmoteShock | 311 / 1.8 | 0 / 0.0 | 2137 / 10.3 | 1.10 |
| MatchHero_Male | Emote_Shock | 311 / 1.8 | 0 / 0.0 | 2137 / 10.3 | 1.10 |
| MatchHero_Male | MissWhiff | 311 / 1.8 | 0 / 0.0 | 2137 / 10.3 | 1.10 |
| MatchHero_Female | EmoteShock | 294 / 1.9 | 0 / 0.0 | 769 / 10.0 | 0.40 |
| MatchHero_Female | Emote_Shock | 294 / 1.9 | 0 / 0.0 | 769 / 10.0 | 0.40 |
| MatchHero_Female | MissWhiff | 294 / 1.9 | 0 / 0.0 | 769 / 10.0 | 0.40 |
| GolfKitHero_Female | Intro_Wave | 1 / 5.6 | 0 / 0.0 | 0 / 0.0 | -1.00 |

## Acc_GloveL

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 26 | 13 | 20.0 mm (GolfKitHero_Male Emote_Defeat) |
| idle / celebrate / other | 8 | 2 | 19.8 mm (GolfKitHero_Female Idle) |
| swing / stroke | 10 | 10 | 20.0 mm (GolfKitHero_Male Drive) |
| walk / run | 6 | 5 | 20.0 mm (GolfKitHero_Male Run) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| GolfKitHero_Male | Emote_Defeat | 0 / 0.0 | 168 / 18.9 | 388 / 20.0 | 0.30 |
| GolfKitHero_Male | Emote_JumpJoy | 0 / 0.0 | 28 / 18.6 | 433 / 20.0 | 0.20 |
| GolfKitHero_Male | Emote_Shock | 0 / 0.0 | 0 / 0.0 | 4618 / 20.0 | 0.30 |
| GolfKitHero_Male | Emote_Slump | 0 / 0.0 | 258 / 19.6 | 3207 / 20.0 | 0.90 |
| GolfKitHero_Male | Emote_Stomp | 0 / 0.0 | 196 / 19.9 | 428 / 20.0 | 1.70 |
| GolfKitHero_Male | Run | 0 / 0.0 | 450 / 19.8 | 2712 / 20.0 | 0.30 |
| GolfKitHero_Male | Walk | 0 / 0.0 | 164 / 19.5 | 39 / 20.0 | 0.80 |
| GolfKitHero_Female | Emote_Defeat | 0 / 0.0 | 234 / 19.9 | 6187 / 20.0 | 1.80 |
| GolfKitHero_Female | Emote_Shock | 0 / 0.0 | 0 / 0.0 | 1376 / 20.0 | 1.40 |
| GolfKitHero_Female | Emote_Slump | 0 / 0.0 | 554 / 20.0 | 12285 / 20.0 | 0.50 |
| GolfKitHero_Female | Emote_Stomp | 0 / 0.0 | 521 / 19.9 | 2198 / 20.0 | 0.80 |
| GolfKitHero_Female | Run | 0 / 0.0 | 575 / 19.9 | 4083 / 20.0 | 0.30 |
| GolfKitHero_Female | Walk | 0 / 0.0 | 318 / 19.7 | 1362 / 20.0 | 0.40 |
| GolfKitHero_Male | Drive | 0 / 0.0 | 0 / 0.0 | 16884 / 20.0 | 2.50 |
| GolfKitHero_Male | HalfSwing | 0 / 0.0 | 0 / 0.0 | 20082 / 20.0 | 2.10 |
| GolfKitHero_Male | IronSwing | 0 / 0.0 | 0 / 0.0 | 16138 / 20.0 | 2.50 |
| GolfKitHero_Male | Putt | 0 / 0.0 | 0 / 0.0 | 11267 / 20.0 | 1.20 |
| GolfKitHero_Female | Drive | 0 / 0.0 | 0 / 0.0 | 7081 / 20.0 | 2.50 |
| GolfKitHero_Female | HalfSwing | 0 / 0.0 | 0 / 0.0 | 18350 / 20.0 | 2.50 |
| GolfKitHero_Female | IronSwing | 0 / 0.0 | 0 / 0.0 | 5993 / 20.0 | 2.40 |
| GolfKitHero_Female | Emote_FistPump | 0 / 0.0 | 2 / 19.9 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Idle | 0 / 0.0 | 227 / 19.8 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Chip | 0 / 0.0 | 0 / 0.0 | 12450 / 18.6 | 1.60 |
| GolfKitHero_Female | Chip | 0 / 0.0 | 0 / 0.0 | 14005 / 18.5 | 0.80 |
| GolfKitHero_Female | Putt | 0 / 0.0 | 0 / 0.0 | 11079 / 18.4 | 1.30 |
| GolfKitHero_Male | Emote_Spike | 0 / 0.0 | 75 / 18.3 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_Scuba | 0 / 0.0 | 0 / 0.0 | 99 / 10.5 | 1.90 |
| GolfKitHero_Male | RunForward | 0 / 0.0 | 28 / 7.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Emote_Spike | 0 / 0.0 | 20 / 4.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Wave | 0 / 0.0 | 105 / 3.5 | 0 / 0.0 | -1.00 |

## BareHandL

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 26 | 26 | 20.0 mm (GolfKitHero_Male Emote_JumpJoy) |
| idle / celebrate / other | 8 | 8 | 20.0 mm (GolfKitHero_Male Cheer) |
| walk / run | 6 | 6 | 20.0 mm (GolfKitHero_Male Run) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| GolfKitHero_Male | Cheer | 0 / 0.0 | 0 / 0.0 | 1835 / 20.0 | 1.40 |
| GolfKitHero_Male | Emote_JumpJoy | 0 / 0.0 | 0 / 0.0 | 2062 / 20.0 | 1.10 |
| GolfKitHero_Male | Emote_Scuba | 0 / 0.0 | 0 / 0.0 | 3994 / 20.0 | 1.20 |
| GolfKitHero_Male | Emote_Shock | 0 / 0.0 | 0 / 0.0 | 4058 / 20.0 | 1.00 |
| GolfKitHero_Male | Emote_Slump | 0 / 0.0 | 0 / 0.0 | 4036 / 20.0 | 0.40 |
| GolfKitHero_Male | Emote_Spike | 0 / 0.0 | 0 / 0.0 | 2650 / 20.0 | 1.50 |
| GolfKitHero_Male | Emote_Thrust | 0 / 0.0 | 0 / 0.0 | 2940 / 20.0 | 0.20 |
| GolfKitHero_Male | Emote_Victory | 0 / 0.0 | 0 / 0.0 | 3716 / 20.0 | 0.90 |
| GolfKitHero_Male | Idle | 0 / 0.0 | 0 / 0.0 | 947 / 20.0 | 0.30 |
| GolfKitHero_Male | Intro_Pushups | 0 / 0.0 | 0 / 0.0 | 1796 / 20.0 | 0.30 |
| GolfKitHero_Male | Intro_Wave | 0 / 0.0 | 0 / 0.0 | 3026 / 20.0 | 1.50 |
| GolfKitHero_Male | Run | 0 / 0.0 | 0 / 0.0 | 2389 / 20.0 | 0.40 |
| GolfKitHero_Male | Walk | 0 / 0.0 | 0 / 0.0 | 1065 / 20.0 | 0.80 |
| GolfKitHero_Male | Wave | 0 / 0.0 | 0 / 0.0 | 945 / 20.0 | 0.30 |
| GolfKitHero_Female | Emote_Defeat | 0 / 0.0 | 0 / 0.0 | 2368 / 20.0 | 1.70 |
| GolfKitHero_Female | Emote_JumpJoy | 0 / 0.0 | 0 / 0.0 | 287 / 20.0 | 0.60 |
| GolfKitHero_Female | Emote_Slump | 0 / 0.0 | 0 / 0.0 | 2869 / 20.0 | 0.80 |
| GolfKitHero_Female | Emote_Stomp | 0 / 0.0 | 0 / 0.0 | 867 / 20.0 | 1.60 |
| GolfKitHero_Female | Emote_Thrust | 0 / 0.0 | 0 / 0.0 | 472 / 20.0 | 1.60 |
| GolfKitHero_Female | FistPump | 0 / 0.0 | 0 / 0.0 | 602 / 20.0 | 0.80 |
| GolfKitHero_Female | Intro_Pushups | 0 / 0.0 | 0 / 0.0 | 425 / 20.0 | 2.20 |
| GolfKitHero_Female | RunForward | 0 / 0.0 | 0 / 0.0 | 103 / 20.0 | 0.40 |
| GolfKitHero_Female | Walk | 0 / 0.0 | 0 / 0.0 | 394 / 20.0 | 0.70 |
| GolfKitHero_Male | Emote_Defeat | 0 / 0.0 | 0 / 0.0 | 1918 / 19.9 | 0.50 |
| GolfKitHero_Male | Emote_FistPump | 0 / 0.0 | 0 / 0.0 | 1601 / 19.9 | 0.20 |
| GolfKitHero_Male | Emote_Stomp | 0 / 0.0 | 0 / 0.0 | 1740 / 19.9 | 1.70 |
| GolfKitHero_Male | FistPump | 0 / 0.0 | 0 / 0.0 | 1499 / 19.9 | 1.20 |
| GolfKitHero_Male | Intro_BringIt | 0 / 0.0 | 0 / 0.0 | 3171 / 19.9 | 0.30 |
| GolfKitHero_Male | RunForward | 0 / 0.0 | 0 / 0.0 | 900 / 19.9 | 0.30 |
| GolfKitHero_Female | Emote_Scuba | 0 / 0.0 | 0 / 0.0 | 604 / 19.9 | 0.10 |
| GolfKitHero_Female | Emote_Shock | 0 / 0.0 | 0 / 0.0 | 600 / 19.9 | 0.70 |
| GolfKitHero_Female | Emote_Victory | 0 / 0.0 | 0 / 0.0 | 585 / 19.9 | 2.10 |
| GolfKitHero_Female | Idle | 0 / 0.0 | 0 / 0.0 | 525 / 19.9 | 1.30 |
| GolfKitHero_Female | Run | 0 / 0.0 | 0 / 0.0 | 705 / 19.9 | 0.20 |
| GolfKitHero_Female | Wave | 0 / 0.0 | 0 / 0.0 | 525 / 19.9 | 0.80 |
| GolfKitHero_Female | Emote_Spike | 0 / 0.0 | 0 / 0.0 | 360 / 19.8 | 0.50 |
| GolfKitHero_Female | Intro_Wave | 0 / 0.0 | 0 / 0.0 | 598 / 19.8 | 0.60 |
| GolfKitHero_Female | Emote_FistPump | 0 / 0.0 | 0 / 0.0 | 265 / 19.5 | 0.20 |
| GolfKitHero_Female | Intro_BringIt | 0 / 0.0 | 0 / 0.0 | 450 / 19.5 | 2.70 |
| GolfKitHero_Female | Cheer | 0 / 0.0 | 0 / 0.0 | 462 / 19.3 | 1.00 |

# Accessories clip check

Poses of every emote / intro / Walk / Run / swing clip (10 samples a second) with the accessories on; Unity skinning (BakeMesh). Depths in mm, search radius 20 mm.
Columns: SKIN = skin vertices inside the accessory wall; CLOTH = garment vertices inside it; ACC-IN-BODY = accessory vertices inside the body (a glove's own hand excluded).

## Acc_Bag

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 26 | 26 | 20.0 mm (GolfKitHero_Male Emote_Defeat) |
| idle / celebrate / other | 8 | 8 | 20.0 mm (GolfKitHero_Male FistPump) |
| swing / stroke | 10 | 10 | 20.0 mm (GolfKitHero_Male Drive) |
| walk / run | 6 | 6 | 19.9 mm (GolfKitHero_Male Run) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| GolfKitHero_Male | Drive | 1507 / 20.0 | 8626 / 20.0 | 2243 / 20.0 | 2.90 |
| GolfKitHero_Male | Emote_Defeat | 26 / 1.5 | 7387 / 18.9 | 847 / 20.0 | 2.90 |
| GolfKitHero_Male | Emote_Shock | 55 / 16.3 | 5315 / 20.0 | 640 / 19.8 | 2.00 |
| GolfKitHero_Male | Emote_Slump | 127 / 1.5 | 5795 / 20.0 | 1558 / 20.0 | 0.10 |
| GolfKitHero_Male | Emote_Thrust | 29 / 1.4 | 5108 / 18.2 | 719 / 20.0 | 1.80 |
| GolfKitHero_Male | FistPump | 312 / 19.0 | 7923 / 19.7 | 230 / 20.0 | 2.30 |
| GolfKitHero_Male | Intro_Pushups | 98 / 9.0 | 7412 / 18.9 | 1300 / 20.0 | 2.50 |
| GolfKitHero_Male | IronSwing | 1903 / 20.0 | 8897 / 20.0 | 1644 / 20.0 | 2.90 |
| GolfKitHero_Male | Putt | 516 / 15.9 | 5770 / 18.3 | 1064 / 20.0 | 0.30 |
| GolfKitHero_Female | Drive | 1298 / 20.0 | 15125 / 20.0 | 2730 / 20.0 | 2.80 |
| GolfKitHero_Female | Emote_Defeat | 14 / 1.3 | 11951 / 4.9 | 1014 / 20.0 | 0.10 |
| GolfKitHero_Female | Emote_FistPump | 13 / 1.5 | 6226 / 4.8 | 812 / 20.0 | 1.40 |
| GolfKitHero_Female | Emote_JumpJoy | 14 / 1.4 | 7991 / 4.8 | 789 / 20.0 | 1.90 |
| GolfKitHero_Female | Emote_Slump | 90 / 1.5 | 8862 / 4.8 | 1022 / 20.0 | 2.50 |
| GolfKitHero_Female | FistPump | 144 / 4.2 | 13795 / 4.9 | 1307 / 20.0 | 1.00 |
| GolfKitHero_Female | HalfSwing | 193 / 10.4 | 12449 / 20.0 | 948 / 20.0 | 2.80 |
| GolfKitHero_Female | Intro_Pushups | 20 / 1.5 | 11659 / 4.8 | 1175 / 20.0 | 0.20 |
| GolfKitHero_Female | IronSwing | 1587 / 20.0 | 15187 / 20.0 | 4388 / 20.0 | 3.30 |
| GolfKitHero_Male | Emote_FistPump | 20 / 1.4 | 3888 / 18.5 | 657 / 19.9 | 1.40 |
| GolfKitHero_Male | Emote_JumpJoy | 32 / 19.8 | 4853 / 19.1 | 735 / 19.9 | 1.90 |
| GolfKitHero_Male | Emote_Stomp | 23 / 1.4 | 5170 / 19.2 | 656 / 19.9 | 1.90 |
| GolfKitHero_Male | HalfSwing | 281 / 9.9 | 7012 / 18.7 | 879 / 19.9 | 0.60 |
| GolfKitHero_Male | Run | 80 / 1.5 | 1864 / 19.1 | 241 / 19.9 | 0.50 |
| GolfKitHero_Female | Emote_Scuba | 4 / 1.3 | 8073 / 4.8 | 262 / 19.9 | 0.00 |
| GolfKitHero_Female | Emote_Shock | 10 / 1.5 | 8685 / 4.8 | 680 / 19.9 | 1.90 |
| GolfKitHero_Female | Emote_Spike | 12 / 3.0 | 7658 / 7.5 | 290 / 19.9 | 0.00 |
| GolfKitHero_Female | Emote_Stomp | 13 / 1.4 | 8648 / 4.8 | 736 / 19.9 | 2.00 |
| GolfKitHero_Female | Emote_Thrust | 19 / 1.5 | 8467 / 19.4 | 550 / 19.9 | 1.90 |
| GolfKitHero_Female | Intro_BringIt | 6 / 1.3 | 12527 / 4.8 | 393 / 19.9 | 0.00 |
| GolfKitHero_Female | Intro_Wave | 11 / 1.4 | 8256 / 4.8 | 434 / 19.9 | 0.00 |
| GolfKitHero_Male | Emote_Scuba | 19 / 1.1 | 4736 / 18.3 | 264 / 19.8 | 0.00 |
| GolfKitHero_Male | Emote_Spike | 7 / 9.0 | 4748 / 18.7 | 192 / 19.8 | 0.00 |
| GolfKitHero_Male | Emote_Victory | 67 / 16.8 | 7595 / 18.8 | 644 / 19.8 | 3.00 |
| GolfKitHero_Male | Intro_BringIt | 20 / 1.1 | 7261 / 18.3 | 643 / 19.8 | 0.00 |
| GolfKitHero_Male | Intro_Wave | 6 / 1.1 | 4847 / 18.3 | 310 / 19.8 | 0.00 |
| GolfKitHero_Male | RunForward | 81 / 15.9 | 1618 / 18.6 | 224 / 19.8 | 0.30 |
| GolfKitHero_Female | Emote_Victory | 12 / 1.2 | 12364 / 4.9 | 683 / 19.8 | 3.00 |
| GolfKitHero_Male | Chip | 229 / 1.4 | 6203 / 18.4 | 328 / 19.3 | 1.00 |
| GolfKitHero_Male | Walk | 0 / 0.0 | 2919 / 18.7 | 2 / 0.0 | 0.90 |
| GolfKitHero_Male | Cheer | 32 / 12.7 | 4861 / 17.5 | 0 / 0.0 | -1.00 |

## Acc_Cap

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 26 | 2 | 9.4 mm (GolfKitHero_Female Emote_Shock) |
| idle / celebrate / other | 8 | 0 | 0.0 mm (GolfKitHero_Male Cheer) |
| swing / stroke | 10 | 0 | 0.0 mm (GolfKitHero_Male Chip) |
| walk / run | 6 | 0 | 0.0 mm (GolfKitHero_Male Run) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| GolfKitHero_Female | Emote_Shock | 290 / 1.9 | 0 / 0.0 | 619 / 9.4 | 1.10 |
| GolfKitHero_Male | Emote_Shock | 286 / 1.8 | 0 / 0.0 | 95 / 7.9 | 0.70 |

## Acc_GloveL

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 26 | 12 | 20.0 mm (GolfKitHero_Male Emote_Defeat) |
| idle / celebrate / other | 8 | 2 | 20.0 mm (GolfKitHero_Female Idle) |
| swing / stroke | 10 | 10 | 20.0 mm (GolfKitHero_Male Drive) |
| walk / run | 6 | 5 | 20.0 mm (GolfKitHero_Male Run) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| GolfKitHero_Male | Drive | 0 / 0.0 | 0 / 0.0 | 16884 / 20.0 | 2.50 |
| GolfKitHero_Male | Emote_Defeat | 0 / 0.0 | 285 / 20.0 | 388 / 20.0 | 0.30 |
| GolfKitHero_Male | Emote_JumpJoy | 0 / 0.0 | 23 / 18.4 | 433 / 20.0 | 0.20 |
| GolfKitHero_Male | Emote_Shock | 0 / 0.0 | 0 / 0.0 | 4618 / 20.0 | 0.30 |
| GolfKitHero_Male | Emote_Slump | 0 / 0.0 | 1068 / 20.0 | 3207 / 20.0 | 0.90 |
| GolfKitHero_Male | Emote_Spike | 0 / 0.0 | 393 / 20.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Emote_Stomp | 0 / 0.0 | 745 / 20.0 | 428 / 20.0 | 1.70 |
| GolfKitHero_Male | HalfSwing | 0 / 0.0 | 0 / 0.0 | 20082 / 20.0 | 2.10 |
| GolfKitHero_Male | IronSwing | 0 / 0.0 | 0 / 0.0 | 16138 / 20.0 | 2.50 |
| GolfKitHero_Male | Putt | 0 / 0.0 | 0 / 0.0 | 11267 / 20.0 | 1.20 |
| GolfKitHero_Male | Run | 0 / 0.0 | 1085 / 20.0 | 2712 / 20.0 | 0.30 |
| GolfKitHero_Male | RunForward | 0 / 0.0 | 328 / 20.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Walk | 0 / 0.0 | 425 / 20.0 | 39 / 20.0 | 0.80 |
| GolfKitHero_Female | Drive | 0 / 0.0 | 0 / 0.0 | 7081 / 20.0 | 2.50 |
| GolfKitHero_Female | Emote_Defeat | 0 / 0.0 | 513 / 20.0 | 6187 / 20.0 | 1.80 |
| GolfKitHero_Female | Emote_Shock | 0 / 0.0 | 0 / 0.0 | 1376 / 20.0 | 1.40 |
| GolfKitHero_Female | Emote_Slump | 0 / 0.0 | 2476 / 20.0 | 12285 / 20.0 | 0.50 |
| GolfKitHero_Female | Emote_Stomp | 0 / 0.0 | 1891 / 20.0 | 2198 / 20.0 | 0.80 |
| GolfKitHero_Female | HalfSwing | 0 / 0.0 | 0 / 0.0 | 18350 / 20.0 | 2.50 |
| GolfKitHero_Female | Idle | 0 / 0.0 | 1059 / 20.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | IronSwing | 0 / 0.0 | 0 / 0.0 | 5993 / 20.0 | 2.40 |
| GolfKitHero_Female | Run | 0 / 0.0 | 1458 / 20.0 | 4083 / 20.0 | 0.30 |
| GolfKitHero_Female | Walk | 0 / 0.0 | 854 / 20.0 | 1362 / 20.0 | 0.40 |
| GolfKitHero_Female | Wave | 0 / 0.0 | 727 / 20.0 | 0 / 0.0 | -1.00 |
| GolfKitHero_Female | Emote_Spike | 0 / 0.0 | 135 / 19.6 | 0 / 0.0 | -1.00 |
| GolfKitHero_Male | Chip | 0 / 0.0 | 0 / 0.0 | 12450 / 18.6 | 1.60 |
| GolfKitHero_Female | Chip | 0 / 0.0 | 0 / 0.0 | 14005 / 18.5 | 0.80 |
| GolfKitHero_Female | Putt | 0 / 0.0 | 0 / 0.0 | 11079 / 18.4 | 1.30 |
| GolfKitHero_Male | Emote_Scuba | 0 / 0.0 | 0 / 0.0 | 99 / 10.5 | 1.90 |

## BareHandL

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 26 | 26 | 20.0 mm (GolfKitHero_Male Emote_JumpJoy) |
| idle / celebrate / other | 8 | 8 | 20.0 mm (GolfKitHero_Male Cheer) |
| swing / stroke | 10 | 10 | 20.0 mm (GolfKitHero_Male Drive) |
| walk / run | 6 | 6 | 20.0 mm (GolfKitHero_Male Run) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| GolfKitHero_Male | Cheer | 0 / 0.0 | 0 / 0.0 | 1835 / 20.0 | 1.40 |
| GolfKitHero_Male | Drive | 0 / 0.0 | 0 / 0.0 | 13554 / 20.0 | 0.60 |
| GolfKitHero_Male | Emote_JumpJoy | 0 / 0.0 | 0 / 0.0 | 2062 / 20.0 | 1.10 |
| GolfKitHero_Male | Emote_Scuba | 0 / 0.0 | 0 / 0.0 | 3995 / 20.0 | 1.20 |
| GolfKitHero_Male | Emote_Shock | 0 / 0.0 | 0 / 0.0 | 4058 / 20.0 | 1.00 |
| GolfKitHero_Male | Emote_Slump | 0 / 0.0 | 0 / 0.0 | 4036 / 20.0 | 0.40 |
| GolfKitHero_Male | Emote_Spike | 0 / 0.0 | 0 / 0.0 | 2650 / 20.0 | 1.50 |
| GolfKitHero_Male | Emote_Thrust | 0 / 0.0 | 0 / 0.0 | 2940 / 20.0 | 0.20 |
| GolfKitHero_Male | Emote_Victory | 0 / 0.0 | 0 / 0.0 | 3716 / 20.0 | 0.90 |
| GolfKitHero_Male | HalfSwing | 0 / 0.0 | 0 / 0.0 | 13244 / 20.0 | 0.50 |
| GolfKitHero_Male | Idle | 0 / 0.0 | 0 / 0.0 | 947 / 20.0 | 0.40 |
| GolfKitHero_Male | Intro_Pushups | 0 / 0.0 | 0 / 0.0 | 1796 / 20.0 | 0.30 |
| GolfKitHero_Male | Intro_Wave | 0 / 0.0 | 0 / 0.0 | 3026 / 20.0 | 1.50 |
| GolfKitHero_Male | IronSwing | 0 / 0.0 | 0 / 0.0 | 13138 / 20.0 | 0.60 |
| GolfKitHero_Male | Putt | 0 / 0.0 | 0 / 0.0 | 10675 / 20.0 | 1.30 |
| GolfKitHero_Male | Run | 0 / 0.0 | 0 / 0.0 | 2389 / 20.0 | 0.40 |
| GolfKitHero_Male | Walk | 0 / 0.0 | 0 / 0.0 | 1065 / 20.0 | 0.80 |
| GolfKitHero_Male | Wave | 0 / 0.0 | 0 / 0.0 | 945 / 20.0 | 0.30 |
| GolfKitHero_Female | Chip | 0 / 0.0 | 0 / 0.0 | 5300 / 20.0 | 1.40 |
| GolfKitHero_Female | Emote_Defeat | 0 / 0.0 | 0 / 0.0 | 2368 / 20.0 | 1.70 |
| GolfKitHero_Female | Emote_JumpJoy | 0 / 0.0 | 0 / 0.0 | 287 / 20.0 | 0.60 |
| GolfKitHero_Female | Emote_Slump | 0 / 0.0 | 0 / 0.0 | 2869 / 20.0 | 0.80 |
| GolfKitHero_Female | Emote_Stomp | 0 / 0.0 | 0 / 0.0 | 867 / 20.0 | 1.60 |
| GolfKitHero_Female | Emote_Thrust | 0 / 0.0 | 0 / 0.0 | 472 / 20.0 | 1.60 |
| GolfKitHero_Female | FistPump | 0 / 0.0 | 0 / 0.0 | 602 / 20.0 | 0.80 |
| GolfKitHero_Female | Intro_Pushups | 0 / 0.0 | 0 / 0.0 | 425 / 20.0 | 2.20 |
| GolfKitHero_Female | Putt | 0 / 0.0 | 0 / 0.0 | 4270 / 20.0 | 1.70 |
| GolfKitHero_Female | RunForward | 0 / 0.0 | 0 / 0.0 | 103 / 20.0 | 0.40 |
| GolfKitHero_Female | Walk | 0 / 0.0 | 0 / 0.0 | 394 / 20.0 | 0.70 |
| GolfKitHero_Male | Chip | 0 / 0.0 | 0 / 0.0 | 11554 / 19.9 | 1.90 |
| GolfKitHero_Male | Emote_Defeat | 0 / 0.0 | 0 / 0.0 | 1918 / 19.9 | 0.50 |
| GolfKitHero_Male | Emote_FistPump | 0 / 0.0 | 0 / 0.0 | 1601 / 19.9 | 0.20 |
| GolfKitHero_Male | Emote_Stomp | 0 / 0.0 | 0 / 0.0 | 1740 / 19.9 | 1.70 |
| GolfKitHero_Male | FistPump | 0 / 0.0 | 0 / 0.0 | 1499 / 19.9 | 1.20 |
| GolfKitHero_Male | Intro_BringIt | 0 / 0.0 | 0 / 0.0 | 3171 / 19.9 | 0.30 |
| GolfKitHero_Male | RunForward | 0 / 0.0 | 0 / 0.0 | 900 / 19.9 | 0.30 |
| GolfKitHero_Female | Drive | 0 / 0.0 | 0 / 0.0 | 2840 / 19.9 | 2.80 |
| GolfKitHero_Female | Emote_Scuba | 0 / 0.0 | 0 / 0.0 | 604 / 19.9 | 0.10 |
| GolfKitHero_Female | Emote_Shock | 0 / 0.0 | 0 / 0.0 | 600 / 19.9 | 0.70 |
| GolfKitHero_Female | Emote_Victory | 0 / 0.0 | 0 / 0.0 | 585 / 19.9 | 2.10 |

# Accessories clip check

Poses of every emote / intro / Walk / Run / swing clip (10 samples a second) with the accessories on; Unity skinning (BakeMesh). Depths in mm, search radius 20 mm.
Columns: SKIN = skin vertices inside the accessory wall; CLOTH = garment vertices inside it; ACC-IN-BODY = accessory vertices inside the body (a glove's own hand excluded).

## Acc_Cap

| group | clips (hero x clip) | with contact >= 1.5 mm | worst |
|---|---|---|---|
| emote / intro | 40 | 5 | 19.6 mm (MatchHero_Female EmoteSpike) |
| idle / celebrate / other | 10 | 0 | 0.2 mm (MatchHero_Male MatchLose) |
| swing / stroke | 4 | 2 | 10.3 mm (MatchHero_Male MissWhiff) |
| walk / run | 10 | 0 | 0.0 mm (MatchHero_Male Run) |

| hero | clip | skin in acc (n / mm) | cloth in acc (n / mm) | acc in body (n / mm) | worst t |
|---|---|---|---|---|---|
| MatchHero_Female | EmoteSpike | 21 / 2.2 | 0 / 0.0 | 112 / 19.6 | 0.50 |
| MatchHero_Male | EmoteShock | 311 / 1.8 | 0 / 0.0 | 2137 / 10.3 | 1.10 |
| MatchHero_Male | Emote_Shock | 311 / 1.8 | 0 / 0.0 | 2137 / 10.3 | 1.10 |
| MatchHero_Male | MissWhiff | 311 / 1.8 | 0 / 0.0 | 2137 / 10.3 | 1.10 |
| MatchHero_Female | EmoteShock | 294 / 1.9 | 0 / 0.0 | 769 / 10.0 | 0.40 |
| MatchHero_Female | Emote_Shock | 294 / 1.9 | 0 / 0.0 | 769 / 10.0 | 0.40 |
| MatchHero_Female | MissWhiff | 294 / 1.9 | 0 / 0.0 | 769 / 10.0 | 0.40 |

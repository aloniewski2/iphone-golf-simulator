# Plan 1 — TennisFluidity_GroundHitBlends: diagnosis + result (2026-09-26)

Status: **review candidate. Stopped for Adnan's approval. Plan 2 not started.**

## Step 0 — Hero V4 wiring: already done (previous phase)
- `TennisHeroSetup.cs` attaches `Resources/Tennis/Hero/Hero_01_Tennis.prefab` to both `TennisActor`s. The hidden actor stays the gameplay authority.
- `HeroTennisDriver.cs` plays the approved `Hero_*_v5` clips through a manual PlayableGraph (no Animator Controller). Stroke clips are time-locked to the gameplay contact.
- Racket: `Hero_Racket_GripSocket` under Hand.R. Root motion off. No CharacterController or Rigidbody.
- The locked V4 look and the approved clip files are unchanged this phase.

## Root causes found

| Issue | Root cause | File |
|---|---|---|
| P0 feet sink | Blend weights summed below 1 (min 0.17). Unity's humanoid mixer fills the missing weight with the muscle-zero pose, which drops the hips. The clips themselves also sit 3–6 cm low on this rig. There was no ground clamp. | `HeroTennisDriver.cs` |
| P0 serve desync | `LaunchServe` teleported the ball onto the hidden actor's strings from up to 1.2 m away and never judged the gap. The toss was paced to the hidden actor's racket, not the hero's. The pose was stale at launch. The swing started early, so contact came before the toss peaked. | `TennisGame.cs` |
| P1 skating runs | Run rate used an unmeasured 3.2 m/s reference; the clips' planted-foot speed is measured at about 5.8 m/s. There was no reverse run when backing up and no plant lock. | `HeroTennisDriver.cs` |
| P1 fake rally contacts | FX and SFX were already on release (`ReturnBall`), not on input. But release never checked that the visible strings met the ball. Movement aimed a fixed 0.65 m wing, while the hero's backhand contact is only 0.26 m out. | `TennisGame.cs`, `TennisActor.cs` |
| P1 arm through body | Blends and the contact-assist nudge moved the arms with no torso check. The two-hander's top hand was dragged into the belly. | `HeroTennisDriver.cs` |
| P1 serve "torso twist" | **Driver bug:** on every other frame, a held prepare was treated as a finished stroke's follow-through and cancelled. The body flickered between the side-on serve stance and front-on Ready at 15 Hz (558 one-frame flips per match). The same bug affected groundstroke takebacks. | `HeroTennisDriver.cs` |
| P1 post-whiff freeze | Not reproduced. Miss_Whiff (1.8 s) plays out into Ready or the next point. | — |
| P2 AI after point | Not reproduced. The rival goes Celebrate/Sad → Run → Ready and plays every next point (the old serve fault loop was fixed with rival serve pacing). | `TennisGame.cs` |

## Measured result (same 2700-frame self-play match, 30 fps; baseline = `HERO_BASELINE=1`)

| Metric | Before | After |
|---|---|---|
| Frames with feet >1 cm below court (player / rival) | 1570 / 1298 | **0 / 0** |
| Foot slide while standing (median) | 0.83 m/s | **0.05 m/s** |
| Foot slide while moving (median) | 3.98 m/s | **2.17 m/s** |
| One-frame state flicker | 558 | **0** |
| Player visible string-to-ball gap at contact (mean / max) | 0.20 / 0.35 m | **0.12 / 0.19 m** |
| Contacts released without a real meet | ungated | **gated at 0.18 m**: 1 honest miss in 35 contacts |
| Arm in torso >1 cm (player / rival) | 298 / 435 | **114 / 72**, all on backhand frames |
| Arm in torso on whiff / run-stop / Ready / Run | 218 | **0** |

## Still open
- **Backhand arm residual (114 frames):** it is in the approved 2HBH clip itself. The off forearm crosses the chest at the finish; the runtime capsule proxy reads about 1 cm (isolated clip, t = 1.5 s, L = 0.104). The Blender mesh check read 0. Left untouched because the clip is sacred.
- **Rival contact gaps** are still larger (mean 0.17 m, max 0.37 m). The rival is not gated by the honest rule, to avoid changing AI difficulty.
- **Moving foot slide p90 is about 8 m/s** on direction changes. It needs a stride-matched turn blend, which is Plan 2 feel work.
- **Smash:** its end pose reads high on the proxy at t = 3.3 s. It never triggered in this capture, so it's unverified in a match.
- Editor Play Mode only; not tested on iPhone.

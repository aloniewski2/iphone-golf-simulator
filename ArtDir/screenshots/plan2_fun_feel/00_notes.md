# Plan 2 — TennisFunFeel_JuiceAndFairness (2026-09-27)

**Status:** review candidate. Stopped for Adnan's approval; Plan 3 not started.

**Capture:** `ArtDir/anims/captures/Plan2_FunFeel.mp4` (20 s, mute-readable captions).

**Stills (this folder):**
- `01` routine hit
- `02` perfect beat
- `03` whiff pop
- `04a` / `04b` input (no FX) vs contact (FX)
- `05` AI honest contact

## What changed (under `Unity/Assets/Scripts/Tennis/`)
- **`TennisJuice.cs` (new):** big-moment beats for a perfect, a smash and a whiff only.
  - Each beat is a partial push-in toward the player, fov −6 to −9, a short slow-mo (0.5–0.6×, 0.22–0.45 s), then a settle back through the normal camera smoothing.
  - A plain perfect gets the camera at most once per 8 s; a supercharged perfect, a smash or a whiff always does.
  - Slow-mo never overrides the app's pause and always restores timeScale 1.
- **`TennisFx.cs`:**
  - Routine contact gets a solid short punch: impact, felt and a 0.18 shake.
  - A perfect adds a star burst, a second shockwave and a flash.
  - A smash gets the biggest tier.
  - New `Whiff()`: an air-swish ring, dust from the stumble, sparkles and a 0.5 shake. That's more than a routine hit.
- **`TennisSounds.cs`:** a synthesized body thump under every hit, a perfect sting (chime and bass drop), a smash crack, a whiff swish with a falling slide whistle, and a duller rival pop.
- **`TennisGame.cs`:**
  - The swing whoosh on button-down is removed (rally swing, serve commit, deferred serve). Sound fires only on contact or on the called whiff.
  - Contact tiers and hit-stop: a smash gets 0.08 s; perfect and supercharged hits keep the existing values.
  - Whiff juice, including a "WHIFF!" banner.
  - Camera beats are layered in `UpdateCamera`.
  - The AI's contact is honest: if its visible strings are more than 0.26 m from the ball, it's a rival whiff (FX and gasp), not a hit from thin air. The contact FX, the new `OpponentStruck` event and the rival pop fire only on a real meet.
  - `AutoPlayTimingJitter` is an editor-proof option only; it defaults to 0, so normal self-play is unchanged.
- **`TennisRules.cs`:** more generous connect windows (see tuning below).
- **`HeroTennisDriver.cs`:**
  - A racket trail lights only after a confirmed contact (0.2 s, gold 0.32 s on a perfect) or a called whiff.
  - The rival's reach aims at the live ball near contact.
  - A knee-bend reach for low balls: hips drop up to 0.34 m with both legs re-solved, so the feet stay planted. This applies to both players.

## Tuning notes (fairness: only ever easier than Plan 1)
- **Timing window:** ±0.20 → ±0.24 s at low power, ±0.12 → ±0.15 s at full power.
- **Reach forgiveness:** 1.45 → 1.6 at low power, 0.95 → 1.05 at full power.
- **Honest-contact gap (player):** 0.18 → 0.22 m.
- **Visible reach assist:** arm 0.45 → 0.50 m, body 0.25 → 0.30 m, plus the knee bend.
- **Placement:** unchanged. Aim still comes from the racket face, and the existing landing ring and aim cues are untouched.

## Measured (3600-frame match; self-play with human-like timing jitter)
| Check | Result |
|---|---|
| Player grades | Good 6 · Great 8 (2 supercharged) · Excellent 10 (2 supercharged) · Perfect 14 (4 supercharged) |
| Player honest misses | 0 |
| Camera beats | Perfect beats cover 242 of 3600 frames (6.7%); routine hits never move the camera; slow-mo on 53 frames, minimum 0.55× |
| AI contact gap | mean 0.13 m, max 0.28 m |
| Point outcomes | rival whiffed a jammed low ball at its feet · rival shot out · rival netted · player whiffed (injected) |
| Plan 1 locks | feet below court 0/0 · racket through body 0 · emote frames 0 · one-frame flicker 1 |
| Known residual | backhand-contact arm residual, 67 frames, under 1 cm per sample |

## Open
- No smash happened in this capture: self-play never set up an overhead. The smash tier and beat are coded and have been checked in code only.
- Audio isn't audible in the mp4 (the capture tool is muted). The stingers are synthesized in `TennisSounds.cs`.

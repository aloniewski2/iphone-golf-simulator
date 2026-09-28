# Plan 2B — CinematicSpectacle_Ultimate (2026-09-27)

**Status:** review candidate. Stopped for Adnan's approval. No Plan 3 custom emotes.

**Capture:** `ArtDir/anims/captures/Plan2B_CinematicSpectacle.mp4` (25 s, the game camera with the in-frame overlay). Stills are in this folder.

## What changed (under `Unity/Assets/Scripts/Tennis/`)
- **`TennisJuice.cs` (rewritten as the spectacle director):**
  - **Named camera cuts during the hit-stop:**
    - PERFECT: low front-side angle with a Dutch tilt, looking up at the face, racket and ball.
    - SMASH: tight low side angle, then a ball track.
    - WHIFF: comedy crash-zoom (fov 46 → 24) with a blue fail vignette.
    - Each has speed lines, a vignette and a flash, a title card, and slow motion (0.08–0.55×), then snaps back to the play camera.
    - A plain perfect takes the camera at most once every 8 s (between those it gets an FOV punch). Supercharged hits, smashes, whiffs and ultimates always cut.
  - **Serve drama:** a good toss (accuracy ≥ 0.8) switches to a toss cam from behind the knees. A perfect serve adds a contact close-up, then a 0.4 s ball track, then the play camera.
  - **Reaction cam** after every point: winner (1.15 s), then loser (1.15 s).
  - **Ultimate cinematic (2.3 s):**
    - Freeze (timeScale 0) → dark void, flash and title "ULTIMATE · <name> · SUPERNOVA SHOT".
    - An orbit of about 200° around the hitter with a Dutch sweep, showing character, racket and outfit.
    - A look down the ball's line, then release at 0.35× → 1× with the hyper ball trail.
  - **RIVAL CAM:** a live inset (320 px render texture) of the far player, shown while the ball is on its side and 0.6 s after its contact.
  - **Rally FOV breath:** an approach pull, an impact punch, and a mild look bias toward the rival while the ball is on its side.
  - **Timing:** the spectacle clock is frame-exact in capture runs. Slow-mo and freezes never override the app's pause.
- **`TennisGame.cs`:**
  - Camera override hook, snap-back after each cut.
  - Toss cam and perfect-serve drama.
  - The point-over timer goes from 1.5 s to 2.4 s to fit the reaction cam and auto emotes.
  - Equal ultimate meters with the hyper trail.
  - Swings are ignored during the ultimate freeze.
  - The AI finds an ultimate as hard to return as a supercharged ball.
- **`HeroTennisDriver.cs`:** point-end auto emotes are back on as temporary placeholders.

## Ultimate charge rules (equal access, no shop or cosmetics)
- **Charging:** every real, honest contact charges the hitter's own meter.
  - Player, by grade: Perfect 12%, Excellent 9%, Great 7%, Good 5%.
  - AI: a flat 7.5%, about the average of those rates.
- **Firing:** when the meter is full, the hitter's next real contact becomes the ultimate (for the player, only if it grades Good or better). There's no extra button.
- **Effect (the same for both sides):**
  - The ball is 1.3× faster (capped at 34 m/s) with a hyper trail.
  - For the AI, returning it counts as a fully struck ball at top pace.
  - The player returning the AI's ultimate faces the same 1.3× ball.
- **Honesty:** the contact is honest. The ultimate fires only after the strings have met the ball.
- **HUD:** "ULTIMATE" and "RIVAL ULTIMATE" bars, which blink "READY!" when full.

## Auto emotes wired (temporary placeholders, automatic, no picker)
| Situation | Emote |
|---|---|
| Point winner (alternating) | Celebrate (racket-up jump and fist pump), or Perfect fist-pump |
| Point loser (alternating) | Sad slump and head shake, or Facepalm (the MatchLose clip) |
| Match won / lost | MatchWin / MatchLose |

The next serve cuts them off at about 2.4 s.

## Camera notes
- **Screen share over a 4200-frame match:**
  - play camera 75.6%
  - hit cuts 6.2%
  - ultimates 8.2%
  - reactions 6.6%
  - toss cam 3.1%
- **Rival readability:** a fixed wide camera can't make the far player large without losing yours; the best framing tested got 54–68 px tall vs 38 px, and the player falls off screen. So readability comes from two things:
  - the RIVAL CAM inset on every rival shot;
  - the look bias while the ball is on the rival's side.

## Locks
| Check | Result |
|---|---|
| Feet below court | 0 / 0 |
| Racket through body | 1 frame |
| Player honest misses | 0 |
| Player contact gap | mean 0.11 m, max 0.15 m |
| Rival contact gap | mean 0.14 m, max 0.29 m |
| Contact-locked FX | unchanged |
| Auto emotes | every point: 4 of 4 |

## Open
- **Smash:** its cut is coded but was never triggered, because self-play doesn't play overheads.
- **Audio:** stingers exist but the capture is silent.
- **Ultimate frequency:** in these long self-play rallies an ultimate lands about every 28 s. The meter gains are the knob if that's too often.

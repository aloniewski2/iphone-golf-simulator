# 34 — Game Presentation Research: loading screen → venue → match → exit

Party Sports (mobile tennis + golf) · research brief · Oct 8, 2026 (rev 2)

**Scope:** Everything from the moment the **loading screen** appears up to the moment the player **leaves the match**:
- the loading screen (what shows while Unity loads, the VS/venue card, tips, hero pose, load masking, the ready handoff)
- the venue reveal (tennis court reveal, golf course flyover, a hole card for each hole)
- character walk-ons and nameplates at the venue
- the match start beat (serve-ready, tee address)
- in-match point and shot reactions
- beats between games and between holes
- the match-end celebration and results card
- the exit transition out of the match

**Out of scope:** menus, the hub, locker/customization, lobby UX. Multiplayer only enters this brief as "all players ready → loading screen."

**Status:** Research and recommendations only. It doesn't change game code. Timings are design targets, not measurements from our build.

---

## 0. TL;DR

1. **The loading screen is the first beat of the match, not dead time.** The best games use it as a venue or VS card that shows *where* you're playing and *who* you're playing. Examples: Clash Royale's ~2 s VS banner, Brawl Stars' VS screen, Valorant's 10-agent load card, and Splatoon's stage/mode card. It only lasts as long as loading really takes.
2. **Order after loading: place → people → go.** First the venue reveal (court sweep or hole flyover with a hole card), then the walk-ons and nameplates, then one clear go signal ("Ready… Serve!" or the address camera at the tee). Mario Kart, Mario Golf and TopSpin all follow this order.
3. **Long once, short after that, and any input skips.** Mario Kart's ~15 s course flyover skips on any input. EA FC split its intro into a ~20 s pre-match cinematic and an opt-in ~3 min full intro after players skipped the long one and missed information. PGA Tour lets you switch off the hole flyover. **Skips must land in a ready-to-play state.**
4. **Hide the loading, but never add extra time.** Supercell cut Brawl Stars loading from ~35 s to 5–6 s. The 2.8 s logo then outlasted the load and players reported it as a bug. Show progress honestly, and end the card when the game is ready.
5. **Sound comes slightly before the picture.** Swells and whooshes start 100–300 ms before each cut, and every card or UI movement has a sound. "Juice It or Lose It" (GDC) shows that sound is the cheapest feel upgrade.
6. **Reactions should scale with how much is at stake.** Point ≈1 s, game ≈2.5 s, match ≈6 s and skippable. Golf rewards scale with the result (birdie > par > bogey; hole-in-one is the big moment). This is the Mario Tennis Aces and PGA Tour "Big Hit / Heartbeat" pattern.
7. **What makes it feel premium vs cheap:** smooth camera moves, short holds on faces, the hero lit like the venue, a clean handoff from presentation camera to gameplay camera, and consistent timing that doesn't wobble with load hitches.
8. **For Party Sports:** eight beats from L1 (Loading / VS card) to L8 (Exit), each with time targets for the first view and repeats, skip rules, and proof captures. See §3.

---

## 1. Reference study (loading screen and onward only)

The sources are primary where possible: Nintendo, Supercell, EA, 2K and GDC. Durations marked "~" are approximate, taken from common gameplay footage rather than official figures.

### 1.1 Loading and VS screens

**Clash Royale (Supercell).** A VS splash with both players' names, trophies, clan and **Battle Banners** (a cosmetic) is shown for ~2 s while the arena loads. It can't be skipped, so both players start in sync. Then the arena appears with the towers already standing, and the elixir bar starts.
- Sources: [RoyaleAPI](https://royaleapi.com/blog/summer-2022-update?lang=en) · [Battle Banners](https://clashroyale.fandom.com/wiki/Battle_Banners)
- Lessons: identity plus a cosmetic flex covers the load, and the timing is identical for every player.

**Brawl Stars (Supercell).** A VS screen shows both teams' brawlers and names, then the map, a short countdown, and play. Supercell's loading write-up adds three points:
- Load time went from ~35 s to 5–6 s on old Android, and from ~7 s to ~2.5 s on newer iOS.
- The 2.8 s logo then outlasted the load, players filed bug reports, and the logo now shows only **once per day**.
- After loads got faster, players played more often in shorter bursts. The engineer put it this way: "You can't really play on a toilet break" when loading takes 35 s.
- Source: [Supercell — How Titan made Brawl Stars toilet-break friendly](https://supercell.com/en/news/titan-game-engine/)

**Valorant (Riot).** After agent select, the map loading screen shows **all 10 players as agent cards with names**, plus the map name and art. A per-player loading indicator shows who you're waiting on.
- Source for the agent-select timer: [OpenSourceSports](https://opensourcesports.io/rules/valorant-riot-games/rules-of-play-map-veto-agent-select)
- Lesson: in multiplayer, show *who* is loading so a wait has a cause.

**Splatoon 2/3 (Nintendo).** Once a match is found, a card shows the **mode and stage** ("Turf War · stage name") with team colors while the stage loads. Then players appear on spawners, the announcer calls out the teams, and "Ready? GO!" plays. In Splatoon 3, players **pick their landing spot** as they launch from the spawner.
- Sources: [Inkipedia — Spawner](https://splatoonwiki.org/wiki/Spawner) · [Grand Splatlands Bowl (announcer calls)](https://splatoonwiki.org/wiki/Grand_Splatlands_Bowl) · [Turf War](https://splatoonwiki.org/wiki/Turf_War)
- Lesson: the card tells you the rules and place in one glance, and the first moment of play is a choice.

**Mario Kart 8 / Deluxe.** Loading is short and branded. The course intro flyover (~15 s) then plays and **skips on a tap or the accelerator**. Lakitu's 3-2-1-GO follows, and holding the accelerator on "2" gives a Rocket Start.
- Sources: [MK8D race start](https://www.wikihow-fun.com/Begin-a-Mario-Kart-8-Deluxe-Race-in-vs.-Mode) · [Course intro trailers (Gematsu)](https://www.gematsu.com/2014/05/mario-kart-8-course-introduction-trailers)
- Lesson: the venue gets the long beat, but it skips instantly, and the countdown itself is gameplay.

**Rocket League.** After the load, a ~3 s kickoff countdown (3-2-1-GO) plays with cars placed symmetrically. Players **hold throttle and boost** through it. The same reset happens after every goal (goal explosion → skippable replay → kickoff).
- Sources: [Dignitas — Kickoffs](https://dignitas.gg/articles/blogs/rocket-league/12642/take-your-rocket-league-gameplay-to-the-next-level-kickoffs) · [Countdown cadence (broadcast tool)](https://github.com/jarrettabello/rocket-league-broadcast-studio) · [RLCS "Hype Chamber" matchup staging](https://www.unrealengine.com/spotlights/enter-the-rocket-league-hype-chamber-a-new-sample-for-broadcast-and-live-events)
- Lesson: the reset beat is short, identical every time, and something you can act during.

**Nintendo Switch Sports.** The developers avoided a pro-stadium feel ("suddenly brought out in front of a large audience… you would not feel comfortable") and went for a casual, stylish venue with "a sense of continuity." Music plays from **in-world speakers by each court**, kept underneath the hit sounds. Sportsmates needed **650+ motions vs ~30** for Wii Sports Miis, which shows how much character animation costs.
- Source: [Nintendo — Ask the Developer Vol. 5 Part 3](https://www.nintendo.com/us/whatsnew/ask-the-developer-vol-5-nintendo-switch-sports-part-3/)
- Lesson: venue music and crowd should feel friendly, not like a stadium.

**Ways to hide loading inside the world.** Animal Crossing hides loading behind a flight and landing at the airport, and Destiny behind a ship-landing transition. Bungie's stated aim is "no UI, progress bars, loading screens."
- Sources: [The Verge — ACNH](https://www.theverge.com/2020/3/20/21188006/animal-crossing-new-horizons-design-interview-aya-kyogoku-hisashi-nogami) · [VG247 — Destiny](https://www.vg247.com/destiny-match-making-public-areas-and-social-interaction-discussed-by-bungie)
- Lesson: a short "travel" motion can stand in for a load.

**Loading-screen design rules** (Game Developer, Bromley, Indieklem, and a loading-interface taxonomy):
- Always show progress. A looping animation is fine for short waits (~2–10 s); beyond that, use a determinate progress indicator (~>4–10 s).
- Don't cut off text or audio the player is reading or hearing.
- Keep content in-world and on-brand (tips, stats, practice).
- People in waiting mode overestimate waits by ~36%, so keep them "active."
- Sources: [Game Developer — Game Design Rules: Loading Screens](https://www.gamedeveloper.com/design/game-design-rules-loading-screens) · [Steve Bromley — great loading screens](https://www.stevebromley.com/blog/2010/02/22/improving-the-player-experience-how-to-make-great-loading-screens/) · [Indieklem — fake faster loading](https://indieklem.com/14-how-to-fake-faster-loading-times-with-design-in-your-video-games/) · [Loading interface taxonomy](https://www.academia.edu/103667307/A_Proposed_Taxonomy_for_the_Design_Qualities_of_Video_Game_Loading_Interfaces_and_Processes)

### 1.2 Venue reveal and hole intros

**Mario Golf: Super Rush.** Each hole opens with an overview of the layout and hazards, then cuts to the golfer at the tee. Before the swing, the player can call up the **overhead view (X)** and the **range/elevation finder (R)** whenever they like.
- Source: [Nintendo — Planning your shot](https://www.nintendo.com/us/whatsnew/planning-your-shot-in-mario-golf-super-rush/)
- Lesson: a golf hole reveal gives the player information, so people tolerate it. It should also be replayable on demand.

**EA Sports PGA Tour.** The official settings include **Hole Preview Flyover** (on/off, played at the start of every hole), **Big Hit Moments** (special presentation on big tee shots) and **Heartbeat Moments** (tension cams on shots into the green). PGA Tour 2K23 removed flyovers and offered scout cam and auto-replay toggles instead. Players complain about rival-cam cutaways ("Takes like half the time to play a round" with them off).
- Sources: [EA — PGA Tour visual settings](https://www.ea.com/able/resources/ea-sports-pga-tour/ea-sports-pga-tour/xbx/visual) · [VideoGamer — 2K23 replays](https://www.videogamer.com/news/pga-tour-2k23-how-to-turn-off-replays/) · [r/pga2k23 thread](https://www.reddit.com/r/pga2k23/comments/yewmho/turning_off_rival_camera/) · [Steam thread (flyovers removed)](https://steamcommunity.com/app/1588010/discussions/0/4288063042476907238/)
- Lesson: every recurring cinematic needs an off switch, and cutaways to other players' shots get resented.

**Golf Clash (mobile).** Uses hole flyovers and markets course flyovers. On mobile, the aim view arrives quickly.
- Source: [Golf Clash TPC flyover](https://www.gamereactor.se/video/635993/Golf+Clash+-+The+Players+Championship+Flyover+Trailer/)

### 1.3 Character walk-ons and nameplates

**TopSpin 2K25.** A tunnel walk-out onto the court, player introductions with crowd cheers ("Ladies and gentlemen, welcome to tennis paradise"), then the match. The official page: "take a moment to get in the mindset of a tennis pro during player introductions, before making your grand entrance." A Tennis.com reviewer notes that custom players "look awkward during the walk-out animation and in between points."
- Sources: [2K — Centre Court Report: Gameplay](https://2k.com/games/topspin-2k/topspin-2k25/centre-court-report/gameplay/) · [Gameplay showcase video](https://www.youtube.com/watch?v=mE9PAKaIBxQ) · [Tennis.com review](https://www.tennis.com/news/articles/review-plenty-to-be-excited-topspin-2k25-successfully-recreates-joy-frustration-tennis-video-game-playstation-xbox-pc)
- Lesson: walk-ons make every flaw in hero quality obvious.

**Mario Tennis Aces.** Each character gets an entrance/intro animation (~3–5 s), short win and loss reactions after points (~1–2 s, press-through), and longer reactions after games and sets.
- Sources: [My Nintendo News](https://mynintendonews.com/2018/06/14/character-animations-in-mario-tennis-aces-show-off-their-unique-personalities/) · [All intros compilation](https://www.youtube.com/watch?v=rUNDrvk-LjM)

**EA Sports FC 24–26.** FC 24 shortened its intros because "players tended to skip the extended intros, and in the process sometimes miss important information." Fans pushed back, and FC 25 brought back full intros as opt-in. FC 26 has **Watch Full Match Intro: Always / Opt-in / Never**: a ~20 s pre-match cinematic by default, a ~3 min full intro on opt-in, scrolling line-ups shown when skipping, and a Hold-to-Skip toggle.
- Sources: [Charlie INTEL — why FC 24 trimmed intros](https://www.charlieintel.com/ea-sports-fc/ea-fc-24-devs-explain-why-matchday-intros-were-removed-from-career-mode-279997/) · [FC 26 settings](https://fifauteam.com/fc-26-game-settings/) · [Operation Sports (FC 25 return)](https://forums.operationsports.com/forums/forum/soccer/ea-sports-fc-and-fifa/946294-unofficial-presentation-tidbit-on-fc-25)
- Lesson: keep a short default, let fans opt into the long version, and **move critical info (line-ups) into the short version** so skippers don't miss it.

**NBA 2K26.** Pregame warm-up animations, arena PA voices, venue-specific details, and commentary whose tone shifts in close games vs blowouts. 2K23's broadcast package had a dedicated style and motion system for overlays and the scorebug.
- Sources: [NBA 2K26 — Presentation](https://nba.2k.com/2k26/courtside-report/presentation/) · [PlayStation Blog](https://blog.playstation.com/2025/08/06/nba-2k26-captures-authentic-nba-presentation-with-new-improvements/) · [Jason Rasmussen — 2K23 broadcast](https://jasonjrasmussen.com/nba2k23-broadcast)
- Lesson: one consistent motion language for every overlay, plus audio intensity that rises with what's at stake.

**Splatoon.** The team callout happens as players come out of the spawners, so you meet the characters while they're already doing something. (Sources in §1.1.)

### 1.4 In-match reactions and the end of the match

**Overwatch.** Play of the Game is a ~5 s highlight with an animator-designed intro, and Highlight Intros and Victory Poses are unlockable cosmetics. Blizzard spent ~1.5–2 weeks of animation per hero on personality pieces.
- Sources: [GDC 2016 talk summary](https://videohighlight.com/v/lHevkQIZL2M) · [Icy Veins](https://www.icy-veins.com/forums/topic/15891-everything-you-need-to-know-about-the-return-of-the-overwatch-beta/)
- Lesson: the post-match moment can double as a reward slot.

**Rocket League.** The goal explosion (a cosmetic) and a skippable replay, then a reset.

**Mario Tennis Aces.** Reactions after points are tiny; larger moments come after games, sets and matches.

**PGA Tour.** Big Hit and Heartbeat moments are saved for high-stakes shots, and every one can be switched off.

### 1.5 Cross-cutting craft
- **GDC "Juice It or Lose It" (Jonasson & Purho):** tweening, squash and stretch, particles, screen shake, and above all sound. Their demo makes two circles that pass through each other look like they bounce off each other, using only a sound. [GDC Vault](https://gdcvault.com/play/1016789/Juice-It-or-Lose) · [video](https://www.youtube.com/watch?v=Fy0aCDmgnxg)
- **GDC "Juicing Your Cameras With Math" (Eiserloh):** framing, smoothed motion, shake. [GDC Vault](https://gdcvault.com/play/1023146/Math-for-Game-Programmers-Juicing)
- **GDC "Punching Up the Juice with Proactive Audio" (Robinson).** [GDC Vault](https://gdcvault.com/play/1023406/Punching-Up-the-Juice-with)
- **Mobile time-to-fun:** skippable intros that land in a playable state, and no artificial padding. [Udonis FTUE](https://www.blog.udonis.co/mobile-marketing/mobile-games/first-time-user-experience)
- **iOS thermal:** at `ProcessInfo.thermalState` `.serious`, lower graphics and frame rate; at `.critical`, do the minimum. [Apple — thermalState](https://developer.apple.com/documentation/foundation/processinfo/thermalstate-swift.property) · [Apple — respond to thermal state](https://developer.apple.com/library/archive/documentation/Performance/Conceptual/power_efficiency_guidelines_osx/RespondToThermalStateChanges.html)

### 1.6 Comparison table (loading → match)

| Title | Loading screen | Venue reveal | Character intro | Go signal | Skip / options |
|---|---|---|---|---|---|
| Clash Royale | VS + banners ~2 s | Arena already standing | Names/banners on VS | Elixir starts | Not skippable (sync) |
| Brawl Stars | VS teams | Map shown | Brawlers on VS | Short countdown | Logo once/day |
| Valorant | 10 agent cards + map | Spawn in buy phase | Cards on load | Barrier drop | n/a |
| Splatoon | Stage + mode card | Brief stage view | Team callout on spawners | "Ready? GO!" + pick landing | n/a |
| Mario Kart 8 | Short branded load | ~15 s flyover | Racers on grid | 3-2-1-GO (rocket start) | Any input skips flyover |
| Rocket League | Arena load | Arena | Cars in kickoff spots | 3-2-1-GO (hold boost) | Replays skippable |
| Mario Golf SR | Load | Hole overview | Golfer at tee | Address cam | Overview on demand |
| PGA Tour (EA) | Load | Hole Preview Flyover | Pre-shot anims | Address cam | Flyover / Big Hit / Heartbeat toggles |
| TopSpin 2K25 | Load | Venue establishing | Tunnel walk-out + PA intro | First serve | Skippable cinematics |
| EA FC 26 | Load | ~20 s pre-match cinematic | Walk-outs / line-ups | Kickoff | Always / Opt-in / Never full intro |
| Switch Sports | Short | Court establishing | Sportsmate | Serve | Fast by default |

---

## 2. Principles (loading screen onward)

1. **The loading card is beat zero.** It shows the venue, the mode and the people. Draw it the instant the player commits, with no black frame in between.
2. **Show honest progress.** For a wait under ~2 s, only the card and its motion. From ~2–10 s, add a looping in-world animation (palm sway, ball bounce). Past ~10 s, add a determinate progress indicator and, in multiplayer, *who* is still loading.
3. **Never add time on purpose.** End the card at its next natural cut once the game is ready. The only exceptions are a minimum read time for identity text (≥1.2 s solo, ≥1.5 s multiplayer) and never cutting a tip mid-read.
4. **One tip, in context, short enough to read.** Tennis tips during tennis loads, a course-specific tip during golf loads. Rotate only if the load runs past ~5 s.
5. **Place → people → go.** Venue reveal, then walk-ons and nameplates, then a single go signal. The go moment should already use the gameplay camera.
6. **One signature move per beat.** A court sweep, a bounce-in landing, or a team-color wipe — one per beat, not three.
7. **Sound slightly before the picture.** Lead each cut with a 100–300 ms swell or whoosh, land hit sounds on the impact frame, and give every card or overlay a sound.
8. **Long once, short after that, any input skips.** The full version runs on the first view for each venue or hole; repeats use the short cut. Any tap or swing skips to the ready state. Player settings: Intros **Full / Short / Off** and Hole Flyover **On / Off** (PGA pattern).
9. **Move critical info into the short version.** Names, hole number, par and yardage must appear even when skipping (the EA FC lesson).
10. **Let the player act during the beat.** Allow a practice swing during the serve-ready or address beat, and keep the overview replayable.
11. **Reactions scale with stakes.** Point < game < set/match. Golf: bogey < par < birdie < eagle < hole-in-one.
12. **No uncanny face holds.** Keep face-forward framing under 0.8 s and always moving. Readable silhouettes beat close-ups on phones.
13. **The hero must match the venue's lighting** (same key and ambient light, contact shadow, post-processing), or walk-ons look pasted on. TopSpin's reviewer called this out.
14. **Clean camera handoff:** the last presentation frame equals the first gameplay frame, or an eased blend of ≥0.25 s.
15. **The cartoon style rules:** squash/stretch landings, springy cards, bright bursts, pops and whistles. No lens flares, handheld shake or long slow-mo.
16. **Mobile:** tap anywhere to skip, a hard cap per beat, and short cuts forced automatically when `thermalState >= .serious`.

### Premium vs cheap
| Premium | Cheap |
|---|---|
| Card shows the venue, people and mode immediately | Black screen, spinner, "Loading…" |
| Progress you can trust; card ends when ready | Fake bar, padded wait, logo outlasting the load |
| Sound slightly ahead of each cut; every overlay moves with sound | Silent overlays, music hard-cuts |
| Eased camera, one subject | Linear pans, drifting framing |
| Hero lit by the venue, contact shadow | Floating, studio-lit hero |
| Skip lands ready to play | Skip lands on black/loading |
| Reaction length scales with stakes | Same long reaction every point |

---

## 3. Party Sports: loading → match → exit system

### 3.0 Known context (product knowledge, not re-verified in the repo for this brief)
- Bouncy cartoon heroes PlayerMale and PlayerFemale.
- Tennis and golf. The phone acts as the racket or club, with a TV/Mac connect mode. Game Center multiplayer.
- Existing hero animations: Intro_Wave, BringIt, Pushups (intros); Scuba and Thrust (flourishes).
- Postcard-style courses: resort tennis court, and golf holes with landmarks such as the lighthouse, sea stacks and crater.
- Current visual gaps: post-processing isn't actually active yet, heroes read as pasted on, and the face work is still converging on the references.

### 3.1 System design
- **One `PresentationDirector`** (concept name) plays the named beats L1–L8. Each beat defines:
  - `fullCut` (first view), `shortCut` (repeat), `skipFrom` (earliest skip), and `readyState` (where a skip lands)
  - `audioCues`, each with a lead offset
  - `camera` (separate phone and TV framing), `heroAnim`, and `overlay`
- **Seen-flags** are tracked per beat × venue/hole × app version. A flag is set only after the full cut *finishes*.
- **Skip input:** any tap on the phone, A on the TV remote, or a swing gesture. Skips always land in `readyState`.
- **Multiplayer:** shared beats always use `shortCut`, can't be skipped by one player, and have hard caps. Clients sync at every beat boundary.
- **Thermal and render tier:** when `thermalState >= .serious` or on the low render tier, short cuts are forced and the flyover LOD is lowered.
- **Phone vs TV framing:** every shot has two framings. On the phone, the hero fills at least 35% of frame height in hero shots, and cards anchor to readable mid and lower areas. On TV, use 16:9 with a 5% title-safe margin and lower-third nameplates.
- **Audio:** a "stinger" bus ducks music about 6 dB for 0.5 s, and stingers lead the picture by ~150 ms. Venue music plays from in-world speakers (the Switch Sports approach).

### 3.2 Beat table

| # | Beat | Full (first view) | Short (repeat / multiplayer) | Skip rule |
|---|---|---|---|---|
| L1 | Loading screen: venue / VS card | = load time (min 1.2 s solo / 1.5 s MP) | Same | Not skippable (it is the load); ends when ready |
| L2 | Venue reveal (tennis court) / course + Hole 1 flyover (golf) | Tennis 3.0 s · Golf 4.0 s | 1.2 s · 1.5 s | Any input after 0.3 s → L3/L4; Golf flyover setting On/Off |
| L3 | Walk-ons + nameplates | Tennis 3.0 s · Golf 1.5 s | 1.5 s · 0.8 s | Any input after 0.3 s → L4 (nameplates still shown on scoreboard) |
| L4 | Match start: tennis serve-ready / golf tee address + hole card | 1.2 s | 0.8 s | Control returns at end; practice swing allowed during |
| L5 | In-match reactions (point / shot) | Point 1.2 s · Ace 1.5 s · Golf by result (0.6–1.5 s; HIO 4.0 s) | Point 0.8 s · Ace 1.0 s · Golf 0.5–1.0 s (HIO 3.0 s) | Any input / next swing skips; HIO skippable after 1.0 s |
| L6 | Between games / holes | Game 2.5 s · Set 4.0 s · Hole-out + scorecard 2.5 s · Next-hole flyover 3.0 s | 1.5 s · 2.5 s · 1.5 s · 1.5 s | Any input after 0.5 s; flyover setting applies |
| L7 | Match end celebration + results | 6.0 s | 3.0 s | Skippable after 1.5 s; results card waits for input |
| L8 | Exit transition | 1.0 s | 0.8 s | Not skippable (it's already the exit) |

**Time from end of load to control:**
- **Tennis:** first view 7.2 s (L2 3.0 + L3 3.0 + L4 1.2); repeat or multiplayer 3.5 s.
- **Golf:** first view 6.5 s (L2 4.0 + L3 1.5 + L4 1.0, with the address camera taking ~1.0 s); repeat 2.9 s.
- **Intros Off:** under 1 s for both (one 0.5 s wipe straight into L4).

#### L1. Loading screen: venue / VS card (beat zero)
- **Trigger:** The player commits to tennis or golf (solo), or all players are ready in multiplayer. Nothing upstream of that is in scope.
- **First frame:** The card must appear **on the very next frame**, with no black frame. If Unity starts or loads its scene at match time, the native layer draws the card instantly and keeps it up until Unity reports the scene is ready and its first frame has rendered. It then crossfades (≈0.25 s) into Unity's first frame. Unity's own splash or startup must never be the first thing the player sees.
- **Layout:**
  - **Background:** a postcard render of the venue (court or Hole 1 landmark) with a slow parallax drift (≤3% scale over 5 s) so it's never static.
  - **VS / roster:**
    - Solo: your hero portrait on the left, with the opponent (CPU) or "Practice" on the right.
    - Multiplayer: up to 4 hero portraits with Game Center name/avatar, each with a **per-player loading tick** (Valorant pattern).
    - Portraits are renders of each player's current outfit in a signature pose (Intro_Wave or BringIt frame), cached whenever the outfit changes. If no render exists, use a clean silhouette in team color, never a broken or default skin.
  - **Mode line:** "Tennis · Singles · Best of 3 · Sunset Court" or "Golf · 9 Holes · Stroke Play · Lighthouse Links".
  - **One tip:** tennis or golf specific, and course-specific where possible ("Hole 3 plays into the wind: club up"). Readable for ≥1.5 s; rotate only after 5 s.
  - **Progress:** under 2 s, the card's own motion only. From 2–10 s, a ball bounce / spinning racket loop in the corner. Past 10 s, a determinate bar plus "Waiting for Sam…" in multiplayer.
- **Audio:** the venue ambience (ocean, gulls, crowd murmur) fades in under the card. A short "card in" swoosh plays on appear and a "ready" chime on completion.
- **Ready handoff:**
  - When loaded, the progress element turns into a ✓ "Ready!" pop (0.3 s), then a team-color iris wipe into L2.
  - In multiplayer, the handoff waits for **all clients**. Past 20 s, offer "Keep waiting / Leave match" (exit via L8).
- **Never pad:** apart from the minimum read time (1.2 s solo / 1.5 s multiplayer, so names are readable), the card ends at the next cut once the game is ready. Brand or logo stings play at most once a day (Supercell lesson).
- **Proof:**
  - `work/presentation/L1_loading_{solo,mp2,mp4}_{phone,tv}.mp4`
  - `L1_load_timing.json`, recording the timestamp of card-first-frame, Unity scene ready, Unity first frame, and wipe start; the gap between Unity ready and wipe start must be ≤ 0.35 s
  - one still per device showing no black frame between commit and card

#### L2. Venue reveal
- **Tennis court reveal:**
  - Full, 3.0 s: a swell starts 150 ms before the wipe ends. Then a single eased crane shot from high behind the stands, down over the net to court level, framing palms and ocean. A **venue name card** slides into the lower third with a whoosh ("Sunset Court · Island Sports Club"), with crowd murmur rising.
  - Short, 1.2 s: the same crane, compressed, with the name card only on the first view per session.
- **Golf course and Hole 1 flyover:**
  - Full, 4.0 s: a fast eased flight from the green (flag flapping) back over the hazards to the tee, deliberately framing that hole's **signature landmark** (lighthouse, sea stacks, crater rim).
  - The **hole card** builds during the flight: Hole 1 · Par 4 · 412 yd · wind arrow · course name.
  - Short, 1.5 s: the green, then a cut to the tee, with the hole card.
  - The flyover is information, so it's replayable on demand with an Overview button at address (Mario Golf X pattern).
- **Skip:** after 0.3 s, any input jumps to L3 or L4. The **hole card always stays up** through the start of L4 so skippers still get par and yardage (EA FC lesson).
- **Proof:** `work/presentation/L2_{tennis_reveal,golf_flyover}_{full,short,skip}_{phone,tv}.mp4` plus one still per hole showing the hole card is legible on phone.

#### L3. Character walk-ons and nameplates
- **Tennis, full 3.0 s:**
  - Each player gets ~1.2 s: a walk or hop into the baseline position and a walk-on emote (Intro_Wave, BringIt or Pushups), with a **nameplate** in the lower third on phone or TV (name, Game Center avatar, small flag or level). Crowd cheer plus a name sting for each player.
  - Then a 0.6 s two-shot across the net.
  - The camera always moves (slow dolly or orbit). Face-forward framing stays ≤0.8 s; mostly full-body or 3/4 framing.
- **Tennis, short 1.5 s:** a single two-shot with both heroes doing a quick flourish at the same time and both nameplates in.
- **Golf, full 1.5 s / short 0.8 s:** the hero walks up to the tee and does one move (club twirl or tee-up), with a nameplate.
  - With 2–4 golfers, the order/turn strip appears instead of separate walk-ons (no one waits through other people's intros).
- **Rules:**
  - The hero uses the venue's lighting, with a contact shadow and post-processing.
  - Use hop/bounce transitions instead of walks with sliding feet if locomotion isn't ready.
  - Equipped walk-on emotes can slot in later as cosmetics (Overwatch / Rocket League pattern).
- **Skip:** after 0.3 s, any input goes to L4. The nameplates then live on the scoreboard and turn strip.
- **Proof:** `work/presentation/L3_walkons_{tennis_1v1,tennis_2v2,golf_1p,golf_4p}_{full,short,skip}_{phone,tv}.mp4` and a frame strip at 0.25 s steps showing face holds ≤0.8 s.

#### L4. Match start
- **Tennis serve-ready, 1.2 s full / 0.8 s short:**
  - The camera blends (eased, ≥0.25 s) into the serve camera, and the scoreboard springs in (0–0, server dot, both names).
  - "Ready…" (crowd hush) then "Serve!" (umpire call or whistle) as the go signal, with a haptic tick on the phone at each word.
  - The server may swing from the "Serve!" frame. Practice swings during "Ready…" are allowed and shown as a ghost swing (Rocket League hold-boost idea).
- **Golf tee address, 1.0 s full / 0.6 s short:**
  - The address camera settles, then the aim line and the shrunken hole card appear, along with the club chip.
  - Control returns at the end. The Overview button replays the L2 flyover on demand.
- **Proof:** `work/presentation/L4_{serve_ready,tee_address}_{phone,tv}.mp4` and a camera-handoff check: position/FOV delta between the last presentation frame and the first gameplay frame is <5%.

#### L5. In-match reactions (tiny by default, bigger only for rare moments)
- **Tennis:**
  - **Point won:** 1.2 s full / 0.8 s short. A small fist pump with a crowd reaction leading by 100 ms, and the score ticks on the scoreboard with a pop. No camera cut, or at most a 0.3 s punch-in.
  - **Point lost:** a 0.8 s shrug or racket tap. Never humiliating.
  - **Ace or great winner:** 1.5 s / 1.0 s. One 0.4 s slow-mo at 0.5×, then a "NICE!" pop card, then back.
  - **Hard rule:** the time from point end until the server can swing is ≤1.5 s.
- **Golf (scaled to result, shown after the ball comes to rest):**

  | Result | Full | Short |
  |---|---|---|
  | Bogey or worse | 0.6 s shrug | 0.5 s |
  | Par | 0.8 s nod and clap | 0.5 s |
  | Birdie | 1.5 s cheer and sparkle | 1.0 s |
  | Eagle | 2.0 s | 1.2 s |
  | Hole-in-one | 4.0 s celebration | 3.0 s |

  - Hole-in-one is skippable after 1.0 s.
  - A big tee shot or a long putt on its way in can get a 1-shot tension cam, PGA's Big Hit / Heartbeat idea. Limit it to one per hole and add a setting to switch it off.
  - **Never cut away to other players' shots** in multiplayer (PGA rival-cam complaint). Show their result as a toast instead.
- **Skip:** any input, or the next swing.
- **Proof:** `work/presentation/L5_{point_win,point_loss,ace,golf_par,golf_birdie,golf_hio}_{phone,tv}.mp4` plus `L5_point_to_serve.json` showing the maximum gap ≤1.5 s across a 20-point sample.

#### L6. Between games and between holes
- **Tennis:**
  - **Game won:** 2.5 s / 1.5 s. A short cut to the game winner doing BringIt or a mini-dance, and the game score card springs.
  - **Set won:** 4.0 s / 2.5 s. A bigger flourish, with the set score card showing both names.
  - **Changeover:** if ends switch, use a 0.6 s wipe instead of showing players walking around.
- **Golf:**
  - **Hole-out + scorecard:** 2.5 s / 1.5 s. The scorecard row fills (stroke count, name, ± par), with a pop per player.
  - **Next hole:** the L2 flyover for that hole (3.0 s full on first view, 1.5 s short, or skipped entirely if Flyover is Off), with its hole card.
  - **Mid-round loading:** if a hole needs streaming, use the same L1 card style in a mini version (hole card plus landmark), with no padding.
- **Skip:** any input after 0.5 s goes straight to the next L4.
- **Proof:** `work/presentation/L6_{game,set,hole_out,next_hole}_{full,short,skip}_{phone,tv}.mp4`.

#### L7. Match end celebration and results
- **Full 6.0 s:**
  1. The final-point or final-putt stinger.
  2. The winner's victory animation, which can later be an unlockable victory pose (Pushups, Scuba, Thrust).
  3. A friendly reaction from the other player (a clap, never humiliating).
  4. A handshake-at-the-net or tee two-shot.
  5. The **results card** springs in: final score, a couple of stats (aces, longest drive, putts), and XP.
- **Short 3.0 s:** the winner's pose (1.5 s), then the results card.
- **Skip:** skippable after 1.5 s. The results card **waits for input**, so players can read it. It shows exactly two actions: Rematch and Leave (they trigger L1 again or L8; anything beyond that is menus and out of scope).
- **Proof:** `work/presentation/L7_match_end_{win,loss,mp}_{full,short,skip}_{phone,tv}.mp4`.

#### L8. Exit transition
- **Full 1.0 s / short 0.8 s:** the venue music ducks, then a team-color iris wipe (reverse of L1's wipe) with a soft whoosh into whatever comes next. The next screen must be ready, or the native layer holds a still frame of the venue under the wipe. Never black, and never Unity's teardown visible.
- **Rematch:** goes straight to L1 using the **short** versions of L2 and L3.
- **Proof:** `work/presentation/L8_exit_{leave,rematch}_{phone,tv}.mp4` plus a frame check showing no black frame.

### 3.3 First view vs repeat rules
- **Full versions** (L2, L3, L6 flyover) play the first time per venue or hole and after each major content update. After that, the short versions play.
- **Settings:** Intros **Full / Short / Off** and Hole Flyover **On / Off**. "Off" keeps L1, L4, L5, L7-short and L8, so transitions never hard-cut.
- **Multiplayer:** always short versions, with no individual skips during shared beats L2/L3 and hard caps instead. L5 reactions are local to each player.
- **Tutorial match:** L2 and L3 play once in full, then the tutorial prompts take over. Never stack a presentation beat on top of tutorial prompts.

### 3.4 Pitfalls (loading screen onward)
1. **Black frames or Unity's splash between commit and card.** The native card must cover all of Unity's startup and scene load, and stay up until Unity's first rendered frame.
2. **Padding or fake progress.** Don't make the card longer than the load (beyond the 1.2/1.5 s read minimum), and don't show a progress bar that lies.
3. **Pasted-on heroes in walk-ons.** Walk-ons and reactions are close and slow, so they magnify any lighting mismatch. Unify the hero's key and ambient light with the venue, add a contact shadow at the feet, and remove camera-side fill. TopSpin's walk-outs were criticized for exactly this.
4. **No post-processing yet.** Flyovers over clipped highlights and muted turf will look worse than gameplay. **Don't capture or tune presentation beats until post-processing and the turf palette pass are in.**
5. **Faces.** Until the face gates pass, keep walk-ons full-body or 3/4 with face moments ≤0.8 s.
6. **Phone vs TV framing.** Wide establishing shots make the known problems worse (tennis ~40% sky, golf hero ~27% of frame height). Every shot needs both framings and a minimum hero size.
7. **Blocking play.** Point end to serve-ready must stay ≤1.5 s, golf reactions are capped, and no cutaways to rival shots.
8. **Multiplayer desync.** Use beat-boundary sync, hard caps, and per-player loading ticks, with a timeout that offers a way out via L8.
9. **Cosmetic portraits on the loading card.** A missing render must fall back to a silhouette, never a default or broken skin.
10. **Opt-in visual flags.** Capture all proofs **without** `VISUAL_CHARACTER_SKIN_POLISH` / `VISUAL_BLINK_NORMAL_FIELD`, because the phone never sets them.
11. **Thermal and draw calls.** Flyovers over whole holes can blow the 250 draw-call budget. Use the gameplay LOD/impostors, and force short cuts when the phone is under thermal pressure.

### 3.5 Implementation phases
1. **P0 (prerequisite, other briefs):** post-processing on, hero lighting unified with the venue, contact shadow.
2. **P1:** L1 loading card. Native instant card, Unity-ready handoff, per-player ticks, honest progress, timing log.
3. **P2:** `PresentationDirector` skeleton (seen-flags, skip → `readyState`, multiplayer caps, thermal fallback) plus L4 match start and L8 exit.
4. **P3:** L5 in-match reactions (the most frequent beat and the biggest feel payoff), plus the point-to-serve timing log.
5. **P4:** L2 venue reveal (tennis) and L2/L6 golf flyovers with hole cards.
6. **P5:** L3 walk-ons and nameplates.
7. **P6:** L6 between-games beats and L7 match end and results.
8. **P7:** Audio pass (stinger bus, lead offsets, in-world venue music) plus the phone/TV framing pass.
9. **P8:** Proof videos for every beat × full/short/skip × phone/TV. Adnan signs off.

### 3.6 Binary gates
- **G1:** No black frame between the commit and the L1 card, and none from L8 into the next screen (frame-checked, phone and TV).
- **G2:** The L1 card ends ≤0.35 s after Unity is ready (beyond the 1.2/1.5 s read minimum). No fake progress.
- **G3:** Every beat L1–L8 has full, short and skip captures on phone and TV saved under `work/presentation/`.
- **G4:** `beat_timings.json` shows every beat within budget ±0.2 s, tennis point-to-serve-ready ≤1.5 s, and load-end-to-control ≤7.2 s full / ≤3.5 s short (tennis) and ≤6.5 s / ≤2.9 s (golf).
- **G5:** Skips always land in `readyState`, and the hole card and nameplates are visible even after skipping.
- **G6:** No camera pop at the presentation → gameplay handoff (<5% position/FOV delta or an eased blend ≥0.25 s).
- **G7:** Hero ≥35% of frame height in phone hero shots, face-forward holds ≤0.8 s.
- **G8:** In multiplayer, all clients enter L2 and L4 within 300 ms of each other.
- **G9:** Captures were made with post-processing on and the opt-in visual flags off.
- **G10:** Adnan approves the videos. An agent PASS alone doesn't count.

---

## 4. Sources
- Supercell — How Titan made Brawl Stars toilet-break friendly: https://supercell.com/en/news/titan-game-engine/
- Game Developer — Game Design Rules: Loading Screens: https://www.gamedeveloper.com/design/game-design-rules-loading-screens
- Steve Bromley — How to make great loading screens: https://www.stevebromley.com/blog/2010/02/22/improving-the-player-experience-how-to-make-great-loading-screens/
- Indieklem — Fake faster loading times with design: https://indieklem.com/14-how-to-fake-faster-loading-times-with-design-in-your-video-games/
- Taxonomy of video game loading interfaces: https://www.academia.edu/103667307/A_Proposed_Taxonomy_for_the_Design_Qualities_of_Video_Game_Loading_Interfaces_and_Processes
- RoyaleAPI — Clash Royale Battle Banners: https://royaleapi.com/blog/summer-2022-update?lang=en · https://clashroyale.fandom.com/wiki/Battle_Banners
- Valorant agent select/load: https://opensourcesports.io/rules/valorant-riot-games/rules-of-play-map-veto-agent-select
- Inkipedia — Turf War / Spawner / Grand Splatlands Bowl: https://splatoonwiki.org/wiki/Turf_War · https://splatoonwiki.org/wiki/Spawner · https://splatoonwiki.org/wiki/Grand_Splatlands_Bowl
- Mario Kart 8 race start / course intros: https://www.wikihow-fun.com/Begin-a-Mario-Kart-8-Deluxe-Race-in-vs.-Mode · https://www.gematsu.com/2014/05/mario-kart-8-course-introduction-trailers
- Rocket League kickoff: https://dignitas.gg/articles/blogs/rocket-league/12642/take-your-rocket-league-gameplay-to-the-next-level-kickoffs · https://github.com/jarrettabello/rocket-league-broadcast-studio · https://www.unrealengine.com/spotlights/enter-the-rocket-league-hype-chamber-a-new-sample-for-broadcast-and-live-events
- Nintendo — Ask the Developer Vol. 5, Switch Sports Part 3: https://www.nintendo.com/us/whatsnew/ask-the-developer-vol-5-nintendo-switch-sports-part-3/
- Nintendo — Planning your shot in Mario Golf: Super Rush: https://www.nintendo.com/us/whatsnew/planning-your-shot-in-mario-golf-super-rush/
- EA — PGA Tour visual settings (Hole Preview Flyover, Big Hit, Heartbeat): https://www.ea.com/able/resources/ea-sports-pga-tour/ea-sports-pga-tour/xbx/visual
- PGA Tour 2K23 replays / rival cam / flyovers: https://www.videogamer.com/news/pga-tour-2k23-how-to-turn-off-replays/ · https://www.reddit.com/r/pga2k23/comments/yewmho/turning_off_rival_camera/ · https://steamcommunity.com/app/1588010/discussions/0/4288063042476907238/
- Golf Clash flyover: https://www.gamereactor.se/video/635993/Golf+Clash+-+The+Players+Championship+Flyover+Trailer/
- 2K — TopSpin 2K25 Centre Court Report (player intros): https://2k.com/games/topspin-2k/topspin-2k25/centre-court-report/gameplay/ · video https://www.youtube.com/watch?v=mE9PAKaIBxQ
- Tennis.com — TopSpin 2K25 review: https://www.tennis.com/news/articles/review-plenty-to-be-excited-topspin-2k25-successfully-recreates-joy-frustration-tennis-video-game-playstation-xbox-pc
- Mario Tennis Aces character animations: https://mynintendonews.com/2018/06/14/character-animations-in-mario-tennis-aces-show-off-their-unique-personalities/ · https://www.youtube.com/watch?v=rUNDrvk-LjM
- EA FC intros: https://www.charlieintel.com/ea-sports-fc/ea-fc-24-devs-explain-why-matchday-intros-were-removed-from-career-mode-279997/ · https://fifauteam.com/fc-26-game-settings/ · https://forums.operationsports.com/forums/forum/soccer/ea-sports-fc-and-fifa/946294-unofficial-presentation-tidbit-on-fc-25
- NBA 2K26 presentation: https://nba.2k.com/2k26/courtside-report/presentation/ · https://blog.playstation.com/2025/08/06/nba-2k26-captures-authentic-nba-presentation-with-new-improvements/ · https://jasonjrasmussen.com/nba2k23-broadcast
- Overwatch PotG / victory poses: https://videohighlight.com/v/lHevkQIZL2M · https://www.icy-veins.com/forums/topic/15891-everything-you-need-to-know-about-the-return-of-the-overwatch-beta/
- Load masking in the world: The Verge (ACNH) https://www.theverge.com/2020/3/20/21188006/animal-crossing-new-horizons-design-interview-aya-kyogoku-hisashi-nogami · VG247 (Destiny) https://www.vg247.com/destiny-match-making-public-areas-and-social-interaction-discussed-by-bungie
- GDC Vault — Juice It or Lose It: https://gdcvault.com/play/1016789/Juice-It-or-Lose (video https://www.youtube.com/watch?v=Fy0aCDmgnxg) · Juicing Your Cameras With Math: https://gdcvault.com/play/1023146/Math-for-Game-Programmers-Juicing · Proactive Audio: https://gdcvault.com/play/1023406/Punching-Up-the-Juice-with
- Mobile FTUE: https://www.blog.udonis.co/mobile-marketing/mobile-games/first-time-user-experience
- Apple — thermalState: https://developer.apple.com/documentation/foundation/processinfo/thermalstate-swift.property · https://developer.apple.com/library/archive/documentation/Performance/Conceptual/power_efficiency_guidelines_osx/RespondToThermalStateChanges.html

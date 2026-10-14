# PLAN — Immersive Menu: The Plaza

Status: **PLAN v2, 2026-10-10.** This replaces v1 (the flat "walk for where, cards for how" hub). Nothing is built yet.

Look target: `proof/menu-hub/concepts/round5_simple/03_simple_evening_plaza.png`, picked by Adnan, made **sport-neutral**.

Earlier rounds, kept for reference:
- `proof/menu-hub/concepts/` (rounds 1–5)
- `proof/menu-hub/examples/` (browser walk-through stills)

References and what each one gives:

| Game | Gives |
|---|---|
| Nintendo Switch Sports (Spocco Square) | Look: a bright sports complex with colour-coded landmarks |
| Splatoon 3 (Splatsville + lobby tower) | Structure: one compact plaza and one PLAY building |
| NBA 2K (MyPark / The City) | Social: party up, walk to the game together, sit and wait for "next" |
| Batman (Adnan's memory) | Glitch-style gameplay preview screens in the queue spot |

---

## 1. The experience in one paragraph

You load into **The Plaza**: an evening town square with a **LOCKER** room on the left, a **CLUBHOUSE** on the right, an emote stage in the middle and the **PLAY** hall straight ahead. Friends you invite appear in your plaza and walk around with you. Every door leads into a **real room**.

Inside PLAY there is a hall with one doorway per sport. Each sport room has **queue bays**: a bench, a floor ring and a big screen showing a glitchy preview of that mode.

Sit down and the game **starts loading while you sit**, with no loading screen. When the match is ready, the camera dives into the screen and you are on the court. When the match ends, you are back on the same bench. To play together, every friend sits in the same bay. The match loads when the last one sits down.

---

## 2. Places (all walkable)

All rooms live in **one Unity scene, `Hub.unity`**. The plaza is in the middle and the interiors are placed off-stage. A door is a 0.4 s walk-through: the camera follows you into the doorway, a quick fade, and you appear inside the room. So there is no scene load between rooms, and party members can see each other's room changes instantly.

| Place | What's there | Reuses |
|---|---|---|
| **The Plaza** (outdoors, ~30 m round) | Spawn point. LOCKER, CLUBHOUSE and PLAY doors. Emote stage. Photo-spot sculpture: the club crest, not a tennis ball. Lamp posts, planters, a sunset sky. Friends' arrival point. | Emote clips; `HeroTennisDriver` locomotion |
| **Locker Room** (~10 × 12 m) | A mirror wall framing your hero. Racks you walk up to, one per slot: Shirt, Shorts, Shoes, Racket, Club. A skin/look station. An emote rehearsal spot in front of the mirror. | `TennisMenu` locker logic, `LockerCatalog`, palette and swatches |
| **Clubhouse** (~10 × 12 m) | Lounge. Settings desk. "How to play" screen. Trophy shelf (campaign progress, records). Feedback mailbox. Invite board (Game Center / nearby). | `IslandSettingsScreen` rows, how-to content, beta feedback |
| **PLAY Hall** (~12 × 14 m) | Atrium with one archway per sport: **TENNIS** (blue), **GOLF** (green), and an empty third arch reading "coming soon" for later sports. A directory board. | — |
| **Tennis Room** | Queue bays: **Exhibition**, **Campaign**, **Training**, **Online**. A court view through glass. | `MenuLaunch`, `TennisVenueChoice` (Tropical Resort / Sky Tower / Magma Crater) |
| **Golf Room** | Queue bays: **Round** (course picker), **Online**, **Pass the phone**. A turf view. | `GolfCourseChoice` (Cliffside / Postcards / Wild Isles / Magma Open) |

Theme rule: the plaza and the hall are **club-branded and sport-neutral**. Each sport room carries its sport's look.

---

## 3. Queue bays (the heart of it)

A bay is where you choose and start a mode. Each bay has:

- **A screen:** a 6–10 s looping video preview of that mode on that venue, played through a **glitch shader** (scanlines, RGB split, block-shift on cuts) so it reads as a stylised memory rather than a plain video.
  - Changing the venue makes the screen glitch over to that venue's clip.
- **Seats:** benches matching the mode's capacity. Solo modes have 1 seat. Online modes have up to 4, which is the current lobby limit.
- **A floor ring** that fills while the game loads.

Flow at a bay:

1. **Walk in.** The screen plays its preview and the phone strip shows "Tennis · Exhibition · A to sit".
2. **Sit** (A). The hero plays SitDown, then SitIdle. The **phone becomes the mode panel**: venue, difficulty, sets/games and a big READY button. The TV stays clean, with only floating labels.
3. **READY:**
   - Solo: loading starts immediately.
   - Party: each friend who sits shows a ✓ on the bench and in the phone list. Loading starts when every party member is seated in this bay and has pressed READY, or when the host presses START.
4. **Loading while seated.** The floor ring fills and the screen's glitch calms down as the match gets closer. Nobody sees a loading screen.
5. **Go.** The camera pushes into the bay screen, the screen fills the TV, and it cuts to the match's own intro camera.
6. **Cancel** (B, or stand up). Loading stops and the scene is unloaded.

After the match, the results show as a card. Then you are **back on the same bench**: staying seated means Rematch, standing up means Leave. The hub is loaded back in the background while the results are on screen.

Preview videos:
- One clip per bay and venue: tennis 4 modes × 3 venues (some shared); golf 3 modes × 4 courses.
- Captured from **our own game** with the existing capture harness (`TennisVenueCapture`, golf flyover capture), staging signature moments: smash, ace, a long putt, a lava hazard carry.
- 640×360 H.264, about 1–1.5 MB each, played by Unity `VideoPlayer` into a RenderTexture.

---

## 4. Friends / party (MyPark-style, private instance)

- **Invite:** ☰ on the phone → Party → Invite (Game Center sheet or nearby). The Clubhouse invite board and a postbox in the plaza do the same thing.
- **Join:** an invited friend arrives at the plaza spawn with an arrival effect and a name tag in the party colour. Each phone and TV renders its own copy of the same plaza.
- **Free roam:** everyone walks, emotes, visits the Locker and changes outfits. The others see your new outfit live.
- **Play together:** everyone walks into the same sport room and sits in the same bay. The bench shows who is missing ("Waiting for Mia, Leo"). The host can **Call party**, which shows an arrow on everyone's TV pointing to the bay and makes their phone vibrate.
- **Leave:** a friend who leaves walks out of the plaza and vanishes. If the host leaves, the next player becomes host.

Under the hood this **reuses `MultiplayerService`** (GameKit `GKMatch` + nearby `NWConnection`, `MultiplayerPacket` v3, max 4 people). The party *is* the existing lobby. Its "lobby" phase becomes the walkable plaza instead of a menu. New packet kinds:

| Kind | Rate | Payload |
|---|---|---|
| `hubPresence` | 10 Hz, unreliable | position, yaw, speed, current room, animation state |
| `hubEvent` | reliable | emote, sit/stand, bay id, ready ✓, call party |
| `hubLook` | reliable, on change | loadout (colours, gear) |
| `partyState` | reliable, from the host | members, host, bay seats |

Starting a match goes through `MultiplayerService.launch` / `SportsSession.startMultiplayer`, with the config taken from the bay.

Not in this plan:
- **A public plaza full of strangers** (true MyPark). That needs the server to run hub instances. `server/` currently only does golf rooms and scores. This would be a later decision (**D5**).
- **Online tennis drops loadout colours** ([[lobby-hero-parity]]). Fix this in Phase 4, or friends will see different outfits in the plaza and in the match.

---

## 5. "No loading screen": how it works technically

Today `NativeSportsSession` uses `SceneManager.LoadSceneAsync("Tennis"|"Golf")` in **single** mode (`NativeSportsSession.cs:232`), which destroys whatever was loaded. The phone shows a loading cover meanwhile. New flow:

1. **Hub mode.** A `hub` session kind keeps `Hub.unity` loaded with the player's hero, camera and the networking feed.
2. **Pre-load at READY.** Call `LoadSceneAsync(sport, LoadSceneMode.Additive)` with `allowSceneActivation = false`. Progress up to 0.9 drives the floor ring.
3. **Activate hidden.** Activate the match scene with its cameras and audio disabled. Its venue builds (`TennisVenueBuilder`, golf course dressing) and `TennisGame.Initialized` comes true while the hub keeps rendering.
   - Any heavy build step gets **time-sliced** so the hub never hitches.
   - Shader warm-up runs here too. A known trap from earlier work: particle prewarm.
4. **Hand-off.** Camera push into the screen → enable the match camera and audio → unload the hub's interiors, or the whole hub if memory demands it.
5. **Online.** Every phone does steps 2–3. The match starts when every phone reports ready (the existing `MultiplayerPhase.loading`).
6. **Return.** During results, load the hub additively. Respawn on the bench.
7. **Fallback.** If the match isn't ready after 15 s, or there's an error, the bay screen shows "warming up…". The old loading cover is kept only for genuine failures.

**Biggest risk: peak memory** with the hub and the match loaded at the same time on an iPhone. Phase 3 measures it first. If it's too high, keep only a tiny "bay booth" (the bench, the screen and the hero) alive during the load and unload the rest of the hub.

---

## 6. Controls

| State | Phone shows | TV shows |
|---|---|---|
| **Explore** | Analog stick (golf `aimPad` look, with magnitude: short push walks, full push runs, held full sprints). Buttons: A, B, ☰, Emote, Party. "On the TV" strip. | The plaza or room. A prompt above the nearest door or bay. |
| **In a room station** (locker rack, settings desk) | That station's panel: swatches, toggles. Uses the existing `TennisMenu` rows. | The hero framed by the room camera. A small floating label. |
| **Seated in a bay** | Mode panel (venue, difficulty, sets) + READY / LEAVE SEAT + party seat list. | The bay screen preview, other seated players, the loading ring. |
| **Party** | Members, invite, call party, kick (host). | Name tags, an arrow to the host's bay when called. |

☰ is always the quick menu, a Destiny-style launcher: jump to any room or bay, or open the classic list menu. A **Classic menus** switch in Settings keeps the flat menu for anyone who wants it.

With no TV connected, the phone keeps today's `TennisPhoneMenu`. You can't play without a TV, so nothing is lost.

---

## 7. New assets

| Asset | Notes |
|---|---|
| Hub kit | Rounded pavilions (drum + band + sign), PLAY tower, lamp posts, planters, low-poly trees, plaza floor with a ring, emote stage, club-crest sculpture, benches, bay screens, door frames. About 25 modular pieces, simple flat materials + baked lighting. |
| Interiors | Locker, Clubhouse, PLAY Hall, Tennis Room, Golf Room, built from the same kit (walls, floors, racks, desks, glass). |
| Character clips | **New:** SitDown, SitIdle (loop), StandUp, DoorPush (optional), PointAt (call party). **Existing:** Walk, RunForward, Idle, emotes/intros (see [[serve-and-feet]], [[emote-overhaul-build]]). |
| Preview videos | 15–25 clips captured from the game, plus a glitch screen shader. |
| Club crest | A sport-neutral logo for the plaza and the PLAY tower (D6). |

---

## 8. Phases and binary gates

Proof for each phase goes in `proof/menu-hub/<phase>/`, with results in `GATE_RESULTS.md`. Device gates need the iPhone and a TV. Unity runs in an APFS clone.

| Phase | Builds | Gates (all must PASS) |
|---|---|---|
| **1 · Greybox walk** | `Hub.unity` greybox: plaza + 5 rooms, door transitions, `HubPlayer` (Idle/Walk/Run), `HubCamera`, phone `HubController` with analog stick, `hub` session kind. | HUB_BOOT (a TV connection opens the plaza); STICK_ANALOG (walk / run / sprint / stop ≤0.25 s); LATENCY ≤100 ms; FEET ≤20 % slide; DOORS (every door in and out ≤0.5 s, no hitch >33 ms); FPS 60 for a 10 min soak; NO_TV (old phone menu unchanged). |
| **2 · Rooms + stations** | Locker racks and mirror, Clubhouse desks, phone station panels on the existing `TennisMenu` rows, ☰ quick menu, Classic switch. | ROUTE_ALL (every old menu destination is reachable by walking or ☰ ≤3 presses); LOCKER_LIVE (a colour change shows on the hero ≤1 frame after the pick). |
| **3 · Bays + seamless load** | Sport rooms, bays, sit clips, preview videos + glitch shader, additive pre-load, hidden activation, screen-dive hand-off, return to bench. | NO_COVER (zero loading-cover frames in 20 solo launches); HITCH (no hub frame >50 ms while seated loading); MEM (peak hub+match ≤ the budget set from the device baseline); RETURN (respawn seated on the same bench); CANCEL (standing up mid-load leaves nothing loaded). |
| **4 · Party** | Presence/event/look packets, arrivals, shared bays, all-seated start, call party, host migration, online loadout colours. | SYNC (4 heroes at 60 fps, remote position error ≤0.3 m); ALL_SEATED (the match starts only when every member is seated and ready, or the host forces it); TOGETHER (all 4 land in the same match and return to the same bay); LOOK (match outfits = plaza outfits). |
| **5 · Art pass** | The evening-plaza look from concept 03: kit models, lighting, sky, club crest, interior dressing, bay screen polish. | READABLE at TV distance (art-bible check); LOOK_MATCH (an in-engine still vs the concept, side-by-side sign-off by Adnan). |
| **6 · Cleanup** | Delete dead menu states and legacy flow (v1 audit list), type the focus IDs, split `TennisMenu.swift`. | NO_DEAD_IDS; all existing menu tests pass. |

---

## 9. Audit facts this rests on (from v1)

- Unity renders only on the external TV (`SportsRuntime.mm:42`). The phone is always the controller. There is no Unity in the simulator.
- Walk and run clips plus speed-blended locomotion exist only in Unity (`HeroTennisDriver.cs`). The golf-kit hero has no walk or run, so the plaza hero is always the MatchHero (D3).
- The golf joystick (`GolfController.swift:344-394`) sends direction only, at 30 Hz, with no haptics. The hub stick needs magnitude.
- Menus have no continuous movement channel. `NativeSportsSession.Update` idles without a match.
- Launching = `MenuLaunch` + `TennisMenu.begin()`. Multiplayer = `MultiplayerService` (max 4, `MultiplayerPacket` v3).
- Dead menu code and string-ID drift: listed in the v1 audit (git history of this file).

---

## 10. Decisions for Adnan

- **D1** Build the hub in Unity on the TV. *(Recommended; assumed throughout.)*
- **D2** No TV: keep the old phone menu. *(Recommended.)*
- **D3** The plaza hero is always the MatchHero, wearing the player's loadout. *(Recommended.)*
- **D4** Mode options live on the **phone** while seated, and the TV stays immersive. *(Recommended.)* The alternative is cards on the TV.
- **D5** Party-only private plazas now; a public MyPark plaza with strangers later, on our own server. *(Recommended.)*
- **D6** Club name and crest for the sport-neutral plaza. *(Needs a name.)*
- **D7** Time of day: always evening (concept 03), or a day/evening cycle. *(Recommended: evening only for v1.)*

# Branch 10/13 — everything since the 10-11 merge (2fe849f0)

Base: `2fe849f0` (Party chat, voice, TV recording). `build-10/11`, `build-10/12` and `gameplay/hit-feel-responsiveness` all pointed at that commit; the work below was uncommitted in the main checkout (plus the `gameplay-feel` worktree) and is now one commit on `10/13`.

## What changed

| Area | What | Notes (details in the linked work folder / memory) |
|------|------|------------|
| **Clothes** | Clothing V2: product-built male polo (kit assets under `Unity/Assets/Characters/KitsV2`, `MatchHero_KitV2_*` textures, lobby `.ktx`) | `work/clothing-v2`, review in `ArtDir/hero/clothing-v2-review`. Awaiting Adnan's review; female polo/skirt, shorts, shoes not done |
| **Characters** | Bald-base passes 1+2: skin/blush, ears, nose/eyes/smile, sneaker shape, contact-AO face fix | `work/bald-base-2026-10-12` |
| | Jaw sculpt (3 passes) + 8 mm jaw radius in HeroSkinNormals | `work/jaw-sculpt-2026-10-12` |
| | Hero sync: lobby heroes = game heroes (export, install, goldens, freshness test) | `work/hero-sync`, `work/lobby-parity` |
| **Emotes / moves** | Standard emote kit: 7 emotion emotes + Walk/Run for both heroes, golf aim footwork (`GolferFootwork.cs`), in tennis, golf and plaza | `Unity/Assets/Resources/HeroMoves/`, `EmoteStandardWire.cs`, `work/emote-standard` (README = rollback), `work/anim-standard` |
| | Animation deformity breakdown (frame-by-frame review of all clips) | `work/emote-golf-compare`, findings in memory |
| **Golf swing** | Swing compare vs pros, then swing fix: straight lead arm, lag held, no head dive, 0.25 s downswing | `work/swing-compare`, `work/swing-fix` |
| **Golf course** | Read/wind/lie pass: no putt line, half-arc aim dots, grain chevrons, rare strong wind + HUD arrow, rough/sand cut distance + scatter, lie chip | `work/golf-read` (patch already applied) |
| | Intro flyovers: HoleIntro highlights, sticker IntroGraphics, 3 s per character, emote only if tapped, skip votes (online: all must skip) | `HoleIntro.cs`, `IntroLens.cs`, `IntroGraphics.cs`, `proof/intro-flyover` |
| **Menus / hub** | Hub country-club restyle (plaza, tennis, golf hub) | `work/hub-country-club`, `ArtDir/ui`, `ArtDir/ui/hub-targets-2026-10-12` |
| | Walkable plaza / menu hub proof and fixes | `proof/menu-hub`, `work/menu-hub` |
| **Online / social** | Friends lobby: parties over the Club Hub relay, phone is always the controller, benches = sport + mode (`HubRoomTransport.swift`) | `proof/friends-lobby`, `FriendsLobbyProofTests` |
| | Multiplayer = single-player parity: tennis VERSUS link (`TennisGame.Versus.cs`, peer packets, split = 2 shoulder views), golf flow parity | `PLAN_Multiplayer_SinglePlayerParity.md`, `proof/multiplayer-parity` |
| **Feel** | Hit-feel haptics (`GolfHitFeel.h`, `TennisHitFeel.cs`, TV-time cues, voice-chat haptics fix, phone knock off by default), 120 Hz input (`InputRate.cs`), swing prediction, iOS Game Mode, racket feel (`RacketFeel.swift`) | `PLAN_HitFeel_Haptics.md`, `GAMEPLAY_TEST.md`, `work/hit-feel`, `proof/hit-feel`. Merged in from the `gameplay-feel` worktree |
| **Housekeeping** | Phone build recipes, cleanup audit (read-only), pre-main-switch snapshot, review screenshots refreshed | `work/phone-build-*`, `CLEANUP_AUDIT_2026-10-10/` |

## Merge decisions (gameplay-feel worktree onto the main checkout)

- 28 of its changed files were already identical here (hit-feel and golf-read had been applied in both). 11 differed:
  - 6 merged cleanly.
  - 4 conflicts were only the audio-mute line (`MuteWhenAutomated.Volume`) and the `Versus` hit flag: kept this checkout's version, which contains both.
  - `SportsInfo.plist`: this checkout had **lost** `CFBundleDisplayName`, `NSGKFriendListUsageDescription` and `NSLocalNetworkUsageDescription`. Restored them and kept the Game Mode keys.
  - `project.pbxproj`: kept as is (see open items).
- Deliberately NOT taken: the gameplay-feel worktree's test-build signing (team `9VP5B3Q5VQ` in `project-unity.yml` / `ProjectSettings.asset`) and its `Gameplay Feel Test` scheme.

## Open items

1. **`GolfArcadeUnity.xcodeproj/project.pbxproj` is stale**: it does not list `RacketFeel.swift`, `HubRoomTransport.swift` or `FriendsLobbyProofTests.swift`. Regenerate through the phone-build recipe (clone, Unity export, xcodegen) before building.
2. Nothing was compiled or run on this branch; the merge is text-only. Several pieces report device gates NOT RUN (voice chat, hit feel, friends invites).
3. Large generated work output (tens of GB under `work/`: serve-walk-arm, golf-look, cloth-overhaul, …) was not committed; only scripts, docs and small proof images were.
4. Other worktrees untouched: `main-build` (old uncommitted blend/meta churn), `integrate-main` (clean), `emote-fluid-staging` (10-05, detached).
5. Old stashes on `final-build` / `newmapsandmenus` still exist.

## Second pass (10-13): Clothing V2 for all heroes, emote clipping audit

- Clothing V2 rebuilt for all 4 heroes: tennis polo/tank/skirt/shorts, golf kit, designed shoe last, tank straps + sewn binding, golf polo/trousers fixes (`Unity/Assets/Characters/KitsV2`, `work/clothing-v2`, `MatchHero_KitV2_*` lobby maps, lobby exports + goldens regenerated).
- Emote clipping audit (`work/emote-standard/CLIPPING_AUDIT.md`, 309 problems found, audit only).
- **Hero accessories (caps, glove, golf bag) are NOT in this branch**: the Unity code (`HeroAccessories*`, installers, resources, tests) and the five small hooks in `GolfGame`, `GolferView`, `HubPlayer`, `HubRemotes`, `MatchHeroLook` stay in the working tree only.
- Caveat: the Swift lobby exports (`CharacterAssets/*.json|lzfse`, `VisualGolden/*`) were exported with the accessories included and cannot be split without a re-export, so the lobby data carries cap/glove entries (plus their textures) that the Unity side of this branch does not draw.
- Frame-by-frame iteration dumps (`after_*`, `shoes_*`, audit images) were not committed.

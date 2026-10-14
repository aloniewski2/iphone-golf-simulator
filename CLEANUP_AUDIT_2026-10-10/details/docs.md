# Docs audit — what is unused, stale or still needed

Repo: `iphone-golf-simulator`, branch `final-build`, HEAD `14a43331`, working tree as of 2026-10-10. Read-only audit; nothing in the repo was changed.

Scope: every `*.md` outside `work/` and `proof/` (294 files: 42 at the root, 30 in `docs/` plus one JSON, 149 in `ArtDir/`, 54 in `SportsLibrary/`, 7 other READMEs, 12 agent-skill files), plus `.claude/`, `.agents/`, `skills-lock.json` and `.github/`.

Git state: 292 of the 294 docs are tracked and unchanged. Two are not:
- `PLAN_MenuHub_WalkableWorld.md` is **untracked**. A copy is committed on `integrate/main-2026-10-10`, but it is older: it still says "Nothing is built yet".
- `server/README.md` has **local edits** (the course keys were updated to the three courses).

Method: I read every root doc and each group's key docs, and skimmed the rest. I searched for inbound references across 2,247 text files outside the heavy folders, 7,030 small text files in `work/` (depth ≤4) and 2,672 in `proof/`. I left out git-status snapshot files, which only list filenames. I checked every relative markdown link, and spot-checked doc claims against code (`Hole.cs`, `TennisAbilities.cs`, `GolfArcadeApp.swift`, the workflows). I also compared the docs with `origin/main` and `integrate/main-2026-10-10`.

Verdicts:
- **SAFE**: remove; nothing else changes.
- **SAFE+FIX**: remove after the named edit.
- **DECIDE**: history or asset-bound notes. The owner chooses: archive them, or delete them together with the assets or code they describe. A suggested default is given.
- **KEEP**: still needed. Many KEEP docs need edits; those are listed in section G.

---

## Facts that change the picture (read first)

1. **`AGENTS.md` is loaded automatically into every Claude Code and Codex session** in this repo. Its stale "Active …" sections reach every agent. This is the doc most worth fixing.
2. **GitHub Pages does not publish `docs/`.** `static.yml` exists only on `main` and `integrate/main-2026-10-10`, not on `final-build`. It deploys the `site/` folder (index, marketing, privacy, support), and only from `main` and `final-build-10/9`. The GitHub API reports `build_type: workflow`. `site/` does not exist on `final-build`. Removing anything in `docs/` cannot affect Pages.
3. **CI on this branch is only `ios.yml` and `server.yml`.** Both run only for `main`/`testing`, so `final-build` gets no CI at all.
   - `netcode.yml` and `static.yml` live only on `main`.
   - `netcode.yml` cites `Tools/netsim/README.md`, which is missing on `final-build`.
   - No workflow, test or build step reads any `.md` file.
4. **`final-build` and `main`/`integrate` have diverged on 15 docs.**
   - The other branches already contain fixes for some stale text here: `MULTIPLAYER.md`, `Unity/README.md` (legacy banner), `tennis-playtest.md`, `rally-contact-quality.md`.
   - They also add new docs: `PLAN_Multiplayer_OnlineLocal.md`, `docs/tennis-feel-pass-2026-10-10.md`, `Tools/netsim/README.md`.
   - Deleting or editing these on `final-build` will cause merge conflicts. Do doc cleanup on the branch that becomes canonical.
5. **Scripts that read docs** (deleting these docs breaks the scripts):
   - `SportsLibrary/Tools/verify_library.py` reads `SportsLibrary/README.md`, `CHARACTER-STANDARD.md`, `UNITY-HANDOFF.md`, `Previews/README.md` and `Animations/V4/*/README.md`. It also fails on any broken link inside them, including `../CONTRIBUTING.md`.
   - `Tools/HeroBase/phase0.py` stops with an error if `ArtDir/hero/base_lock/{ACCEPTANCE,METRICS,SCALE}.md` are missing.
   - `Tools/HeroBase/package_review.py` rewrites `base_lock/METRICS.md`, `SCALE.md`, `proof/GATE_RESULTS.md` and `HAIR_REMOVAL_RESULTS.md`. It packages `REFERENCE_CHANGE.md`, `proof/BLENDER_STARTUP_DIAGNOSIS.md` and `proof/REFERENCE_CONSISTENCY.md`, and copies `Unity/Assets/Characters/HeroBase/README.md`.
   - Nothing else reads docs. Code comments do point at docs (see section H).

---

## A. Root-level docs (42)

**DOC-01 · `AGENTS.md`**
- **What it says:** The agent rulebook. Character authority is `ArtDir/hero/base_lock`, with binary gates and proof rules. It then has dated sections:
  - Active handoff 10-01 (chin + fingers)
  - Bald reference update 09-30
  - Beautification and clothes-only scope 10-08 (male polo → female polo + skirt, then stop)
- **Date:** committed 2026-10-08; tracked.
- **Status:** current but partly stale.
  - The "Active handoff" is finished. Micro1e was done 10-01, and the head has since moved to Micro1l r2.
  - The 10-08 clothes pass is also finished: `ArtDir/hero/clothing/tailored-2026-10-08/proof/GATE_RESULTS.md` says "FEMALE POLO AND SKIRT PASS … Work stops here".
  - The top still says clothes are OUT OF SCOPE and the product is "Party Tennis" only.
- **Linked by:** auto-loaded by agents; `CLAUDE_KICKOFF.md`, 26 root GOAL/PLAN docs, 25 ArtDir prompt `.txt` files.
- **If removed:** every agent loses its rules.
- **Verdict: KEEP** (edit; see section G). Confidence: high.

**DOC-02 · `CLAUDE_KICKOFF.md`**
- **What it says:** A 9-line read order: AGENTS → art-bible → HERO.md → "Active plan (right now: `PLAN_HeroBase_MaleFemale_Lock.md`)" → plates.
- **Date:** committed 10-04 (written 09-30); tracked.
- **Status:** stale. The "active plan" finished on 09-30/10-02. Today's active work is the plaza hub and multiplayer.
- **Linked by:** nothing (only git-status snapshots in `work/`).
- **If removed:** nothing breaks. `AGENTS.md` already gives the same read order and is auto-loaded.
- **Verdict: SAFE.** If Adnan still pastes it into chats, refresh line 4 instead. Confidence: medium.

**DOC-03 · `README.md`**
- **What it says:** The repo front page.
  - The top says to open `GolfArcadeUnity.xcworkspace`.
  - The rest describes the old SceneKit camera/body-tracking golf: playable mock, Practice Lab, Vision calibration, the `GolfArcade.xcodeproj` getting-started steps, and the main/testing/feature branch model.
- **Date:** 09-21; tracked.
- **Status:** mostly stale.
  - The app now opens on `SportsHome`.
  - The old `ContentView`/`MainMenuView`/`PracticeRangeScreen` are only reachable from `ContentView`'s `#Preview`.
- **Linked by:** GitHub repo page. It links `SportsLibrary/IOS-UNITY-INTEGRATION.md`, `SportsLibrary/README.md`, `UNITY-HANDOFF.md`, `docs/PLAYABLE_MOCK.md`, `AUDIT_EXECUTION.md`, `PRACTICE_LAB.md` and `CONTRIBUTING.md`.
- **If removed:** the repo has no front page.
- **Verdict: KEEP** (rewrite; see G). Confidence: high.

**DOC-04 · `CONTRIBUTING.md`**
- **What it says:** Branch from `testing`, open PRs to `testing`, then promote to `main`. It claims `main`/`testing` block direct pushes. It lists the CI checks and the old camera-golf UI-test launch arguments.
- **Date:** 09-17; tracked.
- **Status:** stale.
  - `origin/testing` was last updated 09-20; `main` gets direct commits.
  - The GitHub API reports `main` as **not protected**.
  - Active branches are `final-build`, `integrate/*`, `claude/*`, `codex/*` and `local/*`.
  - It does not mention the `netcode`, `server` or Pages workflows.
- **Linked by:** `README.md`, `SportsLibrary/README.md` (`verify_library.py` checks that link), ArtDir brief `onboarding/32_ONBOARDING_SOL_GOAL.txt`.
- **If removed:** `verify_library.py` reports a broken link, and the README link 404s.
- **Verdict: KEEP** (rewrite; see G). Confidence: high.

**DOC-05 · `art-bible.md`**
- **What it says:** Soft-plastic Switch Sports look, the locked bald base plates, grey mannequin body until clothes exist, URP material values, future customization slots.
- **Date:** 10-04 (written 09-30); tracked.
- **Status:** partly stale. Clothes, rig and animations now exist: MatchHeroes, tailored kits, the 53-bone rig. The title says "Party Tennis".
- **Linked by:** `AGENTS.md`, `CLAUDE_KICKOFF.md`, 27 root GOAL/PLAN docs, 24 ArtDir prompt `.txt` files.
- **If removed:** the agent read order breaks.
- **Verdict: KEEP** (edit). Confidence: high.

**DOC-06 · `VISUAL_OVERHAUL_ART_BIBLE.md`**
- **What it says:** The 10-07 "premium stylized sports resort" direction for golf and tennis: palette, materials, rig contract v1, acceptance rules.
- **Date:** 10-08; tracked.
- **Status:** current. It is the newest whole-game art direction, but no read order mentions it.
  - Its reference concepts live outside the repo, at `/Users/adnanyonathan/Documents/Codex/2026-10-06/open-4/outputs/visual-overhaul/after/`. That folder exists only on this Mac.
- **Linked by:** `work/full-visual-overhaul/PLAN.md` only.
- **If removed:** the overhaul loses its written rules.
- **Verdict: KEEP.** Link it from `AGENTS.md` or `art-bible.md`, and copy the concepts into ArtDir. Confidence: medium.

**DOC-07 · `VISUAL_OVERHAUL_LEDGER.md`**
- **What it says:** The 10-07 implementation ledger for the full visual overhaul (branch `codex/full-visual-overhaul`), with gate table and owners. Several gates still say IN PROGRESS or PENDING.
- **Date:** 10-08; tracked.
- **Status:** finished project record. The work was merged in `9283f6a8` on 10-08, so the ledger is frozen mid-way.
- **Linked by:** nothing live.
- **If removed:** the history of what the overhaul certified is lost.
- **Verdict: DECIDE.** Suggest archiving to `docs/archive/`. Confidence: high.

**DOC-08 · `PLAN_MenuHub_WalkableWorld.md`**
- **What it says:** The current plan for "The Plaza", a walkable 3D menu hub. It is built in editor and simulator. HITCH failed, device gates were not run, LOOK_MATCH and D6 are open.
- **Date:** written 10-10 18:46; **untracked**.
- **Status:** current.
- **Linked by:** `proof/menu-hub/GATE_RESULTS.md`; the plan itself points to `proof/menu-hub/`.
- **If removed:** the only plan for the hub is lost, and it isn't in git.
- **Verdict: KEEP.** Commit it; the newer local copy should replace the one on `integrate`. Confidence: high.

**DOC-09 · `PLAN_HeroBase_MaleFemale_Lock.md`**
- **What it says:** The 09-30 build brief for the bald HeroBase male/female: match the plates, opaque separate hair, 35–50k tris, no clothes.
- **Date:** 10-04 (written 09-30); tracked.
- **Status:** finished plan. Its "no clothes / no rig" scope was superseded on 10-08.
- **Linked by:**
  - `AGENTS.md`, `CLAUDE_KICKOFF.md` ("active plan")
  - `ArtDir/hero/HERO.md`
  - `ArtDir/hero/base_lock/ACCEPTANCE.md`, which names it as **"Authority"**
  - `GOAL_HeroBase_MaleFemale.md` and `Micro5`
- **If removed:** four live docs point at nothing.
- **Verdict: DECIDE.** It is the origin of the base_lock contract. Suggest keeping it until AGENTS, KICKOFF and HERO are refreshed, then archiving it. Confidence: high.

**DOC-10 · The 15 GOAL files**
- **Files:** `GOAL_HeroBase_MaleFemale.md`, plus `GOAL_HeroBase_Micro1_MaleSilhouette`, `Micro1b`, `Micro1c`, `Micro1d`, `Micro1e`, `Micro1f`, `Micro1g`, `Micro1h`, `Micro1i`, `Micro1k`, `Micro2`, `Micro3`, `Micro4`, `Micro5` (`.md`).
- **What they say:** Paste-in "Goal Mode" prompts for each micro step of the HeroBase build: silhouette, form, chin+fingers, hands, face lock, densify, chin/ear, jaw/cheek, female silhouette and head, Unity look.
- **Date:** committed 10-04 (written 09-30–10-01); tracked.
- **Status:** finished or superseded. The male head is now Micro1l r2; torso/hands are FORM3; female work F1–F3 is staged. Outcomes are recorded in `ArtDir/hero/base_lock/proof/*` and `INSTALL_LOG_MALE.md`.
- **Linked by:**
  - `AGENTS.md` "Active handoff" (Micro1e)
  - `HANDOFF_Sonnet…` (1, 1d, 1e)
  - `ArtDir/hero/base_lock/REF_INDEX.md` (1g, 1h)
  - `face_lock/FACE_SPEC.md` and `PROCESS_FaceMatch.md` (1h)
  - `PASTE_PROMPT_*.txt` (1g, 1h, 1i) and `parallel_prompts/01b_*.txt` (1k)
- **If removed:** those mentions become dead text.
- **Verdict: SAFE+FIX.** First remove AGENTS.md's "Active handoff" section; optionally reword the mentions in REF_INDEX/FACE_SPEC/PROCESS_FaceMatch. Confidence: high.

**DOC-11 · `HANDOFF_Sonnet_MaleHero_ChinFingers.md`**
- **What it says:** The 10-01 handoff to Sonnet: finish the male chin and fingers.
- **Date:** 10-04 (written 10-01); tracked.
- **Status:** finished.
- **Linked by:** `AGENTS.md` ("Start: HANDOFF…"), `GOAL…Micro1e`.
- **If removed:** AGENTS points at nothing.
- **Verdict: SAFE+FIX.** Delete the AGENTS.md "Active handoff — 2026-10-01" section in the same change. Confidence: high.

**DOC-12 · `PROCESS_Blender_Claude.md`**
- **What it says:** The Claude × Blender working method: Blender MCP connector plus a vision loop, or headless bpy scripts with fixed cameras and a silhouette gate. It is framed around the chin+fingers job.
- **Date:** 10-04 (written 10-01); tracked.
- **Status:** partly stale. The specific mission is done; the method is reusable, and memory notes Adnan wants the MCP used.
- **Linked by:** `AGENTS.md` (active handoff), `HANDOFF…`.
- **If removed:** the only written Blender SOP is lost.
- **Verdict: DECIDE.** Suggest keeping a generalized version (drop the chin/finger mission) and linking it from AGENTS "How to work". Confidence: medium.

**DOC-13 · Tennis phase briefs `PLAN1…`, `PLAN1B…`, `PLAN1C…`, `PLAN2…`, `PLAN2B…`, `PLAN2C…`, `PLAN2D…`, `PLAN3…`, `PLAN3B…` (9)**
- **What they say:** Paste-in tennis phase briefs from 09-26/27: fluidity, serve/whiff, body clearance, juice/fairness, ultimate spectacle, camera UX, court postcard, animation fluidity, V4 hair/customize.
- **Date:** 09-28; tracked.
- **Status:** finished. Each was executed to "review candidate"; results are in `ArtDir/screenshots/plan*/00_*.md`. They target Hero V4 and the old clip set, both since replaced by MatchHeroes and the 10-03 match-anim-set.
- **Linked by:** only `docs/party-mechanics-research` mentions PLAN2B.
- **If removed:** nothing breaks.
- **Verdict: SAFE.** The results notes (DOC-58) are the record. Confidence: high.

**DOC-14 · `PLAN_Score80_PartyTennis.md`, `PLAN_Score80_PHASE_A_START.md`**
- **What they say:** The 09-27 "Score 64→80" master plan (phases A–F) and its "start here" pointer.
- **Date:** 09-28; tracked.
- **Status:** finished. `ArtDir/score80/NOTES.md` says all six phases were run on 09-27.
- **Linked by:** each other only.
- **If removed:** nothing breaks.
- **Verdict: SAFE.** The results note is in DOC-58. Confidence: high.

**DOC-15 · Hero V5/V6 root plans: `PLAN_HeroLookShip_FinishedProduct.md`, `PLAN_HeroV5_OpaqueHair_ProperLegs.md`, `PLAN_HeroV5_AstraTextureSidePass.md`, `PLAN_HeroV6_ZeroDeficiency.md`, `CONCEPTS_HeroV6_FiveDirections.md` (5)**
- **What they say:** V5 look-ship master plan, V5 hair/legs rebuild, the Astra texture side pass, the V6 "zero deficiency" rebuild, and five V6 concept directions.
- **Date:** 09-28–10-04 (written 09-27–09-30); tracked.
- **Status:** superseded.
  - V5 failed its gate; V6 is "FAILED CANDIDATE".
  - The HeroBase lock replaced them on 09-30.
  - `AGENTS.md` itself calls V4/V5/V6 plans "archive / secondary".
  - Verbatim copies exist in `ArtDir/hero/v5_target/ACCEPTANCE.md` and `v6_target/ACCEPTANCE.md`.
- **Linked by:** each other and archived ArtDir v5/v6 docs only.
- **If removed:** nothing live breaks.
- **Verdict: SAFE.** Confidence: medium-high.

---

## B. `docs/` (30 .md + 1 .json)

**DOC-16 · Old camera-golf docs: `docs/ARCHITECTURE.md`, `PLAYABLE_MOCK.md`, `PRACTICE_LAB.md`, `AUDIT_EXECUTION.md`, `CAMERA_SETUP_FIX.md`, `SWING_FEEDBACK.md`, `VISUAL_QUALITY.md` (7)**
- **What they say:** The SceneKit golf where the body is the controller: Vision pose → calibration → virtual club → shot pipeline, playable 3-hole mock, Practice Lab trials, the 09-16 audit and camera fixes, swing-feedback design, the 09-17 course visual pass.
- **Date:** committed 09-15–09-18; tracked.
- **Status:** describes a feature that is no longer reachable.
  - The app entry is `SportsHome`.
  - `ContentView` (the only route to these screens) is instantiated only from its own `#Preview`.
  - The code still exists (`GolfArcade/Tracking`, `Swing`, `Shot`, `Features/Practice`, `Benchmark/PracticeLab.swift`).
  - `ARCHITECTURE.md`'s generic name hides that it describes only the old pipeline; there is no doc for the current architecture.
- **Linked by:** `README.md` (PLAYABLE_MOCK, AUDIT_EXECUTION, PRACTICE_LAB); `PLAYABLE_MOCK` → `AUDIT_EXECUTION`.
- **If removed:** three README links 404.
- **Verdict: DECIDE.** These go with the old camera-golf code (code area):
  - If that code is removed, delete all 7 and fix the README links.
  - If it is kept, move them to `docs/archive/camera-golf/`.
  - Either way, write a new `docs/ARCHITECTURE.md` for SwiftUI + UnityFramework + TV. Confidence: high.

**DOC-17 · `docs/tennis-ultimates-and-dive.md`**
- **What it says:** Three selectable tennis ultimates and the dive, implemented 09-27.
- **Date:** 09-28; tracked.
- **Status:** describes a removed feature. `docs/dive-only-gameplay.md` (09-28) removed ultimates, and the code has `TennisAbilities.UltimatesEnabled => false`.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: SAFE.** Confidence: high.

**DOC-18 · Tennis change notes: `docs/dive-only-gameplay.md`, `serve-and-court-pace-tuning.md`, `rally-contact-quality.md`, `receiving-stance-and-camera.md`, `lob-contact-audit.md`, `tennis-lobs-and-ultimate-scenes.md`, `post-game-phone-actions.md`, `tutorial-setup-fixes.md`, `tennis-motion-reference.md` (9)**
- **What they say:** Dated notes from 09-24 to 09-29 on individual tennis tuning and fixes: dive-only, court pace 0.88 and serve windows, contact quality and pace, receiving stance and camera, lob intercept, post-game phone buttons, tutorial steps, motion reference pass.
- **Date:** 09-24–09-29; tracked.
- **Status:** a mix of still-true and superseded.
  - Still true: `CourtMovementScale = .88` in `TennisRules.cs`.
  - Superseded: contact weights were changed by `ArtDir/review/tennis-timing-20261006` (timing 80%) and again by the 10-10 feel pass on `integrate`. The "ultimate scenes" half of the lobs doc describes a removed feature.
  - `integrate/main-2026-10-10` edits two of these and adds `docs/tennis-feel-pass-2026-10-10.md`.
- **Linked by:** `serve-and-court-pace-tuning` is cited by `docs/character-handoff/ACCEPTANCE.md`; the others by nothing.
- **If removed:** nothing breaks, but the only "why" for several tuning numbers is lost.
- **Verdict: DECIDE.** Suggest merging them into one tennis tuning log (e.g. `SportsLibrary/TENNIS-GAMEPLAY.md`) on the canonical branch, not on `final-build`. Confidence: medium.

**DOC-19 · `docs/tennis-playtest.md`**
- **What it says:** A 15-minute device QA routine: gameplay depth checks, the `-benchTennis` frame-time run, latency and feel table.
- **Date:** 09-24; tracked.
- **Status:** current. `main` updated it on 10-10 for aim-by-timing.
- **Linked by:** none.
- **If removed:** the only manual device test procedure is lost.
- **Verdict: KEEP.** Confidence: medium.

**DOC-20 · `docs/party-mechanics-research-2026-09-27.md`**
- **What it says:** A research bookmark on smash opportunities, signature mechanics, ultimates and perks, human vs fictional characters. It says "not approved implementation".
- **Date:** 09-28; tracked.
- **Status:** research history. Lob/smash later shipped; ultimates were removed.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** Suggest archiving. Confidence: high.

**DOC-21 · `docs/matchhero-golfer.md`**
- **What it says:** The golfer is Adnan's match hero (since 10-04): pipeline, FBX contents, hair, clubs.
- **Date:** 10-06; tracked.
- **Status:** current.
- **Linked by:** `blender/scripts/matchhero_golf.py`, `matchhero_mhhair.py`, `docs/avatar-kit.md`, `docs/hero-golfer.md`.
- **If removed:** the current golfer pipeline is undocumented.
- **Verdict: KEEP.** Confidence: high.

**DOC-22 · `docs/magma-open.md`**
- **What it says:** The Magma Open course design (lava lake, holes 21–23).
- **Date:** 10-04; tracked.
- **Status:** current but partly stale. `Hole.cs` now deals Magma Open as **holes 10, 16, 21, 22, 23** (five holes), not three.
- **Linked by:** none.
- **If removed:** the only design doc for a live course is lost.
- **Verdict: KEEP** (edit the hole list). Confidence: high.

**DOC-23 · `docs/club-ui.md`**
- **What it says:** Adnan's "Island Sports Club" UI system rebuilt in Unity uGUI (`Unity/Assets/Scripts/UI/Club.cs`): palette, fonts, components.
- **Date:** 09-29; tracked.
- **Status:** describes the **stand-alone Unity build's** menus. `GolfGame.cs` only shows that menu when `!NativeSportsSession.Active`; the iPhone app uses native SwiftUI menus.
- **Linked by:** none.
- **If removed:** `Club.cs` is undocumented.
- **Verdict: DECIDE.** It goes with the code-area decision on the stand-alone Unity UI (HomeScreen/Lobby/Club.cs). Confidence: medium.

**DOC-24 · `docs/avatar-kit.md`, `docs/hero-golfer.md`**
- **What they say:** The earlier golfer pipelines: icon avatar kit and scanned Hero parts. Both carry a "2026-10-04 retired, see matchhero-golfer" banner.
- **Date:** 10-05; tracked.
- **Status:** superseded, but labelled honestly.
- **Linked by:** code comments in `Unity/Assets/Scripts/Game/HeroGolfer.cs`, `Profile/CharacterLook.cs` and `blender/scripts/avatar_parts.py`, `avatar_kit.py`, `avatar_head_parts.py`.
- **If removed:** those comments point at nothing.
- **Verdict: DECIDE.** Keep while `Unity/Assets/Archive/IconKit/` and `avatar_*.py` exist; delete with them. Confidence: high.

**DOC-25 · `docs/character-customization.md` + `docs/character-handoff/` (README, ACCEPTANCE, GOLF_IMPLEMENTATION, `reference/art-bible.md`, `reference/original-agent-brief.md`, `active-prefab-manifest.json`)**
- **What they say:** The 09-28 audit and handoff for bringing the then-current modular "toy-athlete" tennis characters into golf, the shared GLB player bases, and a hash manifest of the tennis clips then active. It also holds copies of the **earlier full party-sports art bible** (256 lines) and the **original AGENTS.md** brief.
- **Date:** 09-28; tracked.
- **Status:** superseded. Golf adopted the match hero on 10-04 instead, and the tennis clips were replaced by the match-anim-set on 10-03.
- **Linked by:** each other. `character-handoff/README.md` links `ArtDir/ANIMATOR.md`, `OWNED_MOCAP.md`, `UNITY_LOOK.md` and `hero/modular/MODULAR.md`.
- **If removed:** nothing live breaks. The only copy of the earlier full-game art bible goes with it.
- **Verdict: DECIDE.** Suggest archiving together with the Hero01/V4 docs (DOC-49). Confidence: high.

---

## C. `SportsLibrary/` (54)

**DOC-26 · `SportsLibrary/IOS-UNITY-INTEGRATION.md`**
- **What it says:** How to build and install the integrated app: Unity export → `Unity/Tools/build-integrated-ios.sh` → `install-phone.sh`, plus the native/Unity bridge architecture and release gates.
- **Date:** 09-21; tracked.
- **Status:** current (the scripts exist). Some text is stale: "exposed games are Cliffside and Tennis Rally"; prerequisites say "Xcode 27" while the README says Xcode 26.
- **Linked by:** `README.md` ("build instructions"), `Unity/README.md`, ArtDir onboarding brief.
- **If removed:** the only build and install procedure is lost.
- **Verdict: KEEP** (edit). Confidence: high.

**DOC-27 · `SportsLibrary/TENNIS-GAMEPLAY.md`**
- **What it says:** The tennis rules standards and a running change log; the latest entry is the 10-04 serve-toss revision.
- **Date:** 10-06; tracked.
- **Status:** current, though the opening "prototype" framing is old.
- **Linked by:** SportsLibrary README (checked by `verify_library.py`), `UNITY-HANDOFF`, `Arenas/Tennis`, `Previews/UnityTennis`.
- **If removed:** `verify_library.py` reports broken links.
- **Verdict: KEEP.** Confidence: high.

**DOC-28 · `SportsLibrary/MULTIPLAYER.md`**
- **What it says:** Native `MultiplayerService`: Game Center and Bonjour parties, the API, rules.
- **Date:** 10-06; tracked.
- **Status:** partly stale. It still says "No online/lobby entry screens are installed", but `IslandScreens`, `ArcadeScreens` and `TennisMenu` now use the service. `origin/main` already fixed this and added a "what the October pass changed" section.
- **Linked by:** none on this branch.
- **If removed:** the only doc for the native multiplayer protocol is lost.
- **Verdict: KEEP.** Take `main`'s version. Confidence: high.

**DOC-29 · `SportsLibrary/TV-INTEGRATION-VALIDATION.md`**
- **What it says:** A TV/external-display validation log ("in progress", 09-21) with later golf and tennis motion notes.
- **Date:** 09-21; tracked.
- **Status:** partly stale. `main` appended 10-09 content, so it is still in use there.
- **Linked by:** none.
- **If removed:** that log is lost.
- **Verdict: KEEP** (merge `main`'s version). Confidence: medium.

**DOC-30 · Library navigation docs: `SportsLibrary/README.md`, `CHARACTER-STANDARD.md`, `UNITY-HANDOFF.md`, `Previews/README.md`, `Animations/V4/{Golf,Tennis,Bowling,Boxing}/README.md` (8)**
- **What they say:** The 09-20 "Sports Library": four sports including bowling and boxing; "permanent" male/female standard characters with floating hands; V4 animation indexes.
- **Date:** 09-20–09-22; tracked.
- **Status:** superseded as product direction. Today's heroes are the HeroBase/MatchHeroes with full arms and hands, and bowling/boxing are not in the game. The docs remain the index of the SportsLibrary binary assets.
- **Linked by:** `README.md` and `Unity/README.md`. `verify_library.py` **reads all 8** and fails if any are missing.
- **If removed:** `verify_library.py` stops with an error; README and Unity README links 404.
- **Verdict: DECIDE.** These go with the asset-area decision on `SportsLibrary/`. If the library stays, add a "historical — not the current heroes" banner (section G). Confidence: high.

**DOC-31 · V4 standard-character integration notes: `INTEGRATION-BASELINE.md`, `GOLF-RUNTIME-INTEGRATION.md`, `CHARACTER-ARMS.md`, `GOLF-GRIP-REFINEMENT.md`, `FLOATING-HANDS-TEST.md`, `HANDOFF-VALIDATION.md` (6)**
- **What they say:** How the V4 standard characters were wired into golf (`StandardCharacterArms`, `StandardGolfGrip`, `standard_*_golf.fbx`), the floating-hands experiment, and library packaging validation.
- **Date:** 09-20; tracked.
- **Status:** superseded. The golfer has been the match hero since 10-04. The code and FBXs still exist (`Unity/Assets/Scripts/Game/StandardCharacterArms.cs`, `StandardGolfGrip.cs`, `Resources/StandardCharacters/*.fbx`; `TennisActor.cs` still adds `StandardCharacterArms`).
- **Linked by:** `Unity/README.md`, `SportsLibrary/README.md` (verify_library checks `INTEGRATION-BASELINE` and `HANDOFF-VALIDATION`), `UNITY-HANDOFF`, `docs/character-handoff/GOLF_IMPLEMENTATION.md`, `Previews/UnityArms` and `UnityGrip` READMEs.
- **If removed:** verify_library reports broken navigation; Unity README links 404.
- **Verdict: DECIDE.** These go with the code-area decision on `StandardCharacterArms`/`StandardGolfGrip` and the asset-area decision on the standard FBXs. Confidence: medium.

**DOC-32 · `SportsLibrary/Design/ANIMATION-PRODUCTION.md`, `Design/CONTROL-AND-CAMERA-BRIEF.md`**
- **What they say:** The 09-20 animation production plan and control/camera intent ("moving the phone left/right drives running").
- **Date:** 09-20; tracked.
- **Status:** superseded by assisted movement and the later camera plans.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: SAFE.** Confidence: medium-high.

**DOC-33 · Concept-image prompt notes (23): `Animations/Concepts/*.prompt.md` (11), `Clothing/Concepts/README.md` + 4 `*.prompt.md`, `Design/InitialConceptLibrary-Historical/{README,ANIMATION-HANDOFF}.md`, `Design/VenueConcepts-Historical/*.md` (5)**
- **What they say:** The image-generation prompts behind the 09-20 concept boards, including boxing and bowling. Two folders are already named "Historical".
- **Date:** 09-20; tracked.
- **Status:** historical.
- **Linked by:** their own READMEs/index.html; SportsLibrary README links `Clothing/Concepts/README.md` (verify_library).
- **If removed:** if `Clothing/Concepts/README.md` goes, fix the SportsLibrary README link.
- **Verdict: DECIDE.** Delete with their concept images (asset area). Confidence: high.

**DOC-34 · `SportsLibrary/Animations/V4/ORIGINAL-NOTES.md`**
- **What it says:** The original V4 studio notes.
- **Date:** 09-20; tracked.
- **Status:** historical, with **13 broken relative links** (author-machine layout). `verify_library.py` deliberately skips it.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: SAFE.** Confidence: high.

**DOC-35 · Arena, crowd and preview READMEs: `Arenas/{Tennis,Bowling,Boxing}/README.md`, `Crowds/README.md`, `Previews/{HighFPS,UnityArms,UnityGameplay,UnityGrip,UnityTennis,UnityTennisArcade}/README.md` (10)**
- **What they say:** Notes for the 09-20 arena .blend files, crowd library and preview videos.
- **Date:** 09-20–09-22; tracked.
- **Status:** historical. Bowling and boxing are not in the game; the tennis arena may still be the venue source (asset area to confirm).
- **Linked by:** SportsLibrary README (verify_library), `CHARACTER-ARMS`, `GOLF-GRIP-REFINEMENT`, `TENNIS-GAMEPLAY`.
- **If removed:** fix those links.
- **Verdict: DECIDE.** These go with their binaries. Confidence: medium.

---

## D. `ArtDir/` (149 .md; images not covered)

**DOC-36 · `ArtDir/hero/HERO.md`**
- **What it says:** "Hero — Base Male/Female (LOCKED)": what ships, what is out of scope, where the gate is.
- **Date:** 10-04 (written 09-30); tracked.
- **Status:** partly stale.
  - "What ships now: HeroBase_Male prefab + Body_M + Hair_M" — in-game heroes are `Unity/Assets/Characters/MatchHeroes`. HeroBase prefabs are used only by editor proof scripts (`HeroBase*Proof.cs`). Runtime `TennisCustomization.HeroBase()` is a different thing despite the name: it loads `Resources/Tennis/Customization/PlayerMale|PlayerFemale`, the match-hero base.
  - "Out of scope: outfits, Mixamo, animator, racket" — all done.
  - The gate it points to (`proof/GATE_RESULTS.md`) is the 09-30 "BASE FAIL" snapshot.
- **Linked by:** `AGENTS.md`, `CLAUDE_KICKOFF.md`, HANDOFF, GOAL/PLAN briefs, `ArtDir/hero/provenance.json`.
- **If removed:** the read order breaks.
- **Verdict: KEEP** (edit). Confidence: high.

**DOC-37 · base_lock contracts: `ArtDir/hero/base_lock/{ACCEPTANCE,METRICS,SCALE,REFERENCE_CHANGE,REF_INDEX}.md` (5)**
- **What they say:** The acceptance contract, measured tris/heights, scale and HairRoot contract, the 09-30 bald reference swap, and the reference plate index.
- **Date:** 10-04 (written 09-30–10-01); tracked.
- **Status:** current authority. `METRICS.md` is a stale generated snapshot: it shows male 39,268 tris, while the install log shows 66,158.
- **Linked by:** `Tools/HeroBase/phase0.py` (**requires** ACCEPTANCE, METRICS, SCALE), `package_review.py` (rewrites METRICS/SCALE, packages REFERENCE_CHANGE), GOAL briefs, prompt `.txt` files.
- **If removed:** `phase0.py` errors.
- **Verdict: KEEP.** Confidence: high.

**DOC-38 · Face specs: `base_lock/face_lock/{FACE_SPEC,PROCESS_FaceMatch}.md`, `face_lock_f/{FACE_SPEC,PROCESS_FaceMatch}.md` (4)**
- **What they say:** Male and female face-lock specs and the face-matching pipeline.
- **Date:** 10-04 (written 10-01–10-02); tracked.
- **Status:** current reference. Female head F1–F3 is still staged, not installed.
- **Linked by:** `REF_INDEX`, PASTE_PROMPT `.txt` files, parallel prompts, female head results.
- **If removed:** the face work loses its spec.
- **Verdict: KEEP.** Confidence: medium-high.

**DOC-39 · `base_lock/parallel_prompts/INSTALL_LOG_MALE.md`**
- **What it says:** Install log of every male-body install, with start/end blend SHAs, tri counts and rollback.
- **Date:** 10-04 (written 10-02); tracked.
- **Status:** current provenance.
- **Linked by:** parallel prompt `.txt` files.
- **If removed:** there is no record of what the installed body is or how to roll back.
- **Verdict: KEEP.** Confidence: high.

**DOC-40 · `base_lock/parallel_prompts/README_PARALLEL_8.md`, `SKIN_LIVELINESS_BRIEF.md`**
- **What they say:** The "8 parallel chats" zone split and the skin-tone brief (10-01/02).
- **Date:** 10-04; tracked.
- **Status:** finished briefs.
- **Linked by:** prompt `.txt` files, `face_lock_f/PROCESS_FaceMatch.md`, `SKIN_LIVELINESS_RESULTS.md`.
- **If removed:** dead text mentions only.
- **Verdict: SAFE.** These go with the parallel-prompt `.txt` files. Confidence: medium.

**DOC-41 · Current gate evidence in `base_lock/proof/` (9)**
- **Files:** `MALE_HEAD_RESULTS`, `MALE_HANDS_FORM3_RESULTS`, `MALE_ARMS_HANDS_FORM_RESULTS`, `MALE_TORSO_FORM2_RESULTS`, `MALE_LEGS_FEET_RESULTS`, `MALE_NAPE_PELVIS_RESULTS`, `SKIN_LIVELINESS_RESULTS`, `FEMALE_HEAD_RESULTS`, `tennis-arm-swing-repair-2026-10-08/GATE_RESULTS` (`.md`).
- **What they say:** PASS/FAIL records for the installed or staged body parts.
- **Date:** 10-04–10-08; tracked.
- **Status:** current evidence. AGENTS.md requires `GATE_RESULTS` + proof.
- **Linked by:** `INSTALL_LOG_MALE.md`, prompts, `work/*` handoffs.
- **If removed:** the installed body has no evidence trail.
- **Verdict: KEEP.** Confidence: medium.

**DOC-42 · Superseded gate iterations and package outputs in `base_lock/proof/` (20)**
- **Files:**
  - `GATE_RESULTS.md` (09-30 "BASE FAIL")
  - `HAIR_REMOVAL_RESULTS`, `BLENDER_STARTUP_DIAGNOSIS`, `REFERENCE_CONSISTENCY`
  - `MALE_SIL_RESULTS`, `MALE_FORM_RESULTS`, `MALE_FORM_COMPLETION_AUDIT`, `MALE_FORM_D_COMPLETION_AUDIT`, `MALE_HAND_4_RESULTS`, `MALE_ARMS_HANDS_RESULTS`, `MALE_TORSO_RESULTS`
  - `MALE_HEAD_{1,2,3}_RESULTS`, `MALE_HEAD_micro1{g,h,i,j,k,l_r1}_RESULTS` (`.md`)
- **What they say:** Earlier attempts' gate lines.
- **Date:** 10-04 (written 09-30–10-02); tracked.
- **Status:** superseded by DOC-41.
- **Linked by:**
  - `HERO.md` and `Unity/Assets/Characters/HeroBase/README.md` (both link `GATE_RESULTS.md`)
  - `Tools/HeroBase/package_review.py` (writes `GATE_RESULTS` and `HAIR_REMOVAL`; packages `BLENDER_STARTUP_DIAGNOSIS` and `REFERENCE_CONSISTENCY`)
  - `reference_consistency.py`, `proof_male_silhouette.py` and `verify_male_silhouette.py` (write or read `MALE_SIL_RESULTS`/`REFERENCE_CONSISTENCY`)
  - HANDOFF, results chain
- **If removed:** re-running `package_review.py` fails at packaging, and the HERO/HeroBase README gate links 404.
- **Verdict: DECIDE.** Suggest keeping `GATE_RESULTS.md` (refreshed or pointed at the install log) and archiving the rest. Confidence: medium.

**DOC-43 · `base_lock/archive/with_hair_20260930/{ACCEPTANCE,METRICS,SCALE}.md` (3)**
- **What they say:** Contracts for the replaced hair-on plates.
- **Date:** 10-04; tracked.
- **Status:** archived on 09-30.
- **Linked by:** each other.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** These go with the archived plates. Confidence: high.

**DOC-44 · `base_lock/preview/outfit_example/OUTFIT_EXAMPLE.md`**
- **What it says:** A 10-01 look-dev preview: clothed and coloured, about 150k tris, Blender only.
- **Date:** 10-04; tracked.
- **Status:** preview-only, superseded by real kits.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** This goes with its renders. Confidence: high.

**DOC-45 · `hero/clothing/tailored-2026-10-08/{README.md, proof/GATE_RESULTS.md}`**
- **What they say:** The shipped tailored male polo, female polo and skirt (customizable colours), with PASS evidence.
- **Date:** 10-08; tracked.
- **Status:** current.
- **Linked by:** none, but this is the evidence that AGENTS.md's 10-08 task is done.
- **If removed:** the only record of the shipped clothing is lost.
- **Verdict: KEEP.** Confidence: high.

**DOC-46 · `hero/kit/` (8): `GARMENT_SPEC`, `COPY_TO_GOLF`, `golf/GOLF_KIT_DESIGN`, `golf/tailoring/proof/GATE_RESULTS` + `pass02`/`pass03`, `premium/tailoring4/README`, `refs/REFS`**
- **What they say:** The reusable garment contract, the golf kit design and gates, the tailoring candidate tools (`author-panel-masks.py`), construction references.
- **Date:** 10-06–10-08; tracked.
- **Status:** current. The `tailoring4` status line ("review in progress") is stale.
- **Linked by:** ArtDir briefs 29/30/30b, `work/cloth-overhaul`, `work/golf-look`.
- **If removed:** the garment standard is lost.
- **Verdict: KEEP.** Confidence: medium-high.

**DOC-47 · `hero/kits/{racket_classic,standard_wb}/README.md`**
- **What they say:** Notes on the reference sheets for the logo-free racket and the white/black kit (10-02).
- **Date:** 10-04; tracked.
- **Status:** reference notes for images.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** These go with their images. Confidence: medium.

**DOC-48 · `hero/source/reference/PROVENANCE.md`**
- **What it says:** The head reference anatomy comes from the official Blender Human Base Meshes v1.4.1.
- **Date:** 10-08; tracked.
- **Status:** current licence/provenance. It points at `work/reference-rebuild/vendor/...`, which is untracked.
- **Linked by:** none.
- **If removed:** shipped assets lose their provenance.
- **Verdict: KEEP.** Confidence: high.

**DOC-49 · Hero01 / V4 era (23)**
- **Files:**
  - `ArtDir/{ANIMATOR,OWNED_MOCAP,UNITY_LOOK}.md`
  - `anims/**` (14: MIXAMO, `_archived` STATUS, anim_repair, backhand_run, diagnose_restore, fix_hand_clip, fullstroke_grip, knees_offarm, legacy_on_v4, multi_angle_review, restore_motion, tennis_complete, `source/mocapdata-tennis/{SOURCE,preview/README}`)
  - `docs/HeroV4_Wardrobe_Slots.md`, `plates/PLATES.md`
  - `hero/{HERO_ARCHIVE_Hero01, _rejected_mannequin/HERO, modular/MODULAR, turnarounds/TURNAROUNDS}.md`
- **What they say:** The 09-25–09-27 Hero_01 character, its animation repair phases, Mixamo, the six reference plates ("NOT LOCKED") and V4 wardrobe slots.
- **Date:** 09-28–10-04; tracked.
- **Status:** superseded. MatchHeroes replaced Hero01 on 10-03; AGENTS.md calls V4–V6 archive.
- **Linked by:** each other, `docs/character-handoff/README.md`, a few GOAL briefs (`_archived/STATUS.md`).
- **If removed:** nothing live breaks.
- **Verdict: DECIDE.** Suggest deleting with the Hero01/V4 assets, including `Unity/Assets/ArtDirection/Hero01` (asset area).
  - **Exception:** `anims/source/mocapdata-tennis/SOURCE.md` records the licence of third-party MocapData files and must stay as long as those files stay.
- Confidence: high.

**DOC-50 · Hero V5 / V6 / integrity archive (17): `hero/v5/{RESTORE_V5,viewer/README}`, `v5_astra/TEX_NOTES`, `v5_proof/{GATE_RESULTS,LIGHTING}`, `v5_target/{ACCEPTANCE,BEFORE/multi_angle_review/README}`, `v6_polish/{PRODUCTION_PROGRESS,README}`, `v6_proof/{CUSTOMIZE_CONTRACT,GATE_RESULTS}`, `v6_target/{ACCEPTANCE,INVENTORY,KILL_LIST}`, `integrity_v1/{GATE_RESULTS,INTEGRITY,viewer/README}`**
- **What they say:** V5 look-ship gate FAIL, the restore, the Astra texture experiment, the integrity repair (archived), the V6 FAIL candidate and its contracts.
- **Date:** 09-28–10-04; tracked.
- **Status:** superseded and labelled as such.
- **Linked by:** each other and the root V5/V6 plans (DOC-15). **`v5_proof/LIGHTING.md` is cited in code comments** in `Unity/Assets/Scripts/Tennis/TennisLook.cs:269` and `GolfArcade/Unity/CharacterModelPreview.swift:364` as the light recipe.
- **If removed:** those two comments point at nothing.
- **Verdict: DECIDE.** Suggest deleting with `Unity/Assets/ArtDirection/{HeroV5AstraTex,HeroV6,HeroV6Polish}` (asset area). Keep `LIGHTING.md` (move it to `docs/`) or update the two comments. Confidence: high.

**DOC-51 · `environments/GOLF_ALL_COURSES_POSTCARD_STANDARD_PLAN.md` + `screenshots/golf_course_standard/GATE_RESULTS.md`**
- **What they say:** The 10-06 plan to raise every golf course to the postcard standard, and its implementation/validation record.
- **Date:** 10-06; tracked.
- **Status:** finished, but it describes the current course look. The results doc has **3 broken links**: `golf-course-review.html` and `golf-tree-repair.html` are missing, and the plan link has the wrong relative path.
- **Linked by:** each other, `work/golf-course-standard/*`.
- **If removed:** the record of the current course standard is lost.
- **Verdict: KEEP** (fix the links). Confidence: medium.

**DOC-52 · `environments/motion-club-3d/{ENVIRONMENTS,mockups-v2/PROMPTS,sources/PROMPT}.md`**
- **What they say:** The 09-29 "living images + 3D locker" menu backgrounds (`ClubLivingImageView`, `ClubEnvironmentView`) and their image prompts.
- **Date:** 10-06; tracked.
- **Status:** describes code that still exists. Whether it is reachable is a code-area question.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** These go with that code. Confidence: medium.

**DOC-53 · `ArtDir/golf/**` (9): README, coastal-scenery GATE, palm-test-diagnosis, dense-turf15 GATE/REPRODUCE, turf16 ART_REFERENCE_NOTES, turf17 GATE, rollout18-19 GATE, `vendor/COASTAL_LICENSES.md`**
- **What they say:** Final golf art sources (rebuild commands), turf/scenery gates, and the CC0 Poly Haven licences.
- **Date:** 10-08; tracked.
- **Status:** current (visual overhaul).
- **Linked by:** the golf manifests (`source-manifest.json`, etc.) and `proof/full-visual-overhaul/golf/GOLF_GATE.md`.
- **If removed:** rebuild steps and licence attribution are lost.
- **Verdict: KEEP.** Confidence: high.

**DOC-54 · `ArtDir/tennis/**` (4): audience, venues/architecture, botany, materials READMEs**
- **What they say:** Sources and rebuild steps for the overhauled tennis crowd and venues.
- **Date:** 10-08; tracked.
- **Status:** current.
- **Linked by:** `audience/source-manifest.json`.
- **If removed:** rebuild steps are lost.
- **Verdict: KEEP.** Confidence: high.

**DOC-55 · `review/point-clips/RECORDING.md`**
- **What it says:** "Save Last Point" recording: player flow, 60 fps toggle, clips.
- **Date:** 10-04 (written 09-29); tracked.
- **Status:** current. `PointClips.swift` and `SportsPointRecorder.mm` exist.
- **Linked by:** none.
- **If removed:** the feature's only user-flow doc is lost (Tools/PointClips covers only the smoke test).
- **Verdict: KEEP.** Confidence: medium-high.

**DOC-56 · `review/tennis-timing-20261006/GATE_RESULTS.md`**
- **What it says:** The 10-06 contact/timing tuning gates (timing = 80% of quality).
- **Date:** 10-06; tracked.
- **Status:** recent tuning record. `integrate` has a newer feel pass.
- **Linked by:** none.
- **If removed:** the only record of the current numbers is lost.
- **Verdict: KEEP** (or fold into the tennis log, DOC-18). Confidence: medium.

**DOC-57 · `review/unimate-tennis/UNIMATE_REVIEW.md`**
- **What it says:** "Review only" UniMate generated tennis clips on Hero V5 (09-29).
- **Date:** 10-04; tracked.
- **Status:** abandoned experiment.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: SAFE** (along with its mp4). Confidence: high.

**DOC-58 · Results of the September tennis plans (10): `score80/NOTES.md` + `screenshots/{plan1_fluidity,plan1b_serve_whiff,plan2_fun_feel,plan2b_spectacle,plan2c_camera_ux,plan2d_env_postcard,plan3a_fluidity,plan3b_hair_customize,body_clearance}/00_*.md`**
- **What they say:** Diagnosis and result notes of PLAN1–3B and Score80. Each says "review candidate, stopped for approval".
- **Date:** 09-28; tracked.
- **Status:** finished record (Hero V4 era).
- **Linked by:** none.
- **If removed:** the only explanation of several September tennis decisions is lost.
- **Verdict: DECIDE.** Suggest archiving with their screenshots. Confidence: high.

**DOC-59 · `screenshots/emote_loadout/GATE_RESULTS.md`, `screenshots/golf_locker_swap/GATE_RESULTS.md`**
- **What they say:** 10-05 verification of equipped emotes and the golf/tennis locker swap.
- **Date:** 10-06; tracked.
- **Status:** current. The `emote_loadout` images are **rewritten by `GolfArcadeTests/TennisMenuSnapshotTests.swift`**, which is why those PNGs show as modified.
- **Linked by:** none.
- **If removed:** the record is lost.
- **Verdict: KEEP.** Confidence: medium.

**DOC-60 · `ui/34_GAME_PRESENTATION_RESEARCH.md`, `ui/35_GAME_PRESENTATION_EXECUTION_PLAN.md`**
- **What they say:** Research and execution plan for the loading → venue → players → play → results flow (10-08). The functional pass is done; visual tuning is held.
- **Date:** 10-08; tracked.
- **Status:** current spec.
- **Linked by:** each other, `work/presentation/GATE_RESULTS.md`.
- **If removed:** the presentation flow has no spec.
- **Verdict: KEEP.** Confidence: high.

**DOC-61 · `ui/toybox-simple-v6/{APPROVED,DIRECTION,IMPLEMENTATION,MENUS,runtime/REVIEW}.md` (5)**
- **What they say:** Adnan's approved native "Island Sports Club" menus (09-29) and what was implemented.
- **Date:** 09-29; tracked.
- **Status:** current. The locker part was reworked on 10-03 and the plaza hub came on 10-10.
- **Linked by:**
  - `GolfArcade/Unity/ClubDesign.swift:430` names this folder as the approved target.
  - `TennisMenuSnapshotTests.swift` writes `runtime/*.png` (the modified PNGs).
  - `Unity/Assets/Editor/HeroLockerExport.cs` writes `source/preview-raw`.
- **If removed:** the approved menu target is lost, and the code comment points at nothing.
- **Verdict: KEEP.** Confidence: high.

**DOC-62 · `ui/motion-club-gameplay-hud-v1/{MOCKUPS.md, implemented/IMPLEMENTATION.md}`**
- **What they say:** The tennis HUD mockups and the implemented "Rally Pop" HUD (`TennisHud.cs`).
- **Date:** 10-04; tracked.
- **Status:** current (`TennisHud.cs` is used by `TennisGame`).
- **Linked by:** none.
- **If removed:** the HUD has no design record.
- **Verdict: KEEP.** Confidence: medium.

**DOC-63 · `ui/motion-club-radial-v1/{IMPLEMENTATION,PREVIEW}.md`, `ui/motion-club-resort-v1/MOCKUPS.md`**
- **What they say:** The 09-29 "Motion Club" curved home menu (`MotionClubMenus.swift`) and resort menu concepts.
- **Date:** 10-04; tracked.
- **Status:** describes code that still exists (`MotionClubHome`, referenced from `ClubScreens.swift`); whether it is reachable is a code-area question.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** These go with `MotionClubMenus.swift`. Confidence: medium.

**DOC-64 · Superseded UI explorations: `ui/gameplay-mockups-v3/README`, `joy-directions-v2/REVIEW`, `toybox-reference-v5/README`, `toybox-refined-v4/NOTES`, `weekend-club-v1/DESIGN_SYSTEM` (5)**
- **What they say:** Rounds of 09-29 menu explorations that led to the approved toybox-simple-v6.
- **Date:** 09-29; tracked.
- **Status:** superseded.
- **Linked by:** their own `index.html`/`build_gallery.py`.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** These go with their image galleries (ArtDir/asset area). Confidence: high.

---

## E. Other READMEs (7)

**DOC-65 · `Unity/README.md`**
- **What it says:** The Unity project guide.
  - The current half covers the iOS integration link, editor workflow, EditMode/PlayMode commands, MCP for Unity, iPhone build and layout.
  - The rest describes the stand-alone "phone is the club" golf build (home screen, profiles, Node online rooms, The Open, unlocks).
- **Date:** 10-06; tracked.
- **Status:** partly stale.
  - "golf now uses our permanent V4 characters" — it's the match hero now.
  - "Cliffside (holes 7, 12–15), Wild Isles (16–20)" — `Hole.cs` says Cliffside 7/8/9/12/15, Wild Isles 13/14/17–20, Magma Open 10/16/21–23.
  - The online section is legacy; `origin/main` already added a legacy banner.
- **Linked by:** ArtDir onboarding brief; links into SportsLibrary.
- **If removed:** the only Unity working guide is lost.
- **Verdict: KEEP** (edit; merge `main`'s banner). Confidence: high.

**DOC-66 · `Unity/Assets/Characters/HeroBase/README.md` (+ `README.md.meta`)**
- **What it says:** Bald HeroBase prefabs, optional hair under HairRoot, pivots, the studio scene.
- **Date:** 10-04; tracked.
- **Status:** current for the HeroBase prefabs. It says "gate remains FAIL" and doesn't mention that the in-game heroes are MatchHeroes.
- **Linked by:** `Tools/HeroBase/package_review.py` (copies it as `HAIR_CUSTOMIZATION.md`).
- **If removed:** packaging fails (and the `.meta` must go with it).
- **Verdict: KEEP.** Confidence: high.

**DOC-67 · `server/README.md`**
- **What it says:** The Node + Postgres backend: profiles, rounds, leaderboard, WebSocket golf rooms, run/deploy instructions.
- **Date:** 09-26, **locally modified** 10-10; tracked.
- **Status:** current for `server/`. However, `origin/main`'s Unity README says this server only serves the **stand-alone Unity build's** online mode. The iPhone app uses Game Center/Bonjour (`MultiplayerService`).
- **Linked by:** code comments in `Unity/Assets/Scripts/Net/Online/BackendClient.cs:16` and `Unity/Assets/Editor/BuildMenu.cs:23`.
- **If removed:** the server is undocumented.
- **Verdict: KEEP** while `server/` exists (server-area decision); add a "legacy for the iPhone app" line. Confidence: medium.

**DOC-68 · `Tools/PointClips/README.md`**
- **What it says:** How to run the point-recorder smoke test (`RecorderSmoke.mm`, `InspectClip.swift`) and `PointClipsTests`.
- **Date:** 10-04; tracked.
- **Status:** current (all named files exist).
- **Linked by:** none.
- **If removed:** the only doc for that tool is lost.
- **Verdict: KEEP.** Confidence: high.

**DOC-69 · `blender/scripts/POSTCARDS_README.md`**
- **What it says:** The pipeline contract for postcard holes 8 Needle, 9 Split, 10 Crater, including hashes that must not change.
- **Date:** 10-06; tracked.
- **Status:** current. The holes are in Cliffside and Magma.
- **Linked by:** `postcard_check.py`, `postcard_smoke_design.py`, `Tools/PostcardCheck/equiv.py`, ArtDir brief 15, `work/postcard-look`.
- **If removed:** the pipeline contract is lost.
- **Verdict: KEEP.** Confidence: high.

**DOC-70 · `blender/characters/golf/README.md`**
- **What it says:** Higgsfield/Tripo golf versions of Adnan's tennis characters (`golfer-*.glb`, 60k faces), "not rigged yet".
- **Date:** 09-23; tracked.
- **Status:** superseded by the match-hero golfer. The GLBs are referenced only by this README.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** This goes with those GLBs/PNGs (asset area). Confidence: high.

**DOC-71 · `blender/holes_mock/README.md`**
- **What it says:** Concept mocks for extra Cliffside holes (Spiral, Witch's Lair, Steps — never built) and Wild Isles 16–20 (built).
- **Date:** 09-26; tracked.
- **Status:** concept history.
- **Linked by:** none.
- **If removed:** nothing breaks.
- **Verdict: DECIDE.** This goes with the mock PNGs and `hole_NN_numbers.txt` files (asset area). Confidence: medium.

---

## F. Agent tooling: `.agents/`, `.claude/`, `skills-lock.json`

**DOC-72 · The vgpu skills**
- **Files:**
  - `.agents/skills/vgpu/` (SKILL.md, scene.md, `blender/index.md` + 6 references: 9 files)
  - `.agents/skills/vgpu-agent-flow/` (SKILL.md + 2 references: 3 files)
  - `.claude/skills/vgpu` and `.claude/skills/vgpu-agent-flow` (symlinks into `.agents/`)
  - `skills-lock.json`
- **What it is, in plain English:** Two third-party AI-agent "skills" installed on 10-02 with `npx skills add vercel-labs/vgpu`.
  - **vgpu** teaches an agent to build WebGPU (browser/Node GPU) apps with Vercel's `vgpu` library, plus Blender baking/export tips for that runtime.
  - **vgpu-agent-flow** is the vgpu repository's own team workflow. It expects a `subharness` CLI, `.subharness/agents/`, pnpm and `.github/guides/workflow-context.md`, **none of which exist here**.
  - The symlinks make Claude Code list both as available skills in every session.
- **Date:** committed 10-06 (in the bulk commit `7ecf8100`), written 10-02; tracked.
- **Status:** unused by the product.
  - The game is Unity URP + SwiftUI/SceneKit + Node; there is no WebGPU, WGSL or `@vgpu` anywhere. The only "WebGPU" hits are inside vendored three.js in two archived ArtDir viewers.
  - Two project briefs explicitly forbid it: `ArtDir/environments/12_VENUE_PRESENCE.txt` ("FORBIDDEN: … installing vgpu") and `work/venue-presence/PLAN.md` ("no vgpu install").
  - Risk: `vgpu-agent-flow/SKILL.md` line 63 tells agents to "follow AGENTS.md". In this repo, that is the Party Tennis rulebook, not vgpu's.
- **Linked by:** `skills-lock.json` and each other only.
- **If removed:** nothing in the app, tests, CI or docs changes; the two skills disappear from agent sessions. The separately configured `vgpu` MCP server is user-level config, not in the repo.
- **Verdict: SAFE.** Remove all four items together. If Adnan values the Blender baking notes, keep only `.agents/skills/vgpu/blender/references/`. Confidence: high.

`.github/` has no markdown. `CODEOWNERS` (`* @aloniewski2`) and `actions/simulator/action.yml` are not docs; they belong to the CI area.

---

## G. Stale passages inside live docs (edit these, don't delete the files)

**`README.md`**
1. Title and tagline "iPhone Golf Simulator … motion-controlled golf game where the player's body is the controller." → "Party sports (tennis + golf): Unity draws the game on the TV; the phone is the controller with native menus."
2. "Current focus: Playable mock …", the feature bullet list and the whole "Practice Lab" section describe the unreachable camera golf. → Move to `docs/archive/camera-golf/README.md`, or delete with that code.
3. "Getting started: Open `GolfArcade.xcodeproj` … run it on an iPhone" contradicts the top of the README. Per `IOS-UNITY-INTEGRATION.md`, that project is native-only without Unity. → "Open `GolfArcadeUnity.xcworkspace`; follow `SportsLibrary/IOS-UNITY-INTEGRATION.md`."
4. "Requirements: A physical iPhone for camera and Vision body-pose testing" → physical iPhone + TV/AirPlay; Unity 6000.3.24f1; XcodeGen.
5. "Standard characters … permanent male/female character standards" (Sports Library) → current heroes are MatchHeroes derived from `ArtDir/hero/base_lock`; the Sports Library is a historical asset library.
6. "Branches: main / testing / feature/*" → describe reality (see `CONTRIBUTING.md` below).

**`AGENTS.md`**
1. Title "Party Tennis (iPhone)" and "(Name says golf; tennis is active.)" → tennis **and** golf are live (three courses).
2. "Current character authority … grey mannequin body + head/hair only" and "Clothes / bandana / fashion are OUT OF SCOPE until Adnan says otherwise" contradict the 10-08 section further down. → One current scope statement.
3. Delete or mark DONE the "Active handoff — 2026-10-01 (Sonnet) … CHIN + FINGERS still FAIL" section. It sends agents to `HANDOFF_Sonnet…`/`PROCESS…`/`GOAL…Micro1e` for finished work.
4. Mark the "Active character beautification instruction — 2026-10-08" plus its three scope clarifications as COMPLETE. Evidence: `ArtDir/hero/clothing/tailored-2026-10-08/proof/GATE_RESULTS.md`. Then name what is actually active now (plaza hub / multiplayer), or say "no active art task".
5. "Tris ≈ 35–50k per base" → the installed male body is 66,158 tris (`INSTALL_LOG_MALE.md`); record the approved exception or the new budget.
6. "Do not … scope-creep into clothes … sport 2" → clothes shipped and golf is live; update.
7. Add `VISUAL_OVERHAUL_ART_BIBLE.md` to the authority docs.
8. The "Bald reference update — 2026-09-30" paragraph is pasted verbatim into four docs (AGENTS, art-bible, HERO.md, PLAN_HeroBase). Keep it in one place.

**`CONTRIBUTING.md`**
1. "Branch from `testing` … PR into `testing` … promote to `main`" → `testing` was last updated 2026-09-20; `main` receives direct commits; active branches are `final-build`, `integrate/*`, `claude/*`, `codex/*`, `local/*`. Write the flow that is really used.
2. "Direct pushes, force pushes, and branch deletion are disabled for `main` and `testing`" → false today (GitHub API: `main` `protected: false`).
3. "Checks" lists only `build-and-test` and `ui-tests (CameraPlayTests, CoursePlayTests)`.
   - Add `netcode`, `server` and the Pages deploy (all on `main`).
   - Note CI only runs for `main`/`testing`.
   - The two UI classes target the old camera-golf menu: `menuSolo` exists only in `MainMenuView`.
4. The UI-test launch-argument list (`-startInCameraMode`, `-fixtureHumanSwing`, `-startCourse easy|medium|hard`, …) is old camera-golf. → Replace with the arguments of the current UI tests (`MenuBetaUITests`, `SportsNavigationTests`, …), or drop it.

**`CLAUDE_KICKOFF.md`**
- "Active plan (right now: `PLAN_HeroBase_MaleFemale_Lock.md`)" → current plan, or delete the file (DOC-02).

**`art-bible.md`**
- "Grey mannequin body (placeholder for clothes Adnan will create)", "Body suit (temp)", "Outfit colors come later … do not invent a permanent kit", "Customization (future)" and the bald update's "No clothes, rig, Mixamo or Animator" → clothes (customizable colours), the 53-bone rig and the clip set now exist.
- Title "Party Tennis" → party sports.

**`ArtDir/hero/HERO.md`**
- "What ships now: HeroBase prefab + Body_M + Hair_M" → the in-game heroes are `Characters/MatchHeroes` built from the base.
- "Out of scope now: outfits, Mixamo, animator, racket" → done.
- "Gate: `proof/GATE_RESULTS.md`" → that is the 09-30 BASE FAIL snapshot; point to `parallel_prompts/INSTALL_LOG_MALE.md` and the latest results.

**`ArtDir/hero/base_lock/METRICS.md`**
- Shows the 09-30 counts (male 39,268 tris). The file is generated by `package_review.py`, so re-run it or add a note pointing to the install log.

**`Unity/README.md`**
- "Current runtime: golf now uses our permanent V4 male/female characters, Golf Kit 1, held driver and V4 drive animation" → the golfer is the match hero (`docs/matchhero-golfer.md`).
- "combined on `feature/standard-characters-integration`" → old branch.
- "It runs on the phone, so the same device shows the hole" → that is only true for the stand-alone build; in the iPhone app Unity renders to the TV.
- Courses → Cliffside 7/8/9/12/15, Wild Isles 13/14/17–20, Magma Open 10/16/21–23.
- Online/2-player section → legacy (take `main`'s banner).

**`SportsLibrary/README.md`, `CHARACTER-STANDARD.md`, `FLOATING-HANDS-TEST.md`, `UNITY-HANDOFF.md`**
- "Permanent standard characters for every sport", "floating hands, no visible shoulders or arms, across all sports", "All standard Unity characters now default to hands only" → add a top banner: "Historical (09-20). The game's heroes are the HeroBase/MatchHeroes with full arms; bowling/boxing are not in the game."
- Do **not** change `Characters/standard-characters.json` `status`: `verify_library.py` asserts it.

**`SportsLibrary/MULTIPLAYER.md`**
- "No online/lobby entry screens are installed" and "Future SwiftUI/UIKit integration" → already rewritten on `origin/main`; merge rather than re-edit.

**`SportsLibrary/IOS-UNITY-INTEGRATION.md`**
- "Exposed games are the current Cliffside golf course and Tennis Rally" → three courses plus tennis venues.
- "Xcode 27" vs README "Xcode 26" → make them agree.

**`docs/magma-open.md`**
- "three holes (21–23)" → five holes: 10 Crater, 16 Volcano Rim, 21–23.

**`docs/club-ui.md`**
- "Every menu in the golf game is on … Island Sports Club … rebuilt in Unity's uGUI" → these are the stand-alone Unity build's menus; the iPhone app's menus are native SwiftUI (`ClubDesign.swift`/`IslandScreens.swift`) plus the 10-10 plaza.

**`server/README.md`**
- "Everyone plays on their own phone, with their own golfer" → add "Used by the stand-alone Unity build's Online mode; the iPhone app's multiplayer is Game Center/Bonjour (`SportsLibrary/MULTIPLAYER.md`)". This matches `origin/main`'s Unity README.

**`Unity/Assets/Characters/HeroBase/README.md`**
- Add one line: the rigged, dressed in-game versions are `Characters/MatchHeroes`.

**`VISUAL_OVERHAUL_ART_BIBLE.md`**
- The concept path `/Users/adnanyonathan/Documents/Codex/2026-10-06/open-4/outputs/visual-overhaul/after/` is outside the repo (7 files, this Mac only). → Copy them into `ArtDir/` and repoint.

**`ArtDir/hero/kit/premium/tailoring4/README.md`**
- "Status: actual review in progress" → superseded by tailored-2026-10-08.

**`ArtDir/screenshots/golf_course_standard/GATE_RESULTS.md`**
- Fix 3 broken links (two missing `.html` viewers; the plan is at `../../environments/GOLF_ALL_COURSES_POSTCARD_STANDARD_PLAN.md`).

**`PLAN_MenuHub_WalkableWorld.md`**
- Commit it. The copy on `integrate/main-2026-10-10` is the older "Nothing is built yet" version.

**`.agents/skills/vgpu-agent-flow/SKILL.md`**
- "follow AGENTS.md and `.github/guides/workflow-context.md`" would send an agent to this repo's tennis rulebook and to a missing file. Moot if DOC-72 is removed.

---

## H. Cross-area links (for the other auditors)

**CI / Pages**
- Pages = `static.yml` → `site/`, from `main` and `final-build-10/9` only. `site/` is missing on `final-build`, so if `final-build` becomes `main`, Pages has nothing to deploy. `docs/` is never published.
- `netcode.yml` (on `main` only) cites `Tools/netsim/README.md`, which is missing here.
- `ios.yml` `ui-tests` run only `CameraPlayTests`/`CoursePlayTests`. These target the old camera-golf menu: `menuSolo` lives only in `MainMenuView`, which is reachable only via `ContentView`'s `#Preview`. They are probably failing or meaningless (tests area). The newer UI tests (`MenuBetaUITests`, `SportsNavigationTests`, …) are not in the CI matrix.

**Tests that write into ArtDir**
- `GolfArcadeTests/TennisMenuSnapshotTests.swift` writes into `ArtDir/ui/toybox-simple-v6/runtime/` and `ArtDir/screenshots/emote_loadout/`. That is why 20 ArtDir PNGs show as modified (16 + 4). They are test outputs, not source art (ArtDir area).

**Tools that require docs**
- `SportsLibrary/Tools/verify_library.py` reads 8 SportsLibrary docs and checks their links (including `../CONTRIBUTING.md`).
- `Tools/HeroBase/phase0.py` requires `base_lock/{ACCEPTANCE,METRICS,SCALE}.md`.
- `Tools/HeroBase/package_review.py` writes or reads `base_lock` docs and copies the Unity HeroBase README.

**Docs that are the only description of something**
- `docs/matchhero-golfer.md`: the current golfer pipeline.
- `blender/scripts/POSTCARDS_README.md`: the postcard-hole pipeline.
- `Tools/PointClips/README.md`: the recorder smoke test.
- `ArtDir/review/point-clips/RECORDING.md`: Save Last Point.
- `SportsLibrary/IOS-UNITY-INTEGRATION.md`: the build and install procedure for `Unity/Tools/build-integrated-ios.sh` and `install-phone.sh`.
- `SportsLibrary/MULTIPLAYER.md`: the native multiplayer protocol.
- `docs/tennis-playtest.md`: device QA.
- `ArtDir/hero/base_lock/parallel_prompts/INSTALL_LOG_MALE.md`: provenance and rollback of the installed male body.
- `ArtDir/golf/vendor/COASTAL_LICENSES.md` and `ArtDir/hero/source/reference/PROVENANCE.md`: licences.
- `ArtDir/anims/source/mocapdata-tennis/SOURCE.md`: licence of the third-party mocap pack.

**Code comments that cite docs** (keep the doc, or update the comment if it moves)
- `TennisLook.cs:269` and `CharacterModelPreview.swift:364` → `ArtDir/hero/v5_proof/LIGHTING.md`
- `HeroGolfer.cs` → `docs/hero-golfer.md`, `docs/avatar-kit.md`
- `CharacterLook.cs` → `docs/hero-golfer.md`
- `BackendClient.cs`, `BuildMenu.cs` → `server/README.md`
- `avatar_*.py` → `avatar-kit.md`
- `matchhero_golf.py`, `matchhero_mhhair.py` → `matchhero-golfer.md`
- `ClubDesign.swift` → `ArtDir/ui/toybox-simple-v6`

**work/ files that live code depends on**
- Live test and editor code cites **untracked** `work/` specs as the only gate definitions:
  - `work/hero-dressed/GATE_DEFS_preregistered.md` (`DressedHeroSnapshotTests.swift`)
  - `work/locker-mirror/GATE_DEFS_preregistered.md` (`LockerMirrorTests.swift`, which also writes proof into `work/locker-mirror/proof/`)
  - `work/postcard-look/{RUNTIME,LOOK_CONTRACT,TEXTURES}.md` and similar (`PostcardLookStills.cs`, `GolfLookGrassCheck.cs`, `GolfPlantSwayCheck.cs`, `GolfWindSwayTests.cs`, …)
- Cleaning `work/` removes these. Copy them into `docs/` first (work area).
- `ArtDir/hero/source/reference/PROVENANCE.md` points at `work/reference-rebuild/vendor/human-base-meshes-bundle-v1.4.1/` (untracked); work cleanup would orphan the licence source.
- `work/golf-motion-fit/rollback/AGENTS.md` is a stale second `AGENTS.md`. Codex applies nested `AGENTS.md` files inside that folder (work area).

**Server / code**
- Two multiplayer stacks exist:
  - The Node `server/` + Unity `Net/Online/BackendClient` (stand-alone Unity build's home screen; `GolfGame` shows that menu only when `!NativeSportsSession.Active`).
  - Native Game Center/Bonjour `MultiplayerService`.
- `origin/main`'s Unity README already labels the Node path "legacy for the iPhone app". The server and code areas should decide whether `server/` is still needed.

**Asset area: docs that follow an asset decision**
- Hero01/V4/V5/V6 docs (DOC-49/50) ↔ `Unity/Assets/ArtDirection/{Hero01,HeroV5AstraTex,HeroV6,HeroV6Polish}` and `ArtDir/hero/v5*`/`v6*` binaries.
- SportsLibrary docs (DOC-30/31/33/35) ↔ SportsLibrary binaries and `Resources/StandardCharacters`.
- `blender/characters/golf` README ↔ `golfer-*.glb`.
- `docs/avatar-kit.md` ↔ `Unity/Assets/Archive/IconKit`.

**ArtDir briefs in `.txt`**
- About 180 `.txt` files in ArtDir are the same kind of agent briefs as the root PLAN/GOAL `.md` files: numbered SOL_GOAL briefs 12–38, `base_lock/parallel_prompts/*.txt`, `face_lock*/PASTE_PROMPT_*.txt`. They are outside this `.md` audit and many reference AGENTS.md and art-bible.md (ArtDir area).

**Branch divergence**
- 15 docs differ between `final-build` and `integrate/main-2026-10-10`, which also has `PLAN_Multiplayer_OnlineLocal.md`, `docs/tennis-feel-pass-2026-10-10.md` and `Tools/netsim/README.md`. Run any doc cleanup after the branches are reconciled.

---

## I. Verdict counts

| Verdict | Entries | .md files | Other files |
|---|---:|---:|---|
| KEEP | 35 | 77 | `HeroBase/README.md.meta` |
| SAFE | 10 | 36 | 2 `.claude/skills` symlinks, `skills-lock.json` |
| SAFE+FIX | 2 | 16 | — |
| DECIDE | 25 | 165 | `docs/character-handoff/active-prefab-manifest.json` |
| **Total** | **72** | **294** | |

The breakdown:
- **KEEP (77):** many need edits (section G). The heaviest are `AGENTS.md`, `README.md`, `CONTRIBUTING.md` and `Unity/README.md`.
- **SAFE (36):**
  - `CLAUDE_KICKOFF.md`
  - PLAN1–3B (9) and Score80 (2)
  - V5/V6 root plans (5)
  - `tennis-ultimates-and-dive.md`
  - SportsLibrary Design briefs (2) and `ORIGINAL-NOTES.md`
  - parallel-8 and skin briefs (2)
  - UniMate review
  - vgpu skills (12)
- **SAFE+FIX (16):** the 15 GOAL_HeroBase prompts and `HANDOFF_Sonnet…`. The fix: remove AGENTS.md's "Active handoff" section first.
- **DECIDE (165):** mostly ArtDir and SportsLibrary history that should be deleted or archived together with the assets or code it describes:
  - Hero01/V4 (23) and V5/V6 (17)
  - SportsLibrary nav, V4 integration, prompts and previews (47)
  - superseded HeroBase proof iterations (20)
  - September plan results (10)
  - camera-golf docs (7)
  - tennis change notes (9)
  - UI explorations and Motion Club (8)
  - the rest

Suggested order:
1. Fix `AGENTS.md`, `README.md` and `CONTRIBUTING.md`.
2. Remove the SAFE and SAFE+FIX set: 52 files plus the skill links, mostly at the repo root. That clears 33 of the 42 root `.md` files.
3. Settle each DECIDE group together with the matching code or asset decision.

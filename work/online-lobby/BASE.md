# Online lobby baseline — 2026-10-05

Repository: iphone-golf-simulator, branch newmapsandmenus.
Read prompt 27_ONLINE_LOBBY_UI.txt in full. Existing changes inventoried in initial-status.txt; unrelated gameplay / emote / course work is preserved.

- Swift build + launch: PASS on iPhone 17 Pro simulator, iOS 26.5 (XcodeBuildMCP).
- Native multiplayer tests: PASS, 17/17, before application changes.
- Current app roots: PASS, six phone and six TV captures in before/ (home, party, locker gear, locker customize, map, post-match). Captured by testOnlineLobbyBeforeScreens before application changes. TV is the actual TennisTVRoot rendered in a simulator UIWindow at 1280x720, not a tvOS port.
- work/multiplayer/check.sh: runtime, EditMode and PlayMode assemblies compile. Bare .NET runner: 182 passed, 3 failed (TennisCampaignBalanceTests x2 NullReferenceException; TennisTutorialTests.KitColoursParseFromTheLaunchMessage SecurityException). The script returns 0 despite failed test output. These are baseline failures; verify in the actual Unity runner before final gates.
- Game Center device test: pending availability check.

Next: P0 bounded look payloads, owner validation, emote cooldown, golf venues and protocol v2; then Unity export in an APFS clone and live lobby UI / proof.

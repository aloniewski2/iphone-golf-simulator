# Post-game phone controls — 2026-09-29

## Report and evidence
The physical phone stayed on the racket screen when the TV appeared finished. Retrieved Documents/SportsDiagnostics.log from the phone; native/Unity polling was still running, but the retained log contained no `match finished` entry. The subsequent physical-device autoplay run reproduced the real failure at match point; see confirmed root cause below.

Confirmed code defects: `advanceAfterMatch(choice)` ignored its choice and always opened rewards. Completion controls disappeared automatically after 3.4 seconds into the rewards/remote flow. The controller's poll timer used the default run-loop mode (can suspend during touch tracking). The durable-result bridge pointers were optional despite being required for recovery.

## Changes
- Full phone root and racket controller use the existing shared finish panel: Next round for eligible campaign wins, Replay, Exit to menu, optional View rewards. No automatic dismissal. Hidden until completion.
- Action handler honors its choice; Exit goes to menu directly, Replay preserves round, Next goes to next unlocked opponent. Rewards remain available and XP is still banked once at finish.
- Unity regular feedback now includes matchComplete/matchWon/finalScore; Swift consumes this authoritative state even if the one-shot matchOver event is lost. Finished-phase events also consume it.
- Result polling no longer depends on the loading-ready flag. Poll timer runs in common modes. Runtime fails explicitly if durable-result symbols are unavailable. Phase transitions logged for future device diagnosis.
- No swing requirement. Mid-match pause/quit retained. No changes to match scoring, animation, camera or wardrobe.

## Verification
- Native tests: live heartbeat does not expose actions; completed heartbeat does; duplicate completion cannot advance twice; buttons persist beyond old auto-dismiss; Next selects next campaign round; Replay after a loss keeps same round; Exit bypasses story/rewards. Passed.
- Actual SportsHome rendered in native simulator with completed heartbeat (not just an isolated button): ArtDir/review/post-match/phone-finish-actions.png.
- Unity iOS export succeeded with zero errors.
- Physical AirPlay completion verified after the root fix: real autoplay short match completed 1–0; native received score/matchOver, logged external=true and phoneVisible=true, and captured actual full-screen Replay / Exit controls. Screenshot: ArtDir/review/post-match/phone-finish-real-device.png. Campaign Next routing separately covered by native tests.

## Confirmed root cause from physical-device simulation
`TennisGame.IsMatchPoint` copies the `TennisMatch` struct, then calls `AwardPoint` to predict a win. Its `List<string> SetScores` was still shared with the real match. Every HUD refresh at a potential set/match point appended a hypothetical result to the real list. Device trace showed hundreds of repeated `1–0` entries before the one real set had finished. The resulting UTF-8 result/event exceeded `SportsEmit`'s 4096-byte limit and `SportsReadTennisResult`'s 1024-byte buffer. Messages were dropped; this was not a Unity main-thread hang.

Fix: `EndSet` detaches the score list before append (copy-on-write), preserving struct-copy forecast semantics. No scoring rules changed. New tests run 1200 forecasts for either winner and a multi-set match. Before: 40 passed / 3 failed. After: all 43 scoring tests passed.

The verification launch `-benchTennis --postgame-check` plays a real one-game match using autoplay, then saves `Documents/PostGameDevice.png` from the actual phone controller window. No fabricated result event. On-phone preview now raises that full native controller at completion (previously it had only a 150-point overlay); AirPlay keeps the game on its external display.

Final device proof: 2026-09-29 01:30:45 EDT match finished, 01:30:46 screenshot captured. This was a non-campaign verification match, so Replay/Exit were the valid choices. Campaign Next is not claimed as physically tapped. Normal launch restored after verification.

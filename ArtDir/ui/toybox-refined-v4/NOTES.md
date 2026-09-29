# Toybox revision 4

## Scope
A revised review prototype, not an installed Unity/iPhone change. Kept the v3 Toybox home and sport-menu materials/layout. Changed their copy. Rebuilt gameplay HUD, score detail, point call, loading warm-up, pause and results. No serve screen is proposed in this revision. No serve-meter source, value, placement, timing, color or input was edited. Old rejected serve mockups remain in the archived v3 pack only.

## Reference research
- Rocket League, official “First Look: Play Menu Changes…”: clear mode/task labels including Training and Play Offline, with deliberate hierarchy and fewer navigation steps. https://www.rocketleague.com/news/first-look-play-menu-changes-coming-to-rocket-league
- Nintendo’s Switch Sports update/support page: Play Globally, Play Locally and Play with Friends are direct action labels. This informed our copy style, not a plan to add unavailable online features. https://en-americas-support.nintendo.com/app/answers/detail/a_id/58598/~/how-to-update-nintendo-switch-sports
- TopSpin 2K25 gameplay screenshot references: compact court-side score display, separate numerical columns, minimal interference with play. Visual references, not assets copied into our design: https://www.purexbox.com/games/xbox-series-x/topspin_2k25 and https://www.jbhifi.com.au/blogs/games/topspin-2k25-review-have-an-ace-day
- Official TopSpin manual was found in search, but full PDF retrieval failed. No claim of a complete manual audit or exact font measurement.

## Copy changes
| Previous | Replacement |
|---|---|
| Good times. Great games. | Island Sports Club |
| Let’s play | Play |
| Your locker | Locker |
| What’s the game? | Select a sport |
| Good times ahead. | Tropical Open / Loading match… |
| Court’s almost yours | Loading match… |
| Take a breather / Back to the court | Paused / Resume |
| That’s a win | Match won |
| Run it back? | Match lost / Rematch |
| Exit to menu | Main Menu |

Supporting loading copy: “Practice your swing”, “Swing your phone to practice”, “Practice only · No score”. No forced jokes or promotional slogans.

## Scorebug design
Stable grid for name, games and points. Tabular numerals with points carrying the highest visual weight. A fine blue edge and shallow shadow retain Toybox material cues without turning each value into a separate toy button. Deuce and advantage use the same point cell; tiebreak points can expand to two digits. Set state stays in a compact footer. The serving dot has a text equivalent. Actual fixture/format and serving labels come from game state in production.

Short shot feedback sits in a single lower-left readout: quality, stroke, actual speed. Point calls retain the gameplay camera. No new serve meter or renamed timing grades.

## Repeatable loading practice
Browser demonstration uses 60 existing V4 forehand frames (every second frame from fullstroke_grip_playmode/frame_0240…0358.png), played at 15 fps. These are unchanged in-engine render files, not generated substitute motion. Press Space or the browser preview Swing button. It returns to ready and accepts another swing indefinitely; a swing during playback queues one subsequent swing rather than infinitely buffering.

“Simulate match ready” tests progression to the venue intro after the active demo take. It does not represent real asynchronous engine loading. The static screenshot shows the intended full-size character and interface, while the browser demonstrates repeat input.

### Required app integration (not performed in this visual-review task)
1. Keep a small, already-loaded hero preview scene alive while the match loads additively/asynchronously. Reuse the equipped wardrobe, existing PlayableGraph and forehand/backhand clips. If the preview hero is not ready, show a lightweight loading state until it is.
2. Route phone motion events to a **loading-practice** sink only while that state is active. Existing phone detection thresholds continue to apply; do not use match hit simulation or servo/toss meter code.
3. One swing per accepted gesture; allow repeated gestures. Coalesce rapid input, never leave a long queue. No hit points, stamina costs, calibration samples, XP or progression effects. Never send warm-up swings into the new match.
4. Expose the preloaded preview on the TV. Phone can show a brief “Swing to practice” instruction; result buttons and current in-game controller behavior remain separate.
5. Real LoadingModel readiness owns progress and transition. Swings never artificially fill the bar or delay engine work. Once ready, finish at a safe contact/recovery boundary, capped around 300 ms, clear queued gestures, then enter the existing intro. The four-second browser source take is illustrative, not the required runtime transition latency.
6. Dispose preview ownership and restore the match input route on success, cancellation, failure and app backgrounding. Retry/Main Menu remain available on actual load failure.
7. Tests: repeated gestures, loading ready during a swing, failed load, display disconnect, scene cancellation, no duplicate match swing at transition, no score/calibration contamination. Preserve the serve meter byte-for-byte.

## Files / verification
- index.html: nine selectable review screens and the interactive loading demonstration.
- screens/: full-size PNGs; review-sheet.png groups eight key states.
- No game implementation code changed. Current app remains as it was.
- Browser verification: nine renders, no JS errors, repeated swing playback and ready-to-intro transition verified.

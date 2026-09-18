# Swing Feedback Design

The arcade feedback loop uses anticipation, release, impact, and payoff. It is inspired by interaction patterns rather than copied assets or proprietary implementations.

## Reference patterns

- [EA Sports PGA Tour](https://www.ea.com/games/ea-sports-pga-tour/pga-tour/news/ea-sports-pga-tour-gameplay-deep-dive) connects backswing length and follow-through speed to the shot, then explains over- and under-swing after contact.
- EA's [accessible gameplay settings](https://www.ea.com/able/resources/ea-sports-pga-tour/ea-sports-pga-tour/xbx/gameplay) describe showing swing path and length while the swing is happening. This supports a readable, peripheral visual cue instead of requiring the player to study numbers.
- PlayStation describes progressive resistance as a way to communicate the [tension of an action](https://blog.playstation.com/archive/2020/04/07/introducing-dualsense-the-new-wireless-game-controller-for-playstation-5/feed/). An iPhone cannot create trigger resistance, so the app translates this principle into a rising continuous haptic and a tightening visual ring.
- Research into [multimodal golf-swing feedback](https://www.jstage.jst.go.jp/article/ipsjjip/30/0/30_107/_article/-char/en) supports combining feedback modalities instead of relying on a single cue.
- Apple's [haptics guidance](https://developer.apple.com/design/human-interface-guidelines/playing-haptics) recommends synchronizing tactile, visual, and auditory feedback, making haptics optional, avoiding overuse, and accounting for disruption to the camera and other sensors.

## Arcade pattern

| Swing moment | Tactile feedback | Visual feedback |
| --- | --- | --- |
| Address | None | Calm mint targeting rings |
| Backswing | Soft continuous intensity ramp over 1.1 seconds | Rings tighten and change from mint to yellow; `LOAD` appears |
| Downswing | Continuous haptic stops immediately | Rings release outward and turn orange |
| Impact | Dense transient followed 45 ms later by a sharper transient | Brief white contact flash and expanded ring |
| Follow-through | None | Feedback clears so ball flight owns attention |

The backswing ceiling is intentionally subtle because the iPhone is expected to sit on a tripod and strong vibration can degrade camera tracking. Impact is brief and unmistakable. Both haptics have a persistent on/off control; the visual channel always remains available.

## Physical-device validation

Test on multiple supported iPhones with and without a tripod. Record whether the pattern is perceptible at typical play distance, whether impact vibration changes tracked landmarks, and whether the tension ramp reaches its strongest point near the top of a normal backswing. If tripod vibration is visible in pose data, retain the visual cue and reduce or disable continuous haptics for mounted play.

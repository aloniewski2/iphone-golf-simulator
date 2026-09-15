# Playable range mock

Launch `GolfArcade` in Xcode on an iPhone simulator or physical iPhone. The default screen is now **Fairway / Meadow Club**, an original, fully local arcade range. No camera permission, account, downloaded assets, or server is needed for touch play.

## Play

1. Choose Driver, Iron, Wedge, or Putter. Labels show the full-power distance from the flight model.
2. Move the aim slider. Negative angles aim left; positive aim right.
3. Pull **down** on the swing pad. A 100-point downward drag equals full power. Release to launch. A tiny drag cancels without consuming a shot.
4. Watch the ball fly, bounce, and roll. Targets score by final resting position: center 100, middle 60, outer ring 30; other fairway shots earn 10.
5. Select Next shot. After five shots, the app shows the scorecard and saves a personal best. Play again starts a fresh round.

**Demo shot** uses the same launch path at 75% power. **Replay** animates the last shot without spending another shot or awarding points twice.

## Ball flight

Shots are simulated, not scripted: club-head speed from your swing → ball speed via the club's smash factor, launch angle and backspin per club, then gravity, aerodynamic drag, and Magnus lift (spin decays in the air), followed by bounce and roll on a firm fairway. A draw or fade tilts the spin axis so the ball curves. At full power the model gives roughly driver 254 + 23 yd (apex 41 yd), iron 148 + 15, wedge 82 + 10, and a 25-yard putt that rolls from the first inch. Flights play in real time (about 7–10 s for a full shot). See `GolfArcade/Mock/BallFlight.swift`; every coefficient is named.

## Swing the phone

Switch the input picker to **Phone** (physical iPhone only; the simulator has no motion sensors). Grip the phone like a club and hold it still for a moment: the panel says **Ready. Take it back.** Rotate it away for the backswing — load, tension haptics, and the club in the scene follow how far you have turned — then swing through. The ball launches the instant the phone passes back through its address position. Power is the peak rotation speed of the swing, scaled per club (a full driver swing needs a fast phone; a putt is a gentle stroke). Aim still comes from the slider. A slow waggle back to address cancels without spending a shot, and after a shot lands the next backswing tees up the next ball automatically.

Hold on tight and clear the space around you. The recognizer is `MotionSwingDetector` in `GolfArcade/Mock/MotionSwing.swift`; every threshold is a named property and the unit tests drive it with synthetic swings.

## Camera

Switch the input picker to **Camera** and prop the phone up facing you (front camera, portrait). It runs the widest front-camera format zoomed all the way out, and the small picture-in-picture shows the whole frame with the tracked skeleton, so you can see you are in view without stepping far back. Only your **shoulders and one hand** need to be visible.

The model is Wii Sports golf read by camera: the power meter is the **arm arc**, the angle of your hands around your shoulders from where they hung at address. Hands level with the shoulders is about 90°; ~140° is a full backswing and 100 % on the meter. Downswing speed adds the final share of power (`speedWeight`), so a lazy full swing and a quick short one both make sense. Swing well past full (165°+) and the shot hooks, harder the further over you go. A slow return is a practice swing and does not spend a shot. Brief tracking loss at the top is skipped. `ArmSwingDetector` in `GolfArcade/Mock/ArmSwing.swift` holds every threshold and is unit-tested with synthetic swings.

The golfer beside the tee is a Mii-style figure with its own fixed-length arms on one clean swing arc; your tracked arm arc only sets **how far along the swing it is**, so nothing stretches or jitters. When the ball launches it plays its own downswing and follow-through. In Touch and Phone modes the load meter drives the same arc.

Settings provide sound, haptics, instructions, round restart, and the existing **Camera lab**. The lab is the separate experimental body-tracking prototype; live camera swings do not yet drive the new target challenge.

## What to test

- Try the same club at 30%, 60%, and 100% power; distance should increase.
- Aim left/right; both the guide and landing should change.
- Driver: about 89% power straight ahead reaches the Summit center at 180 yards.
- Putter: the ball stays on the ground and decelerates.
- Replay a shot: score and shot count should remain unchanged.
- Finish five shots and restart; personal best should survive relaunch.
- Background the app during charge: no accidental shot, no ongoing tone/vibration. During flight, time pauses and resumes.
- Swing phone: a slow backswing and return should cancel; a real swing should launch; a faster swing should go farther; the putter should need only a short stroke.
- Sound follows the phone's Silent Mode. Use a physical iPhone to assess tactile feedback. Simulator renders visuals and audio but cannot reproduce a Taptic Engine.

## Implementation limits

Contact and terrain are intentionally simplified for an arcade mock. The 3D scene uses original SceneKit geometry and procedurally generated sounds. This is a single range challenge, not an 18-hole course or validated golf simulator. Phone-swing direction (face angle, path) is not yet read from the sensors, and the camera reads shape only from over-swing; aim comes from the slider. The flight model has no wind, slope, or lie, and strike quality is assumed centred. AirPlay-specific display layout, live-camera integration into this challenge, spin, wind, and calibrated body measurements remain future work.

## Verification

The shared `GolfArcade` scheme runs domain tests and an automated UI test that drags a real swing, checks replay, completes five shots, and restarts the round. Run Product → Test in Xcode or `xcodebuild test` with a valid simulator destination. The CI check runs the same scheme before any protected branch merge.

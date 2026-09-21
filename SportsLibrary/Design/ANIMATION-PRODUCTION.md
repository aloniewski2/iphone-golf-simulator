# Animation production checklist

This is a production plan, not an inventory of completed clips. The accompanying images show pose intent. Use the final rig and actual recorded sport references to determine timing, contact and anatomical mechanics.

## First playable priority

| Sport | Required base states and transitions | Runtime responsibility |
|---|---|---|
| Tennis | ready, split step, run L/R, start/stop L/R, direction reversal, forehand, two-hand backhand, serve, recovery | Separate locomotion from racket gesture; blend lower-body movement with upper-body shot; select handedness before play |
| Boxing | first-person guard, jab, cross, hook L/R, uppercut L/R, punch recovery, shuffle L/R, forward/back, high/body guard | Select punch type with phone button; discriminate punch from movement; blend POV arms and opponent full-body motions |
| Bowling | ready with two-hand support, pushaway, backswing, approach steps, slide, release, follow-through, stand/watch | Button hold attaches ball; release detaches it; approach/arm phase and release timing affect shot within game-defined rules |
| Golf | address, takeaway, backswing, downswing, impact, follow-through, return to address; putt and chip | Phone controls swing, not walking; maintain both hands on club and rotate hips/chest |

## Expanded action coverage

### Tennis

- Locomotion: idle weight shifts, left/right shuffle, left/right sprint, forward charge, backpedal, crossover retreat, acceleration, braking, reversal and split-step landing.
- Shots: stationary/running forehand and backhand, low pickup, wide reach, volley forehand/backhand, overhead smash, lob, serve toss/windup/contact/landing.
- Emergency: forehand dive, backhand dive, airborne contact, side landing, ground-to-kneel, stand-up, missed swing, late reach, stumble recovery.
- Transitions: ready→run→plant→shot→recover; retreat→overhead→land→ready; dive→land→get-up→ready.
- Left-handed play needs mirrored AND grip-checked variants. A backhand is not automatically a racket hand swap. Support hand may join the dominant grip; deliberate hand swaps would need their own animation and input rule.

### Boxing

- Attacks: jab, cross, lead/rear hook, lead/rear uppercut, body variants, jab-cross, jab-cross-hook, return to guard, interrupted punch.
- Defense: high block, body block, parry L/R, slip L/R, duck, roll under L/R, guard recovery.
- Movement: lateral shuffle L/R, step forward/back, pivot L/R, step-in punch, retreat to guard.
- Reactions: head/body hit reaction, blocked-hit reaction, stagger, knockdown, get-up, tired guard, victory/defeat—only if chosen game rules need them.
- Author first-person arms separately from the opponent/full-body presentation. Keep guard visible, wrists aligned and camera roll/head-bob restrained. A single phone cannot directly track both fists independently; the team must decide how lead/rear hand selection works.

### Bowling

- Ready, two-hand support, grip adjustment, approach start/steps, pushaway, downswing, backswing apex, slide, release and follow-through.
- Straight/hook intent, approach correction, early/late button-release response, aborted approach and retained-ball reset.
- Hold finish, recover stance, watch ball, return from foul line, strike/spare/miss reactions if the game needs them.
- Author separate left-handed delivery and grip. Do not switch the ball hand inside a delivery.
- Ball physics must start at the actual hand-detach event. The image sheets do not define launch speed, spin, score or early/late release outcomes.

### Golf

- Address, waggle, full/half swing, chip, pitch, putt, backswing pause, aborted swing, reset and finish hold.
- Club-specific hand placement and ground contact; right/left handed variants.
- Bunker, rough and celebration/failure variants can follow once mechanics are chosen.
- Hip turn leads through downswing, chest follows, wrists release near contact; do not animate only arms.

## Clip conventions to agree with Unity collaborator

- Names: Sport_Action_Hand_Variant (for example Tennis_RunLeft_RH, Boxing_UppercutLead_LH, Bowling_Release_RH, Golf_Putt_LH).
- Separate loopable locomotion from one-shot actions; keep start/stop transitions explicit.
- Choose in-place versus root-motion per clip before production. Phone-driven displacement should not also be applied a second time by root motion.
- Mark contact, ball detach, foot plant, recovery-complete and cancel windows. Do not fix their timestamps from these still images.
- Reusable male/female animation depends on compatible final skeletons and verified retargeting, not identical-looking mockups.
- Test actions at actual gameplay camera distance, including high/body guard and extreme reaches.

## Review notes on the generated sheets

- These are concepts, not anatomical ground truth. Small labels, racket/ball positions, extreme limb angles and inferred trajectories require animator review.
- Boxing uppercuts on the full-body sheet are overextended: keep elbow bent and drive through the target near chin/body level, not into an overhead reach. Hooks need a clear bent-elbow horizontal arc.
- First-person slip panels communicate visual intent only; implement player camera/body movement, not just an opponent animation.
- Bowling backswing perspective must not be interpreted as changing hands. Retain the ball in the same dominant hand, plant the opposite foot and let the free arm balance.
- Tennis forehands should keep a purposeful free arm for balance. Two-hand backhands must maintain two distinct grips; image silhouettes can obscure this.

## Not implemented here

No new baked skeletal animation clips, runtime state machines, phone tracking, UI, scoring/rules, camera controller or final character weight painting. The next build step is a small Unity test with one character, one arena and a few core actions before producing the full clip list.

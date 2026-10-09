"""Re-author the V4 tennis clips from key poses (see tennis_motion.py for the description
language). Replaces the action behind each named strip in the V4 track, for both hands and
both rigs, and keys the racket to match. The old actions stay in the file.

Every stroke follows the slice of the clip the runtime plays (TennisRules.StrokePlayhead):
    0-15   unit turn and backswing (also held as the "prepare" pose while the ball comes)
    15-30  forward swing, accelerating into contact at frame 30 (t = .5)
    30-44  follow-through
    44-60  recovery toward the ready stance (plays at authored speed while it fades)

Run:  Blender --background STUDIO.blend --python author_tennis_motion.py -- [--clips A,B] [--rigs Male,Female] [--avatar 0.8]
          [--render DIR] [--save]
"""
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
from tennis_motion import Author, Clip  # noqa: E402

# ---------------------------------------------------------------- shared poses

FEET_READY = dict(foot_l=(-.25, .02, 8, 0, 0), foot_r=(.25, .02, -8, 0, 0))
TRUNK_READY = dict(hip_turn=(0, 12, 0), spine=(0, 6, 0), chest=(0, 4, 0), neck=(0, -6, 0), head=(0, -10, 0))
# Racket held out in front of the belly, head up and to the left, off hand on the throat.
RACKET_READY = dict(grip=(.12, .32, .88), shaft=(-.42, .8, .42), face=(.7, .4, 0), off="racket", elbow=(.3, -.2, -.35))
READY = dict(hips=(0, .02, .71), **TRUNK_READY, **RACKET_READY, **FEET_READY)


def ready_clip():
    c = Clip("Ready", loop=True)
    # Light on the toes: a slow settle and rise, the racket breathing with it.
    c.key(0, **READY)
    c.key(15, hips=(.01, .02, .695), grip=(.12, .32, .865), shaft=(-.4, .81, .43), chest=(2, 5, 0))
    c.key(30, hips=(0, .02, .71), grip=(.12, .32, .88), chest=(0, 4, 0))
    c.key(45, hips=(-.01, .02, .695), grip=(.12, .32, .865), shaft=(-.44, .79, .42), chest=(-2, 5, 0))
    return c


def forehand_clip(name="Forehand", low=0.0, topspin=1.0, open_face=0.0, reach=0.0):
    """The modern forehand: unit turn, loop, racket drop, contact out in front at waist
    height, finish over the opposite shoulder. `low` drops contact (pick-ups), `topspin`
    steepens the low-to-high path, `open_face` tilts the strings up (lobs), `reach` moves
    contact wider."""
    c = Clip(name)
    # Contact height of the grip. The game's balls arrive low (0.2-0.6 m after the bounce),
    # so contact is taken low too: knees bent, racket head a touch below the wrist.
    cu = .82 - low
    c.key(0, **READY)
    # Unit turn: shoulders and hips turn together, racket goes back with both hands.
    c.key(8, hips=(.03, .0, .67 - low * .3), hip_turn=(-35, 12, 0), spine=(-15, 5, 0), chest=(-25, 3, -3),
          neck=(20, -4, 0), head=(22, -8, 0),
          grip=(.28, .12, 1.0), shaft=(.25, -.1, .96), face=(.95, .1, .1), off=(-.02, .42, 1.02), elbow=(.35, -.1, -.3),
          foot_r=(.36, -.03, -45, 0, 0), foot_l=(-.26, .06, 5, 0, 0))
    # Top of the loop: the X-factor -- shoulders turned well past the hips.
    c.key(15, hips=(.07, -.02, .66 - low * .4), hip_turn=(-45, 13, -4), spine=(-20, 5, 0), chest=(-30, 2, -4),
          neck=(25, -4, 0), head=(26, -8, 0),
          grip=(.36, -.18, 1.02), shaft=(.12, -.35, .93), face=(.9, 0, -.4), off=(-.08, .45, 1.06), elbow=(.35, .05, -.35),
          foot_r=(.37, -.05, -50, 0, 0), foot_l=(-.22, .12, 10, 12, 0))
    # Racket drop: the head falls below the hand, lagging behind, as the hips start.
    c.key(22, hips=(.03, .03, .64 - low * .5), hip_turn=(-25, 13, -3), spine=(-12, 5, 0), chest=(-18, 3, -3),
          neck=(15, -4, 0), head=(16, -10, 0),
          grip=(.40, -.04, .84 - low), shaft=(.25, -.85, -.45 * topspin), face=(.95, .15, .1), off=(-.2, .4, 1.0),
          elbow=(.3, .0, -.4), foot_l=(-.22, .14, 10, 0, 0))
    # Just before contact: hips square, the racket still lagging behind the hand.
    c.key(27, hips=(0, .06, .66 - low * .5), hip_turn=(-5, 12, -2), spine=(-4, 5, 0), chest=(-6, 3, -1),
          neck=(5, -4, 0), head=(4, -12, 0),
          grip=(.40 + reach * .5, .18, cu - .06), shaft=(.85, -.45, -.25 * topspin), face=(.35, .92, .15 + open_face * .3))
    # Contact: out in front, strings square to the target, the head level with the hand.
    c.key(30, hips=(-.02, .08, .63 - low * .5), hip_turn=(10, 14, 0), spine=(5, 7, 0), chest=(8, 5, 0),
          neck=(-6, -4, 0), head=(-10, -16, 0),
          grip=(.40 + reach, .30, cu), shaft=(.94, .12, -.12), face=(0, 1, .08 + open_face), off=(-.35, .25, .95),
          elbow=(.35, -.05, -.3), foot_r=(.34, -.04, -35, 22, 0))
    # Brushing up and across.
    c.key(34, hips=(-.03, .08, .7 - low * .3), hip_turn=(28, 9, 1), spine=(12, 5, 0), chest=(16, 3, 1),
          neck=(-10, -4, 0), head=(-14, -10, 0),
          grip=(.14, .46, 1.12 + .08 * topspin), shaft=(-.15, .72, .68), face=(-.8, .3, .4 + open_face * .3), off=(-.35, .3, 1.1))
    # Finish over the left shoulder, off hand catching the throat, right heel up.
    c.key(40, hips=(-.04, .07, .7), hip_turn=(45, 7, 2), spine=(15, 4, 0), chest=(20, 3, 2), neck=(-15, -4, 0), head=(-22, -6, 0),
          grip=(-.3, .2, 1.28), shaft=(-.25, -.72, .64), face=(-.7, .1, -.7), off=(-.32, .32, 1.14), elbow=(.25, .35, -.1),
          foot_r=(.26, .0, -10, 45, 0), foot_l=(-.22, .14, 10, 0, 0))
    c.key(46, grip=(-.26, .22, 1.24), shaft=(-.3, -.68, .66), off=(-.3, .3, 1.12))
    # Recover to the ready stance.
    c.key(54, hips=(0, .03, .685), hip_turn=(10, 11, 0), spine=(3, 6, 0), chest=(4, 4, 0), neck=(-3, -5, 0), head=(-4, -9, 0),
          grip=(.05, .32, 1.0), shaft=(-.45, .66, .6), face=(.66, .5, 0), off="racket", elbow=(.3, -.2, -.35),
          foot_r=(.3, .02, -10, 0, 0), foot_l=(-.3, .02, 10, 0, 0))
    c.key(60, **READY)
    return c


def backhand_clip(name="Backhand", reach=0.0, low=0.0):
    """Two-handed backhand: the off hand stays on the racket; the shoulders turn further than
    on the forehand, contact is out in front on the left, and the finish wraps over the right
    shoulder."""
    c = Clip(name)
    cu = .82 - low
    c.key(0, **READY)
    c.key(8, hips=(-.03, .0, .67), hip_turn=(35, 12, 0), spine=(18, 5, 0), chest=(30, 3, 3), neck=(-22, -4, 0), head=(-24, -8, 0),
          grip=(-.22, .14, .98), shaft=(-.3, -.05, .95), face=(-.95, .1, .1), off="racket", elbow=(.1, .35, -.3),
          foot_l=(-.36, -.03, 45, 0, 0), foot_r=(.26, .06, -5, 0, 0))
    c.key(15, hips=(-.07, -.02, .66 - low * .4), hip_turn=(48, 13, 4), spine=(22, 5, 0), chest=(38, 2, 4), neck=(-28, -4, 0), head=(-28, -8, 0),
          grip=(-.34, -.14, .98), shaft=(-.2, -.4, .9), face=(-.9, 0, -.4), elbow=(0, .4, -.35),
          foot_l=(-.37, -.05, 50, 0, 0), foot_r=(.22, .12, -10, 12, 0))
    c.key(22, hips=(-.03, .03, .64 - low * .5), hip_turn=(28, 13, 3), spine=(14, 5, 0), chest=(22, 3, 3), neck=(-16, -4, 0), head=(-16, -10, 0),
          grip=(-.36, -.02, .82 - low), shaft=(-.3, -.85, -.4), face=(-.95, .15, .1), foot_r=(.22, .14, -10, 0, 0))
    c.key(27, hips=(0, .06, .66 - low * .5), hip_turn=(6, 12, 2), spine=(4, 5, 0), chest=(8, 3, 1), neck=(-5, -4, 0), head=(-5, -12, 0),
          grip=(-.34 - reach * .5, .18, cu - .06), shaft=(-.85, -.45, -.25), face=(-.35, .92, .15))
    c.key(30, hips=(.02, .08, .63 - low * .5), hip_turn=(-10, 14, 0), spine=(-6, 7, 0), chest=(-10, 5, 0), neck=(6, -4, 0), head=(10, -16, 0),
          grip=(-.3 - reach, .32, cu), shaft=(-.94, .15, -.12), face=(0, 1, .08), elbow=(0, .35, -.35),
          foot_l=(-.34, -.04, 35, 22, 0))
    c.key(34, hips=(.03, .08, .7), hip_turn=(-28, 9, -1), spine=(-12, 5, 0), chest=(-18, 3, -1), neck=(10, -4, 0), head=(14, -10, 0),
          grip=(-.06, .46, 1.14), shaft=(.2, .72, .66), face=(.8, .3, .4))
    c.key(40, hips=(.04, .07, .7), hip_turn=(-42, 7, -2), spine=(-15, 4, 0), chest=(-22, 3, -2), neck=(15, -4, 0), head=(22, -6, 0),
          grip=(.3, .22, 1.26), shaft=(.25, -.72, .64), face=(.7, .1, -.7), elbow=(.3, .1, -.1),
          foot_l=(-.26, .0, 10, 45, 0), foot_r=(.22, .14, -10, 0, 0))
    c.key(46, grip=(.28, .24, 1.22), shaft=(.3, -.68, .66))
    c.key(54, hips=(0, .03, .685), hip_turn=(-8, 11, 0), spine=(-3, 6, 0), chest=(-4, 4, 0), neck=(3, -5, 0), head=(4, -9, 0),
          grip=(.1, .32, .95), shaft=(-.3, .8, .5), face=(.7, .4, 0), elbow=(.3, -.2, -.35),
          foot_r=(.3, .02, -10, 0, 0), foot_l=(-.3, .02, 10, 0, 0))
    c.key(60, **READY)
    return c


def slice_clip():
    """Forehand slice: high takeback, open face, cutting down and through."""
    c = Clip("Slice")
    c.key(0, **READY)
    c.key(12, hips=(.04, 0, .67), hip_turn=(-40, 12, 0), spine=(-15, 5, 0), chest=(-28, 3, -3), neck=(22, -4, 0), head=(24, -8, 0),
          grip=(.34, -.08, 1.12), shaft=(.35, -.35, .87), face=(.6, .2, .75), off=(-.05, .42, 1.05), foot_r=(.36, -.03, -45, 0, 0))
    c.key(22, grip=(.4, .0, 1.02), shaft=(.55, -.6, .55), face=(.4, .5, .75), hip_turn=(-25, 12, -2), chest=(-15, 3, -2))
    c.key(30, hips=(-.02, .08, .66), hip_turn=(5, 12, 0), spine=(3, 5, 0), chest=(4, 3, 0), neck=(-5, -4, 0), head=(-8, -14, 0),
          grip=(.38, .3, .88), shaft=(.92, .15, .2), face=(0, .85, .5), off=(-.3, .22, .95), foot_r=(.34, -.04, -35, 15, 0))
    c.key(38, grip=(.1, .55, .85), shaft=(-.15, .9, .1), face=(-.5, .4, .75), hip_turn=(22, 10, 0), chest=(12, 3, 0))
    c.key(44, grip=(-.08, .5, .9), shaft=(-.5, .75, .2), face=(-.6, .1, .75), off=(-.35, .2, .98))
    c.key(54, hips=(0, .03, .685), hip_turn=(6, 11, 0), chest=(3, 4, 0), grip=(.08, .33, .92), shaft=(-.4, .8, .45), face=(.7, .4, 0),
          off="racket", foot_r=(.3, .02, -10, 0, 0))
    c.key(60, **READY)
    return c


def volley_clip(backhand=False):
    """A volley is a block, not a swing: racket head up, a short punch out in front with a
    firm wrist, knees bent low behind it and a step toward the ball."""
    s = -1 if backhand else 1
    c = Clip("VolleyBackhand" if backhand else "VolleyForehand")
    c.key(0, **READY)
    c.key(12, hips=(.03 * s, .03, .64), hip_turn=(-35 * s, 14, 0), spine=(-10 * s, 6, 0), chest=(-15 * s, 4, 0),
          neck=(14 * s, -4, 0), head=(16 * s, -10, 0),
          grip=(.32 * s, .22, .96), shaft=(.6 * s, .2, .77), face=(.75 * s, .6, .1), off="racket" if backhand else (-.1, .42, 1.02),
          foot_r=(.32, .0, -30 if s > 0 else -10, 0, 0), foot_l=(-.32, .0, 10 if s > 0 else 30, 0, 0))
    c.key(24, grip=(.34 * s, .28, .96), shaft=(.65 * s, .1, .75), face=(.55 * s, .83, .1))
    c.key(30, hips=(.0, .12, .62), hip_turn=(-12 * s, 14, 0), spine=(-4 * s, 6, 0), chest=(-6 * s, 4, 0), neck=(4 * s, -4, 0), head=(4 * s, -12, 0),
          grip=(.34 * s, .46, .98), shaft=(.7 * s, .35, .62), face=(.1 * s, 1, .15), off=(-.25 * s, .3, 1.02) if backhand else (-.2, .3, 1.0),
          foot_l=(-.2, .3, 10, 0, 0) if s > 0 else (-.32, .0, 30, 0, 0), foot_r=(.32, .0, -30, 20, 0) if s > 0 else (.2, .3, -10, 0, 0))
    c.key(36, grip=(.32 * s, .48, .98), shaft=(.68 * s, .4, .62), face=(.05 * s, 1, .2))
    c.key(48, hips=(0, .05, .67), hip_turn=(0, 12, 0), spine=(0, 6, 0), chest=(0, 4, 0), neck=(0, -6, 0), head=(0, -10, 0),
          grip=(.14, .34, .92), shaft=(-.4, .8, .45), face=(.7, .4, 0), off="racket", foot_l=(-.3, .05, 10, 0, 0), foot_r=(.3, .02, -10, 0, 0))
    c.key(60, **READY)
    return c


def serve_clip(name="Serve", smash=False):
    """The jump serve. Side-on stance, both arms drop and rise together, the trophy position
    (tossing arm straight up, racket up behind the head, knees loaded, back arched), the racket
    drops behind the back as the legs drive, the body leaves the court and uncoils, contact at
    full stretch above and in front, landing inside on the front foot, the racket finishing
    past the left hip. Contact near t = .62, where the toss is struck.

    The smash is the same overhead from a run-up turn, a scissor kick in the air."""
    c = Clip(name)
    start = dict(hips=(0, .0, .72), hip_turn=(-70, 4, 0), spine=(-10, 3, 0), chest=(-10, 2, 0), neck=(40, -2, 0), head=(38, -6, 0),
                 grip=(.1, .38, .98), shaft=(-.2, .85, .5), face=(.95, .1, 0), off=(-.02, .5, 1.02), elbow=(.3, -.2, -.35),
                 foot_l=(-.12, .22, 60, 0, 0), foot_r=(.3, -.2, -20, 0, 0))
    if smash:
        c.key(0, **READY)
        c.key(10, hips=(.03, -.05, .68), hip_turn=(-55, 6, 0), spine=(-10, 2, 0), chest=(-14, 0, -4), neck=(30, -12, 0), head=(30, -18, 0),
              grip=(.3, .05, 1.35), shaft=(.1, -.3, .95), face=(.9, .2, -.2), off=(-.1, .45, 1.5), elbow=(.35, .1, .05),
              foot_l=(-.16, .18, 50, 0, 0), foot_r=(.32, -.22, -30, 0, 0))
    else:
        c.key(0, **start)
        # Both arms drop together, weight rocks back.
        c.key(8, hips=(.04, -.04, .71), grip=(.22, .15, .8), shaft=(.4, .6, -.2), face=(.6, .5, .5), off=(-.05, .4, .82),
              foot_l=(-.12, .22, 60, 25, 0))
        # Arms rise: the toss arm up the line to the court, the racket back and up.
        c.key(16, hips=(.03, -.04, .7), hip_turn=(-75, 2, 0), chest=(-12, -2, -6), neck=(38, -10, 0), head=(36, -16, 0),
              grip=(.36, -.1, 1.02), shaft=(.2, -.4, -.9), face=(.8, .5, .2), off=(-.05, .38, 1.6), elbow=(.4, -.1, -.2),
              foot_l=(-.12, .22, 60, 0, 0))
    # Trophy: toss arm straight up, racket arm cocked, knees loaded, back arched.
    c.key(24 if not smash else 18, hips=(.06, -.02, .58), hip_turn=(-72, 0, -6), spine=(-8, -4, -3), chest=(-10, -5, -6),
          neck=(38, -10, 0), head=(34, -14, 0),
          grip=(.36, -.18, 1.32), shaft=(.1, -.3, .95), face=(.9, .2, -.2), off=(-.02, .32, 1.72), elbow=(.45, -.05, .05),
          foot_l=(-.08, .18, 60, 0, 0), foot_r=(.14, -.06, -20, 25, 0))
    # Legs drive, racket drops behind the back.
    c.key(30 if not smash else 26, hips=(.04, .04, .74), hip_turn=(-55, -4, -6), spine=(-6, -5, -2), chest=(-8, -6, -4),
          neck=(30, -12, 0), head=(26, -16, 0),
          grip=(.32, -.22, 1.35), shaft=(.05, .05, -1), face=(.95, .2, 0), off=(-.1, .4, 1.5), elbow=(.35, -.15, .15),
          foot_l=(-.08, .2, 60, 30, .03), foot_r=(.12, -.02, -20, 45, .04))
    # Airborne and uncoiling: the racket comes up edge-first behind the head.
    c.key(34 if not smash else 31, hips=(.02, .1, .92), hip_turn=(-30, 0, -4), spine=(-4, -2, 0), chest=(-4, -3, -2),
          neck=(18, -10, 0), head=(14, -14, 0),
          grip=(.26, .0, 1.68), shaft=(.35, -.3, .88), face=(.9, .3, .1), off=(-.25, .35, 1.25), elbow=(.4, .1, .3),
          foot_l=(-.12, .3, 40, 35, .14), foot_r=(.18, .0, -10, 50, .22 if not smash else .1))
    # Contact at full stretch, above and in front.
    c.key(37 if not smash else 34, hips=(0, .16, .98), hip_turn=(-8, 6, 0), spine=(-2, 2, 0), chest=(0, 2, 0),
          neck=(6, -12, 0), head=(4, -18, 0),
          grip=(.2, .22, 1.95), shaft=(.12, .3, .95), face=(0, .95, .3), off=(-.3, .3, 1.1), elbow=(.3, .1, .4),
          foot_l=(-.1, .36, 20, 35, .18), foot_r=(.16, .08, -10, 55, .26 if not smash else .08))
    # Pronation and down: the racket turns over and sweeps across.
    c.key(42 if not smash else 39, hips=(-.02, .28, .8), hip_turn=(25, 20, 2), spine=(8, 10, 0), chest=(10, 8, 2),
          neck=(-6, -6, 0), head=(-8, -10, 0),
          grip=(.02, .5, 1.25), shaft=(-.4, .7, -.55), face=(-.6, .2, -.75), off=(-.3, .2, 1.0), elbow=(.25, .2, .0),
          foot_l=(-.1, .44, 10, 0, .02), foot_r=(.14, .02, -5, 40, .18 if not smash else .02))
    # Landing inside on the front foot, the back leg kicking out behind for balance.
    c.key(46, hips=(-.04, .42, .64), hip_turn=(30, 22, 4), spine=(8, 10, 0), chest=(10, 8, 2), neck=(-10, -6, 0), head=(-12, -10, 0),
          grip=(-.28, .36, .78), shaft=(-.6, .1, -.8), face=(-.5, -.3, .8), off=(-.25, .2, .92), elbow=(.15, .3, -.2),
          foot_l=(-.1, .5, 10, 0, 0), foot_r=(.2, .02, -5, 55, .22 if not smash else .05))
    c.key(53, hips=(-.02, .4, .68), hip_turn=(10, 14, 0), spine=(3, 7, 0), chest=(3, 5, 0), neck=(-3, -6, 0), head=(-4, -10, 0),
          grip=(.02, .64, .9), shaft=(-.4, .8, .45), face=(.7, .4, 0), off="racket", elbow=(.3, -.2, -.35),
          foot_l=(-.28, .48, 10, 0, 0), foot_r=(.28, .38, -10, 0, 0))
    c.key(60, hips=(0, .4, .69), grip=(.12, .72, .88), foot_l=(-.3, .42, 10, 0, 0), foot_r=(.3, .42, -10, 0, 0), **TRUNK_READY)
    return c


def split_step_clip():
    """A real split step: a small hop as the opponent strikes, landing wide and low, ready to
    push off either way."""
    c = Clip("SplitStep", loop=False)
    c.key(0, **READY)
    c.key(8, hips=(0, .02, .68))
    c.key(15, hips=(0, .02, .73), foot_l=(-.26, .02, 10, 18, .01), foot_r=(.26, .02, -10, 18, .01))
    c.key(20, hips=(0, .02, .76), foot_l=(-.29, .02, 14, 8, .05), foot_r=(.29, .02, -14, 8, .05), grip=(.12, .33, .9))
    c.key(26, hips=(0, .02, .7), foot_l=(-.32, .02, 16, 0, 0), foot_r=(.32, .02, -16, 0, 0))
    c.key(33, hips=(0, .02, .65), hip_turn=(0, 15, 0), grip=(.12, .32, .85))
    c.key(42, hips=(0, .02, .67), hip_turn=(0, 12, 0), grip=(.12, .32, .88), foot_l=(-.31, .02, 12, 0, 0), foot_r=(.31, .02, -12, 0, 0))
    c.key(60, **READY)
    return c


def run_clip(name, direction):
    """Upper body for running (the runtime plants the feet procedurally): leaning into the
    run, the racket carried up in front, the free arm pumping, the shoulders counter-rotating
    with the stride. `direction` +1 runs to the right."""
    d = direction
    c = Clip(name, loop=True)
    base = dict(hips=(0, .04, .68), hip_turn=(-18 * d, 14, 4 * d), spine=(6 * d, 6, 0), chest=(8 * d, 5, 0), neck=(6 * d, -8, 0), head=(8 * d, -10, 0),
                grip=(.14, .3, .96), shaft=(-.35, .75, .56), face=(.7, .4, 0), elbow=(.3, -.2, -.35))
    for f, s in ((0, 1), (15, 0), (30, -1), (45, 0)):
        k = dict(base)
        k["hips"] = (0, .04, .68 - .02 * abs(s) + .02 * (1 - abs(s)))
        k["chest"] = (8 * d + 10 * s, 5, 0)
        k["off"] = (-.26, .1 + .22 * s, .95 + .06 * s)
        k["grip"] = (.14, .3 - .06 * s, .96 - .03 * s)
        k["foot_l"] = (-.24 + .12 * s * d, .02 + .1 * s, 10, 20 if s < 0 else 0, .08 if s < 0 else 0)
        k["foot_r"] = (.24 + .12 * s * d, .02 - .1 * s, -10, 20 if s > 0 else 0, .08 if s > 0 else 0)
        c.key(f, **k)
    return c


CLIPS = {
    "Ready": ready_clip,
    "SplitStep": split_step_clip,
    "RunLeft": lambda: run_clip("RunLeft", -1),
    "RunRight": lambda: run_clip("RunRight", 1),
    "Forehand": forehand_clip,
    "ForehandTopspin": lambda: forehand_clip("ForehandTopspin", topspin=1.35),
    "MissedSwing": lambda: forehand_clip("MissedSwing", reach=.1),
    "RunningForehand": lambda: forehand_clip("RunningForehand", reach=.12),
    "LowPickup": lambda: forehand_clip("LowPickup", low=.3, topspin=1.4),
    "Lob": lambda: forehand_clip("Lob", topspin=1.3, open_face=.7),
    "Backhand": backhand_clip,
    "RunningBackhand": lambda: backhand_clip("RunningBackhand", reach=.12),
    "Slice": slice_clip,
    "VolleyForehand": volley_clip,
    "VolleyBackhand": lambda: volley_clip(True),
    "Serve": serve_clip,
    "Smash": lambda: serve_clip("Smash", smash=True),
}


# ---------------------------------------------------------------- driving it

def v4_track(rig):
    return next(t for t in rig.animation_data.nla_tracks if t.name.startswith("V4 |"))


def grip_offset(rig, racket, bone, frame):
    bpy.context.scene.frame_set(frame)
    hand = rig.pose.bones[bone].matrix                   # armature space
    racket_arm = rig.matrix_world.inverted() @ racket.matrix_world
    return hand.inverted() @ racket_arm


def author(gender, names, report):
    scene = bpy.context.scene
    rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
    prop = next(c for c in scene.collection.children if c.name.startswith(f"V4 PROP {gender} V4 racket"))
    racket = next(o for o in prop.objects if o.parent is None)
    support = next((o for o in prop.all_objects if o.name.endswith("SupportHand")), None)
    track = v4_track(rig)
    strips = {s.name: s for s in track.strips}
    scale = rig.data.bones["UpperArm.R"].head_local.z / 1.20
    # Where the racket sits in each hand, measured once in the original Ready stance.
    offsets = {"RH": grip_offset(rig, racket, "Hand.R", int(strips["Ready RH"].frame_start) + 1),
               "LH": grip_offset(rig, racket, "Hand.L", int(strips["Ready LH"].frame_start) + 1)}
    for hand in ("RH", "LH"):
        a = Author(rig, racket, support, hand, offsets[hand], scale)
        a.rig_up()
        report.append(f"{gender} {hand} poles: {', '.join(a.pole_report)}")
        for name in names:
            clip = CLIPS[name]()
            strip = strips.get(f"{name} {hand}")
            if not strip: report.append(f"{gender} {hand}: no strip for {name}"); continue
            start = int(strip.frame_start)
            action = a.bake(clip, f"{gender}__Tennis__{name}__{hand}__V5", start)
            strip.action = action
            strip.action_frame_start = 1; strip.action_frame_end = clip.length + 1
            strip.frame_start = start; strip.frame_end = start + clip.length
            report.append(f"{gender} {hand} {name}: {start}-{start + clip.length}")
        a.unrig()


def render(directory, names, gender="Male", per=8, views=("three", "front")):
    """Contact-sheet frames of the authored clips (RH), like render_clip_sheets.py."""
    scene = bpy.context.scene
    out = Path(directory); out.mkdir(parents=True, exist_ok=True)
    rig = bpy.data.objects[f"{gender}_Tennis_Rig"]
    body = bpy.data.collections[f"V4 {gender} Tennis | BODY"]
    kit = bpy.data.collections[f"V4 {gender} Tennis | KIT 1"]
    prop = next(c for c in scene.collection.children if c.name.startswith(f"V4 PROP {gender} V4 racket"))
    keep = {o for o in body.objects if "face decal" not in o.name} | set(kit.objects) | set(prop.all_objects)
    for o in scene.objects:
        if o.type in {"MESH", "CURVE"}: o.hide_render = o not in keep
    cam_data = bpy.data.cameras.get("Review") or bpy.data.cameras.new("Review"); cam_data.lens = 70
    cam = bpy.data.objects.get("Review camera") or bpy.data.objects.new("Review camera", cam_data)
    if cam.name not in scene.collection.objects: scene.collection.objects.link(cam)
    scene.camera = cam
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"; scene.display.shading.color_type = "TEXTURE"; scene.display.shading.show_shadows = True
    scene.render.resolution_x = 360; scene.render.resolution_y = 440
    strips = {s.name: s for s in v4_track(rig).strips}
    for name in names:
        s = strips[f"{name} RH"]
        first, last = int(s.frame_start), int(s.frame_end)
        for i in range(per):
            f = round(first + (last - first) * i / (per - 1))
            scene.frame_set(f)
            hips = rig.matrix_world @ rig.pose.bones["Hips"].head
            target = Vector((hips.x, hips.y, 1.0))
            for view in views:
                named = {"three": Vector((3.4, -4.6, .9)), "front": Vector((0, -5.8, .5)), "side": Vector((5.8, 0, .5)),
                         "back": Vector((.6, 4.4, 2.5)), "video": Vector((0, 5.8, -.2)), "close": Vector((1.3, -1.9, .2)), "closeback": Vector((-.5, 2.2, .3))}
                # Any other view is an explicit camera offset "x:y:z" (e.g. a rotomation source camera).
                offset = named[view] if view in named else Vector(float(x) for x in view.split(":"))
                cam.location = target + offset
                cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
                scene.render.filepath = str(out / f"{name}-{view}-{i:02d}.png")
                bpy.ops.render.render(write_still=True)


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = argv[argv.index("--clips") + 1].split(",") if "--clips" in argv else list(CLIPS)
    rigs = argv[argv.index("--rigs") + 1].split(",") if "--rigs" in argv else ["Male", "Female"]
    scene = bpy.data.scenes["03 TENNIS"]; bpy.context.window.scene = scene
    report = []
    for gender in rigs: author(gender, names, report)
    print("\n".join("AUTHOR " + r for r in report), flush=True)
    if "--avatar" in argv:
        import fit_avatar
        fit_avatar.fit.HEIGHT = fit_avatar.RIG_SHOULDER / fit_avatar.LANDMARKS["shoulder"]
        fit_avatar.fit.build_face = fit_avatar.avatar_face; fit_avatar.fit.remove_fragments = fit_avatar.small_fragments; fit_avatar.fit.remove_fists = fit_avatar.cut_hands
        rep = []
        rig, body = fit_avatar.fit.fit_character("Male", rep, fit_avatar.GLB, fit_avatar.NAME, fit_avatar.LANDMARKS)
        fit_avatar.scale_head(body, rig, float(argv[argv.index("--avatar") + 1]))
    views = tuple(argv[argv.index("--views") + 1].split(",")) if "--views" in argv else ("three", "front")
    if "--render" in argv: render(argv[argv.index("--render") + 1], names, views=views)
    if "--save" in argv:
        bpy.ops.wm.save_mainfile(); print("AUTHOR saved", flush=True)


if __name__ == "__main__":
    main()

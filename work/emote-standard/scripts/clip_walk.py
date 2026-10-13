"""Standard walk cycle (seamless loop, in place): see gait.py."""
import gait

NAME = gait.WALK['name']


def build(H):
    return gait.build(H, gait.WALK, getattr(H, 'prop', 'racket') == 'none')

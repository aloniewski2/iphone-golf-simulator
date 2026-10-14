"""Standard run cycle (seamless loop, in place): see gait.py."""
import gait

NAME = gait.RUN['name']


def build(H):
    return gait.build(H, gait.RUN, getattr(H, 'prop', 'racket') == 'none')

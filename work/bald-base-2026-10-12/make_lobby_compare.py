#!/usr/bin/env python3
"""Lobby before / lobby now / game, same camera, per hero (bald-base pass 2, 2026-10-12).

LOBBY BEFORE = lobby_before/<sport>/ : the committed (HEAD) CharacterAssets + MatchHero*.swift drawn by work/hero-sync/lobby_render.swift
LOBBY NOW    = work/hero-sync/out/lobby/<sport>/ and GAME = work/hero-sync/out/unity/<sport>/ (written by work/hero-sync/sync_heroes.sh)
Writes proof/lobby_<sport>_<sex>.png and proof/lobby_all.png.
"""
import os
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.dirname(os.path.dirname(HERE))
SYNC = os.path.join(REPO, 'work/hero-sync/out')
OUT = os.path.join(HERE, 'proof')
BG, INK = (30, 30, 32), (235, 235, 235)
COLS = (('LOBBY BEFORE', (240, 128, 112), lambda s: os.path.join(HERE, 'lobby_before', s)),
        ('LOBBY NOW', (255, 214, 120), lambda s: os.path.join(SYNC, 'lobby', s)),
        ('GAME (Unity)', (130, 220, 130), lambda s: os.path.join(SYNC, 'unity', s)))


def font(size):
    for path in ('/System/Library/Fonts/Supplemental/Arial Bold.ttf', '/System/Library/Fonts/Helvetica.ttc'):
        try: return ImageFont.truetype(path, size)
        except OSError: pass
    return ImageFont.load_default()


def sheet(sport, sex):
    shots = [('head', (140, 230, 600, 830)), ('body', (130, 40, 590, 1000)), ('feet', (0, 300, 720, 1000))]
    tile = 360; top = 70; label = 40
    W = len(COLS) * (tile + 8); H = top + len(shots) * (tile + label)
    s = Image.new('RGB', (W, H), BG); d = ImageDraw.Draw(s)
    d.text((10, 14), f'{sport} {sex}: lobby before / lobby now / game, same camera and lights', fill=INK, font=font(24))
    for c, (name, colour, root) in enumerate(COLS): d.text((c * (tile + 8) + 8, top - 30), name, fill=colour, font=font(22))
    for r, (shot, box) in enumerate(shots):
        for c, (_, _, root) in enumerate(COLS):
            p = os.path.join(root(sport), f'{sex}_default_{shot}.png')
            if not os.path.exists(p): continue
            im = Image.open(p).convert('RGB').crop(box); im.thumbnail((tile, tile), Image.LANCZOS)
            s.paste(im, (c * (tile + 8) + (tile - im.width) // 2, top + r * (tile + label)))
    return s


if __name__ == '__main__':
    rows = []
    for sport in ('tennis', 'golf'):
        for sex in ('male', 'female'):
            im = sheet(sport, sex); im.save(os.path.join(OUT, f'lobby_{sport}_{sex}.png')); rows.append(im)
    W = sum(r.width for r in rows[:2]) + 10; H = 2 * rows[0].height + 10
    all_ = Image.new('RGB', (W, H), BG)
    for i, r in enumerate(rows): all_.paste(r, ((i % 2) * (r.width + 10), (i // 2) * (r.height + 10)))
    all_.save(os.path.join(OUT, 'lobby_all.png')); print('wrote', OUT)

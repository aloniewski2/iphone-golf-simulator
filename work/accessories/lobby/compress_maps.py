"""Lobby (SceneKit) copies of the ACCESSORY maps: the Swift garment shader samples .ktx (MatchHeroSurfaces.compressed), not the PNG.
Same settings as work/clothing-v2/lobby/compress_maps.py (ASTC 8x8, linear, flipped, clamp). Files: Unity/Assets/Resources/Accessories/<Sex>/<item>/Acc_*.png
-> GolfArcade/Unity/CharacterAssets/MatchHero_Acc_*.ktx (the exporter names a map MatchHero_<png name>).
python3 work/accessories/lobby/compress_maps.py"""
from pathlib import Path
import subprocess, sys
R = Path(__file__).resolve().parents[3]
A = R / 'GolfArcade/Unity/CharacterAssets'
U = R / 'Unity/Assets/Resources/Accessories'
conv = '/Applications/Xcode.app/Contents/Developer/usr/bin/TextureConverter'
# the bag is never drawn in the lobby / menus (plaza + between shots only): its 4096 maps stay out of the app's lobby assets
for p in sorted(U.rglob('Acc_*.png')):
    if '_golf_bag_' in p.name: continue
    if '_GloveR_' in p.name: continue      # the lobby mirrors the left glove for a left-hander (the mirrored right glove is a game piece)
    out = A / ('MatchHero_' + p.stem + '.ktx')
    if out.exists() and out.stat().st_mtime >= p.stat().st_mtime:
        print('up to date', out.name); continue
    r = subprocess.run([conv, '--compression_format=ASTC8x8', '--compression_quality=Highest', '--alpha_mode=Preserve', '--channel_weighting=Linear',
                        '--gamma_in=1', '--gamma_out=1', '--flip_y', '--wrap_mode=Clamp', '--file_format=KTX', '--output=' + str(out), str(p)], capture_output=True, text=True)
    if r.returncode: sys.exit(r.stdout + r.stderr)
    print(out.name, out.stat().st_size)

"""Lobby (SceneKit) copies of the CLOTHING V2 maps: the Swift garment shader samples .ktx (MatchHeroSurfaces.compressed), not the PNG.
Same settings as work/cloth-overhaul/scripts/compress_locker.py (ASTC 8x8 maps, ASTC 4x4 atlas, linear, flipped, clamp).
python3 work/clothing-v2/lobby/compress_maps.py   (after sync_heroes.sh, or before it from the Unity resources)"""
from pathlib import Path
import subprocess, sys
R = Path(__file__).resolve().parents[3]
A = R / 'GolfArcade/Unity/CharacterAssets'
U = [R / 'Unity/Assets/Resources/Tennis/KitsV2', R / 'Unity/Assets/Resources/Golf/KitsV2']
conv = '/Applications/Xcode.app/Contents/Developer/usr/bin/TextureConverter'
src = {p.name.replace('.png', ''): p for d in U if d.exists() for p in d.rglob('KitV2_*.png')}
for name, p in sorted(src.items()):
    out = A / ('MatchHero_' + name + '.ktx')
    fmt = 'ASTC4x4' if 'Atlas' in name else 'ASTC8x8'
    r = subprocess.run([conv, '--compression_format=' + fmt, '--compression_quality=Highest', '--alpha_mode=Preserve', '--channel_weighting=Linear',
                        '--gamma_in=1', '--gamma_out=1', '--flip_y', '--wrap_mode=Clamp', '--file_format=KTX', '--output=' + str(out), str(p)],
                       capture_output=True, text=True)
    if r.returncode: sys.exit(r.stdout + r.stderr)
    print(out.name, fmt, out.stat().st_size)

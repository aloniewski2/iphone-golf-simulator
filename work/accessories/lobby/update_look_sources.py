"""Rewrites the ACCESSORIES block of work/hero-sync/look_sources.txt (the files whose change must be followed by a lobby re-sync). Idempotent.
python3 work/accessories/lobby/update_look_sources.py"""
from pathlib import Path
R = Path(__file__).resolve().parents[3]
LS = R / 'work/hero-sync/look_sources.txt'
BEGIN, END = '# ACCESSORIES (work/accessories): cap / glove / bag pieces, their meshes, maps and item data', '# /ACCESSORIES'
files = ['Unity/Assets/Scripts/Tennis/HeroAccessories.cs']
for p in sorted((R / 'Unity/Assets/Resources/Accessories').rglob('*')):
    if p.is_file() and p.suffix in ('.asset', '.png', '.json', '.bytes') and '/golf_bag/' not in str(p):     # the bag never reaches the lobby
        files.append(str(p.relative_to(R)))
block = '\n'.join([BEGIN] + files + [END]) + '\n'
txt = LS.read_text()
if BEGIN in txt:
    a = txt.index(BEGIN); b = txt.index(END) + len(END) + 1
    txt = txt[:a] + block + txt[b:]
else:
    txt = txt.rstrip('\n') + '\n' + block
LS.write_text(txt)
print(len(files), 'accessory files in look_sources.txt')

"""rewrite the CLOTHING V2 block of work/hero-sync/look_sources.txt from what is installed in Unity/ (run after install_to_repo.sh)"""
from pathlib import Path
R = Path(__file__).resolve().parents[2]
f = R / 'work/hero-sync/look_sources.txt'
lines = f.read_text().splitlines()
a = next(i for i, l in enumerate(lines) if l.startswith('# CLOTHING V2'))
b = next((i for i in range(a + 1, len(lines)) if lines[i].startswith('# ') and not lines[i].startswith('# CLOTHING V2')), len(lines))
U = R / 'Unity'
files = ['Unity/Assets/Scripts/Tennis/KitV2PoseCorrectives.cs', 'Unity/Assets/Scripts/Tennis/HeroGarmentTailoring.cs']
files += sorted(str(p.relative_to(R)) for p in (U / 'Assets/Characters/KitsV2').rglob('*.asset'))
files += ['Unity/Assets/Resources/Tennis/KitsV2/KitV2_FabricAtlas.png']
for sport in ('Tennis', 'Golf'):
    d = U / 'Assets/Resources' / sport / 'KitsV2'
    if d.exists():
        files += sorted(str(p.relative_to(R)) for p in d.rglob('*') if p.suffix in ('.png', '.bytes', '.json') and p.name != 'KitV2_FabricAtlas.png')
block = [lines[a]] + files
f.write_text('\n'.join(lines[:a] + block + lines[b:]) + '\n')
print(len(files), 'files in the CLOTHING V2 block')

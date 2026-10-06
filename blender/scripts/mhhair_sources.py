"""Which MakeHuman hairs the golfer uses (no bpy: shared by fetch_mhhair.py and matchhero_mhhair.py).

game name -> (folder in MakeHuman's system assets, texture file, gap), gap = how far the hair's inner surface stands off the scalp (metres).
All are CC0 (MakeHuman system assets, makehumancommunity.org). The names are what the cut looks like on his heads."""
HAIRS = {
    "Crop":     ("short04", "short04_diffuse.png", 0.006),     # a slicked, side-parted crop
    "Fringe":   ("short03", "short03_diffuse.png", 0.007),     # a long side fringe over one eye
    "Mop":      ("short02", "short02_diffuse.png", 0.007),     # a layered mop with a fringe
    "Quiff":    ("short01", "short01_diffuse.png", 0.007),     # a textured quiff on faded sides
    "SideBob":  ("bob01", "bob01_diffuse.png", 0.009),         # a side-swept bob
    "Bob":      ("bob02", "bob02_diffuse.png", 0.009),         # a blunt bob
    "Long":     ("long01", "long01_diffuse.png", 0.009),       # long and straight, parted in the middle
    "Ponytail": ("ponytail01", "ponytail01_diffuse.png", 0.008),
    "Braid":    ("braid01", "braid01_diffuse.png", 0.008),     # a side braid
}

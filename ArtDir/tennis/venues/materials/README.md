# Tennis physical surface details

AcrylicAggregate is a deterministic 512² periodic aggregate texture. RGB encodes an unpacked micro normal and alpha holds pigment-height variation; the active court/runtime tile spans half a metre. Import as linear Default, uncompressed RGBA, repeat, mipmaps, anisotropic8. It is not a standard packed normal map because alpha is retained.

Regenerate with `python3 ArtDir/tennis/venues/materials/build-acrylic-aggregate.py` (NumPy, Pillow). Use `--out` to stage outside Assets. The shader bounds court pigment to 2.8%, uses .07 court/.06 runoff normal strength, and mipmaps integrate distant aggregate. The court's plane, height, lines and collision are unchanged.

World19 candidate adds actual imported normal-map triplanar relief to basalt, normalizes the already-charcoal albedo before modulation, preserves cloud highlights while calibrating coastal clear blue, uses only the upper sky part of SkyRooftop above the existing horizontal cloud plane, and reflects that sky on a facade alpha glazing mask. World19 matched captures show better rooftop dusk and crater relief but exposed broad court ripples. World20 removes that legacy procedural normal term and retains only bounded micro aggregate; actual World20 comparison remains required.

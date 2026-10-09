# Reference front/back consistency

The unchanged replacement JPEGs have different front/back body proportions in their currently traced outlines. This is an input/framing conflict for the existing fixed orthographic silhouette gate. It does not excuse remaining face or surface differences.

| Base | Front vs mirrored back | Minimum possible worst of the two errors |
|---|---:|---:|
| Male | 20.79% | 10.39% |
| Female | 14.34% | 7.17% |

Under this framing, reversing the view preserves the same projected geometry. The Jaccard triangle inequality requires at least one error to reach half the distance between the reference masks. Both source pairs exceed the 10% distance compatible with two ≤5% errors.

The manual source trace has estimated ±2-pixel boundary precision. The model and source comparison images use the frozen metre/pixel scale and source centres. The diagnostic display uses uniform thumbnail scaling only. Targets, proof cameras and the ~5% gate remain unchanged.

Green is shared source area, red is front only, blue is mirrored back only. See reference-consistency.png and the machine-readable JSON.

Front-view likeness is being refined while the reference-priority question is pending. No overall BASE PASS is claimed.

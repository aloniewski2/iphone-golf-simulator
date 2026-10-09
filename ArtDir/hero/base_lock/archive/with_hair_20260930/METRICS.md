# Hero Base current measured candidate

Plate hashes and landmarks are frozen in plate-measurements.json.

| Base | Body triangles | Default hair triangles | Total triangles | With hair height m | Body-only height m |
|---|---:|---:|---:|---:|---:|
| Male | 35,416 | 13,952 | 49,368 | 1.699999 | 1.650911 |
| Female | 36,336 | 13,216 | 49,552 | 1.697157 | 1.682942 |

Source, independently re-imported FBXs and Unity triangle counts agree.

Current silhouette errors: Male front 8.10%, back 15.08%; Female front 8.78%, back 11.69%. These fail the ~5% gate.

Evidence: proof/saved-source-audit.json, proof/unity-mesh-material-audit.txt, proof/unity-import.txt, proof/silhouette-metrics.json, proof/GATE_RESULTS.md.

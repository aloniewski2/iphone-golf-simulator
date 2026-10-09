# Final golf art sources

Production world source is checkpoint18; GalleryHero4 is installed at checkpoint22. Actual Unity cheer pictures show solid chest/collar geometry and coherent human faces. Parent accepted all-seven near/far/cheer audience views, including the small close armpit crease. Final all19 synchronous dressing physics and completed-frame editor draw gates pass. Phone performance and final main-hero art remain separate. Exact concept-image parity remains partial. Original source courses, colliders and playable surfaces are preserved.

`source/` contains the editable botanical kit, Meadow clubhouse, six decorative landmark finishing files and `GolfGalleryHero4.blend`. `source/gallery3-inputs/` preserves the exact approved human Body/Face and fitted Golf_Master garment inputs shared by the crowd revisions. Input hashes, measured anchors and near/far geometry budgets are in `gallery4-build-manifest.json`. The rig is retained in those inputs for coherent posing; the audience output contains four rigid parts per LOD, with no bones, skinning or colliders.

From the repository root:

```sh
blender --background --threads 4 --python ArtDir/golf/tools/export-golf-art.py
blender --background --threads 4 --python ArtDir/golf/tools/bake-gallery-hero4.py
blender --background --threads 4 --python ArtDir/golf/tools/render-gallery-hero4.py
```

Every command writes candidates to `work/golf-art-exports` and leaves Unity Assets unchanged. To export one asset, append `-- --asset BotanicalKit`. The exporter consumes the editable final sources; the crowd baker rebuilds from the fixed canonical inputs. The renderer uses CPU-only near/far face, combined head-nod/yaw plus raised-arm cheer, and fist-pump proofs. Actual Unity frames, LOD behavior and completed-frame costs must still be checked after a coordinated import. FBX timestamps can change byte hashes; geometry counts and bounds are the reproducibility checks.

GalleryHero4 uses 39,814/44,042 source triangles nearby and 7,363/7,930 distant. Imported Unity geometry measures 39,318/43,672 nearby and 7,221/7,870 distant after degenerate exporter polygons are discarded. The .25 screen-relative threshold switches to distant geometry around 6–8 yards at ordinary gameplay FOV. Original painted Face/eye surfaces are preserved. The lower neck junction follows BODY while the complete head and upper neck follow HEAD; this prevents a cheering nod from pulling chest skin through the placket. Shoulder closures follow each actual shirt armhole. Detailed near collars stay intact; distant shirts use sealed fitted envelopes within their source point bounds. Lower garments, socks and sneakers are capped exterior volumes. The fitted female bob is NPC-specific; approved main heroes are bald.

The botanical source retains near/far crowns, shader wind colors and pivot UVs. Runtime materials and batching are in `Unity/Assets/Scripts/Course` and `Resources/Course/Shaders`. Decorative landmark sources retain original course content for context; the exporter selects only known finishing mesh prefixes and excludes windmill sails. Original terrain/collider sources remain in `blender/` and existing source course assets. GalleryHero3 files are retained as a superseded debugging checkpoint; its combined head/arm cheer exposed chest skin and failed actual acceptance.

# HeroFromPlate turnarounds

Source of identity: ../../plates/01_hero.png; full plate contact sheet inspected.
Tool: built-in imagegen, one reference-guided generation. No rejected mannequin was supplied.
Original generated sheet: turnaround-sheet.png. front.png / side.png / back.png / 34.png are separated panels on a white 1024-square canvas; no anatomy painting or reshaping was performed while extracting panels.

Generation brief: same plate hero in four full-body orthographic views, front / left-facing side / back / three-quarter; consistent relaxed A-pose, no racket, stocky toy-athlete with thick limbs, 24%-height head, identical round warm face, brown eyes/sclera/highlights, swept blonde hair, white/navy curved visor, white polo/navy collar/orange piping, navy shorts, chunky white shoes with orange accents. Soft even light, simple white background, no text, no scene or UI. Plate supplied as strict image reference.

Selected all four from the single sheet for identity consistency. Side view shows rounded fingers that overlap from the front. Back design is inferred by the image model, because plate 01 provides no back view.

Tripo uses front.png, side.png (left slot by positional mapping), back.png. Three-quarter is review-only: it is not mislabeled as an orthographic right view.
Tripo task: 2286dbbb-2ddf-4f3d-8fff-03c30680a07d; model v3.1; requested face_limit 45000, detailed texture, original_image texture alignment.

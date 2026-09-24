# Cliffside — three more holes (mocks)

Higgsfield `gpt_image_2_5` renders, made with `Resources/Course/hole_07_card.jpg` and
`hole_12_card.jpg` as style references so they share the course's parts: faceted grey cliffs
and waterline boulders, low-poly pines, striped fairways, cream bunkers, boardwalks, the
lighthouse. Hole ideas come from the holes in *Rising Impact* (Nakaba Suzuki; Netflix, 2024):
"Spiral", "Witch's Lair" and the step green.

| File | Hole | Par | Idea | What it tests in our game |
|---|---|---|---|---|
| `spiral.png` | The Spiral | 5 | Fairway coils up a rock pinnacle in terraces; green on the summit, boardwalk stair to it | Shot shaping: a straight ball runs off the curling fairway, so it has to be drawn round |
| `witchs_lair.png` | The Witch's Lair | 4 | Green sunk in a crater, dark pines and a ruined tower on the rim, one gap in the front | Chipping down into a bowl; the new cup physics and chip-in hole cam |
| `the_steps.png` | The Steps | 3 | Tee islet, boardwalk over the water, a three-tier terraced green | Putting across tiers on the height grid; landing on the right step |

Next step when one is picked: model it in Blender from these parts (see `hole12_prepare.py` and
`hole07_design.py`), export `hole_NN.fbx`, and add its numbers to `Course.Cliffside()`.

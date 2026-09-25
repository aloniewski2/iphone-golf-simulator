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

# Five themed holes (19–23)

Higgsfield `gpt_image_2_5` concepts, each a different world so no two play or look alike. Built
by `course_builder.py` from `hole19_volcano_design.py` … `hole23_windmill_design.py`, with the
themes, pools/rivers/falls of water, lava or ice, plants and landmarks in `course_extras.py`.

| File | Hole | Par | Idea | What it asks of the player |
|---|---|---|---|---|
| `volcano_rim.png` | Volcano Rim | 4 | Black basalt island under a smoking volcano; a lava river crosses the fairway under a stone arch; steam vents | Carry the lava off the tee, then follow the ridge to a green on a ledge under the crater |
| `frostbite_fjord.png` | Frostbite Fjord | 5 | Snow island, frozen lake, log cabin and snowman, an ice fall off the cliffs, floes in the sea | Play round the frozen lake along the pines, or cut the corner over the ice to get home in two |
| `mesa_canyon.png` | Mesa Canyon | 3 | Two banded red-rock mesas with a canyon of water between, a rope bridge, saguaros | One carry, mesa to mesa: short is in the river |
| `temple_falls.png` | Temple Falls | 4 | Jungle island on two levels; a waterfall into a lagoon across the fairway; stepped temple, ruins, stone head | Carry the lagoon and the step with the drive, or lay up and face a long second |
| `windmill_links.png` | Windmill Links | 4 | Flat tulip-field island; a turning red windmill, two canals with white bridges, a pond with a jetty and boat | Lay up short of a canal or carry it; the pond guards the green |

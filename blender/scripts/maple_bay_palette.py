"""Maple Bay's autumn look, shared by its holes' designs (course_builder.py recolours the
materials by name for the card render; the game uses the same colours from HoleView's "autumn"
theme). Golden rough, a warmer fairway, red sandstone cliffs, maples in crimson, orange and gold.
"""
PALETTE = {
    "MAT_ROUGH": (150, 142, 60), "MAT_FAIRWAY": (140, 196, 70), "MAT_FAIRWAY_STRIPE": (124, 182, 60),
    "MAT_FIRSTCUT": (118, 170, 56), "MAT_GREEN": (150, 218, 86), "MAT_BUNKER_LIP": (164, 192, 82),
    "MAT_SAND": (246, 232, 200), "MAT_CLIFF": (178, 108, 72), "MAT_CLIFF_DARK": (138, 80, 56),
    "MAT_TREE_DARK": (178, 58, 34), "MAT_TREE_MID": (222, 110, 40), "MAT_TREE_LIGHT": (242, 178, 60),
    "MAT_ROCK": (170, 140, 116), "MAT_ROCK_DARK": (128, 100, 84),
    "MAT_WATER": (22, 96, 150), "MAT_WATER_SHALLOW": (62, 160, 188),
}

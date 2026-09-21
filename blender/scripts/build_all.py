"""Rebuild the whole Hole 07 scene from scratch inside Blender (Text Editor > Run Script, or
`blender --python build_all.py`). Expects the .blend to live one folder up so bpy.path.abspath("//")
resolves; phases can also be run individually in this order."""
import os, bpy
HERE = os.path.dirname(os.path.abspath(__file__)) if "__file__" in globals() else os.path.join(bpy.path.abspath("//"), "scripts")
for name in ("phase1_setup.py", "phase2_5_course.py", "phase6_trees.py", "phase7_rocks.py",
             "phase8_path.py", "phase9_10_clubhouse_gameplay.py", "phase11_13_look.py"):
    ns = {"__file__": os.path.join(HERE, name)}
    exec(compile(open(os.path.join(HERE, name)).read(), name, "exec"), ns)
    print(name, "->", ns.get("result"))

# Golfers: run separately (each starts a fresh file). In Blender's Python console or via MCP:
#   VARIANT = "male";   exec(open(".../golfer_build.py").read()); exec(open(".../golfer_export.py").read())
#   VARIANT = "female"; exec(open(".../golfer_build.py").read()); exec(open(".../golfer_export.py").read())

import bpy, os
PREVIEW = globals().get("PREVIEW", "/Users/andrewloniewski/iPhoneGolfSimulator/blender/previews/golfer")
os.makedirs(PREVIEW, exist_ok=True)
sc = bpy.context.scene
sc.render.resolution_x, sc.render.resolution_y = 360, 480
sc.render.resolution_percentage = 100
sc.eevee.taa_render_samples = 8
frames = globals().get("FRAMES", [0, 30, 48, 64, 80, 105])
out = []
for cam_name in globals().get("CAMS", ("CAM_FACE_ON", "CAM_DOWN_LINE")):
    sc.camera = bpy.data.objects[cam_name]
    for f in (frames if cam_name != "CAM_CLOSE" else [0]):
        sc.frame_set(f)
        path = f"{PREVIEW}/{cam_name}_{f:03d}.png"
        sc.render.filepath = path
        bpy.ops.render.render(write_still=True)
        out.append(path)
result = {"frames": out}

"""GLOVE render helpers (Blender). Views in the G frame (+Y fingers, +X thumb, +Z dorsal): shoot(name, direction-from-target, up)."""
import os, sys, math
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np, bpy, mathutils
import acc_common as A


def setup(res=(900, 900), bg=(.30, .31, .33), samples=48, exposure=-.15):
    A.studio(res=res, bg=bg, samples=samples)
    sc = bpy.context.scene
    sc.view_settings.view_transform = 'Standard'; sc.view_settings.exposure = exposure; sc.view_settings.look = 'None'
    # key/fill rigged for Z-up product views: replace the N-frame rig with a softbox-ish rig around +Z / -Z / +X
    sc = bpy.context.scene
    for o in list(sc.objects):
        if o.type == 'LIGHT': bpy.data.objects.remove(o)
    def sun(name, direction, energy, color=(1, 1, 1)):
        L = bpy.data.lights.new(name, 'SUN'); L.energy = energy; L.color = color; L.angle = .5
        o = bpy.data.objects.new(name, L)
        o.rotation_euler = mathutils.Vector(direction).to_track_quat('-Z', 'Y').to_euler()
        sc.collection.objects.link(o)
    return sun


def rig_lights(cam_dir, sc=None):
    """lights relative to the camera direction (unit vector from target to camera)"""
    import bpy
    sc = bpy.context.scene
    for o in list(sc.objects):
        if o.type == 'LIGHT': bpy.data.objects.remove(o)
    d = mathutils.Vector(cam_dir).normalized()
    up = mathutils.Vector((0, 0, 1)) if abs(d.z) < .9 else mathutils.Vector((0, 1, 0))
    r = d.cross(up).normalized(); u = r.cross(d).normalized()
    def sun(name, from_dir, energy, color=(1, 1, 1)):
        L = bpy.data.lights.new(name, 'SUN'); L.energy = energy; L.color = color; L.angle = .45
        o = bpy.data.objects.new(name, L)
        o.rotation_euler = (-mathutils.Vector(from_dir)).to_track_quat('-Z', 'Y').to_euler()
        sc.collection.objects.link(o)
    sun('Key', d * .8 + u * .8 - r * .5, 3.2, (1, .97, .93))
    sun('Fill', d * .6 + r * .9 + u * .2, 1.3, (.88, .92, 1))
    sun('Rim', -d * .7 + u * .6 + r * .3, 1.6)


def shoot(path, target, direction, dist=.55, lens=85, ortho=None, up=(0, 0, 1)):
    sc = bpy.context.scene
    cam = sc.camera
    if cam is None:
        cd = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cd); sc.collection.objects.link(cam); sc.camera = cam
    cd = cam.data
    if ortho: cd.type = 'ORTHO'; cd.ortho_scale = ortho
    else: cd.type = 'PERSP'; cd.lens = lens
    d = mathutils.Vector(direction).normalized(); t = mathutils.Vector(target)
    cam.location = t + d * dist
    q = (t - cam.location).to_track_quat('-Z', 'Y')
    # roll so that `up` points up in the image
    z = (t - cam.location).normalized(); r = z.cross(mathutils.Vector(up)); 
    if r.length < 1e-6: r = z.cross(mathutils.Vector((0, 1, 0)))
    r.normalize(); uu = r.cross(z)
    m = mathutils.Matrix(((r.x, uu.x, -z.x), (r.y, uu.y, -z.y), (r.z, uu.z, -z.z)))
    cam.rotation_euler = m.to_euler()
    cd.clip_start = .005; cd.clip_end = 5
    rig_lights(d)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


GVIEWS = {  # name: (direction from target to camera, up)
    'back': ((0, 0, 1), (0, 1, 0)), 'palm': ((0, 0, -1), (0, 1, 0)), 'side': ((1, 0, 0), (0, 0, 1)), 'sideL': ((-1, 0, 0), (0, 0, 1)),
    'q34': ((.55, -.5, .75), (0, 1, 0)), 'front': ((0, 1, .0), (0, 0, 1)), 'cuff': ((0, -1, .45), (0, 0, 1)), 'tip': ((0, 1, .2), (0, 0, 1)),
}

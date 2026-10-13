"""Jaw sculpt helpers (2026-10-12). Runs inside Blender (bpy + numpy).

* frame()/game_warp(): a numpy port of Unity's HeroFaceReferenceFit.Warp (head-only runtime warp, WITHOUT the removed jawline block),
  so the Blender preview shows the head the game will draw (eyes 10 mm lower, male face x1.085, smaller ears ...).
* silhouette(): orthographic workbench render of a set of objects -> boolean mask + metres-per-pixel mapping.
* reference panels: crop + mask of the target sheets, aligned to our head by crown and chin height.
"""
import bpy, numpy as np, math, os, json

REPO = '/Users/adnanyonathan/Documents/Codex/2026-09-20/wh/outputs/iphone-golf-simulator'
WORK = REPO + '/work/jaw-sculpt-2026-10-12'
REF = REPO + '/ArtDir/hero/targets-2026-10-12/bald-base/'
SHEET = {'Male': '02_male_bald_head.jpg', 'Female': '04_female_bald_head.jpg'}
PANEL = {'front': (0, 320), 'three_quarter': (320, 640), 'side': (640, 960), 'back': (960, 1280)}


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def frame(body, feat, rig):
    """Unity's warp frame in Blender object space: eye = sclera centroid, up = head bone axis, forward = mean sclera normal."""
    fm = feat.data
    mats = [m.name for m in fm.materials]
    scl = [i for i, m in enumerate(mats) if 'Sclera' in m]
    ids = sorted({v for p in fm.polygons if p.material_index in scl for v in p.vertices})
    fco = np.array([fm.vertices[i].co[:] for i in ids]); fn = np.array([fm.vertices[i].normal[:] for i in ids])
    eye = fco.mean(0)
    hb = rig.data.bones['Head']
    up = np.array(hb.tail_local) - np.array(hb.head_local); up /= np.linalg.norm(up)
    fwd = fn.sum(0); fwd -= up * fwd.dot(up); fwd /= np.linalg.norm(fwd)
    right = np.cross(fwd, up); right /= np.linalg.norm(right)    # Blender is right-handed; the warp is mirror-symmetric in x
    return eye, right, up, fwd


def feature_sets(feat):
    fm = feat.data; brow, iris = set(), set()
    for p in fm.polygons:
        n = fm.materials[p.material_index].name
        if 'Brow' in n: brow.update(p.vertices)
        if any(k in n for k in ('Iris', 'Pupil', 'Limbal', 'Catch')): iris.update(p.vertices)
    return brow, iris


def game_warp(co, female, is_body, F, brow=(), iris=()):
    """Vectorised port of HeroFaceReferenceFit.Warp (Unity, current: no jaw block)."""
    eye, right, up, fwd = F
    d = co - eye
    x = d @ right; y = d @ up; z = d @ fwd
    n = len(co); idx = np.arange(n)
    isbrow = np.isin(idx, list(brow)) if len(brow) else np.zeros(n, bool)
    isiris = np.isin(idx, list(iris)) if len(iris) else np.zeros(n, bool)
    Settle = -.010
    head = smooth(-.17, -.12, y) * (1 - smooth(.16, .20, np.abs(x)))
    live = head >= .00001
    front = smooth(-.075, -.045, z)
    eyeCenter = .0453 if female else .0367
    orbit = smooth(-.085, -.022, y) * (1 - smooth(.060, .110, y)) * front
    eyeRegion = np.exp(-((np.abs(x) - eyeCenter) / .027) ** 4 - (y / .018) ** 4) * front
    if female and is_body:
        lid = np.exp(-((np.abs(x) - eyeCenter) / .025) ** 4 - ((y - .010) / .011) ** 4) * smooth(0, .005, y) * front
        y = np.where(y > 0, y + .0035 * lid, y)
    side = x / np.sqrt(x * x + .0001)
    if not female:
        m = ~isbrow
        y2 = y + y * .32 * eyeRegion
        irisRegion = np.exp(-((np.abs(x) - eyeCenter) / .030) ** 4) * eyeRegion
        x2 = x + (x - side * eyeCenter) * .20 * irisRegion
        y = np.where(m, y2, y); x = np.where(m, x2, x)
    orbitalSides = smooth(.012, .030, np.abs(x)) * np.exp(-(y / .050) ** 4)
    x = x - side * (.0028 if female else .0035) * orbit * orbitalSides
    y = y - .010 * orbit
    if female: y = np.where(isbrow, .0249 + (y - .0249) * .88, y)
    mouth = np.exp(-((y + .079) / .022) ** 4) * (1 - smooth(.045, .075, np.abs(x))) * front
    if not female: x = x * (1 + .06 * mouth)
    if is_body:
        if not female:
            ear = smooth(.072, .100, np.abs(x)) * (1 - smooth(-.085, -.025, z)) * smooth(-.100, -.060, y) * (1 - smooth(.010, .055, y))
            ang = np.radians(20) * ear
            dx = np.abs(x) - .078; dz = z + .090
            x = np.sign(x) * (.078 + dx * np.cos(ang) - dz * np.sin(ang))
            z = -.090 + dx * np.sin(ang) + dz * np.cos(ang)
        yc = (-.003 if female else -.017) + Settle; zc = -.138 if female else -.092; root = .0888 if female else .0906 / 1.085
        ry = .048 if female else .040; rz = .032 if female else .026; shrink = .16 if female else .14
        reach = np.sqrt(((y - yc) / ry) ** 2 + ((z - zc) / rz) ** 2)
        es = shrink * (1 - smooth(1, 1.6, reach)) * smooth(root - .030, root - .015, np.abs(x))
        on = es > 0
        y = np.where(on, yc + (y - yc) * (1 - es), y); z = np.where(on, zc + (z - zc) * (1 - es), z)
        lat = np.abs(x) - root
        x = np.where(on, x - np.sign(x) * es * smooth(0, .006, lat) * np.maximum(lat, 0), x)
        cheek = np.exp(-2 * ((np.abs(x) - (.058 if female else .052)) / .030) ** 2 - 2 * ((y + .049) / .032) ** 2) * smooth(-.055, -.010, z)
        z = z + (.0045 if female else .0035) * cheek
        x = x + side * .0012 * cheek
        if female:
            nose = np.exp(-(x / .029) ** 4 - ((y + .055) / .021) ** 4) * smooth(.014, .023, z)
            y = y + .0055 * nose; x = x * (1 + .12 * nose)
        tip = np.exp(-2 * (x / (.016 if female else .023)) ** 2 - 2 * ((y + (.043 if female else .049)) / (.015 if female else .019)) ** 2)
        bridge = np.exp(-2 * (x / (.0075 if female else .010)) ** 2 - 2 * ((y + .019) / .035) ** 2)
        alar = np.exp(-2 * ((np.abs(x) - (.016 if female else .020)) / .009) ** 2 - 2 * ((y + .053) / .011) ** 2)
        targetNose = (.015 if female else .035) + (.020 if female else .019) * tip + (.008 if female else .014) * bridge + .006 * alar
        noseBlend = np.exp(-(x / .032) ** 4 - ((y + .047) / .032) ** 4) * smooth(0 if female else .020, .028 if female else .052, z)
        noseBlend = noseBlend * smooth(-.074, -.060, y)
        z = z + (targetNose - z) * noseBlend * (.25 if female else .55)
        lipWidth = .034 if female else .041
        lipShape = np.clip(1 - x * x / (lipWidth * lipWidth), 0, 1) ** 2 * front
        lower = np.exp(-((y + (.0875 if female else .084)) / .0070) ** 2)
        z = z + (.0025 if female else .0018) * lower * lipShape
    if not female: x = x * (1 + .085 * head)
    # pass 2 block
    sx = np.sign(x); ax = np.abs(x)
    ec = .0467 if female else .0385; ey = (-.0006 if female else -.0015) + Settle; hw = .029 if female else .020; hh = .016 if female else .0075
    grow = 1.0 if female else 1.08; tall = 1.20 if female else 1.55; inward = .003 if female else 0
    dx = ax - ec; dy = y - ey; reach = np.sqrt(dx * dx / (hw * hw) + dy * dy / (hh * hh))
    eyeFront = smooth(-.060, -.035, z)
    eyeWeight = (1 - smooth(1.0, 2.6, reach)) * eyeFront
    ic = .0426 if female else .0366; iy = (.0007 if female else .0014) + Settle; irs = 1.06 if female else 1.16
    ax_i = ic + (ax - ic) * irs; y_i = iy + (y - iy) * irs
    other = (~isbrow) & (eyeWeight > 0) & (~isiris)
    ax_o = ec + dx * (1 + (grow - 1) * eyeWeight); y_o = ey + dy * (1 + (tall - 1) * eyeWeight)
    ax = np.where(isiris, ax_i, np.where(other, ax_o, ax)); y = np.where(isiris, y_i, np.where(other, y_o, y))
    ax = ax - inward * (1 - smooth(1.3, 3.2, reach)) * eyeFront * smooth(.004, .022, ax)
    x = sx * ax
    if is_body:
        noseFront = smooth(.004, .016, z) if female else smooth(.031, .045, z)
        nose = np.exp(-(x / .028) ** 4 - ((y - Settle + (.047 if female else .048)) / .030) ** 4) * noseFront
        x = x * (1 - (.22 if female else .24) * nose)
        z = z - (.0040 if female else .0045) * nose * smooth(0, 1, nose)
        yc = (-.0016 if female else -.0152) + Settle; zc = -.1384 if female else -.0932; root = .0892 if female else .0909
        ry = .052 if female else .036; rz = .032 if female else .022; shrink = .16 if female else .14
        er = np.sqrt(((y - yc) / ry) ** 2 + ((z - zc) / rz) ** 2)
        es = shrink * (1 - smooth(1, 1.6, er)) * smooth(root - .030, root - .015, np.abs(x))
        on = es > 0
        y = np.where(on, yc + (y - yc) * (1 - es), y); z = np.where(on, zc + (z - zc) * (1 - es), z)
        lat = np.abs(x) - root
        x = np.where(on, x - np.sign(x) * es * smooth(0, .006, lat) * np.maximum(lat, 0), x)
    out = eye + np.outer(x, right) + np.outer(y, up) + np.outer(z, fwd)
    return np.where(live[:, None], out, co)


def coords(me, key=None):
    a = np.empty(len(me.vertices) * 3)
    (me.shape_keys.key_blocks[key].data if key else me.vertices).foreach_get('co', a)
    return a.reshape(-1, 3)


def set_coords(me, co, key=None):
    (me.shape_keys.key_blocks[key].data if key else me.vertices).foreach_set('co', co.ravel())


def build_preview(sex):
    """GamePreview_Body / GamePreview_Features: copies of the meshes with the runtime warp applied (not exported)."""
    body = bpy.data.objects['Body']; feat = bpy.data.objects['ReferenceFeatures']; rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    F = frame(body, feat, rig); brow, iris = feature_sets(feat); female = sex == 'Female'
    col = bpy.data.collections.get('GamePreview') or bpy.data.collections.new('GamePreview')
    if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
    made = {}
    for src, name, isb in ((body, 'GamePreview_Body', True), (feat, 'GamePreview_Features', False)):
        co = coords(src.data)
        w = game_warp(co, female, isb, F, () if isb else brow, () if isb else iris)
        ob = bpy.data.objects.get(name)
        me = src.data.copy()                                   # fresh copy each time: carries the current custom normals
        if ob is None:
            ob = bpy.data.objects.new(name, me); col.objects.link(ob)
        else:
            old = ob.data; ob.data = me; bpy.data.meshes.remove(old)
        me.name = name
        if me.shape_keys: ob.shape_key_clear()
        me = ob.data
        if len(me.vertices) != len(co):
            raise RuntimeError('preview vertex count mismatch')
        set_coords(me, w); me.update()
        made[name] = float(np.abs(w - co).max())
    return made


def render_mask(objects, view, center, half, res=1200, path=None):
    """Orthographic flat silhouette of `objects`. view: 'side' (looking along +x from -x? see below) or 'front'.
    Returns mask (rows top->bottom) and a function mapping metres (u, z) -> pixel."""
    sc = bpy.context.scene
    cam = bpy.data.objects.get('JawCam')
    if cam is None:
        cam = bpy.data.objects.new('JawCam', bpy.data.cameras.new('JawCam')); sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = 2 * half; cam.data.clip_start = .01; cam.data.clip_end = 20
    cx, cy, cz = center
    if view == 'front':      # camera on -Y looking +Y (sees the face); image right = +X(Blender) = the hero's left
        cam.location = (cx, cy - 3, cz); cam.rotation_euler = (math.pi / 2, 0, 0)
    elif view == 'side':     # camera on +X looking -X: the face (-Y) appears on the image LEFT, like the target side panel
        cam.location = (cx + 3, cy, cz); cam.rotation_euler = (math.pi / 2, 0, math.pi / 2)
    elif view == 'three_quarter':
        a = math.radians(-40); cam.location = (cx + 3 * math.sin(a), cy - 3 * math.cos(a), cz); cam.rotation_euler = (math.pi / 2, 0, a)
    sc.camera = cam
    r = sc.render; r.engine = 'BLENDER_WORKBENCH'; r.resolution_x = res; r.resolution_y = res; r.resolution_percentage = 100
    r.film_transparent = True; r.image_settings.file_format = 'PNG'; r.image_settings.color_mode = 'RGBA'
    sh = sc.display.shading; sh.light = 'FLAT'; sh.color_type = 'SINGLE'; sh.single_color = (1, 1, 1); sh.show_object_outline = False
    hidden = {}
    for o in sc.objects:
        if o.type in ('MESH', 'CURVE', 'EMPTY'):
            hidden[o.name] = o.hide_render; o.hide_render = o.name not in objects
    out = path or (WORK + '/tmp_mask.png')
    sc.render.filepath = out
    bpy.ops.render.render(write_still=True)
    for k, v in hidden.items(): bpy.data.objects[k].hide_render = v
    im = bpy.data.images.load(out, check_existing=False); im.reload()
    a = np.array(im.pixels[:], dtype=np.float32).reshape(res, res, 4)[::-1, :, 3]; bpy.data.images.remove(im)
    return a > .5


def load_ref(sex):
    im = bpy.data.images.load(REF + SHEET[sex], check_existing=True)
    w, h = im.size
    px = np.array(im.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1, :, :3]
    mx = px.max(-1); mn = px.min(-1); sat = (mx - mn) / np.maximum(mx, 1e-4)
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    skin = (sat > .22) & (r > g) & (g > b * .95) & (r > .25)
    return px, skin


def edges(mask):
    """Per row: (first, last) filled column, or None."""
    out = []
    for r in range(mask.shape[0]):
        c = np.flatnonzero(mask[r])
        out.append((int(c.min()), int(c.max())) if len(c) > 2 else None)
    return out


def ref_side_anchor(sex):
    """Target side panel: crown row, chin-bottom row (biggest step back of the front edge below the mouth), cranium edges."""
    px, skin = load_ref(sex)
    x0, x1 = PANEL['side']; m = skin[:, x0:x1].copy()
    e = edges(m)
    rows = [r for r, v in enumerate(e) if v]
    crown = rows[0]
    best = (0, None)
    for r in rows:
        if r + 1 < len(e) and e[r] and e[r + 1] and r > crown + 200:
            jump = e[r + 1][0] - e[r][0]
            if jump > best[0]: best = (jump, r)
    return px[:, x0:x1], m, crown, best[1], e


def our_side_anchor(mask, C, H, zwin=(1.38, 1.52)):
    res = mask.shape[0]; mpp = 2 * H / res
    e = edges(mask); rows = [r for r, v in enumerate(e) if v]; crown = rows[0]
    best = (0, None)
    for r in rows:
        z = C[2] + H - r * mpp
        if r + 1 < len(e) and e[r] and e[r + 1] and zwin[0] < z < zwin[1]:
            jump = e[r + 1][0] - e[r][0]
            if jump > best[0]: best = (jump, r)
    return crown, best[1], e


def side_alignment(sex, ourmask, C, H, chin_z=None):
    """Map target-panel pixels to our metres: vertical scale by crown + chin bottom, horizontal shift = least squares on the cranium
    outline (crown to brow, front and back). chin_z pins our chin height (use the BEFORE value so a moved chin can't move the anchor)."""
    res = ourmask.shape[0]; mpp = 2 * H / res
    pan, rm, rc, rch, re = ref_side_anchor(sex)
    oc, och, oe = our_side_anchor(ourmask, C, H)
    z_crown = C[2] + H - oc * mpp
    z_chin = chin_z if chin_z is not None else C[2] + H - (och + .5) * mpp
    s = (z_crown - z_chin) / ((rch + .5) - rc)             # metres per target pixel
    def rz(r): return z_crown - (r - rc) * s
    # cranium: target rows from crown to 40 % of the head height
    rows = [r for r in range(rc + 5, int(rc + .40 * (rch - rc))) if re[r]]
    offs = []
    for r in rows:
        z = rz(r); orow = int(round((C[2] + H - z) / mpp))
        if 0 <= orow < res and oe[orow]:
            for k in (0, 1):
                offs.append((C[1] - H + oe[orow][k] * mpp) - re[r][k] * s)
    shift = float(np.median(offs))
    return {'s': s, 'shift': shift, 'z_crown': z_crown, 'z_chin': z_chin, 'ref_crown': rc, 'ref_chin': rch, 'panel': pan, 'refmask': rm, 'ref_edges': re}


def save_png(rgb, path):
    h, w = rgb.shape[:2]
    rgba = np.dstack([rgb, np.ones((h, w))]).astype(np.float32)[::-1]
    im = bpy.data.images.new('jaw_tmp_out', w, h, alpha=True); im.pixels.foreach_set(rgba.ravel())
    im.filepath_raw = path; im.file_format = 'PNG'; im.save(); bpy.data.images.remove(im)


def outline(mask):
    m = mask
    e = np.zeros_like(m)
    e[1:-1, 1:-1] = m[1:-1, 1:-1] & ~(m[:-2, 1:-1] & m[2:, 1:-1] & m[1:-1, :-2] & m[1:-1, 2:])
    return e


def side_overlay(sex, masks, C, H, path, al, zrange=None, colors=None):
    """Target side panel upscaled into our metre frame, with outlines: target (green) and each mask in `masks` (red, then yellow...)."""
    res = masks[0].shape[0]; mpp = 2 * H / res
    pan = al['panel']; rm = al['refmask']; s = al['s']
    rows = np.arange(res); cols = np.arange(res)
    zs = C[2] + H - rows * mpp; ys = C[1] - H + cols * mpp
    rr = np.clip(np.round(al['ref_crown'] + (al['z_crown'] - zs) / s).astype(int), 0, pan.shape[0] - 1)
    cc = np.round((ys - al['shift']) / s).astype(int); okc = (cc >= 0) & (cc < pan.shape[1]); cc = np.clip(cc, 0, pan.shape[1] - 1)
    img = pan[rr][:, cc] * okc[None, :, None] + .15 * (~okc)[None, :, None]
    refm = rm[rr][:, cc] & okc[None, :]
    img = img * .85
    img[outline(refm)] = (0, 1, 0)
    cols_ = colors or [(1, 0, 0), (1, 1, 0), (0, .6, 1)]
    for k, m in enumerate(masks):
        o = outline(m); o2 = o.copy(); o2[1:] |= o[:-1]; o2[:, 1:] |= o[:, :-1]
        img[o2] = cols_[k]
    if zrange:
        r0 = int((C[2] + H - zrange[1]) / mpp); r1 = int((C[2] + H - zrange[0]) / mpp); img = img[max(r0, 0):r1]
    save_png(img, path)
    return refm


def front_alignment(sex, ourmask, C, H, z_crown, z_chin, eye_z=None):
    """Target front panel -> our metres: same vertical mapping as the side (crown + chin rows of the FRONT panel), centred on the
    face's symmetry axis (mid-point of the cranium edges)."""
    px, skin = load_ref(sex)
    x0, x1 = PANEL['front']; m = skin[:, x0:x1].copy(); e = edges(m)
    rows = [r for r, v in enumerate(e) if v]; rc = rows[0]
    # chin bottom in the front view: lowest row of the face silhouette = where the neck starts is not visible;
    # use the side panel's chin row (same sheet, same camera height and scale)
    _, _, _, rch, _ = ref_side_anchor(sex)
    s = (z_crown - z_chin) / ((rch + .5) - rc)
    cen = np.median([(e[r][0] + e[r][1]) / 2 for r in range(rc + 10, rc + 80) if e[r]])
    return {'s': s, 'shift': -cen * s, 'z_crown': z_crown, 'z_chin': z_chin, 'ref_crown': rc, 'ref_chin': rch, 'panel': px[:, x0:x1], 'refmask': m, 'ref_edges': e}


def collar_line(fbx_paths, cy, step=15):
    """Highest garment point around the neck per angle (0 = front, +-180 = back) over all tops in `fbx_paths`; the probe objects are
    imported into a temporary collection and removed again."""
    col = bpy.data.collections.new('KitProbe'); bpy.context.scene.collection.children.link(col)
    line = {}; per = {}
    try:
        for f in fbx_paths:
            before = set(bpy.data.objects)
            bpy.ops.import_scene.fbx(filepath=f)
            new = [o for o in bpy.data.objects if o not in before]
            for o in new:
                for c in o.users_collection: c.objects.unlink(o)
                col.objects.link(o)
            dg = bpy.context.evaluated_depsgraph_get()
            pts = []
            for o in new:
                if o.type != 'MESH' or 'Top' not in o.name: continue
                oe = o.evaluated_get(dg); m = oe.to_mesh()
                co = np.array([(o.matrix_world @ v.co)[:] for v in m.vertices]); oe.to_mesh_clear()
                pts.append(co)
            co = np.vstack(pts)
            near = co[(np.hypot(co[:, 0], co[:, 1] - cy) < .11) & (co[:, 2] > 1.25)]
            ang = np.degrees(np.arctan2(near[:, 0], -(near[:, 1] - cy)))
            per[os.path.basename(os.path.dirname(f)) + '/' + os.path.basename(f)] = this = {}
            for a in range(-180, 181, step):
                s = near[np.abs(((ang - a + 180) % 360) - 180) < step / 2]
                if len(s):
                    this[a] = round(float(s[:, 2].max()), 4)
                    line[a] = min(line.get(a, 9), this[a])
    finally:
        for o in list(col.objects): bpy.data.objects.remove(o)
        bpy.data.collections.remove(col)
        for m in [m for m in bpy.data.meshes if m.users == 0]: bpy.data.meshes.remove(m)
        for a in [a for a in bpy.data.armatures if a.users == 0]: bpy.data.armatures.remove(a)
    return line, per


def place_refs(sex, al_side, al_front, side_x=-.30, front_y=.30):
    """Reference heads behind the model as image empties, lined up to our head (crown + chin heights; side panel shifted by the cranium
    fit, front panel centred on the head's axis). Each one shows only in its own orthographic view."""
    px, _ = load_ref(sex)
    col = bpy.data.collections.get('JawReferences') or bpy.data.collections.new('JawReferences')
    if col.name not in bpy.context.scene.collection.children: bpy.context.scene.collection.children.link(col)
    made = {}
    for view, al, (x0, x1) in (('side', al_side, PANEL['side']), ('front', al_front, PANEL['front'])):
        crop = px[:, x0:x1]
        path = f'{WORK}/refs/{sex}_{view}_panel.png'
        save_png(crop, path)
        img = bpy.data.images.load(path, check_existing=False); img.name = f'Ref_{sex}_{view}'; img.reload()
        name = f'Ref_{sex}_{view.capitalize()}'
        ob = bpy.data.objects.get(name) or bpy.data.objects.new(name, None)
        if ob.name not in col.objects: col.objects.link(ob)
        ob.empty_display_type = 'IMAGE'; ob.data = img
        h, w = crop.shape[:2]; s = al['s']
        ob.empty_display_size = max(h, w) * s
        ob.empty_image_offset = (-.5, -.5)
        ob.empty_image_depth = 'BACK'; ob.show_empty_image_only_axis_aligned = True
        ob.show_empty_image_orthographic = True; ob.show_empty_image_perspective = False
        ob.use_empty_image_alpha = True; ob.color[3] = .85
        zc = al['z_crown'] - (h / 2 - al['ref_crown']) * s
        uc = (w / 2) * s + al['shift']
        if view == 'side':
            ob.location = (side_x, uc, zc); ob.rotation_euler = (math.pi / 2, 0, math.pi / 2)
        else:
            ob.location = (uc, front_y, zc); ob.rotation_euler = (math.pi / 2, 0, 0)
        ob.hide_render = True
        made[name] = {'size_m': ob.empty_display_size, 'loc': tuple(round(v, 4) for v in ob.location), 'm_per_px': s}
    return made


def topology(me):
    """Welded (by exact position) triangle list, vertex->unique map and unique edges."""
    co = coords(me)
    key = np.round(co, 6)
    uniq, idx = np.unique(key, axis=0, return_inverse=True)
    idx = idx.ravel()
    me.calc_loop_triangles()
    tri = np.empty(len(me.loop_triangles) * 3, dtype=np.int64); me.loop_triangles.foreach_get('vertices', tri)
    tri = idx[tri.reshape(-1, 3)]
    e = np.vstack([tri[:, [0, 1]], tri[:, [1, 2]], tri[:, [2, 0]]]); e.sort(1); e = np.unique(e, axis=0)
    e = e[e[:, 0] != e[:, 1]]
    return idx, tri, e, len(uniq)


def vertex_normals(P, tri):
    n = np.zeros_like(P)
    c = np.cross(P[tri[:, 1]] - P[tri[:, 0]], P[tri[:, 2]] - P[tri[:, 0]])
    for k in range(3): np.add.at(n, tri[:, k], c)
    return n / np.maximum(np.linalg.norm(n, axis=1)[:, None], 1e-12)


def shaded(objects, view, center, half, res, path, light='STUDIO', matcap=None, color=(.80, .62, .50), cavity=False):
    """Workbench shaded still of `objects` from 'front' / 'side' / 'three_quarter' (orthographic)."""
    sc = bpy.context.scene
    render_mask(objects, view, center, half, 8, WORK + '/tmp_mask.png')   # positions the camera
    r = sc.render; r.film_transparent = False; r.resolution_x = res; r.resolution_y = res
    sh = sc.display.shading; sh.light = light; sh.color_type = 'SINGLE'; sh.single_color = color
    sh.show_cavity = cavity; sh.show_object_outline = False; sh.show_specular_highlight = True
    if light == 'STUDIO': sh.studio_light = 'Default'
    if matcap: sh.light = 'MATCAP'; sh.studio_light = matcap
    sc.world.color = (.42, .42, .42)
    hidden = {}
    for o in sc.objects:
        if o.type in ('MESH', 'CURVE', 'EMPTY'):
            hidden[o.name] = o.hide_render; o.hide_render = o.name not in objects
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    for k, v in hidden.items(): bpy.data.objects[k].hide_render = v
    r.film_transparent = True
    return path


def topology_at(co, me):
    """topology() but welding by the given coordinates (the untouched source), so iterations keep one fixed graph."""
    key = np.round(co, 6)
    uniq, idx = np.unique(key, axis=0, return_inverse=True); idx = idx.ravel()
    me.calc_loop_triangles()
    tri = np.empty(len(me.loop_triangles) * 3, dtype=np.int64); me.loop_triangles.foreach_get('vertices', tri)
    tri = idx[tri.reshape(-1, 3)]
    e = np.vstack([tri[:, [0, 1]], tri[:, [1, 2]], tri[:, [2, 0]]]); e.sort(1); e = np.unique(e, axis=0)
    return idx, tri, e[e[:, 0] != e[:, 1]], len(uniq)


def dilate(edges, n, mask, k):
    m = mask.copy()
    for _ in range(k):
        nm = m.copy(); nm[edges[m[edges[:, 0]], 1]] = True; nm[edges[m[edges[:, 1]], 0]] = True; m = nm
    return m


def evaluate(sex, tag, zoom=None):
    """Side + front silhouette overlays (target green, before yellow, now red) and shaded stills of the game preview."""
    a = json.load(open(f'{WORK}/align_{sex}.json')); C = tuple(a['C']); H = a['H']
    out = f'{WORK}/eval'
    side = render_mask(['GamePreview_Body'], 'side', C, H, 1200, out + '/tmp_side.png')
    front = render_mask(['GamePreview_Body'], 'front', C, H, 1200, out + '/tmp_front.png')
    np.save(f'{out}/{tag}_side_{sex}.npy', side); np.save(f'{out}/{tag}_front_{sex}.npy', front)
    bs = np.load(f'{out}/before_side_{sex}.npy'); bf = np.load(f'{out}/before_front_{sex}.npy')
    al = side_alignment(sex, bs, C, H, chin_z=a['side']['z_chin'])
    alf = front_alignment(sex, bf, C, H, al['z_crown'], al['z_chin'])
    side_overlay(sex, [side, bs], C, H, f'{out}/{tag}_ov_side_{sex}.png', al, colors=[(1, 0, 0), (1, 1, 0)])
    side_overlay(sex, [front, bf], (0, 0, C[2]), H, f'{out}/{tag}_ov_front_{sex}.png', alf, colors=[(1, 0, 0), (1, 1, 0)])
    zc = C if zoom is None else zoom
    for v in ('front', 'three_quarter', 'side'):
        shaded(['GamePreview_Body', 'GamePreview_Features'], v, zc, .125, 640, f'{out}/{tag}_{v}_{sex}.png')
    return out


def table(sex, tag, zs):
    """mm per height: side front edge (before / now / target), front half-width (before / now / target)."""
    a = json.load(open(f'{WORK}/align_{sex}.json')); C = tuple(a['C']); H = a['H']; mpp = 2 * H / 1200
    out = f'{WORK}/eval'
    bs = np.load(f'{out}/before_side_{sex}.npy'); bf = np.load(f'{out}/before_front_{sex}.npy')
    ns = np.load(f'{out}/{tag}_side_{sex}.npy'); nf = np.load(f'{out}/{tag}_front_{sex}.npy')
    al = side_alignment(sex, bs, C, H, chin_z=a['side']['z_chin']); alf = front_alignment(sex, bf, C, H, al['z_crown'], al['z_chin'])
    E = {k: edges(m) for k, m in (('bs', bs), ('ns', ns), ('bf', bf), ('nf', nf))}
    re, rf = al['ref_edges'], alf['ref_edges']
    rows = []
    for z in zs:
        r = int(round((C[2] + H - z) / mpp))
        rr = int(round(al['ref_crown'] + (al['z_crown'] - z) / al['s'])); rfr = int(round(alf['ref_crown'] + (alf['z_crown'] - z) / alf['s']))
        f = lambda e: round((C[1] - H + e[0] * mpp) * 1000, 1) if e else None
        w = lambda e: round((e[1] - e[0]) / 2 * mpp * 1000, 1) if e else None
        rows.append((round(z, 3), f(E['bs'][r]), f(E['ns'][r]), round((re[rr][0] * al['s'] + al['shift']) * 1000, 1) if re[rr] else None,
                     w(E['bf'][r]), w(E['nf'][r]), round((rf[rfr][1] - rf[rfr][0]) / 2 * alf['s'] * 1000, 1) if rf[rfr] else None))
    return rows


def game_normals(sex, jaw_radius=.008, jaw_blend=(.004, .040), base_radius=.012, obj='GamePreview_Body'):
    """Port of HeroSkinNormals.Continuous (area-weighted) onto the game preview, so Blender stills shade like the game: area-weighted
    geometric normals averaged within a per-vertex radius (12 mm; mouth 6, nose 5, ear 3.5; under the jawline `jaw_radius`, blended
    over `jaw_blend` above the line), neighbours only if their normals agree (dot >= .5), weight falloff^2 * agreement^2 * area; the
    result replaces the normal in the head region (strength fades below the neck bone like the game)."""
    from mathutils.kdtree import KDTree
    ob = bpy.data.objects[obj]; me = ob.data
    rig = next(o for o in bpy.data.objects if o.type == 'ARMATURE' and o.name.startswith('Rig_'))
    hb = np.array(rig.data.bones['Head'].head_local); nb = np.array(rig.data.bones['Neck'].head_local)
    co = coords(me)
    key = np.round(co, 6); uniq, idx = np.unique(key, axis=0, return_inverse=True); idx = idx.ravel(); U = uniq.astype(float); nu = len(U)
    me.calc_loop_triangles()
    tri = np.empty(len(me.loop_triangles) * 3, dtype=np.int64); me.loop_triangles.foreach_get('vertices', tri); tri = idx[tri.reshape(-1, 3)]
    c = np.cross(U[tri[:, 1]] - U[tri[:, 0]], U[tri[:, 2]] - U[tri[:, 0]]); area = np.linalg.norm(c, axis=1)
    geo = np.zeros((nu, 3)); rep = np.zeros(nu)
    for k in range(3): np.add.at(geo, tri[:, k], c); np.add.at(rep, tri[:, k], area)
    geo /= np.maximum(np.linalg.norm(geo, axis=1), 1e-12)[:, None]
    up = (hb - nb) / np.linalg.norm(hb - nb); neckH = (nb - hb) @ up
    d = U - hb; h = d @ up; radial = np.linalg.norm(d - np.outer(h, up), axis=1)
    strength = smooth(neckH - .032, neckH + .004, h) * (1 - smooth(.145, .185, radial))
    lx = np.abs(U[:, 0]); ly = U[:, 2] - hb[2]; lz = -(U[:, 1] - hb[1])
    mouth = (1 - smooth(.040, .065, lx)) * (1 - smooth(.014, .036, np.abs(ly + .007))) * smooth(.065, .095, lz)
    rad = base_radius + (.006 - base_radius) * mouth
    nose = np.exp(-2 * (lx / .030) ** 2 - 2 * ((ly - .027) / .023) ** 2) * smooth(.105, .120, lz)
    ear = smooth(.078, .096, lx) * (1 - smooth(.030, .060, lz)) * (1 - smooth(.070, .110, np.abs(ly - .040)))
    rad = rad + (.005 - rad) * nose; rad = rad + (.0035 - rad) * ear
    jl = .005 + ((-.007 - .5 * lz) - .005) * smooth(.005, .030, lz)
    jm = 1 - smooth(jl + jaw_blend[0], jl + jaw_blend[1], ly)
    rad = np.minimum(rad, rad + (jaw_radius - rad) * jm)
    sel = np.flatnonzero(strength > .001)
    kd = KDTree(len(sel))
    for n, i in enumerate(sel): kd.insert(U[i], n)
    kd.balance()
    field = geo.copy()
    for i in sel:
        r = rad[i]; acc = geo[i] * rep[i]; cnt = rep[i]
        for (_, n, dist) in kd.find_range(U[i], r):
            j = sel[n]; ag = geo[i] @ geo[j]
            if ag < .5: continue
            f = 1 - dist * dist / (r * r); w = f * f * ag * ag * rep[j]
            acc = acc + geo[j] * w; cnt += w
        field[i] = acc / max(cnt, 1e-12)
    field /= np.maximum(np.linalg.norm(field, axis=1), 1e-12)[:, None]
    # outside the head region keep the mesh's own (custom) normals; blend by strength
    old = np.empty(len(me.loops) * 3); me.corner_normals.foreach_get('vector', old); old = old.reshape(-1, 3)
    lv = np.empty(len(me.loops), dtype=np.int64); me.loops.foreach_get('vertex_index', lv)
    s = strength[idx[lv]][:, None]
    new = old * (1 - s) + field[idx[lv]] * s
    new /= np.maximum(np.linalg.norm(new, axis=1), 1e-12)[:, None]
    me.normals_split_custom_set(new.tolist()); me.update()
    return dict(selected=int(len(sel)), jaw_verts=int((jm > .5).sum()))


LINE_VIEW = {'Male': ((0, -0.08, 1.485), (-0.03, -0.07, 1.485)), 'Female': ((0, -0.06, 1.46), (-0.025, -0.05, 1.46))}


def line_closeups(tag, sex, h=.05):
    """Tight side + three-quarter stills of the jawline (game preview)."""
    cs, cq = LINE_VIEW[sex]
    shaded(['GamePreview_Body', 'GamePreview_Features'], 'side', cs, h, 800, f'{WORK}/eval/{tag}_line_side_{sex}.png')
    shaded(['GamePreview_Body', 'GamePreview_Features'], 'three_quarter', cq, h, 800, f'{WORK}/eval/{tag}_line_3q_{sex}.png')

"""Golf swings authored from scratch for Adnan's match heroes (imported by matchhero_golf.py; runs inside Blender, with his rig loaded).

Nothing here is copied from a studio clip or retargeted from motion capture. It is built the way an animator builds a swing: a handful of KEY POSES (address, takeaway,
the top, the first move down, impact, follow-through, finish), each a few numbers a golf coach would use; smooth curves through them (monotone cubics, so a swing never
wobbles between its poses); and a solver that poses his skeleton from the numbers. What real form is, in numbers, comes from golf instruction and from measuring real golf
(blender/scripts/cmu_golf_measure.py reads CMU's public motion capture of a golfer: how far the shoulders and hips turn, how the lead arm stays long while the trail elbow
folds, where the weight goes, how the knees straighten into the ball, and when each happens).

The golfer's frame (the clips' root space): the ball is in front of him along -X, the target is to his left along -Y, up is +Z (a right-hander: his left side leads).

THE BODY, in degrees and metres
  pelvis    turn (+ back: clockwise from above), forward tilt of the pelvis, side tilt, slide toward the target (+), push toward the ball (+), lift
  spine     the chest turns about the spine: the twist between hips and shoulders is spread up the spine; forward bend; side bend (+ away from the target); the collar bones
            turn a little further than the chest (`girdle`: the lead shoulder slides across the chest in the backswing)
  head      turns less than the chest, stays tipped to look at the ball
  feet      planted: the lead foot flared, the trail heel comes up through the ball; knees from the legs' two-bone solve
THE CLUB AND THE ARMS
  The club is the thing authored: the lead hand's place relative to the shoulders (in the chest's own turning frame: the arms stay in front of the chest), the shaft's direction,
  the face's roll about the shaft. The hands sit on the grip, palms facing each other across it, and the arms are solved from the shoulders to the wrists.
"""
import math
from mathutils import Vector, Matrix, Quaternion

DEG = math.pi / 180.0
UP = Vector((0.0, 0.0, 1.0))
FWD = Vector((-1.0, 0.0, 0.0))        # toward the ball
TGT = Vector((0.0, -1.0, 0.0))        # toward the target


def Rx(d): return Matrix.Rotation(d * DEG, 3, 'X')
def Ry(d): return Matrix.Rotation(d * DEG, 3, 'Y')
def Rz(d): return Matrix.Rotation(d * DEG, 3, 'Z')
def lerp(a, b, t): return a + (b - a) * t


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * (3 - 2 * t)


def perp(v, axis):
    """v with its part along `axis` taken out."""
    a = axis.normalized()
    return v - a * v.dot(a)


def seg_dist(p1, q1, p2, q2):
    """The closest two segments p1-q1 and p2-q2 come to each other."""
    u, v, w = q1 - p1, q2 - p2, p1 - p2
    a, b, c, d, e = u.dot(u), u.dot(v), v.dot(v), u.dot(w), v.dot(w)
    D = a * c - b * b
    sD = tD = D
    if D < 1e-9: sN, sD, tN, tD = 0.0, 1.0, e, c
    else:
        sN, tN = b * e - c * d, a * e - b * d
        if sN < 0: sN, tN, tD = 0.0, e, c
        elif sN > sD: sN, tN, tD = sD, e + b, c
    if tN < 0:
        tN = 0.0
        if -d < 0: sN = 0.0
        elif -d > a: sN = sD
        else: sN, sD = -d, a
    elif tN > tD:
        tN = tD
        if (-d + b) < 0: sN = 0.0
        elif (-d + b) > a: sN = sD
        else: sN, sD = (-d + b), a
    sc = 0.0 if abs(sN) < 1e-9 else sN / sD
    tc = 0.0 if abs(tN) < 1e-9 else tN / tD
    return (w + u * sc - v * tc).length


def frame(y, z_hint):
    """A right-handed frame (columns x, y, z) with the given y axis and the z axis as near z_hint as it can be."""
    y = y.normalized()
    z = perp(z_hint, y)
    if z.length < 1e-6: z = perp(Vector((0, 0, 1)) if abs(y.z) < 0.9 else Vector((1, 0, 0)), y)
    z.normalize()
    x = y.cross(z)
    m = Matrix(((x.x, y.x, z.x), (x.y, y.y, z.y), (x.z, y.z, z.z)))
    return m


def cols(x, y, z):
    return Matrix(((x.x, y.x, z.x), (x.y, y.y, z.y), (x.z, y.z, z.z)))


class Curve:
    """A smooth curve through keys [(t, value)]: monotone cubic (Fritsch-Carlson), so it never overshoots between two keys."""
    def __init__(self, keys):
        ks = sorted(keys)
        self.t = [k[0] for k in ks]
        self.v = [float(k[1]) for k in ks]
        n = len(self.t)
        self.m = [0.0] * n
        if n > 1:
            h = [self.t[i + 1] - self.t[i] for i in range(n - 1)]
            d = [(self.v[i + 1] - self.v[i]) / h[i] for i in range(n - 1)]
            if n == 2: self.m = [d[0], d[0]]
            else:
                for i in range(1, n - 1):
                    if d[i - 1] * d[i] > 0:
                        w1, w2 = 2 * h[i] + h[i - 1], h[i] + 2 * h[i - 1]
                        self.m[i] = (w1 + w2) / (w1 / d[i - 1] + w2 / d[i])
                def end(h0, h1, d0, d1):
                    e = ((2 * h0 + h1) * d0 - h0 * d1) / (h0 + h1)
                    if e * d0 <= 0: return 0.0
                    if d0 * d1 <= 0 and abs(e) > 3 * abs(d0): return 3 * d0
                    return e
                self.m[0] = end(h[0], h[1], d[0], d[1]); self.m[-1] = end(h[-1], h[-2], d[-1], d[-2])

    def __call__(self, x):
        t, v, m = self.t, self.v, self.m
        if x <= t[0]: return v[0]
        if x >= t[-1]: return v[-1]
        k = 0
        while t[k + 1] < x: k += 1
        h = t[k + 1] - t[k]
        s = (x - t[k]) / h
        return ((2 * s ** 3 - 3 * s ** 2 + 1) * v[k] + (s ** 3 - 2 * s ** 2 + s) * h * m[k] + (-2 * s ** 3 + 3 * s ** 2) * v[k + 1] + (s ** 3 - s ** 2) * h * m[k + 1])


def curves(table):
    """{name: [(t, v), ...]} -> {name: Curve}."""
    return {k: Curve(v) for k, v in table.items()}


class VecCurve:
    """A 3D curve through keys [(t, Vector)]: one monotone cubic per component."""
    def __init__(self, keys):
        self.c = [Curve([(t, v[i]) for t, v in keys]) for i in range(3)]

    def __call__(self, x): return Vector((self.c[0](x), self.c[1](x), self.c[2](x)))


class QuatCurve:
    """Orientation keys [(t, Matrix3)] -> a smooth orientation: monotone cubics through each quaternion component (hemisphere-aligned), normalised."""
    def __init__(self, keys):
        qs = [m.to_quaternion() for _t, m in keys]
        for i in range(1, len(qs)):
            if qs[i].dot(qs[i - 1]) < 0: qs[i] = -qs[i]
        self.c = [Curve([(keys[i][0], qs[i][j]) for i in range(len(qs))]) for j in range(4)]

    def __call__(self, x):
        q = Quaternion((self.c[0](x), self.c[1](x), self.c[2](x), self.c[3](x)))
        q.normalize()
        return q.to_matrix()


def two_bone(Sp, E, Tg, l1, l2, radius=None):
    """The elbow (or knee) of a two-bone chain from Sp reaching Tg with lengths l1, l2, bent toward the hint E, and the end it reaches.
    `radius` (p -> how deep p is in the torso, 1 on the skin) turns the elbow about the Sp->Tg line, the least it takes, until it is out of the torso."""
    d = (Tg - Sp).length
    d = min(max(d, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    u = (Tg - Sp).normalized()
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d)
    h = math.sqrt(max(l1 * l1 - a * a, 0.0))
    v = perp(E - Sp, u)
    if v.length < 1e-6: v = perp(Vector((0, -1, 0)), u)
    v.normalize()
    elbow = Sp + u * a + v * h
    if radius is not None and radius(elbow) < 1.0:
        # turned about the line the least that clears the torso (no further than 40 degrees, and to whichever side clears it better, so the choice does not flip between frames)
        w = u.cross(v)
        best = (radius(elbow), elbow)
        for k in range(1, 9):
            ang = math.radians(5 * k)
            c1 = Sp + u * a + (v * math.cos(ang) + w * math.sin(ang)) * h
            c2 = Sp + u * a + (v * math.cos(ang) - w * math.sin(ang)) * h
            r1, r2 = radius(c1), radius(c2)
            cand = (r1, c1) if r1 >= r2 else (r2, c2)
            if cand[0] > best[0]: best = cand
            if best[0] >= 1.0: break
        return best[1], Sp + u * d
    return elbow, Sp + u * d


class Rig:
    """His rest skeleton and the forward kinematics that pose it: `put(name, world rotation, head)` for each bone parent first; basis[n] is the pose-bone basis
    (rotation matrix, location) Blender wants."""
    def __init__(self, heads, rots, parent, lengths):
        self.h, self.R, self.parent, self.len = heads, rots, parent, lengths



class Pose:
    def __init__(self, rig):
        self.rig = rig
        self.posed, self.heads, self.basis = {}, {}, {}

    def put(self, n, R, head=None):
        r = self.rig
        p = r.parent[n]
        if p is None:
            base = r.R[n]; exp = r.h[n]
        else:
            base = self.posed[p] @ r.R[p].inverted() @ r.R[n]
            exp = self.heads[p] + self.posed[p] @ r.R[p].inverted() @ (r.h[n] - r.h[p])
        h = exp if head is None else head
        self.basis[n] = (base.inverted() @ R, base.inverted() @ (h - exp))
        self.posed[n] = R
        self.heads[n] = h

    def child_head(self, n, parent):
        """Where bone n's head is, given its parent's posed rotation and head (the rest offset carried along)."""
        r = self.rig
        return self.heads[parent] + self.posed[parent] @ r.R[parent].inverted() @ (r.h[n] - r.h[parent])


# ======================================================================= the clubs
LIE = {"Driver": 56.0, "Iron": 62.0, "Wedge": 64.0, "Putter": 70.0}      # a club's lie: the shaft's angle to the ground when the sole sits flat
CLUB_BALL_Z = {"Driver": 0.040, "Iron": 0.0214, "Wedge": 0.0214, "Putter": 0.0214}      # the ball's centre (the driver's ball is on a tee)
CLUB_SOLE_Z = {"Driver": 0.008, "Iron": 0.0, "Wedge": 0.0, "Putter": 0.0}               # the lowest point of the head at address
BALL_R = 0.0214


def prepare_club_mesh(obj, lie):
    """The studio's clubs are left-handed models (toe +X, face -Y, shaft +Z is a mirror of a right-hander's) with the sole square to the shaft. Mirror the mesh (y -> -y, the
    faces turned back) so that toe = +X, face = +Y, up the shaft = +Z is a right-handed club, and tip the head about the face's axis by (90 - lie) so that the sole sits flat when the
    shaft is at the club's lie. The lower shaft eases into the tilt over the hosel."""
    import bmesh
    me = obj.data
    if me.get("golf_swing_prepared"): return
    for v in me.vertices: v.co.y = -v.co.y
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.reverse_faces(bm, faces=bm.faces); bm.to_mesh(me); bm.free()
    zmin = min(v.co.z for v in me.vertices)
    pivot = Vector((0.0, 0.0, zmin + 0.03))
    head_top = zmin + 0.096
    eps = math.radians(90.0 - lie)
    for v in me.vertices:
        w = smooth((head_top + 0.07 - v.co.z) / 0.07)           # 1 on the head, 0 up the shaft above the hosel
        if w <= 0: continue
        p = v.co - pivot
        q = (Matrix.Rotation(eps * w, 3, 'Y') @ p) + pivot
        v.co = q
    me.update()
    me["golf_swing_prepared"] = True


class ClubGeo:
    """A prepared club mesh, measured: the face's centre and normal, the head's far tip, the butt, and the vertices (for 'how low is the head')."""
    def __init__(self, obj, name, length, v4_length):
        me = obj.data
        vs = [v.co.copy() for v in me.vertices]
        self.k = length / v4_length
        self.name = name
        self.verts = [Vector((v.x, v.y, v.z * self.k)) for v in vs]
        zmin = min(v.z for v in vs)
        area = 0.0; cen = Vector((0, 0, 0)); nrm = Vector((0, 0, 0))
        for p in me.polygons:
            if max(me.vertices[i].co.z for i in p.vertices) > zmin + 0.14: continue
            n = p.normal
            if n.y > 0.5:
                a = p.area; area += a; cen += p.center * a; nrm += n * a
        cen /= area; nrm.normalize()
        self.fc = Vector((cen.x, cen.y, cen.z * self.k))
        self.fn = nrm
        self.top = max(v.z for v in self.verts)
        self.tip = min(self.verts, key=lambda v: v.z)
        self.zmin = min(v.z for v in self.verts)


def club_frame(s_up, fperp):
    """The club's frame in the world: columns = local x (toe), y (the face's normal, less its loft), z (up the shaft)."""
    z = s_up.normalized()
    y = perp(fperp, z)
    if y.length < 1e-6: y = perp(Vector((0, -1, 0)), z)
    y.normalize()
    x = y.cross(z)
    return cols(x, y, z)


def place_club(geo, R, ball, sole_z, gap=BALL_R):
    """Where the club bone's origin goes for the head to sit at the ball: the face's centre one ball radius behind the ball's centre along the face normal, in line with it
    along the toe, and the lowest point of the head at sole_z."""
    nw = (R @ geo.fn).normalized()
    tw = R @ Vector((1, 0, 0))
    fc = R @ geo.fc
    low = min((R @ v).z for v in geo.verts)
    A = Matrix((tuple(nw), tuple(tw), (0.0, 0.0, 1.0)))
    rhs = Vector(((ball - fc).dot(nw) - gap, (ball - fc).dot(tw), sole_z - low))
    return A.inverted() @ rhs


# ======================================================================= the golfer
SIDES = (("L", "Left"), ("R", "Right"))
FINGERS = ("Index", "Middle", "Ring", "Little")


def seg(turn, bend, side, base=-90.0):
    """A body segment's orientation change from his rest pose (which faces -Y): turned `turn` degrees back (clockwise from above) about its own spine, then bent `bend` forward
    and `side` away from the target; `base` turns the rest pose to face the ball."""
    return Rx(-side) @ Ry(-bend) @ Rz(-turn + base)


class Golfer:
    """One golfer (a rig, his arm and leg lengths, the clubs) and the solver that poses him from a table of curves."""
    def __init__(self, rig, female, geos):
        self.rig, self.female, self.geos = rig, female, geos
        self.armlen = {s: (rig.len[f"arm{s}"][0], rig.len[f"arm{s}"][1]) for s, _ in SIDES}
        self.leglen = {s: (rig.len[f"leg{s}"][0], rig.len[f"leg{s}"][1]) for s, _ in SIDES}
        self.reach = sum(self.armlen["L"])
        self.torso = (0.19 + 0.05, 0.13 + 0.05) if not female else (0.165 + 0.05, 0.12 + 0.05)
        self.neck_z = rig.h["Neck"].z - rig.h["Hips"].z
        self.size = rig.h["Hips"].z / 0.886          # (this golfer's size against the male's, for the distances that are a share of the body)
        # how far his hand is turned about the forearm from the forearm's own frame at rest: a hand that is turned no further than that is not twisting the skin at the wrist
        self.wrist_rest = {}
        for s_, side_ in SIDES:
            la = rig.R[f"{side_}LowerArm"]; ha = rig.R[f"{side_}Hand"]
            y = la @ Vector((0, 1, 0))
            self.wrist_rest[s_] = self.twist_between(la @ Vector((0, 0, 1)), ha @ Vector((0, 0, 1)), y)

    @staticmethod
    def twist_between(za, zb, y):
        """The angle (degrees, about y) from za to zb, each with its part along y taken out."""
        a, b = perp(za, y).normalized(), perp(zb, y).normalized()
        return math.degrees(math.atan2(y.dot(a.cross(b)), a.dot(b)))

    # ---- the body
    def body(self, p, club):
        """The pose of the pelvis, spine, head, collar bones from the parameters p; the legs and arms are solved after."""
        rig = self.rig
        P = Pose(rig)
        st = p["stance"]
        hips = Vector((st["px"], st["py"], st["pz"])) + TGT * p["slide"] + FWD * p["push"] + UP * p["lift"]
        P.put("Root", rig.R["Root"], rig.h["Root"])
        Dp = seg(p["hip_turn"], p["pelvis_bend"], p["hip_side"])
        P.put("Hips", Dp @ rig.R["Hips"], hips)
        tw = p["hip_turn"], p["shoulder_turn"]
        for name, w, bend, side in (("Spine", 0.30, p["spine_bend"], p["spine_side"]), ("Chest", 0.62, p["chest_bend"], p["chest_side"]), ("UpperChest", 1.0, p["chest_bend"], p["chest_side"])):
            P.put(name, seg(lerp(tw[0], tw[1], w), bend, side) @ rig.R[name], P.child_head(name, "Hips" if name == "Spine" else ("Spine" if name == "Chest" else "Chest")))
        Dc = seg(p["shoulder_turn"], p["chest_bend"], p["chest_side"])
        P.put("Neck", seg(lerp(p["shoulder_turn"], p["head_turn"], 0.5), p["neck_bend"], p["chest_side"] * 0.5) @ rig.R["Neck"], P.child_head("Neck", "UpperChest"))
        P.put("Head", seg(p["head_turn"], p["head_bend"], p["chest_side"] * 0.3) @ rig.R["Head"], P.child_head("Head", "Neck"))
        Dg = seg(p["shoulder_turn"] + p.get("girdle", 0.0), p["chest_bend"], p["chest_side"])       # the collar bones turn a little further than the chest: the lead shoulder slides across
        for s, side in SIDES:
            P.put(f"{side}Shoulder", Dg @ rig.R[f"{side}Shoulder"], P.child_head(f"{side}Shoulder", "UpperChest"))
        return P

    def torso_frame(self, P):
        d = P.posed["Chest"] @ self.rig.R["Chest"].inverted()
        return d @ Vector((1, 0, 0)), d @ Vector((0, -1, 0)), d @ Vector((0, 0, 1))

    def torso_r(self, P, q):
        """How deep q is in the torso: 1 on its skin, 0 on the spine, large outside; 9 above the neck's base and below the hips."""
        lat, fwd, up = self.torso_frame(P)
        v = q - P.heads["Hips"]
        t = v.dot(up)
        if not (0.02 < t < self.neck_z): return 9.0
        return math.sqrt((v.dot(lat) / self.torso[0]) ** 2 + (v.dot(fwd) / self.torso[1]) ** 2)

    # ---- the legs
    def legs(self, P, p):
        """Planted feet; the ankle goes where the stance says (raised by the heel lift), the knee forward and a little out."""
        rig = self.rig
        st = p["stance"]
        for s, side in SIDES:
            lead = s == "L"
            yaw = p["lead_flare"] if lead else -p["trail_flare"]
            ank = Vector((st["ax"], st["py"] + (-1 if lead else 1) * st["half"], rig.h[f"{side}Foot"].z))
            lift = p["lead_heel"] if lead else p["trail_heel"]
            ank.z += lift
            if not lead: ank += Vector((0, 0, 0))
            Dfoot = Rz(yaw - 90.0)
            Rfoot = Dfoot @ rig.R[f"{side}Foot"]
            Rtoes = Dfoot @ rig.R[f"{side}Toes"]
            toe_len = (rig.h[f"{side}Toes"] - rig.h[f"{side}Foot"]).length
            pitch = math.asin(min(0.95, lift / 0.12)) if lift > 1e-4 else 0.0
            if pitch > 1e-4:
                lat = Dfoot @ Vector((-1, 0, 0)) if False else Vector((0, 0, 1)).cross(Dfoot @ (rig.h[f"{side}Toes"] - rig.h[f"{side}Foot"]).normalized())
                for sg in (1, -1):
                    cand = Matrix.Rotation(sg * pitch, 3, lat) @ Rfoot
                    yv = cand @ rig.R[f"{side}Foot"].inverted() @ (rig.h[f"{side}Toes"] - rig.h[f"{side}Foot"])
                    if sg == 1: best = (yv.z, cand)
                    elif yv.z < best[0]: best = (yv.z, cand)
                Rfoot = best[1]
            hipj = P.child_head(f"{side}UpperLeg", "Hips")
            fwd = (Rz(yaw * 0.6) @ FWD) + Vector((0, (-1 if lead else 1) * 0.12, 0))
            knee, ank2 = two_bone(hipj, hipj + fwd, ank, *self.leglen[s])
            P.leg_miss = getattr(P, "leg_miss", {}); P.leg_miss[s] = (ank2 - ank).length
            zf = fwd
            P.put(f"{side}UpperLeg", frame(knee - hipj, zf), hipj)
            P.put(f"{side}LowerLeg", frame(ank2 - knee, zf), knee)
            P.put(f"{side}Foot", Rfoot, ank2)
            P.put(f"{side}Toes", Rtoes)
        return P


    # ---- the club and the hands
    def heading(self, p):
        """The chest's turning frame: the world's axes turned with the shoulders about the vertical. Heading-frame coordinates are (right, forward, up)."""
        return Rz(-p["shoulder_turn"])

    @staticmethod
    def rfu(v):
        """(right, forward, up) as a world vector at no turn."""
        return Vector((-v[1], v[0], v[2]))

    @staticmethod
    def to_rfu(w):
        return Vector((w.y, -w.x, w.z))

    def shoulder_centre(self, P):
        return (P.child_head("LeftUpperArm", "LeftShoulder") + P.child_head("RightUpperArm", "RightShoulder")) / 2

    GRIP_LEAD, GRIP_TRAIL = 0.085, 0.175        # the grip centres, below the butt along the shaft (m)
    WRIST_Y, WRIST_Z = 0.070, 0.030             # from the wrist to the shaft: along the fingers, and toward the palm side

    def hand_frames(self, R, gamma_l, gamma_t):
        """The two hands' frames on the club (world 3x3 columns of his rig's hand bones) and the fan angle's finger rotation."""
        s_h = -(R @ Vector((0, 0, 1)))
        yp = R @ Vector((0, 1, 0))                 # the face's normal less its loft
        out = {}
        for s, gam, pz in (("L", gamma_l, -yp), ("R", gamma_t, yp)):
            u1 = s_h; u2 = pz.cross(s_h)
            c, sn = math.cos(gam), math.sin(gam)
            if s == "L":
                px = -c * u1 - sn * u2; py = -c * u2 + sn * u1
                out[s] = (cols(px, py, pz), px, py, pz)
            else:
                px = -c * u1 + sn * u2; py = c * u2 + sn * u1
                out[s] = (cols(-px, py, pz), px, py, pz)
        return out

    def arms(self, P, p, R, origin, geo, club):
        """Both arms from the shoulders to the wrists, the hands on the grip. Returns the wrists' reach shortfalls and the fan angles."""
        rig = self.rig
        top = geo.top
        G = {"L": origin + R @ Vector((0, 0, top - self.GRIP_LEAD)), "R": origin + R @ Vector((0, 0, top - self.GRIP_TRAIL))}
        lat, fwd_c, up_c = self.torso_frame(P)
        miss = {}
        gam = {}
        for s, side in SIDES:
            Sp = P.child_head(f"{side}UpperArm", f"{side}Shoulder")
            l1, l2 = self.armlen[s]
            hint_dir = (-up_c * 1.0 + (lat if s == "L" else -lat) * p["elbow_out"] + fwd_c * p["elbow_fwd"])
            best = None
            for deg in range(24, 67, 6):
                g = math.radians(deg)
                fr = self.hand_frames(R, g if s == "L" else 0.0, g if s == "R" else 0.0)[s]
                _m, px, py, pz = fr
                W = G[s] - py * self.WRIST_Y - pz * self.WRIST_Z
                E, W2 = two_bone(Sp, Sp + hint_dir, W, l1, l2, lambda q: self.torso_r(P, q))
                f = (W2 - E).normalized()
                dev = math.degrees(math.acos(max(-1.0, min(1.0, f.dot(py)))))
                score = dev + 0.15 * abs(deg - 50) + 40.0 * (W2 - W).length
                if best is None or score < best[0]: best = (score, g, fr, W, W2, E)
            score, g, fr, W, W2, E = best
            gam[s] = g
            if s == "R" and "LeftLowerArm" in P.heads:
                # the trail arm keeps clear of the lead arm (the forearms cross through the ball and the arms wrap round each other in the finish): the elbow turns about the shoulder-wrist
                # line, as little as it takes to leave the two arms their thickness apart (the grip's last 12 cm of the forearms are where the hands meet, and do not count)
                def shr(a_, b_): return a_, a_ + (b_ - a_).normalized() * max(0.0, (b_ - a_).length - 0.12)
                Ls = [(P.child_head("LeftUpperArm", "LeftShoulder"), P.heads["LeftLowerArm"]), shr(P.heads["LeftLowerArm"], P.heads["LeftHand"])]
                axis = (W2 - Sp).normalized()
                def clear(E_):
                    Rs = [(Sp, E_), shr(E_, W2)]
                    return min(seg_dist(*a_, *b_) for a_ in Ls for b_ in Rs)
                bestc = None
                for deg in (0, 6, -6, 12, -12, 18, -18, 24, -24, 32, -32):
                    E_ = Sp + rodrigues(E - Sp, axis, math.radians(deg))
                    cost = 4000.0 * max(0.0, 0.115 - clear(E_)) ** 2 + 2.0e-4 * deg * deg
                    if bestc is None or cost < bestc[0] - 1e-12: bestc = (cost, E_)
                E = bestc[1]
            # the bone's bend direction: away from where the elbow stands out of the shoulder-wrist line; as the arm straightens that offset (and its direction) is unsteady, so
            # it gives way to the hint's own direction, never the twist of a bone flipping from one frame to the next
            line = (W2 - Sp).normalized()
            o = perp(E - Sp, line)
            zh = -perp(hint_dir, line)
            zh = zh.normalized() if zh.length > 1e-6 else Vector((0, -1, 0))
            if o.length > 1e-6:
                w = smooth(o.length / 0.03)
                zo = -o.normalized()
                zb = (zo * w + zh * (1.0 - w)) if zo.dot(zh) > 0 else (zo if w > 0.5 else zh)
            else:
                zb = zh
            if not hasattr(P, "dbg"): P.dbg = {}
            P.dbg[s] = dict(hint=hint_dir, line=line, o=o, zh=zh, zb=zb)
            # The hand is turned about the forearm by the grip (the palms face each other across the shaft), a long way from where the forearm's own frame has it: 60-160 degrees in a
            # swing. All of it at the wrist would wring the skin there; so the forearm turns with the hand, most of the way, and the upper arm a little: the rest is spread over the
            # shoulder, the elbow and the wrist (each hand's twist is kept to one continuous range, so a bone never turns the long way round between two frames).
            Ru, Rl = frame(E - Sp, zb), frame(W2 - E, zb)
            yl = (W2 - E).normalized(); yu = (E - Sp).normalized()
            tw = self.twist_between(Rl @ Vector((0, 0, 1)), fr[0] @ Vector((0, 0, 1)), yl) - self.wrist_rest[s]
            P.dbg[s + "_tw_raw"] = tw
            ref = getattr(self, "twref", None)
            if ref is not None:
                tw += 360.0 * round((ref[s] - tw) / 360.0)        # (the turn of this swing nearest the one the swing's own sequence has here)
            sm = getattr(self, "twsm", None)
            tws = sm[s] if sm is not None else tw         # (the bones turn with the hand's turn smoothed over a few frames: a forearm does not whip with the club)
            P.put(f"{side}UpperArm", Matrix.Rotation(math.radians(0.25 * tws), 3, yu) @ Ru, Sp)
            P.put(f"{side}LowerArm", Matrix.Rotation(math.radians(0.55 * tws), 3, yl) @ Rl, E)
            P.put(f"{side}Hand", fr[0], W2)
            miss[s] = (W2 - W).length
        return miss, gam

    # ---- one frame, and a whole swing
    def params(self, cv, t):
        p = {k: c(t) for k, c in cv["curves"].items()}
        p["stance"] = cv["stance"]
        return p

    def club_keys(self, cv, club, keys):
        """The club's curves, in the chest's turning frame. Each key is a world description at its time: the shaft's direction (head -> hands is -s_up; given as `s_h`, from the hands to
        the head) and the face's direction `face`; and where the lead hand is, either `at_ball` (the head sits at the ball: the hands follow) or `hands` = (right, forward, up) from
        the mid-shoulder point in units of his reach, in the chest's own turning frame. Returns (g curve, orientation curve) in that frame."""
        geo = self.geos[club]
        gk, rk = [], []
        done = {}
        s_up_prev = R_prev = None
        for k in keys:
            p = self.params(cv, k["t"])
            if "like" in k:
                # the same hold as an earlier key's, in the chest's own frame (a putt, a chip: the club stays where the arms and chest have it), moved by `dhands` and turned by `droll` about the shaft
                g0, Rc0 = done[k["like"]]
                g = g0 + Vector(k.get("dhands", (0, 0, 0)))
                Rc = Rc0
                if k.get("droll"): Rc = Rc0 @ Matrix.Rotation(math.radians(k["droll"]), 3, 'Z')
                gk.append((k["t"], g)); rk.append((k["t"], Rc)); done[k["t"]] = (g, Rc)
                continue
            P = self.pelvis_height_fit(p, club)
            S = self.shoulder_centre(P)
            Rch = self.heading(p)
            s_up = -k["s_h"].normalized()
            if k["face"] is None:
                R = s_up_prev.rotation_difference(s_up).to_matrix() @ R_prev
            else:
                R = club_frame(s_up, k["face"])
            s_up_prev, R_prev = s_up, R
            if "at_ball" in k:
                ball = k["at_ball"]
                o = place_club(geo, R, ball, k.get("sole", CLUB_SOLE_Z[club]))
                GL = o + R @ Vector((0, 0, geo.top - self.GRIP_LEAD))
                g = (Rch.inverted() @ (GL - S)) / self.reach
                gk.append((k["t"], self.to_rfu(g)))
            else:
                gk.append((k["t"], Vector(k["hands"])))
            rk.append((k["t"], Rch.inverted() @ R))
            done[k["t"]] = (gk[-1][1], rk[-1][1])
        return VecCurve(gk), QuatCurve(rk)

    def pelvis_height_fit(self, p, club):
        """The body for p, with the pelvis no higher than his legs let it be (a planted foot stays planted: the pelvis comes up only as far as the straightened lead leg allows)."""
        st = p["stance"]
        for _ in range(3):
            P = self.body(p, club)
            worst = 0.0
            for s, side in SIDES:
                lead = s == "L"
                ank = Vector((st["ax"], st["py"] + (-1 if lead else 1) * st["half"], self.rig.h[f"{side}Foot"].z + (p["lead_heel"] if lead else p["trail_heel"])))
                hipj = P.child_head(f"{side}UpperLeg", "Hips")
                worst = max(worst, (hipj - ank).length - 0.997 * sum(self.leglen[s]))
            if worst <= 0.0005: break
            p["lift"] -= worst * 1.05
        return P

    def frame_at(self, cv, ck, club, t):
        geo = self.geos[club]
        p = self.params(cv, t)
        P = self.pelvis_height_fit(p, club)
        S = self.shoulder_centre(P)
        Rch = self.heading(p)
        g, Rc = ck
        GL = S + Rch @ self.rfu(g(t) * self.reach)
        R = Rch @ Rc(t)
        origin = GL - R @ Vector((0, 0, geo.top - self.GRIP_LEAD))
        miss, gam = self.arms(P, p, R, origin, geo, club)
        self.legs(P, p)
        M = Matrix.Translation(origin) @ R.to_4x4() @ Matrix.Diagonal(Vector((1, 1, geo.k, 1)))
        return P, dict(M=M, R=R, origin=origin, miss=miss, gamma=gam, p=p, head=origin + R @ geo.tip, S=S, leg_miss=getattr(P, 'leg_miss', {}), low=origin.z + min((R @ v).z for v in geo.verts))

    # ---- the address, found from the club
    def address_curves(self, v, club):
        """The body's parameters at address for a posture v = (bend, pelvis x, pelvis z, ankle x)."""
        beta, px, pz, ax, side = v
        c = dict(hip_turn=0.0, shoulder_turn=0.0, head_turn=0.0, pelvis_bend=beta - 4, spine_bend=beta, chest_bend=beta + 1, neck_bend=beta + 6, head_bend=beta + 14,
                 hip_side=side * 0.25, spine_side=side * 0.7, chest_side=side, slide=0.0, push=0.0, lift=0.0, lead_flare=18.0, trail_flare=8.0, lead_heel=0.0, trail_heel=0.0,
                 elbow_out=0.5, elbow_fwd=0.0)
        return c, dict(px=px, py=0.0, pz=pz, ax=ax, half=0.0)

    def address_solve(self, club, shaft_lie, ball, want, tpl, half, ball_fwd):
        """Posture at address from the club: how far each arm is stretched (distance from the shoulder to the wrist, of his reach) must come out `want` = (lead, trail), with the back
        bent and the pelvis and feet placed as a golfer's are (`tpl` = the posture a golfer of this club starts from). A pattern search over (bend, pelvis x, pelvis height, ankle x)."""
        geo = self.geos[club]
        lie = math.radians(shaft_lie)
        key = dict(t=0, s_h=Vector((-math.cos(lie), 0, -math.sin(lie))), face=Vector((0, -1, 0)), at_ball=ball)
        yc = ball_fwd           # (the stance's centre: the ball is `ball_fwd` toward the target from it, and the ball is at y = 0)

        def build(v):
            c, st = self.address_curves(v, club)
            st["py"] = yc; st["half"] = half
            cv = {"curves": {k: Curve([(0.0, val)]) for k, val in c.items()}, "stance": st}
            return cv

        def cost(v):
            cv = build(v)
            ck = self.club_keys(cv, club, [key])
            P, info = self.frame_at(cv, ck, club, 0.0)
            err = 0.0
            for s, side, w in (("L", "Left", want[0]), ("R", "Right", want[1])):
                Sp = P.child_head(f"{side}UpperArm", f"{side}Shoulder")
                err += ((P.heads[f"{side}Hand"] - Sp).length / self.reach - w) ** 2 * 100.0 + info["miss"][s] ** 2 * 4000.0
            # a golfer's knees are bent 20-35 degrees
            for s, side in SIDES:
                hipj = P.child_head(f"{side}UpperLeg", "Hips")
                ank = Vector((v[3], yc + (-1 if s == "L" else 1) * half, self.rig.h[f"{side}Foot"].z))
                d = (hipj - ank).length
                l1, l2 = self.leglen[s]
                knee = 180.0 - math.degrees(math.acos(max(-1, min(1, (l1 * l1 + l2 * l2 - d * d) / (2 * l1 * l2)))))
                err += max(0.0, knee - 36.0) ** 2 * 0.02 + max(0.0, 18.0 - knee) ** 2 * 0.02
            # the hips behind the ankles, a little
            err += max(0.0, 0.04 - (v[1] - v[3])) ** 2 * 30 + max(0.0, (v[1] - v[3]) - 0.2) ** 2 * 30
            # stay near what a golfer does
            err += sum(((a - b) / sc) ** 2 for a, b, sc in zip(v, list(tpl) + [13.0], (14.0, 0.14, 0.10, 0.20, 5.0))) * 0.02
            err += max(0.0, v[4] - 17.0) ** 2 * 0.2 + max(0.0, 7.0 - v[4]) ** 2 * 0.2
            err += max(0.0, MIN_BEND[club] - v[0]) ** 2 * 0.5 + max(0.0, v[3] - 0.17) ** 2 * 80.0
            return err

        v = list(tpl) + [13.0]
        steps = [4.0, 0.03, 0.015, 0.03, 3.0]
        best = cost(v)
        for _ in range(60):
            moved = False
            for i in range(5):
                for sg in (1, -1):
                    w = list(v); w[i] += sg * steps[i]
                    c = cost(w)
                    if c < best - 1e-9: v, best, moved = w, c, True
            if not moved:
                steps = [s * 0.5 for s in steps]
                if steps[1] < 0.002: break
        cv = build(v)
        return v, cv, best


# ======================================================================= the swings
STANCE_TPL = {"Driver": (32, 0.27, 0.86, 0.17), "Iron": (36, 0.18, 0.855, 0.08), "Wedge": (38, 0.15, 0.85, 0.05), "Putter": (46, 0.06, 0.80, -0.06)}      # (bend, pelvis x, pelvis z, ankle x)
ARMS_WANT = {"Driver": (0.90, 0.92), "Iron": (0.96, 0.95), "Wedge": (0.95, 0.92), "Putter": (0.95, 0.92)}       # how far each arm is stretched at address, of his reach
STANCE_HALF = {"Driver": 0.23, "Iron": 0.19, "Wedge": 0.17, "Putter": 0.15}       # half the ankles' distance
MIN_BEND = {"Driver": 30.0, "Iron": 33.0, "Wedge": 36.0, "Putter": 44.0}       # the least a golfer bends from the hips for the club
BALL_FWD = {"Driver": 0.05, "Iron": 0.0, "Wedge": -0.02, "Putter": 0.0}        # how far the ball is toward the target from the stance's centre


def rodrigues(v, axis, ang):
    axis = axis.normalized(); c, s = math.cos(ang), math.sin(ang)
    return v * c + axis.cross(v) * s + axis * (axis.dot(v)) * (1 - c)


def plane_dir(lie_deg, theta, off=0.0):
    """The shaft's direction from the hands to the head when the club has swung `theta` degrees about the swing plane (the plane through the ball that holds the target line and the
    address shaft): 0 at address, 90 parallel to the ground behind the ball, 180 up the plane, 270 parallel again, pointing at the target; negative through the ball."""
    lie = math.radians(lie_deg); th = math.radians(theta)
    u1 = Vector((-math.cos(lie), 0, -math.sin(lie)))
    n_p = Vector((math.cos(lie), 0, math.sin(lie))).cross(Vector((0, 1, 0))).normalized()
    return (u1 * math.cos(th) + Vector((0, 1, 0)) * math.sin(th) + n_p * off).normalized()


def key(t, s_h, face, roll=0.0, **kw):
    """A club key: shaft direction from the hands to the head, the face's direction (turned `roll` degrees about the shaft), and `hands` or `at_ball`."""
    s_h = s_h.normalized()
    if face is None:        # the face goes wherever the shortest turn from the last key's club takes it (no turn about the shaft of its own)
        d = dict(t=t, s_h=s_h, face=None)
    else:
        f0 = perp(face, s_h).normalized()
        d = dict(t=t, s_h=s_h, face=rodrigues(f0, s_h, math.radians(roll)))
    d.update(kw)
    return d


def impact_zone(club, ball, lie, IMP, lean=0.17):
    """Keys through the ball: the club head moves along the target line on the arc of its swing (a circle of its own radius about the golfer's shoulders), the shaft's lean going from
    the head behind the hands (before the ball) to the head ahead of them (after it), the face closing a little. s is authored time (frames): one key a frame either side and two."""
    R_arc = 1.45 + 0.55 * (club == "Driver")
    ks = []
    for dt, u in ((-2, -0.45), (-1, -0.22), (0, 0.0), (1, 0.22), (2, 0.45)):
        pt = ball + Vector((0, -u, u * u / (2 * R_arc)))
        s_h = Vector((-math.cos(lie), lean - 0.8 * u, -math.sin(lie)))
        ks.append(key(IMP + dt, s_h, Vector((0, -1, 0)), 35.0 * u / 0.45, at_ball=pt, sole=CLUB_SOLE_Z[club] + u * u / (2 * R_arc)))
    return ks


class Swing:
    """One clip's worth of authored swing: the body's curves, the club's keys, the clip's landmark frames, and how to pose any frame. The poses are authored on a time of their own (s);
    the clip's frame t is posed at s = sched(t): that is how the club head is made to speed up into the ball (see `retime`)."""
    def __init__(self, golfer, club, cv, ck, ball, top, impact, end, finish=None):
        self.golfer, self.club, self.cv, self.ck, self.ball = golfer, club, cv, ck, ball
        self.top, self.impact, self.end = top, impact, end
        self.finish = finish if finish is not None else end
        self.sched = lambda t: t

    def worst_miss(self, s0, s1):
        """How far, at most, a hand falls short of the grip anywhere from authored time s0 to s1."""
        return max(max(self.pose_s(s0 + (s1 - s0) * i / 12.0)[1]["miss"].values()) for i in range(13))

    def twist_reference(self, step=0.25):
        """How far each hand is turned about its forearm all through the swing, as one continuous sequence (a turn is only known modulo 360 degrees, and a bone must not take the long way
        round between two frames): the swing is posed once at a fine step, and the turns unwrapped."""
        g = self.golfer
        g.twref = None; g.twsm = None
        ss = [i * step for i in range(0, int(self.end / step) + 1)]
        raw = {"L": [], "R": []}
        for x in ss:
            P, _info = self.golfer.frame_at(self.cv, self.ck, self.club, x)
            for k in raw: raw[k].append(P.dbg[k + "_tw_raw"])
        ref, smo = {}, {}
        w = int(round(1.5 / step))
        for k, vals in raw.items():
            out = [vals[0]]
            for v in vals[1:]: out.append(v + 360.0 * round((out[-1] - v) / 360.0))
            ref[k] = Curve(list(zip(ss, out)))
            sm = [sum(out[max(0, i - w):i + w + 1]) / len(out[max(0, i - w):i + w + 1]) for i in range(len(out))]
            smo[k] = Curve(list(zip(ss, sm)))
        self._twref, self._twsm = ref, smo

    def pose_s(self, s):
        if getattr(self, "_twref", None) is None: self.twist_reference()
        self.golfer.twref = {k: c(s) for k, c in self._twref.items()}
        self.golfer.twsm = {k: c(s) for k, c in self._twsm.items()}
        try: return self.golfer.frame_at(self.cv, self.ck, self.club, s)
        finally: self.golfer.twref = None; self.golfer.twsm = None

    def pose(self, t):
        return self.pose_s(self.sched(t))

    def retime(self, p_in=2.2, p_out_min=1.2, step=0.125):
        """A swing's club head speeds up all the way into the ball (its speed peaks AT impact) and slows into the finish; the head's path from the top to impact is travelled at a speed
        that rises as tau**(p_in - 1), tau the share of the downswing's time, and from impact to the finish at the speed it had, easing off (the exponent after the ball is chosen so
        the speed is continuous at impact). The clip's top, impact and finish frames stay where they are; only the poses between them are sampled at other times."""
        T0, T1, T2 = self.top, self.impact, self.finish
        n = int((T2 - T0) / step) + 1
        ss = [T0 + i * step for i in range(n)]
        H = [self.pose_s(x)[1]["head"] for x in ss]
        A = [0.0]
        for i in range(1, n): A.append(A[-1] + (H[i] - H[i - 1]).length)
        i1 = int(round((T1 - T0) / step))
        A1, A2 = A[i1], A[-1] - A[i1]
        D1, D2 = T1 - T0, T2 - T1
        if A1 < 1e-4 or A2 < 1e-4 or D2 < 2: return self
        p_out = max(p_out_min, min(3.0, p_in * (A1 / D1) * (D2 / A2)))
        import bisect
        def s_of(arc):
            k = bisect.bisect_left(A, arc)
            k = max(1, min(n - 1, k))
            span = A[k] - A[k - 1]
            return ss[k - 1] + (ss[k] - ss[k - 1]) * ((arc - A[k - 1]) / span if span > 1e-9 else 0.0)
        def sched(t):
            if t <= T0: return t
            if t <= T1: return s_of(A1 * ((t - T0) / D1) ** p_in)
            if t < T2: return s_of(A1 + A2 * (1.0 - (1.0 - (t - T1) / D2) ** p_out))
            return t
        self.sched = sched
        self.retimed = (A1, A2, p_in, p_out)
        return self


FIN_HANDS = (-0.40, 0.30, 0.62)       # the finish: the hands high, beside the lead ear (right, forward, up of the mid-shoulder, in his reach, in the chest's frame)


def full_swing(golfer, club, top=54, impact=66, finish=86, end=100, imp_bend=None, imp_side=8.0, imp_turn=-6.0, retime=True):
    """The full swing (a driver, an iron): address, takeaway, the top, the first move down, impact, follow-through, finish. Times are frames at 30 per second.
    `imp_bend` is how much further than at address he is bent forward at impact (a golfer stays in his posture and the club pulls the arms out): when not given, as little as lets both
    arms reach the grip."""
    if imp_bend is None:
        for ib in [2.0 + 1.5 * i for i in range(0, 10)]:
            sw = full_swing(golfer, club, top, impact, finish, end, ib, imp_side, imp_turn, False)
            if sw.worst_miss(impact - 3, impact + 6) < 0.003: break
        return sw.retime(2.2) if retime else sw
    TOP, IMP, FIN, END = top, impact, finish, end
    drv = club == "Driver"
    ball = Vector((-0.75, 0, CLUB_BALL_Z[club]))
    lie_d = LIE[club]; lie = math.radians(lie_d)
    size = golfer.size
    v, cv0, _c = golfer.address_solve(club, lie_d, ball, ARMS_WANT[club], STANCE_TPL[club], STANCE_HALF[club] * size, BALL_FWD[club])
    st = cv0["stance"]
    c0 = {k: cc.v[0] for k, cc in cv0["curves"].items()}
    hipT, shT = (50, 98) if drv else (46, 92)
    K = lambda *pairs: list(pairs)
    cur = {
        "hip_turn":      K((0, 0), (19, hipT * 0.43), (37, hipT * 0.78), (TOP, hipT), (TOP + 2, hipT * 0.96), (TOP + 4, hipT * 0.74), (IMP - 4, hipT * 0.13), (IMP, -32), (IMP + 4, -55), (IMP + 9, -72), (FIN, -88), (END, -88)),
        "shoulder_turn": K((0, 0), (19, shT * 0.46), (37, shT * 0.78), (TOP, shT), (TOP + 2, shT * 0.99), (TOP + 4, shT * 0.91), (IMP - 4, shT * 0.52), (IMP, imp_turn), (IMP + 4, -45), (IMP + 9, -85), (FIN, -116), (END, -116)),
        "girdle":        K((0, 0), (19, 6), (37, 12), (TOP, 14), (IMP - 4, 8), (IMP, 0), (IMP + 4, -4), (FIN, 0), (END, 0)),
        "head_turn":     K((0, 0), (19, 12), (37, 22), (TOP, 28), (IMP - 4, 14), (IMP, 0), (IMP + 4, -8), (IMP + 9, -30), (FIN, -60), (END, -62)),
        "pelvis_bend":   K((0, c0["pelvis_bend"]), (TOP, c0["pelvis_bend"]), (IMP, c0["pelvis_bend"] + imp_bend + 1), (IMP + 9, c0["pelvis_bend"] - 14), (FIN, c0["pelvis_bend"] - 22), (END, c0["pelvis_bend"] - 22)),
        "spine_bend":    K((0, c0["spine_bend"]), (TOP, c0["spine_bend"]), (IMP, c0["spine_bend"] + imp_bend), (IMP + 9, c0["spine_bend"] - 16), (FIN, c0["spine_bend"] - 24), (END, c0["spine_bend"] - 24)),
        "chest_bend":    K((0, c0["chest_bend"]), (TOP, c0["chest_bend"]), (IMP, c0["chest_bend"] + imp_bend), (IMP + 9, c0["chest_bend"] - 17), (FIN, c0["chest_bend"] - 25), (END, c0["chest_bend"] - 25)),
        "neck_bend":     K((0, c0["neck_bend"]), (IMP, c0["neck_bend"]), (FIN, c0["neck_bend"] - 22), (END, c0["neck_bend"] - 22)),
        "head_bend":     K((0, c0["head_bend"]), (IMP, c0["head_bend"]), (FIN, c0["head_bend"] - 30), (END, c0["head_bend"] - 30)),
        "hip_side":      K((0, 3), (TOP, 6), (IMP, 8), (IMP + 9, 6), (FIN, 2), (END, 2)),
        "spine_side":    K((0, c0["spine_side"]), (TOP, c0["spine_side"] + 3), (IMP, c0["spine_side"] + 7), (IMP + 9, c0["spine_side"] + 4), (FIN, c0["spine_side"] - 2), (END, c0["spine_side"] - 2)),
        "chest_side":    K((0, c0["chest_side"]), (TOP, c0["chest_side"] + 4), (IMP, c0["chest_side"] + imp_side), (IMP + 9, c0["chest_side"] + 4), (FIN, c0["chest_side"] - 2), (END, c0["chest_side"] - 2)),
        "slide":         K((0, 0), (19, -0.03 * size), (37, -0.06 * size), (TOP, -0.07 * size), (TOP + 4, -0.045 * size), (IMP - 4, 0.0), (IMP, 0.035 * size), (IMP + 9, 0.12 * size), (FIN, 0.17 * size), (END, 0.18 * size)),
        "push":          K((0, 0), (TOP, -0.01 * size), (IMP, 0.0), (FIN, -0.07 * size), (END, -0.08 * size)),
        "lift":          K((0, 0), (TOP, 0.0), (IMP - 4, 0.01 * size), (IMP, 0.02 * size), (IMP + 9, 0.06 * size), (FIN, 0.08 * size), (END, 0.08 * size)),
        "lead_flare":    K((0, 18), (IMP, 18), (FIN, 38), (END, 38)), "trail_flare": K((0, 8), (IMP, 8), (FIN, -26), (END, -26)), "lead_heel": K((0, 0), (END, 0)),
        "trail_heel":    K((0, 0), (IMP - 4, 0), (IMP, 0.02), (IMP + 4, 0.06), (IMP + 9, 0.09), (FIN, 0.10), (END, 0.10)),
        "elbow_out":     K((0, 0.5), (END, 0.5)), "elbow_fwd": K((0, 0.0), (END, 0.0)),
    }
    cv = {"curves": curves(cur), "stance": st}
    ks = [
        key(0, Vector((-math.cos(lie), 0, -math.sin(lie))), Vector((0, -1, 0)), at_ball=ball),
        key(10, plane_dir(lie_d, 45), Vector((-0.5, -0.8, 0)), 14, hands=(0.14, 0.50, -0.62)),
        key(19, Vector((0.0, 0.99, 0.10)), Vector((-1, 0, 0.05)), 28, hands=(0.42, 0.65, -0.39)),
        key(28, plane_dir(lie_d, 128), Vector((-1, 0, 0.1)), 20, hands=(0.45, 0.62, -0.12)),
        key(37, plane_dir(lie_d, 165), Vector((-1, 0, 0.2)), 10, hands=(0.38, 0.58, 0.12)),
        key(46, plane_dir(lie_d, 225), Vector((-1, 0, 0.4)), 0, hands=(0.30, 0.52, 0.40)),
        key(TOP, Vector((0.065, -0.97, 0.2)), Vector((-0.7, 0, 0.7)), 0, hands=(0.24, 0.50, 0.52)),
        key(TOP + 4, plane_dir(lie_d, 205), Vector((-0.7, 0, 0.7)), 20, hands=(0.40, 0.50, 0.30)),
        key(IMP - 4, Vector((0.15, 0.9, 0.25)), Vector((-1, 0, 0.4)), 40, hands=(0.46, 0.58, -0.45)),
    ] + impact_zone(club, ball, lie, IMP) + [
        key(IMP + 4, Vector((0.2, -0.9, 0.4)), None, hands=(-0.14, 0.80, -0.10)),
        key(IMP + 9, Vector((0.2, -0.3, 0.93)), None, hands=(-0.25, 0.70, 0.20)),
        key(FIN, Vector((-0.9, -0.3, -0.3)), None, hands=FIN_HANDS),
        key(END, Vector((-0.9, -0.3, -0.3)), None, hands=FIN_HANDS),
    ]
    ck = golfer.club_keys(cv, club, ks)
    return Swing(golfer, club, cv, ck, ball, TOP, IMP, END, FIN)


def _base(golfer, club):
    ball = Vector((-0.75, 0, CLUB_BALL_Z[club]))
    lie_d = LIE[club]
    v, cv0, _c = golfer.address_solve(club, lie_d, ball, ARMS_WANT[club], STANCE_TPL[club], STANCE_HALF[club] * golfer.size, BALL_FWD[club])
    c0 = {k: cc.v[0] for k, cc in cv0["curves"].items()}
    return ball, lie_d, math.radians(lie_d), cv0["stance"], c0


def _flat(c0, END, **over):
    """Curves that hold their address values (the parts of the body a short swing leaves alone), with `over` replacing any."""
    names = ["hip_turn", "shoulder_turn", "head_turn", "girdle", "pelvis_bend", "spine_bend", "chest_bend", "neck_bend", "head_bend", "hip_side", "spine_side", "chest_side", "slide", "push", "lift",
             "lead_flare", "trail_flare", "lead_heel", "trail_heel", "elbow_out", "elbow_fwd"]
    cur = {}
    for n in names:
        v = c0.get(n, 0.0)
        cur[n] = [(0, v), (END, v)]
    cur["girdle"] = [(0, 0.0), (END, 0.0)]
    cur["elbow_out"] = [(0, 0.5), (END, 0.5)]
    cur["elbow_fwd"] = [(0, 0.0), (END, 0.0)]
    cur.update(over)
    return cur


def half_swing(golfer, top=44, impact=54, finish=72, end=86, imp_bend=None, imp_side=7.0, retime=True):
    """A pitch: the backswing to the lead arm's parallel (the club vertical), the same back through, a short balanced finish. The iron's geometry."""
    club = "Iron"
    if imp_bend is None:
        for ib in [2.0 + 1.5 * i for i in range(0, 10)]:
            sw = half_swing(golfer, top, impact, finish, end, ib, imp_side, False)
            if sw.worst_miss(impact - 3, impact + 6) < 0.003: break
        return sw.retime(1.9) if retime else sw
    TOP, IMP, FIN, END = top, impact, finish, end
    ball, lie_d, lie, st, c0 = _base(golfer, club)
    size = golfer.size
    K = lambda *pairs: list(pairs)
    cur = _flat(c0, END, **{
        "hip_turn":      K((0, 0), (14, 10), (30, 20), (TOP, 28), (TOP + 2, 26), (TOP + 4, 18), (IMP - 3, 4), (IMP, -24), (IMP + 4, -42), (IMP + 8, -58), (FIN, -70), (END, -70)),
        "shoulder_turn": K((0, 0), (14, 26), (30, 46), (TOP, 58), (TOP + 2, 57), (TOP + 4, 50), (IMP - 3, 26), (IMP, -4), (IMP + 4, -34), (IMP + 8, -62), (FIN, -92), (END, -92)),
        "girdle":        K((0, 0), (TOP, 8), (IMP, 0), (END, 0)),
        "head_turn":     K((0, 0), (TOP, 16), (IMP, 0), (IMP + 8, -14), (FIN, -45), (END, -47)),
        "pelvis_bend":   K((0, c0["pelvis_bend"]), (TOP, c0["pelvis_bend"]), (IMP, c0["pelvis_bend"] + imp_bend + 1), (IMP + 8, c0["pelvis_bend"] - 8), (FIN, c0["pelvis_bend"] - 14), (END, c0["pelvis_bend"] - 14)),
        "spine_bend":    K((0, c0["spine_bend"]), (TOP, c0["spine_bend"]), (IMP, c0["spine_bend"] + imp_bend), (IMP + 8, c0["spine_bend"] - 10), (FIN, c0["spine_bend"] - 16), (END, c0["spine_bend"] - 16)),
        "chest_bend":    K((0, c0["chest_bend"]), (TOP, c0["chest_bend"]), (IMP, c0["chest_bend"] + imp_bend), (IMP + 8, c0["chest_bend"] - 11), (FIN, c0["chest_bend"] - 17), (END, c0["chest_bend"] - 17)),
        "neck_bend":     K((0, c0["neck_bend"]), (IMP, c0["neck_bend"]), (FIN, c0["neck_bend"] - 14), (END, c0["neck_bend"] - 14)),
        "head_bend":     K((0, c0["head_bend"]), (IMP, c0["head_bend"]), (FIN, c0["head_bend"] - 20), (END, c0["head_bend"] - 20)),
        "hip_side":      K((0, c0["hip_side"]), (IMP, c0["hip_side"] + 4), (END, c0["hip_side"] + 1)),
        "spine_side":    K((0, c0["spine_side"]), (TOP, c0["spine_side"] + 2), (IMP, c0["spine_side"] + imp_side * 0.8), (END, c0["spine_side"])),
        "chest_side":    K((0, c0["chest_side"]), (TOP, c0["chest_side"] + 3), (IMP, c0["chest_side"] + imp_side), (END, c0["chest_side"])),
        "slide":         K((0, 0), (TOP, -0.04 * size), (IMP - 3, 0.0), (IMP, 0.03 * size), (IMP + 8, 0.08 * size), (FIN, 0.11 * size), (END, 0.12 * size)),
        "push":          K((0, 0), (IMP, 0.0), (FIN, -0.04 * size), (END, -0.04 * size)),
        "lift":          K((0, 0), (IMP, 0.015 * size), (FIN, 0.05 * size), (END, 0.05 * size)),
        "trail_heel":    K((0, 0), (IMP - 3, 0), (IMP, 0.015), (IMP + 4, 0.05), (FIN, 0.08), (END, 0.08)),
        "lead_flare":    K((0, 18), (IMP, 18), (FIN, 34), (END, 34)), "trail_flare": K((0, 8), (IMP, 8), (FIN, -20), (END, -20)),
    })
    cv = {"curves": curves(cur), "stance": st}
    ks = [
        key(0, Vector((-math.cos(lie), 0, -math.sin(lie))), Vector((0, -1, 0)), at_ball=ball),
        key(14, plane_dir(lie_d, 70), Vector((-1, 0, 0.05)), 20, hands=(0.30, 0.60, -0.45)),
        key(30, plane_dir(lie_d, 128), Vector((-1, 0, 0.1)), 18, hands=(0.38, 0.60, -0.15)),
        key(TOP, plane_dir(lie_d, 160), Vector((-1, 0, 0.2)), 10, hands=(0.38, 0.58, 0.10)),
        key(TOP + 4, plane_dir(lie_d, 140), Vector((-1, 0, 0.3)), 20, hands=(0.40, 0.55, -0.10)),
        key(IMP - 4, Vector((0.15, 0.85, 0.35)), Vector((-1, 0, 0.4)), 40, hands=(0.44, 0.58, -0.40)),
    ] + impact_zone(club, ball, lie, IMP, 0.14) + [
        key(IMP + 4, Vector((0.2, -0.9, 0.4)), None, hands=(-0.12, 0.78, -0.12)),
        key(IMP + 8, Vector((0.25, -0.5, 0.85)), None, hands=(-0.22, 0.70, 0.10)),
        key(FIN, Vector((0.45, -0.2, 0.87)), None, hands=(-0.26, 0.62, 0.22)),
        key(END, Vector((0.45, -0.2, 0.87)), None, hands=(-0.26, 0.62, 0.22)),
    ]
    ck = golfer.club_keys(cv, club, ks)
    return Swing(golfer, club, cv, ck, ball, TOP, IMP, END, FIN)


def chip(golfer, top=30, impact=42, finish=62, end=76, imp_bend=None):
    """A chip: the arms and chest rock as one, a little wrist; the wedge."""
    club = "Wedge"
    if imp_bend is None:
        for ib in [0.0 + 1.5 * i for i in range(0, 10)]:
            sw = chip(golfer, top, impact, finish, end, ib)
            if max(max(sw.pose(t)[1]["miss"].values()) for t in range(top, min(end, impact + 8))) < 0.004: break
        return sw
    TOP, IMP, FIN, END = top, impact, finish, end
    ball, lie_d, lie, st, c0 = _base(golfer, club)
    size = golfer.size
    K = lambda *pairs: list(pairs)
    cur = _flat(c0, END, **{
        "hip_turn":      K((0, 0), (TOP, 6), (TOP + 2, 5), (IMP - 3, 0), (IMP, -8), (IMP + 6, -18), (FIN, -26), (END, -28)),
        "shoulder_turn": K((0, 0), (TOP, 26), (TOP + 2, 25), (IMP - 3, 10), (IMP, -4), (IMP + 6, -26), (FIN, -40), (END, -42)),
        "head_turn":     K((0, 0), (TOP, 6), (IMP, 0), (FIN, -24), (END, -26)),
        "slide":         K((0, 0), (TOP, -0.01 * size), (IMP, 0.02 * size), (FIN, 0.05 * size), (END, 0.05 * size)),
        "lift":          K((0, 0), (IMP, 0.0), (FIN, 0.02 * size), (END, 0.02 * size)),
        "trail_heel":    K((0, 0), (IMP, 0.0), (FIN, 0.03), (END, 0.03)),
        "pelvis_bend":   K((0, c0["pelvis_bend"]), (TOP, c0["pelvis_bend"] + imp_bend * 0.5), (IMP, c0["pelvis_bend"] + imp_bend + 1), (FIN, c0["pelvis_bend"] - 5), (END, c0["pelvis_bend"] - 5)),
        "spine_bend":    K((0, c0["spine_bend"]), (TOP, c0["spine_bend"] + imp_bend * 0.5), (IMP, c0["spine_bend"] + imp_bend + 1), (FIN, c0["spine_bend"] - 6), (END, c0["spine_bend"] - 6)),
        "chest_bend":    K((0, c0["chest_bend"]), (TOP, c0["chest_bend"] + imp_bend * 0.5), (IMP, c0["chest_bend"] + imp_bend + 1), (FIN, c0["chest_bend"] - 6), (END, c0["chest_bend"] - 6)),
    })
    cv = {"curves": curves(cur), "stance": st}
    ks = [
        key(0, Vector((-math.cos(lie), 0, -math.sin(lie))), Vector((0, -1, 0)), at_ball=ball),
        key(TOP, Vector((0, 0, 0)), Vector((0, 0, 0)), like=0, dhands=(0.0, 0.0, 0.07)),
        key(IMP, Vector((-math.cos(lie) * 0.96, 0.22, -math.sin(lie) * 0.96)), Vector((0, -1, 0)), at_ball=ball),
        key(FIN, Vector((0, 0, 0)), Vector((0, 0, 0)), like=0, dhands=(-0.12, 0.04, 0.12)),
        key(END, Vector((0, 0, 0)), Vector((0, 0, 0)), like=0, dhands=(-0.12, 0.04, 0.12)),
    ]
    ck = golfer.club_keys(cv, club, ks)
    return Swing(golfer, club, cv, ck, ball, TOP, IMP, END, FIN)


def putt(golfer, top=30, impact=42, finish=58, end=72, imp_bend=None):
    """A putt: the shoulders rock, the arms and the putter go with them."""
    club = "Putter"
    if imp_bend is None:
        for ib in [0.0 + 1.0 * i for i in range(0, 10)]:
            sw = putt(golfer, top, impact, finish, end, ib)
            if max(max(sw.pose(t)[1]["miss"].values()) for t in range(top, min(end, impact + 8))) < 0.004: break
        return sw
    TOP, IMP, FIN, END = top, impact, finish, end
    ball, lie_d, lie, st, c0 = _base(golfer, club)
    K = lambda *pairs: list(pairs)
    cur = _flat(c0, END, **{
        "pelvis_bend":   K((0, c0["pelvis_bend"]), (TOP, c0["pelvis_bend"] + imp_bend * 0.5), (IMP, c0["pelvis_bend"] + imp_bend), (FIN, c0["pelvis_bend"] + imp_bend * 0.5), (END, c0["pelvis_bend"] + imp_bend * 0.5)),
        "spine_bend":    K((0, c0["spine_bend"]), (TOP, c0["spine_bend"] + imp_bend * 0.5), (IMP, c0["spine_bend"] + imp_bend), (FIN, c0["spine_bend"] + imp_bend * 0.5), (END, c0["spine_bend"] + imp_bend * 0.5)),
        "chest_bend":    K((0, c0["chest_bend"]), (TOP, c0["chest_bend"] + imp_bend * 0.5), (IMP, c0["chest_bend"] + imp_bend), (FIN, c0["chest_bend"] + imp_bend * 0.5), (END, c0["chest_bend"] + imp_bend * 0.5)),
        "hip_turn":      K((0, 0), (TOP, 2), (IMP, -2), (FIN, -5), (END, -5)),
        "shoulder_turn": K((0, 0), (TOP, 13), (TOP + 2, 13), (IMP - 3, 5), (IMP, 0), (IMP + 6, -10), (FIN, -15), (END, -15)),
        "head_turn":     K((0, 0), (END, 0)),
    })
    cv = {"curves": curves(cur), "stance": st}
    ks = [
        key(0, Vector((-math.cos(lie), 0, -math.sin(lie))), Vector((0, -1, 0)), at_ball=ball),
        key(TOP, Vector((0, 0, 0)), Vector((0, 0, 0)), like=0),
        key(IMP, Vector((-math.cos(lie) * 0.99, 0.10, -math.sin(lie) * 0.99)), Vector((0, -1, 0)), at_ball=ball),
        key(FIN, Vector((0, 0, 0)), Vector((0, 0, 0)), like=IMP),
        key(END, Vector((0, 0, 0)), Vector((0, 0, 0)), like=IMP),
    ]
    ck = golfer.club_keys(cv, club, ks)
    return Swing(golfer, club, cv, ck, ball, TOP, IMP, END, FIN)

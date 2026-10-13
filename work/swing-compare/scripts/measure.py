"""Body measurements of our in-game swing from SwingFilm's bones.csv (positions relative to the stance centre c).
    python3 scripts/measure.py [male_Driver]   -> prints a table, writes measurements_<tag>.txt"""
import csv, math, sys, os
W = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
tag = sys.argv[1] if len(sys.argv) > 1 else "male_Driver"
meta = open(f"{W}/frames/{tag}/frame.txt").read().split()
vec = lambda s: tuple(map(float, s.split("=")[1].split(";")))
face, aim = vec(meta[1]), vec(meta[2])
rows = list(csv.DictReader(open(f"{W}/frames/{tag}/bones.csv")))
def P(f, b):
    x = tuple(map(float, rows[f][b].split(";")))
    return (x[0] * face[0] + x[2] * face[2], x[1], x[0] * aim[0] + x[2] * aim[2])   # (toward ball, up, toward target)
sub = lambda a, b: tuple(i - j for i, j in zip(a, b))
def ang(u, v):
    d = sum(i * j for i, j in zip(u, v)) / (math.hypot(*u) * math.hypot(*v)); return math.degrees(math.acos(max(-1, min(1, d))))
def yaw(f, l, r):   # horizontal direction of the line r->l, degrees from the target line (+ = turned away from the target, i.e. back)
    v = sub(P(f, l), P(f, r)); return math.degrees(math.atan2(-v[0], v[2]))
def tilt(f):        # spine forward bend: Hips->Neck from vertical, in the ball/up plane
    v = sub(P(f, "Neck"), P(f, "Hips")); return math.degrees(math.atan2(v[0], v[1]))
def side(f):        # spine side bend: + = away from the target
    v = sub(P(f, "Neck"), P(f, "Hips")); return math.degrees(math.atan2(-v[2], v[1]))
def flex(f, a, b, c): return 180 - ang(sub(P(f, a), P(f, b)), sub(P(f, c), P(f, b)))
F = {"address": 30, "top": 80, "impact": 103, "settled": 140, "finish": 164}
A = F["address"]
lines = [f"{tag}: frames " + ", ".join(f"{k} f{v}" for k, v in F.items())]
def row(name, fn, unit="deg", fmt="{:6.1f}"):
    lines.append(f"{name:<38}" + "".join(f"{fmt.format(fn(f)):>10}" for f in F.values()) + f"  {unit}")
lines.append(f"{'':<38}" + "".join(f"{k:>10}" for k in F))
row("shoulder turn (from address)", lambda f: yaw(f, "LeftUpperArm", "RightUpperArm") - yaw(A, "LeftUpperArm", "RightUpperArm"))
row("hip turn (from address)", lambda f: yaw(f, "LeftUpperLeg", "RightUpperLeg") - yaw(A, "LeftUpperLeg", "RightUpperLeg"))
row("X-factor (shoulders - hips)", lambda f: (yaw(f, "LeftUpperArm", "RightUpperArm") - yaw(A, "LeftUpperArm", "RightUpperArm")) - (yaw(f, "LeftUpperLeg", "RightUpperLeg") - yaw(A, "LeftUpperLeg", "RightUpperLeg")))
row("spine forward bend", tilt)
row("spine side bend (+ away from target)", side)
row("lead (left) knee flex", lambda f: flex(f, "LeftUpperLeg", "LeftLowerLeg", "LeftFoot"))
row("trail (right) knee flex", lambda f: flex(f, "RightUpperLeg", "RightLowerLeg", "RightFoot"))
row("lead elbow bend", lambda f: flex(f, "LeftUpperArm", "LeftLowerArm", "LeftHand"))
row("trail elbow bend", lambda f: flex(f, "RightUpperArm", "RightLowerArm", "RightHand"))
row("head height change", lambda f: (P(f, "Head")[1] - P(A, "Head")[1]) * 100, "cm")
row("head move toward target", lambda f: (P(f, "Head")[2] - P(A, "Head")[2]) * 100, "cm")
row("head move toward ball", lambda f: (P(f, "Head")[0] - P(A, "Head")[0]) * 100, "cm")
row("pelvis move toward target", lambda f: (P(f, "Hips")[2] - P(A, "Hips")[2]) * 100, "cm")
row("pelvis move toward ball (early ext.)", lambda f: (P(f, "Hips")[0] - P(A, "Hips")[0]) * 100, "cm")
row("pelvis height change", lambda f: (P(f, "Hips")[1] - P(A, "Hips")[1]) * 100, "cm")
row("lead hand height", lambda f: P(f, "LeftHand")[1] * 100, "cm")
row("hands above head (lead hand - head)", lambda f: (P(f, "LeftHand")[1] - P(f, "Head")[1]) * 100, "cm")
row("hands from head (3D, width)", lambda f: math.dist(P(f, "LeftHand"), P(f, "Head")) * 100, "cm")
row("trail heel/foot height", lambda f: P(f, "RightFoot")[1] * 100, "cm")
row("lead foot height", lambda f: P(f, "LeftFoot")[1] * 100, "cm")
row("feet apart (along target line)", lambda f: (P(f, "LeftFoot")[2] - P(f, "RightFoot")[2]) * 100, "cm")
out = "\n".join(lines); print(out)
open(f"{W}/measurements_{tag}.txt", "w").write(out + "\n")

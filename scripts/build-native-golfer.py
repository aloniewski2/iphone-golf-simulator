"""Convert the owned Sunward mesh and sampled performances to one connected USD rig.

Requires free usd-core + numpy. Regenerate the two input exports with the opt-in
NativeArchitectureTests authoring test first. No downloaded/generated art is used.
All source performances are embedded in the USD animation timeline; the manifest
provides named clip ranges, avoiding importer-dependent multiple-animation support.
"""
import argparse
import json
from pathlib import Path

import numpy as np
from pxr import Gf, Sdf, Usd, UsdGeom, UsdShade, UsdSkel, UsdUtils, Vt


REPO = Path(__file__).resolve().parents[1]
SCALE = 0.292608
BALL = np.array([3.2, 0.0, 0.0])  # stance translates laterally; ground remains y=0
# Parent-before-child joint order. Existing bone-space corrections are preserved;
# only hierarchy and the explicit rig-units/metres boundary change.
PARENTS = {
    "root": None, "link_10": "root", "link_0": "link_10",
    "link_11": "link_0", "link_1": "link_0", "head": "link_1",
    "poloDetails": "link_0", "collar": "link_1",
    "link_2": "link_11", "link_3": "link_2", "leftHand": "link_3",
    "link_4": "link_11", "link_5": "link_4", "rightHand": "link_5",
    "link_6": "link_10", "link_7": "link_6", "leftFoot": "link_7",
    "link_8": "link_10", "link_9": "link_8", "rightFoot": "link_9",
    "shaft": "leftHand", "shaftTip": "leftHand", "handle": "leftHand", "clubHead": "leftHand",
}
NAMES = list(PARENTS)
PATHS = {}
for key, parent in PARENTS.items():
    PATHS[key] = f"{PATHS[parent]}/{key}" if parent else key


def matrix(values, preserve_scale=False):
    # Swift writes columns; Gf uses row-vector matrices, so reshape is intentional.
    value = np.array(values, dtype=float).reshape(4, 4)
    # Geometry already carries the authored accessory proportions. Export rigid
    # joint frames: nonuniform SCN link scales otherwise introduce shear when
    # reparented, which USD's TRS skeletal animation cannot represent.
    if not preserve_scale:
        u, _, vt = np.linalg.svd(value[:3, :3])
        value[:3, :3] = u @ vt
    value[3, :3] = (value[3, :3] - BALL) * SCALE
    return Gf.Matrix4d(*value.flatten().tolist())


def worlds(snapshot):
    result = {key: Gf.Matrix4d(1) if key == "root" else matrix(snapshot[key])
              for key in NAMES if key != "shaftTip"}
    # IK produces rigid joint transforms. Skin the shaft between rigid endpoints
    # instead of relying on nonuniform joint scale that the solver discards.
    tip = Gf.Matrix4d(result["shaft"])
    tip.SetTranslateOnly(result["clubHead"].ExtractTranslation())
    result["shaftTip"] = tip
    return result


def locals_for(world):
    return [world[key] * world[parent].GetInverse() if parent else world[key]
            for key, parent in PARENTS.items()]


def material(stage, name, color, roughness=0.8, metallic=0):
    path = "/Golfer/Materials/" + name
    result = UsdShade.Material.Define(stage, path)
    shader = UsdShade.Shader.Define(stage, path + "/PBR")
    shader.CreateIdAttr("UsdPreviewSurface")
    shader.CreateInput("diffuseColor", Sdf.ValueTypeNames.Color3f).Set(Gf.Vec3f(*color[:3]))
    shader.CreateInput("roughness", Sdf.ValueTypeNames.Float).Set(float(roughness))
    shader.CreateInput("metallic", Sdf.ValueTypeNames.Float).Set(float(metallic))
    if name.startswith("visor_"):
        shader.CreateInput("opacity", Sdf.ValueTypeNames.Float).Set(0.0)
    shader.CreateOutput("surface", Sdf.ValueTypeNames.Token)
    result.CreateSurfaceOutput().ConnectToSource(shader.ConnectableAPI(), "surface")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=REPO / "art/Native/export")
    parser.add_argument("--output", type=Path, default=REPO / "GolfArcade/Resources/Native")
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    authored = json.loads((REPO / "GolfArcade/Resources/Sunward/SunwardGolfer.golfmesh").read_text())
    motion = json.loads((args.source / "GolferAnimationSource.json").read_text())
    stage_path = args.source / "NativeGolfer.usdc"
    stage = Usd.Stage.CreateNew(str(stage_path))
    root = UsdSkel.Root.Define(stage, "/Golfer")
    stage.SetDefaultPrim(root.GetPrim())
    UsdGeom.SetStageMetersPerUnit(stage, 1)
    UsdGeom.SetStageUpAxis(stage, UsdGeom.Tokens.y)
    stage.SetTimeCodesPerSecond(60)
    stage.SetFramesPerSecond(60)
    skeleton = UsdSkel.Skeleton.Define(stage, "/Golfer/Skeleton")
    joints = Vt.TokenArray([PATHS[x] for x in NAMES])
    skeleton.CreateJointsAttr(joints)
    rest = worlds(motion["bind"])
    skeleton.CreateBindTransformsAttr(Vt.Matrix4dArray([rest[x] for x in NAMES]))
    # The skin bind remains the editable T-pose, but the IK reference pose must
    # use a playable address stance (bent elbows/knees), not straight T-pose
    # chains at their singularity. USD explicitly separates rest from bind.
    address = worlds(next(c for c in motion["clips"] if c["name"] == "iron")["frames"][0]["transforms"])
    skeleton.CreateRestTransformsAttr(Vt.Matrix4dArray(locals_for(address)))
    root_motion = UsdGeom.Xformable(root).AddTranslateOp(opSuffix="rootMotion")
    root_motion.Set(Gf.Vec3d(0))

    def animation_worlds(snapshot):
        result = worlds(snapshot)
        offset = result["link_10"].ExtractTranslation() - address["link_10"].ExtractTranslation()
        # Keep bone offsets fixed for IK. Authored sway/jumps are root-entity
        # motion, not animated translations buried inside the limb hierarchy.
        for key in NAMES[1:]:
            result[key].SetTranslateOnly(result[key].ExtractTranslation() - offset)
        return result, offset
    bind = UsdSkel.BindingAPI.Apply(root.GetPrim())
    bind.CreateSkeletonRel().SetTargets([skeleton.GetPath()])

    mats = [material(stage, x["name"], x["color"], x["roughness"], x["metalness"])
            for x in authored["materials"]]
    points, normals, uvs, faces, indices, weights, sections = [], [], [], [], [], [], []

    def append_mesh(p, n, uv, triangles, bone_indices, bone_weights, mat):
        offset = len(points)
        start = len(faces) // 3
        points.extend(p); normals.extend(n); uvs.extend(uv)
        faces.extend([int(x) + offset for x in triangles])
        indices.extend(bone_indices); weights.extend(bone_weights)
        sections.append((start, len(triangles) // 3, mat))

    for mesh in authored["meshes"]:
        p = (np.array(mesh["positions"]).reshape(-1, 3) - BALL) * SCALE
        ji = [NAMES.index("link_" + str(authored["boneLinks"][x])) for x in mesh["joints"]]
        append_mesh(p, np.array(mesh["normals"]).reshape(-1, 3),
                    np.array(mesh["uv"]).reshape(-1, 2), mesh["indices"], ji,
                    mesh["weights"], mats[mesh["material"]])

    accessory_meshes = []
    sources = [("GolferAccessories.usdz", False, None), ("GolferVisorAccessories.usdz", True, None)]
    sources += [(f"GolferClub-{kind}.usdz", False, kind) for kind in ("driver", "iron", "putter")]
    for filename, visor_only, club_kind in sources:
        accessory_stage = Usd.Stage.Open(str(args.source / filename))
        for prim in accessory_stage.Traverse():
            if prim.HasRelationship("material:binding"):
                UsdShade.MaterialBindingAPI.Apply(prim)
            if visor_only and not any(part in ("golfCap", "visorHairCrown") for part in str(prim.GetPath()).split("/")):
                continue
            is_head = "clubHead" in str(prim.GetPath()).split("/")
            if (club_kind is not None) != is_head:
                continue
            if prim.IsA(UsdGeom.Mesh):
                accessory_meshes.append((accessory_stage, prim, club_kind))
    cache = UsdGeom.XformCache(0)
    for accessory_stage, prim, club_kind in accessory_meshes:
        if not prim.IsA(UsdGeom.Mesh):
            continue
        mesh = UsdGeom.Mesh(prim)
        source_points = mesh.GetPointsAttr().Get(0)
        if not source_points:
            continue
        # Every exported accessory belongs to exactly one authored rig node.
        owner = next((part for part in str(prim.GetPath()).split("/") if part in PARENTS), None)
        if not owner:
            raise ValueError(f"Unbound accessory: {prim.GetPath()}")
        transform = cache.GetLocalToWorldTransform(prim)
        p = [(np.array(transform.Transform(Gf.Vec3d(x))) - BALL) * SCALE for x in source_points]
        if owner in ("shaft", "handle"):
            source_frame = matrix(motion["bind"][owner], preserve_scale=True)
            local_points = np.array([source_frame.GetInverse().Transform(Gf.Vec3d(*point)) for point in p])
            if abs(local_points[:, 1].min()) > 0.001 or abs(local_points[:, 1].max() - SCALE) > 0.001:
                raise ValueError(f"Unbaked club pivot leaves a disconnected {owner}: {prim.GetPath()}")
        source_normals = mesh.GetNormalsAttr().Get(0)
        normal_matrix = transform.GetInverse().GetTranspose()
        n = [normal_matrix.TransformDir(Gf.Vec3d(x)).GetNormalized() for x in source_normals]
        count = len(p)
        if len(n) != count:
            raise ValueError(f"Non-vertex normals require explicit expansion: {prim.GetPath()}")
        pv = UsdGeom.PrimvarsAPI(prim).GetPrimvar("st")
        uv = pv.ComputeFlattened(0) if pv else [Gf.Vec2f(0)] * count
        if len(uv) != count:
            uv = [Gf.Vec2f(0)] * count
        face_counts = mesh.GetFaceVertexCountsAttr().Get(0)
        face_indices = mesh.GetFaceVertexIndicesAttr().Get(0)
        if any(x != 3 for x in face_counts):
            raise ValueError(f"Nontriangular accessory: {prim.GetPath()}")
        bound, _ = UsdShade.MaterialBindingAPI(prim).ComputeBoundMaterial()
        shader = bound.ComputeSurfaceSource()[0] if bound else None
        def value(key, fallback):
            entry = shader.GetInput(key) if shader else None
            return entry.Get(0) if entry and entry.Get(0) is not None else fallback
        color = value("diffuseColor", Gf.Vec3f(0.9))
        # Keep explicit tint roles for all editable appearance fields.
        role = "accessory_" + str(len(sections))
        source_role = next((part.removeprefix("nativeMaterial_").split("_part_")[0]
                            for part in str(prim.GetPath()).split("/") if part.startswith("nativeMaterial_")), "")
        for candidate in ["cap_ivory", "cap_shirt", "visor_ivory", "visor_hair", "shirt", "skin", "hair", "ivory", "trousers"]:
            if source_role == candidate or source_role.startswith(candidate + "_"):
                role = candidate
                break
        if owner in ("shaft", "handle", "clubHead"):
            role = "club_" + (club_kind + "_" if club_kind else "") + role
        mat = material(stage, role, color, value("roughness", 0.75), value("metallic", 0))
        ji = [v for _ in p for v in [NAMES.index(owner), 0, 0, 0]]
        jw = [v for _ in p for v in [1.0, 0.0, 0.0, 0.0]]
        if owner in ("shaft", "handle"):
            grip = np.array(rest["shaft"].ExtractTranslation())
            direction = np.array(rest["shaftTip"].ExtractTranslation()) - grip
            amounts = [float(np.clip(np.dot(point - grip, direction) / np.dot(direction, direction), 0, 1)) for point in p]
            ji = [value for _ in p for value in [NAMES.index("shaft"), NAMES.index("shaftTip"), 0, 0]]
            jw = [value for amount in amounts for value in [1 - amount, amount, 0.0, 0.0]]
        append_mesh(p, n, uv, face_indices, ji, jw, mat)

    skin = UsdGeom.Mesh.Define(stage, "/Golfer/GolferSkin")
    skin.CreatePointsAttr(Vt.Vec3fArray([Gf.Vec3f(*map(float, p)) for p in points]))
    skin.CreateNormalsAttr(Vt.Vec3fArray([Gf.Vec3f(*map(float, p)) for p in normals]))
    skin.SetNormalsInterpolation(UsdGeom.Tokens.vertex)
    skin.CreateFaceVertexCountsAttr([3] * (len(faces) // 3))
    skin.CreateFaceVertexIndicesAttr(faces)
    skin.CreateSubdivisionSchemeAttr(UsdGeom.Tokens.none)
    skin.CreateExtentAttr(UsdGeom.PointBased.ComputeExtent(skin.GetPointsAttr().Get()))
    UsdGeom.PrimvarsAPI(skin).CreatePrimvar("st", Sdf.ValueTypeNames.TexCoord2fArray,
        UsdGeom.Tokens.vertex).Set(Vt.Vec2fArray([Gf.Vec2f(*map(float, x)) for x in uvs]))
    skin_bind = UsdSkel.BindingAPI.Apply(skin.GetPrim())
    skin_bind.CreateSkeletonRel().SetTargets([skeleton.GetPath()])
    skin_bind.CreateGeomBindTransformAttr(Gf.Matrix4d(1))
    skin_bind.CreateJointIndicesPrimvar(False, 4).Set(indices)
    skin_bind.CreateJointWeightsPrimvar(False, 4).Set(weights)
    for i, (start, count, mat) in enumerate(sections):
        subset = UsdGeom.Subset.CreateGeomSubset(skin, "material_" + str(i), UsdGeom.Tokens.face,
            list(range(start, start + count)), "materialBind", UsdGeom.Tokens.partition)
        UsdShade.MaterialBindingAPI.Apply(subset.GetPrim()).Bind(mat)

    animation = UsdSkel.Animation.Define(stage, "/Golfer/Performances")
    animation.CreateJointsAttr(joints)
    translations = animation.CreateTranslationsAttr()
    rotations = animation.CreateRotationsAttr()
    scales = animation.CreateScalesAttr()
    UsdSkel.BindingAPI.Apply(skeleton.GetPrim()).CreateAnimationSourceRel().SetTargets([animation.GetPath()])
    clips = {}
    cursor = 0
    max_shear_error = 0.0
    for clip in motion["clips"]:
        name = {"reaction-holed": "celebration", "knockdown": "recovery"}.get(clip["name"], clip["name"])
        contacts = []
        for frame in clip["frames"]:
            item = {"time": frame["time"]}
            item["clubGrip"] = ((np.array(frame["grip"]) - BALL) * SCALE).tolist()
            item["clubHead"] = ((np.array(frame["head"]) - BALL) * SCALE).tolist()
            item["clubDropped"] = bool(frame.get("clubDropped", False))
            frame_worlds, _ = animation_worlds(frame["transforms"])
            for joint in ("leftHand", "rightHand", "leftFoot", "rightFoot"):
                transform = frame_worlds[joint]
                item[joint] = list(transform.ExtractTranslation())
                if joint.endswith("Hand"):
                    rotation = transform.ExtractRotationQuat()
                    item[joint + "Rotation"] = list(rotation.GetImaginary()) + [rotation.GetReal()]
            contacts.append(item)
        clips[name] = {"start": cursor / 60, "duration": clip["duration"], "impact": clip["impact"], "contacts": contacts}
        for frame in clip["frames"]:
            frame_worlds, motion_offset = animation_worlds(frame["transforms"])
            # The authored shaft endpoint must reach the head in every sampled
            # pose, including length changes between address and follow-through.
            tip = frame_worlds["shaftTip"].ExtractTranslation()
            if (tip - frame_worlds["clubHead"].ExtractTranslation()).GetLength() > 0.001:
                raise ValueError(f"Detached club head: {name} at {frame['time']}")
            local = locals_for(frame_worlds)
            ts, rs, ss = [], [], []
            for m in local:
                t = Gf.Transform(m)
                ts.append(Gf.Vec3f(t.GetTranslation()))
                rs.append(Gf.Quatf(t.GetRotation().GetQuat()))
                ss.append(Gf.Vec3h(t.GetScale()))
                reconstructed = Gf.Matrix4d().SetScale(t.GetScale()) * Gf.Matrix4d().SetRotate(t.GetRotation())
                reconstructed.SetTranslateOnly(t.GetTranslation())
                max_shear_error = max(max_shear_error, float(np.max(np.abs(np.array(m) - np.array(reconstructed)))))
            time = cursor + frame["time"] * 60
            translations.Set(Vt.Vec3fArray(ts), time)
            rotations.Set(Vt.QuatfArray(rs), time)
            scales.Set(Vt.Vec3hArray(ss), time)
            root_motion.Set(motion_offset, time)
        # Hold the final authored pose across the importer's resampling window.
        # Without a plateau, a non-integral final frame blends toward the next
        # clip's address pose even while seeking inside the current clip.
        hold = cursor + int(np.ceil(clip["duration"] * 60)) + 6
        translations.Set(Vt.Vec3fArray(ts), hold)
        rotations.Set(Vt.QuatfArray(rs), hold)
        scales.Set(Vt.Vec3hArray(ss), hold)
        root_motion.Set(motion_offset, hold)
        cursor = hold + 6
    stage.SetStartTimeCode(0)
    if max_shear_error > 1e-5:
        raise ValueError(f"Animation cannot be represented by rigid/TRS joints: {max_shear_error}")
    stage.SetEndTimeCode(cursor - 2)
    stage.GetRootLayer().customLayerData = {"source": "Owned Sunward editable mesh and authored motion", "clipRanges": json.dumps(clips)}
    stage.GetRootLayer().Save()
    output = args.output / "NativeGolfer.usdz"
    if not UsdUtils.CreateNewUsdzPackage(Sdf.AssetPath(str(stage_path)), str(output)):
        raise RuntimeError("USDZ packaging failed")
    manifest = {"file": output.name, "skeletonEntity": "Golfer", "requiredJoints": list(PATHS.values()),
        "impactMarkers": {k: v["impact"] for k, v in clips.items() if v["impact"] > 0}, "clips": clips}
    (args.source / "NativeGolfer.json").write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({"file": str(output), "vertices": len(points), "triangles": len(faces) // 3,
        "joints": len(NAMES), "clips": len(clips), "maximumTRSResidual": max_shear_error}, indent=2))


if __name__ == "__main__":
    main()

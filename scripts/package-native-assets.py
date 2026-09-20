"""Validate and stage existing converted assets; never invent missing course entries."""
import hashlib
import argparse
import json
from pathlib import Path
import shutil
import zipfile
import numpy as np
from pxr import Gf, Sdf, Usd, UsdGeom, UsdShade, UsdUtils

REPO = Path(__file__).resolve().parents[1]
SOURCE = REPO / "art/Native/export"
OUTPUT = REPO / "GolfArcade/Resources/Native"
OUTPUT.mkdir(parents=True, exist_ok=True)
entries, provenance = [], []
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--only", help="Validate/optimize one completed filename without publishing the manifest")
parser.add_argument("--golfer-only", action="store_true", help="Publish a regenerated golfer while retaining validated course entries")
args = parser.parse_args()
if args.golfer_only:
    if args.only: parser.error("--golfer-only cannot be combined with --only")
    manifest_file = OUTPUT / "GolfNativeAssets.json"
    manifest = json.loads(manifest_file.read_text())
    golfer = json.loads((SOURCE / "NativeGolfer.json").read_text())
    if not (OUTPUT / golfer["file"]).is_file(): raise ValueError("Missing regenerated golfer")
    manifest["golfer"] = golfer
    manifest_file.write_text(json.dumps(manifest, indent=2) + "\n")
    print(json.dumps({"updatedGolfer": golfer["file"], "preservedCourses": len(manifest["holes"])}))
    raise SystemExit(0)


def compact_mesh(prim):
    """Remove unused vertices from split material meshes, preserving every corner."""
    points = prim.GetAttribute("points").Get()
    indices = prim.GetAttribute("faceVertexIndices").Get()
    if not points or not indices:
        return 0
    used = sorted(set(indices))
    if len(used) == len(points):
        return 0
    count = len(points)
    remap = {old: new for new, old in enumerate(used)}
    def compact(attr):
        value = attr.Get()
        if value is None or len(value) != count:
            raise ValueError(f"Invalid vertex attribute {attr.GetPath()}")
        attr.Set(type(value)([value[index] for index in used]))
    compact(prim.GetAttribute("points"))
    normals = prim.GetAttribute("normals")
    if normals and normals.Get() and UsdGeom.Mesh(prim).GetNormalsInterpolation() in ("vertex", "varying"):
        compact(normals)
    for variable in UsdGeom.PrimvarsAPI(prim).GetPrimvars():
        if variable.GetInterpolation() in ("vertex", "varying"):
            compact(variable.GetIndicesAttr() if variable.IsIndexed() else variable.GetAttr())
    indices = type(indices)([remap[index] for index in indices])
    prim.GetAttribute("faceVertexIndices").Set(indices)
    UsdGeom.Mesh(prim).CreateExtentAttr(UsdGeom.PointBased.ComputeExtent(prim.GetAttribute("points").Get()))
    return count - len(used)


def verify_corners(original, converted):
    """Exact triangle-corner equivalence, allowing harmless vertex renumbering."""
    if original.GetAttribute("faceVertexCounts").Get(0) != converted.GetAttribute("faceVertexCounts").Get(0):
        raise ValueError(f"Optimization changed topology: {original.GetPath()}")
    a_index = np.array(original.GetAttribute("faceVertexIndices").Get(0), dtype=int)
    b_index = np.array(converted.GetAttribute("faceVertexIndices").Get(0), dtype=int)
    for field in ("points", "normals"):
        a, b = original.GetAttribute(field).Get(0), converted.GetAttribute(field).Get(0)
        if a is None or b is None:
            if a != b: raise ValueError(f"Lost {field}: {original.GetPath()}")
            continue
        vertex = field == "points" or UsdGeom.Mesh(original).GetNormalsInterpolation() in ("vertex", "varying")
        left, right = (np.array(a)[a_index], np.array(b)[b_index]) if vertex else (np.array(a), np.array(b))
        if not np.array_equal(left, right):
            raise ValueError(f"Optimization changed {field}: {original.GetPath()}")
    for variable in UsdGeom.PrimvarsAPI(original).GetPrimvars():
        other = UsdGeom.PrimvarsAPI(converted).GetPrimvar(variable.GetPrimvarName())
        if not other or variable.GetInterpolation() != other.GetInterpolation():
            raise ValueError(f"Lost primvar {variable.GetAttr().GetPath()}")
        a, b = variable.ComputeFlattened(0), other.ComputeFlattened(0)
        if a is None and b is None: continue
        left, right = np.array(a), np.array(b)
        if variable.GetInterpolation() in ("vertex", "varying"):
            left, right = left[a_index], right[b_index]
        if not np.array_equal(left, right):
            raise ValueError(f"Optimization changed primvar {variable.GetAttr().GetPath()}")
    if original.GetRelationship("material:binding").GetTargets() != converted.GetRelationship("material:binding").GetTargets():
        raise ValueError(f"Optimization changed material: {original.GetPath()}")


def optimize(file):
    """Share identical meshes without changing a single vertex or material."""
    directory = REPO / "art/Native/optimized" / file.stem
    directory.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(file) as package:
        for name in package.namelist():
            path = directory / name
            if not path.resolve().is_relative_to(directory.resolve()):
                raise ValueError("Unsafe package member")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(package.read(name))
        root_file = directory / package.namelist()[0]
    stage = Usd.Stage.Open(str(root_file))
    seen, duplicates = {}, 0
    for prim in list(stage.Traverse()):
        # Exports are static; remove time-zero-only samples so they cannot be
        # mistaken for animated resources by the runtime importer.
        for attr in prim.GetAttributes():
            if attr.GetTimeSamples() == [0.0]:
                value = attr.Get(0)
                attr.ClearAtTime(0)
                attr.Set(value)
        if prim.HasRelationship("material:binding"):
            UsdShade.MaterialBindingAPI.Apply(prim)
        if prim.IsA(UsdShade.Shader) and prim.GetAttribute("info:id").Get() == "UsdPreviewSurface":
            # SceneKit exports its default ambient term as emission and its
            # unset normal property as (1,1,1). Neither is a valid PBR equivalent.
            emission = prim.GetAttribute("inputs:emissiveColor")
            if emission and emission.Get() is not None and not emission.HasAuthoredConnections():
                color = emission.Get()
                if max(abs(float(x) - 0.4845292) for x in color) < 1e-5:
                    emission.Set(Gf.Vec3f(0))
            normal = prim.GetAttribute("inputs:normal")
            if normal and normal.Get() == Gf.Vec3f(1) and not normal.HasAuthoredConnections():
                normal.Set(Gf.Vec3f(0, 0, 1))
        if not prim.IsA(UsdGeom.Mesh):
            continue
        if not prim.GetAttribute("points").Get():
            prim.SetTypeName("Xform")
            continue
        compact_mesh(prim)
        digest = hashlib.sha256()
        properties = []
        for attr in prim.GetAttributes():
            if attr.GetName().startswith("xformOp") or attr.GetName() == "visibility":
                continue
            properties.append(attr.GetName())
            digest.update(attr.GetName().encode())
            value = attr.Get()
            try:
                digest.update(memoryview(value).tobytes())
            except TypeError:
                digest.update(repr(value).encode())
            digest.update(str(attr.GetMetadata("interpolation")).encode())
        for relation in prim.GetRelationships():
            digest.update(str(relation.GetTargets()).encode())
        subsets = list(prim.GetChildren())
        # Child meshes need their own transforms; don't instance compound prims.
        if any(p.GetTypeName() != "GeomSubset" for p in subsets):
            continue
        for subset in subsets:
            for attr in subset.GetAttributes():
                digest.update(attr.GetName().encode())
                value = attr.Get(0)
                try:
                    digest.update(memoryview(value).tobytes())
                except TypeError:
                    digest.update(repr(value).encode())
            for relation in subset.GetRelationships():
                digest.update(str(relation.GetTargets()).encode())
        key = digest.hexdigest()
        if key not in seen:
            seen[key] = prim.GetPath()
            continue
        # A local empty op order must also override the prototype's transform.
        order = prim.GetAttribute("xformOpOrder").Get() or []
        for name in properties:
            prim.RemoveProperty(name)
        for relation in prim.GetRelationships():
            prim.RemoveProperty(relation.GetName())
        for child in subsets:
            stage.RemovePrim(child.GetPath())
        prim.GetReferences().AddInternalReference(seen[key])
        UsdGeom.Xformable(prim).CreateXformOpOrderAttr().Set(order)
        prim.SetInstanceable(True)
        duplicates += 1
    compact = directory / "Optimized.usdc"
    stage.GetRootLayer().Export(str(compact))
    output = OUTPUT / file.name
    if not UsdUtils.CreateNewUsdzPackage(Sdf.AssetPath(str(compact)), str(output)):
        raise RuntimeError(f"Packaging failed: {file}")
    # Prove instancing preserved geometry and world placement, including props.
    original = Usd.Stage.Open(str(file))
    converted = Usd.Stage.Open(str(output))
    original_cache, converted_cache = UsdGeom.XformCache(0), UsdGeom.XformCache(0)
    for prim in original.Traverse():
        if not prim.IsA(UsdGeom.Mesh):
            continue
        if not prim.GetAttribute("points").Get(0):
            continue
        other = converted.GetPrimAtPath(prim.GetPath())
        if not other or not other.IsA(UsdGeom.Mesh):
            raise ValueError(f"Optimization lost mesh {prim.GetPath()}")
        verify_corners(prim, other)
        a, b = original_cache.GetLocalToWorldTransform(prim), converted_cache.GetLocalToWorldTransform(other)
        if max(abs(a[i][j] - b[i][j]) for i in range(4) for j in range(4)) > 1e-6:
            raise ValueError(f"Optimization moved mesh {prim.GetPath()}")
    return duplicates


for file in sorted(SOURCE.glob("Course-*.usdz")):
    if args.only and file.name != args.only:
        continue
    metadata = json.loads(file.with_suffix(".json").read_text())
    stage = Usd.Stage.Open(str(file))
    if UsdGeom.GetStageMetersPerUnit(stage) != 1:
        raise ValueError(f"Non-metric course: {file}")
    if UsdGeom.GetStageUpAxis(stage) != "Y":
        raise ValueError(f"Wrong up axis: {file}")
    surfaces = [p for p in stage.Traverse() if p.GetName() == "sharedPlayableSurface"]
    if len(surfaces) != 1:
        raise ValueError(f"Expected one authoritative playable surface: {file}")
    bindings = []
    for surface in surfaces:
        for child in surface.GetChildren():
            if child.IsA(UsdGeom.Mesh):
                bindings.append(tuple(child.GetRelationship("material:binding").GetTargets()))
    if surfaces and (len(bindings) != 8 or any(not binding for binding in bindings) or len(set(bindings)) != 8):
        raise ValueError(f"Terrain's eight material regions were lost or aliased: {file}")
    with zipfile.ZipFile(file) as package:
        # Check actual package entries, not the exporter's temporary-layer warnings.
        for prim in stage.Traverse():
            for attr in prim.GetAttributes():
                if attr.GetTypeName() == Sdf.ValueTypeNames.Asset:
                    asset = attr.Get(0)
                    if asset and asset.path and asset.path not in package.namelist():
                        raise ValueError(f"Unembedded asset: {asset.path} in {file}")
    for name in ("tee", "pin"):
        marker = next((p for p in stage.Traverse() if p.GetName() == name), None)
        if marker is None:
            raise ValueError(f"Missing {name}: {file}")
        position = UsdGeom.XformCache(0).GetLocalToWorldTransform(marker).ExtractTranslation()
        if max(abs(position[i] - metadata[name][i] * 0.9144) for i in range(3)) > 0.02:
            raise ValueError(f"Misaligned {name}: {file}")
    shared = optimize(file)
    entries.append({"courseID": metadata["courseID"], "hole": metadata["hole"], "file": file.name})
    provenance.append({"file": file.name, "sha256": hashlib.sha256(file.read_bytes()).hexdigest(),
                       "source": "Existing CourseScene and authoritative Hole metadata", "sourceBytes": file.stat().st_size,
                       "bytes": (OUTPUT / file.name).stat().st_size, "sharedMeshes": shared})

if args.only:
    print(json.dumps(provenance, indent=2))
    raise SystemExit(0)

golfer = json.loads((SOURCE / "NativeGolfer.json").read_text())
manifest = {"version": 1, "metresPerUnit": 1, "upAxis": "Y", "holes": entries, "golfer": golfer}
(OUTPUT / "GolfNativeAssets.json").write_text(json.dumps(manifest, indent=2) + "\n")
(REPO / "art/Native/conversion-provenance.json").write_text(json.dumps({
    "sourcePolicy": "Owned existing assets; no new generation or purchases",
    "courses": provenance, "golfer": {"file": golfer["file"],
    "sources": ["SunwardGolfer.golfmesh", "SunwardMotion.json", "AvatarRig.swift"],
    "converter": "scripts/build-native-golfer.py"}}, indent=2) + "\n")
print(json.dumps({"convertedHoles": len(entries), "totalCourseBytes": sum(x["bytes"] for x in provenance)}, indent=2))

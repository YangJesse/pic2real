"""Measure native Unity PAR bounds and identify the two visible ornament bands."""
import json
import math
import re
import struct
from prepare_models import PROJECT, read_glb
from validate_models import matrix, mul


def blocks(path):
    text = path.read_text(encoding="utf-8")
    return [(int(kind), int(file_id), body) for kind, file_id, body in
            re.findall(r"--- !u!(\d+) &(-?\d+)\n(.*?)(?=--- !u!|\Z)", text, re.S)]


def field_vector(body, field):
    found = re.search(r"\b"+field+r": \{(.*?)\}", body)
    return [float(v) for v in re.findall(r"[xyzw]: ([^,}]+)", found.group(1))]


def native_nodes(key):
    mesh_bounds = {}
    for kind, file_id, body in blocks(PROJECT/"Assets"/"Models"/"Generated"/(key+"_Meshes.asset")):
        if kind != 43:
            continue
        bound = body[body.index("  m_LocalAABB:"):]
        center, extent = field_vector(bound, "m_Center"), field_vector(bound, "m_Extent")
        mesh_bounds[file_id] = ([center[j]-extent[j] for j in range(3)],
                                [center[j]+extent[j] for j in range(3)])
    names, transforms, filters = {}, {}, {}
    for kind, file_id, body in blocks(PROJECT/"Assets"/"Resources"/"Models"/(key+".prefab")):
        if kind == 1:
            names[file_id] = re.search(r"  m_Name: (.*)", body).group(1)
        elif kind == 4:
            go = int(re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body).group(1))
            transforms[file_id] = dict(go=go,
                parent=int(re.search(r"m_Father: \{fileID: (-?\d+)\}", body).group(1)),
                translation=field_vector(body, "m_LocalPosition"),
                rotation=field_vector(body, "m_LocalRotation"),
                scale=field_vector(body, "m_LocalScale"))
        elif kind == 33:
            go = int(re.search(r"m_GameObject: \{fileID: (-?\d+)\}", body).group(1))
            filters[go] = int(re.search(r"m_Mesh: \{fileID: (-?\d+)", body).group(1))
    world_cache = {}
    def world(file_id):
        if file_id not in world_cache:
            node = transforms[file_id]
            world_cache[file_id] = mul(world(node["parent"]), matrix(node)) if node["parent"] else matrix(node)
        return world_cache[file_id]
    output = {}
    for file_id, node in transforms.items():
        go = node["go"]
        if go not in filters:
            continue
        lo, hi = mesh_bounds[filters[go]]
        points = []
        for mask in range(8):
            point = [hi[j] if mask & (1<<j) else lo[j] for j in range(3)]
            points.append([sum(world(file_id)[j][k]*point[k] for k in range(3))+world(file_id)[j][3] for j in range(3)])
        output[names[go]] = ([min(p[j] for p in points) for j in range(3)],
                            [max(p[j] for p in points) for j in range(3)])
    return output


def source_nodes(key):
    doc, binary = read_glb(PROJECT/"SourceModels"/(key+".glb"))
    nodes = doc["nodes"]
    parents = {child: i for i,n in enumerate(nodes) for child in n.get("children", [])}
    cache = {}
    def world(i):
        if i not in cache:
            cache[i] = mul(world(parents[i]), matrix(nodes[i])) if i in parents else matrix(nodes[i])
        return cache[i]
    output = {}
    for i, node in enumerate(nodes):
        if "mesh" not in node:
            continue
        lo, hi = [math.inf]*3, [-math.inf]*3
        for primitive in doc["meshes"][node["mesh"]]["primitives"]:
            accessor = doc["accessors"][primitive["attributes"]["POSITION"]]
            view = doc["bufferViews"][accessor["bufferView"]]
            start = view.get("byteOffset", 0)+accessor.get("byteOffset", 0)
            stride = view.get("byteStride", 12)
            for vertex in range(accessor["count"]):
                xyz = struct.unpack_from("<fff", binary, start+vertex*stride)
                p = [sum(world(i)[j][k]*xyz[k] for k in range(3))+world(i)[j][3] for j in range(3)]
                p[2] *= -1
                for j in range(3):
                    lo[j] = min(lo[j], p[j]); hi[j] = max(hi[j], p[j])
        output[node["name"]] = (lo, hi)
    return output


def pixel_y(point):
    # Unity fixed viewpoint B(0,48,1.65), aim B(0,14,6.5), vertical FOV 64.
    y, z = point[1]-1.65, point[2]-48
    size = math.hypot(4.85, -34)
    vertical = (34*y+4.85*z)/size
    forward = (4.85*y-34*z)/size
    return 450 - vertical/forward * (450/math.tan(math.radians(32)))


report = []
max_delta = 0
for key in ("PAR_initial_preview", "PAR_scene_preview"):
    original, installed = source_nodes(key), native_nodes(key)
    for name, bounds in installed.items():
        if not any(part in name for part in ("Capital", "Reward", "NewFill_N06_04_06", "Architrave_06_07", "ColumnSouth_N06")):
            continue
        delta = max(abs(bounds[side][axis]-original[name][side][axis]) for side in range(2) for axis in range(3))
        max_delta = max(max_delta, delta)
        center = [(bounds[0][j]+bounds[1][j])*.5 for j in range(3)]
        role = "cornice_reward" if "RewardCornice" in name else "frieze_reward" if "FriezeReward" in name else "relief_reward" if "RewardRelief" in name else "capital" if "Capital" in name else "shaft_or_beam"
        report.append(dict(model=key, node=name, role=role,
            native_min=[round(x,6) for x in bounds[0]], native_max=[round(x,6) for x in bounds[1]],
            source_agreement_error_metres=delta, screen_center_y_900px=round(pixel_y(center),2)))
assert max_delta < 0.0001, max_delta
(PROJECT/"Tools"/"par_geometry_diagnostics.json").write_text(json.dumps(dict(
    conclusion="Upper eight ornaments are delivered FriezeReward and RewardCornice meshes, not duplicate capitals. Lower shaft/capital/architrave contacts are exact at Y11.1 and Y11.7. Static N06-N13 capital suffixes belong to south/short side columns and must remain visible.",
    maximum_native_source_error_metres=max_delta, nodes=report), ensure_ascii=False, indent=2), encoding="utf-8")
for item in report:
    if item["model"] == "PAR_scene_preview" and any(term in item["node"] for term in ("N06", "Reward_06", "Cornice_06", "Architrave_06_07")):
        print(item["node"], item["native_min"], item["native_max"], "pixel_y", item["screen_center_y_900px"])
print("Maximum native/source delta", max_delta, "metres")

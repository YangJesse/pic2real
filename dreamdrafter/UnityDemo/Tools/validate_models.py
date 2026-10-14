"""Validate prepared GLB data against the original delivered models."""
from __future__ import annotations
import hashlib
import json
import math
import pathlib
import struct
from prepare_models import PROJECT, model_root, source_from_manifest, read_glb

def accessor_bytes(doc, binary, accessor):
    view = doc["bufferViews"][accessor["bufferView"]]
    size = {5120: 1, 5121: 1, 5122: 2, 5123: 2, 5125: 4, 5126: 4}[accessor["componentType"]]
    count = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}[accessor["type"]]
    width = size * count
    stride = view.get("byteStride", width)
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    end = start + max(0, accessor["count"] - 1) * stride + width
    assert end <= len(binary)
    return b"".join(binary[start+i*stride:start+i*stride+width] for i in range(accessor["count"]))

def mul(a, b):
    return [[sum(a[i][k]*b[k][j] for k in range(4)) for j in range(4)] for i in range(4)]

def matrix(node):
    if "matrix" in node:
        return [[node["matrix"][j*4+i] for j in range(4)] for i in range(4)]
    x, y, z, w = node.get("rotation", [0, 0, 0, 1])
    scale = node.get("scale", [1, 1, 1])
    p = node.get("translation", [0, 0, 0])
    r = [[1-2*y*y-2*z*z, 2*x*y-2*z*w, 2*x*z+2*y*w, 0],
         [2*x*y+2*z*w, 1-2*x*x-2*z*z, 2*y*z-2*x*w, 0],
         [2*x*z-2*y*w, 2*y*z+2*x*w, 1-2*x*x-2*y*y, 0], [0, 0, 0, 1]]
    for i in range(3):
        for j in range(3):
            r[i][j] *= scale[j]
        r[i][3] = p[i]
    return r

def bounds(doc):
    nodes = doc["nodes"]
    parents = {child: i for i, n in enumerate(nodes) for child in n.get("children", [])}
    cache = {}
    def world(i):
        if i not in cache:
            cache[i] = mul(world(parents[i]), matrix(nodes[i])) if i in parents else matrix(nodes[i])
        return cache[i]
    low = [math.inf]*3
    high = [-math.inf]*3
    triangles = 0
    for i, n in enumerate(nodes):
        if "mesh" not in n:
            continue
        transform = world(i)
        for primitive in doc["meshes"][n["mesh"]]["primitives"]:
            acc = doc["accessors"][primitive["attributes"]["POSITION"]]
            tri_acc = doc["accessors"][primitive["indices"]] if "indices" in primitive else acc
            triangles += tri_acc["count"] // 3
            for mask in range(8):
                corner = [acc["max" if mask & (1 << j) else "min"][j] for j in range(3)] + [1]
                v = [sum(transform[j][k]*corner[k] for k in range(4)) for j in range(3)]
                # GLTF -> Unity uses Z reflection. Units remain metres.
                v[2] *= -1
                for j in range(3):
                    low[j] = min(low[j], v[j]); high[j] = max(high[j], v[j])
    return {"min": low, "max": high, "size": [high[j]-low[j] for j in range(3)], "instance_triangles": triangles}

def validate():
    root = model_root()
    manifest = json.loads((PROJECT/"Tools"/"models_manifest.json").read_text(encoding="utf-8"))
    report = []
    for item in manifest:
        src = source_from_manifest(root, item["source"])
        target = PROJECT / item["prepared"]
        old, old_bin = read_glb(src)
        new, new_bin = read_glb(target)
        assert hashlib.sha256(src.read_bytes()).hexdigest() == item["source_sha256"]
        for field in ("meshes", "nodes", "materials", "textures", "scenes"):
            assert old.get(field) == new.get(field), (item["key"], field)
        assert len(old["accessors"]) == len(new["accessors"])
        for old_acc, new_acc in zip(old["accessors"], new["accessors"]):
            assert accessor_bytes(old, old_bin, old_acc) == accessor_bytes(new, new_bin, new_acc), item["key"]
        for old_image, new_image in zip(old.get("images", []), new.get("images", [])):
            view = old["bufferViews"][old_image["bufferView"]]
            data = old_bin[view.get("byteOffset", 0):view.get("byteOffset", 0)+view["byteLength"]]
            assert data == (target.parent/new_image["uri"]).read_bytes()
        entry = dict(key=item["key"], validated=True, **bounds(new))
        report.append(entry)
        print(item["key"], "verified", entry["instance_triangles"], "triangles", "size", [round(x, 3) for x in entry["size"]])
    output = PROJECT / "Tools" / "models_validation.json"
    output.write_text(json.dumps({"models": report, "total_models": len(report), "checks": ["source SHA256", "node and hierarchy preservation", "material/texture references", "all accessor bytes identical", "all image bytes identical", "binary buffer bounds", "world bounds and triangle counts"]}, ensure_ascii=False, indent=2), encoding="utf-8")
    print("Verified", len(report), "models. Report:", output)

if __name__ == "__main__":
    validate()

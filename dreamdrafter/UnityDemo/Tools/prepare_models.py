"""Prepare the delivered GLB meshes for the Unity demo, with shared textures.

Run using Blender's bundled Python or any Python 3.10+. No bpy/packages required.
Source files are read only. The reproducible selection manifest is models_manifest.json.
"""
from __future__ import annotations
import hashlib
import json
import os
import pathlib
import struct

PROJECT = pathlib.Path(__file__).resolve().parents[1]

def model_root():
    """Locate optional external authoring assets; normal Unity use needs none."""
    configured = os.environ.get("DREAMDRAFTER_MODEL_ROOT")
    candidates = ([pathlib.Path(configured).expanduser()] if configured else
                  [PROJECT.parent / "模型", PROJECT.parent / "交付"])
    for candidate in candidates:
        if candidate.is_dir() and (candidate / "比萨斜塔").is_dir():
            return candidate.resolve()
    raise FileNotFoundError(
        "External model sources are not included in this repository. Set "
        "DREAMDRAFTER_MODEL_ROOT to the directory containing 比萨斜塔, 帕特农, "
        "万神殿, 科隆大教堂 and 圣家堂. Normal Unity play/build uses the bundled "
        "Assets and SourceModels and does not require this preprocessing step. "
        "Checked: " + ", ".join(str(p) for p in candidates))

def source_from_manifest(root, source):
    """Accept source paths from both original and newly generated manifests."""
    relative = pathlib.PurePosixPath(source.replace("\\", "/"))
    if relative.parts and relative.parts[0] in ("交付", "模型"):
        relative = pathlib.PurePosixPath(*relative.parts[1:])
    return root.joinpath(*relative.parts)
SELECTED = {
    "比萨斜塔": ["PIS_initial_preview", "PIS_scene_preview", "PIS_Loggia_L04", "PIS_Loggia_L05", "PIS_Loggia_L06", "PIS_Loggia_L07", "PIS_Belfry", "PIS_collision"],
    "帕特农": ["PAR_initial_preview", "PAR_scene_preview", "PAR_five_column_route", "PAR_old_drums", "PAR_architraves", "PAR_column_template", "PAR_bronze_portal", "PAR_collision"],
    "万神殿": ["PAN_initial", "PAN_complete", "PAN_open_rain", "PAN_top_sealed", "PAN_top_patch_D7p8", "PAN_pressure_ring", "PAN_water", "PAN_portal_halves", "PAN_drain_modules", "PAN_collision_initial", "PAN_collision"],
    "科隆大教堂": ["COL_initial", "COL_complete", "COL_south_upper", "COL_crane_wood", "COL_fragment_original", "COL_ground_portal", "COL_crayon_pickup", "COL_split_page", "COL_collision_initial", "COL_collision_complete"],
    "圣家堂": ["SAG_room_initial", "SAG_room_complete", "SAG_nave_complete", "SAG_final_complete", "SAG_chain_A", "SAG_chain_B", "SAG_chain_C", "SAG_fragment_cores", "SAG_mirror_and_dust", "SAG_collision", "SAG_final_doors", "SAG_wks_return_preview"],
}

def read_glb(path):
    blob = path.read_bytes()
    magic, version, length = struct.unpack_from("<III", blob)
    assert magic == 0x46546c67 and version == 2 and length == len(blob), path
    offset, doc, binary = 12, None, b""
    while offset < len(blob):
        size, tag = struct.unpack_from("<II", blob, offset)
        chunk = blob[offset+8:offset+8+size]
        if tag == 0x4e4f534a:
            doc = json.loads(chunk)
        elif tag == 0x004e4942:
            binary = chunk
        offset += 8 + size
    assert doc is not None
    return doc, binary

def write_glb(path, doc, binary):
    json_blob = json.dumps(doc, ensure_ascii=False, separators=(",", ":")).encode()
    json_blob += b" " * ((-len(json_blob)) % 4)
    binary += b"\0" * ((-len(binary)) % 4)
    size = 12 + 8 + len(json_blob) + (8 + len(binary) if binary else 0)
    out = struct.pack("<III", 0x46546c67, 2, size)
    out += struct.pack("<II", len(json_blob), 0x4e4f534a) + json_blob
    if binary:
        out += struct.pack("<II", len(binary), 0x004e4942) + binary
    path.write_bytes(out)

def prepare():
    root = model_root()
    dest = PROJECT / "SourceModels"
    textures = dest / "Textures"
    textures.mkdir(parents=True, exist_ok=True)
    manifest, node_catalog = [], {}
    for scene, names in SELECTED.items():
        export = root / scene / "Export"
        for name in names:
            sources = list(export.rglob(name + ".glb"))
            assert len(sources) == 1, (name, sources)
            source = sources[0]
            doc, binary = read_glb(source)
            image_views = set()
            for image in doc.get("images", []):
                if "bufferView" not in image:
                    continue
                view_id = image.pop("bufferView")
                image_views.add(view_id)
                view = doc["bufferViews"][view_id]
                data = binary[view.get("byteOffset", 0):view.get("byteOffset", 0)+view["byteLength"]]
                ext = ".jpg" if image.get("mimeType") == "image/jpeg" else ".png"
                filename = hashlib.sha256(data).hexdigest()[:24] + ext
                image["uri"] = "Textures/" + filename
                image.pop("mimeType", None)
                (textures / filename).write_bytes(data)
            # Pack geometry/animation data, updating references to old buffer views.
            new_views, mapping, new_binary = [], {}, bytearray()
            for i, view in enumerate(doc.get("bufferViews", [])):
                if i in image_views:
                    continue
                mapping[i] = len(new_views)
                start = view.get("byteOffset", 0)
                data = binary[start:start + view["byteLength"]]
                new_binary.extend(b"\0" * ((-len(new_binary)) % 4))
                view = dict(view, byteOffset=len(new_binary))
                new_binary.extend(data)
                new_views.append(view)
            for acc in doc.get("accessors", []):
                if "bufferView" in acc:
                    acc["bufferView"] = mapping[acc["bufferView"]]
                for field in ("indices", "values"):
                    if field in acc.get("sparse", {}):
                        acc["sparse"][field]["bufferView"] = mapping[acc["sparse"][field]["bufferView"]]
            doc["bufferViews"] = new_views
            doc["buffers"] = [{"byteLength": len(new_binary)}]
            target = dest / (name + ".glb")
            write_glb(target, doc, bytes(new_binary))
            nodes = doc.get("nodes", [])
            catalog = []
            for i, node in enumerate(nodes):
                item = {"id": i, "name": node.get("name", "node_" + str(i))}
                for key in ("mesh", "translation", "rotation", "scale", "matrix", "children", "extras"):
                    if key in node:
                        item[key] = node[key]
                catalog.append(item)
            node_catalog[name] = catalog
            manifest.append({"key": name, "scene": scene, "source": str(source.relative_to(root)).replace("\\", "/"),
                             "prepared": "SourceModels/" + name + ".glb", "source_sha256": hashlib.sha256(source.read_bytes()).hexdigest(),
                             "nodes": len(nodes), "meshes": len(doc.get("meshes", [])), "materials": len(doc.get("materials", [])),
                             "geometry_bytes": len(new_binary), "textures": len(doc.get("images", [])),
                             "primitives": sum(len(m.get("primitives", [])) for m in doc.get("meshes", [])),
                             "attributes": sorted(set(a for m in doc.get("meshes", []) for p in m["primitives"] for a in p["attributes"])),
                             "extensions_used": doc.get("extensionsUsed", [])})
            print(f"{name}: {len(nodes)} nodes, {len(doc.get('meshes',[]))} meshes, {len(new_binary)/1048576:.2f} MB geometry")
    (PROJECT / "Tools" / "models_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    (PROJECT / "Tools" / "model_nodes.json").write_text(json.dumps(node_catalog, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"Prepared {len(manifest)} GLBs and {len(list(textures.iterdir()))} shared textures.")

if __name__ == "__main__":
    prepare()

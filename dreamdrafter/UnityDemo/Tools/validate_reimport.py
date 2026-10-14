"""Blender-side round-trip smoke check of the repacked, externally textured GLBs.

blender --background --python Tools/validate_reimport.py
Only reads SourceModels; no source or imported .blend file is saved.
"""
import json
import pathlib
import bpy
from mathutils import Vector

project = pathlib.Path(__file__).resolve().parents[1]
keys = ["PIS_initial_preview", "PIS_scene_preview", "PAR_scene_preview", "PAN_complete", "COL_complete", "SAG_room_complete", "SAG_final_complete"]
report = []
for key in keys:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.gltf(filepath=str(project/"SourceModels"/(key+".glb")))
    assert "FINISHED" in result, (key, result)
    objects = [o for o in bpy.data.objects if o.type == "MESH"]
    images = [im for im in bpy.data.images if im.source == "FILE"]
    # Imported image buffers can be lazy. Access size/pixels before testing has_data.
    for image in images:
        size = tuple(image.size)
        if size[0] > 0:
            _ = image.pixels[0]
        assert image.has_data and size[0] > 0, (key, image.name, image.filepath, size)
    low, high = [float("inf")]*3, [-float("inf")]*3
    for obj in objects:
        for c in obj.bound_box:
            p = obj.matrix_world @ Vector(c)
            for axis in range(3):
                low[axis] = min(low[axis], p[axis]); high[axis] = max(high[axis], p[axis])
    report.append({"key": key, "mesh_nodes": len(objects), "textures_loaded": len(images), "bounds_blender_min": low, "bounds_blender_max": high})
    print("REIMPORT_OK", key, "mesh_nodes", len(objects), "textures", len(images), flush=True)
(project/"Tools"/"models_blender_reimport.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print("REIMPORT_COMPLETE", len(report), flush=True)

"""Extract the delivered 0/30/60 percent crayon surface textures, unchanged."""
import hashlib
import json
import sys
from prepare_models import PROJECT, model_root, read_glb

sys.stdout.reconfigure(encoding="utf-8")
root = model_root()
sources = [("COL_02_SAMPLE_A_0", "M_New_Sandstone", "COL_Stone", 0),
           ("COL_03_SAMPLE_B_30", "M_New_Sandstone_Crayon30", "COL_Crayon30", 0.3),
           ("COL_04_SAMPLE_C_60", "M_New_Sandstone_Crayon60", "COL_Crayon60", 0.6)]
target = PROJECT/"Assets"/"Resources"/"Surfaces"
target.mkdir(parents=True, exist_ok=True)
report = []
for filename, material_name, output, mix in sources:
    source = root/"科隆大教堂"/"Export"/"COL"/(filename+".glb")
    doc, binary = read_glb(source)
    material = next(m for m in doc["materials"] if m["name"] == material_name)
    tex = doc["textures"][material["pbrMetallicRoughness"]["baseColorTexture"]["index"]]
    image = doc["images"][tex["source"]]
    view = doc["bufferViews"][image["bufferView"]]
    data = binary[view.get("byteOffset", 0):view.get("byteOffset", 0)+view["byteLength"]]
    assert data.startswith(b"\x89PNG\r\n\x1a\n")
    (target/(output+".png")).write_bytes(data)
    digest = hashlib.sha256(data).hexdigest()
    report.append(dict(source=str(source.relative_to(root)).replace("\\", "/"), material=material_name,
                       crayon_mix=mix, texture="Assets/Resources/Surfaces/"+output+".png", sha256=digest))
    print(output, material_name, "mix", mix, "sha256", digest[:16])
assert len({item["sha256"] for item in report}) == 3, "Candidate textures must differ"
(PROJECT/"Tools"/"col_surfaces_manifest.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")

"""Copy external preview art and record the bundled OFL Chinese UI font.

No image content is altered. The bundled font and license are never overwritten.
"""
from __future__ import annotations
import json
import pathlib
import shutil
import struct
import sys
from prepare_models import PROJECT, model_root

sys.stdout.reconfigure(encoding="utf-8")

ART = {
    "比萨斜塔": {"PIS_Page": "PIS_00_INITIAL", "PIS_A": "PIS_03_FAIL_BELFRY", "PIS_B": "PIS_01_SUCCESS", "PIS_C": "PIS_08_FAIL_MIDLEVEL", "PIS_Complete": "PIS_01_SUCCESS"},
    "帕特农": {"PAR_Page": "PAR_00_INITIAL", "PAR_A": "PAR_09_SAMPLE_A_3of3", "PAR_B": "PAR_03_FULL_RESTORATION", "PAR_C": "PAR_06_OFF_RHYTHM", "PAR_Complete": "PAR_03_FULL_RESTORATION"},
    "万神殿": {"PAN_Page": "PAN_00_INITIAL", "PAN_A": "PAN_10_SAMPLE_A_6p4", "PAN_B": "PAN_11_SAMPLE_B_7p8", "PAN_C": "PAN_12_SAMPLE_C_9p6", "PAN_Complete": "PAN_03_COMPLETE"},
    "科隆大教堂": {"COL_Page": "COL_00_INITIAL", "COL_A": "COL_02_SAMPLE_A_0", "COL_B": "COL_03_SAMPLE_B_30", "COL_C": "COL_04_SAMPLE_C_60", "COL_Complete": "COL_01_COMPLETE"},
    "圣家堂": {"SAG_Page": "SAG_00_INITIAL_mirror", "SAG_A": "SAG_17_SAMPLE_SMALL_mirror", "SAG_B": "SAG_18_SAMPLE_LARGE_mirror", "SAG_C": "SAG_19_SAMPLE_LONG_mirror", "SAG_Complete": "SAG_02_COMPLETE_mirror", "Final_Page": "SAG_10_FINAL_FACADE"},
}

def font_names(path):
    data = path.read_bytes()
    table_count = struct.unpack_from(">H", data, 4)[0]
    for i in range(table_count):
        tag, checksum, offset, size = struct.unpack_from(">4sIII", data, 12+i*16)
        if tag != b"name":
            continue
        format_id, records, strings = struct.unpack_from(">HHH", data, offset)
        names = {}
        for j in range(records):
            platform, encoding, language, name_id, length, string_offset = struct.unpack_from(">HHHHHH", data, offset+6+j*12)
            raw = data[offset+strings+string_offset:offset+strings+string_offset+length]
            text = raw.decode("utf-16-be" if platform in (0, 3) else "mac_roman", errors="replace")
            names.setdefault(str(name_id), [])
            if text not in names[str(name_id)]:
                names[str(name_id)].append(text)
        return names
    raise ValueError("Font contains no name table")

def prepare():
    root = model_root()
    fonts = PROJECT/"Assets"/"Resources"/"Fonts"
    font = fonts/"Chinese.otf"
    license_path = fonts/"OFL.txt"
    if not font.is_file() or not license_path.is_file():
        raise FileNotFoundError("Restore bundled Assets/Resources/Fonts/Chinese.otf and OFL.txt before preparing art.")
    dest = PROJECT / "Assets" / "Resources" / "Art"
    dest.mkdir(parents=True, exist_ok=True)
    manifest = []
    for scene, entries in ART.items():
        for key, filename in entries.items():
            source = root / scene / "Preview" / (filename+".png")
            target = dest / (key+".png")
            assert source.is_file(), source
            shutil.copyfile(source, target)
            manifest.append({"key": key, "source": str(source.relative_to(root)).replace("\\", "/"), "prepared": str(target.relative_to(PROJECT)).replace("\\", "/")})
    (PROJECT/"Tools"/"art_manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
    metadata = {"source": "NotoSansCJKsc-Regular.otf (bundled Noto Sans CJK SC)", "prepared": "Assets/Resources/Fonts/Chinese.otf", "license": "SIL Open Font License 1.1", "license_file": "Assets/Resources/Fonts/OFL.txt", "name_id_legend": {"0": "copyright", "1": "family", "4": "full name", "8": "manufacturer", "11": "vendor URL", "13": "license description", "14": "license URL"}, "font_name_table": font_names(font)}
    (PROJECT/"Tools"/"font_metadata.json").write_text(json.dumps(metadata, ensure_ascii=False, indent=2), encoding="utf-8")
    copyright_text = "\n".join(metadata["font_name_table"].get("0", []))
    print("Copied", len(manifest), "unaltered preview PNGs; retained bundled Chinese.otf and OFL.txt.")
    print("Font copyright:", copyright_text)
    print("Font license: SIL Open Font License 1.1; see Assets/Resources/Fonts/OFL.txt.")

if __name__ == "__main__":
    prepare()

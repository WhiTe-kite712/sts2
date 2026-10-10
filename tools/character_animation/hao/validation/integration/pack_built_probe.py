"""Prepare a read-only native probe of the final compiled mod PCK."""
import hashlib
import json
from pathlib import Path
import struct

HERE = Path(__file__).resolve().parent
REPO = Path(r"C:\Users\Amazi\Desktop\Spire2Mod\Git Clone\sts2")
ASSETS = REPO / "MySts2Mod/MySts2Mod/animations/coding_farmer"
PACK = REPO / "MySts2Mod/.godot/mono/temp/bin/Debug/MySts2Mod.pck"
DLL = Path(r"E:\steam\steamapps\common\Slay the Spire 2\libspine_godot.windows.template_release.x86_64.dll")

def pack_files(files):
    names = {}
    for name in files:
        raw = name.encode() + b"\0"
        names[name] = raw + b"\0" * (-len(raw) % 4)
    offset = 100 + sum(4 + len(raw) + 8 + 8 + 16 + 4 for raw in names.values())
    header = b"GDPC" + struct.pack("<IIIIIQ", 2, 4, 5, 1, 0, 0) + b"\0" * 64 + struct.pack("<I", len(files))
    directory, payload = bytearray(), bytearray()
    for name, data in files.items():
        directory += struct.pack("<I", len(names[name])) + names[name]
        directory += struct.pack("<QQ", offset, len(data)) + hashlib.md5(data).digest() + struct.pack("<I", 0)
        payload += data
        offset += len(data)
    return header + directory + payload

def file_sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()

source = json.loads((ASSETS / "hao.spjson").read_text(encoding="utf-8"))
forearm = next(bone for bone in source["bones"] if bone["name"] == "forearm_main")
def clip_duration(value):
    if isinstance(value,dict):return max([float(value.get('time',0))]+[clip_duration(v) for v in value.values()])
    if isinstance(value,list):return max([0]+[clip_duration(v) for v in value])
    return 0
peak = max(source["animations"]["attack"]["bones"]["forearm_main"]["rotate"], key=lambda key: key.get("value", 0))
manifest = {
    "pack": PACK.as_posix(), "pack_sha256": file_sha(PACK),
    "source_skeleton": (ASSETS / "hao.spjson").as_posix(),
    "source_png": (ASSETS / "hao.png").as_posix(),
    "source_sha256": {"skeleton": file_sha(ASSETS / "hao.spjson"), "atlas": file_sha(ASSETS / "hao.atlas"), "png": file_sha(ASSETS / "hao.png")},
    "resource_prefix": "res://MySts2Mod/animations/coding_farmer",
    "resource": "res://MySts2Mod/animations/coding_farmer/hao.tres",
    "factory_source_sha256": file_sha(REPO / "MySts2Mod/MySts2ModCode/Character/CodingFarmerCombatVisuals.cs"),
    "durations": {name:clip_duration(clip) for name,clip in source['animations'].items()},
    "peak_time": peak["time"], "setup_forearm_rotation": forearm.get("rotation", 0),
    "timeline_forearm_addition": peak.get("value", 0),
    "expected_forearm_local_rotation": forearm.get("rotation", 0) + peak.get("value", 0),
    "result": (HERE / "built_pack_result.json").as_posix(),
}
descriptor = f'''[configuration]
entry_symbol="spine_godot_library_init"
compatibility_minimum="4.1"
[libraries]
windows.release.x86_64="{DLL.as_posix()}"
windows.debug.x86_64="{DLL.as_posix()}"
'''
files = {
    "project.godot": b'config_version=5\n[application]\nconfig/name="Final Built Mod Spine Probe"\nconfig/features=PackedStringArray("4.5")\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n',
    "built_pack_probe.gd": (HERE / "built_pack_probe.gd").read_bytes(),
    "built_pack_manifest.json": (json.dumps(manifest, indent=2) + "\n").encode(),
    "spine_probe.gdextension": descriptor.encode(),
    ".godot/global_script_class_cache.cfg": b"list=Array[Dictionary]([])\n",
}
(HERE / "built_pack_manifest.json").write_bytes(files["built_pack_manifest.json"])
(HERE / "built_pack_probe.pck").write_bytes(pack_files(files))
print(json.dumps({"probe_pack": str(HERE / "built_pack_probe.pck"), "pack_sha256": manifest["pack_sha256"], "expected_forearm_local_rotation": manifest["expected_forearm_local_rotation"]}))

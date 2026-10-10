"""Pack the isolated Spine probe without starting any Godot/game process.

Code and the manifest enter spine-probe.pck; source/runtime assets enter the
separate animation-assets.pck. Game assemblies, DLLs, scenes, autoloads, and
saves are never packaged.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import struct


PROBE_DIR = Path(__file__).resolve().parent
WORKSPACE = PROBE_DIR.parents[1]
DEFAULT_ASSETS = (
    PROBE_DIR.parent / "source"
    if (PROBE_DIR.parent / "source").is_dir()
    else WORKSPACE / "outputs/hao-skeleton-animations-2026-10-08/source"
)
DEFAULT_DLL = Path(
    r"E:\steam\steamapps\common\Slay the Spire 2\libspine_godot.windows.template_release.x86_64.dll"
)


def make_pack(files: dict[str, bytes]) -> bytes:
    names = {}
    for name in files:
        encoded = name.encode("utf-8") + b"\0"
        names[name] = encoded + b"\0" * (-len(encoded) % 4)
    directory_size = sum(4 + len(name) + 8 + 8 + 16 + 4 for name in names.values())
    offset = 100 + directory_size
    header = (
        b"GDPC"
        + struct.pack("<IIIIIQ", 2, 4, 5, 1, 0, 0)
        + b"\0" * 64
        + struct.pack("<I", len(files))
    )
    assert len(header) == 100
    directory = bytearray()
    payload = bytearray()
    for name, data in files.items():
        directory += struct.pack("<I", len(names[name])) + names[name]
        directory += struct.pack("<QQ", offset, len(data))
        directory += hashlib.md5(data).digest() + struct.pack("<I", 0)
        payload += data
        offset += len(data)
    return header + directory + payload


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--assets-root", type=Path, default=DEFAULT_ASSETS)
    parser.add_argument("--spine-dll", type=Path, default=DEFAULT_DLL)
    parser.add_argument("--result", type=Path, default=PROBE_DIR / "validation.json")
    parser.add_argument("--motion-bones", default="", help="Optional comma-separated exact bone names")
    args = parser.parse_args()
    assets = args.assets_root.resolve()
    spine_dll = args.spine_dll.resolve()
    result = args.result.resolve()
    try:
        result.relative_to(WORKSPACE)
    except ValueError:
        parser.error("--result must stay inside this Codex workspace")
    if not spine_dll.is_file():
        parser.error(f"Existing game Spine DLL not found: {spine_dll}")
    for source_name in ("hao.spine-json", "hao.atlas", "hao.png"):
        if not (assets / source_name).is_file():
            parser.error(f"Animation source asset not found: {assets / source_name}")
    result.parent.mkdir(parents=True, exist_ok=True)
    descriptor = (
        '[configuration]\nentry_symbol="spine_godot_library_init"\ncompatibility_minimum="4.1"\n\n'
        '[libraries]\n'
        f'windows.release.x86_64="{spine_dll.as_posix()}"\n'
        f'windows.debug.x86_64="{spine_dll.as_posix()}"\n'
    )
    manifest = {
        "skeleton": (assets / "hao.spine-json").as_posix(),
        "atlas": (assets / "hao.atlas").as_posix(),
        "png": (assets / "hao.png").as_posix(),
        "spine_dll": spine_dll.as_posix(),
        "expected_version": "4.2.43",
        "animations": ["attack", "cast"],
        "expected_durations": {"attack": 1.0, "cast": 1.4},
        "sample_ratios": [0.0, 0.125, 0.25, 0.4, 0.5, 0.65, 0.8, 1.0],
        "motion_bones": [name.strip() for name in args.motion_bones.split(",") if name.strip()],
        "result": result.as_posix(),
        "asset_pack": (PROBE_DIR / "animation-assets.pck").as_posix(),
        "wrapped_data": "res://runtime/hao.tres",
        "native_texture": (assets.parent / "runtime" / "hao.texture.res").as_posix(),
        "packed_source_sha256": {
            "skeleton": hashlib.sha256((assets / "hao.spine-json").read_bytes()).hexdigest(),
            "atlas": hashlib.sha256((assets / "hao.atlas").read_bytes()).hexdigest(),
            "png": hashlib.sha256((assets / "hao.png").read_bytes()).hexdigest(),
        },
    }
    runtime_dir = assets.parent / "runtime"
    runtime_dir.mkdir(parents=True, exist_ok=True)
    skeleton_bytes = (assets / "hao.spine-json").read_bytes()
    atlas_text = (assets / "hao.atlas").read_text(encoding="utf-8-sig")
    atlas_wrapper = {
        "source_path": "res://source/hao.atlas",
        "atlas_data": atlas_text,
        "normal_texture_prefix": "",
        "specular_texture_prefix": "",
    }
    atlas_bytes = (json.dumps(atlas_wrapper, indent=2) + "\n").encode("utf-8")
    data_bytes = b'''[gd_resource type="SpineSkeletonDataResource" load_steps=3 format=3]

[ext_resource type="SpineAtlasResource" path="res://runtime/hao.spatlas" id="1_atlas"]
[ext_resource type="SpineSkeletonFileResource" path="res://runtime/hao.spjson" id="2_skeleton"]

[resource]
atlas_res = ExtResource("1_atlas")
skeleton_file_res = ExtResource("2_skeleton")
default_mix = 0.0
'''
    (runtime_dir / "hao.spjson").write_bytes(skeleton_bytes)
    (runtime_dir / "hao.spatlas").write_bytes(atlas_bytes)
    (runtime_dir / "hao.tres").write_bytes(data_bytes)
    asset_files = {
        "runtime/hao.spjson": skeleton_bytes,
        "runtime/hao.spatlas": atlas_bytes,
        "runtime/hao.tres": data_bytes,
        "source/hao.spine-json": skeleton_bytes,
        "source/hao.atlas": atlas_text.encode("utf-8"),
        "source/hao.png": (assets / "hao.png").read_bytes(),
    }
    if (runtime_dir / "hao.texture.res").is_file():
        png_import = b'[remap]\nimporter="texture"\ntype="ImageTexture"\npath="res://runtime/hao.texture.res"\n\n[deps]\nsource_file="res://source/hao.png"\ndest_files=["res://runtime/hao.texture.res"]\n'
        (assets / "hao.png.import").write_bytes(png_import)
        asset_files["source/hao.png.import"] = png_import
        asset_files["runtime/hao.texture.res"] = (runtime_dir / "hao.texture.res").read_bytes()
    (PROBE_DIR / "animation-assets.pck").write_bytes(make_pack(asset_files))
    files = {
        "project.godot": (PROBE_DIR / "project.godot").read_bytes(),
        "probe.gd": (PROBE_DIR / "probe.gd").read_bytes(),
        "texture.gd": (PROBE_DIR / "texture.gd").read_bytes(),
        "spine_probe.gdextension": descriptor.encode("utf-8"),
        "manifest.json": (json.dumps(manifest, indent=2) + "\n").encode("utf-8"),
        ".godot/global_script_class_cache.cfg": b"list=Array[Dictionary]([])\n",
    }
    (PROBE_DIR / "manifest.json").write_bytes(files["manifest.json"])
    (PROBE_DIR / "spine_probe.gdextension").write_text(descriptor, encoding="utf-8")
    pack_path = PROBE_DIR / "spine-probe.pck"
    pack_path.write_bytes(make_pack(files))
    print(f"Probe PCK: {pack_path}")
    print(f"Skeleton: {manifest['skeleton']}")
    print(f"Validation result: {result}")
    print(f"Godot resource wrappers: {runtime_dir}")
    print("No Godot/game process was started; no sts2 project or game directory was changed.")


if __name__ == "__main__":
    main()

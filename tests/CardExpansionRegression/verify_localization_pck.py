"""Read-only validation of cards/powers JSON in standalone Godot PCK v2/v3.

No extraction, build, game startup, dependency installation, or file writes.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import BinaryIO


class VerificationError(Exception):
    pass


@dataclass(frozen=True)
class Entry:
    offset: int
    size: int
    md5: bytes
    flags: int


def read_exact(stream: BinaryIO, count: int) -> bytes:
    value = stream.read(count)
    if len(value) != count:
        raise VerificationError("Truncated PCK header, directory, or payload")
    return value


def u32(stream: BinaryIO) -> int:
    return struct.unpack("<I", read_exact(stream, 4))[0]


def u64(stream: BinaryIO) -> int:
    return struct.unpack("<Q", read_exact(stream, 8))[0]


def canonical_resource_path(path: str) -> str:
    value = path.replace("\\", "/")
    return value[6:] if value.startswith("res://") else value


def read_directory(stream: BinaryIO, pack_size: int) -> dict[str, Entry]:
    if read_exact(stream, 4) != b"GDPC":
        raise VerificationError("Expected a standalone PCK beginning with GDPC")
    version = u32(stream)
    if version not in (2, 3):
        raise VerificationError(f"Unsupported PCK format {version}; expected 2 or 3")
    engine = tuple(u32(stream) for _ in range(3))
    pack_flags = u32(stream)
    file_base = u64(stream)
    directory_offset = u64(stream) if version == 3 else None
    read_exact(stream, 16 * 4)  # Reserved header fields.
    if pack_flags & 1:
        raise VerificationError("Encrypted PCK directory is unsupported")
    if pack_flags & ~3:
        raise VerificationError(f"Unsupported PCK flags 0x{pack_flags:x}")
    if version == 3:
        if directory_offset is None or not 0 <= directory_offset < pack_size:
            raise VerificationError("PCK directory offset lies outside the pack")
        stream.seek(directory_offset)
    count = u32(stream)
    if count > 1_000_000:
        raise VerificationError(f"Implausible PCK directory entry count: {count}")
    entries: dict[str, Entry] = {}
    for _ in range(count):
        path_size = u32(stream)
        if path_size == 0 or path_size > 65_536:
            raise VerificationError(f"Invalid PCK path length: {path_size}")
        raw_path = read_exact(stream, path_size)
        path = canonical_resource_path(raw_path.rstrip(b"\0").decode("utf-8"))
        stored_offset = u64(stream)
        size = u64(stream)
        checksum = read_exact(stream, 16)
        flags = u32(stream)
        # Standalone packs have a pack start offset of zero. Both formats store
        # entry offsets relative to their declared data/file base.
        offset = file_base + stored_offset
        if offset < 0 or size > pack_size or offset + size > pack_size:
            raise VerificationError(f"Payload lies outside the PCK: {path}")
        if path in entries:
            raise VerificationError(f"Duplicate resource path in PCK: {path}")
        entries[path] = Entry(offset, size, checksum, flags)
    print(
        f"PCK format={version}, engine={'.'.join(map(str, engine))}, "
        f"files={count}, data_base={file_base}, directory={directory_offset}"
    )
    return entries


def unique_json_pairs(pairs: list[tuple[str, object]]) -> dict[str, object]:
    result: dict[str, object] = {}
    for key, value in pairs:
        if key in result:
            raise VerificationError(f"Duplicate JSON key: {key}")
        result[key] = value
    return result


def validate_translation_json(payload: bytes) -> int:
    values = json.loads(payload.decode("utf-8-sig"), object_pairs_hook=unique_json_pairs)
    if not isinstance(values, dict) or not values:
        raise VerificationError("Translation JSON must be a nonempty object")
    if any(not isinstance(key, str) or not isinstance(value, str) for key, value in values.items()):
        raise VerificationError("Translation JSON must map strings to strings")
    if any(not value.strip() for value in values.values()):
        raise VerificationError("Translation JSON contains an empty value")
    return len(values)


def verify(pack: Path, resources: Path, mod_id: str) -> int:
    failures = 0
    with pack.open("rb") as stream:
        pack_size = pack.stat().st_size
        entries = read_directory(stream, pack_size)
        for language in ("zhs", "eng"):
            for table in ("cards", "powers"):
                relative = f"localization/{language}/{table}.json"
                resource_path = f"{mod_id}/{relative}"
                try:
                    entry = entries.get(resource_path)
                    if entry is None:
                        raise VerificationError("Required resource missing from PCK")
                    if entry.flags:
                        raise VerificationError(f"Unsupported entry flags 0x{entry.flags:x}")
                    stream.seek(entry.offset)
                    packed_bytes = read_exact(stream, entry.size)
                    packed_md5 = hashlib.md5(packed_bytes, usedforsecurity=False).digest()
                    if packed_md5 != entry.md5:
                        raise VerificationError("Payload MD5 differs from PCK directory checksum")
                    source_path = resources / relative
                    source_bytes = source_path.read_bytes()
                    key_count = validate_translation_json(source_bytes)
                    if packed_bytes != source_bytes:
                        packed_sha = hashlib.sha256(packed_bytes).hexdigest()
                        source_sha = hashlib.sha256(source_bytes).hexdigest()
                        raise VerificationError(
                            "Packed bytes differ from source "
                            f"(PCK sha256={packed_sha}, source sha256={source_sha})"
                        )
                    print(f"PASS res://{resource_path}: exact bytes, valid JSON, {key_count} keys")
                except (VerificationError, OSError, UnicodeError, json.JSONDecodeError) as error:
                    failures += 1
                    print(f"FAIL res://{resource_path}: {error}")
    print(f"RESULT {4 - failures}/4 localization resources verified")
    return 1 if failures else 0


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--pck", type=Path, required=True, help="Standalone .pck file to read")
    parser.add_argument(
        "--resources", type=Path, required=True,
        help="Source asset directory containing localization/ (not the C# project root)",
    )
    parser.add_argument("--mod-id", default="MySts2Mod", help="Manifest ID / PCK resource prefix")
    args = parser.parse_args()
    if not args.mod_id or "/" in args.mod_id or "\\" in args.mod_id:
        parser.error("--mod-id must be a nonempty single path component")
    try:
        return verify(args.pck, args.resources, args.mod_id)
    except (VerificationError, OSError, UnicodeError, json.JSONDecodeError) as error:
        print(f"FAIL PCK validation: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())

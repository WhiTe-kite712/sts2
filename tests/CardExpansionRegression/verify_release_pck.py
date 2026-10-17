"""Read-only release-PCK verification: directory hashes, artwork routing and clean resources.

Requires the local bundled Pillow runtime to compare decoded texture pixels with
source PNGs. This script never extracts, builds, installs or changes the PCK.
Only --report writes a small JSON report when explicitly supplied.
"""
from __future__ import annotations

from pathlib import Path
import argparse
import contextlib
import hashlib
import importlib.util
import io
import json
import re
import struct
import sys
from datetime import datetime, timedelta, timezone

from PIL import Image

NOOB_V2_HASH = '5d3d0653399fd0402707fa9859e41f2724663b7a0c7bb95b4a29313e466e1e43'


class CheckError(Exception):
    pass


def require(condition, message):
    if not condition:
        raise CheckError(message)


def normalized(value):
    value = value.replace('\\', '/')
    return value.removeprefix('res://')


def string_value(section, key):
    found = re.search(r'^' + re.escape(key) + r'\s*=\s*"([^"\n]+)"\s*$', section, flags=re.M)
    return found.group(1) if found else None


def decode_texture(payload, expected_size):
    require(len(payload) >= 40 and payload[:4] == b'GST2', 'Texture is not a GST2 CompressedTexture2D')
    version, width, height = struct.unpack_from('<III', payload, 4)
    require(version == 1, f'Unsupported GST2 version {version}')
    require((width, height) == expected_size, f'GST2 dimensions {(width, height)} differ from source {expected_size}')
    errors = []
    for signature in (b'RIFF', b'\x89PNG\r\n\x1a\n'):
        offset = payload.find(signature, 36)
        while offset >= 0:
            try:
                with Image.open(io.BytesIO(payload[offset:])) as image:
                    image.load()
                    if image.size == expected_size:
                        return image.convert('RGBA')
            except (OSError, ValueError) as error:
                errors.append(str(error))
            offset = payload.find(signature, offset + len(signature))
    raise CheckError('GST2 has no supported full-size lossless PNG/WebP image payload')


def verify(args):
    root = args.repo.resolve()
    resources = root / 'MySts2Mod/MySts2Mod'
    manifest_path = args.art_manifest or root / 'docs/card-expansion-54/card-art-map.json'
    art = json.loads(manifest_path.read_text(encoding='utf-8-sig'))
    records = {item['className']: item for item in art['cards']}
    source_cards = {path.stem for path in (root / 'MySts2Mod/MySts2ModCode/Cards').glob('*.cs')
                    if re.search(r'public\s+(?:sealed\s+)?class\s+\w+\s*:\s*MySts2ModCard\b',
                                 path.read_text(encoding='utf-8-sig'))}
    require(bool(source_cards) and len(records) == len(art['cards']) and records.keys() == source_cards,
            'Artwork manifest must match all unique source cards')
    own = {name: item for name, item in records.items() if item['kind'] == 'normal'}
    require(len(own) == len(records) - 2 == art['ownedArtCards'] and {name for name, item in records.items() if item['kind'] == 'vanilla'} == {'Strike', 'Defend'},
            'Expected formal portraits for every card except native Strike/Defend')
    require(art['missingCards'] == 0, 'Artwork manifest still lists missing cards')
    reader_path = root / 'tests/CardExpansionRegression/verify_localization_pck.py'
    spec = importlib.util.spec_from_file_location('release_localization_pck_reader', reader_path)
    require(spec is not None and spec.loader is not None, 'Cannot load read-only PCK reader')
    reader = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = reader
    spec.loader.exec_module(reader)
    failures = []
    report = {'status': 'FAIL', 'date': datetime.now(timezone(timedelta(hours=8))).date().isoformat(), 'pck': str(args.pck.resolve()),
              'repo': str(root), 'readOnly': True, 'compiled': False,
              'pckBytes': args.pck.stat().st_size, 'pckSha256': hashlib.sha256(args.pck.read_bytes()).hexdigest(),
              'directoryEntries': 0, 'payloadMd5Verified': 0, 'formalPortraitsVerified': 0,
              'packedPixelsEqualSource': 0, 'localizationResourcesVerified': 0,
              'unwantedResources': [], 'errors': []}
    with args.pck.open('rb') as stream:
        with contextlib.redirect_stdout(io.StringIO()) as captured:
            entries = reader.read_directory(stream, args.pck.stat().st_size)
        report['header'] = captured.getvalue().strip()
        report['directoryEntries'] = len(entries)

        def payload(path):
            require(path in entries, f'Missing exact-case PCK resource: res://{path}')
            entry = entries[path]
            require(entry.flags == 0, f'Unsupported resource flags for {path}: {entry.flags}')
            stream.seek(entry.offset)
            result = reader.read_exact(stream, entry.size)
            require(hashlib.md5(result, usedforsecurity=False).digest() == entry.md5, f'PCK payload MD5 mismatch: {path}')
            return result

        for path in entries:
            try:
                payload(path)
                report['payloadMd5Verified'] += 1
            except (CheckError, reader.VerificationError, OSError) as error:
                failures.append(str(error))
        # Draft documentation/contact sheets and source/build tools never belong
        # to the runtime pack. Match these names before checking resource routes.
        forbidden_fragments = ('draft', 'contact', 'prompt', 'review', 'verification', 'report', 'readme')
        forbidden_extensions = ('.md', '.html', '.htm', '.py', '.ps1', '.cs', '.csproj', '.sln', '.log', '.csv', '.bak', '.zip')
        unwanted = [path for path in entries if any(token in path.lower() for token in forbidden_fragments)
                    or path.lower().endswith(forbidden_extensions)]
        report['unwantedResources'] = unwanted
        if unwanted:
            failures.append(f'{len(unwanted)} draft/contact/prompt/review/document/tool resources found in PCK')
        allowed_portraits = {name + '.png' for name in own} | {'card.png'}
        portrait_prefix = args.mod_id + '/images/card_portraits/'
        for path in entries:
            if path.startswith(portrait_prefix):
                name = path[len(portrait_prefix):]
                bare = name.removesuffix('.import').removesuffix('.remap')
                if bare not in allowed_portraits:
                    failures.append(f'Orphan/incorrect-case portrait resource: {path}')

        for name, item in sorted(own.items()):
            try:
                relative = 'images/card_portraits/' + name + '.png'
                resource = args.mod_id + '/' + relative
                source = resources / relative
                require(source.name in {p.name for p in source.parent.iterdir()}, f'Source portrait exact-case filename mismatch: {name}')
                source_bytes = source.read_bytes()
                source_hash = hashlib.sha256(source_bytes).hexdigest()
                require(item.get('sha256') == source_hash, f'Source SHA256 differs from artwork map: {name}')
                with Image.open(io.BytesIO(source_bytes)) as image:
                    source_image = image.convert('RGBA')
                require(source_image.size == (item['width'], item['height']), f'Source dimensions differ from artwork map: {name}')
                import_paths = [path for path in (resource + '.import', resource + '.remap') if path in entries]
                require(import_paths, f'No .png.import/.png.remap metadata for {name}')
                targets = set()
                for import_path in import_paths:
                    metadata = payload(import_path).decode('utf-8-sig')
                    parts = re.split(r'(?m)^\[([^\]\r\n]+)\][ \t]*\r?$', metadata)
                    sections = dict(zip(parts[1::2], parts[2::2]))
                    remap = sections.get('remap', '')
                    texture_path = string_value(remap, 'path')
                    require(texture_path is not None, f'No remap path in {import_path}')
                    texture = normalized(texture_path)
                    require(texture.startswith('.godot/imported/' + name + '.png-') and texture.endswith('.ctex'),
                            f'Incorrect-case or unexpected texture remap for {name}: {texture}')
                    targets.add(texture)
                    if import_path.endswith('.import'):
                        require(string_value(remap, 'type') == 'CompressedTexture2D', f'Incorrect resource type for {name}')
                        require(normalized(string_value(sections.get('deps', ''), 'source_file') or '') == resource,
                                f'Incorrect source_file metadata for {name}')
                        require('res://' + texture in sections.get('deps', ''), f'Texture absent from destination dependencies for {name}')
                require(len(targets) == 1, f'Duplicate import/remap metadata disagree for {name}')
                texture = next(iter(targets))
                decoded = decode_texture(payload(texture), source_image.size)
                require(decoded.tobytes() == source_image.tobytes(), f'Packed texture pixels differ from current source artwork: {name}')
                report['packedPixelsEqualSource'] += 1
                report['formalPortraitsVerified'] += 1
                if name == 'Noob':
                    require(item.get('selectedVariant') == 'v2' and source_hash == NOOB_V2_HASH, 'Noob source is not the selected v2')
                    if args.imports:
                        imports = json.loads(args.imports.read_text(encoding='utf-8-sig'))
                        noob_imports = [value for value in imports if value['className'] == 'Noob']
                        require(len(noob_imports) == 1 and noob_imports[0]['sha256'] == source_hash,
                                'Noob v2 source SHA256 differs from staged imports')
                    report['noob'] = {'variant': 'v2', 'sha256': source_hash,
                                      'size': list(source_image.size), 'packedPixelsEqualSource': True,
                                      'matchesImports': bool(args.imports)}
            except (CheckError, reader.VerificationError, OSError, UnicodeError, ValueError, KeyError) as error:
                failures.append(f'{name}: {error}')

        for language in ('zhs', 'eng'):
            for table in ('cards', 'powers'):
                try:
                    relative = f'localization/{language}/{table}.json'
                    source = (resources / relative).read_bytes()
                    reader.validate_translation_json(source)
                    require(payload(args.mod_id + '/' + relative) == source,
                            f'Packed localization differs from source: {relative}')
                    report['localizationResourcesVerified'] += 1
                except (CheckError, reader.VerificationError, OSError, ValueError) as error:
                    failures.append(str(error))
    require(report['formalPortraitsVerified'] <= len(own), 'Invalid verification count')
    if report['formalPortraitsVerified'] != len(own):
        failures.append(f"Only {report['formalPortraitsVerified']}/{len(own)} formal card textures verified")
    if report['localizationResourcesVerified'] != 4:
        failures.append('Not all four localization JSON files verified')
    if report['payloadMd5Verified'] != report['directoryEntries']:
        failures.append('Not every PCK payload MD5 verified')
    report['errorCount'] = len(failures)
    report['errors'] = failures[:30]
    report['status'] = 'FAIL' if failures else 'PASS'
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pck', type=Path, required=True)
    parser.add_argument('--repo', type=Path, required=True)
    parser.add_argument('--mod-id', default='CodeFarmer')
    parser.add_argument('--art-manifest', type=Path, help='Optional staged art map instead of repository art map')
    parser.add_argument('--imports', type=Path, help='Staged imports.json for explicit Noob v2 source comparison')
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    if not args.mod_id or '/' in args.mod_id or '\\' in args.mod_id:
        parser.error('--mod-id must be a single nonempty resource path component')
    try:
        report = verify(args)
    except Exception as error:
        report = {'status': 'FAIL', 'readOnly': True, 'errorCount': 1, 'errors': [str(error)]}
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0 if report['status'] == 'PASS' else 1


if __name__ == '__main__':
    raise SystemExit(main())

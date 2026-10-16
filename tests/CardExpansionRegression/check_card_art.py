"""Validate every card's release artwork by source name, exact path, PNG CRC and manifest hash.

This source-only check never loads the game or a compiled assembly. Existing user
artwork keeps its dimensions; each file's actual dimensions are recorded in the
manifest so later edits must update that record intentionally.
"""
from pathlib import Path
import argparse
import hashlib
import json
import re
import struct
import zlib

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('root', nargs='?', type=Path, default=Path(__file__).resolve().parents[2])
parser.add_argument('--overlay', type=Path, help='Read prepared source and manifests over the repository.')
parser.add_argument('--imports', type=Path, help='Use staged artwork described in imports.json before installation.')
parser.add_argument('--report', type=Path)
args = parser.parse_args()
root = args.root.resolve()
code = Path('MySts2Mod/MySts2ModCode/Cards')
resources = Path('MySts2Mod/MySts2Mod')
portrait_dir = root / resources / 'images/card_portraits'


def selected(relative):
    overlaid = args.overlay / relative if args.overlay else None
    return overlaid if overlaid and overlaid.is_file() else root / relative


def read(relative):
    return selected(relative).read_text(encoding='utf-8-sig')


def png_info(path):
    data = path.read_bytes()
    assert data[:8] == b'\x89PNG\r\n\x1a\n', ('PNG signature', path)
    assert data[12:16] == b'IHDR', ('PNG IHDR', path)
    width, height = struct.unpack('>II', data[16:24])
    assert width > 0 and height > 0, path
    offset = 8
    end = False
    while offset < len(data):
        count = struct.unpack('>I', data[offset:offset + 4])[0]
        kind = data[offset + 4:offset + 8]
        chunk = data[offset + 8:offset + 8 + count]
        expected = struct.unpack('>I', data[offset + 8 + count:offset + 12 + count])[0]
        assert zlib.crc32(kind + chunk) & 0xffffffff == expected, ('PNG CRC', path, kind)
        offset += count + 12
        if kind == b'IEND':
            end = True
            break
    assert end and offset == len(data), ('PNG end', path)
    return {'width': width, 'height': height, 'bytes': len(data), 'sha256': hashlib.sha256(data).hexdigest()}


def slug(name):
    return 'MYSTS2MOD-' + re.sub(r'(?<=[A-Za-z0-9])(?=[A-Z])', '_', name).upper()


models = {}
for path in (root / code).glob('*.cs'):
    source = read(code / path.name)
    for name in re.findall(r'public\s+(?:sealed\s+)?class\s+(\w+)\s*:\s*MySts2ModCard\b', source):
        assert path.name == name + '.cs' and name not in models, name
        models[name] = source
assert len(models) == 88, len(models)
manifest = json.loads(read(Path('docs/card-expansion-54/card-art-map.json')))
records = {item['className']: item for item in manifest['cards']}
assert len(records) == len(manifest['cards']) and records.keys() == models.keys()
assert (manifest['ownedArtCards'], manifest['vanillaCards'], manifest['missingCards']) == (86, 2, 0)
texts = json.loads(read(resources / 'localization/zhs/cards.json'))
imports = json.loads(args.imports.read_text(encoding='utf-8-sig')) if args.imports else []
staged = {item['className']: Path(item['source']) for item in imports}
file_names = {p.name for p in portrait_dir.glob('*.png')}
checked = []
for name in sorted(models):
    item = records[name]
    assert item['title'] == texts[slug(name) + '.title'], (name, 'title')
    if name in ('Strike', 'Defend'):
        native = name + 'Ironclad'
        assert item['kind'] == 'vanilla' and item['portraitPath'] == f'ModelDb.Card<{native}>().PortraitPath'
        for prop, target in [('CustomPortraitPath', 'PortraitPath'), ('PortraitPath', 'PortraitPath'), ('BetaPortraitPath', 'BetaPortraitPath')]:
            assert f'{prop} => ModelDb.Card<{native}>().{target};' in models[name], (name, prop)
        checked.append({'className': name, 'kind': 'vanilla'})
        continue
    filename = name + '.png'
    assert item['kind'] == 'normal' and item['portraitPath'] == 'res://CodeFarmer/images/card_portraits/' + filename, name
    if name in staged:
        path = staged[name]
    else:
        assert filename in file_names, ('Missing artwork or exact case mismatch', name)
        path = portrait_dir / filename
    actual = png_info(path)
    assert all(item.get(key) == value for key, value in actual.items()), ('Artwork manifest mismatch', name, actual)
    checked.append({'className': name, 'kind': 'normal', **actual})

allowed = {name + '.png' for name in models if name not in ('Strike', 'Defend')} | {'card.png'}
assert file_names <= allowed, ('Orphan portrait filenames', sorted(file_names - allowed))
assert file_names | {name + '.png' for name in staged} == allowed, ('Missing portrait assets', sorted(allowed - file_names - {name + '.png' for name in staged}))
assert not [p for p in portrait_dir.rglob('*') if p.is_file() and p.suffix.lower() in ('.png', '.jpg', '.jpeg', '.webp') and p.parent != portrait_dir], 'Unreferenced nested card artwork'
png_info(portrait_dir / 'card.png')
base = read(code / 'MySts2ModCard.cs')
assert 'PortraitPath => $"{GetType().Name}.png".CardImagePath();' in base
assert 'CustomPortraitPath => $"{GetType().Name}.png".BigCardImagePath();' in base
assert 'BetaPortraitPath => PortraitPath;' in base, 'Beta art must use the formal portrait when no distinct beta set exists.'
noob = records['Noob']
assert noob.get('selectedVariant') == 'v2' and noob['sha256'] == '5d3d0653399fd0402707fa9859e41f2724663b7a0c7bb95b4a29313e466e1e43'
generated = [item for item in records.values() if item.get('generated')]
assert len(generated) == manifest['generatedArtCards'] == 33
different_sizes = [{'className': item['className'], 'width': item['width'], 'height': item['height']}
                   for item in generated if (item['width'], item['height']) != (1000, 790)]
report = {'status': 'PASS', 'date': '2026-10-16', 'compiled': False,
          'counts': {'cards': 88, 'ownedArtwork': 86, 'nativeArtwork': 2, 'newArtwork': 33, 'missing': 0},
          'noobVariant': 'v2', 'allPngCrcsValid': True,
          'userEditedDraftsWithOtherDimensions': different_sizes,
          'checks': ['Exact-case class filename mapping', 'Native Strike and Defend portraits preserved',
                     'Every artwork SHA256, byte count, dimensions and PNG chunk CRCs',
                     'No orphan or duplicate nested card portrait files', 'Regular, large and beta portrait routing',
                     'Noob v2 selected and no placeholders assigned to active cards'],
          'files': checked}
if args.report:
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('PASS: 88 cards: 86 exact-case owned portraits and 2 native portraits; no missing artwork.')
print('PASS: SHA256, PNG CRC, dimensions, Noob v2, source portrait routing and orphan checks.')
if different_sizes:
    print('NOTE: Later user-edited draft dimensions preserved for ' + ', '.join(item['className'] for item in different_sizes) + '.')
print('No compilation, DLL execution, game launch or installation was performed.')

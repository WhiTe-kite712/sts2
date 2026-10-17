"""Check localization and deliberately selected hover-tip bindings without loading game DLLs."""

import argparse
import json
import re
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('root', nargs='?', type=Path, default=Path(__file__).resolve().parents[2])
parser.add_argument('--overlay', type=Path)
parser.add_argument('--native-root', type=Path)
args = parser.parse_args()
ROOT = args.root.resolve()
PROJECT = Path('MySts2Mod')
CODE = PROJECT / 'MySts2ModCode'
RESOURCES = PROJECT / 'MySts2Mod/localization'
DUMB_POWERS = {'HaoPower', 'HiddenStatePower', 'OpenHaoStatePower'}
errors = []


def read(relative):
    prepared = args.overlay / relative if args.overlay else None
    return (prepared if prepared and prepared.is_file() else ROOT / relative).read_text(encoding='utf-8-sig')


def files(relative, pattern):
    names = {p.name for p in (ROOT / relative).glob(pattern)}
    if args.overlay:
        names |= {p.name for p in (args.overlay / relative).glob(pattern)}
    return sorted(names)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            errors.append(f'Duplicate localization key: {key}')
        result[key] = value
    return result


def slug(name):
    return 'MYSTS2MOD-' + re.sub(r'(?<=[A-Za-z0-9])(?=[A-Z])', '_', name).upper()


models = {}
all_sources = {}
for folder, table in [('Cards', 'cards'), ('Powers', 'powers'), ('Relics', 'relics')]:
    for name in files(CODE / folder, '*.cs'):
        relative = CODE / folder / name
        source = re.sub(r'/\*.*?\*/|//[^\n]*', '', read(relative), flags=re.S)
        all_sources[relative] = source
        match = re.search(r'public\s+(?:sealed\s+)?class\s+(\w+)\s*:\s*(\w+)', source)
        if not match:
            continue
        # This bridge inherits the native/BaseLib temporary-power localization and does not
        # declare its own MySts2ModPower title or descriptions.
        if match[2] == 'CustomTemporaryPowerModel':
            continue
        custom_id = re.search(r'\[CustomID\("([^"]+)"\)\]', source)
        entry = custom_id[1] if custom_id else slug(match[1])
        models[match[1]] = (table, entry, source, relative)

languages = sorted(p.name for p in (ROOT / RESOURCES).iterdir() if p.is_dir())
tables = {}
for language in languages:
    for table in ('cards', 'powers', 'relics', 'static_hover_tips'):
        relative = RESOURCES / language / (table + '.json')
        try:
            translations = json.loads(read(relative), object_pairs_hook=unique_object)
        except (OSError, ValueError) as exc:
            errors.append(f'{relative}: {exc}')
            translations = {}
        tables[language, table] = translations
        for key, value in translations.items():
            if not isinstance(value, str) or not value.strip():
                errors.append(f'{language}/{table}: empty/non-string value at {key}')
        if language != languages[0] and translations.keys() != tables[languages[0], table].keys():
            errors.append(f'{language}/{table}: localization keys differ between languages')

native = args.native_root or ROOT.parents[1] / 'decompiled-pck/recovered/src/Core'
game_powers = native / 'Models/Powers'
if not game_powers.is_dir():
    errors.append(f'Native source directory not found: {game_powers}; pass --native-root')

tip_count = 0
for name, (table, entry, source, relative) in models.items():
    references = re.findall(r'HoverTipFactory\.FromPower<(\w+)>', source)
    tip_count += len(references)
    if len(references) != len(set(references)):
        errors.append(f'{relative}: duplicate power hover tips')
    for target in references:
        if target == name:
            errors.append(f'{relative}: self-referencing power hover tip')
        elif target in models:
            if models[target][0] != 'powers' or target not in DUMB_POWERS:
                errors.append(f'{relative}: custom power {target} has no public dumb tooltip')
        elif not (game_powers / (target + '.cs')).is_file():
            errors.append(f'{relative}: unknown native power tooltip {target}')

    variables = set(re.findall(r'new\s+(?:\w+\.)*\w+Var(?:<\w+>)?\(\s*"([^"]+)"', source))
    variables.update(re.findall(r'new\s+PowerVar<(\w+)>\(\s*(?!")\d', source))
    variables.update(re.findall(r'new\s+(\w+)Var\(', source))
    variables.update(re.findall(r'description\.Add\(\s*"([^"]+)"', source))
    variables.update({'IfUpgrade', 'IfUpgraded', 'IsUpgraded', 'OnTable', 'InCombat', 'IsTargeting',
                      'TargetType', 'GainsBlock', 'IsOstyAlive', 'energyPrefix', 'singleStarIcon'})
    if table == 'powers':
        variables.update({'Amount', 'AmountOnTurnStart', 'OnPlayer', 'IsMultiplayer', 'PlayerCount',
                          'OwnerName', 'ApplierName', 'TargetName'})
    fields = ['title', 'smartDescription'] if table == 'powers' else ['title', 'description']
    if table == 'powers' and name in DUMB_POWERS:
        fields.append('description')
    for language in languages:
        translations = tables[language, table]
        for suffix in fields:
            key = entry + '.' + suffix
            if key not in translations:
                errors.append(f'{language}/{table}: missing {key}')
                continue
            used = set(re.findall(r'\{([A-Za-z_]\w*)(?=[:}])', translations[key]))
            if used - variables:
                errors.append(f'{language}/{table}: {key} has unbound variables {sorted(used - variables)}')
        if table == 'powers' and name not in DUMB_POWERS and entry + '.description' in translations:
            errors.append(f'{language}/{table}: retired dumb description still exists: {entry}')

for relative, source in all_sources.items():
    if re.search(r'\bEnergyHoverTip\b|HoverTipFactory\.ForEnergy\(|StaticHoverTip\.Block\b', source):
        errors.append(f'{relative}: redundant explicit energy/block tooltip')
    for table, key in re.findall(r'new\s+LocString\("([^"]+)",\s*"([^"]+)"\)', source):
        if table in {'cards', 'powers', 'relics', 'static_hover_tips'}:
            for language in languages:
                if key not in tables[language, table]:
                    errors.append(f'{language}/{table}: source references missing {key}')

regular = read(CODE / 'Extensions/RegularDebuffs.cs')
for suffix in ('title', 'description'):
    key = 'MYSTS2MOD-REGULAR_DEBUFFS.' + suffix
    if key not in regular:
        errors.append('RegularDebuffs.HoverTips does not include glossary key ' + key)
    for language in languages:
        if not tables[language, 'static_hover_tips'].get(key):
            errors.append(f'{language}/static_hover_tips: missing {key}')

if errors:
    for error in errors:
        print('FAIL:', error)
    raise SystemExit(f'{len(errors)} binding error(s)')
counts = {table: sum(model[0] == table for model in models.values()) for table in ('cards', 'powers', 'relics')}
print(f'PASS {counts}; languages={languages}; {tip_count} deliberate native/core power tooltip links')
print('PASS smart descriptions on all custom powers; dumb descriptions only for Hao and its two states')
print('PASS regular-debuff glossary; no explicit energy/block glossary duplication')
print('Static binding checks only; this does not execute gameplay or validate the in-game UI.')

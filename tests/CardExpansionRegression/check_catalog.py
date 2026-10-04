"""Validate runtime card/power inventory against source localization and pool rules."""
from pathlib import Path
from collections import Counter
import json
import re
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
docs = root / 'docs/card-expansion-54'
resources = root / 'MySts2Mod/MySts2Mod'
code = root / 'MySts2Mod/MySts2ModCode'
def pairs(values):
    out = {}
    for key, value in values:
        if key in out: raise ValueError('Duplicate JSON key ' + key)
        out[key] = value
    return out
def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=pairs)
cards = read(docs / 'runtime-inventory.json')
powers = read(docs / 'power-inventory.json')
catalog = read(docs / 'catalog-new-cards.json')
power_catalog = read(docs / 'catalog-new-powers.json')
assert len(cards) == 54 and len(catalog) == 18
assert len({c['id'] for c in cards}) == 54
assert len(powers) == 20 and len({p['id'] for p in powers}) == 20
assert len(power_catalog) == 4 and len({p['className'] for p in power_catalog}) == 4
power_by_class = {p['className']: p for p in powers}
assert {p['className'] for p in power_catalog} <= power_by_class.keys()
new = {c['className']: c for c in catalog}
by_class = {c['className']: c for c in cards}
assert Counter(c['pool'] for c in cards) == {'HaoCardPool': 52, 'ColorlessCardPool': 2}
assert {c['className'] for c in cards if c['before']['rarity'] == 'Token'} == {'Xiao', 'Chuan'}
assert len([c for c in cards if c['before']['rarity'] not in ('Basic', 'Token')]) == 47
assert len([c for c in cards if c['before']['rarity'] == 'Basic']) == 5
assert Counter(c['before']['type'] for c in cards) == {'Attack': 21, 'Skill': 26, 'Power': 7}
assert Counter('X' if c['before']['costsX'] else str(c['before']['cost']) for c in cards) == {
    '0': 10, '1': 30, '2': 9, '3': 4, 'X': 1
}
for name, card in new.items():
    actual = by_class[name]
    assert actual['pool'] == 'HaoCardPool'
    for field in ('cost', 'type', 'rarity'):
        assert actual['before'][field] == card[field], (name, field)
    assert actual['before'] != actual['after'], 'No observable upgrade: ' + name
    assert actual['before']['keywords'] == sorted(card['keywords']), (name, 'keywords')
print('PASS 54 distinct cards; 52 character cards, 2 existing colorless tokens, 5 Basic cards, 47 non-Basic reward cards')
print('PASS all 18 new cards match design cost/type/rarity/keywords and have an observable upgrade; 20 concrete powers, 4 new')

loaded = {}
for language, suffix in [('zhs', 'Zhs'), ('eng', 'Eng')]:
    loaded[language] = {}
    for table, models in [('cards', cards), ('powers', powers)]:
        texts = read(resources / 'localization' / language / (table + '.json'))
        loaded[language][table] = texts
        assert all(isinstance(v, str) and v.strip() for v in texts.values()), (language, table, 'empty text')
        for model in models:
            fields = ['title', 'description'] + (['smartDescription'] if table == 'powers' else [])
            known_vars = set(model['before']['variables'] if table == 'cards' else model['variables'])
            if table == 'cards': known_vars |= {'IfUpgraded', 'OnTable', 'InCombat', 'IsTargeting', 'TargetType', 'GainsBlock', 'IsOstyAlive', 'energyPrefix', 'singleStarIcon'}
            if table == 'powers': known_vars |= {'Amount', 'AmountOnTurnStart', 'energyPrefix', 'singleStarIcon'}
            for field in fields:
                key = model['id'] + '.' + field
                assert key in texts, (language, key)
                if field != 'title':
                    used = set(re.findall(r'\{([A-Za-z][A-Za-z0-9_]*)[}:]', texts[key]))
                    assert used <= known_vars, (language, key, sorted(used - known_vars))
        print(f'PASS {language}/{table}: duplicate-free JSON, {len(models)} model descriptions, all variables bound to actual DLL')
    for card in catalog:
        texts = loaded[language]['cards']
        model = by_class[card['className']]
        for field in ('title', 'description'):
            assert texts[model['id'] + '.' + field] == card[field + suffix], (language, card['className'], field)
    for power in power_catalog:
        texts = loaded[language]['powers']
        model = power_by_class[power['className']]
        for field in ('title', 'description', 'smartDescription'):
            assert texts[model['id'] + '.' + field] == power[field + suffix], (language, power['className'], field)

for table in ('cards', 'powers'):
    assert loaded['zhs'][table].keys() == loaded['eng'][table].keys(), (table, 'language key mismatch')
print('PASS Chinese and English localization keys match exactly')
prompt_count = 0
for card in cards:
    source = (code / 'Cards' / (card['className'] + '.cs')).read_text(encoding='utf-8-sig')
    if re.search(r'\bSelectionScreenPrompt\b', source):
        for language in ('zhs', 'eng'):
            assert card['id'] + '.selectionScreenPrompt' in loaded[language]['cards'], (language, card['className'])
        prompt_count += 1
print(f'PASS all {prompt_count} card-specific selection screens have Chinese and English prompts')
stats = {'cardCount': len(cards), 'newCardCount': len(catalog), 'powerCount': len(powers), 'newPowerCount': len(power_catalog),
         'types': dict(Counter(c['before']['type'] for c in cards)),
         'rarities': dict(Counter(c['before']['rarity'] for c in cards)),
         'costs': dict(Counter('X' if c['before']['costsX'] else str(c['before']['cost']) for c in cards)),
         'newTypes': dict(Counter(c['type'] for c in catalog)),
         'newRarities': dict(Counter(c['rarity'] for c in catalog))}
(docs / 'validation-statistics.json').write_text(json.dumps(stats, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('RESULT catalog/localization validation passed')

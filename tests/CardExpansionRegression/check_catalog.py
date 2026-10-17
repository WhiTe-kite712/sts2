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
DUMB_POWER_NAMES = {'HaoPower', 'HiddenStatePower', 'OpenHaoStatePower'}
def fields_for(table, name):
    if table == 'cards': return ['title', 'description']
    return ['title', 'smartDescription'] + (['description'] if name in DUMB_POWER_NAMES else [])
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
assert len(cards) == 88, 'Runtime inventory is stale. Build the current mod and run Harness with expected count 88 to regenerate runtime-inventory.json.'
assert len(catalog) == 18
assert len({c['id'] for c in cards}) == 88
assert len(powers) == 30 and len({p['id'] for p in powers}) == 30, 'Power inventory is stale. Build the current mod and run Harness to regenerate power-inventory.json with 30 concrete powers.'
assert len(power_catalog) == 4 and len({p['className'] for p in power_catalog}) == 4
power_by_class = {p['className']: p for p in powers}
assert {p['className'] for p in power_catalog} <= power_by_class.keys()
new = {c['className']: c for c in catalog}
by_class = {c['className']: c for c in cards}
assert Counter(c['pool'] for c in cards) == {'HaoCardPool': 86, 'ColorlessCardPool': 2}
assert {c['className'] for c in cards if c['before']['rarity'] == 'Token'} == {'Xiao', 'Chuan'}
assert len([c for c in cards if c['before']['rarity'] in ('Common', 'Uncommon', 'Rare')]) == 79
assert {c['className'] for c in cards if c['before']['rarity'] == 'Ancient'} == {'GodList', 'Lost'}
assert Counter(c['before']['rarity'] for c in cards) == {'Basic': 5, 'Common': 24, 'Uncommon': 32, 'Rare': 23, 'Token': 2, 'Ancient': 2}
assert len([c for c in cards if c['before']['rarity'] == 'Basic']) == 5
assert Counter(c['before']['type'] for c in cards) == {'Attack': 26, 'Skill': 49, 'Power': 13}
assert Counter('X' if c['before']['costsX'] else str(c['before']['cost']) for c in cards) == {
    '0': 15, '1': 49, '2': 16, '3': 6, 'X': 2
}
god_list = by_class['GodList']
assert god_list['before']['cost'] == 2 and god_list['after']['cost'] == 1
assert all(god_list[version]['type'] == 'Power' and god_list[version]['rarity'] == 'Ancient' for version in ('before', 'after'))
assert 'ITomeCard' in (code / 'Cards/GodList.cs').read_text(encoding='utf-8-sig')
assert 'GodListPower' in power_by_class
lost = by_class['Lost']
assert lost['before']['cost'] == lost['after']['cost'] == 1 and lost['before']['type'] == 'Skill' and lost['before']['rarity'] == 'Ancient'
assert 'ITomeCard' not in (code / 'Cards/Lost.cs').read_text(encoding='utf-8-sig')
narcissism_source = (code / 'Cards/Narcissism.cs').read_text(encoding='utf-8-sig')
assert 'ITranscendenceCard' in narcissism_source and re.search(r'\bLost\b', narcissism_source)
hao_relic = (code / 'Relics/Hao.cs').read_text(encoding='utf-8-sig')
upgraded_relic = (code / 'Relics/SkyHighHao.cs').read_text(encoding='utf-8-sig')
assert 'GetUpgradeReplacement' in hao_relic and 'ModelDb.Relic<SkyHighHao>()' in hao_relic
assert 'class SkyHighHao' in upgraded_relic and 'RelicRarity.Starter' in upgraded_relic
for name, card in new.items():
    actual = by_class[name]
    assert actual['pool'] == 'HaoCardPool'
    for field in ('cost', 'type', 'rarity'):
        assert actual['before'][field] == card[field], (name, field)
    assert actual['before'] != actual['after'], 'No observable upgrade: ' + name
    assert actual['before']['keywords'] == sorted(card['keywords']), (name, 'keywords')
print('PASS 88 distinct cards; 86 character cards, 2 existing colorless tokens, 5 Basic cards, 79 Common/Uncommon/Rare reward cards and 2 Ancient cards (GodList/Lost)')
print('PASS all 18 migration expansion cards match design cost/type/rarity/keywords and have an observable upgrade; 30 concrete powers, 4 migration expansion powers')

loaded = {}
for language, suffix in [('zhs', 'Zhs'), ('eng', 'Eng')]:
    loaded[language] = {}
    for table, models in [('cards', cards), ('powers', powers)]:
        texts = read(resources / 'localization' / language / (table + '.json'))
        loaded[language][table] = texts
        assert all(isinstance(v, str) and v.strip() for v in texts.values()), (language, table, 'empty text')
        for model in models:
            fields = fields_for(table, model['className'])
            known_vars = set(model['before']['variables'] if table == 'cards' else model['variables'])
            if table == 'cards': known_vars.update(model['before'].get('descriptionArgs', []))
            if table == 'cards': known_vars |= {'IfUpgraded', 'IfUpgrade', 'OnTable', 'InCombat', 'IsTargeting', 'TargetType', 'GainsBlock', 'IsOstyAlive', 'energyPrefix', 'singleStarIcon'}
            if table == 'powers': known_vars |= {'Amount', 'AmountOnTurnStart', 'energyPrefix', 'singleStarIcon'}
            for field in fields:
                key = model['id'] + '.' + field
                assert key in texts, (language, key)
                if field != 'title':
                    used = set(re.findall(r'\{([A-Za-z][A-Za-z0-9_]*)[}:]', texts[key]))
                    assert used <= known_vars, (language, key, sorted(used - known_vars))
        if table == 'powers':
            expected_dumb = {model['id'] + '.description' for model in models if model['className'] in DUMB_POWER_NAMES}
            assert {key for key in texts if key.endswith('.description')} == expected_dumb, (language, 'unexpected dumb power descriptions')
        print(f'PASS {language}/{table}: duplicate-free JSON, {len(models)} model descriptions, all variables bound to actual DLL')
    for card in catalog:
        texts = loaded[language]['cards']
        model = by_class[card['className']]
        for field in ('title', 'description'):
            assert texts[model['id'] + '.' + field] == card[field + suffix], (language, card['className'], field)
    for power in power_catalog:
        texts = loaded[language]['powers']
        model = power_by_class[power['className']]
        for field in fields_for('powers', power['className']):
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

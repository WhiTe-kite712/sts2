"""Validate current source/resources without building, debugging or loading a DLL."""
from pathlib import Path
from collections import Counter
import json
import hashlib
import re
import sys

root = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(__file__).resolve().parents[2]
docs = root / 'docs/card-expansion-54'
code = root / 'MySts2Mod/MySts2ModCode'
DUMB_POWER_NAMES = {'HaoPower', 'HiddenStatePower', 'OpenHaoStatePower'}
def fields_for(table, name):
    if table == 'cards': return ['title', 'description']
    return ['title', 'smartDescription'] + (['description'] if name in DUMB_POWER_NAMES else [])
resources = root / 'MySts2Mod/MySts2Mod'

def pairs(items):
    data = {}
    for key, value in items:
        assert key not in data, 'Duplicate JSON key: ' + key
        data[key] = value
    return data

def read(path): return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=pairs)
def slug(name): return 'MYSTS2MOD-' + re.sub(r'(?<=[A-Za-z0-9])(?=[A-Z])', '_', name).upper()
mapping = read(docs / 'rename-map.json')
catalog = read(docs / 'catalog-new-cards.json')
powers = read(docs / 'catalog-new-powers.json')
inventory = read(docs / 'source-inventory.json')
models = {}
for path in code.rglob('*.cs'):
    text = path.read_text(encoding='utf-8-sig')
    for name, base in re.findall(r'public\s+(?:sealed\s+)?class\s+(\w+)\s*:\s*(MySts2ModCard|MySts2ModPower|HaoStatStatePower)\b', text):
        assert name not in models, name
        assert path.name == name + '.cs', (path, name)
        if base == 'MySts2ModCard': assert re.search(r'public\s+' + name + r'\s*\(', text), 'Constructor mismatch: ' + name
        models[name] = (base, path, text)
cards = {name: value for name, value in models.items() if value[0] == 'MySts2ModCard'}
power_models = {name: value for name, value in models.items() if value[0] != 'MySts2ModCard'}
assert len(cards) == 88 and len(power_models) == 30
assert len(catalog) == 18 and len(powers) == 4
assert sum('Pool(typeof(HaoCardPool))' in item[2] for item in cards.values()) == 86
assert sum('Pool(typeof(ColorlessCardPool))' in item[2] for item in cards.values()) == 2
assert {c['className'] for c in inventory['cards']} == cards.keys()
assert {p['className'] for p in inventory['powers']} == power_models.keys()
for name, (_, path, text) in cards.items():
    # Metadata remains a source-derived manifest, not a freshly executed inventory.
    item = next(c for c in inventory['cards'] if c['className'] == name)
    constructor = re.search(r'public\s+' + name + r'\s*\(\s*\)\s*:\s*base\(([^)]+)\)', text)
    if constructor:
        constants = dict(re.findall(r'const\s+(?:int|CardType|CardRarity|TargetType)\s+(\w+)\s*=\s*([^;]+);', text))
        arguments = [constants.get(value.strip(), value.strip()) for value in constructor.group(1).split(',')]
        signature = ', '.join(arguments)
        assert 'CardType.' + item['before']['type'] in signature, name
        assert 'CardRarity.' + item['before']['rarity'] in signature, name
        assert 'TargetType.' + item['before']['target'] in signature, name
        if not item['before']['costsX']: assert int(signature.split(',')[0]) == item['before']['cost'], name
assert Counter(c['before']['type'] for c in inventory['cards']) == {'Attack': 26, 'Skill': 49, 'Power': 13}
assert Counter(c['before']['rarity'] for c in inventory['cards']) == {'Basic': 5, 'Common': 24, 'Uncommon': 32, 'Rare': 23, 'Token': 2, 'Ancient': 2}
normal_rewards = [c for c in inventory['cards'] if c['before']['rarity'] in ('Common', 'Uncommon', 'Rare')]
assert len(normal_rewards) == 79
assert {c['className'] for c in inventory['cards'] if c['before']['rarity'] == 'Ancient'} == {'GodList', 'Lost'}
assert sum(c['before']['rarity'] == 'Basic' for c in inventory['cards']) == 5
assert Counter('X' if c['before']['costsX'] else str(c['before']['cost']) for c in inventory['cards']) == {'0': 15, '1': 49, '2': 16, '3': 6, 'X': 2}
god_list = next(c for c in inventory['cards'] if c['className'] == 'GodList')
assert god_list['before']['cost'] == 2 and god_list['after']['cost'] == 1
assert all(god_list[version]['type'] == 'Power' and god_list[version]['rarity'] == 'Ancient' for version in ('before', 'after'))
assert 'ITomeCard' in cards['GodList'][2] and 'GodListPower' in power_models
lost = next(c for c in inventory['cards'] if c['className'] == 'Lost')
assert lost['before']['cost'] == lost['after']['cost'] == 1 and lost['before']['type'] == 'Skill' and lost['before']['rarity'] == 'Ancient'
assert 'ITomeCard' not in cards['Lost'][2]
assert 'ITranscendenceCard' in cards['Narcissism'][2] and re.search(r'\bLost\b', cards['Narcissism'][2])
hao_relic = (code / 'Relics/Hao.cs').read_text(encoding='utf-8-sig')
upgraded_relic = (code / 'Relics/SkyHighHao.cs').read_text(encoding='utf-8-sig')
assert 'GetUpgradeReplacement' in hao_relic and 'ModelDb.Relic<SkyHighHao>()' in hao_relic
assert 'class SkyHighHao' in upgraded_relic and 'RelicRarity.Starter' in upgraded_relic
stats = read(docs / 'source-statistics.json')
assert stats['cardCount'] == 88 and stats['characterCards'] == 86 and stats['colorlessTokens'] == 2
assert stats['normalRewardCards'] == 79 and stats['ancientCards'] == 2
assert stats['newCardCount'] == 52 and stats['postMigrationCardCount'] == 34
assert stats['powerCount'] == 30 and stats['newPowerCount'] == 14 and stats['postMigrationPowerCount'] == 10
assert stats['migrationExpansionCardCount'] == len(catalog) == 18
assert stats['migrationExpansionPowerCount'] == len(powers) == 4
assert stats['types'] == Counter(c['before']['type'] for c in inventory['cards'])
assert stats['rarities'] == Counter(c['before']['rarity'] for c in inventory['cards'])
assert stats['costs'] == Counter('X' if c['before']['costsX'] else str(c['before']['cost']) for c in inventory['cards'])
print('PASS source models: 88 cards, 86 character cards, 2 colorless tokens, 30 custom powers, 18 migration expansion cards plus 34 later additions')
print('PASS 79 Common/Uncommon/Rare reward cards, 5 Basic cards and 2 Ancient cards: GodList from ITomeCard, Lost from Narcissism ITranscendenceCard; Hao upgrades to SkyHighHao')
print('PASS class/file/constructor names and source metadata match; no duplicate models')

for language, suffix in [('zhs', 'Zhs'), ('eng', 'Eng')]:
    for table, source in [('cards', cards), ('powers', power_models)]:
        translations = read(resources / 'localization' / language / (table + '.json'))
        for name, (_, path, text) in source.items():
            fields = fields_for(table, name)
            for field in fields: assert translations.get(slug(name) + '.' + field), (language, name, field)
            if table == 'cards' and 'SelectionScreenPrompt' in text:
                assert translations.get(slug(name) + '.selectionScreenPrompt'), (language, name, 'selection prompt')
            known = {'Amount', 'AmountOnTurnStart', 'IfUpgraded', 'IfUpgrade', 'energyPrefix', 'singleStarIcon', 'OnTable', 'InCombat', 'IsTargeting', 'TargetType', 'GainsBlock', 'IsOstyAlive'}
            known.update(re.findall(r'new\s+(?:DynamicVar|IntVar)\s*\(\s*"([^"]+)"', text))
            known.update(re.findall(r'new\s+\w+Var\s*(?:<[^>]+>)?\s*\(\s*"([^"]+)"', text))
            known.update(re.findall(r'new\s+PowerVar\s*<\s*(\w+)\s*>', text))
            known.update(re.findall(r'new\s+(\w+)Var\s*\(', text))
            known.update(re.findall(r'description\.Add\(\s*"([^"]+)"', text))
            for field in fields[1:]:
                used = set(re.findall(r'\{([A-Za-z][A-Za-z0-9_]*)[}:]', translations[slug(name) + '.' + field]))
                assert used <= known, (language, name, field, sorted(used - known))
        if table == 'powers':
            assert {key for key in translations if key.endswith('.description')} == {slug(name) + '.description' for name in DUMB_POWER_NAMES}, (language, 'unexpected dumb power descriptions')
        for item in catalog if table == 'cards' else powers:
            assert item['className'] in source
            if table == 'cards': assert (code / item['sourcePath']).is_file(), item['className']
            for field in fields_for(table, item['className']):
                assert translations[slug(item['className']) + '.' + field] == item[field + suffix], (language, item['className'], field)
        for old in list(mapping['cards']) + list(mapping['powers']) + list(mapping['removed']):
            assert not any(key.startswith(slug(old) + '.') for key in translations), (language, old)
    texts = read(resources / 'localization' / language / 'cards.json')
    for old, (new, zhs, eng) in mapping['cards'].items():
        assert texts[slug(new) + '.title'] == (zhs if language == 'zhs' else eng)
    print('PASS ' + language + ': titles/descriptions, variables, selection prompts and catalogs match')
for table in ('cards', 'powers'):
    assert read(resources / 'localization/zhs' / (table + '.json')).keys() == read(resources / 'localization/eng' / (table + '.json')).keys()
print('PASS matching Chinese/English keys, with deleted model entries removed')

active_paths = list(code.rglob('*.cs')) + list((root / 'tests/CardExpansionRegression').glob('*.cs'))
active_text = '\n'.join(path.read_text(encoding='utf-8-sig') for path in active_paths)
for old in mapping['identifiers']:
    assert not re.search(r'\b' + re.escape(old) + r'\b', active_text), old
for old in mapping['removed']:
    assert old not in active_text, old
    assert not list(code.rglob(old + '.cs')), old
print('PASS no removed card/power classes or old identifiers in active C# source/tests')

states = (code / 'Powers/HaoStatStatePower.cs').read_text(encoding='utf-8')
helper = (code / 'Extensions/HaoStateExtensions.cs').read_text(encoding='utf-8')
assert 'abstract class HaoStatStatePower' in states
assert 'PowerStackType.Single' in states and 'AfterApplied' in states and 'AfterRemoved' in states
assert 'restored = (power?.Amount ?? 0) - contribution' in states
assert 'data.Strength = data.Dexterity = 0' in states
assert 'ModifyDamageAdditive' not in states and 'ModifyBlockAdditive' not in states
assert helper.index('await PowerCmd.Remove<HiddenStatePower>') < helper.index('await PowerCmd.Apply<HiddenStatePower>')
assert helper.index('await PowerCmd.Remove<OpenHaoStatePower>') < helper.index('await PowerCmd.Apply<OpenHaoStatePower>')
assert 'next == before' in helper and 'DualLifePower' in helper
for name, strength, dexterity in [('HiddenStatePower', -2, 3), ('OpenHaoStatePower', 2, -1)]:
    text = power_models[name][2]
    assert f'StrengthChange => {strength};' in text and f'DexterityChange => {dexterity};' in text
print('PASS native stats: hidden -2 Strength/+3 Dexterity; open +2 Strength/-1 Dexterity; old contribution rollback wired')

rhythm = power_models['RhythmPower'][2]
assert 'AfterPowerAmountChanged' in rhythm and 'power is not HaoPower' in rhythm
assert 'power.Owner != Owner' in rhythm and '(int)amount == 0' in rhythm
assert 'state.CurrentSide != Owner.Side' in rhythm and 'ValueProp.Unpowered' in rhythm
assert 'AfterCardPlayed' not in rhythm and 'TriggerLimit' not in rhythm
card = cards['Rhythm'][2]
assert 'PowerVar<RhythmPower>(2)' in card and 'UpgradeValueBy(1)' in card
assert 'base(1, CardType.Power, CardRarity.Uncommon, TargetType.Self)' in card
assert 'AfterSideTurnEnd' not in rhythm
sequence = (code / 'Extensions/SequenceHistoryExtensions.cs').read_text(encoding='utf-8')
assert set(re.findall(r'public static [^\n]+? (\w+)\(', sequence)) == {'PriorPlays', 'BeforeCurrentPlay', 'PreviousTypeIs'}
print('PASS Rhythm is persistent 1c Uncommon Power, Hao-change reward 2/3 on own turn; PressForward uses the accepted release tuning')

# Effect fingerprints use the accepted release source: PressForward tuning from commit 772f145 and current ChatHistory costs 2/1; no gameplay changes are made by this check.
for name, expected_hash in read(docs / 'rename-effect-baselines.json').items():
    normalized = cards[name][2].encode('utf-8')
    assert hashlib.sha256(normalized).hexdigest() == expected_hash, name
assert not (code / 'Extensions/SupportCardExtensions.cs').exists()
assert not any(word in active_text for word in ['CountOtherHandCards', 'HasOtherHandSkill'])
print('PASS accepted release fingerprints for PressForward/KeyboardWarrior/ChatHistory; deleted support helpers have no callers')
print('RESULT source-only validation passed; no compile, debug, DLL execution or game deployment')

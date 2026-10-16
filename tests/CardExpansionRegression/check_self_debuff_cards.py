"""Check the self-debuff expansion against source and local native API; never loads a DLL."""
from pathlib import Path
from collections import Counter
import argparse
import hashlib
import json
import re

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('root', nargs='?', type=Path, default=Path(__file__).resolve().parents[2])
parser.add_argument('--overlay', type=Path, help='Read prepared files over the existing repository.')
parser.add_argument('--native-root', type=Path)
parser.add_argument('--report', type=Path)
args = parser.parse_args()
root = args.root.resolve()
native_root = args.native_root or root.parents[1] / 'decompiled-pck/recovered/src/Core'
code = Path('MySts2Mod/MySts2ModCode')
resources = Path('MySts2Mod/MySts2Mod')
docs = Path('docs/card-expansion-54')


def raw(relative):
    staged = args.overlay / relative if args.overlay else None
    return (staged if staged and staged.is_file() else root / relative).read_bytes()


def text(relative):
    return raw(relative).decode('utf-8-sig')


def unique_pairs(items):
    data = {}
    for key, value in items:
        assert key not in data, 'Duplicate JSON key: ' + key
        data[key] = value
    return data


def load(relative):
    return json.loads(text(relative), object_pairs_hook=unique_pairs)


def slug(name):
    return 'MYSTS2MOD-' + re.sub(r'(?<=[A-Za-z0-9])(?=[A-Z])', '_', name).upper()


catalog = load(docs / '2026-10-15-self-debuff-cards.json')
assert catalog['regularDebuffs'] == ['FrailPower', 'WeakPower', 'VulnerablePower']
assert len(catalog['cards']) == 11 and len(catalog['powers']) == 2

models = {}
for folder in ('Cards', 'Powers'):
    files = {p.name for p in (root / code / folder).glob('*.cs')}
    if args.overlay:
        files |= {p.name for p in (args.overlay / code / folder).glob('*.cs')}
    for filename in sorted(files):
        relative = code / folder / filename
        source = text(relative)
        for name, base in re.findall(r'public\s+(?:sealed\s+)?class\s+(\w+)\s*:\s*(MySts2ModCard|MySts2ModPower|HaoStatStatePower)\b', source):
            assert filename == name + '.cs' and name not in models, name
            models[name] = (relative, source, base)
assert sum(base == 'MySts2ModCard' for _, _, base in models.values()) == 88
assert sum(base != 'MySts2ModCard' for _, _, base in models.values()) == 30
inventory = load(docs / 'source-inventory.json')
assert {c['className'] for c in inventory['cards']} == {n for n, (_, _, b) in models.items() if b == 'MySts2ModCard'}
assert {p['className'] for p in inventory['powers']} == {n for n, (_, _, b) in models.items() if b != 'MySts2ModCard'}
stats = load(docs / 'source-statistics.json')
assert (stats['cardCount'], stats['characterCards'], stats['colorlessTokens'], stats['powerCount']) == (88, 86, 2, 30)
for key, field in [('types', 'type'), ('rarities', 'rarity')]:
    assert stats[key] == Counter(c['before'][field] for c in inventory['cards'])
art = load(docs / 'card-art-map.json')
assert {c['className'] for c in art['cards']} == {c['className'] for c in inventory['cards']}

methods = re.compile(r'(?:public|protected)\s+(?:static\s+)?(?:virtual|override)\s+(?:async\s+)?(\w+(?:<[^>]*>)?\??)\s+(\w+)\s*\(([^)]*)\)', re.S)


def signature(return_type, arguments):
    return (return_type.replace('?', ''), tuple(
        re.sub(r'\s+', ' ', arg.strip()).split(' ')[0].replace('?', '')
        for arg in arguments.split(',') if arg.strip()))


native_methods = {}
for filename in ('AbstractModel.cs', 'CardModel.cs', 'PowerModel.cs'):
    native = (native_root / 'Models' / filename).read_text(encoding='utf-8-sig')
    for return_type, name, arguments in methods.findall(native):
        native_methods.setdefault(name, set()).add(signature(return_type, arguments))

aliases = {'Weak': 'WeakPower', 'Vulnerable': 'VulnerablePower',
           'Strength': 'StrengthPower', 'Dexterity': 'DexterityPower'}
checked = []
for table, items in [('cards', catalog['cards']), ('powers', catalog['powers'])]:
    translations = {lang: load(resources / 'localization' / lang / (table + '.json')) for lang in ('zhs', 'eng')}
    for item in items:
        name = item['className']
        relative, source, _ = models[name]
        assert slug(name) == item.get('id', slug(name))
        variables = {}
        for power, amount in re.findall(r'new\s+PowerVar<(\w+)>\((-?\d+)\)', source):
            variables[power] = int(amount)
        for custom, amount in re.findall(r'new\s+IntVar\("(\w+)",\s*(-?\d+)\)', source):
            variables[custom] = int(amount)
        for var, amount in re.findall(r'new\s+(Damage|Block|Cards|Energy|Heal)Var\((-?\d+)', source):
            variables[var] = int(amount)

        if table == 'cards':
            assert '[Pool(typeof(HaoCardPool))]' in source
            ctor = re.search(r'public\s+' + name + r'\(\)\s*:\s*base\(([^)]+)\)', source)
            assert ctor, name
            expected = item['before']
            assert [v.strip() for v in ctor.group(1).split(',')] == [str(expected['cost']),
                'CardType.' + expected['type'], 'CardRarity.' + expected['rarity'], 'TargetType.' + expected['target']], name
            assert variables == expected['variables'], (name, variables, expected['variables'])
            upgraded = dict(variables)
            upgrade_source = source[source.index('OnUpgrade'): ] if 'OnUpgrade' in source else ''
            for alias, delta in re.findall(r'DynamicVars\.(\w+)\.UpgradeValueBy\((-?\d+)\)', upgrade_source):
                upgraded[aliases.get(alias, alias)] += int(delta)
            for key, delta in re.findall(r'DynamicVars\["(\w+)"\]\.UpgradeValueBy\((-?\d+)\)', upgrade_source):
                upgraded[key] += int(delta)
            assert upgraded == item['after']['variables'], (name, 'upgraded variables')
            upgraded_cost = expected['cost'] + sum(map(int, re.findall(r'EnergyCost\.UpgradeBy\((-?\d+)\)', upgrade_source)))
            assert upgraded_cost == item['after']['cost'], name
            keywords = re.search(r'CanonicalKeywords\s*=>\s*\[([^]]*)\]', source)
            before_keywords = re.findall(r'CardKeyword\.(\w+)', keywords.group(1)) if keywords else []
            assert sorted(before_keywords) == sorted(expected['keywords']), name
            after_keywords = set(before_keywords)
            after_keywords.update(re.findall(r'(?<!\.)AddKeyword\(CardKeyword\.(\w+)\)', upgrade_source))
            after_keywords.difference_update(re.findall(r'RemoveKeyword\(CardKeyword\.(\w+)\)', upgrade_source))
            assert after_keywords == set(item['after']['keywords']), name
            recorded = next(c for c in inventory['cards'] if c['className'] == name)
            assert recorded['before'] == expected and recorded['after'] == item['after']
            art_record = next(c for c in art['cards'] if c['className'] == name)
            assert art_record['kind'] == 'normal' and art_record['portraitPath'] == f'res://CodeFarmer/images/card_portraits/{name}.png'
        else:
            assert variables == item['variables'], name

        known = set(variables) | {'Amount', 'IfUpgraded', 'InCombat', 'OnTable', 'energyPrefix'}
        for lang in ('zhs', 'eng'):
            for field in ('title', 'description') + (('smartDescription',) if table == 'powers' else ()):
                value = translations[lang][slug(name) + '.' + field]
                assert isinstance(value, str) and value.strip(), (lang, name, field)
                used = set(re.findall(r'\{([A-Za-z][A-Za-z0-9_]*)[}:]', value))
                assert used <= known, (lang, name, field, used - known)
                if table == 'powers' and field == 'description':
                    # Canonical power hover tips only bind native dumb variables, not CanonicalVars.
                    assert used <= {'Amount', 'energyPrefix'}, (name, 'canonical power description')
            if 'SelectionScreenPrompt' in source:
                assert translations[lang][slug(name) + '.selectionScreenPrompt']

        for power in set(re.findall(r'(?:PowerCmd\.Apply|HoverTipFactory\.FromPower)<(\w+)>', source)):
            native_file = native_root / 'Models/Powers' / (power + '.cs')
            assert power in models or native_file.is_file(), (name, 'unregistered power', power)
            if native_file.is_file():
                native_source = native_file.read_text(encoding='utf-8-sig')
                assert not re.search(r'abstract\s+class\s+' + power + r'\b', native_source), (name, power)
        for return_type, method, arguments in methods.findall(source):
            assert signature(return_type, arguments) in native_methods.get(method, set()), (name, method)

        stripped = re.sub(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"', '', source, flags=re.S)
        stack = []
        closing = {')': '(', ']': '[', '}': '{'}
        for char in stripped:
            if char in '([{': stack.append(char)
            elif char in closing: assert stack and stack.pop() == closing[char], (name, 'delimiter')
        assert not stack, name
        checked.append(dict(path=relative.as_posix(), sha256=hashlib.sha256(raw(relative)).hexdigest()))

# Cross-card boundaries which have caused regressions in this project.
helper = text(code / 'Extensions/RegularDebuffs.cs')
assert 'power is FrailPower or WeakPower or VulnerablePower' in helper
assert 'power.Amount > 0' in helper and '.Sum(power => power.Amount)' in helper
assert '1 + RegularDebuffs.CountKinds' in models['Tilted'][1]
assert 'RegularDebuffs.CountLayers' in models['AmIShiNong'][1]
assert 'Apply<FlexPotionPower>' in models['AmIShiNong'][1] and 'Apply<AnticipatePower>' in models['AmIShiNong'][1]
assert 'if (IsUpgraded) selected.AddKeyword(CardKeyword.Retain)' in models['Enlightened'][1]
assert 'CardKeyword.Retain' not in models['EvenWorse'][1]
lining = models['SilverLiningPower'][1]
assert 'PowerInstanceType.Instanced' in lining and 'HashSet<CardPlay>' in lining
assert 'BeforePowerAmountChanged' in lining and 'amount <= 0' in lining
assert 'target != Owner || applier != Owner' in lining
assert 'cardPlay.Player != Owner.Player' in lining and 'participants.Contains(Owner)' in lining
assert 'CombatManager.Instance.IsPartOfPlayerTurn(cardPlay.Player)' in lining
assert 'data.QualifyingPlays.Remove(cardPlay)' in lining and 'data.UsesThisTurn >= Amount' in lining
assert lining.index('data.UsesThisTurn++;') < lining.index('await CardPileCmd.Draw')
assert 'ModifyHandDraw' in models['NoEscapePower'][1] and 'AfterEnergyReset' in models['NoEscapePower'][1]
assert 'PowerCmd.Apply<' not in models['NoEscapePower'][1]  # Debuffs are only applied by the card.
assert 'AddThisTurnOrUntilPlayed' in models['FullSurrender'][1] and '!card.EnergyCost.CostsX' in models['FullSurrender'][1]
checked.append(dict(path=(code / 'Extensions/RegularDebuffs.cs').as_posix(),
                    sha256=hashlib.sha256(raw(code / 'Extensions/RegularDebuffs.cs')).hexdigest()))

checks = ['88 card / 30 custom power registrations and complete source inventories',
          '11 card constructors, base/upgrade values, keywords and costs',
          'Chinese/English text, prompts, energy icon placeholders and canonical power hover tips',
          'Native override signatures and concrete native/custom power references',
          'Per-card self-debuff tracking, owner guards, quota reservation and own-turn reset',
          'Debuff kind/layer distinction, upgraded target Retain, native temporary stats and cost reduction']
report = dict(status='PASS', date='2026-10-15', compiled=False, runtimeTesting=False,
              scope='11 new cards, 2 new powers and shared regular-debuff classification',
              counts=dict(cards=88, characterCards=86, colorlessTokens=2, customPowers=30),
              checks=checks, files=checked,
              limitations=['Static checks do not execute combat hooks or validate gameplay interactions.',
                           'Historical source inventory drift remains; the full source checker can still fail on ChatHistory.'])
if args.report:
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
for check in checks:
    print('PASS: ' + check)
print('No compilation, DLL execution, game launch or installation was performed.')

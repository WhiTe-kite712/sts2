"""Check source/localization/hover-tip bindings without game DLLs or a .NET SDK."""

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
PROJECT = ROOT / "MySts2Mod"
CODE = PROJECT / "MySts2ModCode"
errors = []


def fail(message):
    errors.append(message)


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            fail(f"Duplicate localization key: {key}")
        result[key] = value
    return result


def slug(name):
    # StringHelper.Slugify inserts '_' before capital letters following letters/digits.
    return re.sub(r"(?<=[A-Za-z0-9])(?=[A-Z])", "_", name).upper()


def source_only(source):
    return re.sub(r"/\*.*?\*/|//[^\n]*", "", source, flags=re.S)


manifest = json.loads((PROJECT / "MySts2Mod.json").read_text(encoding="utf-8-sig"))
resource_path = PROJECT / manifest["id"] / "localization"
models = {}
for folder, table in [("Cards", "cards"), ("Powers", "powers"), ("Relics", "relics")]:
    for file in sorted((CODE / folder).glob("*.cs")):
        raw = file.read_text(encoding="utf-8-sig")
        source = source_only(raw)
        match = re.search(r"public\s+(?:sealed\s+)?class\s+(\w+)\s*:", source)
        if not match:
            continue
        name = match[1]
        namespace = re.search(r"namespace\s+([\w.]+)", source)[1]
        custom_id = re.search(r'\[CustomID\("([^"]+)"\)\]', source)
        entry = custom_id[1] if custom_id else namespace.split(".")[0].upper() + "-" + slug(name)
        models[name] = (table, entry, source, file)

languages = sorted(path.name for path in resource_path.iterdir() if path.is_dir())
tables = {}
for language in languages:
    for table in ["cards", "powers", "relics"]:
        file = resource_path / language / (table + ".json")
        try:
            translations = json.loads(file.read_text(encoding="utf-8-sig"), object_pairs_hook=unique_object)
        except (OSError, ValueError) as exc:
            fail(f"{file.relative_to(ROOT)}: {exc}")
            translations = {}
        tables[language, table] = translations
        for key, value in translations.items():
            if not isinstance(value, str) or not value.strip():
                fail(f"{language}/{table}: empty/non-string text at {key}")
    for table in ["cards", "powers", "relics"]:
        if language != languages[0]:
            missing = tables[languages[0], table].keys() - tables[language, table].keys()
            extra = tables[language, table].keys() - tables[languages[0], table].keys()
            if missing or extra:
                fail(f"{language}/{table}: language key mismatch; missing={sorted(missing)}, extra={sorted(extra)}")

game_powers = ROOT.parent / "decompiled-csharp" / "MegaCrit.Sts2.Core.Models.Powers"
tip_count = 0
for name, (table, entry, source, file) in models.items():
    label = str(file.relative_to(ROOT))
    tips_match = re.search(r"ExtraHoverTips\s*=>\s*\[(.*?)\];", source, re.S)
    tips_source = tips_match[1] if tips_match else ""
    references = re.findall(r"HoverTipFactory\.FromPower<(\w+)>", tips_source)
    tip_count += len(references)
    if len(references) != len(set(references)):
        fail(f"{label}: duplicate power hover tips")
    if name in references:
        fail(f"{label}: self-referencing power hover tip")
    for target in references:
        if target not in models and not (game_powers / (target + ".cs")).is_file():
            fail(f"{label}: unknown power hover tip {target}")
        elif target in models and models[target][0] != "powers":
            fail(f"{label}: hover tip target {target} is not a power")

    applied = set(re.findall(r"(?:PowerCmd\.Apply|CommonActions\.Apply)<(\w+)>", source))
    for target in applied - set(references):
        fail(f"{label}: applies {target} without linking its hover tip")
    # Resource helpers don't directly call PowerCmd in the card class.
    uses_hao = any(re.search(r"豪意|\bHao\b", tables[language, table].get(entry + ".description", "")) for language in languages)
    if uses_hao and name != "HaoPower" and "HaoPower" not in references:
        fail(f"{label}: mentions Hao without its hover tip")

    variables = set(re.findall(r'new\s+(?:\w+\.)*\w+Var(?:<\w+>)?\(\s*"([^"]+)"', source))
    variables.update(re.findall(r'new\s+PowerVar<(\w+)>\(\s*(?!")\d', source))
    variables.update(re.findall(r'new\s+(Damage|Block|Heal|Cards|Energy)Var\(\s*(?!")\d', source))
    variables.update(["IfUpgraded", "IsUpgraded", "energyPrefix"])
    if table == "powers":
        variables.update(["Amount", "singleStarIcon"])
    for language in languages:
        translations = tables[language, table]
        for suffix in ["title", "description"]:
            if entry + "." + suffix not in translations:
                fail(f"{language}/{table}: missing {entry}.{suffix}")
        for suffix in ["description", "smartDescription"]:
            text = translations.get(entry + "." + suffix, "")
            placeholders = set(re.findall(r"\{([A-Za-z_]\w*)(?=[:}])", text))
            for variable in placeholders - variables:
                fail(f"{language}/{table}: {entry}.{suffix} has unbound variable {variable}")
        for target_table, key in re.findall(r'new\s+LocString\("([^"]+)",\s*"([^"]+)"\)', source):
            if target_table in ["cards", "powers", "relics"] and key not in tables[language, target_table]:
                fail(f"{language}/{target_table}: source refers to missing key {key}")

if errors:
    for error in errors:
        print("FAIL:", error)
    print(f"{len(errors)} error(s)")
    sys.exit(1)
counts = {table: sum(model[0] == table for model in models.values()) for table in ["cards", "powers", "relics"]}
print(f"PASS: {counts}, languages={languages}, power hover-tip links={tip_count}")
print("Static binding checks only; C# compilation, text semantics, formatting and in-game UI still require runtime validation.")

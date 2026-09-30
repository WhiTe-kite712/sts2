# Localization and hover-tip bindings

Run from the repository root with Python 3 (standard library only):

```powershell
python tests/LocalizationLinks/check.py
```

The script checks concrete card, power and relic models against all supplied
languages: model ID prefixes and title/description keys, duplicate or empty JSON
entries, matching language key sets, description variables, explicitly used
LocString keys, and hover-tip targets. It also checks that powers applied by a
model and Hao mentioned in its description have corresponding hover tips.

To validate vanilla power targets, the supplied game source must be at
`../decompiled-csharp/MegaCrit.Sts2.Core.Models.Powers` relative to the repository.
This check does not need game DLLs, a .NET SDK or a running game.

These are static binding checks. They do not compile C#, evaluate SmartFormat,
prove that prose matches combat behavior, or validate Godot resource loading.

After building and packing the mod, check both Chinese and English in the game:

- Hover Hao Strike, Conflict and Fire in Soul: Hao, Vulnerable and Frail should
  show the corresponding explanations.
- Hover Genius and Shanghai Student, including upgrade previews: draw/block
  values in the card description should update, and the related powers should
  appear next to the card.
- Hover Unrepentant and Magnificent Hao: both stages should have explanations.
- Hover Math Prince: Hao and Strength explanations should appear; in combat,
  its temporary Strength power should also explain Strength.
- Hover the starting Hao relic and the related powers in combat: Hao should
  have an explanation. Spark should not display unrelated Hao tips.

Vanilla `FromPower` uses the generic power description. The current power amount
is displayed through `smartDescription` on the applied power in combat; the card's
own dynamic description continues to display its base/upgraded values.

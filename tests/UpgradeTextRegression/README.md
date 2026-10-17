# Upgrade text regression

Exercises the game's original `ShowIfUpgradedFormatter` with the mod's authored
Chinese and English descriptions. Checks normal, upgraded and green upgrade
preview output, nested dynamic variables, both upgrade syntaxes, idempotence,
and isolation from native cards, other mods and power descriptions.

```powershell
dotnet run --project tests/UpgradeTextRegression/Harness.csproj
```

The default run also patches the real `LocString.GetRawText` with Harmony. Use
the .NET 9 runtime matching the game for this mode. The game's bundled Harmony
does not support a console harness rolling forward to .NET 10.

If only .NET 10 is available, the formatter can still be checked without
installing a hook in the console process:

```powershell
dotnet run --project tests/UpgradeTextRegression/Harness.csproj -- --formatter-only
```

This mode invokes the same compatibility function and the native game
formatter, and explicitly reports that it did not exercise the Harmony hook.
An optional positional argument overrides the localization directory.

# GeniusPower regression checks

This console harness invokes the compiled mod's actual GeniusPower hooks against
the installed game and BaseLib DLLs. It does not include a copied implementation
of GeniusPower and does not start Godot or change the game installation.

Requirements: .NET 9 or newer SDK, built MySts2Mod DLL, installed Slay the Spire 2,
and compatible BaseLib DLL (verified against 3.4.7).

In PowerShell, run from the repository root, replacing the paths for your installation:

```powershell
$gameData = 'E:\steam\steamapps\common\Slay the Spire 2\data_sts2_windows_x86_64'
$baseLib = Join-Path $env:USERPROFILE '.nuget\packages\alchyr.sts2.baselib\3.4.7\lib\net9.0\BaseLib.dll'
dotnet run --project .\tests\GeniusPowerRegression\Harness.csproj "-p:Sts2DataDir=$gameData" "-p:BaseLibDll=$baseLib"
```

The default mod DLL is `MySts2Mod/.godot/mono/temp/bin/Debug/MySts2Mod.dll`.
Override it with `-p:ModDll=<absolute DLL path>` if needed. Rebuild the mod before
running to avoid testing stale output. The process exits with code 1 if any check
fails.

The checks cover neutral/armed multipliers, repeated preview calculations,
consumption only after positive damage settles, fully blocked hits, zero damage,
multi-hit behavior, damage to other targets, self/friendly/no-dealer cases,
own versus teammates' Hao loss, Hao gains, unrelated powers, zero remaining Hao,
and the original eligibility of enemy non-move/unpowered damage.
One integration check also invokes the game's actual Hook.ModifyDamage pipeline
with a minimal IRunState listener proxy to confirm that base damage 10 becomes 20
while armed and returns to 10 after settlement.

Fixtures use uninitialized real game creatures/powers and set only the owner,
side, and mutability fields that these hooks inspect. The zero-Hao check invokes
the hook with the power's final amount of zero; this harness does not execute
PowerCmd.ModifyAmount's full combat pipeline. Its zero-removal event ordering was
separately verified from the installed game DLL's IL. In-game validation of UI,
animation, and combat orchestration is still required.

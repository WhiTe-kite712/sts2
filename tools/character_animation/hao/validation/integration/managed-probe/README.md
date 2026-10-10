# Isolated compiled-factory probe

This folder is an isolated Godot 4.5.1 C# project. It does not contain the game's autoloads or main scene and never calls MainFile.Initialize, NGame, NCreature, ModelDb or SaveManager.

## Actual result of this attempt

- Offline SDK restore succeeded from the existing local NuGet cache.
- The C# probe compiled for net9.0 / Godot.NET.Sdk 4.5.1 with zero errors and zero warnings.
- After the parent supplied the new compiled mod DLL and PCK, one hidden, headless exported-game-host attempt was made.
- The host returned before the 25-second deadline without generating factory-result.json.
- Its log reported "Could not load global script cache"; this is insufficient to identify a precise managed-loader root cause.
- The managed factory probe did not provide evidence of entry or execution. Actual CodingFarmerCombatVisuals/NCreatureVisuals/CreatureAnimator runtime playback is therefore UNVERIFIED.
- No second host attempt was made. The separately running native GDScript PCK/resource probe provides a different level of evidence and must not be described as a real C# factory test.

## Prepared genuine C# checks

IntegrationProbe.cs reflects the compiled internal type MySts2Mod.MySts2ModCode.Character.CodingFarmerCombatVisuals and invokes its public static Create and CreateAnimator methods. It checks:

1. Actual returned type is NCreatureVisuals; it is added to the active SceneTree and has completed its real _Ready.
2. Unique %Visuals is a native SpineSprite, with %Bounds/%FormVfx Control and %IntentPos/%CenterPos Marker2D.
3. HasSpineAnimation is true without forcing or changing that property, and the actual MegaSprite native animation state becomes ready.
4. idle_loop, attack, cast, hurt and die are present.
5. Actual game CreatureAnimator exposes Attack/Cast/PowerUp/Idle.
6. Attack plays attack; Cast and PowerUp play cast; real track time advances during SceneTree frames and actions return to idle_loop.
7. Transient native wrapper objects are disposed on the Godot main thread and the visuals/controller/animator are retained strongly.

The C# SDK GodotSharp and game GodotSharp both identify as version 4.5.1.0. No standalone Godot mono editor executable was found in the inspected local Spire2Mod/tools folder; efficiently bootstrapping this isolated compiled script through the exported MegaDot host remains unresolved.

## Files and isolation

- run_probe.ps1 restores/builds locally, creates a minimal probe pack, then starts the supplied engine hidden and headless with --path plus --main-pack.
- prepare_probe.py embeds only the isolated project/scene/probe, its own compiled assembly variants, a Spine extension descriptor, and the explicit mod configuration.
- project.godot has no gameplay autoloads, uses an isolated custom user directory, sets sentry/options/auto_init=false and has an empty Sentry DSN.
- During launch, APPDATA and LOCALAPPDATA are redirected into this folder's runtime/AppData and DOTNET_BUNDLE_EXTRACT_BASE_DIR is redirected into runtime/bundle; previous values are restored afterwards.
- The original game installation and mod project were only read. No game files were copied or edited.
- factory-host-result.json describes host bootstrap evidence; it is not a fabricated factory-result.json.
- factory-probe.log, factory-probe.stdout and factory-probe.stderr are the actual launch outputs.

## Future reproduction

Use run_probe.ps1 only when a suitable C#-capable host is available and the mod DLL/PCK are deliberately supplied. The defaults point to the parent-confirmed build under the sts2 checkout and the existing exported game executable.

A normal Godot mono editor/runtime host may require an isolated copy of the compatible Spine native library and matching DLL dependencies. Do not restore gameplay autoloads to make the test start; that would run save migration and other game startup work outside this probe's scope.


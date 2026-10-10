Native validation passed on 2026-10-09 at 00:11:13 local time. Both hidden headless
processes exited successfully, and their stderr logs were empty. The host was the
installed game's MegaDot 4.5.1-m.14 with its existing Spine extension; no gameplay
scenes, autoloads, save initialization, mod deployment, or mod repository edits
were involved.

- Spine source version: 4.2.43; 22 bones.
- Atlas decoded successfully, then serialized as a native Godot texture.
- Attack: 1.0 seconds; eight poses; actual local motion in the main arm/forearm/hand.
- Cast: 1.399999976 seconds; eight poses; actual local motion in off arm/forearm and main hand.
- Weighted cape: 77 vertices and 120 triangles in the source; the native cape slot
  contains the expected cape attachment. No deform timelines are declared, so
  bone-weighted cape motion does not require a changing slot-deform array.
- `res://runtime/hao.tres` loaded through the real ResourceLoader and played in a
  native SpineSprite. The loaded atlas texture's decoded RGBA pixels exactly
  match the current source PNG.
- Source hashes matched the packaged snapshot and stayed unchanged during the run.
- Sword-strike direction: the native 0.25s → 0.4s samples move the weapon
  +31.1651 in X and +10.9459 in Y, and move the sword hand +12.8653 in Y.
  With the game's Y-down coordinates, this verifies the requested forward/down
  motion. Exact before/after vectors are in direction-check.json.
- Main elbow direction: all six active attack samples place the
  `forearm_main` elbow origin screen-right of the `upper_arm_main` shoulder,
  with a minimum X gap of +27.6281. The initial/final sampled poses also remain
  right-sided. Exact native positions are in elbow-right-check.json.

| Source | SHA256 |
|---|---|
| hao.spine-json | 9279a94700681ce560b5e9c1464ee4fdd1160c9907223ade32bd04c719bcc0ba |
| hao.atlas | a56eafd2cdf6d37848d5c904698ed8add771365fb6ad4cc01c02929b71b20b20 |
| hao.png | 17fc494c3985c2b1d4553b20374fa5365982b033277fe35fe5b1a7b34e284ef7 |

Run `validate.ps1` with the available Python executable to reproduce the test.
It creates the isolated packs, saves the native texture, repacks the resource
wrappers, and executes `probe.gd`. See README.md for the command. Re-run after
changing any source asset; this report applies only to the hashes above.

The detailed measurements are in validation.json. Illustration quality and
visual preview correctness are separate from this native compatibility test.

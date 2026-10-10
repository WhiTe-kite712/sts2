The final compiled MySts2Mod.pck passed an independent native MegaDot/Spine probe.
The probe mounts the actual 57,213,953-byte build output and loads
`res://MySts2Mod/animations/coding_farmer/hao.tres` through ResourceLoader.

- PCK SHA256: `7037981b8ef314f81ffdcf4b852717d619b068bc3ac22f67ec7d8773d8d225d7`.
- Source Spine 4.2.43; 22 bones; native cape attachment present.
- PckPacker-generated PNG import remap resolves to a present `.ctex`, loaded as
  CompressedTexture2D at 2048×2048. Visible RGBA pixels and alpha match the source.
  2,836,420 fully transparent pixels have normalized hidden RGB; full RGBA byte
  equality is therefore false, while visible mismatches are zero.
- Native playback/durations: attack 1.0s, cast 1.399999976s, idle_loop 1.0s,
  hurt 0.180000007s, die 1.5s.
- At the forearm-raise peak (0.266667s), native local forearm angle is
  45.886688°, matching source setup 5.88669° + timeline addition 40°.
  Native world rotation changes are upper arm −55.176508° and forearm
  −95.176510°: the forearm adds 40° beyond the upper arm.
- The mirrored Godot node layout resolves all seven unique paths to the expected
  native classes with the correct owner, including Visuals=SpineSprite,
  Bounds/FormVfx=Control, and CenterPos/IntentPos/OrbPos/TalkPos=Marker2D.
- Probe exit code was zero; stderr was empty.

The node layout is a GDScript fixture mirroring the inspected factory, with a
plain Node2D root. It does not invoke the actual managed NCreatureVisuals factory
or CreatureAnimator; the separate exported-host C# probe did not start its
managed test. These results establish actual packaged Spine resources, native
playback and the mirrored node contract, without claiming a completed in-game
C# factory test.

Detailed evidence is in built_pack_result.json. Reproduce with run_built_probe.ps1;
the probe code and its temporary data stay in this Codex workspace. It does not
edit the mod repository, game files, SDK, or animation source.

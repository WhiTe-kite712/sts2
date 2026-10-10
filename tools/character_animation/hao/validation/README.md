# Native Spine validation

Run only after `../source/hao.spine-json`, `hao.atlas`, and `hao.png` are finalized.
This project has no gameplay scenes, autoloads, game C# assembly, or save setup.
It loads the installed game's MegaDot host and Spine extension in hidden headless
processes. Environment data and logs stay in this validation directory.

```powershell
& '.\validate.ps1' -Python 'C:\Users\Amazi\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe'
```

`pack_probe.py` prepares isolated probe/data packs and the runtime wrappers.
`texture.gd` serializes the atlas image as a genuine Godot ImageTexture resource.
The second packaging step remaps `source/hao.png` to that native texture, enabling
real ResourceLoader loading of `runtime/hao.tres` through `.spjson` and `.spatlas`.

`probe.gd` verifies source Spine 4.2.43, atlas decoding, attack/cast durations,
native skeletal movement, real ResourceLoader wrappers, and stable source SHA256
hashes. The ResourceLoader texture must also match the source PNG's decoded RGBA
pixels, so a stale serialized texture cannot satisfy the wrapper check.
`check_direction.py` reads the native attack samples at 0.25s and 0.4s: the sword
must move forward (+X) and down (+Y), and the sword hand must move down (+Y).
Its measurements and hash linkage are saved as `direction-check.json`.
`check_elbow.py` compares the native `forearm_main` elbow origin with the
`upper_arm_main` shoulder origin at every active attack sample. It requires the
elbow to be screen-right (+X) and saves all eight measured poses, including the
initial/final poses, as `elbow-right-check.json`.
If default-skin mesh attachments exist, it checks corresponding native
setup attachments by name. If deform timelines exist, it checks actual native
slot deform-array changes at the eight sampled poses. Bone-weighted meshes do not
require deform timelines, so their report records native attachment loading and
skeletal motion without claiming an exposed mesh RTTI API.

Binding evidence:

- Local game C# bindings: `decompiled-pck/recovered/src/Core/Bindings/MegaSpine`.
- [SpineSlot 4.2](https://raw.githubusercontent.com/EsotericSoftware/spine-runtimes/4.2/spine-godot/spine_godot/SpineSlot.cpp): native attachment and deform access.
- [SpineAttachment 4.2](https://raw.githubusercontent.com/EsotericSoftware/spine-runtimes/4.2/spine-godot/spine_godot/SpineAttachment.cpp): attachment name access.
- [SkeletonJson 4.2](https://raw.githubusercontent.com/EsotericSoftware/spine-runtimes/4.2/spine-cpp/spine-cpp/src/spine/SkeletonJson.cpp): version-specific JSON schema and attachment deform timelines.

The main result is `validation.json`. Successful exit does not assess illustration
quality; visual QA belongs to the package previews. Revalidate after changing any
source asset. The mod repository is not modified by this workflow.

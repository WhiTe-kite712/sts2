extends SceneTree

# ClassDB instantiation is deliberate: Spine classes are registered only after
# the isolated probe explicitly loads the game's GDExtension. No game autoloads.
# Binding references and the exact 4.2 sources are documented in README.md.

var _config: Dictionary = {}
var _result: Dictionary = {"ok": false, "errors": [], "animations": []}
var _result_path: String = ""


func _initialize() -> void:
    call_deferred("_run")


func _run() -> void:
    var manifest: Variant = JSON.parse_string(FileAccess.get_file_as_string("res://manifest.json"))
    if not manifest is Dictionary:
        push_error("The isolated probe manifest is missing or invalid")
        quit(3)
        return
    _config = manifest
    _result_path = String(_config["result"])
    _result["engine"] = Engine.get_version_info()
    _result["source"] = {"skeleton": _config["skeleton"], "atlas": _config["atlas"]}
    _result["runtime_dll"] = _config["spine_dll"]
    _result["started_at"] = Time.get_datetime_string_from_system()
    _result["source_sha256"] = _source_hashes()
    if _result["source_sha256"] != _config["packed_source_sha256"]:
        _abort("Source assets changed after the runtime pack was built; rebuild the probe")
        return

    var source: Variant = JSON.parse_string(FileAccess.get_file_as_string(_config["skeleton"]))
    if not source is Dictionary:
        _abort("Skeleton source is missing or invalid JSON")
        return
    var metadata: Variant = source.get("skeleton", {})
    if not metadata is Dictionary or String(metadata.get("spine", "")) != String(_config["expected_version"]):
        _abort("Skeleton must declare Spine " + String(_config["expected_version"]))
        return
    _result["spine_source_version"] = metadata["spine"]
    var source_bones: Variant = source.get("bones", [])
    if not source_bones is Array or source_bones.is_empty():
        _abort("Skeleton has no bone definitions")
        return
    var bone_names: Array = []
    for definition: Variant in source_bones:
        if not definition is Dictionary or String(definition.get("name", "")).is_empty():
            _abort("Skeleton contains an invalid bone definition")
            return
        bone_names.append(String(definition["name"]))
    _result["bone_names"] = bone_names
    var mesh_specs: Array = _source_mesh_specs(source)
    _result["source_mesh_attachments"] = mesh_specs

    var status: int = GDExtensionManager.load_extension("res://spine_probe.gdextension")
    _result["extension_load_status"] = status
    for class_name_to_check: String in ["SpineSkeletonFileResource", "SpineAtlasResource", "SpineSkeletonDataResource", "SpineSprite"]:
        if not ClassDB.class_exists(class_name_to_check):
            _abort("Game Spine extension did not register " + class_name_to_check)
            return

    var skeleton_file: Object = ClassDB.instantiate("SpineSkeletonFileResource")
    var atlas: Object = ClassDB.instantiate("SpineAtlasResource")
    var data: Object = ClassDB.instantiate("SpineSkeletonDataResource")
    if not _require_methods(skeleton_file, ["load_from_file"], "SpineSkeletonFileResource"):
        return
    if not _require_methods(atlas, ["load_from_atlas_file", "get_textures"], "SpineAtlasResource"):
        return
    if not _require_methods(data, ["find_animation"], "SpineSkeletonDataResource"):
        return
    var skeleton_error: int = int(skeleton_file.call("load_from_file", String(_config["skeleton"])))
    _result["skeleton_load_error"] = skeleton_error
    if skeleton_error != OK:
        _abort("Native skeleton load failed: " + error_string(skeleton_error))
        return
    var atlas_error: int = int(atlas.call("load_from_atlas_file", String(_config["atlas"])))
    _result["atlas_load_error"] = atlas_error
    if atlas_error != OK:
        _abort("Native atlas load failed: " + error_string(atlas_error))
        return

    # Absolute atlas paths use Spine's Image.load_from_file -> ImageTexture
    # route; no Godot editor imports or replacement PNG loader are required.
    var textures: Variant = atlas.call("get_textures")
    if not textures is Array or textures.is_empty():
        _abort("Native atlas has no decoded page textures")
        return
    var pages: Array = []
    for texture: Variant in textures:
        if not texture is Texture2D:
            _abort("Native atlas page is not a Texture2D")
            return
        var image: Image = texture.get_image()
        if texture.get_width() <= 0 or texture.get_height() <= 0 or image == null or image.is_empty():
            _abort("Native atlas page texture did not decode")
            return
        pages.append({"width": texture.get_width(), "height": texture.get_height()})
    _result["atlas_pages"] = pages

    data.set("atlas_res", atlas)
    data.set("skeleton_file_res", skeleton_file)
    data.set("default_mix", 0.0)

    var sprite: Node = ClassDB.instantiate("SpineSprite")
    if not _require_methods(sprite, ["set_skeleton_data_res", "get_animation_state", "get_skeleton", "update_skeleton"], "SpineSprite"):
        return
    sprite.call("set_skeleton_data_res", data)
    root.add_child(sprite)
    var state: Object = null
    var skeleton: Object = null
    # The game binding explicitly documents delayed SpineSprite initialization.
    for frame_index: int in range(120):
        state = sprite.call("get_animation_state")
        skeleton = sprite.call("get_skeleton")
        if state != null and skeleton != null:
            _result["ready_after_frames"] = frame_index
            break
        await process_frame
    if state == null or skeleton == null:
        _abort("SpineSprite did not initialize within 120 process frames")
        return
    if not _require_methods(state, ["clear_tracks", "set_animation"], "SpineAnimationState"):
        return
    if not _require_methods(skeleton, ["set_to_setup_pose", "find_bone"], "SpineSkeleton"):
        return
    for bone_name: String in bone_names:
        var bone: Object = skeleton.call("find_bone", bone_name)
        if not _require_methods(bone, ["get_x", "get_y", "get_rotation", "get_scale_x", "get_scale_y", "get_world_x", "get_world_y", "get_world_rotation_x"], "SpineBone " + bone_name):
            return
    var mesh_slots: Array = []
    var active_meshes: Array = []
    if not mesh_specs.is_empty():
        if not _require_methods(skeleton, ["find_slot"], "SpineSkeleton mesh lookup"):
            return
        for spec: Dictionary in mesh_specs:
            var slot: Object = skeleton.call("find_slot", String(spec["slot"]))
            if not _require_methods(slot, ["get_attachment", "get_deform"], "SpineSlot " + String(spec["slot"])):
                return
            var attachment: Object = slot.call("get_attachment")
            if not _require_methods(attachment, ["get_attachment_name"], "SpineAttachment " + String(spec["slot"])):
                return
            var actual_name: String = String(attachment.call("get_attachment_name"))
            if actual_name != String(spec["attachment"]):
                _abort("Native setup attachment mismatch for mesh slot " + String(spec["slot"]))
                return
            active_meshes.append({"slot": spec["slot"], "attachment": actual_name})
            if not mesh_slots.has(spec["slot"]):
                mesh_slots.append(spec["slot"])
    _result["native_mesh_setup_attachments"] = active_meshes

    var motion_bones: Array = _motion_bone_names(bone_names)
    if motion_bones.is_empty():
        _abort("No arm/hand/weapon motion bones found; pass --motion-bones to the pack script")
        return
    _result["motion_bones"] = motion_bones
    for animation_name: String in _config["animations"]:
        var animation: Object = data.call("find_animation", animation_name)
        if not _require_methods(animation, ["get_duration"], "SpineAnimation " + animation_name):
            return
        var duration: float = float(animation.call("get_duration"))
        if duration <= 0.0 or not is_finite(duration):
            _abort("Animation " + animation_name + " has no positive finite duration")
            return
        var expected_duration: float = float(_config["expected_durations"][animation_name])
        if absf(duration - expected_duration) > 0.001:
            _abort("Animation " + animation_name + " duration " + str(duration) + " differs from expected " + str(expected_duration))
            return
        var expected_deform_slots: Array = _source_deform_slots(source, animation_name)
        var entry: Dictionary = {"name": animation_name, "duration": duration, "samples": [], "changed_local_bones": [], "changed_world_bones": [], "expected_deform_slots": expected_deform_slots, "changed_deform_slots": []}
        var baseline: Dictionary = {}
        var baseline_deform: Dictionary = {}
        for ratio: float in _config["sample_ratios"]:
            state.call("clear_tracks")
            skeleton.call("set_to_setup_pose")
            var track: Object = state.call("set_animation", animation_name, false, 0)
            if track == null:
                _abort("Native set_animation failed for " + animation_name)
                return
            # This bound method performs state update/apply and the runtime's
            # correct Physics_Update world-transform update, without guessing
            # the physics enum or calling C++-only APIs.
            var sample_time: float = duration * ratio
            sprite.call("update_skeleton", sample_time)
            var pose: Dictionary = _snapshot(skeleton, bone_names)
            var deformations: Dictionary = _snapshot_deform(skeleton, mesh_slots)
            entry["samples"].append({"time": sample_time, "ratio": ratio, "bones": pose, "mesh_deform": deformations})
            if baseline.is_empty():
                baseline = pose
                baseline_deform = deformations
            else:
                for bone_name: String in bone_names:
                    if _different(pose[bone_name], baseline[bone_name], ["x", "y", "rotation", "scale_x", "scale_y"]):
                        if not entry["changed_local_bones"].has(bone_name):
                            entry["changed_local_bones"].append(bone_name)
                    if _different(pose[bone_name], baseline[bone_name], ["world_x", "world_y", "world_rotation_x"]):
                        if not entry["changed_world_bones"].has(bone_name):
                            entry["changed_world_bones"].append(bone_name)
                for slot_name: String in mesh_slots:
                    if _arrays_different(deformations[slot_name], baseline_deform[slot_name]):
                        if not entry["changed_deform_slots"].has(slot_name):
                            entry["changed_deform_slots"].append(slot_name)
        var changed_motion_bones: Array = []
        for bone_name: String in motion_bones:
            if entry["changed_local_bones"].has(bone_name):
                changed_motion_bones.append(bone_name)
        entry["changed_motion_bones"] = changed_motion_bones
        entry["ok"] = not changed_motion_bones.is_empty()
        for slot_name: String in expected_deform_slots:
            if not entry["changed_deform_slots"].has(slot_name):
                entry["ok"] = false
                _result["errors"].append(animation_name + " native deform timeline did not change mesh slot " + slot_name)
        _result["animations"].append(entry)
        if not entry["ok"]:
            _result["errors"].append(animation_name + " has no sampled local arm/hand/weapon bone motion")
        print("PASS " if entry["ok"] else "FAIL ", animation_name, " duration=", duration, " motion=", changed_motion_bones)
    if not ProjectSettings.load_resource_pack(String(_config["asset_pack"])):
        _abort("Unable to mount the isolated runtime asset PCK")
        return
    var wrapped_data: Object = ResourceLoader.load(String(_config["wrapped_data"]), "SpineSkeletonDataResource", ResourceLoader.CACHE_MODE_IGNORE)
    if not _require_methods(wrapped_data, ["find_animation"], "ResourceLoader SpineSkeletonDataResource"):
        return
    var wrapped_atlas: Object = wrapped_data.get("atlas_res")
    if not _require_methods(wrapped_atlas, ["get_textures"], "ResourceLoader SpineAtlasResource"):
        return
    var wrapped_pages: Array = []
    var source_page_image: Image = Image.load_from_file(String(_config["png"]))
    if source_page_image == null or source_page_image.is_empty():
        _abort("Cannot decode source page for runtime texture comparison")
        return
    source_page_image.convert(Image.FORMAT_RGBA8)
    for texture: Variant in wrapped_atlas.call("get_textures"):
        if not texture is Texture2D:
            _abort("Wrapped atlas page is not Texture2D")
            return
        var decoded: Image = texture.get_image()
        if decoded == null or decoded.is_empty():
            _abort("Wrapped atlas page did not decode through ResourceLoader")
            return
        decoded.convert(Image.FORMAT_RGBA8)
        if decoded.get_width() != source_page_image.get_width() or decoded.get_height() != source_page_image.get_height() or decoded.get_data() != source_page_image.get_data():
            _abort("Runtime Godot texture differs from the current source PNG; regenerate it")
            return
        wrapped_pages.append({"width": texture.get_width(), "height": texture.get_height()})
    if wrapped_pages.is_empty():
        _abort("Wrapped atlas has no page textures")
        return
    var wrapped_sprite: Node = ClassDB.instantiate("SpineSprite")
    wrapped_sprite.call("set_skeleton_data_res", wrapped_data)
    root.add_child(wrapped_sprite)
    var wrapped_state: Object = null
    var wrapped_skeleton: Object = null
    for frame_index: int in range(120):
        wrapped_state = wrapped_sprite.call("get_animation_state")
        wrapped_skeleton = wrapped_sprite.call("get_skeleton")
        if wrapped_state != null and wrapped_skeleton != null:
            break
        await process_frame
    if not _require_methods(wrapped_state, ["clear_tracks", "set_animation"], "Wrapped SpineAnimationState"):
        return
    if not _require_methods(wrapped_skeleton, ["set_to_setup_pose", "find_bone"], "Wrapped SpineSkeleton"):
        return
    var runtime_checks: Array = []
    for animation_name: String in _config["animations"]:
        var animation: Object = wrapped_data.call("find_animation", animation_name)
        if not _require_methods(animation, ["get_duration"], "Wrapped SpineAnimation " + animation_name):
            return
        var duration: float = float(animation.call("get_duration"))
        if absf(duration - float(_config["expected_durations"][animation_name])) > 0.001:
            _abort("Wrapped animation duration mismatch: " + animation_name)
            return
        wrapped_state.call("clear_tracks")
        wrapped_skeleton.call("set_to_setup_pose")
        var track: Object = wrapped_state.call("set_animation", animation_name, false, 0)
        if track == null:
            _abort("Wrapped native set_animation failed for " + animation_name)
            return
        wrapped_sprite.call("update_skeleton", 0.0)
        var before: Dictionary = _snapshot(wrapped_skeleton, motion_bones)
        wrapped_sprite.call("update_skeleton", duration * 0.4)
        var after: Dictionary = _snapshot(wrapped_skeleton, motion_bones)
        var moved: Array = []
        for bone_name: String in motion_bones:
            if _different(after[bone_name], before[bone_name], ["x", "y", "rotation", "scale_x", "scale_y"]):
                moved.append(bone_name)
        if moved.is_empty():
            _abort("Wrapped native animation has no motion: " + animation_name)
            return
        runtime_checks.append({"name": animation_name, "duration": duration, "changed_motion_bones": moved})
    _result["runtime_resource_loader"] = {"ok": true, "path": _config["wrapped_data"], "atlas_pages": wrapped_pages, "texture_pixels_match_source": true, "animations": runtime_checks}
    print("PASS ResourceLoader ", _config["wrapped_data"], " with native SpineSprite playback")
    _result["source_sha256_after"] = _source_hashes()
    if _result["source_sha256_after"] != _result["source_sha256"]:
        _result["errors"].append("Source assets changed during native validation; rerun the probe")
    _result["ok"] = _result["errors"].is_empty()
    _finish()


func _require_methods(instance: Object, methods: Array, label: String) -> bool:
    if instance == null or not is_instance_valid(instance):
        _abort(label + " is null")
        return false
    for method: String in methods:
        if not instance.has_method(method):
            _abort(label + " lacks the required verified binding " + method)
            return false
    return true


func _motion_bone_names(names: Array) -> Array:
    var selected: Array = _config.get("motion_bones", []).duplicate()
    if not selected.is_empty():
        for bone_name: String in selected:
            if not names.has(bone_name):
                _result["errors"].append("Configured motion bone does not exist: " + bone_name)
        return selected
    for bone_name: String in names:
        var lower_name: String = bone_name.to_lower()
        for token: String in ["arm", "hand", "weapon", "sword", "staff", "fist", "blade", "palm"]:
            if lower_name.contains(token):
                selected.append(bone_name)
                break
    return selected


func _snapshot(skeleton: Object, names: Array) -> Dictionary:
    var pose: Dictionary = {}
    for bone_name: String in names:
        var bone: Object = skeleton.call("find_bone", bone_name)
        pose[bone_name] = {
            "x": float(bone.call("get_x")),
            "y": float(bone.call("get_y")),
            "rotation": float(bone.call("get_rotation")),
            "scale_x": float(bone.call("get_scale_x")),
            "scale_y": float(bone.call("get_scale_y")),
            "world_x": float(bone.call("get_world_x")),
            "world_y": float(bone.call("get_world_y")),
            "world_rotation_x": float(bone.call("get_world_rotation_x")),
        }
    return pose


func _different(current: Dictionary, previous: Dictionary, fields: Array) -> bool:
    for field: String in fields:
        if absf(float(current[field]) - float(previous[field])) > 0.0001:
            return true
    return false


func _source_mesh_specs(source: Dictionary) -> Array:
    var specs: Array = []
    for skin: Dictionary in source.get("skins", []):
        if String(skin.get("name", "")) != "default":
            continue
        var attachments: Dictionary = skin.get("attachments", {})
        for slot_name: String in attachments:
            var entries: Dictionary = attachments[slot_name]
            for entry_name: String in entries:
                var definition: Dictionary = entries[entry_name]
                var attachment_type: String = String(definition.get("type", "region"))
                if attachment_type in ["mesh", "linkedmesh"]:
                    specs.append({"slot": slot_name, "attachment": definition.get("name", entry_name), "source_type": attachment_type, "uv_count": definition.get("uvs", []).size(), "triangle_index_count": definition.get("triangles", []).size(), "vertex_value_count": definition.get("vertices", []).size()})
    return specs


func _source_deform_slots(source: Dictionary, animation_name: String) -> Array:
    var result: Array = []
    var animation: Dictionary = source["animations"][animation_name]
    if animation.has("deform"):
        _result["errors"].append(animation_name + " uses obsolete root-level deform; Spine 4.2 requires attachments.<skin>.<slot>.<attachment>.deform")
    var attachments: Dictionary = animation.get("attachments", {})
    var default_skin: Dictionary = attachments.get("default", {})
    for slot_name: String in default_skin:
        var entries: Dictionary = default_skin[slot_name]
        for entry_name: String in entries:
            var timelines: Dictionary = entries[entry_name]
            if timelines.has("deform") and not result.has(slot_name):
                result.append(slot_name)
    return result


func _snapshot_deform(skeleton: Object, slot_names: Array) -> Dictionary:
    var values: Dictionary = {}
    for slot_name: String in slot_names:
        var slot: Object = skeleton.call("find_slot", slot_name)
        values[slot_name] = slot.call("get_deform")
    return values


func _arrays_different(current: Array, previous: Array) -> bool:
    if current.size() != previous.size():
        return true
    for index: int in range(current.size()):
        if absf(float(current[index]) - float(previous[index])) > 0.0001:
            return true
    return false


func _source_hashes() -> Dictionary:
    var hashes: Dictionary = {}
    for source_key: String in ["skeleton", "atlas", "png"]:
        var context: HashingContext = HashingContext.new()
        context.start(HashingContext.HASH_SHA256)
        context.update(FileAccess.get_file_as_bytes(String(_config[source_key])))
        hashes[source_key] = context.finish().hex_encode()
    return hashes


func _abort(message: String) -> void:
    _result["ok"] = false
    _result["errors"].append(message)
    push_error(message)
    _finish()


func _finish() -> void:
    _result["finished_at"] = Time.get_datetime_string_from_system()
    var output: FileAccess = FileAccess.open(_result_path, FileAccess.WRITE)
    if output == null:
        push_error("Cannot write validation JSON at " + _result_path)
        quit(3)
        return
    output.store_string(JSON.stringify(_result, "  "))
    output.close()
    print("RESULT ", "PASS" if _result["ok"] else "FAIL", " -> ", _result_path)
    quit(0 if _result["ok"] else 1)

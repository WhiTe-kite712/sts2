extends SceneTree

var config: Dictionary = {}
var result: Dictionary = {"ok": false, "errors": [], "animations": []}

func _initialize() -> void:
    call_deferred("run_checks")

func run_checks() -> void:
    config = JSON.parse_string(FileAccess.get_file_as_string("res://built_pack_manifest.json"))
    result["started_at"] = Time.get_datetime_string_from_system()
    result["engine"] = Engine.get_version_info()
    result["pack"] = config["pack"]
    result["pack_sha256"] = config["pack_sha256"]
    result["resource"] = config["resource"]
    result["source_sha256"] = config["source_sha256"]
    result["extension_load_status"] = GDExtensionManager.load_extension("res://spine_probe.gdextension")
    if not ClassDB.class_exists("SpineSprite"):
        fail("Installed Spine extension did not register SpineSprite")
        return
    if not ProjectSettings.load_resource_pack(String(config["pack"])):
        fail("Unable to mount the final compiled mod PCK")
        return
    var prefix: String = config["resource_prefix"]
    for name: String in ["hao.spjson", "hao.atlas"]:
        var packed_bytes: PackedByteArray = FileAccess.get_file_as_bytes(prefix + "/" + name)
        var key: String = "skeleton" if name == "hao.spjson" else "atlas"
        if sha256(packed_bytes) != String(config["source_sha256"][key]):
            fail("PCK payload differs from the current asset source: " + name)
            return
    var source: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(String(config["source_skeleton"])))
    result["source_version"] = source["skeleton"]["spine"]
    result["bone_count"] = source["bones"].size()
    if result["source_version"] != "4.2.43" or result["bone_count"] != 22:
        fail("Unexpected source Spine version or bone count")
        return
    if not ResourceLoader.exists(String(config["resource"]), "SpineSkeletonDataResource"):
        fail("ResourceLoader cannot find the built PCK skeleton data resource")
        return
    var data: Object = ResourceLoader.load(String(config["resource"]), "SpineSkeletonDataResource", ResourceLoader.CACHE_MODE_IGNORE)
    if data == null:
        fail("ResourceLoader failed to load the built PCK skeleton data resource")
        return
    var atlas: Object = data.get("atlas_res")
    var textures: Array = atlas.call("get_textures")
    if textures.size() != 1:
        fail("Expected one atlas page in this character pack")
        return
    var texture: Texture2D = textures[0]
    var decoded: Image = texture.get_image()
    var source_image: Image = Image.load_from_file(String(config["source_png"]))
    if decoded == null or decoded.is_empty() or source_image == null or source_image.is_empty():
        fail("Built atlas texture or source PNG could not decode")
        return
    var texture_check: Dictionary = compare_pixels(decoded, source_image)
    texture_check["texture_class"] = texture.get_class()
    texture_check["texture_resource_path"] = texture.resource_path
    var remap: ConfigFile = ConfigFile.new()
    var remap_error: int = remap.load(prefix + "/hao.png.import")
    texture_check["import_load_error"] = remap_error
    if remap_error == OK:
        texture_check["ctex_path"] = remap.get_value("remap", "path", "")
        texture_check["ctex_payload_exists"] = FileAccess.file_exists(String(texture_check["ctex_path"]))
    result["texture"] = texture_check
    if not texture_check["visible_pixels_equal"]:
        fail("Converted CTEX visible pixels differ from the source PNG")
        return
    if texture.get_class() != "CompressedTexture2D" or remap_error != OK or not String(texture_check.get("ctex_path", "")).ends_with(".ctex") or not texture_check.get("ctex_payload_exists", false):
        fail("Final pack did not load the expected PckPacker-converted CTEX")
        return

    var sprite: Node = ClassDB.instantiate("SpineSprite")
    sprite.name = "Visuals"
    sprite.set("position", Vector2(0, -4))
    sprite.set("scale", Vector2(0.55, 0.55))
    sprite.call("set_skeleton_data_res", data)
    # This mirrors the inspected factory's unique-node layout in native Godot.
    # It is NOT an invocation of the managed NCreatureVisuals/C# factory.
    var fixture_root: Node2D = Node2D.new()
    fixture_root.name = "CodingFarmerLayoutFixture"
    add_unique(fixture_root, sprite)
    var bounds: Control = Control.new()
    bounds.name = "Bounds"
    bounds.position = Vector2(-105, -278)
    bounds.size = Vector2(210, 278)
    bounds.mouse_filter = Control.MOUSE_FILTER_IGNORE
    add_unique(fixture_root, bounds)
    for item: Dictionary in [{"name": "CenterPos", "position": Vector2(0, -155)}, {"name": "IntentPos", "position": Vector2(0, -305)}, {"name": "OrbPos", "position": Vector2(0, -305)}, {"name": "TalkPos", "position": Vector2(20, -275)}]:
        var marker: Marker2D = Marker2D.new()
        marker.name = item["name"]
        marker.position = item["position"]
        add_unique(fixture_root, marker)
    var form_vfx: Control = Control.new()
    form_vfx.name = "FormVfx"
    form_vfx.mouse_filter = Control.MOUSE_FILTER_IGNORE
    add_unique(fixture_root, form_vfx)
    root.add_child(fixture_root)
    var nodes: Array = []
    for item: Dictionary in [{"name": "Visuals", "class": "SpineSprite"}, {"name": "Bounds", "class": "Control"}, {"name": "CenterPos", "class": "Marker2D"}, {"name": "IntentPos", "class": "Marker2D"}, {"name": "OrbPos", "class": "Marker2D"}, {"name": "TalkPos", "class": "Marker2D"}, {"name": "FormVfx", "class": "Control"}]:
        var node: Node = fixture_root.get_node("%" + String(item["name"]))
        if node == null or node.get_class() != String(item["class"]) or node.owner != fixture_root or not node.unique_name_in_owner:
            fail("Mirrored unique node contract failed: " + String(item["name"]))
            return
        nodes.append({"path": "%" + String(item["name"]), "class": node.get_class(), "unique_name_in_owner": node.unique_name_in_owner, "owner_is_fixture_root": node.owner == fixture_root})
    result["mirrored_node_fixture"] = {"ok": true, "root_class": "Node2D", "actual_managed_factory_invoked": false, "factory_source_sha256": config["factory_source_sha256"], "nodes": nodes}
    var state: Object = null
    var skeleton: Object = null
    for frame_index: int in range(120):
        state = sprite.call("get_animation_state")
        skeleton = sprite.call("get_skeleton")
        if state != null and skeleton != null:
            result["ready_after_frames"] = frame_index
            break
        await process_frame
    if state == null or skeleton == null:
        fail("Native SpineSprite did not initialize")
        return
    for animation_name: String in config["durations"]:
        var animation: Object = data.call("find_animation", animation_name)
        if animation == null:
            fail("Built skeleton lacks animation: " + animation_name)
            return
        var duration: float = float(animation.call("get_duration"))
        var expected: float = float(config["durations"][animation_name])
        if absf(duration - expected) > 0.001:
            fail("Built animation duration mismatch: " + animation_name)
            return
        state.call("clear_tracks")
        skeleton.call("set_to_setup_pose")
        if state.call("set_animation", animation_name, false, 0) == null:
            fail("Native animation could not start: " + animation_name)
            return
        sprite.call("update_skeleton", duration * 0.4)
        result["animations"].append({"name": animation_name, "duration": duration, "native_playback": true})
        print("PASS built animation ", animation_name, " duration=", duration)

    var peak_time: float = float(config["peak_time"])
    var baseline: Dictionary = attack_pose(sprite, state, skeleton, 0.0)
    var peak: Dictionary = attack_pose(sprite, state, skeleton, peak_time)
    var expected_local: float = float(config["expected_forearm_local_rotation"])
    var actual_local: float = float(peak["forearm_main"]["rotation"])
    var upper_raise: float = signed_angle(float(peak["upper_arm_main"]["world_rotation_x"]) - float(baseline["upper_arm_main"]["world_rotation_x"]))
    var forearm_raise: float = signed_angle(float(peak["forearm_main"]["world_rotation_x"]) - float(baseline["forearm_main"]["world_rotation_x"]))
    var angle_check: Dictionary = {
        "ok": absf(actual_local - expected_local) <= 0.05 and absf(forearm_raise) > absf(upper_raise) + 0.1,
        "peak_time": peak_time,
        "setup_forearm_rotation": config["setup_forearm_rotation"],
        "timeline_forearm_addition": config["timeline_forearm_addition"],
        "expected_forearm_local_rotation": expected_local,
        "actual_forearm_local_rotation": actual_local,
        "upper_arm_world_raise": upper_raise,
        "forearm_world_raise": forearm_raise,
        "forearm_raise_exceeds_upper_arm": absf(forearm_raise) > absf(upper_raise) + 0.1,
        "baseline": baseline,
        "peak": peak,
    }
    result["point_angles"] = angle_check
    if not angle_check["ok"]:
        fail("Native point-angle check failed")
        return
    var cape_slot: Object = skeleton.call("find_slot", "cape")
    if cape_slot == null or cape_slot.call("get_attachment") == null:
        fail("Built native cape attachment is missing")
        return
    result["native_cape_attachment"] = cape_slot.call("get_attachment").call("get_attachment_name")
    result["ok"] = true
    print("PASS built PCK ResourceLoader/CTEX/5 animations/point angles: forearm ", actual_local)
    finish()

func attack_pose(sprite: Object, state: Object, skeleton: Object, time: float) -> Dictionary:
    state.call("clear_tracks")
    skeleton.call("set_to_setup_pose")
    state.call("set_animation", "attack", false, 0)
    sprite.call("update_skeleton", time)
    var pose: Dictionary = {}
    for name: String in ["upper_arm_main", "forearm_main", "hand_main", "weapon"]:
        var bone: Object = skeleton.call("find_bone", name)
        pose[name] = {"rotation": float(bone.call("get_rotation")), "world_rotation_x": float(bone.call("get_world_rotation_x")), "world_x": float(bone.call("get_world_x")), "world_y": float(bone.call("get_world_y"))}
    return pose

func add_unique(owner_node: Node, child: Node) -> void:
    owner_node.add_child(child)
    child.owner = owner_node
    child.unique_name_in_owner = true

func compare_pixels(decoded: Image, source: Image) -> Dictionary:
    decoded.convert(Image.FORMAT_RGBA8)
    source.convert(Image.FORMAT_RGBA8)
    var check: Dictionary = {"width": decoded.get_width(), "height": decoded.get_height(), "exact_rgba_equal": false, "visible_pixels_equal": false, "visible_mismatch_pixels": 0, "transparent_rgb_only_mismatch_pixels": 0}
    if decoded.get_size() != source.get_size():
        check["error"] = "Decoded dimensions differ from source"
        return check
    var actual: PackedByteArray = decoded.get_data()
    var expected: PackedByteArray = source.get_data()
    check["decoded_rgba_sha256"] = sha256(actual)
    check["source_rgba_sha256"] = sha256(expected)
    check["exact_rgba_equal"] = actual == expected
    if not check["exact_rgba_equal"]:
        for index: int in range(0, actual.size(), 4):
            var rgb_differs: bool = actual[index] != expected[index] or actual[index + 1] != expected[index + 1] or actual[index + 2] != expected[index + 2]
            if actual[index + 3] != expected[index + 3] or (rgb_differs and (actual[index + 3] > 0 or expected[index + 3] > 0)):
                check["visible_mismatch_pixels"] += 1
            elif rgb_differs:
                check["transparent_rgb_only_mismatch_pixels"] += 1
    check["visible_pixels_equal"] = check["visible_mismatch_pixels"] == 0
    return check

func signed_angle(value: float) -> float:
    return fposmod(value + 180.0, 360.0) - 180.0

func sha256(bytes: PackedByteArray) -> String:
    var context: HashingContext = HashingContext.new()
    context.start(HashingContext.HASH_SHA256)
    context.update(bytes)
    return context.finish().hex_encode()

func fail(message: String) -> void:
    result["errors"].append(message)
    result["ok"] = false
    push_error(message)
    finish()

func finish() -> void:
    result["finished_at"] = Time.get_datetime_string_from_system()
    var file: FileAccess = FileAccess.open(String(config["result"]), FileAccess.WRITE)
    if file == null:
        push_error("Cannot write final built-pack probe report")
        quit(3)
        return
    file.store_string(JSON.stringify(result, "  "))
    file.close()
    quit(0 if result["ok"] else 1)

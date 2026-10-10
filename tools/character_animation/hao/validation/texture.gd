extends SceneTree

func _initialize() -> void:
    var config = JSON.parse_string(FileAccess.get_file_as_string("res://manifest.json"))
    var image = Image.load_from_file(config["png"])
    if image == null or image.is_empty():
        push_error("Cannot decode source texture")
        quit(1)
        return
    var texture = ImageTexture.create_from_image(image)
    var error = ResourceSaver.save(texture, config["native_texture"], ResourceSaver.FLAG_COMPRESS)
    print("Native texture serialization: ", error, " -> ", config["native_texture"])
    quit(0 if error == OK else 1)

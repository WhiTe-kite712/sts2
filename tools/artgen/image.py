import os
from PIL import Image, ImageOps

# 修改以下常量即可使用
INPUT_PATH = r"C:\\Users\\Amazi\\Desktop\\Spire2Mod\\Git Clone\\sts2\\MySts2Mod\\MySts2Mod\\images\\card_portraits\\tsinghua_form.png"
OUTPUT_PATH = r"C:\\Users\\Amazi\\Desktop\\Spire2Mod\\Git Clone\\sts2\\MySts2Mod\\MySts2Mod\\images\\card_portraits\\tsinghua_form.png"
TARGET_SIZE = (64, 64)  # (宽度, 高度)，单位：像素


def main():
    if not os.path.isfile(INPUT_PATH):
        raise FileNotFoundError(f"输入图片不存在：{INPUT_PATH}")

    if TARGET_SIZE[0] <= 0 or TARGET_SIZE[1] <= 0:
        raise ValueError("宽度和高度必须大于 0")

    output_dir = os.path.dirname(os.path.abspath(OUTPUT_PATH))
    os.makedirs(output_dir, exist_ok=True)

    with Image.open(INPUT_PATH) as image:
        image = ImageOps.exif_transpose(image)
        image = image.resize(TARGET_SIZE, Image.Resampling.LANCZOS)

        if os.path.splitext(OUTPUT_PATH)[1].lower() in (".jpg", ".jpeg"):
            image = image.convert("RGB")

        image.save(OUTPUT_PATH)

    print(f"已保存：{OUTPUT_PATH}，大小：{TARGET_SIZE}")


if __name__ == "__main__":
    main()
using Godot;
using MegaCrit.Sts2.Core.Assets;

namespace MySts2Mod.MySts2ModCode.Character;

internal static class CodingFarmerIcon
{
    public const string TexturePath = MainFile.ResPath + "/images/charui/coding_farmer_icon.png";
    private const string OutlinePath = MainFile.ResPath + "/images/charui/runtime_icon_outline.png";

    public static Control Create()
    {
        var icon = new TextureRect
        {
            Name = "CodingFarmerIcon",
            Texture = LoadTexture(),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return icon;
    }

    private static Texture2D LoadTexture() => ResourceLoader.Load<Texture2D>(TexturePath)
        ?? throw new InvalidOperationException("Missing character icon: " + TexturePath);

    public static string GetOutlinePath()
    {
        if (PreloadManager.Cache.ContainsKey(OutlinePath)) return OutlinePath;
        using var source = LoadTexture().GetImage();
        if (source.IsCompressed()) source.Decompress();
        int width = source.GetWidth(), height = source.GetHeight();
        using var outline = Image.CreateEmpty(width, height, false, Image.Format.Rgba8);
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float alpha = 0;
            for (int dy = -2; dy <= 2; dy++)
            for (int dx = -2; dx <= 2; dx++)
            {
                if (dx * dx + dy * dy > 4) continue;
                int px = x + dx, py = y + dy;
                if (px >= 0 && px < width && py >= 0 && py < height)
                    alpha = Math.Max(alpha, source.GetPixel(px, py).A);
            }
            outline.SetPixel(x, y, new Color(1, 1, 1, alpha));
        }
        var texture = ImageTexture.CreateFromImage(outline);
        PreloadManager.Cache.SetAsset(OutlinePath, texture);
        return OutlinePath;
    }
}

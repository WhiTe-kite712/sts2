using Godot;

namespace MySts2Mod.MySts2ModCode.Character;

internal static class CodingFarmerEnergy
{
    public const string SmallPath = MainFile.ResPath + "/images/energy_hao.png";
    public const string CounterPath = MainFile.ResPath + "/images/energy_hao_big.png";
    private const string EmptyPath = MainFile.ResPath + "/images/runtime_energy_empty.png";
    private static ImageTexture? emptyLayer;

    public static string CounterLayerPath(int layer)
    {
        if (layer == 1) return CounterPath;
        // BaseLib creates five orb layers; the existing art is one complete orb.
        if (emptyLayer == null || !GodotObject.IsInstanceValid(emptyLayer))
        {
            using var image = Image.CreateEmpty(1, 1, false, Image.Format.Rgba8);
            image.Fill(Colors.Transparent);
            emptyLayer = ImageTexture.CreateFromImage(image);
        }
        emptyLayer.TakeOverPath(EmptyPath);
        return EmptyPath;
    }
}

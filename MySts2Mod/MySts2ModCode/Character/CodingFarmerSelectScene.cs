using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;

namespace MySts2Mod.MySts2ModCode.Character;

internal static class CodingFarmerSelectScene
{
    public const string ScenePath = MainFile.ResPath + "/scenes/char_select/coding_farmer_select_bg.tscn";
    public const string TexturePath = MainFile.ResPath + "/images/charui/coding_farmer_select_bg.png";
    private static PackedScene? scene;

    public static string GetScenePath()
    {
        if (scene != null && GodotObject.IsInstanceValid(scene) && PreloadManager.Cache.ContainsKey(ScenePath))
            return ScenePath;

        var texture = ResourceLoader.Load<Texture2D>(TexturePath)
            ?? throw new InvalidOperationException("Character select background is missing: " + TexturePath);
        var root = new Control
        {
            Name = "CodingFarmerSelectBg",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            AnchorLeft = 0.5f,
            AnchorTop = 0.5f,
            AnchorRight = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -960f,
            OffsetTop = -540f,
            OffsetRight = 1600f,
            OffsetBottom = 660f,
            PivotOffset = new Vector2(1280, 600),
        };
        var image = new TextureRect
        {
            Name = "Portrait",
            Texture = texture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var packed = new PackedScene();
        try
        {
            root.AddChild(image);
            image.Owner = root;
            image.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            var result = packed.Pack(root);
            if (result != Error.Ok)
            {
                packed.Dispose();
                throw new InvalidOperationException("Cannot create character select background: " + result);
            }
        }
        finally
        {
            root.Free();
        }

        packed.TakeOverPath(ScenePath);
        PreloadManager.Cache.SetAsset(ScenePath, packed);
        scene = packed;
        return ScenePath;
    }
}

// This generated scene has no disk file to reload after leaving the main menu.
[HarmonyPatch(typeof(AssetCache), nameof(AssetCache.UnloadAssets))]
internal static class KeepCodingFarmerSelectSceneCached
{
    [HarmonyPrefix]
    private static void Prefix(AssetCache __instance, ref IEnumerable<string> assetsToUnloadSet)
    {
        if (ReferenceEquals(__instance, PreloadManager.Cache))
            assetsToUnloadSet = assetsToUnloadSet.Where(path => path != CodingFarmerSelectScene.ScenePath);
    }
}

using BaseLib.Utils.NodeFactories;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace MySts2Mod.MySts2ModCode.Character;

internal static class CodingFarmerMerchantScene
{
    public const string ScenePath = MainFile.ResPath + "/scenes/merchant/coding_farmer_merchant.tscn";
    internal const string VisualName = "CodingFarmerMerchantVisuals";
    private static PackedScene? scene;

    public static string GetScenePath()
    {
        if (scene != null && GodotObject.IsInstanceValid(scene) && PreloadManager.Cache.ContainsKey(ScenePath))
            return ScenePath;

        var data = ResourceLoader.Load<Resource>(CodingFarmerCombatVisuals.SkeletonPath)
            ?? throw new InvalidOperationException("Missing merchant character Spine data: " + CodingFarmerCombatVisuals.SkeletonPath);
        // BaseLib wraps this root as the first child of NMerchantCharacter.
        var root = ClassDB.Instantiate("SpineSprite").As<Node2D>();
        root.Name = VisualName;
        root.Set("skeleton_data_res", data);

        var packed = new PackedScene();
        try
        {
            var result = packed.Pack(root);
            if (result != Error.Ok)
            {
                packed.Dispose();
                throw new InvalidOperationException("Cannot create merchant character scene: " + result);
            }
        }
        finally
        {
            root.Free();
        }

        packed.TakeOverPath(ScenePath);
        PreloadManager.Cache.SetAsset(ScenePath, packed);
        NodeFactory.RegisterSceneType<NMerchantCharacter>(ScenePath);
        scene = packed;
        return ScenePath;
    }
}

[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter._Ready))]
internal static class CodingFarmerMerchantLayout
{
    [HarmonyPrefix]
    private static void Prefix(NMerchantCharacter __instance)
    {
        if (__instance.GetChildCount() == 0) return;
        if (__instance.GetChild(0) is not Node2D body
            || body.Name.ToString() != CodingFarmerMerchantScene.VisualName) return;

        // Scale after conversion so the factory does not copy the scale to both nodes.
        body.Position = new Vector2(0, -4);
        body.Scale = Vector2.One * 0.55f;
    }
}

[HarmonyPatch(typeof(NMerchantCharacter), nameof(NMerchantCharacter.PlayAnimation))]
internal static class CodingFarmerMerchantIdleAnimation
{
    [HarmonyPrefix]
    private static void Prefix(NMerchantCharacter __instance, ref string anim)
    {
        if (anim != "relaxed_loop" || __instance.GetChildCount() == 0) return;
        if (__instance.GetChild(0).Name.ToString() != CodingFarmerMerchantScene.VisualName) return;

        anim = "idle_loop";
    }
}

// Runtime scenes have no disk file to reload after leaving a room.
[HarmonyPatch(typeof(AssetCache), nameof(AssetCache.UnloadAssets))]
internal static class KeepCodingFarmerMerchantSceneCached
{
    [HarmonyPrefix]
    private static void Prefix(AssetCache __instance, ref IEnumerable<string> assetsToUnloadSet)
    {
        if (ReferenceEquals(__instance, PreloadManager.Cache))
            assetsToUnloadSet = assetsToUnloadSet.Where(path => path != CodingFarmerMerchantScene.ScenePath);
    }
}

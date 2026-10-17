using BaseLib.Utils.NodeFactories;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.RestSite;

namespace MySts2Mod.MySts2ModCode.Character;

internal static class CodingFarmerRestSiteScene
{
    public const string ScenePath = MainFile.ResPath + "/scenes/rest_site/coding_farmer_rest_site.tscn";
    private static PackedScene? scene;

    public static string GetScenePath()
    {
        if (scene != null && GodotObject.IsInstanceValid(scene) && PreloadManager.Cache.ContainsKey(ScenePath))
            return ScenePath;

        var data = ResourceLoader.Load<Resource>(CodingFarmerCombatVisuals.SkeletonPath)
            ?? throw new InvalidOperationException("Missing rest-site character Spine data: " + CodingFarmerCombatVisuals.SkeletonPath);
        // BaseLib converts this complete, generic root to NRestSiteCharacter.
        // Keep SpineSprite directly under it: the native rest-site code finds,
        // starts and flips only direct SpineSprite children, separately from UI.
        var root = new Node2D { Name = "CodingFarmerRestSite" };
        var body = ClassDB.Instantiate("SpineSprite").As<Node2D>();
        body.Name = "SpineSprite";
        body.Position = new Vector2(0, 10);
        body.Scale = Vector2.One * 0.55f;
        body.Set("skeleton_data_res", data);
        Add(root, root, body);

        var controls = new Control
        {
            Name = "ControlRoot",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        Add(root, root, controls);
        var hitbox = new Control
        {
            Name = "Hitbox",
            Position = new Vector2(-115, -275),
            Size = new Vector2(205, 292),
            FocusMode = Control.FocusModeEnum.All,
        };
        Add(root, controls, hitbox, unique: true);

        // Reuse the game's reticle so player targeting and multiplayer support
        // work exactly as for native characters, including mirrored positions.
        var reticle = SceneHelper.Instantiate<NSelectionReticle>("ui/selection_reticle");
        reticle.Name = "SelectionReticle";
        reticle.Position = hitbox.Position;
        reticle.Size = hitbox.Size;
        Add(root, controls, reticle, unique: true);
        Add(root, controls, new Control
        {
            Name = "ThoughtBubbleLeft",
            Position = new Vector2(-95, -245),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        }, unique: true);
        Add(root, controls, new Control
        {
            Name = "ThoughtBubbleRight",
            Position = new Vector2(90, -245),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        }, unique: true);

        var packed = new PackedScene();
        try
        {
            var result = packed.Pack(root);
            if (result != Error.Ok)
            {
                packed.Dispose();
                throw new InvalidOperationException("Cannot create rest-site character scene: " + result);
            }
        }
        finally
        {
            root.Free();
        }

        packed.TakeOverPath(ScenePath);
        PreloadManager.Cache.SetAsset(ScenePath, packed);
        NodeFactory.RegisterSceneType<NRestSiteCharacter>(ScenePath);
        scene = packed;
        return ScenePath;
    }

    private static void Add(Node root, Node parent, Node child, bool unique = false)
    {
        parent.AddChild(child);
        child.Owner = root;
        child.UniqueNameInOwner = unique;
    }
}

// The generated scene has no disk file to reload when revisiting a rest site.
[HarmonyPatch(typeof(AssetCache), nameof(AssetCache.UnloadAssets))]
internal static class KeepCodingFarmerRestSiteSceneCached
{
    [HarmonyPrefix]
    private static void Prefix(AssetCache __instance, ref IEnumerable<string> assetsToUnloadSet)
    {
        if (ReferenceEquals(__instance, PreloadManager.Cache))
            assetsToUnloadSet = assetsToUnloadSet.Where(path => path != CodingFarmerRestSiteScene.ScenePath);
    }
}

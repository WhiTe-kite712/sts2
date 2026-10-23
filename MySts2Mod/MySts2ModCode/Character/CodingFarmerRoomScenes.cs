using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Assets;

namespace MySts2Mod.MySts2ModCode.Character;

/// <summary>
/// 火堆 / 商店角色场景：复制原版铁甲战士场景，把 SpineSprite 的骨架数据换成码农的 hao.tres，
/// 再注册成我们自己的虚拟路径。
///
/// ═══════════════════════════════════════════════════════════════════════
/// 为什么需要这个（用户 2026-10-23 反馈「火堆和商店用的都是铁甲战士占位」）
/// ═══════════════════════════════════════════════════════════════════════
/// `CodingFarmer : PlaceholderCharacterModel`，而 BaseLib 的
/// `PlaceholderCharacterModel` 里：
///     public virtual string PlaceholderID => "ironclad";
///     public override string CustomRestSiteAnimPath =>
///         SceneHelper.GetScenePath("rest_site/characters/" + PlaceholderID + "_rest_site");
///     public override string CustomMerchantAnimPath =>
///         SceneHelper.GetScenePath("merchant/characters/" + PlaceholderID + "_merchant");
/// ⇒ 战斗中走 `CreateCustomVisuals()`（我们的码农），但火堆 / 商店没有覆盖
///   ⇒ 回落到 `ironclad_rest_site` / `ironclad_merchant` ⇒ 显示铁甲战士。
///
/// 引擎侧对这两个场景的要求（sts2.decompiled.cs 实查）：
///   · 商店 NMerchantCharacter._Ready():
///         RunWhenSpineReady(new MegaSprite(GetChild(0)),
///             () => PlayAnimation("relaxed_loop", loop: true));
///     ⇒ 子节点 0 必须是 SpineSprite，且骨架要有 relaxed_loop（我们已加）
///   · 火堆 NRestSiteCharacter._Ready():
///         animName = ActIndex switch { 0 => "overgrowth_loop", 1 => "hive_loop", 2 => "glory_loop" }
///         animState.SetAnimation(animName);
///     ⇒ 骨架要有这三个动画名（我们已加别名）
///
/// 做法照抄本仓已有的先例 `CodingFarmerSelectScene`：
///   程序化建/改场景 → `packed.TakeOverPath(path)` → `PreloadManager.Cache.SetAsset(path, packed)`
///   → Harmony 在 `AssetCache.UnloadAssets` 里把这两个路径排除，防止离开房间后被卸载。
/// </summary>
internal static class CodingFarmerRoomScenes
{
    /// <summary>原版铁甲战士的火堆场景（作为模板）。</summary>
    internal const string VanillaRestSitePath =
        "res://scenes/rest_site/characters/ironclad_rest_site.tscn";

    /// <summary>原版铁甲战士的商店场景（作为模板）。</summary>
    internal const string VanillaMerchantPath =
        "res://scenes/merchant/characters/ironclad_merchant.tscn";

    /// <summary>码农的火堆场景虚拟路径。</summary>
    internal const string RestSitePath =
        MainFile.ResPath + "/scenes/rest_site/coding_farmer_rest_site.tscn";

    /// <summary>码农的商店场景虚拟路径。</summary>
    internal const string MerchantPath =
        MainFile.ResPath + "/scenes/merchant/coding_farmer_merchant.tscn";

    /// <summary>商店：原版 ironclad_merchant 的 SpineSprite.scale = 0.47 ⇒ ×2 = 0.94。</summary>
    private const float MerchantScaleMultiplier = 2.0f;

    /// <summary>火堆：原版 ironclad_rest_site 的 root.scale = 0.76 ⇒ ×1.2 = 0.912。</summary>
    private const float RestSiteScaleMultiplier = 1.2f;

    private static PackedScene? _restSite;
    private static PackedScene? _merchant;

    /// <summary>火堆场景路径（getter 内部会构建并缓存）。</summary>
    internal static string GetRestSitePath()
        => EnsureScene(ref _restSite, VanillaRestSitePath, RestSitePath,
                       "CodingFarmerRestSite", RestSiteScaleMultiplier, scaleSpine: false);

    /// <summary>商店场景路径（getter 内部会构建并缓存）。</summary>
    internal static string GetMerchantPath()
        => EnsureScene(ref _merchant, VanillaMerchantPath, MerchantPath,
                       "CodingFarmerMerchant", MerchantScaleMultiplier, scaleSpine: true);

    private static string EnsureScene(
        ref PackedScene? cache, string vanillaPath, string ourPath, string rootName,
        float scaleMultiplier, bool scaleSpine)
    {
        if (cache != null && GodotObject.IsInstanceValid(cache) && PreloadManager.Cache.ContainsKey(ourPath))
        {
            return ourPath;
        }

        var src = ResourceLoader.Load<PackedScene>(vanillaPath)
            ?? throw new InvalidOperationException("Vanilla room scene missing: " + vanillaPath);
        var skeleton = ResourceLoader.Load<Resource>(CodingFarmerCombatVisuals.SkeletonPath)
            ?? throw new InvalidOperationException(
                "Coding farmer skeleton missing: " + CodingFarmerCombatVisuals.SkeletonPath);

        Node2D? instance = null;
        try
        {
            instance = src.Instantiate<Node2D>();
            instance.Name = rootName;

            // 子节点 0 必须是 SpineSprite（引擎用 GetChild(0) 取它）
            var spine = instance.GetNodeOrNull<Node2D>("SpineSprite")
                ?? FindSpineChild(instance)
                ?? throw new InvalidOperationException(
                    "SpineSprite node not found in " + vanillaPath);
            spine.Set("skeleton_data_res", skeleton);

            // ★ 2026-10-23 用户要求：商店放大 200%、火堆放大 120%
            //   商店改 SpineSprite 缩放（原 0.47），火堆改 root 缩放（原 0.76）
            if (scaleSpine)
            {
                var originalSpineScale = spine.Scale;
                spine.Scale = originalSpineScale * scaleMultiplier;
                MainFile.Logger.Info(
                    $"[CodingFarmer] room scene spine scale: {originalSpineScale} -> {spine.Scale} (x{scaleMultiplier})");
            }
            else
            {
                var originalRootScale = instance.Scale;
                instance.Scale = originalRootScale * scaleMultiplier;
                MainFile.Logger.Info(
                    $"[CodingFarmer] room scene root scale: {originalRootScale} -> {instance.Scale} (x{scaleMultiplier})");
            }

            var packed = new PackedScene();
            var err = packed.Pack(instance);
            if (err != Error.Ok)
            {
                packed.Dispose();
                throw new InvalidOperationException($"Cannot pack {rootName}: {err}");
            }

            packed.TakeOverPath(ourPath);
            PreloadManager.Cache.SetAsset(ourPath, packed);
            cache = packed;
            MainFile.Logger.Info(
                $"[CodingFarmer] room scene ready: {ourPath} (from {vanillaPath})");
            return ourPath;
        }
        finally
        {
            instance?.Free();
        }
    }

    /// <summary>兜底：递归找第一个 SpineSprite 类型的节点。</summary>
    private static Node2D? FindSpineChild(Node2D root)
    {
        foreach (var child in root.GetChildren())
        {
            if (child is Node2D n2 && n2.GetClass() == "SpineSprite")
            {
                return n2;
            }
            if (child is Node n)
            {
                var found = FindSpineChildRecursive(n);
                if (found != null) return found;
            }
        }
        return null;
    }

    private static Node2D? FindSpineChildRecursive(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            if (child is Node2D n2 && n2.GetClass() == "SpineSprite")
            {
                return n2;
            }
            if (child is Node n)
            {
                var found = FindSpineChildRecursive(n);
                if (found != null) return found;
            }
        }
        return null;
    }
}

/// <summary>
/// 这两个场景是程序化生成的，磁盘上没有对应文件；
/// 离开房间时 `AssetCache.UnloadAssets` 会把它们当"未使用的资源"卸载 ⇒ 下次进房间缓存为 null。
/// 与 `KeepCodingFarmerSelectSceneCached` 同样的做法：把这两个路径从待卸载集合里剔除。
/// </summary>
[HarmonyPatch(typeof(AssetCache), nameof(AssetCache.UnloadAssets))]
internal static class KeepCodingFarmerRoomScenesCached
{
    [HarmonyPrefix]
    private static void Prefix(AssetCache __instance, ref IEnumerable<string> assetsToUnloadSet)
    {
        if (!ReferenceEquals(__instance, PreloadManager.Cache))
        {
            return;
        }
        assetsToUnloadSet = assetsToUnloadSet.Where(path =>
            path != CodingFarmerRoomScenes.RestSitePath &&
            path != CodingFarmerRoomScenes.MerchantPath);
    }
}

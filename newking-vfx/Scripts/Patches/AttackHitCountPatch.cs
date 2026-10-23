using System;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;

namespace NewKingVfx.Patches;

/// <summary>
/// 拦截 <see cref="AttackCommand"/> 的构建，把「真实命中段数」记到卡牌上。
///
/// ═══════════════════════════════════════════════════════════════════
/// 为什么需要两个补丁
/// ═══════════════════════════════════════════════════════════════════
/// 引擎里两种链式写法都存在（都活体核实过）：
///   A. <c>DamageCmd.Attack(x).WithHitCount(2).FromCard(this, play)</c>   （TwinStrike / Maul…）
///   B. <c>DamageCmd.Attack(x).FromCard(this, play).WithHitCount(2)</c>   （Uproar / SovereignBlade…）
///
/// ⇒ 只拦 FromCard 会漏掉 B；只拦 WithHitCount 又拿不到卡牌引用。
/// ⇒ 解法：FromCard 时建立 <c>AttackCommand → CardModel</c> 映射；
///         WithHitCount 时通过该映射回查卡牌并刷新记录。两种顺序都能覆盖。
/// </summary>
internal static class AttackHitCountPatch
{
    /// <summary>AttackCommand → 它来自哪张卡（用于 WithHitCount 回查）。</summary>
    private static readonly ConditionalWeakTable<AttackCommand, CardModel> OwnerOf = new();

    /// <summary>FromCard 之后：此时 _hitCount 可能是最终值，也可能是初始值 1。</summary>
    internal static void FromCardPostfix(AttackCommand __instance, CardModel __0)
    {
        try
        {
            if (__instance == null || __0 == null) return;
            OwnerOf.Remove(__instance);
            OwnerOf.Add(__instance, __0);
            HitCountReader.Record(__0, __instance);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] FromCard hitcount hook failed: {ex.Message}");
        }
    }

    /// <summary>供其它补丁回查：这条 AttackCommand 属于哪张卡。</summary>
    internal static CardModel? ResolveOwner(AttackCommand? command)
    {
        if (command == null) return null;
        try
        {
            return OwnerOf.TryGetValue(command, out var card) ? card : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>WithHitCount 之后：如果这张 command 已关联卡牌，刷新记录。</summary>
    internal static void WithHitCountPostfix(AttackCommand __instance)
    {
        try
        {
            if (__instance == null) return;
            if (OwnerOf.TryGetValue(__instance, out var card) && card != null)
            {
                HitCountReader.Record(card, __instance);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] WithHitCount hook failed: {ex.Message}");
        }
    }

    /// <summary>注册两个补丁。</summary>
    internal static void Apply(Harmony harmony)
    {
        TryBind(harmony, "AttackCommand.FromCard",
            AccessTools.Method(typeof(AttackCommand), "FromCard"),
            postfix: AccessTools.Method(typeof(AttackHitCountPatch), nameof(FromCardPostfix)));

        TryBind(harmony, "AttackCommand.WithHitCount",
            AccessTools.Method(typeof(AttackCommand), "WithHitCount"),
            postfix: AccessTools.Method(typeof(AttackHitCountPatch), nameof(WithHitCountPostfix)));
    }

    private static void TryBind(Harmony harmony, string label,
        System.Reflection.MethodBase? target, System.Reflection.MethodInfo? postfix)
    {
        try
        {
            if (target == null || postfix == null)
            {
                GD.PushWarning($"[{Entry.ModId}] hitcount patch skipped (missing target): {label}");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            GD.Print($"[{Entry.ModId}] patched {label}");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] hitcount patch failed ({label}): {ex.Message}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;

namespace NewKingVfx;

/// <summary>
/// 读取一次攻击的「命中段数（hit count）」。
///
/// ═══════════════════════════════════════════════════════════════════
/// 为什么要专门做这个
/// ═══════════════════════════════════════════════════════════════════
/// 引擎里多段攻击有 4 种来源（全部活体核实过）：
///  ① <c>RepeatVar</c> + <c>DynamicVars.Repeat.IntValue</c>（FooledYou / Conflagration / Exterminate…）
///  ② 硬编码 <c>.WithHitCount(2)</c>（TwinStrike / Maul / Thrash / Uproar / RipAndTear…）
///  ③ <c>CalculatedHits</c> 动态计算（Finisher / Flechettes / HelixDrill…）
///  ④ X 能量 / 状态数（Skewer / Volley / FlakCannon…）
///
/// ⇒ 只读 <c>DynamicVars</c> 会漏掉 ②③④。所以：
///    · 先用 <c>DynamicVars["Repeat"]</c> 做快速判断（覆盖 ①）；
///    · 同时用 Harmony 拦截 <see cref="AttackCommand.WithHitCount"/> 把真实段数记进
///      <see cref="ConditionalWeakTable{TKey,TValue}"/>（按 CardModel 关联）；
///    · 最后用反射读 <c>AttackCommand._hitCount</c> 做校验。
///
/// ⇒ 对 <c>hit == 1</c> 的牌返回 1；读不到时也返回 1（保守：走单段动画）。
/// </summary>
internal static class HitCountReader
{
    /// <summary>card → 最近一次 AttackCommand 的真实 hitCount。</summary>
    private static readonly ConditionalWeakTable<CardModel, StrongBox<int>> Recorded = new();

    private static readonly FieldInfo? HitCountField =
        AccessTools.Field(typeof(AttackCommand), "_hitCount");

    /// <summary>Harmony 拦截点：任何 <c>WithHitCount</c> 调用都会记下真实段数。</summary>
    internal static void Record(CardModel? card, AttackCommand command)
    {
        try
        {
            if (card == null || command == null) return;
            int hit = ReadRaw(command);
            Recorded.Remove(card);
            Recorded.Add(card, new StrongBox<int>(hit));
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] hit count record failed: {ex.Message}");
        }
    }

    /// <summary>反射读 AttackCommand 的私有 _hitCount。</summary>
    internal static int ReadRaw(AttackCommand? command)
    {
        if (command == null || HitCountField == null) return 1;
        try
        {
            object? v = HitCountField.GetValue(command);
            return v is int i ? Math.Max(1, i) : 1;
        }
        catch
        {
            return 1;
        }
    }

    /// <summary>
    /// 读这张牌的命中段数。返回值 ≥ 1。
    ///   1 = 单段（用跳跃斩）；&gt;1 = 多段（用插剑+裂隙+虚影）。
    /// </summary>
    internal static int Get(CardModel? card)
    {
        if (card == null) return 1;

        // ① Harmony 记录的真实值（最准）
        if (Recorded.TryGetValue(card, out var box))
        {
            int recorded = Math.Max(1, box.Value);
            if (recorded > 1) return recorded;
        }

        // ② DynamicVars.Repeat（覆盖 RepeatVar 系）
        try
        {
            if (card.DynamicVars != null && card.DynamicVars.TryGetValue("Repeat", out var rv))
            {
                int repeat = rv is null ? 1 : Convert.ToInt32(rv.IntValue);
                if (repeat > 1) return repeat;
            }
        }
        catch
        {
            // 忽略：没有 Repeat 变量是正常情况
        }

        return 1;
    }

    /// <summary>清除某张牌的记录（战斗结束或卡牌变更时调用）。</summary>
    internal static void Forget(CardModel? card)
    {
        if (card == null) return;
        try { Recorded.Remove(card); } catch { /* ignore */ }
    }
}

using System;
using System.Collections.Generic;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace NewKingVfx.Patches;

/// <summary>
/// 多段攻击专用动画切换：<c>hit != 1</c> 的攻击牌改用我们自己的 <c>attack_multi</c>。
///
/// ═══════════════════════════════════════════════════════════════════
/// 用户 2026-10-22 的需求（逐字）
/// ═══════════════════════════════════════════════════════════════════
/// 「接下来做多段攻击动画，因为这个显然不适用多段攻击」
/// 「此动画在 hit=!1 的攻击卡牌启用」
///
/// ⇒ 单段（hit==1）继续用 <c>attack</c>（跳跃斩）；多段用 <c>attack_multi</c>（跪地插剑）。
///
/// ═══════════════════════════════════════════════════════════════════
/// 为什么在 AttackCommand.FromCard 上挂
/// ═══════════════════════════════════════════════════════════════════
/// 本仓 <c>DeathMarchCharge</c> 已验证同一做法（DeathMarchPatches.cs:49-55）：
/// <code>
/// private static void Postfix(AttackCommand __result, CardModel __0)
/// {
///     if (__0 is DeathMarch)
///         __result.WithAttackerAnim("Cast", 0.4f);
/// }
/// </code>
/// ⇒ <c>WithAttackerAnim(动画名, 出伤延迟)</c> 是引擎公开的构建器方法，
///   一次性覆盖"播哪个动画"与"伤害延迟到什么时候结算"。
///
/// ⚠️ 出伤延迟取 <c>1.05f</c>：与 <c>attack_multi</c> 的 <c>eventFrames = 105</c>（虚影开始冲锋）一致。
/// </summary>
internal static class MultiHitAnimPatch
{
    /// <summary>多段攻击使用的动画名（由 rebuild.py 生成到 hao.spjson）。</summary>
    internal const string MultiAnim = "attack_multi";

    /// <summary>多段攻击的出伤延迟（秒）= 虚影开始冲锋的时刻。</summary>
    internal const float MultiHitDelay = 1.05f;

    /// <summary>反射：<c>AttackCommand._singleTarget</c>（取攻击目标，供演出定位）。</summary>
    private static readonly System.Reflection.FieldInfo? SingleTargetField =
        AccessTools.Field(typeof(AttackCommand), "_singleTarget");

    /// <summary>
    /// 反射：<c>AttackCommand.GetPossibleTargets()</c>（AOE 取目标列表）。
    /// ★ 2026-10-23 性能：原来每次攻击都调一次 AccessTools.Method ⇒ 缓存成静态字段。
    /// </summary>
    private static readonly System.Reflection.MethodInfo? PossibleTargetsMethod =
        AccessTools.Method(typeof(AttackCommand), "GetPossibleTargets");

    /// <summary>锚点方法：<c>AttackCommand.FromCard(CardModel, CardPlay?)</c>。</summary>
    internal static void FromCardPostfix(AttackCommand __result, CardModel __0)
    {
        TrySwitch(__result, __0);
        // ★ 2026-10-23：演出触发已移到 Targeting 的 Postfix
        //   （引擎是 FromCard(...).Targeting(...) 链式调用，FromCard 时 _singleTarget 还是 null）
    }

    /// <summary>
    /// 补充锚点：<c>AttackCommand.WithHitCount(int)</c>。
    ///
    /// ⚠️ 必须两个锚点都有：引擎里两种链式写法并存，
    ///   <c>.WithHitCount(2).FromCard(...)</c> 与 <c>.FromCard(...).WithHitCount(2)</c>。
    ///   只在 FromCard 上判断的话，后一种写法轮到 FromCard 时 _hitCount 还是 1 ⇒ 会误判成单段。
    /// </summary>
    internal static void WithHitCountPostfix(AttackCommand __instance)
    {
        try
        {
            if (__instance == null) return;
            // 通过 AttackHitCountPatch 的映射找回 card
            CardModel? card = AttackHitCountPatch.ResolveOwner(__instance);
            if (card == null) return;
            TrySwitch(__instance, card);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] multi-hit anim (withHitCount) failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 每张牌上次触发动画切换的时刻（秒）。用于去重：
    /// 多段攻击的每一段都会走 FromCard/WithHitCount，不去重就会反复重置动画，
    /// 把第一段的跪地插剑动画打断。
    /// </summary>
    private static readonly Dictionary<string, float> LastSwitchAt = new(StringComparer.Ordinal);

    /// <summary>同一张牌两次动画切换的最小间隔（秒）—— 覆盖整段 attack_multi（2.06s）。</summary>
    private const float SwitchCooldownSeconds = 2.10f;

    /// <summary>幂等切换：读不到 card 或 hit ≤ 1 就不动；同一张牌冷却期内不重复触发。</summary>
    private static void TrySwitch(AttackCommand? command, CardModel? card)
    {
        try
        {
            if (command == null || card == null) return;

            int hit = HitCountReader.Get(card);
            if (hit <= 1) return;   // 单段：保持默认 attack（跳跃斩）

            // ★ 2026-10-23 审批 B2：归属门禁！
            //   之前不看归属 ⇒ 任何角色/敌人的多段攻击都会被改成 attack_multi，
            //   而他们的骨骼没有这个动画 ⇒ SetTrigger 静默 no-op ⇒ 攻击完全没有动画。
            if (!CardVfxPlayer.IsCodingFarmerOwner(card)) return;

            // ★ 去重：多段攻击的后续段不再重触发（否则会打断第一段动画）
            string key = card.Id.Entry;
            float now = Time.GetTicksMsec() / 1000f;
            if (LastSwitchAt.TryGetValue(key, out var last) && now - last < SwitchCooldownSeconds)
            {
                return;
            }
            LastSwitchAt[key] = now;

            command.WithAttackerAnim(MultiAnim, MultiHitDelay);
            // ★ 2026-10-22 删除：原先在这里 Arm 抑制窗口，但抑制对象是攻击者自己（玩家），
            //   玩家攻击时不会触发自己的 "Hit" ⇒ 抑制无效，反而会吞掉玩家被打的真实反馈。
            // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] multi-hit anim: card={key} hit={hit} -> {MultiAnim} delay={MultiHitDelay:0.00}s");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] multi-hit switch failed: {ex.Message}");
        }
    }

    /// <summary>去重：同一张牌短时间内只播一次攻击演出。</summary>
    private static readonly System.Collections.Generic.Dictionary<string, float> LastVfxAt =
        new(StringComparer.Ordinal);

    /// <summary>
    /// <c>AttackCommand.Targeting(Creature)</c> 的 Postfix —— 演出触发点。
    /// 引擎链式调用 <c>FromCard(...).Targeting(cardPlay.Target)</c>，
    /// 这里 <c>_singleTarget</c> 已就绪，且 <c>ModelSource</c> 能拿到卡牌。
    /// </summary>
    internal static void TargetingPostfix(AttackCommand __result, Creature __0)
    {
        try
        {
            if (__result == null) return;
            // 只服务码农，且必须是卡牌来源
            var card = __result.ModelSource as CardModel;
            if (card == null) return;
            if (!CardVfxPlayer.IsCodingFarmerOwner(card)) return;

            // 去重（0.8s 内同一张卡只播一次，防止 Targeting 被多次调用时重复演出）
            string key = card.Id.Entry;
            float now = Time.GetTicksMsec() / 1000f;
            if (LastVfxAt.TryGetValue(key, out var last) && now - last < 0.8f) return;
            LastVfxAt[key] = now;

            int hit = HitCountReader.Get(card);

            // ★ 2026-10-23 用户要求：优先级 AOE > 多段 > 单段
            //   AOE 判定 = AttackCommand.IsMultiTargeted（_combatState != null）
            if (__result.IsMultiTargeted)
            {
                try
                {
                    var targets = new System.Collections.Generic.List<Creature>();
                    // GetPossibleTargets 是 private ⇒ 反射调用（方法已缓存）
                    var raw = PossibleTargetsMethod?.Invoke(__result, null) as System.Collections.IEnumerable;
                    if (raw != null)
                        foreach (var o in raw)
                            if (o is Creature c && !c.IsDead) targets.Add(c);
                    // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] AOE detected: card={key} targets={targets.Count}");
                    CardVfxPlayer.PlayAoeVfx(card, targets);
                    return;   // AOE 优先，不再走单段/多段
                }
                catch (Exception ex)
                {
                    GD.PushWarning($"[{Entry.ModId}] AOE branch failed: {ex.Message}");
                }
            }

            // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] attack vfx (targeting): card={key} hit={hit} target={__0?.Name ?? "null"}");
            CardVfxPlayer.PlayAttackVfx(card, hit, __0);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] targeting attack vfx failed: {ex.Message}");
        }
    }

    /// <summary>
    /// <c>AttackCommand.TargetingAllOpponents(ICombatState)</c> 的 Postfix ——
    /// 无目标 AOE（如原版 BREAKTHROUGH）走这条路径，不是 Targeting(Creature)。
    /// 这里同样触发 AOE 演出。
    /// </summary>
    internal static void TargetingAllPostfix(AttackCommand __result)
    {
        try
        {
            if (__result == null) return;
            var card = __result.ModelSource as CardModel;
            if (card == null) return;
            if (!CardVfxPlayer.IsCodingFarmerOwner(card)) return;

            // 去重（和 TargetingPostfix 共用 LastVfxAt）
            string key = card.Id.Entry;
            float now = Time.GetTicksMsec() / 1000f;
            if (LastVfxAt.TryGetValue(key, out var last) && now - last < 0.8f) return;
            LastVfxAt[key] = now;

            // 取目标列表（反射 private GetPossibleTargets）
            var targets = new System.Collections.Generic.List<Creature>();
            try
            {
                var raw = PossibleTargetsMethod?.Invoke(__result, null) as System.Collections.IEnumerable;
                if (raw != null)
                    foreach (var o in raw)
                        if (o is Creature c && !c.IsDead) targets.Add(c);
            }
            catch { }

            if (__result.IsMultiTargeted && targets.Count > 0)
            {
                // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] AOE detected (TargetingAll): card={key} targets={targets.Count}");
                CardVfxPlayer.PlayAoeVfx(card, targets);
            }
            else
            {
                // 非 AOE 但走了 TargetingAll（例如随机单体）⇒ 退回普通攻击演出
                int hit = HitCountReader.Get(card);
                Creature? single = targets.Count == 1 ? targets[0] : null;
                // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] targetingAll non-AOE: card={key} hit={hit} targets={targets.Count}");
                CardVfxPlayer.PlayAttackVfx(card, hit, single);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] TargetingAll attack vfx failed: {ex.Message}");
        }
    }

    internal static void Apply(Harmony harmony)
    {
        var fromCard = AccessTools.Method(typeof(AttackCommand), "FromCard");
        var withHit = AccessTools.Method(typeof(AttackCommand), "WithHitCount");
        var targeting = AccessTools.Method(typeof(AttackCommand), "Targeting",
            new[] { typeof(Creature) });

        TryBind(harmony, "AttackCommand.FromCard (multi-hit anim)", fromCard,
            AccessTools.Method(typeof(MultiHitAnimPatch), nameof(FromCardPostfix)));
        TryBind(harmony, "AttackCommand.WithHitCount (multi-hit anim)", withHit,
            AccessTools.Method(typeof(MultiHitAnimPatch), nameof(WithHitCountPostfix)));
        try
        {
            if (targeting != null)
            {
                harmony.Patch(targeting,
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(MultiHitAnimPatch), nameof(TargetingPostfix))));
                GD.Print($"[{Entry.ModId}] patched AttackCommand.Targeting (attack vfx trigger)");
            }
            else
            {
                GD.PushWarning($"[{Entry.ModId}] Targeting patch skipped (method not found)");
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] Targeting patch failed: {ex.Message}");
        }

        // ★ 无目标 AOE（TargetingAllOpponents）也要触发演出
        try
        {
            var targetingAll = AccessTools.Method(typeof(AttackCommand), "TargetingAllOpponents");
            if (targetingAll != null)
            {
                harmony.Patch(targetingAll,
                    postfix: new HarmonyMethod(AccessTools.Method(
                        typeof(MultiHitAnimPatch), nameof(TargetingAllPostfix))));
                GD.Print($"[{Entry.ModId}] patched AttackCommand.TargetingAllOpponents (AOE vfx trigger)");
            }
            else
            {
                GD.PushWarning($"[{Entry.ModId}] TargetingAllOpponents patch skipped (method not found)");
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] TargetingAllOpponents patch failed: {ex.Message}");
        }
    }

    private static void TryBind(Harmony harmony, string label,
        System.Reflection.MethodBase? target, System.Reflection.MethodInfo? postfix)
    {
        try
        {
            if (target == null || postfix == null)
            {
                GD.PushWarning($"[{Entry.ModId}] multi-hit patch skipped: {label}");
                return;
            }
            harmony.Patch(target, postfix: new HarmonyMethod(postfix));
            GD.Print($"[{Entry.ModId}] patched {label}");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] multi-hit patch failed ({label}): {ex.Message}");
        }
    }
}

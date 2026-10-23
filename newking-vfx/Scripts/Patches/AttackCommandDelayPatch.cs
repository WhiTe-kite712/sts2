using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace NewKingVfx.Patches;

/// <summary>
/// 精确延迟出伤：多段攻击的伤害延后到 <see cref="DelaySeconds"/> 秒才结算。
///
/// ═══════════════════════════════════════════════════════════════════
/// 为什么不用 WithAttackerAnim 的 delay 参数
/// ═══════════════════════════════════════════════════════════════════
/// 引擎源码 <c>CreatureCmd.TriggerAnim</c>（CreatureCmd.cs:967-1000）里：
/// <code>
/// creatureNode.SetAnimationTrigger(triggerName);
/// await Cmd.CustomScaledWait(Mathf.Min(waitTime * 0.5f, 0.25f), waitTime);
/// </code>
/// ⇒ 实际等待被压到 <c>[min(waitTime*0.5, 0.25), waitTime]</c>，还受游戏快进速度影响
///   ⇒ 传 1.05 根本等不到 1.05 秒，出伤/受击时机必然漂移。
///
/// 原版「华丽收场」（GrandFinale.cs:27-39）的正确做法是：
/// <code>
/// await Cmd.Wait(动画时长);          // 精确等待
/// await DamageCmd.Attack(...).Execute(choiceContext);
/// </code>
/// 但它改的是卡牌自己的 OnPlay。
///
/// ⇒ 本补丁采用同样思路，但**不改卡牌**：拦在 <see cref="AttackCommand.Execute"/> 的入口，
///   多段攻击先 <c>await Cmd.Wait(DelaySeconds)</c> 再放行原方法。
///
/// ⚠️ 递归保护：延迟结束后会再次调用 <c>cmd.Execute(...)</c>，那次必须放行（<see cref="Delaying"/>）。
/// ⚠️ 只拦 <c>_hitCount &gt; 1</c> 的攻击；单段攻击完全不受影响。
/// </summary>
internal static class AttackCommandDelayPatch
{
    /// <summary>多段攻击的出伤延迟（秒）= 虚影开始冲锋时刻。</summary>
    internal const float DelaySeconds = 1.05f;

    /// <summary>正在延迟中的 AttackCommand（防递归）。</summary>
    private static readonly HashSet<AttackCommand> Delaying = new();

    /// <summary>反射：<c>AttackCommand._attackerAnimName</c>（读要播的动画名）。</summary>
    private static readonly System.Reflection.FieldInfo? AnimNameField =
        AccessTools.Field(typeof(AttackCommand), "_attackerAnimName");

    /// <summary>反射：<c>AttackCommand._shouldPlayAnimation</c>（调原方法前临时关掉）。</summary>
    private static readonly System.Reflection.FieldInfo? ShouldPlayField =
        AccessTools.Field(typeof(AttackCommand), "_shouldPlayAnimation");

    /// <summary><c>AttackCommand.Execute</c> 的 Prefix。</summary>
    internal static bool Prefix(
        AttackCommand __instance,
        PlayerChoiceContext? choiceContext,
        ref Task<AttackCommand> __result)
    {
        try
        {
            if (__instance == null) return true;

            // ★ 2026-10-23 关键修复：只拦**玩家**的攻击！
            //   之前只判断 hit>1，结果把敌人的多段攻击（如 AttackTriple 3×3）也延迟了 1.05s
            //   ⇒ 用户反馈「敌人播完攻击动画后玩家才收到反馈」的真因就是这个。
            try
            {
                var attacker = __instance.Attacker;
                if (attacker == null || !attacker.IsPlayer) return true;
            }
            catch
            {
                return true;   // 读不到攻击者就放行，绝不影响游戏
            }

            // 递归放行：延迟后重入原方法时直接走原逻辑
            lock (Delaying)
            {
                if (Delaying.Contains(__instance)) return true;
            }

            int hit = HitCountReader.ReadRaw(__instance);
            if (hit <= 1) return true;   // 单段：不干预

            lock (Delaying) { Delaying.Add(__instance); }

            // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] delay attack: hit={hit} wait={DelaySeconds:0.00}s");
            __result = DelayedExecute(__instance, choiceContext);
            return false;   // 跳过原方法，由我们异步接管
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] delay attack prefix failed: {ex.Message}");
            return true;
        }
    }

    private static async Task<AttackCommand> DelayedExecute(
        AttackCommand command, PlayerChoiceContext? choiceContext)
    {
        // ── ① 先立刻触发攻击动画（否则动画要等 1.05s 才启动）──
        try
        {
            string? animName = AnimNameField?.GetValue(command) as string;
            var attacker = command.Attacker;
            if (!string.IsNullOrEmpty(animName) && attacker != null)
            {
                // waitTime 给小值：只负责把动画状态切过去，不阻塞
                _ = CreatureCmd.TriggerAnim(attacker, animName!, 0.01f);
                // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] delay attack: anim '{animName}' started immediately");
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] early anim trigger failed: {ex.Message}");
        }

        // ── ② 精确等待到出伤时刻 ──
        try
        {
            await Cmd.Wait(DelaySeconds);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] delay wait failed: {ex.Message}");
        }

        // ── ③ 调原方法，但临时关掉动画触发（否则会重播、把进度打回 0）──
        AttackCommand result;
        bool restorePlayAnim = true;
        bool hadField = false;
        try
        {
            if (ShouldPlayField != null)
            {
                object? raw = ShouldPlayField.GetValue(command);
                if (raw is bool b)
                {
                    restorePlayAnim = b;
                    hadField = true;
                    ShouldPlayField.SetValue(command, false);
                }
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] suppress reanim failed: {ex.Message}");
        }

        try
        {
            result = await command.Execute(choiceContext);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] delayed execute failed: {ex.Message}");
            result = command;
        }
        finally
        {
            if (hadField)
            {
                try { ShouldPlayField!.SetValue(command, restorePlayAnim); } catch { /* ignore */ }
            }
            lock (Delaying) { Delaying.Remove(command); }
        }
        return result;
    }

    internal static void Apply(Harmony harmony)
    {
        try
        {
            var target = AccessTools.Method(typeof(AttackCommand), "Execute",
                new[] { typeof(PlayerChoiceContext) });
            // PlayerChoiceContext? 在元数据里仍是 PlayerChoiceContext（可空只是注解）
            target ??= AccessTools.Method(typeof(AttackCommand), "Execute");
            var prefix = AccessTools.Method(typeof(AttackCommandDelayPatch), nameof(Prefix));
            if (target == null || prefix == null)
            {
                GD.PushWarning($"[{Entry.ModId}] delay attack patch skipped (missing method)");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            GD.Print($"[{Entry.ModId}] patched AttackCommand.Execute (delay {DelaySeconds:0.00}s for multi-hit)");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] delay attack patch failed: {ex.Message}");
        }
    }
}

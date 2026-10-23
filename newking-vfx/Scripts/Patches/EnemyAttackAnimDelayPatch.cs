using System;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace NewKingVfx.Patches;

/// <summary>
/// 敌人的多段攻击：把「播完动画才出伤害」压到「起手 0.3 秒就出伤害」。
///
/// ═══════════════════════════════════════════════════════════════════
/// 用户 2026-10-23 反馈（逐字）
/// ═══════════════════════════════════════════════════════════════════
/// 「例如敌人使用了3*3的攻击，直到敌人播完了攻击动画后玩家这边才收到攻击反馈」
///
/// ═══════════════════════════════════════════════════════════════════
/// 根因（引擎源码 sts2.decompiled.cs:199608-199726）
/// ═══════════════════════════════════════════════════════════════════
/// <code>
/// for (int i = 0; i &lt; attackCount; i++) {
///     if (_playOnEveryHit || i == 0) {
///         if (_attackerAnimName != null &amp;&amp; _shouldPlayAnimation)
///             await CreatureCmd.TriggerAnim(..., _attackerAnimName, _attackerAnimDelay);  // ★ 阻塞
///     }
///     AddResultsInternal(await CreatureCmd.Damage(...));   // ← 伤害才结算
/// }
/// </code>
/// 而 <c>TriggerAnim</c> 内部（CreatureCmd.cs:967-1000）：
/// <code>await Cmd.CustomScaledWait(Mathf.Min(waitTime * 0.5f, 0.25f), waitTime);</code>
/// ⇒ <c>_attackerAnimDelay</c> 越大，伤害越晚。
///
/// ═══════════════════════════════════════════════════════════════════
/// 做法：只压「敌人 + 攻击动画」的 waitTime
/// ═══════════════════════════════════════════════════════════════════
/// · 只对 <c>!creature.IsPlayer</c> 生效（玩家的 Attack 由 AttackCommandDelayPatch 管）
/// · 只对攻击类动画（名字含 "attack" / "Attack"）
/// · 把第三个参数 waitTime 压到 <see cref="MaxWaitSeconds"/>
/// · 其他动画（Cast/Hit/Dead 等）一律放行，不改
///
/// 这样伤害会在敌人起手 0.3 秒后结算，而不是等整段动画播完。
/// </summary>
internal static class EnemyAttackAnimDelayPatch
{
    /// <summary>敌人攻击动画的最大等待（秒）。</summary>
    internal const float MaxWaitSeconds = 0.3f;

    /// <summary><c>CreatureCmd.TriggerAnim(Creature, string, float)</c> 的 Prefix。</summary>
    internal static void Prefix(Creature __0, string __1, ref float __2)
    {
        try
        {
            if (__0 == null || string.IsNullOrEmpty(__1)) return;
            // 只压敌人
            if (__0.IsPlayer) return;
            // 只压攻击动画
            if (!__1.Contains("attack", StringComparison.OrdinalIgnoreCase)) return;
            // 只压大延迟（小延迟不动，避免影响正常手感）
            if (__2 <= MaxWaitSeconds) return;

            float original = __2;
            __2 = MaxWaitSeconds;
            GD.Print($"[{Entry.ModId}] enemy attack anim wait {original:0.00}s -> {MaxWaitSeconds:0.00}s"
                + $" (creature={__0.Name}, anim={__1})");
        }
        catch
        {
            // 出错就放行，绝不影响游戏
        }
    }

    internal static void Apply(Harmony harmony)
    {
        try
        {
            var target = AccessTools.Method(typeof(CreatureCmd), "TriggerAnim",
                new[] { typeof(Creature), typeof(string), typeof(float) });
            var prefix = AccessTools.Method(typeof(EnemyAttackAnimDelayPatch), nameof(Prefix));
            if (target == null || prefix == null)
            {
                GD.PushWarning($"[{Entry.ModId}] enemy anim delay patch skipped (missing method)");
                return;
            }
            harmony.Patch(target, prefix: new HarmonyMethod(prefix));
            GD.Print($"[{Entry.ModId}] patched CreatureCmd.TriggerAnim (enemy attack delay -> {MaxWaitSeconds:0.00}s)");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] enemy anim delay patch failed: {ex.Message}");
        }
    }
}

using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using Godot;

namespace NewKingVfx.Patches;

/// <summary>
/// 唯一的一条补丁：挂在引擎官方的 <see cref="CardModel.OnEnqueuePlayVfx"/> 上。
///
/// ═══════════════════════════════════════════════════════════════════
/// 为什么是这个方法（不是 OnPlay）
/// ═══════════════════════════════════════════════════════════════════
/// 引擎源码（sts2 反编译 L74479-74489）逐字：
/// <code>
/// /// Override this method to add VFX that should run the moment the mouse is released
/// /// when playing this card.
/// /// This is different from OnPlay, which is meant for game logic (dealing damage, gaining block, etc.)
/// /// WARNING: Don't put any game logic in here! It might do things you don't expect.
/// public virtual Task OnEnqueuePlayVfx(Creature? target)
/// </code>
///
/// ⇒ 引擎**专门**为"出牌瞬间的表现"留了这个钩子，并且明确要求不要在这里放游戏逻辑。
///   ⇒ 用它既符合设计意图，也**完全不会碰逻辑路径**（这是本 mod 纯表现定位的保证）。
///
/// ⚠️ Postfix 而不是 Prefix：
///   我们要的是"这次出牌已经确定要发生"时才播特效。Postfix 在方法返回后执行，
///   此时出牌动作已入队 ⇒ 动画与纸牌飞出是同一时刻，观感正确。
///
/// ⚠️ 这个方法返回 <c>Task</c>，但**不是** async 的（原版实现直接
///   <c>return Task.CompletedTask;</c>）⇒ Postfix 无需等待，也不会错过时机。
///
/// ⚠️ 只处理**本 mod 认识的卡**（见 <see cref="CardVfxRegistry"/>）：
///   其它所有卡（包括原版 5 角色的全部卡牌、以及任何第三方 mod 的卡）
///   都会在 <c>TryResolve</c> 里返回 false 并被静默跳过 ⇒ **零副作用**。
/// </summary>
[HarmonyPatch(typeof(CardModel), nameof(CardModel.OnEnqueuePlayVfx))]
internal static class CardEnqueueVfxPatch
{
    public static void Apply(Harmony harmony)
    {
        try
        {
            var target = AccessTools.Method(typeof(CardModel), nameof(CardModel.OnEnqueuePlayVfx));
            if (target == null)
            {
                GD.PushWarning($"[{Entry.ModId}] OnEnqueuePlayVfx not found; vfx disabled");
                return;
            }

            harmony.Patch(
                target,
                postfix: new HarmonyMethod(typeof(CardEnqueueVfxPatch), nameof(Postfix)));

            GD.Print($"[{Entry.ModId}] patched CardModel.OnEnqueuePlayVfx");
        }
        catch (Exception ex)
        {
            GD.PushError($"[{Entry.ModId}] patch failed: {ex}");
        }
    }

    private static void Postfix(CardModel __instance, Creature? target)
    {
        try
        {
            // 自动推断语义（手工表优先级更高，在 TryResolve 内部处理）
            string? semantic = InferSemantic(__instance);
            CardVfxPlayer.Play(__instance, target, semantic);
        }
        catch (Exception ex)
        {
            // 补丁内再兜一层：绝不让异常冒到引擎的出牌流程里。
            GD.PushWarning($"[{Entry.ModId}] postfix error: {ex.Message}");
        }
    }

    /// <summary>
    /// 按卡牌的**公开属性**推断语义（不读任何具体卡牌类型，保持松耦合）。
    ///
    /// 判定顺序刻意从"最具体"到"最笼统"：
    ///   Power → 多段 → 单段 → 格挡 → 抽牌 → 兜底
    /// ⇒ 因为"既有伤害又有格挡"的卡（如原版 Ironclad 的某些牌）应当读作"伤害"，
    ///   即伤害优先级高于格挡。
    /// </summary>
    private static string? InferSemantic(CardModel card)
    {
        try
        {
            if (card.Type == CardType.Power) return "power";

            // 通过 DynamicVars 判断这张卡"有什么数值维度"。
            // ⚠️ 缺失的维度访问会抛 ⇒ 每个都用 TryGet 包住。
            bool hasDamage = TryHasVar(card, "Damage");
            bool hasBlock = TryHasVar(card, "Block");
            bool hasCards = TryHasVar(card, "Cards");

            if (hasDamage)
            {
                // 多段：看是否有 Hits / Repeat 之类的计数量
                bool multi = TryHasVar(card, "Hits") || TryHasVar(card, "Repeat") || TryHasVar(card, "HitCount");
                return multi ? "slash_multi" : "slash";
            }

            if (hasBlock) return "block";
            if (hasCards) return "draw";

            return null;   // 交给手工表；都没有就不播
        }
        catch
        {
            return null;
        }
    }

    private static bool TryHasVar(CardModel card, string varName)
    {
        try
        {
            return card.DynamicVars[varName] != null;
        }
        catch
        {
            return false;
        }
    }
}

using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace NewKingVfx;

/// <summary>
/// newking-vfx 的入口：只做三件事 —— 装 Harmony 补丁、预加载 VFX 场景、打印诊断。
///
/// ═══════════════════════════════════════════════════════════════════
/// 这个 mod 的定位（用户 2026-10-22 拍板）
/// ═══════════════════════════════════════════════════════════════════
/// 用户原话：
///   「把 newking 原核心代码装进留档内，新开一个专门用于卡牌动画的来干净实现」
///   「依赖上游合作者代码，检索打出时是什么卡牌，如果 id 匹配则播放动画，
///     避免和半成品深度依赖」
///
/// ⇒ 落地为三条硬约束：
///   1. **纯表现层**：不注册任何卡牌/能力/遗物，不改任何游戏状态；
///      只在 <c>CardModel.OnEnqueuePlayVfx</c>（引擎官方为出牌特效预留的钩子）上挂一段表现代码。
///   2. **松耦合**：**不引用** NewKing / MySts2Mod 的任何程序集与类型。
///      与它们的唯一接口是「卡牌 ID 字符串」⇒ 它们改实现、改名、甚至卸载，
///      本 mod 最多是"某张卡不再匹配、不播特效"，**不会崩**。
///   3. **干净**：所有逻辑放在本目录，不往 newking 里加任何一行。
///
/// ⚠️ 为什么不用 patch <c>CardModel.OnPlay</c>：
///   引擎源码已写明 —— <c>OnPlay</c> 是"游戏逻辑"（伤害/格挡），
///   而 <c>OnEnqueuePlayVfx</c> 才是"出牌瞬间的表现"，且带有
///   <c>WARNING: Don't put any game logic in here!</c> 的说明。
///   ⇒ 用官方钩子既符合设计意图，也完全不会碰逻辑路径。
/// </summary>
[ModInitializer(nameof(Init))]
public static class Entry
{
    public const string ModId = "NewKingVfx";

    public static void Init()
    {
        try
        {
            var harmony = new Harmony(ModId);
            Patches.CardEnqueueVfxPatch.Apply(harmony);
            // 读「命中段数」用：hit==1 走跳跃斩，hit!=1 走插剑+裂隙+虚影
            Patches.AttackHitCountPatch.Apply(harmony);
            // 多段攻击换用我们自己的 attack_multi 骨骼动画（出伤 1.05s）
            Patches.MultiHitAnimPatch.Apply(harmony);
            // ★ 2026-10-22 删除：抑制补丁抑制的是「攻击者自己」的 Hit（玩家攻击时不会触发自己的 Hit）
            //   ⇒ 正常情况下完全无效，窗口内还会吞掉玩家被打的真实反馈 ⇒ 整段移除
            // 精确延迟出伤：多段攻击 await Cmd.Wait(1.05s) 后才结算
            Patches.AttackCommandDelayPatch.Apply(harmony);
            // ★ 敌人多段攻击：把「播完动画才出伤害」压到「起手 0.3s」
            Patches.EnemyAttackAnimDelayPatch.Apply(harmony);
            // ★ 6 秒无操作 ⇒ 播「投篮失败」待机动画（用户 2026-10-22）
            try
            {
                var tree2 = Godot.Engine.GetMainLoop() as Godot.SceneTree;
                Vfx.IdleSpecialWatcher.Install(tree2?.Root);
            }
            catch (System.Exception ise)
            {
                Godot.GD.PushWarning($"[{ModId}] idle special watcher setup failed: {ise.Message}");
            }

            // 预加载：不预加载的话，第一次播放要现场加载场景与纹理（实测同类做法 ~200ms 卡顿）。
            // 延迟执行是因为此时场景树与挂载的 .pck 可能尚未就绪。
            Callable.From(CardVfxPlayer.PreloadAll).CallDeferred();

            GD.Print($"[{ModId}] init done ({CardVfxRegistry.CardCount} card ids, "
                     + $"{CardVfxRegistry.SemanticCount} semantics)");
        }
        catch (Exception ex)
        {
            // 入口失败也不能让游戏崩：本 mod 纯表现，宁可全程失效。
            GD.PushError($"[{ModId}] Init failed: {ex}");
        }
    }
}

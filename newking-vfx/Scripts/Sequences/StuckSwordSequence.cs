using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using NewKingVfx.Vfx;

namespace NewKingVfx.Sequences;

/// <summary>
/// 多段攻击演出：跪地插剑 → 裂隙蔓延 → 虚影冲锋。
///
/// ═══════════════════════════════════════════════════════════════════
/// 用户 2026-10-22 的设计（逐字，按确认顺序）
/// ═══════════════════════════════════════════════════════════════════
/// ① 「角色把剑竖直插在水平地上」→ Spine 动画 <c>attack_multi</c>（格斗姿态由骨骼承担）
/// ② 「闪光裂痕向目标敌人从角色脚下传播」→ <see cref="CrackGrowth"/>
/// ③ 「到达敌人是类似于死亡行军的特效」→ 敌人端冲击
/// ④ 「每间隔0.2s冲出1个虚影角色，共N（N&lt;=3）次」→ <see cref="GhostRingCharge"/>
/// ⑤ 「虚影采用3点钟±30deg、9点钟±30deg，脚底始终在下」→ 见 <see cref="GhostRingCharge"/>
/// ⑥ 「出伤时刻定在虚影开始冲锋时」→ <see cref="MultiHitAnimPatch"/> 的 1.05s
///
/// ═══════════════════════════════════════════════════════════════════
/// 与 Spine 动画的分工
/// ═══════════════════════════════════════════════════════════════════
/// 姿势（跪地/插剑/拔剑）**全部由我们自己的 <c>attack_multi</c> 骨骼动画承担**
/// （<see cref="MultiHitAnimPatch"/> 负责把攻击牌切到该动画）。
/// 本类**只负责叠加特效**：裂隙 + 虚影。二者互不阻塞：
///   · 动画由引擎侧播放（OnEnqueuePlayVfx 触发后即开始）
///   · 特效用 SceneTreeTimer 按相对时间排队
/// </summary>
internal static class StuckSwordSequence
{
    // ─────────────────────────────────────────────────────────────
    // 时间轴（秒，相对出牌瞬间）—— 与 attack_multi 的 2.06s 对齐
    // ─────────────────────────────────────────────────────────────

    /// <summary>裂隙开始生长时刻（跪地+插剑约 0.4s 完成，略提前起裂更连贯）。</summary>
    private const float CrackStartSeconds = 0.30f;

    /// <summary>裂隙生长总时长（从起点到敌人）。</summary>
    private const float CrackGrowSeconds = 0.75f;

    /// <summary>第一个虚影冲出时刻 = 出伤时刻（1.05s）。</summary>
    private const float FirstGhostSeconds = 1.05f;

    /// <summary>虚影之间的间隔（用户要求 0.2s）。</summary>
    private const float GhostIntervalSeconds = 0.20f;

    /// <summary>虚影数上限（用户要求 N ≤ 3）。</summary>
    private const int MaxGhosts = 3;

    /// <summary>裂隙被拔剑打断的时刻（attack_multi 的 130 帧 = 1.30s）。</summary>
    private const float SwordPullSeconds = 1.30f;

    /// <summary>整段演出结束时刻（attack_multi 的 2.06s）。</summary>
    private const float EndSeconds = 2.06f;

    private const float CrackTargetLength = 620f;

    /// <summary>主入口：播放整套特效。立刻返回，不阻塞出牌流程。</summary>
    internal static void Play(CardModel card, Creature? target)
    {
        try
        {
            var room = NCombatRoom.Instance;
            var container = room?.CombatVfxContainer;
            if (room == null || container == null) return;

            var owner = card.Owner;
            if (owner == null) return;

            var selfNode = room.GetCreatureNode(owner.Creature);
            if (selfNode == null || !selfNode.IsInsideTree()) return;

            NCreature? enemyNode = null;
            if (target != null && !target.IsDead)
                enemyNode = room.GetCreatureNode(target);

            int hit = HitCountReader.Get(card);
            int ghostCount = Math.Clamp(hit, 1, MaxGhosts);
            // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] StuckSword: card={card.Id.Entry} hit={hit} ghosts={ghostCount}");

            var tree = selfNode.GetTree();
            if (tree == null) return;

            // ═══ 阶段 1（0.30s）：裂隙开始从脚下向敌人蔓延 ═══
            tree.CreateTimer(CrackStartSeconds).Timeout += () =>
            {
                if (!GodotObject.IsInstanceValid(selfNode)) return;
                var crack = BuildCrack(container, selfNode, enemyNode);
                if (crack == null) return;

                // 拔剑时停止生长（视觉冻结），随后淡出
                tree.CreateTimer(SwordPullSeconds - CrackStartSeconds).Timeout += () =>
                {
                    if (!GodotObject.IsInstanceValid(crack)) return;
                    crack.StopGrowing();
                    crack.BeginFadeOut();
                };
            };

            // ═══ 阶段 2（1.05s 起，每 0.2s 一个）：虚影冲锋 ═══
            for (int i = 0; i < ghostCount; i++)
            {
                int idx = i;
                float delay = FirstGhostSeconds + i * GhostIntervalSeconds;
                tree.CreateTimer(delay).Timeout += () =>
                {
                    if (enemyNode == null || !GodotObject.IsInstanceValid(enemyNode)) return;
                    var ghosts = new GhostRingCharge();
                    container.AddChildSafely(ghosts);
                    var rng = new RandomNumberGenerator();
                    rng.Seed = (ulong)(Time.GetTicksMsec() + (ulong)idx * 7919);
                    ghosts.SpawnWave(enemyNode, 1, rng);   // 每次只放 1 个（间隔由外层控制）
                    SpawnImpact(room, enemyNode.GlobalPosition);
                };
            }

            // ═══ 阶段 3：清理（2.06s 后无残留）═══
            tree.CreateTimer(EndSeconds + 0.4f).Timeout += () =>
            {
                // 所有特效节点自己 QueueFree，这里只留日志便于排查
                // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] StuckSword: done card={card.Id.Entry}");
            };
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] StuckSword failed: {ex.Message}");
        }
    }

    /// <summary>在角色脚下建裂隙节点，指向敌人。</summary>
    private static CrackGrowth? BuildCrack(Node container, NCreature selfNode, NCreature? enemyNode)
    {
        try
        {
            // ★ 用 GlobalPosition：特效节点挂在 CombatVfxContainer 下，
            //   而 NCreature.Position 是相对 creature 容器的局部坐标 ⇒ 混用会错位
            var from = selfNode.GlobalPosition;
            var to = enemyNode != null ? enemyNode.GlobalPosition : from + new Vector2(CrackTargetLength, 0f);
            var delta = to - from;
            float dist = Mathf.Max(60f, delta.Length());
            float angle = Mathf.RadToDeg(Mathf.Atan2(delta.Y, delta.X));

            var crack = new CrackGrowth
            {
                RotationDegrees = angle,
                GrowSeconds = CrackGrowSeconds,
                TargetLength = dist,
            };
            container.AddChildSafely(crack);
            // ★ GlobalPosition 必须在入树之后设置，否则 Godot 会退化成局部坐标
            crack.GlobalPosition = from;
            return crack;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] crack build failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>敌人端冲击闪光（简单一圈，避免依赖原版场景）。</summary>
    private static void SpawnImpact(NCombatRoom room, Vector2 at)
    {
        try
        {
            var container = room.CombatVfxContainer;
            if (container == null) return;
            var ring = new ImpactRing { ZIndex = 8 };
            container.AddChildSafely(ring);
            ring.GlobalPosition = at;   // ★ 入树后再设
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] impact spawn failed: {ex.Message}");
        }
    }
}

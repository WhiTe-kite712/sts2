using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace NewKingVfx.Sequences;

/// <summary>
/// 跳跃下劈演出（Jump Slam）：攻击牌打出时"跃起到敌人头顶 → 快速下坠 → 下劈"的整套表现。
///
/// ═══════════════════════════════════════════════════════════════════
/// 用户 2026-10-22 的设计（逐字）
/// ═══════════════════════════════════════════════════════════════════
/// 「举起的前摇加成，劈下的速度变快，并且模型本身向敌人跳跃，
///   到敌人头顶快速下坠并下劈」
///
/// ⇒ 拆成五个阶段（见 <see cref="Play"/> 里的时间轴注释）。
///
/// ═══════════════════════════════════════════════════════════════════
/// 为什么用【节点坐标移动】而不是 Spine 动画位移
/// ═══════════════════════════════════════════════════════════════════
/// Spine 的 <c>root</c> 骨骼 translate 只能做**固定轨迹**（前进 150 / 上升 170），
/// 而敌人位置是动态的 ⇒ 跳不到敌人头上。
///
/// ⇒ 改为游戏层移动 <c>NCreature</c> 节点本身（本仓 <c>issac</c> 已有的成熟范式）：
///   · <c>NCombatRoom.GetCreatureNode(creature)</c> 取单位节点
///   · 与目标同容器 ⇒ 写 <c>Position</c>（局部坐标，见 WarPetSummoner.cs:290 的注释）
///   · 用 <c>Tween</c> 做轨迹补间
///
/// ⚠️ 只写 <c>NCreature</c> 节点、**不写** <c>NCreature.Body</c>（精灵）——
///   后者会让血条/名字牌/意图栏与本体脱节（IssacArenaLayout.cs:43-48 的教训）。
///
/// ⚠️ <c>NCreature</c> 是**复用节点** ⇒ 所有变换必须在结束时恢复原值，
///   否则后续所有动画都会错位（同 <see cref="IaiSlashSequence"/> 的 <c>_restore</c> 约束）。
/// </summary>
internal static class JumpSlashSequence
{
    // ─────────────────────────────────────────────────────────────
    // 时间轴（秒）—— 全部相对"打出的瞬间"
    // ─────────────────────────────────────────────────────────────

    /// <summary>阶段1：前摇蓄势（下蹲 + 后仰）。用户要求"举起的前摇加成" ⇒ 比居合斩长。</summary>
    private const float WindUpSeconds = 0.26f;

    /// <summary>
    /// 阶段2：跃起 → 敌人头顶（上升段）。
    /// 用户 2026-10-22：「跳跃速度增加 100%」⇒ 由 0.24s 减半为 <b>0.12s</b>。
    /// </summary>
    private const float JumpUpSeconds = 0.12f;

    /// <summary>
    /// 阶段3：最高点顿帧。
    /// 用户 2026-10-22：「在最高点顿帧」⇒ 跃到顶点后停住一拍（蓄力感 + 让玩家看清位置）。
    /// </summary>
    private const float ApexHoldSeconds = 0.10f;

    /// <summary>阶段4：头顶快速下坠（用户要求"劈下的速度变快"）。</summary>
    private const float FallSeconds = 0.11f;

    /// <summary>阶段5：落地保持（劈到底的定格）。</summary>
    private const float HitHoldSeconds = 0.10f;

    /// <summary>阶段6：回到原位。</summary>
    private const float ReturnSeconds = 0.26f;

    /// <summary>
    /// 角色节点的「真实原点」快照（位置 + 朝向/缩放/透明度）。
    /// ★ 用户 2026-10-23：缓存起点时必须一起缓存模型朝向，否则会出现错误旋转/缩放。
    /// 只在「没有动画在跑」时刷新；有动画在跑时一律用缓存值。
    /// </summary>
    private struct NodeSnapshot
    {
        public Vector2 Position;
        public Vector2 Scale;
        public float Rotation;
        public Color Modulate;
    }

    private static readonly System.Collections.Generic.Dictionary<ulong, NodeSnapshot> OriginalSnapshots = new();

    /// <summary>正在播放该角色的 tween（新序列会先 Kill 旧的）。</summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, Tween?> ActiveTweens = new();

    /// <summary>
    /// 连打排队的「追加下劈」次数。
    /// ★ 用户 2026-10-23：前一张动画未结束前打下一张攻击牌时，
    ///   不要 Kill 重播，而是让当前序列结束后「再劈一次」。
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<ulong, int> PendingExtras = new();

    // ─────────────────────────────────────────────────────────────
    // 蓄势姿态参数
    // ─────────────────────────────────────────────────────────────

    /// <summary>蓄势时前倾角度（度）。</summary>
    private const float LeanDegrees = -6f;

    /// <summary>蓄势时垂直压缩比（下蹲）。</summary>
    private const float CrouchScaleY = 0.88f;

    /// <summary>蓄势时水平略展（前伸感）。</summary>
    private const float CrouchScaleX = 1.05f;

    /// <summary>蓄势下移像素。</summary>
    private const float CrouchOffsetY = 16f;

    // ─────────────────────────────────────────────────────────────
    // 跳跃位置参数
    // ─────────────────────────────────────────────────────────────

    /// <summary>跃起时相对敌人位置上方的像素（越高越夸张）。</summary>
    private const float ApexOffsetY = -230f;

    /// <summary>跃起时相对敌人位置的水平偏移（0 = 正上方）。</summary>
    private const float ApexOffsetX = 0f;

    /// <summary>上升途中的前倾角度（度）。</summary>
    private const float AirLeanDegrees = -14f;

    /// <summary>下坠终点相对敌人位置的垂直偏移（略高于敌人中心 = 劈在头上）。</summary>
    private const float HitOffsetY = -10f;

    /// <summary>落地瞬间的垂直压缩比（冲击感）。</summary>
    private const float LandScaleY = 0.93f;

    /// <summary>落地瞬间的水平拉伸比。</summary>
    private const float LandScaleX = 1.07f;

    // ─────────────────────────────────────────────────────────────
    // 入口
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 在任意节点上播完整的「跳跃下劈」运动（前摇→跃起→顿帧→下劈→命中→回位）。
    /// ★ 供 AOE 分身复用：selfNode 可以是 NCreature，也可以是 SpineSprite 副本。
    /// </summary>
    internal static void PlayOnNode(NCombatRoom room, Node selfNode, NCreature? enemyNode, string tag)
    {
        try
        {
            if (room == null || selfNode == null || !GodotObject.IsInstanceValid(selfNode)) return;
            float t0 = Time.GetTicksMsec() / 1000f;
            // [perf-off 2026-10-23] Log($"BEGIN node={tag} target={enemyNode?.Name ?? "-"} expectHit={WindUpSeconds + JumpUpSeconds + ApexHoldSeconds + FallSeconds:0.00}s");

            // ── 记录原始状态（必须原样恢复）──
            // ★ 2026-10-23 Bug 修复：快速连打攻击牌时，前一个序列可能还没播完，
            //   此时 selfNode.Position 已经被前一个序列改成了「下劈位置」；
            //   如果直接记录当前 Position，新序列就会把「下劈位置」当成原点，
            //   结束时 Restore 到那里 ⇒ 模型滞留在下劈位置。
            //   ⇒ 用缓存：第一次见到该角色时记录真正的原点，之后一直用缓存值。
            ulong nodeId = selfNode.GetInstanceId();
            bool hasActiveTween = ActiveTweens.TryGetValue(nodeId, out var prevTween)
                && prevTween != null && prevTween.IsValid();
            NodeSnapshot snap;
            if (hasActiveTween)
            {
                // ★ 用户 2026-10-23：前一张动画未结束 ⇒ 不重播，排队「追加一次下劈」
                int pending = PendingExtras.TryGetValue(nodeId, out var n) ? n : 0;
                PendingExtras[nodeId] = pending + 1;
                // [perf-off 2026-10-23] Log($"queued extra slash for node {nodeId} (pending={pending + 1}) - skip replay");
                return;
            }
            else
            {
                // 没有动画在跑 ⇒ 当前位置/朝向就是真实状态（战斗站位调整等「非本动画原因」的移动）
                // ★ 用户 2026-10-23：非该动画原因移动角色时缓存该状态，接下来均使用该固定值
                snap = CaptureSnapshot(selfNode);
                OriginalSnapshots[nodeId] = snap;
                // [perf-off 2026-10-23] Log($"refresh original snapshot for node {nodeId}: pos={snap.Position} scale={snap.Scale} rot={snap.Rotation}");
            }
            var originalPos = snap.Position;
            var originalScale = snap.Scale;
            var originalRotation = snap.Rotation;
            var originalModulate = snap.Modulate;

            // ── 目标位置 ──
            Vector2 apexPos = originalPos;                          // 无敌人 ⇒ 原地跳
            Vector2 hitPos = originalPos;
            if (enemyNode != null)
            {
                apexPos = enemyNode.Position + new Vector2(ApexOffsetX, ApexOffsetY);
                hitPos = enemyNode.Position + new Vector2(0f, HitOffsetY);
            }

            var tween = selfNode.CreateTween();
            tween.SetProcessMode(Tween.TweenProcessMode.Idle);
            ActiveTweens[nodeId] = tween;

            // ═══════════════════════════════════════════════════
            // 阶段 1：前摇蓄势（下蹲 + 前倾）
            // ═══════════════════════════════════════════════════
            tween.TweenProperty(selfNode, "position", originalPos + new Vector2(0f, CrouchOffsetY), WindUpSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.Parallel()
                 .TweenProperty(selfNode, "scale", new Vector2(CrouchScaleX, CrouchScaleY), WindUpSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.Parallel()
                 .TweenProperty(selfNode, "rotation_degrees", LeanDegrees, WindUpSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

            // ═══════════════════════════════════════════════════
            // 阶段 2：跃起 → 敌人头顶（上升段，减速）
            //   用户 2026-10-22「跳跃速度增加 100%」⇒ JumpUpSeconds 已减半
            // ═══════════════════════════════════════════════════
            tween.TweenProperty(selfNode, "position", apexPos, JumpUpSeconds)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Parallel()
                 .TweenProperty(selfNode, "scale", Vector2.One, JumpUpSeconds)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Parallel()
                 .TweenProperty(selfNode, "rotation_degrees", AirLeanDegrees, JumpUpSeconds)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);

            // ═══════════════════════════════════════════════════
            // 阶段 3：最高点顿帧（用户 2026-10-22「在最高点顿帧」）
            //   跃到顶点后停住一拍：蓄力感 + 让玩家看清落点
            // ═══════════════════════════════════════════════════
            // [perf-off 2026-10-23] tween.TweenCallback(Callable.From(() =>
                // [perf-off 2026-10-23] Log($"APEX t={Time.GetTicksMsec() / 1000f - t0:0.000}s")));
            tween.TweenInterval(ApexHoldSeconds);

            // ═══════════════════════════════════════════════════
            // 阶段 4：下坠 —— ⚡ 打击特效与下坠【同时】出现
            //   用户 2026-10-22「然后下坠同时出现打击特效」
            //   ⇒ 特效不再是"落地后才炸"，而是"角色带着特效一起砸下来"
            // ═══════════════════════════════════════════════════
            tween.TweenCallback(Callable.From(() =>
            {
                // [perf-off 2026-10-23] Log($"FALL+FX t={Time.GetTicksMsec() / 1000f - t0:0.000}s");

                if (enemyNode != null)
                {
                    IaiSlashSequence.SpawnSlashVfx(room, enemyNode.VfxSpawnPosition);
                }
                else
                {
                    IaiSlashSequence.SpawnSlashVfx(room, hitPos);
                }
            }));
            tween.TweenProperty(selfNode, "position", hitPos, FallSeconds)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel()
                 .TweenProperty(selfNode, "scale", new Vector2(LandScaleX, LandScaleY), FallSeconds)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel()
                 .TweenProperty(selfNode, "rotation_degrees", 0f, FallSeconds)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);

            // ═══════════════════════════════════════════════════
            // 阶段 5：落地保持（劈到底的定格）
            // ═══════════════════════════════════════════════════
            // [perf-off 2026-10-23] tween.TweenCallback(Callable.From(() =>
                // [perf-off 2026-10-23] Log($"LAND (hit timing) t={Time.GetTicksMsec() / 1000f - t0:0.000}s")));
            tween.TweenInterval(HitHoldSeconds);

            // ═══════════════════════════════════════════════════
            // 阶段 5：回到原位 + 恢复姿态
            // ═══════════════════════════════════════════════════
            tween.TweenProperty(selfNode, "position", originalPos, ReturnSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.Parallel()
                 .TweenProperty(selfNode, "scale", originalScale, ReturnSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.Parallel()
                 .TweenProperty(selfNode, "rotation_degrees", originalRotation, ReturnSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.Parallel()
                 .TweenProperty(selfNode, "modulate:a", originalModulate.A, ReturnSeconds);

            // ⚠️ 无论 Tween 是否被中途打断，最终都要把状态写回原值
            tween.Finished += () =>
            {
                // [perf-off 2026-10-23] Log($"END t={Time.GetTicksMsec() / 1000f - t0:0.000}s");
                Restore((Node)selfNode, originalPos, originalScale, originalRotation, originalModulate);
                // 快照已恢复，清掉缓存（下次无动画时重新抓取最新状态）
                OriginalSnapshots.Remove(nodeId);

                // ★ 连打排队：还有 pending ⇒ 追加一次下劈
                if (PendingExtras.TryGetValue(nodeId, out var pend) && pend > 0)
                {
                    PendingExtras[nodeId] = pend - 1;
                    // [perf-off 2026-10-23] Log($"play extra slash for node {nodeId} (remaining={pend - 1})");
                    var extraSnap = new NodeSnapshot
                    {
                        Position = originalPos,
                        Scale = originalScale,
                        Rotation = originalRotation,
                        Modulate = originalModulate,
                    };
                    PlayExtraSlash(selfNode, extraSnap, enemyNode, room);
                }
                // 只有当前 tween 是自己时才清理（避免误删后启动的序列）
                if (ActiveTweens.TryGetValue(nodeId, out var cur) && cur == tween)
                {
                    ActiveTweens.Remove(nodeId);
                }
            };
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] jump slash (node) failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 播放整个跳跃下劈序列。
    ///
    /// ⚠️ 全程 **不 await**（纯 Tween 驱动，立即返回）：
    ///   出牌流程不应该被特效阻塞 —— 伤害结算、卡牌移动都要照常进行。
    ///   所有状态恢复都写在 Tween 的回调里。
    /// </summary>
    internal static void Play(CardModel card, Creature? target)
    {
        try
        {
            var room = NCombatRoom.Instance;
            if (room == null) return;

            var owner = card.Owner;
            if (owner == null) return;

            var selfNode = room.GetCreatureNode(owner.Creature);
            if (selfNode == null || !selfNode.IsInsideTree()) return;

            // ── 日志：记录起始时刻，便于在 godot.log 里核对实际节奏 ──
            float t0 = Time.GetTicksMsec() / 1000f;
            // [perf-off 2026-10-23] Log($"BEGIN card={card.Id.Entry} target={target?.Name ?? "-"} " +
                // [perf-off 2026-10-23] $"windUp={WindUpSeconds} jump={JumpUpSeconds} apexHold={ApexHoldSeconds} " +
                // [perf-off 2026-10-23] $"fall={FallSeconds} hitHold={HitHoldSeconds} return={ReturnSeconds} " +
                // [perf-off 2026-10-23] $"expectHit={WindUpSeconds + JumpUpSeconds + ApexHoldSeconds + FallSeconds:0.00}s");

            // 敌人节点（决定"头顶"在哪）；没有目标就只做原地起跳
            NCreature? enemyNode = null;
            if (target != null && !target.IsDead)
            {
                enemyNode = room.GetCreatureNode(target);
            }

            // ★ 复用统一的节点运动实现
            PlayOnNode(room, selfNode, enemyNode, card.Id.Entry);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] jump slash failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 「追加一次下劈」：只做「跳到敌人上方 → 下劈命中 → 回原位」。
    /// 没有前摇/跃起，因为角色已经在战斗节奏里了。
    /// </summary>
    private static void PlayExtraSlash(Node selfNode, NodeSnapshot snap, NCreature? enemyNode, NCombatRoom room)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(selfNode)) return;
            float t0 = Time.GetTicksMsec() / 1000f;
            Vector2 hitPos = snap.Position;
            Vector2 apexPos = snap.Position;
            if (enemyNode != null && GodotObject.IsInstanceValid(enemyNode))
            {
                apexPos = enemyNode.Position + new Vector2(ApexOffsetX, ApexOffsetY * 0.6f);
                hitPos = enemyNode.Position + new Vector2(0f, HitOffsetY);
            }
            var tween = selfNode.CreateTween();
            tween.SetProcessMode(Tween.TweenProcessMode.Idle);
            ulong nodeId = selfNode.GetInstanceId();
            ActiveTweens[nodeId] = tween;

            // [perf-off 2026-10-23] Log($"EXTRA BEGIN t={0:0.000}");
            // 直接跳到敌人上方（0.10s）
            tween.TweenProperty(selfNode, "position", apexPos, 0.10f)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
            tween.Parallel().TweenProperty(selfNode, "scale", new Vector2(1f, 1f), 0.10f);
            tween.Parallel().TweenProperty(selfNode, "rotation_degrees", AirLeanDegrees, 0.10f);
            // 下劈（0.11s）
            tween.TweenCallback(Callable.From(() =>
            {
                if (enemyNode != null && GodotObject.IsInstanceValid(enemyNode))
                    IaiSlashSequence.SpawnSlashVfx(room, enemyNode.VfxSpawnPosition);
                else
                    IaiSlashSequence.SpawnSlashVfx(room, hitPos);
            }));
            tween.TweenProperty(selfNode, "position", hitPos, FallSeconds)
                 .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
            tween.Parallel().TweenProperty(selfNode, "scale", new Vector2(LandScaleX, LandScaleY), FallSeconds);
            tween.Parallel().TweenProperty(selfNode, "rotation_degrees", 0f, FallSeconds);
            // 命中顿帧
            tween.TweenInterval(HitHoldSeconds);
            // 回原位
            tween.TweenProperty(selfNode, "position", snap.Position, ReturnSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
            tween.Parallel().TweenProperty(selfNode, "scale", snap.Scale, ReturnSeconds);
            tween.Parallel().TweenProperty(selfNode, "rotation_degrees", snap.Rotation, ReturnSeconds);
            tween.Finished += () =>
            {
                // [perf-off 2026-10-23] Log($"EXTRA END t={Time.GetTicksMsec() / 1000f - t0:0.000}s");
                ApplySnapshot(selfNode, snap);
                if (ActiveTweens.TryGetValue(nodeId, out var cur) && cur == tween)
                    ActiveTweens.Remove(nodeId);
                // 还有排队 ⇒ 继续
                if (PendingExtras.TryGetValue(nodeId, out var pend) && pend > 0)
                {
                    PendingExtras[nodeId] = pend - 1;
                    PlayExtraSlash(selfNode, snap, enemyNode, room);
                }
            };
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] extra slash failed: {ex.Message}");
        }
    }

    /// <summary>抓取节点当前完整状态（位置 + 朝向/缩放/透明度）。</summary>
    private static NodeSnapshot CaptureSnapshot(Node node)
        => new NodeSnapshot
        {
            Position = node.Get("position").AsVector2(),
            Scale = node.Get("scale").AsVector2(),
            Rotation = node.Get("rotation").AsSingle(),
            Modulate = ((CanvasItem)node).Modulate,
        };

    /// <summary>把节点恢复到快照状态（位置 + 朝向/缩放/透明度）。</summary>
    private static void ApplySnapshot(Node node, NodeSnapshot snap)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.Set("position", snap.Position);
            node.Set("scale", snap.Scale);
            node.Set("rotation", snap.Rotation);
            ((CanvasItem)node).Modulate = snap.Modulate;
        }
        catch { /* 表现层静默 */ }
    }

    /// <summary>统一日志出口（写到 godot.log，前缀便于过滤）。</summary>
    private static void Log(string msg)
        => GD.Print($"[{Entry.ModId}] JumpSlash: {msg}");

    /// <summary>恢复节点到原始状态（幂等，可在任何时机调用）。</summary>
    private static void Restore(
        Node node, Vector2 pos, Vector2 scale, float rotation, Color modulate)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.Set("position", pos);
            node.Set("scale", scale);
            node.Set("rotation", rotation);
            ((CanvasItem)node).Modulate = modulate;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] jump slash restore failed: {ex.Message}");
        }
    }
}

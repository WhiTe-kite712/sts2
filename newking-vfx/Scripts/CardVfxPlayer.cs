using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace NewKingVfx;

/// <summary>
/// 通用 VFX 播放器：把一个"语义"翻译成一个原版 VFX 场景，实例化、定位、染色、挂到战斗层。
///
/// ═══════════════════════════════════════════════════════════════════
/// 设计纪律（抄自 DeathMarchCharge 的工程经验，见其 GhostChargeDirector.cs:37-111）
/// ═══════════════════════════════════════════════════════════════════
/// 1. **全流程 try/catch**，出错只 PushWarning：本 mod 是纯表现，任何失败都不该影响出牌。
/// 2. **早退保护齐全**：room / container / anchor / scene 任一为 null 就静默返回。
/// 3. **预加载**：不预加载的话首次播放要现场加载场景与纹理（同类做法实测 ~200ms 卡顿）。
/// 4. **节流**：同一回合内同类特效合并，避免"一回合打 10 张牌冒出 10 个特效"。
///
/// ⚠️ 与 DeathMarchCharge 的差异：那边是单卡专用 director（875 行），
///   这里是**给 66 张卡共用的通用播放器**，所以只保留"实例化 + 定位 + 染色"，
///   不做逐帧导演（原版 VFX 场景自带自己的动画）。
/// </summary>
internal static class CardVfxPlayer
{
    /// <summary>已加载的场景缓存（key = 资源路径）。</summary>
    private static readonly Dictionary<string, PackedScene?> SceneCache = new(StringComparer.Ordinal);

    /// <summary>节流：语义 key → 上次播放的游戏时间。</summary>
    private static readonly Dictionary<string, float> LastPlayedAt = new(StringComparer.Ordinal);

    /// <summary>同类特效的最小间隔（秒）。0.05 = 几乎不过滤，但有爆炸保护。</summary>
    private const float ThrottleSeconds = 0.05f;

    /// <summary>位移偏置：让特效落在目标身体中段而不是脚底。</summary>
    private static readonly Vector2 AnchorOffset = new(0f, -60f);

    // ─────────────────────────────────────────────────────────────
    // 预加载
    // ─────────────────────────────────────────────────────────────

    /// <summary>把所有注册过的场景拉进 Godot 资源缓存，避免首次播放的加载卡顿。</summary>
    internal static void PreloadAll()
    {
        try
        {
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var spec in CardVfxRegistry.BySemantic.Values) paths.Add(spec.ScenePath);

            int ok = 0;
            foreach (var path in paths)
            {
                if (GetScene(path) != null) ok++;
            }

            // 居合斩用的翻转书纹理不在场景列表里，单独预加载
            Sequences.IaiSlashSequence.Preload();

            GD.Print($"[{Entry.ModId}] preloaded {ok}/{paths.Count} vfx scene(s) + slash flipbook");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] preload failed: {ex}");
        }
    }

    private static PackedScene? GetScene(string scenePath)
    {
        if (SceneCache.TryGetValue(scenePath, out var cached)) return cached;

        PackedScene? scene = null;
        try
        {
            string full = SceneHelper.GetScenePath(scenePath);
            if (ResourceLoader.Exists(full))
            {
                scene = GD.Load<PackedScene>(full);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] load scene '{scenePath}' failed: {ex.Message}");
        }

        SceneCache[scenePath] = scene;
        return scene;
    }

    // ─────────────────────────────────────────────────────────────
    // 播放
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 是否对**攻击类**卡牌启用居合斩演出（用户 2026-10-22：「先做一个攻击动画试试水」）。
    ///
    /// ⇒ 改成 false 就退回"普通 VFX"老行为，方便对比两种效果。
    /// ⇒ 只影响 <see cref="AttackSemantics"/> 里的三种攻击语义，其余卡牌不受影响。
    /// </summary>
    private static bool EnableIaiSlash => true;

    /// <summary>
    /// 是否对攻击类卡牌启用**跳跃下劈**演出。
    /// ⇒ 与 <see cref="EnableIaiSlash"/> 二选一（跳跃斩优先）。
    /// ⇒ 位移用游戏层节点坐标（<c>NCreature.Position</c> 补间），能精确对准任意敌人的头顶。
    /// </summary>
    private static bool EnableJumpSlash => true;

    /// <summary>被判定为"攻击牌"的语义（走居合斩演出）。</summary>
    private static readonly HashSet<string> AttackSemantics = new(StringComparer.Ordinal)
    {
        "slash", "slash_wide", "slash_multi",
    };

    /// <summary>
    /// 为一次卡牌出牌播放特效。
    /// <paramref name="semantic"/> 由调用方（补丁）根据卡牌推算给出，作为手工表未命中时的 fallback。
    /// </summary>
    /// <summary>
    /// 群体攻击（AOE）演出入口：挥斗篷消失 → 敌人附近闪亮登场 → 打响指 → 回原点。
    /// ★ 优先级最高（AOE > 多段 > 单段），由 MultiHitAnimPatch.TargetingPostfix 调用。
    /// </summary>
    internal static void PlayAoeVfx(CardModel card, System.Collections.Generic.IReadOnlyList<Creature> targets)
    {
        try
        {
            if (card == null) return;
            // 归属门禁（B1）：只服务码农
            if (!IsCodingFarmerOwner(card)) return;
            if (targets == null || targets.Count == 0) return;
            // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] AOE vfx: card={card.Id.Entry} targets={targets.Count}");
            Sequences.AoeSnapSequence.Play(card, targets);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] PlayAoeVfx failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 由 <c>AttackCommand.FromCard</c> 的 Postfix 调用（此时 hitCount 已就绪）。
    /// 按命中段数分流：hit==1 ⇒ 跳跃斩；hit>1 ⇒ 跪地插剑（StuckSword）。
    /// ★ 2026-10-23 审批 M1：攻击演出从这里触发，不再从 OnEnqueuePlayVfx。
    /// </summary>
    internal static void PlayAttackVfx(CardModel card, int hit, Creature? target)
    {
        try
        {
            if (card == null) return;
            // 归属门禁（B1）：只服务码农
            if (!IsCodingFarmerOwner(card)) return;
            // 只处理攻击语义
            string cardId = card.Id.Entry;
            if (!CardVfxRegistry.TryResolve(cardId, "slash", out _, out var sem)) return;
            if (!AttackSemantics.Contains(sem)) return;

            if (hit != 1)
            {
                // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] attack vfx (fromCard): card={cardId} hit={hit} -> StuckSword");
                Sequences.StuckSwordSequence.Play(card, target);
            }
            else if (EnableJumpSlash)
            {
                // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] attack vfx (fromCard): card={cardId} hit=1 -> JumpSlash");
                Sequences.JumpSlashSequence.Play(card, target);
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] PlayAttackVfx failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 判断这张牌的拥有者是不是「码农（CodingFarmer）」角色。
    /// 角色 Id 形如 <c>CODING_FARMER</c>（引擎日志里是 <c>CHARACTER.IRONCLAD</c> 这种格式）。
    /// 找不到角色信息时保守返回 false ⇒ 宁可不播，也不干扰别人。
    /// </summary>
    internal static bool IsCodingFarmerOwner(CardModel? card)
    {
        try
        {
            var character = card?.Owner?.Character;
            if (character == null) return false;
            string id = character.Id?.Entry ?? string.Empty;
            // 宽松匹配：包含 CODING 或 FARMER 都算（不同命名风格都能命中）
            return id.Contains("CODING", StringComparison.OrdinalIgnoreCase)
                || id.Contains("FARMER", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    internal static void Play(CardModel card, Creature? target, string? semantic)
    {
        try
        {
            string cardId = card.Id.Entry;

            // ★ 2026-10-23 Bug 修复：只对「码农（CODING_FARMER）」角色播放特效，
            //   避免干扰原版角色（铁甲战士/静默猎手等）的动作。
            if (!IsCodingFarmerOwner(card))
            {
                return;
            }

            if (!CardVfxRegistry.TryResolve(cardId, semantic, out var spec, out var resolvedSemantic))
            {
                return;   // 不认识的卡 ⇒ 静默跳过（这不是错误）
            }

            // 节流
            float now = Time.GetTicksMsec() / 1000f;
            if (LastPlayedAt.TryGetValue(resolvedSemantic, out var last)
                && now - last < ThrottleSeconds)
            {
                return;
            }
            LastPlayedAt[resolvedSemantic] = now;

            // ── 攻击牌演出：按命中段数分流 ──
            // ★ 2026-10-23 审批 M1：**不再在这里播攻击演出**。
            //   原因：OnEnqueuePlayVfx 早于 AttackCommand.FromCard，此刻 hitCount 还没记录，
            //   0.05s 后重查也一定读到旧值（飞牌动画 + 动作队列远大于 50ms）。
            //   攻击演出改由 MultiHitAnimPatch.FromCardPostfix 触发（见 PlayAttackVfx）。
            if (AttackSemantics.Contains(resolvedSemantic))
            {
                return;
            }

            if (false)   // 旧路径保留为死代码，便于对照/回退
            {
                // ★ hit != 1（多段攻击）：跪地插剑 + 裂隙 + 虚影冲锋
                //   姿势由我们自己的 attack_multi 骨骼动画承担（MultiHitAnimPatch 切换）
                //   本序列只叠加特效：裂隙 + 虚影 + 冲击
                int hit = HitCountReader.Get(card);
                if (hit != 1)
                {
                    Sequences.StuckSwordSequence.Play(card, target);
                    return;
                }

                // ⚠️ 时序修复：OnEnqueuePlayVfx 早于 AttackCommand.FromCard，
                //    那一刻 hitCount 还没被记录 ⇒ 直接判定会把多段牌误判成单段。
                //    ⇒ 延迟一帧（≈0.05s）后重查；仍读不到才走单段动画。
                if (TryDeferIfMultiHit(card, target)) return;

                if (EnableJumpSlash)
                {
                    Sequences.JumpSlashSequence.Play(card, target);
                    return;   // 跳跃斩自带特效，不再叠普通 VFX
                }

                if (EnableIaiSlash)
                {
                    Sequences.IaiSlashSequence.Play(card, target);
                    return;   // 居合斩自带特效，不再叠普通 VFX
                }
            }

            var room = NCombatRoom.Instance;
            var container = room?.CombatVfxContainer;
            if (room == null || container == null) return;

            // 定位：有目标就打目标身上，否则打玩家自己身上
            var owner = card.Owner;
            var anchorCreature = (target != null && !target.IsDead) ? target : owner?.Creature;
            if (anchorCreature == null) return;

            var anchorNode = room.GetCreatureNode(anchorCreature);
            if (anchorNode == null) return;

            var scene = GetScene(spec.ScenePath);
            if (scene == null) return;

            var vfx = scene.Instantiate<Node2D>();
            if (vfx == null) return;

            vfx.Position = anchorNode.Position + AnchorOffset;

            if (spec.HasTint && spec.Tint is { } tint)
            {
                ApplyTint(vfx, tint);
            }

            container.AddChildSafely(vfx);
        }
        catch (Exception ex)
        {
            // 表现失败绝不能影响玩法。
            GD.PushWarning($"[{Entry.ModId}] play failed for '{card.Id.Entry}': {ex.Message}");
        }
    }

    /// <summary>
    /// 递归把 modulate 设成 tint。原版 VFX 场景内部多为 Sprite2D / GPUParticles2D，
    /// 都属于 <see cref="CanvasItem"/>，所以统一走 modulate 是安全的。
    /// ⚠️ 已经是半透明/特殊混合的节点会被覆盖 —— 第一版可接受，后续按需排除。
    /// </summary>
    private static void ApplyTint(Node node, Color tint)
    {
        if (node is CanvasItem ci)
        {
            ci.Modulate = new Color(
                ci.Modulate.R * tint.R,
                ci.Modulate.G * tint.G,
                ci.Modulate.B * tint.B,
                ci.Modulate.A);
        }

        foreach (var child in node.GetChildren())
        {
            ApplyTint(child, tint);
        }
    }

    /// <summary>
    /// 时序修复：<c>OnEnqueuePlayVfx</c> 早于 <c>AttackCommand.FromCard</c>，
    /// 那一刻 hitCount 尚未被记录（<see cref="HitCountReader.Get"/> 会返回 1）
    /// ⇒ 直接判定会把多段攻击牌误判成单段。
    ///
    /// 做法：延迟 0.05s 重查一次 —— 此时 FromCard/WithHitCount 都已完成。
    ///   · hit != 1 ⇒ 播 <see cref="Sequences.StuckSwordSequence"/>
    ///   · hit == 1 ⇒ 播 <see cref="Sequences.JumpSlashSequence"/>
    ///   · 拿不到场景树 ⇒ 返回 false（调用方按旧逻辑继续）
    /// </summary>
    private static bool TryDeferIfMultiHit(CardModel card, Creature? target)
    {
        try
        {
            var room = NCombatRoom.Instance;
            var owner = card.Owner;
            var selfNode = owner == null ? null : room?.GetCreatureNode(owner.Creature);
            var tree = selfNode?.GetTree();
            if (tree == null) return false;

            tree.CreateTimer(0.05f).Timeout += () =>
            {
                try
                {
                    int late = HitCountReader.Get(card);
                    if (late != 1)
                    {
                        // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] deferred: card={card.Id.Entry} hit={late} -> StuckSword");
                        Sequences.StuckSwordSequence.Play(card, target);
                    }
                    else if (EnableJumpSlash)
                    {
                        Sequences.JumpSlashSequence.Play(card, target);
                    }
                }
                catch (Exception ex)
                {
                    GD.PushWarning($"[{Entry.ModId}] deferred vfx failed: {ex.Message}");
                }
            };
            return true;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] defer check failed: {ex.Message}");
            return false;
        }
    }

}

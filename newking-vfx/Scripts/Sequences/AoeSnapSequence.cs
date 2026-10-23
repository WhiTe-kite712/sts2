using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace NewKingVfx.Sequences;

/// <summary>
/// 群体攻击（AOE）演出 v2（用户 2026-10-23 修正版）。
///
/// 用户需求：
/// 「角色打一个响指然后立刻淡化，在每个敌人身后高处生成下劈角色模型，
///   结束后淡化，角色在原位重新淡入，这样可以避免斗篷和特效问题」
///
/// 时间轴（秒）：
///   0.00–0.25  打响指（Spine 播 Cast）
///   0.25–0.45  角色淡化
///   0.45–0.55  每个敌人身后高处生成下劈分身（SpineSprite 副本，播 attack）
///   0.55–1.10  分身下劈
///   1.10–1.40  分身淡化
///   1.40–1.70  角色在原位重新淡入
/// </summary>
internal static class AoeSnapSequence
{
    private const float SnapSeconds = 0.25f;
    private const float FadeOutSeconds = 0.20f;
    private const float GhostFadeOutSeconds = 0.30f;
    private const float FadeInSeconds = 0.30f;

    private const float GhostOffsetX = 0f;    // ★ 竖直向下（不横向偏移）
    private const float GhostOffsetY = -390f;   // ★ 高 50%（原 -260 的 150%）
    private const float GhostScale = 0.55f;   // ★ 适当缩小（掩饰单手细节）
    // ★ 竖直落到怪物节点同高度（略高于地面）
    private const float GhostLandOffsetY = -40f;
    // ★ 下落耗时 = 命中帧时刻（6 帧 / 54.5fps ≈ 0.11s）⇒ 命中瞬间正好落地
    private const float GhostFallSeconds = 0.11f;

    // ★ 用户 2026-10-23：骨架新建不方便 ⇒ 改用烘焙精灵图（attack 动画 12 帧）
    private const string AttackSheetPath = "res://NewKingVfx/vfx/attack_sheet.png";
    private const int AttackSheetFrames = 12;
    private const int AttackSheetFrameW = 1034;    // 12408 / 12
    private const int AttackSheetFrameH = 800;
    private const int AttackHitFrame = 6;          // 命中帧索引（动画帧 59）
    // 下劈开始于 0.26+0.12+0.10 = 0.48s，命中 0.59s ⇒ 6 帧 / 0.11s
    private const float SlashStartSeconds = 0.48f;
    private const float AttackHitSeconds = 0.11f;

    private static readonly Dictionary<ulong, Tween> ActiveTweens = new();

    private struct NodeSnapshot
    {
        public Vector2 Position;
        public Vector2 Scale;
        public float Rotation;
        public float ModulateA;
    }

    private static readonly Dictionary<ulong, NodeSnapshot> Snapshots = new();

    internal static void Play(CardModel card, IReadOnlyList<Creature> targets)
    {
        try
        {
            var room = NCombatRoom.Instance;
            if (room == null) return;
            var owner = card.Owner;
            if (owner == null) return;
            var selfNode = room.GetCreatureNode(owner.Creature);
            if (selfNode == null || !selfNode.IsInsideTree()) return;

            ulong nodeId = selfNode.GetInstanceId();
            if (ActiveTweens.TryGetValue(nodeId, out var prev) && prev != null && prev.IsValid())
            {
                GD.Print($"[{Entry.ModId}] AoeSnap: skip (previous still playing)");
                return;
            }

            var snap = new NodeSnapshot
            {
                Position = selfNode.Position,
                Scale = selfNode.Scale,
                Rotation = selfNode.Rotation,
                ModulateA = selfNode.Modulate.A,
            };
            Snapshots[nodeId] = snap;

            float t0 = Time.GetTicksMsec() / 1000f;
            // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] AoeSnap: BEGIN card={card.Id.Entry} targets={targets.Count}");

            var ghostNodes = new List<Node2D>();
            var tween = selfNode.CreateTween();
            tween.SetProcessMode(Tween.TweenProcessMode.Idle);
            ActiveTweens[nodeId] = tween;

            // ① 打响指
            tween.TweenCallback(Callable.From(() =>
            {
                // [perf-off 2026-10-23] Log("SNAP");
                try { MegaCrit.Sts2.Core.Commands.CreatureCmd.TriggerAnim(owner.Creature, "Cast", 0.01f); } catch { }
            }));
            tween.TweenInterval(SnapSeconds);

            // ② 角色淡化
            // [perf-off 2026-10-23] tween.TweenCallback(Callable.From(() => Log("FADE OUT")));
            tween.TweenProperty(selfNode, "modulate:a", 0f, FadeOutSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
            tween.TweenInterval(0.05f);

            // ③ 每个敌人身后高处生成下劈分身
            tween.TweenCallback(Callable.From(() =>
            {
                // [perf-off 2026-10-23] Log($"SPAWN GHOSTS x{targets.Count}");
                foreach (var t in targets)
                {
                    if (t == null || t.IsDead) continue;
                    var enemyNode = room.GetCreatureNode(t);
                    if (enemyNode == null) continue;
                    var ghost = SpawnGhostSlash(room, enemyNode.GlobalPosition + new Vector2(GhostOffsetX, GhostOffsetY));
                    if (ghost == null) continue;
                    ghostNodes.Add(ghost);
                    // ★ 用户 2026-10-23：分身从高处「快速竖直下落」到怪物节点同高度
                    //   下落耗时 = 命中帧时刻（6/54.5fps ≈ 0.11s）⇒ 命中瞬间正好落地
                    var landPos = enemyNode.GlobalPosition + new Vector2(0f, GhostLandOffsetY);
                    var fromY = ghost.GlobalPosition.Y;
                    var ft = ghost.CreateTween();
                    ft.TweenProperty(ghost, "position", landPos, GhostFallSeconds)
                      .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
                    // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] AoeSnap: ghost fall {fromY:0} -> {landPos.Y:0} in {GhostFallSeconds:0.00}s");
                }
            }));
            tween.TweenInterval(0.45f);   // 下落 0.11s + sheet 0.22s + 余量

            // ④ 分身淡化
            tween.TweenCallback(Callable.From(() =>
            {
                // [perf-off 2026-10-23] Log($"FADE GHOSTS x{ghostNodes.Count}");
                foreach (var g in ghostNodes)
                {
                    if (g == null || !GodotObject.IsInstanceValid(g)) continue;
                    var tw = g.CreateTween();
                    tw.TweenProperty(g, "modulate:a", 0f, GhostFadeOutSeconds)
                      .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.In);
                    var node = g;
                    tw.Finished += () => { try { if (GodotObject.IsInstanceValid(node)) node.QueueFree(); } catch { } };
                }
            }));
            tween.TweenInterval(GhostFadeOutSeconds);

            // ⑤ 角色在原位重新淡入
            tween.TweenCallback(Callable.From(() =>
            {
                // [perf-off 2026-10-23] Log("FADE IN (back to origin)");
                selfNode.Position = snap.Position;
                selfNode.Scale = snap.Scale;
                selfNode.Rotation = snap.Rotation;
            }));
            tween.TweenProperty(selfNode, "modulate:a", snap.ModulateA, FadeInSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

            tween.Finished += () =>
            {
                // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] AoeSnap: END t={Time.GetTicksMsec() / 1000f - t0:0.00}s");
                Restore(selfNode, snap);
                Snapshots.Remove(nodeId);
                if (ActiveTweens.TryGetValue(nodeId, out var cur) && cur == tween)
                    ActiveTweens.Remove(nodeId);
            };
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] AoeSnap failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 生成一个「下劈分身」：用烘焙好的 attack 精灵图序列（AnimatedSprite2D）。
    /// ★ 不新建 Spine 骨架（用户 2026-10-23 建议改用烘焙精灵图）。
    /// </summary>
    private static Node2D? SpawnGhostSlash(NCombatRoom room, Vector2 worldPos)
    {
        try
        {
            var container = room.CombatVfxContainer;
            if (container == null) return null;
            var sheet = GD.Load<Texture2D>(AttackSheetPath);
            if (sheet == null)
            {
                GD.PushWarning($"[{Entry.ModId}] ghost sheet missing: {AttackSheetPath}");
                return null;
            }

            // 从 sheet 切帧
            var frames = new SpriteFrames();
            frames.AddAnimation("slash");
            for (int i = 0; i < AttackSheetFrames; i++)
            {
                var at = new AtlasTexture
                {
                    Atlas = sheet,
                    Region = new Rect2(i * AttackSheetFrameW, 0, AttackSheetFrameW, AttackSheetFrameH),
                };
                frames.AddFrame("slash", at);
            }
            // 帧率：让命中帧正好落在 0.59s
            float fps = AttackHitFrame / AttackHitSeconds;   // 6 / 0.11 ≈ 54.5
            frames.SetAnimationSpeed("slash", fps);
            frames.SetAnimationLoop("slash", false);

            var spr = new AnimatedSprite2D
            {
                SpriteFrames = frames,
                Animation = "slash",
                GlobalPosition = worldPos,
                Scale = Vector2.One * GhostScale,
                FlipH = true,                                  // ★ 镜像（从背后/反向攻击）
                Modulate = new Color(0.75f, 0.85f, 1f, 0.95f),
                ZIndex = 9,
            };
            // 初速淡入，减少静态细节停留
            spr.Modulate = new Color(0.75f, 0.85f, 1f, 0f);
            container.AddChildSafely(spr);
            var tw = spr.CreateTween();
            tw.TweenProperty(spr, "modulate:a", 0.95f, 0.12f);
            // ★ 分身一出现就播（f48 举剑 → f59 命中），与下落同步
            spr.Play("slash");
            // [perf-off 2026-10-23] GD.Print($"[{Entry.ModId}] AoeSnap: ghost sheet at {worldPos} fps={fps:0.00} hitFrame={AttackHitFrame} delay={SlashStartSeconds:0.00}s");
            return spr;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] SpawnGhostSlash failed: {ex.Message}");
            return null;
        }
    }

    private static void Log(string msg)
        => GD.Print($"[{Entry.ModId}] AoeSnap: {msg}");

    private static void Restore(NCreature node, NodeSnapshot snap)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.Position = snap.Position;
            node.Scale = snap.Scale;
            node.Rotation = snap.Rotation;
            var m = node.Modulate;
            node.Modulate = new Color(m.R, m.G, m.B, snap.ModulateA);
        }
        catch { }
    }
}

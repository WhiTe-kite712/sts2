using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace NewKingVfx.Vfx;

/// <summary>
/// 虚影冲锋：在敌人左右两侧扇区随机生成幽灵虚影，朝敌人冲刺后消失。
///
/// ═══════════════════════════════════════════════════════════════════
/// 用户 2026-10-22 的设计（逐字）
/// ═══════════════════════════════════════════════════════════════════
/// 「虚影冲锋采用在敌人周围上半圆环随机生成，朝向敌人并冲刺后消失」
///   ↓ 修正为
/// 「3点钟方向±30deg，9点钟方向±30deg」
/// 「角色适当旋转，脚底在下指的是不要出现脚比头高的情况」
///
/// ⇒ 最终口径：
///   · 生成区：**3 点钟 ±30°**（右侧扇形：−30°~+30°）与 **9 点钟 ±30°**（左侧扇形：150°~210°）
///   · 虚影**不旋转**（脚底永远朝下，绝不出现脚比头高），只按左右做水平翻转
///   · 每个虚影冲刺 0.3s 后淡出消失
///
/// ═══════════════════════════════════════════════════════════════════
/// 为什么用烘焙精灵图而不是实时 Spine
/// ═══════════════════════════════════════════════════════════════════
/// 本仓 <c>DeathMarchCharge</c> 的实测结论（GhostChargeDirector.cs:33-35 原话）：
/// 「spinning up a skeleton per ghost cost ~100-160ms each,
///   while a texture plus a Sprite2D costs a few milliseconds」
/// ⇒ 虚影只用「挥剑冲锋」烘焙图的第 3 帧（剑水平前指），配一个 Sprite2D 即可。
/// </summary>
public partial class GhostRingCharge : Node2D
{
    /// <summary>冲锋精灵图（5 帧横向，每帧等宽）。</summary>
    public const string SheetPath = "res://NewKingVfx/vfx/charge_flipbook.png";

    /// <summary>取第几帧作为虚影造型（0-based，2 = 第 3 帧"冲锋·剑水平"）。</summary>
    private const int GhostFrameIndex = 2;

    /// <summary>图集总帧数。</summary>
    private const int SheetFrames = 5;

    /// <summary>单个虚影冲刺时长（秒）。用户要求 0.3s。</summary>
    public float ChargeSeconds { get; set; } = 0.30f;

    /// <summary>生成半径（相对敌人中心的像素）。</summary>
    public float SpawnRadius { get; set; } = 250f;

    /// <summary>虚影缩放。</summary>
    public float GhostScale { get; set; } = 0.55f;

    /// <summary>幽灵色调（冷蓝）。</summary>
    public Color GhostTint { get; set; } = new(0.47f, 0.78f, 1f, 0.78f);

    private Texture2D? _sheet;

    private sealed class Ghost
    {
        public Sprite2D Sprite = null!;
        public Vector2 From;
        public Vector2 To;
        public float Elapsed;
        public bool Done;
    }

    private readonly List<Ghost> _ghosts = new();

    public override void _Ready()
    {
        _sheet = GD.Load<Texture2D>(SheetPath);
        if (_sheet == null)
        {
            GD.PushWarning($"[{Entry.ModId}] ghost sheet missing: {SheetPath}");
            QueueFree();
        }
    }

    /// <summary>
    /// 生成一轮虚影（每 0.2s 一个，共 count 个）。
    /// <paramref name="enemyNode"/> 提供敌人位置；<paramref name="inRightPool"/> 决定左右扇形。
    /// </summary>
    public void SpawnWave(NCreature enemyNode, int count, RandomNumberGenerator rng)
    {
        if (_sheet == null || count <= 0) return;

        float frameW = _sheet.GetWidth() / (float)SheetFrames;
        float frameH = _sheet.GetHeight();
        var atlas = new AtlasTexture
        {
            Atlas = _sheet,
            Region = new Rect2(GhostFrameIndex * frameW, 0, frameW, frameH),
        };

        for (int i = 0; i < count; i++)
        {
            // 左右交替（第 0 个右、第 1 个左、第 2 个右…），让两侧都有人
            bool rightPool = (i % 2 == 0);
            // 角度：右侧 [−30°, +30°]，左侧 [150°, 210°]（屏幕坐标，0°=正右，Y 向下为正）
            float angleDeg = rightPool
                ? rng.RandfRange(-30f, 30f)
                : rng.RandfRange(150f, 210f);
            float rad = Mathf.DegToRad(angleDeg);

            // ★ 用 GlobalPosition（本节点挂在 CombatVfxContainer 下）
            var enemyPos = enemyNode.GlobalPosition;
            var from = enemyPos + new Vector2(
                SpawnRadius * Mathf.Cos(rad),
                SpawnRadius * Mathf.Sin(rad));
            var to = enemyPos;

            var sprite = new Sprite2D
            {
                Texture = atlas,
                Scale = new Vector2(GhostScale, GhostScale),
                // ★ 脚底始终朝下：不旋转，只按左右翻转
                FlipH = from.X > enemyPos.X,
                Modulate = GhostTint,
                ZIndex = 5,
            };
            AddChild(sprite);
            sprite.GlobalPosition = from;   // ★ 入树后再设
            _ghosts.Add(new Ghost { Sprite = sprite, From = from, To = to });
        }
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        for (int i = _ghosts.Count - 1; i >= 0; i--)
        {
            var g = _ghosts[i];
            if (g.Done)
            {
                _ghosts.RemoveAt(i);
                continue;
            }

            g.Elapsed += dt;
            float t = Mathf.Clamp(g.Elapsed / Mathf.Max(0.05f, ChargeSeconds), 0f, 1f);
            // 加速冲刺（EaseIn）
            float eased = t * t;

            g.Sprite.GlobalPosition = g.From.Lerp(g.To, eased);

            // 前半段保持亮度，最后 35% 淡出
            float alpha = t < 0.65f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.65f) / 0.35f);
            var c = GhostTint;
            g.Sprite.Modulate = new Color(c.R, c.G, c.B, c.A * alpha);

            if (t >= 1f)
            {
                g.Done = true;
                g.Sprite.QueueFree();
            }
        }

        if (_ghosts.Count == 0)
        {
            // 全部结束后自毁（不依赖外部持有引用）
            QueueFree();
        }
    }

    /// <summary>当前是否还有虚影在飞。</summary>
    public bool AnyAlive => _ghosts.Count > 0;
}

using System;
using Godot;

namespace NewKingVfx.Vfx;

/// <summary>
/// 裂隙生长节点：从起点持续向右"长"出一条蓝光裂纹。
///
/// ═══════════════════════════════════════════════════════════════════
/// 用户 2026-10-22 的设计（逐字）
/// ═══════════════════════════════════════════════════════════════════
/// 「裂隙闪蓝光，连续向右移动，在大剑保持插入时持续从起点蔓延，
///   拔起时起点处停止继续产生」
///
/// ═══════════════════════════════════════════════════════════════════
/// 实现方式：region_rect 裁剪（一张纹理做生长动画）
/// ═══════════════════════════════════════════════════════════════════
/// 纹理 <c>res://NewKingVfx/vfx/crack_blue.png</c> 是 1400×140 的横向长条。
/// 每帧把 <c>RegionRect.Size.X</c> 从 0 递增到纹理宽度 ⇒ 视觉上"从左向右长出来"。
///
/// ⇒ 优点：**零额外纹理**、无序列帧、GPU 只画一个 quad。
/// ⇒ 拔剑时调用 <see cref="StopGrowing"/>：冻结当前长度，不再变长。
/// </summary>
public partial class CrackGrowth : Node2D
{
    private Sprite2D? _sprite;
    private float _textureWidth = 1f;
    private float _elapsed;
    private bool _growing;
    private bool _fading;
    private float _fadeLeft;

    /// <summary>生长总时长（秒）。从 0 长到满长。</summary>
    public float GrowSeconds { get; set; } = 1.35f;

    /// <summary>起点到终点的世界距离（像素）。纹理会被横向缩放到这个长度。</summary>
    public float TargetLength { get; set; } = 600f;

    /// <summary>淡出时长（秒）。</summary>
    public float FadeSeconds { get; set; } = 0.28f;

    /// <summary>起始透明度。</summary>
    public float PeakAlpha { get; set; } = 0.95f;

    /// <summary>纹理路径。</summary>
    public const string TexturePath = "res://NewKingVfx/vfx/crack_blue.png";

    public override void _Ready()
    {
        var tex = GD.Load<Texture2D>(TexturePath);
        if (tex == null)
        {
            GD.PushWarning($"[{Entry.ModId}] crack texture missing: {TexturePath}");
            QueueFree();
            return;
        }

        _textureWidth = tex.GetWidth();
        _sprite = new Sprite2D
        {
            Texture = tex,
            Centered = false,
            RegionEnabled = true,
            RegionRect = new Rect2(0, 0, 0, tex.GetHeight()),
            Scale = new Vector2(TargetLength / _textureWidth, 1f),
            Modulate = new Color(0.75f, 0.92f, 1f, 0f),
            ZIndex = -1,
        };
        AddChild(_sprite);
        StartGrowing();
    }

    /// <summary>开始生长（alpha 拉起 + 长度递增）。</summary>
    public void StartGrowing()
    {
        _growing = true;
        _fading = false;
        _elapsed = 0f;
        if (_sprite != null)
        {
            _sprite.Modulate = new Color(_sprite.Modulate.R, _sprite.Modulate.G, _sprite.Modulate.B, PeakAlpha);
        }
    }

    /// <summary>拔剑时调用：停止变长（视觉冻结在当前长度）。</summary>
    public void StopGrowing()
    {
        _growing = false;
    }

    /// <summary>停止生长并淡出（拔剑后的清理）。</summary>
    public void BeginFadeOut()
    {
        _growing = false;
        _fading = true;
        _fadeLeft = FadeSeconds;
    }

    public override void _Process(double delta)
    {
        if (_sprite == null) return;

        float dt = (float)delta;

        if (_growing)
        {
            _elapsed += dt;
            float t = Mathf.Clamp(_elapsed / Mathf.Max(0.05f, GrowSeconds), 0f, 1f);
            // smoothstep：起步略慢、中段快、末尾收
            float eased = t * t * (3f - 2f * t);
            var rect = _sprite.RegionRect;
            rect.Size = new Vector2(_textureWidth * eased, rect.Size.Y);
            _sprite.RegionRect = rect;
        }

        if (_fading)
        {
            _fadeLeft -= dt;
            float a = Mathf.Clamp(_fadeLeft / Mathf.Max(0.05f, FadeSeconds), 0f, 1f);
            var c = _sprite.Modulate;
            _sprite.Modulate = new Color(c.R, c.G, c.B, PeakAlpha * a);
            if (_fadeLeft <= 0f) QueueFree();
        }
    }

    /// <summary>当前已生长比例（0~1），供外部判断"是否已经蔓延到敌人"。</summary>
    public float Progress
    {
        get
        {
            if (_sprite == null) return 0f;
            float full = _textureWidth;
            return full <= 0f ? 0f : Mathf.Clamp(_sprite.RegionRect.Size.X / full, 0f, 1f);
        }
    }
}

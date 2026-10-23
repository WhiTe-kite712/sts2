using System;
using Godot;

namespace NewKingVfx.Vfx;

/// <summary>
/// 敌人端冲击闪光：一个快速扩张并淡出的蓝色圆环。
/// 完全由代码绘制，不依赖任何原版场景/纹理。
/// </summary>
public partial class ImpactRing : Node2D
{
    /// <summary>总时长（秒）。</summary>
    public float LifeSeconds { get; set; } = 0.32f;

    /// <summary>起始半径 → 结束半径（像素）。</summary>
    public float StartRadius { get; set; } = 18f;

    /// <summary>结束半径。</summary>
    public float EndRadius { get; set; } = 120f;

    /// <summary>圆环颜色。</summary>
    public Color RingColor { get; set; } = new(0.62f, 0.88f, 1f, 1f);

    private float _elapsed;

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;
        float t = Mathf.Clamp(_elapsed / Mathf.Max(0.05f, LifeSeconds), 0f, 1f);
        QueueRedraw();
        if (t >= 1f) QueueFree();
    }

    public override void _Draw()
    {
        float t = Mathf.Clamp(_elapsed / Mathf.Max(0.05f, LifeSeconds), 0f, 1f);
        float radius = Mathf.Lerp(StartRadius, EndRadius, t * t);
        float alpha = (1f - t) * 0.85f;

        // 外圈（粗，半透明）
        var outer = new Color(RingColor.R, RingColor.G, RingColor.B, alpha * 0.45f);
        DrawArc(Vector2.Zero, radius, 0f, Mathf.Tau, 48, outer, 10f, true);

        // 内圈（细，更亮）
        var inner = new Color(1f, 1f, 1f, alpha);
        DrawArc(Vector2.Zero, radius * 0.82f, 0f, Mathf.Tau, 48, inner, 3f, true);
    }
}

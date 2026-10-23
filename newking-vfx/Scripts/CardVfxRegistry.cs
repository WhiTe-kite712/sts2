using Godot;

namespace NewKingVfx;

/// <summary>
/// 卡牌 ID / 语义 → VFX 场景 的映射表。<b>这是本 mod 唯一「认识」其他 mod 的地方</b>，
/// 而且只通过**字符串**认识 —— 没有任何类型引用。
///
/// ═══════════════════════════════════════════════════════════════════
/// ID 匹配规则（刻意做得宽容）
/// ═══════════════════════════════════════════════════════════════════
/// 上游与本地的 ID 前缀不同：
///   · 上游：<c>MYSTS2MOD-HAO_STRIKE</c>
///   · 本地：<c>NEWKING-HAO_STRIKE</c>
/// ⇒ 所以匹配时**剥掉前缀**，只比后面的键（<c>HAO_STRIKE</c>）。
///   这样同一个卡牌键在两套项目里都能命中，也符合用户"避免深度依赖"的要求。
///
/// ⚠️ 未知卡牌不会报错，只是不播特效（见 <see cref="TryResolve"/> 返回 false）。
/// </summary>
internal static class CardVfxRegistry
{
    /// <summary>一条特效规格。</summary>
    internal readonly record struct VfxSpec(string ScenePath, Color? Tint)
    {
        /// <summary>是否要染色（null = 用原色）。</summary>
        public bool HasTint => Tint.HasValue;
    }

    // ─────────────────────────────────────────────────────────────
    // 身份色（用户 2026-10-22 确认的方案）
    // ─────────────────────────────────────────────────────────────

    /// <summary>豪意：琥珀金 —— 本角色的身份色。</summary>
    internal static readonly Color Gold = new(1.00f, 0.82f, 0.30f);

    /// <summary>能量：冷蓝。</summary>
    internal static readonly Color EnergyBlue = new(0.55f, 0.75f, 1.00f);

    /// <summary>天命：紫蓝。</summary>
    internal static readonly Color DestinyViolet = new(0.72f, 0.62f, 1.00f);

    // ─────────────────────────────────────────────────────────────
    // 语义 → 规格（自动推断时用）
    // ─────────────────────────────────────────────────────────────

    internal static readonly Dictionary<string, VfxSpec> BySemantic = new(StringComparer.Ordinal)
    {
        ["slash"]       = new("vfx/vfx_attack_slash", null),
        ["slash_big"]   = new("vfx/vfx_big_slash", null),
        ["slash_multi"] = new("vfx/vfx_flying_slash", null),
        ["slash_wide"]  = new("vfx/vfx_giant_horizontal_slash", null),
        ["block"]       = new("vfx/block_spark_vfx", null),
        ["draw"]        = new("vfx/card_sparkles_vfx", null),
        ["energy"]      = new("vfx/power_applied_vfx", EnergyBlue),
        ["hao"]         = new("vfx/power_applied_vfx", Gold),
        ["destiny"]     = new("vfx/power_applied_vfx", DestinyViolet),
        ["debuff"]      = new("vfx/vfx_poison_impact", null),
        ["generate"]    = new("vfx/vfx_card_enchant", null),
        ["exhaust"]     = new("vfx/vfx_card_smith", null),
        ["power"]       = new("vfx/power_flash_vfx", null),
    };

    internal static int SemanticCount => BySemantic.Count;

    // ─────────────────────────────────────────────────────────────
    // 卡牌键 → 语义（可选的手工指定；没写就自动推断）
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 手工指定的「卡牌键 → 语义」。键是**剥掉前缀**后的卡牌 ID
    /// （<c>NEWKING-HAO_STRIKE</c> 与 <c>MYSTS2MOD-HAO_STRIKE</c> 都写成 <c>HAO_STRIKE</c>）。
    ///
    /// ⇒ 只在"自动推断不对"或"想要专属效果"时才需要往这里加。
    /// ⇒ 空白是允许的：全部走自动推断。
    /// </summary>
    internal static readonly Dictionary<string, string> ByCardKey = new(StringComparer.OrdinalIgnoreCase)
    {
        // 起始牌 / 身份牌：先给几个明确的，其余自动推断
        ["HAO_STRIKE"] = "slash",   // ★ 攻击牌：必须走 slash 语义才能进跳跃斩分支
        ["HAO_DEFEND"] = "hao",
        ["NARCISSISM"] = "hao",
        ["APPEAR"]     = "hao",
        ["FENCE_SITTER"] = "hao",
        ["EQUAL680"]   = "destiny",
        ["JIA_HAO_GOD"] = "destiny",

        // ★ 多段攻击牌（hit > 1）⇒ 走 attack_multi（跪地插剑 + 裂隙 + 虚影）
        //   上游 MySts2Mod 的 InfiniteHao / PlayingMyself 与部分原版多段牌
        ["INFINITE_HAO"]  = "slash_multi",
        ["PLAYING_MYSELF"] = "slash_multi",
        // 原版多段攻击（供对照测试）
        ["TWIN_STRIKE"]   = "slash_multi",
        ["MAUL"]          = "slash_multi",
        ["THRASH"]        = "slash_multi",
        ["UPROAR"]        = "slash_multi",
        ["RIP_AND_TEAR"]  = "slash_multi",
        ["DAGGER_SPRAY"]  = "slash_multi",
        ["REFRACT"]       = "slash_multi",
        ["FOOLED_YOU"]    = "slash_multi",
    };

    internal static int CardCount => ByCardKey.Count;

    // ─────────────────────────────────────────────────────────────
    // 解析
    // ─────────────────────────────────────────────────────────────

    /// <summary>把任意形式的卡牌 ID 归一成"卡牌键"（剥掉 <c>XXX-</c> 前缀，大写）。</summary>
    internal static string NormalizeKey(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return string.Empty;

        string id = cardId.Trim();
        int dash = id.IndexOf('-');
        if (dash >= 0 && dash + 1 < id.Length)
        {
            id = id[(dash + 1)..];
        }

        return id.ToUpperInvariant();
    }

    /// <summary>
    /// 尝试为这张卡解析出特效规格。
    ///
    /// 顺序：**手工指定 → 自动推断（由调用方提供 fallback 语义）**。
    /// ⇒ 返回 false 表示"这张卡不认识"，调用方应静默跳过（不是错误）。
    /// </summary>
    internal static bool TryResolve(string cardId, string? fallbackSemantic, out VfxSpec spec, out string semantic)
    {
        spec = default;
        semantic = string.Empty;

        string key = NormalizeKey(cardId);
        if (key.Length == 0) return false;

        // ① 手工指定
        if (ByCardKey.TryGetValue(key, out var mapped) && BySemantic.TryGetValue(mapped, out var direct))
        {
            spec = direct;
            semantic = mapped;
            return true;
        }

        // ② 自动推断给的语义
        if (fallbackSemantic != null && BySemantic.TryGetValue(fallbackSemantic, out var inferred))
        {
            spec = inferred;
            semantic = fallbackSemantic;
            return true;
        }

        return false;
    }
}

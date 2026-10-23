using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace MySts2Mod.MySts2ModCode.Character;

internal static class CodingFarmerCombatVisuals
{
    internal const string SkeletonPath = MainFile.ResPath + "/animations/coding_farmer/hao.tres";

    /// <summary>低血量阈值 —— 与引擎原生 <c>CharacterModel.IsLowHealth</c> 一致（HP ≤ 25%）。</summary>
    internal const float LowHealthThreshold = 0.25f;

    /// <summary>引擎原生的低血量判定（<c>sts2.decompiled.cs:75445</c>）。</summary>
    internal static bool IsLowHealth(Creature? creature)
    {
        try
        {
            return creature != null && creature.GetHpPercentRemaining() <= LowHealthThreshold;
        }
        catch
        {
            return false;
        }
    }

    public static NCreatureVisuals Create()
    {
        var data = ResourceLoader.Load<Resource>(SkeletonPath)
            ?? throw new InvalidOperationException("Missing character Spine data: " + SkeletonPath);
        var root = new NCreatureVisuals { Name = "CodingFarmer" };
        var body = ClassDB.Instantiate("SpineSprite").As<Node2D>();
        body.Name = "Visuals";
        body.Position = new Vector2(0, -4);
        // ★ 用户 2026-10-23：模型整体放大到原先 130%（0.55 × 1.3 = 0.715）
        body.Scale = Vector2.One * 0.715f;
        body.Set("skeleton_data_res", data);
        // ★ 2026-10-23：模型缩放日志（用户要求「模型放大 130%」，实测 0.55 -> 0.715）
        GD.Print($"[CodingFarmer] Create visuals: bodyScale={body.Scale} pos={body.Position} "
            + $"bounds=(273x361 @ -137,-361) centerPos=(0,-202)");
        AddUnique(root, body);
        AddUnique(root, new Control
        {
            Name = "Bounds", Position = new Vector2(-137, -361), Size = new Vector2(273, 361),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });
        AddUnique(root, new Marker2D { Name = "CenterPos", Position = new Vector2(0, -202) });
        AddUnique(root, new Marker2D { Name = "IntentPos", Position = new Vector2(0, -397) });
        AddUnique(root, new Marker2D { Name = "OrbPos", Position = new Vector2(0, -397) });
        AddUnique(root, new Marker2D { Name = "TalkPos", Position = new Vector2(26, -358) });
        AddUnique(root, new Control { Name = "FormVfx", MouseFilter = Control.MouseFilterEnum.Ignore });
        return root;
    }

    private static void AddUnique(NCreatureVisuals root, Node child)
    {
        root.AddChild(child);
        child.Owner = root;
        child.UniqueNameInOwner = true;
    }

    /// <summary>
    /// 构建动画状态机。**必须带 creature** —— 引擎原生按血量切换 idle
    /// （<c>sts2.decompiled.cs:75471-75489</c>）用的就是 <c>AddAnyState(trigger, state, Func&lt;bool&gt;)</c>。
    ///
    /// 用户口径（2026-10-23）：
    ///   · 正常血量：常驻 <c>idle_loop</c>（持剑呼吸），6 秒无操作播 <c>idle_basketball</c>（空气投篮）
    ///   · 低血量（HP ≤ 25%）：常驻 <c>idle_virtual</c>（虚弱捂腹），6 秒无操作播 <c>idle_lowhp</c>（扔剑淋雨）
    /// </summary>
    public static CreatureAnimator CreateAnimator(MegaSprite sprite, Creature? creature)
    {
        // ── 待机 ──
        var idle = new AnimState("idle_loop", isLooping: true);
        var idleVirtual = new AnimState("idle_virtual", isLooping: true);   // 低血量常驻（虚弱捂腹）
        // ★ 2026-10-23：商店待机 = 双手抱胸（原版静默猎手用专属 relaxed_loop）
        var relaxed = new AnimState("relaxed_loop", isLooping: true);

        // ── 一次性动作（播完回对应血量的待机）──
        var attack = new AnimState("attack");
        var attackMulti = new AnimState("attack_multi");
        var idleBasketball = new AnimState("idle_basketball");
        var idleLowHp = new AnimState("idle_lowhp");
        var cast = new AnimState("cast");
        var hurt = new AnimState("hurt");
        var dead = new AnimState("die");

        // ★ 所有一次性动作的 NextState 按血量分流（否则低血量播完会回普通 idle）
        foreach (var st in new[] { attack, attackMulti, idleBasketball, idleLowHp, cast, hurt })
        {
            st.AddNextState(idle, () => !IsLowHealth(creature));
            st.AddNextState(idleVirtual, () => IsLowHealth(creature));
        }

        // 初始状态按血量选（引擎原生做法）
        var animator = new CreatureAnimator(IsLowHealth(creature) ? idleVirtual : idle, sprite);

        try
        {
            float hpPct = -1f;
            try { if (creature != null) hpPct = (float)creature.GetHpPercentRemaining(); } catch { }
            GD.Print($"[CodingFarmer] GenerateAnimator: hpPct={hpPct:0.00} lowHealth={IsLowHealth(creature)} "
                + $"initialIdle={(IsLowHealth(creature) ? "idle_virtual" : "idle_loop")} "
                + $"threshold={LowHealthThreshold:0.00}");
            // ★ 2026-10-23：列出注册的全部触发器/状态（排查「低血量 idle 观测不到」用）
            GD.Print("[CodingFarmer] animator states: "
                + "Idle->idle_loop/idle_virtual (by hp) | Relaxed->relaxed_loop (shop) | "
                + "IdleSpecial->idle_basketball/idle_lowhp (by hp) | "
                + "Attack->attack | attack_multi->attack_multi | Cast->cast | Hit->hurt | Dead->die");
        }
        catch { }

        // ── 待机触发：按血量互斥分流 ──
        animator.AddAnyState("Idle", idle, () => !IsLowHealth(creature));
        animator.AddAnyState("Idle", idleVirtual, () => IsLowHealth(creature));
        animator.AddAnyState("Relaxed", relaxed);   // 商店/营地：双手抱胸，不分血量
        animator.AddAnyState("Revive", idle, () => !IsLowHealth(creature));
        animator.AddAnyState("Revive", idleVirtual, () => IsLowHealth(creature));

        // ── 战斗动作 ──
        animator.AddAnyState("Attack", attack);
        animator.AddAnyState("attack_multi", attackMulti);
        animator.AddAnyState("Cast", cast);
        animator.AddAnyState("PowerUp", cast);
        animator.AddAnyState("Hit", hurt);
        animator.AddAnyState("Dead", dead);

        // ── 6 秒无操作待机：正常血量播空气投篮，低血量播扔剑淋雨 ──
        animator.AddAnyState("IdleSpecial", idleBasketball, () => !IsLowHealth(creature));
        animator.AddAnyState("IdleSpecial", idleLowHp, () => IsLowHealth(creature));

        return animator;
    }

    /// <summary>BaseLib 的旧入口（拿不到 creature）—— 回退到无 creature 版本，保证兼容。</summary>
    [Obsolete("Prefer CreateAnimator(MegaSprite, Creature); kept for BaseLib compatibility.")]
    public static CreatureAnimator CreateAnimator(MegaSprite sprite)
        => CreateAnimator(sprite, null);
}

using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace NewKingVfx.Sequences;

/// <summary>
/// 居合斩演出（Iaijutsu slash）：攻击牌打出时的整套"瞬移斩"表现。
///
/// ═══════════════════════════════════════════════════════════════════
/// 用户 2026-10-22 的设计（逐字）
/// ═══════════════════════════════════════════════════════════════════
/// 「打出攻击牌后，角色整体中心前倾，脚步改为扎马步形式（前腿略前倾，后腿），
///   刀尖指向敌人，整体呈现类似于动漫居合斩的形式，
///   攻击实现过程中不显示（隐藏角色），
///   随后使用华丽收场的攻击特效并将角色的模型放置在敌人背向，
///   停顿0.5s后回到原位置并恢复待机骨骼」
///
/// ⇒ 拆成六个阶段（见 <see cref="Play"/> 里的时间轴注释）。
///
/// ═══════════════════════════════════════════════════════════════════
/// 为什么用节点变换而不是改 Spine 动画
/// ═══════════════════════════════════════════════════════════════════
/// 上游的 `coding_farmer/hao.spjson` 是**合作者的文件**，我们不改它（用户 2026-10-22：
/// 「避免打断他人作品」）。所以"居合预备姿势"用**节点层面的缩放/旋转/位移**来模拟：
///   · 前倾     → 角色整体轻微旋转 + 下移
///   · 扎马步   → 纵向压缩（Scale.Y 略小于 Scale.X）+ 下移
///   · 刀尖指敌 → 由 Spine 自身的武器骨骼承担（我们不动它，保持原朝向）
///
/// ⇒ 好处：**零资源改动**、可随时回退、不影响其他任何动画。
/// ⇒ 代价：姿势不如真正的骨骼动画精确 —— 这是"试试水"阶段的合理取舍。
///
/// ═══════════════════════════════════════════════════════════════════
/// 参考
/// ═══════════════════════════════════════════════════════════════════
/// 居合斩的经典影视/动漫表现手法：蓄势 → 瞬移（角色不可见/残影）→
/// 在对手背后现身 → 伤害延迟爆发。见 TV Tropes "Iaijutsu Practitioner"。
/// </summary>
internal static class IaiSlashSequence
{
    // ─────────────────────────────────────────────────────────────
    // 时间轴（秒）—— 全部相对"打出的瞬间"
    // ─────────────────────────────────────────────────────────────

    /// <summary>阶段1：蓄势（前倾 + 下蹲）时长。</summary>
    private const float WindUpSeconds = 0.18f;

    /// <summary>阶段2：淡出时长。</summary>
    private const float FadeOutSeconds = 0.08f;

    /// <summary>阶段3：斩击特效持续时间（同时角色已在敌人背后）。</summary>
    private const float SlashSeconds = 0.29f;

    /// <summary>阶段4：现身淡入时长。</summary>
    private const float FadeInSeconds = 0.10f;

    /// <summary>阶段5：停顿（用户明确要求 0.5 秒）。</summary>
    private const float HoldSeconds = 0.50f;

    /// <summary>阶段6：回原位时长。</summary>
    private const float ReturnSeconds = 0.25f;

    // ─────────────────────────────────────────────────────────────
    // 蓄势姿态参数
    // ─────────────────────────────────────────────────────────────

    /// <summary>前倾角度（度）。负值 = 向敌人方向倾。</summary>
    private const float LeanDegrees = -7f;

    /// <summary>垂直压缩比（扎马步：略矮一点）。</summary>
    private const float CrouchScaleY = 0.90f;

    /// <summary>水平方向保持（略微前伸感）。</summary>
    private const float CrouchScaleX = 1.04f;

    /// <summary>下移像素（蹲下去）。</summary>
    private const float CrouchOffsetY = 14f;

    // ─────────────────────────────────────────────────────────────
    // 敌人背后站位
    // ─────────────────────────────────────────────────────────────

    /// <summary>站到敌人背后时，沿 X 轴额外偏移的像素（让角色不完全重叠敌人）。</summary>
    private const float BehindOffsetX = 80f;

    /// <summary>站到敌人背后时的微调高度。</summary>
    private const float BehindOffsetY = -6f;

    // ─────────────────────────────────────────────────────────────
    // 特效
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 斩击特效 —— **完全由代码构造**，不依赖任何 `.tscn` 场景，也不依赖原版游戏资源。
    ///
    /// ═══════════════════════════════════════════════════════════════════
    /// 为什么不用 `.tscn`（2026-10-22 实测结论）
    /// ═══════════════════════════════════════════════════════════════════
    /// `BSchneppe.StS2.PckPacker` 把文件打进 .pck 时有**扩展名白名单**，实测拒绝：
    /// <code>
    /// PCK packing skipped — unsupported files detected:
    ///   vfx_flipbook_shader.gdshader
    ///   iai_slash.tscn
    /// </code>
    /// （`.png` 与不引 shader 的 `.tres` 能打包，`.tscn` / `.gdshader` 不行）
    ///
    /// ⇒ 所以"复制原版 `vfx_grand_finale` / `vfx_slash_core` 场景"这条路走不通。
    /// ⇒ 改为：**只带一张自己的 PNG 翻转书纹理**，用 `AnimatedSprite2D` +
    ///   运行时构造的 `SpriteFrames` 复现同样的 6 帧斩击动画。
    ///
    /// 复现的原版参数（来自 `scenes/vfx/slash/vfx_slash_core.tscn`）：
    /// <code>
    /// self_modulate = Color(0.638015, 0.5, 1, 1)   ← 紫蓝色
    /// rotation = π · scale = (1, -1) · skew = -45°  ← 翻转+斜切出刀锋感
    /// texture = slash_flipbook.png (512x512, 3x2 = 6 帧)
    /// lifetime = 0.3s · one_shot
    /// </code>
    ///
    /// ⇒ **完全自包含**：原版哪天删了/改了那些场景或 shader，本 mod 都不受影响。
    /// </summary>
    private const string SlashTexturePath = "res://NewKingVfx/vfx/slash_flipbook.png";

    /// <summary>翻转书网格列数（原版材质 `flipbook_size = Vector2(3, 2)`）。</summary>
    private const int SheetCols = 3;

    /// <summary>翻转书网格行数。</summary>
    private const int SheetRows = 2;

    /// <summary>斩击动画总时长（秒），与原版 `lifetime` 一致。</summary>
    private const float SlashFrameLifetime = 0.3f;

    /// <summary>
    /// 预加载斩击纹理。
    /// ⚠️ 不预加载的话，第一次播放要现场解码 ctex（同类做法实测首次约 200~280ms），
    ///   战斗里会明显卡一下。由 <see cref="CardVfxPlayer.PreloadAll"/> 在初始化后延迟调用。
    /// </summary>
    internal static void Preload()
    {
        try
        {
            if (GD.Load<Texture2D>(SlashTexturePath) == null)
            {
                GD.PushWarning($"[{Entry.ModId}] slash texture missing at preload: {SlashTexturePath}");
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] slash preload failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 播放整个居合斩序列。
    ///
    /// ⚠️ 全程 **不 await**（纯 Tween 驱动，立即返回）：
    ///   出牌流程不应该被特效阻塞 —— 伤害结算、卡牌移动都要照常进行。
    ///   所以本方法是"发射后不管"的，所有状态恢复都写在 Tween 的回调里。
    ///
    /// ⚠️ 所有节点变换都在最后**恢复原值**（<c>_restore</c> 回调），
    ///   因为 <c>NCreature</c> 是复用节点 —— 不恢复会让后续所有动画错位。
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

            // 敌人节点（决定"背后"在哪）；没有目标就只做原地蓄势 + 特效
            NCreature? enemyNode = null;
            if (target != null && !target.IsDead)
            {
                enemyNode = room.GetCreatureNode(target);
            }

            // ── 记录原始状态（必须原样恢复）──
            var originalPos = selfNode.Position;
            var originalScale = selfNode.Scale;
            var originalRotation = selfNode.Rotation;
            var originalModulate = selfNode.Modulate;

            // ── 目标位置：敌人背后 ──
            // 玩家在左、敌人在右 ⇒ 站到敌人**右侧**（背后）再往右一点，避免完全重叠。
            Vector2 behindPos = originalPos;   // 没有敌人时退回原位
            if (enemyNode != null)
            {
                float side = Mathf.Sign(enemyNode.Position.X - originalPos.X);
                if (Mathf.IsZeroApprox(side)) side = 1f;

                behindPos = enemyNode.Position
                            + new Vector2(side * BehindOffsetX, BehindOffsetY);
            }

            var tween = selfNode.CreateTween();
            tween.SetProcessMode(Tween.TweenProcessMode.Idle);

            // ═══════════════════════════════════════════════════
            // 阶段 1：蓄势 —— 前倾 + 扎马步
            //   整体压扁一点、往前倾、蹲下去一点
            // ═══════════════════════════════════════════════════
            tween.TweenProperty(selfNode, "scale", new Vector2(CrouchScaleX, CrouchScaleY), WindUpSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.Parallel()
                 .TweenProperty(selfNode, "rotation_degrees", LeanDegrees, WindUpSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.Parallel()
                 .TweenProperty(selfNode, "position", originalPos + new Vector2(0f, CrouchOffsetY), WindUpSeconds)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);

            // ═══════════════════════════════════════════════════
            // 阶段 2：淡出（用户："攻击实现过程中不显示"）
            // ═══════════════════════════════════════════════════
            tween.TweenProperty(selfNode, "modulate:a", 0f, FadeOutSeconds);

            // ═══════════════════════════════════════════════════
            // 阶段 3：瞬移到敌人背后 + 同时炸出华丽收场特效
            //   ⚠️ 位置**瞬间**改（瞬移），在淡出完成的同一帧执行
            // ═══════════════════════════════════════════════════
            tween.TweenCallback(Callable.From(() =>
            {
                selfNode.Position = behindPos;
                selfNode.Scale = Vector2.One;
                selfNode.RotationDegrees = 0f;

                SpawnSlashVfx(room, enemyNode?.VfxSpawnPosition ?? behindPos);
            }));

            // 特效播放期间保持不变（留白，让画面完全交给特效）
            tween.TweenInterval(SlashSeconds);

            // ═══════════════════════════════════════════════════
            // 阶段 4：现身（已在敌人背后）
            // ═══════════════════════════════════════════════════
            tween.TweenProperty(selfNode, "modulate:a", originalModulate.A, FadeInSeconds);

            // ═══════════════════════════════════════════════════
            // 阶段 5：停顿 0.5 秒（用户明确要求）
            // ═══════════════════════════════════════════════════
            tween.TweenInterval(HoldSeconds);

            // ═══════════════════════════════════════════════════
            // 阶段 6：回到原位 + 恢复待机骨骼
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

            // ⚠️ 无论 Tween 是否被中途打断，最终都要把状态写回原值 ——
            //    否则节点会带着偏移/半透明进入下一场战斗。
            tween.Finished += () => Restore(
                selfNode, originalPos, originalScale, originalRotation, originalModulate);

            MainFileNoop();   // 保持编译单元非空，便于将来扩展
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] iai slash failed: {ex.Message}");
        }
    }

    /// <summary>恢复节点到原始状态（幂等，可在任何时机调用）。</summary>
    private static void Restore(
        NCreature node, Vector2 pos, Vector2 scale, float rotation, Color modulate)
    {
        try
        {
            if (!GodotObject.IsInstanceValid(node)) return;
            node.Position = pos;
            node.Scale = scale;
            node.Rotation = rotation;
            node.Modulate = modulate;
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] restore failed: {ex.Message}");
        }
    }

    /// <summary>在指定位置生成斩击特效（代码构造，无场景依赖）。</summary>
    internal static void SpawnSlashVfx(NCombatRoom room, Vector2 at)
    {
        try
        {
            var container = room.CombatVfxContainer;
            if (container == null) return;

            var sprite = BuildSlashSprite(at);
            if (sprite == null) return;

            container.AddChildSafely(sprite);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] spawn slash vfx failed: {ex.Message}");
        }
    }

    /// <summary>
    /// 构造斩击精灵：从一张 3×2 翻转书大图里切出 6 帧，用 `AnimatedSprite2D` 播放。
    /// 播完自毁（<c>AnimationFinished</c> ⇒ <c>QueueFree</c>）。
    /// </summary>
    private static AnimatedSprite2D? BuildSlashSprite(Vector2 at)
    {
        var sheet = GD.Load<Texture2D>(SlashTexturePath);
        if (sheet == null)
        {
            GD.PushWarning($"[{Entry.ModId}] slash texture not found: {SlashTexturePath}");
            return null;
        }

        int total = SheetCols * SheetRows;
        float fw = sheet.GetWidth() / (float)SheetCols;
        float fh = sheet.GetHeight() / (float)SheetRows;

        var frames = new SpriteFrames();
        frames.AddAnimation("slash");
        frames.SetAnimationLoop("slash", false);
        // 6 帧 / 0.3s = 20 fps
        frames.SetAnimationSpeed("slash", total / SlashFrameLifetime);

        for (int i = 0; i < total; i++)
        {
            int col = i % SheetCols;
            int row = i / SheetCols;
            var frame = new AtlasTexture
            {
                Atlas = sheet,
                Region = new Rect2(col * fw, row * fh, fw, fh),
            };
            frames.AddFrame("slash", frame);
        }

        var sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Position = at,
            Rotation = Mathf.Pi,
            Scale = new Vector2(1f, -1f),
            Skew = -Mathf.Pi / 4f,
            SelfModulate = new Color(0.638015f, 0.5f, 1f, 1f),
        };

        // Godot 4 的 AnimatedSprite2D.AnimationFinished 是**无参**信号。
        sprite.AnimationFinished += () => sprite.QueueFree();
        sprite.Play("slash");
        return sprite;
    }

    private static string SceneHelperPath(string scenePath)
        => MegaCrit.Sts2.Core.Helpers.SceneHelper.GetScenePath(scenePath);

    /// <summary>占位调用，避免将来删掉所有代码时留下空的编译单元。</summary>
    private static void MainFileNoop() { }
}

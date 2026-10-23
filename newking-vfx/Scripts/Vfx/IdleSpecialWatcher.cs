using System;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace NewKingVfx.Vfx;

/// <summary>
/// 高空血待机 B（投篮失败喜剧）的触发器。
///
/// 用户口径（2026-10-22，逐字）：
///   「待机动画分为2个：平缓手持大剑，玩家回合内6s内无行动则开始播放这一套动画，
///     在玩家进行任意其他操作前不播放第二次」
///
/// 规则：
///   · 只在**玩家回合**计时
///   · 连续 **6 秒**没有任何操作 ⇒ 触发一次 <c>IdleSpecial</c>（idle_basketball，5.2s）
///   · 玩家做任何操作（点击/按键/出牌）⇒ 立刻回 <c>Idle</c> 并**本回合不再播**
///   · 回合结束 / 离开战斗 ⇒ 重置标记（下个玩家回合可再触发）
/// </summary>
internal partial class IdleSpecialWatcher : Node
{
    /// <summary>无操作多久后触发（秒）。</summary>
    private const double IdleThreshold = 6.0;

    /// <summary>正常血量 idle_basketball 动画时长（秒）。</summary>
    private const float AnimDurationNormal = 5.2f;

    /// <summary>低血量 idle_lowhp 动画时长（秒）—— 比 normal 短得多。</summary>
    private const float AnimDurationLow = 2.0f;

    /// <summary>低血量阈值（与引擎 CharacterModel.IsLowHealth 一致：HP ≤ 25%）。</summary>
    private const double LowHealthThreshold = 0.25;

    private double _idleSeconds;
    private bool _playedThisTurn;
    private bool _playing;
    private Creature? _playerCreature;

    public override void _Ready()
    {
        Name = "NewKingIdleSpecialWatcher";
        SetProcess(true);
        SetProcessInput(true);
    }

    // ── 输入：任何点击/按键都算"操作" ──
    public override void _Input(InputEvent @event)
    {
        try
        {
            bool acted = false;
            if (@event is InputEventMouseButton mb && mb.Pressed) acted = true;
            else if (@event is InputEventKey k && k.Pressed && !k.Echo) acted = true;

            if (!acted) return;

            _idleSeconds = 0;
            if (_playing) ReturnToIdle();
        }
        catch { /* 表现层：静默 */ }
    }

    /// <summary>低血量判定（与引擎原生 <c>CharacterModel.IsLowHealth</c> 一致：HP ≤ 25%）。</summary>
    private static bool IsLowHealth(Creature? creature)
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

    private void ReturnToIdle()
    {
        try
        {
            _playing = false;
            if (_playerCreature != null)
            {
                CreatureCmd.TriggerAnim(_playerCreature, "Idle", 0.15f);
                GD.Print($"[{Entry.ModId}] idle special -> back to Idle");
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] return idle failed: {ex.Message}");
        }
    }

    private static Creature? FindPlayerCreature(NCombatRoom room)
    {
        try
        {
            foreach (NCreature? node in room.CreatureNodes)
            {
                if (node == null || !GodotObject.IsInstanceValid(node)) continue;
                Creature? e = node.Entity;
                if (e != null && e.IsPlayer) return e;
            }
        }
        catch { /* ignore */ }
        return null;
    }

    public override void _Process(double delta)
    {
        try
        {
            NCombatRoom? room = NCombatRoom.Instance;
            if (room == null)
            {
                _idleSeconds = 0;
                _playedThisTurn = false;
                _playing = false;
                _playerCreature = null;
                return;
            }

            // ★ 2026-10-23 性能：原来每帧遍历 room.CreatureNodes 找玩家
            //   ⇒ 缓存 _playerCreature，只在为空/死亡时重新查找（战斗内玩家 creature 不会变）
            Creature? player = _playerCreature;
            if (player == null || player.IsDead)
            {
                player = FindPlayerCreature(room);
                _playerCreature = player;
            }
            if (player == null) return;

            // 非玩家回合 / 战斗未进行 ⇒ 重置（下个回合可再触发）
            CombatManager? cm = CombatManager.Instance;
            Player? playerModel = player.Player;
            if (cm == null || playerModel == null || !cm.IsInProgress || !cm.IsPartOfPlayerTurn(playerModel))
            {
                if (_playing) ReturnToIdle();
                _idleSeconds = 0;
                _playedThisTurn = false;
                return;
            }

            // 正在出牌/用药水 ⇒ 算操作
            if (cm.IsExecutingCardOrPotionEffect(playerModel))
            {
                _idleSeconds = 0;
                if (_playing) ReturnToIdle();
                return;
            }

            _idleSeconds += delta;

            // 低血量时 6 秒触发的动画是 idle_lowhp（2.0s），正常血量是 idle_basketball（5.2s）
            bool lowHealth = IsLowHealth(player);
            float animDuration = lowHealth ? AnimDurationLow : AnimDurationNormal;

            // 满足条件 ⇒ 播一次
            if (!_playing && !_playedThisTurn && _idleSeconds >= IdleThreshold)
            {
                _playedThisTurn = true;
                _playing = true;
                CreatureCmd.TriggerAnim(player, "IdleSpecial", animDuration);
                GD.Print($"[{Entry.ModId}] idle special START (idle {_idleSeconds:0.0}s >= {IdleThreshold}s, "
                    + $"lowHealth={lowHealth}, anim={animDuration:0.0}s)");
            }

            // 播放中超过动画时长 ⇒ 收尾（保险）
            if (_playing && _idleSeconds >= IdleThreshold + animDuration + 1.0)
            {
                ReturnToIdle();
            }
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] idle special watcher failed: {ex.Message}");
        }
    }

    /// <summary>由 Entry 调用：挂到 SceneTree 根上。</summary>
    internal static void Install(Node? sceneRoot)
    {
        try
        {
            if (sceneRoot == null)
            {
                GD.PushWarning($"[{Entry.ModId}] idle special watcher: no scene root");
                return;
            }
            if (sceneRoot.GetNodeOrNull<IdleSpecialWatcher>("NewKingIdleSpecialWatcher") != null) return;
            sceneRoot.AddChild(new IdleSpecialWatcher());
            GD.Print($"[{Entry.ModId}] idle special watcher installed (6s threshold)");
        }
        catch (Exception ex)
        {
            GD.PushWarning($"[{Entry.ModId}] idle special watcher install failed: {ex.Message}");
        }
    }
}

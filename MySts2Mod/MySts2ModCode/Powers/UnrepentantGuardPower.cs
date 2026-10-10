using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.ValueProps;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Extensions;

using MegaCrit.Sts2.Core.HoverTips;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 嘴硬（第一阶段）：格挡正常扣除，剩余伤害不扣生命而改为获得等量豪意值。
/// 覆盖本回合和敌方回合，直到拥有者的下个回合开始时切换为反噬。
/// </summary>
public class UnrepentantGuardPower : MySts2ModPower
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
        HoverTipFactory.FromPower<UnrepentantPainPower>(),
    ];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    // 该钩子运行时格挡已经扣除。先记录本应失去的生命值并归零伤害，
    // 再在实际伤害结算后使用 PlayerChoiceContext 兑换豪意。
    private int pendingHaoGain;

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // Unblockable marks direct HP loss, which must not be converted into Hao.
        if (target != Owner || amount <= 0 || props.HasFlag(ValueProp.Unblockable)) return amount;

        pendingHaoGain += (int)amount;
        return 0;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || pendingHaoGain <= 0 || props.HasFlag(ValueProp.Unblockable)) return;

        var gain = pendingHaoGain;
        pendingHaoGain = 0;
        await Owner.Player!.GainHao(choiceContext, gain, null);
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner)) return;

        await PowerCmd.Remove(this);
        await PowerCmd.Apply<UnrepentantPainPower>(choiceContext, Owner, 1, Owner, null);
    }
}

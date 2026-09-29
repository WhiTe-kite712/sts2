using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.ValueProps;using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 死不悔改（第一阶段）：本回合你受到的伤害改为增加等量豪意值。回合结束时移除并施加第二阶段。
/// </summary>
public class UnrepentantGuardPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    // ModifyHpLost 钩子没有 PlayerChoiceContext，先记录待转换数值，在 AfterDamageReceived 里结算
    private int pendingHaoGain;

    public override decimal ModifyHpLostBeforeOsty(Creature target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || amount <= 0) return amount;

        pendingHaoGain += (int)amount;
        return 0;
    }

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || pendingHaoGain <= 0) return;

        var gain = pendingHaoGain;
        pendingHaoGain = 0;
        await Owner.Player!.GainHao(choiceContext, gain, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side) return;

        await PowerCmd.Apply<UnrepentantPainPower>(choiceContext, Owner, 1, Owner, null);
        await PowerCmd.Remove(this);
    }
}

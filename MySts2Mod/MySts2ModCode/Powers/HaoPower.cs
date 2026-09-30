using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 豪意值：本模组的核心资源，以玩家身上的可叠加增益形式存在，层数即豪意值。
/// 图标自动加载 MySts2Mod/images/powers/hao_power.png（缺失时使用占位图）。
/// </summary>
public class HaoPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;


public override async Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target != Owner || Amount == 0 || result.UnblockedDamage <= 0)
        {
            return;
        }

        decimal lostAmount = Amount;

        Flash();
        await PowerCmd.ModifyAmount(choiceContext, this, -Amount, dealer, cardSource);
        await CreatureCmd.Damage(
            choiceContext,
            Owner,
            lostAmount,
            ValueProp.Unpowered,
            dealer ?? Owner);
    }

    public override decimal ModifyDamageAdditive(
        Creature? target,
        decimal amount,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource,
        CardPlay? cardPlay)
    {
        if (Owner != dealer || !props.IsPoweredAttack())
        {
            return 0m;
        }

        return 0m;
    }

    public override List<(string, string)>? Localization =>
    [
        ("title", "Hao Point"),
        ("description", "When you receive unblocked damage, lose all Hao Point and take damage equal to the amount lost.")
    ];
}
}
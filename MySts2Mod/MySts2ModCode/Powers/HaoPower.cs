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
    private sealed class Data
    {
        public bool ResolvingBacklash;
    }

    protected override object InitInternalData() => new Data();
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
        //The game marks HP loss (e.g. Offering and Poison) with Unblockable; it is not damage for Hao.
        if (target != Owner || Amount == 0 || result.UnblockedDamage <= 0 || props.HasFlag(ValueProp.Unblockable))
        {
            return;
        }

        var data = GetInternalData<Data>();
        if (data.ResolvingBacklash) return;

        var retained = Math.Max(0, Owner.GetPower<BondPower>()?.Amount ?? 0);
        var lostAmount = Math.Max(0, Amount - retained);
        if (lostAmount == 0) return;

        // Keep the retained Hao in place: losing then regaining it would trigger gain effects.
        // Backlash itself must not clear the retained Hao or recursively trigger more backlash.
        data.ResolvingBacklash = true;
        try
        {
            Flash();
            await PowerCmd.ModifyAmount(choiceContext, this, -lostAmount, dealer, cardSource);
            await CreatureCmd.Damage(
                choiceContext,
                Owner,
                lostAmount,
                ValueProp.Unpowered,
                dealer ?? Owner);
        }
        finally
        {
            data.ResolvingBacklash = false;
        }
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

}

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 清华之姿：当你失去豪意值时，获得等同于失去数量一半的豪意值与格挡（净效果约只失去一半）。
/// </summary>
public class TsinghuaFormPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power is not HaoPower || amount >= 0 || power.Owner != Owner) return;

        var half = (int)(-amount) / 2;
        if (half <= 0) return;

        await Owner.Player!.GainHao(choiceContext, half, null);
        await CreatureCmd.GainBlock(Owner, half, ValueProp.Move, null);
    }
}

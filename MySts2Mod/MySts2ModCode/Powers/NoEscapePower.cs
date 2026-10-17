using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace MySts2Mod.MySts2ModCode.Powers;

// Smart description only; source checks still require localized title and smartDescription.
#pragma warning disable STS001
public class NoEscapePower : MySts2ModPower
#pragma warning restore STS001
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override decimal ModifyHandDraw(Player player, decimal count) =>
        player == Owner.Player ? count + Amount : count;

    public override async Task AfterEnergyReset(Player player)
    {
        if (player == Owner.Player) await PlayerCmd.GainEnergy(Amount, player);
    }
}

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;

namespace MySts2Mod.MySts2ModCode.Powers;

// Smart description only; source checks still require localized title and smartDescription.
#pragma warning disable STS001
public class DualLifePower : MySts2ModPower
#pragma warning restore STS001
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public async Task OnHaoStateSwitched(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.PlayerCombatState;
        if (player.Creature != Owner || combatState == null) return;
        await CardPileCmd.Draw(choiceContext, Amount, player);
    }
}

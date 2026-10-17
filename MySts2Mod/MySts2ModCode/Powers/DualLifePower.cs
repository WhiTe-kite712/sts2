using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

// Smart description only; source checks still require localized title and smartDescription.
#pragma warning disable STS001
public class DualLifePower : MySts2ModPower
#pragma warning restore STS001
{
    private sealed class Data
    {
        public int LastTriggeredTurn = -1;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override object InitInternalData() => new Data();

    public async Task OnHaoStateSwitched(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = player.PlayerCombatState;
        if (player.Creature != Owner || combatState == null) return;
        var data = GetInternalData<Data>();
        if (!HaoStateExtensions.TryConsumeHaoStateSwitchReward(
            combatState.TurnNumber, ref data.LastTriggeredTurn)) return;
        await CardPileCmd.Draw(choiceContext, Amount, player);
    }
}

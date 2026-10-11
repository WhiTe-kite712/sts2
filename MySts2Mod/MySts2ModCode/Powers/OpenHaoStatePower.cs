using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MySts2Mod.MySts2ModCode.Powers;

public class OpenHaoStatePower : HaoStatStatePower
{
    public override int StrengthChange => 2;
    public override int DexterityChange => -1;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        base.ExtraHoverTips.Append(HoverTipFactory.FromPower<VulnerablePower>());

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        var combatState = Owner.CombatState;
        if (player.Creature != Owner || combatState is null) return;

        Flash();
        await PowerCmd.Apply<VulnerablePower>(choiceContext, combatState.HittableEnemies, 1, Owner, null);
    }
}

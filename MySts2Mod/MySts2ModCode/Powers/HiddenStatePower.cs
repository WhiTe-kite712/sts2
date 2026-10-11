using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MySts2Mod.MySts2ModCode.Powers;

public class HiddenStatePower : HaoStatStatePower
{
    public override int StrengthChange => -2;
    public override int DexterityChange => 3;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        base.ExtraHoverTips.Append(HoverTipFactory.FromPower<WeakPower>());

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || Owner.CombatState is null) return;

        Flash();
        await PowerCmd.Apply<WeakPower>(choiceContext, Owner, 1, Owner, null);
    }
}

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Cards;

namespace MySts2Mod.MySts2ModCode.Powers;

public class XiaoNextTurnPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Xiao>(),
    ];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || AmountOnTurnStart <= 0) return;

        //Consume only the requests present at turn start; new requests wait until the following turn.
        var count = AmountOnTurnStart;
        Flash();
        await PowerCmd.ModifyAmount(choiceContext, this, -count, Owner, null);
        for (var i = 0; i < count; i++)
        {
            var card = player.Creature.CombatState!.CreateCard<Xiao>(player);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player));
        }
    }
}

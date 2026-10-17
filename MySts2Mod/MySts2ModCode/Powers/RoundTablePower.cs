using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.HoverTips;

namespace MySts2Mod.MySts2ModCode.Powers;

// Smart description only; source checks still require localized title and smartDescription.
#pragma warning disable STS001
public class RoundTablePower : MySts2ModPower
#pragma warning restore STS001
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Retain)];

    public override async Task BeforeFlush(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner) return;

        if (Hook.ShouldFlush(CombatState!, player))
        {
            var selected = await CardSelectCmd.FromHand(choiceContext, player,
                new CardSelectorPrefs(SelectionScreenPrompt, 0, Amount),
                card => !card.ShouldRetainThisTurn, this);
            foreach (var card in selected)
                CardCmd.ApplySingleTurnRetain(card);
        }

        await PowerCmd.Remove(this);
    }
}

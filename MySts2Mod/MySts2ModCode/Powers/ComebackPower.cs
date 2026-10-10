using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

public class ComebackPower : MySts2ModPower
{
    private sealed class Data
    {
        public bool EnterOpenNextTurn;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override object InitInternalData() => new Data();
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HiddenStatePower>(),
        HoverTipFactory.FromPower<OpenHaoStatePower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
    ];

    public override async Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner) return;
        var data = GetInternalData<Data>();
        if (data.EnterOpenNextTurn) return;
        if (!player.IsHidden())
        {
            await PowerCmd.Remove(this);
            return;
        }

        foreach (var card in PileType.Hand.GetPile(player).Cards)
            if (card.Type == CardType.Attack)
                CardCmd.ApplySingleTurnRetain(card);

        data.EnterOpenNextTurn = true;
    }

    public override async Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (player.Creature != Owner) return;
        var data = GetInternalData<Data>();
        if (!data.EnterOpenNextTurn) return;

        data.EnterOpenNextTurn = false;
        await PowerCmd.Remove(this);
        await player.EnterHaoState(choiceContext, HaoState.OpenHao, null);
    }
}

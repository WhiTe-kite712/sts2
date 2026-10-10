using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

public class TimeWaitsForNoOnePower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HiddenStatePower>(),
        HoverTipFactory.FromPower<OpenHaoStatePower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
    ];

    public override Task BeforeFlushLate(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner) return Task.CompletedTask;

        CardType? retainedType = player.GetHaoState() switch
        {
            HaoState.Hidden => CardType.Attack,
            HaoState.OpenHao => CardType.Skill,
            _ => null,
        };
        if (retainedType.HasValue)
            foreach (var card in PileType.Hand.GetPile(player).Cards)
                if (card.Type == retainedType.Value)
                    CardCmd.ApplySingleTurnRetain(card);

        return Task.CompletedTask;
    }
}

using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class TimeWaitsForNoOne : MySts2ModCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<TimeWaitsForNoOnePower>(),
        HoverTipFactory.FromPower<HiddenStatePower>(),
        HoverTipFactory.FromPower<OpenHaoStatePower>(),
        HoverTipFactory.FromKeyword(CardKeyword.Retain),
    ];

    public TimeWaitsForNoOne() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = cardPlay.Player.Creature;
        await PowerCmd.Apply<TimeWaitsForNoOnePower>(choiceContext, creature, 1, creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

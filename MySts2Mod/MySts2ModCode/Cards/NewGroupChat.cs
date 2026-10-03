using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class NewGroupChat : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromCard<Xiao>(),
    ];

    public NewGroupChat() : base(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        var card = CombatState!.CreateCard<Xiao>(player);
        CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player));
    }
}

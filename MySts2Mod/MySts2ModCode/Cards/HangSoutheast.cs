using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class HangSoutheast : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

    public HangSoutheast() : base(2, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var powers = PileType.Hand.GetPile(Owner).Cards.Where(card => card.Type == CardType.Power).ToArray();
        foreach (var card in powers)
            if (card.Pile?.Type == PileType.Hand) await CardCmd.AutoPlay(choiceContext, card, null);

        foreach (var card in PileType.Hand.GetPile(Owner).Cards.ToArray())
            if (card.Pile?.Type == PileType.Hand) await CardCmd.Exhaust(choiceContext, card);

        await CreatureCmd.LoseBlock(choiceContext, Owner.Creature, Owner.Creature.Block, Owner.Creature);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Ethereal);
}

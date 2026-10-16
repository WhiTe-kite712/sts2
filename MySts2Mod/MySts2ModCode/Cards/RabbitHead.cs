using BaseLib.Utils;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class RabbitHead : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromKeyword(CardKeyword.Exhaust)];

    public RabbitHead() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hand = PileType.Hand.GetPile(Owner);
        var maxSelect = Math.Min(DynamicVars.Cards.IntValue, CardPile.MaxCardsInHand - hand.Cards.Count);
        if (maxSelect <= 0) return;

        var selected = (await CardSelectCmd.FromSimpleGrid(choiceContext,
            PileType.Discard.GetPile(Owner).Cards, Owner,
            new CardSelectorPrefs(SelectionScreenPrompt, 0, maxSelect))).ToArray();
        if (selected.Length == 0) return;

        var added = await CardPileCmd.Add(selected, PileType.Hand);
        var count = added.Count(result => result.success && result.cardAdded.Pile?.Type == PileType.Hand);
        if (count == 0) return;

        var toExhaust = (await CardSelectCmd.FromHand(choiceContext, Owner,
            new CardSelectorPrefs(CardSelectorPrefs.ExhaustSelectionPrompt, count), null, this)).ToArray();
        foreach (var card in toExhaust)
            if (card.Pile?.Type == PileType.Hand) await CardCmd.Exhaust(choiceContext, card);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

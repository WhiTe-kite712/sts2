using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class Enlightened : MySts2ModCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<FrailPower>(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<FrailPower>(), HoverTipFactory.FromKeyword(CardKeyword.Retain)];

    public Enlightened() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrailPower>(choiceContext, Owner.Creature,
            DynamicVars["FrailPower"].BaseValue, Owner.Creature, this);
        var selected = await CardSelectCmd.FromHandForUpgrade(choiceContext, Owner, this);
        if (selected == null) return;

        CardCmd.Upgrade(selected);
        if (IsUpgraded) selected.AddKeyword(CardKeyword.Retain);
    }
}

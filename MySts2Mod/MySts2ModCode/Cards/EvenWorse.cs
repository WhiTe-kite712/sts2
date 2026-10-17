using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class EvenWorse : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<FrailPower>(3), new PowerVar<WeakPower>(3), new PowerVar<VulnerablePower>(3),
         new CardsVar(4), new EnergyVar(3)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => RegularDebuffs.HoverTips;

    public EvenWorse() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<FrailPower>(choiceContext, Owner.Creature,
            DynamicVars["FrailPower"].BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<WeakPower>(choiceContext, Owner.Creature,
            DynamicVars.Weak.BaseValue, Owner.Creature, this);
        await PowerCmd.Apply<VulnerablePower>(choiceContext, Owner.Creature,
            DynamicVars.Vulnerable.BaseValue, Owner.Creature, this);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner);
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, Owner);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["FrailPower"].UpgradeValueBy(-1);
        DynamicVars.Weak.UpgradeValueBy(-1);
        DynamicVars.Vulnerable.UpgradeValueBy(-1);
    }
}

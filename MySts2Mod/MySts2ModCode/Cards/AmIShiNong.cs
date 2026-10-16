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
public class AmIShiNong : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<StrengthPower>(2), new PowerVar<DexterityPower>(2), new IntVar("StatCap", 10)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => RegularDebuffs.HoverTips.Concat(
        [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()]);

    public AmIShiNong() : base(1, CardType.Skill, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var layers = RegularDebuffs.CountLayers(Owner.Creature);
        if (layers == 0) return;

        var cap = DynamicVars["StatCap"].IntValue;
        var strength = Math.Min(cap, layers * DynamicVars.Strength.IntValue);
        var dexterity = Math.Min(cap, layers * DynamicVars.Dexterity.IntValue);
        // Native temporary powers apply the stats now and take back exactly their contribution at turn end.
        await PowerCmd.Apply<FlexPotionPower>(choiceContext, Owner.Creature, strength, Owner.Creature, this);
        await PowerCmd.Apply<AnticipatePower>(choiceContext, Owner.Creature, dexterity, Owner.Creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

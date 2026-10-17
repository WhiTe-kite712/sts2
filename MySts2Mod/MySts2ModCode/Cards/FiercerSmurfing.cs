using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class FiercerSmurfing : MySts2ModCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("HaoThreshold", 20)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<HaoPower>()];

    public FiercerSmurfing() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<FiercerSmurfingPower>(choiceContext, Owner.Creature,
            DynamicVars["HaoThreshold"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["HaoThreshold"].UpgradeValueBy(-5);
}

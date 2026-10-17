using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class JT83 : MySts2ModCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(11, ValueProp.Unpowered)];
    public JT83() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<JT83Power>(choiceContext, Owner.Creature,
            DynamicVars.Damage.BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class NothingToMention : MySts2ModCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("SelfDamage", 10), new DamageVar(15, ValueProp.Unpowered | ValueProp.Move)];

    public NothingToMention() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.AllEnemies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await CreatureCmd.Damage(choiceContext, Owner.Creature, DynamicVars["SelfDamage"].BaseValue,
            ValueProp.Unpowered | ValueProp.Move, this, cardPlay);
        await CreatureCmd.Damage(choiceContext, CombatState!.HittableEnemies, DynamicVars.Damage,
            Owner.Creature, this, cardPlay);
    }

    protected override void OnUpgrade() => DynamicVars["SelfDamage"].UpgradeValueBy(-3);
}

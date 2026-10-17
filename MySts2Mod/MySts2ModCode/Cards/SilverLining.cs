using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Extensions;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class SilverLining : MySts2ModCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new IntVar("TriggerLimit", 2), new EnergyVar(1), new CardsVar(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        RegularDebuffs.HoverTips;

    public SilverLining() : base(2, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await PowerCmd.Apply<SilverLiningPower>(choiceContext, Owner.Creature,
            DynamicVars["TriggerLimit"].IntValue, Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars["TriggerLimit"].UpgradeValueBy(1);
}

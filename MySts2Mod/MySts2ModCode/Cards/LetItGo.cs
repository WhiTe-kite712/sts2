using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class LetItGo : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new HealVar(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => RegularDebuffs.HoverTips;

    public LetItGo() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var debuffs = RegularDebuffs.On(Owner.Creature).ToList();
        var removedKinds = 0;
        foreach (var power in debuffs)
        {
            await PowerCmd.Remove(power);
            if (!Owner.Creature.Powers.Contains(power)) removedKinds++;
        }
        if (removedKinds > 0)
            await CreatureCmd.Heal(Owner.Creature, removedKinds * DynamicVars.Heal.BaseValue);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

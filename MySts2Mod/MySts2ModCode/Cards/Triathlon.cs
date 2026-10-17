using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class Triathlon : MySts2ModCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<StrengthPower>(2), new PowerVar<DexterityPower>(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()];

    public Triathlon() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var power = await PowerCmd.Apply<TriathlonPower>(choiceContext, Owner.Creature, 1, Owner.Creature, this);
        power?.StartAfter(cardPlay);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class AnswerQuestions : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("DamageThreshold", 30)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<HaoPower>(), HoverTipFactory.FromPower<AnswerQuestionsPower>(DynamicVars["DamageThreshold"].IntValue)];

    public AnswerQuestions() : base(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card,
        bool fromHandDraw)
    {
        if (card == this && Pile?.Type == PileType.Hand)
            await CardCmd.AutoPlay(choiceContext, this, null);
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay) =>
        await PowerCmd.Apply<AnswerQuestionsPower>(choiceContext, Owner.Creature,
            DynamicVars["DamageThreshold"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["DamageThreshold"].UpgradeValueBy(-10);
}

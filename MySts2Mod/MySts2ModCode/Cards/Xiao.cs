using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(ColorlessCardPool))]
public class Xiao : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override bool CanBeGeneratedInCombat => false;
    public override bool CanBeGeneratedByModifiers => false;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
        HoverTipFactory.FromCard<Chuan>(),
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Hao", 2m),
        new BlockVar(5, ValueProp.Move),
    ];

    public Xiao() : base(0, CardType.Skill, CardRarity.Token, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        await player.GainHao(choiceContext, DynamicVars["Hao"].IntValue, this);
        await CreatureCmd.GainBlock(player.Creature, DynamicVars.Block, cardPlay);
        await PowerCmd.Apply<ChuanNextTurnPower>(choiceContext, player.Creature, 1, player.Creature, this);
    }
}

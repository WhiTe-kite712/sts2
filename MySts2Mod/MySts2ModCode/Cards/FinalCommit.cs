using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MySts2Mod.MySts2ModCode.Extensions;
using MySts2Mod.MySts2ModCode.Powers;

using MegaCrit.Sts2.Core.HoverTips;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class FinalCommit : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<FinalCommitPower>(),
        HoverTipFactory.FromPower<HaoPower>(),
    ];

    private const int cost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType target = TargetType.Self;

    public FinalCommit() : base(cost, type, rarity, target)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        var creature = player.Creature;
        // Prevent block-break and Hao-change hooks from restoring Block.
        await PowerCmd.Apply<FinalCommitPower>(choiceContext, creature, 1, creature, this);
        await CreatureCmd.LoseBlock(choiceContext, creature, creature.Block, creature);
        var hao = player.GetHao();
        await player.GainHao(choiceContext, hao * 2, this);
    }

    protected override void OnUpgrade() => AddKeyword(CardKeyword.Retain);
}

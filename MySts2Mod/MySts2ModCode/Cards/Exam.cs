using BaseLib.Abstracts;
using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MySts2Mod.MySts2ModCode.Extensions;

using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class Exam : MySts2ModCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
    ];

    private const int cost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType target = TargetType.Self;

    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("HaoThreshold", 8), new HealVar(4)];

    public Exam() : base(cost, type, rarity, target)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        var hao = player.GetHao();
        if (hao <= DynamicVars["HaoThreshold"].IntValue)
        {
            await player.GainHao(choiceContext, hao, this);
        }

        else
        {
            await CreatureCmd.Heal(player.Creature, DynamicVars.Heal.BaseValue);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars["HaoThreshold"].UpgradeValueBy(2);
        DynamicVars.Heal.UpgradeValueBy(2);
    }

}

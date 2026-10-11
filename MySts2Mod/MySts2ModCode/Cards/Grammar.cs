using BaseLib.Abstracts;
using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Factories;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

/// <summary>
/// 语法课：随机生成一张已升级的技能牌加入手牌，本回合免费打出；豪意值不低于18（升级后12）时改为本场战斗免费；消耗。
/// v1 简化：从卡池随机生成一张（未做“3张选1”界面），后续迭代再补。
/// </summary>
[Pool(typeof(HaoCardPool))]
public class Grammar : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("HaoThreshold", 18)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
    ];

    private const int cost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType target = TargetType.Self;

    public Grammar() : base(cost, type, rarity, target)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        var options = Owner.Character.CardPool
            .GetUnlockedCards(Owner.UnlockState, Owner.RunState.CardMultiplayerConstraint)
            .Where(card => card.Type == CardType.Skill)
            .ToList();
        var generated = CardFactory.GetDistinctForCombat(
            Owner, options, 1, Owner.RunState.Rng.CombatCardGeneration).FirstOrDefault();
        if (generated == null) return;

        CardCmd.Upgrade(generated);
        await CardPileCmd.AddGeneratedCardToCombat(generated, PileType.Hand, Owner, CardPilePosition.Top);

        if (player.GetHao() >= DynamicVars["HaoThreshold"].IntValue)
        {
            generated.SetToFreeThisCombat();
        }
        else
        {
            generated.SetToFreeThisTurn();
        }
    }

    protected override void OnUpgrade() => DynamicVars["HaoThreshold"].UpgradeValueBy(-6);
}

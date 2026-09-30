using BaseLib.Abstracts;
using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Powers;
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
/// 语法课：随机生成一张已升级的技能牌加入手牌，本回合免费打出；豪意值不低于10时改为本局免费。
/// v1 简化：从卡池随机生成一张（未做“3张选1”界面），后续迭代再补。
/// </summary>
[Pool(typeof(HaoCardPool))]
public class Grammar : MySts2ModCard
{
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
        var generated = CommonActions.GenerateSingleCard(this, c => c.Type == CardType.Skill);
        if (generated == null) return;

        generated.UpgradeInternal();
        await CardPileCmd.Add(generated, PileType.Hand, CardPilePosition.Top, null);

        if (player.GetHao() >= 10)
        {
            generated.SetToFreeThisCombat();
        }
        else
        {
            generated.SetToFreeThisTurn();
        }
    }
}

using BaseLib.Abstracts;
using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models.CardPools;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Cards;

/// <summary>
/// 无限豪：X费。造成X点伤害X次，失去3点豪意值，再打出这张卡1次。
/// X费用官方机制声明（重写 HasEnergyCostX，与原版旋风斩等一致）；
/// “再打出”在效果里循环实现（每轮扣3豪意），链式打出持续到豪意不足3点。
/// </summary>
[Pool(typeof(HaoCardPool))]
public class InfiniteHao : MySts2ModCard
{
    private const int cost = 0;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Rare;
    private const TargetType target = TargetType.AnyEnemy;

    protected override bool HasEnergyCostX => true;

    public InfiniteHao() : base(cost, type, rarity, target)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var x = ResolveEnergyXValue();
        var player = cardPlay.Card.Owner;

        // 每轮伤害需先支付3点豪意；升级后首轮免费
        var firstRoundFree = IsUpgraded;
        while (firstRoundFree || player.GetHao() >= 3)
        {
            if (firstRoundFree)
            {
                firstRoundFree = false;
            }
            else
            {
                var snapshot = player.GetHao();
                await player.LoseHao(choiceContext, 3, this);
                if (player.GetHao() >= snapshot) return; // 豪意未实际减少，防无限循环
            }

            if (x > 0)
            {
                await DealDamage(choiceContext, cardPlay, x);
            }
        }
    }

    private async Task DealDamage(PlayerChoiceContext choiceContext, CardPlay cardPlay, int x)
    {
        await DamageCmd.Attack(x)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .WithHitCount(x)
            .Execute(choiceContext);
    }
}

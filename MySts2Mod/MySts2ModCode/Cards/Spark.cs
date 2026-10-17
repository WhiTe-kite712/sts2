using BaseLib.Abstracts;
using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
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

// 卡牌 ID 规则：命名空间第一段大写 + 类名大写蛇形，即 MYST2MOD-SPARK
[Pool(typeof(HaoCardPool))]
public class Spark : MySts2ModCard
{
    private const int cost = 1;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType target = TargetType.AnyEnemy;

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HiddenStatePower>(),
        HoverTipFactory.FromPower<OpenHaoStatePower>()
    ];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(7, ValueProp.Move), new CardsVar(1)];

    public Spark() : base(cost, type, rarity, target)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        var player = cardPlay.Player;
        if (player.GetHaoState() == HaoState.None)
            await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);
        else
            await player.FlipHaoState(choiceContext, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
    }
}

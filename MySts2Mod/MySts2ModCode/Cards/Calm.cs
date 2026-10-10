using BaseLib.Abstracts;
using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class Calm : MySts2ModCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
    ];

    private const int cost = 3;
    private const CardType type = CardType.Attack;
    private const CardRarity rarity = CardRarity.Uncommon;
    private const TargetType target = TargetType.AnyEnemy;

    public override bool GainsBlock => true;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new CalculationBaseVar(0),
        new ExtraDamageVar(4),
        new CalculationExtraVar(4),
        new CalculatedDamageVar(ValueProp.Move).WithMultiplier(static (card, _) => card.Owner.GetHao()),
        new CalculatedBlockVar(ValueProp.Move).WithMultiplier(static (card, _) => card.Owner.GetHao()),
    ];

    protected override void AddExtraArgsToDescription(LocString description)
    {
        base.AddExtraArgsToDescription(description);
        description.Add("HasHaoToLose", IsMutable && Owner != null && Owner.GetHao() > 0);
    }

    public Calm() : base(cost, type, rarity, target)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Card.Owner;
        var lost = await player.LoseHao(choiceContext, player.GetHao(), this);
        if (lost <= 0) return;
        

        // Keep this loss snapshot: Hao has already changed before either effect resolves.
        await DamageCmd.Attack(lost * 4)
            .FromCard(this, cardPlay)
            .Targeting(cardPlay.Target!)
            .Execute(choiceContext);

        await CommonActions.CardBlock(this, new BlockVar(lost * 4, ValueProp.Move), cardPlay);
    }
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
    

using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class UnspokenFury : MySts2ModCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(15, ValueProp.Move)];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<UnspokenFuryPower>(),
    ];

    public UnspokenFury() : base(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        await DamageCmd.Attack(DynamicVars.Damage.BaseValue)
            .FromCard(this, cardPlay)
            .TargetingAllOpponents(CombatState!)
            .Execute(choiceContext);

        var creature = cardPlay.Player.Creature;
        await PowerCmd.Apply<UnspokenFuryPower>(choiceContext, creature, 1, creature, this);
    }
}

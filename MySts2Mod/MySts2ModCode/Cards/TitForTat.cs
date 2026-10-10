using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class TitForTat : MySts2ModCard
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<VulnerablePower>("Vulnerable", 2), new PowerVar<WeakPower>("Weak", 1)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<VulnerablePower>()];

    public TitForTat() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var target = cardPlay.Target!;
        var creature = cardPlay.Player.Creature;
        var block = target.Block;
        if (block <= 0)
        {
            await PowerCmd.Apply<VulnerablePower>(choiceContext, target, DynamicVars["Vulnerable"].IntValue, creature, this);
            await PowerCmd.Apply<WeakPower>(choiceContext, target, DynamicVars["Weak"].IntValue, creature, this);
            return;
        }

        // Snapshot the removed block before break-block hooks can grant new block.
        await CreatureCmd.LoseBlock(choiceContext, target, block, creature);
        await CreatureCmd.Damage(choiceContext, target, block, ValueProp.Unpowered, creature, this, cardPlay);
    }

    protected override void OnUpgrade() => RemoveKeyword(CardKeyword.Exhaust);
}

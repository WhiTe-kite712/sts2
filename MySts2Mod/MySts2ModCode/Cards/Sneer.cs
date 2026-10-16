using BaseLib.Abstracts;
using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.Models.Powers;
using MySts2Mod.MySts2ModCode.Extensions;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Powers;

using MegaCrit.Sts2.Core.HoverTips;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class Sneer : MySts2ModCard
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            yield return HoverTipFactory.FromPower<VulnerablePower>();
            yield return HoverTipFactory.FromPower<SneerPower>();
            if (IsUpgraded) yield return HoverTipFactory.FromPower<BlockNextTurnPower>();
        }
    }

    private const int cost = 0;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Common;
    private const TargetType target = TargetType.Self;

    public override bool GainsBlock => IsUpgraded;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new PowerVar<VulnerablePower>("Vulnerable", 1), new BlockVar(0, ValueProp.Move)];

    public Sneer() : base(cost, type, rarity, target)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = cardPlay.Player.Creature;
        await PowerCmd.Apply<VulnerablePower>(choiceContext, creature, DynamicVars["Vulnerable"].IntValue, creature, this);
        await PowerCmd.Apply<SneerPower>(choiceContext, creature, 1, creature, this);
        if (IsUpgraded)
        {
            var block = DynamicVars.Block;
            var blockNextTurn = Hook.ModifyBlock(CombatState!, creature,
                block.BaseValue, block.Props, this, cardPlay, out _);
            await PowerCmd.Apply<BlockNextTurnPower>(choiceContext, creature, blockNextTurn, creature, this);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(6);
}

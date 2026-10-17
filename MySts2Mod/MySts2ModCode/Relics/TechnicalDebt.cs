using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Extensions;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Relics;

[Pool(typeof(HaoRelicPool))]
public class TechnicalDebt : MySts2ModRelic
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<WeakPower>(1),
        new IntVar("Hao", 4),
        new BlockVar(10, ValueProp.Move),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
        HoverTipFactory.FromPower<WeakPower>(),
    ];

    public override async Task BeforeCombatStart()
    {
        var choiceContext = new ThrowingPlayerChoiceContext();
        await PowerCmd.Apply<WeakPower>(choiceContext, Owner.Creature, DynamicVars.Weak.BaseValue, Owner.Creature, null);
        await Owner.GainHao(choiceContext, DynamicVars["Hao"].IntValue, null);
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, null);
    }
}

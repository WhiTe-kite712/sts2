using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Relics;

[Pool(typeof(HaoRelicPool))]
public class DefensiveProgramming : MySts2ModRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(1, ValueProp.Move)];

    public override decimal ModifyBlockAdditive(
        Creature? target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner.Creature || !Owner.IsHidden() || cardSource == null) return 0m;
        return DynamicVars.Block.IntValue;
    }
}

using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 等效680：当你的生命上限低于目标敌人时，伤害额外增加4点（升级后6点）。
/// </summary>
public class Equal680Power : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("ExtraDamage", 4)];

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (dealer != Owner || target == null || target.Side == Owner.Side) return 0m;
        if (Owner.MaxHp >= target.MaxHp) return 0m;

        return DynamicVars["ExtraDamage"].BaseValue;
    }
}

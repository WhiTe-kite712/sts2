using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace MySts2Mod.MySts2ModCode.Powers;

public class GodListPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("MaxHpCap", 8)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<HaoPower>()];

    public override bool TryModifyPowerAmountReceived(PowerModel canonicalPower, Creature target,
        decimal amount, Creature? applier, out decimal modifiedAmount)
    {
        modifiedAmount = amount;
        if (canonicalPower is not HaoPower || target != Owner || amount <= 0) return false;

        // Modify this gain once; a separate GainHao call would recursively trigger this effect.
        modifiedAmount = amount + Amount;
        return true;
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        // This hook runs before combat powers, including Hao, are cleared.
        var hao = Owner.GetPower<HaoPower>()?.Amount ?? 0;
        var reward = Math.Min(Math.Max(hao, 0) / 2, DynamicVars["MaxHpCap"].IntValue);
        if (reward <= 0) return;

        Flash();
        // The native command also heals the amount of Max HP actually gained.
        await CreatureCmd.GainMaxHp(Owner, reward);
    }
}

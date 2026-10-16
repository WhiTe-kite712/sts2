using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MySts2Mod.MySts2ModCode.Extensions;

/// <summary>The three regular debuffs: Frail, Weak and Vulnerable.</summary>
internal static class RegularDebuffs
{
    public static bool IsRegular(PowerModel power) =>
        power is FrailPower or WeakPower or VulnerablePower;

    public static IEnumerable<PowerModel> On(Creature creature) =>
        creature.Powers.Where(power => IsRegular(power) && power.Amount > 0);

    public static int CountKinds(Creature creature) => On(creature).Count();

    public static int CountLayers(Creature creature) => On(creature).Sum(power => power.Amount);

    public static IEnumerable<IHoverTip> HoverTips =>
    [
        HoverTipFactory.FromPower<FrailPower>(),
        HoverTipFactory.FromPower<WeakPower>(),
        HoverTipFactory.FromPower<VulnerablePower>(),
    ];
}

using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;

namespace MySts2Mod.MySts2ModCode.Powers;

// Smart description only; source checks still require localized title and smartDescription.
#pragma warning disable STS001
public class BondPower : MySts2ModPower
#pragma warning restore STS001
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<HaoPower>()];
}

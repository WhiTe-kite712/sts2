using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 豪意值：本模组的核心资源，以玩家身上的可叠加增益形式存在，层数即豪意值。
/// 图标自动加载 MySts2Mod/images/powers/hao_power.png（缺失时使用占位图）。
/// </summary>
public class HaoPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
}

using BaseLib.Abstracts;

namespace MySts2Mod.MySts2ModCode.Character;

/// <summary>
/// Coding Farmer 的专属遗物池。起始遗物「豪意」及其他角色遗物注册在此。
/// </summary>
public class HaoRelicPool : CustomRelicPoolModel
{
    public override bool IsShared => false;
    public override string? TextEnergyIconPath => CodingFarmerEnergy.SmallPath;
    public override string? BigEnergyIconPath => CodingFarmerEnergy.SmallPath;
}

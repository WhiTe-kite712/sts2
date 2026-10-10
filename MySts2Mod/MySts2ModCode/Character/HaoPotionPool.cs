using BaseLib.Abstracts;

namespace MySts2Mod.MySts2ModCode.Character;

/// <summary>
/// Coding Farmer 的专属药水池。暂空，后续角色药水注册在此。
/// </summary>
public class HaoPotionPool : CustomPotionPoolModel
{
    public override string? TextEnergyIconPath => CodingFarmerEnergy.SmallPath;
    public override string? BigEnergyIconPath => CodingFarmerEnergy.SmallPath;
}

using BaseLib.Abstracts;
using Godot;

namespace MySts2Mod.MySts2ModCode.Character;

/// <summary>
/// Coding Farmer 的专属卡池。豪意卡牌统一注册在此池。
/// </summary>
public class HaoCardPool : CustomCardPoolModel
{
    // 卡池 ID，需唯一
    public override string Title => "hao";

    // 卡池主题色（青蓝色系，对应“码农”身份）
    public override Color ShaderColor => new(0.3f, 0.6f, 1f);
    public override Color DeckEntryCardColor => new(0.3f, 0.6f, 1f);

    public override bool IsColorless => false;

    // BaseLib's BigEnergyIconPath is also used by card/hover icons, not the combat orb.
    public override string? TextEnergyIconPath => CodingFarmerEnergy.SmallPath;
    public override string? BigEnergyIconPath => CodingFarmerEnergy.SmallPath;
}

using BaseLib.Abstracts;
using Godot;

namespace MySts2Mod.MySts2ModCode.Character;

/// <summary>
/// Coding Farmer 的专属卡池。29 张豪意卡牌全部注册在此池。
/// </summary>
public class HaoCardPool : CustomCardPoolModel
{
    // 卡池 ID，需唯一
    public override string Title => "hao";

    // 卡池主题色（青蓝色系，对应“码农”身份）
    public override Color ShaderColor => new(0.3f, 0.6f, 1f);
    public override Color DeckEntryCardColor => new(0.3f, 0.6f, 1f);

    public override bool IsColorless => false;

    // 能量表盘：小图标随费用文字显示，大图标为能量球（由 tools/artgen 生成）
    public override string? TextEnergyIconPath => $"{MainFile.ResPath}/images/energy_hao.png";
    public override string? BigEnergyIconPath => $"{MainFile.ResPath}/images/energy_hao_big.png";
}

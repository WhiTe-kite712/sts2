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

    // TODO 正式版：能量图标 res://MySts2Mod/images/energy_hao.png (24x24) 与 energy_hao_big.png (74x74)
}

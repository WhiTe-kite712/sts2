using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace MySts2Mod.MySts2ModCode.Character;

/// <summary>
/// 角色：Coding Farmer（码农）。豪意机制卡组的使用者。
/// 资源均为占位：战斗模型/能量表盘/选择界面用的是模板占位图，
/// 商店/篝火/多人手势等未覆盖的部分回落到原版（铁甲战士）资源。
/// </summary>
public class CodingFarmer : PlaceholderCharacterModel
{
    // ---- 视觉（占位） ----
    // 注意：.tscn 场景必须用 Godot(MegaDot) 导出才能进 pck（PckPacker 不支持），
    // 所以战斗模型/能量表盘/选择背景暂用原版回落；场景文件保留在项目 _scenes_for_publish 目录。
    public override Color NameColor => new(0.3f, 0.6f, 1f);
    public override Color EnergyLabelOutlineColor => new(0.1f, 0.1f, 0.8f);
    public override Color MapDrawingColor => new(0.3f, 0.6f, 1f);

    public override CharacterGender Gender => CharacterGender.Masculine;

    public override int StartingHp => 75;
    public override int StartingGold => 99;
    public override int MaxEnergy => 3;

    // 人物头像（png/jpg 可被 PckPacker 打包；当前为用户提供的占位图）
    public override string CustomIconTexturePath => $"{MainFile.ResPath}/images/charui/coding_farmer_icon.jpg";
    public override string CustomCharacterSelectIconPath => $"{MainFile.ResPath}/images/charui/coding_farmer_icon.jpg";
    public override string CustomCharacterSelectLockedIconPath => $"{MainFile.ResPath}/images/charui/coding_farmer_icon.jpg";

    // 过渡音效不能删
    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

    // ---- 池子 ----
    public override CardPoolModel CardPool => ModelDb.CardPool<HaoCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<HaoRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<HaoPotionPool>();

    // ---- 初始卡组：4 打击 + 4 防御 + 1 自恋 ----
    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<Cards.HaoStrike>(),
        ModelDb.Card<Cards.HaoStrike>(),
        ModelDb.Card<Cards.HaoStrike>(),
        ModelDb.Card<Cards.HaoStrike>(),
        ModelDb.Card<Cards.HaoDefend>(),
        ModelDb.Card<Cards.HaoDefend>(),
        ModelDb.Card<Cards.HaoDefend>(),
        ModelDb.Card<Cards.HaoDefend>(),
        ModelDb.Card<Cards.Narcissism>(),
    ];

    // ---- 初始遗物：豪意 ----
    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<Relics.Hao>()];

    // 攻击建筑师的攻击特效
    public override List<string> GetArchitectAttackVfx() =>
    [
        "vfx/vfx_attack_blunt",
        "vfx/vfx_heavy_blunt",
        "vfx/vfx_attack_slash",
        "vfx/vfx_rock_shatter"
    ];
}

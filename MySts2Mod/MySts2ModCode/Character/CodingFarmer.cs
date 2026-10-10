using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;

namespace MySts2Mod.MySts2ModCode.Character;

/// <summary>
/// 角色：Coding Farmer（码农）。豪意机制卡组的使用者。
/// 选角和战斗模型使用自定义美术；能量表盘、
/// 商店/篝火/多人手势等未覆盖的部分回落到原版（铁甲战士）资源。
/// </summary>
public class CodingFarmer : PlaceholderCharacterModel
{
    // ---- 视觉 ----
    // 选角背景运行时创建PackedScene，资源包仍只需要PNG。
    // 战斗模型使用Spine；未覆盖的其他界面资源保留原版回落。
    public override Color NameColor => new(0.3f, 0.6f, 1f);
    public override Color EnergyLabelOutlineColor => new(0.1f, 0.1f, 0.8f);
    public override Color MapDrawingColor => new(0.3f, 0.6f, 1f);

    public override CharacterGender Gender => CharacterGender.Masculine;

    public override int StartingHp => 75;
    public override int StartingGold => 99;
    public override int MaxEnergy => 3;
    public override CustomEnergyCounter? CustomEnergyCounter => new CustomEnergyCounter(
        CodingFarmerEnergy.CounterLayerPath, EnergyLabelOutlineColor, new Color(0.3f, 0.6f, 1f));

    // 顶栏使用方形头像，选角使用独立竖向头像。
    public override string CustomIconTexturePath => CodingFarmerIcon.TexturePath;
    public override Control CustomIcon => CodingFarmerIcon.Create();
    public override string CustomIconOutlineTexturePath => CodingFarmerIcon.GetOutlinePath();
    public override string CustomMapMarkerPath => CodingFarmerIcon.TexturePath;
    public override string CustomCharacterSelectIconPath => $"{MainFile.ResPath}/images/charui/coding_farmer_select_icon.png";
    public override string CustomCharacterSelectLockedIconPath => CustomCharacterSelectIconPath;
    public override string CustomCharacterSelectBg => CodingFarmerSelectScene.GetScenePath();

    public override NCreatureVisuals CreateCustomVisuals() => CodingFarmerCombatVisuals.Create();
    public override CreatureAnimator SetupCustomAnimationStates(MegaSprite controller)
        => CodingFarmerCombatVisuals.CreateAnimator(controller);
    public override float AttackAnimDelay => 0.34f;
    public override float CastAnimDelay => 0.48f;

    // 过渡音效不能删
    public override string CharacterTransitionSfx => "event:/sfx/ui/wipe_ironclad";

    // ---- 池子 ----
    public override CardPoolModel CardPool => ModelDb.CardPool<HaoCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<HaoRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<HaoPotionPool>();

    // ---- 初始卡组：4 打击 + 4 防御 + 1 豪意打击 + 1 豪意防御 + 1 自恋（11张） ----
    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<Cards.Strike>(),
        ModelDb.Card<Cards.Strike>(),
        ModelDb.Card<Cards.Strike>(),
        ModelDb.Card<Cards.Strike>(),
        ModelDb.Card<Cards.Defend>(),
        ModelDb.Card<Cards.Defend>(),
        ModelDb.Card<Cards.Defend>(),
        ModelDb.Card<Cards.Defend>(),
        ModelDb.Card<Cards.HaoStrike>(),
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

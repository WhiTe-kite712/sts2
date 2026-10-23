using BaseLib.Abstracts;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
    // ★ 2026-10-23：火堆 / 商店也用码农模型（原先回落到铁甲战士 ironclad_rest_site / ironclad_merchant）
    public override string CustomRestSiteAnimPath => CodingFarmerRoomScenes.GetRestSitePath();
    public override string CustomMerchantAnimPath => CodingFarmerRoomScenes.GetMerchantPath();

    public override NCreatureVisuals CreateCustomVisuals() => CodingFarmerCombatVisuals.Create();

    /// <summary>
    /// 覆写引擎原生的动画器构建（<c>sts2.decompiled.cs:75471</c>）。
    /// **这是唯一能拿到 Creature 的入口** —— 按血量切换 idle 必须用它
    /// （BaseLib 的 <c>SetupCustomAnimationStates(MegaSprite)</c> 拿不到 creature）。
    /// </summary>
    public override CreatureAnimator GenerateAnimator(MegaSprite controller, Creature creature)
        => CodingFarmerCombatVisuals.CreateAnimator(controller, creature);
    // 出伤时机 = 动画播到「下劈命中」的那一刻（引擎在 AttackCommand 里按此延迟结算伤害）。
    // ★ 与 JumpSlashSequence 的命中时刻一致：
    //   WindUp 0.26 + JumpUp 0.12 + ApexHold 0.10 + Fall 0.11 = 0.59s
    // ⚠️ 注意：Spine 骨架动画 attack 的 eventFrames = 84（0.84s），比 VFX/伤害晚 0.25s
    //   ⇒ 若要三套对齐，应改 Spine 动画的 hit 关键帧时刻（不是改这里）。
    public override float AttackAnimDelay => 0.59f;
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

using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MySts2Mod.MySts2ModCode.Cards;

using MegaCrit.Sts2.Core.HoverTips;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 数学王子给予的临时力量：本回合内有效，回合结束时自动收回。
/// 临时力量不能直接 PowerCmd.Apply 游戏的抽象类 TemporaryStrengthPower（ModelDb 无注册条目，
/// 会抛 KeyNotFoundException 并杀死回合循环）；官方模式是每个来源建一个子类（如 FlexPotionPower），
/// 这里用 BaseLib 的 CustomTemporaryPowerModel 封装，内部联动原版力量（StrengthPower）。
/// </summary>
public class MathPrinceTempStrengthPower : CustomTemporaryPowerModel
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<StrengthPower>(),
    ];

    public override PowerModel InternallyAppliedPower => ModelDb.Power<StrengthPower>();

    public override AbstractModel OriginModel => ModelDb.Card<MathPrince>();

    protected override Func<PlayerChoiceContext, Creature, decimal, Creature?, CardModel?, bool, Task> ApplyPowerFunc =>
        (choiceContext, target, amount, applier, cardSource, silent) =>
            PowerCmd.Apply<StrengthPower>(choiceContext, target, amount, applier, cardSource, silent);
}

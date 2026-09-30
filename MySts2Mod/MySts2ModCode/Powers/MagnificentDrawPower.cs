using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.ValueProps;using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Extensions;

using MegaCrit.Sts2.Core.HoverTips;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 豪华的豪（第一阶段）：本回合你每抽2张牌，获得1点豪意值。回合结束时移除并施加第二阶段。
/// Amount 用作计数器（0..1）。
/// </summary>
public class MagnificentDrawPower : MySts2ModPower
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
        HoverTipFactory.FromPower<MagnificentHaoPower>(),
    ];

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override async Task AfterCardDrawn(PlayerChoiceContext choiceContext, CardModel card, bool fromHandDraw)
    {
        if (card.Owner.Creature != Owner) return;

        SetAmount(Amount + 1);
        if (Amount >= 2)
        {
            SetAmount(Amount - 2);
            await Owner.Player!.GainHao(choiceContext, 1, null);
        }
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side) return;

        await PowerCmd.Apply<MagnificentHaoPower>(choiceContext, Owner, 1, Owner, null);
        await PowerCmd.Remove(this);
    }
}

using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 天才：回合开始时获得1点能量并抽{Amount}张牌；每次失去豪意值后，你受到的下一次伤害翻倍。
/// HaoLostFlag 为非序列化字段，战斗中途存档读档后该标记会丢失（仅影响一次伤害翻倍判定）。
/// </summary>
public class GeniusPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public bool HaoLostFlag { get; set; }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner) return;

        await PlayerCmd.GainEnergy(1, player);
        await CardPileCmd.Draw(choiceContext, Amount, player);
    }

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power, decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power is HaoPower && amount < 0)
        {
            HaoLostFlag = true;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        if (target != Owner || dealer == null || dealer.Side == Owner.Side || !HaoLostFlag) return amount;

        HaoLostFlag = false;
        return amount * 2;
    }
}

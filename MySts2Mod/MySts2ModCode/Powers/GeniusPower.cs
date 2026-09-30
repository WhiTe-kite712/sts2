using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 天才：回合开始时获得1点能量并抽{Amount}张牌；每次失去豪意值后，你受到的下一次伤害翻倍。
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
        if (power is HaoPower && power.Owner == Owner && amount < 0)
        {
            HaoLostFlag = true;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? cardPlay)
    {
        // This hook also runs for damage previews and returns a multiplier, not damage.
        return HaoLostFlag && IsEligibleDamage(target, dealer) ? 2m : 1m;
    }

    public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        // A positive hit consumes the effect even when fully blocked; zero damage does not.
        if (IsEligibleDamage(target, dealer) && result.TotalDamage > 0)
        {
            HaoLostFlag = false;
        }

        return Task.CompletedTask;
    }

    private bool IsEligibleDamage(Creature? target, Creature? dealer) =>
        target == Owner && dealer != null && dealer.Side != Owner.Side;
}

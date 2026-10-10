using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Cards;

namespace MySts2Mod.MySts2ModCode.Powers;

public class ChuanNextTurnPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override object InitInternalData() => new List<bool>();

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power == this && amount > 0)
        {
            var upgrades = GetInternalData<List<bool>>();
            for (var i = 0; i < (int)amount; i++) upgrades.Add(cardSource?.IsUpgraded == true);
        }
        return Task.CompletedTask;
    }

    protected override IEnumerable<IHoverTip> ExtraHoverTips
    {
        get
        {
            var upgrades = IsMutable ? GetInternalData<List<bool>>() : null;
            if (upgrades == null || upgrades.Count == 0 || upgrades.Contains(false))
                yield return HoverTipFactory.FromCard<Chuan>();
            if (upgrades?.Contains(true) == true)
                yield return HoverTipFactory.FromCard<Chuan>(true);
        }
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || AmountOnTurnStart <= 0) return;

        //Consume only the requests present at turn start; new requests wait until the following turn.
        var count = AmountOnTurnStart;
        var upgrades = GetInternalData<List<bool>>();
        var queuedUpgrades = upgrades.Take(count).ToArray();
        upgrades.RemoveRange(0, Math.Min(count, upgrades.Count));
        Flash();
        await PowerCmd.ModifyAmount(choiceContext, this, -count, Owner, null);
        for (var i = 0; i < count; i++)
        {
            var card = player.Creature.CombatState!.CreateCard<Chuan>(player);
            if (i < queuedUpgrades.Length && queuedUpgrades[i]) CardCmd.Upgrade(card);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player));
        }
    }
}

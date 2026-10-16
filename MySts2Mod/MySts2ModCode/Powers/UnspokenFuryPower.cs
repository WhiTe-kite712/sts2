using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Cards;

namespace MySts2Mod.MySts2ModCode.Powers;

public class UnspokenFuryPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [new DamageVar(15, ValueProp.Move), new StringVar("QueuedDamage", "15")];
    protected override object InitInternalData() => new List<decimal>();

    public override Task AfterPowerAmountChanged(PlayerChoiceContext choiceContext, PowerModel power,
        decimal amount, Creature? applier, CardModel? cardSource)
    {
        if (power != this) return Task.CompletedTask;
        var damages = GetInternalData<List<decimal>>();
        if (amount > 0)
        {
            var damage = cardSource is UnspokenFury fury
                ? fury.DynamicVars.Damage.BaseValue
                : DynamicVars.Damage.BaseValue;
            for (var i = 0; i < (int)amount; i++) damages.Add(damage);
        }
        ((StringVar)DynamicVars["QueuedDamage"]).StringValue =
            damages.Count > 0 ? string.Join(", ", damages) : "0";
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || AmountOnTurnStart <= 0) return;

        // Player turn numbers include the following enemy turn and distinguish extra turns.
        var wasDamaged = CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>().Any(entry =>
            entry.Receiver == player.Creature && entry.HappenedLastPlayerTurn(player) &&
            entry.Result.UnblockedDamage > 0 && !entry.Result.Props.HasFlag(ValueProp.Unblockable));

        // Keep each card's damage and consume the old requests before awaited hooks can add new ones.
        var count = AmountOnTurnStart;
        var damages = GetInternalData<List<decimal>>();
        var queuedDamages = damages.Take(count).ToArray();
        damages.RemoveRange(0, Math.Min(count, damages.Count));
        if (wasDamaged) Flash();
        await PowerCmd.ModifyAmount(choiceContext, this, -count, player.Creature, null);
        if (!wasDamaged) return;

        var combatState = player.Creature.CombatState!;
        for (var i = 0; i < count; i++)
        {
            var damage = i < queuedDamages.Length ? queuedDamages[i] : DynamicVars.Damage.BaseValue;
            await CreatureCmd.Damage(choiceContext, combatState.HittableEnemies,
                damage, DynamicVars.Damage.Props, player.Creature);
        }
    }
}

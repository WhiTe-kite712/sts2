using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace MySts2Mod.MySts2ModCode.Powers;

public class UnspokenFuryPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(15, ValueProp.Move)];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner || AmountOnTurnStart <= 0) return;

        //Player turn numbers include the following enemy turn, and distinguish extra turns.
        var wasDamaged = CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>().Any(entry =>
            entry.Receiver == player.Creature && entry.HappenedLastPlayerTurn(player) &&
            entry.Result.UnblockedDamage > 0 && !entry.Result.Props.HasFlag(ValueProp.Unblockable));

        //Only resolve stacks already present at turn start, preserving any newly applied stacks.
        var count = AmountOnTurnStart;
        if (wasDamaged) Flash();
        await PowerCmd.ModifyAmount(choiceContext, this, -count, player.Creature, null);
        if (!wasDamaged) return;

        var combatState = player.Creature.CombatState!;
        for (var i = 0; i < count; i++)
        {
            await CreatureCmd.Damage(choiceContext, combatState.HittableEnemies, DynamicVars.Damage, player.Creature);
        }
    }
}

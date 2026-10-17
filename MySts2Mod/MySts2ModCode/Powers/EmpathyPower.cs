using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;

namespace MySts2Mod.MySts2ModCode.Powers;

// Smart description only; the card already explains the effect.
#pragma warning disable STS001
public class EmpathyPower : MySts2ModPower
#pragma warning restore STS001
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    internal async Task ShareHealing(int amount)
    {
        var teammates = CombatState!.GetTeammatesOf(Owner)
            .Where(creature => creature.IsPlayer && creature.IsAlive && creature != Owner)
            .ToArray();
        if (teammates.Length == 0) return;

        Flash();
        foreach (var teammate in teammates)
            await CreatureCmd.Heal(teammate, amount);
    }
}

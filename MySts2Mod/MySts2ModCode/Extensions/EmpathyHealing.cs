using System.Threading;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Extensions;

[HarmonyPatch(typeof(CreatureCmd), nameof(CreatureCmd.Heal))]
internal static class EmpathyHealing
{
    private sealed record Healing(Creature Creature, ICombatState State, EmpathyPower Power, int Amount);
    private static readonly AsyncLocal<bool> Sharing = new();

    private static bool IsDuringCombat => CombatManager.Instance.IsStarting || CombatManager.Instance.IsInProgress;

    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void BeforeHeal(Creature creature, decimal amount, out Healing? __state)
    {
        __state = null;
        if (Sharing.Value || amount <= 0 || !creature.IsPlayer || !IsDuringCombat ||
            creature.CombatState is not { } state || creature.GetPower<EmpathyPower>() is not { } power) return;

        // Native Heal immediately clamps HP, before awaiting effects or issuing
        // AfterCurrentHpChanged (whose delta is the requested, not actual heal).
        int actualHealing = (int)Math.Max(0m, Math.Min(amount, creature.MaxHp - creature.CurrentHp));
        if (actualHealing > 0)
            __state = new Healing(creature, state, power, actualHealing);
    }

    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    private static void AfterHeal(ref Task __result, Healing? __state)
    {
        if (__state != null)
            __result = ShareAfterHeal(__result, __state);
    }

    private static async Task ShareAfterHeal(Task heal, Healing healing)
    {
        await heal;
        if (!IsDuringCombat || !ReferenceEquals(healing.Creature.CombatState, healing.State) ||
            !ReferenceEquals(healing.Creature.GetPower<EmpathyPower>(), healing.Power)) return;

        bool previous = Sharing.Value;
        Sharing.Value = true;
        try
        {
            await healing.Power.ShareHealing(healing.Amount);
        }
        finally
        {
            Sharing.Value = previous;
        }
    }
}

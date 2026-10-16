using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Extensions;

/// <summary>Read-only snapshots for victory conditions, including cards generated after combat starts.</summary>
internal static class CombatBoundarySnapshots
{
    private sealed class Data
    {
        public Dictionary<ulong, int> StartingHp { get; } = new();
        public Dictionary<ulong, (int Hp, int Hao)> EndingValues { get; } = new();
    }

    private static readonly ConditionalWeakTable<ICombatState, Data> Snapshots = new();

    public static bool TryGetCompletedState(Creature creature, out int startingHp, out int endingHp, out int endingHao)
    {
        startingHp = endingHp = endingHao = 0;
        if (creature.Player is not { } player || creature.CombatState is not { } state ||
            !Snapshots.TryGetValue(state, out var data) ||
            !data.StartingHp.TryGetValue(player.NetId, out startingHp) ||
            !data.EndingValues.TryGetValue(player.NetId, out var values)) return false;

        endingHp = values.Hp;
        endingHao = values.Hao;
        return true;
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.BeforeCombatStart))]
    private static class CaptureStart
    {
        private static void Prefix(ICombatState? combatState)
        {
            if (combatState == null) return;
            var data = new Data();
            foreach (var player in combatState.Players)
                data.StartingHp[player.NetId] = player.Creature.CurrentHp;
            Snapshots.Remove(combatState);
            Snapshots.Add(combatState, data);
        }
    }

    [HarmonyPatch(typeof(Hook), nameof(Hook.AfterCombatEnd))]
    private static class CaptureEnd
    {
        private static void Prefix(ICombatState? combatState)
        {
            if (combatState == null || !Snapshots.TryGetValue(combatState, out var data)) return;
            // Check the finished combat before end-of-combat healing, Hao removal or JT8-3's HP loss.
            foreach (var player in combatState.Players)
                data.EndingValues[player.NetId] =
                    (player.Creature.CurrentHp, player.Creature.GetPower<HaoPower>()?.Amount ?? 0);
        }
    }
}

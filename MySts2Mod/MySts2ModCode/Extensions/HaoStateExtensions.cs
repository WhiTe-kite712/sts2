using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Extensions;

public enum HaoState
{
    None,
    Hidden,
    OpenHao
}

/// <summary>Two mutually exclusive states. No Hao or additional resource is required.</summary>
public static class HaoStateExtensions
{
    public static HaoState GetHaoState(this Player player)
    {
        if (player.Creature.GetPower<HiddenStatePower>() != null)
            return HaoState.Hidden;
        if (player.Creature.GetPower<OpenHaoStatePower>() != null)
            return HaoState.OpenHao;
        return HaoState.None;
    }

    public static bool IsHidden(this Player player) =>
        player.GetHaoState() == HaoState.Hidden;

    public static bool IsOpenHao(this Player player) =>
        player.GetHaoState() == HaoState.OpenHao;

    // These pure helpers are also useful for validating the transition and once-per-turn rules.
    public static HaoState GetOppositeHaoState(HaoState current) =>
        current == HaoState.Hidden ? HaoState.OpenHao : HaoState.Hidden;

    public static bool IsRewardingHaoStateSwitch(HaoState before, HaoState after) =>
        before != HaoState.None && after != HaoState.None && before != after;

    public static bool TryConsumeHaoStateSwitchReward(int currentTurn, ref int lastTriggeredTurn)
    {
        if (lastTriggeredTurn == currentTurn) return false;
        lastTriggeredTurn = currentTurn;
        return true;
    }

    public static Task FlipHaoState(this Player player, PlayerChoiceContext choiceContext, CardModel cardSource) =>
        player.EnterHaoState(choiceContext, GetOppositeHaoState(player.GetHaoState()), cardSource);

    public static async Task EnterHaoState(this Player player, PlayerChoiceContext choiceContext,
        HaoState next, CardModel? cardSource)
    {
        var before = player.GetHaoState();
        if (next == HaoState.None || next == before) return;

        var creature = player.Creature;
        await PowerCmd.Remove<HiddenStatePower>(creature);
        await PowerCmd.Remove<OpenHaoStatePower>(creature);
        if (next == HaoState.Hidden)
            await PowerCmd.Apply<HiddenStatePower>(choiceContext, creature, 1, creature, cardSource);
        else
            await PowerCmd.Apply<OpenHaoStatePower>(choiceContext, creature, 1, creature, cardSource);

        if (!IsRewardingHaoStateSwitch(before, player.GetHaoState())) return;

        // Consume the once-per-turn switch reward before drawing cards.
        var dualLife = creature.GetPower<DualLifePower>();
        if (dualLife != null)
            await dualLife.OnHaoStateSwitched(choiceContext, player);
    }
}


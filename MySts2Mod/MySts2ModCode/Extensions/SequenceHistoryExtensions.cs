using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace MySts2Mod.MySts2ModCode.Extensions;

/// <summary>
/// Own-player, current-turn sequence queries. CardPlay identity matters: excluding the
/// card object would also erase its earlier plays and break Replay and returned cards.
/// </summary>
public static class SequenceHistoryExtensions
{
    public static IReadOnlyList<CardPlay> PriorPlays(CardModel card, CardPlay? currentPlay = null)
    {
        if (card.CombatState is not { } state || !CombatManager.Instance.IsInProgress)
            return Array.Empty<CardPlay>();

        var player = currentPlay?.Player ?? card.Owner;
        var plays = CombatManager.Instance.History.CardPlaysStarted
            .Where(entry => entry.HappenedThisTurn(state) && entry.CardPlay.Player == player)
            .Select(entry => entry.CardPlay)
            .ToList();

        // Preview calculation does not receive a CardPlay. Only infer the active entry
        // while this exact card is on the table, including the current Replay index.
        if (currentPlay is null && card.Pile?.Type == PileType.Play)
            currentPlay = plays.LastOrDefault(play => play.Card == card && play.PlayIndex == card.CurrentPlayIndex);

        return BeforeCurrentPlay(plays, currentPlay);
    }

    // Pure sequence entry point for regression fixtures; preserves earlier plays of
    // the same CardModel and truncates by the exact CardPlay reference.
    public static IReadOnlyList<CardPlay> BeforeCurrentPlay(IEnumerable<CardPlay> plays, CardPlay? currentPlay) =>
        plays.TakeWhile(play => !ReferenceEquals(play, currentPlay)).ToArray();

    public static bool PreviousTypeIs(CardModel card, CardType type, CardPlay? currentPlay = null) =>
        PreviousTypeIs(PriorPlays(card, currentPlay), type);

    public static bool PreviousTypeIs(IReadOnlyList<CardPlay> prior, CardType type) =>
        prior.LastOrDefault()?.Card.Type == type;
}

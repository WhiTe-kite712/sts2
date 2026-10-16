using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

public class SilverLiningPower : MySts2ModPower
{
    private sealed class Data
    {
        public int UsesThisTurn;
        public List<CardPlay> ActivePlays { get; } = new();
        public HashSet<CardPlay> QualifyingPlays { get; } = new();
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1), new CardsVar(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => RegularDebuffs.HoverTips;
    protected override object InitInternalData() => new Data();

    public override Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == Owner.Side && participants.Contains(Owner))
        {
            var data = GetInternalData<Data>();
            data.UsesThisTurn = 0;
            data.ActivePlays.Clear();
            data.QualifyingPlays.Clear();
        }
        return Task.CompletedTask;
    }

    public override Task BeforeCardPlayed(CardPlay cardPlay)
    {
        if (cardPlay.Player == Owner.Player && CombatManager.Instance.IsPartOfPlayerTurn(cardPlay.Player))
            GetInternalData<Data>().ActivePlays.Add(cardPlay);
        return Task.CompletedTask;
    }

    public override Task BeforePowerAmountChanged(PowerModel power, decimal amount, Creature target,
        Creature? applier, CardModel? cardSource)
    {
        if (target != Owner || applier != Owner || cardSource == null || amount <= 0 ||
            !RegularDebuffs.IsRegular(power)) return Task.CompletedTask;

        var data = GetInternalData<Data>();
        var play = data.ActivePlays.LastOrDefault(play => ReferenceEquals(play.Card, cardSource));
        // Observe the card's application attempt, before Artifact or other application modifiers.
        if (play != null) data.QualifyingPlays.Add(play);
        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var data = GetInternalData<Data>();
        data.ActivePlays.Remove(cardPlay);
        if (!data.QualifyingPlays.Remove(cardPlay) || cardPlay.Player != Owner.Player ||
            !CombatManager.Instance.IsPartOfPlayerTurn(cardPlay.Player) || data.UsesThisTurn >= Amount) return;

        // Reserve the use before drawing: automatic card plays can re-enter these hooks.
        data.UsesThisTurn++;
        Flash();
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, cardPlay.Player);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, cardPlay.Player);
    }
}

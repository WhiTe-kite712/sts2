using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MySts2Mod.MySts2ModCode.Powers;

// Smart description only; source checks still require localized title and smartDescription.
#pragma warning disable STS001
public class TriathlonPower : MySts2ModPower
#pragma warning restore STS001
{
    private sealed class Data
    {
        public CardPlay? StartingPlay;
        public int TypesPlayed;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new PowerVar<StrengthPower>(2), new PowerVar<DexterityPower>(2)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
        [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()];
    protected override object InitInternalData() => new Data();

    public void StartAfter(CardPlay play) => GetInternalData<Data>().StartingPlay = play;

    public override async Task AfterCardPlayed(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var data = GetInternalData<Data>();
        if (cardPlay.Player != Owner.Player || ReferenceEquals(cardPlay, data.StartingPlay) ||
            Owner.CombatState?.CurrentSide != Owner.Side) return;

        data.TypesPlayed |= cardPlay.Card.Type switch
        {
            CardType.Attack => 1,
            CardType.Skill => 2,
            CardType.Power => 4,
            _ => 0,
        };
        if (data.TypesPlayed != 7) return;

        Flash();
        await PowerCmd.Remove(this);
        await PowerCmd.Apply<StrengthPower>(choiceContext, Owner, DynamicVars.Strength.BaseValue, Owner, null);
        await PowerCmd.Apply<DexterityPower>(choiceContext, Owner, DynamicVars.Dexterity.BaseValue, Owner, null);
    }

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side == Owner.Side && participants.Contains(Owner)) await PowerCmd.Remove(this);
    }
}

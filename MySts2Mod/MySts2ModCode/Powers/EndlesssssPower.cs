using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MySts2Mod.MySts2ModCode.Powers;

public class EndlesssssPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new IntVar("PlayThreshold", 12)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<DrawCardsNextTurnPower>()];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner)) return;
        var state = Owner.CombatState;
        var plays = CombatManager.Instance.History.CardPlaysStarted.Count(entry =>
            entry.CardPlay.Player == Owner.Player && entry.HappenedThisTurn(state));
        var cards = Amount;
        await PowerCmd.Remove(this);
        if (plays <= DynamicVars["PlayThreshold"].IntValue) return;

        await PowerCmd.Apply<DrawCardsNextTurnPower>(choiceContext, Owner, cards, Owner, null);
    }
}

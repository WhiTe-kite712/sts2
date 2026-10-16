using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

public class AnswerQuestionsPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<HaoPower>()];

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != Owner.Side || !participants.Contains(Owner)) return;
        var damage = CombatManager.Instance.History.Entries.OfType<DamageReceivedEntry>()
            .Where(entry => entry.Dealer == Owner && entry.Receiver.Side != Owner.Side &&
                entry.HappenedThisTurn(Owner.CombatState) && !entry.Result.Props.HasFlag(ValueProp.Unblockable))
            .Sum(entry => entry.Result.TotalDamage);
        var threshold = Amount;
        await PowerCmd.Remove(this);
        var player = Owner.Player!;
        var hao = player.GetHao();
        Flash();
        if (damage >= threshold)
        {
            await player.GainHao(choiceContext, hao);
        }
        else
        {
            var lost = await player.LoseHao(choiceContext, hao);
            if (lost > 0) await CreatureCmd.Damage(choiceContext, Owner, lost, ValueProp.Unpowered, Owner);
        }
    }
}

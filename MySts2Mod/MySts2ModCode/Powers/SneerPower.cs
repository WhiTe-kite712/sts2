using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 讥笑：本回合你格挡掉的伤害，会对攻击者造成等量伤害。回合结束移除。
/// </summary>
// Smart description only; source checks still require localized title and smartDescription.
#pragma warning disable STS001
public class SneerPower : MySts2ModPower
#pragma warning restore STS001
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (target != Owner || dealer == null || dealer.Side == Owner.Side || result.BlockedDamage <= 0) return;

        await CreatureCmd.Damage(choiceContext, dealer, result.BlockedDamage, ValueProp.Unpowered, Owner);
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choiceContext, CombatSide side, IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side != Owner.Side || !participants.Contains(Owner)) return;

        await PowerCmd.Remove(this);
    }
}

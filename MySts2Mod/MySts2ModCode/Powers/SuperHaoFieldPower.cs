using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>
/// 超豪力场：回合结束时，获得等同于豪意值一半的格挡，并对所有敌人造成等同于豪意值一半的伤害。
/// </summary>
public class SuperHaoFieldPower : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;

    public override async Task AfterSideTurnEnd(PlayerChoiceContext choiceContext, CombatSide side, IEnumerable<Creature> participants)
    {
        if (side != Owner.Side) return;

        var half = Owner.Player!.GetHao() / 2;
        if (half <= 0) return;

        await CreatureCmd.GainBlock(Owner, half, ValueProp.Move, null);
        var enemies = CombatState.Enemies.Where(e => e.IsAlive && e.IsHittable).ToArray();
        if (enemies.Length > 0)
        {
            await CreatureCmd.Damage(choiceContext, enemies, half, ValueProp.Move, Owner);
        }
    }
}

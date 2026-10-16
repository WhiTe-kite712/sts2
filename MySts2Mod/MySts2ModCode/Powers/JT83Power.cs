using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace MySts2Mod.MySts2ModCode.Powers;

public class JT83Power : MySts2ModPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<HaoPower>()];

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player.Creature != Owner) return;
        Flash();
        await CreatureCmd.Damage(choiceContext, Owner.CombatState!.HittableEnemies,
            Amount, ValueProp.Unpowered, Owner);
    }

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        var turns = Owner.Player?.PlayerCombatState?.TurnNumber ?? 0;
        if (Owner.IsDead || turns <= 0) return;
        await CreatureCmd.Damage(new ThrowingPlayerChoiceContext(), Owner, turns,
            ValueProp.Unblockable | ValueProp.Unpowered, Owner);
    }
}

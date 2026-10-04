using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;

namespace MySts2Mod.MySts2ModCode.Powers;

/// <summary>Tracks only the native stats contributed by this state.</summary>
public abstract class HaoStatStatePower : MySts2ModPower
{
    private sealed class Data
    {
        public int Strength;
        public int Dexterity;
    }

    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Single;
    public abstract int StrengthChange { get; }
    public abstract int DexterityChange { get; }
    protected override object InitInternalData() => new Data();
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [HoverTipFactory.FromPower<StrengthPower>(), HoverTipFactory.FromPower<DexterityPower>()];

    public override async Task AfterApplied(Creature? applier, CardModel? cardSource)
    {
        var context = new ThrowingPlayerChoiceContext();
        var data = GetInternalData<Data>();
        int strengthBefore = Owner.GetPower<StrengthPower>()?.Amount ?? 0;
        await PowerCmd.Apply<StrengthPower>(context, Owner, StrengthChange, applier, cardSource);
        data.Strength = (Owner.GetPower<StrengthPower>()?.Amount ?? 0) - strengthBefore;
        int dexterityBefore = Owner.GetPower<DexterityPower>()?.Amount ?? 0;
        await PowerCmd.Apply<DexterityPower>(context, Owner, DexterityChange, applier, cardSource);
        data.Dexterity = (Owner.GetPower<DexterityPower>()?.Amount ?? 0) - dexterityBefore;
    }

    public override async Task AfterRemoved(Creature oldOwner)
    {
        var data = GetInternalData<Data>();
        int strength = data.Strength;
        int dexterity = data.Dexterity;
        // Consume the ledger before effects can enter this callback again.
        data.Strength = data.Dexterity = 0;
        await RevertContribution<StrengthPower>(oldOwner, strength);
        await RevertContribution<DexterityPower>(oldOwner, dexterity);
    }

    private static async Task RevertContribution<T>(Creature owner, int contribution) where T : PowerModel
    {
        if (contribution == 0) return;
        var power = owner.GetPower<T>();
        int restored = (power?.Amount ?? 0) - contribution;
        // Undo bookkeeping exactly. A fresh Apply could be blocked by Artifact
        // or multiplied by a Strength relic, accumulating stats on every switch.
        if (power is null)
        {
            if (restored != 0) ModelDb.Power<T>().ToMutable().ApplyInternal(owner, restored);
        }
        else
        {
            power.SetAmount(restored);
            if (power.Amount == 0) await PowerCmd.Remove(power);
        }
    }
}

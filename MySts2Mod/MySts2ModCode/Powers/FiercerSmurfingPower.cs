using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Rooms;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Powers;

// Smart description only; source checks still require localized title and smartDescription.
#pragma warning disable STS001
public class FiercerSmurfingPower : MySts2ModPower
#pragma warning restore STS001
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;
    public override PowerInstanceType InstanceType => PowerInstanceType.Instanced;
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<HaoPower>()];

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner.IsDead || !CombatBoundarySnapshots.TryGetCompletedState(Owner,
            out var startingHp, out var endingHp, out var endingHao) ||
            endingHp < startingHp || endingHao < Amount) return;

        var player = Owner.Player!;
        Flash();
        var selected = await CardSelectCmd.FromDeckForUpgrade(player,
            new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 0, 1) { Cancelable = true });
        foreach (var card in selected) CardCmd.Upgrade(card);
    }
}

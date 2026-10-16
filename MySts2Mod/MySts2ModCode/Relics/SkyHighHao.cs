using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Extensions;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Relics;

[Pool(typeof(HaoRelicPool))]
public class SkyHighHao : MySts2ModRelic
{
    // The original game's upgraded starting relics retain Starter rarity.
    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new IntVar("StartHao", 6),
        new IntVar("StartHeal", 6),
        new IntVar("Turns", 3),
        new IntVar("Hao", 2),
        new HealVar(2),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<HaoPower>()];

    public override async Task BeforeCombatStart()
    {
        Flash();
        await Owner.GainHao(new ThrowingPlayerChoiceContext(), DynamicVars["StartHao"].IntValue, null);
        await CreatureCmd.Heal(Owner.Creature, DynamicVars["StartHeal"].BaseValue);
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return;

        var turnNumber = player.PlayerCombatState?.TurnNumber ?? 0;
        if (turnNumber < 1 || turnNumber > DynamicVars["Turns"].IntValue) return;

        Flash();
        await player.GainHao(choiceContext, DynamicVars["Hao"].IntValue, null);
        await CreatureCmd.Heal(player.Creature, DynamicVars.Heal.BaseValue);
    }
}

using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Extensions;

using MegaCrit.Sts2.Core.HoverTips;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Relics;

/// <summary>
/// 「豪意」——Coding Farmer 的起始遗物。
/// 每场战斗的前 3 个自己的回合开始时，获得 2 点豪意并恢复 2 点生命。
/// </summary>
[Pool(typeof(HaoRelicPool))]
public class Hao : MySts2ModRelic
{
    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
    ];

    public override RelicRarity Rarity => RelicRarity.Starter;

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new DynamicVar("Turns", 3m),
        new DynamicVar("Hao", 2m),
        new HealVar(2m),
    ];

    public override async Task AfterPlayerTurnStart(MegaCrit.Sts2.Core.GameActions.Multiplayer.PlayerChoiceContext choiceContext, MegaCrit.Sts2.Core.Entities.Players.Player player)
    {
        // Turn-start hooks are broadcast to every player's relics in multiplayer.
        if (player != Owner) return;

        var turnNumber = player.PlayerCombatState?.TurnNumber ?? 0;
        if (turnNumber < 1 || turnNumber > DynamicVars["Turns"].IntValue) return;

        await player.GainHao(choiceContext, DynamicVars["Hao"].IntValue, null);
        await CreatureCmd.Heal(player.Creature, DynamicVars.Heal.IntValue);
    }
}

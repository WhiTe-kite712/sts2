using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Extensions;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Relics;

[Pool(typeof(HaoRelicPool))]
public class DualBoot : MySts2ModRelic
{
    private int _lastTriggeredTurn = -1;

    public override RelicRarity Rarity => RelicRarity.Rare;

    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<HaoPower>()];

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new BlockVar(3, ValueProp.Move),
        new CardsVar(1),
    ];

    public override Task BeforeCombatStart()
    {
        _lastTriggeredTurn = -1;
        return Task.CompletedTask;
    }

    public async Task OnHaoStateSwitched(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return;

        var turnNumber = player.PlayerCombatState?.TurnNumber ?? 0;
        if (turnNumber < 1 || !HaoStateExtensions.TryConsumeHaoStateSwitchReward(turnNumber, ref _lastTriggeredTurn))
            return;

        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, null);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
    }
}

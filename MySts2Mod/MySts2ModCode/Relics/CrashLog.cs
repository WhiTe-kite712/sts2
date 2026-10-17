using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Relics;

[Pool(typeof(HaoRelicPool))]
public class CrashLog : MySts2ModRelic
{
    private bool _tookEnemyDamageThisTurn;

    public override RelicRarity Rarity => RelicRarity.Uncommon;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    public override Task BeforeCombatStart()
    {
        _tookEnemyDamageThisTurn = false;
        return Task.CompletedTask;
    }

    public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
    {
        if (player != Owner) return;

        var shouldDraw = _tookEnemyDamageThisTurn;
        _tookEnemyDamageThisTurn = false;
        if (!shouldDraw) return;

        Flash();
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
    }

    public override Task AfterDamageReceived(
        PlayerChoiceContext choiceContext,
        Creature target,
        DamageResult result,
        ValueProp props,
        Creature? dealer,
        CardModel? cardSource)
    {
        if (target == Owner.Creature && result.UnblockedDamage > 0 &&
            !props.HasFlag(ValueProp.Unblockable) && dealer != null && dealer.Side != Owner.Creature.Side)
        {
            _tookEnemyDamageThisTurn = true;
        }

        return Task.CompletedTask;
    }
}

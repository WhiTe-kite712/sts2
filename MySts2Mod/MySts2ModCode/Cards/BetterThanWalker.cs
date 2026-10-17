using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class BetterThanWalker : MySts2ModCard
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(10, ValueProp.Move), new HealVar(3)];

    public BetterThanWalker() : base(1, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        var combatState = CombatState!;
        // Group the chain as one attack, as the native EchoingSlash card does.
        await using var attackContext = await AttackCommand.CreateContextAsync(combatState, choiceContext, cardPlay);
        await CreatureCmd.TriggerAnim(player.Creature, "Attack", player.Character.AttackAnimDelay);
        int pendingAttacks = 1;
        while (pendingAttacks > 0)
        {
            pendingAttacks--;
            var results = (await CreatureCmd.Damage(choiceContext, combatState.HittableEnemies,
                DynamicVars.Damage, player.Creature, this, cardPlay)).ToList();
            attackContext.AddHit(results);

            int kills = results.Count(result => result.WasTargetKilled);
            for (int i = 0; i < kills; i++)
                await CreatureCmd.Heal(player.Creature, DynamicVars.Heal.BaseValue);
            pendingAttacks += kills;
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

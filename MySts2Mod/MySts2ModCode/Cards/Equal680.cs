using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class Equal680 : MySts2ModCard
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new PowerVar<StrengthPower>(4)];

    public Equal680() : base(1, CardType.Power, CardRarity.Uncommon, TargetType.AllAllies) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var state = CombatState!;
        var allies = state.Players.Select(player => player.Creature).ToArray();
        long alliedMaxHp = allies.Sum(creature => (long)creature.MaxHp);
        long enemyMaxHp = state.Enemies.Where(creature => creature.IsAlive).Sum(creature => (long)creature.MaxHp);
        if (alliedMaxHp >= enemyMaxHp) return;

        foreach (var ally in allies.Where(creature => creature.IsAlive))
            await PowerCmd.Apply<StrengthPower>(choiceContext, ally,
                DynamicVars.Strength.BaseValue, cardPlay.Player.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Strength.UpgradeValueBy(2);
}

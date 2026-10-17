using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class ZeroSumGame : MySts2ModCard
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(3), new CardsVar(2)];

    public ZeroSumGame() : base(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        await PlayerCmd.GainEnergy(DynamicVars.Energy.IntValue, player);
        await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.BaseValue, player);

        // Cloning after the increase preserves the accumulated combat cost and upgrades.
        EnergyCost.AddThisCombat(1);
        foreach (var teammate in CombatState!.Players.Where(other => other != player).ToArray())
        {
            var copy = CreateCloneForPlayer(teammate);
            CardCmd.PreviewCardPileAdd(await CardPileCmd.AddGeneratedCardToCombat(copy, PileType.Discard, player));
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

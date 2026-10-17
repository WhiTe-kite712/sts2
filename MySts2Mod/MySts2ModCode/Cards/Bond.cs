using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class Bond : MySts2ModCard
{
    public override CardMultiplayerConstraint MultiplayerConstraint => CardMultiplayerConstraint.MultiplayerOnly;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new IntVar("RetainOne", 4), new IntVar("RetainTwo", 7), new IntVar("RetainThree", 11)];
    protected override IEnumerable<IHoverTip> ExtraHoverTips => [HoverTipFactory.FromPower<HaoPower>()];

    public Bond() : base(1, CardType.Power, CardRarity.Rare, TargetType.Self) { }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var player = cardPlay.Player;
        var retained = CombatState!.Players.Count(other => other != player) switch
        {
            1 => DynamicVars["RetainOne"].IntValue,
            2 => DynamicVars["RetainTwo"].IntValue,
            >= 3 => DynamicVars["RetainThree"].IntValue,
            _ => 0,
        };
        await PowerCmd.Apply<BondPower>(choiceContext, player.Creature, retained, player.Creature, this);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["RetainOne"].UpgradeValueBy(2);
        DynamicVars["RetainTwo"].UpgradeValueBy(2);
        DynamicVars["RetainThree"].UpgradeValueBy(2);
    }
}

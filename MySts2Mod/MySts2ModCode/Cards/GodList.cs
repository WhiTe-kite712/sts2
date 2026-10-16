using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Character;
using MySts2Mod.MySts2ModCode.Powers;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class GodList : MySts2ModCard, ITomeCard
{
    public CharacterModel TomeCharacter => ModelDb.Character<CodingFarmer>();

    protected override IEnumerable<DynamicVar> CanonicalVars =>
    [
        new PowerVar<GodListPower>(2),
        new IntVar("MaxHpCap", 8),
    ];

    protected override IEnumerable<IHoverTip> ExtraHoverTips =>
    [
        HoverTipFactory.FromPower<HaoPower>(),
        HoverTipFactory.FromPower<GodListPower>(),
    ];

    public GodList() : base(3, CardType.Power, CardRarity.Ancient, TargetType.Self)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var creature = cardPlay.Player.Creature;
        await PowerCmd.Apply<GodListPower>(choiceContext, creature,
            DynamicVars["GodListPower"].BaseValue, creature, this);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

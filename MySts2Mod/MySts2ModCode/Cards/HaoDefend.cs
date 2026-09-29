using BaseLib.Abstracts;
using BaseLib.Utils;
using MySts2Mod.MySts2ModCode.Character;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.CardPools;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Extensions;

namespace MySts2Mod.MySts2ModCode.Cards;

[Pool(typeof(HaoCardPool))]
public class HaoDefend : MySts2ModCard
{
    private const int cost = 1;
    private const CardType type = CardType.Skill;
    private const CardRarity rarity = CardRarity.Basic;
    private const TargetType target = TargetType.Self;

    public override bool GainsBlock => true;

    public override IEnumerable<CardTag> Tags => [CardTag.Defend];

    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("BlockCap", 11m)];

    public HaoDefend() : base(cost, type, rarity, target)
    {
    }

    protected override async Task OnPlay(PlayerChoiceContext choiceContext, CardPlay cardPlay)
    {
        var hao = cardPlay.Card.Owner.GetHao();
        var block = Math.Min(hao * 3, (int)DynamicVars["BlockCap"].BaseValue);
        await CommonActions.CardBlock(this, new BlockVar(block, ValueProp.Move), cardPlay);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["BlockCap"].UpgradeValueBy(4);
    }
}

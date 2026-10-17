using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Character;

namespace MySts2Mod.MySts2ModCode.Relics;

[Pool(typeof(HaoRelicPool))]
public class GarbageCollection : MySts2ModRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;

    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(2, ValueProp.Move)];

    public override async Task AfterCardExhausted(
        PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
    {
        if (card.Owner != Owner) return;
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, null);
    }
}

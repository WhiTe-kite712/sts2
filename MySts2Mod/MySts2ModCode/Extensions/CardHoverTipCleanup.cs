using HarmonyLib;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MySts2Mod.MySts2ModCode.Cards;

namespace MySts2Mod.MySts2ModCode.Extensions;

// GainsBlock also affects game mechanics, so keep it intact and remove only
// the redundant explanations after the game's card tooltip list is assembled.
[HarmonyPatch(typeof(CardModel), nameof(CardModel.HoverTips), MethodType.Getter)]
internal static class CardHoverTipCleanup
{
    [HarmonyPostfix]
    private static void Postfix(CardModel __instance, ref IEnumerable<IHoverTip> __result)
    {
        if (__instance is not MySts2ModCard) return;

        var blockId = HoverTipFactory.Static(StaticHoverTip.Block).Id;
        var nextTurnBlockId = ModelDb.Power<BlockNextTurnPower>().Id.ToString();
        __result = __result.Where(tip => tip.IsSmart ||
            (tip.Id != blockId && tip.Id != nextTurnBlockId)).ToArray();
    }
}

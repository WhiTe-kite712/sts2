using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;

namespace MySts2Mod.MySts2ModCode.Extensions;

/// <summary>
/// Keep authored localization intact while routing shorthand upgrade branches to
/// the game's ShowIfUpgradedFormatter, including its green upgrade preview.
/// </summary>
[HarmonyPatch(typeof(LocString), nameof(LocString.GetRawText))]
internal static class UpgradeTextCompatibility
{
    private static readonly Regex Shorthand = new(@"\{IfUpgraded:(?!show:)", RegexOptions.Compiled);

    [HarmonyPostfix]
    private static void Postfix(LocString __instance, ref string __result)
    {
        __result = Normalize(__instance.LocTable, __instance.LocEntryKey, __result);
    }

    internal static string Normalize(string table, string key, string text) =>
        table == "cards" && key.StartsWith("MYSTS2MOD-", StringComparison.Ordinal)
            ? Shorthand.Replace(text, "{IfUpgraded:show:")
            : text;
}

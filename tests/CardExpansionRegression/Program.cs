using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MySts2Mod.MySts2ModCode.Cards;
using MySts2Mod.MySts2ModCode.Powers;

string dataDir = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(x => x.Key == "Sts2DataDir").Value!;
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string file = Path.Combine(dataDir, name.Name + ".dll");
    return File.Exists(file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
};
int expected = args.Length > 0 ? int.Parse(args[0]) : 54;
string output = args.Length > 1 ? args[1] : "inventory.json";
var types = typeof(MySts2ModCard).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(MySts2ModCard).IsAssignableFrom(t)).OrderBy(t => t.Name).ToArray();
int failures = 0, tests = 0;
var inventory = new List<object>();
var powerInventory = new List<object>();
Check("concrete card count", () => Equal(expected, types.Length));
Check("distinct card class names", () => Equal(types.Length, types.Select(t => t.Name).Distinct().Count()));
foreach (var type in types)
{
    Check(type.Name + " registers, clones and upgrades", () =>
    {
        ModelDb.Inject(type);
        var canonical = ModelDb.GetById<CardModel>(ModelDb.GetId(type));
        var card = canonical.ToMutable();
        var before = State(card);
        card.UpgradeInternal();
        card.FinalizeUpgradeInternal();
        var after = State(card);
        Equal(1, card.CurrentUpgradeLevel);
        Equal(0, canonical.CurrentUpgradeLevel);
        var pools = type.GetCustomAttributes<PoolAttribute>().ToArray();
        Equal(1, pools.Length);
        inventory.Add(new { className = type.Name, id = Id(type), pool = pools[0].PoolType!.Name, before, after });
    });
}
foreach (var type in typeof(MySts2ModPower).Assembly.GetTypes().Where(t => !t.IsAbstract && typeof(MySts2ModPower).IsAssignableFrom(t)).OrderBy(t => t.Name))
{
    Check(type.Name + " registers and defines dynamic variables", () =>
    {
        ModelDb.Inject(type);
        var power = ModelDb.GetById<PowerModel>(ModelDb.GetId(type));
        powerInventory.Add(new { className = type.Name, id = Id(type), variables = power.DynamicVars.ToDictionary(v => v.Key, v => v.Value.BaseValue) });
    });
}
ExpansionRules.Run(Check);
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
File.WriteAllText(output, JsonSerializer.Serialize(inventory, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output))!, "power-inventory.json"), JsonSerializer.Serialize(powerInventory, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"RESULT {tests - failures}/{tests} checks passed; inventory {inventory.Count} cards");
return failures == 0 ? 0 : 1;

object State(CardModel card) => new
{
    cost = card.EnergyCost.GetWithModifiers(CostModifiers.None),
    costsX = card.EnergyCost.CostsX,
    type = card.Type.ToString(),
    rarity = card.Rarity.ToString(),
    target = card.TargetType.ToString(),
    keywords = card.GetKeywordsWithSources(KeywordSources.Local).Select(k => k.ToString()).Order().ToArray(),
    variables = card.DynamicVars.ToDictionary(v => v.Key, v => v.Value.BaseValue),
};
string Id(Type type) => "MYSTS2MOD-" + Regex.Replace(type.Name, "(?<=[A-Za-z0-9])(?=[A-Z])", "_").ToUpperInvariant();
void Check(string name, Action action)
{
    tests++;
    try { action(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex); }
}
void Equal<T>(T expectedValue, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expectedValue, actual)) throw new Exception($"Expected {expectedValue}, got {actual}");
}

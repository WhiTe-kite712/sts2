using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.RegularExpressions;
using HarmonyLib;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MySts2Mod.MySts2ModCode.Extensions;

var assembly = Assembly.GetExecutingAssembly();
string Metadata(string key) => assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(x => x.Key == key).Value!;
string dataDir = Metadata("Sts2DataDir");
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var path = Path.Combine(dataDir, name.Name + ".dll");
    return File.Exists(path) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(path) : null;
};
return Run();

int Run()
{
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    var manager = (LocManager)RuntimeHelpers.GetUninitializedObject(typeof(LocManager));
    typeof(LocManager).GetMethod("LoadLocFormatters", flags)!.Invoke(manager, null);
    typeof(LocManager).GetField("<Instance>k__BackingField", flags)!.SetValue(null, manager);
    typeof(LocManager).GetField("<CultureInfo>k__BackingField", flags)!.SetValue(manager, CultureInfo.InvariantCulture);
    var tables = new Dictionary<string, LocTable>();
    typeof(LocManager).GetField("_tables", flags)!.SetValue(manager, tables);
    typeof(LocManager).GetField("_engTables", flags)!.SetValue(manager, tables);
    var fixture = new Dictionary<string, string>
    {
        ["MYSTS2MOD-BARE.description"] = "{IfUpgraded:UP {Block:diff()}|BASE}",
        ["MYSTS2MOD-SHOW.description"] = "{IfUpgraded:show:UP {Block:diff()}|BASE}",
        ["MYSTS2MOD-NESTED.description"] = "{InCombat:In {IfUpgraded:UP|BASE}|Out}",
        ["OTHER-BARE.description"] = "{IfUpgraded:UP|BASE}",
    };
    tables["cards"] = new LocTable("cards", fixture);
    tables["powers"] = new LocTable("powers", fixture);
    bool formatterOnly = args.Contains("--formatter-only");
    var harmony = new Harmony("CodeFarmer.UpgradeTextRegression");
    if (!formatterOnly) harmony.PatchAll(assembly);
    int tests = 0;
    try
    {
        foreach (var (input, expected) in new[]
        {
            ("Plain text {Block:diff()}", "Plain text {Block:diff()}"),
            ("{IfUpgraded:UP|BASE}", "{IfUpgraded:show:UP|BASE}"),
            ("{IfUpgraded:show:UP|BASE}", "{IfUpgraded:show:UP|BASE}"),
            ("{IfUpgraded:UP {Block:diff()}|BASE}", "{IfUpgraded:show:UP {Block:diff()}|BASE}"),
            ("{InCombat:{IfUpgraded:UP|BASE}|Out}", "{InCombat:{IfUpgraded:show:UP|BASE}|Out}"),
            ("{IfUpgraded:show:{IfUpgraded:UP|BASE}|}", "{IfUpgraded:show:{IfUpgraded:show:UP|BASE}|}"),
            ("{IfUpgradedFoo:UP|BASE}", "{IfUpgradedFoo:UP|BASE}"),
        })
        {
            var normalized = UpgradeTextCompatibility.Normalize("cards", "MYSTS2MOD-TEST.description", input);
            Equal(expected, normalized);
            Equal(expected, UpgradeTextCompatibility.Normalize("cards", "MYSTS2MOD-TEST.description", normalized));
            Equal(input, UpgradeTextCompatibility.Normalize("cards", "OTHER-TEST.description", input));
            Equal(input, UpgradeTextCompatibility.Normalize("powers", "MYSTS2MOD-TEST.description", input));
        }
        foreach (var display in Enum.GetValues<UpgradeDisplay>())
        {
            var expected = display switch
            {
                UpgradeDisplay.Normal => "BASE",
                UpgradeDisplay.Upgraded => "UP 7",
                UpgradeDisplay.UpgradePreview => "[green]UP 7[/green]",
                _ => throw new Exception("Unexpected upgrade display"),
            };
            Equal(expected, Format("MYSTS2MOD-BARE.description", display));
            Equal(expected, Format("MYSTS2MOD-SHOW.description", display));
            Equal("Out", Format("MYSTS2MOD-NESTED.description", display, false));
            Equal("In " + (display == UpgradeDisplay.Normal ? "BASE" : display == UpgradeDisplay.Upgraded ? "UP" : "[green]UP[/green]"),
                Format("MYSTS2MOD-NESTED.description", display, true));
        }
        Equal(fixture["OTHER-BARE.description"], Raw("cards", "OTHER-BARE.description"));
        Equal(fixture["MYSTS2MOD-BARE.description"], Raw("powers", "MYSTS2MOD-BARE.description"));
        Equal(fixture["MYSTS2MOD-SHOW.description"], Raw("cards", "MYSTS2MOD-SHOW.description"));
        var dir = args.FirstOrDefault(x => !x.StartsWith("--")) ?? Metadata("ModLocalizationDir");
        foreach (var language in new[] { "zhs", "eng" })
        {
            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(dir, language, "cards.json")))!;
            foreach (var entry in entries.Where(x => x.Value.Contains("{IfUpgraded:")))
            {
                var nativeKey = "NATIVE-EXPLICIT.description";
                fixture[entry.Key] = entry.Value;
                // The expected string explicitly requests the original game's show formatter.
                fixture[nativeKey] = Regex.Replace(entry.Value, @"\{IfUpgraded:(?!show:)", "{IfUpgraded:show:");
                tables["cards"] = new LocTable("cards", fixture);
                foreach (var display in Enum.GetValues<UpgradeDisplay>())
                {
                    var actual = Format(entry.Key, display);
                    Equal(Format(nativeKey, display), actual);
                    if (actual.Contains('{') || actual.Contains('}')) throw new Exception("Unformatted upgrade branch: " + entry.Key + " " + actual);
                }
            }
        }
        Console.WriteLine($"PASS {tests} native upgrade formatter checks (normal, upgraded, preview, nested values and scope; Harmony hook {(formatterOnly ? "not exercised" : "exercised")})");
        return 0;
    }
    finally { if (!formatterOnly) harmony.UnpatchAll(harmony.Id); }

    string Format(string key, UpgradeDisplay display, bool inCombat = false)
    {
        if (formatterOnly)
            tables["cards"] = new LocTable("cards", fixture.ToDictionary(x => x.Key,
                x => UpgradeTextCompatibility.Normalize("cards", x.Key, x.Value)));
        var loc = new LocString("cards", key);
        foreach (Match match in Regex.Matches(fixture[key], @"\{([A-Za-z][A-Za-z0-9]*)(?=[:}])"))
        {
            var name = match.Groups[1].Value;
            if (name is not ("IfUpgraded" or "InCombat")) loc.Add(new DynamicVar(name, 7));
        }
        loc.Add(new IfUpgradedVar(display));
        loc.Add("InCombat", inCombat);
        return loc.GetFormattedText();
    }
    string Raw(string table, string key)
    {
        var value = new LocString(table, key).GetRawText();
        return formatterOnly ? UpgradeTextCompatibility.Normalize(table, key, value) : value;
    }
    void Equal(string expected, string actual)
    {
        tests++;
        if (expected != actual) throw new Exception($"Expected '{expected}', got '{actual}'");
    }
}

using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Cards;
using MySts2Mod.MySts2ModCode.Powers;

internal static class HaoPreviewRules
{
    private static readonly Type[] CardTypes = [typeof(HaoStrike), typeof(HaoDefend), typeof(Debugging), typeof(CodeOffense), typeof(Calm)];
    private static readonly BindingFlags Flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;

    public static void Run(Action<string, Action> check, string localizationDir, bool fixtureOnly = false)
    {
        var owner = Creature(CombatSide.Player);
        var enemy = Creature(CombatSide.Enemy);
        var player = Player(owner);
        var hao = Power<HaoPower>(owner, 0);
        var strength = Power<StrengthPower>(owner, 3);
        var dexterity = Power<DexterityPower>(owner, 2);
        var listeners = new AbstractModel[] { strength, dexterity };
        var run = DispatchProxy.Create<IRunState, PreviewListenerProxy>();
        ((PreviewListenerProxy)(object)run).Listeners = listeners;
        Field(typeof(Player), "_runState").SetValue(player, run);
        var combat = DispatchProxy.Create<ICombatState, PreviewListenerProxy>();
        var proxy = (PreviewListenerProxy)(object)combat;
        proxy.Listeners = listeners; proxy.Run = run;
        owner.CombatState = combat; enemy.CombatState = combat;
        var turnField = Field(typeof(CombatManager), "_turnState");
        var previousTurn = turnField.GetValue(CombatManager.Instance);
        var turn = RuntimeHelpers.GetUninitializedObject(turnField.FieldType);
        var concreteCombat = (CombatState)RuntimeHelpers.GetUninitializedObject(typeof(CombatState));
        Field(typeof(CombatState), "_enemies").SetValue(concreteCombat, new List<Creature> { enemy });
        Field(turnField.FieldType, "<State>k__BackingField").SetValue(turn, concreteCombat);
        Field(turnField.FieldType, "<IsInProgress>k__BackingField").SetValue(turn, true);
        turnField.SetValue(CombatManager.Instance, turn);
        try
        {
            check("fixture has an active combat and real original-game stat hooks", () =>
            {
                Equal(true, CombatManager.Instance.IsInProgress);
                Equal(false, CombatManager.Instance.IsEnding);
                Equal(9m, Hook.ModifyDamage(run, combat, enemy, owner, 6, ValueProp.Move, null, null, ModifyDamageHookType.All, default, out _));
                Equal(7m, Hook.ModifyBlock(combat, owner, 5, ValueProp.Move, null, null, out _));
            });
            if (fixtureOnly) return;
            foreach (var type in CardTypes.Concat([typeof(Strike), typeof(Defend)])) ModelDb.Inject(type);
            check("ordinary Strike DamageVar automatically previews Strength once", () =>
            {
                var card = Card(typeof(Strike), player);
                Equal(false, card.DynamicVars.ContainsKey("CalculatedDamage"));
                Preview(card, enemy); Equal(9m, card.DynamicVars.Damage.PreviewValue);
                Upgrade(card); Preview(card, enemy); Equal(12m, card.DynamicVars.Damage.PreviewValue);
                Equal(9m, card.DynamicVars.Damage.BaseValue);
            });
            check("ordinary Defend BlockVar automatically previews Dexterity once", () =>
            {
                var card = Card(typeof(Defend), player);
                Equal(false, card.DynamicVars.ContainsKey("CalculatedBlock"));
                Preview(card, enemy); Equal(7m, card.DynamicVars.Block.PreviewValue);
                Upgrade(card); Preview(card, enemy); Equal(10m, card.DynamicVars.Block.PreviewValue);
                Equal(8m, card.DynamicVars.Block.BaseValue);
            });
            foreach (var type in CardTypes)
            foreach (var upgraded in new[] { false, true })
            {
                var card = Card(type, player);
                if (upgraded) Upgrade(card);
                foreach (var value in new[] { 0, 1, 5, 6, 10, 11, 20 })
                {
                    var expected = Expected(type, value, upgraded);
                    check($"{type.Name}{(upgraded ? "+" : "")} hao={value}: formula and stats applied once", () =>
                    {
                        SetAmount(hao, value);
                        Preview(card, enemy);
                        if (expected.Damage.HasValue)
                        {
                            Equal(expected.Damage.Value, card.DynamicVars.CalculatedDamage.Calculate(enemy));
                            Equal(expected.Damage.Value + 3, card.DynamicVars.CalculatedDamage.PreviewValue);
                            Equal(expected.Damage.Value + 3, Hook.ModifyDamage(run, combat, enemy, owner,
                                card.DynamicVars.CalculatedDamage.Calculate(enemy), ValueProp.Move, card, null, ModifyDamageHookType.All, default, out _));
                        }
                        if (expected.Block.HasValue)
                        {
                            Equal(expected.Block.Value, card.DynamicVars.CalculatedBlock.Calculate(owner));
                            Equal(expected.Block.Value + 2, card.DynamicVars.CalculatedBlock.PreviewValue);
                            Equal(expected.Block.Value + 2, Hook.ModifyBlock(combat, owner,
                                card.DynamicVars.CalculatedBlock.Calculate(owner), ValueProp.Move, card, null, out _));
                        }
                    });
                }
                check($"{type.Name}{(upgraded ? "+" : "")}: repeated recalculation does not mutate base values", () =>
                {
                    var initial = card.DynamicVars.ToDictionary(x => x.Key, x => x.Value.BaseValue);
                    for (var iteration = 0; iteration < 10; iteration++)
                    {
                        SetAmount(hao, 7); Preview(card, enemy);
                        SetAmount(hao, 2); Preview(card, enemy);
                        SetAmount(hao, 7); Preview(card, enemy);
                    }
                    foreach (var variable in card.DynamicVars) Equal(initial[variable.Key], variable.Value.BaseValue);
                    var expected = Expected(type, 7, upgraded);
                    if (expected.Damage.HasValue) Equal(expected.Damage.Value, card.DynamicVars.CalculatedDamage.Calculate(enemy));
                    if (expected.Block.HasValue) Equal(expected.Block.Value, card.DynamicVars.CalculatedBlock.Calculate(owner));
                });
                check($"{type.Name}{(upgraded ? "+" : "")}: upgrade values", () =>
                {
                    if (type == typeof(HaoStrike)) Equal(upgraded ? 9m : 6m, card.DynamicVars.CalculationBase.BaseValue);
                    if (type == typeof(HaoDefend)) Equal(upgraded ? 15m : 11m, card.DynamicVars["BlockCap"].BaseValue);
                    if (type == typeof(Calm)) Equal(upgraded ? 2m : 3m, card.EnergyCost.GetWithModifiers(CostModifiers.None));
                });
            }
            foreach (var type in new[] { typeof(HaoStrike), typeof(HaoDefend) })
            check(type.Name + " OnPlay has no DynamicVar BaseValue mutation", () =>
            {
                var onPlay = type.GetMethod("OnPlay", Flags)!;
                var stateMachine = onPlay.GetCustomAttribute<AsyncStateMachineAttribute>()!.StateMachineType;
                var moveNext = stateMachine.GetMethod("MoveNext", Flags)!;
                foreach (var method in CalledMethods(moveNext))
                    if (method.DeclaringType == typeof(DynamicVar) && method.Name == "set_BaseValue")
                        throw new Exception("OnPlay still mutates its base variable.");
            });
            FormatChecks(check, player, hao, enemy, localizationDir);
        }
        finally { turnField.SetValue(CombatManager.Instance, previousTurn); }
    }

    private static void FormatChecks(Action<string, Action> check, Player player, HaoPower hao, Creature enemy, string localizationDir)
    {
        var manager = (LocManager)RuntimeHelpers.GetUninitializedObject(typeof(LocManager));
        typeof(LocManager).GetMethod("LoadLocFormatters", Flags)!.Invoke(manager, null);
        typeof(LocManager).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, manager);
        Field(typeof(LocManager), "<CultureInfo>k__BackingField").SetValue(manager, CultureInfo.InvariantCulture);
        foreach (var language in new[] { "zhs", "eng" })
        {
            var translations = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(localizationDir, language, "cards.json")))!;
            Field(typeof(LocManager), "_tables").SetValue(manager, new Dictionary<string, LocTable> { ["cards"] = new LocTable("cards", translations) });
            foreach (var type in CardTypes)
            foreach (var inCombat in new[] { false, true })
            foreach (var haoValue in new[] { 0, 10, 11, 20 })
            check($"{language} {type.Name}: real formatter InCombat={inCombat}, hao={haoValue}", () =>
            {
                var card = Card(type, player);
                SetAmount(hao, haoValue); Preview(card, enemy);
                var key = "MYSTS2MOD-" + Regex.Replace(type.Name, "(?<=[A-Za-z0-9])(?=[A-Z])", "_").ToUpperInvariant() + ".description";
                Equal(true, translations[key].Contains("{InCombat:"));
                var description = new LocString("cards", key);
                card.DynamicVars.AddTo(description);
                card.GetType().GetMethod("AddExtraArgsToDescription", Flags)!.Invoke(card, [description]);
                description.Add("InCombat", inCombat);
                var result = Regex.Replace(description.GetFormattedText(), @"\[[^\]]+\]", "");
                if (result.Contains('{') || result.Contains('}')) throw new Exception("Unformatted placeholder remains: " + result);
                Equal(inCombat, result.Contains('\n'));
                if (!inCombat) return;
                var expected = Expected(type, haoValue, false);
                var line = result[(result.IndexOf('\n') + 1)..];
                var inactive = (type == typeof(Debugging) && haoValue <= 10) || (type == typeof(Calm) && haoValue <= 0);
                var numbers = Regex.Matches(line, @"\d+").Select(x => int.Parse(x.Value)).ToArray();
                var wanted = new List<int>();
                if (expected.Damage.HasValue) wanted.Add(inactive ? 0 : (int)expected.Damage.Value + 3);
                if (expected.Block.HasValue) wanted.Add(inactive ? 0 : (int)expected.Block.Value + 2);
                if (!wanted.SequenceEqual(numbers)) throw new Exception($"Expected [{string.Join(',', wanted)}], got {line}");
            });
        }
    }

    private static (decimal? Damage, decimal? Block) Expected(Type type, int hao, bool upgraded) => type.Name switch
    {
        nameof(HaoStrike) => ((upgraded ? 9m : 6m) + hao, null),
        nameof(HaoDefend) => (null, Math.Min(hao * 3m, upgraded ? 15m : 11m)),
        nameof(Debugging) => (null, Math.Max(0, hao - 10) * 2m),
        nameof(CodeOffense) => (hao * 2m * (hao >= 6 ? 2 : 1), null),
        nameof(Calm) => (hao * 4m, hao * 4m),
        _ => throw new ArgumentException(type.Name),
    };
    private static void Preview(CardModel card, Creature target) => card.UpdateDynamicVarPreview(CardPreviewMode.Normal, target, card.DynamicVars);
    private static void Upgrade(CardModel card) { card.UpgradeInternal(); card.FinalizeUpgradeInternal(); }
    private static CardModel Card(Type type, Player owner)
    {
        var card = ModelDb.GetById<CardModel>(ModelDb.GetId(type)).ToMutable();
        card.Owner = owner;
        ((List<CardModel>)Field(typeof(CardPile), "_cards").GetValue(owner.PlayerCombatState!.Hand)!).Add(card);
        return card;
    }
    private static Creature Creature(CombatSide side)
    {
        var value = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
        Field(typeof(Creature), "<Side>k__BackingField").SetValue(value, side);
        Field(typeof(Creature), "_powers").SetValue(value, new List<PowerModel>());
        Field(typeof(Creature), "_currentHp").SetValue(value, 100);
        return value;
    }
    private static Player Player(Creature owner)
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        Field(typeof(Player), "<Creature>k__BackingField").SetValue(player, owner);
        Field(typeof(Creature), "<Player>k__BackingField").SetValue(owner, player);
        Field(typeof(Player), "_runPiles").SetValue(player, Array.Empty<CardPile>());
        var state = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        foreach (var (name, type) in new[] { ("Hand", PileType.Hand), ("PlayPile", PileType.Play), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust) })
            Field(typeof(PlayerCombatState), "<" + name + ">k__BackingField").SetValue(state, new CardPile(type));
        Field(typeof(Player), "<PlayerCombatState>k__BackingField").SetValue(player, state);
        return player;
    }
    private static T Power<T>(Creature owner, int amount) where T : PowerModel
    {
        var value = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
        Field(typeof(AbstractModel), "<IsMutable>k__BackingField").SetValue(value, true);
        Field(typeof(PowerModel), "_owner").SetValue(value, owner);
        SetAmount(value, amount);
        ((List<PowerModel>)Field(typeof(Creature), "_powers").GetValue(owner)!).Add(value);
        return value;
    }
    private static void SetAmount(PowerModel power, int amount) => Field(typeof(PowerModel), "_amount").SetValue(power, amount);
    private static FieldInfo Field(Type type, string name) => type.GetField(name, Flags) ?? throw new MissingFieldException(type.Name, name);
    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }
    private static IEnumerable<MethodBase> CalledMethods(MethodInfo method)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static).Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => unchecked((ushort)o.Value));
        var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        for (var i = 0; i < bytes.Length;)
        {
            ushort code = bytes[i++]; if (code == 0xfe) code = (ushort)(0xfe00 | bytes[i++]);
            var opcode = opcodes[code];
            var length = opcode.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + BitConverter.ToInt32(bytes, i) * 4,
                _ => 4,
            };
            if (opcode.OperandType == OperandType.InlineMethod) yield return method.Module.ResolveMethod(BitConverter.ToInt32(bytes, i))!;
            i += length;
        }
    }
}

public class PreviewListenerProxy : DispatchProxy
{
    public IEnumerable<AbstractModel> Listeners { get; set; } = [];
    public IRunState? Run { get; set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
    {
        "IterateHookListeners" => Listeners,
        "get_RunState" => Run,
        _ => throw new NotSupportedException("Unexpected fixture call: " + method?.Name),
    };
}

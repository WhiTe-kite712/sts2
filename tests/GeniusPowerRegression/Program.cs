using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Powers;

string dataDir = Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().Single(x => x.Key == "Sts2DataDir").Value!;
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    string file = Path.Combine(dataDir, name.Name + ".dll");
    return File.Exists(file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
};
var owner = MakeCreature(CombatSide.Player);
var enemy = MakeCreature(CombatSide.Enemy);
var teammate = MakeCreature(CombatSide.Player);
int failures = 0, tests = 0;
Check("neutral multiplier is one", () => Equal(1m, MakePower<GeniusPower>(owner).ModifyDamageMultiplicative(owner, 10m, default, enemy, null, null)));
Check("unarmed flag does not boost unrelated damage", () => Equal(1m, MakePower<GeniusPower>(owner).ModifyDamageMultiplicative(teammate, 10m, default, enemy, null, null)));
Check("armed multiplier is two and previews do not consume flag", () =>
{
    var genius = MakePower<GeniusPower>(owner); genius.HaoLostFlag = true;
    for (int i = 0; i < 3; i++) Equal(2m, genius.ModifyDamageMultiplicative(owner, 10m, default, enemy, null, null));
    Equal(true, genius.HaoLostFlag);
});
Check("enemy hit consumes flag and later hit gets neutral multiplier", () =>
{
    var genius = MakePower<GeniusPower>(owner); genius.HaoLostFlag = true;
    Equal(2m, genius.ModifyDamageMultiplicative(owner, 10m, default, enemy, null, null));
    Settle(genius, owner, enemy, 0, 20);
    Equal(false, genius.HaoLostFlag);
    Equal(1m, genius.ModifyDamageMultiplicative(owner, 10m, default, enemy, null, null));
});
Check("fully blocked positive hit consumes flag", () =>
{
    var genius = MakePower<GeniusPower>(owner); genius.HaoLostFlag = true;
    Settle(genius, owner, enemy, 20, 0); Equal(false, genius.HaoLostFlag);
});
Check("zero damage callback preserves flag", () =>
{
    var genius = MakePower<GeniusPower>(owner); genius.HaoLostFlag = true;
    Settle(genius, owner, enemy, 0, 0); Equal(true, genius.HaoLostFlag);
});
Check("other target callback preserves flag", () =>
{
    var genius = MakePower<GeniusPower>(owner); genius.HaoLostFlag = true;
    Settle(genius, teammate, enemy, 0, 20); Equal(true, genius.HaoLostFlag);
});
Check("self friendly and no dealer cannot boost or consume flag", () =>
{
    var genius = MakePower<GeniusPower>(owner); genius.HaoLostFlag = true;
    foreach (var dealer in new Creature?[] { owner, teammate, null })
    {
        Equal(1m, genius.ModifyDamageMultiplicative(owner, 10m, default, dealer, null, null));
        Settle(genius, owner, dealer, 0, 10); Equal(true, genius.HaoLostFlag);
    }
});
Check("own Hao loss arms flag", () =>
{
    var genius = MakePower<GeniusPower>(owner);
    genius.AfterPowerAmountChanged(null!, MakePower<HaoPower>(owner), -1, owner, null).GetAwaiter().GetResult();
    Equal(true, genius.HaoLostFlag);
});
Check("teammate Hao loss does not arm flag", () =>
{
    var genius = MakePower<GeniusPower>(owner);
    genius.AfterPowerAmountChanged(null!, MakePower<HaoPower>(teammate), -1, teammate, null).GetAwaiter().GetResult();
    Equal(false, genius.HaoLostFlag);
});
Check("Hao gains and zero deltas do not arm flag", () =>
{
    var genius = MakePower<GeniusPower>(owner);
    foreach (var delta in new decimal[] { 0, 1 }) genius.AfterPowerAmountChanged(null!, MakePower<HaoPower>(owner), delta, owner, null).GetAwaiter().GetResult();
    Equal(false, genius.HaoLostFlag);
});
Check("other own power loss does not arm flag", () =>
{
    var genius = MakePower<GeniusPower>(owner);
    genius.AfterPowerAmountChanged(null!, MakePower<GeniusPower>(owner), -1, owner, null).GetAwaiter().GetResult();
    Equal(false, genius.HaoLostFlag);
});
Check("enemy unpowered and non-move damage retain original eligibility", () =>
{
    foreach (var props in new[] { ValueProp.Unpowered, default(ValueProp), ValueProp.Move | ValueProp.Unpowered })
    {
        var genius = MakePower<GeniusPower>(owner); genius.HaoLostFlag = true;
        Equal(2m, genius.ModifyDamageMultiplicative(owner, 10m, props, enemy, null, null));
        var result = new DamageResult(owner, props) { UnblockedDamage = 20 };
        genius.AfterDamageReceived(null!, owner, result, props, enemy, null).GetAwaiter().GetResult();
        Equal(false, genius.HaoLostFlag);
    }
});
Check("Hao delta reducing power to zero still arms flag", () =>
{
    var genius = MakePower<GeniusPower>(owner); var hao = MakePower<HaoPower>(owner);
    typeof(PowerModel).GetField("_amount", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(hao, 0);
    genius.AfterPowerAmountChanged(null!, hao, -1, owner, null).GetAwaiter().GetResult();
    Equal(true, genius.HaoLostFlag);
});
Check("real game damage pipeline gives 10 then 20 then 10", () =>
{
    var genius = MakePower<GeniusPower>(owner);
    var runState = DispatchProxy.Create<IRunState, HookListenersProxy>();
    ((HookListenersProxy)(object)runState).Listeners = [genius];
    decimal Calculate() => Hook.ModifyDamage(runState, null, owner, enemy, 10m, ValueProp.Move, null, null, ModifyDamageHookType.All, default, out _);
    Equal(10m, Calculate());
    genius.HaoLostFlag = true;
    for (int i = 0; i < 3; i++) Equal(20m, Calculate());
    Equal(true, genius.HaoLostFlag);
    Settle(genius, owner, enemy, 0, 20);
    Equal(10m, Calculate());
});
Console.WriteLine($"RESULT {tests - failures}/{tests} passed; assembly {typeof(GeniusPower).Assembly.Location}");
return failures == 0 ? 0 : 1;

void Check(string name, Action act)
{
    tests++;
    try { act(); Console.WriteLine("PASS " + name); }
    catch (Exception ex) { failures++; Console.WriteLine("FAIL " + name + ": " + ex.GetBaseException().Message); }
}
static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"expected {expected}, actual {actual}"); }
static Creature MakeCreature(CombatSide side)
{
    // These hook tests use real game classes without starting Godot or a combat room.
    // Only the fields inspected by GeniusPower are initialized; this is not a full combat fixture.
    var creature = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
    typeof(Creature).GetField("<Side>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(creature, side);
    return creature;
}
static T MakePower<T>(Creature owner) where T : PowerModel
{
    var power = (T)RuntimeHelpers.GetUninitializedObject(typeof(T));
    typeof(AbstractModel).GetField("<IsMutable>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(power, true);
    typeof(PowerModel).GetField("_owner", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(power, owner);
    return power;
}
static void Settle(GeniusPower power, Creature target, Creature? dealer, int blocked, int unblocked)
{
    var result = new DamageResult(target, default) { BlockedDamage = blocked, UnblockedDamage = unblocked };
    power.AfterDamageReceived(null!, target, result, default, dealer, null).GetAwaiter().GetResult();
}

public class HookListenersProxy : DispatchProxy
{
    public IEnumerable<AbstractModel> Listeners { get; set; } = [];
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "IterateHookListeners") return Listeners;
        throw new NotSupportedException("Unexpected game run state call: " + method?.Name);
    }
}

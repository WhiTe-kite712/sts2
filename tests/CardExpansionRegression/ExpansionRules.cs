using System.Reflection;
using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using MySts2Mod.MySts2ModCode.Cards;
using MySts2Mod.MySts2ModCode.Extensions;
using MySts2Mod.MySts2ModCode.Powers;

internal static class ExpansionRules
{
    public static void Run(Action<string, Action> check)
    {
        var owner = Creature(CombatSide.Player);
        var enemy = Creature(CombatSide.Enemy);
        var ally = Creature(CombatSide.Player);
        var player = Player(owner);
        var allyPlayer = Player(ally);
        var attack = Card<HaoStrike>(player);
        var skill = Card<Hindsight>(player);
        var powerCard = Card<Genius>(player);
        var current = Play(attack, player, 2);
        var previous = Play(attack, player, 1);
        var previousSkill = Play(skill, player, 0);
        var previousPower = Play(powerCard, player, 2);

        check("state transitions exclude initial entry and repeated identity", () =>
        {
            foreach (HaoState before in Enum.GetValues<HaoState>())
                foreach (HaoState after in Enum.GetValues<HaoState>())
                    Equal(before != HaoState.None && after != HaoState.None && before != after,
                        HaoStateExtensions.IsRewardingHaoStateSwitch(before, after));
            Equal(HaoState.Hidden, HaoStateExtensions.GetOppositeHaoState(HaoState.None));
            Equal(HaoState.OpenHao, HaoStateExtensions.GetOppositeHaoState(HaoState.Hidden));
            Equal(HaoState.Hidden, HaoStateExtensions.GetOppositeHaoState(HaoState.OpenHao));
        });
        check("state reward locks once per own turn including extra turns", () =>
        {
            int last = -1;
            Equal(true, HaoStateExtensions.TryConsumeHaoStateSwitchReward(1, ref last));
            Equal(false, HaoStateExtensions.TryConsumeHaoStateSwitchReward(1, ref last));
            Equal(true, HaoStateExtensions.TryConsumeHaoStateSwitchReward(2, ref last));
            Equal(false, HaoStateExtensions.TryConsumeHaoStateSwitchReward(2, ref last));
        });
        foreach (var (type, strength, dexterity) in new[] { (typeof(HiddenStatePower), -2, 4), (typeof(OpenHaoStatePower), 4, -2) })
        {
            var state = Power(type, owner);
            check(type.Name + " defines its reversible original-game stat contribution", () =>
            {
                var stats = (HaoStatStatePower)state;
                Equal(strength, stats.StrengthChange);
                Equal(dexterity, stats.DexterityChange);
            });
            check(type.Name + " does not add duplicate damage outside StrengthPower", () =>
            {
                Equal(0m, state.ModifyDamageAdditive(enemy, 10m, ValueProp.Move, owner, attack, current));
                Equal(0m, state.ModifyDamageAdditive(enemy, 10m, ValueProp.Move, ally, attack, current));
                Equal(0m, state.ModifyDamageAdditive(enemy, 10m, ValueProp.Move | ValueProp.Unpowered, owner, attack, current));
                Equal(0m, state.ModifyDamageAdditive(enemy, 10m, ValueProp.Move, owner, null, null));
            });
            check(type.Name + " does not add duplicate block outside DexterityPower", () =>
            {
                Equal(0m, state.ModifyBlockAdditive(owner, 7m, ValueProp.Move, skill, null));
                Equal(0m, state.ModifyBlockAdditive(ally, 7m, ValueProp.Move, skill, null));
                Equal(0m, state.ModifyBlockAdditive(owner, 7m, ValueProp.Move | ValueProp.Unpowered, skill, null));
                Equal(0m, state.ModifyBlockAdditive(owner, 7m, ValueProp.Move, Card<Hindsight>(allyPlayer), null));
                Equal(0m, state.ModifyBlockAdditive(owner, 7m, ValueProp.Unpowered, null, null));
            });
            check(type.Name + " leaves the engine damage pipeline to original-game stats", () =>
            {
                var run = DispatchProxy.Create<IRunState, ListenerProxy>();
                ((ListenerProxy)(object)run).Listeners = [state];
                Equal(10m, Hook.ModifyDamage(run, null, enemy, owner, 10m, ValueProp.Move, attack, current,
                    ModifyDamageHookType.All, default, out _));
            });
        }

        check("sequence excludes exact current play while retaining an earlier play of the same card", () =>
        {
            var prior = SequenceHistoryExtensions.BeforeCurrentPlay([previous, previousSkill, current, previousPower], current);
            Equal(2, prior.Count);
            Equal(previous, prior[0]);
            Equal(4, SequenceHistoryExtensions.BeforeCurrentPlay([previous, previousSkill, current, previousPower], null).Count);
        });
        check("PressForward does not receive its bonus with no preceding skill", () =>
        {
            Equal(false, SequenceHistoryExtensions.PreviousTypeIs([], CardType.Skill));
            Equal(false, SequenceHistoryExtensions.PreviousTypeIs([previous], CardType.Skill));
            Equal(false, SequenceHistoryExtensions.PreviousTypeIs([previousPower], CardType.Skill));
        });
        check("PressForward requires the immediately preceding card to be a skill", () =>
        {
            Equal(true, SequenceHistoryExtensions.PreviousTypeIs([previousSkill], CardType.Skill));
            Equal(true, SequenceHistoryExtensions.PreviousTypeIs([previous, previousSkill], CardType.Skill));
            Equal(false, SequenceHistoryExtensions.PreviousTypeIs([previousSkill, previous], CardType.Skill));
        });
        check("PressForward retains its existing damage and upgrade", () =>
        {
            var card = Card<PressForward>(player);
            Equal(6m, card.DynamicVars.CalculationBase.BaseValue);
            Equal(4m, card.DynamicVars.ExtraDamage.BaseValue);
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
            Equal(9m, card.DynamicVars.CalculationBase.BaseValue);
            Equal(4m, card.DynamicVars.ExtraDamage.BaseValue);
        });
        check("Rhythm grants two block per event and three when upgraded", () =>
        {
            var card = Card<Rhythm>(player);
            Equal(2m, card.DynamicVars["RhythmPower"].BaseValue);
            card.UpgradeInternal();
            card.FinalizeUpgradeInternal();
            Equal(3m, card.DynamicVars["RhythmPower"].BaseValue);
        });

        check("HP loss preserves Hao even when it has positive unblocked value", () =>
        {
            var hao = Power(typeof(HaoPower), owner);
            Field(typeof(PowerModel), "_amount").SetValue(hao, 10);
            var props = ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move;
            var result = new DamageResult(owner, props) { UnblockedDamage = 6 };
            hao.AfterDamageReceived(null!, owner, result, props, owner, skill).GetAwaiter().GetResult();
            Equal(10, hao.Amount);
        });
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
    }
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!;
    private static Creature Creature(CombatSide side)
    {
        var value = (Creature)RuntimeHelpers.GetUninitializedObject(typeof(Creature));
        Field(typeof(Creature), "<Side>k__BackingField").SetValue(value, side);
        Field(typeof(Creature), "_powers").SetValue(value, new List<PowerModel>());
        return value;
    }
    private static Player Player(Creature creature)
    {
        var player = (Player)RuntimeHelpers.GetUninitializedObject(typeof(Player));
        Field(typeof(Player), "<Creature>k__BackingField").SetValue(player, creature);
        Field(typeof(Creature), "<Player>k__BackingField").SetValue(creature, player);
        var state = (PlayerCombatState)RuntimeHelpers.GetUninitializedObject(typeof(PlayerCombatState));
        foreach (var (name, type) in new[] { ("Hand", PileType.Hand), ("PlayPile", PileType.Play), ("DrawPile", PileType.Draw), ("DiscardPile", PileType.Discard), ("ExhaustPile", PileType.Exhaust) })
            Field(typeof(PlayerCombatState), "<" + name + ">k__BackingField").SetValue(state, new CardPile(type));
        Field(typeof(Player), "<PlayerCombatState>k__BackingField").SetValue(player, state);
        return player;
    }
    private static T Card<T>(Player player) where T : CardModel
    {
        var card = (T)ModelDb.GetById<CardModel>(ModelDb.GetId(typeof(T))).ToMutable();
        card.Owner = player;
        return card;
    }
    private static PowerModel Power(Type type, Creature owner)
    {
        var value = (PowerModel)RuntimeHelpers.GetUninitializedObject(type);
        Field(typeof(AbstractModel), "<IsMutable>k__BackingField").SetValue(value, true);
        Field(typeof(PowerModel), "_owner").SetValue(value, owner);
        return value;
    }
    private static CardPlay Play(CardModel card, Player player, int cost, int? energySpent = null) => new()
    {
        Card = card, Player = player, Target = null, ResultPile = PileType.Discard,
        Resources = new ResourceInfo { EnergyValue = cost, EnergySpent = energySpent ?? cost, StarValue = 0, StarsSpent = 0 },
        IsAutoPlay = energySpent == 0, PlayIndex = 0, PlayCount = 1,
    };
}

public class ListenerProxy : DispatchProxy
{
    public IEnumerable<AbstractModel> Listeners { get; set; } = [];
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == "IterateHookListeners") return Listeners;
        throw new NotSupportedException("Unexpected run state call: " + method?.Name);
    }
}

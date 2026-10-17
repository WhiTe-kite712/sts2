# CodeFarmer 遗物设计草案

> 状态：**设计草案**。本文只做设计，不新增或修改任何源码、资源或本地化文件。
> 体系基线：CodeFarmer v0.0.8（88 张卡 / 30 个自定义能力 / 豪意·状态切换·自挂常规负面状态三条主轴）。
> 设计约束：只使用现有体系——现有 `AbstractModel` 钩子、现有原版命令与数值类型、现有 `HaoStateExtensions` / `RegularDebuffs` / `HaoExtensions` 扩展；**不新增自定义能力**。

## 一、设计目标与原则

1. **只补空白，不重复卡牌**：每件遗物只承担一个清晰的角色，避免与已有卡牌（如“节奏”“清华形态”“嘉豪领域”“双面日常”）效果重叠。
2. **咬合三条主轴**：豪意的获得/失去与反噬、开豪↔隐匿的状态切换、对自身施加脆弱/虚弱/易伤。
3. **尊重既有数值口径**：豪意反噬只认“未被格挡的伤害”（`ValueProp.Unblockable` 的生命损失不触发）；常规负面状态严格指脆弱、虚弱、易伤三种。
4. **可静态验证**：触发条件都落在具体钩子上，便于沿用 `tests/LocalizationLinks/check.py` 与 `ModSts2ModAnalyzers` 做键与变量检查。
5. **多人安全**：所有遗物钩子对全体玩家广播，实现时一律先判 `Owner`；只影响持有者本人与其自身状态。

## 二、总览

| # | 名称（中／英） | 类名 | 稀有度 | 主要触发 | 一句话效果 |
| --- | --- | --- | --- | --- | --- |
| 1 | 防御性编程／Defensive Programming | `DefensiveProgramming` | 普通 | `ModifyBlockAdditive` | 隐匿状态时，卡牌给的格挡额外 +1。 |
| 2 | 垃圾回收／Garbage Collection | `GarbageCollection` | 普通 | `AfterCardExhausted` | 每消耗 1 张牌，获得 2 点格挡。 |
| 3 | 崩溃日志／Crash Log | `CrashLog` | 罕见 | `AfterDamageReceived` + `AfterPlayerTurnStart` | 每回合结束被敌人打掉血后，下回合开始额外抽 2 张牌。 |
| 4 | 双系统／Dual Boot | `DualBoot` | 稀有 | 状态切换监听 | 每回合首次切换开豪/隐匿，+3 格挡并抽 1 张牌。 |
| 5 | 技术债／Technical Debt | `TechnicalDebt` | 商店 | `BeforeCombatStart` | 开局给自己 1 层虚弱，获得 4 点豪意和 10 点格挡。 |
| 6 | 祖传代码／Legacy Code | `LegacyCode` | Boss（`Ancient`） | `ModifyMaxEnergy` + `AfterPlayerTurnStart` | 能量上限 +1，每回合给自己 1 层脆弱。 |

- 池：全部注册进 `[Pool(typeof(HaoRelicPool))]`，与 `LuckyCoin`、`SkyHighHao` 一致。
- 基类：全部继承 `MySts2ModRelic`，图标路径按类名小写下划线自动解析。
- `RelicRarity` 枚举实际取值：`None, Starter, Common, Uncommon, Rare, Shop, Event, Ancient`——**没有 `Boss`**；原版 Boss 级遗物（Sozu、Ectoplasm、VelvetChoker 等）使用 `Ancient`。

## 三、逐件详设

### 1. 防御性编程／Defensive Programming（普通）

- 效果：处于隐匿状态时，你每**通过卡牌**获得格挡，就额外获得 1 点格挡。
- `DynamicVars`：`new BlockVar(1, ValueProp.Move)`（描述用 `{Block}`）。
- 钩子：
  ```csharp
  public override decimal ModifyBlockAdditive(
      Creature? target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? cardPlay)
      => target == Owner.Creature && Owner.IsHidden() && cardSource != null ? DynamicVars.Block.IntValue : 0m;
  ```
- 说明：`ModifyBlockAdditive` 返回的是**加算值**。`Owner.IsHidden()` 复用 `HaoStateExtensions`。
- 关键取舍：加 `cardSource != null` 只认卡牌格挡，避免与“上海考生”“节奏”“嘉豪领域”等**每次事件都获得格挡**的效果形成失控叠加；若实测过窄，可放开来源但把数值降回 +1。
- 边界：确认该钩子不会在战斗外的数值预览中生效（需要一个“战斗中”判断）。
- 多人：`target == Owner.Creature` 保证只加自己的格挡。

### 2. 垃圾回收／Garbage Collection（普通）

- 效果：每当你消耗 1 张牌，获得 2 点格挡。
- `DynamicVars`：`new BlockVar(2, ValueProp.Move)`。
- 钩子：
  ```csharp
  public override async Task AfterCardExhausted(
      PlayerChoiceContext choiceContext, CardModel card, bool causedByEthereal)
  {
      if (card.Owner != Owner) return;
      await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, null);
  }
  ```
- 说明：`Consume`/`虚无`（`causedByEthereal`）都算消耗，符合“回收”主题；与“还能更烂”“摆烂到底”“自挂东南枝”“女装”等消耗/虚无牌天然协同。
- 原版先例：JossPaper/CharonsAshes（消耗）、ArtOfWar/CentennialPuzzle（遗物私有字段）、StrikeDummy（ModifyDamageAdditive）、Sozu 系（ModifyMaxEnergy）。
- 边界：整手消耗（自挂东南枝）会产生一次格挡小爆发，属于需要铺垫的组合，可接受。
- 多人：`card.Owner != Owner` 排除队友的消耗。

### 3. 崩溃日志／Crash Log（罕见）

- 效果：若本回合曾受到敌人的未被格挡伤害，在你的下回合开始时额外抽 2 张牌；同一回合多次受伤仍只触发一次。
- DynamicVars：new CardsVar(2)。
- 钩子：
  ~~~csharp
  private bool _tookEnemyDamageThisTurn;
  public override Task BeforeCombatStart()
  {
      _tookEnemyDamageThisTurn = false;
      return Task.CompletedTask;
  }
  public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
  {
      if (player != Owner) return Task.CompletedTask;
      var shouldDraw = _tookEnemyDamageThisTurn;
      _tookEnemyDamageThisTurn = false;
      return shouldDraw ? CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, Owner) : Task.CompletedTask;
  }
  public override Task AfterDamageReceived(PlayerChoiceContext choiceContext, Creature target, DamageResult result, ValueProp props, Creature? dealer, CardModel? cardSource)
  {
      if (target == Owner.Creature && result.UnblockedDamage > 0 && !props.HasFlag(ValueProp.Unblockable) && dealer != null && dealer.Side != Owner.Creature.Side)
          _tookEnemyDamageThisTurn = true;
      return Task.CompletedTask;
  }
  ~~~
- 说明：受伤时只记录本回合标记，不立即抽牌；在持有者下回合开始时统一抽 2 张。只计入敌人造成的未被格挡生命伤害，不计入自伤、豪意反噬或 Unblockable 生命损失。
- 状态字段：回合开始后清除标记，战斗开始时也重置，避免重复触发或跨战斗残留。

### 4. 双系统／Dual Boot（稀有）

- 效果：每回合首次在隐匿状态与开豪状态之间切换时，获得 3 点格挡并抽 1 张牌。
- `DynamicVars`：`new BlockVar(3, ValueProp.Move)`、`new CardsVar(1)`。
- 推荐实现（对现有文件做**追加式**修改）：`HaoStateExtensions.EnterHaoState` 目前只在切换奖励段落通知 `DualLifePower`。可在此处再通知持有者遗物中实现“状态切换监听”的实例；遗物沿用现成的每回合一次工具：
  ```csharp
  if (!HaoStateExtensions.IsRewardingHaoStateSwitch(before, after)) return;
  if (!HaoStateExtensions.TryConsumeHaoStateSwitchReward(
          player.PlayerCombatState!.TurnNumber, ref _lastTriggeredTurn)) return;
  await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, null);
  await CardPileCmd.Draw(choiceContext, DynamicVars.Cards.IntValue, player);
  ```
  - `IsRewardingHaoStateSwitch` 已排除“从无状态第一次进入状态”，与“双面日常”口径一致。
  - `TryConsumeHaoStateSwitchReward` 保证每回合最多一次。
- 备选实现（不改现有文件）：在遗物内实现 `AfterPowerAmountChanged`，用私有字段追踪“上一次状态”以判断是否发生了开豪↔隐匿的互换，再调用同一对工具方法。
- 平衡：状态切换牌（“双面日常”“卷土重来”“临门一脚”等）越多收益越高，但每回合封顶一次，属于组合向稀有。

### 5. 技术债／Technical Debt（商店）

- 效果：战斗开始时，对自己施加 1 层虚弱，获得 4 点豪意值和 10 点格挡。
- `DynamicVars`：`new PowerVar<WeakPower>(1)`、`new IntVar("Hao", 4)`、`new BlockVar(10, ValueProp.Move)`。
- 钩子：
  ```csharp
  public override async Task BeforeCombatStart()
  {
      var ctx = new ThrowingPlayerChoiceContext();   // 与 SkyHighHao 同款
      await PowerCmd.Apply<WeakPower>(ctx, Owner.Creature, DynamicVars.Weak.BaseValue, Owner.Creature, null);
      await Owner.GainHao(ctx, DynamicVars["Hao"].IntValue, null);
      await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.IntValue, ValueProp.Move, null);
  }
  ```
- 说明：
  - `ThrowingPlayerChoiceContext` 已是本模组 `BeforeCombatStart` 的既有用法（`SkyHighHao`）。
  - 自挂虚弱与“急了”“石农是我？”“苦中作乐”的常规负面状态计数/触发口径相容（虚弱属于三种常规负面状态之一）。
- 平衡：商店档的“以小博大”，4 点豪意和 10 点格挡提供开局资源；虚弱惩罚前期输出，给出明确的取舍。
- 本地化：原版 PowerVar 占位符沿用卡牌写法 `{WeakPower:diff()}`（若遗物文本不需要升级差异，可先用 `{WeakPower}`，实现时以分析器为准）。

### 6. 祖传代码／Legacy Code（Boss / Ancient）

- 效果：能量上限 +1。你的每回合开始时，对自己施加 1 层脆弱。
- DynamicVars：new PowerVar<FrailPower>(1)。
- 钩子：
  ~~~csharp
  public override decimal ModifyMaxEnergy(Player player, decimal amount)
      => player == Owner ? amount + 1 : amount;
  public override async Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player)
  {
      if (player != Owner) return;
      await PowerCmd.Apply<FrailPower>(choiceContext, Owner.Creature, DynamicVars.FrailPower.BaseValue, Owner.Creature, null);
  }
  ~~~
- 说明：ModifyMaxEnergy 是原版能量遗物的既有路径。每回合开始施加脆弱，符合“祖传代码拖慢迭代”的主题。
- 稀有度：RelicRarity.Ancient（Boss 级）。

## 四、复用清单（全部为现有体系）

- 状态与豪意：`HaoStateExtensions.GetHaoState/IsHidden/IsOpenHao/IsRewardingHaoStateSwitch/TryConsumeHaoStateSwitchReward/EnterHaoState`；`HaoExtensions.GetHao/GainHao/LoseHao`。
- 常规负面状态：`RegularDebuffs.IsRegular/On/CountKinds/CountLayers`、`VulnerablePower`、`FrailPower`、`WeakPower`。
- 命令：`CreatureCmd.GainBlock(creature, amount, ValueProp.Move, null)`、`CardPileCmd.Draw(choiceContext, count, player)`、`PowerCmd.Apply<T>(choiceContext, target, amount, applier, cardSource)`、`PlayerCmd`/`Owner.GainHao`。
- 复用既有模型：`HaoPower`、`OpenHaoStatePower`、`HiddenStatePower`、`DualLifePower`。
- 钩子（均来自 AbstractModel）：BeforeCombatStart、ModifyMaxEnergy、ModifyBlockAdditive、AfterCardExhausted、AfterDamageReceived、AfterPlayerTurnStart。
- 悬停提示：`HoverTipFactory.FromPower<HaoPower/WeakPower/FrailPower>()`。
- 原版先例：JossPaper/CharonsAshes（消耗）、ArtOfWar/CentennialPuzzle（遗物私有字段）、StrikeDummy（ModifyDamageAdditive）、Sozu 系（ModifyMaxEnergy）。

## 五、实现前必须验证

1. **崩溃日志标记时机**：确认敌方回合受到未被格挡伤害后，标记保留到持有者下回合开始并只抽一次。
2. **格挡加算语义**：确认 ModifyBlockAdditive 返回加算值，并核对战斗外预览和非卡牌来源行为。
3. **遗物实例状态**：私有字段在同一场 run 内持续，并在 BeforeCombatStart/AfterPlayerTurnStart 正确重置。
4. **本地化键派生**：确认类名到 ID 的转换规则（PascalCase → UPPER_SNAKE，前缀 MYSTS2MOD-）；建议实现后跑 python tests/LocalizationLinks/check.py 与 ModSts2ModAnalyzers。
5. **多人广播**：所有钩子对每个玩家的遗物都会调用，务必保留 Owner 判断。
6. **平衡复测**：重点看“防御性编程 + 上海考生/节奏”“技术债 + 急了/石农是我？”两组组合。

## 六、本地化与资源模板（实现时可直接落地）

每件遗物需要：

- zhs/eng 两套 `relics.json` 条目，键为 `MYSTS2MOD-<ID>.title/description/flavor`：
  - DEFENSIVE_PROGRAMMING / GARBAGE_COLLECTION / CRASH_LOG / DUAL_BOOT / TECHNICAL_DEBT / LEGACY_CODE
- 四张图（`MySts2ModRelic` 的自动路径，缺图会回落 `relic.png` 占位，不阻塞构建）：
  - `MySts2Mod/images/relics/<id>.png`
  - `MySts2Mod/images/relics/<id>_outline.png`
  - `MySts2Mod/images/relics/big/<id>.png`
  - `MySts2Mod/images/relics/big/<id>_outline.png`
  - `<id>` 为类名小写下划线，例如 `defensive_programming`、`garbage_collection`、`crash_log`、`technical_debt`。
文本示例（zhs）：

    "MYSTS2MOD-DEFENSIVE_PROGRAMMING.description": "处于隐匿状态时，你每通过卡牌获得格挡，就额外获得[blue]{Block}[/blue]点格挡。",
    "MYSTS2MOD-GARBAGE_COLLECTION.description": "每当你消耗1张牌，获得[blue]{Block}[/blue]点格挡。",
    "MYSTS2MOD-CRASH_LOG.description": "若本回合受到敌人的未被格挡伤害，你的下回合开始时额外抽[blue]{Cards}[/blue]张牌。",
    "MYSTS2MOD-DUAL_BOOT.description": "每回合首次在隐匿状态与开豪状态之间切换时，获得[blue]{Block}[/blue]点格挡并抽[blue]{Cards}[/blue]张牌。",
    "MYSTS2MOD-TECHNICAL_DEBT.description": "战斗开始时，对自己施加[blue]{WeakPower}[/blue]层虚弱，获得[blue]{Hao}[/blue]点豪意值和[blue]{Block}[/blue]点格挡。",
    "MYSTS2MOD-LEGACY_CODE.description": "能量上限+1。你的每回合开始时，对自己施加[blue]{FrailPower}[/blue]层脆弱。"

## 七、明确不在本文范围

- 不实现任何 C# 类、不改 `HaoStateExtensions`、不动本地化 JSON、不新增图片。
- 不调整现有卡牌/能力数值，也不重做“豪意”“状态切换”的既有规则。
- 未做游戏内实测；表中数值是首轮数值，需按第五节复测后再定稿。
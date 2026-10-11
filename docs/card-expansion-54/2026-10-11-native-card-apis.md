# 卡牌能量图标与原版接口统一（2026-10-11）

检查当前60张卡的源码，替换9处 BaseLib `CommonActions` 效果调用，并展开无限豪仅使用一次的私有攻击转发方法。卡牌数值、升级、稀有度、费用和已有触发条件保持。

## 能量描述

参考原版 `EnergyIconsFormatter`、`ADRENALINE` 和 `SUBROUTINE`：

- 动态能量使用 `{Energy:energyIcons()}`，读取 `EnergyVar.PreviewValue`。
- 固定1点能量使用 `{energyPrefix:energyIcons(1)}`。
- 原版对1至3点能量重复显示相应数量的图标；其余数值显示数字加图标。

中英文的心灵怒火、天才？、测不准原理、发言共8条描述已修改；天才能力的3条现有说明同步使用图标。卡池能量前缀继续指向既有小图。测不准原理的设计清单同步当前描述，其余清单记录保持。

## 统一规则

| 效果 | 使用的原版接口 |
| --- | --- |
| 获得格挡 | `CreatureCmd.GainBlock` |
| 攻击伤害 | `DamageCmd.Attack(...).FromCard(...).Execute(...)` |
| 直接伤害、生命损失 | `CreatureCmd.Damage`，保留相应 `ValueProp` |
| 抽牌 | `CardPileCmd.Draw` |
| 获得能量 | `PlayerCmd.GainEnergy` |
| 施加能力 | `PowerCmd.Apply` |
| 随机生成战斗卡 | `CardFactory.GetDistinctForCombat` |
| 新生成的牌加入战斗 | `CardPileCmd.AddGeneratedCardToCombat` |
| 移动已有牌 | `CardPileCmd.Add` |
| 选择卡牌 | `CardSelectCmd` 对应选择入口 |
| 实际升级 | `CardCmd.Upgrade` |

普通格挡传入 `BlockVar`；豪意防御的计算格挡先用 `Calculate(Owner.Creature)` 得到原始公式值，再传入原属性和 `cardPlay`，由原版执行一次修正。预览值不作为结算输入。冷静、调试保留失去豪意后的 `lost` 快照与原触发条件。

后知后觉使用原版 `Hook.ModifyBlock` 预计算下回合格挡，再施加 `BlockNextTurnPower`。这是延迟效果的计算步骤，不能替换为当场获得格挡。

## 修改范围

- 冷静、调试、码上码上码上、自恋、豪意防御：移除 `CommonActions.CardBlock`。
- 背影：移除 `CommonActions.Draw`。
- 冲突：移除 `CommonActions.Apply`，明确读取现有 `Vulnerable` 变量。
- 语法：使用原版卡池候选、解锁/多人限制及 `CombatCardGeneration` 随机源生成技能牌；用原版升级入口最终化升级，然后把新牌放到手牌顶部，最后按当前豪意设置免费期限。
- 网络冲浪：使用原版 `FromSimpleGrid`，保留弃牌候选、原提示和选一配置。保持选择成功后移动牌、再失去豪意的顺序。
- 无限豪：攻击调用直接放回循环，移除单次私有转发方法。

语法改用生成牌入口后，会正确写入生成历史并调用 `AfterCardGeneratedForCombat`。这项生成语义补齐会让原版生成牌相关互动生效；其他牌堆移动不会误记为生成。

豪意读写、状态切换、出牌顺序判定属于模组专用规则，继续使用已有统一扩展。BaseLib 的角色/卡池注册、`Pool` 属性和资源接入仍是项目基础设施。

## 验证范围

源码检查确认60张卡、卡牌目录内剩余 `CommonActions` 调用为0、无重复 `CardBlock` / `GetBlock` / `DealDamage` 转发，修改前后数值声明与升级实现一致。所有相关JSON可解析，键集合不变；仅修改指定能量文本和对应设计清单。原版方法签名、命名空间、生成候选、选择配置和结算属性已对照本机源码复核。

本次没有编译或运行游戏，不把既有DLL和PCK当成本轮验证结果。详细调用清单见同目录 `native-card-api-audit.json`。

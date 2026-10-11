# 起始牌标签与潘多拉魔盒修复（2026-10-11）

根因：原版 CardModel.IsBasicStrikeOrDefend 按 Basic 稀有度与 Strike/Defend 标签判断；潘多拉魔盒再结合 IsRemovable 选择变化对象。豪意打击、豪意防御、自恋此前均满足该条件。名字本身不参与判断。

按用户选择，将三张牌作为独立起始牌处理，移除错误的 Strike/Defend 标签。普通 Strike / Defend 的 CanonicalTags 保留。特殊牌名称、Basic 稀有度、数值、升级和11张初始卡组均保持；无需修改原版遗物或加入特例补丁。

## 同类筛选排查

| 对象 | 原版筛选/行为 | 修复结果 |
| --- | --- | --- |
| 潘多拉魔盒 PandorasBox | IsBasicStrikeOrDefend + IsRemovable；变化所有匹配牌 | 初始卡组只匹配4打击、4防御 |
| 幽灵种子 GhostSeed | Basic + Strike/Defend；施加虚无 | 排除三张特殊起始牌 |
| 巨大扭蛋 LargeCapsule | 卡池中第一张 Basic + 对应家族标签；向牌组添加牌 | 只选择普通打击、防御 |
| 涅奥的护符 NeowsTalisman | Basic 牌中最后一张 Strike/Defend；升级 | 避免原起始牌组顺序导致豪意打击、自恋被错误选择 |
| 树叶药膏 LeafyPoultice | Basic 牌中第一张 Strike/Defend；变化 | 普通牌移除后也不会误选特殊起始牌 |
| 营养汤 NutritiousSoup | Basic + Strike；附魔 | 不再匹配豪意打击 |
| 熔合者事件 Amalgamator | Basic + 对应标签 + IsRemovable；合成 | 排除特殊起始牌 |
| Tezcatara 事件 | 牌组有 Basic + Strike 时提供营养汤 | 特殊起始牌不参与条件 |

原版源码位置为 decompiled-pck/recovered/src/Core/Models 下上述同名文件，以及 CardModel.cs 中的 IsBasicStrikeOrDefend 属性。

移除家族标签同时排除了打击木偶/打击木偶？？？、完美打击、HellraiserPower、Fasten/FastenPower、相关打击/防御附魔和药水对豪意牌的家族识别。这符合用户确认的独立起始牌方案。

EmptyCage、Astrolabe 使用玩家选择的可移除/可变形牌，Whetstone、WarPaint 按攻击/技能类型与可升级性筛选；这些规则并不只作用于基础打击、防御，不需要本次特例排除。

## 验证与生效范围

源码扫描确认整个模组中仅普通 Strike.cs 与 Defend.cs 声明对应家族标签。现有 CardExpansionRegression 新增两项回归源码，覆盖 canonical/可变/升级卡分类、11张初始卡组的潘多拉与直接 Basic+Tags 筛选，以及最后一张家族牌的类型。回归源码待下次编译执行，本次没有编译或实战运行。

重新编译打包、部署并重启游戏后，新获取的遗物按修复后的分类处理。已经被变化的卡牌不会自动还原。
# 豪意动态预览修复（2026-10-09）

普通打击、防御的 `DamageVar` / `BlockVar` 本来就会在 `UpdateCardPreview` 中调用原版伤害 / 格挡 Hook，预览力量、敏捷等修正；不需要额外注册 calculated 变量。此次问题是豪意公式只写在出牌逻辑中，或 JSON 所引用的变量没有注册到 `CanonicalVars`。

## 修改

- 豪意打击：注册 `CalculatedDamage`，基础伤害 6，升级后 9，加当前豪意。基础描述使用 `CalculationBase`，战斗括号使用计算后的总伤害。
- 豪意防御：注册 `CalculatedBlock`，基础格挡为 `min(豪意 × 3, BlockCap)`，上限 11 / 15。上限仍作用于豪意公式基础值，之后按原逻辑接受敏捷、脆弱等修正。
- 调试：预览 `max(豪意 − 10, 0) × 2` 格挡。
- 代码进攻：预览豪意 × 2 伤害；豪意不低于 6 时公式结果翻倍。
- 冷静：预览豪意 × 4 的伤害及格挡，保留升级减 1 费。
- 调试、冷静通过 `HasHaoToLose` 条件在不满足触发条件时明确显示 0；否则基础 0 仍会因力量或敏捷出现误导的非零预览。
- 豪意打击、豪意防御不再在出牌时通过 `BaseValue +=` 改写基础值，消除同一张牌重复使用时累加的问题。
- 调试、冷静仍在失去豪意后使用本次 `lost` 快照执行效果，避免重新读取已减少的豪意；能力返还豪意也不会改变本次转换的倍率。
- 中英描述同步。现有回归快照新增 `descriptionArgs`，记录实际 DLL 注入的额外描述参数。

## 验证

- 模组编译、PCK 打包：0 警告、0 错误，`DeployToGame=false`。
- 现有 58 卡回归 98 项，加专用动态预览 175 项：整合后 **273 / 273 通过**。专用源码保存在 `tests/CardExpansionRegression/HaoPreviewRules.cs`。
- 检查普通打击、防御无 calculated 变量也能正确预览原版属性；五张豪意牌的 0、1、5、6、10、11、20 豪意、升级、反复刷新、力量 / 敏捷只修正一次，以及中英文 `InCombat` 真 / 假分支与不触发时显示 0。
- 真实游戏程序集的 Hook 与本地化 formatter 在控制台 fixture 中运行；此检查没有启动完整游戏战斗。
- PCK 中英卡牌、能力本地化四个资源与源码逐字节一致，JSON 有效。

## 现有全量文本检查的问题

`check_source_names.py` 仍因设计清单中的测不准原理描述与当前中文文本不同而失败；节奏也存在同类差异。这两处中文文本在本次修改前就已不同。

`check_catalog.py` 的中文卡牌变量检查已通过，但在中文能力的普通 `.description` 字段检查处失败。当前下列 10 个能力有 `.smartDescription`，缺少该检查要求的普通 `.description`：`ChuanNextTurnPower`、`Equal680Power`、`FinalCommitPower`、`GeniusPower`、`ShangHaiStudentPower`、`SneerPower`、`TsinghuaFormPower`、`UnrepentantGuardPower`、`UnrepentantPainPower`、`XiaoNextTurnPower`。本次没有修改能力本地化文件。这些全量检查问题不属于本次五张卡的动态预览修复。

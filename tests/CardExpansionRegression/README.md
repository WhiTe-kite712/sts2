# CardExpansionRegression（当前原本 58 卡源码）

迁移时将副本中的卡牌与状态变化同步回原本项目，保留原本 Strike、Defend 和默认 11 张起始牌组。当时未重新构建或执行 DLL，迁移摘要和清单属于 source-only；后续编译结果单独记录在编译修复报告中。

按用户选择，仅保留最新 source-only 摘要、清单和检查源码；旧历史设计、备份与旧编译缓存不迁移，随副本舍弃。当前结果不引用旧版本的运行时验证。

当前共 58 张卡：56 张人物卡、2 张原有无色衍生牌，包含 5 张基础卡及 51 张非基础奖励卡。保留迁入的 18 张扩充牌，后续新增幡然悔悟、卷土重来、时不我待、针锋相对。MySts2ModPower 的具体能力类共 22 个，其中迁入能力 4 个、后续新增能力 2 个。HaoStatStatePower 为抽象基类，不计入具体能力数量。

卡牌类型为 21 张攻击、29 张技能、8 张能力；费用分布为 0 费 10 张、1 费 33 张、2 费 10 张、3 费 4 张、X 费 1 张。幡然悔悟通过既有 LoseHao 入口失去当前全部豪意，保留节奏、清华形态等豪意变动协同；其它能力可能随后返还豪意。2026-10-07三张新卡的效果、升级和手动验收见 `docs/card-expansion-54/2026-10-07-retention-cards.md`。

当前可运行的源码检查（原本项目根目录）：

```powershell
python .\tests\CardExpansionRegression\check_source_names.py
```

源码检查只读取源码、本地化和最新清单。键盘侠、聊天记录、步步紧逼的效果未变检查使用最新目录中的 rename-effect-baselines.json SHA256 基线，不依赖旧历史备份。

编译或运行测试时，默认通过 ProjectReference 先构建当前模组源码，且不安装到游戏目录，无需手动提前生成 DLL。无界面回归使用 58 张卡的期望，并生成当前版本的实际快照：

```powershell
dotnet build .\tests\CardExpansionRegression\Harness.csproj -p:DeployToGame=false
dotnet run --project .\tests\CardExpansionRegression\Harness.csproj --no-build -- 58 .\docs\card-expansion-54\runtime-inventory.json
python .\tests\CardExpansionRegression\check_catalog.py
```

如需专门测试某个已有 DLL，可以显式传入 `-p:ModDll="完整 DLL 路径"`。此模式不会构建模组源码，应自行确认 DLL 版本；路径不存在会给出明确错误。

2026-10-05 编译修复验证（54卡历史版本）：两个 Harness 均为 0 错误、0 警告；该版 DLL 的无界面回归 92/92 通过。2026-10-07 按卡图整理任务的一次性授权重新构建当前58卡DLL：构建及打包0错误、0警告，现有无界面回归98/98通过，中英文本地化与实际DLL校对通过。MegaDot原生纹理加载/解码62/62通过，详见 `docs/card-expansion-54/2026-10-07-card-art-link.md`。Console Harness 仅移除 Godot 与模组专用分析器；模组项目仍执行原有生成器和分析器。

状态测试检查隐匿状态的 -2 力量/+4 敏捷及开豪状态的 +4 力量/-2 敏捷定义，并防止状态继续直接修改伤害或格挡而重复计算。步步紧逼（PressForward）保留上张技能判定与原升级；节奏（Rhythm）检查 2/3 格挡的能力数值。保留豪意不因生命损失而减少的边界检查。

无界面 fixture 仅初始化必要字段，不覆盖完整战斗、动画、网络、全部 OnPlay，或状态 BUFF 的生命周期与实际节奏奖励。状态撤销、原版属性修饰器互动与完整豪意事件仍需实际战斗验证。

2026-10-09 加入 `HaoPreviewRules.cs` 的 175 项动态预览回归，检查普通打击/防御的原版力量/敏捷预览，以及豪意打击、豪意防御、调试、代码进攻、冷静的豪意公式、升级、重复预览不累加、中英 `InCombat` 真实格式化和不触发时显示 0。加上原有 98 项检查，共 273 项；本地化路径由项目元数据解析，不依赖作者桌面路径。快照新增 `descriptionArgs`，记录实际 DLL 的 `AddExtraArgsToDescription` 注入参数。

docs/card-expansion-54 沿用历史目录名，其中 source-inventory.json/source-statistics.json 为当前58卡的源码参考。runtime-inventory.json 和 power-inventory.json 已于2026-10-07实际执行当前58卡/22能力DLL后重新生成，check_catalog.py验证通过。检查器会明确拒绝过期卡牌快照，不会把手填源码记录当作DLL执行结果。catalog-new-cards.json 继续保留迁入的18张扩充套组，不包含后续新增的四张牌。

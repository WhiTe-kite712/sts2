# CardExpansionRegression（当前原本 54 卡源码）

迁移时将副本中的卡牌与状态变化同步回原本项目，保留原本 Strike、Defend 和默认 11 张起始牌组。当时未重新构建或执行 DLL，迁移摘要和清单属于 source-only；后续编译结果单独记录在编译修复报告中。

按用户选择，仅保留最新 source-only 摘要、清单和检查源码；旧历史设计、备份与旧编译缓存不迁移，随副本舍弃。当前结果不引用旧版本的运行时验证。

当前共 54 张卡：52 张人物卡、2 张原有无色衍生牌，包含 5 张基础卡及 47 张非基础奖励卡。保留新增卡 18 张；MySts2ModPower 的具体能力类共 20 个，其中新增能力 4 个。HaoStatStatePower 为抽象基类，不计入具体能力数量。

卡牌类型为 21 张攻击、26 张技能、7 张能力；费用分布为 0 费 10 张、1 费 30 张、2 费 9 张、3 费 4 张、X 费 1 张。

当前可运行的源码检查（原本项目根目录）：

```powershell
python .\tests\CardExpansionRegression\check_source_names.py
```

源码检查只读取源码、本地化和最新清单。键盘侠、聊天记录、步步紧逼的效果未变检查使用最新目录中的 rename-effect-baselines.json SHA256 基线，不依赖旧历史备份。

编译或运行测试时，默认通过 ProjectReference 先构建当前模组源码，且不安装到游戏目录，无需手动提前生成 DLL。无界面回归使用 54 张卡的期望，并生成当前版本的实际快照：

```powershell
dotnet build .\tests\CardExpansionRegression\Harness.csproj -p:DeployToGame=false
dotnet run --project .\tests\CardExpansionRegression\Harness.csproj --no-build -- 54 .\docs\card-expansion-54\runtime-inventory.json
python .\tests\CardExpansionRegression\check_catalog.py
```

如需专门测试某个已有 DLL，可以显式传入 `-p:ModDll="完整 DLL 路径"`。此模式不会构建模组源码，应自行确认 DLL 版本；路径不存在会给出明确错误。

状态测试检查隐匿状态的 -2 力量/+4 敏捷及开豪状态的 +4 力量/-2 敏捷定义，并防止状态继续直接修改伤害或格挡而重复计算。步步紧逼（PressForward）保留上张技能判定与原升级；节奏（Rhythm）检查 2/3 格挡的能力数值。保留豪意不因生命损失而减少的边界检查。

无界面 fixture 仅初始化必要字段，不覆盖完整战斗、动画、网络、全部 OnPlay，或状态 BUFF 的生命周期与实际节奏奖励。状态撤销、原版属性修饰器互动与完整豪意事件仍需实际战斗验证。

docs/card-expansion-54 中的 source-inventory.json 是源码数值参考，不是本次 DLL 运行结果；check_catalog.py 仍要求 runtime-inventory.json 和 power-inventory.json 的实际执行快照，不应拿旧 DLL 快照替代。

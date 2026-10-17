# CardExpansionRegression

当前源码包含88张卡：86张人物卡、2张无色衍生牌；其中攻击26、技能49、能力13，Basic5、Common24、Uncommon32、Rare23、Token2、Ancient2。具体自定义能力30种；临时力量桥接类单独计数。

初始卡组保留11张：4打击、4防御、1豪意打击、1豪意防御、1自恋。状态数值按当前源码：隐匿-2力量/+3敏捷，开豪+2力量/-1敏捷。

从仓库根目录运行源码检查：

```powershell
python tests/CardExpansionRegression/check_source_names.py
python tests/CardExpansionRegression/check_self_debuff_cards.py
```

发布构建使用Godot的ExportRelease配置。以下命令只生成DLL、PCK和清单，不安装到游戏：

```powershell
dotnet build MySts2Mod/MySts2Mod.csproj -c ExportRelease -p:DeployToGame=false
```

无界面回归可以直接使用刚生成的DLL，避免再次打包：

```powershell
dotnet run --project tests/CardExpansionRegression/Harness.csproj -c Release -p:DeployToGame=false -p:ModDll="../../MySts2Mod/.godot/mono/temp/bin/ExportRelease/CodeFarmer.dll" -- 88 docs/card-expansion-54/runtime-inventory.json
python tests/CardExpansionRegression/check_catalog.py
```

ModDll为相对Harness项目目录的路径，也可传入DLL绝对路径。首次构建需要已安装游戏；自动发现失败时为构建/Harness提供`-p:Sts2Path="游戏目录"`。BaseLib依赖从本机NuGet缓存获取。

`source-inventory.json`与`source-statistics.json`是源码清单；`runtime-inventory.json`与`power-inventory.json`只有实际执行Harness后才代表该DLL。检查器会拒绝旧卡数和旧分布；源码清单不能代替运行结果。迁入18张牌与4个能力的catalog保留为固定套组，不代表当前全部卡牌数量。

Harness检查模型注册、克隆、升级、原版打击/防御家族、状态属性、豪意预览和本地化变量；它不覆盖完整战斗、动画、网络和所有卡牌结算。当前实际运行结果以发布验证报告为准。

2026-10-17：新增 UpgradeTextRegression 原生格式化回归。自定义能力仅豪意、隐匿、开豪保留普通描述；其余只校验智能描述。还能更烂保留当前源码4抽3能量调值。

发布PCK可另行只读验证（需要Pillow）：

```powershell
python tests/CardExpansionRegression/verify_release_pck.py --repo . --pck "发布目录/CodeFarmer/CodeFarmer.pck"
```

会检查所有PCK条目MD5、86张卡图的导入链接和逐像素一致性、最新中英本地化及草稿残留。当前结果见[2026-10-17发布验证](../../docs/releases/RELEASE-2026-10-17.md)。

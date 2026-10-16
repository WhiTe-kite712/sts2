# CodeFarmer · 码农

为《杀戮尖塔 2》添加角色「码农」，以「豪意」、状态切换与对自身施加常规负面状态等机制展开。

当前版本 **v0.0.8**。源码包含 **88 种卡牌**（86 种角色卡牌及「孝」「串」2 种无色衍生牌）、**30 种自定义能力**及角色专属遗物。初始卡组为 4 张打击、4 张防御、1 张豪意打击、1 张豪意防御、1 张自恋。

## 安装

1. 安装并启用 **BaseLib 3.4.7 或以上版本**。
2. 将发布包中的 `CodeFarmer` 文件夹放入游戏的 `mods` 目录。
3. 确认目录结构如下，再启动游戏并在模组列表中启用 CodeFarmer。

```text
Slay the Spire 2/
└─ mods/
   └─ CodeFarmer/
      ├─ CodeFarmer.dll
      ├─ CodeFarmer.pck
      └─ CodeFarmer.json
```

BaseLib 需单独安装，发布包不包含游戏或 BaseLib 的程序集。手动更新时先退出游戏，再替换这三个文件；旧版 `MySts2Mod` 安装目录如仍启用，应停用或移出 `mods`，避免重复加载。

## 构建发布包

需要 .NET 9 SDK、游戏本体以及项目声明的 NuGet 依赖。项目会自动查找 Steam 游戏目录；找不到时使用 `-p:Sts2Path="游戏安装目录"` 指定。

在仓库根目录执行以下命令，仅构建和打包，不安装到游戏：

```powershell
dotnet build .\MySts2Mod\MySts2Mod.csproj -c ExportRelease -p:DeployToGame=false
```

产物位于 `MySts2Mod/.godot/mono/temp/bin/ExportRelease/`。只需分发 `CodeFarmer.dll`、`CodeFarmer.pck`、`CodeFarmer.json`；调试符号、测试输出和依赖程序集无需加入发布包。

也可以使用发布脚本，将产物放入显式指定的仓库外目录并生成 ZIP：

```powershell
.\tools\build-release.ps1 -OutputDirectory "C:\ModReleases\CodeFarmer-v0.0.8"
```

脚本保留构建输出和 SHA256 清单，ZIP 仅包含 `CodeFarmer/` 下的三个必需文件。同名发布包已存在时脚本会停止，避免覆盖。

**普通 `dotnet build` 和默认 VS Code 构建任务会自动部署到游戏目录。发布验证时应始终使用 `-p:DeployToGame=false`。**

## 源码与资源

```text
MySts2Mod/
├─ MySts2Mod.csproj        # 项目文件，输出程序集 CodeFarmer
├─ MySts2Mod.json          # 模组清单，安装 ID CodeFarmer
├─ MySts2ModCode/          # 卡牌、能力、遗物和角色代码
└─ MySts2Mod/
   ├─ images/             # 正式卡图、角色图片及图标
   ├─ animations/         # 游戏使用的 Spine 动画
   └─ localization/       # 简体中文及英文文本
tests/                    # 回归检查源码与说明
tools/                    # 发布脚本、美术及动画编辑工具
docs/                     # 设计、变更及验证记录
```

源码目录及 C# 命名空间保留 `MySts2Mod`，卡牌等模型的本地化键保留 `MYSTS2MOD-*`；安装 ID、DLL/PCK 文件名及运行资源前缀统一为 `CodeFarmer`，例如 `res://CodeFarmer/images/card_portraits/Noob.png`。

卡图以 `MySts2ModCode/Cards/` 中的类名命名。动画可编辑源、离线预览及重建说明见 [动画工具](tools/character_animation/hao/README.md)。

## 验证记录

检查工具和使用方法见 [卡牌回归检查](tests/CardExpansionRegression/README.md)、[天才能力回归检查](tests/GeniusPowerRegression/README.md)及[本地化检查](tests/LocalizationLinks/README.md)。历史运行清单和旧日志只对应当时版本，不能作为本次发布的验证结果；部分旧检查仍需跟随当前卡牌清单更新。

本 README 不代表已完成当前版本的游戏内或多人联机验收。发布前应另行确认角色选取、初始卡组、卡图、状态切换、战斗结算和多人所有权行为，并以最新发布验证报告为准。

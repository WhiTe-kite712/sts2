# 杀戮尖塔 2 模组工作区（BaseLib 框架）

模组项目位于 `MySts2Mod\`，基于 Alchyr 官方内容模组模板 + BaseLib 3.4.7。
核心内容：自定义角色 **Coding Farmer（码农）**、核心资源机制 **「豪意」**、围绕豪意的 28 张卡牌与起始遗物。

## 环境现状

| 项目 | 状态 |
|---|---|
| dotnet SDK 9.0 | ✅ 已安装（VS / `dotnet build` 均可构建） |
| 项目模板 | ✅ 已装进 dotnet（`dotnet new alchyrsts2contentmod` 等） |
| 游戏路径 | ✅ `C:\steam\steamapps\common\Slay the Spire 2`（构建时自动识别） |
| BaseLib 运行时 | ✅ 创意工坊订阅 v3.4.7（与 NuGet 版本一致） |
| PckPacker（自动打包 .pck） | ✅ 已启用；**不支持 .tscn**（见"已知限制"） |

## 日常开发流程

1. 用 Visual Studio / VS Code 打开 `MySts2Mod` 文件夹
2. 构建项目（VS 直接 Build；VS Code `Ctrl+Shift+B`；命令行 `dotnet build`）
3. 构建成功后自动把 dll/json/pdb/pck 复制到游戏 mods 目录
4. 启动游戏测试（游戏开着时构建会因文件锁部署失败，关游戏重试即可）

## 项目结构

```
MySts2Mod\
├─ MySts2Mod.csproj            # 项目文件（BaseLib、PckPacker 依赖）
├─ MySts2Mod.json              # 模组清单：id/作者(qqzd)/版本/依赖 BaseLib
├─ _scenes_for_publish\        # 角色场景 tscn（发布时用 Godot 导出，平时不参与打包）
├─ MySts2ModCode\              # ★ C# 代码
│  ├─ MainFile.cs              # 入口 [ModInitializer]，Harmony 挂载 + 脚本查找
│  ├─ Character\
│  │  ├─ CodingFarmer.cs       # 角色：75血/3能量，初始卡组与起始遗物
│  │  ├─ HaoCardPool.cs        # 专属卡池（28张豪意卡都在这里）
│  │  ├─ HaoRelicPool.cs       # 专属遗物池
│  │  └─ HaoPotionPool.cs      # 专属药水池（暂空）
│  ├─ Cards\                   # 29 张卡（28 张 CSV 设计 + 演示卡 Spark）
│  ├─ Powers\                  # 豪意本体 + 12 个联动能力
│  ├─ Relics\                  # LuckyCoin（演示）+ Hao（起始遗物）
│  └─ Extensions\HaoExtensions.cs  # 豪意读写统一入口 GetHao/GainHao/LoseHao
└─ MySts2Mod\                  # ★ 资源（PckPacker 打包进 .pck）
   ├─ images\charui\           # 角色头像占位图
   ├─ images\card_portraits\   # 卡牌图（占位）
   ├─ images\powers\           # 能力图标（占位）
   ├─ images\relics\           # 遗物图标（占位）
   ├─ localization\eng\        # 英文：cards/powers/relics/characters/ancients...
   ├─ localization\zhs\        # 简体中文（同上全套）
   └─ mod_image.png            # 模组列表图标
```

## 游戏内测试

战斗中按 `~` 开控制台：
- `card MYSTS2MOD-HAO_STRIKE`（+ 后缀拿升级版）——所有卡 ID 规律：`MYSTS2MOD-` + 类名大写蛇形
- `power MYSTS2MOD-HAO_POWER 8 0` —— 加 8 层豪意
- `relic MYSTS2MOD-HAO` —— 获得起始遗物
- 单人模式直接选 **码农** 角色体验完整初始卡组

### 天才回归检查

构建模组后，可运行 `tests/GeniusPowerRegression` 中的 15 项自动回归检查；具体命令见该目录的 README。测试直接调用已编译模组和本机游戏 DLL 中的真实钩子，不需要启动游戏。

1. 打出天才但未失去豪意时，敌方攻击和自己的攻击都应保持正常伤害（例如 6 点仍为 6 点）。
2. 失去豪意后，反复查看敌人意图或卡牌伤害，下一次敌方伤害仍应翻倍（例如 6 点变为 12 点）。
3. 攻击实际结算后，后续攻击恢复正常；多段攻击只翻倍第一段。正数伤害即使全部被格挡，也会消耗这次效果。
4. 0 点伤害、自伤和无伤害来源的效果不消耗标记。再次失去豪意后可以重新触发。
5. 多人模式下，仅拥有天才的玩家自己失去豪意会触发；队友的豪意变化不应影响该玩家。

存读档说明（已核对游戏 v0.111.0）：普通战斗中退出后读档会从战斗检查点重开，并不保存当前能力、豪意等战斗状态。因此不能把重开后天才标记重置当作单独的能力序列化缺陷。

## 已知限制 / 待办

- **约 14 张卡无升级效果**（考试/平静/冲突/调试/代码攻势/讥笑/平等680/小组讨论/死不悔改/语法课/豪华的豪/超豪力场/清华之姿/比行者更强/最终提交），升级设计待定
- **全部卡图/能力图标/遗物图标为占位图**——把图放进 `MySts2Mod\images\` 对应目录（文件名 = 小写蛇形类名，如 `hao_strike.png`），重新构建自动打包
- **角色战斗形象/能量表盘/选择背景回落原版**——`.tscn` 需要 Godot(MegaDot 4.5.1) 导出，场景已备好放在 `_scenes_for_publish\`，发布时配置 `Directory.Build.props` 的 `GodotPath` 后 `dotnet publish`
- **遗物 Hao 效果为占位设计**（每回合+2豪意），可调整
- **语法课** v1 简化：随机 1 张而非"3 选 1"（自定义选卡界面 API 未验证）

stdbitmap补充：1.'嘴硬'的实际效果与描述不符合。
- 正式发布创意工坊：官方工具 <https://github.com/megacrit/sts2-mod-uploader>

## 参考

- BaseLib 源码：<https://github.com/Alchyr/BaseLib-StS2>
- 模板与 Wiki：<https://github.com/Alchyr/ModTemplate-StS2/wiki>
- **中文教程库（强烈推荐）**：<https://github.com/GlitchedReme/SlayTheSpire2ModdingTutorials>
  （BaseLib 系列 16 篇：卡牌/遗物/能力/药水/怪物/事件/人物/先古对话/上传工坊等；
  Basics 系列含"变量与描述"——所有本地化占位变量和 formatter 的速查表）


目前还在测试

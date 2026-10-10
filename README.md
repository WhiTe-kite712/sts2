# 杀戮尖塔 2 模组工作区（BaseLib 框架）

模组项目位于 `MySts2Mod\`，基于 Alchyr 官方内容模组模板 + BaseLib 3.4.7。
核心内容：自定义角色 **Coding Farmer（码农）**、核心资源机制 **「豪意」**、58种卡牌与起始遗物。

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
│  │  ├─ HaoCardPool.cs        # 专属卡池（56种角色卡，含5种基础牌）
│  │  ├─ HaoRelicPool.cs       # 专属遗物池
│  │  └─ HaoPotionPool.cs      # 专属药水池（暂空）
│  ├─ Cards\                   # 58种卡：56种角色卡 + 孝/串2种无色衍生牌
│  ├─ Powers\                  # 22种能力 + 临时力量桥接
│  ├─ Relics\                  # LuckyCoin（演示）+ Hao（起始遗物）
│  └─ Extensions\HaoExtensions.cs  # 豪意读写统一入口 GetHao/GainHao/LoseHao
└─ MySts2Mod\                  # ★ 资源（PckPacker 打包进 .pck）
   ├─ images\charui\           # 顶栏头像、选角头像及人物背景
   ├─ images\card_portraits\   # 卡牌图按类名命名，普通优先、缺失回退big
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

### 名称、效果文本与悬浮提示关联

已为 28 张涉及状态或豪意的卡牌、10 个关联能力及起始遗物配置 `ExtraHoverTips`。卡牌能显示对应 Power 的名称和通用效果说明；实际施加后的层数仍由 Power 的 `smartDescription` 显示。火花不涉及状态，不增加豪意提示。

中文 Power 名称已同步到卡牌使用的现名（上海考生、等效680、嘴硬、天才？、“佳”豪、嘉豪领域、清华形态），并补齐嘴硬、佳豪后续阶段的说明。佳豪的中文卡牌描述已明确计数按“获得豪意的次数”，首次计数从 1 开始；天才已补充完全格挡也会消耗翻倍效果的说明。

可执行 `python tests/LocalizationLinks/check.py` 检查中英文键、模型 ID、变量与悬浮说明关联，具体条件和游戏内验收步骤见 `tests/LocalizationLinks/README.md`。该检查不代替构建、PCK 加载和游戏内 UI 验证。

## 已知限制 / 待办

- **约 14 张卡无升级效果**（考试/平静/冲突/调试/代码攻势/讥笑/平等680/小组讨论/死不悔改/语法课/豪华的豪/超豪力场/清华之姿/比行者更强/最终提交），升级设计待定
- **卡图仍有缺口**——2026-10-07已有35种卡图链接，21种卡无对应图；打击/防御复用原版。卡图文件名以Cards源码文件为准（如HaoStrike.png），能力/遗物图标继续使用对应ID的小写蛇形名称。现有普通新美术优先于旧big图；普通缺失时使用同名big。
- **角色战斗形象/能量表盘仍回落原版**——对应草案位于 `_scenes_for_publish\`，后续可配置Godot导出。选角头像和人物背景已于2026-10-08接入；背景由C#生成PackedScene并注册游戏缓存，PckPacker只需打包PNG，无需把TSCN放进资源目录。
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

## 当前卡牌版本

副本的卡牌更改已同步到本仓库；新增幡然悔悟、卷土重来、时不我待、针锋相对后，当前58种卡（攻击21、技能29、能力8）。初始卡组仍为11张：4打击、4防御、1豪意打击、1豪意防御、1自恋。迁移摘要与当前清单位于 [docs/card-expansion-54](docs/card-expansion-54/同步完成摘要.md)，检查见 [tests/CardExpansionRegression](tests/CardExpansionRegression/README.md)。目录沿用历史名称；源码清单与实际运行快照均为58卡。三张新卡的效果和升级见 [新增卡说明](docs/card-expansion-54/2026-10-07-retention-cards.md)。2026-10-07按本次授权已完成卡图改名、链接、编译打包及资源调试：0错误/0警告，基础回归98/98，纹理解码62/62；详见 [卡图验证报告](docs/card-expansion-54/2026-10-07-card-art-link.md)。未部署到游戏目录。

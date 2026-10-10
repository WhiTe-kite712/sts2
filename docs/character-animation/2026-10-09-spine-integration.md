# 角色 Spine 接入

抬手峰值的小臂额外上抬由30°增至40°，两臂夹角比上一版增加10°。实际 Spine 局部夹角为45.8867°；前臂整体抬升95.1765°，大臂抬升55.1765°。

战斗资源入口：`res://MySts2Mod/animations/coding_farmer/hao.tres`。角色覆盖 `CreateCustomVisuals` 与 `SetupCustomAnimationStates`，使用运行时 `SpineSprite`，攻击映射 attack，能力牌/施法映射 cast，结束回 idle_loop。攻击效果延迟0.4秒，施法效果延迟0.8秒。

作者数据、分层图片、绑定和预览保存在 `tools/character_animation/hao`，不是游戏资源目录。`deploy.py` 复制数据到模组资源并添加中立姿势的待机、受击、死亡兼容片段；这三种动作尚未单独制作美术。保留原版 CustomVisualPath 预加载，实际战斗图由自定义工厂返回；其他界面回落资源保持原配置。

使用运行时工厂是因为项目的 PckPacker 不支持 tscn，直接添加此类文件会跳过资源打包。spjson、spatlas、tres及PNG可以正常随原有构建打包。

验证：

- 项目编译、打包成功，0错误。2条NU1900警告来自无法联网读取NuGet漏洞索引。
- 实际生成的 MySts2Mod.pck 已由游戏的原生 Spine 扩展加载：资源路径、CTEX、22根骨骼、披风网格和五个动画播放通过。
- 七个唯一节点的类型、Owner、UniqueName检查通过，使用的是原生节点镜像，未冒称运行了C#工厂。
- 小臂抬幅大于大臂、肘在肩右侧、手与剑尖向下、剑尖向前的当前动作检查通过。没有删除质量检查。
- 实际C#工厂的隔离探针编译通过，但导出游戏宿主未进入托管探针入口；尚未完成实战运行验证。

原生结果详见本目录的 `built-pack-result.json`；隔离工厂尝试见 `factory-host-result.json`。完整复现源码保存在作者目录的 `validation/integration`。

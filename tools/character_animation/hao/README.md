# 码农 Spine 动画

2026-10-17：火堆独立角色场景已接入，复用当前角色贴图与骨架。三幕循环overgrowth_loop、hive_loop、glory_loop采用轻微呼吸站姿并隐藏武器；本次未制作独立坐姿。战斗与商店保持各自实例。

攻击保持100 FPS时间栅格、0.70秒总时长：第20帧举剑至头顶，手肘高于肩49.54骨架单位；第24帧复制最高姿态，保持0.04秒，再于第26–32帧大幅劈下。收剑仅保留第40、54、70帧三个旋转关键帧。命中事件与CodingFarmer.AttackAnimDelay均为0.32秒；攻击入场混合0.04秒、返回0.08秒。能力动作0.84秒、释放0.48秒保持不变。

最高点大臂世界转角较初始增加120.53°，小臂增加180.53°，两者同向且小臂幅度更大。每条攻击旋转时间线由71键减少为40键；预览按同一稀疏时间线重采样，预览缩放1.05以完整显示剑尖。原骨长、握柄和身体绑定保留，避免重绑导致畸形。

贴图参考原版Silent/战士的明暗块面，使用内置imagegen简化密集褶皱、发丝和碎高光，再按原图集规范导出。2048×2048尺寸、20个区域、原透明通道、22根骨骼、披风网格与UV均保留；fx_aura/fx_slash未改。生成说明见texture-simplification-prompt.md。

## 编辑与重建

bind.json保存关节与分割边界，motion-keyframes.json保存动作。Spine4.2可导入editable/hao.json，图片目录editable/parts。

```powershell
python .\editable\rebuild.py --keep-textures
python .\editable\build_viewer.py
python .\deploy.py
```

务必使用--keep-textures以保留已简化的parts和图集；省略该参数会从历史character-reference重新裁切原画。此次同时修复了保留贴图时GIF预览仍读取旧reference的问题。运行时部署只复制Spine数据、atlas、tres和正式图集，authoring/审核资料不会进入PCK。

preview/index.html是唯一离线HTML入口，支持时间轴和骨骼；当前动画GIF为attack-100fps.gif、cast-100fps.gif。viewer.template.txt为生成源码，应保留。idle/hurt/die仍为兼容中立片段。

修改前备份保存在桌面Spire2Mod/Backups：
- Spine-before-campfire-20261017-182143.zip
- Spine-before-overhead-20261017-184013.zip

旧动画副本、原生探针和回归日志已清理。validation目录仅保留说明；重建时会重新生成bind-check.json，它不参与游戏加载。

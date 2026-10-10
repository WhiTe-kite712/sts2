# Coding Farmer Spine 动画

角色战斗视觉已接入 `MySts2ModCode/Character/CodingFarmerCombatVisuals.cs`。
运行资源在 `MySts2Mod/MySts2Mod/animations/coding_farmer`，资源入口为 `res://MySts2Mod/animations/coding_farmer/hao.tres`。

帧率由 motion-keyframes.json 的 fps 控制，当前为100 FPS。攻击 `attack` 为0.700秒，能力牌 `cast` 为0.840秒。游戏的 Attack、Cast、PowerUp 触发已映射，动作结束回到待机；效果等待时间分别为0.340与0.480秒。
待机、受击、死亡使用中立姿势兼容片段，尚未另做这些动作的美术。

抬手峰值的小臂相对大臂角度在上一版基础上增加10度（额外上抬由30改为40度），大臂额外上抬仍为20度。绑定、等比缩放、握柄锚点保留。

## 后续编辑

`editable/bind.json` 保存图片关节与分割边界；`editable/motion-keyframes.json` 保存动作坐标与抬臂幅度。
`editable/hao.json` 可用 Spine 4.2 的 Import Data 导入，图片目录为 `editable/parts`。
`preview/index.html` 是离线预览。

```powershell
python .\editable\rebuild.py --keep-textures
python .\editable\build_viewer.py
python .\deploy.py
```

最后一步将源数据复制到模组资源目录，并添加兼容片段；普通 VS Code 构建会将 `.spjson`、`.spatlas`、`.tres` 和图集打包进 PCK。
当前打包器不支持 `.tscn`，所以节点由角色工厂运行时创建。

`validation/history` 是接入前的历史检查，哈希只证明对应历史版本。实际接入验证见仓库 `docs` 中的报告。
素材来源与提示词保存在 `generation-prompt.md` 和 `reused-textures-prompt.md`。
100 FPS 版本将原40 FPS版的帧坐标翻倍，在每两个原关键姿态之间插入一个局部骨骼插值帧（`keyframeSubdivisions=2`）。攻击骨骼时间线由36个键增至71个，能力牌由43个增至85个，整体时长缩短20%。原来的姿态、绑定、图片和手臂运动方向保持。

攻击下挥 wristY 阶段现在为第16→34帧，攻击结束于第70帧；能力牌结束于第84帧。特效和事件时间使用配置中的帧号除以fps。事件只移动时间，不增加触发次数。`--keep-textures` 保留现有图片和图集，当前预览为 `attack-100fps.gif`、`cast-100fps.gif` 与 `preview/index.html`。

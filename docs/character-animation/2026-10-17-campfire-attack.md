# 当前码农动画

当前使用Spine4.2骨骼，时间栅格100 FPS。攻击0.70秒，第20帧举剑到头顶，第24帧保持同姿，停顿0.04秒；第32帧命中。收剑使用第40、54、70帧。能力牌动作0.84秒，第48帧释放。

火堆使用码农呼吸站姿并隐藏武器，三幕循环名称为overgrowth_loop、hive_loop、glory_loop。战斗、商店和火堆分别创建实例。

编辑和重建步骤见[动画说明](../../tools/character_animation/hao/README.md)。[离线预览](../../tools/character_animation/hao/preview/index.html)与[攻击GIF](../../tools/character_animation/hao/preview/attack-100fps.gif)保留。

贴图简化的完整提示词见[生成说明](../../tools/character_animation/hao/texture-simplification-prompt.md)。旧动画副本、临时探针和回归检查日志已清理。

修改前备份保存在桌面Spire2Mod/Backups中的Spine-before-campfire-20261017-182143.zip和Spine-before-overhead-20261017-184013.zip。

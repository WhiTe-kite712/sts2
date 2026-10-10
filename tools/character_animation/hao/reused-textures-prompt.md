# 素材生成记录

模式：内置 `imagegen`，透明背景；没有使用 CLI/API 备用模式。
参考角色卡图：`HaoStrike.png` 和 `CodeOffense.png`。

最终素材制作规格（整理版，供复现）：

```text
Create a production-quality transparent 2D skeletal puppet texture sheet using the supplied card art as character references.
Preserve the young blond amber-eyed swordsman, dark hood and lower-face mask, fitted black/navy armor with gold details, cobalt blue cape, pale lightning/W chest motif, and silver-blue longsword.
Match the detailed painterly fantasy game artwork of the references. Character faces right in a three-quarter battle view.
Strict 4-column by 5-row layout. Each cell contains exactly one isolated part. Consistent character scale, clean transparent margins, rounded overlapping joints, no baked backdrop, no shadows, no checkerboard, no words or labels.
Limbs are separated at shoulder, elbow, wrist, hip and knee, aligned downward in their neutral artwork orientation.
Row 1: hooded head; torso; pelvis/waist; upper cape shoulder section.
Row 2: far upper arm; far forearm; far open hand; near upper arm.
Row 3: near forearm; near closed hand with grip stub; alternate near open hand; complete sword pointing up.
Row 4: far thigh; far shin and boot; near thigh; near shin and boot.
Row 5: middle cape section; cape tail section; isolated blue sword-slash effect; isolated blue-and-gold casting aura.
Keep enough hidden overlap at the joints for smooth bone rotation. Do not assemble a full-body character in the sheet.
```

生成结果是 1254 × 1254 RGBA；本包 `source/hao.png` 保存原始透明图集。脚本按网格拆分并绑定部件，不把整张角色图当作一个动画平面。

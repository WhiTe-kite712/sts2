# 素材记录

模式：内置 imagegen，透明背景。参考：HaoStrike.png、CodeOffense.png。完整人物图保存于 source/character-reference.png；武器与两个特效复用前次生成素材，其规格保存在 reused-textures-prompt.md。

完整人物图的最终提示词：

```text
Create ONE coherent anatomically correct full-body 2D game character on a truly transparent background. Use the supplied card art only as identity and costume references: a young adult blond amber-eyed male swordsman with a dark lower-face mask, black/navy hood, black fitted combat clothes, cobalt blue cape with restrained gold edging, pale double lightning/W emblem on the chest. Match the confident anime painterly fantasy style of the references, with clean readable forms for game animation. The foremost priority is normal human anatomy and connected proportions: one head, one torso, exactly two arms, exactly two hands, exactly two legs and exactly two boots. Use approximately 6.5 heads tall, natural shoulder width, normal-size hands, matched thigh/shin proportions. Single character, full head-to-toe, three-quarter view facing screen right, both boots planted at shoulder width, no perspective foreshortening. Animation-friendly neutral relaxed A pose: both upper arms slightly out from the body, elbows gently bent, forearms angled downward, hands clearly separate from torso and legs; nearer screen-right hand is a closed gripping fist but holds no object, farther screen-left hand is slightly open. Nothing crosses the face or torso. Cape is ONE continuous simple cloth silhouette hanging behind him to the calves; no floating ribbons, no disconnected scraps, no flying shattered feathers. Both legs and both arm silhouettes must be visible and easy to isolate. Moderate armor/clothing details, simpler than busy card art. Keep the head and boots fully inside a tall portrait composition with generous empty transparent padding. No sword or other prop, no aura or particles, no ground shadow, no base, no background, no text, no extra poses, no grid, no exploded parts. This must look like a finished believable complete human character in the neutral pose, ready to cut into layers after anatomical inspection.
```

随后仅按同一图拆分、绑定与做动画，未再生成其他角色或肢体图。


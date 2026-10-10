# 卡图命名与资源链接验证（2026-10-07）

已以 Cards/*.cs 的文件名为准完成图片命名和链接，本次使用用户的一次性编译调试授权。

- 改名44个文件：普通图15个，大图29个。
- 保留62个PNG全部内容，改名前后SHA256一致；公共card.png及big/card.png保留原名称。
- 清理Git索引的旧大小写/蛇形别名，索引没有大小写重名；只记录涉及改名的图片，非图片暂存状态未变。
- 普通图作为当前美术优先使用，普通不存在时读取同名big图。两套图片均保留，旧big图不会覆盖当前普通新插画。
- Equal680、Exam、Grammar、Narcissism的普通图保持用户已删除状态，通过对应big图显示。
- 35种卡有本模组图片，打击/防御复用原版；21种卡没有对应图片，仍使用共享占位。

| 验证 | 结果 |
| --- | --- |
| 当前模组与CardExpansionRegression构建、PCK打包 | 成功，0错误、0警告 |
| 58卡/22能力及现有无界面回归 | 98/98通过 |
| MegaDot原生Texture2D加载及图像解码 | 62/62通过，尺寸与原PNG一致 |
| 中英文目录和实际DLL变量 | 通过 |
| PCK内两语cards/powers与源文件 | 4/4字节一致 |
| 源码名称与清单检查 | 通过 |

引擎调试使用 SlayTheSpire2.exe 的 MegaDot 4.5.1.m.14，通过隔离小PCK启动无界面SceneTree探针，挂载新模组PCK后调用原生ResourceLoader，未注册替代图像加载器。没有进入游戏场景、创建存档或部署到游戏mods目录。

隔离探针日志包含缺少全局脚本缓存及证书存储访问的环境提示；所有纹理加载和解码均成功，探针退出码0。这次验证覆盖资源加载及现有基础回归，未验证完整战斗或实际卡牌UI排版。

## 改名表

| 原相对路径 | 新相对路径 |
| --- | --- |
| better_than_walker.png | BetterThanWalker.png |
| big/better_than_walker.png | big/BetterThanWalker.png |
| big/calm.png | big/Calm.png |
| big/code_code_code.png | big/CodeCodeCode.png |
| big/code_offense.png | big/CodeOffense.png |
| big/conflict.png | big/Conflict.png |
| big/debugging.png | big/Debugging.png |
| big/equal680.png | big/Equal680.png |
| big/exam.png | big/Exam.png |
| big/figure_shadow.png | big/FigureShadow.png |
| big/final_commit.png | big/FinalCommit.png |
| big/fire_in_soul.png | big/FireInSoul.png |
| big/genius.png | big/Genius.png |
| big/grammar.png | big/Grammar.png |
| big/group_discuss.png | big/GroupDiscuss.png |
| big/hao_defend.png | big/HaoDefend.png |
| big/hao_strike.png | big/HaoStrike.png |
| big/infinite_hao.png | big/InfiniteHao.png |
| big/kick_up.png | big/KickUp.png |
| big/magnificent_hao.png | big/MagnificentHao.png |
| big/math_prince.png | big/MathPrince.png |
| big/narcissism.png | big/Narcissism.png |
| big/shang_hai_student.png | big/ShangHaiStudent.png |
| big/sneer.png | big/Sneer.png |
| big/spark.png | big/Spark.png |
| big/student_leader.png | big/StudentLeader.png |
| big/super_hao_field.png | big/SuperHaoField.png |
| big/surfing.png | big/Surfing.png |
| big/tsinghua_form.png | big/TsinghuaForm.png |
| big/unrepentant.png | big/Unrepentant.png |
| debugging.png | Debugging.png |
| final_commit.png | FinalCommit.png |
| fire_in_soul.png | FireInSoul.png |
| group_discuss.png | GroupDiscuss.png |
| kick_up.png | KickUp.png |
| magnificent_hao.png | MagnificentHao.png |
| math_prince.png | MathPrince.png |
| shang_hai_student.png | ShangHaiStudent.png |
| spark.png | Spark.png |
| Student_leader.png | StudentLeader.png |
| super_hao_field.png | SuperHaoField.png |
| surfing.png | Surfing.png |
| Tsinghua_form.png | TsinghuaForm.png |
| unrepentant.png | Unrepentant.png |

## 仍缺少卡图的21张

| 卡牌 | 应补普通图文件 |
| --- | --- |
| 卷土重来 | Comeback.png |
| 编译警告 | CompilerWarning.png |
| 赶DDL | DeadlineSprint.png |
| 抑郁 | Depression.png |
| 推导？ | Derivation.png |
| 双面日常 | DualLife.png |
| 墙头草 | FenceSitter.png |
| 骗你的 | FooledYou.png |
| 封“神”榜 | GodList.png |
| 后知后觉 | Hindsight.png |
| 键盘侠 | KeyboardWarrior.png |
| 新群聊 | NewGroupChat.png |
| 规划 | Planning.png |
| 本色出演 | PlayingMyself.png |
| 步步紧逼 | PressForward.png |
| 量子力学 | QuantumMechanics.png |
| 节奏 | Rhythm.png |
| 石农 | ShiNong.png |
| 时不我待 | TimeWaitsForNoOne.png |
| 针锋相对 | TitForTat.png |
| 测不准原理 | UncertaintyPrinciple.png |

完整资源映射见同目录 card-art-map.json。

改名前原图备份、改名计划、构建/回归/引擎加载日志位于 `C:\Users\Amazi\Documents\Codex\2026-09-30\github-slay-the-spire-2-mod\work\card-art-link-2026-10-07`。

本次授权仅用于这一轮编译和调试；后续修改继续沿用先前的源码处理方式。

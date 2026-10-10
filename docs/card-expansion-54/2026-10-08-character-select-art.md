# 选角美术接线（2026-10-08）

| 原文件 | 新位置 | 用途 |
| --- | --- | --- |
| images/_264_390.png | images/charui/coding_farmer_select_icon.png | 选角头像及锁定状态头像 |
| images/_2560_1200.png | images/charui/coding_farmer_select_bg.png | 角色选择界面人物特写背景 |

两次移动及改名均逐个核对SHA256，图像内容没有修改。实际PNG尺寸分别为1032×1524、1832×859，文件名中的目标尺寸不是实际像素；界面按比例适配。顶栏仍使用原有方形头像coding_farmer_icon.png。

CodingFarmer的选角头像属性指向新竖图，背景属性指向CodingFarmerSelectScene创建的虚拟PackedScene。背景画布采用原版2560×1200布局，TextureRect保持比例覆盖，节点忽略鼠标事件。

背景通过PreloadManager.Cache.SetAsset与Resource.TakeOverPath注册；原版角色选择和随机选择流程仍负责实例化。只对这一条生成场景路径保留缓存，避免进入战斗卸载后返回主菜单时尝试从磁盘读取不存在的TSCN。已有其他资源的缓存清理继续执行。

资源目录仍只新增两个PNG，不包含TSCN，因此不会触发现有PckPacker的“不支持场景、跳过打包”分支。_scenes_for_publish中的背景草案也已同步到新图片及布局，可供后续编辑参考；运行时使用C#场景工厂。

验证：文件移动前后哈希一致，源码清单检查通过；对照本机游戏和Godot接口完成静态复核。按先前的一次性编译限制，本轮没有编译、打包、启动游戏或实机验证。下次正常构建后，新头像和背景才会进入实际DLL/PCK。

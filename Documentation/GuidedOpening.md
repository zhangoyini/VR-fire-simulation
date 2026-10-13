# 正常校园生活 → 火灾 → 两步毛巾练习

项目：`F:\unityproject\VR-fire-simulation-my`；主场景：`Assets/Scenes/Campus/CampusTraining.unity`。
本轮在现有四层楼上新增引导章节，不替换门窗、电梯、厕所、水龙头、毛巾、自然烟雾和整体逃生终点。

## 已实现流程

1. 确认进入游戏以前，可以自由移动、拿放两本练习书、推开后窗。头显 3D 教学停留 30 秒，两页手柄/双手教学各停留 40 秒；A 可在阅读后跳过。**没有蓝色确认按钮，扳机只用于物体交互。**
2. 准备页至少阅读 8 秒后，**按住右手柄 A 键 0.75 秒进入游戏**；无头显预览用 Enter。不会自动开始剧情，也不会把抓书当成确认。
3. 开场约 22 秒，演示就位、读书、翻页、放书、手回桌面、虚拟坐下和休息。实际 CharacterController 被禁用，虚拟身体位置由系统独占，每帧保持指定姿态；摇杆和物体操作不能改变动画。就位转向和坐下在短暂淡出中完成，不修改传感器的头部姿态；自然转头和真实头部追踪始终保留。
4. 警报与气味阶段约 14 秒，随后等待用户阅读起身说明并按 A 虚拟起身。起身动作结束立即恢复行走、物体操作及左右真实手柄模型，不等待额外任务确认。真实玩家不必实际坐下或起身。
5. 将教室门推到至少 85°（接近完全打开），并进入可见门口烟雾后，播放血量与消防知识教学：血量演示页约 24 秒，知识页约 32 秒，自动继续；阅读 20 秒后可按 A 跳过。血量演示约 9 秒后暂停伤害，保留烟雾 6 秒宽限与 2 HP/s 游戏数值；握持湿毛巾遮住口鼻时为 0.8 HP/s（40%），而不是免伤。
6. 寻找毛巾和浸湿撤离任务都先展开约 22 秒，随后自动缩成**正前方**短任务卡。任务发布不锁定移动、不会强制掉落毛巾，也无需碰按钮确认；地面路线使用与普通绿色 EXIT 不同的青蓝色发光箭头，任务 1/2 期间 411 门牌显示高亮边框及前景文字。未完成的任务持续显示，完成后展开下一项。
7. 进入 411 卫生间且实际持有四楼毛巾才进入任务 2；真实流水中保持 1.5 秒可浸湿。遮住口鼻并持续握持后，引导沿楼梯到三楼。慢速松手掉落，快速挥动并松开扳机可投掷；书本也可投掷，能蹲下重新捡起；拿起/放下保留震动。完成本章不跳过全楼逃生终点。
8. 地图、任务、血条和个人提示均围绕视野正前方分层排列，不再固定在左下/右上角。游戏 HUD 约 1.6 米，警告保留 1 米，触碰操作的暂停/结束菜单约 0.7 米并缩放到可读、可触及尺寸；仍为 VR 世界空间前景 UI。阅读卡展开时暂时隐藏地图，避免重叠。
9. 练习/开场期间不能提前触发集合点或避难区胜利，正式自由活动时重新启用终点。原门窗、电梯、卫生间、水龙头、毛巾、自然烟雾、物理楼梯和整体成功/失败逻辑均保留。

## 追踪与拾取修正

优先请求 XR 的真实地面追踪（Floor），头部和手柄共享未缩放的追踪原点，输入位置只转换一次。地面模式下**不再把所有人的视点强行抬到 1.70 米**，否则真实手柄触地时，虚拟手仍被整体抬高。保留真实身高、下蹲及转头；1.70 米仅为无设备预览/无地面模式的设备原点后备值。

左右手新增 OpenXR/SteamVR 兼容的真实姿态桥接，支持手柄输入及震动；没有虚构的手臂伸长或距离倍率。交互检测使用可见手腕位置；书本/毛巾拾取容差为 20 cm，门窗/按钮仍为 12 cm，不恢复远距离射线。动画结束时显式恢复详细手套渲染、模型活动状态与真实追踪；丢失真实控制器时不以预览手冒充追踪。

实现参考 Unity 的 [Floor/Device 原点定义](https://docs.unity.com/en-us/engine/7000.0/script-reference/unityengine/xr/trackingoriginmodeflags)、[原点设置接口](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/xr/xrinputsubsystem/trysettrackingoriginmode)和 [XR 控制器输入说明](https://docs.unity3d.com/cn/current/Manual/xr_input.html)。实际使用的接口已在项目的 Unity 2022.3 中编译、运行检查。

## 烟火、窗户与安全边界

阅览室内部火焰延续至门口，使用动画火焰贴图、不同火芯/外焰、尺寸生命周期及扰动；烟雾使用烟序列贴图、柔和粒子、顶部堆积/沉降、扰动和碰撞。两侧遵循同一参数，只由路径距离造成到达时间差；水平到达参数由 0.75 提升为 1.6，楼层间传播间隔由 50 秒改为 18 秒，开场警报时预置已发展一段时间的火情。教学阅读期间仍暂停进一步蔓延，以保留观察时间。已有流程检查验证从卫生间离开时，4F→3F 楼梯及三楼入口有真实可见烟雾；后续继续按 3F→2F→1F 渐进发展，不瞬间整楼灌满。

A/B 两侧八扇楼梯窗修正了可动窗扇的 Static 标记，使用运动学 Rigidbody，去掉残留固定中梃，将把手降低到每层约 1.4 m。左右手分别通过真实接触选中、扳机握持和 30 cm 向外推，均可打开 90°；第二次握持后释放仍显示双语禁止跳窗警告并在 2 秒后自动关闭。修改同步到 `Assets/Prefabs/Campus/StairWindow.prefab`。

放书动画烘焙手套时，完整保留左手负 X 镜像、法线和三角形绕序，另调整手掌及休息姿势朝向。没有改变真实手柄的 1:1 追踪朝向。

毛巾仍是教学情境内的操作任务，但不再免疫烟雾：湿、握持、贴近口鼻三个条件同时成立才减缓游戏伤害，放下/远离立即恢复正常扣血。已移除破坏叙事的“虚拟练习”等长篇游戏内提示，保留简短“仍受烟气伤害、不能过滤一氧化碳”的提醒。

数值是游戏平衡，不代表真实毛巾能把火灾伤害降低 60%，也不意味着能防止窒息或中毒。消防科普指出湿毛巾不能过滤一氧化碳；真实火灾不能把取毛巾当成撤离的先决条件。[国家消防救援局相关科普](https://www.119.gov.cn/kp/zjts/2022/180.shtml) 烟气可致窒息和中毒，低姿撤离；烟火封路时关闭房门并报告位置、等待救援。[应急管理部消防科普](https://www.mem.gov.cn/kp/shaq/201806/t20180615_366012.shtml)

正式用于学生教学前，应由消防专业人员审核路线和文案。烟雾传播、血量均是游戏近似，不是 CFD 或火灾预测模型。

## 代码与维护

新增代码全部在 `Assets/Scripts/Campus`：

- `CampusLessonDirector.cs`：完整状态流程、确认输入、移动/交互/伤害门控、剧情和任务完成条件。
- `CampusLessonAnimation.cs`、`CampusTeachingHand.cs`：圆润三维头显/Touch 风格手柄与纹理双手动画，按实际绑定演示移动、左摇杆按下暂停、双扳机、投掷、右 A 确认等。A/B/X/Y 显示在对应手柄上，未绑定键不虚构功能。
- `CampusLessonLayout.cs`：教学页和任务卡布局；`CampusLessonRoute.cs`：地面箭头、方向/距离；`CampusDestinationHighlight.cs`：阶段性目标门牌高亮。
- `CampusTrackingHeight.cs`：地面原点与设备原点后备；`CampusPracticeBook.cs`：练习书本拿取、独占、掉落与复位。
- `CampusControllerInput.cs`、`CampusTrackedHeadPose.cs`、`CampusTrackedHandPose.cs`：真实头部/手柄输入、A 确认、暂停及震动桥接。
- `CampusHandVisual.cs`、`CampusLocomotion.cs`：动画手/真实手切换与独占开场移动锁定。
- `CampusThrowMotion.cs`：松开前约 120 ms 的物理手部位置/旋转采样；排除摇杆身体位移，丢失追踪/重定位不投掷，系统/暂停释放安全掉落，限制线速度及角速度。书本、毛巾使用 Rigidbody 真正飞行，不设置固定投掷距离。
- `Shaders/CampusTeachingProp.shader`：兼容 VR 双眼路径的教学示意物前景材质，仍需 Quest 实机验收。
- `Editor/CampusLessonUpgrade.cs`：新增教学对象、开场桌椅、书本/手套资产与绑定，不重建楼体。
- `Editor/CampusLessonChecks.cs`、`Editor/CampusLessonPreview.cs`、`Tests/CampusLessonDriver.cs`：完整章节回归与实际画面采集。

原 `CampusLocomotion`、`CampusHandInteractor`、`CampusSmokeExposure`、`CampusSmokeSpread` 接入章节门控；`CampusExperience` 不再自行提前播放警报，练习计时从起身解锁后开始。旧毛巾提示仅在没有本章节任务引导时显示。

本次环境/教学更新菜单：`Tools → Campus → Refine teaching, fire and physical interaction`，对应 `Editor/CampusRefinementUpgrade.cs`，不会重建其他房间。原教学更新菜单仍为 `Tools → Campus → Apply Guided Classroom Opening (keeps building)`。重复应用仅替换 `07 Guided Classroom Lesson` 和它自己的淡出画布，并移除旧确认按钮，复用身高追踪原点，不重建其他房间；请先保存手工修改。重建校园/应用环境更新会重新绑定此章节；单独更新洗手台会重新绑定目标毛巾。

本轮修改前备份：`Assets/Scenes/Legacy/CampusTraining_v10_PreRefinement_Backup.unity`。v7/v8/v9 更早的备份仍保留。备份仅包含场景，不包含共享代码的回退。

不接头显时，可在 Unity Play 模式使用 **Enter** 逐步确认、**Backspace** 跳过操作教学、WASD 预览移动。手持推门/毛巾仍使用既有 VR 手柄交互；自动检查会模拟这些输入，不代表已经实机验证。

## 验证文件

最终通过 **108 项章节检查、361 项原交互回归**，缺失脚本为 0。检查覆盖身高、双手偏移、练习书本/窗户、A 键确认、阅读节奏、动作环节、伤害暂停、任务展开/正前方常驻、持续拿取及三楼完成条件。另独立回归原门窗、电梯、毛巾/流水、物理楼梯和整栋楼逃生终点；具体结果见以下文件。截图验证包括展开中间帧和常驻任务卡，并检测文字溢出。

- `Documentation/LessonChecks.txt`：本章节检查结果；`Documentation/RevisionChecks.txt`：独立的原交互回归。
- `Logs/campus-refinement-update.log`：本轮保存和静态验证。
- `Logs/campus-refinement-checks.log`、`Logs/campus-refinement-regression.log`：最终章节及原功能检查。
- `Documentation/Previews/Lesson_01_Headset.png` 至 `Lesson_23_StairGlassOpen.png`：实际 Unity 渲染（含正前方任务卡、展开中间帧、开场后的双手和三种菜单）。

单次截图使用测试控制的动画时刻；完整场景中的动画正常按时间播放。未连接 Quest 3，真实震感、追踪、双眼阅读体验及性能需另行验证。

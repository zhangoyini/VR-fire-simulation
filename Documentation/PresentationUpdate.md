# UI 防遮挡、自然烟雾与环境细节

项目：`F:\unityproject\VR-fire-simulation-my`；主场景：`Assets/Scenes/Campus/CampusTraining.unity`。
本说明优先于旧版关于实验室侧加速烟雾、杂物移至侧边，以及提示没有防遮挡的描述。后续卫生间的干毛巾、流水浸湿与持续遮住口鼻免扣血机制见 [RestroomInteractions.md](RestroomInteractions.md)，取代主场景原一次性毛巾增益。

## 本次更新

- 玩家 UI 使用世界空间前景材质：血条、小地图、主菜单、暂停/失败/成功菜单、动态健康提示和门窗/电梯双语警告不再被墙体深度遮挡。文字保留字体图集和遮罩；没有改成屏幕空间菜单，没有锁定头部。警告仍距离眼前 1 m，近墙菜单不再被挤到眼前。房号、EXIT 等场景实体标牌仍正常被墙遮挡。
- 烟雾取消 414 实验室侧的特殊速度、密度与烟团尺寸。两侧参数一致，只按与起火房门的实际路径距离到达；房门开度影响逸出速度。粒子沿顶棚累积、缓慢下沉，并按房间→走廊→楼梯→三/二/一楼逐步出现，保留图集动画、扰动、随机颜色/尺寸、柔边和碰撞。暂停/结算冻结蔓延；暴露区随浓度启用，原扣血与毛巾游戏数值保留。
- 三楼 A 楼梯倒柜、纸箱、木板及椅子恢复原位置和原尺度，不再移到侧边。只去掉“封闭/CLOSED”标识，不增加隐形整宽堵墙。原杂物实体碰撞仍在，所以这处障碍仍需玩家观察并选择路线；B 楼梯保留完整下行路径。
- 新增各层走廊防护条、候坐长椅、饮水设施、回收箱、时钟和设备细节；教室笔记本电脑、文具；实验室样品；阅览室书本和台灯；卫生间烘手机、纸巾机和清洁用品；室外自行车、校车、步道灯和绿化。补充装饰避开门口及主通道，不添加新的强制任务。
- 按后续要求移除四层走廊的 16 组灭火器（含标签、把手和软管装饰），保留报警按钮及其他设施；环境生成代码也不再放置灭火器，重新应用更新时不会恢复。移除前场景备份为 `Assets/Scenes/Legacy/CampusTraining_v5_PreExtinguisherRemoval_Backup.unity`。
- 仅左摇杆移动、右摇杆无操作、自然转头、真实手套姿态、双手扳机比例推门窗、高亮/震动、窗户二次警告与自动关闭、近距离电梯警告及已有其他任务均保留。

烟雾是实时训练用的分阶段视觉近似，不是流体计算，也不能预测真实火灾烟气向下传播或真实逃生安全性。Quest 3 双眼显示、追踪、震动和性能尚未实机验证。

## 代码与重新应用

- `Assets/Scripts/Campus/CampusUIForeground.cs`、`CampusUIForegroundGraphic.cs`、`Shaders/CampusForegroundUI.shader`：前景 UI；TMP 字体使用项目自带的 SDF Overlay 着色器。
- `Assets/Scripts/Campus/CampusMessagePresenter.cs`：动态双语警告，仍保持 1 m。
- `Assets/Scripts/SystemUI/WorldSpaceUIAnchor.cs`：前景菜单不因近墙而缩短距离，旧场景仍保留原放置逻辑。
- `Assets/Scripts/Campus/CampusSmokeSpread.cs`、`Editor/CampusEvacuationUpgrade.cs`：自然路径参数、渐增浓度和烟层下沉。
- `Assets/Scripts/Campus/Editor/CampusPresentationUpgrade.cs`：环境补充与场景/健康提示预制体配置。Unity 菜单 `Tools → Campus → Apply Natural Smoke and Foreground UI (keeps scene layout)` 可再次应用，不重建整栋楼；同名生成细节组会替换，手工添加的其他场景组不动。
- `Assets/Scripts/Campus/Tests/CampusPresentationChecks.cs`：无头显回归；`Editor/CampusUIOcclusionPreview.cs`：近墙四方向实际 GPU 渲染检查；`Editor/CampusPreview.cs`：场景/烟雾/双手预览。

布局备份：`Assets/Scenes/Legacy/CampusTraining_v4_PrePresentation_Backup.unity`。仅备份更新前场景，不能单独回退共享脚本和材质；完整回退应使用版本控制。

## 验证记录

Unity 编译和 237 项无头显自动回归通过（含走廊灭火器移除检查）；场景缺失脚本为 0。原门窗比例推动、楼梯窗警告/自动关闭、电梯警告、烟雾伤害、暂停及结算均在检查范围内。

最新自动检查结果见 `Documentation/RevisionChecks.txt`，实际渲染在 `Documentation/Previews/`。本轮主日志：`Logs/campus-presentation-final-save.log`、`Logs/campus-presentation-verification.log` 与 `Logs/campus-presentation-preview.log`。没有接入头显，自动检查不等于 Quest 实机验收。

走廊灭火器移除及后续检查日志：`Logs/campus-remove-extinguishers.log`、`Logs/campus-remove-extinguishers-checks.log`。之前的烟雾/环境预览图可能仍包含移除前的灭火器，以已保存主场景为准。

Unity 实际渲染已通过近墙四方向检查（0°/90°/180°/270°），分别检查血条、小地图和中英警告；另检查真实主菜单及暂停/失败/成功面板。预览：`UIWall_Combined_270.png`、`UIWall_Pause.png`、`UIWall_MainMenu.png`、`Exterior.png`、`ReadingRoomFire_4F.png`、`Stair_A_OriginalDebris.png`。3F/2F/1F 烟雾按 120/170/220 秒画面检查粒子出现；这是充分开门的预览时序，不是固定的实际火灾传播时间。

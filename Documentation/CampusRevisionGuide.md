# 教学楼第二版（2026-10-11）

当前 UI 防遮挡、自然烟雾、原位杂物和环境细节以 [PresentationUpdate.md](PresentationUpdate.md) 为准。以下第二版与后续旧版说明保留作历史记录。

最新控制、双侧出口、三维双手、楼梯窗户和跨楼层烟雾见 [EvacuationUpdate.md](EvacuationUpdate.md)。下文第二版的单侧疏散和封闭楼梯设置已被替换。

后续的舒适移动、30 cm 门窗推拉与渐进烟雾更新见 [ComfortUpdate.md](ComfortUpdate.md)。下文的 1.35 m/s、连续转向和固定烟雾区描述为第二版初始实现，当前参数以更新说明为准。

本次修改的项目是 `F:\unityproject\VR-fire-simulation-my`，不是同级旧项目 `VR-fire-simulation`。

## 打开

主场景仍为 `Assets/Scenes/Campus/CampusTraining.unity`。上一版布局保存为 `Assets/Scenes/Legacy/CampusTraining_v1_Backup.unity`，不加入 Build Settings；这是场景布局备份，不是旧版脚本/输入配置的完整快照，完整回退应使用版本控制。主菜单、重试和结算仍使用同一个主场景。

## 场景

- 出口牌仅保留 EXIT 和几何箭头，沿走廊统一指向东侧 B 楼梯；房号牌放在对应门框上方。三层 A 楼梯堆放倒落柜体、纸箱、断裂木板和椅子，并有实体阻挡。
- 外墙按实际网格尺寸连续对齐，补齐楼层边梁和主楼/楼梯接缝。
- 电梯为走廊侧的封闭不锈钢门和呼叫按钮，不进入独立房间，不在火灾练习中运行。新预制体为 `Assets/Prefabs/Campus/CorridorElevator.prefab`；旧电梯预制体保留。
- 阅览室重新布置十个书架、独立阅读桌椅，桌面不含洗手池。
- 四层阅览室为主要起火点：4×4 火焰图集与 8×8 烟雾图集动画、多层火焰、上升烟、顶棚烟层和闪烁火光。烟雾效果是实时粒子，并非真实流体/烟气仿真。
- 每层西侧卫生间有三个封闭隔间、马桶、洗手设备、镜面、皂液器和湿毛巾。毛巾保留原拾取接口，便于继续扩展。

## 新控制方式

左摇杆任意方向连续行走，最高 1.35 m/s，斜向不加速。右摇杆水平转向，65°/s，围绕头部位置转动身体；按用户确认保留头显自然转头。CharacterController 提供墙体碰撞、重力和 0.24 m 的踏步跨越。没有移动弧线、瞬移或世界/菜单射线。

双手都使用 GrabPinch 扳机，接近实体表面约 0.18 m 后物体高亮。可以先按住扳机再把手移至门窗表面；按钮则需要接触后按下。握住扳机时，手绕铰链的位移控制门窗角度；按下本身不会自动打开。门可从两侧推拉，松手保持角度；窗户沿右铰链向外打开，限制 90°。同一个铰链同时只由一只手控制。握持开始和角度变化会发送短震动。

窗户打开后再次握放，保留双语禁止跳窗警告、2 秒等待和关窗；电梯按钮近距离握放保留双语禁止乘梯警告。警告仍为距头部 1 m 的世界空间文字，没有屏幕空间或防遮挡效果。

暂停、重试、主菜单及结算按钮使用手部接触扳机；暂停和结算状态禁止走动与门窗操作。左摇杆按下仍为暂停。Oculus Touch 的 Walk/Turn/Haptic 绑定及生成代码已更新，实际 Quest/SteamVR 接线还须实机确认。

脚步声随实际水平位移和着地状态播放，停止或顶住墙体不会持续播放。当前使用三段可替换的合成硬质地面脚步声，位于 `Assets/Audio/Campus`。

## HUD 和血量

视野上方是实时血条与 HP 数值；左下是按当前位置更新的楼层平面小地图，黄色标记和白色指向表示玩家位置/朝向。一层地图包含室外集合点；A 楼梯在三、四层标为受阻。

清洁空气中不再按时间自动扣血。头部进入烟雾体积后，累计 3 秒开始按 4 HP/s 持续扣血；离开立即停止并重置暴露计时。暂停/结算时不扣血。原毛巾的回血和短时倍率机制仍是游戏数值，不表示湿毛巾能够防护真实有毒烟气。

## 代码

所有新增代码仍在 `Assets/Scripts/Campus`：

| 文件 | 功能 |
| --- | --- |
| CampusLocomotion | 连续行走、摇杆转向、重力、脚步 |
| CampusHandInteractor / CampusContactHighlight | 双手近接、握持、高亮、震动 |
| CampusHingeInteractable | 手部位移驱动门窗铰链 |
| CampusElevatorButton / CampusMessagePresenter | 电梯按钮与双语警告 |
| CampusMinimap | 玩家位置、朝向、楼层和室外地图 |
| CampusSmokeExposure / CampusSmokeVolume | 烟雾体积、宽限期和持续伤害 |
| CampusFireFlicker | 火光变化 |
| Editor/CampusSceneRevision | 第二版建筑、卫生间、阅览室及控制器整合 |
| Editor/CampusRevisionChecks | 不接头显的自动回归 |

同时修改了原 `DummyController`（保留手部视觉、关闭旧输入逻辑）、`RuntimeFireWindow`（保留分割玻璃、增加手驱动模式）、`HealthTimeController`（伤害接口）及预制体支持组件。

## 无头显检查

Scene 视图可右键 + WASD 浏览。编辑器 Play 模式在没有 SteamVR 设备时可用 WASD 预览行走、左右方向键转向；手部推拉仍需控制器或自动测试，不会变成鼠标点门。

第二版初始的 50 项自动检查已扩展为本轮 74 项，新增分段转向、行走方向保持、周边暗角、恒定距离门窗推拉和渐进烟雾检查，原真实楼梯上下行、墙体碰撞、双语警告、烟雾伤害、暂停和结算检查继续通过。最新结果见 `RevisionChecks.txt`，旧 `PlayModeChecks.txt` 仅是第一版记录。预览图在 `Documentation/Previews`。自动测试不会保存禁用设备行为后的临时场景。

真实手部可达性、追踪抖动、振动强度、转向舒适度、Quest 帧率和烟雾可读性尚未完成实机测试。可以先使用编辑器查看，再戴头显复测。

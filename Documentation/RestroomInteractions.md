# 卫生间：水龙头与手持毛巾

项目：`F:\unityproject\VR-fire-simulation-my`。主场景：`Assets/Scenes/Campus/CampusTraining.unity`。
四层卫生间均已更新，每层两个独立水龙头、一条初始干毛巾。其他场景功能不重建。

后续新增的入场教学与四楼两步毛巾任务见 [GuidedOpening.md](GuidedOpening.md)。任务期间用新的双语任务卡代替重复的拿取/浸湿通知，实体毛巾交互不变。

## 使用方式

1. 任一只手靠近毛巾时高亮；按住该手扳机拿起，拿起时震动。已按住扳机再接触毛巾也可拿起。同一条毛巾不能同时被两只手抢占。
2. 空闲手靠近水龙头上方的蓝色标记开关并按一次扳机，开启流水；松开后再按一次关闭。持续按住不会反复切换。也可先打开水，再用同一只手拿毛巾。
3. 保持拿着毛巾，让毛巾与流水连续接触约 **1.5 秒**，干毛巾变成颜色较深的湿毛巾，并显示中英文提示。关水不会立即变干。
4. 继续按住扳机，将湿毛巾放在口鼻前，HUD 出现“湿毛巾遮挡 / Wet towel in position”。只有仍被持有、且实际靠近口鼻时，才免除烟雾扣血。干毛巾、远离脸的湿毛巾、掉落毛巾均无保护效果。
5. 任意时刻松开扳机，原手柄震动，毛巾受重力掉落；可用任一只手重新拿起。掉落后保留干湿状态，但保护立即结束，不回血、不消失、不产生限时或永久免伤增益。

口鼻判断使用头部前下方的参考点及毛巾表面，允许约 **12 厘米**误差，排除头后方和伸到远处的毛巾。烟雾持续暴露计时不会因遮挡而重置，因此在已经长时间处于烟雾时移开毛巾，会立即恢复扣血。暂停/结算不能继续浸湿或切换水龙头，持有状态安全释放。

**上述免扣血是按需求实现的游戏规则，不能理解为真实火灾防护能力。** 游戏中的毛巾采用有皱褶的轻量实体网格及刚体碰撞，不是完整布料流体模拟。

## 预制体与代码

- `Assets/Prefabs/Campus/RestroomWashStation.prefab`：整个洗手台，包括实际凹槽洗手池、两个水龙头和开关、流水粒子/水声、干毛巾。拖入现有项目场景即可复用；保留原双手接触交互器与共享血量/UI 系统。
- `Assets/Prefabs/Campus/CarryTowel.prefab`：可单独放置的干毛巾，带干湿材质、碰撞体和刚体。
- `Assets/Scripts/Campus/CampusFaucet.cs`：开关、流水可见性、水声与实际流水接触范围。
- `Assets/Scripts/Campus/CampusTowelInteractable.cs`：持续握持、单手占用、浸湿、口鼻判断及松手掉落。
- `Assets/Scripts/Campus/CampusHandInteractor.cs`：双手近距离高亮、扳机状态与拾取/释放震动路由。
- `Assets/Scripts/Campus/CampusSmokeExposure.cs`：仅在持有湿毛巾并遮住口鼻时停止扣血。
- `Assets/Scripts/Campus/CampusExperience.cs`：双语湿毛巾状态。
- `Assets/Scripts/Campus/Editor/CampusRestroomUpgrade.cs`：生成预制体并替换四层原洗手台/一次性毛巾；菜单 `Tools → Campus → Apply Restroom Water and Held Towel (keeps other rooms)`。再次应用会重置这组洗手台和毛巾的场景放置，请先保存手工修改。
- `Assets/Scripts/Campus/Tests/CampusTowelWaterChecks.cs`、`Editor/CampusRestroomPreview.cs`：无头显逻辑回归和实际画面检查。

其他旧场景仍保留原 `TowelPickup` / `PickableTowel` 脚本；本主场景卫生间的四条毛巾不再使用它们。之后重新应用校园环境更新也会生成新的洗手台交互。

更新前场景备份：`Assets/Scenes/Legacy/CampusTraining_v6_PreWaterTowel_Backup.unity`。该备份只保存场景，不能单独回退共享代码/材质，完整回退需版本控制。

## 验证

最终 **320 项**无头显自动回归通过，包含双手独立拿取、水开关、连续浸湿、干湿保护条件、松手掉落/再拿起、暂停、八个洗手池的凹槽/台面四角碰撞检查，以及原有交互。场景缺失脚本为 0。结果：`Documentation/RevisionChecks.txt`；最终渲染与回归日志：`Logs/campus-restroom-final-checks.log`；场景保存日志：`Logs/campus-restroom-update.log`。

实际 GPU 画面已检查：`Documentation/Previews/RestroomFixtures_4F.png`、`Restroom_DryTowel.png`、`Restroom_RunningWater.png`、`Restroom_WetTowel.png`、`WetTowel_GameplayHUD.png`。台面填补椭圆洗手池周围的空隙，盆内可放入毛巾；缩短贴脸毛巾的高度以减少前方视野遮挡，双语保护状态没有文本溢出。预览姿势不会保存回主场景。

尚未连接 Quest 3：自动检查验证了震动请求路由，不能验证实际手柄震感、双眼显示、真实追踪姿态或设备性能。

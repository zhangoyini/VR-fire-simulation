# 左手移动、双侧疏散与四层烟雾更新

后续更新见 [PresentationUpdate.md](PresentationUpdate.md)：UI 已加入世界空间前景防遮挡；烟雾不再偏向实验室侧；杂物恢复原位和原尺度，仅删除封闭牌。下文的侧边堆放、两侧均无障碍和东侧加速参数是上一轮记录，不再代表当前场景。

项目：`F:\unityproject\VR-fire-simulation-my`。主场景：`Assets/Scenes/Campus/CampusTraining.unity`。此文优先于前几版说明中的右摇杆转向、A 楼梯封闭及仅四楼烟雾描述。

当前状态：增量更新已保存；Unity 编译和 150 项无头显回归检查通过，包括两侧楼梯从四楼完整下行到一楼、楼梯窗户警告/自动关闭、手指实际蒙皮变形及烟雾分层顺序；缺失脚本为 0。Unity 图形预览确认实验室侧烟更浓、三/二/一楼逐层出现粒子、手掌/握拳画面不同，舒适暗角仍保留中央视野。预览结果在 `Documentation/Previews/`，主要日志为 `Logs/campus-evacuation-preview.log`、`Logs/campus-hands-preview.log` 和 `Logs/campus-evacuation-verification.log`。

## 本轮操作与场景

- 只读取左手摇杆四向移动，最高 1.8 m/s；右摇杆没有移动、转向或菜单操作绑定。头显自然转头保留。开始推动左摇杆时采用当时的水平观察方向；行走中转头不会突然改变路径，回中后再推动会采用新的方向。松杆立即停止，脚步声和行走舒适暗角保留。编辑器可用 WASD 预览，方向键不再转向。
- 删除一至四楼的独立 STAIR A/B 楼层牌和杂物处 CLOSED 牌。三楼倒柜、纸箱、木板和椅子保留，缩放并移到 A 楼梯半层平台侧边；删除旧人工封路碰撞箱及“返回走廊改走 B”的触发提示，两侧楼梯都可下行。走廊 EXIT 几何箭头和地面箭头按位置就近指向 A 或 B，中间等距位置选 A；一楼楼梯间则指向中央大厅出口。
- 双手改为项目原有 SteamVR 三维手套资产，使用 URP 纹理与法线材质、31 骨骼蒙皮。扳机驱动握拳、推门窗时放松手掌、按钮时伸指的平滑姿态。没有启用光学手追踪，也不放大或偏移真实手柄推动距离。
- A/B 楼梯每层窗户（共 8 扇）都可接触高亮、左右手扳机握持并按推动距离向外开 0–90°。15 cm 推动约 45°、30 cm 约 90°；再次握放已打开的窗户会显示原中英双语“不要跳窗”警告，等待 2 秒后以 0.7 秒动画关闭。整窗预制体：`Assets/Prefabs/Campus/StairWindow.prefab`，可拖入场景。原 78 扇外墙窗户、门、电梯按钮及双手震动逻辑保留。

## 烟雾

四楼阅览室先积烟，8 秒后从门口逐渐逸出；关闭房门仍缓慢泄漏，开启后更快。烟雾采用 8×8 动画图集、低频扰动、颜色/大小随机变化、渐增发射量、粒子物理碰撞、深度柔边和渐隐。

四楼走廊朝 414 实验室（东侧）推进速度设为约 1.5 m/s，西侧卫生间方向约 0.42 m/s；东侧发射率约 13/s、西侧 7/s，东侧烟团也更大。这是视觉引导，不是把东侧楼梯设成不可用。

62 个受限粒子阶段沿门口、走廊、两座楼梯的上下梯段和各层走廊顺序启用。房门充分打开时，三、二、一楼东端走廊分别约第 74、112、150 秒开始出现烟团，浓度增加后才启用暴露区（约第 82、120、158 秒）；门关闭时更晚。西侧楼梯蔓延较慢。具体时间会随门开度变化，时间按游戏时间计算；暂停与结算冻结传播。暴露区仅在相应烟团形成后启用，保留 3 秒宽限、4 HP/s、游戏毛巾倍率和健康/小地图 UI。

这是为教学叙事设计的分阶段视觉近似，不是烟气流体计算或真实火灾疏散安全预测。不能据此推断真实烟气向下传播规律或毛巾防护能力。阶段烟雾池上限约 2,000 粒子，另有起火点粒子；Quest 3 双眼渲染和帧率仍需实机验证。

## 代码、应用与回退

- `Assets/Scripts/Campus/CampusLocomotion.cs`：仅左手移动；`CampusHandVisual.cs`：三维手指姿态；`CampusStairWindow.cs`：楼梯整窗配置。
- `Assets/Scripts/Campus/CampusHingeInteractable.cs` / `CampusHandInteractor.cs`：共用实体接触、比例推拉、单手占有、高亮、警告及震动，原逻辑不丢失。
- `Assets/Scripts/Campus/CampusSmokeSpread.cs`：渐进烟雾时钟、粒子浓度、暴露阶段。
- `Assets/Scripts/Campus/Editor/CampusEvacuationUpgrade.cs`：场景增量更新，不重建整个教学楼；Tools → Campus → Apply Evacuation Update 可重新应用。原 Comfort 更新和完整重建也会调用本轮配置。
- `Assets/Scripts/Campus/Tests/`：仅编辑器编译的回归测试；`Documentation/RevisionChecks.txt`：检查结果；`Documentation/Previews/`：Unity 实际渲染预览。
- `Assets/StreamingAssets/SteamVR/bindings_oculus_touch.json`：移除 default 动作集的右摇杆绑定及旧左右方向瞬移/转向绑定；保留左摇杆 Walk 和左摇杆按下暂停。

更新前布局在 `Assets/Scenes/Legacy/CampusTraining_v3_PreEvacuation_Backup.unity`。它只备份场景，不能单独还原共用脚本、材质和输入配置；完整回退应使用版本控制。

未接入头显：追踪对齐、双眼舒适度、扳机震动和 Quest 性能没有实机验证。

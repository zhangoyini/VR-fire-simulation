# 四层教学楼 · 小组协作说明

最新入场教学、第一视角课间动画与两步毛巾任务见 [GuidedOpening.md](GuidedOpening.md)。旧版流程不代表当前入场行为。

> 本文记录第一版。2026-10-11 的连续行走、双手推拉、厕所、火烟和小地图版本，请以 [CampusRevisionGuide.md](CampusRevisionGuide.md) 为准；第一版场景已单独备份。

## 打开与运行

主训练场景：`Assets/Scenes/Campus/CampusTraining.unity`。主菜单的 Start 和 Restart 已连接此场景，Build Settings 只启用 MainMenu 与 CampusTraining。Unity 版本为 2022.3.62f3。

玩家从四层 401 教室出生。每层层高 3.8 m，总计 32 个教学/服务空间：教室、讨论空间、科学实验室、阅览室、电梯厅和二层避难区。两座折返楼梯直接连通四层，地面大堂通向室外集合点；不再加载另一个楼层/集合点场景。

沿走廊去 A 或 B 楼梯。A 楼梯的三层下行支路设置了训练用阻挡，应返回走廊改走 B。B 楼梯可连续下至一层。普通摇杆瞄准移动保留，楼梯每级及平台均有 TeleportArea；落点须可站立，单次高度差最多 2.2 m，不能直接跨层或从窗口跳至地面。可以在二层避难区使用对讲机确认等待救援，或继续去室外集合点停留 2 秒完成练习。

输入和 XR 配置沿用项目原有 SteamVR/GrabPinch/Teleport/Pause 绑定，没有替换为另一套 SDK。本次没有进行 Quest 3 实机、Link/Air Link 或 Android APK 验证；硬件输入与帧率仍需组员戴头显复测。

## 已集成的交互

| 功能 | 操作与行为 | 脚本目录 |
| --- | --- | --- |
| 窗户 | 靠近玻璃（随宽度计算，1.5–2 m）；右扳机瞄准高亮，首次松开右侧铰链向外开 90°；再次瞄准原窗口模型并松开，显示中英文警告，2 秒后开始关闭 | Scripts/Interaction/RuntimeFireWindow、VRFireSafetyInteraction |
| 电梯 | 离 up/down 按钮 1.5 m 内，右扳机瞄准高亮，松开显示中英文禁用电梯警告；不真的运行电梯 | Scripts/Interaction/FireSafetyElevator |
| 警告提示 | 原有世界空间提示，位于头显前方 1 m，默认 5 秒；没有增加屏幕空间或穿透物体的提示效果 | Scripts/Interaction/VRFireSafetyInteraction |
| 门 | 单门和大堂双门均可近距离右扳机射线操作；保留手柄接触交互和离开后的自动关门 | Scripts/Interaction/DoorInteraction、DoorProximityTrigger |
| 毛巾 | 安全柜蓝色毛巾可射线或接触领取，回复 20、30 秒内扣时倍率 0.5；四层另有可抓持、移动和投掷的白色毛巾 | Scripts/Health/TowelPickup、Scripts/Legacy/PickableTowel |
| 烟雾/时间 | 三、四层西侧烟雾区使时间流失倍率变为 3，离开恢复；保留血量/时间条、低血量闪烁、声音和失败界面 | Scripts/Health |
| 手与移动 | 原手模型、握持/推门/按按钮姿势、摇杆移动弧线与入场控制教程；触发射线和移动弧线使用独立 LineRenderer | Scripts/Player |
| 菜单 | 保留暂停/继续、完成/失败、重试、主菜单、音效和 VR 按钮射线；暂停或结算时禁止世界射线交互 | Scripts/SystemUI |
| 训练流程 | 当前楼层 HUD、路线阻挡提示、二层对讲机、室外集合完成、练习时间与简要复盘 | Scripts/Campus |
| 日志 | 保留原 JourneyDirector、GeoFence、SafeZoneEndpoint、SessionLogger，并记录楼层及可见疏散标识 | Scripts/Journey/Runtime |

毛巾的回血/减速是原有训练数值机制，不表示毛巾可防护有毒烟气。该楼是虚拟训练场景，路线和避难区设置不作为真实建筑的消防合规设计。

## 文件框架

```text
Assets/
  Scenes/
    Campus/          新主训练场景
    MainMenu/        入口菜单
    Legacy/          原 whole_floor、SampleScene、Yining、AssemblyArea_1F
    Tests/           原健康时间和 UI 测试场景
  Scripts/
    Campus/          新场景整合逻辑
      Editor/        搭建、整理、验证、预览与自动回归工具
    Interaction/     门、窗、电梯、安全射线及预制体配置工具
    Health/          原 Interaction2 的计时、烟雾、毛巾、声音与 HUD
    Journey/         原 1F 场景中的流程和日志代码
    Player/          手柄、手模型、VR 移动和操作教程
    SystemUI/        暂停、结算、导航与 UI
    Interface/       IInteractable
    Legacy/          保留的旧交互/桌面测试脚本
  Prefabs/
    Environment/     小组已有环境组合、电梯及门等
    Campus/          新训练 HUD 提示预制体
    UI/              旧健康时间 UI 预制体
    LegacyAssembly/  原室外集合区组合
  Art/
    Campus/Materials 新建筑 URP 材质；第三方材质的兼容副本
    LegacyAssembly/  原集合区材质和网格
  ThirdParty/Art/    学校、室内、办公楼、特效等原始资产包
  Audio/            UI 与健康时间音效
  Fonts/            双语 TMP 字体
  Settings/Rendering/ URP 设置
  Legacy/           旧零散测试资产
```

`SteamVR`、`SteamVR_Input`、`SteamVR_Resources`、`StreamingAssets`、`XR`、`XRI`、`TextMesh Pro` 的 SDK/运行时约定路径保留，避免 SDK 编辑器和包引用失效。窗口可读网格、字体与射线材质仍位于 `Scripts/Interaction/Resources`，以保持现有独立预制体的 Resources 查找兼容。

目录移动使用 Unity AssetDatabase 保留原 `.meta` GUID；原场景归档而非删除。删除了已退役的 `StairTeleportTrigger.cs` 和 `FloorManager.cs`，并先从场景/预制体中移除它们的组件，避免 Missing Script。完整资产路径与 GUID 见 `AssetIndex.tsv`。

## 可独立拖放的预制体

- 电梯：`Assets/Prefabs/Environment/elevator(button))+wu.prefab`（原文件名实际有两个右括号）。FireSafetyElevator 和 FireSafetyPrefabSupport 绑定仍在，拖入有原项目 VR rig 的场景即可使用。
- 窗户：`Assets/ThirdParty/Art/Interiors/Interiors_A/prefabs/wall/Wall_3m_Window_H.prefab`。RuntimeFireWindow 会在运行时分离玻璃，不改变 FBX 源模型，保留开窗、二次警告及自动关窗。

不要删除以上预制体上的资源引用与支持组件。重缩放窗户可以改变交互范围，但范围仍限制为 1.5–2 m。

## 场景维护和检查

新场景不是运行时生成：建筑、道具、区域和引用都已经保存到 `.unity`，其他组员可以直接在 Unity 编辑。使用 `Tools > Campus > Rebuild Four Storey Campus` 会重建并覆盖**新生成场景**，需要先把手工修改另存；该工具不会覆盖归档场景或原始模型。不要用旧的 AssemblyAreaSceneBuilder 来重建主场景。

验证入口：`Tools > Campus > Validate Integrated Scene`。图像预览在 `Documentation/Previews`，静态检查在 `CampusValidation.txt`，不接入头显的 Play Mode 回归结果在 `PlayModeChecks.txt`。

暂时不接头显时，直接在 Unity 的 Scene 视图查看、右键配合 WASD 飞行浏览即可；不要把 VR 控制器输入当作键鼠输入。自动检查会临时关闭设备相关行为，不修改已保存场景或 XR 配置。复盘预览图中的 `03:12` 是排版演示数据。

头显复测顺序：401 开门 → 领取保护毛巾 → 在窗旁试两次右扳机 → 电梯 up/down 的远近距离 → 穿过烟雾观察 HUD → A 路线受阻改 B → 沿实体楼梯下行 → 二层对讲机或一层集合点 → 重试/主菜单；另外测试暂停、低血量与失败。

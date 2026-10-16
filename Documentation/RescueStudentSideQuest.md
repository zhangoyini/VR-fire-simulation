# 轮椅同学支线（双手点击切换前方跟随）

可选支线：只提示、记录、写日志，**不门禁二层避难区呼叫铃，也不影响胜利判定**。

## 文件

- `Assets/Scripts/Modules/F_RescueStudent/CampusRescueStudent.cs` — 支线主体，实现 `IInteractable`。
- `Assets/Scripts/Modules/F_RescueStudent/Editor/CampusRescueStudentSetup.cs` — 编辑器：程序化建模、保存预制体、一键放置。
- `Assets/Prefabs/Campus/RescueStudent.prefab` — 菜单生成的预制体（低模轮椅+坐姿同学，含 URP 材质，位于 `Assets/Prefabs/Campus/RescueStudentMats/`）。

## 搭建

1. 打开 `Assets/Scenes/Campus/CampusTraining.unity`。
2. 菜单 `Tools → Campus → Rescue Student → 1. Build Rescue Student Prefab`，生成/更新 `Assets/Prefabs/Campus/RescueStudent.prefab`（首次会同时生成 8 个材质）。
3. 菜单 `Tools → Campus → Place Rescue Student (2F corridor)`，在 2F 走廊中间实例化预制体，轮椅朝向避难区；自动绑定 `refugeVolume`（`2F Refuge Occupancy`）。
4. 在 Scene 视图按实际走廊微调实例 X/Z；Y 保持在 2F 楼面（约 3.8）。

## 模型说明

- 开放式车轮使用环形网格与辐条，人物有衣服、脸部和坐姿细节；无需外部模型。
- 预制体根：`BoxCollider`（非触发，0.88×1.4×1.16，中心 y=0.67）+ `Rigidbody`（kinematic，冻结旋转）+ `CampusRescueStudent`；所有子物体**无碰撞体**，悬停高亮由根统一驱动。
- 模型朝向 +Z，原点在地面；替换外部模型时保留根物体的 Collider/Rigidbody/脚本，只换 `Wheelchair`、`Student` 子物体。

## 行为

1. 教学正式开始前：靠近/按扳机无反应。
2. 双手分别靠近轮椅后方的左、右推把，在 0.5 秒内各按一次扳机；写日志 `RESCUE_STARTED`，轮椅开始在玩家视线前方约 1.05 米跟随。不必持续按住扳机，任一摇杆可传送。
3. 再次双手点击左右推把即停止；重复可继续。轮椅只在二楼安全落点移动，前方无可用空间或路径有障碍时不移动；跟随期间传送会同时校验轮椅落点。
4. 学生进入 `2F Refuge Occupancy` 并停留 1 秒 → 提示“已安全送达”，写日志 `RESCUE_COMPLETED`，停止跟随。
5. 呼叫铃与 1F 集合点胜利逻辑不变。

## 可调参数（CampusRescueStudent 组件）

| 参数 | 默认 | 含义 |
| --- | --- | --- |
| followSpeed | 2.2 | 平时跟随玩家视角目标点的速度（米/秒） |
| followOffset | 1.05 | 轮椅与玩家视角的前方距离（米） |
| dwellSeconds | 1 | 避难区内停留判定秒数 |

## Play 验证清单

- [ ] 开场教学阶段靠近学生无提示、按扳机无反应。
- [ ] 正式练习后，两手分别在左右推把点击一次才开始跟随；单手点击无效。
- [ ] 松开扳机后仍跟随；轮椅在视线前方移动，传送时与玩家一起移动。
- [ ] 再次双手点击推把后，玩家自行传送，轮椅留在原地。
- [ ] 墙、家具、下楼梯会使不安全的轮椅传送落点变红。
- [ ] 推入避难区停留 1 秒出现完成提示，日志含 `RESCUE_COMPLETED`。
- [ ] 不接应学生，直接按呼叫铃仍能正常胜利。
- [ ] 胜利/失败结算后学生不再移动。

## 后续可选

- 换正式坐姿人物/轮椅模型：替换预制体里 `Wheelchair`、`Student` 两个子物体，保留根物体的 BoxCollider、Rigidbody 与脚本。
- 调色：改 `Assets/Prefabs/Campus/RescueStudentMats/` 下材质的 `_BaseColor`，重新 Build 预制体即可。
- 结算语追加“已协助 1 人”：在 `CampusExperience.OnOutcome` 读 `CampusRescueStudent.Rescued` 拼到 debriefLabel。

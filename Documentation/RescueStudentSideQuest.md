# 轮椅同学支线（方案B：对话接应后带路跟随）

可选支线：只提示、记录、写日志，**不门禁二层避难区呼叫铃，也不影响胜利判定**。

## 文件

- `Assets/Scripts/Modules/F_RescueStudent/CampusRescueStudent.cs` — 支线主体，实现 `IInteractable`。
- `Assets/Scripts/Modules/F_RescueStudent/Editor/CampusRescueStudentSetup.cs` — 编辑器：程序化建模、保存预制体、一键放置。
- `Assets/Prefabs/Campus/RescueStudent.prefab` — 菜单生成的预制体（低模轮椅+坐姿同学，含 URP 材质，位于 `Assets/Prefabs/Campus/RescueStudentMats/`）。

## 搭建

1. 打开 `Assets/Scenes/Campus/CampusTraining.unity`。
2. 菜单 `Tools → Campus → Rescue Student → 1. Build Rescue Student Prefab`，生成/更新 `Assets/Prefabs/Campus/RescueStudent.prefab`（首次会同时生成 8 个材质）。
3. 菜单 `Tools → Campus → Place Rescue Student (2F corridor)`，在 2F 走廊中间实例化预制体；自动绑定 `followTarget`（CampusLocomotion）和 `refugeVolume`（`2F Refuge Occupancy`）。
4. 在 Scene 视图按实际走廊微调实例 X/Z；Y 保持在 2F 楼面（约 3.8）。

## 模型说明

- 全部由 Unity 基础几何体拼成（无外部素材）：大后轮+手轮圈+辐条、小前轮、车架、座椅、靠背、扶手、脚踏、推把；坐姿同学含头、躯干、四肢。
- 预制体根：`BoxCollider`（非触发，0.78×1.34×1.05，中心 y=0.67）+ `Rigidbody`（kinematic，冻结旋转）+ `CampusRescueStudent`；所有子物体**无碰撞体**，悬停高亮由根统一驱动。
- 模型朝向 +Z，原点在地面；替换外部模型时保留根物体的 Collider/Rigidbody/脚本，只换 `Wheelchair`、`Student` 子物体。

## 行为

1. 教学正式开始前：靠近/按扳机无反应。
2. 靠近学生，手悬停显示“按扳机打招呼”；扣一次扳机 → 接应成功，提示“跟我走，前往二层避难区”，写日志 `RESCUE_STARTED`。
3. 学生以 1.4 m/s 跟在玩家身后 0.75 m，自动转向；Y 锁死在出生楼层，不会下楼。
4. 学生进入 `2F Refuge Occupancy` 并停留 1 秒 → 提示“已安全送达”，写日志 `RESCUE_COMPLETED`，停止跟随。
5. 呼叫铃与 1F 集合点胜利逻辑不变。

## 可调参数（CampusRescueStudent 组件）

| 参数 | 默认 | 含义 |
| --- | --- | --- |
| followSpeed | 1.4 | 跟随速度 m/s |
| stopDistance | 0.8 | 距玩家多近停下 |
| followOffset | 0.75 | 跟在身后距离 |
| dwellSeconds | 1 | 避难区内停留判定秒数 |

## Play 验证清单

- [ ] 开场教学阶段靠近学生无提示、按扳机无反应。
- [ ] 正式练习后悬停出现打招呼提示。
- [ ] 扣扳机后学生开始跟随，转身时学生同向转向。
- [ ] 玩家停下，学生在身后停住。
- [ ] 推入避难区停留 1 秒出现完成提示，日志含 `RESCUE_COMPLETED`。
- [ ] 不接应学生，直接按呼叫铃仍能正常胜利。
- [ ] 胜利/失败结算后学生不再移动。

## 后续可选

- 换正式坐姿人物/轮椅模型：替换预制体里 `Wheelchair`、`Student` 两个子物体，保留根物体的 BoxCollider、Rigidbody 与脚本。
- 调色：改 `Assets/Prefabs/Campus/RescueStudentMats/` 下材质的 `_BaseColor`，重新 Build 预制体即可。
- 结算语追加“已协助 1 人”：在 `CampusExperience.OnOutcome` 读 `CampusRescueStudent.Rescued` 拼到 debriefLabel。

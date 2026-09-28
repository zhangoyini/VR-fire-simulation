# XR-Fire-Evacuation

COMP5424 Extended Reality · Group 11 · Scenario D（Campus Emergency Preparedness）
Unity 2022.3.62f3 · URP 14 · XR Interaction Toolkit 2.5.4 · OpenXR（Quest 3）

---

## 第一次在本机打开

1. Unity Hub → Open → **Add project from disk** → 选中本文件夹（含 `Assets` / `Packages` / `ProjectSettings`）。
2. 打开后 Unity 会自动安装 `Packages/manifest.json` 里声明的依赖，首次导入约需几分钟。
3. 菜单 **Harbourview → Check Project Setup**：确认 URP / Input System / XRI 三项都是 `[OK]`。
4. 菜单 **Harbourview → Apply URP Settings**：把 `_Project/Settings/` 下的渲染管线资产挂到 Graphics Settings。
5. **Edit → Project Settings → Player → Active Input Handling** 改为 `Input System Package (New)`，否则 XRI 不工作（Unity 会弹对话框，点 Yes 也会自动重启编辑器）。
6. 菜单 **Harbourview → Build 1F Assembly Area Scene**：生成本次交付的一楼集合点场景。
7. 跑之前按 Docs 里的《Git 协作规范》先配好 git 身份。

## 目录约定

```
Assets/_Project/
  Scenes/        各阶段场景；AssemblyArea_1F.unity 为一楼安全终点
  Art/
    Materials/   场景构建器生成的共享材质（改这里 = 全局生效）
    Meshes/      程序化生成的网格（跑道环、集合点轮廓）
  Settings/      URP 管线资产（复用组里验证过的配置）
  Scripts/
    Runtime/     运行时：JourneyDirector / GeoFence / SessionLogger / SafeZoneEndpoint / NotableCue
    Editor/      编辑器工具：场景构建器与工程自检
```

**只有 `_Project/` 是我们自己写的东西。** 第三方素材一律放 `Assets/Vendor/<来源>/`，不要在里面改东西，方便后续整体替换。

## 场景构建器

菜单 **Harbourview → Build 1F Assembly Area Scene** 会清空当前场景并重建以下内容：

| 物体 | 作用 |
|------|------|
| `Environment/Building_Facade_Placeholder` | 北侧教学楼立面，中间留 4 m 出口开洞，是为等 Module C 的建筑模型而放的临时替身，模型到位后整体替换 |
| `Environment/Plaza_Paving` | 楼前铺装广场 |
| `Environment/Ground_Track`、`Pitch_Infield` | 操场跑道与内场草坪，提供空间参照 |
| `Environment/AssemblyPoint/` | 白色轮廓圆 + 隔离柱 + 集合点指示牌 |
| `Environment/AssemblyPoint/T12_GroundReached_Zone` | **T12 终点触发器**：GeoFence + SafeZoneEndpoint，停留 0.8 s 判定到达 |
| `PlayerStart_BuildingExit` | 临时玩家锚点（相机 + 碰撞代理），正式版换成 XR Origin，**记得保留碰撞体否则 T12 永远不触发** |
| `Systems/JourneyDirector`、`Systems/SessionLogger` | 状态机单例与匿名行为日志 |

构建器可反复运行；材质和网格会被复用而不是重复创建。

## 交付物：楼层 Prefab `PF_1F_AssemblyArea`

按组长《场景建立》的要求，每个人交付的是一个**可以塞进总装场景的楼层 Prefab**，不是私有的场景文件。

- 路径：`Assets/_Project/Prefabs/PF_1F_AssemblyArea.prefab`
- 内容：`Environment/` 下的全部地面与建筑内容，含 `T12_GroundReached_Zone` 终点触发器与集合点指示牌
- **不含**：`PlayerStart_BuildingExit`、`Systems/`（XR 设备与 JourneyDirector 属于场景级，不能跟着楼层 Prefab 走，否则总装后会出现多个 director）
- 单独导出：菜单 **Harbourview → Export 1F Assembly Area Prefab**（跑 Build 时也会自动导出一次）

### 坐标约定（总装时看这里）

所有内容以 **原点 = 操场几何中心、地面 y = 0** 为准：

| 参照点 | 坐标 |
|--------|------|
| 教学楼立面外沿（z 最小侧） | `z = +30.5` |
| 出口开洞中心 | `x = 0, z ≈ +30.4`，宽 4 m |
| 出口门槛（用户被投递的位置） | `x = 0, z ≈ +28.8` |
| 集合点圆心 | `x = -12, z = +23.5`，半径 4.5 m |
| 跑道环 | 圆心 `(0, -6)`，内径 19、外径 25 |

也就是说：**把本 Prefab 整体摆到地面上、教学楼南立面与 `Building_Facade_Placeholder` 对位即可**，用户从 `z ≈ 28.8` 入场往 `-x / -z` 方向走到集合点，约 13 m。

### 素材来源

Henry 的 `VR-fire-simulation-Henry-asset-import` 工程里有 `school basic prefab/`、`Stair Prefab/`、`Elevator Exit sign prefab/` 等可用素材。要用就把整个文件夹拷到本工程的 `Assets/Vendor/Henry/` 下，**不要在原地改**，以便后续整体替换；当前场景里的几何体都是程序化占位体，随时可被替换。

## 当前的临时替身（务必知道）

- 指示牌用的是 **Canvas + 内置 Arial**（英文），不是最终的 TextMeshPro 版本。中文/双语字幕由 Module F 后续替换。
- 几何体全是 Unity 基础形状 + 纯色 URP 材质，等内容资产阶段换成 low-poly 模型后，再统一核算性能预算。
- `PlayerStart_BuildingExit` 不是 XR Origin：XRI 装好后由 Module B 换成正式 rig。

## 行为日志

`SessionLogger` 每场会话在 `Application.persistentDataPath/session-logs/<sessionId>.jsonl` 写一行一条 JSON，字段与提案 Appendix C §3.5 的评估指标一一对应。sessionId 运行时随机生成，**不与知情同意书或任何身份信息关联**（提案 R7 / Scenario D §6）。

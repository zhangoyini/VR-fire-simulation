# 消防交互 prefab 使用说明

将以下任一 prefab 拖入已配置 SteamVR 头显和双手柄的场景，进入 Play 即可使用：

- 电梯：`Assets/prefab/elevator(button))+wu.prefab`（实际文件名含两个右括号）。
- 窗户：`Assets/Stair Prefab/Interiors_A/prefabs/wall/Wall_3m_Window_H.prefab`。

无需在场景中手工挂载消防交互脚本。`FireSafetyPrefabSupport` 会复用场景已有的
`VRFireSafetyInteraction`；若没有，会在 `inputSource = RightHand` 的
`SteamVR_Behaviour_Pose` 下自动创建射线与交互组件。右扳机沿用项目的 `GrabPinch`
动作绑定。场景仍需正常配置 VR 相机、右手柄 Pose 和 SteamVR 输入动作。

电梯根节点的 `FireSafetyElevator` 已绑定自己的两个按钮，交互距离默认 1.5m，
可在 Inspector 中调整。重命名 prefab 实例或按钮不会影响绑定。

窗户根节点的 `RuntimeFireWindow` 已绑定可读取网格副本，实例激活时自动拆分玻璃。
靠近后，第一次对准玻璃并松开扳机打开 90°；第二次对准原窗框或窗洞并松开扳机，
显示中英文警告，等待 2 秒自动关闭。开关过程中不重复响应。运行时 Instantiate
和先禁用后激活的实例也会自行初始化。

两个 prefab 都直接引用中文字体及射线材质；窗户额外引用网格缓存。提示保持头显前
1m 的 World Space Canvas。多人合作时同步 prefab、`Assets/Scripts/Interaction`
及其资源和 `.meta` 文件。跨 Unity 项目复用还需 SteamVR、TMP 和 URP 依赖；不要只
复制单个 `.prefab` 文件。导出 Unity Package 时勾选 Include Dependencies，并确认
包含 Interaction 文件夹、中文字体和字体许可证。

维护入口：`Tools > Fire Safety > Configure and validate interaction prefabs`。
验证入口：`Tools > Fire Safety > Validate interaction prefabs`。

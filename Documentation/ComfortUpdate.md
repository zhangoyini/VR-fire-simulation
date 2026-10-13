# 移动、门窗与渐进烟雾更新

最新版本见 [EvacuationUpdate.md](EvacuationUpdate.md)：右摇杆现已停用、两侧楼梯均可疏散、烟雾可逐层蔓延。下文是前一版实现记录，不代表最新转向/封路配置。

项目：`F:\unityproject\VR-fire-simulation-my`。主场景：`Assets/Scenes/Campus/CampusTraining.unity`。

当前状态：更新已应用并保存到主场景，原布局已备份。运行时与编辑器代码编译通过，74 项不接头显的回归检查通过，缺失脚本为 0；图形验证确认暗角降低周边亮度而保留中央视野，并生成了实际粒子推进的 5 / 20 / 45 秒烟雾预览。`RevisionChecks.txt` 已更新为本轮结果。Quest 双眼呈现、追踪、震动、舒适度与 GPU 帧率仍未实机验证。

## 操作变化

- 右摇杆改为每次 30° 分段转向，回中后才能再次转向，持续推住不会连续旋转。保留头显真实转头，不摇晃相机或改变相机 FOV。
- 左摇杆仍可四向连续行走，最高速度从 1.35 提高到 1.8 m/s，以 6 m/s² 短暂加速；松开立即停止，没有惯性滑行。行走方向在开始推动摇杆时确定，自然转头观察不会突然改变路径；回中再推动会按新的观察方向行走，分段转向则同步改变身体和行走方向。
- 行走时渐入周边暗角，停下渐退，中央视野保持可见。右摇杆分段转向也会触发短暂周边遮罩。暗角是舒适度辅助，不能保证所有人都不晕；仍需 Quest 实机体验与帧率检查。
- 双手接近门窗实体约 12 cm 时高亮；握住扳机后，沿门窗表面法向推拉约 15 cm 对应 45°、30 cm 对应 90°，不再根据手离铰链的半径降低开合幅度。退回握持位置可回到原角度，松手仍保持角度。窗口仍限制向外 0–90°，保留再次握放警告和 2 秒后自动关闭。
- 追踪手模型不做位移放大或伪造伸长。推动距离在玩家追踪空间中计算，摇杆行走和分段转向不会被误算成推手距离。忽略 3 mm 以内的抖动，保留双手互斥握持和角度变化震动。

Meta 的舒适度设计建议提供分段转向，保持移动一致并进行目标头显测试：[Locomotion Overview](https://developers.meta.com/vr/documentation/iwsdk/concepts/locomotion/)。Unity 也将周边暗角作为舒适度辅助：[Tunneling Vignette Controller](https://docs.unity.cn/Packages/com.unity.xr.interaction.toolkit%403.6/manual/tunneling-vignette-controller.html)。本项目沿用已有 SteamVR 输入实现，不额外安装另一套 XR 控制系统。

## 阅览室与烟雾

移除阅览室一侧三、四楼尽头的 A 楼梯牌；保留 B 楼梯牌、走廊出口箭头和障碍处的封闭警告。

四楼烟雾不再一开始就启用整片走廊伤害区。顶棚烟层先在房间中逐渐增多，8 秒后开始通过门口逸出，再以分段粒子发射器向走廊两侧推进。关闭的门按较慢速率泄漏，打开后扩散更快。门口、十段走廊分别在对应烟层形成后启用暴露区，仍采用原 3 秒宽限、4 HP/s 和毛巾游戏倍率；暂停和结算停止扩散。重试重载场景会恢复初始状态。

走廊粒子设置了物理碰撞、每段最多 36 个粒子和限制在走廊内的发射形状。这是分阶段实时视觉近似，不是烟气流体仿真、火灾传播预测或真实疏散安全计算。

## 文件与应用

新增代码仍位于 `Assets/Scripts/Campus`：`CampusComfortVignette.cs`、`Shaders/CampusComfortVignette.shader`、`CampusSmokeSpread.cs`、`Editor/CampusComfortUpgrade.cs`。修改 `CampusLocomotion.cs`、`CampusHingeInteractable.cs`、`CampusHandInteractor.cs`、`CampusSmokeVolume.cs`，并同步更新场景生成器和自动检查。

场景更新入口：Unity 菜单 `Tools > Campus > Apply Comfort and Smoke Update (keeps scene layout)`。它在现有场景上修改对应组件和烟雾区域，不重新生成整栋教学楼；首次应用时保存 `Assets/Scenes/Legacy/CampusTraining_v2_PreComfort_Backup.unity` 布局备份。备份不包含旧版脚本的完整快照。

实际验证结果见 `RevisionChecks.txt`；烟雾阶段预览位于 `Documentation/Previews/SmokeSpread_5s.png`、`SmokeSpread_20s.png`、`SmokeSpread_45s.png`，周边暗角预览为 `GameplayHUD_ComfortWalk.png`。生成预览时暂时将阅览室门打开 60°，不会保存这个开门状态。更新已保存，无需再次运行菜单；重新打开主场景后开始 Play 即可使用。

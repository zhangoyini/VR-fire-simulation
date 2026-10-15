# 4号：灾害机制验收

## 基本信息

- 姓名：Jiao Ruikang
- 测试日期：2026.10.15
- 测试提交SHA：待填写
- Unity版本：2022.3.62f3
- 测试设备：本地Unity Editor Play Mode（暂无头显设备）

## 对AI改动的理解

- AI对烟雾、生命值或毛巾保护做了什么：1. 烟雾系统拆为CampusSmokeSpread/CampusSmokeVolume/CampusSmokeExposure三个脚本；2. 毛巾系统改为 CampusTowelInteractable，湿毛巾必须被握持且覆盖口鼻才生效，生效后烟雾伤害降为40%；3. 水龙头CampusFaucet支持任意一只手开关。
- 与原来功能有什么不同：1. 原来SmokeZone是Trigger+掉血倍率，现在是体积检测+口鼻覆盖判定；2. 原来TowelPickup拾取后直接回血+限时减速，现在是湿毛巾捂住口鼻不再回血，更符合火灾逃生教学逻辑；3. 现在支持双手分工（一手持毛巾、一手开水龙头），掉落可重新拾取，且有震动反馈。
- 我认为需要保留或修改的内容：1. 保留烟雾分Stage扩散的设计可保留；2. 湿毛巾减伤40%而非免伤。
- 判断原因：符合"房间→走廊→楼梯→下层"的烟气蔓延；符合"不完全防护"的安全教育。

## 测试记录

| 测试项目     | 预期结果      | 实际结果                                                                                                                                                                              | 状态 |
| -------- | --------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -- |
| 不拿毛巾进入烟雾 | 宽限时间后正常扣血 | Brief smoke exposure retains its six-second grace period；Prolonged exposure removes 2 HP per second（5.9s=1.00，7s=0.98）                                                            | 通过 |
| 干毛巾放在口鼻  | 不能获得湿毛巾保护 | A dry towel near the mouth does not stop smoke damage（左右手均验证，血量 0.98 无减免）                                                                                                         | 通过 |
| 湿毛巾未放在口鼻 | 不能获得完整保护  | A wet towel held away from the face does not stop smoke damage；A towel behind the head is not treated as mouth/nose coverage（血量 0.98）                                             | 通过 |
| 湿毛巾放在口鼻  | 按设计降低烟雾伤害 | Sustained wet cloth coverage reduces but never eliminates smoke damage for RightHand/LeftHand（12s 后血量约 0.952，伤害降至 40%）；Protection does not reset the ongoing smoke exposure clock | 通过 |
| 离开烟雾区    | 停止扣血      | Leaving smoke resets exposure without extra damage；Moving the wet towel away restores damage without a fresh grace exploit（Exposure 归零，无额外伤害）                                     | 通过 |
| 暂停或游戏结束  | 停止伤害和计时   | Pause blocks damage and smoke progression；Pause freezes wetting and ignores faucet switch input；Pause safely releases held cloth without leaving a persistent shield              | 通过 |
| 生命值归零    | 只触发一次失败结果 | Health depletion preserves the failure outcome（Outcome=GameOver）；Arrival cannot overwrite failure（TryShowVictory()=false）                                                         | 通过 |

状态填写：`未测试`、`通过`、`失败`或`阻塞`。

## 问题与复测

- 发现的问题：HealthTimeController 在主场景中的 baseDrainPerSecond 被覆盖为 0，血条不会随时间自然下降，8 分钟倒计时机制失效。本次回归中 "Clear air causes no passive countdown damage" 一项因此通过（血量恒为 1.00）；修复为 0.208333 后 0.2 秒仅掉 0.0417（HealthPercent=0.99958>0.999），该项仍应通过，但需复测确认。
- 复现步骤：见上一条
- 实测生命值变化：无保护进烟雾-5.9s = 1.00，7s = 0.98（每秒 2 HP）；干毛巾捂口鼻：0.98（无减免） - 湿毛巾离脸/脑后：0.98（无减免） - 湿毛巾捂口鼻：12s ≈ 0.952（伤害降至 40%） - 移开湿毛巾：恢复满伤害；放回：再次降伤 - 掉落湿毛巾：立即失去保护
- 截图或视频：待填写
- 修复提交：待填写
- 复测结果：待填写
- 复杂问题的GitHub Issue（可选）：待填写

## 验收结论

- 结论：通过
- 遗留问题：待填写
- 签名：待填写

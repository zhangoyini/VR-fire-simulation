# 2号：玩家移动验收

## 基本信息

- 姓名：Henry Cheung
- 测试日期：15/10/2026
- 测试提交SHA：待填写
- Unity版本：2022.3.62f3
- 测试设备：quest 3

## 对AI改动的理解

- AI对玩家移动做了什么：1. It change from teleport to Continuous stick movement 2. add scripts on controller left/right and left/right controller dummy to reads the right controller’s tracked position and rotation, animates the glove’s fingers, enhance interaction, and positions the hand from controller tracking and hides it when tracking is lost
- 与原来功能有什么不同：same as above
- 我认为需要保留或修改的内容：Determine if the group would like to have teleport movement or Continuous stick movement
- 判断原因：Consider if motion sickness is properly addressed

## 测试记录

| 测试项目 | 预期结果 | 实际结果 | 状态 |
| --- | --- | --- | --- |
| 基础传送 | 可以传送到允许的地面 | 通过 | you can adjust the ray cast time to adjust the teleport distance you would like to provide to the player|
| 最大传送距离 | 超过最大距离时不能传送 | 通过 | you can adjust the ray cast time to adjust the teleport distance you would like to provide to the player |
| 非法落点 | 墙面、窗外和禁区不能传送 | 通过 | For teleport, the player can only teleport when you set the platform ground layer and attach teleport area scripts |
| 场景边界 | 不能传送到建筑或地图外 | 通过 | Same as above |
| 教学期间移动 | 按教学流程正确启用或禁止 | 未实现 | 失败 |
| 暂停与结算 | 暂停或结算时不能继续移动 | 未实现 | We do not have such function in older version, in latest version, it is controlled by script of SystemUIPauseController and CampusLocomotion |
| VR舒适度 | 没有明显跳变或异常眩晕 | it depends which system we use at last | 
| Audio| Walking sound | 未实现 | For teleport version, walking sound is not included at we were teleporting, in latest version, movement create sound

状态填写：`未测试`、`通过`、`失败`或`阻塞`。

## 问题与复测

- 发现的问题：it could raised issue when you combine teleport system with some current features
- 复现步骤：待填写
- 截图或视频：待填写
- 修复提交：待填写
- 复测结果：待填写
- 复杂问题的GitHub Issue（可选）：待填写

## 验收结论

- 结论：未测试 / 通过 / 不通过 / 阻塞
- 遗留问题：待填写
- 签名：待填写

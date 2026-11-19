# 血条底座与状态图标

采用 Assets/Resources/FireTrainingUI/hud/hud_health_base_flat.png。血条左侧圆形位置显示状态图标，中间槽显示实时血量，右侧显示原先归一化至 100 的血量数值。楼层/血量文字及其他非毛巾防护显示保留。

- 血量 > 50%：绿色、icon_health。
- 血量 > 25% 且 <= 50%：黄色、icon_warning。
- 血量 <= 25%：红色、icon_cross。
- 原有实际毛巾防护状态生效时：优先蓝色、icon_protection；防护结束恢复血量档位颜色与图标。

HUD 中“防护生效 · 掉血减缓”和“湿毛巾已就位”的双语状态行已移除。未防护的烟雾提醒、毛巾操作教学和结算说明保留。没有改变烟雾伤害、时间消耗或毛巾保护判定。

HealthTimeBar.Start 自动应用底座和布局。血量事件和防护变化同步更新填充、图标及数字，避免保护状态变更时血条突然回满。

Unity 2022.3.62f3 编译与四态渲染通过，验证 78/45/18/45 的实时数字、四种图标、防护解除后恢复警戒图标及数值文字无溢出。预览使用实际 HealthTimeCanvas 预制体，并关闭只应由运行时控制的旧红色闪烁遮罩，正常游戏低血量闪烁逻辑仍保留。未做头显实机验收。

截图：Documentation/Previews/HealthHUD_FourStates.png

日志：Logs/health-hud-preview.log（HEALTH_HUD_FOUR_STATES_PASS）

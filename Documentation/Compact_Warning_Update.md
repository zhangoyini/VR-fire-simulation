# 警告卡缩小并放到血条下方

普通警告和救援楼梯警告共用 CampusCompactWarning：560×180，缩放 0.0013，实际宽约 0.73 米，约为之前普通警告的一半。正文 450×112，字号 18～22，保留双语。

根据实际 HealthTimeBar 下的 Slider 四角定位，警告卡上边缘与血条下边缘之间留 3.5 厘米间距，并保持相同视距。没有血条的兼容场景使用视线前方 1.6 米的回退位置。普通救援跟随说明卡不属于危险警告，不受这次尺寸调整影响。

Unity 2022.3.62f3 编译与 GPU 渲染通过；五种实际双语警告均无文字溢出，检查了真实血条与警告卡之间的间距、警告宽度。该截图展示布局，未进行 Quest 真机清晰度验收。

截图：Documentation/Previews/Warning_BelowHealth.png

日志：Logs/compact-warning-preview.log，COMPACT_WARNING_LAYOUT_PASS。

## 图标与首次开窗触发

电梯和跳窗警告分别在正文左侧使用 `icon_no_elevator.png` 和 `icon_no_jump.png`。文字区向右避让，其他警告隐藏图标并恢复完整文字宽度。

玩家第一次实际推开窗户（角度大于 0.1°）时立即弹出跳窗警告，不等待松手或第二次抓取；同一次开窗不逐帧重复弹出。玩家操作的教学练习窗也采用此规则，程序调用 SetAngle 的演示不弹警告。松手后仍保留自动关闭逻辑。非物理交互的运行时窗户也在首次打开的旋转过程中提示。

Unity 编译、首次物理开窗持握状态检查及五种双语警告布局检查通过。新增截图 `Warning_NoElevator.png`、`Warning_NoJump.png`，日志 `Logs/warning-icons-preview.log`，含 FIRST_WINDOW_OPEN_WARNING_PASS 和 COMPACT_WARNING_LAYOUT_PASS；未进行头显实机验收。

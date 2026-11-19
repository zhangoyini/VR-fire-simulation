# 教学改为逐页手动继续

所有可见操作教学页下方使用 `Assets/Resources/FireTrainingUI/buttons/button_skip_wide.png`。SKIP 跳过当前页，进入下一页；不跳过整个教学。使用既有双手菜单射线和扳机点击，也可通过现有接触交互触发。

教学说明和示范不再按阅读时间自动切页，完成页面要求的动作后也停留到点击 SKIP。内部无文字的淡出/折叠过渡继续自动完成。地图 X、任务 Y 和实物练习仍可操作，但不会因此自动切换教学页。暂停练习中打开暂停菜单后，仍使用“继续”返回教学。

首次开始、结算重开、暂停菜单重开均重新进入手动教学第一页，移除了重开自动跳过 Controls 的静态标记。教学完成后的演练开场和任务提示时序不受这次页控调整影响。

Unity 2022.3.62f3 编译和专用运行检查通过：32 个可见页各等待 60 秒均不自动切换，SKIP 均能前进；X/Y 可打开对应面板且不切页；实际场景重载后回到手动 Introduction。检查了文字溢出、按钮位于说明框下方，并渲染普通说明、手柄示范和窗户示范三类页面。该检查替代原来的定时自动切页预期；旧 CampusLessonDriver 内按自动播放时长断言的测试尚需按新手动流程整体迁移，不能把旧 389 项记录视为当前教学的验收。未做头显实机操作验收。

日志：Logs/manual-tutorial-render.log，MANUAL_TUTORIAL_PASS。

截图：Documentation/Previews/Tutorial_Manual_Intro.png、Tutorial_Manual_Controller.png、Tutorial_Manual_Window.png。

# VR-fire-simulation：空模型与五个独立完整工程包

你们的 GitHub 仓库是 **VR-fire-simulation**。成品来源是本机 **VR-fire-simulation-my**。两者名字不同没有关系：这次把新版内容放进已有仓库，不新建另一个 GitHub 仓库，也不复制原工程的 `.git`。

整个操作只使用 **GitHub Desktop + Windows 资源管理器 + Unity**，不需要命令行，不运行任何 .ps1。

## 六包分别是什么

| 包 | 内容 | 负责目录 | 个人分支 |
| --- | --- | --- | --- |
| 01_EmptyModel.zip | 只有四层建筑、家具、场地、碰撞和模型查看相机；不运行交互/火灾/任务 | 场景、公共基础与资产，成员 1 维护 | codex/campus-model |
| 02_Model_A_Player.zip | 同一个模型 + A：舒适行走、脚步声、练习书抓取、物体投掷 | Assets/Scripts/Modules/A_Player | codex/module-a-player |
| 03_Model_B_DoorsWindowsElevator.zip | 同一个模型 + B：双手推门窗、电梯/跳窗警告、窗户自动关闭 | Assets/Scripts/Modules/B_DoorsWindowsElevator | codex/module-b-doors-windows |
| 04_Model_C_TowelWater.zip | 同一个模型 + C：水龙头开关、流水、干湿毛巾、持有/掉落/口鼻覆盖 | Assets/Scripts/Modules/C_TowelWater | codex/module-c-towel-water |
| 05_Model_D_FireSmokeHealth.zip | 同一个模型 + D：火焰、自然逐层烟雾、烟雾伤害与湿毛巾减伤 | Assets/Scripts/Modules/D_FireSmokeHealth | codex/module-d-fire-smoke |
| 06_Model_E_LessonTasksUI.zip | 同一个模型 + E：3D 操作教学、开场动画、任务/路线/目的地高亮、UI与实时地图 | Assets/Scripts/Modules/E_LessonTasksUI | codex/module-e-lesson-ui |

**六个包都是完整 Unity 源工程，不是上一次的零散 Payload 包。** 每个包里面都有一个 `VR-fire-simulation` 文件夹，直接包含 Assets、Packages、ProjectSettings、Documentation 和三个根配置文件。

“空模型”指在 Play 中不启用任何游戏交互；建筑不是空房间，所有场景模型/陈设仍在。为避免循环引用、Missing Script，必要 SDK、共享输入接口及未接入组件的 API 占位仍保留。打开空模型后可在 Unity Scene 视图观察/自由浏览，不会自动进入剧情。

单模块包为了独立检查，启用必要的公共相机/真实手柄追踪、接触高亮、震动请求和基础浏览；高级行走、脚步和投掷属于 A。B/C 等单模块包不代表其余模块也已完成。E 的教学演示可单独查看，但完整火灾/毛巾任务需要 A–D 合并后验收。

## 关键：模型分支固定不变，另建整合分支

只把 01 上传到 **codex/campus-model**，之后不要往这个分支合并任何功能。

另建 **codex/campus-integrated** 用来接收五个成员 PR。否则前两人的功能已经并入模型分支，后来的成员再覆盖“模型+B”完整包，会把前人的功能退回占位版本。

所有成员都从同一份 **codex/campus-model** 创建自己的分支；PR 都合并到 **codex/campus-integrated**。最后检查成品，再把整合分支合并到 main（默认分支叫 master 时换成 master）。

## 一、你 / 成员 1：把空模型放进 VR-fire-simulation 仓库

1. 打开 GitHub Desktop，登录自己的账号。选择 `File → Clone Repository`，选择你们现有的 **VR-fire-simulation**，克隆到一个新的空目录，例如 `E:\github\VR-fire-simulation-team`。不要选完整原工程 `VR-fire-simulation-my`，也不要把 ZIP 所在目录当仓库。
2. 克隆完成，通过 `Repository → Show in Explorer` 打开实际目录。本文之后说的“仓库根目录”就是这个窗口里的文件夹，不是包目录，也不是上一级 E:\github。
3. 在 Desktop 切换到当前默认分支，点击 `Current Branch → New Branch`，创建 **codex/campus-model**。确认顶部确实显示这个名字。先不要打开克隆中的旧 Unity 场景。
4. 在仓库外新建备份目录，例如 `E:\VR-backup\旧仓库工程`。把新克隆中的旧 **Assets、Packages、ProjectSettings、Documentation**（存在的才移动）剪切到这个备份目录；旧 .gitignore、.gitattributes、.vsconfig、.vscode 和 unityProject.vrmanifest 也先移出去备份。
5. **绝对保留仓库里的隐藏 .git。** 其它根文档、LICENSE、.github 可保留，但你要检查旧 README/CI 是否仍适用。这一步只对这份新克隆操作，不改你的原游戏工程。
6. 解压 **01_EmptyModel.zip**，进入包里的 **VR-fire-simulation**，全选其内部内容，复制到仓库根目录。不是复制最外层 01 文件夹，也不是把整个 VR-fire-simulation 再套进仓库中。
7. 仓库根目录应直接看到 **Assets、Packages、ProjectSettings、Documentation**，并保留原来的 .git。不要留下第二份旧 Assets，不要把备份放进 Assets。
8. GitHub Desktop 的 Changes 会出现新版文件和旧文件删除；这是整体迁移。查看清单，不勾选 ZIP、备份、Library、Temp、Logs、UserSettings。首次迁移应包含旧目录的删除，避免残留重复代码。
9. 用 Unity Hub 添加这个**工作仓库根目录**，选择 **2022.3.62f3**。打开 `Assets/Scenes/Campus/CampusTraining.unity`，确认建筑显示、Console 没有红色编译错误，空模型不自动执行交互。首次导入依赖和资源可能需要几分钟。不要点重建校园或场景升级菜单。检查后关闭 Unity。
10. 回到 Desktop，Summary 填“迁入四层教学楼模型与共享接口”，点击提交到 **codex/campus-model**，再点击 **Publish branch**（已发布时是 Push origin）。不是 Publish repository，不创建新的 GitHub 仓库。
11. 从刚提交的 **codex/campus-model** 再新建 **codex/campus-integrated**，创建窗口中明确选择基于模型分支；发布此分支，不应用任何功能包。模型分支继续保持固定。
12. 通知五位成员：模型分支与整合分支都已发布，并给每人自己的一个 ZIP。先给他们仓库写入权限，确保能直接发布个人分支。

## 二、举例：小张负责 B 门窗与电梯

小张拿到的是 **03_Model_B_DoorsWindowsElevator.zip**，不是所有包。

1. 小张用自己的 GitHub Desktop 克隆同一个 **VR-fire-simulation** 仓库到自己的本地目录，例如 `E:\github\VR-fire-simulation-zhang`。
2. 点击 Fetch origin，切换到 **codex/campus-model**；出现 Pull origin 时先同步。确认这里是空模型，不能用原 main 或别人已经整合功能的分支作为起点。
3. 点击新建分支，名称 **codex/module-b-doors-windows**，基于 **codex/campus-model**。确认顶部当前分支已是自己的分支。
4. 关闭 Unity。把自己的工作副本做一份仓库外备份，或至少备份要替换的文件；不要复制 .git 到包内。
5. 解压 B 包，进入其中的 **VR-fire-simulation** 文件夹，将里面所有工程内容复制到小张的**工作仓库根目录**；同名文件选择“替换目标中的文件”。这次只覆盖，不要删掉 .git，不要把外层文件夹套进去。
6. 虽然复制的是完整工程，绝大多数文件与模型分支完全相同。Desktop 应只显示本包 `实际变更文件.tsv` 里的差异：B 模块源码、B 独立接入资产、B 验收记录。**不应改主场景、A/C/D/E 源码或其它模块接入资产**。
7. 如果看到别的模块被改回、共享场景变化或大量预期外改动，停止提交；检查是不是从固定模型分支创建的分支，或是否误用了旧包。不要选择“全部丢弃”去处理未知问题。
8. 用 Unity 打开小张的工作仓库，检查双手推门窗、电梯及禁止跳窗提示。无头显时先验证编译、查看对应组件，实机项目如实记为待验证。不能把尚未集成的完整毛巾/火灾章节写成已通过。
9. 填写 `Documentation/TeamModules/B_DoorsWindowsElevator.md` 的真实测试记录。关 Unity后，在 Desktop 只勾选自己的实际文件变更，Summary 写“集成门窗电梯交互并记录验收”，提交到个人分支并 Publish branch/Push origin。
10. 点击 Preview Pull Request，**base 选 codex/campus-integrated，compare 选 codex/module-b-doors-windows**，再 Create Pull Request。不要用模型分支或 main 作接收方。你审核之后合并。

A/C/D/E 成员完全同样，只换对应包、模块目录、分支名字、验收文档。

## 三、最终整合与替换主分支

1. 逐个审核五位成员的 PR，全部合并到 **codex/campus-integrated**。共同场景、配置、目录元数据相同；模块文件和接入资产各自独立，正常复制不会产生跨成员文件冲突。
2. 整合人在 Desktop 切换到整合分支，Fetch/Pull 最新内容。检查 `Assets/Resources/CampusTeamModules` 中 A–E 的 integrated 均为 true；未合齐时不要求完整故事任务全部可完成。
3. Unity 中检查完整成品：教学/开场锁定与恢复 → 门 → 烟雾血量 → 毛巾/开水/浸湿/贴脸 → 三楼 → 门窗电梯警告 → 避难/集合 → 暂停/失败/重试。原有测试工具保留；本次打包的实际结果见根目录验证报告。
4. 确认之后创建 **codex/campus-integrated → main** 的最终 PR，正常审核合并。这样主分支成为新版工程，原有仓库历史保留，不用强制推送、不删除 main。

## 最重要的复制规则

- 你首次迁移：把**新克隆里的旧工程目录移到仓库外备份**，再复制 01 内部工程内容。
- 成员：必须基于固定空模型分支，然后用自己的完整包覆盖自己的新个人分支。
- 不要把单模块完整包覆盖到整合分支或已经合齐功能的工作副本。
- 只复制包里的 VR-fire-simulation **内部内容**；永远保留目标仓库的 .git。
- .meta 必须跟资源一起复制，不要重新生成 GUID。模型和材质目录由成员 1 管理；组员主要在自己的 Modules 子目录维护业务。
- 若只出现公共 Imported 材质的隐藏 URP 版本标记，或 Unity 自动生成的测试预览图，先交整合人判断，不要让成员顺手提交公共资产。
- 不上传 ZIP、说明包装层、临时缓存或备份。上传的是工作副本中的 Unity 源文件及真实验证记录。

## 开发与授权边界

这些包来自当前已经完成的项目。成员的复制提交是**已有功能接入/集成与实际测试**，不是新原创作者声明；后续实际开发、修复由对应成员在自己的目录提交，不修改作者或日期去补造历史。

保留 `.meta` 的作用见 [Unity 资源元数据说明](https://docs.unity3d.com/2022.3/Documentation/Manual/AssetMetadata.html)。分支及 PR 的界面操作参考 [GitHub Desktop 分支说明](https://docs.github.com/en/desktop/making-changes-in-a-branch/managing-branches-in-github-desktop)、[创建 PR](https://docs.github.com/en/desktop/working-with-your-remote-repository-on-github-or-github-enterprise/creating-an-issue-or-pull-request-from-github-desktop)。

未连接 Quest 3；真实追踪、双眼显示、震感、舒适度和性能仍需本人实机验证。湿毛巾是游戏减伤机制，不是呼吸防护保证。ZIP 是 Unity 源工程，不是 Windows exe 或 Quest APK。公开上传前须自行核对第三方模型/声音/字体的再分发许可。

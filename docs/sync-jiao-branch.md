# JIAO 分支整合说明（已完成，供回看与后续复现）

> 仓库：`https://github.com/zhangoyini/VR-fire-simulation`
> 本地仓库：`D:\NOW\PolyU\1st Sem\COMP5424 XR\group11\XR-Fire-Evacuation`

## ⚠️ 最新状态（2026-09-29 更新，以此为准，下方「结论先说」是旧版）

- **远端 `JIAO` = `679984a`（= `Henry-asset-import`），只含 Henry 素材**，已按本人要求清掉 WU 与 Classroom-1 的内容。
  执行方式：`git reset --hard origin/Henry-asset-import` + `git push --force-with-lease origin JIAO`；旧状态备份在本地 `JIAO-prev-main`（`7970ec2`）。
- **JIAO 现在落后于 `main`（`7970ec2`）**。若直接 base `main` ← compare `JIAO` 提 PR，合并后会把
  `Assets/Scenes/classroom2corridorstair.unity`、`Assets/prefab/{elevator,refuge area}.prefab`、
  `Assets/Scenes/refuge area and elavator scene.unity` 从 main 里**删掉**，并回退 144 个 `.mat`。
  → 提 PR 前必须先和组长确认口径。
- **本地开发分支 `d-1f-henry`**：基底 = 纯 Henry，叠加我们的一楼内容 + `docs/`、`README.md` 与 5 个 XR 依赖。**无 upstream，不会误推。**
  - 目录已扁平化（2026-09-29）：`Assets/1FScenes/`（场景）、`Assets/Playground Prefab/`（交付 Prefab）、
    `Assets/1FArt/`（材质+网格）、`Assets/1FScripts/`（Runtime+Editor 脚本）。旧的 `Assets/_Project`、`Assets/XR`、`Assets/XRI` 已删除。
- 另有两个历史分支保留内容：`d-1f-work`（基于旧 main，含教室/WU 内容）、`JIAO-backup`（`e6f701f`）。
- 交付时若要把 `d-1f-henry` 并回 JIAO：`git switch JIAO && git merge d-1f-henry && git push origin JIAO`（先确认删内容是否可接受）。

## 结论先说

- **Henry 的素材不需要单独拉**：`Henry-asset-import`（`679984a`）落后于 `main`，而 `main` 与 `JIAO` 当时是同一个提交 `7970ec2`，里面已经包含
  `Baic Asset`、`school basic prefab`、`Stair Prefab`、`Window Prefab`、`Elevator Exit sign prefab`、`Red light prefab`、`prefab` 和 URP 资产。
  也就是说素材早就在 JIAO 里了，我们要做的只是把一楼的东西叠上去。
- 本轮改动只有一个提交 `5302ec0`，内容 = 团队素材 + `Assets/_Project/**` + `Assets/XR`、`Assets/XRI` 设置 + `docs/`、`README.md` + manifest 增补 XR 依赖。
  **没有动团队的 ProjectSettings 和 .gitignore**，PR 的 diff 干净，只显示新增。

## 当时踩到的坑（别再犯）

1. **URP 资产 GUID 重复**：我们自己工程里的 `Assets/_Project/Settings/New Universal Render Pipeline Asset.asset` 与团队的
   `Assets/New Universal Render Pipeline Asset.asset` GUID 完全相同（`4adb5c1d…`）。两份并存 Unity 会报重复 GUID。
   → 已删除我们那份；`Harbourview/Apply URP Settings` 是按类型搜索 URP 资产的，会自动找到团队那份，无需改代码。
2. **网络**：WorkBuddy 沙箱代理会拦住 git 的 CONNECT（502）。本仓库已配置走本机 Clash 代理：
   ```
   git config --local http.proxy  http://127.0.0.1:7897
   git config --local https.proxy http://127.0.0.1:7897
   ```
   Clash 没开时 git 会连不上，开一下即可。
3. **凭据**：GCM 里已存 PolyU 账号（`26103438g`）的 token，路径用了无空格短写法
   `C:/PROGRA~1/Git/mingw64/bin/git-credential-manager.exe`（`Program Files` 带空格会被 git 拆坏）。
   提交署名邮箱已切为 `26103438g@connect.polyu.hk`。

## 复现步骤（如果需要重做）

```bash
git fetch origin
git switch -c JIAO origin/JIAO
git checkout d-1f-assembly-area -- Assets/_Project Assets/XR Assets/XRI docs README.md
# 编辑 Packages/manifest.json，在团队清单上补：
#   inputsystem 1.7.0 / xr.core-utils 2.2.3 / xr.interaction.toolkit 2.5.4 / xr.management 4.4.0 / xr.openxr 1.9.1
git commit -m "feat(jiao): 1F assembly area scene + floor prefab and Module D scripts on top of team assets"
git push -u origin JIAO
```

本地 `d-1f-assembly-area` 分支保留着完整开发历史，需要回溯时切回去看。

## 下一步

1. Unity 里打开本工程（现在是 JIAO 分支，素材约 618 MB，首次导入会久一点）
2. 等包解析完成后：**Player → Active Input Handling = Input System Package (New)**
3. **Harbourview → Apply URP Settings**，再 **Check Project Setup** 看各项 `[OK]`
4. Unity 会重写 `Packages/packages-lock.json`，这个改动单独提交一次即可
5. GitHub 网页：Pull requests → New → base `main` ← compare `JIAO` → Create（或 GitHub Desktop：Branch → Create pull request）

# Git / GitHub 协作规范

COMP5424 Group 11 · XR-Fire-Evacuation
适用对象：负责仓库的同学 + 全体组员。目标是把现在「打包 650 MB zip 互传」的方式换成可回溯的版本管理。

---

## 0. 当前状态清单（本机已完成）

| 项目 | 状态 |
|------|------|
| 本地仓库初始化 | 已完成（`XR-Fire-Evacuation/.git`） |
| `.gitignore`（排除 Library/Temp/Logs/UserSettings 等） | 已配置 |
| `.gitattributes`（LF 换行 + Git LFS 规则） | 已配置 |
| `git lfs` 过滤器 | 需每台机器执行一次 `git lfs install` |
| Git 身份 user.name / user.email | **每台机器必须自己配**（见第 1 节） |
| 远端 GitHub 仓库 | 待创建（见第 2 节） |

> 仓库根目录是本工程目录本身（`XR-Fire-Evacuation/`），**不要把它外层的 `group11/` 整个初始化成仓库**——那里放着两个 600 MB 的 zip，一旦纳入 git 就再也甩不掉。

---

## 1. 每台机器的一次性配置

```bash
git config --global user.name  "你的 GitHub 显示名"
git config --global user.email "你的 GitHub 提交邮箱"
git lfs install
```

如果只想对本仓库生效（不想改全局配置），去掉 `--global` 在工程目录里执行即可。

---

## 2. 建立远端仓库并推送（负责仓库的同学执行）

### 方式 A：网页创建（推荐，最省事）

1. GitHub → New repository
   - Repository name：`XR-Fire-Evacuation`
   - Visibility：**Private**（作业到公开前夕再改 Public，避免被判自我抄袭）
   - **不要**勾 Add README / .gitignore / license（本地已有）
2. 创建后在工程目录执行：

```bash
cd "D:/NOW/PolyU/1st Sem/COMP5424 XR/group11/XR-Fire-Evacuation"
git remote add origin https://github.com/<owner>/XR-Fire-Evacuation.git
git branch -M main
git lfs push --all origin      # 先把 LFS 对象推上去（如有大二进制才需要）
git push -u origin main
```

首次 push 会被要求登录，选 **浏览器登录** 即可，之后 Windows 凭据管理器会记住。

### 方式 B：命令行创建（需要装 GitHub CLI）

```bash
gh auth login
gh repo create XR-Fire-Evacuation --private --source=. --remote=origin --push
```

---

## 3. 分支约定（组长的《场景建立》五步）

组长要求的工作流：

1. 建自己的新分支
2. 基于 Henry 的分支/素材取素材
3. 创建自己的场景
4. **在自己的分支上随便改、随便 commit / push**，不会影响别人的场景
5. **楼层 Prefab 做完后，用 GitHub Desktop 提交 Pull Request 合并回 main**

对应到命令行（GitHub Desktop 用户直接看 3.2）：

### 3.1 命令行流程

| 分支 | 用途 |
|------|------|
| `main` | 只放能打开、能跑的版本。任何人不得直接 commit 到 main |
| `feature/<module>-<内容>` | 每人/每模块一条，例如 `feature/d-assembly-trigger` |
| `hotfix/<内容>` | 演示前的紧急修复 |

流程：`main` → 拉 `feature/xxx` → 提交 → 合并回 `main`（推荐 PR，至少要有 1 人看过 diff）。

```bash
git switch main
git pull
git switch -c feature/d-assembly-trigger
# ... 干活 ...
git add -A
git commit -m "feat(d): add assembly area endpoint trigger"
git push -u origin feature/d-assembly-trigger
```

---

### 3.2 GitHub Desktop 流程（推荐给不熟命令行的同学）

1. **File → Clone repository** → 选组长建的远端 → 克隆到本地
2. 左下角 **Current branch → New branch**，名字用 `d-1f-assembly-area`（个人分支，随便取号但别带空格）
3. 代码/场景改完后回到 Desktop：左下角 Summary 填 `scene(d): add 1F assembly prefab`，点 **Commit to d-1f-assembly-area**
4. 右上角 **Push origin**（这一步是你的分支独有，绝对不会动 main）
5. Prefab 做完要合并：切回 `main` 分支 → **Branch → Create pull request** → 填说明 → Create → 交给组长 review 合并

> ⚠️ **合并前先 pull main**（Fetch origin → Pull），否则 PR 里会出现一堆和别人重复的旧提交。

## 4. 提交信息规范

`<type>(<module>): <中文或英文简述>`，type 用：`feat` / `fix` / `docs` / `refactor` / `perf` / `scene` / `test`。

例：
```
feat(d): implement GeoFence dwell trigger for T12
fix(d): endpoint fires before player reaches the zone
scene(c): import low-poly building model, replace facade placeholder
docs: update evaluation plan to v1.2 revisions
```

---

## 5. 场景文件冲突（最重要的一条）

`.unity` 和 `.prefab` 是 YAML，**两个人同时改同一个场景必然冲突**，且手工解冲突几乎不可能。

处理办法：

1. **任一时刻每个场景只有一个人在改**，改完立刻 push。这是最有效的办法。
2. 配置 Unity 自带的合并工具（一次即可，每台机器）：

```bash
git config --global merge.unityyamlmerge.name "Unity SmartMerge"
git config --global merge.unityyamlmerge.driver "'D:/Program Files/UnityProject/Editor/Editor/Data/Tools/UnityYAMLMerge.exe' merge -h -p %O %B %A %A"
git config --global mergetool.unityyamlmerge.cmd "'D:/Program Files/UnityProject/Editor/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
```

3. 冲突后：`git mergetool` → 自动尝试合并 → 失败的部分在 Unity 里打开肉眼确认。
4. 不要把整个文件夹打包 zip 再塞进来替换——那会让上面的所有配置失去意义。

---

## 6. 大二进制素材的提交流程

1. 素材放到 `Assets/Vendor/<来源>/`，不要混进 `_Project/`。
2. `.png/.fbx/.wav/.mp4/.unitypackage/...` 已写进 LFS 规则，直接 `git add` 即可自动走 LFS。
3. 提交前确认它确实被 LFS 接管：`git lfs status`，看到 `(Git: <size> -> LFS: <size>)` 就没问题。
4. **单文件超过 100 MB 的素材先压缩/降采样**——GitHub 免费账号对大文件有网络和存储配额。

---

## 7. 每日开工 / 收工

```bash
git switch main && git pull        # 开工
git add -A && git commit -m "..."  # 收工，至少每天一次
git push
```

**收工不 push = 当天工作丢失风险。**

---

## 8. 提交前自查

- [ ] Unity 里能正常打开，Console 无红色报错
- [ ] 没有把 `Library/`、`Temp/`、`Logs/` 加进来（`.gitignore` 已挡住，但如果用了 `git add -f` 会绕过）
- [ ] 没有把 zip 包加进来
- [ ] 场景改动前确认没有别人正在改同一个文件
- [ ] 行为日志（`session-logs/`）没有被提交

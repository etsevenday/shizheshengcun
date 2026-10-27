---
name: git-watch
description: 定时检查 main 上别人（策划 dotpad1994 经 Codex）推的新提交，按改动内容匹配现有流程（/design-dev 新策划包、策划答复待确认问题、旧策划案修订、纯文档整理），自动跑完流程并推送上线；需要策划确认的写进待确认问题文档一起推，提交说明标明在哪，再在 GitHub 开 issue @dotpad1994 提醒。本机定时任务每 10 分钟触发（无新提交时由预检 hook 直接拦下）；用户想手动触发时可在侧栏 Scheduled 点"Run now"，或说"/git-watch""检查一下 git 有没有新提交"。
---

# git 新提交自动处理

用户 2026-10-27 定的规则：**跑完流程就推**；需要确认的列进文档一起上传，提交说明里标明哪些要确认、在哪个文档；然后给 dotpad1994 发消息提醒。本技能是 `/design-dev` 的"无人值守版"：凡 `/design-dev` 里写"问用户/等用户点头/用户拍板"的地方，按下面第 4 节改成"写进待确认文档、按最保守做法继续"。

## 0 约定

- 仓库 `E:\Workspace\shizheshengcun`，远端 `etsevenday/shizheshengcun`，分支 `main`。本方 git 作者 `etsevenday`；策划 GitHub 账号 `dotpad1994`（仓库协作者）。
- `/design-dev` 第 0 节全部约定照旧（数值只来自策划案、不自补；只 add 自己改的路径；不强推；不碰留言板相关文件；压缩包不入库；代理不传 `model`、不用 Fable）。
- 无人值守：不用 `AskUserQuestion`、`EnterPlanMode`/`ExitPlanMode`、`start_session`；开发与试玩一律用 `Agent`（`general-purpose`，不传 `model`，传 `effort: "high"`：用户 2026-10-27 要求定时任务用 Opus 5.5 高）。
- 待确认问题的**唯一存放处**：`doc/策划案/系统策划总册 v1.0/00 总则/02 开发方待确认问题.md`（路径固定，不新建别的问题文件）。
- 状态文件 `.git/claude-git-watch.json`（在 .git 里，不会入库）：`{"lastSeen": "<已处理到的 origin/main 提交>", "runningSince": "<ISO 时间或 null>", "notifiedHead": "<上次因受阻而通知用户时的 origin/main 提交>"}`。
- 凡是"通知用户、停下、`lastSeen` 不更新"的情况，同时把 `notifiedHead` 记为当前 origin/main；下一轮 origin/main 仍是这个提交就安静结束、不再重复通知，等用户处理或策划再推新提交。

## 1 加锁与检测（没事就安静退出）

0. **预检**（省 token）：定时任务提示词带 `[git-watch-scheduled]` 标记，本机 `.claude/settings.local.json` 的 UserPromptSubmit hook 会先跑 `python tools/git-watch-precheck.py --hook`：结论为 IDLE（锁未过期 / origin/main 已通知过 / 无新提交）时直接拦下提示词，模型不启动；WORK 或 ERROR 时把预检结论作为附加上下文放进来。上下文里已有 WORK 结论 → 下面第 2 步的 fetch 和列提交可以省掉，直接用它；没有（hook 没生效、手动触发）→ 先跑一次 `python tools/git-watch-precheck.py`，首行是 IDLE 就回一句结论结束，不用往下读。每次 hook 结论记在 `.git/claude-git-watch.log`。预检只读、不加锁，WORK 时仍从第 1 条开始加锁。
1. 读状态文件。`runningSince` 非空且距今不到 6 小时 → 上一轮还在跑，直接结束，不通知。否则写入当前时间作为 `runningSince`。**本轮无论怎么结束，最后都要把 `runningSince` 清回 null**（第 6 节）。
2. `git fetch origin`。列出新提交：`git log --format='%h %an %ad %s' <lastSeen>..origin/main`，只看作者不是 `etsevenday` 的。**不要用 `HEAD..origin/main`**：别的会话可能已把策划提交拉进本地 HEAD，那样会漏掉。没有 → 清锁，结束，不通知、不汇报。
3. 本地有正在进行的工作就不碰：`git log origin/main..HEAD` 有未推送提交，或 `git status --porcelain` 里有已跟踪文件被改（未跟踪文件如 `doc/策划案.7z` 不算）→ 说明别的会话正在干活。`PushNotification`："git 有策划新提交 <短号>，但本地有未推送/未提交的工作，没自动处理"，清锁，结束（`lastSeen` 不更新，下轮再试）。

## 2 分类（按提交改到的文件判断，可以同时命中几类）

先 `git show --stat <每个新提交>`，再逐个看 diff。

| 类别 | 判断 | 处理 |
| --- | --- | --- |
| X 越界 | 改到 `doc/策划案/` 以外（代码、`data/`、`web/`、`.github/`、`AGENTS.md` 等） | **不合入、不处理**。`PushNotification` 告诉用户哪几个提交改了哪些文件，清锁结束，`lastSeen` 不更新 |
| A 答复 | 改了待确认问题文档里的"策划答复"栏（从"待定"变成结论） | 第 3.1 节 |
| B 修订 | 改了已有策划案的规则或数值（S/R 系统案、旧版本文件） | 第 3.2 节 |
| C 新策划包 | 新增版本目录（`doc/策划案/<名称> v<号>/`）或大面积新增玩法 | 第 3.3 节 |
| D 整理 | 只是重排、改措辞、补说明，没有规则/数值变化（如 v1.0 重编） | 第 3.4 节 |

拿不准 B 还是 D：逐条比对改动句与当前实现（`data/config.v0.6.json`、`src/`），有任何一条要改代码就是 B。

合入：`git pull --rebase --autostash origin main`。冲突按 `/design-dev` 第 1 步第 2 条：策划原文以策划为准，本方【实现同步】与修改记录两边保留；两边改了同一句拿不准 → `git rebase --abort`，通知用户，清锁结束。

## 3 各类处理

### 3.1 A 策划答复了待确认问题

对每条有结论的 Q：
- 结论与现状一致（"保持现状""采纳现行口径"）→ 只改文档：把相关【实现同步】处按结论整理（原文就地改成与实现一致的说法或保留注释并注明"已经策划确认"），在待确认文档该 Q 下加一行 `处理结果：已处理（日期，提交 <短号>）：……`。
- 结论要求改行为 → 按 `/design-dev` 第 8 节第 2 条走小流程：开发代理改代码（一次提交，测试全绿）→ 有界面或玩法变化就起试玩代理复验 → 第 5 节推送。然后同上更新文档与"处理结果"。
- 策划写了结论但含义不清、或引出新数值空白 → 不猜，在该 Q 下追加 `开发方追问：……`，算作本轮的新待确认项。

### 3.2 B 修订已有策划案

按 `/design-dev` 全流程，但规模按改动大小裁剪：小改（几条规则/数值）不用两个 Explore 代理，自己读代码定改法，直接交一个开发代理；试玩代理只走改动涉及的路线并做一轮快速回归。新规则推翻了旧【实现同步】时按新规则做，修改记录里写明。

### 3.3 C 新策划包

完整走 `/design-dev` 第 1–6 步，按第 4 节的无人值守改法。计划写在 scratchpad 的 `plan.md`（不进 plan mode），里程碑编号接着最新开发计划往下排，开发代理照常写 `doc/开发计划 v<下一号> <策划包名>.md`。

### 3.4 D 纯文档整理

不改代码。对照当前实现给新文本补【实现同步】与文末修改记录（做法照本仓库提交 49603af：只追加、不删改策划原句，每条写当前实际做法与出处，数值先核对 config/代码）。推翻现状、或含义不清的地方写成待确认问题。

## 4 无人值守改法（替换 /design-dev 里需要用户的地方）

| /design-dev 原来 | 现在 |
| --- | --- |
| 第 2 步用 `AskUserQuestion` 问阻塞空白 | 需要补数值才能做的那部分**不做**，写成待确认问题；其余照做 |
| 第 2 步 `ExitPlanMode` 等批准 | 计划写好直接开发 |
| 第 5 步"体验改进、策划案空白 → 列给用户拍板" | 不做，写成待确认问题（写清现状、可选做法、开发方建议） |
| 第 5 步"两轮复验不过 → 问用户" | **不推送**，本地提交保留，`PushNotification` 告诉用户卡在哪，清锁结束（`lastSeen` 不更新） |
| 第 7 步"用 wrap_up 问现在推送吗" | 满足推送条件就推（第 5 节） |
| 开发代理汇报里的"待确认" | 写成待确认问题 |

### 4.1 无人值守试玩

`preview_start`/`preview_stop` 已在本机 `.claude/settings.local.json` 放行（2026-10-27），定时回合里不用人点确认就能起服务器。

- 试玩固定用 `preview_start {name:"web-fb"}`（5190），开发代理手测用 `web`（5177）/`web-alt`（5180），两者别抢端口。试玩代理起不来时本会话自己试一次。
- 仍被拒（返回"declined"）或起不来 → 不重试、不推送；本地提交保留，文档同步可以先做好提交，`PushNotification`："<短号> 已在本地实现、测试全绿，试玩服务器起不来未复验，未推送"，清锁结束，`notifiedHead` 记当前 origin/main，`lastSeen` 不更新。
- 试玩手法：`?dev=1` 后菜单里有开发入口（测试箱含烤肉/水煮肉、放各种敌人、普通活性物质、跳日期等）；要触发暂停层，可在 `localStorage['survival.save.v1']` 里把 `Stats.Energy` 改到略高于低体力线再开行动；动存档前先另存一份到 `qa.backup.*`。批量连点时注意战斗结束后界面切回地块，同一坐标可能点到敌踪卡误接敌。
- 试玩只记录现象：违反策划案的交开发代理修（第 5 步规则），体验问题写成待确认问题。

写待确认问题的格式：在待确认文档"## 修改记录"之前新增一节 `## <日期> 来自 <策划提交短号>（<一句话主题>）`，编号接着文档里最大的 Q 往下（Q6、Q7……）。每条照 Q2 的样子写：总册写法 / 游戏现状 / 请确认（A、B 选项，A 为开发方建议）/ `策划答复：待定`。文末修改记录加一行。

## 5 推送条件与提交说明

**推送条件（全部满足）**：`dotnet build web/Survival.sln` 0 错误；`dotnet test web/Survival.sln` 全绿；改了玩法或界面时试玩复验通过（第 4.1 节，起不来就不推）；文档已按 `/design-dev` 第 6 步同步。只改文档的跳过前三条。

**提交说明**：本轮最后一个提交（通常是文档同步那次）的说明末尾、`Co-Authored-By` 之前必须有这一段：

```
待策划确认（N 项）：doc/策划案/系统策划总册 v1.0/00 总则/02 开发方待确认问题.md Q6–Q8
- Q6 <一句话>
- Q7 <一句话>
```

没有新问题就写 `待策划确认：无`。另起一行写 `来源：策划提交 <短号>…`。

**推送**：`git pull --rebase --autostash origin main`；拉到策划的新提交且改到本轮涉及的策划案文件 → 不推，通知用户，清锁结束。否则 `git push origin main`，然后 `gh run list --limit 1 --json databaseId,status,conclusion` 拿 run id，`gh run watch <id> --exit-status --compact`；失败读日志修，修不了通知用户。成功后 `curl -s -o /dev/null -w "%{http_code}" https://etsevenday.github.io/shizheshengcun/` 应为 200。

## 6 通知与收尾

1. **提醒策划**（本轮有新待确认问题，或处理完了策划的答复时）：用 `gh` 在仓库开 issue，指派并 @ 策划，GitHub 会给他发邮件和站内通知：

   ```bash
   gh issue create --repo etsevenday/shizheshengcun --assignee dotpad1994 --label 待策划确认 --title "待策划确认：<主题>（Q6–Q8）" --body-file <scratchpad 里写好的正文>
   ```

   正文：`@dotpad1994` 开头；一句话说明开发方已按哪次策划提交处理完并上线；列出每个 Q 的一句话与文档链接（`https://github.com/etsevenday/shizheshengcun/blob/main/<URL 编码后的待确认文档路径>`）；提交链接；最后写"请在文档里每条的'策划答复'一栏填结论后推送，开发方会自动处理"。标签 `待策划确认` 不存在时先 `gh label create 待策划确认 --repo etsevenday/shizheshengcun --color D93F0B`。
   处理完策划的答复后：在对应 open issue 下 `gh issue comment` 写明每个 Q 的处理结果；全部 Q 都已处理就 `gh issue close`。用 `gh issue list --repo etsevenday/shizheshengcun --label 待策划确认 --state open` 找 issue。
2. **告诉用户**：`PushNotification` 一行：处理了哪个策划提交、是否已上线、几项待策划确认（例："策划 82151fb 已处理并上线，3 项待策划确认，已开 issue #5"）。最终消息按 `/design-dev` 第 7 步的四段写。
3. 更新状态文件：`lastSeen` = 本轮处理到的 origin/main 提交号（只有成功处理完才更新），`runningSince` = null。
4. 流程有新经验写进记忆 `git-watch-automation.md`。

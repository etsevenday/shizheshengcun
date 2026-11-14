# BM5c 探索遭遇时序

批次 BM5c 探索遭遇时序与入战清窗口（2026-11-14）。需求 R220（1c227ab 答 Q605：真实入战清窗口；探索自身随机突遇与原地到期分流）、R231（bb917e6 答 Q620：成功开工的探索覆盖开工前已到期的敌踪，先完成再检查）、R232（bb917e6 答 Q621—Q624：随机遭遇日志三态与暂停原因、探索预览五句、系统说明两段、旧在办探索说明位置）。

## BM5c.1 来源与范围

规则与玩家可见文字只取自以下来源：

- R05 文首「2026-11-14 BM5c Q620—Q624：先完成本次探索，再处理原有到期敌踪」全文与验收 Q620-A01—A04、Q621-A01、Q622-A01、Q623-A01、Q624-A01；R05「2026-11-14 Q605用户新增：探索随机突遇与在地到期分流」全文。
- S14 文首「2026-11-14 BM5c／BM5a Q620—Q628」§Q620—Q624；S14「2026-11-14 Q605：正式入战清窗口，探索随机与到期分流」全文与验收 Q605-A01—A08。
- S01、S02、S03 文首「2026-11-14 Q620」「2026-11-14 Q605」两节；R03 文首「Q620／Q621／Q628」「Q605」两节；05 文首「Q620—Q628最新流程」「Q605」两节。
- 《重墨 V2 开工裁定与界面逐字稿》「2026-11-14 Q620—Q628完整逐字补齐」「2026-11-14 Q605：探索遭遇与全局入战清页逐字」。
- 《开发方待确认问题》Q605、Q620—Q624 的策划答复。

本批新出现或改动的玩家可见文字全部照抄来源：

| 文字 | 来源 |
| --- | --- |
| 探索中遭遇敌人，即将进入战斗。（短提示，及完成边界命中的日志全文） | R05 Q621 表「探索在本段已经完成」；S14 Q605「探索随机短提示」 |
| 探索中遭遇敌人，即将进入战斗。{行动名}已暂停，战后可以继续或退出。／……战后可以继续。 | R05 Q621 表两行 |
| 探索遭遇（暂停原因、战报身份） | R05 Q621「未完探索暂停原因固定『探索遭遇』」；R05 Q605 节 |
| 首战胜利前不会触发探索随机遭遇。／从第{n}日起，探索途中才可能触发随机遭遇。／安全区不会触发探索随机遭遇。／城市与公共地点不会触发探索随机遭遇。／当前已有一组敌踪，本次探索不会触发探索随机遭遇。 | R05 Q622 表 |
| 本次探索途中随机遭遇概率{概率}（本地敌对{数量}/上限{上限}）；随机遭遇会短暂提示后直接进入战斗。 | R05「探索正常风险行改……」 |
| 系统说明「敌踪与遭遇」新第2段（探索段正式全文） | R05 Q623「探索段正式全文」 |
| 系统说明「敌踪与遭遇」新第3段（清理段：原第2段删「探索或」） | R05 Q623「清理段正式全文」前半；段末见 BM5c.4 |
| 本次探索沿原开工规则，在结束时检查随机遭遇。 | R05 Q624 |

不在本批：Q625—Q628（BM6 对白压暗与淡入淡出）、Q606／Q610、Q636、R09 濒死、UI 重做。

## BM5c.2 工程映射与里程碑

| 需求 | 实现位置 |
| --- | --- |
| Q605 唯一检查段（内核） | 新 `src/Core/Engine/GameEngine.ExploreEncounter.cs`：`LockExploreCheck` 在 `Start` 锁好余量、放进主行动位之后调用（`GameEngine.cs` Start），按 `EstimateEndMinute`（与中央条／正式预览同源的完整模拟）算 N，在 1…max(1,N−1) 用 `s.Rng` 取 K；模拟给不出结束时用 `FallbackWorkMinutes`。`ActionState` 新字段 `EncounterSegment`（可空）、`EncounterChecked`、`EncounterPending`。 |
| Q605 检查与入战（内核） | `GameEngine.Segment.cs` Advance：探索完成走 `ExploreEncounterCheck(s, a, true)`（不再走 `CheckRandomEncounter` 出组卡），未完成且已提交满 K 段走 `ExploreCheckDueMidway` → `ExploreEncounterCheck(s, a, false)`。命中后 `StartExploreEncounter`：`AddGroupCard`（来源 explore）、计时记为已到期、未完运行中的同一次探索暂停（`PauseReason.ExploreEncounter`、`PauseNote` 探索遭遇）、写一条带 `GameEvent.EncounterPrompt` 的日志、`StartBattle`（来源「探索遭遇」、`FearEncounter`、`FirstStrikeEnemy`），`BattleState.ExploreEncounter` 标记本场。 |
| Q605 对白承接 | `ExploreEncounterCheck` 遇 `DialogueLocked` 记待处理（完成边界存快照 `GameState.ExploreCheckPending`）；`GameEngine.Story.cs` AckDialogue 在对白结束、原到期边界之前调 `ExploreCheckBoundary`。 |
| Q605／Q620 原地到期延后 | `GameEngine.EnemyApproach.cs` `EnemyApproachDeferred` 加 `ExploreDefersApproach`：主行动位是探索（运行、暂停、有补给／休息子行动）且队伍在其原地点／区域。结束边界沿原：完成在同一 Advance 的接近边界检查，取消（`Cancel`）、锁定中止（`AbortLocked`）、战后（`AfterBattleBoundary`）各自原有 `EnemyApproachBoundary`。 |
| Q605 旧在办 | `LegacyExploreCheck`：探索缺 `EncounterSegment`、未检查 → 只在完成边界检查一次。`SaveMigrator` 49→50 只升版本号。 |
| 预览副本 | `GameState.SimCopy`（不进存档）在 `Preview.cs`／`CookWait.cs` 的估时副本上置真，副本里新开探索 K 记 1、不嵌套估时、不推进随机。 |
| Q621 | `StartExploreEncounter` 三态拼句；`GameEngine.Progress.cs` `PauseText` 加探索遭遇。 |
| Q605 清窗口（壳） | 新 `web/Survival.Web/Services/GameSession.EncounterPrompt.cs` `OnBattleOnset`：`Notify` 里在 `ReconcilePanelChain` 之前，新 `BattleId` 出现就 `CloseAllPanels`、清 `Pop`／点火菜单／拖拽预览／食饮草稿。 |
| Q605 短提示（壳） | 同文件 `ShowEncounterPrompt`：`Drain` 见 `EncounterPrompt` 记待播；`RunLoop` 末尾进战斗前、`AckDialogue` 经 `PromptThenBattle` 先播；时长 `SingleSegmentShowMs`，`FxPaused`（菜单／说明）时不计时；结束写 `_fxEndedMs`（Enter 不穿透）；`BattleLoop` 在提示期间等待；`ResetUi` 清待播（读档不补播）。`Components/BattleScreen.razor` `.enc-prompt` 层（菜单之下）与 `lockedUi`；`Components/GameScreen.razor` OnKey；`wwwroot/js/app.js` 播放层按键选择器加 `.enc-prompt[data-active]`；`wwwroot/css/battle.css` 样式与减少动态效果。 |
| Q622 | `GameEngine.EnemyCards.cs` `GroupCardPreviewNote` 按 `type == Explore` 分支。 |
| Q623 | `Services/HelpCatalog.cs` 常量 `ExploreEncounterHelp`，「敌踪与遭遇」段序改为 1、探索、清理、其余。 |
| Q624 | `ActionProgressInfo.LabelNote`（`LegacyExploreNoteFor`）；`Components/CentralProgress.razor` 行动名在有说明时为按钮（title、读屏名、点击展开同一句），暂停层嵌同一组件。 |
| 用例 | 新 `web/Survival.Core.Tests/BM5cExploreEncounterTests.cs`（16 项）、`BM5cShellTests.cs`（5 项）；B41b／B41e3 原“探索被到期卡打断”用例与 R618／B523 改用清理；B41a／B41a2 预览句、BM5b1 帮助检查按新句更新；schema 断言 49→50；COMP20 驱动守卫战前换装。 |

里程碑：

| 提交 | 内容 |
| --- | --- |
| M874 `f892f5d` | R220／R231 内核：唯一检查段、探索遭遇被动战、同次探索延后原地到期、Q621 日志与暂停原因、旧在办兼容、schema 50。 |
| M875 `7824e63` | R220 网页壳：真实入战清窗口、探索遭遇短提示。 |
| M876 `4c3ad59` | R232：Q622 预览五句与风险行、Q623 帮助两段、Q624 行动名说明。 |
| M877 `9a28595` | 内核补：中途命中时探索已停着则不改原暂停原因，日志仍按未完态句。 |
| M878 | 本开发记录、策划案【实现同步】与修改记录、02 处理结果与新待确认问题。 |

## BM5c.3 开发假设

以下为代码结构、命名、排版与交互层面的自定，不涉及规则、数值或新玩家文字：

1. 检查段 K 存在行动状态上、随存档；已检查／待处理用两个布尔字段，旧在办用“检查段为空”识别。
2. 估时副本用不进存档的标记区分，副本里不为新开探索再做一次完整估时。
3. 短提示层盖在战斗画面之上、战斗菜单之下，居中一块不透明底框，点任意处跳过；入场有短淡入，减少动态效果时静态显示。
4. Q624 触屏入口：原行动名没有说明入口，按 R05 Q624【交开发方定】做成行动名本身可点（虚线下划线），点一下在名下展开同一句，再点收起；鼠标悬停读 title，键盘聚焦读屏读“行动名。说明句”。展开状态只在组件内。
5. 清窗口在每次刷新前按场次号判断，连场换场次时也照清（战斗中顶栏页本就打不开）。

## BM5c.4 实施记录与偏差

- K 的“开工时真实预计分钟”取中央条同源的完整模拟到结束；模拟途中终局等给不出结束时，按本段倍率与剩余需求工作量折算，只用于选检查段。
- 实际完成早于第 K 段（例如中途状态变化让工作提前做完）时，按 R05 在实际完成边界检查一次，成果先交。
- 中途检查在本段提交之后、状态提醒之前进行，此时探索通常仍在运行；若对白承接后接续检查时探索已因别的原因停着，不改原暂停原因、不再写第二次暂停，日志仍按未完态句（探索确未完成）。
- 完成边界命中的随机战与其他到期卡：先入随机战，战后由原战后边界检查到期卡；组卡吸收的成员原单卡不再独立排战（Q605-A05）。
- 「该敌踪已到期，将在本次探索结束后尝试接近。」（R05 Q620 末段、R05 Q605 §5）未接入：与 Q639 无倒计时（“不显示据剩余量变换的阶段提示”，已到期等待者统一「可能主动接近。」）冲突，写 Q656。被延后的卡仍显示「可能主动接近。」。
- Q623 清理段原文段末是“你可以先做其他事，接近倒计时会随同地推进的游戏时间减少。”，与 BM5b1 按 Q639 改的「你可以先做其他事。」＋原行动帮助句不一致；本批只删「探索或」，段末保持 Q639 现状，写 Q657。探索段原文里的“主动接近时间即使在开工前已经到期”按原文保留（规则说明、不含时长），BM5b1 帮助无倒计时检查排除这一句。
- Q605-A01 “焦点在战斗可操作时回其合法安全起点”沿原战斗界面焦点规则，本批未另改。
- COMP20 端到端：开工多一次抽签改变了随机序列，科学院线先种子 1 在地城连战里木矛耗尽、剩下后排敌人谁都够不着而僵持（脚本策略不足，不是规则错误）；驱动在守卫战前像出发前一样换新耐久不足一半的穿戴装备（记入账本开发入口）。

## BM5c.5 试玩修复

（待线上试玩后填写）

## BM5c.6 线上复验结论

（待线上复验后填写）

## BM5c.7 遗留

- 新问 Q656（被探索延后的敌踪状态句与 Q639 冲突）、Q657（Q623 清理段段末与 Q639 冲突），见 02「2026-11-14 来自 bb917e6 BM5c（探索遭遇时序）」；对应部分未做。
- Q625—Q628 属 BM6，未做。

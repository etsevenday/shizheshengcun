# BM4a 顶栏覆盖与逐层返回

批次 BM4a（2026-11-13，次要问题攒批）。需求 R212（Q586 选 B、Q590 用户自定，策划提交 ac39e93）、R205（Q552—Q555 均选 A，策划提交 fc76504）：顶栏面板覆盖打开、保留下层原窗口并逐层返回，加工制作「查看来源」返回原页并重验；成长页练习盘 ESC 先收盘；可选强敌悬停行序／前缀与撤退预览口径核对。

## BM4a.1 来源与范围

规则与玩家可见文字只取自以下来源：

- S14「2026-11-13 B45 Q581—Q590试玩答复与下方对白布局」§Q586、§Q590 正文及验收 Q586-A01—A02、Q590-A01—A03。
- 《重墨 V2 开工裁定与界面逐字稿》「2026-11-13 B45 Q581—Q590试玩定稿与对白／右栏／逐层返回」表中 Q586、Q590 行及表后两段（含系统说明新增段落）；「2026-11-12 B41e2 Q552—Q555 补充逐字稿」。
- 《开发方待确认问题》Q552—Q555、Q586、Q590 的答复。

本批新出现的玩家可见文字全部照抄来源：

| 文字 | 来源 |
| --- | --- |
| 「返回」／「关闭」（与原 ESC 键帽组成「返回 ESC」「关闭 ESC」） | 逐字稿 Q590 行 |
| 「这处设施已无法操作，请重新选择。」 | 逐字稿 Q586 行（内核 `GameEngine.FacReturnGoneText`） |
| 「内容已变化，请重新选择。」 | 逐字稿 Q582／Q586 行（原句 `ContentChangedText`） |
| 系统说明「ESC 与界面层级」新增段落「在当前允许打开的顶栏面板之间切换时，……文字输入、对白和其他锁定仍优先。」 | 逐字稿同日节表后正式段落（`PanelReturnChain.EscHelpParagraph`） |
| 练习盘「返回 ESC」 | B41e2 补充逐字稿「练习盘返回业务名＋当前层键帽」（原「返回」＋原 ESC 键帽） |

不在本批（属 BM4b）：Q589 右栏重排、Q585 下方对白布局、Q528—Q530 战斗瞄准右列。

## BM4a.2 工程映射与里程碑

| 需求 | 实现位置 |
| --- | --- |
| Q590 返回链层序 | `src/Core/Engine/PanelReturnChain.cs`（新）：只记被盖住的下层；`Cover` 覆盖打开（同类页已在下层则提到最上层、不复制）、`Pop` 一次一层、`Remove`、`Clear`；`ReturnLabel`／`CloseLabel`、`EscHelpParagraph`。 |
| Q590 覆盖打开与逐层返回 | `web/Survival.Web/Services/GameSession.PanelStack.cs`（新）：`OpenTopPanel`（当前最上层就是它＝关闭返回下层）、`CloseAllPanels`、`PopRestorable`／`PanelRestoreBlock`／`RestorePanel`（逐层重验）、`DropPanelState`、`ReconcilePanelChain`（Notify 时：无面板、终局、战斗、地点或区变化即清链）、`PanelCloseLabel`、`CardPopHeldUnderPanel`。<br>`GameSession.cs`：`OpenPanel` 改由 `SwitchPanel(panel, recipe, cover)` 实现——各面板的草稿只在该面板真正离开整条链时清（加工菜单 `LeaveFireMenu`、制作 Craft* 与选择设施父链、地图来源查询、编队草稿、练习盘、整备交接）；恢复层不重新初始化、不再给操作前引导。`OpenHelp`／`CloseHelp` 改走返回链（去掉 `_helpReturn`）；`OpenFormation` 覆盖打开；`ResetUi` 清链。<br>`GameScreen.razor`：顶栏 `Toggle` → `OpenTopPanel`；编队在下层时提上来；`.panel-host` 加 `data-panel`；渲染后恢复层调 `survivalPanels.restore`、新开层调 `forget`。<br>`app.js` `survivalPanels`（新）：按面板记滚动区位置与最后获得焦点的控件，恢复时取回，失效回该层标题。 |
| Q590 关闭控件文字 | 地图、制作、休息、日志、目标、提示、对话记录、系统说明、成长、整备、远行准备、城市事项、晾制架、火堆加工菜单的原「关闭」改 `@G.PanelCloseLabel`（制作经选择设施进入且无下层时仍「返回」）；编队「取消」、安全屋「返回」、鸟瞰「返回当前地点」不改。 |
| Q590 清链 | 真实出发（地图移动 `ActionPops`、鸟瞰前往 `CityGo`）、休息开工、练习开工、开发传送改调 `CloseAllPanels`；`CloseFireMenuForAction` 在菜单处于下层时同样收起；其余原规则直接 `Panel = null` 的收盘由 `ReconcilePanelChain` 清链。`ReconcileFireMenu`、`ReconcileGear` 在本面板处于下层时保留上下文。 |
| Q590 物品操作盘 | 从无面板画面覆盖打开时记下正显示的物品操作盘（`_cardPopBase`）；被盖住期间 `CardPop.razor` 同游戏菜单（Q390）只加 `menu-hidden` 隐藏、不卸载，盘内数量保留；整条链按返回关完、选中未变时原样再显示。 |
| Q586 查看来源 | `OpenSources` 改为 `SwitchPanel("map", null, true)`：地图盖在制作页上，关地图（关闭、ESC、M）按返回链恢复制作页；删除旧快照 `_craftReturn`。来源列表「返回制作」：制作页紧挨在下时同关闭，否则提到最上层。`PanelEscape` 在制作页下面另有窗口时先退一层。 |
| Q586 重验 | 恢复加工菜单：原地点／区或火源已不在 → 不恢复，底部提示 `FacReturnGoneText`，继续看再下一层；仍在 → 右栏同步该设施，下一次核对按“非本菜单造成的变化”处理（来源被用掉或内容变化显示原「内容已变化，请重新选择。」并清掉，数量文本不动）。恢复制作页：经选择设施进入的加工站已不在可操作列表 → 同上提示不恢复；所指定批次／锅／目标／修补包已不存在 → 清掉并提示「内容已变化，请重新选择。」，份数不动。 |
| 系统说明 | `ShellRules.cs` `ShellKeyHelp.EscParagraphs` 第一段后加入 Q590 段落（共七段）。 |
| Q554 练习盘 ESC | `GameSession.Keys.cs` 新 `EscOwner.PracticeBack`（成长页且练习盘开着，排在一级面板之前）；`GameScreen` 承接时调 `ClosePractice`（与盘内「返回」同一入口）；`ClosePractice` 收盘后焦点回「练习潜行」入口（id `growth-practice`）；`GrowthOverlay.razor` 盘内「返回」带 `EscCap`。 |
| Q552、Q553、Q555 | 核对现状已符合（`EnemyCardHoverLines`、`EliteDropNote` 悬停与 `EnemyDetail.razor` 同源；`RetreatChance` 与 `BattlePanel` 撤退行都用不含先手的 `PlayerEffectiveSpeed`／`EnemyEffectiveSpeed`），只补用例。 |
| 用例 | `web/Survival.Core.Tests/BM4aPanelReturnTests.cs`（新，11 项）、`BM4aEnemyCardAnswerTests.cs`（新，3 项）；旧用例按新规则改写 8 处（BM4a.4）。 |

| 里程碑 | 提交 | 内容 |
| --- | --- | --- |
| M826 | bf9f9f4 | Q590 顶栏覆盖打开与逐层返回链、Q586 查看来源返回与重验、关闭控件文字、物品操作盘隐藏保留、系统说明段落；Q554 练习盘 ESC 先收盘 |
| M827 | c5f3d68 | Q552、Q553、Q555 核对用例 |
| M828 | （本提交） | 开发记录 |

## BM4a.3 开发假设（只涉代码结构、内部命名、排版与焦点）

1. 能叠进返回链的面板＝原 `IsTopPanel` 全部一级面板（顶栏各页及加工菜单、工坊、安全屋、晾制架、城市事项、远行准备、鸟瞰）；远征中止／封存、地牢重置等确认窗不叠，仍按原替换处理。
2. 覆盖打开的入口：顶栏 11 个按钮与对应快捷键（含编队）、制作「查看来源」、从详情／状态浮层定位打开的系统说明。其余面板内业务跳转（Q547「查看火堆」、目标页「远行准备」、地图「安全屋返程」、制作「打开晾制架」等）仍是替换：只换掉当前最上层，下层链保留；被替换跳转重新打开的同类页若在下层，下层那份作废。
3. 关闭链里某层时若它已失效，继续往下找下一层可恢复的（「回当前合法画面」）；加工设施失效的提示显示在底部提示位；编队草稿已不可编辑、鸟瞰已不可开时静默丢掉该层。
4. 「内容已变化」等恢复提示：加工菜单沿用菜单内原提示位；制作页显示在底部提示位。
5. 焦点：覆盖打开后焦点进新面板首个可用控件（沿 B32g `focusPanel`）；恢复层回该层最后获得焦点的控件，失效则回该层标题（加工菜单 `fm-title`，其余取第一个 h2／h3 并设 `tabindex=-1`）。滚动按面板里各滚动区的序号记忆。
6. 系统说明新段落放在「ESC 与界面层级」第一段之后。
7. 加工菜单虽不是顶栏页，其主层「关闭」也按有无下层写「返回／关闭」（它可能经 Q547 替换出现在其他窗口之上）。

## BM4a.4 实施记录与偏差

### 按新规则改写的旧用例

| 用例 | 原断言 | 现断言 |
| --- | --- | --- |
| `B31cShellTests.B31bLeftovers` | 开发传送后 `G.OpenPanel(null)` | `G.CloseAllPanels()` |
| `B31cShellTests.Q324_ProgressDocksUnderLocalDisk`、`B32dTests.Q390_A01_MenuHidesDiskKeepsState` | `menuHide = G.MenuOpen \|\| G.BattleMenuOpen ? …` | 再加 `\|\| G.CardPopHeldUnderPanel` |
| `B32dTests.Q378_A01_CoveredSourceClosesDisk` | CardPop 条件 `!G.CardPopCovered` | `(!G.CardPopCovered \|\| G.CardPopHeldUnderPanel)` |
| `B32gTests.Q401_A01_NewPanelAlwaysCollapsesCardPop` | 在 `OpenPanel` 里找收盘、`OpenHelp` 原条件 | 在 `SwitchPanel` 里找；`OpenHelp` 新条件 |
| `B43KeyHintTests.A03_A06_EscCapFollowsEscOwner` | 说明页「关闭<EscCap…>」 | `@G.PanelCloseLabel<EscCap…>` |
| `B45FacMenuShellTests.Q560_ChooserWiring` | `G.CraftFromChooser ? "返回" : "关闭"` | `G.CraftFromChooser && !G.PanelHasUnder ? "返回" : G.PanelCloseLabel` |
| `BM2KeyAnswerTests.Q501_A01_EscHelpVerbatim`、`B41c2ShellTests` | 六段、第 3 段下标 2 | 七段（新段在第 2 段）、原段逐字核对不变 |

### 实现中按正文取舍的地方

- **Q401 收盘的替代**：原「打开新面板一律收起物品操作盘」改为隐藏保留（同 Q390 游戏菜单的做法），关完整条链原样恢复；只在从无面板画面覆盖打开时适用。物品操作盘开着时四轨有遮罩、字母键本就不受理，实际入口是点顶栏按钮。
- **食饮说明**：从食饮盘进入的系统说明再被顶栏页覆盖时，食饮草稿仍按原规则丢弃（原 `OpenPanel` 一律清 `FeedPending`），说明页留在下层。
- **经选择设施进入的制作页被重排**：「制作（经选择设施）→M→C」后制作页在地图之上，此时它的返回先退到地图（一次一层），选择设施父链随之结束；制作页在链最底层时返回仍先回选择层（Q560）。
- **透明／隐藏窗口的暂停**：常态下一级面板没有阅读暂停计数（只有战斗菜单与战斗说明抽屉有），本批无需处理。

### 本地自测

本地开发服务器＋无头浏览器（`tools/playtest/cdp.mjs`），1280×720，控制台无错误：

- **Q590-A01**：火堆菜单进加工位1、选来源金属锅（数量框 1）→ M 地图（关闭键「返回 ESC」）→ 点「手工制作」→ M（地图提到最上层）→ ESC 回制作 → ESC 回火堆加工位1层，来源仍选中、数量 1、「放入加工位1」可用，焦点回原控件 → ESC 回主层（焦点在加工位1卡）→ ESC 关菜单（焦点回火堆卡、关闭键「关闭 ESC」）。数量框里按 M 不打开地图。
- **Q586-A01**：制作选绷带 →「查看来源」→ 地图显示「草药的已知来源」、关闭键「返回 ESC」→ ESC 回制作，配方仍是绷带。
- **滚动**：制作配方列表滚到 120 → L 日志 → ESC，配方列表仍在 120。
- **物品操作盘**：选中地面木材，盘内数量改 2 → 点顶栏「行动日志」（盘隐藏、无遮罩）→ T 生存提示 → ESC → ESC，盘原样出现、数量仍 2。
- **Q554**：成长页选潜行 →「练习潜行」→ 盘内「返回 ESC」→ 选练习者 → ESC：盘收起、成长页仍在潜行门类、焦点在「练习潜行」、关闭键「关闭 ESC」→ 再 ESC 关成长页。

## BM4a.5 遗留

- 未在浏览器里造出的路径（仅单元测试与源码检查）：返回时加工设施已失效（「这处设施已无法操作，请重新选择。」）、下层期间来源被用掉、链中途进入战斗／移动换区／读档的清理。
- 无策划缺口：本批未发现需要开发方自补规则、数值或玩家可见文字的地方。

## BM4a.6 试玩修复

线上试玩（c6c8ae6）对照 S14 §Q590 发现两处违反、一处待判定。

| 编号 | 试玩发现 | 违反 | 修法 |
| --- | --- | --- | --- |
| B1 | 先开火堆菜单／制作／日志，再按 M 覆盖打开地图：焦点落在地图左上层级下拉框 `SELECT.map-layer`，按 B43 下拉框吞掉普通字母，M／L／C 全部无效，须先点空白处 | §Q590「再按当前已在最上层的顶栏快捷键，等同关闭该面板」、Q590-A02 | `app.js` `survivalPop.focusPanel` 选落点时跳过输入控件（下拉、文字框、数量框、可编辑区）；面板本身是确认区（休息、制作、编队、远行准备：`.sub-panel[data-enter-scope][tabindex]`）时焦点交给面板本身，Enter 仍归该区唯一确认（B43）；只有输入控件时回该面板标题。B43「下拉、文字框、数量框里普通字母不当快捷键」原规则不改。 |
| B2 | 点地面木材卡开物品操作盘 → ESC 收盘（焦点回木材卡）→ K／L：焦点仍留在被盖住的木材卡，其 `.focus-tip`（z-index 120）浮在成长页中间 | §Q590「仅最上层可见且接收焦点……下层不浮到新面板上面」「焦点进入新面板首个合法控件」 | `GameSession.SwitchPanel`：原只在覆盖打开（`covered`）时记 `PanelFocusPending`，改为凡新打开一级面板（覆盖打开或从无面板画面打开，`next != from && !restore && IsChainPanel(next)`）都记；渲染后 `focusPanel` 把焦点交进新面板，下层卡失焦、焦点提示随之收起。恢复层仍由 `survivalPanels.restore` 取回原焦点。 |

开发假设（只涉焦点）：「首个合法控件」取面板里第一个可用、非输入类控件；面板本身是确认区时取面板（避免落在第一个选项按钮上让 Enter 改选而不是确认）。地图首个落点为「缩小 −」（不可用时为「放大 +」），日志、成长等为头部「关闭／返回」按钮（同 BM4a 覆盖打开与 B32g 原做法）。

### E1 结论：进入战斗时最上层面板不关（沿旧行为）

试玩：「探索」进行中按 M 再按 L（地图→日志）→ 遇敌进战斗：战斗中 `.panel-host` 仍挂着日志，战斗结束回行动暂停层时日志仍开着，地图那层已清。

- 旧行为（`git show 12913cf^:`，BM4a 之前）：行动循环遇敌进入战斗只 `ResetBattleUi`、清 Pop 与选中，不动 `Panel`；`GameScreen.razor` 的 `.panel-host` 只按 `IsTopPanel(G.Panel)` 渲染、与战斗无关；唯一在战斗中收起的一级面板是火堆加工菜单（`ReconcileFireMenu` 的 `InBattleUi` 分支，现仍保留）。即旧版进入战斗时日志、地图等顶栏页本来就留着，战后仍开着。
- BM4a 只新增 `ReconcilePanelChain`：进入战斗清掉下层返回链，不关最上层。所以不是 BM4a 改坏，本次不改。
- 策划案缺口：§Q590 写「真正移动／换区、进入战斗、终局……时清理失效返回链，回当前合法画面」，没写清进入战斗时**当前最上层**的顶栏页（日志、地图等，非加工菜单）是否一起关闭——「回当前合法画面」可读作只丢返回链，也可读作连最上层一并关。可选：A 保持现状（只清下层，最上层沿旧行为留着，战后仍开）；B 进入战斗连最上层一起关，战后回行动暂停层／原画面；C 战斗中隐藏、战后只恢复最上层（下层仍丢）。待策划定，未按任何一种改。

### 用例与自测

- 用例：`BM4aPanelReturnTests.BM4a6_NewPanelTakesFocusButNotIntoTypingControls`（新）。`dotnet build web/Survival.sln` 0 错误，`dotnet test` 2426 项全绿。
- 本地开发服务器＋无头浏览器（`tools/playtest/cdp.mjs`，1280×720），控制台无错误：
  - B1：L→M（焦点「缩小 −」）→C（焦点制作面板本身）→M（地图提上来）→L（日志提上来，「返回 ESC」）→C → ESC 回日志 → ESC 回地图 → ESC 全关；火堆菜单→M→C→M→ESC 回制作→ESC 回火堆菜单→ESC 关菜单（焦点回火堆卡）。每步快捷键都生效，不再需要先点空白处。
  - B2：点地面木材开盘 → ESC（焦点在木材卡、焦点提示显示）→ K：成长页打开，焦点在成长页「关闭 ESC」，焦点提示已收起；→ L：日志覆盖，焦点在「返回 ESC」；ESC、ESC 逐层关完。
  - 从无面板画面 R：焦点在休息面板本身（Enter 仍归「开始」唯一确认）；M 再按 M 关闭。
- 未在浏览器里复现 E1 遇敌路径（未改代码，结论据源码）。

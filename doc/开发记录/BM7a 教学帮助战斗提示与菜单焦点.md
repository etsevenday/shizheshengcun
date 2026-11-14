# BM7a 教学帮助战斗提示与菜单焦点

批次 BM7a 次要合并：教学帮助战斗提示文字、加工菜单焦点、菜单 Tab（2026-11-14）。需求 R234（策划提交 bab3b48 答 Q629—Q635：首战附注、队友帮助两句、调息短反馈、敌人旁战斗信息浮框与右侧敌人属性、敌我出手不可跳过、调整站位不合法号位不可选、战斗信息与预测详情两层介绍）、R238（策划提交 153b858 答 Q644、Q645、Q655：移入锅去额外说明、来源行消失后的焦点、游戏菜单键盘层级）。同批 Q636（安全区残留清除）归 B48，Q646—Q654 归 BM7b，本批不做。

## BM7a.1 来源与范围

规则与玩家可见文字只取自以下来源：

- S14 文首「2026-11-14 Q629—Q636：敌人旁预估、双方不可跳过与安全残留清除」§Q632、§Q629／Q630／Q631／Q635、§Q633、§Q634 与验收 Q629-A01—Q635-A01；S14 文首「2026-11-14 Q644—Q655」§Q644／Q645、§Q655 与验收 Q644-A01、Q645-A01、Q655-A01。
- R03 文首「2026-11-14 Q629—Q636当前战斗裁定」；N02 附录文首「Q629／Q630／Q631」；S11 文首「Q644／Q645」；05 文首「Q644—Q655完整流程补齐」「Q629—Q636最新流程」。
- 《重墨 V2 开工裁定与界面逐字稿》文首「Q629—Q636正式文字与旧规则替换」表与其后说明、「Q644—Q655正式文字」表「移锅按钮／落点」行。
- 《开发方待确认问题》Q629—Q635、Q644、Q645、Q655 的策划答复。

本批接入的玩家可见文字（照抄逐字稿表）：

| 位置 | 原句 |
| --- | --- |
| tutorial_arrive 的 guide 附注 | 轮到我方某人时，可以使用他的行动点（AP）。先选技能，再把鼠标移到亮起的敌人上，看敌人旁的战斗信息浮框；右侧信息栏可看敌人详细属性，也可用←／→切换合法目标。看好后，点击亮起的目标，或按Enter执行。自用或无目标指令，选好后点击底部确认按钮，或按Enter确认；需要选择我方成员的技能，点击亮起的我方成员。用完行动点，或选“结束行动”后再确认，战斗就会继续。 |
| 「指令一览」末句 | 队友按实际编队参战，指挥、医疗等按技能范围选择对象；保护、掩护、整队、协同仍须满足各自的队友和站位条件。 |
| 「站位与目标」敌人选目标段末句 | 有队友参战时，他们也可能成为合法攻击目标。 |
| 调息成功短反馈 | {人物名}喘了口气 |
| 默认攻击瞄准引导（底部反馈位） | 把鼠标移到亮起的敌人上，看敌人旁的战斗信息浮框；右侧信息栏可看敌人详细属性。点击亮起的目标或按Enter执行，ESC取消。 |
| 合法敌人悬停说明 | 点击即对它执行所选技能；本次预估见敌人旁的战斗信息浮框，详细属性见右侧信息栏。 |
| 生命条「预计」条纹说明 | 预计剩余生命（未暴击；暴击预估见战斗信息浮框或预测详情） |
| 「指令一览」两层介绍 | “战斗信息”显示实际目标、成本、命中与暴击、伤害和目标状态；“预测详情”提供逐项来源。选好攻击技能后，悬停亮起的敌人可在敌人旁查看战斗信息浮框，右侧信息栏显示该敌人的详细属性。 |
| 「指令一览」战斗表现帮助 | 敌我出手表现均不可跳过。播放期间可按ESC打开菜单，关闭菜单后从原处继续。 |
| 贯穿／成对引导里的替换 | 「悬停看逐个预测」→「悬停看敌人旁的战斗信息浮框逐个预估」；「悬停看预测」→「悬停看敌人旁的战斗信息浮框」（逐字稿 Q632 说明） |
| 浮框标题 | 战斗信息（原「战斗信息」页签名） |
| 移入锅按钮／落点 | 移入锅（加工位{目标序号}）（原按钮字，落点改用同一句） |

## BM7a.2 工程映射与里程碑

| 需求项 | 位置 |
| --- | --- |
| Q629 首战附注 | `data/config.v0.6.json` tutorial_arrive 的 `note`；对白记录写展示时的 `cfg.Note` 快照（`GameEngine.Story.cs`，未改） |
| Q630／Q635／Q633 帮助 | `web/Survival.Web/Services/HelpCatalog.cs` 主题 actions（指令一览）与 targets（站位与目标）；`src/Core/Engine/ShellRules.cs` `ShellKeys.BattleNoSkipHelp`，键表撤两级跳过行 |
| Q631 调息短反馈 | `src/Core/Engine/GameEngine.Combat.cs` breathe 分支 `FxShort(s, BreatheShortText(...))`；`GameEngine.BattleFx.cs` `BreatheShortText` |
| Q632 浮框与右栏 | `web/Survival.Web/Services/GameSession.AimPop.cs`（`AimPopTarget`、`AimRailTarget`、`HideAimRail`、键盘目标记录）；`GameSession.cs` `BattleCycleTarget`／`InitAimTarget`／`FocusBattleTarget`／`HoverBattleTarget` 记清键盘目标；`Components/BattlePanel.razor` 新增 `Mode="pop"`（`PopView`、`MemberRows`、`KeyRows(p, allMembers)`）、`Mode="foe"`（`FoeView`），敌人公开属性抽成 `FoeFacts` 与未瞄准时的战斗信息页共用；`Components/BattleScreen.razor` 场景里 `.bfs-aimpop`、操作台里 `.bfs-aimrail`、`AimHint`／`AimDefaultHint`／`FoeAimTip`／`ForecastTip`；`wwwroot/css/battle.css` 文末 BM7a 段 |
| Q633 不可跳过 | `GameSession.Playback.cs`（撤 `FxAtResult`、`_fxSkipAll`、`PlaybackSkip`、跳过说明与结果帧缩短；`ClosingHoldEscape`）；`GameSession.cs` `ExecuteBattleCommand` 播放中／结束帧不提交、`StepPause`；`Components/BattleFxLayer.razor`、`GameScreen.razor`（`bfx-hold`、`OnKey` 的 Skip 与停留 ESC）；`battle.css` 撤跳过按钮与结果帧样式 |
| Q634 选位 | `Components/BattleScreen.razor` `AllyAnchor`（`swapOff` → tabindex／aria-disabled、选位期间空位悬停句）；`GameSession.BattleSwap.cs` `ClickSwapSlot` 不合法直接返回 |
| Q644 移锅说明 | `src/Core/Engine/GameEngine.MoveToPot.cs` 撤 `MoveToPotOption.Hint`／`MoveToPotHint`；`GameEngine.Drop.cs` 落点改 `MoveToPotLabel`；`Components/FireMenu.razor` 按钮去 title／aria-description |
| Q645 来源焦点 | `Components/FireMenu.razor` 来源行 `@key`＋`data-src-key`；`wwwroot/js/app.js` 文末「BM7a 焦点修复」`repairSrc` |
| Q655 菜单键盘层级 | `wwwroot/js/app.js` 按键路由 `.menu-modal` 分支（`trapTab`／`topMenu`、按住 ESC）与文末「BM7a 焦点修复」（打开记原焦点、收起回原位置）；`MenuOverlay.razor`、`ManualSavePage.razor` 标题 `tabindex="-1"` |
| 测试 | `web/Survival.Core.Tests/BM7aTeachingTextTests.cs`（4 项）、`BM7aBattleTests.cs`（5 项）、`BM7aMenuFocusTests.cs`（2 项）、`BM5b1FacMenuTests.BM7a_Q644_A01_NoExtraMoveSentence`（1 项）；按新行为改的旧测试见 BM7a.4 |

| 里程碑 | 内容 |
| --- | --- |
| M885 `9cd44cb` | Q629 首战附注、Q630 帮助两句、Q635 两层介绍、Q631 调息短反馈。 |
| M886 `0578046` | Q633 敌我出手不可跳过（撤两级跳过、播放层接住输入、结束帧不点穿、收束停留 ESC、战斗表现帮助）。 |
| M887 `5ff36cd` | Q634 调整站位不合法号位不可选、空位悬停句。 |
| M888 `4b7f130` | Q632 敌人旁战斗信息浮框、右侧敌人属性叠层、瞄准引导与悬停说明换句。 |
| M889 `10efc4b` | Q644 移锅去额外说明、Q645 来源行焦点修复、Q655 游戏菜单 Tab 循环与关闭回原位置。 |
| M890 | 本开发记录、策划案【实现同步】与修改记录、02 处理结果与新待确认问题。 |

## BM7a.3 开发假设

以下为代码结构、命名、像素排版、动效快慢等开发方自定，不涉及规则、数值或新玩家文字：

1. Q632 浮框位置：放在被悬停（或键盘指定）的合法敌人锚点左侧（朝中线一侧），右边离锚点 6 像素，顶边对齐立绘区（锚点顶 10u＋50 像素），不盖头顶名牌与脚下生命条。敌方四个号位都在中线右侧，左侧总有空间，故不做“靠屏边换另一侧”的翻转。单体宽 340 像素，多目标宽 560 像素、两列分列成员；最大宽度按场景宽度收。浮框 `pointer-events: none`，点击穿过到下面的锚点，自然“不遮目标点击”。
2. Q632 浮框内容：标题「战斗信息」＋原五项行（标签与精度照 `KeyRows`）；多目标时第 1 行写「受影响{数量}个目标」、第 2 行成本，其后每名真实受影响成员一块（名称，保护转伤时按原写法「{原目标}；实际承伤：{承伤者}」，再列命中与暴击、伤害（未暴击）、目标状态三行），不只拿悬停者代表全体。预演不成立时只写真实原因。
3. Q632 单体浮框出现条件：本次明确目标（`BattleAimTarget`）合法，且鼠标正停在它上面或它是最近一次由键盘（Tab 聚焦、←／→、初入瞄准的键盘合法焦点）指定的；鼠标真实移出敌人锚点即清键盘标记，所以“鼠标离开即撤”、静止的旧键盘焦点不会把浮框抢回来；键盘标记带瞄准上下文（技能、物品、行动者），换技能或重新瞄准后不兜底。群体技能按鼠标或键盘所在的那名受影响者定位浮框。
4. Q632 右栏：全屏战斗原本没有右栏，按“右栏按只读叠层呈现，不挤窄、换位或压缩人物”做成盖在操作台右块（通用行动区）上的叠层，宽度沿原 `--bc-side` 三档（300／330／380 像素）。叠层只在本次明确目标合法时出现，沿明确目标保留（鼠标移开不撤，同 BM2 Q497）；为了不挡住通用行动、医疗与「结束行动」，鼠标移进叠层即让开（之后约 400 毫秒留透明层接住紧跟着的点击，同 B34 灰卡说明做法），键盘焦点进入通用行动区时也让开，直到明确目标换人。头部用右栏「对象」页签字＋原 RailHead 紧凑表头（敌人小图、「{号位}号位 {敌名}」），内容与未瞄准时战斗信息页的敌人段同一段（`FoeFacts`）。
5. Q632 浮框、叠层不显示的情形：播放中、对白、探索遭遇提示、战斗菜单、只读子页（物资／装备／帮助／预测详情）、请求处理中、不是我方下令时；浮框不可聚焦（无 tabindex、不接点击与键），读屏按 `role="note"`、名称「战斗信息」。
6. Q633 收束停留（撤退成功等收束前的短反馈停留）期间所有组件冻结不重绘，无法在冻结画面上叠出菜单；停留期间按 ESC 先记下，停留自然结束（不缩短）后在真实情境打开菜单（仍在战斗界面开战斗菜单，否则开游戏菜单）。
7. Q645 稳定键：Q642 来源合行属 BM5b2 未做，当前一行就是一个物品实例，暂以实例编号作稳定键；BM5b2 合行后应改为来源组键。修复延后 60 毫秒复查，让 Q604 主动操作成功回相关工位的焦点先落定；整个来源区随内容页一起消失时不在本条处理，交 Q604 原规则。
8. Q655 关闭后的回退顺序：原控件仍在、可见、未禁用、不在 inert／hidden 里、没被更高层（确认窗、数量框、局部盘、战斗只读子页、对白）盖住 → 回它；否则若原位置是已消失的来源行，按 Q645 落点；再否则加工菜单为最高层时回菜单标题；最后回原「菜单」入口按钮。打开菜单前本来没有键盘焦点（如鼠标点开）时不硬塞焦点。ESC 打开菜单前按键路由会把按钮焦点收起，故在 ESC 按下时先记下当时焦点，1.5 秒内打开的菜单用它作原位置。
9. Q655 Tab 循环与 Q602 加工菜单共用一套逻辑（`trapTab`：可见、未禁用、tabIndex≥0、不在 inert／hidden 里），游戏菜单、战斗菜单与手动存档子页取最上层的 `.menu-modal`；没有可聚焦控件时回本层标题（三处标题加 `tabindex="-1"`，不进 Tab 序列）。
10. Q655 “按住不连退”：菜单打开时按住 ESC 的重复按键不再收层；菜单因 ESC 收起后，同一次按住的重复 ESC（到松开前）也不落到背后去重开菜单或收别的层。

## BM7a.4 实施记录与偏差

- Q629：只改配置里的 note；对白记录本来就按展示时的配置快照写（`Note = cfg.Note`），旧档已存的历史附注不改写，读档不重播。
- Q633：撤掉的内核常量 `SkipToResult`／`SkipRest`／两句说明与键表「跳到结果」「跳过余下」两行一并删除；app.js 仍把 Enter／Space 交给 C# 的 'Skip'，C# 对特写与收束停留什么也不做（播放层接住、不落到背后）。收束停留期间的 ESC 改为停留结束后开菜单，见开发假设 6（偏差：不在停留中途打开）。
- Q632：浮框打开时再算一次同一 `PreviewCommand`（与下方五项同参数），不改领域预演；`FocusBattleTarget` 原先群体技能直接返回，现在群体也记下键盘目标（仍不设单一明确目标）。
- Q634：不合法空号位本来就没有 role／tabindex／点击，不用改；只改有人的不合法号位与 `ClickSwapSlot`。
- Q644：`MoveToPotOption.Hint` 字段与 `MoveToPotHint` 函数一并删除，不留死代码；成功日志 `MoveToPotDoneText` 与清零句不动。
- Q655：手动存档子页返回首页仍按 BM2 Q500 回底部关闭键（Q500 是这一处的具体裁定，Q655 写“关闭一层回其真实上一层原合法控件”，二者是否冲突另问 Q659），没改。
- 按新行为改的旧测试：`V11ModelTests`、`B38N02RewriteTests`（tutorial_arrive 附注改按 Q629 新句）；`B34bPlaybackWiringTests`、`BM2KeyAnswerTests`（键表「动画跳过」区只留「跳过动画 空格」；瞄准引导调用处不再追加取消提示）、`B31aPresentationTests`、`B41e3Tests`（撤两级跳过）；`BM3AimAnswerTests.Q511`（`FocusBattleTarget` 群体分支）；`BM5b1FacMenuTests`（Q601 按钮说明句断言撤下、Q602 循环改查 `trapTab`）；`B31dShellTests`（战斗菜单标题 tabindex）。
- 本地浏览器自测未做（启动本地服务被拒），只跑了构建与单元测试；布局（浮框位置、多目标两列、叠层让开）与 JS 焦点修复需试玩代理在浏览器里确认。

测试：2568 项全绿（基线 2556，新增 12 项）。

## BM7a.5 试玩修复

（待填）

## BM7a.6 线上复验结论

（待填）

## BM7a.7 遗留

- 新问 Q659（手动存档子页返回首页时焦点回「手动存档」入口还是底部关闭键），见 02「2026-11-14 来自 bab3b48、153b858 BM7a（开发中发现）」；现状保持 Q500 回关闭键。
- Q636（安全区残留清除）归 B48；Q646—Q654 归 BM7b；Q642 来源合行（BM5b2）做完后，Q645 的稳定键改按来源组键。

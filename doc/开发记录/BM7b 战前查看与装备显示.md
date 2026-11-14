# BM7b 战前查看与装备显示

批次 BM7b 次要合并：战前查看与装备显示（2026-11-14）。需求 R239（策划提交 153b858 答 Q646—Q651、Q653、Q654：敌踪失效回当前地块、风险与战前命中计入确定入场恐慌、非法普通攻击写无可用攻击、地牢 BOSS 名、战前公开特性、非武器固定效果按真实消费者、技能归属按物主、可选强敌帮助句；bab3b48 同节 Q629—Q636 的界面框架已在 BM7a）。同一策划提交里的 Q652 多途径暴露、混合组与成员传播途径、喷溅／病毒风险相关句归 B49，Q644、Q645、Q655 已在 BM7a 做完，本批不做。

## BM7b.1 来源与范围

规则与玩家可见文字只取自以下来源：

- S14 文首「2026-11-14 Q644—Q655：简化说明、真实估算与多途径感染」§Q646、§Q647／Q654、§Q648／Q649（含正式特性句表）、§Q650／Q651、§Q652／Q653 的 Q653 段，与验收 Q646-A01—Q651-A01、Q653-A01、Q654-A01。
- R05 文首「2026-11-14 Q646—Q654：失效回地点、确定恐慌与传播特性」（含 Q648 八座地牢名称表）；R03 文首「2026-11-14 Q647／Q649／Q652／Q654」的 Q647／Q654 段；05 文首「2026-11-14 Q644—Q655完整流程补齐」。
- 《重墨 V2 开工裁定与界面逐字稿》文首「2026-11-14 Q644—Q655正式文字与感染覆盖」表（除感染各行）、Q649 特性句表及其后 Q650 说明。
- 《开发方待确认问题》Q646—Q651、Q653、Q654 的策划答复。

本批接入的玩家可见文字（照抄来源）：

| 位置 | 原句 | 来源 |
| --- | --- | --- |
| 右栏「你的命中率」悬停说明（接在原公式句后） | 已计入确定的入场恐慌；未计入随机受惊。 | 逐字稿表「战前命中口径说明」 |
| 右栏「你的命中率」行、中央处理盘成员命中处 | 当前无可用攻击 | 逐字稿表「当前武器普通攻击非法」（原 Q613 常量） |
| 地牢最终守卫卡面、处理盘、右栏标题 | BOSS·{BOSS名字}（8 座：BOSS·封锁祭室守卫／隔离核心守卫／军械库守卫／天线台守卫／泵房推挤者／深井重击者／扶梯拦截者／仓管守卫） | 逐字稿表；R05 Q648 名称表 |
| 其他真实守卫标题 | 守卫·{守卫名字} | 逐字稿表（原 BM5b1 写法） |
| 右栏特性组标签 | 特性 | S14 §Q649「“特性”在Q639主要属性后」 |
| 特性句 | 流血免疫：不会流血。／护盔：抵挡本场首次失衡效果。／低血逃跑：健康不高于{健康阈值}时可能尝试逃离。／保护同伴：具备保护同伴的能力，需满足实际站位和目标条件。 | S14／逐字稿 Q649 正式特性句表 |
| 死亡喷溅特性（只接前半句，见 BM7b.4） | 死亡喷溅：死亡时伤及我方1、2号位，基础伤害{伤害值}，护甲照常计算。 | 同表 deathSplash 行前半 |
| 武器说明「技能归属」 | {门类}熟练（当前{持有人等级}级，已解锁{持有人解锁数}个）：{原节点列表}（原格式，数值换人） | 逐字稿表「武器技能归属」 |
| 系统说明「敌踪与遭遇」 | 必经关卡的固定敌人必须战胜，无法躲避。可选强敌由你决定何时处理，不会主动接战。 | 逐字稿表「可选强敌帮助整句」 |
| 非武器固定效果分项 | 逐字取装备原效果句（`gear[].effectText`）的各分项；无消费者分项沿原注记「（相关技能上线后生效，当前只显示）」 | 逐字稿 Q650【交开发方定】沿 Q608 句式；原 EquipDetailLines 注记 |

撤下的旧文字：「这个敌人已经离开。」「选择地点、路线、资源或物品查看信息」（敌踪右栏空态）；右栏「这处敌踪已失效，请重新选择。」（常量仍在交战风险结果里作领域判定）；右栏命中率悬停「恐慌、失衡开战后才计」；帮助「不会倒计时主动接战」。

## BM7b.2 工程映射与里程碑

| 需求项 | 位置 |
| --- | --- |
| Q646 失效回地点 | `src/Core/Engine/GameEngine.EnemyCardsUi.cs` 新 `EnemyCardShowable`（卡在当前地点／区域且仍有有效敌方活成员，同风险 Invalid 判定）；`web/Survival.Web/Components/SelectionDetail.razor` `case SelectionKind.Enemy when e.EnemyCardShowable(...)`，否则落到原 `default`「当前位置」分支；`Components/EnemyDetail.razor` 撤 `card == null` 空态与 `risk.Invalid` 失效句 |
| Q647 确定入场恐慌 | `src/Core/Engine/GameEngine.EngageRisk.cs`：原 `EngageRiskFactorsFor` 的临时战斗搭建抽成 `EntryProjection`（每名参战者 `b.Panic = DeterministicEntryPanic`），求值改 `EngageRiskFactorsOn`；新 `DeterministicEntryPanic`、常量 `PreBattleHitNote` |
| Q654 非法普通攻击 | `GameEngine.EngageRisk.cs` 新 `PreBattleHits`（与 `EnemyCardPreviewMembers` 同序，`CommandBlocked`＋`PreviewCommand` 于入场投影）与 `PreBattleHit` 类；`web/Survival.Web/Services/GameSession.cs` `PreBattleHits(card)` 缓存（键同 `EngageRisk`）；`EnemyDetail.razor` `MemberLines(m, hit, …)`；`Components/ActionPops.razor` 中央处理盘成员命中；`GameEngine.Combat.cs` `OutOfBattleHit` 注释（UI 不再调用） |
| Q648 BOSS 名 | `src/Core/Engine/GameEngine.EnemyCards.cs` 守卫卡 `Title` 取 `f.EnemyOrder[0]`、新 `EnemyCardView.Boss`（guardian 且 `DungeonCfg(...).GuardNode == node`）；`GameEngine.EnemyCardsUi.cs` `EnemyRailIdentity`、`BossIdentity`；`Components/LocationStrip.razor` 卡面 `Title`、`ActionPops.razor` 处理盘 `Title` 改用 `EnemyRailIdentity` |
| Q649 公开特性 | `GameEngine.EnemyCardsUi.cs` 新 `EnemyPublicTraits` 与特性句常量／函数；`EnemyDetail.razor` 新 `Traits(enemyId)`（主区 `CoreLines` 之后）、`MemberLines` 其他成员属性后 |
| Q650 非武器效果 | `src/Core/Engine/GameEngine.EquipRail.cs` 新 `GearEffectClauses`、`GearConsumedKeys`、`GearClauseWords`、`GearNotLiveNote`；`EquipActiveTraitLines` 与 `RailEquipLines` 的装备分支；`GameEngine.EquipEffects.cs` `EquipDetailLines` 装备「固定效果」注记改按分项；`Components/ItemDetail.razor`、`GearPeekDetail.razor` 的 `mainEquip` 不再含「固定效果」 |
| Q651 技能归属 | `GameEngine.EquipEffects.cs` `EquipDetailLines`「技能归属」用 `TraitHolderId` 的人物等级 |
| Q653 帮助句 | `web/Survival.Web/Services/HelpCatalog.cs` 主题「敌踪与遭遇」 |
| 测试 | `web/Survival.Core.Tests/BM7bTests.cs`（9 项：Q646-A01、Q647-A01、Q647 分人精神、Q648-A01、Q649-A01、Q650-A01、Q651-A01、Q653-A01、Q654-A01）；按新行为改的旧测试见 BM7b.4 |

| 里程碑 | 内容 |
| --- | --- |
| M893 `e4814a4` | Q646 失效回当前位置、Q647 入场投影计入确定恐慌、Q654 非法普通攻击写无可用攻击、Q648 BOSS 名、Q649 公开特性、Q653 帮助句。 |
| M894 `e3202b4` | Q650 非武器固定效果逐项按真实消费者、Q651 技能归属按持有人；BM7bTests。 |
| M895 | 本开发记录、策划案【实现同步】与修改记录、02 处理结果与新待确认问题 Q663。 |

## BM7b.3 开发假设

以下为代码结构、命名、排版等开发方自定，不涉及规则、数值或新玩家文字：

1. 交战风险与「你的命中率」共用一个入场投影构造函数，每次查看各建一份状态副本（两者分别缓存），不合并成一个结果对象；缓存键沿 BM5b1 交战风险（`Version`、状态对象、卡键）。
2. 「你的命中率」按成员与 `EnemyCardPreviewMembers` 同序给出；入场投影里的敌人按世界实体编号对回成员（可选强敌未生成实体时按敌种对回）。
3. 公开特性每条一行，样式沿右栏装备特性组（`rail-fx` 整行）；主区特性块放在六项属性之后、右栏提示之前。
4. 非武器效果分项的进组顺序、行样式沿武器固定特性组（Q608／Q638 同一组件位置），同一件的多个分项各占一行。

## BM7b.4 实施记录与偏差

- Q646：右栏「当前位置」分支本来就是读当前实际地点的原默认信息（地点、区域、遮蔽、探索度），当前位置读不出时这几行为空、只留标题与「选择对象查看详情。」，即“无法解析时原默认信息”，未另做第二级回退。悬停与点选走同一 `SelectionDetail`，悬停新对象时按新键分派，不存在旧回调覆盖新对象的路径。
- Q647：现有代码里没找到别的已接入的确定入场修正（镇定勋章 `resolve_medal` 无效果键、无消费者），只带入本人精神与有效惊魂未定；恐慌只影响命中能力与撤退率两处查询，风险里用到的是命中能力。
- Q654：合法性直接用战斗中同一个 `CommandBlocked`（顺序：解锁 → 冷却 → AP → 体力 → 耐久 → 弹药 → 使用位 → 目标），百分比用同一个 `PreviewCommand`，所以合法时与开战后第一手的预演同源。超出本场容量、不入场的成员没有入场号位，也写「当前无可用攻击」（另问 Q663）。命中计算异常（如配置缺定义）时该卡各行按不合法处理、不显示百分比（不猜数值；同一卡的交战风险在这种情况下写「交战风险：暂无法评估」）。中央处理盘每名成员的命中原来也是同一个战外估算，本批一并改为同一份结果：合法写「命中{百分比}」，不合法写「当前无可用攻击」。
- Q648：当前数据里 8 个守卫固定战都在各自地牢的 guardNode 上，故全部是「BOSS·」；「守卫·{守卫名字}」分支保留给今后非最终守卫。原卡名已以「守卫」开头时不再加「守卫·」沿 BM5b1 原判断未改。守卫卡名字原来取当前第一名存活成员，本体被击杀后会变成杂兵名，本批改取 enemyOrder 首名，日志里用卡名的句子随之用本体名（只影响之后新写的日志）。
- Q649（偏差）：正式句 deathSplash 行全文为「死亡喷溅：死亡时伤及我方1、2号位，基础伤害{伤害值}，护甲照常计算；造成正伤害时可能传播病毒。」。后半句说的是 Q652 新感染规则，现行死亡喷溅仍不判感染（B49 接入），照抄会写出游戏里不存在的效果；按本批范围只显示前半句，分号处改句号收尾。B49 接入喷溅感染时把后半句补回。
- Q649：「中立，受到攻击后才转为敌对。」按正式表说明沿卡面身份状态，不进特性组；战前没有持续公开状态，不造状态行。
- Q650：装备效果没有独立的「效果名」，逐字稿【交开发方定】的「{效果名}：{数值与原触发条件}」句式落为直接用原效果句的分项（如「护甲+0.20」「位移抵抗+15个百分点」），不加前缀、不改数值。分项判定按“分项文字里的领域词 + 本件确有对应效果键 + 该键有真实读取”：救护防护背心「接受的非病毒医疗恢复×1.10」、均衡战术甲「首次受暴击时该击暴击倍率减0.25（每场一次）」、猎手护符「对动物命中能力+6」等没有对应效果键的分项判为未接入。整备比较等一级面板沿 Q609 不改布局，但原注记会把混合件整件写「生效中」、把重型防护甲写「部分生效中；其余在相关技能上线后生效」，与 Q650“不冒称生效”“位移抵抗已接入”冲突，故注记改按同一分项判定，三种注记句本身不变。
- Q651：右栏与一级面板都读同一 `EquipDetailLines`，一并改为按持有人。
- 按新行为改的旧测试：`B41e1ShellTests`（卡面标题改用 `EnemyRailIdentity`；详情段起点改 `@if (card != null)`）、`B41e2ShellTests`（帮助句）、`BM4bRailTests`、`BM5b1EnemyRailTests`（`MemberLines` 新签名与 `@for` 循环、`@Traits`、处理盘命中写法）、`BM5b1RiskTests`（不再断言右栏失效句与 `OutOfBattleHasAttack` 接线，改查 `@if (!risk.Invalid)`、`@if (hit.Legal)`；A04 补 `PreBattleHits`）。
- 本地浏览器自测未做，只跑了构建与单元测试；右栏特性行、BOSS 标题长度、非武器效果分项的排版需试玩代理在浏览器里确认。

测试：2578 项全绿（基线 2569，新增 9 项）。

## BM7b.5 试玩修复

（待填）

## BM7b.6 线上复验结论

（待填）

## BM7b.7 遗留

- 新问 Q663（次要）：超出本场容量、不会入场的敌踪成员，「你的命中率」写「当前无可用攻击」是否可以，见 02「2026-11-14 来自 153b858 BM7b（开发中发现）」；现状写无可用攻击。
- Q652（B49）接入时要一并处理：死亡喷溅特性句补回后半「造成正伤害时可能传播病毒。」；右栏与处理盘的「它不会咬伤，攻击不传播病毒。」、咬伤唯一感染句、帮助里的咬伤唯一传播段按新文案替换。
- 中央处理盘不显示 Q649 特性（S14 只要求战前敌踪右栏），未加。

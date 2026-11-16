# MT1 战斗技能与交战风险变异补测

2026-11-16。不是策划批次：全量变异测试（Stryker.NET 4.16，基线 10da301d，引擎总分 67.3%）后，按用户定的优先级先补两处得分最低的战斗文件。**只补测试，没改实现。**

- `src/Core/Engine/GameEngine.CombatSkillSupport.cs`：非攻击技能（格挡、自身增益、指挥、医疗、装填）、攻击增益的消耗、反应攻击（反击、迎击）。
- `src/Core/Engine/GameEngine.EngageRisk.cs`：交战风险五档与战前命中预估（R05 Q613 主控定稿、Q646—Q654、Q663、Q665）。

## 结果

| 文件 | 补测前 抓到／存活／无覆盖 | 得分 | 补测后 抓到／存活／无覆盖 | 得分 |
| --- | --- | --- | --- | --- |
| CombatSkillSupport | 383／434／154 | 39% | 859／97／15 | 88% |
| EngageRisk | 130／110／64 | 43% | 254／44／6 | 84% |

（含文字类变异；只看逻辑类：CombatSkillSupport 88%、EngageRisk 86%。）

新增测试 102 项，全部守"测试防假绿"7 条，期望值写字面量并注明出处（R03 §4 战斗公式、R05 Q613 各条、A05、A06、Q82 等），每项都把实现按对应变异改坏、只跑本类确认变红后还原：

- `web/Survival.Core.Tests/EngageRiskMutationGapTests.cs`（33 项）
- `web/Survival.Core.Tests/CombatSkillSupportMutationGapTests.cs`（25 项：指挥、增益窗口、斧蓄力、格挡、挣脱）
- `web/Survival.Core.Tests/CombatSkillSupportMutationGapSupportTests.cs`（34 项：医疗、快速装填、护身类、保护／掩护、整队、预览）
- `web/Survival.Core.Tests/CombatSkillSupportMutationGapReactionTests.cs`（10 项：反击、架矛迎击）

## 补测发现的实现错误（已修，d186af06）

用户定"修了再一起推"。两处都改为与策划案一致，回归测试 `web/Survival.Core.Tests/MT1CombatBugfixTests.cs` 3 项修复前均红、修复后绿；全量 2871 项通过。本地 Release 包轻量试玩：加载、新游戏、控制台 0 错误；两处改动在界面上要指挥技能＋队友医疗＋体力耗尽的战斗才能走到，以测试为准。

- 应急拳击：`ConsumeAttackBuffs` 不再对应急拳击直接返回，吃到的鼓舞／总攻令用掉，鼓舞用掉照常给施术者指挥经验+2（斧蓄力原本就不作用于应急拳击，不变）。
- 救援医疗箱「稳定」：×0.9 记在被治疗的目标身上。

原记录如下：

| 位置 | 现象 | 依据 |
| --- | --- | --- |
| CombatSkillSupport.cs:33 `ConsumeAttackBuffs` | 应急拳击结算时吃到鼓舞（命中+6）与总攻令（×1.15）（CombatSkills.cs:405、419 只排除反应攻击），但这里对 `Desperate` 直接返回，增益没被用掉，下一次攻击还能再吃一次 | A06／R03 Q82：增益作用于"下一次攻击"，只排除反应攻击；R03 我方攻击能力公式含鼓舞 |
| CombatSkillSupport.cs:545 救援医疗箱"稳定" | 给队友治疗时，`b.PlayerNextDamageFactor = 0.9` 写在 `FocusOn(target)` 块外，记到了施术者身上 | A05 rescue_medkit：有效医疗后**目标**获下一次直接伤害−10% |

## 策划案没规定、未补

| 位置 | 问题 |
| --- | --- |
| EngageRisk.cs:97、98、141、236、286、293、310、404 | 各处浮点容差（恰好等于 1e-12／1e-9 时算 0 还是不算） |
| EngageRisk.cs:266 | 伤害系数为 0 的控制技能若带词条平加，是否计入 D_i（策划案只说"直接伤害攻击"） |
| CombatSkillSupport.cs:96 | 创伤处置包 +0.2 回血是否作用于不回血的应急护理（medicine_07） |
| CombatSkillSupport.cs:122 | 是否需要单独的"已在反击架势"拒绝（冷却期内本就到不了） |
| CombatSkillSupport.cs:482 | 首次号令是稳心（持续 0 回合）时，指挥终端的预判 +4 维持几回合 |
| CombatSkillSupport.cs:86 | 缺多份材料时原因是否写"×N"（现有医疗技能都只要 1 份） |

## 不补的其余存活（理由）

逐条处理表在两个代理的汇报里，这里按理由归类：

- **等价改动**（逐条改坏后相关测试仍全绿）：投影副本不推进时间、不写日志、`Hostile` 不参与风险计算、finally 里的复原与 `Acting` 清理、槽位连续时 `First`／`FirstOrDefault`、ahead 总存在；快速装填只出现在弹仓武器上；command_10 施术者一定指挥满级、待发经验恒 0；唯一的掩护来源是 ×0.8；反应攻击都是近战且固定加伤为 0；`CheckVictory` 自己判战斗结束；各技能体力消耗都 >0；医疗物品无保质期、堆叠可互换。
- **不可达**：静态检查先返回的原因句（CombatSkillSupport.cs:108、114、148、157、164、165、125、126、493）；EngageRisk.cs:345。
- **文案细节**：装饰性括注，规则可见的数值、判定词、技能名、原因都已有精确字符串断言。

补测后复跑仍剩的逻辑类存活里，还有几处不在上面的清单中，留待下一轮：CombatSkillSupport.cs:187（预览的"行动后体力"）、188（预览说明行）、590／592（医疗物品可用份数计算）、399／491（整队、协同的耐久扣减条件）、18／19／204／328／349／356／576（`> 0`／`< 1` 改 `>=`／`<=` 的边界，多为配置值不会恰好等于边界）。

## 过程备注

- 两个开发代理各在独立 worktree 里做，提交 cherry-pick 到 main（1fc8b59e、5062968e）。
- 一个代理的批量改坏脚本曾把实现文件行尾从 CRLF 改成 LF，已转回并确认与 HEAD 一致；最终 `git diff -- src/Core` 为空。
- 复跑变异测试时初始测试 2996 项全绿（Stryker 初始运行，含本批新增）。

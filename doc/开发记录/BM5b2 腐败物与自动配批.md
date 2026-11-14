# BM5b2 腐败物与自动配批

批次 BM5b2（2026-11-14）。需求 R223、R224（策划提交 6e9b6e7、6110c39 答 Q606、Q610：取消手选批次改系统自动配批、到期物品转为独立新物品「腐败物」）、R235（4d58288 答本批开工核对的 Q640—Q643，均选 A：身边才写腐败日志、取出预估接原去向、火堆来源按来源组合行、自动配批不跨分侧）。先做 Q610（M897），再做 Q606（M898、M899）。同一来源里的 Q636（B48）、R09 濒死（B47a）、Q652（B49）、B35—B37 与 BM8 的 Q656—Q665 不在本批。

## BM5b2.1 来源与范围

规则与玩家可见文字只取自以下来源：

- S06 文首「2026-11-14 Q640—Q643：附近腐败日志、取出说明与分侧配批」全文与验收 Q640-A01／A02、Q641-A01／A02、Q643-A02；「2026-11-14 BM4b Q606／Q610：自动选批与独立腐败物」§Q606、§Q610、到期时序、随身满落地、正式文字与旧档及本节验收 Q606-A01—A03、Q610-A01—A07。
- S14 文首「2026-11-14 Q637—Q643」§Q642／Q643 与验收 Q642-A01／A02、Q643-A01；「2026-11-14 BM4b Q606—Q614完整答复」中 Q606／Q610 部分。
- S07、S08、S10、S11 文首「2026-11-14 BM4b Q606—Q614联动」，S11 文首「2026-11-14 Q637／Q640—Q643」，S01 文首「2026-11-14 Q610：保质到期转换与既有加工保护」。
- 《重墨 V2 开工裁定与界面逐字稿》文首「Q637—Q643正式文字与显示分工」表的腐败与来源行各行、「BM4b Q606—Q614联动」表的转移总数至旧档迁移各行；05 文首「Q637—Q643界面流程补齐」「BM4b Q606—Q614联动」。
- 《开发方待确认问题》Q606、Q610、Q640—Q643 的策划答复。

本批接入的玩家可见文字（照抄来源）：

| 位置 | 原句 | 来源 |
| --- | --- | --- |
| 新物品名／说明 | 腐败物／已腐败的物品，不能食用、治疗或加工。 | 逐字稿 BM4b 表「新物品」「腐败物说明」 |
| 腐败物用于食用、放进火堆／锅、处理尸体 | 腐败物不能用于此操作。 | 逐字稿「拒绝原用途」 |
| 身边原位腐败日志 | {原物名}×{数量}已腐败，变为腐败物×{数量}。 | 逐字稿「原位腐败」；S06 Q640 |
| 身边随身满落地日志 | {持有者}的{原物名}×{数量}已腐败，变为腐败物并放在{地点}{区域后缀}地面。 | 逐字稿「随身满落地」；区域后缀按 S06「沿原有屋内／屋外，无分区省略」 |
| 输入腐败停工结果（接在原中止／失败句后） | 腐败部分已变为腐败物。 | 逐字稿「输入腐败停工分句」 |
| 取出预估（接原取出按钮去向文案） | 取出后解除烹饪保护，已过期的食材将变为腐败物。 | 逐字稿「取出后果」；S06 Q641 |
| 旧档一次迁移提示 | 已将存档中过期的物品转换为腐败物。 | 逐字稿「旧档一次迁移提示」 |
| 操作窗批次条标题 | 批次／批次（{批数}） | 逐字稿「批次标题」 |
| 火堆来源行 | 原来源组名／原物品名（含侧卡公开名）／×{总数}／最近有效到期（沿原到期格式） | 逐字稿 Q637—Q643 表「原料来源行」 |

撤下的旧文字：取出日志尾句「，X解除烹饪保护、已过原保质期而腐败」；操作窗批次条「选择批次（食用和转移都只用所选这一批）」「点选批次：……」、标题后「· 所选批……」、提示「共{n}批，转移先取最早到期的有效批次（……）」「共{n}批，转移所选批次（……）」；叠卡后层悬停「（点选这一批）」与「选择第{k}批……」；制作材料批次「已手工指定，点击取消」「点击固定使用这一批」「 · 指定」、材料槽悬停「把具体批次的卡拖到这里指定使用」、开工拒绝「指定的材料批次已失效，请重新选择」；帮助「材料拖到制作面板的材料槽指定批次」「指定批次」两处字样（只删，不补新句）。

## BM5b2.2 工程映射与里程碑

| 需求项 | 位置 |
| --- | --- |
| Q610 物品定义 | `data/config.v0.6.json` 末尾 `spoiled_matter`；`src/Core/State/GameState.cs` 存档号 51、`ItemInstance.SpoiledFrom`／`SpoiledUnitGrams`／`SpoiledStackMax` |
| Q610 转换事务 | 新 `src/Core/Engine/GameEngine.Spoilage.cs`：`ConvertDueSpoilage`（快照根卡→原位转换→随身满落地→身边日志）、`DueToSpoil`、`ToSpoiledMatter`、`UnitGrams`、`StackMaxOf`、`IsStackableInst`、`NearPlaceOf`、`GroundPlaceText`；调用点 `GameEngine.Segment.cs`（段末）、`UpdateFlags`（变更边界）、`GameEngine.Facility.cs` 取出事务 |
| Q610 守恒与计数 | `GameEngine.Items.cs`（合堆键、真实批次键加「每份克重＋上限」、`ItemGrams`、根卡计数 `AddStack`／`CardsOfStacks`、`Normalize`）；`GearPreview.cs`、`Workshop.cs`、`Instant.cs` 单件重量 |
| Q610 停工 | `Drying.cs`（晾制腐败停止）、`ExitLock.cs`（锁定自动中止）、`KeptWork.cs`、`GameEngine.cs`（段前／完成时腐败中止）、`Butcher.cs`、`CookWait.cs`、`PotRecipe.cs`（按原物 ID 判定）；`SpoilWorkInputs` 静默转换＋结果接分句 |
| Q610 旧档 | `src/Core/Save/SaveMigrator.cs` `From50To51` → `MigrateSpoilage` |
| Q641 取出 | `GameEngine.Facility.cs` `TakeOut`（事务前快照、解保护后同事务转换、主句后接一次原位句）、`TakeOutConsequence`；`web/Survival.Web/Components/CardActions.razor` 取出预估接按钮去向 |
| Q606 配批内核 | 新 `src/Core/Engine/GameEngine.Batching.cs`：`GroupStacks`、`GroupOf`、`GroupCount`、`PlanGroup`、`RootCardsAfterPlan`、`RootCardsAfterRemovePlan`、`PlanGrams`、`MaxGroupPickup`、`GroupPickupBlockedText`、`TransferGroup`；`Requests.cs` `InstantRequest.Group*`、`PickupGroup`／`DropGroup` |
| Q606 各入口 | `GameEngine.Instant.cs`（拾取／放下组路径、`FillPot` 组路径与 `MaxFill(available)`）、`GameEngine.Facility.cs`（`PutFoodInPot`、`PutInFire` 组路径、`MaxPutFood(available)`）、`GameEngine.Drop.cs`（`Groupable`、`AsGroup`，拖放到区域／锅／火堆发组意图）、`GameEngine.Workshop.cs`＋`GearPreview.cs`（交接 `GivePlan`／`GiveAvailable`）、`web/Survival.Web/Services/GameSession.cs`（`TransferGroup` 取代 `TransferBatch`、`ExecuteDrop`、卡角箭头 `TransferOne`）、`GameSession.Gear.cs`（交接盘数量） |
| Q606 制作／晾制 | `GameEngine.Items.cs` `Reserve` 去掉指定批次；`GameEngine.cs` 删指定批次校验；`GameEngine.Drop.cs` 材料槽不带指定批次；`GameEngine.Drying.cs` `DryingSources` 删 Q214 排序；`Components/CraftOverlay.razor`、`GameSession.cs`／`GameSession.PanelStack.cs` 删 `CraftPreferred` |
| Q606 界面只读 | `Components/CardActions.razor`（批次条只读、`BatchHead`、按组预演重量与卡数）、`ItemDetail.razor`（批次组只读）、`ItemShelf.razor`（后层点击选同一组）、`wwwroot/css/fourrows.css`、`Services/HelpCatalog.cs` |
| Q642／Q643 火堆来源 | `Components/FireMenu.razor`（来源组合行、`Row.Key`／`Count`、按组提交、原因控件按行键）、`Services/GameSession.FacMenu.cs`（`SourceKey`、`FireMenuSourceKey`、`FireMenuGroupHead`、组首重取、`FireMenuMaxFor` 按组合计） |
| 开发入口 | `GameEngine.Debug.cs` `DebugSpoilageKit`、`Components/MenuOverlay.razor`「腐败测试物资（10分钟后到期）」 |
| 测试 | `web/Survival.Core.Tests/BM5b2Tests.cs`（20 项，见 BM5b2.4 末）；按新规则改的旧测试见 BM5b2.4 |

| 里程碑 | 内容 |
| --- | --- |
| M897 `711f58f` | Q610 独立腐败物、Q640 身边日志、Q641 取出预估与日志、旧档 50→51 迁移、开发入口；BM5b2Tests 13 项。 |
| M898 `0513e69` | Q606 自动配批（按钮、拖放／Shift、锅食材、火堆加工位、锅装水、同伴交接、制作材料、晾制），批次只读界面，Q642／Q643 火堆来源组合行与来源组稳定键；BM5b2Tests 7 项。 |
| M899 `2015c57` | 卡角箭头走同一配批。 |
| M900 | 本开发记录、策划案【实现同步】与修改记录、02 处理结果与新待确认问题 Q666—Q671。 |

## BM5b2.3 开发假设

以下为代码结构、命名、排版等开发方自定，不涉及规则、数值或新玩家文字：

1. 腐败物用一个新物品 ID，原物、每份克重、原单堆上限记在实例上；合堆与真实批次按「物品＋每份克重＋上限」区分，界面卡片仍按物品归组（S06「不同重量可视觉归组」）。
2. 到期转换集中在一个函数里，世界段末、变更边界、取出事务、停工结算和读档迁移都调它；同一事务前先快照随身根卡数。
3. 自动配批放在内核：界面只发「来源组＋物品＋卡侧＋总数」，提交时按实时实物重配；请求里带的组首实例号只用于拖放来源辨认。
4. 火堆来源行的稳定键写成文本键（`g:{owner}|{物品}|{卡侧}`，非堆叠物 `i:{实例号}`），菜单里另存当前组首实例号供原有逻辑使用。
5. 操作窗批次条标题用小号灰字放在批次条最前，样式沿原批次条。

## BM5b2.4 实施记录与偏差

- Q610：在办工作的输入（行动预留、晾制中的原料）到期时只在该工作的结果句后接「腐败部分已变为腐败物。」，不另写普通原位句（S06 Q640「不删真实主行动／批次失败或中止所需的结果」与「原中止／返还句不重复」之间没写两句是否都写，另问 Q666）。
- Q640：「身边」按物品真实所在区判断：随身取持有者所在区，锅取锅所在，设施、晾制架取其区，屋柜取屋内；当前区的遗物箱、地牢封存等不在 S06 列举的「地面、储物、设施与锅内容」里，静默转换（另问 Q667）。
- Q641：取出预估在原风险分句位置写「{物名}×{数量}已过原保质期。」再接原句，操作窗里再接取出按钮的原去向文字；前半的物品清单沿原「已过原保质期」措辞（另问 Q668）。
- Q610：腐败物在配置里必须有单件重量与单堆上限字段，实例一律按原物记录的值算重量与堆叠，配置值不参与；现填 1 克、上限 2，只是为了让配置判为可堆叠、界面按物品归组（另问 Q669）。
- Q606：安全屋柜子面板仍是逐堆一行、整堆放进／取出（没有数量框），本批没改成按组配批（另问 Q670）。卡下按钮、拖放与 Shift 数量、卡角箭头、锅食材、火堆加工位、锅装水、同伴交接、制作材料与晾制都已走同一配批。
- Q606 交接：交接盘仍从整备里选中的那一堆打开，数量上限与扣减按出物人随身同组合计、早到期先交；部分交接后若打开时的那一堆已交完、同组还有别的批，盘回空态（沿原「来源耗尽回空态」），需重新选择。
- Q606 制作：拖材料到材料槽仍打开对应配方，只是不再带指定批次；材料行下的批次明细改只读。
- Q606 晾制：取消 Q214「截止晚于完成的先取」排序后，果干临期红字警告照旧显示（S06「不改变果干允许临期投料的警告」）。
- Q642：预留、行动占用等不能进组的实例仍按原来的一行一堆列出，沿原灰显原因；组首被用完或转腐败物时，菜单所选来源在同组内重取组首；整组都没了才清来源并提示内容变化，不改选腐败物。
- 一些旧清单在腐败物后面仍会带原有的「（已腐败）」「，已腐败」标记（地牢残留、远行清单），没逐处删（另问 Q671）。
- 按新规则改的旧测试：存档号 50→51（B41a、B41b、B41c2、B41cSchema、BM5c、V11Model），MigrationTests 伪造新档号改 52，ConfigTests 配置件数 +1，V11CookProtect、FacilityProcess、B22Reset、CombatV05、B17Storage、B22c、B22k 的过期物断言改为腐败物；B22k 晾制取料顺序、DropResolver 材料槽（不再指定批次、与按钮开工一致）、B42／BM4b 只读批次标记、B45b／BM7a 来源行稳定键。
- 本地浏览器自测未做，只跑了构建与单元测试；批次条只读样式、火堆来源合行、取出预估排版需试玩代理在浏览器里确认。

测试：2598 项全绿（基线 2578，新增 20 项：Q610-A01、A02、A03×2、A04、A05×2、A07／Q640-A02、Q641-A01、A02、Q640-A01×2、腐败物拒绝原用途、Q606-A01—A03、Q643-A02、Q642-A01／A02、交接配批、界面只读与来源组键）。

## BM5b2.5 试玩修复

（待填）

## BM5b2.6 线上复验结论

（待填）

## BM5b2.7 遗留

- 新问 Q666—Q671 见 02「2026-11-14 来自 4d58288 BM5b2（开发中发现）」：在办工作输入是否另写普通腐败句、遗物箱／封存物算不算身边、取出预估前的物品清单、腐败物配置占位值、屋柜存取不按组配批、旧清单「已腐败」标记。
- 安全屋柜子存取未改为按组配批（Q670 待答）。
- 交接盘部分交接后打开的那一堆已交完时回空态，未自动接到同组下一批（沿原规则）。

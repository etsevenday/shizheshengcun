using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 陶窑与固定净水台（A07 X1.4、S11 X4、expansion-supply.json facilities/operationalRules.water；开发计划 v0.9 B17 R56）：
    /// 只建在临时营地外部或已完工安全屋屋内（Q107 每区每类一座）。被动批次：在当前区的工坊投料（0 分钟、不占行动），原料从随身/当前地面取、
    /// B51c2a1（S11 Q872 选 B，存档 57）起原料（配方 Inputs）移进设施该批（owner <see cref="ItemInstance.StationOwner"/>），燃料（配方 Fuel）仍移到该区地面按批预留（Q921，待 B35a2a 燃料库存）；
    /// 按世界时间推进（陶窑 60 分钟、净水台 30 分钟，不吃熟练/装备/夜间倍率），离开也继续、不能远程投取；
    /// 完成一次核销原料与燃料（陶窑每批另耗木炭1），产物落该区地面、不自动入包；取出（GameEngine.TakeAll.cs）把原料放回该区地面、燃料只解锁，时间不退、没有产物。
    /// B22d（Q174 选 B，passiveCraftXp）：每成功整批给首次投料且完成时仍存活的角色制造经验（陶窑 6、净水台 3，投料时快照），
    /// 两类共享每角色每世界日 12（按完成日，剩余不足给剩余，满级 0），与主动制造/建造经验独立，不另给烹饪；取消/原料不全/读档/领取都不给。
    /// B32e（Q380 选 B，S08「2026-11-10 Q380」）原为每座一个加工位＋等待队列；B51b（S08 文首 Q701.2、B51 Q721／Q727，存档 55）起改为每座最多 10 批全部并行：
    /// 成功投料的每批立即从自己的投料时刻开始，每个世界 5 分钟段对段首在办的每批各 +5，各自按本批耗时完成；没有等待批。投料时本批实际绑定的有期限输入暂停保质（Q701.1），
    /// 完成核销、取出／失败释放时恢复 C＋R。B51c2a1（Q778／Q870 B）撤「取消本批」，改为每批「取出」与「全部取出」（GameEngine.TakeAll.cs）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>被动批次的配方：工坊是陶窑或固定净水台、模式为 passive/passive_heat。</summary>
        public bool IsStationBatchRecipe(RecipeConfig rc) => rc.IsPassive && !rc.IsPot && rc.Station is "kiln" or "water_station";

        /// <summary>Q701.2：每座工坊最多同时加工的未完成批数（原 Q380 含加工中 10 批上限转为 10 并行批上限）。</summary>
        public const int StationParallelCap = 10;

        /// <summary>Q727：有加工批时才显示一次的并行说明（逐字稿 B51 表 parallelHelp）。</summary>
        public const string StationParallelHelp = "各批同时加工，每座最多10批。";

        /// <summary>某区这座工坊的全部未完成批次，按原创建顺序（列表先后＝创建先后）。</summary>
        private static List<StationBatch> StationList(FacilityState? fac, string stationId) =>
            fac?.StationBatches.Where(b => b.StationId == stationId).ToList() ?? new List<StationBatch>();

        /// <summary>B51c2a1：某区（place）设施里批次 ID 为 batchId 的在办批；没有为 null。</summary>
        public StationBatch? StationBatchAt(GameState s, string place, int batchId) =>
            FacOf(s, place)?.StationBatches.FirstOrDefault(b => b.BatchId == batchId);

        /// <summary>当前区这座工坊在办的全部批次（都在加工中），按原创建顺序；显示序号＝在本列表中的位置＋1，只作显示、不作提交身份。</summary>
        public List<StationBatch> StationBatchesHere(GameState s, string stationId) => StationList(FacOf(s, s.Ground), stationId);

        /// <summary>当前区这座工坊还能同时加工的批数（10 减在办批数）。</summary>
        public int StationRoom(GameState s, string stationId) =>
            Math.Max(0, StationParallelCap - StationList(FacOf(s, s.Ground), stationId).Count);

        /// <summary>主角随身与当前区地面上可用、未预留、未锁的这种物品份数（投料来源）。</summary>
        private int StationFreeCount(GameState s, string itemId) =>
            UsableBatches(s, itemId, true).Where(i => !IsLocked(s, i)).Sum(i => i.Count);

        /// <summary>材料与燃料足够的完整批数（不看余位）。</summary>
        public int StationBatchAffordable(GameState s, RecipeConfig rc) =>
            rc.AllInputs.Count == 0 ? StationParallelCap : rc.AllInputs.Min(kv => StationFreeCount(s, kv.Key) / Math.Max(1, kv.Value));

        /// <summary>投料批数的可选上限：min(10 − 当前在办批数, 材料与燃料足够的完整批数)。</summary>
        public int StationBatchMaxNew(GameState s, RecipeConfig rc) =>
            Math.Max(0, Math.Min(StationRoom(s, rc.Station), StationBatchAffordable(s, rc)));

        /// <summary>在当前区投 batches 批被动加工现在不能做的原因；能做为 null（整次校验：余位与全部批的材料、燃料）。</summary>
        public string? StationBatchBlocked(GameState s, RecipeConfig rc, int batches = 1)
        {
            if (!IsStationBatchRecipe(rc)) return "这不是固定工坊的被动加工";
            if (s.Progress.Dead) return "本局已结束";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "战斗中不能投料";
            if (s.Main != null || s.Sub != null) return "行动进行中，先完成或取消再投料";
            if (RecipeUnknownReason(s, rc) is string unknown) return unknown;
            foreach (var kv in rc.MinSkills)
            {
                var lv = SkillLevel(s, kv.Key);
                if (lv < kv.Value) return $"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）";
            }
            if (StationBlocked(s, rc) is string station) return station;
            var room = StationRoom(s, rc.Station);
            // Q701.4（逐字稿 Q701—Q703 表）：并行批额满／投料超过并行剩余容量；校验顺序沿 Q380／Q397
            if (room <= 0) return $"加工批次已满（{StationParallelCap}批）。";
            if (batches < 1) return "投料批数必须是1或更大的整数。";
            if (batches > room) return $"还可同时加工{room}批，请减少投料批数。";
            foreach (var kv in rc.AllInputs)
            {
                var have = StationFreeCount(s, kv.Key);
                var need = kv.Value * batches;
                if (have < need) return $"缺少{ItemName(kv.Key)}（{have}/{need}，只取随身与当前地面）";
            }
            return null;
        }

        /// <summary>某批的预计完成时刻：当前世界时刻 + 本批剩余时长（Q701.2 各批同时加工，不串行相加）。</summary>
        public int StationBatchFinish(GameState s, StationBatch b) => s.GameMinute + Math.Max(0, b.RequiredMinutes - b.DoneMinutes);

        /// <summary>当前区这座工坊再投 batches 批时，新批各自的预计完成时刻（新批都从现在开始，按本批耗时）。</summary>
        public List<int> StationNewFinishes(GameState s, RecipeConfig rc, int batches) =>
            Enumerable.Repeat(s.GameMinute + rc.BaseMinutes, Math.Max(1, batches)).ToList();

        /// <summary>
        /// Q727（逐字稿 B51 表 sameFinish／differentFinish）：新投各批预计完成时刻同刻合成「预计{日期时刻}完成」，
        /// 不同时刻去重后按时间升序「预计分别于{完成时刻清单}完成」（顿号连接，时刻沿「第N日 HH:mm」）。
        /// </summary>
        public string StationEtaText(GameState s, RecipeConfig rc, int batches) => StationFinishText(StationNewFinishes(s, rc, batches));

        private static string StationFinishText(IEnumerable<int> finishes)
        {
            var list = finishes.Distinct().OrderBy(x => x).ToList();
            return list.Count == 1 ? $"预计{Clock.DayTime(list[0])}完成" : $"预计分别于{string.Join("、", list.Select(Clock.DayTime))}完成";
        }

        /// <summary>
        /// Q701.4 设施主标题「加工中{批数}批（最多10批）」；Q779 选 B：同处显示并行说明「各批同时加工，每座最多10批。」时只写「加工中{批数}批」，不重复上限。
        /// </summary>
        public static string StationTitle(int batches, bool helpShown) => helpShown ? $"加工中{batches}批" : $"加工中{batches}批（最多{StationParallelCap}批）";

        /// <summary>Q727 并行批次主行（逐字稿 B51 表 batchRow）：「批次{序号} · {配方名} · {已进行}/{所需}分 · 预计{日期时刻}完成」，序号只作显示。</summary>
        public string StationBatchRow(GameState s, StationBatch b, int seq) =>
            $"批次{seq} · {RecipeNameOf(b.RecipeId)} · {b.DoneMinutes}/{b.RequiredMinutes}分 · 预计{Clock.DayTime(StationBatchFinish(s, b))}完成";

        /// <summary>Q727 每批原产物落点（逐字稿 B51 表 outputGround）：「完成后放在{区域}地面。」，区域为这座设施原所在区。</summary>
        public string StationOutputGround(GameState s, string place) => $"完成后放在{ZoneName(s, place)}地面。";

        /// <summary>
        /// Q727 工坊汇总（逐字稿 B51 表 workshopSummary／workshopIdle）：有加工批「{设施名} · 加工中{批数}批 · 最近{日期时刻}完成」（取全部在办批最早完成），
        /// 无批「{设施名} · 空闲」。place 为设施所在区。
        /// </summary>
        public string StationSummary(GameState s, string place, string stationId)
        {
            var name = StationName(stationId);
            var list = StationList(FacOf(s, place), stationId);
            return list.Count == 0 ? $"{name} · 空闲"
                : $"{name} · 加工中{list.Count}批 · 最近{Clock.DayTime(list.Min(b => StationBatchFinish(s, b)))}完成";
        }

        private string RecipeNameOf(string recipeId) => _recipes.TryGetValue(recipeId, out var rc) ? rc.Name : recipeId;

        /// <summary>投料说明（预览）：原料、燃料、产物、分钟与落点。</summary>
        public string StationBatchSummary(RecipeConfig rc)
        {
            var ins = string.Join("、", rc.Inputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"));
            var fuel = rc.Fuel.Count > 0 ? "＋燃料" + string.Join("、", rc.Fuel.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}")) : "";
            var outs = string.Join("、", rc.Outputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"));
            return $"{ins}{fuel} → {outs}，被动{rc.BaseMinutes}世界分钟（不占行动，离开也继续），完成后放在本区地面；取出退回原料，时间不退";   // B51c2a1（S11 Q880）：撤取消入口，原句只把动词「取消」换成「取出」
        }

        private const string DailyStationXp = "stationXp";

        /// <summary>这座工坊每成功整批的基础经验（Q174：陶窑 6、净水台 3）；没配置为 0。</summary>
        public int StationBatchXpFor(string stationId) =>
            Config.Progression.StationXp.PerBatch.TryGetValue(stationId, out var n) ? n : 0;

        /// <summary>某角色某世界日被动批次日额（陶窑+净水台共享 12）还剩多少。</summary>
        private int StationXpRoom(ActorState actor, int day)
        {
            var c = Config.Progression.StationXp;
            return Math.Max(0, c.DailyCap - DailyCount(actor, day, DailyStationXp, c.Skill));
        }

        /// <summary>
        /// 同段完成的结账顺序（Q701.2「原同段设施稳定遍历顺序、同设施按批次原创建稳定ID顺序逐批结账」）：全部地点外部区／屋内区的原遍历顺序，
        /// 区内按原工坊分组先后，同一座按批次创建先后。预估与真实结账同用这一顺序。
        /// </summary>
        private IEnumerable<(string Place, FacilityState Fac, StationBatch Batch)> StationSettleOrder(GameState s)
        {
            foreach (var (place, fac) in AllFacilityStates(s).ToList())
                foreach (var g in fac.StationBatches.GroupBy(b => b.StationId).ToList())
                    foreach (var b in g.ToList()) yield return (place, fac, b);
        }

        /// <summary>
        /// 预估用（B22d 试玩修复 M418，开发假设 B22d-8）：同一投料者其他在途批次中，预计在同一完成日、且比这一批先（或同一刻排在前面）完成的快照经验合计。
        /// 只用于显示；结算规则不变（仍按每批完成那一刻的余额）。新批次（self 为 null）同一刻完成的在途批次都算在前面。
        /// B51b：各批完成时刻按本批剩余（并行），同刻按真实结账顺序（StationSettleOrder）。
        /// </summary>
        private int StationXpPending(GameState s, string actorId, int day, int finish, StationBatch? self)
        {
            var flat = StationSettleOrder(s).Select(x => x.Batch).ToList();
            var selfIdx = self == null ? flat.Count : flat.IndexOf(self);
            var sum = 0;
            for (var i = 0; i < flat.Count; i++)
            {
                var b = flat[i];
                if (b == self || b.XpSnapshot <= 0 || b.InitiatorActorId != actorId) continue;
                var f = StationBatchFinish(s, b);
                if (Clock.Day(f) != day) continue;
                if (f < finish || (f == finish && i < selfIdx)) sum += b.XpSnapshot;
            }
            return sum;
        }

        /// <summary>BM9（S08 Q842、逐字稿 2026-11-16 Q841—Q842）：今日已完成批次用满被动制造经验日额时的经验说明（原句无句末标点）。</summary>
        public const string StationXpDailyFullText = "今日可获得生产制造经验已满";

        /// <summary>
        /// 经验说明（预估/进行中共用）：基础经验与完成日余额。xp 为这一批的快照（0 = 旧批次按原规则不给）；finishMinute 为预计完成时刻。
        /// </summary>
        private string StationXpText(GameState s, int xp, int finishMinute, bool started, StationBatch? self = null)
        {
            var c = Config.Progression.StationXp;
            if (xp <= 0) return started ? "这一批开始时被动加工不给经验（按原规则完成）" : "被动加工不给经验";
            var skill = c.Skill;
            var basic = $"完成后{SkillName(skill)}经验+{xp}（陶窑与净水台被动批次合计每日最多{c.DailyCap}）";
            var who = s.Actors.TryGetValue(self?.InitiatorActorId ?? GameState.PlayerId, out var a) ? a : s.Actors[GameState.PlayerId];   // 投料者（主角），不随出战焦点变
            if (SkillLevel(who, skill) >= SkillCap(skill)) return $"{SkillName(skill)}已满{SkillCap(skill)}级，完成不再获得经验";
            var day = Clock.Day(finishMinute);
            var free = StationXpRoom(who, day);
            // BM9（S08 Q842，文案策划 shioko0315 定稿）：完成日是今日、今日已完成批次已用满原被动制造经验日额时，新批与在途批说明恰为此句，
            // 不再接「先完成的在途批次预计占…」「完成不再给经验」「已满12」；日额、结算与其他分支不变
            if (free <= 0 && day == Clock.Day(s.GameMinute)) return StationXpDailyFullText;
            var pending = StationXpPending(s, who.Id, day, finishMinute, self);
            var room = Math.Max(0, free - pending);
            var ahead = pending > 0 && room < xp ? $"；先完成的在途批次预计占{Math.Min(pending, free)}" : "";
            return room <= 0 ? $"{DayWord(s, day)}被动批次{SkillName(skill)}经验{(free <= 0 ? "已满" : "预计已满")}{c.DailyCap}{ahead}，完成不再给经验"
                : room < xp ? $"完成后{SkillName(skill)}经验+{room}（陶窑与净水台被动批次合计每日最多{c.DailyCap}，{DayWord(s, day)}只剩{room}{ahead}）"
                : basic + (day == Clock.Day(s.GameMinute) ? "" : $"，按{DayWord(s, day)}额度");
        }

        /// <summary>
        /// 投料前预估：新投 batches 批完成后的制造经验。按预计完成日分组，同一日的新批经验合计、扣掉同一投料者先完成的在途批次，
        /// 各日沿用单批的原句式（非今日带「按完成日第N日额度」），段间用「；」。B51b：新批都从现在开始，同刻完成。
        /// </summary>
        public string StationBatchXpPreview(GameState s, RecipeConfig rc, int batches = 1)
        {
            var xp = StationBatchXpFor(rc.Station);
            var parts = StationNewFinishes(s, rc, batches).GroupBy(Clock.Day)
                .Select(g => StationXpText(s, xp * g.Count(), g.Last(), false)).Distinct().ToList();
            return string.Join("；", parts);
        }

        /// <summary>在办批次：完成后的制造经验（按快照与本批预计完成日余额）。</summary>
        public string StationBatchXpNote(GameState s, StationBatch b) => StationXpText(s, b.XpSnapshot, StationBatchFinish(s, b), true, b);

        /// <summary>
        /// 批次成功完成：给投料者（完成时仍存活）快照经验，受完成日共享日额；返回日志附注。只在这里结算一次（产物同一处提交），读档/领取不重给。
        /// </summary>
        private string StationBatchXp(GameState s, StationBatch b)
        {
            var c = Config.Progression.StationXp;
            if (b.XpSnapshot <= 0) return "这一批开始时被动加工不给经验";
            if (b.InitiatorActorId == null || !s.Actors.TryGetValue(b.InitiatorActorId, out var actor)
                || IsDead(s, actor)) return "投料者已不在，不给经验";
            var skill = c.Skill;
            if (SkillLevel(actor, skill) >= SkillCap(skill)) return $"{SkillName(skill)}已满{SkillCap(skill)}级，不再获得经验";
            var day = Clock.Day(s.GameMinute);
            var grant = Math.Min(b.XpSnapshot, StationXpRoom(actor, day));
            if (grant <= 0) return $"今日被动批次{SkillName(skill)}经验已满{c.DailyCap}（陶窑与净水台合计），不再给经验";
            BumpDaily(s, actor, DailyStationXp, skill, day, grant);
            GainXp(s, actor, skill, grant);
            return grant < b.XpSnapshot
                ? $"{ActorPrefix(actor)}{SkillName(skill)}经验+{grant}（陶窑与净水台合计每日{c.DailyCap}，只剩{grant}）"
                : $"{ActorPrefix(actor)}{SkillName(skill)}经验+{grant}";
        }

        /// <summary>
        /// 在当前区的陶窑/净水台投 batches 批（0 分钟、不占主行动；S08 Q380、Q701.2）：所有新批的材料与燃料一次校验，逐批分配（每份只归一批），
        /// 原料移进设施该批（B51c2a1，Q872 B），燃料移到该区地面按批预留（<see cref="ReserveStationFuel"/>，Q921 待 B35a2a），全部成功或全部拒绝。新批都立即从现在开始加工（不等旧批、不补旧进度）；本批实际绑定的有期限输入在同一事务暂停保质（Q701.1）。同一 requestId 只生效一次。
        /// </summary>
        public ActionResult StartStationBatch(GameState s, string recipeId, string? requestId = null, int batches = 1)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (!_recipes.TryGetValue(recipeId, out var rc)) return ActionResult.Fail("配方不存在");
            if (StationBatchBlocked(s, rc, batches) is string why) return ActionResult.Fail(why);
            var place = s.Ground;
            var fac = FacOf(s, place)!;
            var preview = StationBatchXpPreview(s, rc, batches);
            int? first = null;
            for (var n = 0; n < batches; n++)
            {
                var id = s.NextActionId++;
                first ??= id;
                var locked = new List<int>();
                foreach (var kv in rc.Inputs)
                {
                    // B51c2a1（S11 Q872 选 B）：原料真实归属本设施该批，不留地面预留、不算地面来源
                    foreach (var moved in TakeStationSources(s, kv.Key, kv.Value))
                    {
                        moved.Owner = ItemInstance.StationOwner(place, id);
                        moved.PlacedOrder = null;
                        moved.ReservedBy = null;
                        PauseShelf(s, moved);   // Q701.1：成功投料的本批输入暂停保质（无期限不造 R）
                        locked.Add(moved.InstanceId);
                    }
                }
                ReserveStationFuel(s, rc, place, id, locked);
                fac.StationBatches.Add(new StationBatch
                {
                    BatchId = id, StationId = rc.Station, RecipeId = rc.Id, InputInstanceIds = locked,
                    RequiredMinutes = rc.BaseMinutes, StartMinute = s.GameMinute, InitiatorActorId = GameState.PlayerId,
                    XpSnapshot = StationBatchXpFor(rc.Station),
                });
            }
            Normalize(s, ItemInstance.Carried);
            RememberRequest(s, requestId);
            Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}{StationName(rc.Station)}投料（{batches}批）：{StationBatchSummary(rc)}；各批同时开始加工；{preview}。");
            UpdateFlags(s);
            return ActionResult.Success(first);
        }

        /// <summary>从随身与当前区地面按 UsableBatches 顺序取够 need 份这种物品（未锁），不足整份时拆堆；返回实际取出的实例（调用方改归属）。</summary>
        private List<ItemInstance> TakeStationSources(GameState s, string itemId, int need)
        {
            var list = new List<ItemInstance>();
            foreach (var b in UsableBatches(s, itemId, true).Where(i => !IsLocked(s, i)).ToList())
            {
                if (need <= 0) break;
                var take = Math.Min(need, b.Count);
                list.Add(take < b.Count ? SplitOff(s, b, take) : b);
                need -= take;
            }
            return list;
        }

        /// <summary>
        /// 燃料预留（集中一处，B35a2a 改为本座燃料库存时只换这里与 <see cref="ReleaseStationFuel"/>／<see cref="StationFuelOf"/>）：
        /// Q921 燃料库存未做前，本批燃料（配方 Fuel）沿原样移到设施所在区地面、以批次 ID 预留，暂停保质（无期限不造 R）。
        /// </summary>
        private void ReserveStationFuel(GameState s, RecipeConfig rc, string place, int batchId, List<int> bound)
        {
            foreach (var kv in rc.Fuel)
                foreach (var moved in TakeStationSources(s, kv.Key, kv.Value))
                {
                    moved.Owner = place;
                    moved.PlacedOrder = null;
                    moved.ReservedBy = batchId;
                    PauseShelf(s, moved);
                    bound.Add(moved.InstanceId);
                }
        }

        /// <summary>这一批在地面预留的燃料（及旧档里仍按本批预留的任何物品）。</summary>
        private List<ItemInstance> StationFuelOf(GameState s, StationBatch b) => s.Items.Where(i => i.ReservedBy == b.BatchId).ToList();

        /// <summary>燃料释放（集中一处）：只解除本批预留、原地不动，暂停保质的恢复 C＋R（Q921：取出不含燃料、未耗只解锁）。</summary>
        private void ReleaseStationFuel(GameState s, StationBatch b)
        {
            foreach (var it in StationFuelOf(s, b))
            {
                it.ReservedBy = null;
                ResumeReleased(s, it);
            }
        }

        /// <summary>B51c2a1：这一批在设施内的原料（owner 为 station:{place}#{批次ID}），按实例号。</summary>
        public List<ItemInstance> StationBatchInputs(GameState s, string place, StationBatch b)
        {
            var owner = ItemInstance.StationOwner(place, b.BatchId);
            return s.Items.Where(i => i.Owner == owner).OrderBy(i => i.InstanceId).ToList();
        }

        /// <summary>
        /// 取出或失败时释放这一批：设施内原料放回设施所在区地面（Q870 B／Q872 B），燃料只解锁（<see cref="ReleaseStationFuel"/>），暂停保质的恢复 C＋R（Q701.1）。
        /// 返回放回地面的原料（调用方合堆、列清单）。
        /// </summary>
        private List<ItemInstance> ReleaseStationBatch(GameState s, string place, StationBatch b)
        {
            var back = StationBatchInputs(s, place, b);
            foreach (var it in back)
            {
                it.Owner = place;
                it.PlacedOrder = null;
                it.ReservedBy = null;
                ResumeReleased(s, it);
            }
            ReleaseStationFuel(s, b);
            return back;
        }

        /// <summary>BM5a（02 Q563 选 A，照 Q446⑦ 最小删词）：工坊操作战斗中的拒绝句（B51c2a1 起用于「取出」「全部取出」，Q871 原战斗阻断保持）；普通敌踪卡在场不挡。</summary>
        public const string StationBattleBlocked = "战斗时不能操作";

        /// <summary>
        /// 每个世界 5 分钟段（Q701.2）：每区每座工坊段首在办的每一批各 +5 分钟（世界只推进一次），达标即重验该批原料（设施内 owner，B51c2a1）与燃料（地面预留）、
        /// 一次核销、产物落该区地面；原料或燃料不全只终止该批、原料放回该区地面、燃料解锁（暂停保质的恢复 C＋R），没有产物与经验，不影响其他批。同段完成按 StationSettleOrder 逐批结账。读档按存档进度续算，不重复出。
        /// </summary>
        private void AdvanceStationBatches(GameState s)
        {
            foreach (var (place, fac, b) in StationSettleOrder(s).ToList())
            {
                b.DoneMinutes += Tick;
                if (b.DoneMinutes < b.RequiredMinutes) continue;
                fac.StationBatches.Remove(b);
                var rc = _recipes[b.RecipeId];
                var inputs = StationBatchInputs(s, place, b);
                var fuel = StationFuelOf(s, b);
                var ok = fuel.All(i => i.Owner == place)
                         && rc.Inputs.All(kv => inputs.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) >= kv.Value)
                         && rc.Fuel.All(kv => fuel.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) >= kv.Value);
                if (!ok)
                {
                    // BM11（S08 Q882 用户新裁定）：撤 B54（Q791）失败句后的恢复保质短句，原失败句与真实返料不变；
                    // B51c2a1：设施内剩余原料放回所在区地面（沿原句「剩下的原料已解锁」），燃料解锁
                    ReleaseStationBatch(s, place, b);
                    Normalize(s, place);
                    Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}{StationName(b.StationId)}：原料不全，这一批没有产物，剩下的原料已解锁。");
                    continue;
                }
                foreach (var it in inputs.Concat(fuel)) s.Items.Remove(it);   // 受保护原料直接核销，不返 R
                var made = new List<string>();
                foreach (var kv in rc.Outputs)
                {
                    CreateItems(s, kv.Key, kv.Value, place);
                    made.Add($"{ItemName(kv.Key)}×{kv.Value}");
                }
                Normalize(s, place);
                var xp = StationBatchXp(s, b);
                Emit(s, GameEventKind.FacilityDone, $"{ZoneName(s, place)}{StationName(b.StationId)}：{string.Join("、", made)}已完成，放在{ZoneName(s, place)}地面；{xp}。");
            }
        }
    }
}

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
    /// 移到该区地面并锁定；按世界时间推进（陶窑 60 分钟、净水台 30 分钟，不吃熟练/装备/夜间倍率），离开也继续、不能远程投取；
    /// 完成一次核销原料与燃料（陶窑每批另耗木炭1），产物落该区地面、不自动入包；取消释放原料、时间不退、没有产物。
    /// B22d（Q174 选 B，passiveCraftXp）：每成功整批给首次投料且完成时仍存活的角色制造经验（陶窑 6、净水台 3，投料时快照），
    /// 两类共享每角色每世界日 12（按完成日，剩余不足给剩余，满级 0），与主动制造/建造经验独立，不另给烹饪；取消/原料不全/读档/领取都不给。
    /// B32e（Q380 选 B，S08「2026-11-10 Q380」）原为每座一个加工位＋等待队列；B51b（S08 文首 Q701.2、B51 Q721／Q727，存档 55）起改为每座最多 10 批全部并行：
    /// 成功投料的每批立即从自己的投料时刻开始，每个世界 5 分钟段对段首在办的每批各 +5，各自按本批耗时完成；没有等待批。投料时本批实际绑定的有期限输入暂停保质（Q701.1），
    /// 完成核销、取消／失败释放时恢复 C＋R。「取消本批」只绑定所点批次的稳定 batchId（不认整列表版本），本批已结束提示「加工批次已变化，请重新选择。」。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>被动批次的配方：工坊是陶窑或固定净水台、模式为 passive/passive_heat。</summary>
        public bool IsStationBatchRecipe(RecipeConfig rc) => rc.IsPassive && !rc.IsPot && rc.Station is "kiln" or "water_station";

        /// <summary>Q701.2：每座工坊最多同时加工的未完成批数（原 Q380 含加工中 10 批上限转为 10 并行批上限）。</summary>
        public const int StationParallelCap = 10;

        /// <summary>Q721：新的独立请求中原批已结束／不在原设施（逐字稿 B51 表 changedBatch）。</summary>
        public const string StationBatchChanged = "加工批次已变化，请重新选择。";

        /// <summary>Q727：有加工批时才显示一次的并行说明（逐字稿 B51 表 parallelHelp）。</summary>
        public const string StationParallelHelp = "各批同时加工，每座最多10批。";

        /// <summary>批次按钮（逐字稿 B51 表 cancelBatch）。</summary>
        public const string StationCancelLabel = "取消本批";

        /// <summary>某区这座工坊的全部未完成批次，按原创建顺序（列表先后＝创建先后）。</summary>
        private static List<StationBatch> StationList(FacilityState? fac, string stationId) =>
            fac?.StationBatches.Where(b => b.StationId == stationId).ToList() ?? new List<StationBatch>();

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

        /// <summary>Q701.4 设施主标题：「加工中{批数}批（最多10批）」。</summary>
        public static string StationTitle(int batches) => $"加工中{batches}批（最多{StationParallelCap}批）";

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
            return $"{ins}{fuel} → {outs}，被动{rc.BaseMinutes}世界分钟（不占行动，离开也继续），完成后放在本区地面；取消退回原料，时间不退";
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
        /// 在当前区的陶窑/净水台投 batches 批（0 分钟、不占主行动；S08 Q380、Q701.2）：所有新批的材料与燃料一次校验，逐批分配并移到该区地面预留（每份只归一批），
        /// 全部成功或全部拒绝。新批都立即从现在开始加工（不等旧批、不补旧进度）；本批实际绑定的有期限输入在同一事务暂停保质（Q701.1）。同一 requestId 只生效一次。
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
                foreach (var kv in rc.AllInputs)
                {
                    var need = kv.Value;
                    foreach (var b in UsableBatches(s, kv.Key, true).Where(i => !IsLocked(s, i)).ToList())
                    {
                        if (need <= 0) break;
                        var take = Math.Min(need, b.Count);
                        ItemInstance moved;
                        if (take < b.Count) moved = SplitOff(s, b, take);
                        else moved = b;
                        moved.Owner = place;
                        moved.PlacedOrder = null;
                        moved.ReservedBy = id;
                        PauseShelf(s, moved);   // Q701.1：成功投料的本批输入暂停保质（无期限不造 R）
                        locked.Add(moved.InstanceId);
                        need -= take;
                    }
                }
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

        /// <summary>取消或失败时把这一批的预留全部释放在设施所在区地面，暂停保质的恢复 C＋R（Q701.1）。</summary>
        private void ReleaseStationBatch(GameState s, StationBatch b)
        {
            foreach (var it in s.Items.Where(i => i.ReservedBy == b.BatchId).ToList())
            {
                it.ReservedBy = null;
                ResumeReleased(s, it);
            }
        }

        /// <summary>BM5a（02 Q563 选 A，照 Q446⑦ 最小删词）：工坊「取消本批」战斗中的拒绝句；普通敌踪卡在场不挡。</summary>
        public const string StationBattleBlocked = "战斗时不能操作";

        /// <summary>
        /// 「取消本批」（S08 文首 Q701.2、B51 Q721／Q727）：只取消当前区这座工坊里所点中的那一批（稳定 batchId），释放该批全部未核销材料和燃料到本区地面
        /// （暂停保质的恢复 C＋R），已过时间不退，没有产物和经验；其他批继续原进度。只能在当地操作，0 分钟。
        /// 不认整列表版本：其他批完成、取消、新投或本批正常进度更新都不影响；本批已完成／已取消／不在这座设施时「加工批次已变化，请重新选择。」，不改指另一批。
        /// seenSeq 为点击时所见的显示序号（日志用；缺省取当前位置）。同一 requestId 只生效一次（重复不双返）。
        /// </summary>
        public ActionResult CancelStationBatch(GameState s, string stationId, int? batchId, string? requestId = null, int? seenSeq = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (s.Progress.Dead) return ActionResult.Fail("本局已结束");
            if (s.Battle != null) return ActionResult.Fail(StationBattleBlocked);
            var place = s.Ground;
            var fac = FacOf(s, place);
            var list = StationList(fac, stationId);
            if (batchId == null) return ActionResult.Fail(list.Count == 0 ? $"当前区的{StationName(stationId)}没有进行中的加工批次。" : StationBatchChanged);
            var idx = list.FindIndex(x => x.BatchId == batchId.Value);
            if (fac == null || idx < 0) return ActionResult.Fail(StationBatchChanged);
            var b = list[idx];
            var seq = seenSeq ?? idx + 1;
            var hasReturn = s.Items.Any(i => i.ReservedBy == b.BatchId);
            fac.StationBatches.Remove(b);
            var restored = _restoreSink = new List<ShelfRestore>();
            try { ReleaseStationBatch(s, b); }
            finally { _restoreSink = null; }
            Normalize(s, place);
            RememberRequest(s, requestId);
            var zone = ZoneName(s, place);
            var head = $"已取消{zone}{StationName(stationId)}的批次{seq} · {RecipeNameOf(b.RecipeId)}（已进行{Clock.Duration(b.DoneMinutes)}）。";
            // Q727（逐字稿 B51 表 batchCancelResult／batchCancelNoReturn）：返料→恢复保质分句（可为空）→后果→已耗时间
            Emit(s, GameEventKind.ActionCancelled, hasReturn
                ? $"{head}未消耗原料已退回{zone}地面。{RestoreClauses(restored, false)}没有产物和经验。已耗时间保留。"
                : $"{head}没有可返还的剩余原料，没有产物和经验。已耗时间保留。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>
        /// 每个世界 5 分钟段（Q701.2）：每区每座工坊段首在办的每一批各 +5 分钟（世界只推进一次），达标即重验该批预留、一次核销原料与燃料、产物落该区地面；
        /// 预留缺失只终止该批、释放剩余预留（暂停保质的恢复 C＋R），没有产物与经验，不影响其他批。同段完成按 StationSettleOrder 逐批结账。读档按存档进度续算，不重复出。
        /// </summary>
        private void AdvanceStationBatches(GameState s)
        {
            foreach (var (place, fac, b) in StationSettleOrder(s).ToList())
            {
                b.DoneMinutes += Tick;
                if (b.DoneMinutes < b.RequiredMinutes) continue;
                fac.StationBatches.Remove(b);
                var rc = _recipes[b.RecipeId];
                var inputs = s.Items.Where(i => i.ReservedBy == b.BatchId).ToList();
                var ok = inputs.All(i => i.Owner == place) && rc.AllInputs.All(kv => inputs.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) >= kv.Value);
                if (!ok)
                {
                    // B54（S08 Q791）：原失败句后只对本次实际释放、有可信 R 的原料接「{原料名}×{数量}保质至{日期时刻}。」（C＋R；同目的地／物主／卡侧同物同截止合计）
                    var outer = _restoreSink;
                    var restored = _restoreSink = new List<ShelfRestore>();
                    try { ReleaseStationBatch(s, b); }
                    finally { _restoreSink = outer; }
                    Normalize(s, place);
                    Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}{StationName(b.StationId)}：原料不全，这一批没有产物，剩下的原料已解锁。" + FailureRestoreClauses(restored));
                    continue;
                }
                foreach (var it in inputs) s.Items.Remove(it);   // 受保护原料直接核销，不返 R
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

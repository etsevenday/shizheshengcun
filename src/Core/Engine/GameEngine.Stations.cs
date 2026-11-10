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
    /// B32e（Q380 选 B，S08「2026-11-10 Q380」）：每座仍一个加工位，另有等待队列，含加工中最多 10 批；一次投多批时整次校验、逐批预留，
    /// 每 5 分钟段只推进段首加工中的一批，完成（或失败、取消）后由最早等候批接上、从下一段开始计时；可分别取消当前批与全部排队批。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>被动批次的配方：工坊是陶窑或固定净水台、模式为 passive/passive_heat。</summary>
        public bool IsStationBatchRecipe(RecipeConfig rc) => rc.IsPassive && !rc.IsPot && rc.Station is "kiln" or "water_station";

        /// <summary>Q380：每座工坊含加工中最多容纳的未完成批数（S08「2026-11-10 Q380」数量与投料）。</summary>
        public const int StationQueueCap = 10;

        /// <summary>Q380 提交时所见批次或等待名单已变化的反馈（S08 Q380 推进与取消）。</summary>
        public const string StationQueueChanged = "队列已变化，请重新选择。";

        /// <summary>某区这座工坊的全部未完成批次，按队列先后（加工中在前）。</summary>
        private static List<StationBatch> StationQueue(FacilityState? fac, string stationId) =>
            fac?.StationBatches.Where(b => b.StationId == stationId).ToList() ?? new List<StationBatch>();

        /// <summary>当前区这座工坊加工中的一批；没有为 null。</summary>
        public StationBatch? StationBatchHere(GameState s, string stationId) =>
            FacOf(s, s.Ground)?.StationBatches.FirstOrDefault(b => b.StationId == stationId && !b.Queued);

        /// <summary>当前区这座工坊排队中的批次，按先后。</summary>
        public List<StationBatch> StationWaitingHere(GameState s, string stationId) =>
            StationQueue(FacOf(s, s.Ground), stationId).Where(b => b.Queued).ToList();

        /// <summary>当前区这座工坊还能加入的批数（10 减未完成批数）。</summary>
        public int StationQueueRoom(GameState s, string stationId) =>
            Math.Max(0, StationQueueCap - StationQueue(FacOf(s, s.Ground), stationId).Count);

        /// <summary>主角随身与当前区地面上可用、未预留、未锁的这种物品份数（投料来源）。</summary>
        private int StationFreeCount(GameState s, string itemId) =>
            UsableBatches(s, itemId, true).Where(i => !IsLocked(s, i)).Sum(i => i.Count);

        /// <summary>材料与燃料足够的完整批数（不看队列余位）。</summary>
        public int StationBatchAffordable(GameState s, RecipeConfig rc) =>
            rc.AllInputs.Count == 0 ? StationQueueCap : rc.AllInputs.Min(kv => StationFreeCount(s, kv.Key) / Math.Max(1, kv.Value));

        /// <summary>投料批数的可选上限：min(10 − 当前未完成批数, 材料与燃料足够的完整批数)。</summary>
        public int StationBatchMaxNew(GameState s, RecipeConfig rc) =>
            Math.Max(0, Math.Min(StationQueueRoom(s, rc.Station), StationBatchAffordable(s, rc)));

        /// <summary>在当前区投 batches 批被动加工现在不能做的原因；能做为 null（整次校验：余位与全部批的材料、燃料）。</summary>
        public string? StationBatchBlocked(GameState s, RecipeConfig rc, int batches = 1)
        {
            if (!IsStationBatchRecipe(rc)) return "这不是固定工坊的被动加工";
            if (s.Progress.Dead) return "本局已结束";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "战斗中不能投料";
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (s.Main != null || s.Sub != null) return "行动进行中，先完成或取消再投料";
            if (RecipeUnknownReason(s, rc) is string unknown) return unknown;
            foreach (var kv in rc.MinSkills)
            {
                var lv = SkillLevel(s, kv.Key);
                if (lv < kv.Value) return $"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）";
            }
            if (StationBlocked(s, rc) is string station) return station;
            var room = StationQueueRoom(s, rc.Station);
            if (room <= 0) return $"队列已满（{StationQueueCap}批）。";
            // 开发假设 B32e-3：下面两句只在直接调用内核时出现（界面的批数上限已按余位封顶、最少 1 批），策划未给原句
            if (batches < 1) return "投料批数须为正整数";
            if (batches > room) return $"队列只剩{room}批空位（最多{StationQueueCap}批）";
            foreach (var kv in rc.AllInputs)
            {
                var have = StationFreeCount(s, kv.Key);
                var need = kv.Value * batches;
                if (have < need) return $"缺少{ItemName(kv.Key)}（{have}/{need}，只取随身与当前地面）";
            }
            return null;
        }

        /// <summary>
        /// 某批的预计完成时刻：当前世界时刻 + 加工中批的剩余时长 + 排在前面的等待批时长 + 自身加工时长（S08 Q380）。
        /// 完成那一段接上的下一批从下一段开始计时，所以各批首尾相接、没有空档。
        /// </summary>
        private int StationBatchFinish(GameState s, FacilityState fac, StationBatch b)
        {
            var t = s.GameMinute;
            foreach (var x in StationQueue(fac, b.StationId))
            {
                t += Math.Max(0, x.RequiredMinutes - x.DoneMinutes);
                if (x == b) break;
            }
            return t;
        }

        /// <summary>当前区这座工坊再投 batches 批时，新批各自的预计完成时刻（按队尾先后）。</summary>
        public List<int> StationNewFinishes(GameState s, RecipeConfig rc, int batches)
        {
            var t = s.GameMinute + StationQueue(FacOf(s, s.Ground), rc.Station).Sum(x => Math.Max(0, x.RequiredMinutes - x.DoneMinutes));
            var list = new List<int>();
            for (var i = 0; i < Math.Max(1, batches); i++)
            {
                t += rc.BaseMinutes;
                list.Add(t);
            }
            return list;
        }

        /// <summary>这座工坊没有加工中批时，让最早的等候批进入加工中（进度从 0、下一世界段开始推进）。</summary>
        private void PromoteStationQueue(GameState s, FacilityState fac, string stationId)
        {
            var q = StationQueue(fac, stationId);
            if (q.Count == 0 || q.Any(b => !b.Queued)) return;
            var head = q[0];
            head.Queued = false;
            head.DoneMinutes = 0;
            head.StartMinute = s.GameMinute;
        }

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
        /// 预估用（B22d 试玩修复 M418，开发假设 B22d-8）：同一投料者其他在途批次中，预计在同一完成日、且比这一批先（或同一刻排在前面）完成的快照经验合计。
        /// 只用于显示；结算规则不变（仍按每批完成那一刻的余额）。新批次（self 为 null）同一刻完成的在途批次都算在前面。
        /// B32e：排队批的预计完成时刻按队列顺序算（StationBatchFinish）。
        /// </summary>
        private int StationXpPending(GameState s, string actorId, int day, int finish, StationBatch? self)
        {
            var flat = AllFacilityStates(s).SelectMany(x => x.Fac.StationBatches.Select(b => (x.Fac, b))).ToList();
            var selfIdx = self == null ? flat.Count : flat.FindIndex(x => x.b == self);
            var sum = 0;
            for (var i = 0; i < flat.Count; i++)
            {
                var (fac, b) = flat[i];
                if (b == self || b.XpSnapshot <= 0 || b.InitiatorActorId != actorId) continue;
                var f = StationBatchFinish(s, fac, b);
                if (Clock.Day(f) != day) continue;
                if (f < finish || (f == finish && i < selfIdx)) sum += b.XpSnapshot;
            }
            return sum;
        }

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
            var pending = StationXpPending(s, who.Id, day, finishMinute, self);
            var room = Math.Max(0, free - pending);
            var ahead = pending > 0 && room < xp ? $"；先完成的在途批次预计占{Math.Min(pending, free)}" : "";
            return room <= 0 ? $"{DayWord(s, day)}被动批次{SkillName(skill)}经验{(free <= 0 ? "已满" : "预计已满")}{c.DailyCap}{ahead}，完成不再给经验"
                : room < xp ? $"完成后{SkillName(skill)}经验+{room}（陶窑与净水台被动批次合计每日最多{c.DailyCap}，{DayWord(s, day)}只剩{room}{ahead}）"
                : basic + (day == Clock.Day(s.GameMinute) ? "" : $"，按{DayWord(s, day)}额度");
        }

        /// <summary>
        /// 投料前预估：新投 batches 批完成后的制造经验。B32e（Q380「预览按队列和预计完成日计入先完成的在途批次，跨日分开显示」）：
        /// 按排到的预计完成日分组，同一日的新批经验合计、扣掉同一投料者先完成的在途批次，各日沿用单批的原句式（非今日带「按完成日第N日额度」），段间用「；」。
        /// </summary>
        public string StationBatchXpPreview(GameState s, RecipeConfig rc, int batches = 1)
        {
            var xp = StationBatchXpFor(rc.Station);
            var parts = StationNewFinishes(s, rc, batches).GroupBy(Clock.Day)
                .Select(g => StationXpText(s, xp * g.Count(), g.Last(), false)).Distinct().ToList();
            return string.Join("；", parts);
        }

        /// <summary>加工中（或排队中）的批次：完成后的制造经验（按快照与按队列算的预计完成日余额）。</summary>
        public string StationBatchXpNote(GameState s, StationBatch b)
        {
            var fac = AllFacilityStates(s).Select(x => x.Fac).FirstOrDefault(f => f.StationBatches.Contains(b));
            var finish = fac != null ? StationBatchFinish(s, fac, b) : s.GameMinute + Math.Max(0, b.RequiredMinutes - b.DoneMinutes);
            return StationXpText(s, b.XpSnapshot, finish, true, b);
        }

        /// <summary>
        /// 批次成功完成：给投料者（完成时仍存活）快照经验，受完成日共享日额；返回日志附注。只在这里结算一次（产物同一处提交），读档/领取不重给。
        /// </summary>
        private string StationBatchXp(GameState s, StationBatch b)
        {
            var c = Config.Progression.StationXp;
            if (b.XpSnapshot <= 0) return "这一批开始时被动加工不给经验";
            if (b.InitiatorActorId == null || !s.Actors.TryGetValue(b.InitiatorActorId, out var actor)
                || (actor.IsPlayer ? s.Progress.Dead : actor.Stats.Health <= 0)) return "投料者已不在，不给经验";
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
        /// 在当前区的陶窑/净水台投 batches 批（0 分钟、不占主行动；S08 Q380）：所有新批的材料与燃料一次校验，逐批分配并移到该区地面预留（每份只归一批），
        /// 全部成功入队或全部拒绝。新批按提交顺序从队尾加入；空设施时首批立即成为加工中。同一 requestId 只生效一次。
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
                        if (take < b.Count)
                        {
                            b.Count -= take;
                            moved = b.Clone();
                            moved.InstanceId = s.NextInstanceId++;
                            moved.Count = take;
                            s.Items.Add(moved);
                        }
                        else moved = b;
                        moved.Owner = place;
                        moved.PlacedOrder = null;
                        moved.ReservedBy = id;
                        locked.Add(moved.InstanceId);
                        need -= take;
                    }
                }
                fac.StationBatches.Add(new StationBatch
                {
                    BatchId = id, StationId = rc.Station, RecipeId = rc.Id, InputInstanceIds = locked,
                    RequiredMinutes = rc.BaseMinutes, StartMinute = s.GameMinute, InitiatorActorId = GameState.PlayerId,
                    XpSnapshot = StationBatchXpFor(rc.Station), Queued = true,
                });
            }
            PromoteStationQueue(s, fac, rc.Station);
            Normalize(s, ItemInstance.Carried);
            RememberRequest(s, requestId);
            Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}{StationName(rc.Station)}投料（{batches}批）：{StationBatchSummary(rc)}；{preview}。");
            UpdateFlags(s);
            return ActionResult.Success(first);
        }

        /// <summary>取消或失败时把这一批的预留全部释放在设施所在区地面。</summary>
        private static void ReleaseStationBatch(GameState s, StationBatch b)
        {
            foreach (var it in s.Items.Where(i => i.ReservedBy == b.BatchId)) it.ReservedBy = null;
        }

        /// <summary>
        /// 「取消当前批」：取消当前区这座工坊加工中的一批，释放该批全部未核销材料和燃料到本区地面，已过时间不退，没有产物和经验；
        /// 有等待批时队首立即转为加工中（下一世界段起计时）。只能在当地操作，0 分钟。
        /// expectBatchId 为提交时所见的加工中批，已换批时反馈「队列已变化，请重新选择。」；同一 requestId 只生效一次。
        /// </summary>
        public ActionResult CancelStationBatch(GameState s, string stationId, int? expectBatchId = null, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (s.Progress.Dead) return ActionResult.Fail("本局已结束");
            if (s.Battle != null || s.Encounter != null) return ActionResult.Fail("战斗或敌踪待处理时不能操作");
            var place = s.Ground;
            var fac = FacOf(s, place);
            var b = StationBatchHere(s, stationId);
            if (expectBatchId != null && b?.BatchId != expectBatchId) return ActionResult.Fail(StationQueueChanged);
            if (fac == null || b == null) return ActionResult.Fail($"{StationName(stationId)}没有进行中的批次（只能操作当前区的工坊）");
            fac.StationBatches.Remove(b);
            ReleaseStationBatch(s, b);
            PromoteStationQueue(s, fac, stationId);
            Normalize(s, place);
            RememberRequest(s, requestId);
            Emit(s, GameEventKind.ActionCancelled, $"已取消{ZoneName(s, place)}{StationName(stationId)}的这一批（已进行{b.DoneMinutes}分钟，不退时间）：原料退回{ZoneName(s, place)}地面，没有产物。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>
        /// 「取消排队（{批数}批）」：一次取消当前区这座工坊所有尚未开始的批，释放这些批的预留到本区地面，加工中的一批继续。0 分钟，只能在当地操作。
        /// expectWaiting 为提交时所见的等待名单（批次号按先后），名单已变时反馈「队列已变化，请重新选择。」；同一 requestId 只生效一次。
        /// </summary>
        public ActionResult CancelStationQueue(GameState s, string stationId, IReadOnlyList<int>? expectWaiting = null, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (s.Progress.Dead) return ActionResult.Fail("本局已结束");
            if (s.Battle != null || s.Encounter != null) return ActionResult.Fail("战斗或敌踪待处理时不能操作");
            var place = s.Ground;
            var fac = FacOf(s, place);
            var waiting = StationWaitingHere(s, stationId);
            if (expectWaiting != null && !waiting.Select(b => b.BatchId).SequenceEqual(expectWaiting)) return ActionResult.Fail(StationQueueChanged);
            if (fac == null || waiting.Count == 0) return ActionResult.Fail($"{StationName(stationId)}没有排队中的批次（只能操作当前区的工坊）");
            foreach (var b in waiting)
            {
                fac.StationBatches.Remove(b);
                ReleaseStationBatch(s, b);
            }
            Normalize(s, place);
            RememberRequest(s, requestId);
            Emit(s, GameEventKind.ActionCancelled, $"已取消{ZoneName(s, place)}{StationName(stationId)}排队中{waiting.Count}批：原料退回{ZoneName(s, place)}地面，没有产物。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>
        /// 每个世界 5 分钟段：每区每座工坊只推进段首加工中的一批 +5 分钟（Q380），达标即重验该批预留、一次核销原料与燃料、产物落该区地面；
        /// 预留缺失只终止该批、释放剩余预留，没有产物与经验。随后最早等候批接上，从下一段开始推进（同段进度仍为 0）。读档按存档进度续算，不重复出。
        /// </summary>
        private void AdvanceStationBatches(GameState s)
        {
            foreach (var (place, fac) in AllFacilityStates(s).ToList())
            {
                if (fac.StationBatches.Count == 0) continue;
                var heads = fac.StationBatches.GroupBy(b => b.StationId)
                    .Select(g => g.FirstOrDefault(b => !b.Queued)).Where(b => b != null).Select(b => b!).ToList();
                foreach (var b in heads)
                {
                    b.DoneMinutes += Tick;
                    if (b.DoneMinutes < b.RequiredMinutes) continue;
                    fac.StationBatches.Remove(b);
                    var rc = _recipes[b.RecipeId];
                    var inputs = s.Items.Where(i => i.ReservedBy == b.BatchId).ToList();
                    var ok = inputs.All(i => i.Owner == place) && rc.AllInputs.All(kv => inputs.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) >= kv.Value);
                    if (!ok)
                    {
                        ReleaseStationBatch(s, b);
                        PromoteStationQueue(s, fac, b.StationId);
                        Normalize(s, place);
                        Emit(s, GameEventKind.Info, $"{ZoneName(s, place)}{StationName(b.StationId)}：原料不全，这一批没有产物，剩下的原料已解锁。");
                        continue;
                    }
                    foreach (var it in inputs) s.Items.Remove(it);
                    var made = new List<string>();
                    foreach (var kv in rc.Outputs)
                    {
                        CreateItems(s, kv.Key, kv.Value, place);
                        made.Add($"{ItemName(kv.Key)}×{kv.Value}");
                    }
                    PromoteStationQueue(s, fac, b.StationId);
                    Normalize(s, place);
                    var xp = StationBatchXp(s, b);
                    Emit(s, GameEventKind.FacilityDone, $"{ZoneName(s, place)}{StationName(b.StationId)}：{string.Join("、", made)}已完成，放在{ZoneName(s, place)}地面；{xp}。");
                }
            }
        }
    }
}

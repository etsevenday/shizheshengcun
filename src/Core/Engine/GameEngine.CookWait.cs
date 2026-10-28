using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>等待烹饪的一个绑定目标（S11 §9）：火堆里某个加工进程（直烤、净水、水煮、复煮）。</summary>
    public sealed class CookTargetInfo
    {
        /// <summary>设施进程 ID（FacilityProcess.ProcessId），等待绑定的就是它。</summary>
        public string ProcessId { get; set; } = "";
        public int TargetInstanceId { get; set; }
        /// <summary>“生块根#21 · 水煮”“金属锅#7 · 烧水”。</summary>
        public string Label { get; set; } = "";
        /// <summary>按当前燃料预计从现在起多少游戏分钟完成；燃料不足完不成为 null。</summary>
        public int? EtaMinutes { get; set; }
        public bool Done { get; set; }
    }

    /// <summary>火堆里没被纳入本次等待的物品及原因（空锅、仅成品、只存放、缺水、首次受热前就会腐败……）。</summary>
    public sealed class CookExcludedInfo
    {
        public int InstanceId { get; set; }
        public string Label { get; set; } = "";
        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// 等待烹饪的只读解析（S11 §9、S14 §11.3）：开始前悬停预览与执行中中央条共用。全部在状态副本上模拟，不推进真实时间、不改随机。
    /// </summary>
    public sealed class CookWaitPlan
    {
        /// <summary>现在能不能开始（Reason 为不能的原因）。</summary>
        public bool Ok { get; set; }
        public string? Reason { get; set; }
        public string LocationId { get; set; } = "";
        public List<CookTargetInfo> Targets { get; set; } = new List<CookTargetInfo>();
        public List<CookExcludedInfo> Excluded { get; set; } = new List<CookExcludedInfo>();
        /// <summary>按当前燃料全部完成的预计剩余游戏分钟（并行取最晚，不相加）；燃料不足完不成为 null。</summary>
        public int? EstimatedMinutes { get; set; }
        /// <summary>连续供热时全部完成还需的有效分钟（依赖已计入）。</summary>
        public int NeededEffectiveMinutes { get; set; }
        /// <summary>当前燃料不够全部完成：耗尽时停止。</summary>
        public bool FuelShort { get; set; }
        /// <summary>“尚需至少45有效分钟，当前燃料不足，耗尽时停止”；够用时为 null。</summary>
        public string? FuelNote { get; set; }
        /// <summary>等待期间的身体每小时变化（同普通等待 wait 速率，不另加制作消耗或休息恢复）。</summary>
        public Config.RateBlock BodyRates { get; set; } = new Config.RateBlock();
    }

    public sealed partial class GameEngine
    {
        private const int CookSimMaxSegments = 24 * 12;

        /// <summary>
        /// 等待烹饪的解析（S11 §9）：本地火堆当前全部可加工进程中，连续供热下能完成的才纳入（含锅内脏水净化后供水的食材链）；
        /// 空锅、仅成品、只存放、缺水、首次受热前就会腐败的列入未纳入及原因（Q16：能取得受热保护的新鲜食材不因熟前过原期限排除）。预计时间按当前燃料模拟，并行取最晚完成；燃料不足明确说明。
        /// 只读：在副本上模拟，不推进真实时间、不改真实随机。
        /// </summary>
        public CookWaitPlan PreviewCookWait(GameState s)
        {
            var loc = s.CurrentLocation;
            var plan = new CookWaitPlan { LocationId = loc, BodyRates = Config.HourlyRates[RateKeyFor(ActionType.WaitCook)].Copy() };
            if (!_locs.TryGetValue(loc, out var lc) || !lc.HasFire) { plan.Reason = "这里没有火堆"; return plan; }
            AnalyzeCook(s, loc, null, plan);
            plan.Reason = Validate(s, ActionRequest.WaitCook(), out _);
            plan.Ok = plan.Reason == null;
            return plan;
        }

        private string RateKeyFor(ActionType t)
        {
            var raw = RateKey(t);
            return Config.Actions.RateAliases.TryGetValue(raw, out var alias) ? alias : raw;
        }

        /// <summary>
        /// 解析目标并估时：bound 为 null 时从火堆现有进程中挑选（开始前）；否则只看这些已绑定的进程（执行中/读档后按真实进度重算）。
        /// </summary>
        private void AnalyzeCook(GameState s, string loc, IReadOnlyCollection<string>? bound, CookWaitPlan plan)
        {
            var c = s.Clone();
            SimReadDialogues(c);
            SyncFacility(c, loc);
            var fac = c.Locations[loc].Facility;
            var candidates = bound != null
                ? bound.ToList()
                : fac.Processes.Where(p => { var t = FindItem(c, p.TargetInstanceId); return t != null && !IsSpoiled(c, t); }).Select(p => p.ProcessId).ToList();

            // 连续供热（燃料视为充足）：谁能完成、各需多少有效分钟
            var ideal = SimCook(c, loc, candidates, infiniteFuel: true, out var idealFail);
            var targets = bound != null ? candidates : candidates.Where(ideal.ContainsKey).ToList();
            // 按当前燃料：预计完成时刻
            var real = SimCook(c, loc, targets, infiniteFuel: false, out _);

            plan.Targets = targets.Select(pid =>
            {
                var p = fac.Processes.FirstOrDefault(x => x.ProcessId == pid);
                var t = p != null ? FindItem(c, p.TargetInstanceId) : null;
                return new CookTargetInfo
                {
                    ProcessId = pid,
                    TargetInstanceId = p?.TargetInstanceId ?? 0,
                    Label = t == null ? pid : $"{ItemName(t.ItemId)}#{t.InstanceId} · {Process(p!.RecipeId).Name}",
                    EtaMinutes = real.TryGetValue(pid, out var m) ? m : (int?)null,
                    Done = p == null,
                };
            }).ToList();
            plan.NeededEffectiveMinutes = targets.Count == 0 ? 0 : targets.Max(pid => ideal.TryGetValue(pid, out var m) ? m : 0);
            var allReal = targets.Count > 0 && targets.All(real.ContainsKey);
            plan.EstimatedMinutes = allReal ? targets.Max(pid => real[pid]) : (int?)null;
            plan.FuelShort = targets.Count > 0 && !allReal;
            if (plan.FuelShort)
                plan.FuelNote = $"尚需至少{plan.NeededEffectiveMinutes}有效分钟，当前燃料不足，耗尽时停止";

            // 未纳入：火堆与火堆内锅里不是目标的东西
            var targetItems = new HashSet<int>(plan.Targets.Select(t => t.TargetInstanceId));
            foreach (var it in FireContents(c, loc))
            {
                var list = new List<ItemInstance> { it };
                if (IsPot(it)) list.AddRange(PotFood(c, it.InstanceId));
                foreach (var x in list)
                {
                    if (targetItems.Contains(x.InstanceId)) continue;
                    var pid = fac.Processes.FirstOrDefault(p => p.TargetInstanceId == x.InstanceId)?.ProcessId;
                    string why;
                    if (IsPot(x)) why = PotLiquid(c, x.InstanceId) == null && PotFood(c, x.InstanceId).Count == 0 ? "空锅，不需要烹饪" : "锅里没有需要净化的水";
                    else if (IsSpoiled(c, x)) why = "已腐败，不再加工";
                    else if (pid == null) why = x.InContainer && PotConfig(ContainerOf(c, x)!).StoreOnlyFoodIds.Contains(x.ItemId) ? "只存放，不再加工" : "已是成品，不需要烹饪";
                    else if (idealFail.TryGetValue(pid, out var f)) why = f;
                    else why = "本次无法完成加工";
                    plan.Excluded.Add(new CookExcludedInfo { InstanceId = x.InstanceId, Label = $"{ItemName(x.ItemId)}#{x.InstanceId}", Reason = why });
                }
            }
        }

        /// <summary>
        /// 在副本上按 wait 速率逐段推进（每段一次世界 tick），记录各进程从现在起完成所需的游戏分钟。
        /// infiniteFuel 时把该火堆视为持续燃烧（求“尚需有效分钟”）；完不成的写入 fail（腐败/缺水/消失）。
        /// </summary>
        private Dictionary<string, int> SimCook(GameState src, string loc, List<string> pids, bool infiniteFuel, out Dictionary<string, string> fail)
        {
            var done = new Dictionary<string, int>();
            fail = new Dictionary<string, string>();
            if (pids.Count == 0) return done;
            var c = src.Clone();
            var fac = c.Locations[loc].Facility;
            if (infiniteFuel) { fac.FireLit = true; fac.FuelMinutes = Config.Fire.MaxFuelMinutes * 1000; }
            c.Sub = null;
            c.Battle = null;
            c.Encounter = null;
            c.Main = new ActionState
            {
                ActionId = -1, Type = ActionType.Wait, PlannedMinutes = int.MaxValue / 2, Label = "模拟等待",
                SourceLocation = loc, StartMinute = c.GameMinute, EncounterRoll = 1,
            };
            var t0 = c.GameMinute;
            var open = new List<string>(pids);
            var stuck = 0;
            for (var k = 0; k < CookSimMaxSegments && open.Count > 0 && !c.Progress.Dead; k++)
            {
                SimReadDialogues(c);
                if (c.Main == null || c.Main.ActionId != -1) break;
                if (c.Main.Status == ActionStatus.Paused) Resume(c);
                if (!FireHot(c, loc)) break;
                var heatBefore = open.Sum(pid => fac.Processes.FirstOrDefault(p => p.ProcessId == pid)?.HeatDoneMinutes ?? 0);
                Advance(c);
                var progressed = false;
                foreach (var pid in open.ToList())
                {
                    if (fac.Processes.Any(p => p.ProcessId == pid)) continue;
                    open.Remove(pid);
                    progressed = true;
                    if (fac.Completed.Any(x => x.StartsWith(pid + "@"))) done[pid] = c.GameMinute - t0;
                    else fail[pid] = "目标已不在，本次无法完成";
                }
                foreach (var pid in open.ToList())
                {
                    var p = fac.Processes.First(x => x.ProcessId == pid);
                    var t = FindItem(c, p.TargetInstanceId);
                    // Q16：受热保护的食材不再因原期限停止；只有首次有效受热前就到期的才会在这里腐败
                    if (t != null && IsSpoiled(c, t)) { open.Remove(pid); fail[pid] = "首次受热前就会腐败，不能烹饪"; }
                }
                var heatAfter = open.Sum(pid => fac.Processes.FirstOrDefault(p => p.ProcessId == pid)?.HeatDoneMinutes ?? 0);
                // 火堆其他进程（如本锅净水）也算推进路径：所有开放目标和其所在锅的净水都不动才算卡住
                var chain = fac.Processes.Any(p => Process(p.RecipeId).IsPotBatch && p.Status == ProcessStatus.Heating
                                                   && open.Any(pid => fac.Processes.First(x => x.ProcessId == pid).PotInstanceId == p.TargetInstanceId));
                stuck = progressed || heatAfter > heatBefore || chain ? 0 : stuck + 1;
                if (stuck >= 2) break;
            }
            foreach (var pid in open)
            {
                if (fail.ContainsKey(pid)) continue;
                var p = fac.Processes.FirstOrDefault(x => x.ProcessId == pid);
                var t = p == null ? null : FindItem(c, p.TargetInstanceId);
                fail[pid] = t == null ? "本次无法完成加工" : CookBlockedText(ContentStatus(c, t));
            }
            return done;
        }

        private static string CookBlockedText(string status)
        {
            if (status.StartsWith("待")) return $"本次分不到加工用水（{status}）";
            if (status.Length == 0) return "本次无法完成加工";
            return status;
        }

        // ───────────── 行动：开始校验、每段检查、结束 ─────────────

        /// <summary>Validate 的等待烹饪分支：空闲、本地有燃烧的火堆、下一段燃料有效（开发假设 A8）、至少一个可完成的目标。</summary>
        private string? ValidateCookWait(GameState s, Plan plan)
        {
            if (plan.IsSub) return "主行动暂停中：请先退出原行动再等待烹饪";
            var here = s.CurrentLocation;
            if (!_locs[here].HasFire) return "这里没有火堆";
            var fac = s.Locations[here].Facility;
            if (!fac.FireLit) return "火堆未燃烧：请先点火（等待烹饪不会自动点火）";
            if (fac.FuelMinutes < Tick) return "火堆燃料不够下一段，请先投入木材（等待烹饪不会自动加木）";
            var info = new CookWaitPlan { LocationId = here };
            AnalyzeCook(s, here, null, info);
            if (info.Targets.Count == 0)
                return info.Excluded.Count > 0
                    ? "火堆里没有可加工的目标：" + string.Join("；", info.Excluded.Select(x => $"{x.Label}{x.Reason}"))
                    : "火堆里没有可加工的目标";
            plan.TargetId = here;
            plan.Planned = info.EstimatedMinutes ?? info.NeededEffectiveMinutes;
            plan.CookTargets = info.Targets.Select(t => t.ProcessId).ToList();
            plan.CookTargetItems = info.Targets.Select(t => t.TargetInstanceId).ToList();
            plan.Label = "等待烹饪";
            return null;
        }

        /// <summary>段前：火堆已不能给下一段完整供热（熄灭/燃料不足5分钟）就停止等待，不再 tick（不自动点火、加木）。</summary>
        private bool CookWaitStoppedBeforeSegment(GameState s, ActionState a)
        {
            if (a.Type != ActionType.WaitCook || FireHot(s, a.TargetId)) return false;
            var f = s.Locations[a.TargetId].Facility;
            StopCookWait(s, a, f.FireLit ? "火堆燃料不够下一段" : "火堆已熄灭");
            return true;
        }

        /// <summary>
        /// 段后：先登记本段完成的目标；全部完成即结束（燃料恰好够最后一段也按完成）；否则目标腐败/消失、火堆没法给下一段供热、
        /// 或已无有效推进路径时停止并说明原因。返回 true 表示行动已结束（完成或停止），completed 区分二者。
        /// </summary>
        private bool CookWaitAfterSegment(GameState s, ActionState a, out bool completed)
        {
            completed = false;
            var loc = a.TargetId;
            var fac = s.Locations[loc].Facility;
            foreach (var pid in a.CookTargets.Where(p => !a.CookDone.Contains(p)).ToList())
            {
                if (fac.Processes.Any(p => p.ProcessId == pid)) continue;
                if (fac.Completed.Any(x => x.StartsWith(pid + "@"))) a.CookDone.Add(pid);
                else { StopCookWait(s, a, "有目标已不在火堆里"); return true; }
            }
            var open = a.CookTargets.Where(p => !a.CookDone.Contains(p)).ToList();
            if (open.Count == 0)
            {
                ClearAction(s, a);
                Emit(s, GameEventKind.ActionCompleted, $"等待烹饪结束：{LocName(loc)}火堆{a.CookTargets.Count}项加工全部完成，成品留在原位（{Clock.Duration(a.ElapsedMinutes)}）。");
                completed = true;
                return true;
            }
            foreach (var pid in open)
            {
                var p = fac.Processes.First(x => x.ProcessId == pid);
                var t = FindItem(s, p.TargetInstanceId);
                if (t != null && IsSpoiled(s, t)) { StopCookWait(s, a, $"{ItemName(t.ItemId)}#{t.InstanceId}已腐败"); return true; }
            }
            if (!FireHot(s, loc))
            {
                StopCookWait(s, a, fac.FireLit ? "火堆燃料不够下一段" : "火堆燃料耗尽、已熄灭");
                return true;
            }
            // 下一段还有没有推进路径：任一开放目标可受热，或它所在锅的脏水还在净化
            var path = open.Any(pid =>
            {
                var p = fac.Processes.First(x => x.ProcessId == pid);
                if (HeatEligible(s, loc, p)) return true;
                return p.PotInstanceId.HasValue && fac.Processes.Any(b => Process(b.RecipeId).IsPotBatch && b.TargetInstanceId == p.PotInstanceId && HeatEligible(s, loc, b));
            });
            if (!path) { StopCookWait(s, a, "剩下的目标已无法继续加工（缺水或条件失效）"); return true; }
            return false;
        }

        /// <summary>停止等待：不取锅、不清受热进度、不解除仍合法的水预留、不灭火、不领取成品；被动加工照旧。</summary>
        private void StopCookWait(GameState s, ActionState a, string reason)
        {
            var left = a.CookTargets.Count - a.CookDone.Count;
            ClearAction(s, a);
            Emit(s, GameEventKind.ActionAborted, $"等待烹饪停止：{reason}。已完成{a.CookDone.Count}项、未完成{left}项，原料与受热进度原样保留。");
        }
    }
}

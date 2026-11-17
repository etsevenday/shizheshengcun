using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 永久丢弃的正式文字（逐字稿文首「2026-11-15 B50a Q763—Q768与永久丢弃」表与「2026-11-17 B53主要原批Q857—Q861」§Q859 水句；
    /// 合同 assets/20261117/b53-discard-main-q857-q861.json water.text）。{物品清单}／{内容清单}每项写法 Q864 待答，见 <see cref="GameEngine.DiscardItemLabel"/>（暂用）。
    /// </summary>
    public static class DiscardCopy
    {
        public const string Discard = "丢弃";
        public const string DiscardWater = "丢弃水";
        public const string ChooseQuantity = "选择数量";
        public const string DiscardAll = "全部丢弃";
        public const string Quantity = "数量";
        public const string Next = "下一步";
        public const string Cancel = "取消";
        public const string Back = "返回";
        public const string ConfirmTitle = "确认丢弃";
        public const string Confirm = "确认丢弃";
        public const string Irreversible = "丢弃后无法找回。";
        public const string Busy = "物品被占用，不能丢弃。";
        public const string Processing = "加工中：先停止加工。";
        public const string Worn = "先卸下再丢弃。";
        public const string Deployed = "先收起再丢弃。";
        public const string Changed = "物品已变，请重选。";
        public const string NoPermission = "当前无法丢弃该物品。";
        public const string NoWater = "无可丢弃的水。";

        public static string Available(int n) => $"可丢弃：{n}";
        public static string InvalidQuantity(int max) => $"请输入1至{max}的整数。";
        public static string ConfirmItems(string list) => $"永久丢弃：{list}。";
        public static string Contents(string list) => $"包含：{list}。";
        public static string Result(string list) => $"已永久丢弃：{list}。";
        public static string ResultContents(string list) => $"同时丢弃：{list}。";
        public static string WaterAvailable(int ml) => $"可丢弃：{ml}毫升";
        public static string WaterQuantity(int unitMl) => $"数量（每份{unitMl}毫升）";
        public static string WaterConfirm(string water, int ml) => $"永久丢弃：{water}{ml}毫升。";
        public static string WaterResult(string water, int ml) => $"已永久丢弃：{water}{ml}毫升。";
    }

    /// <summary>丢弃对象的种类：同组物品（随身／当前地面同物品同卡侧）、明确的一件（工具、锅、水壶、锅内食材）、锅里的水、水壶里的水。</summary>
    public enum DiscardKind { Group, Instance, PotWater, CanteenWater }

    /// <summary>
    /// 玩家这次要丢弃的真实范围（S06 文首 2026-11-17 §Q858 选 A）：Group 带 BatchKey 时只处理玩家明确手选的那一批；BatchKey 为空才按同 owner／物品／卡侧整组。
    /// 自动默认批不算手选，界面不要把默认批写进 BatchKey。B53b 无右栏清单行复用同一对象（扩 <see cref="GameEngine.DiscardOwnerAllowed"/> 的可处置范围）。
    /// </summary>
    public sealed class DiscardTarget
    {
        public DiscardKind Kind { get; set; }
        public string Owner { get; set; } = "";
        /// <summary>Group：物品 ID；PotWater：水种。</summary>
        public string ItemId { get; set; } = "";
        /// <summary>Group：卡侧（CardSideOf，普通为 null）。</summary>
        public string? Side { get; set; }
        /// <summary>Group：玩家明确手选的批（批次键去掉预留段，见 <see cref="GameEngine.DiscardBatchKeyOf"/>）；null＝未手选。</summary>
        public string? BatchKey { get; set; }
        /// <summary>Instance：那一件；PotWater：锅；CanteenWater：水壶。</summary>
        public int? InstanceId { get; set; }

        public static DiscardTarget Group(string owner, string itemId, string? side, string? batchKey = null) =>
            new DiscardTarget { Kind = DiscardKind.Group, Owner = owner, ItemId = itemId, Side = side, BatchKey = batchKey };
        public static DiscardTarget Instance(int instanceId) => new DiscardTarget { Kind = DiscardKind.Instance, InstanceId = instanceId };
        public static DiscardTarget PotWater(int potInstanceId, string liquidId) => new DiscardTarget { Kind = DiscardKind.PotWater, InstanceId = potInstanceId, ItemId = liquidId };
        public static DiscardTarget CanteenWater(int canteenInstanceId) => new DiscardTarget { Kind = DiscardKind.CanteenWater, InstanceId = canteenInstanceId };

        public string Key => $"{Kind}|{Owner}|{ItemId}|{Side}|{BatchKey}|{InstanceId}";
        public bool IsWater => Kind == DiscardKind.PotWater || Kind == DiscardKind.CanteenWater;
    }

    /// <summary>丢弃子区的只读数据：物品按份／件，水按毫升。</summary>
    public sealed class DiscardView
    {
        public bool IsWater { get; set; }
        /// <summary>物品：可丢弃份数；水：可丢弃毫升（含不足一份的尾水）。</summary>
        public int Available { get; set; }
        /// <summary>物品：本次范围总份数（含被占用的）；水：容器里该水种总毫升。</summary>
        public int Total { get; set; }
        /// <summary>水：每份毫升（沿该容器现行水卡转移单位）；物品为 0。</summary>
        public int UnitMl { get; set; }
        /// <summary>「选择数量」的上限 N：物品＝可丢弃份数；水＝floor(可丢弃毫升／每份)，为 0 时数量路径禁用。</summary>
        public int MaxUnits { get; set; }
        /// <summary>无可丢弃时的最优先真实原因（有可丢弃时为 null）。</summary>
        public string? Blocked { get; set; }
        /// <summary>水种名（水）。</summary>
        public string WaterName { get; set; } = "";
    }

    /// <summary>最终确认：本次真实清单与提交快照（S06 §Q858「进入最终确认前固定真实实例／批次／数量／owner／容器内容」）。</summary>
    public sealed class DiscardPlanView
    {
        public string Stamp { get; set; } = "";
        public bool All { get; set; }
        /// <summary>物品份数（数量路径的 N；全部为本次全部可丢弃份数）；水为份数（全部时 0）。</summary>
        public int Count { get; set; }
        /// <summary>水：本次删除毫升。</summary>
        public int WaterMl { get; set; }
        /// <summary>确认正文各行：「永久丢弃：…。」（含容器时「包含：…。」）与末句「丢弃后无法找回。」。</summary>
        public List<string> Lines { get; set; } = new List<string>();
        /// <summary>成功日志（一条）。</summary>
        public string ResultText { get; set; } = "";
    }

    public sealed class DiscardRequest
    {
        public DiscardTarget Target { get; set; } = new DiscardTarget();
        public int Count { get; set; } = 1;
        public bool All { get; set; }
        /// <summary>确认时的 <see cref="DiscardPlanView.Stamp"/>；提交重算后不同整次拒绝「物品已变，请重选。」。</summary>
        public string Stamp { get; set; } = "";
        public string? RequestId { get; set; }
    }

    /// <summary>
    /// 永久丢弃（B53a；S06／S10／S14 文首「2026-11-15 B50a Q763—Q768与永久丢弃」与「2026-11-17 B53主要原批Q857—Q861」，验收 PD-A01—A07、B53-Q857-02—Q860-01）：
    /// 右栏「丢弃」→「选择数量／全部丢弃」→ 最终确认 → 0 世界分钟、0 AP 原子删除实物（不落地、不进箱、不返料、不发经验／产物）。
    /// 只处理当前可合法处置、未被加工／预留／行动占用的真实份额；确认时固定真实清单快照，提交时重算比对，有变化整次拒绝；带请求号幂等。
    /// 锅水／壶水「丢弃水」只删确认的毫升，保留容器与其他内容（§Q859 选 B）。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── 资格 ─────────────

        /// <summary>整理输入锁（死亡、战斗、对白、主／子行动）：沿原整理拒绝句；丢弃不享受撤出途中拾放的例外。</summary>
        private string? DiscardInputLock(GameState s)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (s.Battle != null) return "战斗中不能整理物品；换武器、装填与用药请用战斗指令";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (IsBusy(s)) return "行动进行中，不能整理物品";
            return null;
        }

        /// <summary>
        /// 本批可处置的来源（B53a）：主角随身、当前区地面、当前区火堆加工位、根在随身或当前地面的锅里。同伴随身／柜子／遗物认领／回收／封存（无右栏清单）归 B53b，在这里扩。
        /// </summary>
        public bool DiscardOwnerAllowed(GameState s, ItemInstance i)
        {
            if (i.InContainer) return ContainerOf(s, i) is ItemInstance pot && DiscardOwnerAllowed(s, pot);
            if (ItemInstance.TryFacilityId(i.Owner, out var fid)) return FacilityLocation(fid) == s.Ground;
            return i.Owner == ItemInstance.Carried || i.Owner == s.Ground;
        }

        /// <summary>这一件是否被加工绑定：火堆／锅加工记录未完成、锅食谱辅料、晾制位里未完成的原料、工坊批次输入；锅连内部任一子物。</summary>
        private bool DiscardProcessing(GameState s, ItemInstance i)
        {
            if (AllProcesses(s).Any(p => p.Status != ProcessStatus.Done && (TouchesItem(p, i.InstanceId) || p.PotInstanceId == i.InstanceId))) return true;
            if (i.InContainer && IsPotAux(s, i)) return true;
            if (ItemInstance.TryDrying(i.Owner, out var place, out var slot) && DryingSlotAt(s, place, slot) is DryingSlot b && b.Status != DryingSlot.Done) return true;
            if (AllFacilityStates(s).Any(x => x.Fac.StationBatches.Any(sb => sb.InputInstanceIds.Contains(i.InstanceId)))) return true;
            if (IsPot(i)) return PotContents(s, i.InstanceId).Any(c => DiscardProcessing(s, c));
            return false;
        }

        private bool DiscardOccupied(GameState s, ItemInstance i) =>
            i.ReservedBy != null || IsLocked(s, i) || (IsPot(i) && PotContents(s, i.InstanceId).Any(c => c.ReservedBy != null || IsLocked(s, c)));

        /// <summary>
        /// 这一件为什么不能永久丢弃（可以为 null）。多条同时命中时只出一句，暂按 加工＞预留占用＞穿戴＞部署＞无权限（Q866 待答，开发方暂用其选项 A 的顺序）；
        /// 输入锁另在整体先判。首战教学样本不因教学身份拒绝（§Q860 选 A）；借用物等无处置权用「当前无法丢弃该物品。」。
        /// </summary>
        public string? DiscardItemBlock(GameState s, ItemInstance i)
        {
            if (DiscardProcessing(s, i)) return DiscardCopy.Processing;
            if (DiscardOccupied(s, i)) return DiscardCopy.Busy;
            if (i.IsWorn) return DiscardCopy.Worn;
            if (IsDeployed(s, i)) return DiscardCopy.Deployed;
            if (i.LoanSlot != null || !DiscardOwnerAllowed(s, i)) return DiscardCopy.NoPermission;
            return null;
        }

        /// <summary>手选批的批次身份：真实批次键去掉预留段（同一批里预留的份与未预留的份是同一批，§Q858 例「该批预留1只可删2」）。</summary>
        public static string DiscardBatchKeyOf(ItemInstance i)
        {
            if (i.ReservedBy == null) return BatchKeyOf(i);
            var c = i.Clone();
            c.ReservedBy = null;
            return BatchKeyOf(c);
        }

        private static int QualityRank(ItemInstance i) => i.Quality switch { null => 0, "white" => 0, "green" => 1, "blue" => 2, _ => 3 };

        /// <summary>本次范围内的全部实物（含不可丢的），按既定序：真实有效到期近→远、无期限在后，同到期品质白→绿→蓝，再实例号（S06 §数量范围、Q757 组内顺序）。</summary>
        private List<ItemInstance> DiscardScope(GameState s, DiscardTarget t)
        {
            IEnumerable<ItemInstance> q;
            if (t.Kind == DiscardKind.Instance)
                q = t.InstanceId is int id && FindItem(s, id) is ItemInstance one ? new[] { one } : Array.Empty<ItemInstance>();
            else
            {
                q = s.Items.Where(i => i.Owner == t.Owner && i.ItemId == t.ItemId && CardSideOf(i) == t.Side);
                if (t.BatchKey != null) q = q.Where(i => DiscardBatchKeyOf(i) == t.BatchKey);
            }
            return q.OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(QualityRank).ThenBy(i => i.InstanceId).ToList();
        }

        private List<ItemInstance> DiscardLegal(GameState s, List<ItemInstance> scope) =>
            scope.Where(i => !DueToSpoil(s, i) && DiscardItemBlock(s, i) == null).ToList();

        // ───────────── 子区数据 ─────────────

        /// <summary>丢弃子区：可丢弃数（水为毫升）、总数、每份毫升、数量上限与无可丢弃时的原因。</summary>
        public DiscardView DiscardViewOf(GameState s, DiscardTarget t)
        {
            if (t.IsWater) return WaterDiscardView(s, t);
            var v = new DiscardView();
            var scope = DiscardScope(s, t);
            v.Total = scope.Sum(i => i.Count);
            if (DiscardInputLock(s) is string lockWhy) { v.Blocked = lockWhy; return v; }
            if (scope.Count == 0) { v.Blocked = DiscardCopy.Changed; return v; }
            v.Available = DiscardLegal(s, scope).Sum(i => i.Count);
            v.MaxUnits = v.Available;
            if (v.Available == 0)
                v.Blocked = scope.Select(i => DiscardItemBlock(s, i)).Where(x => x != null).OrderBy(BlockRank).FirstOrDefault() ?? DiscardCopy.Changed;
            return v;
        }

        private static int BlockRank(string? why) => why switch
        {
            DiscardCopy.Processing => 0,
            DiscardCopy.Busy => 1,
            DiscardCopy.Worn => 2,
            DiscardCopy.Deployed => 3,
            _ => 4,
        };

        /// <summary>水：容器、水种、每份、总毫升、可丢毫升与原因。</summary>
        private (ItemInstance? container, string? liquid, int unit, int total, int free, string? why) WaterState(GameState s, DiscardTarget t)
        {
            var c = t.InstanceId is int id ? FindItem(s, id) : null;
            if (c == null) return (null, null, 0, 0, 0, DiscardCopy.Changed);
            if (t.Kind == DiscardKind.CanteenWater)
            {
                var cfg = CanteenOf(c);
                if (cfg == null) return (null, null, 0, 0, 0, DiscardCopy.Changed);
                var liquid = c.LiquidMl > 0 ? c.LiquidId : null;
                var unit = liquid != null ? _items[liquid].UnitVolumeMl ?? cfg.TransferMl : cfg.TransferMl;
                var total = liquid != null ? c.LiquidMl : 0;
                string? why = DiscardProcessing(s, c) ? DiscardCopy.Processing
                    : DiscardOccupied(s, c) ? DiscardCopy.Busy
                    : c.LoanSlot != null || !DiscardOwnerAllowed(s, c) || c.IsWorn ? DiscardCopy.NoPermission
                    : total <= 0 ? DiscardCopy.NoWater : null;
                return (c, liquid, unit, total, why == null ? total : 0, why);
            }
            if (!IsPot(c)) return (null, null, 0, 0, 0, DiscardCopy.Changed);
            var pc = PotConfig(c);
            var waters = PotLiquids(s, c.InstanceId).Where(w => w.ItemId == t.ItemId).ToList();
            var all = waters.Sum(w => w.Count) * pc.UnitMl;
            // 锅水只能作为加工输入被绑定：净化中的锅水（加工记录以锅为目标）整锅水都在加工；水煮食材预留的毫升不可丢
            var purifying = AllProcesses(s).Any(p => p.Status != ProcessStatus.Done && p.TargetInstanceId == c.InstanceId);
            var freeUnits = waters.Count == 0 ? 0 : Math.Min(waters.Sum(w => w.Count), PotFreeWaterUnits(s, c));
            string? whyPot = purifying ? DiscardCopy.Processing
                : IsLocked(s, c) || c.ReservedBy != null || waters.Any(w => w.ReservedBy != null || IsLocked(s, w)) ? DiscardCopy.Busy
                : c.LoanSlot != null || !DiscardOwnerAllowed(s, c) ? DiscardCopy.NoPermission
                : all <= 0 ? DiscardCopy.NoWater
                : freeUnits <= 0 ? DiscardCopy.Processing : null;
            return (c, waters.Count > 0 ? t.ItemId : null, pc.UnitMl, all, whyPot == null ? freeUnits * pc.UnitMl : 0, whyPot);
        }

        private DiscardView WaterDiscardView(GameState s, DiscardTarget t)
        {
            var (_, liquid, unit, total, free, why) = WaterState(s, t);
            var v = new DiscardView { IsWater = true, UnitMl = unit, Total = total, WaterName = liquid != null ? ItemName(liquid) : "" };
            if (DiscardInputLock(s) is string lockWhy) { v.Blocked = lockWhy; return v; }
            v.Available = free;
            v.MaxUnits = unit > 0 ? free / unit : 0;
            v.Blocked = free > 0 ? null : why ?? DiscardCopy.NoWater;
            return v;
        }

        // ───────────── 确认清单 ─────────────

        /// <summary>
        /// 确认清单与成功日志里的一项（暂用写法，Q864 待答）：「{名}×{数}」，有保质截止加「（保质到{日期时刻}）」，装备加品质「（{品质}）」；同写法的多堆合并份数。
        /// </summary>
        public string DiscardItemLabel(GameState s, ItemInstance i)
        {
            var name = InstanceName(i);
            if (_items.TryGetValue(i.ItemId, out var cfg) && cfg.IsEquipment) name += $"（{QualityName(i)}）";
            if (i.ExpiresAt is int x && !IsSpoiledMatter(i)) name += $"（保质到{Clock.DayTime(x)}）";
            return name;
        }

        private string DiscardListText(GameState s, IEnumerable<(ItemInstance inst, int n)> plan) =>
            string.Join("、", plan.GroupBy(x => DiscardItemLabel(s, x.inst)).Select(g => $"{g.Key}×{g.Sum(x => x.n)}"));

        /// <summary>容器内容清单：锅水「{水种}{毫升}毫升」、锅内其他各份「{名}×{数}」；水壶「{水种}{毫升}毫升」；空容器为 null（不造包含行）。</summary>
        private string? DiscardContentsText(GameState s, ItemInstance c)
        {
            var parts = new List<string>();
            if (CanteenOf(c) != null)
            {
                if (c.LiquidId != null && c.LiquidMl > 0) parts.Add($"{ItemName(c.LiquidId)}{c.LiquidMl}毫升");
            }
            else if (IsPot(c))
            {
                var unit = PotConfig(c).UnitMl;
                foreach (var w in PotLiquids(s, c.InstanceId).GroupBy(w => w.ItemId)) parts.Add($"{ItemName(w.Key)}{w.Sum(x => x.Count) * unit}毫升");
                foreach (var g in PotContents(s, c.InstanceId).Where(x => !IsLiquid(x)).GroupBy(x => InstanceName(x))) parts.Add($"{g.Key}×{g.Sum(x => x.Count)}");
            }
            return parts.Count == 0 ? null : string.Join("、", parts);
        }

        private static string StampOf(ItemInstance i, int n) =>
            $"{i.InstanceId}:{i.ItemId}:{i.Owner}:{i.Count}:{n}:{i.ExpiresAt}:{i.ShelfPausedMinutes}:{i.ReservedBy}:{i.Quality}:{i.Durability}:{i.LiquidId}:{i.LiquidMl}:{i.LoanSlot}";

        /// <summary>
        /// 本次要删的真实清单（只读）。count 为数量路径的 N（全部时忽略）；reason 给出不能确认的原因（数量非法、无可丢弃、输入锁）。
        /// </summary>
        public DiscardPlanView? DiscardPreview(GameState s, DiscardTarget t, int count, bool all, out string? reason) =>
            BuildDiscardPlan(s, t, count, all, out reason, out _, out _);

        private DiscardPlanView? BuildDiscardPlan(GameState s, DiscardTarget t, int count, bool all, out string? reason,
                                                  out List<(ItemInstance inst, int n)> plan, out ItemInstance? waterContainer)
        {
            plan = new List<(ItemInstance, int)>();
            waterContainer = null;
            var v = DiscardViewOf(s, t);
            if (v.Blocked != null) { reason = v.Blocked; return null; }
            if (!all && (count < 1 || count > v.MaxUnits)) { reason = DiscardCopy.InvalidQuantity(v.MaxUnits); return null; }
            reason = null;
            if (t.IsWater)
            {
                var (c, liquid, unit, total, free, _) = WaterState(s, t);
                waterContainer = c;
                var ml = all ? free : count * unit;
                var name = ItemName(liquid!);
                return new DiscardPlanView
                {
                    All = all, Count = all ? 0 : count, WaterMl = ml,
                    Stamp = $"w|{t.Key}|{c!.InstanceId}:{c.Owner}:{c.ReservedBy}|{liquid}|{total}|{free}|{ml}",
                    Lines = new List<string> { DiscardCopy.WaterConfirm(name, ml), DiscardCopy.Irreversible },
                    ResultText = DiscardCopy.WaterResult(name, ml),
                };
            }
            var legal = DiscardLegal(s, DiscardScope(s, t));
            plan = PlanGroup(legal, all ? legal.Sum(i => i.Count) : count);
            var items = DiscardListText(s, plan);
            var contents = plan.Where(x => x.n >= x.inst.Count).Select(x => DiscardContentsText(s, x.inst)).Where(x => x != null).ToList();
            var lines = new List<string> { DiscardCopy.ConfirmItems(items) };
            // 一次可丢多件容器时每件一行「包含：」（Q865 待答；本批右栏容器都是单件，实际只出一行）
            lines.AddRange(contents.Select(x => DiscardCopy.Contents(x!)));
            lines.Add(DiscardCopy.Irreversible);
            var stamp = "i|" + t.Key + "|" + string.Join(";", plan.Select(x => StampOf(x.inst, x.n)
                        + (x.n >= x.inst.Count && (IsPot(x.inst) || CanteenOf(x.inst) != null) ? "{" + string.Join(",", PotContents(s, x.inst.InstanceId).Select(c => StampOf(c, c.Count))) + "}" : "")));
            return new DiscardPlanView
            {
                All = all, Count = plan.Sum(x => x.n), Stamp = stamp, Lines = lines,
                ResultText = DiscardCopy.Result(items) + (contents.Count > 0 ? DiscardCopy.ResultContents(string.Join("、", contents)) : ""),
            };
        }

        // ───────────── 执行 ─────────────

        /// <summary>
        /// 原子执行一次永久丢弃（0 世界分钟、0 AP）：先做到期转换，再按同一范围重算清单，与确认快照不同整次拒绝「物品已变，请重选。」；
        /// 通过才删除（部分堆按原拆分账扣来源、整件容器连本次确认过的内容一起删），写一条成功日志。同请求号重发直接成功、不再删除或写日志。
        /// </summary>
        public ActionResult Discard(GameState s, DiscardRequest r)
        {
            if (SeenRequest(s, r.RequestId)) return ActionResult.Repeated();
            if (DiscardInputLock(s) is string lockWhy) return ActionResult.Fail(lockWhy);
            ConvertDueSpoilage(s);
            var view = BuildDiscardPlan(s, r.Target, r.Count, r.All, out _, out var plan, out var container);
            if (view == null) return ActionResult.Fail(DiscardCopy.Changed);   // 输入锁已先判；确认后数量、占用、物主、腐败变种或水量变化都按「物品已变」整次拒绝（§Q858-03／Q859-03）
            if (view.Stamp != r.Stamp) return ActionResult.Fail(DiscardCopy.Changed);

            if (r.Target.IsWater) RemoveWater(s, r.Target, container!, view.WaterMl);
            else
                foreach (var (inst, n) in plan)
                {
                    if (n >= inst.Count)
                    {
                        foreach (var child in PotContents(s, inst.InstanceId)) s.Items.Remove(child);
                        s.Items.Remove(inst);
                    }
                    else
                    {
                        TakeOrigins(inst, n);   // 部分堆：按原拆分账先扣已知来源，余下账不超过余下份数
                        inst.Count -= n;
                    }
                }
            RememberRequest(s, r.RequestId);
            Emit(s, GameEventKind.Info, view.ResultText);
            SyncFacilities(s);
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>只扣确认的毫升：锅按整份减水卡份数（锅水总按整份存放），水壶减真实毫升；清空沿原空容器状态，容器与其他内容不动。</summary>
        private void RemoveWater(GameState s, DiscardTarget t, ItemInstance c, int ml)
        {
            if (t.Kind == DiscardKind.CanteenWater)
            {
                c.LiquidMl -= ml;
                if (c.LiquidMl <= 0) { c.LiquidMl = 0; c.LiquidId = null; }
                return;
            }
            var units = ml / PotConfig(c).UnitMl;
            foreach (var w in PotLiquids(s, c.InstanceId).Where(w => w.ItemId == t.ItemId).OrderBy(w => w.InstanceId).ToList())
            {
                if (units <= 0) break;
                var take = Math.Min(units, w.Count);
                w.Count -= take;
                if (w.Count <= 0) s.Items.Remove(w);
                units -= take;
            }
        }
    }
}

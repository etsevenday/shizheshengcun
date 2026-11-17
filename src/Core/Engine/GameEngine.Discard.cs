using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 永久丢弃的正式文字（逐字稿文首「2026-11-15 B50a Q763—Q768与永久丢弃」表与「2026-11-17 B53主要原批Q857—Q861」§Q859 水句；
    /// 合同 assets/20261117/b53-discard-main-q857-q861.json water.text）。B53b：子区标题与总数行按「2026-11-17 B53次要原批Q862—Q868」§Q862／Q863
    /// （合同 b53-discard-copy-q862-q868.json ui），取代原「可丢弃：…」单行；{物品清单}／{内容清单}每项写法见 <see cref="GameEngine.DiscardItemLabel"/>（§Q864／Q865）。
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

        /// <summary>§Q862 A：物品子区标题「丢弃 · {物名}」（水子区仍是 <see cref="DiscardWater"/>，不叠两个标题）。</summary>
        public static string Title(string name) => $"丢弃 · {name}";
        /// <summary>§Q863 A：子区一直显示「总数{总数}，可丢弃{可丢弃数}」，相等也显示。</summary>
        public static string Totals(int total, int available) => $"总数{total}，可丢弃{available}";
        /// <summary>§Q863 A：水按真实毫升「总数{总毫升}毫升，可丢弃{可丢弃毫升}毫升」。</summary>
        public static string WaterTotals(int totalMl, int availableMl) => $"总数{totalMl}毫升，可丢弃{availableMl}毫升";
        public static string InvalidQuantity(int max) => $"请输入1至{max}的整数。";
        public static string ConfirmItems(string list) => $"永久丢弃：{list}。";
        public static string Contents(string list) => $"包含：{list}。";
        public static string Result(string list) => $"已永久丢弃：{list}。";
        public static string ResultContents(string list) => $"同时丢弃：{list}。";
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
        /// <summary>无可丢弃时的第一条真实原因（有可丢弃时为 null）；等于 <see cref="Reasons"/> 的第一条。</summary>
        public string? Blocked { get; set; }
        /// <summary>
        /// 无可丢弃时命中的全部真实原因（§Q866 B）：输入锁→加工→预留／行动占用→穿戴→部署→无处置权，相同句去重；有可丢弃时为空（合法子集照常可删，不列被过滤份的原因）。
        /// </summary>
        public List<string> Reasons { get; set; } = new List<string>();
        /// <summary>物品子区标题里的 {物名}（当前真实显示名）；水子区不用。</summary>
        public string Name { get; set; } = "";
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
        /// 可处置的来源：主角随身、当前区地面、当前区火堆加工位、根在这些地方的锅里（B53a）；
        /// B53b（S06／S14 文首 2026-11-17 §Q857 B「原可管理同伴随身、柜内、遗物／认领箱、回收箱、封存栏」，只对真实有权同地点区域物、不扩权）：
        /// 同行（在场、编入同行的）同伴随身；人在该屋屋内时这间安全屋的柜子；当前地点（屋内遗物只在屋内）的遗物、认领箱、远征回收箱、旧实例遗物认领箱；
        /// 人在本次远征地牢里时该实例的封存战利品——与各清单原有取出资格同一口径（ActiveCompanions、CabinetBlocked、TakeFromRemainsBlocked、UnsealBlocked）。
        /// </summary>
        public bool DiscardOwnerAllowed(GameState s, ItemInstance i)
        {
            if (i.InContainer) return ContainerOf(s, i) is ItemInstance pot && DiscardOwnerAllowed(s, pot);
            if (ItemInstance.TryFacilityId(i.Owner, out var fid)) return FacilityLocation(fid) == s.Ground;
            if (i.Owner == ItemInstance.Carried || i.Owner == s.Ground) return true;
            if (ActiveCompanions(s).Any(r => ItemInstance.CarriedOwner(r.ActorId) == i.Owner)) return true;
            // 穿在主角或同行同伴身上的：有处置权，真实挡住的是「先卸下再丢弃。」，不另报无权限（§Q866 B 只列真实原因）
            if (ItemInstance.TryEquip(i.Owner, out var wearer, out _)) return wearer == GameState.PlayerId || ActiveCompanions(s).Any(r => r.ActorId == wearer);
            if (i.InSafehouseStorage) return s.CurrentZone == SafehouseZone && CabinetOwnerOf(s, s.CurrentLocation) == i.Owner;
            if (i.InRemains)
                return s.Remains.TryGetValue(i.Owner.Substring(ItemInstance.RemainsPrefix.Length), out var rem)
                       && rem.LocationId == s.CurrentLocation && (rem.Zone == null || rem.Zone == s.CurrentZone);
            if (i.Owner.StartsWith(SealedPrefix))
                return DungeonAt(s.CurrentLocation) is string d && ExpeditionEnabled(d) && i.Owner == SealedOwner(InstanceId(s, d));
            return false;
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

        /// <summary>这一件为什么不能永久丢弃的第一条原因（可以为 null）；全部原因见 <see cref="DiscardItemBlocks"/>。</summary>
        public string? DiscardItemBlock(GameState s, ItemInstance i) => DiscardItemBlocks(s, i).FirstOrDefault();

        /// <summary>
        /// 这一件命中的全部拒绝原因（§Q866 B：逐条显示，按 加工→预留／行动占用→穿戴→部署→无处置权 的既定分类顺序；输入锁另在整体先判）。
        /// 首战教学样本不因教学身份拒绝（§Q860 选 A）；借用物等无处置权用「当前无法丢弃该物品。」。
        /// </summary>
        public List<string> DiscardItemBlocks(GameState s, ItemInstance i)
        {
            var list = new List<string>();
            if (DiscardProcessing(s, i)) list.Add(DiscardCopy.Processing);
            if (DiscardOccupied(s, i)) list.Add(DiscardCopy.Busy);
            if (i.IsWorn) list.Add(DiscardCopy.Worn);
            if (IsDeployed(s, i)) list.Add(DiscardCopy.Deployed);
            if (i.LoanSlot != null || !DiscardOwnerAllowed(s, i)) list.Add(DiscardCopy.NoPermission);
            return list;
        }

        /// <summary>
        /// B53b（§Q857 B「只作用于点中的实际行」）：无右栏清单的一行就是一件真实实例（一堆或一件），丢弃目标按该件；
        /// 同伴锅里的水行按 §Q859 的「丢弃水」（该锅＋该水种，按毫升）。
        /// </summary>
        public DiscardTarget DiscardRowTarget(GameState s, int instanceId)
        {
            var i = FindItem(s, instanceId);
            if (i != null && i.InContainer && IsLiquid(i) && ContainerOf(s, i) is ItemInstance pot && IsPot(pot)) return DiscardTarget.PotWater(pot.InstanceId, i.ItemId);
            return DiscardTarget.Instance(instanceId);
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

        /// <summary>
        /// 丢弃子区：标题物名、本次范围总数与可丢弃数（§Q863：两数同一范围，水为毫升）、每份毫升、数量上限与无可丢弃时的全部原因（§Q866 B）。
        /// </summary>
        public DiscardView DiscardViewOf(GameState s, DiscardTarget t)
        {
            if (t.IsWater) return WaterDiscardView(s, t);
            var v = new DiscardView();
            var scope = DiscardScope(s, t);
            v.Total = scope.Sum(i => i.Count);
            v.Name = scope.Count > 0 ? InstanceName(scope[0]) : t.Kind == DiscardKind.Group && _items.ContainsKey(t.ItemId) ? ItemName(t.ItemId) : "";
            var lockWhy = DiscardInputLock(s);
            var legal = DiscardLegal(s, scope);
            if (lockWhy != null) v.Reasons.Add(lockWhy);
            else v.Available = legal.Sum(i => i.Count);
            v.MaxUnits = v.Available;
            // 只在本次范围里没有合法份时列原因：输入锁在前，其后本范围各份命中的原因按分类顺序去重；合法子集可删时不列被过滤份的原因（§Q866 B「合法7预留2」）
            if (scope.Count > 0 && legal.Count == 0)
                v.Reasons.AddRange(scope.SelectMany(i => DiscardItemBlocks(s, i)).Distinct().OrderBy(BlockRank));
            if (v.Available == 0 && v.Reasons.Count == 0) v.Reasons.Add(DiscardCopy.Changed);
            v.Blocked = v.Reasons.FirstOrDefault();
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

        /// <summary>
        /// 水：容器、水种、每份、总毫升、可丢毫升与全部原因（§Q866 B：加工→占用→无处置权逐条；都没有时才是空容器「无可丢弃的水。」）。
        /// </summary>
        private (ItemInstance? container, string? liquid, int unit, int total, int free, List<string> whys) WaterState(GameState s, DiscardTarget t)
        {
            var whys = new List<string>();
            var c = t.InstanceId is int id ? FindItem(s, id) : null;
            if (c == null) { whys.Add(DiscardCopy.Changed); return (null, null, 0, 0, 0, whys); }
            if (t.Kind == DiscardKind.CanteenWater)
            {
                var cfg = CanteenOf(c);
                if (cfg == null) { whys.Add(DiscardCopy.Changed); return (null, null, 0, 0, 0, whys); }
                var liquid = c.LiquidMl > 0 ? c.LiquidId : null;
                var unit = liquid != null ? _items[liquid].UnitVolumeMl ?? cfg.TransferMl : cfg.TransferMl;
                var total = liquid != null ? c.LiquidMl : 0;
                if (DiscardProcessing(s, c)) whys.Add(DiscardCopy.Processing);
                if (DiscardOccupied(s, c)) whys.Add(DiscardCopy.Busy);
                if (c.LoanSlot != null || !DiscardOwnerAllowed(s, c) || c.IsWorn) whys.Add(DiscardCopy.NoPermission);
                if (whys.Count == 0 && total <= 0) whys.Add(DiscardCopy.NoWater);
                return (c, liquid, unit, total, whys.Count == 0 ? total : 0, whys);
            }
            if (!IsPot(c)) { whys.Add(DiscardCopy.Changed); return (null, null, 0, 0, 0, whys); }
            var pc = PotConfig(c);
            var waters = PotLiquids(s, c.InstanceId).Where(w => w.ItemId == t.ItemId).ToList();
            var all = waters.Sum(w => w.Count) * pc.UnitMl;
            // 锅水只能作为加工输入被绑定：净化中的锅水（加工记录以锅为目标）整锅水都在加工；水煮食材预留的毫升不可丢
            var purifying = AllProcesses(s).Any(p => p.Status != ProcessStatus.Done && p.TargetInstanceId == c.InstanceId);
            var freeUnits = waters.Count == 0 ? 0 : Math.Min(waters.Sum(w => w.Count), PotFreeWaterUnits(s, c));
            if (purifying || (all > 0 && freeUnits <= 0)) whys.Add(DiscardCopy.Processing);
            if (IsLocked(s, c) || c.ReservedBy != null || waters.Any(w => w.ReservedBy != null || IsLocked(s, w))) whys.Add(DiscardCopy.Busy);
            if (c.LoanSlot != null || !DiscardOwnerAllowed(s, c)) whys.Add(DiscardCopy.NoPermission);
            if (whys.Count == 0 && all <= 0) whys.Add(DiscardCopy.NoWater);
            return (c, waters.Count > 0 ? t.ItemId : null, pc.UnitMl, all, whys.Count == 0 ? freeUnits * pc.UnitMl : 0, whys);
        }

        private DiscardView WaterDiscardView(GameState s, DiscardTarget t)
        {
            var (_, liquid, unit, total, free, whys) = WaterState(s, t);
            var v = new DiscardView { IsWater = true, UnitMl = unit, Total = total, WaterName = liquid != null ? ItemName(liquid) : "" };
            if (DiscardInputLock(s) is string lockWhy) v.Reasons.Add(lockWhy);
            else v.Available = free;
            v.MaxUnits = unit > 0 ? v.Available / unit : 0;
            if (free <= 0) v.Reasons.AddRange(whys);   // 本容器本身没有可丢的水时才列水的原因（whys 为空即有水可丢）
            v.Reasons = v.Reasons.Distinct().ToList();
            v.Blocked = v.Reasons.FirstOrDefault();
            return v;
        }

        // ───────────── 确认清单 ─────────────

        /// <summary>
        /// 确认清单与成功日志里的一项（§Q864 A，合同 b53-discard-copy-q862-q868.json items）：普通可分批物每批「{物名}×{数量}（保质至{截止}）」，无期限省括注；
        /// 腐败物「腐败物×{数量} · {单重}公斤/份」（Q852 短单位）；工具／装备／锅／水壶等独立件逐件「{物名}×1」，接非白品质与现有状态摘要（暂用写法，见开发记录 B53b 暂用文案）。
        /// 返回该项的写法（{n} 处填本次真实数量）与是否逐件：同写法的可分批物合并真实数量，逐件不合并。
        /// </summary>
        private (string pattern, bool piece) DiscardEntryKey(ItemInstance i)
        {
            var name = InstanceName(i);
            if (IsSpoiledMatter(i)) return ($"{name}×{{n}} · {Fmt.KgValue(UnitGrams(i))}公斤/份", false);
            if (_items.TryGetValue(i.ItemId, out var cfg) && !cfg.IsStackable) return (name + "×1" + DiscardPieceNote(i), true);
            return (name + "×{n}" + (i.ExpiresAt is int x ? $"（保质至{Clock.DayTime(x)}）" : ""), false);
        }

        /// <summary>独立件的状态摘要（Q762 A：非白品质公开名＋原一行适用的耐久／弹仓／保质；白色省略）；没有可写的为空串。</summary>
        private string DiscardPieceNote(ItemInstance i)
        {
            var parts = new List<string>();
            if (_items.TryGetValue(i.ItemId, out var cfg) && cfg.IsEquipment && (i.Quality ?? "white") != "white") parts.Add(QualityName(i));
            if (i.Durability.HasValue && i.MaxDurability.HasValue) parts.Add(i.Durability.Value <= 0 ? "已损坏" : $"耐久{DurabilityText(i)}");
            if (i.LoadedAmmoId != null) parts.Add($"弹{i.LoadedAmmo}");
            if (i.ExpiresAt is int x) parts.Add($"保质至{Clock.DayTime(x)}");
            return parts.Count == 0 ? "" : $"（{string.Join(" · ", parts)}）";
        }

        /// <summary>一项的显示文字（数量 n 为本次真实份数）。</summary>
        public string DiscardItemLabel(GameState s, ItemInstance i, int n) => DiscardEntryKey(i).pattern.Replace("{n}", n.ToString());

        /// <summary>本次清单：按本次实际选取的稳定顺序，同批（同键）合并真实数量、独立件逐件，项间「、」。</summary>
        private string DiscardListText(GameState s, IEnumerable<(ItemInstance inst, int n)> plan)
        {
            var entries = new List<(string pattern, bool piece, int n)>();
            foreach (var (inst, n) in plan)
            {
                var (pattern, piece) = DiscardEntryKey(inst);
                var at = piece ? -1 : entries.FindIndex(e => !e.piece && e.pattern == pattern);
                if (at >= 0) entries[at] = (pattern, false, entries[at].n + n);
                else entries.Add((pattern, piece, n));
            }
            return string.Join("、", entries.Select(e => e.pattern.Replace("{n}", e.n.ToString())));
        }

        /// <summary>
        /// 本次整件删除的全部容器的连带内容合计成一条（§Q865 B，合同 contents）：同种水实际毫升相加、净水脏水分开（水在前，按首次出现）；
        /// 其他内容按 §Q864 写法、同摘要合并真实数量；所选容器全空为 null（省「包含／同时丢弃」）。
        /// </summary>
        private string? DiscardContentsText(GameState s, IEnumerable<ItemInstance> containers)
        {
            var waters = new List<(string id, int ml)>();
            var goods = new List<(ItemInstance inst, int n)>();
            void AddWater(string id, int ml)
            {
                var at = waters.FindIndex(w => w.id == id);
                if (at >= 0) waters[at] = (id, waters[at].ml + ml);
                else waters.Add((id, ml));
            }
            foreach (var c in containers)
            {
                if (CanteenOf(c) != null)
                {
                    if (c.LiquidId != null && c.LiquidMl > 0) AddWater(c.LiquidId, c.LiquidMl);
                }
                else if (IsPot(c))
                {
                    var unit = PotConfig(c).UnitMl;
                    foreach (var w in PotLiquids(s, c.InstanceId)) AddWater(w.ItemId, w.Count * unit);
                    goods.AddRange(PotContents(s, c.InstanceId).Where(x => !IsLiquid(x)).Select(x => (x, x.Count)));
                }
            }
            var parts = waters.Select(w => $"{ItemName(w.id)}{w.ml}毫升").ToList();
            if (goods.Count > 0) parts.Add(DiscardListText(s, goods));
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
            // §Q865 B：本次整件删除的全部容器内容合计一条「包含：」，全空省行
            var contents = DiscardContentsText(s, plan.Where(x => x.n >= x.inst.Count).Select(x => x.inst));
            var lines = new List<string> { DiscardCopy.ConfirmItems(items) };
            if (contents != null) lines.Add(DiscardCopy.Contents(contents));
            lines.Add(DiscardCopy.Irreversible);
            var stamp = "i|" + t.Key + "|" + string.Join(";", plan.Select(x => StampOf(x.inst, x.n)
                        + (x.n >= x.inst.Count && (IsPot(x.inst) || CanteenOf(x.inst) != null) ? "{" + string.Join(",", PotContents(s, x.inst.InstanceId).Select(c => StampOf(c, c.Count))) + "}" : "")));
            return new DiscardPlanView
            {
                All = all, Count = plan.Sum(x => x.n), Stamp = stamp, Lines = lines,
                ResultText = DiscardCopy.Result(items) + (contents != null ? DiscardCopy.ResultContents(contents) : ""),
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

            // 遗物／认领箱／回收箱删空后与原「取出」一样从地点上移除（TakeFromRemains），不留空箱
            var boxes = plan.Select(x => x.inst.Owner).Where(o => o.StartsWith(ItemInstance.RemainsPrefix)).Distinct().ToList();
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
            foreach (var key in boxes.Select(o => o.Substring(ItemInstance.RemainsPrefix.Length)))
                if (RemainsItems(s, key).Count == 0) s.Remains.Remove(key);
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

        // ───────────── 开发：试玩起点 ─────────────

        /// <summary>
        /// 开发：B53a 永久丢弃试用组（只造起点）：当前地面野果两批（3份2小时后到期、5份按原保质）、金属锅（净水1000毫升＋生肉1）；
        /// 随身分量水壶（净水750毫升）、野果3份（1分钟后到期，等一下即在原位转成腐败物）。
        /// </summary>
        public string DebugDiscardKit(GameState s)
        {
            var berries = CreateItems(s, "berry", 8, s.Ground).Single();
            var soon = SplitOff(s, berries, 3);   // 同组拆成两批（不经合堆）
            soon.ExpiresAt = s.GameMinute + 120;
            var pot = CreateItems(s, "pot", 1, s.Ground).Single();
            CreateItems(s, "clean_water", 2, ItemInstance.ContainerOwner(pot.InstanceId));
            CreateItems(s, "raw_meat", 1, ItemInstance.ContainerOwner(pot.InstanceId));
            var canteen = CreateItems(s, "ration_canteen", 1, ItemInstance.Carried).Single();
            canteen.LiquidId = "clean_water";
            canteen.LiquidMl = 750;
            var rot = CreateItems(s, "berry", 3, ItemInstance.Carried).Single();
            rot.ExpiresAt = s.GameMinute + 1;
            var text = $"开发：野果3（2小时后到期）＋野果5、金属锅（净水1000毫升、生肉1）放在{ZoneName(s, s.Ground)}地面；随身分量水壶（净水750毫升）、野果3（1分钟后到期，转腐败物）";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }

        /// <summary>
        /// 开发：B53b 清单行末丢弃试用组（只造起点）：第一名固定队友编入同行，随身放野果3、金属锅（净水2份＋野果1）、分量水壶（净水750毫升）；
        /// 当前地点（当前区）放四个箱：遗物（野果2、木材3）、认领箱（罐头1、借用木矛1）、远征回收箱（生肉2）、旧实例遗物认领箱（金属锅连脏水1份）；
        /// 人在已建安全屋屋内时柜子里放野果2、木材2；人在本次远征地牢里时封存账放罐头1。
        /// </summary>
        public string DebugDiscardListKit(GameState s)
        {
            s.Progress.Flags.Add("hub_registered");
            s.Progress.Flags.Add("church_route_record");
            s.Progress.Flags.Add("academy_safety_record");
            var parts = new List<string>();
            var mate = Config.Party.NamedCompanions.Select(n => n.ActorId).FirstOrDefault(id => RosterOf(s, id) == null || s.Party.Contains(id));
            if (mate != null && RosterOf(s, mate) == null) DebugRecruit(s, mate);
            if (mate != null && s.Party.Contains(mate))
            {
                var bag = ItemInstance.CarriedOwner(mate);
                CreateItems(s, "berry", 3, bag);
                var pot = CreateItems(s, "pot", 1, bag).Single();
                CreateItems(s, "clean_water", 2, ItemInstance.ContainerOwner(pot.InstanceId));
                CreateItems(s, "berry", 1, ItemInstance.ContainerOwner(pot.InstanceId));
                var canteen = CreateItems(s, "ration_canteen", 1, bag).Single();
                canteen.LiquidId = "clean_water";
                canteen.LiquidMl = 750;
                parts.Add($"{ActorDisplayName(s, mate)}随身：野果3、金属锅（净水{2 * PotConfig(pot).UnitMl}毫升、野果1）、分量水壶（净水750毫升）");
            }
            string Box(string key, string name, string? kind)
            {
                if (!s.Remains.ContainsKey(key))
                    s.Remains[key] = new RemainsState { Name = name, LocationId = s.CurrentLocation, Minute = s.GameMinute, Cause = "开发试用", Kind = kind, Zone = s.CurrentZone == SafehouseZone ? SafehouseZone : null };
                return ItemInstance.RemainsOwner(key);
            }
            var dead = Box("dev_b53b_remains", "试用者", null);
            CreateItems(s, "berry", 2, dead);
            CreateItems(s, "wood", 3, dead);
            var claim = Box("dev_b53b_claim", "试用者", RemainsState.ClaimKind);
            CreateItems(s, "canned_food", 1, claim);
            CreateItems(s, "wooden_spear", 1, claim).Single().LoanSlot = "loan_1";
            CreateItems(s, "raw_meat", 2, Box("dev_b53b_recovery", "试用远征回收箱", RemainsState.RecoveryKind));
            var relicPot = CreateItems(s, "pot", 1, Box("dev_b53b_relic", "试用旧实例遗物认领箱", RemainsState.RelicKind)).Single();
            CreateItems(s, "dirty_water", 1, ItemInstance.ContainerOwner(relicPot.InstanceId));
            parts.Add($"{LocName(s.CurrentLocation)}遗物（野果2、木材3）、认领箱（罐头1、借用木矛1）、试用远征回收箱（生肉2）、试用旧实例遗物认领箱（金属锅连脏水{PotConfig(relicPot).UnitMl}毫升）");
            if (s.CurrentZone == SafehouseZone && CabinetOwnerOf(s, s.CurrentLocation) is string cab)
            {
                CreateItems(s, "berry", 2, cab);
                CreateItems(s, "wood", 2, cab);
                parts.Add("安全屋柜子：野果2、木材2");
            }
            if (s.Expedition is ExpeditionState ex && DungeonAt(s.CurrentLocation) == ex.DungeonId)
            {
                var sealedFood = CreateItems(s, "canned_food", 1, SealedOwner(ex.InstanceId)).Single();
                sealedFood.RewardOrigin = ex.InstanceId;
                parts.Add("封存战利品：罐头1");
            }
            var text = "开发：" + string.Join("；", parts);
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }
    }
}

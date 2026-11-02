using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 地牢实例与远征（开发计划 v0.9 批次 B21，需求 R65/R66；R03「2026-10-30 B4 地牢远征、战术品与生命周期」D2—D5、D10，
    /// S01「B3/B4 世界时钟与实例原子事务」、S12「B4 撤出事件包」、S13「B4 地牢整备与撤出恢复边界」、R04「B4 远征感染与死亡优先」）。
    /// <list type="bullet">
    /// <item>ID（D2）：dungeonId = 配置 id；instanceId = 局种子8位十六进制:dungeonId:g代号（开发假设 B21-1）；expeditionId = instanceId:e序号。</item>
    /// <item>状态（D2）：到达任一房间只建实例记录（B11），第一次从 01 确认走向危险房间才提交远征、UNOPENED→ACTIVE；终止确认→EXTRACTING，
    /// 抵达父地点回 ACTIVE；本代核心记录已领取/核对按 CLEARED 显示；主角终局叠加 CAMPAIGN_ENDED。RESET_READY 由 B22 的 72 小时重置给出（本批恒假）。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        private DungeonExpeditionConfig DX => Config.DungeonExpedition;

        /// <summary>终止惩罚包的去重键后缀（R03 D5.1“冷却式去重键 expeditionId+abortPenalty”）。</summary>
        public const string AbortPenaltyKeySuffix = ":abortPenalty";

        /// <summary>未确认就从 01 走向危险房间时的拒绝原因（网页据 <see cref="NeedsExpeditionConfirm"/> 弹进入预告）。</summary>
        public const string ExpeditionConfirmReason = "进入危险区前要先确认开始远征";

        /// <summary>地牢配置；不存在为 null。</summary>
        public DungeonConfig? DungeonCfg(string dungeonId) => Config.Map.Dungeons.FirstOrDefault(d => d.Id == dungeonId);

        /// <summary>该地点所属地牢（不是地牢房间为 null）。</summary>
        public string? DungeonAt(string locationId) => _locs.TryGetValue(locationId, out var l) ? l.DungeonId : null;

        /// <summary>地牢入口缓冲房（R03 D3 的 01）；没有远征配置为 null。</summary>
        public string? BufferNodeOf(string dungeonId) => DX.BufferNodes.TryGetValue(dungeonId, out var b) ? b : null;

        /// <summary>该地牢是否启用远征规则（有入口缓冲配置）。</summary>
        public bool ExpeditionEnabled(string? dungeonId) => dungeonId != null && BufferNodeOf(dungeonId) != null && DungeonCfg(dungeonId) != null;

        /// <summary>危险房间：启用远征的地牢里入口缓冲以外的房间。</summary>
        public bool IsDangerRoom(string locationId) => DungeonAt(locationId) is string d && ExpeditionEnabled(d) && BufferNodeOf(d) != locationId;

        /// <summary>instanceId（开发假设 B21-1）：局种子8位十六进制:dungeonId:g代号。没有实例记录按第 1 代。</summary>
        public string InstanceId(GameState s, string dungeonId)
        {
            var gen = s.DungeonInstances.TryGetValue(dungeonId, out var inst) ? inst.Generation : 1;
            return $"{s.Seed:x8}:{dungeonId}:g{gen}";
        }

        /// <summary>本代是否已通关（核心记录已领取/核对，B12 写的实例事实 recordClaimedAndClear）。</summary>
        public bool DungeonCleared(GameState s, string dungeonId) => InstanceFact(s, dungeonId, RecordClaimedFact);

        /// <summary>
        /// 实例状态（R03 D2）：主角终局 → CAMPAIGN_ENDED；没有实例记录 → UNOPENED；撤出中 → EXTRACTING；本代已通关 → CLEARED
        /// （可重置时 RESET_READY，B22 起）；其余按存的状态（UNOPENED / ACTIVE）。
        /// </summary>
        public string DungeonStatus(GameState s, string dungeonId)
        {
            if (s.Progress.Dead) return DungeonStatuses.CampaignEnded;
            if (!s.DungeonInstances.TryGetValue(dungeonId, out var inst)) return DungeonStatuses.Unopened;
            if (inst.Status == DungeonStatuses.CampaignEnded) return DungeonStatuses.CampaignEnded;
            if (inst.Status == DungeonStatuses.Extracting) return DungeonStatuses.Extracting;
            if (DungeonCleared(s, dungeonId)) return DungeonResetReady(s, dungeonId) ? DungeonStatuses.ResetReady : DungeonStatuses.Cleared;
            return inst.Status;
        }

        /// <summary>
        /// 可重置判定接口（R03 D8：通关 72 世界小时后、全队在父地点、实例内无人/无事务）。clearedAt/resetEligibleAt 与重置事务属批次 B22，本批恒为 false。
        /// </summary>
        public bool DungeonResetReady(GameState s, string dungeonId) => false;

        /// <summary>状态的中文短名（预告与开发入口显示）。</summary>
        public static string DungeonStatusName(string status) => status switch
        {
            DungeonStatuses.Unopened => "未建立",
            DungeonStatuses.Active => "进行中（未通关）",
            DungeonStatuses.Extracting => "撤出中",
            DungeonStatuses.Cleared => "已通关",
            DungeonStatuses.ResetReady => "可重置",
            DungeonStatuses.CampaignEnded => "终局",
            _ => status,
        };

        // ───────────────────────── 远征提交、轨迹与结束 ─────────────────────────

        /// <summary>
        /// 提交一次远征（R03 D3：第一次跨过 01→危险房间的确认）：建 expeditionId、成员快照（当时同行）、轨迹起点 01、提交分钟；实例 UNOPENED→ACTIVE。
        /// origin：null 正常提交；"migrated32" 存档 32 人已在危险房间；"dev" 开发传送。trail 为空时从 01 起。
        /// </summary>
        private ExpeditionState CommitExpedition(GameState s, string dungeonId, string? origin = null, List<string>? trail = null)
        {
            var inst = EnsureInstance(s, dungeonId);
            inst.ExpeditionSeq += 1;
            var iid = InstanceId(s, dungeonId);
            var e = new ExpeditionState
            {
                Id = $"{iid}:e{inst.ExpeditionSeq}",
                DungeonId = dungeonId,
                InstanceId = iid,
                Generation = inst.Generation,
                StartedMinute = s.GameMinute,
                Members = new List<string>(s.Party),
                Trail = trail ?? new List<string> { BufferNodeOf(dungeonId)! },
                Origin = origin,
            };
            if (inst.Status != DungeonStatuses.CampaignEnded) inst.Status = DungeonStatuses.Active;
            s.Expedition = e;
            return e;
        }

        /// <summary>到达（或撤退落到）地牢房间：维护来路轨迹（开发假设 B21-5）——回到轨迹上已有房间时截掉其后部分，否则追加。</summary>
        private static void TrackTrail(ExpeditionState e, string node)
        {
            var k = e.Trail.IndexOf(node);
            if (k >= 0) e.Trail.RemoveRange(k + 1, e.Trail.Count - k - 1);
            else e.Trail.Add(node);
        }

        /// <summary>
        /// 结束当前远征（kind：cleared_exit 通关离开 / abort 主动终止抵达父地点 / death 主角终局 / dev 开发结束）：写已结束记录，实例回 ACTIVE
        /// （终局为 CAMPAIGN_ENDED；通关按事实显示 CLEARED），清空进行中远征。
        /// </summary>
        private void EndExpedition(GameState s, string kind)
        {
            var e = s.Expedition;
            if (e == null) return;
            if (s.DungeonInstances.TryGetValue(e.DungeonId, out var inst))
                inst.Status = kind == "death" ? DungeonStatuses.CampaignEnded : DungeonStatuses.Active;
            s.ExpeditionLog.Add(new ExpeditionRecord
            {
                Id = e.Id, DungeonId = e.DungeonId, Generation = e.Generation, StartedMinute = e.StartedMinute,
                EndedMinute = s.GameMinute, EndKind = kind, Members = new List<string>(e.Members),
            });
            s.Expedition = null;
            if (kind == "abort" || kind == "cleared_exit") OnExpeditionArrived(s, e);
        }

        /// <summary>
        /// 远征成功抵达外部父地点的钩子（R03 D7/D10“存活抵达原子移动角色与封存owner”）。封存战利品账与回收箱属批次 B22：本批个人随身物照旧归本人，
        /// 这里不做任何迁移。
        /// </summary>
        private void OnExpeditionArrived(GameState s, ExpeditionState e) { }

        /// <summary>主角死亡或尸变（R03 D5.2/D6“主角死亡立即终局，停止未来返程段”）：进行中的远征按终局结束，实例记 CAMPAIGN_ENDED。</summary>
        private void ExpeditionOnDeath(GameState s)
        {
            if (s.Expedition != null) EndExpedition(s, "death");
        }

        // ───────────────────────── 地牢内路径（B21-4） ─────────────────────────

        /// <summary>
        /// 地牢内可用于返程/轨迹的边（开发假设 B21-4）：本代已开通、两端本代都到过的双向内部边，加 01↔父地点入口边；不用单向出口、不走没到过的房间。
        /// </summary>
        private IEnumerable<EdgeConfig> ExpeditionEdges(GameState s, string dungeonId)
        {
            var d = DungeonCfg(dungeonId);
            var buf = BufferNodeOf(dungeonId);
            if (d == null || buf == null) yield break;
            s.DungeonInstances.TryGetValue(dungeonId, out var inst);
            foreach (var e in Config.Edges)
            {
                if (!e.Bidirectional) continue;
                var inA = DungeonAt(e.A) == dungeonId;
                var inB = DungeonAt(e.B) == dungeonId;
                if (inA && inB)
                {
                    if (inst == null || !EdgeOpen(s, e)) continue;
                    if (!inst.VisitedRooms.Contains(e.A) || !inst.VisitedRooms.Contains(e.B)) continue;
                    yield return e;
                }
                else if ((e.A == buf && e.B == d.Parent) || (e.B == buf && e.A == d.Parent)) yield return e;
            }
        }

        /// <summary>
        /// 基础分钟最短路（Dijkstra，同长按配置边顺序先到者）：返回节点序列（含两端）与各段基础分钟；不可达为 null。
        /// </summary>
        private (List<string> Nodes, List<int> Legs)? ShortestPath(IEnumerable<EdgeConfig> edges, string from, string to)
        {
            var adj = new Dictionary<string, List<(string To, int Min)>>();
            void Add(string a, string b, int m)
            {
                if (!adj.TryGetValue(a, out var l)) adj[a] = l = new List<(string, int)>();
                l.Add((b, m));
            }
            foreach (var e in edges) { Add(e.A, e.B, e.BaseMinutes); Add(e.B, e.A, e.BaseMinutes); }
            var dist = new Dictionary<string, int> { [from] = 0 };
            var prev = new Dictionary<string, (string Node, int Min)>();
            var done = new HashSet<string>();
            while (true)
            {
                string? u = null;
                foreach (var kv in dist) if (!done.Contains(kv.Key) && (u == null || kv.Value < dist[u])) u = kv.Key;
                if (u == null) return null;
                if (u == to) break;
                done.Add(u);
                if (!adj.TryGetValue(u, out var nb)) continue;
                foreach (var (v, m) in nb)
                {
                    if (done.Contains(v)) continue;
                    var nd = dist[u] + m;
                    if (!dist.TryGetValue(v, out var old) || nd < old) { dist[v] = nd; prev[v] = (u, m); }
                }
            }
            var nodes = new List<string> { to };
            var legs = new List<int>();
            for (var x = to; x != from;) { var p = prev[x]; legs.Insert(0, p.Min); nodes.Insert(0, p.Node); x = p.Node; }
            return (nodes, legs);
        }

        /// <summary>从入口缓冲 01 到某房间的已走通最短路（迁移/开发补建远征的轨迹用）；不可达时退回 [01, 房间]。</summary>
        private List<string> TrailFromBuffer(GameState s, string dungeonId, string node)
        {
            var buf = BufferNodeOf(dungeonId)!;
            if (node == buf) return new List<string> { buf };
            var path = ShortestPath(ExpeditionEdges(s, dungeonId), buf, node);
            return path?.Nodes ?? new List<string> { buf, node };
        }

        // ───────────────────────── 存档 32→33（B21-13） ─────────────────────────

        /// <summary>
        /// 存档 32→33（开发假设 B21-13）：每座已有实例记录的地牢补本代到过的房间（按到访标记）与状态——除 01 外有房间到过为 ACTIVE（核心记录已领取/核对的
        /// 按事实显示 CLEARED），否则 UNOPENED。存档时人在某地牢危险房间：补建一次已提交远征（成员=当前同行、轨迹=01 到当前的已走通最短路，日志写明）；
        /// 人在 01 不建远征（可免费离开）。不补扣任何包、不加世界时间。返回给玩家看的说明。幂等（已有远征不重建）。
        /// </summary>
        public List<string> MigrateExpeditions32(GameState s)
        {
            var notes = new List<string>();
            foreach (var kv in s.DungeonInstances)
            {
                var d = kv.Key;
                var inst = kv.Value;
                inst.VisitedRooms ??= new HashSet<string>();
                foreach (var l in DungeonRooms(d))
                    if (s.Locations.TryGetValue(l.Id, out var ls) && ls.Visited) inst.VisitedRooms.Add(l.Id);
                if (inst.Status != DungeonStatuses.Unopened) continue;
                var buf = BufferNodeOf(d);
                if (inst.VisitedRooms.Any(r => r != buf)) inst.Status = DungeonStatuses.Active;
            }
            if (s.Expedition == null && !s.Progress.Dead && IsDangerRoom(s.CurrentLocation))
            {
                var d = DungeonAt(s.CurrentLocation)!;
                EnsureInstance(s, d).VisitedRooms.Add(s.CurrentLocation);
                var e = CommitExpedition(s, d, "migrated32", TrailFromBuffer(s, d, s.CurrentLocation));
                notes.Add($"你在{DungeonCfg(d)!.Name}的{LocName(s.CurrentLocation)}：按已开始的远征处理（{e.Id}），离开要在安全点终止远征或通关后离开");
            }
            return notes;
        }

        // ───────────────────────── 进入提交与离开（M393，R03 D3/D4） ─────────────────────────

        /// <summary>
        /// 从当前地点走向 target 是否需要先确认开始远征（R03 D3：在 01 第一次走向危险房间、本地牢没有进行中远征）。网页据此弹进入预告，
        /// 确认后用 ConfirmExpedition=true 的移动请求开始。
        /// </summary>
        public bool NeedsExpeditionConfirm(GameState s, string target)
        {
            var here = s.CurrentLocation;
            var d = DungeonAt(here);
            if (!ExpeditionEnabled(d) || here != BufferNodeOf(d!)) return false;
            if (!IsDangerRoom(target) || DungeonAt(target) != d) return false;
            if (s.Expedition?.DungeonId == d) return false;
            var edge = EdgeBetween(here, target);
            return edge != null && EdgeOpen(s, edge);
        }

        /// <summary>撤出中拒绝其他操作的原因（开发假设 B21-9）。</summary>
        public string ExtractingBusyReason(GameState s)
        {
            var parent = s.Expedition != null ? DungeonCfg(s.Expedition.DungeonId)?.Parent : null;
            return $"正在撤出（不可取消）：到达{(parent != null ? LocName(parent) : "外部入口")}之前不能做别的事";
        }

        /// <summary>终止惩罚包短句（每人）：“饱食−25、水分−30、体力−8、清醒−20、精神−15、健康最多−2（不致死）”。</summary>
        public string AbortPenaltyText()
        {
            var p = DX.AbortPenalty;
            return $"饱食{Fmt.SignedT(p.Satiety)}、水分{Fmt.SignedT(p.Hydration)}、体力{Fmt.SignedT(p.Energy)}、清醒{Fmt.SignedT(p.Wakefulness)}、精神{Fmt.SignedT(p.Morale)}、健康最多−{Fmt.T(p.HealthMax)}（不致死）";
        }

        /// <summary>惊魂未定短句：“惊魂未定6小时（下一次入战恐慌+10）”。</summary>
        public string ShakenText() => $"惊魂未定{Clock.Duration(DX.ShakenMinutes)}（下一次入战恐慌+{Fmt.T(DX.ShakenPanic)}）";

        /// <summary>
        /// 移动校验的远征部分（R03 D3/D4/D5）：01→危险房间且没有进行中远征 → 未确认拒绝、确认则开始时提交（commit 返回地牢 ID）；
        /// 有进行中远征且本代未通关时离开地牢 → 拒绝，提示走“终止本次远征”（DR03）。通关后离开（出口或 01 原路）不拦（B21-12）。
        /// </summary>
        private string? ExpeditionTravelBlock(GameState s, string here, string target, bool confirm, out string? commit)
        {
            commit = null;
            var d = DungeonAt(here);
            if (!ExpeditionEnabled(d)) return null;
            var exp = s.Expedition?.DungeonId == d ? s.Expedition : null;
            if (exp == null && here == BufferNodeOf(d!) && IsDangerRoom(target) && DungeonAt(target) == d)
            {
                if (!confirm) return ExpeditionConfirmReason;
                commit = d;
                return null;
            }
            if (exp != null && DungeonAt(target) != d && !DungeonCleared(s, d!))
                return $"未通关离开要先终止本次远征（在安全点或菜单里发起）：每人{AbortPenaltyText()}、{ShakenText()}，撤出至少{DX.ExtractMinMinutes}分钟";
            return null;
        }

        /// <summary>开始移动时提交远征（R03 D3“确认才提交远征开始/成员快照”，开发假设 B21-3：确认即开始，之后取消移动也不退）。</summary>
        private void StartExpeditionOnTravel(GameState s, string dungeonId)
        {
            var e = CommitExpedition(s, dungeonId);
            var d = DungeonCfg(dungeonId)!;
            Emit(s, GameEventKind.Info,
                $"远征开始：{d.Name}第{e.Generation}代（{e.Members.Count}人同行）。未通关离开要在安全点终止远征：每人{AbortPenaltyText()}、{ShakenText()}，撤出至少{DX.ExtractMinMinutes}分钟；战斗里撤退只退回来路上没有敌人的房间，不算离开。");
        }

        /// <summary>移动到达后（R03 D2/D4）：还在本远征地牢里就维护来路轨迹；离开了地牢（只有通关后才放行）则按“通关离开”结束远征，不扣惩罚。</summary>
        private void ExpeditionAfterArrive(GameState s, string target)
        {
            var e = s.Expedition;
            if (e == null) return;
            if (DungeonAt(target) == e.DungeonId) { TrackTrail(e, target); return; }
            var name = DungeonCfg(e.DungeonId)?.Name ?? e.DungeonId;
            EndExpedition(s, "cleared_exit");
            Emit(s, GameEventKind.Info, $"通关离开{name}：本次远征结束，不扣终止惩罚。");
        }

        /// <summary>
        /// 开发传送（?dev=1，开发假设 B21-14）：传进危险房间时按已开始的远征补提交（不扣包）；进行中远征时传到地牢外直接结束远征（不扣包）；
        /// 同地牢内传送重算轨迹（01 到目标的已走通最短路）。返回说明短语，没有为 null。
        /// </summary>
        private string? DevExpeditionAfterTeleport(GameState s, string loc)
        {
            var d = DungeonAt(loc);
            if (s.Expedition != null && s.Expedition.DungeonId != d)
            {
                EndExpedition(s, "dev");
                return "开发传送离开地牢：本次远征直接结束（不扣惩罚）";
            }
            if (s.Expedition != null) { s.Expedition.Trail = TrailFromBuffer(s, d!, loc); return null; }
            if (!IsDangerRoom(loc) || s.Progress.Dead) return null;
            var e = CommitExpedition(s, d!, "dev", TrailFromBuffer(s, d!, loc));
            return $"开发传送进危险房间：按已开始的远征处理（{e.Id}）";
        }

        // ───────────────────────── 普通战斗撤退落点（M394，R03 D4，B21-5） ─────────────────────────

        /// <summary>该房间是否有活跃敌人（存活、未隐藏、未在交战中的世界敌人，含固定战敌人）。</summary>
        private bool HasActiveEnemy(GameState s, string node) => s.WorldEnemies.Any(w => w.LocationId == node && !w.Engaged && !Hidden(s, w));

        /// <summary>
        /// 撤退落点（开发假设 B21-5，待确认 Q196）：本次远征在地牢危险房间里撤退成功——沿来路轨迹从上一个房间往回找第一个没有活跃敌人的房间，
        /// 都有敌人则回 01；不出地牢、不另加 5 分钟（含在本场唯一 tick 里）、不触发首次到达、不结束远征、不扣包。返回给战报的短语；不在远征里为 null（野外照旧原地）。
        /// </summary>
        public string? DungeonRetreatTarget(GameState s, string battleNode)
        {
            var e = s.Expedition;
            if (e == null || e.IsExtracting || DungeonAt(battleNode) != e.DungeonId || !IsDangerRoom(battleNode)) return null;
            var trail = new List<string>(e.Trail);
            var k0 = trail.IndexOf(battleNode);
            if (k0 >= 0) trail.RemoveRange(k0 + 1, trail.Count - k0 - 1); else trail.Add(battleNode);
            for (var k = trail.Count - 2; k >= 0; k--)
                if (!HasActiveEnemy(s, trail[k])) return trail[k];
            return BufferNodeOf(e.DungeonId);
        }

        private string? DungeonRetreatLanding(GameState s, BattleState b)
        {
            if (s.Progress.Dead) return null;
            var to = DungeonRetreatTarget(s, b.LocationId);
            if (to == null) return null;
            var e = s.Expedition!;
            TrackTrail(e, b.LocationId);
            s.CurrentLocation = to;
            s.CurrentZone = ExteriorZone;
            TrackTrail(e, to);
            return $"全队撤退到{LocName(to)}（沿来路第一个没有敌人的房间{(to == BufferNodeOf(e.DungeonId) ? "，即入口缓冲" : "")}；仍在本次远征里）";
        }

        // ───────────────────────── 撤出路线与时长（R03 D5.2，B21-4/B21-7） ─────────────────────────

        /// <summary>撤出计划：返程节点（起点…01、父地点）、各段基础分钟、锁定倍率与原因、总分钟 T。</summary>
        public sealed class ExtractPlan
        {
            public List<string> Route = new List<string>();
            public List<int> Legs = new List<int>();
            public int BaseMinutes;
            public double Factor = 1;
            public List<string> FactorReasons = new List<string>();
            public int TotalMinutes;
        }

        /// <summary>T = max(30, 向上取 5 分钟整数(基础路程 × 倍率))（R03 D5.2；已含 01→父地点，不另加）。</summary>
        public int ExtractTotalMinutes(int baseMinutes, double factor)
        {
            var round = Math.Max(1, DX.ExtractRoundMinutes);
            var scaled = (int)(Math.Ceiling(baseMinutes * factor / round - 1e-9) * round);
            return Math.Max(DX.ExtractMinMinutes, scaled);
        }

        /// <summary>
        /// 从 from 撤出到该地牢父地点的计划（开发假设 B21-4/B21-7）：按本代已开通、两端到过的双向边 + 01↔父地点求基础分钟最短路（不用未发现捷径、不用单向出口）；
        /// 万一不通，沿来路轨迹反向。倍率取确认时全队最慢的 S01 移动倍率（夜间/负重/低E，封顶）。
        /// </summary>
        public ExtractPlan ComputeExtractPlan(GameState s, string dungeonId, string from)
        {
            var d = DungeonCfg(dungeonId)!;
            var plan = new ExtractPlan();
            var path = ShortestPath(ExpeditionEdges(s, dungeonId), from, d.Parent);
            if (path != null) { plan.Route = path.Value.Nodes; plan.Legs = path.Value.Legs; }
            else
            {
                var back = new List<string> { from };
                if (s.Expedition?.DungeonId == dungeonId)
                    for (var k = s.Expedition.Trail.Count - 1; k >= 0; k--) if (back.Last() != s.Expedition.Trail[k]) back.Add(s.Expedition.Trail[k]);
                var buf = BufferNodeOf(dungeonId)!;
                if (back.Last() != buf) back.Add(buf);
                back.Add(d.Parent);
                plan.Route = back;
                for (var k = 0; k + 1 < back.Count; k++)
                    plan.Legs.Add(EdgesAt(back[k]).FirstOrDefault(e => e.Other(back[k]) == back[k + 1])?.BaseMinutes ?? 5);
            }
            plan.BaseMinutes = plan.Legs.Sum();
            plan.Factor = TimeFactor(s, s.Actor(GameState.PlayerId), ActionType.Travel, plan.FactorReasons);
            plan.TotalMinutes = ExtractTotalMinutes(plan.BaseMinutes, plan.Factor);
            return plan;
        }

        // ───────────────────────── 进入预告（R03 D3、A10 §7，B21-15） ─────────────────────────

        /// <summary>
        /// 进入前预告（R03 D3）：容量与实际同行、已揭示固定战、全员 N/W/E/S/M/H/I/V 与警告、随身药/弹/武器耐久、续探与否、终止惩罚具体数值、
        /// 撤出最低 30 分钟与当前估算、探索时间区间（B21-15）、提示。只读，不建实例、不抽物资。首次/重复奖励与重置剩余时间在 B22。
        /// </summary>
        public ExpeditionPreviewInfo ExpeditionPreview(GameState s, string dungeonId)
        {
            var d = DungeonCfg(dungeonId)!;
            var p = new ExpeditionPreviewInfo
            {
                DungeonId = dungeonId,
                Name = d.Name,
                ParentName = LocName(d.Parent),
                BufferName = LocName(BufferNodeOf(dungeonId) ?? d.SafeNodes.FirstOrDefault() ?? ""),
                Status = DungeonStatus(s, dungeonId),
                Capacity = BattleCapacity(s),
                PartyCount = s.Party.Count,
                InExpedition = s.Expedition?.DungeonId == dungeonId,
            };
            p.StatusName = DungeonStatusName(p.Status);
            if (s.DungeonInstances.TryGetValue(dungeonId, out var inst))
            {
                p.Generation = inst.Generation;
                p.Continuation = inst.ExpeditionSeq > 0 || inst.Status != DungeonStatuses.Unopened;
            }
            else p.Generation = 1;
            p.Cleared = DungeonCleared(s, dungeonId);
            foreach (var st in s.Population.Fixed.Values.Where(x => x.DungeonId == dungeonId && !x.Won).OrderBy(x => x.NodeId))
            {
                if (FixedStateAt(s, st.NodeId) == null) continue;
                var alive = FixedEnemiesAt(s, st.NodeId);
                if (alive.Count == 0) continue;
                var f = FixedBattleAt(st.NodeId);
                p.KnownEnemies.Add($"{LocName(st.NodeId)}：{(f?.Guardian == true ? "守卫 " : "")}{string.Join("、", alive.Select(w => _enemies[w.EnemyId].Name))}");
            }
            var w = DX.Warn;
            foreach (var a in s.PartyActors)
            {
                var st = a.Stats;
                var row = new ExpeditionMemberRow
                {
                    Id = a.Id, Name = ActorDisplayName(s, a.Id),
                    Satiety = st.Satiety, Hydration = st.Hydration, Energy = st.Energy, Wakefulness = st.Wakefulness, Morale = st.Morale,
                    Health = st.Health, Immunity = a.Body.Immunity, Virus = a.Body.VirusLoad, Infected = a.Body.Infected,
                };
                if (st.Satiety < w.SatietyBelow) row.Warnings.Add($"饱食低于{Fmt.T(w.SatietyBelow)}");
                if (st.Hydration < w.HydrationBelow) row.Warnings.Add($"水分低于{Fmt.T(w.HydrationBelow)}");
                if (st.Energy < w.EnergyBelow) row.Warnings.Add($"体力低于{Fmt.T(w.EnergyBelow)}");
                if (st.Wakefulness < w.WakefulnessBelow) row.Warnings.Add($"清醒低于{Fmt.T(w.WakefulnessBelow)}");
                if (st.Morale < w.MoraleBelow) row.Warnings.Add($"精神低于{Fmt.T(w.MoraleBelow)}");
                if (st.Health <= w.HealthAtMost + 1e-9) row.Warnings.Add($"健康不高于{Fmt.T(w.HealthAtMost)}");
                if (a.Body.VirusLoad >= w.VirusAtLeast - 1e-9) row.Warnings.Add($"病毒不低于{Fmt.T(w.VirusAtLeast)}");
                if (a.Body.BleedTriggers > 0) row.Warnings.Add("出血中");
                row.Supplies = SupplySummary(s, a.Id);
                p.Members.Add(row);
            }
            p.PenaltyText = $"每名存活同行{AbortPenaltyText()}，{ShakenText()}；已带的物资不没收";
            p.ExtractMinMinutes = DX.ExtractMinMinutes;
            var from = p.InExpedition && DungeonAt(s.CurrentLocation) == dungeonId ? s.CurrentLocation : BufferNodeOf(dungeonId)!;
            var plan = ComputeExtractPlan(s, dungeonId, from);
            p.ExtractFrom = LocName(from);
            p.ExtractEstimate = plan.TotalMinutes;
            p.ExtractBaseMinutes = plan.BaseMinutes;
            p.ExtractFactor = plan.Factor;
            (p.ExploreLow, p.ExploreHigh) = ExploreEstimate(s, dungeonId);
            p.Notes.Add("不需要带特定食物才能进；建议备好食水与医疗");
            p.Notes.Add("战斗中饱食、水分暂停变化，但探索、移动与整备照常消耗；撤出途中感染与伤病也会继续");
            p.Notes.Add($"在{p.BufferName}还没走进危险房间前，可按正常10分钟原路返回{p.ParentName}，不扣惩罚");
            if (p.Continuation) p.Notes.Add($"续探第{p.Generation}代：已打掉的敌人与已领的箱子不会刷新，受伤的敌人保留伤势");
            if (p.Cleared) p.Notes.Add("本代已通关：从出口或原路离开都不扣惩罚");
            return p;
        }

        /// <summary>某人随身与穿戴的药品、弹药数与武器耐久（进入预告用）。</summary>
        private string SupplySummary(GameState s, string actorId)
        {
            var carried = ItemInstance.CarriedOwner(actorId);
            var mine = s.Items.Where(i => i.Owner == carried || i.IsWornBy(actorId)).ToList();
            var med = mine.Where(i => _items.TryGetValue(i.ItemId, out var c) && c.Kind == "medical").Sum(i => i.Count);
            var ammo = mine.Where(i => _items.TryGetValue(i.ItemId, out var c) && c.Kind == "ammo").Sum(i => i.Count);
            var weapons = mine.Where(i => i.IsWornBy(actorId) && _items.TryGetValue(i.ItemId, out var c) && c.IsWeaponKind)
                .Select(i => i.Durability.HasValue && i.MaxDurability.HasValue ? $"{ItemName(i.ItemId)}耐久{Fmt.N(i.Durability.Value)}/{Fmt.N(i.MaxDurability.Value)}" : ItemName(i.ItemId)).ToList();
            return $"药品{med}份、弹药{ammo}发；" + (weapons.Count > 0 ? string.Join("、", weapons) : "没有武器（只能用拳）");
        }

        /// <summary>
        /// 预计探索基础分钟区间（开发假设 B21-15，待确认 Q200）：下限 = 01 到核心记录房最短路的移动 + 路上各房间（不含记录房）探索到 100%；
        /// 上限 = 全部房间探索到 100% + 每条内部边走一次。按当前探索度现算，不含战斗与倍率。
        /// </summary>
        public (int Low, int High) ExploreEstimate(GameState s, string dungeonId)
        {
            var d = DungeonCfg(dungeonId);
            var buf = BufferNodeOf(dungeonId);
            if (d == null || buf == null) return (0, 0);
            var rooms = DungeonRooms(dungeonId).Select(l => l.Id).ToList();
            var internalEdges = Config.Edges.Where(e => e.Bidirectional && DungeonAt(e.A) == dungeonId && DungeonAt(e.B) == dungeonId).ToList();
            int ToFull(string n)
            {
                var prog = s.Locations.TryGetValue(n, out var ls) ? ls.Progress : 0;
                if (prog >= 100 || !_locs[n].ExploreEnabled) return 0;
                var inc = Math.Max(1, ExploreIncrementAt(n));
                return (int)Math.Ceiling((100 - prog) / (double)inc) * ExploreMinutesAt(n);
            }
            var path = ShortestPath(internalEdges, buf, d.RecordNode);
            var low = path == null ? 0 : path.Value.Legs.Sum() + path.Value.Nodes.Take(path.Value.Nodes.Count - 1).Sum(ToFull);
            var high = rooms.Sum(ToFull) + internalEdges.Sum(e => e.BaseMinutes);
            return (low, high);
        }
    }

    /// <summary>进入前预告（R03 D3，B21）。</summary>
    public sealed class ExpeditionPreviewInfo
    {
        public string DungeonId { get; set; } = "";
        public string Name { get; set; } = "";
        public string ParentName { get; set; } = "";
        public string BufferName { get; set; } = "";
        public string Status { get; set; } = "";
        public string StatusName { get; set; } = "";
        public int Generation { get; set; }
        /// <summary>旧实例续探（已提交过远征或已有进度）。</summary>
        public bool Continuation { get; set; }
        public bool Cleared { get; set; }
        /// <summary>当前就在这座地牢的进行中远征里。</summary>
        public bool InExpedition { get; set; }
        public int Capacity { get; set; }
        public int PartyCount { get; set; }
        public List<string> KnownEnemies { get; set; } = new List<string>();
        public List<ExpeditionMemberRow> Members { get; set; } = new List<ExpeditionMemberRow>();
        public string PenaltyText { get; set; } = "";
        public int ExtractMinMinutes { get; set; }
        public string ExtractFrom { get; set; } = "";
        public int ExtractEstimate { get; set; }
        public int ExtractBaseMinutes { get; set; }
        public double ExtractFactor { get; set; } = 1;
        public int ExploreLow { get; set; }
        public int ExploreHigh { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
    }

    /// <summary>预告/终止确认里的一名同行。</summary>
    public sealed class ExpeditionMemberRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Energy { get; set; }
        public double Wakefulness { get; set; }
        public double Morale { get; set; }
        public double Health { get; set; }
        public double Immunity { get; set; }
        public double Virus { get; set; }
        public bool Infected { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public string Supplies { get; set; } = "";
    }
}

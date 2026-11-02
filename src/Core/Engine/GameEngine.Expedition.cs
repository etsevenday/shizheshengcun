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
    }
}

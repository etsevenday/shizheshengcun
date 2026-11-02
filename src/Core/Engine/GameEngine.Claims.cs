using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 资源四账（开发计划 v0.9 批次 B12，R36/R37；S04「四账及奖励防重」、S05「定量采集与四生命周期」「唯一与迁移」、A08、gate-contract stateRules）：
    /// <list type="bullet">
    /// <item>world_stock：资源状态库存、午夜日补夹到 cap（含预留），离线不补。</item>
    /// <item>instanceId（地牢普通箱）：库存记在当前地牢实例代号上，只在合法新一代恢复（<see cref="NewDungeonGeneration"/>）；正式重置条件（72 小时、撤出）在 B21/B22。</item>
    /// <item>campaignClaimId（8 份核心记录）：领取写 campaignId:&lt;resourceId&gt;、claim:&lt;resourceId&gt;、记录 ID 事实与 factProducers 事实，再写当前代实例事实
    /// recordClaimedAndClear；不发物资卡。已领后新一代再到只补写本代实例事实，不重发（开发假设 H4）。</item>
    /// <item>知识/任务账：监听同一 resourceId 的事实（knowledge grantedByFacts、容量 church_route_record 等），不另发第二份。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>单局唯一领取键（A09 表“campaignId:d01_08_unique”）。</summary>
        public static string CampaignKey(string resourceId) => "campaignId:" + resourceId;

        /// <summary>知识授予用的领取事实（config knowledge grantedByFacts “claim:d03_08_unique”）。</summary>
        public static string ClaimKey(string resourceId) => "claim:" + resourceId;

        /// <summary>实例事实：本代核心记录已领取/核对（gate-contract instance_complete 读它）。</summary>
        public const string RecordClaimedFact = "recordClaimedAndClear";

        /// <summary>某唯一领取是否已领（本局永久）。</summary>
        public bool CampaignClaimed(GameState s, string resourceId) => s.Progress.Flags.Contains(CampaignKey(resourceId));

        /// <summary>按当前状态的动词：本局已领的核心记录写“核对”（新一代只补写本代实例事实），其余同 <see cref="GatherVerb"/>。</summary>
        public string GatherVerbNow(GameState s, ResourceConfig res) =>
            res.IsCampaign && CampaignClaimed(s, res.NodeId) ? "核对" : GatherVerb(res);

        /// <summary>资源点所在地牢；不在地牢为 null。</summary>
        public string? DungeonOf(ResourceConfig res) => _locs.TryGetValue(res.LocationId, out var lc) ? lc.DungeonId : null;

        /// <summary>核心记录现在不能领取/核对的原因；可以为 null。</summary>
        private string? ClaimBlocked(GameState s, ResourceConfig res)
        {
            var d = DungeonOf(res);
            var claimed = CampaignClaimed(s, res.NodeId);
            if (!claimed) return null;
            if (d == null || InstanceFact(s, d, RecordClaimedFact)) return "已领取（本局唯一，不再重复发放）";
            return null;
        }

        /// <summary>领取预演说明：领到什么、写哪些事实、不发物资卡；已领则写“只核对本次地牢”。</summary>
        private List<string> ClaimNotes(GameState s, ResourceConfig res)
        {
            var notes = new List<string>();
            if (CampaignClaimed(s, res.NodeId))
            {
                notes.Add($"{res.Name}本局已领取：这次只核对本次地牢的记录（打开本代出口），不再重复发放");
                return notes;
            }
            notes.Add($"领取{res.Name}：本局唯一一次，地牢换代后也不会再发；记作记录，不占物资卡");
            var know = KnowledgeGrantedBy(s, ClaimFactsOf(res));
            if (know.Count > 0) notes.Add("同时获得做法知识：" + string.Join("、", know));
            if (res.RecordId != null && Config.Scope.Capacity.Unlocks.Any(u => u.AllOf.Contains(res.RecordId) || u.AnyOf.Contains(res.RecordId)))
                notes.Add("这份记录计入战斗容量解锁（教会路线记录、学院安全记录）");
            return notes;
        }

        /// <summary>领取同事务写入的全部 campaign 事实。</summary>
        private IEnumerable<string> ClaimFactsOf(ResourceConfig res)
        {
            yield return CampaignKey(res.NodeId);
            yield return ClaimKey(res.NodeId);
            if (res.RecordId != null) yield return res.RecordId;
            foreach (var f in res.ClaimFacts) yield return f;
        }

        /// <summary>写入这些事实后新获得的知识名（现在还不会的）。</summary>
        private List<string> KnowledgeGrantedBy(GameState s, IEnumerable<string> facts)
        {
            var set = new HashSet<string>(facts);
            return Config.Knowledge
                .Where(k => k.GrantedByFacts.Any(set.Contains) && !k.GrantedByFacts.Any(s.Progress.Flags.Contains) && !s.Progress.Flags.Contains("knowledge:" + k.Id))
                .Select(k => k.Name).ToList();
        }

        /// <summary>核心记录领取/核对完成：一次事务写 campaign 事实与本代实例事实，重判门槛；不发物资、不给经验、无划伤。</summary>
        private bool CompleteClaim(GameState s, ActionState a, ResourceConfig res)
        {
            ClearAction(s, a);
            var d = DungeonOf(res);
            var first = !CampaignClaimed(s, res.NodeId);
            var text = new List<string>();
            if (first)
            {
                var know = KnowledgeGrantedBy(s, ClaimFactsOf(res));
                foreach (var f in ClaimFactsOf(res)) s.Progress.Flags.Add(f);
                s.Resources[res.NodeId].Available = 0;
                text.Add($"已领取{res.Name}（本局唯一，记作记录，不占物资卡）");
                if (know.Count > 0) text.Add("获得做法知识：" + string.Join("、", know));
            }
            else text.Add($"已核对本次地牢的{res.Name}（本局早已领取，不再重复发放）");
            if (d != null) EnsureInstance(s, d).Facts.Add(RecordClaimedFact);
            var routes = RouteNotes(s, RefreshGates(s), s.CurrentLocation);
            if (routes.Count > 0) text.Add("发现" + string.Join("与", routes));
            text.Add(NoFieldXpText(res));
            UpdateFlags(s);
            Emit(s, GameEventKind.ActionCompleted, string.Join("；", text) + "。");
            return true;
        }

        /// <summary>
        /// 资源卡角标（S14「地图、来源与四类账」：不同账写清楚）：日补“10/12”；finite“6/8·不补”/“搜空不补”；flowing“无限”；
        /// 地牢普通箱“本代剩余2/2”/“本代已搜空”；核心记录“可领取”/“本局已领·可核对”/“已领取”；单次箱“可搜1次”/“已搜空”。Empty 为灰显。
        /// </summary>
        public (string Badge, bool Empty) ResourceStock(GameState s, ResourceConfig r)
        {
            var rs = s.Resources[r.NodeId];
            var avail = rs.Available ?? 0;
            if (r.IsCampaign)
            {
                if (!CampaignClaimed(s, r.NodeId)) return ("可领取", false);
                var d = DungeonOf(r);
                return d != null && !InstanceFact(s, d, RecordClaimedFact) ? ("本局已领·可核对", false) : ("已领取", true);
            }
            if (r.IsUnlimited) return ("无限", false);
            if (r.IsInstance) return avail > 0 ? ($"本代剩余{avail}/{r.Cap}", false) : ("本代已搜空", true);
            if (r.IsCache) return avail > 0 ? ("可搜1次", false) : ("已搜空", true);
            if (!r.Refills) return avail > 0 ? ($"{avail}/{r.Cap}·不补", false) : ("搜空不补", true);
            return ($"{avail}/{r.Cap}", avail <= 0);
        }

        // ───────────────────────── 地牢实例代号 ─────────────────────────

        /// <summary>某地牢的全部房间（配置顺序）。</summary>
        public IEnumerable<LocationConfig> DungeonRooms(string dungeonId) => Config.Locations.Where(l => l.DungeonId == dungeonId);

        /// <summary>新实例（第 1 代）建立时：本地牢普通箱库存记到这一代（库存本就为 cap，S04 instance）。</summary>
        private void BindInstanceResources(GameState s, string dungeonId, int generation)
        {
            foreach (var r in Config.Resources.Where(r => r.IsInstance && DungeonOf(r) == dungeonId))
                if (s.Resources.TryGetValue(r.NodeId, out var rs) && rs.Generation < generation) rs.Generation = generation;
        }

        /// <summary>
        /// 地牢换代钩子（gate-contract stateRules.instanceLatch）：代号 +1，丢弃旧代实例边锁存（开通状态）、实例事实（守卫、核心核对）、房间探索度与清理次数，
        /// 普通箱补满到 cap 并记到新一代；campaign 知识（旧代已知路线记入 KnownEdges，仍显示为未开通并写原因）、唯一领取与已发现不动。正式触发条件（72 世界小时、撤出、全队同地）在 B21/B22，
        /// 本批只给开发入口与测试用（开发假设 H3）。返回 null 表示成功，否则为拒绝原因。
        /// </summary>
        public string? NewDungeonGeneration(GameState s, string dungeonId)
        {
            if (!Config.Map.Dungeons.Any(x => x.Id == dungeonId)) return "地牢不存在";
            if (IsBusy(s)) return "行动进行中或有敌踪/战斗，先结束再换代";
            if (_locs[s.CurrentLocation].DungeonId == dungeonId) return "人在这座地牢里，先离开再换代";
            var inst = EnsureInstance(s, dungeonId);
            // 路线知识是 campaign 的（instanceLatch）：旧代已开通或已揭示的本地牢路线记为已知，只退回开通状态
            foreach (var e in Config.Edges.Where(e => e.DungeonId == dungeonId && EdgeRevealed(s, e))) inst.KnownEdges.Add(e.Id);
            inst.Generation += 1;
            inst.UnlockedEdges.Clear();
            inst.Facts.Clear();
            foreach (var l in DungeonRooms(dungeonId))
            {
                var ls = s.Locations[l.Id];
                ls.Progress = l.InitialProgress;
                ls.TriggeredMilestones.Clear();
                ls.ClearCharges = l.Clear.InitialCharges;
                ls.ClearRefillProgress = 0;
                ls.ClearUpdatedMinute = s.GameMinute;
            }
            foreach (var r in Config.Resources.Where(r => r.IsInstance && DungeonOf(r) == dungeonId))
            {
                var rs = s.Resources[r.NodeId];
                rs.Available = r.Cap;
                rs.Reserved = 0;
                rs.Generation = inst.Generation;
            }
            RefreshGates(s);
            return null;
        }

        /// <summary>开发入口（?dev=1）：某座地牢换代并说明。</summary>
        public string DebugNewDungeonGeneration(GameState s, string dungeonId)
        {
            var why = NewDungeonGeneration(s, dungeonId);
            if (why != null) return $"开发：{why}";
            var name = Config.Map.Dungeons.First(x => x.Id == dungeonId).Name;
            return $"开发：{name}已换到第{s.DungeonInstances[dungeonId].Generation}代——普通物资箱补满、房间探索度与清理次数重置、守卫与本代记录核对清空；核心记录（本局唯一）与已开世界路线不变。";
        }
    }
}

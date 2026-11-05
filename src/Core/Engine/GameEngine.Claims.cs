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

        /// <summary>
        /// 揭示/领取资格（B13b，Q120）：资源点写了 unlockAnyFacts 时须有其一（工坊首次支援包：FA02_record_credit 或可信旧 QMAP02 已完成）。
        /// </summary>
        public bool ResourceUnlocked(GameState s, ResourceConfig res) =>
            res.UnlockAnyFacts.Count == 0 || res.UnlockAnyFacts.Any(s.Progress.Flags.Contains);

        /// <summary>某唯一领取是否已领（本局永久）。</summary>
        public bool CampaignClaimed(GameState s, string resourceId) => s.Progress.Flags.Contains(CampaignKey(resourceId));

        /// <summary>按当前状态的动词：本局已领的核心记录写“核对”（新一代只补写本代实例事实），其余同 <see cref="GatherVerb"/>。</summary>
        public string GatherVerbNow(GameState s, ResourceConfig res) =>
            res.IsRecord && CampaignClaimed(s, res.NodeId) ? "核对" : GatherVerb(res);

        /// <summary>资源点所在地牢；不在地牢为 null。</summary>
        public string? DungeonOf(ResourceConfig res) => _locs.TryGetValue(res.LocationId, out var lc) ? lc.DungeonId : null;

        /// <summary>
        /// 实例事实条件（Q122：守卫战利品箱读 d03 当前代 guardianDefeated，B13c）是否满足；没有此条件为满足。
        /// 只读当前代实例事实：换代后须本代再次满足，旧代或别的地牢不算。
        /// </summary>
        public bool InstanceConditionMet(GameState s, ResourceConfig res) =>
            res.RevealInstanceFact == null || (DungeonOf(res) is string d && InstanceFact(s, d, res.RevealInstanceFact));

        /// <summary>
        /// 实例事实条件的资源点（守卫战利品箱）：本代有该事实、且到过该地点时显示（守卫胜利后当场出现可领取入口，不必折返别的房间；B13c，Q122）。
        /// 只显示，不写领取账、不生成物品。返回新显示的资源点名。守卫战（B20）提交胜利事实后也调它；现在由开发入口记事实时调。
        /// </summary>
        private List<string> RevealByInstanceFacts(GameState s)
        {
            var list = new List<string>();
            foreach (var r in Config.Resources.Where(r => r.RevealInstanceFact != null))
            {
                var rs = s.Resources[r.NodeId];
                if (rs.Discovered || !s.Locations[r.LocationId].Visited || !ResourceUnlocked(s, r) || !InstanceConditionMet(s, r)) continue;
                rs.Discovered = true;
                list.Add(r.Name);
            }
            return list;
        }

        /// <summary>核心记录现在不能领取/核对的原因；可以为 null。</summary>
        private string? ClaimBlocked(GameState s, ResourceConfig res)
        {
            var d = DungeonOf(res);
            var claimed = CampaignClaimed(s, res.NodeId);
            if (!res.IsRecord && claimed) return "已领取（本局唯一，不再重复发放）";
            if (!ResourceUnlocked(s, res)) return "还没有领取资格";
            // Q122：守卫战利品箱须本代守卫胜利（换代后未领的要本代再胜一次）
            if (!claimed && !InstanceConditionMet(s, res)) return "需要：" + FactLabel(res.RevealInstanceFact!) + "（本代）";
            if (!claimed) return null;
            if (d == null || InstanceFact(s, d, RecordClaimedFact)) return "已领取（本局唯一，不再重复发放）";
            return null;
        }

        /// <summary>领取预演说明：领到什么、写哪些事实、不发物资卡；已领则写“只核对本次地牢”。</summary>
        private List<string> ClaimNotes(GameState s, ResourceConfig res)
        {
            var notes = new List<string>();
            if (CampaignClaimed(s, res.NodeId) && !res.IsRecord)
            {
                notes.Add($"{res.Name}本局已领取，不再重复发放");
                return notes;
            }
            if (CampaignClaimed(s, res.NodeId))
            {
                notes.Add($"{res.Name}本局已领取：这次只核对本次地牢的记录（打开本代出口），不再重复发放");
                notes.AddRange(StagedClaimNotes(s, res));
                return notes;
            }
            if (!res.IsRecord)
            {
                // B13b（Q120）：工坊首次支援包、消防车器材箱（实物整包落当地地面）、采石坡图册（知识包，不发纸卡或成品）
                notes.Add($"领取{res.Name}：本局唯一一次" + (res.IsCampaignPhysical ? "，产物整包放在当前地点地面" : "，只记作知识，不发纸卡或成品"));
                if (res.IsCampaignPhysical) notes.Add("包满也能领：放在地面后再逐人拾取（照常受随身五卡与负重限制）；不计采集/搜集经验，不能用工具加速");
                if (res.RevealInstanceFact != null) notes.Add("本代守卫战胜利后出现；领过以后地牢换代也不会再出现");
                // Q136 选 A（EQ-Q136-01）：预览只写区间，不消耗真实随机、不预生成；领取时按模板区间 0.1 步长正常抽一次最大耐久，当前=最大
                if (res.EquipmentQuality != null)
                    foreach (var id in res.Outputs.Keys.Where(k => _items[k].IsEquipment))
                    {
                        var range = _items[id].DurabilityMaxRange;
                        notes.Add(range is { Length: 2 }
                            ? $"{ItemName(id)}为白色、无附加词条；最大耐久在{Fmt.T(range[0])}—{Fmt.T(range[1])}内随机生成一次，领取时当前耐久为满"
                            : $"{ItemName(id)}为白色、无附加词条、满耐久");
                    }
                var k = KnowledgeGrantedBy(s, ClaimFactsOf(res));
                if (k.Count > 0) notes.Add("获得做法知识：" + string.Join("、", k));
                if (NoOutputYieldLine(res) is string yieldLine) notes.Add(yieldLine);   // Q254：选中后读得到卡面全句
                return notes;
            }
            notes.Add($"领取{res.Name}：本局唯一一次，地牢换代后也不会再发；记作记录，不占物资卡");
            var know = KnowledgeGrantedBy(s, ClaimFactsOf(res));
            if (know.Count > 0) notes.Add("同时获得做法知识：" + string.Join("、", know));
            // B23（A11 §3）：知识包里已从别处掌握的只记来源，不兑换材料、经验、钱或别的蓝图
            if (PackKnownNote(s, res, know) is string kn) notes.Add(kn);
            notes.AddRange(RecordQuestNotes(s, res, true));
            notes.AddRange(StagedClaimNotes(s, res));
            if (res.RecordId != null && Config.Scope.Capacity.Unlocks.Any(u => u.AllOf.Contains(res.RecordId) || u.AnyOf.Contains(res.RecordId)))
            {
                // Q125：容量说明用正式记录名（旧名“教会路线记录/学院安全记录”只是同一份记录 ID 的旧称）
                var names = Config.Resources.Where(r => r.RecordId != null && Config.Scope.Capacity.Unlocks.Any(u => u.AllOf.Contains(r.RecordId) || u.AnyOf.Contains(r.RecordId)))
                    .Select(r => r.Name).Distinct();
                notes.Add($"这份记录计入战斗容量解锁（{string.Join("、", names)}）");
            }
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
            s.Resources[res.NodeId].ClaimMinutesDone = 0;   // B23：两段都提交，本次领取/核对结束
            var d = DungeonOf(res);
            var first = !CampaignClaimed(s, res.NodeId);
            var text = new List<string>();
            var capBefore = BattleCapacity(s);
            if (first && !res.IsRecord)
            {
                // B13b（Q120 RES-Q120-03/04）：实物包整包生成在当地地面（装备白色无词条满耐久），知识包只补缺；与领取账同一事务
                var know = KnowledgeGrantedBy(s, ClaimFactsOf(res));
                var made = new List<ItemInstance>();
                // B22（R03 D7.1）：地牢里的唯一箱（d03 旧军械箱）产物记本实例来源，可封存
                var origin = d != null ? RewardOriginAt(s, res.LocationId) : null;
                foreach (var kv in res.Outputs)
                {
                    if (_items[kv.Key].IsEquipment && res.EquipmentQuality != null)
                        for (var k = 0; k < kv.Value; k++) { var one = NewSingle(s, kv.Key, s.Ground, false, null, null, "white"); one.RewardOrigin = origin; made.Add(one); }
                    else made.AddRange(CreateItems(s, kv.Key, kv.Value, s.Ground, rewardOrigin: origin));
                }
                Normalize(s, s.Ground);
                foreach (var f in ClaimFactsOf(res)) s.Progress.Flags.Add(f);
                s.Resources[res.NodeId].Available = 0;
                text.Add($"已领取{res.Name}（本局唯一）");
                if (res.Outputs.Count > 0)
                    text.Add(string.Join("，", res.Outputs.Select(kv => $"{ItemName(kv.Key)}+{kv.Value}")) + QualitySuffix(made) + $"，已放在{ZoneName(s, s.Ground)}地面");
                if (know.Count > 0) text.Add("获得做法知识：" + string.Join("、", know));
            }
            else if (first)
            {
                var know = KnowledgeGrantedBy(s, ClaimFactsOf(res));
                var packNote = PackKnownNote(s, res, know);   // 写事实前判断（之后都已掌握）
                foreach (var f in ClaimFactsOf(res)) s.Progress.Flags.Add(f);
                s.Resources[res.NodeId].Available = 0;
                text.Add($"已领取{res.Name}（本局唯一，记作记录，不占物资卡）");
                if (know.Count > 0) text.Add("获得做法知识：" + string.Join("、", know));
                if (packNote != null) text.Add(packNote);
                text.AddRange(RecordQuestNotes(s, res, false));
                // B15（A11 §4/R46）：首次拿到不同种类的关键记录时容量升档（同一种记录重置后再领不再加）
                if (BattleCapacity(s) > capBefore) text.Add($"队伍容量升到 C{BattleCapacity(s)}");
            }
            else text.Add($"已核对本次地牢的{res.Name}（本局早已领取，不再重复发放）");
            // B22（R03 D2、Q127）：只有核心记录的领取/核对才算本代通关（写 clearedAt/resetEligibleAt），并发本局首通补给包；
            // 顺带修正：B13c 起 d03 旧唯一箱（同为 campaign 领取）也会写本代完成事实、提前打开出口，现在只认记录
            if (d != null && res.IsRecord)
            {
                if (MarkCleared(s, d)) text.Add($"本代通关：{DR.ResetCooldownMinutes / 60}世界小时后（{Clock.DayTime(s.DungeonInstances[d].ResetEligibleAt!.Value)}起）可在{LocName(DungeonCfg(d)?.Parent ?? res.LocationId)}确认重置");
                if (GrantFirstClear(s, d, s.Ground) is string fc) text.Add(fc);
            }
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
                if (!CampaignClaimed(s, r.NodeId)) return InstanceConditionMet(s, r) ? ("可领取", false) : ("待本代守卫胜利", true);
                var d = DungeonOf(r);
                // 只有核心记录在新一代可“核对”；唯一实物箱（B13c 旧 d03 箱等）领过就是已领取，跨代不复活
                return r.IsRecord && d != null && !InstanceFact(s, d, RecordClaimedFact) ? ("本局已领·可核对", false) : ("已领取", true);
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
            if (s.Expedition?.DungeonId == dungeonId) return "这座地牢还有进行中的远征，先结束远征再换代";
            var inst = EnsureInstance(s, dungeonId);
            // B22（R03 D8）：先把旧实例尚存的真实物资/遗物迁到父地点“旧实例遗物认领箱”，再换代（不静默删除、不进新敌群）
            MoveLeftoversToRelicBox(s, dungeonId);
            // 路线知识是 campaign 的（instanceLatch）：旧代已开通或已揭示的本地牢路线记为已知，只退回开通状态
            foreach (var e in Config.Edges.Where(e => e.DungeonId == dungeonId && EdgeRevealed(s, e))) inst.KnownEdges.Add(e.Id);
            inst.Generation += 1;
            inst.UnlockedEdges.Clear();
            inst.Facts.Clear();
            inst.VisitedRooms.Clear();   // B21：新一代从未到过任何房间、尚未提交远征
            inst.ClearedAt = null;       // B22：新一代未通关、不计时
            inst.ResetEligibleAt = null;
            if (inst.Status != DungeonStatuses.CampaignEnded) inst.Status = DungeonStatuses.Unopened;
            foreach (var l in DungeonRooms(dungeonId))
            {
                var ls = s.Locations[l.Id];
                ls.Progress = l.InitialProgress;
                ls.TriggeredMilestones.Clear();
                ls.ClearCharges = l.Clear.InitialCharges;
                ls.ClearRefillProgress = 0;
                ls.ClearUpdatedMinute = s.GameMinute;
            }
            // B23：旧代未完成的核心记录领取/核对段属于旧实例，新一代从头算
            foreach (var r in Config.Resources.Where(r => r.IsRecord && DungeonOf(r) == dungeonId && s.Resources.ContainsKey(r.NodeId))) s.Resources[r.NodeId].ClaimMinutesDone = 0;
            foreach (var r in Config.Resources.Where(r => r.IsInstance && DungeonOf(r) == dungeonId))
            {
                var rs = s.Resources[r.NodeId];
                rs.Available = r.Cap;
                rs.Reserved = 0;
                rs.Generation = inst.Generation;
            }
            ResetFixedForGeneration(s, dungeonId);   // B20：旧代固定战敌人移除，新一代第一次到达重新揭示（新 worldEnemyId）
            RefreshGates(s);
            return null;
        }

        /// <summary>开发入口（?dev=1）：某座地牢换代并说明。</summary>
        public string DebugNewDungeonGeneration(GameState s, string dungeonId)
        {
            var why = NewDungeonGeneration(s, dungeonId);
            if (why != null) return $"开发：{why}";
            var name = Config.Map.Dungeons.First(x => x.Id == dungeonId).Name;
            return $"开发：{name}已换到第{s.DungeonInstances[dungeonId].Generation}代——普通物资箱补满、房间探索度与清理次数重置、守卫与本代记录核对清空、固定战下次到达重新揭示；核心记录（本局唯一）与已开世界路线不变。";
        }
    }
}

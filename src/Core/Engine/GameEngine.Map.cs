using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// v3 地图门槛（开发计划 v0.9 批次 B11，R34；S02「Q30B 完整门槛契约」、S03「Q30/Q31 探索与门槛衔接」、A08、gate-contract.json）。
    /// <list type="bullet">
    /// <item>谓词按 gate-contract expressionLanguage v1 解释：literal/and/or/fact/visited/exploreAtLeast/instanceFact/contextEquals/if；
    /// $authoredFrom/$dungeonParent/$dungeonId 从边数据绑定（A 是数据原 from，不用当前位置）；未知运算或未绑定变量抛错，不默认放行。</item>
    /// <item>campaign 门槛首次满足写永久锁存（s.UnlockedEdges），正反共享；current_instance 锁存在当前地牢实例；
    /// revalidate_current_instance（单向出口 instance_complete）不锁存、每次走边重验。</item>
    /// <item>重判时机：新局、每次实际到达成功、探索完成、事实提交、读档（含当前版本读档）。门槛谓词一满足就锁存开通并把该边两端记为已发现，
    /// 不要求任一端已到访（Q115 选 B，取代 B11 开发假设 B11-3）；发现不写到访、不改探索度、不发奖励；远方没有实例的地牢不凭发现创建实例。</item>
    /// <item>某地点探索到 100%（含到访自动 100）时揭示从它出发的全部表列边：已满足的照常开通，门槛未满足的显示为“未开通”并写明原因（S03 新区探索与发现、EXP30-01）。</item>
    /// </list>
    /// 事实来源：campaign 事实读 s.Progress.Flags（与容量事实 hub_registered 等同一字典）；tutorialBattleWon / legacyTutorialEquivalent 读剧情状态。
    /// 本批真实产生的事实只有 c02/r02 到达（church_contacted / academy_contacted）；其余事实的产生系统在后续批次，目前返回“未满足”。
    /// </summary>
    public sealed partial class GameEngine
    {
        private Dictionary<string, GateDefinitionConfig>? _gateDefs;
        private Dictionary<string, List<EdgeConfig>>? _edgesAt;

        private Dictionary<string, GateDefinitionConfig> GateDefs => _gateDefs ??= Config.Map.Gates.ToDictionary(g => g.Id);

        private Dictionary<string, List<EdgeConfig>> EdgesAtMap
        {
            get
            {
                if (_edgesAt != null) return _edgesAt;
                var m = new Dictionary<string, List<EdgeConfig>>();
                foreach (var e in Config.Edges)
                {
                    if (!m.TryGetValue(e.A, out var la)) m[e.A] = la = new List<EdgeConfig>();
                    la.Add(e);
                    if (!m.TryGetValue(e.B, out var lb)) m[e.B] = lb = new List<EdgeConfig>();
                    lb.Add(e);
                }
                return _edgesAt = m;
            }
        }

        /// <summary>与该地点相连的全部物理边（含单向边的两端）。</summary>
        public IReadOnlyList<EdgeConfig> EdgesAt(string locationId) =>
            EdgesAtMap.TryGetValue(locationId, out var l) ? l : (IReadOnlyList<EdgeConfig>)Array.Empty<EdgeConfig>();

        /// <summary>从 from 能直接走到 to 的边（单向出口只从数据原 from 走）；没有为 null。</summary>
        public EdgeConfig? EdgeBetween(string from, string to) =>
            EdgesAt(from).FirstOrDefault(e => e.TraversableFrom(from) && e.Other(from) == to);

        public GateDefinitionConfig? GateOf(EdgeConfig e) =>
            !string.IsNullOrEmpty(e.Gate) && GateDefs.TryGetValue(e.Gate!, out var g) ? g : null;

        /// <summary>这条边的解锁生命周期（无门槛的旧式边为 campaign）。</summary>
        public string GateScope(EdgeConfig e) => GateOf(e)?.UnlockScope ?? GateScopes.Campaign;

        /// <summary>当前地点已开通、可从这里出发的相邻连接。</summary>
        public IEnumerable<EdgeConfig> AdjacentUnlocked(GameState s)
        {
            var here = s.CurrentLocation;
            return EdgesAt(here).Where(e => e.TraversableFrom(here) && EdgeOpen(s, e));
        }

        /// <summary>
        /// 这条边现在是否可走：campaign 看永久锁存；current_instance 看当前地牢实例的锁存；revalidate 每次重验谓词（不锁存）。
        /// </summary>
        public bool EdgeOpen(GameState s, EdgeConfig e)
        {
            switch (GateScope(e))
            {
                case GateScopes.CurrentInstance:
                    return e.DungeonId != null && s.DungeonInstances.TryGetValue(e.DungeonId, out var inst) && inst.UnlockedEdges.Contains(e.Id);
                case GateScopes.RevalidateCurrentInstance:
                    return EvaluateGate(s, e);
                default:
                    return s.UnlockedEdges.Contains(e.Id);
            }
        }

        /// <summary>
        /// 边是否已被玩家知道：已开通，或它可以出发的一端已实际到访且探索 100%（“100% 揭表列外出边”）。
        /// 未开通但已揭示的边显示为“未开通 + 原因”，未揭示的不泄露。
        /// </summary>
        public bool EdgeRevealed(GameState s, EdgeConfig e)
        {
            if (EdgeOpen(s, e)) return true;
            // 地牢换代后旧代已知的路线仍算已揭示（只退回开通状态，B12 试玩修复）
            if (e.DungeonId != null && s.DungeonInstances.TryGetValue(e.DungeonId, out var inst) && inst.KnownEdges.Contains(e.Id)) return true;
            return RevealedFrom(s, e, e.A) || RevealedFrom(s, e, e.B);
        }

        private bool RevealedFrom(GameState s, EdgeConfig e, string end) =>
            e.TraversableFrom(end) && s.Locations.TryGetValue(end, out var ls) && ls.Visited && ls.Progress >= 100;

        /// <summary>从当前地点出发、已揭示但门槛未满足的边与原因（地块栏“未开通”卡、区域地图）。</summary>
        public List<(EdgeConfig Edge, string Target, string Reason)> AdjacentLocked(GameState s)
        {
            var here = s.CurrentLocation;
            return EdgesAt(here)
                .Where(e => e.TraversableFrom(here) && !EdgeOpen(s, e) && EdgeRevealed(s, e))
                .Select(e => (e, e.Other(here), GateBlockedReason(s, e)))
                .ToList();
        }

        /// <summary>
        /// 相邻栏用：未开通路线按原因分组（B12 B11 遗留：同一原因 ≥3 条收成一张“N 条路线未开通”叠卡，原因写一次）。
        /// 组内按配置顺序；组的顺序按首条出现顺序。
        /// </summary>
        public List<(string Reason, List<(EdgeConfig Edge, string Target)> Routes)> AdjacentLockedGroups(GameState s) =>
            AdjacentLocked(s).GroupBy(x => x.Reason).Select(g => (g.Key, g.Select(x => (x.Edge, x.Target)).ToList())).ToList();

        // ───────────────────────── 谓词求值 ─────────────────────────

        /// <summary>门槛谓词当前是否满足（不改状态、不锁存）。无门槛的旧式边按 unlockAny（任一地点探索度≥X）。</summary>
        public bool EvaluateGate(GameState s, EdgeConfig e)
        {
            var g = GateOf(e);
            if (g == null)
            {
                if (!string.IsNullOrEmpty(e.Gate)) throw new InvalidOperationException($"连接 {e.Id} 的门槛 {e.Gate} 未定义");
                return e.InitiallyUnlocked || e.UnlockAny.Any(u => s.Locations.TryGetValue(u.Location, out var l) && l.Progress >= u.Progress);
            }
            return Eval(s, e, g.Predicate);
        }

        private bool Eval(GameState s, EdgeConfig e, GatePredicate p)
        {
            switch (p.Op)
            {
                case "literal": return p.Value != null && p.Value.Type == JTokenType.Boolean && p.Value.Value<bool>();
                case "and": return p.Args.All(a => Eval(s, e, a));
                case "or": return p.Args.Any(a => Eval(s, e, a));
                case "fact": return MapFact(s, p.Key!);
                case "visited": return s.Locations.TryGetValue(BindNode(e, p.Node), out var v) && v.Visited;
                case "exploreAtLeast": return ExploreOf(s, BindNode(e, p.Node), p.Scope) >= Threshold(p);
                case "instanceFact": return InstanceFact(s, BindDungeon(e, p.Dungeon), p.Key!);
                case "contextEquals": return ContextValue(e, p.Key!) == p.Value?.Value<string>();
                case "if": return Eval(s, e, p.Condition!) ? Eval(s, e, p.Then!) : Eval(s, e, p.Else!);
                default: throw new InvalidOperationException($"门槛运算 {p.Op} 未知（连接 {e.Id}）");
            }
        }

        private static int Threshold(GatePredicate p) => p.Value?.Value<int>() ?? throw new InvalidOperationException("exploreAtLeast 缺少阈值");

        private static string BindNode(EdgeConfig e, string? node) => node switch
        {
            null => throw new InvalidOperationException($"连接 {e.Id} 的门槛缺少节点"),
            "$authoredFrom" => e.A,
            "$dungeonParent" => e.DungeonParent ?? throw new InvalidOperationException($"连接 {e.Id} 没有 dungeonParent"),
            _ when node.StartsWith("$") => throw new InvalidOperationException($"连接 {e.Id} 的门槛变量 {node} 未绑定"),
            _ => node,
        };

        private static string BindDungeon(EdgeConfig e, string? d) => d switch
        {
            "$dungeonId" => e.DungeonId ?? throw new InvalidOperationException($"连接 {e.Id} 没有 dungeonId"),
            null => throw new InvalidOperationException($"连接 {e.Id} 的门槛缺少地牢"),
            _ when d.StartsWith("$") => throw new InvalidOperationException($"连接 {e.Id} 的门槛变量 {d} 未绑定"),
            _ => d,
        };

        private static string? ContextValue(EdgeConfig e, string key) => key switch
        {
            "edgeRole" => e.Role,
            "authoredFrom" => e.A,
            "authoredTo" => e.B,
            "dungeonId" => e.DungeonId,
            "dungeonParent" => e.DungeonParent,
            _ => throw new InvalidOperationException($"门槛上下文 {key} 未知（连接 {e.Id}）"),
        };

        /// <summary>
        /// 探索度：campaign 读世界持久探索度；current_instance 读当前地牢实例的房间探索度（B11 只有第 1 代，房间探索度存在地点状态里，
        /// 换代清零在 B21/B22，开发假设 B11-5）。缺失为 0。
        /// </summary>
        public int ExploreOf(GameState s, string node, string? scope)
        {
            if (!s.Locations.TryGetValue(node, out var ls)) return 0;
            if (scope == GateScopes.CurrentInstance)
            {
                var d = _locs.TryGetValue(node, out var lc) ? lc.DungeonId : null;
                if (d == null || !s.DungeonInstances.ContainsKey(d)) return 0;
            }
            return ls.Progress;
        }

        /// <summary>campaign 门槛事实：首战胜利/旧档等效读剧情状态，其余读 s.Progress.Flags（缺失为未满足）。</summary>
        public bool MapFact(GameState s, string key) => key switch
        {
            "tutorialBattleWon" => s.Story.TutorialWon,
            "legacyTutorialEquivalent" => s.Story.Legacy,
            _ => s.Progress.Flags.Contains(key),
        };

        /// <summary>当前地牢实例的实例事实（guardianDefeated / recordClaimedAndClear）。没有实例为未满足。</summary>
        public bool InstanceFact(GameState s, string dungeonId, string key) =>
            s.DungeonInstances.TryGetValue(dungeonId, out var inst) && inst.Facts.Contains(key);

        /// <summary>地牢当前实例；没有就建第 1 代并把本地牢普通箱记到这一代（实际到达地牢房间或锁存实例边时建；换代见 NewDungeonGeneration，B12）。</summary>
        private DungeonInstanceState EnsureInstance(GameState s, string dungeonId)
        {
            if (!s.DungeonInstances.TryGetValue(dungeonId, out var inst))
            {
                s.DungeonInstances[dungeonId] = inst = new DungeonInstanceState { Generation = 1 };
                BindInstanceResources(s, dungeonId, 1);
            }
            return inst;
        }

        // ───────────────────────── 未满足原因 ─────────────────────────

        /// <summary>门槛未满足时给玩家看的原因：“需要：……”；已满足为空串。</summary>
        public string GateBlockedReason(GameState s, EdgeConfig e)
        {
            if (EvaluateGate(s, e)) return "";
            var g = GateOf(e);
            List<string> parts;
            if (g == null) parts = e.UnlockAny.Select(u => ExploreNeed(s, u.Location, u.Progress)).ToList();
            else parts = Unmet(s, e, g.Predicate);
            parts = parts.Distinct().ToList();
            return parts.Count == 0 ? "暂不开放" : "需要：" + string.Join("；", parts);
        }

        private string ExploreNeed(GameState s, string node, int v) =>
            $"把{LocName(node)}探索到{v}%（现在{(s.Locations.TryGetValue(node, out var l) ? l.Progress : 0)}%）";

        private string FactLabel(string key) => Config.Map.FactLabels.TryGetValue(key, out var t) ? t : key;

        private List<string> Unmet(GameState s, EdgeConfig e, GatePredicate p)
        {
            var list = new List<string>();
            if (Eval(s, e, p)) return list;
            switch (p.Op)
            {
                case "and":
                    foreach (var a in p.Args) list.AddRange(Unmet(s, e, a));
                    break;
                case "or":
                {
                    var alts = p.Args.Select(a => string.Join("、", Unmet(s, e, a).Distinct())).Where(x => x.Length > 0).Distinct().ToList();
                    list.Add(alts.Count == 1 ? alts[0] : "以下任一：" + string.Join("／", alts));
                    break;
                }
                case "fact": list.Add(FactLabel(p.Key!)); break;
                case "instanceFact": list.Add(FactLabel(p.Key!)); break;
                case "visited": list.Add($"到访过{LocName(BindNode(e, p.Node))}"); break;
                case "exploreAtLeast":
                {
                    var node = BindNode(e, p.Node);
                    list.Add(p.Scope == GateScopes.CurrentInstance
                        ? $"在本次地牢里把{LocName(node)}探索到{Threshold(p)}%（现在{ExploreOf(s, node, p.Scope)}%）"
                        : ExploreNeed(s, node, Threshold(p)));
                    break;
                }
                case "if":
                    list.AddRange(Eval(s, e, p.Condition!) ? Unmet(s, e, p.Then!) : Unmet(s, e, p.Else!));
                    break;
                default:
                    list.Add("暂不开放");
                    break;
            }
            return list;
        }

        // ───────────────────────── 重判与锁存 ─────────────────────────

        /// <summary>
        /// 重判全部门槛（Q115 选 B、Q116 选 A；gate-contract stateRules.campaignLatch/discoveryVsVisit、map-adjudications gateOpening/reveal100）：
        /// 谓词满足的边立即锁存（campaign 永久 / 当前实例），并把该边两端记为已发现——不要求任一端已到访；只“发现”，不写 visited、不改探索度、
        /// 不写接触/登记事实、不发奖励。current_instance 边只在该地牢已有当前实例时锁存（远方没有实例的地牢不凭发现创建）；单向出口不锁存、每次重验。
        /// 扫描全部边并反复处理到没有新可开的边为止。随后：探索 100% 的已到访地点揭示其可出发的全部相连边（双向边不分数据 from/to，单向只算 from），
        /// 未开通的远端记为已发现（只显示名称与“未开通”原因）。返回本次新开通与新揭示（未开通）的边。幂等。
        /// </summary>
        public (List<EdgeConfig> Unlocked, List<EdgeConfig> Revealed) RefreshGates(GameState s)
        {
            var unlocked = new List<EdgeConfig>();
            var revealed = new List<EdgeConfig>();
            for (var changed = true; changed;)
            {
                changed = false;
                foreach (var e in Config.Edges)
                {
                    var scope = GateScope(e);
                    if (scope == GateScopes.RevalidateCurrentInstance) continue;   // 单向出口不锁存
                    if (EdgeOpen(s, e)) continue;
                    if (scope == GateScopes.CurrentInstance && (e.DungeonId == null || !s.DungeonInstances.ContainsKey(e.DungeonId))) continue;
                    if (!EvaluateGate(s, e)) continue;
                    if (scope == GateScopes.CurrentInstance) s.DungeonInstances[e.DungeonId!].UnlockedEdges.Add(e.Id);
                    else s.UnlockedEdges.Add(e.Id);
                    Discover(s, e.A);
                    Discover(s, e.B);
                    unlocked.Add(e);
                    changed = true;
                }
            }
            foreach (var e in Config.Edges)
            {
                if (EdgeOpen(s, e)) continue;
                var any = false;
                foreach (var end in new[] { e.A, e.B })
                {
                    if (!RevealedFrom(s, e, end)) continue;
                    var other = e.Other(end);
                    if (s.Locations.TryGetValue(other, out var ol) && !ol.Discovered) { ol.Discovered = true; any = true; }
                }
                if (any) revealed.Add(e);
            }
            return (unlocked, revealed);
        }

        private static bool Visited(GameState s, string id) => s.Locations.TryGetValue(id, out var l) && l.Visited;

        private static void Discover(GameState s, string id)
        {
            if (s.Locations.TryGetValue(id, out var l)) l.Discovered = true;
        }

        /// <summary>
        /// 重判结果写成日志短语：“通往X的路线”“通往Y的路线（未开通，需要……）”；同一原因的多条未开通路线合成一句
        /// “通往A、B、C的路线（未开通，需要……）”，原因只写一次（B12 B11 遗留：营区广场 9 条同因路线）。from 为空时写两端。
        /// </summary>
        private List<string> RouteNotes(GameState s, (List<EdgeConfig> Unlocked, List<EdgeConfig> Revealed) r, string? from)
        {
            string Far(EdgeConfig e) => from != null && e.Touches(from) ? LocName(e.Other(from)) : $"{LocName(e.A)}—{LocName(e.B)}";
            var list = r.Unlocked.Select(e => $"通往{Far(e)}的路线").ToList();
            foreach (var g in r.Revealed.GroupBy(e => GateBlockedReason(s, e)))
                list.Add($"通往{string.Join("、", g.Select(Far))}的路线（未开通，{g.Key}）");
            return list;
        }

        /// <summary>
        /// 实际到达成功后的地图事务（S03「首次到访」、gate-contract publicVisitPolicy/factProducers）：地牢房间建当前实例；
        /// 公共城市/服务节点首次到访探索度置 100（不发里程碑物资、不随机接敌）；c02/r02 提交接触事实；随后重判门槛。返回给日志的短语。
        /// </summary>
        private List<string> ArriveMap(GameState s, string locationId)
        {
            var notes = new List<string>();
            var lc = _locs[locationId];
            var ls = s.Locations[locationId];
            if (lc.DungeonId != null) EnsureInstance(s, lc.DungeonId).VisitedRooms.Add(locationId);   // B21：本代到过的房间（返程路径用）
            if (lc.ExploreAuto100OnVisit && ls.Progress < 100)
            {
                ls.Progress = 100;
                foreach (var t in new[] { 25, 50, 75, 100 }) ls.TriggeredMilestones.Add(t);
                notes.Add($"{lc.Name}是公开地点，到达即已全部知晓");
            }
            if (Config.Map.ArrivalFacts.TryGetValue(locationId, out var facts))
                foreach (var f in facts)
                    if (s.Progress.Flags.Add(f)) notes.Add($"已与{lc.Name}建立公开接触");
            ArriveCity(s, locationId, notes);   // B14：持登记授权实际走到营区广场才完成营区登记
            RevealFixedBattle(s, locationId, notes);   // B20：固定战首次揭示（按当时容量 C 截名单并锁存）
            PopulateOnArrival(s, locationId, notes);   // B20：人口区节点首次到达生成本地敌人，记该区已进入
            var r = RefreshGates(s);
            var routes = RouteNotes(s, r, locationId);
            if (routes.Count > 0) notes.Add("发现" + string.Join("与", routes));
            return notes;
        }

        /// <summary>危险度说明（Q31 contracts.danger：0—3 只作预告，不乘接敌率、伤害或敌属性）。</summary>
        public static string DangerText(int danger) => $"危险等级{danger}（0—3，只作提示，不改变遇敌概率与敌人强度）";

        /// <summary>
        /// 开发入口（?dev=1）与测试：记下一条门槛事实并重判。返回说明。门槛事实是 campaign 锁存（已开路线不回退），
        /// 记下后不能从这里清除（B12 B11 遗留：原 ✓ 按钮像能切换，清掉事实路线却不关，容易误会）。
        /// </summary>
        public string DebugMapFact(GameState s, string key, bool on = true)
        {
            if (!on) return $"开发：地图事实 {key} 是 campaign 锁存，记下后不能清除（已开路线不回退）。";
            if (s.Progress.Flags.Contains(key)) return $"开发：地图事实 {key}（{FactLabel(key)}）已记下。";
            s.Progress.Flags.Add(key);
            SettleFixedByFacts(s);   // B20：记下“检查哨路障已解决”时检查哨固定战视为已结清
            var r = RefreshGates(s);
            var routes = RouteNotes(s, r, s.CurrentLocation);
            UpdateFlags(s);
            return $"开发：已{(on ? "记下" : "清除")}地图事实 {key}（{FactLabel(key)}）" + (routes.Count > 0 ? "；" + string.Join("、", routes) : "") + "。";
        }

        /// <summary>开发入口（?dev=1）：直接传送到任一地点（按实际到达处理：记到访、公开地点自动 100、接触事实、门槛重判）。忙碌时拒绝。</summary>
        public string DebugMapTeleport(GameState s, string locationId)
        {
            if (!_locs.ContainsKey(locationId)) return "开发：地点不存在";
            if (IsBusy(s)) return "开发：行动进行中或有敌踪/战斗，先结束再传送";
            var notes = new List<string>();
            var from = s.CurrentLocation;
            s.LastMove = null;   // B22h：开发传送不是已完成的移动，之前的移动记录不再可信（撤退无来处）
            s.CurrentLocation = locationId;
            s.CurrentZone = ExteriorZone;
            Visit(s, locationId, notes);
            if (DevExpeditionAfterTeleport(s, locationId) is string expNote) notes.Add(expNote);   // B21-14
            NoteArrive(s, from, locationId);   // B13 试玩修复 P7：与真实到达同一序章钩子（往返、到达反应、出区抵达）
            UpdateFlags(s);
            return $"开发：已传送到{LocName(locationId)}" + (notes.Count > 0 ? "；" + string.Join("；", notes) : "") + "。";
        }

        /// <summary>开发入口（?dev=1）：当前地点探索度直接到 100%（结算里程碑与路线揭示，不发探索物资）。</summary>
        public string DebugExploreHere100(GameState s)
        {
            var here = s.CurrentLocation;
            if (!_locs[here].ExploreEnabled) return $"开发：{LocName(here)}不需要探索";
            DebugSetProgress(s, here, 100);   // 内含里程碑、资源点揭示与门槛重判
            var locked = AdjacentLocked(s).Select(x => $"{LocName(x.Target)}（{x.Reason}）").ToList();
            return $"开发：{LocName(here)}探索度已到100%" + (locked.Count > 0 ? "；未开通：" + string.Join("、", locked) : "") + "。";
        }

        /// <summary>开发入口与测试：给当前（或指定）地牢实例记下实例事实（guardianDefeated / recordClaimedAndClear）并重判。</summary>
        public string DebugInstanceFact(GameState s, string dungeonId, string key)
        {
            if (key == RecordClaimedFact) MarkCleared(s, dungeonId);   // B22：开发记“核心已领取”同样写本代 clearedAt（不发首通包）
            else EnsureInstance(s, dungeonId).Facts.Add(key);
            SettleFixedByFacts(s);   // B20：记下“本地牢守卫已击败”时本代守卫固定战视为已结清
            var r = RefreshGates(s);
            var routes = RouteNotes(s, r, s.CurrentLocation);
            var shown = RevealByInstanceFacts(s);   // B13c（Q122）：守卫战利品箱当场出现
            return $"开发：{dungeonId} 当前实例已记下 {key}（{FactLabel(key)}）" + (routes.Count > 0 ? "；" + string.Join("、", routes) : "")
                + (shown.Count > 0 ? "；" + string.Join("、", shown) + "已出现，可当场领取" : "") + "。";
        }
    }
}

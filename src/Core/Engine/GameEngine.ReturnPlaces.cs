using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B54（S08 文首「2026-11-15 B51a Q787—Q790」「B51b Q791—Q792」；逐字稿同两节；辅助合同 q787ToQ790、q791ToQ792）：
    /// 撤退离地保留按来源返还时恢复 C＋R 后的五根卡容量与溢出落地（Q787）、多去向恢复分句标真实去向（Q789）、
    /// 建造完工地点失效与陶窑／净水台缺料失败的短恢复分句（Q788、Q791）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>撤退离地保留按来源返还的一份实际（或只读预告）去向：原物实例、物品、最终 owner、份数、是否随身放不下而落原工作地面。</summary>
        internal readonly struct KeptReturnPart
        {
            public KeptReturnPart(int instanceId, string itemId, string dest, int count, bool overflow)
            {
                InstanceId = instanceId; ItemId = itemId; Dest = dest; Count = count; Overflow = overflow;
            }
            public int InstanceId { get; }
            public string ItemId { get; }
            public string Dest { get; }
            public int Count { get; }
            public bool Overflow { get; }
        }

        /// <summary>ReleaseToSources 本次实际按来源返还的去向（结果句读它；为 null 时不收集）。</summary>
        private List<KeptReturnPart>? _keptReturnSink;

        /// <summary>某 owner 是不是某角色的随身（主角 carried，队友 carried:&lt;id&gt;）；是则给出角色 ID。</summary>
        private static bool TryCarriedActor(string owner, out string actorId)
        {
            actorId = "";
            if (owner == ItemInstance.Carried) { actorId = GameState.PlayerId; return true; }
            if (!owner.StartsWith(ItemInstance.Carried + ":")) return false;
            actorId = owner.Substring(ItemInstance.Carried.Length + 1);
            return true;
        }

        /// <summary>原工作地点／区域的地面 owner（Q787 溢出落点：不是撤退落点或队伍当前地面）。</summary>
        private static string WorkGround(ActionState a) => ZonePlace(a.SourceLocation, a.StartZone ?? ExteriorZone);

        /// <summary>
        /// 撤退离地保留工作按开工来源返还的计划（Q466／Q471 原去向＋Q787 五根卡判定；只读，预告与提交同源）：
        /// 原地面部分回原来源地面；原随身部分（仍归本行动预留、原有随身返还资格）先从旧占位排除，再按恢复后真实截止 C＋R 与原完整合堆键、原单堆上限，
        /// 沿原预留清单顺序逐份预演该角色随身最终根卡，能放下的最大份额回原随身，超出份额留原工作地点／区域地面。
        /// 上限为 5 张（旧档本来已超的不强制扫地、也不得再增加，与 <see cref="RootCardsAllow"/> 同口径）；不看重量、不动其他随身物。
        /// </summary>
        internal List<KeptReturnPart> KeptReturnPlan(GameState s, ActionState a)
        {
            var parts = new List<KeptReturnPart>();
            var entries = new List<(ItemInstance inst, string src)>();
            foreach (var id in a.ReservedItemIds)
            {
                var inst = FindItem(s, id);
                if (inst == null) continue;
                var src = a.ReserveSources != null && a.ReserveSources.TryGetValue(id, out var o) ? o : inst.Owner;
                entries.Add((inst, src));
            }
            var candidates = new HashSet<int>(entries
                .Where(x => !TryPlace(x.src, out _, out _) && x.inst.ReservedBy == a.ActionId && TryCarriedActor(x.inst.Owner, out _))
                .Select(x => x.inst.InstanceId));
            var projections = new Dictionary<string, CardProjection>();
            var ground = WorkGround(a);
            foreach (var (inst, src) in entries)
            {
                if (TryPlace(src, out _, out _)) { parts.Add(new KeptReturnPart(inst.InstanceId, inst.ItemId, src, inst.Count, false)); continue; }
                var owner = inst.Owner;
                if (!candidates.Contains(inst.InstanceId)) { parts.Add(new KeptReturnPart(inst.InstanceId, inst.ItemId, owner, inst.Count, false)); continue; }
                if (!projections.TryGetValue(owner, out var pj)) projections[owner] = pj = NewProjection(s, owner, candidates);
                var expiry = inst.ShelfPausedMinutes is int r ? s.GameMinute + r : inst.ExpiresAt;
                var keep = pj.Fit(this, inst, (inst.ItemId, expiry, MergeTag(inst)));
                if (keep > 0) parts.Add(new KeptReturnPart(inst.InstanceId, inst.ItemId, owner, keep, false));
                if (inst.Count - keep > 0) parts.Add(new KeptReturnPart(inst.InstanceId, inst.ItemId, ground, inst.Count - keep, true));
            }
            return parts;
        }

        /// <summary>某角色随身去掉本次候选后的根卡底数与上限（Q787 预演用）。</summary>
        private CardProjection NewProjection(GameState s, string owner, HashSet<int> exclude)
        {
            TryCarriedActor(owner, out var actor);
            var pj = new CardProjection { Limit = Math.Max(MaxRootCards, RootCardCountOf(s, actor)) };
            foreach (var i in s.Items.Where(i => i.Owner == owner && !exclude.Contains(i.InstanceId)))
            {
                if (StacksAsBatch(i)) pj.Add(StackKey(i), i.Count, StackMaxOf(i));
                else pj.Singles++;
            }
            return pj;
        }

        /// <summary>只读根卡预演：按合堆键累计份数与单堆上限，整件物品各算 1 张。</summary>
        private sealed class CardProjection
        {
            public Dictionary<(string, int?, string?), (int n, int cap)> Stacks { get; } = new Dictionary<(string, int?, string?), (int n, int cap)>();
            public int Singles { get; set; }
            public int Limit { get; set; }

            public void Add((string, int?, string?) key, int n, int cap)
            {
                var had = Stacks.TryGetValue(key, out var c) ? c.n : 0;
                Stacks[key] = (had + n, cap);
            }

            private int CardsWith((string, int?, string?) key, int n, int cap)
            {
                var cards = Singles;
                var seen = false;
                foreach (var kv in Stacks)
                {
                    var total = kv.Value.n + (kv.Key.Equals(key) ? n : 0);
                    if (kv.Key.Equals(key)) seen = true;
                    cards += (total + Math.Max(1, kv.Value.cap) - 1) / Math.Max(1, kv.Value.cap);
                }
                if (!seen && n > 0) cards += (n + Math.Max(1, cap) - 1) / Math.Max(1, cap);
                return cards;
            }

            /// <summary>这份候选最多能合法放回几份（份数越多卡数不减，二分），并把放回的份额记进预演。</summary>
            public int Fit(GameEngine e, ItemInstance inst, (string, int?, string?) key)
            {
                if (!e.StacksAsBatch(inst))
                {
                    if (CardsWith(key, 0, 1) + 1 > Limit) return 0;
                    Singles++;
                    return inst.Count;
                }
                var cap = e.StackMaxOf(inst);
                var lo = 0;
                var hi = inst.Count;
                while (lo < hi)
                {
                    var mid = (lo + hi + 1) / 2;
                    if (CardsWith(key, mid, cap) <= Limit) lo = mid; else hi = mid - 1;
                }
                if (lo > 0) Add(key, lo, cap);
                return lo;
            }
        }

        /// <summary>
        /// Q787 有实际（或预计）溢出时替换原 Q471 返还段（逐字稿 B51a 表 actual／predicted／overflow 系列）：按最终去向、物名分组合计份数，
        /// 沿原清单首次出现顺序；原合法其他物主随身在「随身」前加本人公开名。没有溢出时调用方沿原 Q471 返还句。
        /// </summary>
        private string KeptPlanClause(GameState s, IReadOnlyList<KeptReturnPart> parts, bool result)
        {
            var groups = new List<(int kind, string dest, string name, int n)>();
            foreach (var p in parts)
            {
                var kind = p.Overflow ? 2 : TryPlace(p.Dest, out _, out _) ? 1 : 0;
                var name = ItemName(p.ItemId);
                var k = groups.FindIndex(g => g.kind == kind && g.dest == p.Dest && g.name == name);
                if (k < 0) groups.Add((kind, p.Dest, name, p.Count));
                else groups[k] = (kind, p.Dest, name, groups[k].n + p.Count);
            }
            return string.Concat(groups.Select(g => g.kind switch
            {
                2 => result ? $"随身已满，{g.name}×{g.n}留在{ZoneName(s, g.dest)}地面。" : $"随身放不下，{g.name}×{g.n}将留在{ZoneName(s, g.dest)}地面。",
                1 => result ? $"{g.name}×{g.n}已退回{ZoneName(s, g.dest)}地面。" : $"{g.name}×{g.n}将退回{ZoneName(s, g.dest)}地面。",
                _ => result ? $"{g.name}×{g.n}已退回{CarryLabel(s, g.dest)}。" : $"{g.name}×{g.n}将退回{CarryLabel(s, g.dest)}。",
            }));
        }

        /// <summary>随身去向的称呼：主角「随身」，原合法其他物主「{人物名}随身」。</summary>
        private string CarryLabel(GameState s, string owner) =>
            TryCarriedActor(owner, out var actor) && actor != GameState.PlayerId ? $"{ActorDisplayName(s, actor)}随身" : "随身";

        /// <summary>
        /// 建造完工时地点失效（逐字稿 buildSiteFailure，Q788）：「{配方名}未完成：{原因}。材料已退回{地点}地面。无经验。」；
        /// {地点}为原失败规则实际返还的当前地面，原因句末已有标点不重复追加。BM11（S08 Q882 用户新裁定）：撤原恢复保质分句。
        /// </summary>
        internal string BuildSiteFailureText(GameState s, string recipeName, string reason)
        {
            var why = reason.EndsWith("。") || reason.EndsWith("！") || reason.EndsWith("？") ? reason : reason + "。";
            return $"{recipeName}未完成：{why}材料已退回{ZoneName(s, s.Ground)}地面。无经验。";
        }
    }
}

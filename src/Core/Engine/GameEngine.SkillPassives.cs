using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 求生被动（A06 §2 制造/采集/搜集/烹饪/行旅各 1/3/5/7/10 级共 25 个；开发计划 v0.9 B5 H7）。
    /// 角色等级达到节点等级即生效；同一效果键只取最强的一个（A06“替代3级/替代5级”），不叠乘。
    /// 求生工作量与装备相乘后下限 ×0.70、负小时率与装备相乘后每项下限 ×0.75（与 B4 装备合成同一口径，R16）；
    /// 只作用非战斗指定活动，不减少招式 E 成本（R01 §3）。只有 Status=live 的节点进入计算，其余只显示。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>某角色已获得（等级已到）的求生被动节点，按门类与等级排序；liveOnly 时只取已接入的。</summary>
        public List<SkillNodeConfig> SurvivalPassives(ActorState actor, bool liveOnly = false) =>
            Config.SkillNodes.Where(n => n.IsSurvival && SkillLevel(actor, n.SkillId) >= n.Level && (!liveOnly || n.IsLive)).ToList();

        private IEnumerable<(SkillNodeConfig node, EquipEffectConfig fx)> PassiveEffects(ActorState actor, string key) =>
            SurvivalPassives(actor, true).SelectMany(n => n.Effects.Where(e => e.Key == key).Select(e => (n, e)));

        /// <summary>乘数类被动：同键取最强（最小）的一个；没有为 1。</summary>
        public double PassiveFactor(ActorState actor, string key) =>
            PassiveEffects(actor, key).Where(x => x.fx.Op == "mul").Select(x => x.fx.Value).DefaultIfEmpty(1).Min();

        /// <summary>加值类被动：同键取最大的一个；没有为 0。</summary>
        public double PassiveAdd(ActorState actor, string key) =>
            PassiveEffects(actor, key).Where(x => x.fx.Op == "add").Select(x => x.fx.Value).DefaultIfEmpty(0).Max();

        public double? PassiveSet(ActorState actor, string key)
        {
            var v = PassiveEffects(actor, key).Where(x => x.fx.Op == "set").Select(x => x.fx.Value).ToList();
            return v.Count == 0 ? (double?)null : v.Max();
        }

        /// <summary>提供该键最强效果的节点名（界面写“熟练：熟练采伐×0.90”）。</summary>
        private string? PassiveSource(ActorState actor, string key, bool min = true)
        {
            var list = PassiveEffects(actor, key).ToList();
            if (list.Count == 0) return null;
            var best = min ? list.OrderBy(x => x.fx.Value).First() : list.OrderByDescending(x => x.fx.Value).First();
            return $"{SkillName(best.node.SkillId)}{best.node.Level}级·{best.node.Name}";
        }

        /// <summary>
        /// 该行动的被动工作量倍率与来源：采集（非箱子）熟练采伐×0.9；移动在走过的物理边上远途规划×0.90，否则≤20kg 轻装步伐×0.95（10 级替代 5 级）。
        /// targetId：移动为目的地，采集为资源点。
        /// </summary>
        public (double factor, string? source) PassiveWorkFactor(GameState s, ActorState actor, ActionType type, string? targetId)
        {
            switch (type)
            {
                case ActionType.Gather:
                    if (targetId != null && _res.TryGetValue(targetId, out var res))
                    {
                        // 采集被动只管练采集的资源点（熟练采伐），搜集被动只管练搜集的（分类勘整/周密检索，Q54/Q55）；遗迹勘整不吃
                        var key = GatherSkillOf(res) == Gathering ? "work.gather" : "work.scavenge";
                        var f = PassiveFactor(actor, key);
                        return (f, f < 1 ? PassiveSource(actor, key) : null);
                    }
                    break;
                case ActionType.Travel:
                {
                    if (targetId == null) break;
                    var edge = Config.Edges.FirstOrDefault(e => e.Touches(s.CurrentLocation) && e.Other(s.CurrentLocation) == targetId);
                    var visited = PassiveFactor(actor, "work.moveVisited");
                    if (edge != null && visited < 1 && s.TraveledEdges.Contains(edge.Id))
                        return (visited, PassiveSource(actor, "work.moveVisited") + "（走过的路）");
                    var light = PassiveFactor(actor, "work.moveLight");
                    if (light < 1 && CarriedGramsOf(s, actor.Id) <= Config.SkillPassiveRules.MoveLightMaxGrams)
                        return (light, PassiveSource(actor, "work.moveLight") + $"（随身≤{Config.SkillPassiveRules.MoveLightMaxGrams / 1000}公斤）");
                    break;
                }
            }
            return (1, null);
        }

        /// <summary>采集划伤概率的被动倍率：练采集的资源点稳手采集×0.9，练搜集的（危险搜集）危险辨识×0.8。</summary>
        public double PassiveHazardFactor(ActorState actor, ResourceConfig res) =>
            PassiveFactor(actor, GatherSkillOf(res) == Scavenging ? "hazard.scavengeChance" : "hazard.gatherChance");

        /// <summary>
        /// 定向采集的被动加产（只对按库存的资源点，仍受库存限制）：合理取材（木/石/纤维）+1；精取（该资源点今日第一次采集，非工业材料）+1。
        /// notes 写出来源。
        /// </summary>
        public int PassiveGatherBonus(GameState s, ActorState actor, ResourceConfig res, string itemId, List<string>? notes = null)
        {
            if (!res.IsUnits || GatherSkillOf(res) != Gathering) return 0;   // 采集加产只管自然采出的资源点（Q55）
            var bonus = 0;
            var rules = Config.SkillPassiveRules;
            var basic = (int)PassiveAdd(actor, "yield.gatherBasic");
            if (basic > 0 && rules.GatherBasicItems.Contains(itemId))
            {
                bonus += basic;
                notes?.Add($"{PassiveSource(actor, "yield.gatherBasic", false)}：产量+{basic}（受库存限制）");
            }
            var first = (int)PassiveAdd(actor, "yield.gatherFirstDaily");
            if (first > 0 && !rules.IndustrialItems.Contains(itemId) && DailyCount(s, actor, DailyGather, res.NodeId) == 0)
            {
                bonus += first;
                notes?.Add($"{PassiveSource(actor, "yield.gatherFirstDaily", false)}：此处今日首次采集额外+{first}（仍扣库存）");
            }
            return bonus;
        }

        /// <summary>精细加工（制造5级）：维护恢复在配方比例上 +5 个百分点，最高 30%。没有该被动返回配方比例。</summary>
        public double PassiveRepairFraction(ActorState actor, double baseFraction)
        {
            var bonus = PassiveAdd(actor, "repair.fractionBonus");
            if (bonus <= 0) return baseFraction;
            var cap = PassiveSet(actor, "repair.fractionCap") ?? double.MaxValue;
            return Math.Min(cap, baseFraction + bonus);
        }

        /// <summary>
        /// 回收专家（Q54：由搜集10级迁到制造10级，常驻被动、不是节点）：拆解后尝试追加同材料的份数上限（Q46：可自制件受 80% 材料价值预算，见 SalvageExpertBonus）。
        /// </summary>
        public int SalvageExpertLevel(GameState s)
        {
            var d = Config.Progression.Dismantle;
            return d.ExpertCraftingLevel > 0 && SkillLevel(s, Crafting) >= d.ExpertCraftingLevel ? d.ExpertBonus : 0;
        }

        /// <summary>回收专家的来源说明：“制造10级·回收专家”。</summary>
        public string SalvageExpertSource => $"{SkillName(Crafting)}{Config.Progression.Dismantle.ExpertCraftingLevel}级·回收专家";

        /// <summary>人物页“已获得的求生被动”一行一个：“采集3级·稳手采集：采集划伤概率×0.9（生效中）”。</summary>
        public List<string> SurvivalPassiveLines(ActorState actor) =>
            SurvivalPassives(actor).Select(n => $"{SkillName(n.SkillId)}{n.Level}级·{n.Name}：{n.Text}（{PassiveStatus(actor, n)}）").ToList();

        /// <summary>
        /// 制造的常驻被动（Q54：拆解许可按制造 1/3/7/10 级开放 T0—T3、10 级回收专家；不是第六个节点），人物页逐条写。
        /// </summary>
        public List<string> CraftingStandingLines(ActorState actor)
        {
            var lv = SkillLevel(actor, Crafting);
            var d = Config.Progression.Dismantle;
            var lines = new List<string>();
            var stages = string.Join("、", d.Stages.OrderBy(x => x.CraftingLevel).Select(x => $"{x.CraftingLevel}级{x.MaxTier}阶"));
            lines.Add(SalvageMaxTier(lv) is int t
                ? $"{SkillName(Crafting)}常驻·拆解许可：可拆解0—{t}阶装备，每件完成制造经验+{d.Xp}（{stages}）"
                : $"{SkillName(Crafting)}常驻·拆解许可：尚未开放（{stages}）");
            if (d.ExpertCraftingLevel > 0)
                lines.Add($"{SkillName(Crafting)}{d.ExpertCraftingLevel}级常驻·回收专家：拆解后尝试追加1份同种回收材料，可自制装备仍不超过标准配方材料价值80%（{(lv >= d.ExpertCraftingLevel ? "生效中" : "未解锁")}）");
            return lines;
        }

        /// <summary>
        /// 已生效的被动是否被同门更高级的节点替代（同一效果键全部由更高级的已获得节点提供，如节奏行进被耐力行进替代）。返回替代它的等级，没有为 null。
        /// </summary>
        public int? SupersededBy(ActorState actor, SkillNodeConfig n)
        {
            if (!n.IsLive || n.Effects.Count == 0) return null;
            var higher = SurvivalPassives(actor, true).Where(x => x.SkillId == n.SkillId && x.Level > n.Level).OrderBy(x => x.Level).ToList();
            var by = higher.FirstOrDefault(x => n.Effects.All(e => x.Effects.Any(h => h.Key == e.Key)));
            return by?.Level;
        }

        /// <summary>某角色视角的被动状态：被替代的写“已被7级替代”，其余同 PassiveStatusText。</summary>
        public string PassiveStatus(ActorState actor, SkillNodeConfig n) =>
            SupersededBy(actor, n) is int lv ? $"已被{lv}级替代" : PassiveStatusText(n);

        public static string PassiveStatusText(SkillNodeConfig n) => n.Status switch
        {
            "live" => "生效中",
            "baseline" => "原有内容，已开放",
            "pending" => "部分内容待策划确认，暂未生效",
            "dormant" => "相关配方/内容尚未上线，暂未生效",
            "closed" => "暂未开放：不加成、不磨损",
            _ => "四对四战斗上线后生效",
        };
    }
}

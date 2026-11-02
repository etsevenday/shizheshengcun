using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B20 R100 五处旧精英可选单局挑战（Q81 答复；A09「Q81 旧五种的可选投放」、R05 同轮章；legacy-elite-placements.json；验收 RES-Q81a—c）：
    /// - 营区登记（hub_registered）且当地探索 100% 后显示挑战告示；告示不是活动敌踪，不挡行路/资源/安全屋/主线。
    /// - 玩家明确“接战”才建唯一世界敌人并开战；每处只这一名敌，不受 C 截掉、不附助手/增援，不进随机池、不占种群名额。
    /// - 首次接战锁定特殊掉落抽签（z_guard 40% 钢材）；撤退保留 HP、意图进度与抽签，可以再明确接近；不随 72 小时/清理/世界日/离区刷新。
    /// - 最终胜利且主角存活一次提交：只发该敌原特殊掉落（替代原附加掉落，不另加首领/首通/唯一箱），落空也记已结算；campaign 一次账。
    /// - 旧档：存档里从没有按 enemyId 记的结算/奖励凭据，五处一律从未结算开始（不按背包推测，A09 Q81“不按同名新守卫或背包是否有材料推测”）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public LegacyEliteConfig? EliteConfig(string id) => WE.LegacyElites.FirstOrDefault(x => x.Id == id);

        private LegacyEliteState EliteState(GameState s, string id)
        {
            if (!s.Population.Elites.TryGetValue(id, out var st)) s.Population.Elites[id] = st = new LegacyEliteState { Id = id };
            return st;
        }

        /// <summary>挑战告示是否显示：未结算、满足登记事实、当地探索度达到揭示值。</summary>
        public bool EliteVisible(GameState s, LegacyEliteConfig c) =>
            Config.CombatEnabled
            && !(s.Population.Elites.TryGetValue(c.Id, out var st) && st.Settled)
            && (c.RequiresFact == null || MapFact(s, c.RequiresFact))
            && s.Locations.TryGetValue(c.NodeId, out var ls) && ls.Progress >= c.RevealAtExplorePercent;

        /// <summary>当前地点（外部区）显示的挑战告示。</summary>
        public List<LegacyEliteConfig> EliteChallengesHere(GameState s) =>
            s.CurrentZone == SafehouseZone ? new List<LegacyEliteConfig>()
            : WE.LegacyElites.Where(c => c.NodeId == s.CurrentLocation && EliteVisible(s, c)).ToList();

        /// <summary>已接过战、还活着的那名唯一敌人（撤退后留在原地、保留 HP）；没接过为 null。</summary>
        public WorldEnemy? EliteEnemy(GameState s, string id) =>
            s.Population.Elites.TryGetValue(id, out var st) && st.WorldId is int wid && FindWorldEnemy(s, wid) is WorldEnemy w && w.FixedOf == ElitePrefix + id ? w : null;

        public string? CanEngageElite(GameState s, string id)
        {
            var c = EliteConfig(id);
            if (c == null) return "没有这项挑战";
            var why = EngageBlocked(s);
            if (why != null) return why;
            if (s.CurrentLocation != c.NodeId) return $"要在{LocName(c.NodeId)}接战";
            if (!EliteVisible(s, c)) return s.Population.Elites.TryGetValue(id, out var st) && st.Settled ? "本局已挑战成功" : "挑战还没有出现";
            if (EliteEnemy(s, id) is WorldEnemy w && w.Engaged) return "这个敌人正在交战";
            return null;
        }

        /// <summary>
        /// 明确接战：第一次接战时建唯一世界敌人并锁定特殊掉落抽签（只抽这一次，撤退/读档不重抽）；之后再接战用同一只（保留 HP）。
        /// 单独入场（不受 C 截掉、不附同地敌人），受惊按主动接近；本场禁增援。
        /// </summary>
        public ActionResult EngageElite(GameState s, string id, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = CanEngageElite(s, id);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var c = EliteConfig(id)!;
            var st = EliteState(s, id);
            var w = EliteEnemy(s, id);
            if (w == null)
            {
                w = AddWorldEnemy(s, c.EnemyId, c.NodeId);
                w.FixedOf = ElitePrefix + id;
                st.WorldId = w.Id;
                st.AcceptedMinute = s.GameMinute;
                st.RewardWon ??= c.RewardChance >= 1 || s.Rng.NextDouble() < c.RewardChance;
            }
            StartBattle(s, new List<WorldEnemy> { w }, false, "可选强敌挑战", FearApproach, fixedKey: ElitePrefix + id);
            return ActionResult.Success();
        }

        /// <summary>挑战敌人死亡时锁定的掉落：只用首次接战时锁定的特殊掉落计划（替代该敌原附加掉落，不另抽）。不是挑战敌人为 null。</summary>
        private BattleLoot? EliteLoot(GameState s, BattleState b, BattleEnemy e)
        {
            if (b.FixedKey == null || !b.FixedKey.StartsWith(ElitePrefix)) return null;
            var id = b.FixedKey.Substring(ElitePrefix.Length);
            var c = EliteConfig(id);
            if (c == null || !s.Population.Elites.TryGetValue(id, out var st) || st.WorldId != e.WorldId) return null;
            return st.RewardWon == true ? new BattleLoot { WorldId = e.WorldId, ItemId = c.RewardItemId, Count = c.RewardCount }
                : new BattleLoot { WorldId = e.WorldId, ItemId = null, Count = 0 };
        }

        /// <summary>挑战最终胜利（主角存活）：记已结算（campaign 一次，落空也算），本局不再出现。</summary>
        private void EliteVictory(GameState s, BattleState b, List<string> lines)
        {
            var id = b.FixedKey!.Substring(ElitePrefix.Length);
            var c = EliteConfig(id);
            if (c == null) return;
            var st = EliteState(s, id);
            if (st.Settled || EliteEnemy(s, id) != null) return;
            st.Settled = true;
            st.SettledMinute = s.GameMinute;
            lines.Add(st.RewardWon == true
                ? $"可选强敌挑战完成（本局一次）：特殊掉落{ItemName(c.RewardItemId)}×{c.RewardCount}"
                : $"可选强敌挑战完成（本局一次）：特殊掉落{ItemName(c.RewardItemId)}的{Fmt.Pct(c.RewardChance)}抽签没有落中（已结算，不再重抽）");
        }

        /// <summary>安全边界：当前地点的挑战告示第一次出现时写一条日志（只写一次；不建敌人、不挡任何行动）。</summary>
        private void EliteNoticeBoundary(GameState s)
        {
            foreach (var c in EliteChallengesHere(s))
            {
                var st = EliteState(s, c.Id);
                if (st.Noticed) continue;
                st.Noticed = true;
                Emit(s, GameEventKind.Info, $"可选强敌告示：{LocName(c.NodeId)}有{_enemies[c.EnemyId].Name}出没（单局一次的可选挑战；不接也不影响行路、资源与主线）。");
            }
        }

        /// <summary>挑战告示上的说明：特殊掉落与抽签状态、是否已接过战（HP）。</summary>
        public string EliteNote(GameState s, LegacyEliteConfig c)
        {
            var parts = new List<string> { "可选强敌 · 单局一次" };
            parts.Add(c.RewardChance >= 1 ? $"特殊掉落{ItemName(c.RewardItemId)}×{c.RewardCount}" : $"特殊掉落{ItemName(c.RewardItemId)}×{c.RewardCount}（{Fmt.Pct(c.RewardChance)}，首次接战时抽定）");
            if (EliteEnemy(s, c.Id) is WorldEnemy w) parts.Add($"已交过手：健康{Fmt.N(w.Hp)}/{Fmt.N(_enemies[c.EnemyId].Hp)}");
            return string.Join(" · ", parts);
        }

        /// <summary>开发入口：记营区登记、传送到某处挑战地点（按实际到达）并把当地探索度拨到 100%，让告示出现。</summary>
        public string DebugEliteGo(GameState s, string id)
        {
            var c = EliteConfig(id);
            if (c == null) return "开发：没有这项挑战";
            if (IsBusy(s)) return "开发：行动进行中或有敌踪/战斗，先结束再传送";
            if (c.RequiresFact != null && !MapFact(s, c.RequiresFact)) DebugMapFact(s, c.RequiresFact);
            DebugMapTeleport(s, c.NodeId);
            DebugSetProgress(s, c.NodeId, 100);
            return $"开发：已到{LocName(c.NodeId)}（记营区登记、探索度100%）；设施行“可选强敌”卡：{_enemies[c.EnemyId].Name}。";
        }

        /// <summary>开发入口：人口区计数一览（“ex_pop_utility 3/8 已进入”）。</summary>
        public string DebugPopulationText(GameState s) =>
            "人口区：" + string.Join("；", RegionSummary(s).Select(x => $"{x.Region.Id} {x.Live}/{x.Region.Cap}{(x.Entered ? "（已进入）" : "（未进入，不刷新）")}"))
            + $"；新手8点 {LiveCount(s)}/{WE.GlobalCap}";
    }
}

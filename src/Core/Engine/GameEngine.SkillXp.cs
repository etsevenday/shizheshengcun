using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B5 新增的经验来源与防刷（A06 §3、R01 §2；开发计划 v0.9 B5 H4）：
    /// 采集/搜集每成功提交一次 +2（非箱子资源点练“采集”、单次箱子练“搜集”），同一资源点每个世界日最多 3 次给经验；
    /// 行旅每完整、非取消的物理边移动 +1，同一有向边每个世界日最多 1 次。预览、取消、读档都不给经验（只在完成时结算）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string DailyGather = "gather";
        public const string DailyMove = "move";

        private static string DailyKey(int day, string kind, string id) => $"{day}|{kind}|{id}";

        /// <summary>今日（按世界日）某类别某对象已完成的次数。</summary>
        public int DailyCount(GameState s, ActorState actor, string kind, string id) =>
            actor.DailyCounts.TryGetValue(DailyKey(Clock.Day(s.GameMinute), kind, id), out var n) ? n : 0;

        /// <summary>今日计数 +1（顺带清掉非今日的键），返回加后的次数。</summary>
        private int BumpDaily(GameState s, ActorState actor, string kind, string id)
        {
            var day = Clock.Day(s.GameMinute);
            var prefix = $"{day}|";
            foreach (var k in actor.DailyCounts.Keys.Where(k => !k.StartsWith(prefix)).ToList()) actor.DailyCounts.Remove(k);
            var key = DailyKey(day, kind, id);
            actor.DailyCounts.TryGetValue(key, out var n);
            actor.DailyCounts[key] = n + 1;
            return n + 1;
        }

        /// <summary>采集/搜集练哪门：单次箱子（遗迹容器）练搜集，其余资源点（含废金属堆、脏水源）练采集（开发假设 H4，待确认 Q55）。</summary>
        public static string GatherSkillOf(ResourceConfig res) => res.IsCache ? Scavenging : Gathering;

        public static string MoveKey(string from, string to) => $"{from}>{to}";

        /// <summary>预估用：完成这次采集/搜集会得到的经验说明。</summary>
        public string GatherXpPreview(GameState s, ResourceConfig res)
        {
            var skill = GatherSkillOf(res);
            if (SkillMaxed(s, skill)) return XpText(s, skill, Config.Xp.GatherSubmit);
            var done = DailyCount(s, s.Player, DailyGather, res.NodeId);
            var cap = Config.Xp.GatherDailyPerNode;
            return done < cap
                ? $"{SkillName(skill)}经验+{Config.Xp.GatherSubmit}（此处今日第{done + 1}/{cap}次计经验）"
                : $"此处今日已计{cap}次{SkillName(skill)}经验，本次不再给经验";
        }

        /// <summary>采集/搜集成功提交：计今日次数，额度内给经验。返回日志用文字。</summary>
        private string AwardGatherXp(GameState s, ResourceConfig res)
        {
            var skill = GatherSkillOf(res);
            var n = BumpDaily(s, s.Player, DailyGather, res.NodeId);
            if (n > Config.Xp.GatherDailyPerNode) return $"此处今日已计{Config.Xp.GatherDailyPerNode}次{SkillName(skill)}经验";
            var text = XpText(s, skill, Config.Xp.GatherSubmit);
            GainXp(s, skill, Config.Xp.GatherSubmit);
            return text;
        }

        /// <summary>预估用：完成这次移动会得到的行旅经验说明。</summary>
        public string TravelXpPreview(GameState s, string from, string to)
        {
            if (SkillMaxed(s, Athletics)) return XpText(s, Athletics, Config.Xp.TravelEdge);
            return DailyCount(s, s.Player, DailyMove, MoveKey(from, to)) < Config.Xp.TravelDailyPerDirectedEdge
                ? $"{SkillName(Athletics)}经验+{Config.Xp.TravelEdge}"
                : $"今日已从此方向走过这条路，本次不再给{SkillName(Athletics)}经验";
        }

        /// <summary>完整移动到达：同一有向边每世界日最多给一次行旅经验。</summary>
        private string AwardTravelXp(GameState s, string from, string to)
        {
            var n = BumpDaily(s, s.Player, DailyMove, MoveKey(from, to));
            if (n > Config.Xp.TravelDailyPerDirectedEdge) return $"今日已从此方向走过这条路，不再给{SkillName(Athletics)}经验";
            var text = XpText(s, Athletics, Config.Xp.TravelEdge);
            GainXp(s, Athletics, Config.Xp.TravelEdge);
            return text;
        }
    }
}

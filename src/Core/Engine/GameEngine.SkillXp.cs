using System;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B5 新增的经验来源与防刷（A06 §3、R01 §2；开发计划 v0.9 B5 H4），B7b 按 Q55 改：
    /// 采集/搜集每成功提交一次 clamp(2×ceil(原基础分钟/5),2,12)（按资源点熟练归属练采集或搜集），同一资源点每个世界日最多 3 次给经验，
    /// 每角色每门每日最多 60；
    /// 行旅每完整、非取消的物理边移动 +1，同一有向边每个世界日最多 1 次。预览、取消、读档都不给经验（只在完成时结算）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string DailyGather = "gather";
        public const string DailyMove = "move";
        /// <summary>每角色每门每日已得经验（Q55 采集/搜集 60，Q60 烹饪食品 30）。</summary>
        public const string DailyXp = "xp";
        /// <summary>遗迹勘整每角色每地点每日完成次数（Q55：2 次）。</summary>
        public const string DailySurvey = "survey";
        /// <summary>烹饪：净化已完成的毫升（每 500ml 1 经验，日 4）与食品经验（日 30）（Q60）。</summary>
        public const string DailyWaterMl = "waterMl";
        public const string DailyWaterXp = "waterXp";

        private static string DailyKey(int day, string kind, string id) => $"{day}|{kind}|{id}";

        /// <summary>今日（按世界日）某类别某对象已完成的次数。</summary>
        public int DailyCount(GameState s, ActorState actor, string kind, string id) => DailyCount(actor, Clock.Day(s.GameMinute), kind, id);

        private static int DailyCount(ActorState actor, int day, string kind, string id) =>
            actor.DailyCounts.TryGetValue(DailyKey(day, kind, id), out var n) ? n : 0;

        /// <summary>今日计数 +1（顺带清掉非今日的键），返回加后的次数。</summary>
        private int BumpDaily(GameState s, ActorState actor, string kind, string id) => BumpDaily(s, actor, kind, id, Clock.Day(s.GameMinute), 1);

        /// <summary>
        /// 某世界日计数 +by（Q55：采集/搜集按开工日记账，跨午夜完成仍记开工日）。清掉比 day 与今日都早的键，返回加后的值。
        /// </summary>
        private int BumpDaily(GameState s, ActorState actor, string kind, string id, int day, int by)
        {
            var keep = Math.Min(day, Clock.Day(s.GameMinute));
            foreach (var k in actor.DailyCounts.Keys.ToList())
            {
                var bar = k.IndexOf('|');
                if (bar > 0 && int.TryParse(k.Substring(0, bar), out var d) && d < keep) actor.DailyCounts.Remove(k);
            }
            var key = DailyKey(day, kind, id);
            actor.DailyCounts.TryGetValue(key, out var n);
            actor.DailyCounts[key] = n + by;
            return n + by;
        }

        /// <summary>
        /// 采集/搜集练哪门（Q55）：按资源点配置的熟练归属（自然采出练采集；人工/工业资源与箱子练搜集，含废金属堆）；
        /// 配置没写时按旧口径（箱子搜集、其余采集）。
        /// </summary>
        public static string GatherSkillOf(ResourceConfig res) => res.ProficiencyId ?? (res.IsCache ? Scavenging : Gathering);

        /// <summary>采集/搜集完成经验 clamp(2×ceil(原基础分钟/5),2,12)（Q55；工具/装备/节点折减不降低，用资源点原基础分钟）。</summary>
        public int FieldXp(ResourceConfig res)
        {
            var f = Config.Progression.FieldXp;
            if (f.StepMinutes <= 0) return 0;
            var raw = f.StepXp * (int)Math.Ceiling(res.BaseMinutes / (double)f.StepMinutes);
            return Math.Max(f.Min, Math.Min(f.Max, raw));
        }

        /// <summary>某角色某门在某世界日已得的经验（日额计数）。</summary>
        private int DailyXpUsed(ActorState actor, int day, string skill) => DailyCount(actor, day, DailyXp, skill);

        /// <summary>日额内实际可给的经验：min(amount, 上限−已得)，不足为 0。</summary>
        private int DailyXpRoom(ActorState actor, int day, string skill, int amount, int cap) =>
            Math.Max(0, Math.Min(amount, cap - DailyXpUsed(actor, day, skill)));

        /// <summary>预估用：完成这次采集/搜集会得到的经验说明（Q55：同点每日前3次、每门每日60）。</summary>
        public string GatherXpPreview(GameState s, ResourceConfig res)
        {
            var skill = GatherSkillOf(res);
            var xp = FieldXp(res);
            var f = Config.Progression.FieldXp;
            if (SkillMaxed(s, skill)) return XpText(s, skill, xp);
            var day = Clock.Day(s.GameMinute);
            var done = DailyCount(s, s.Player, DailyGather, res.NodeId);
            if (done >= f.PerResourceDaily) return $"此处今日已计{f.PerResourceDaily}次{SkillName(skill)}经验，本次不再给经验（仍正常取材）";
            var used = DailyXpUsed(s.Player, day, skill);
            var room = DailyXpRoom(s.Player, day, skill, xp, f.DailyPerActorSkill);
            var where = res.IsCache ? "" : $"此处今日第{done + 1}/{f.PerResourceDaily}次计经验；";
            if (room <= 0) return $"今日{SkillName(skill)}经验已满{f.DailyPerActorSkill}，本次不再给经验（仍正常取材）";
            return $"{SkillName(skill)}经验+{room}（{where}今日{SkillName(skill)}经验{used}/{f.DailyPerActorSkill}）";
        }

        /// <summary>采集/搜集成功提交：按开工日计同点次数，额度内给经验（受每门每日上限）。返回日志用文字。</summary>
        private string AwardGatherXp(GameState s, ResourceConfig res, ActionState a)
        {
            var skill = GatherSkillOf(res);
            var f = Config.Progression.FieldXp;
            var day = a.XpDay ?? Clock.Day(s.GameMinute);
            var n = BumpDaily(s, s.Player, DailyGather, res.NodeId, day, 1);
            if (n > f.PerResourceDaily) return $"此处今日已计{f.PerResourceDaily}次{SkillName(skill)}经验";
            return AwardFieldXp(s, skill, FieldXp(res), day, f.DailyPerActorSkill);
        }

        /// <summary>给野外经验（采集/搜集/勘整共用每门每日上限）：满级、日额满都不给；返回日志文字。</summary>
        private string AwardFieldXp(GameState s, string skill, int amount, int day, int cap)
        {
            if (SkillMaxed(s, skill)) return XpText(s, skill, amount);
            var room = DailyXpRoom(s.Player, day, skill, amount, cap);
            if (room <= 0) return $"今日{SkillName(skill)}经验已满{cap}，不再给经验";
            BumpDaily(s, s.Player, DailyXp, skill, day, room);
            var text = XpText(s, skill, room);
            GainXp(s, skill, room);
            return text;
        }

        public static string MoveKey(string from, string to) => $"{from}>{to}";

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

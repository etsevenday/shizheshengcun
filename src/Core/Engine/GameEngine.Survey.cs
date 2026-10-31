using System;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 遗迹勘整（策划答复 Q55、S05/A06 本轮章，开发计划 v0.9 B7b）：村庄、五金店、诊所、废料场到访且探索 100%、空闲无敌踪时可开始的搜集训练。
    /// 30 基础分钟，按正常搜集身体费率与 S01 时间倍率推进；每角色每地点每世界日 2 次，成功搜集经验 +12（与采集/搜集共用每门每日 60）。
    /// 不产物、不消耗库存、不恢复箱子/资源/探索、不扣清理次数；工具、装备与搜集速度被动都不缩短训练，危险辨识可降风险。
    /// 取消、未完成、死亡不给经验、不占次数。
    /// </summary>
    public sealed partial class GameEngine
    {
        public SurveyConfig SurveyRules => Config.Progression.Survey;

        /// <summary>这里是否有遗迹勘整入口（只看地点，不看探索度与次数）。</summary>
        public bool HasSurvey(string locationId) => SurveyRules.NodeIds.Contains(locationId);

        /// <summary>今日本人在该地点已完成的勘整次数。</summary>
        public int SurveyDoneToday(GameState s, string locationId) => DailyCount(s, s.Player, DailySurvey, locationId);

        /// <summary>开始校验（只读）；可以开始返回 null。通用的死亡/战斗/敌踪/忙碌由 Validate 前段处理。</summary>
        private string? ValidateSurvey(GameState s, Plan plan)
        {
            if (plan.IsSub) return "暂停中只能补给、休息或点火维护";
            var here = s.CurrentLocation;
            if (!HasSurvey(here)) return "这里没有可勘整的遗迹";
            var ls = s.Locations[here];
            if (!ls.Visited) return "只能勘整已到访的当前地点";
            if (ls.Progress < SurveyRules.RequiresExplorePercent) return $"探索度{SurveyRules.RequiresExplorePercent}%后才能勘整（当前{ls.Progress}%）";
            var done = SurveyDoneToday(s, here);
            if (done >= SurveyRules.DailyPerActorNode) return $"今日已在这里勘整{SurveyRules.DailyPerActorNode}次，明天再来";
            plan.TargetId = here;
            plan.BaseWork = SurveyRules.BaseMinutes;
            plan.Label = SurveyRules.Name;
            return null;
        }

        /// <summary>勘整的风险（按地点）；危险辨识（搜集7级）同样降低概率，装备划伤减伤照算。没有风险为 null。</summary>
        public HazardConfig? SurveyHazard(string locationId) =>
            SurveyRules.HazardByNode.TryGetValue(locationId, out var h) && h.Probability > 0 ? h : null;

        public double SurveyHazardChance(GameState s, string locationId) =>
            SurveyHazard(locationId) is HazardConfig h ? h.Probability * PassiveFactor(s.Player, "hazard.scavengeChance") : 0;

        public double SurveyHazardLoss(GameState s, string locationId) =>
            SurveyHazard(locationId) is HazardConfig h ? h.HealthLoss * EquipmentEffects(s).Factor("hazard.health") : 0;

        /// <summary>开工：锁开工日、按地点抽一次风险（读档不重抽）。</summary>
        private void StartSurvey(GameState s, ActionState a)
        {
            a.XpDay = Clock.Day(s.GameMinute);
            if (SurveyHazard(a.TargetId) != null) a.HazardHit = s.Rng.NextDouble() < SurveyHazardChance(s, a.TargetId);
        }

        /// <summary>预估用的经验说明。</summary>
        public string SurveyXpPreview(GameState s, string locationId)
        {
            var r = SurveyRules;
            var skill = r.ProficiencyId;
            if (SkillMaxed(s, skill)) return XpText(s, skill, r.Xp);
            var cap = Config.Progression.FieldXp.DailyPerActorSkill;
            var day = Clock.Day(s.GameMinute);
            var room = DailyXpRoom(s.Player, day, skill, r.Xp, cap);
            if (room <= 0) return $"今日{SkillName(skill)}经验已满{cap}，本次不再给经验";
            return $"{SkillName(skill)}经验+{room}（今日第{SurveyDoneToday(s, locationId) + 1}/{r.DailyPerActorNode}次勘整；今日{SkillName(skill)}经验{DailyXpUsed(s.Player, day, skill)}/{cap}）";
        }

        /// <summary>完成：计次数、给经验（共用每门每日上限），再结算风险。不产物。</summary>
        private bool CompleteSurvey(GameState s, ActionState a)
        {
            var r = SurveyRules;
            var loc = a.TargetId;
            var day = a.XpDay ?? Clock.Day(s.GameMinute);
            var n = BumpDaily(s, s.Player, DailySurvey, loc, day, 1);
            var xp = AwardFieldXp(s, r.ProficiencyId, r.Xp, day, Config.Progression.FieldXp.DailyPerActorSkill);
            ClearAction(s, a);
            Emit(s, GameEventKind.ActionCompleted, $"{r.Name}完成（今日第{n}/{r.DailyPerActorNode}次）：只是练习，没有物资；{xp}。");
            if (a.HazardHit && SurveyHazard(loc) is HazardConfig h)
            {
                var loss = SurveyHazardLoss(s, loc);
                s.Stats.Add(StatId.Health, -loss);
                s.Progress.LastDamageCause = $"勘整时{h.Label}";
                Emit(s, GameEventKind.Hazard, $"{h.Label}，健康−{Fmt.T(loss)}。");
                if (s.Stats.Health <= 0) Kill(s, s.Progress.LastDamageCause);
            }
            return true;
        }

        /// <summary>预估的静态说明。</summary>
        private void FillSurveyPreview(GameState s, ActionPreview p)
        {
            var here = s.CurrentLocation;
            var r = SurveyRules;
            p.BaseMinutes = r.BaseMinutes;
            p.ConfirmText = $"开始{r.Name}·{r.BaseMinutes}分钟";
            p.Notes.Add($"练习，不产物：不消耗资源点库存，不恢复箱子、资源或探索度，不扣清理次数");
            p.Notes.Add($"每天每个地点最多{r.DailyPerActorNode}次（今日已{SurveyDoneToday(s, here)}次）；取消不给经验、不占次数");
            p.Notes.Add("工具、装备与搜集速度被动都不缩短勘整；按正常搜集消耗身体");
            if (SurveyHazard(here) is HazardConfig h)
            {
                p.HazardProbability = SurveyHazardChance(s, here);
                p.HazardHealthLoss = SurveyHazardLoss(s, here);
                p.HazardLabel = h.Label;
            }
            p.XpGains.Add(SurveyXpPreview(s, here));
        }
    }
}

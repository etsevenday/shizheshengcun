using System;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 烹饪经验（策划答复 Q60 选 B、S11/A06 本轮章，开发计划 v0.9 B7b）：
    /// 烤根/煮根/烤肉/煮肉成功产出时，给首次投料/启用进程的存活角色 clamp(ceil(原受热分钟/10),1,12)（30 分钟 = 3），每份一次；
    /// 复煮 0；净化每实际完成 500ml 给 1，每角色每世界日最多 4；食品（含 B16 主动配方）每角色每日合计最多 30。
    /// 只在成品提交时发；投料、点火、等待、取消、取出、转锅都不发；旧进程没有发起者（B7b 之前建立）不补。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>该进程完成时给发起者的烹饪经验（单份）；不在四种食品进程里为 0。</summary>
        public int CookFoodXpFor(string processId)
        {
            var c = Config.Progression.Cooking;
            if (!c.FoodProcesses.Contains(processId) || c.FoodHeatStepMinutes <= 0) return 0;
            var heat = Process(processId).HeatMinutes;
            return Math.Max(c.FoodMin, Math.Min(c.FoodMax, (int)Math.Ceiling(heat / (double)c.FoodHeatStepMinutes)));
        }

        /// <summary>发起者：存活且是现有角色；旧进程无发起者为 null。</summary>
        private ActorState? CookCredit(GameState s, FacilityProcess p)
        {
            if (p.InitiatorActorId == null || !s.Actors.TryGetValue(p.InitiatorActorId, out var actor)) return null;
            if (actor.IsPlayer ? s.Progress.Dead : actor.Stats.Health <= 0) return null;
            return actor;
        }

        /// <summary>食品进程完成：按份给经验（受每日 30）。返回日志附注；不给经验时为 null 或说明。</summary>
        private string? CookFoodXp(GameState s, FacilityProcess p, Survival.Core.Config.ProcessConfig cfg, int portions)
        {
            var per = CookFoodXpFor(cfg.Id);
            if (per <= 0) return null;
            var actor = CookCredit(s, p);
            if (actor == null) return null;
            var c = Config.Progression.Cooking;
            var day = Clock.Day(s.GameMinute);
            var room = DailyXpRoom(actor, day, Cooking, per * Math.Max(1, portions), c.FoodDailyCap);
            if (SkillLevel(actor, Cooking) >= SkillCap(Cooking)) return $"{SkillName(Cooking)}已满{SkillCap(Cooking)}级，不再获得经验";
            if (room <= 0) return $"今日{SkillName(Cooking)}经验已满{c.FoodDailyCap}，不再给经验";
            BumpDaily(s, actor, DailyXp, Cooking, day, room);
            GainXp(s, actor, Cooking, room);
            return $"{ActorPrefix(actor)}{SkillName(Cooking)}经验+{room}";
        }

        /// <summary>净化完成：本日累计净化毫升每满 500 给 1，每日最多 4。</summary>
        private string? CookWaterXp(GameState s, FacilityProcess p, int ml)
        {
            var c = Config.Progression.Cooking;
            if (p.RecipeId != c.WaterProcess || c.WaterMlPerXp <= 0 || ml <= 0) return null;
            var actor = CookCredit(s, p);
            if (actor == null) return null;
            var day = Clock.Day(s.GameMinute);
            var totalMl = BumpDaily(s, actor, DailyWaterMl, Cooking, day, ml);
            var given = DailyCount(actor, day, DailyWaterXp, Cooking);
            var due = Math.Min(c.WaterDailyCap, totalMl / c.WaterMlPerXp) - given;
            if (SkillLevel(actor, Cooking) >= SkillCap(Cooking)) return $"{SkillName(Cooking)}已满{SkillCap(Cooking)}级，不再获得经验";
            if (due <= 0) return given >= c.WaterDailyCap ? $"今日净化经验已满{c.WaterDailyCap}" : null;
            BumpDaily(s, actor, DailyWaterXp, Cooking, day, due);
            GainXp(s, actor, Cooking, due);
            return $"{ActorPrefix(actor)}{SkillName(Cooking)}经验+{due}";
        }

        /// <summary>火堆/锅卡与预估用：这一项完成后的烹饪经验说明（旧进程写不补）。</summary>
        public string? CookXpNote(GameState s, FacilityProcess p)
        {
            var c = Config.Progression.Cooking;
            if (p.RecipeId == c.WaterProcess)
                return p.InitiatorActorId == null ? "旧进程，不给烹饪经验" : $"完成后每500毫升{SkillName(Cooking)}经验+1（每日最多{c.WaterDailyCap}）";
            var per = CookFoodXpFor(p.RecipeId);
            if (per <= 0) return c.FoodProcesses.Count > 0 && !c.FoodProcesses.Contains(p.RecipeId) ? "复煮不给烹饪经验" : null;
            return p.InitiatorActorId == null ? "旧进程，不给烹饪经验" : $"完成后{SkillName(Cooking)}经验+{per}（每日最多{c.FoodDailyCap}）";
        }
    }
}

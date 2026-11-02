using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 配方框架（开发计划 v0.9 B16，R48；A07「X1.3 每个配方及知识」、S08 X4）：三种模式——active_work（主动工作量，吃技能/装备加速）、
    /// fixed_wait（固定等待，全队等待，不吃加速、不给经验）、passive_heat/passive（设施被动加工：火堆锅食谱见 GameEngine.PotRecipe.cs，
    /// 陶窑与固定净水台随 B17）。投料按具体实例预留，完成只出一次。知识 expedition_basics（B14）与 ceramic_methods（本批）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>这批实例里最早的截止时间；都无保质为 null（口粮棒/恢复剂成品保质上限）。</summary>
        private int? EarliestExpiry(GameState s, IEnumerable<int> instanceIds)
        {
            int? min = null;
            foreach (var id in instanceIds)
            {
                var it = FindItem(s, id);
                if (it?.ExpiresAt is int e && (min == null || e < min)) min = e;
            }
            return min;
        }

        /// <summary>
        /// 制作完成经验（返回日志短句）：固定等待不给经验（开发假设 B16-3）；烹饪门类（远征口粮棒）与食品进程合计每角色每日 30（Q60）；
        /// 其余按 clamp(ceil(原基础分钟/10),1,12)×批数给开工快照的门类。
        /// </summary>
        private string CraftCompleteXp(GameState s, ActionState a, RecipeConfig rc, string xpSkill, int batch)
        {
            if (a.FixedWait) return "固定等待不给经验";
            var xp = CraftXp(rc.BaseMinutes) * batch;
            if (xpSkill == Cooking)
            {
                var c = Config.Progression.Cooking;
                if (SkillMaxed(s, Cooking)) return XpText(s, Cooking, xp);
                var day = Clock.Day(s.GameMinute);
                var room = DailyXpRoom(s.Player, day, Cooking, (int)xp, c.FoodDailyCap);
                if (room <= 0) return $"今日{SkillName(Cooking)}经验已满{c.FoodDailyCap}，不再给经验";
                BumpDaily(s, s.Player, DailyXp, Cooking, day, room);
                GainXp(s, s.Player, Cooking, room);
                return $"{SkillName(Cooking)}经验+{room}";
            }
            GainXp(s, xpSkill, xp);
            return XpText(s, xpSkill, xp);
        }

        /// <summary>配方模式的玩家说法：“主动制作（吃熟练加速）”“固定等待20分钟（不吃加速）”“被动加工（不吃加速）”。</summary>
        public string RecipeModeText(RecipeConfig rc) => rc.ModeOrDefault switch
        {
            RecipeConfig.ModeFixedWait => $"固定等待{Clock.Duration(rc.BaseMinutes)}（全队等待，不吃熟练加速，不给经验）",
            RecipeConfig.ModePassiveHeat or RecipeConfig.ModePassive => rc.IsPot
                ? $"火堆锅里被动加工{rc.BaseMinutes}有效受热分钟（不吃熟练加速）"
                : $"{StationName(rc.Station)}被动加工{Clock.Duration(rc.BaseMinutes)}（不吃熟练加速）",
            _ => "主动制作（吃熟练与装备加速）",
        };
    }
}

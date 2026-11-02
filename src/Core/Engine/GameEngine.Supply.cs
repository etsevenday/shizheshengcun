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

        // ───────────── 护理品（B16 R51；A07 X1.2/X1.6、「二十种食品用品」口服补液） ─────────────

        /// <summary>护理品现在不能用的原因（冷却中、没有腹泻、体力已满、水分满且没有腹泻收益）；能用为 null。</summary>
        public string? CareBlocked(GameState s, ItemUseConfig u)
        {
            var a = s.Player;
            if (u.OutOfBattleOnly && s.Battle != null) return "战斗中不能使用";
            if (u.CooldownGroup != null && a.Cooldowns.TryGetValue(u.CooldownGroup, out var ready) && ready > s.GameMinute)
                return $"{CareGroupName(u.CooldownGroup)}冷却中，还需{Clock.Duration(ready - s.GameMinute)}";
            var diarrhea = a.DiarrheaUntil.HasValue && a.DiarrheaUntil.Value > s.GameMinute;
            if (u.RequiresDiarrhea && !diarrhea) return "没有腹泻，用了没有效果";
            if (u.BlockWhenEnergyFull && a.Stats.Energy >= a.Stats.EnergyMax - 1e-9) return "体力已满，用了没有效果";
            if (u.BlockWhenNoHydrationOrDiarrheaGain && !diarrhea && a.Stats.Hydration >= a.Stats.Max(StatId.Hydration) - 1e-9)
                return "水分已满且没有腹泻，用了没有效果";
            return null;
        }

        private static string CareGroupName(string group) => group switch
        {
            "relief" => "缓解（止泻药粉与口服补液共用）",
            "tonic" => "应急恢复剂给药",
            _ => "护理",
        };

        /// <summary>护理品效果（Use 开始时一次结清；饱食/水分/精神由通用食用结算）：体力、清醒、腹泻剩余减少，并启动冷却。</summary>
        private void ApplyCare(GameState s, ItemUseConfig u, List<string> parts)
        {
            var a = s.Player;
            if (u.Energy != 0) { s.Stats.Add(StatId.Energy, u.Energy); parts.Add(Signed("体力", u.Energy)); }
            if (u.Wakefulness != 0) { s.Stats.Add(StatId.Wakefulness, u.Wakefulness); parts.Add(Signed("清醒", u.Wakefulness)); }
            if (u.DiarrheaReduceMinutes > 0 && a.DiarrheaUntil.HasValue && a.DiarrheaUntil.Value > s.GameMinute)
            {
                var left = a.DiarrheaUntil.Value - s.GameMinute;
                var cut = Math.Min(left, u.DiarrheaReduceMinutes);
                a.DiarrheaUntil = left - cut <= 0 ? (int?)null : a.DiarrheaUntil.Value - cut;
                parts.Add(a.DiarrheaUntil == null ? "腹泻已止住" : $"腹泻剩余减{cut}分钟（还剩{Clock.Duration(a.DiarrheaUntil.Value - s.GameMinute)}）");
            }
            if (u.CooldownGroup != null && u.CooldownMinutes > 0)
            {
                a.Cooldowns[u.CooldownGroup] = s.GameMinute + u.CooldownMinutes;
                parts.Add($"{CareGroupName(u.CooldownGroup)}冷却{Clock.Duration(u.CooldownMinutes)}");
            }
        }

        /// <summary>
        /// 开发入口（?dev=1，B16）：学会远行基础做法与基础陶工做法，并把试新配方/水壶/锅食谱/护理品/小修用的材料放到当前地面
        /// （件数是测试用，不是玩法数值）。
        /// </summary>
        public string DebugSupplyKit(GameState s)
        {
            s.Progress.Flags.Add("knowledge:expedition_basics");
            s.Progress.Flags.Add("knowledge:ceramic_methods");
            var kit = new (string id, int n)[]
            {
                ("dirty_water", 4), ("clean_water", 2), ("purification_tablet", 1), ("filter_cartridge", 1), ("water_filter", 1), ("ration_canteen", 1),
                ("grain", 3), ("salt", 2), ("raw_meat", 1), ("dried_meat", 1), ("dried_berries", 1), ("disinfectant", 1),
                ("antidiarrheal_powder", 1), ("oral_rehydration", 1), ("emergency_tonic", 1), ("field_patch", 2),
            };
            foreach (var (id, n) in kit) CreateItems(s, id, n, s.Ground);
            var text = "开发：学会远行基础做法、基础陶工做法，B16 材料（脏水4、净水2、净水片、滤芯、滤水器、分量水壶、谷物3、盐2、生肉、肉干、果干、消毒液、止泻药粉、口服补液、应急恢复剂、野外修补包2）放在当前地面";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
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

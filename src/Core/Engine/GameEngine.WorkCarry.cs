using System;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 工作量余额结转（策划答复 Q61 选 A，S01「2026-10-31 Q53—Q61成长与行动裁定」；开发计划 v0.9 B7b）：
    /// 需求工作 W = 开工快照的基础工作分钟 × 装备与熟练节点倍率（合成下限×0.70，位于 S01 倍率 M 之前）；每段推进 5/M。
    /// 同角色同动作族（移动按同行角色集合）一个余额桶：开工整笔锁住原余额 C，推进到 C+J≥W 完成（至少 1 个真实 5 分钟段），
    /// 成功后新余额 = floor到0.0001(min(4.9999, max(0, C+J−W)))；取消/中断/死亡退回 C、本次不结新余额。
    /// 固定时长（等待/休息/睡眠/使用/点火/绕行/融合/等待烹饪）与火堆被动受热不结转；旧档余额从 0 开始，旧在途动作（RuleVersion 0）按旧规则结束。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>B7b 起开工的行动规则版本（见 ActionState.RuleVersion）。</summary>
        public const int WorkRuleVersion = 1;

        /// <summary>动作族（余额桶名）：采集/搜集按资源点熟练归属分开，勘整训练、制造、维修、拆解、建造、探索、清理、屠宰各自独立。</summary>
        public string WorkFamily(ActionType type, string? targetId) => type switch
        {
            ActionType.Gather => targetId != null && _res.TryGetValue(targetId, out var res) ? GatherSkillOf(res) : Gathering,
            ActionType.Survey => SurveyRules.Id,
            ActionType.Craft => Crafting,
            ActionType.Repair => "repair",
            ActionType.Salvage => "dismantle",
            ActionType.Build => "building",
            ActionType.Travel => "travel",
            ActionType.Explore => "exploration",
            ActionType.Clear => "clearing",
            ActionType.Butcher => "butchering",
            _ => "",
        };

        /// <summary>余额桶键：“actorId|动作族”；移动为“travel|同行角色ID排序集合”（不能借队友余额，换队另起一桶）。</summary>
        public string CarryKeyOf(GameState s, ActionType type, string? targetId)
        {
            var family = WorkFamily(type, targetId);
            if (type == ActionType.Travel) return $"{family}|{string.Join(",", s.Party.OrderBy(x => x, StringComparer.Ordinal))}";
            return $"{GameState.PlayerId}|{family}";
        }

        /// <summary>某桶当前余额（未被进行中的行动锁住的部分）。</summary>
        public double CarryOf(GameState s, string key) => s.WorkCarry.TryGetValue(key, out var v) ? v : 0;

        /// <summary>开工：需求工作量乘装备/节点倍率快照，整笔锁住原余额。</summary>
        private void ReserveCarry(GameState s, ActionState a, double baseWork)
        {
            a.RuleVersion = WorkRuleVersion;
            a.BaseWorkMinutes = baseWork * GearWorkFactor(s, GameState.PlayerId, a.Type, a.TargetId);
            var key = CarryKeyOf(s, a.Type, a.TargetId);
            a.CarryKey = key;
            a.CarryReserved = CarryOf(s, key);
            s.WorkCarry.Remove(key);
        }

        /// <summary>按配置截断：floor 到 0.0001，上限 4.9999（只丢小数，不凭空增工作）。</summary>
        public double CarryClamp(double raw)
        {
            var c = Config.Progression.WorkCarry;
            var scale = Math.Pow(10, c.RoundingDecimals);
            var v = Math.Min(c.MaxCredit, Math.Max(0, raw));
            return Math.Floor(v * scale + 1e-6) / scale;
        }

        /// <summary>成功完成：写回新余额（与产物/经验同一段提交）。</summary>
        private void SettleCarry(GameState s, ActionState a)
        {
            if (a.CarryKey == null) return;
            var credit = CarryClamp(a.CarryReserved + a.WorkDone - a.BaseWorkMinutes);
            if (credit > 0) s.WorkCarry[a.CarryKey] = credit;
            else s.WorkCarry.Remove(a.CarryKey);
            a.CarryKey = null;
        }

        /// <summary>取消/中断/死亡：原锁余额退回原桶，本次已花时间不兑换。可重复调用。</summary>
        private void RestoreCarry(GameState s, ActionState a)
        {
            if (a.CarryKey == null) return;
            if (a.CarryReserved > 0) s.WorkCarry[a.CarryKey] = a.CarryReserved;
            a.CarryKey = null;
        }
    }
}

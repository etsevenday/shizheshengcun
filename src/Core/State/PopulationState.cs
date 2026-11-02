using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.State
{
    /// <summary>
    /// B20 新敌人、固定战与人口区（开发计划 v0.9 批次 B20；schema 32 起）：
    /// 人口区是否已进入、各节点首次人口是否已生成；固定战名单（首次揭示时锁定）；旧精英可选挑战账（Q81）。
    /// </summary>
    public sealed class PopulationState
    {
        /// <summary>已进入（实际到过任一节点）的人口区 ID；未进入的区不刷新、不追补（R05/A09 Q31 节）。</summary>
        public HashSet<string> EnteredRegions { get; set; } = new HashSet<string>();
        /// <summary>已做过首次人口的节点（首次实际到达时按 firstPopulation 生成，之后不再补）。</summary>
        public HashSet<string> InitializedNodes { get; set; } = new HashSet<string>();
        /// <summary>固定战，键为节点 ID（地牢房间每代一份，换代时丢弃；m02 单局一份）。</summary>
        public Dictionary<string, FixedBattleState> Fixed { get; set; } = new Dictionary<string, FixedBattleState>();
        /// <summary>旧精英可选挑战，键为挑战 ID（legacy_elite_*）。</summary>
        public Dictionary<string, LegacyEliteState> Elites { get; set; } = new Dictionary<string, LegacyEliteState>();

        public PopulationState Clone() => new PopulationState
        {
            EnteredRegions = new HashSet<string>(EnteredRegions),
            InitializedNodes = new HashSet<string>(InitializedNodes),
            Fixed = Fixed.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
            Elites = Elites.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
        };
    }

    /// <summary>一组已揭示的固定战：揭示时的容量 C 与截取后的名单、对应的世界敌人 ID，是否已胜。</summary>
    public sealed class FixedBattleState
    {
        public string NodeId { get; set; } = "";
        public string? DungeonId { get; set; }
        /// <summary>所属地牢实例代号；m02 等 campaign 固定战为 0。</summary>
        public int Generation { get; set; }
        /// <summary>首次揭示时的容量 C（之后扩容不重编、少带队员不缩怪；R05/A09 E2、DR21）。</summary>
        public int Capacity { get; set; }
        public int RevealedMinute { get; set; }
        /// <summary>按 C 截取后的名单（敌人 ID，前后顺序即 1 号位起的入场顺序）。</summary>
        public List<string> Roster { get; set; } = new List<string>();
        /// <summary>名单对应的世界敌人 ID（同序）；死亡后世界实例删除，这里保留编号作记录。</summary>
        public List<int> WorldIds { get; set; } = new List<int>();
        /// <summary>最终胜利且主角存活后为 true（写了守卫/检查哨事实）；本代不再出现。</summary>
        public bool Won { get; set; }
        public int? WonMinute { get; set; }

        public FixedBattleState Clone()
        {
            var c = (FixedBattleState)MemberwiseClone();
            c.Roster = new List<string>(Roster);
            c.WorldIds = new List<int>(WorldIds);
            return c;
        }
    }

    /// <summary>一处旧精英可选挑战（Q81）：告示是否已出现、是否已接战、唯一世界敌人、首次接战时锁定的奖励抽签、是否已结算。</summary>
    public sealed class LegacyEliteState
    {
        public string Id { get; set; } = "";
        /// <summary>告示首次出现过（只写一次日志）。</summary>
        public bool Noticed { get; set; }
        /// <summary>首次明确接战后建的唯一世界敌人；撤退后仍在原地（保留 HP）。死亡后世界实例删除。</summary>
        public int? WorldId { get; set; }
        public int? AcceptedMinute { get; set; }
        /// <summary>首次接战时锁定的特殊掉落抽签结果（40% 钢材这类；“固定”为 true）；撤退、读档不重抽。null 为还没接战。</summary>
        public bool? RewardWon { get; set; }
        /// <summary>最终胜利且主角存活已结算（落空也算已结算）；本局不再出现。</summary>
        public bool Settled { get; set; }
        public int? SettledMinute { get; set; }

        public LegacyEliteState Clone() => (LegacyEliteState)MemberwiseClone();
    }
}

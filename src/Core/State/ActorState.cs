using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.State
{
    /// <summary>
    /// 一名可控角色的个人状态（R08 §2「身体、技能、装备与经验」、§5，R01 §3；开发计划 v0.9 B3 H1）：六项求生状态、免疫病毒与出血、
    /// 熟练度、腹泻与绷带冷却、阈值提醒锁存。主角 actorId 固定为 "p0"（GameState.PlayerId）；世界时间、地点、物品表等仍是整局唯一。
    /// 物品不存在这里：随身与穿戴按 ItemInstance.Owner 区分角色（"carried" 为主角随身，"equip:&lt;actorId&gt;:&lt;槽&gt;" 为穿戴）。
    /// </summary>
    public sealed class ActorState
    {
        public string Id { get; set; } = "";
        public Stats Stats { get; set; } = new Stats();
        /// <summary>免疫、病毒、药效与出血（v0.6 §08–§11）。</summary>
        public BodyState Body { get; set; } = new BodyState();
        /// <summary>熟练度，键为技能 ID（v0.6 §03）。</summary>
        public Dictionary<string, SkillState> Skills { get; set; } = new Dictionary<string, SkillState>();
        public int? DiarrheaUntil { get; set; }
        /// <summary>绷带回血冷却结束时刻（v0.6 bandageHealthReadyAt）。</summary>
        public int BandageReadyAt { get; set; }
        public Dictionary<StatId, bool> AlertLatched { get; set; } = new Dictionary<StatId, bool>();
        public Dictionary<StatId, bool> ZeroLatched { get; set; } = new Dictionary<StatId, bool>();
        /// <summary>
        /// 按世界日的完成计数（A06 §3 防刷、gathering_10“每日首次”；开发计划 v0.9 B5 H4）：键 "日|类别|对象"，
        /// 类别 gather（资源点 NodeId）或 move（"出发地>目的地"）。只保留当日的键，跨日自动清掉。
        /// </summary>
        public Dictionary<string, int> DailyCounts { get; set; } = new Dictionary<string, int>();

        public bool IsPlayer => Id == GameState.PlayerId;

        public ActorState Clone()
        {
            var c = (ActorState)MemberwiseClone();
            c.Stats = Stats.Clone();
            c.Body = Body.Clone();
            c.Skills = Skills.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
            c.AlertLatched = new Dictionary<StatId, bool>(AlertLatched);
            c.ZeroLatched = new Dictionary<StatId, bool>(ZeroLatched);
            c.DailyCounts = new Dictionary<string, int>(DailyCounts);
            return c;
        }
    }
}

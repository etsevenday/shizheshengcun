using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.State
{
    /// <summary>
    /// B41a 新版敌踪卡模型（R05 文首「Q342 新版敌踪、潜行和躲避定稿」；schema 46 起）：随机组卡、存档是否已采纳新版模型。
    /// 普通单卡就是世界敌人实例本身（WorldEnemy），固定战沿 PopulationState.Fixed 的锁定名单，可选强敌沿 PopulationState.Elites，
    /// 这里只存新增的随机组卡。接近时限、现身、隐藏重现、撤退保护等记录在后续批次（B41b—B41d）补。
    /// </summary>
    public sealed class EnemyCardsState
    {
        /// <summary>
        /// 0 = 还没采纳新版卡模型（旧档读入后的默认值）；1 = 已采纳
        /// （新局直接为 1，旧档读档时做一次采纳后置 1，见 GameEngine.AdoptEnemyCards；B41e3 起新版敌踪是唯一流程，这个值只决定要不要做那一次采纳）。
        /// </summary>
        public int ModelVersion { get; set; }
        public int NextGroupId { get; set; } = 1;
        /// <summary>存续中的随机组卡（成员全灭即移除）。</summary>
        public List<EnemyGroupCard> Groups { get; set; } = new List<EnemyGroupCard>();
        /// <summary>
        /// B41b 每张卡的接近时限与撤退保护（卡键 → 记录；R05 Q416、Q439—Q443）。没有记录 = 本次现身还没抽接近时间、也没有撤退保护。
        /// 卡失效（敌人死亡、组散、单卡入组、固定战已胜）即移除。
        /// </summary>
        public Dictionary<string, EnemyCardTimer> Timers { get; set; } = new Dictionary<string, EnemyCardTimer>();
        /// <summary>B41c 每张卡因躲避隐藏后重现的次数（卡键 → 次数；没有记录 = 0）。「卡键@次数」就是本次现身的身份，潜行经验按它每次现身只领一次（Q342、Q505）。</summary>
        public Dictionary<string, int> Appearances { get; set; } = new Dictionary<string, int>();
        /// <summary>B41c 已实际获得过潜行经验的现身（"卡键@次数"）；实际经验为 0 不记（Q505 B）。</summary>
        public List<string> StealthXpClaims { get; set; } = new List<string>();

        public EnemyCardsState Clone() => new EnemyCardsState
        {
            ModelVersion = ModelVersion,
            NextGroupId = NextGroupId,
            Groups = Groups.Select(g => g.Clone()).ToList(),
            Timers = Timers.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()),
            Appearances = new Dictionary<string, int>(Appearances),
            StealthXpClaims = new List<string>(StealthXpClaims),
        };
    }

    /// <summary>
    /// 一张敌踪卡的接近时限与撤退保护（B41b；R05 Q416、Q421、Q439—Q443、Q468）。
    /// </summary>
    public sealed class EnemyCardTimer
    {
        /// <summary>本次现身剩余接近分钟（首次同地同区可操作时从 20／25／30／35／40 等概率抽一次；同地同区非战斗每推进一段减 5，最低 0）。还没抽为 null。</summary>
        public int? Left { get; set; }
        /// <summary>减到 0 的世界时刻（同刻多卡先后按它，Q468）；未到期为 null。</summary>
        public int? DueMinute { get; set; }
        /// <summary>同卡撤退保护截止（世界分钟，撤退结算时刻 + 60；重复撤退取新截止，Q416、Q443）。没有为 null。</summary>
        public int? GuardUntil { get; set; }

        public EnemyCardTimer Clone() => (EnemyCardTimer)MemberwiseClone();
    }

    /// <summary>
    /// 一张随机组卡（R05「Q417–Q419 敌踪卡、组卡与现身」）：成组时按当时容量锁定的参战名单（世界敌人 ID，原顺序；成员死亡后从名单删去），
    /// 成员不另出可交战单卡、不巡游、不被噪声征用、不进新组；撤退后幸存成员仍属本组，成员全灭后组卡移除。
    /// </summary>
    public sealed class EnemyGroupCard
    {
        public int Id { get; set; }
        public string LocationId { get; set; } = "";
        /// <summary>锁定名单里还活着的成员（世界敌人 ID，按成组时的原顺序）。</summary>
        public List<int> MemberIds { get; set; } = new List<int>();
        /// <summary>来源：explore / clear（旧档待处理名单迁来的沿原来源）。</summary>
        public string Source { get; set; } = "";
        public int FormedMinute { get; set; }
        /// <summary>成组时的队伍容量 C（名单按它截取，之后扩容不重编）。</summary>
        public int Capacity { get; set; }

        public EnemyGroupCard Clone()
        {
            var c = (EnemyGroupCard)MemberwiseClone();
            c.MemberIds = new List<int>(MemberIds);
            return c;
        }
    }
}

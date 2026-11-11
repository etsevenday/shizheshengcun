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
        /// 0 = 旧版敌踪（旧档读入后的默认值；开关 encounters.enemyCardsV2 关着时一直是 0）；1 = 已采纳新版卡模型
        /// （开关开着时新局直接为 1，旧档读档时做一次采纳后置 1，见 GameEngine.AdoptEnemyCards）。
        /// </summary>
        public int ModelVersion { get; set; }
        public int NextGroupId { get; set; } = 1;
        /// <summary>存续中的随机组卡（成员全灭即移除）。</summary>
        public List<EnemyGroupCard> Groups { get; set; } = new List<EnemyGroupCard>();

        public EnemyCardsState Clone() => new EnemyCardsState
        {
            ModelVersion = ModelVersion,
            NextGroupId = NextGroupId,
            Groups = Groups.Select(g => g.Clone()).ToList(),
        };
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

using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.State
{
    /// <summary>
    /// 持久世界敌人（v0.6 §14）：出生时生成 worldEnemyId；巡游、接敌、撤退沿用同一实例，HP 与各技能经验计数跟随它保存。
    /// 死亡即从世界移除（尸体与掉落另行结算），不复活。
    /// </summary>
    public sealed class WorldEnemy
    {
        public int Id { get; set; }
        public string EnemyId { get; set; } = "";
        public string LocationId { get; set; } = "";
        public double Hp { get; set; }
        /// <summary>中立雄鹿初始为否，被主动攻击后转敌对。</summary>
        public bool Hostile { get; set; } = true;
        /// <summary>交战中：锁住不巡游，仍计入上限。</summary>
        public bool Engaged { get; set; }
        /// <summary>狼/鹿逃离却没有合法相邻空位时，原地隐藏到此刻（不可见、不接敌、不巡游）。</summary>
        public int? HiddenUntil { get; set; }
        public int SpawnMinute { get; set; }
        /// <summary>反刷：每个技能对该敌人已计入经验的命中次数（最多 3 次，撤退、巡游、重接都不重置）。</summary>
        public Dictionary<string, int> XpHits { get; set; } = new Dictionary<string, int>();

        public WorldEnemy Clone()
        {
            var c = (WorldEnemy)MemberwiseClone();
            c.XpHits = new Dictionary<string, int>(XpHits);
            return c;
        }
    }

    /// <summary>探索/清理完成后发现的敌踪（v0.6 §15）：等待玩家选“接敌”或“绕行10分钟”。敌人是同地已有实例，不另造。</summary>
    public sealed class PendingEncounter
    {
        public string LocationId { get; set; } = "";
        public List<int> EnemyIds { get; set; } = new List<int>();
        /// <summary>explore / clear。</summary>
        public string Source { get; set; } = "";
        public int Minute { get; set; }

        public PendingEncounter Clone()
        {
            var c = (PendingEncounter)MemberwiseClone();
            c.EnemyIds = new List<int>(EnemyIds);
            return c;
        }
    }

    /// <summary>战斗中的一个敌人（v0.5 §17 EnemyInstance）：站位、HP、状态、本轮公开意图与是否已行动。</summary>
    public sealed class BattleEnemy
    {
        /// <summary>对应的世界敌人 ID（持久）。</summary>
        public int WorldId { get; set; }
        /// <summary>本场稳定实例序号（先手平手按它升序；玩家为 0）。</summary>
        public int Index { get; set; }
        public string EnemyId { get; set; } = "";
        /// <summary>F1 / F2 / B1；死亡或逃离后清空。</summary>
        public string Slot { get; set; } = "";
        public double Hp { get; set; }
        public bool Hostile { get; set; } = true;
        public bool Dead { get; set; }
        public bool Fled { get; set; }
        public bool Acted { get; set; }
        /// <summary>防御：下一次直接伤害×0.5（下次自己激活开始清除）。</summary>
        public bool Guard { get; set; }
        /// <summary>失衡：下一次攻击命中能力×0.9，最迟到施加后的下一轮末清除。</summary>
        public bool Stagger { get; set; }
        public int StaggerRound { get; set; }
        /// <summary>减速：本轮被施加 → 下一轮先手速度−2 → 下一轮末清除；不叠层。</summary>
        public bool SlowPending { get; set; }
        public bool SlowActive { get; set; }
        public int BleedTriggers { get; set; }
        public double BleedDamage { get; set; }
        /// <summary>本轮公开意图：attack / bite / charge_prepare / charge / guard / howl / flee / advance / watch。</summary>
        public string Intent { get; set; } = "";
        public string IntentLabel { get; set; } = "";
        /// <summary>野猪：上一轮已蓄力。</summary>
        public bool Charged { get; set; }
        /// <summary>流血、狼/鹿：血量低于阈值后，下次行动逃离。</summary>
        public bool WillFlee { get; set; }

        public bool Alive => !Dead && !Fled;

        public BattleEnemy Clone() => (BattleEnemy)MemberwiseClone();
    }

    /// <summary>首次死亡时锁定的掉落（丧尸抽签、动物尸体），最终胜利时一次落地；撤退放弃。</summary>
    public sealed class BattleLoot
    {
        public int WorldId { get; set; }
        public string? ItemId { get; set; }
        public int Count { get; set; }

        public BattleLoot Clone() => (BattleLoot)MemberwiseClone();
    }

    /// <summary>
    /// 战斗状态（v0.5 §17、v0.6 §07、§16）：battleId、encounterId、地点、轮次、行动队列与游标、玩家 AP、恐慌、噪声与增援、
    /// 双方状态与持续期、掉落计划。每条指令提交、每次敌方行动、轮末 tick 与结束都保存；读档恢复覆盖层与队列游标，敌人不重复行动。
    /// </summary>
    public sealed class BattleState
    {
        public int BattleId { get; set; }
        public string EncounterId { get; set; } = "";
        public string LocationId { get; set; } = "";
        public int Round { get; set; } = 1;
        public int StartMinute { get; set; }
        /// <summary>随机接敌（受惊遭遇，恐慌+10）。</summary>
        public bool Surprise { get; set; }

        /// <summary>本轮行动队列：0 为玩家，其余为敌人 Index；按先手值降序、平手按 Index 升序。</summary>
        public List<int> Queue { get; set; } = new List<int>();
        public List<double> Initiative { get; set; } = new List<double>();
        public int Cursor { get; set; }
        /// <summary>玩家本次激活的剩余行动点；不在激活中为 0。</summary>
        public int PlayerAp { get; set; }
        public bool PlayerActing { get; set; }
        public bool BreatheUsed { get; set; }
        public bool RetreatUsed { get; set; }
        /// <summary>本轮玩家开始激活时的体力（轮初先手按轮初体力，命中按付费后体力）。</summary>
        public double RoundStartEnergy { get; set; }

        public double Panic { get; set; }
        public double PanicPeak { get; set; }
        public double Noise { get; set; }
        public bool ReinforcementRequested { get; set; }
        public bool ReinforcementUsed { get; set; }

        public bool PlayerGuard { get; set; }
        public bool PlayerStagger { get; set; }
        public int PlayerStaggerRound { get; set; }
        public bool PlayerSlowPending { get; set; }
        public bool PlayerSlowActive { get; set; }

        /// <summary>当前武器（随身实例）；徒手为 null。</summary>
        public int? WeaponInstanceId { get; set; }

        public List<BattleEnemy> Enemies { get; set; } = new List<BattleEnemy>();
        public List<BattleLoot> Loot { get; set; } = new List<BattleLoot>();
        /// <summary>战斗日志（最近 80 条），界面逐条展示。</summary>
        public List<string> Log { get; set; } = new List<string>();
        /// <summary>本场已结算的世界 tick 次数（每有效轮一次）。</summary>
        public int Ticks { get; set; }
        /// <summary>本场累计：攻击次数、消耗弹药、获得经验等，结束时写进结果。</summary>
        public int ShotsFired { get; set; }

        public IEnumerable<BattleEnemy> Alive => Enemies.Where(e => e.Alive);

        public BattleState Clone()
        {
            var c = (BattleState)MemberwiseClone();
            c.Queue = new List<int>(Queue);
            c.Initiative = new List<double>(Initiative);
            c.Enemies = Enemies.Select(e => e.Clone()).ToList();
            c.Loot = Loot.Select(l => l.Clone()).ToList();
            c.Log = new List<string>(Log);
            return c;
        }
    }
}

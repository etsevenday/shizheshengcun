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
        /// <summary>受惊随机数 u 与生成的世界分钟（R03 §2.1：60 游戏分钟内再入战复用同一个 u，防撤退重接刷受惊）。</summary>
        public double FearRoll { get; set; }
        public int? FearRollMinute { get; set; }

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
        /// <summary>
        /// 敌方站位 "1"—"4"（R03 §1 四对四：1 最靠中央、4 最后；schema 12 起，旧 F1/F2/B1 迁为 1/2/3）；死亡或逃离后清空。
        /// 每条完整指令（玩家指令或一次敌方行动）后存活者按原顺序从 1 起紧凑重排。
        /// </summary>
        public string Slot { get; set; } = "";
        /// <summary>站位号 1—4；不在场为 0。</summary>
        [Newtonsoft.Json.JsonIgnore] public int Pos => int.TryParse(Slot, out var p) ? p : 0;
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
        /// <summary>追击表进度（R03 §2：每轮初加 q，满 100 扣 100 得一次 2AP 追击；不跨战斗）。</summary>
        public double PursuitProgress { get; set; }
        /// <summary>同速排序用的本局锁定随机数 u（入场时抽一次，读档不重抽）。</summary>
        public double TieRoll { get; set; }
        /// <summary>各技能已计入经验的命中次数（入战时从世界实例拷入，随时写回；死亡后仍用于本次指令判断）。</summary>
        public Dictionary<string, int> XpHits { get; set; } = new Dictionary<string, int>();

        public bool Alive => !Dead && !Fled;

        public BattleEnemy Clone()
        {
            var c = (BattleEnemy)MemberwiseClone();
            c.XpHits = new Dictionary<string, int>(XpHits);
            return c;
        }
    }

    /// <summary>首次死亡时锁定的掉落（丧尸抽签、动物尸体），最终胜利时一次落地；撤退放弃。</summary>
    public sealed class BattleLoot
    {
        public int WorldId { get; set; }
        public string? ItemId { get; set; }
        public int Count { get; set; }
        /// <summary>R07 活性物质等需要保留来源的掉落；普通掉落为 null。</summary>
        public ItemSource? Source { get; set; }

        public BattleLoot Clone()
        {
            var c = (BattleLoot)MemberwiseClone();
            c.Source = Source?.Clone();
            return c;
        }
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
        /// <summary>随机接敌（探索/清理后的敌踪“接敌”）。</summary>
        public bool Surprise { get; set; }
        /// <summary>入战情境（R03 §2.1）：approach 主动接近 / encounter 探索清理接敌 / tutorial 剧情首战。</summary>
        public string Context { get; set; } = "";
        /// <summary>剧情首战（S15 §5）：唯一保留游荡尸，不调用噪声增援，胜利发教学活性物质。</summary>
        public bool Tutorial { get; set; }
        /// <summary>受惊结果（同一 battleId 只判一次，读档不重抽）：主惊吓源、入战精神快照、随机数、概率、是否受惊、附加恐慌。</summary>
        public int? FearSourceWorldId { get; set; }
        public double FearMorale { get; set; }
        public double FearRoll { get; set; }
        public double FearChance { get; set; }
        public bool Frightened { get; set; }
        public double FearPanic { get; set; }

        /// <summary>
        /// 本轮行动队列（R03 §2）：0 为玩家，其余为敌人 Index。前 NormalCount 项是正常激活，按轮初有效速度降序、同速按锁定随机数 u 降序、
        /// 再按 worldActorId（玩家为 0、敌人为 worldEnemyId）升序；其后是本轮的追击激活（同样排序），每角色每轮至多一次。
        /// </summary>
        public List<int> Queue { get; set; } = new List<int>();
        /// <summary>与 Queue 对应的轮初有效速度（schema 12 起；旧档为先手值，只供本轮显示）。</summary>
        public List<double> Initiative { get; set; } = new List<double>();
        /// <summary>Queue 中正常激活的项数；之后的是追击。旧档为 0 时按全部正常处理。</summary>
        public int NormalCount { get; set; }
        public int Cursor { get; set; }
        /// <summary>当前这次激活是追击（固定 2AP，不清防御、不刷新每轮一次的限制）。</summary>
        public bool ActingPursuit { get; set; }
        /// <summary>玩家的追击进度与同速随机数 u（同 BattleEnemy）。</summary>
        public double PlayerPursuit { get; set; }
        public double PlayerTieRoll { get; set; }
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

        /// <summary>schema 8 的战内当前武器（随身实例引用）。只供 8→9 迁移读取（迁到武器Ⅰ后清空）；schema 9 起当前武器由 ActiveWeaponSlot 指向武器槽。</summary>
        public int? WeaponInstanceId { get; set; }

        /// <summary>当前武器页（1=武器Ⅰ、2=武器Ⅱ；A10 §4.2、B3 H6）：开战为 1，切页 0AP；随战斗存档。</summary>
        public int ActiveWeaponSlot { get; set; } = 1;

        public List<BattleEnemy> Enemies { get; set; } = new List<BattleEnemy>();
        /// <summary>
        /// 队伍容量 C（R03 §1、A11：1—4，含主角）：本场敌人数上限（入战与噪声增援累计都不超过它），战斗建立时锁存。
        /// 容量随主线解锁在后续批次（B8/R31）；本批按配置 scope.defaultCapacity（开发假设 B6-1）。
        /// </summary>
        public int Capacity { get; set; }
        /// <summary>我方站位（R03 §1）：按 1..实际人数紧凑排列的 actorId；本批只有主角（队友参战在 B8）。</summary>
        public List<string> Allies { get; set; } = new List<string> { GameState.PlayerId };
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
            c.Allies = new List<string>(Allies);
            c.Loot = Loot.Select(l => l.Clone()).ToList();
            c.Log = new List<string>(Log);
            return c;
        }
    }
}

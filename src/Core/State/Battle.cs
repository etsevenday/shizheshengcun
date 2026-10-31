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
        /// Q80（schema 14）起不再自动压紧：死亡、指令后、轮初都保留空位；只有新战初次生成按名单紧凑排位。
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
        /// <summary>稳态（R03 §4）：控制成功后至此轮末，控制抗性临时+30个百分点；0 为没有。</summary>
        public int SteadyUntilRound { get; set; }
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

        // ───── B7 技能附带状态（R03 §4）与 A09 敌人意图（R26） ─────
        /// <summary>破甲：护甲−ArmorBreak 到 ArmorBreakUntilRound 轮末（最低 0）；同名取强。</summary>
        public double ArmorBreak { get; set; }
        public int ArmorBreakUntilRound { get; set; }
        /// <summary>虚弱：直接伤害×WeakFactor 到 WeakUntilRound 轮末。</summary>
        public double WeakFactor { get; set; } = 1;
        public int WeakUntilRound { get; set; }
        /// <summary>标记：我方对它的攻击命中能力+MarkBonus 到 MarkUntilRound 轮末；同名取强、不叠层。</summary>
        public double MarkBonus { get; set; }
        public int MarkUntilRound { get; set; }
        /// <summary>失衡持续到它下次攻击后（钢制钉锤“震荡”），不在轮末清除。</summary>
        public bool StaggerSticky { get; set; }
        /// <summary>减速额外值（绝缘警棍“打断成功后下轮速度额外−1”，不叠）：本轮施加，下一轮生效（SlowExtraActive），轮末清除。</summary>
        public double SlowExtra { get; set; }
        public double SlowExtraActive { get; set; }
        /// <summary>护盔感染者“首次失衡免疫”已用掉。</summary>
        public bool StaggerImmuneUsed { get; set; }
        /// <summary>加入本场的轮次（意图循环按 Round−JoinRound 取第几次正常激活；0 视为 1）。</summary>
        public int JoinRound { get; set; }
        /// <summary>本轮锁定的意图在意图循环里的序号（opening 与 cycle 合并编号）；−1 为特殊意图（逃离/警戒）。</summary>
        public int IntentStep { get; set; }
        /// <summary>本轮意图的对象（保护意图要保护的同伴本场序号）；0 为没有。</summary>
        public int IntentTarget { get; set; }
        /// <summary>正在保护的同伴（本场序号）；到它下次正常激活开始清除。0 为没有。</summary>
        public int ProtectingIndex { get; set; }
        /// <summary>本场已执行保护的次数（隔离守卫限 1 次）。</summary>
        public int ProtectUses { get; set; }
        /// <summary>下一次攻击伤害×（野犬首领“首轮仅自身下一击×1.2”）；1 为没有。</summary>
        public double NextHitFactor { get; set; } = 1;
        /// <summary>流窜枪手：弹仓与本场剩余备弹（弹药计划总数，打空后不补）。−1 为未初始化。</summary>
        public int Mag { get; set; } = -1;
        public int Reserve { get; set; }

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
        /// <summary>这次防御的直接伤害倍率：普通 0.5，装着未损坏的盾时 0.4（R03 §3）；不叠多个同类格挡。</summary>
        public double PlayerGuardFactor { get; set; } = 0.5;
        /// <summary>本次格挡的来源盾实例（Q69：提交时当前页的盾；通用防御为 null）。授予时锁定，切页不重算；随存档。</summary>
        public int? PlayerGuardSourceId { get; set; }
        /// <summary>本次激活开始时得到的 AP（界面画 AP 格用）：普通激活按训练阶位 3/4/5，追击 2。</summary>
        public int ActivationAp { get; set; }
        public bool PlayerStagger { get; set; }
        public int PlayerStaggerRound { get; set; }
        /// <summary>主角的稳态截止轮（同 BattleEnemy.SteadyUntilRound）。</summary>
        public int PlayerSteadyUntilRound { get; set; }
        /// <summary>
        /// 保护关系（R03 §4）：保护者 actorId → 被保护者 actorId；一名保护者只接一名目标，不允许相互/循环保护。
        /// 保护指令属于 B7 技能、队友参战在 B8；本批只有规则校验（ProtectBlocked），场上不会出现保护。
        /// </summary>
        public Dictionary<string, string> Protections { get; set; } = new Dictionary<string, string>();
        public bool PlayerSlowPending { get; set; }
        public bool PlayerSlowActive { get; set; }

        // ───── B7 技能（A06 §2、R03 §4） ─────
        /// <summary>各技能最近一次使用的大回合（冷却：r 使用 CD1 → r+1 不可用、r+2 可用；两武器页与追击共享）。</summary>
        public Dictionary<string, int> SkillUsedRound { get; set; } = new Dictionary<string, int>();
        /// <summary>主角格挡剩余次数（坚守 2 次；普通防御/护身/稳固 1 次）；与 PlayerGuard 同时生效。</summary>
        public int PlayerGuardCharges { get; set; }
        /// <summary>反击架势：受直接攻击命中后反击 1 次（伤害×CounterFactor），用掉或下次自己正常激活开始清除。CounterPage 为架势所用武器页。</summary>
        public double CounterFactor { get; set; }
        public int CounterPage { get; set; }
        /// <summary>架矛：敌人主动前移后迎击（剩余次数、伤害倍率、武器页），到下次自己正常激活开始清除。</summary>
        public int BraceCharges { get; set; }
        public double BraceFactor { get; set; }
        public int BracePage { get; set; }
        /// <summary>斧蓄力：下次该斧攻击伤害×ChargeFactor，到 ChargeUntilRound 轮末；ChargeItem 为蓄力的斧实例。</summary>
        public double ChargeFactor { get; set; }
        public int ChargeUntilRound { get; set; }
        public int? ChargeItem { get; set; }
        /// <summary>鼓舞：下一次攻击命中能力+InspireBonus，到 InspireUntilRound 轮末。</summary>
        public double InspireBonus { get; set; }
        public int InspireUntilRound { get; set; }
        /// <summary>总攻令：下一次攻击伤害×AllOutFactor，到 AllOutUntilRound 轮末；同名不叠。</summary>
        public double AllOutFactor { get; set; }
        public int AllOutUntilRound { get; set; }
        /// <summary>稳固：位移抵抗+到 DisplaceUntilRound 轮末（Q80 起单人被拉扯/击退也会真实移位，判控制时生效）。</summary>
        public double DisplaceResist { get; set; }
        public int DisplaceUntilRound { get; set; }
        /// <summary>主角被破甲（酸液喷吐）：护甲−到轮末。</summary>
        public double PlayerArmorBreak { get; set; }
        public int PlayerArmorBreakUntilRound { get; set; }
        /// <summary>主角虚弱（伤害×，到轮末）。</summary>
        public double PlayerWeakFactor { get; set; } = 1;
        public int PlayerWeakUntilRound { get; set; }
        /// <summary>下一次受到的直接伤害×（救援医疗箱“稳定”）；1 为没有。</summary>
        public double PlayerNextDamageFactor { get; set; } = 1;
        /// <summary>稳心：同目标每大回合最多一次（记录用过的轮）。</summary>
        public int CalmRound { get; set; }
        /// <summary>本场已发过指挥经验的技能（每场每目标每 skillId 至多 +2，A06 §3）。</summary>
        public List<string> CommandXp { get; set; } = new List<string>();
        /// <summary>鼓舞/总攻令的经验待发：增益被攻击实际用掉时才给（A06 §3“有增益后实际消耗”）。</summary>
        public List<string> PendingCommandXp { get; set; } = new List<string>();
        /// <summary>便携指挥终端“每场首次号令”已用。</summary>
        public bool FirstCommandUsed { get; set; }

        /// <summary>schema 8 的战内当前武器（随身实例引用）。只供 8→9 迁移读取（迁到武器Ⅰ后清空）；schema 9 起当前武器由 ActiveWeaponSlot 指向武器槽。</summary>
        public int? WeaponInstanceId { get; set; }

        /// <summary>当前武器页（1=武器Ⅰ、2=武器Ⅱ；A10 §4.2、B3 H6）：开战为 1，切页 0AP；随战斗存档。</summary>
        public int ActiveWeaponSlot { get; set; } = 1;

        public List<BattleEnemy> Enemies { get; set; } = new List<BattleEnemy>();
        /// <summary>
        /// 队伍容量 C（R03 §1、A11：1—4，含主角）：本场敌人数上限（入战与噪声增援累计都不超过它），战斗建立时锁存。
        /// 新战斗按存档里的真实解锁事实计算（Q62 选 B：新局 C1，见 GameEngine.BattleCapacity）；开战后不随事实变化，旧战斗保持已存值到结束。
        /// </summary>
        public int Capacity { get; set; }
        /// <summary>
        /// 我方真实站位（R03 §1、Q80）：下标=槽号−1 的稀疏表，空位为 ""，末尾空位省略（主角在 1 号位时即 [p0]）；
        /// 单人也可在 2/3/4 号位，不自动归 1。本批只有主角（队友参战在 B8）。
        /// </summary>
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
            c.Protections = new Dictionary<string, string>(Protections);
            c.SkillUsedRound = new Dictionary<string, int>(SkillUsedRound);
            c.CommandXp = new List<string>(CommandXp);
            c.PendingCommandXp = new List<string>(PendingCommandXp);
            c.Loot = Loot.Select(l => l.Clone()).ToList();
            c.Log = new List<string>(Log);
            return c;
        }
    }
}

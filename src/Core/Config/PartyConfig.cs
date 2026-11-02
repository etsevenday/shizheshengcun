using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.Config
{
    /// <summary>
    /// 队伍与共同生存（R08 §1—§5、A11 §5—§7；开发计划 v0.9 批次 B8，R31/R32）。由 tools/import-b8-party.py 写入。
    /// 控制抗性/闪避/基础甲/速度的人物档仍读 actorBaseDefense（Q63），这里不复制第二份。
    /// </summary>
    public sealed class PartyConfig
    {
        /// <summary>A11 §6 六名命名可招募角色。</summary>
        public List<NamedCompanionConfig> NamedCompanions { get; set; } = new List<NamedCompanionConfig>();
        /// <summary>A11 §7 RB01 通用护送员模板。</summary>
        public EscortTemplateConfig EscortTemplate { get; set; } = new EscortTemplateConfig();
        /// <summary>新招募初值（A11 §5.2、R08 §2）。</summary>
        public RecruitStatsConfig RecruitInitialStats { get; set; } = new RecruitStatsConfig();
        /// <summary>候补公共照护（R08 §3、A11 §5.3）。</summary>
        public ReserveCareConfig ReserveCare { get; set; } = new ReserveCareConfig();
        /// <summary>
        /// 营区替身地点（待确认 Q89 选 A）：v3 地图（B11）前编队、候补居住与照护、RB01、借用矛报失都在这里安全空闲办理。
        /// </summary>
        public string HubStandIn { get; set; } = "camp";
        /// <summary>
        /// 收容所服务的替身地点（Q88/Q89 选 A，开发计划 v0.9 B8b）：v3 地图前 RB01 登记与借用矛报失在这里办理；与营区公共居所分成两个配置位。
        /// </summary>
        public string ShelterStandIn { get; set; } = "camp";
        /// <summary>v3 地图的营区公共居所节点（Q88：候补居住与照护、成员换入/留守、公共候补遗物）。地图里有它与 <see cref="ShelterV3"/> 时才迁址。</summary>
        public string HubV3 { get; set; } = "h02_square";
        /// <summary>v3 地图的收容所节点（Q88/Q94：RB01 与借用矛报失、报失补发落地）。</summary>
        public string ShelterV3 { get; set; } = "h06_shelter";
        /// <summary>收容所周期招募（R08 RC1—RC4，开发计划 v0.9 B19，需求 R60/R61）。由 tools/import-b19-recruitment.py 写入。</summary>
        public RecruitmentConfig Recruitment { get; set; } = new RecruitmentConfig();

        public NamedCompanionConfig? Named(string actorId) => NamedCompanions.FirstOrDefault(n => n.ActorId == actorId);
    }

    public sealed class NamedCompanionConfig
    {
        public string ActorId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Background { get; set; } = "";
        /// <summary>列出的技能等级（其余 1 级、级内经验 0）。</summary>
        public Dictionary<string, int> Skills { get; set; } = new Dictionary<string, int>();
        public double BaseSpeed { get; set; } = 4;
        public string Weapon1 { get; set; } = "";
        public string Weapon2 { get; set; } = "";
        public string RecruitTask { get; set; } = "";
        /// <summary>营区工作职能标签（N01 §5、A11 §5，B14）：只改标签，不改名字、速度、武器与技能。</summary>
        public string Affiliation { get; set; } = "";
    }

    public sealed class EscortTemplateConfig
    {
        public string NamePrefix { get; set; } = "护送员";
        public int SkillLevel { get; set; } = 1;
        public double BaseSpeed { get; set; } = 4;
        public string LoanWeapon { get; set; } = "wooden_spear";
        public int RegistrationMinutes { get; set; } = 30;
        /// <summary>同时存活通用护送员上限 = min(MaxAlive, C−1)（R08 RC1）。</summary>
        public int MaxAlive { get; set; } = 3;
    }

    public sealed class RecruitStatsConfig
    {
        public double Health { get; set; } = 20;
        public double HealthMax { get; set; } = 20;
        public double Satiety { get; set; } = 85;
        public double Hydration { get; set; } = 85;
        public double Energy { get; set; } = 18;
        public double Wakefulness { get; set; } = 90;
        public double Morale { get; set; } = 70;
        public double Immunity { get; set; } = 70;
        public double VirusLoad { get; set; }
    }

    public sealed class ReserveCareConfig
    {
        // Q90（策划答复，R08/S13「Q90 休息睡眠与候补自主恢复」本轮章，开发计划 v0.9 B8c）：取代原 N/W 托底 40 与 22:00—06:00 固定作息。
        /// <summary>照护规则版本（存档 GameState.ReserveCareRule；2 = Q90 自主 rest/sleep 与慢速公共补给）。</summary>
        public int RuleVersion { get; set; } = 2;
        /// <summary>段首 S（清醒）低于它或 E（体力）低于 EnterSleepEnergyBelow 时进入睡眠。</summary>
        public double EnterSleepWakefulnessBelow { get; set; } = 60;
        public double EnterSleepEnergyBelow { get; set; } = 6;
        /// <summary>睡眠中段首 S ≥ 它且 E ≥ ExitSleepEnergyAtLeast 才回休息。</summary>
        public double ExitSleepWakefulnessAtLeast { get; set; } = 90;
        public double ExitSleepEnergyAtLeast { get; set; } = 14;
        /// <summary>公共补给（每小时，叠在 rest/sleep 自然消耗上）：N+5.2、W+6.4，该项只补到 PublicNutritionTarget。</summary>
        public double PublicSatietyPerHour { get; set; } = 5.2;
        public double PublicHydrationPerHour { get; set; } = 6.4;
        public double PublicNutritionTarget { get; set; } = 80;
        /// <summary>营区公共关怀 M+1/h，该项只补到 PublicMoraleTarget。</summary>
        public double PublicMoralePerHour { get; set; } = 1;
        public double PublicMoraleTarget { get; set; } = 80;
        /// <summary>红线：H 首次降到 ≤ 它、V 首次升到 ≥ 它各提示一次（A11 §5.3）。</summary>
        public double RedHealth { get; set; } = 5;
        public double RedVirus { get; set; } = 80;
    }

    /// <summary>
    /// 收容所周期招募（R08「2026-10-30 B3 实体收容所、周期招募与战利品owner」RC1—RC4；B19）。
    /// 候选的控制抗性/闪避/基础甲/基础速度读 actorBaseDefense.randomRecruit（R08「Q62—Q74」当前优先节），这里不复制。
    /// </summary>
    public sealed class RecruitmentConfig
    {
        /// <summary>城市服务表里的服务 ID（A11 Q33）。</summary>
        public string ServiceId { get; set; } = "shelter_recruitment";
        /// <summary>策划数据 expansion-combat.json recruitment.serviceId（只作对照）。</summary>
        public string ContractServiceId { get; set; } = "";
        public string LocationId { get; set; } = "h06_shelter";
        /// <summary>开放条件：QMAP01 登记（hub_registered）后首次实际到达收容所。</summary>
        public string OpenedByFact { get; set; } = "hub_registered";
        /// <summary>一期世界分钟（72 小时）。</summary>
        public int RefreshMinutes { get; set; } = 4320;
        public int CandidatesPerCycle { get; set; } = 3;
        /// <summary>登记 wait 分钟（两个 5 分钟段）。</summary>
        public int RegistrationMinutes { get; set; } = 10;
        /// <summary>随机 + RB01 共享名额 K = min(SharedRosterMax, C−1)。</summary>
        public int SharedRosterMax { get; set; } = 3;
        /// <summary>全名册存活硬上限（主角 1 + 固定 6 + 共享 3）。</summary>
        public int RosterAliveCap { get; set; } = 10;
        /// <summary>主熟练等级权重（Lv → 权重）。</summary>
        public Dictionary<string, int> MainLevelWeights { get; set; } = new Dictionary<string, int>();
        /// <summary>副熟练等级权重。</summary>
        public Dictionary<string, int> SubLevelWeights { get; set; } = new Dictionary<string, int>();
        public int OtherLevel { get; set; } = 1;
        public RecruitBodyConfig Body { get; set; } = new RecruitBodyConfig();
        /// <summary>候选名前缀（开发假设 B19-2：仓库没有姓名池，名字 = 前缀 + 模板名 + 三位流水号）。</summary>
        public string NamePrefix { get; set; } = "";
        public List<RecruitTemplateConfig> Templates { get; set; } = new List<RecruitTemplateConfig>();

        public RecruitTemplateConfig? Template(string id) => Templates.FirstOrDefault(t => t.Id == id);
    }

    /// <summary>RC3 候选身体：区间为闭区间整数均匀。</summary>
    public sealed class RecruitBodyConfig
    {
        public int[] Health { get; set; } = { 14, 20 };
        public double HealthMax { get; set; } = 20;
        public int[] Energy { get; set; } = { 10, 16 };
        public double EnergyMax { get; set; } = 20;
        public double Satiety { get; set; } = 40;
        public double Hydration { get; set; } = 40;
        public int[] Wakefulness { get; set; } = { 50, 75 };
        public int[] Morale { get; set; } = { 40, 65 };
        public int[] Immunity { get; set; } = { 60, 75 };
        public double Virus { get; set; }
    }

    /// <summary>RC2 职业模板：主熟练 + 两个副熟练候选（二选一等权）。</summary>
    public sealed class RecruitTemplateConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Main { get; set; } = "";
        public List<string> Subs { get; set; } = new List<string>();
        public string Use { get; set; } = "";
    }
}

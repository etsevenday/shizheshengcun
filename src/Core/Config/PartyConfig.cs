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
        public double SatietyFloor { get; set; } = 40;
        public double HydrationFloor { get; set; } = 40;
        /// <summary>遮蔽睡眠时段（日内分钟，跨午夜）：22:00—06:00。</summary>
        public int SleepStartMinute { get; set; } = 1320;
        public int SleepEndMinute { get; set; } = 360;
        /// <summary>其余时段按什么结算：wait（R08 §3，待确认 Q90 选 A）或 rest（A11 §5.3）。</summary>
        public string AwakeAction { get; set; } = "wait";
        /// <summary>红线：H 首次降到 ≤ 它、V 首次升到 ≥ 它各提示一次（A11 §5.3）。</summary>
        public double RedHealth { get; set; } = 5;
        public double RedVirus { get; set; } = 80;
    }
}

using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Survival.Core.Config
{
    /// <summary>
    /// v3 地图门槛与探索（开发计划 v0.9 批次 B11，R33–R35；S02「Q30B 完整门槛契约」、S03、A08、gate-contract.json、
    /// node-expansion-rules.json；tools/import-b11-map.py）。门槛谓词原样来自 gate-contract.json，由内核按其表达式语言解释。
    /// </summary>
    public sealed class MapConfig
    {
        public string Version { get; set; } = "";
        public string GateContractVersion { get; set; } = "";
        public string ExpansionRulesVersion { get; set; } = "";
        /// <summary>
        /// Q89 服务迁址（B8b）：为 true 时营区/收容所服务迁到 h02_square/h06_shelter。B11 已导入这两个节点，但 register 门槛的事实来源
        /// （营区登记）在 B14，现在迁过去玩家走不到，故保持 false、继续在 camp 代办（开发假设 B11-9）。
        /// </summary>
        public bool ServicesAtV3Nodes { get; set; }
        /// <summary>原标签 → 规范门槛（from_explore_100 → from_explore100）。</summary>
        public Dictionary<string, string> GateAliases { get; set; } = new Dictionary<string, string>();
        public List<GateDefinitionConfig> Gates { get; set; } = new List<GateDefinitionConfig>();
        /// <summary>门槛事实的玩家可见说明（门槛未满足时拼成“需要：……”）。</summary>
        public Dictionary<string, string> FactLabels { get; set; } = new Dictionary<string, string>();
        /// <summary>实际到达成功即提交的事实（gate-contract factProducers：c02 → church_contacted，r02 → academy_contacted）。</summary>
        public Dictionary<string, List<string>> ArrivalFacts { get; set; } = new Dictionary<string, List<string>>();
        /// <summary>新区探索 75% 安全线索日志的引导语（开发方措辞“一条安全线索：”），后接该地点的逐点文案 <see cref="LocationConfig.Clue"/>（Q117 选 B，B13a）。</summary>
        public string ClueText { get; set; } = "";
        /// <summary>旧核心节点 → 新核心节点（legacy-node-claim-migration.json：d01_06→d01_08 等；按 claim 身份迁移，不对同号侧室全局替换，B12 R37）。</summary>
        public Dictionary<string, string> LegacyCoreNodes { get; set; } = new Dictionary<string, string>();
        /// <summary>6 个旧唯一箱的 claimId → 新领取节点（claimId 原样、已领不补；B12 建账，B13c 起按 Q122 在资源表里可领取，officer_cache 改在 d03_07）。</summary>
        public List<LegacyClaimConfig> LegacyClaims { get; set; } = new List<LegacyClaimConfig>();
        /// <summary>地牢 → 核心记录节点。</summary>
        public Dictionary<string, string> RecordNodes { get; set; } = new Dictionary<string, string>();
        public List<DungeonConfig> Dungeons { get; set; } = new List<DungeonConfig>();
        /// <summary>
        /// 13 个世界分区（地点 zone）→ 正式中文名（Q124 选 A，resource-adjudications.json regionNames，B13c）：面包屑与主世界分区导航同源；
        /// 地牢按父入口地点的分区。只是显示名，不改土地归属或建屋许可。
        /// </summary>
        public Dictionary<string, string> RegionNames { get; set; } = new Dictionary<string, string>();
    }

    public static class GateScopes
    {
        public const string Campaign = "campaign";
        public const string CurrentInstance = "current_instance";
        public const string RevalidateCurrentInstance = "revalidate_current_instance";
    }

    public sealed class GateDefinitionConfig
    {
        public string Id { get; set; } = "";
        public GatePredicate Predicate { get; set; } = new GatePredicate();
        /// <summary>campaign：首次满足写永久锁存、正反共享；current_instance：锁存在当前地牢实例（换代丢弃）；revalidate_current_instance：不锁存，每次走边重验。</summary>
        public string UnlockScope { get; set; } = GateScopes.Campaign;
    }

    /// <summary>
    /// gate-contract.json expressionLanguage v1：literal / and / or / fact / visited / exploreAtLeast / instanceFact / contextEquals / if。
    /// 变量 $authoredFrom / $dungeonParent / $dungeonId 从边的 gateContext 绑定（不可绑定当前位置）；未知 op 或未绑定变量报错，不默认放行。
    /// </summary>
    public sealed class GatePredicate
    {
        public string Op { get; set; } = "";
        /// <summary>literal 的布尔、exploreAtLeast 的阈值、contextEquals 的比较值。</summary>
        public JToken? Value { get; set; }
        public List<GatePredicate> Args { get; set; } = new List<GatePredicate>();
        public string? Key { get; set; }
        public string? Node { get; set; }
        /// <summary>exploreAtLeast：campaign 或 current_instance。</summary>
        public string? Scope { get; set; }
        /// <summary>instanceFact：地牢 ID 或 $dungeonId。</summary>
        public string? Dungeon { get; set; }
        public GatePredicate? Condition { get; set; }
        public GatePredicate? Then { get; set; }
        public GatePredicate? Else { get; set; }
    }

    /// <summary>旧唯一箱的迁移账（expansion-combat legacyUniqueRewards + legacy-node-claim-migration claimNodes）。</summary>
    public sealed class LegacyClaimConfig
    {
        public string ClaimId { get; set; } = "";
        public string Node { get; set; } = "";
        public Dictionary<string, int> Grants { get; set; } = new Dictionary<string, int>();
        /// <summary>Q125 正式名（“器材室防暴盾箱”等）；领取在资源表里同 ID 的唯一领取（Q122，B13c）。</summary>
        public string? Name { get; set; }
    }

    public sealed class DungeonConfig
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Parent { get; set; } = "";
        public string GuardNode { get; set; } = "";
        public string RecordNode { get; set; } = "";
        public string ExitNode { get; set; } = "";
        public List<string> SafeNodes { get; set; } = new List<string>();
    }

    /// <summary>
    /// 地牢远征（开发计划 v0.9 B21，R65/R66；R03「2026-10-30 B4」D3/D5.1/D5.2、S12「B4 撤出事件包」、A01 B4 表；tools/import-b21-expedition.py）。
    /// 旧测试配置没有本块时 BufferNodes 为空，远征逻辑整体不生效。
    /// </summary>
    public sealed class DungeonExpeditionConfig
    {
        public string Source { get; set; } = "";
        public AbortPenaltyConfig AbortPenalty { get; set; } = new AbortPenaltyConfig();
        /// <summary>惊魂未定持续世界分钟（R03 D5.1：6 世界小时）。</summary>
        public int ShakenMinutes { get; set; } = 360;
        /// <summary>惊魂未定：下一次入战恐慌初值加值（夹紧 0—100）。</summary>
        public double ShakenPanic { get; set; } = 10;
        /// <summary>撤出最少分钟（R03 D5.2：T=max(30, …)）。</summary>
        public int ExtractMinMinutes { get; set; } = 30;
        /// <summary>撤出时长向上取整的粒度（R03 D5.2：向上取 5 分钟整数）。</summary>
        public int ExtractRoundMinutes { get; set; } = 5;
        public ExpeditionWarnConfig Warn { get; set; } = new ExpeditionWarnConfig();
        /// <summary>每座地牢的入口缓冲房（R03 D3“01 是实例内安全缓冲”）：地牢 ID → 节点 ID。</summary>
        public Dictionary<string, string> BufferNodes { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>主动终止远征的单次惩罚包（每名仍存活同行各一次）：五项负值（最低 0），健康实扣 min(HealthMax, max(0, H−HealthKeep))。</summary>
    public sealed class AbortPenaltyConfig
    {
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Energy { get; set; }
        public double Wakefulness { get; set; }
        public double Morale { get; set; }
        public double HealthMax { get; set; }
        public double HealthKeep { get; set; }
    }

    /// <summary>进入预告的警告线（R03 D3）：N/W&lt;40、E&lt;8、S&lt;40、M&lt;30、H≤5、V≥80。</summary>
    public sealed class ExpeditionWarnConfig
    {
        public double SatietyBelow { get; set; }
        public double HydrationBelow { get; set; }
        public double EnergyBelow { get; set; }
        public double WakefulnessBelow { get; set; }
        public double MoraleBelow { get; set; }
        public double HealthAtMost { get; set; }
        public double VirusAtLeast { get; set; }
    }
    /// <summary>
    /// 地牢奖励分账与重置（B22，A09「E6.2」「E6.3」「E7」「E8」、R03 D8、A07 X1.13；tools/import-b22-rewards.py）。地图箱（E6.1）与旧唯一箱（E6.4）
    /// 直接复用资源表同一 resourceId，不在这里重复。
    /// </summary>
    public sealed class DungeonRewardsConfig
    {
        public string Source { get; set; } = "";
        /// <summary>通关后到可重置的世界分钟（72 世界小时，从本代 clearedAt 起算）。</summary>
        public int ResetCooldownMinutes { get; set; } = 4320;
        /// <summary>每代守卫战最终胜利后的固定回收材料（instance_once，键 instanceId:守卫节点:combat_salvage）。</summary>
        public List<DungeonPackConfig> BossSalvage { get; set; } = new List<DungeonPackConfig>();
        /// <summary>本局首次通关补给包（campaign_once，键 campaignId:地牢:combat_first），在 recordNode 领取/核对成功时发。</summary>
        public List<DungeonPackConfig> FirstClear { get; set; } = new List<DungeonPackConfig>();
        /// <summary>三种战术品（flash_marker / brace_pack / rally_patch）。</summary>
        public List<TacticalItemConfig> Tactical { get; set; } = new List<TacticalItemConfig>();
    }

    /// <summary>一份固定材料包：所属地牢、发放节点（守卫节点或核心记录节点）与物品。</summary>
    public sealed class DungeonPackConfig
    {
        public string DungeonId { get; set; } = "";
        public string NodeId { get; set; } = "";
        public string ClearNode { get; set; } = "";
        public Dictionary<string, int> Items { get; set; } = new Dictionary<string, int>();
        /// <summary>发放节点：守卫回收为 NodeId，首通包为 ClearNode。</summary>
        public string Node => NodeId != "" ? NodeId : ClearNode;
    }

    /// <summary>战术品（A09 E7）：战斗指令 ID、目标类别（enemy/self/ally）、AP/E/噪声与效果数值。</summary>
    public sealed class TacticalItemConfig
    {
        public string Id { get; set; } = "";
        public string Action { get; set; } = "";
        public string Target { get; set; } = "";
        public int Ap { get; set; }
        public double Energy { get; set; }
        public double Noise { get; set; }
        public double MarkBonus { get; set; }
        public int Rounds { get; set; }
        public double DisplaceResist { get; set; }
        public double PanicReduce { get; set; }
        public double PerTargetRoundCap { get; set; }
        public string Text { get; set; } = "";
    }
}

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
        /// <summary>6 个旧唯一箱的 claimId → 新领取节点（claimId 原样、已领不补；B12 只建账，领取动作待 Q122）。</summary>
        public List<LegacyClaimConfig> LegacyClaims { get; set; } = new List<LegacyClaimConfig>();
        /// <summary>地牢 → 核心记录节点。</summary>
        public Dictionary<string, string> RecordNodes { get; set; } = new Dictionary<string, string>();
        public List<DungeonConfig> Dungeons { get; set; } = new List<DungeonConfig>();
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
}

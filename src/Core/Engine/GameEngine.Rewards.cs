using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 地牢四类奖励分账（开发计划 v0.9 批次 B22，需求 R67；A09「E6.1」—「E6.4」「每地牢每代固定重复产出合计」、R03 D7.2「奖励时序」、D8）：
    /// <list type="bullet">
    /// <item>地图箱（E6.1）：24 个实例侧箱按 instanceId、8 份核心记录按 campaignClaimId——直接是资源表同一 resourceId（B12 起），这里只给产物记来源。</item>
    /// <item>守卫回收材料（E6.2）：本代守卫固定战最终胜利、唯一 tick 后主角存活才提交，键 instanceId:守卫节点:combat_salvage，落战斗地点地面。</item>
    /// <item>首通补给包（E6.3）：recordNode 领取/核对事务成功（即通关）时，键 campaignId:地牢:combat_first 本局一次，落核心记录房地面；与记录分开。</item>
    /// <item>旧唯一箱（E6.4）：B13c 已按原 claimId 迁到 d03，跨代不复活。</item>
    /// </list>
    /// 通关（clearedAt）与 72 小时（resetEligibleAt）也在这里记（R03 D2、Q127：领取或核对核心记录的原 10 分钟事务成功即通关，只杀守卫不算）。
    /// </summary>
    public sealed partial class GameEngine
    {
        private DungeonRewardsConfig DR => Config.DungeonRewards;

        /// <summary>首通补给包的本局唯一键（A09 E6.3 “campaignId:d01:combat_first”，写进 Progress.Flags）。</summary>
        public static string FirstClearKey(string dungeonId) => CampaignKey(dungeonId + ":combat_first");

        /// <summary>守卫回收材料的本代一次键（A09 E6.2 “instanceId:d01_07:combat_salvage”，用真实 instanceId，写进 AppliedEventKeys）。</summary>
        public string SalvageKey(GameState s, string dungeonId, string guardNode) => $"{InstanceId(s, dungeonId)}:{guardNode}:combat_salvage";

        /// <summary>该地点产出的地牢奖励应记的来源（R03 D7.1 封存白名单）：启用远征的地牢房间 → 当前 instanceId；其余为 null。</summary>
        public string? RewardOriginAt(GameState s, string locationId) =>
            DungeonAt(locationId) is string d && ExpeditionEnabled(d) ? InstanceId(s, d) : null;

        /// <summary>某地牢的核心记录资源点（recordNode 上的 campaignClaimId 记录）；没有为 null。</summary>
        public ResourceConfig? RecordResourceOf(string dungeonId) => Config.Resources.FirstOrDefault(r => r.IsRecord && DungeonOf(r) == dungeonId);

        /// <summary>首通补给包配置；没有为 null。</summary>
        public DungeonPackConfig? FirstClearOf(string dungeonId) => DR.FirstClear.FirstOrDefault(x => x.DungeonId == dungeonId);

        /// <summary>守卫回收配置；没有为 null。</summary>
        public DungeonPackConfig? SalvageOf(string dungeonId) => DR.BossSalvage.FirstOrDefault(x => x.DungeonId == dungeonId);

        /// <summary>本局首通补给包是否已领（含旧档迁移为“已领”的）。</summary>
        public bool FirstClearClaimed(GameState s, string dungeonId) => s.Progress.Flags.Contains(FirstClearKey(dungeonId));

        /// <summary>本代守卫回收是否已发。</summary>
        public bool SalvageClaimed(GameState s, string dungeonId) =>
            SalvageOf(dungeonId) is DungeonPackConfig p && s.AppliedEventKeys.Contains(SalvageKey(s, dungeonId, p.Node));

        /// <summary>物品清单短语“布料+3，线+2”。</summary>
        public string PackText(IReadOnlyDictionary<string, int> items) => string.Join("，", items.Select(kv => $"{ItemName(kv.Key)}+{kv.Value}"));

        /// <summary>
        /// 记本代通关（R03 D2：原子写 clearedAt 并设 resetEligibleAt=clearedAt+4320）：只在本代第一次写；已记过的不改（核对再走、走出口都不第二次写）。
        /// 返回是否这次新记。
        /// </summary>
        private bool MarkCleared(GameState s, string dungeonId)
        {
            var inst = EnsureInstance(s, dungeonId);
            inst.Facts.Add(RecordClaimedFact);
            if (inst.ClearedAt.HasValue) return false;
            inst.ClearedAt = s.GameMinute;
            inst.ResetEligibleAt = s.GameMinute + DR.ResetCooldownMinutes;
            return true;
        }

        /// <summary>
        /// 首通补给包（A09 E6.3）：通关事务里、本局还没领过时整包生成在 ground（核心记录房地面），记来源（可封存）与本局唯一键；返回日志短语，没有为 null。
        /// 不再赠与同一把枪/知识/任务物；旧档已有通关者迁移时直接记为已领（不追补）。
        /// </summary>
        private string? GrantFirstClear(GameState s, string dungeonId, string ground)
        {
            var p = FirstClearOf(dungeonId);
            if (p == null || FirstClearClaimed(s, dungeonId)) return null;
            var origin = RewardOriginAt(s, ground) ?? InstanceId(s, dungeonId);
            foreach (var kv in p.Items) CreateItems(s, kv.Key, kv.Value, ground, rewardOrigin: origin);
            s.Progress.Flags.Add(FirstClearKey(dungeonId));
            return $"本局首次通关补给：{PackText(p.Items)}，已放在{ZoneName(s, ground)}地面（只发这一次，换代不再发）";
        }

        /// <summary>
        /// 守卫回收材料（A09 E6.2）：本代守卫固定战最终胜利、唯一 tick 后主角存活（FinishBattle 已先结算 tick）时，按本代 instance 一次键落战斗地点地面并记来源；
        /// 撤退、未结束、旧代都不发。返回日志短语，没有为 null。
        /// </summary>
        private string? GrantBossSalvage(GameState s, string dungeonId, string node)
        {
            var p = SalvageOf(dungeonId);
            if (p == null || p.Node != node || s.Progress.Dead) return null;
            var key = SalvageKey(s, dungeonId, node);
            if (!s.AppliedEventKeys.Add(key)) return null;
            foreach (var kv in p.Items) CreateItems(s, kv.Key, kv.Value, node, rewardOrigin: InstanceId(s, dungeonId));
            return $"守卫回收材料：{PackText(p.Items)}，已放在{LocName(node)}地面（本代一次）";
        }

        // ───────────────────────── 存档 33→34（B22） ─────────────────────────

        /// <summary>
        /// 存档 33→34（开发假设 B22-9）：本代已核对核心记录（已通关）的实例补 clearedAt=升级时刻、resetEligibleAt=+4320（旧档没有通关时刻，不倒推、不提前可重置）；
        /// 本局已领核心记录的地牢把首通补给包记为已领、不追补（A09 E6.3“已有旧版通关者迁移为 combat_first 已领”）；不发任何物资、不加时间。返回说明。幂等。
        /// </summary>
        public List<string> MigrateRewards33(GameState s)
        {
            var notes = new List<string>();
            var cleared = new List<string>();
            foreach (var kv in s.DungeonInstances)
            {
                if (!kv.Value.Facts.Contains(RecordClaimedFact) || kv.Value.ClearedAt.HasValue) continue;
                kv.Value.ClearedAt = s.GameMinute;
                kv.Value.ResetEligibleAt = s.GameMinute + DR.ResetCooldownMinutes;
                cleared.Add(DungeonCfg(kv.Key)?.Name ?? kv.Key);
            }
            if (cleared.Count > 0) notes.Add($"{string.Join("、", cleared)}本代已通关：72小时重置从这次升级时起算");
            var firsts = new List<string>();
            foreach (var d in Config.Map.Dungeons)
            {
                var rec = RecordResourceOf(d.Id);
                if (rec == null || !CampaignClaimed(s, rec.NodeId) || FirstClearClaimed(s, d.Id) || FirstClearOf(d.Id) == null) continue;
                s.Progress.Flags.Add(FirstClearKey(d.Id));
                firsts.Add(d.Name);
            }
            if (firsts.Count > 0) notes.Add($"{string.Join("、", firsts)}的首次通关补给按已领取处理（旧版已通关，不追补）");
            return notes;
        }
    }
}

using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 资源四账（开发计划 v0.9 批次 B12，R36/R37；S04「四账及奖励防重」、S05「定量采集与四生命周期」「唯一与迁移」、A08）：
    /// world_stock 走资源状态库存与午夜日补；地牢普通箱按实例代号记账；campaignClaimId 写 campaignId:&lt;resourceId&gt;（单局永久一次）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>单局唯一领取键（A09 表“campaignId:d01_08_unique”）。</summary>
        public static string CampaignKey(string resourceId) => "campaignId:" + resourceId;

        /// <summary>知识授予用的领取事实（config knowledge grantedByFacts “claim:d03_08_unique”）。</summary>
        public static string ClaimKey(string resourceId) => "claim:" + resourceId;

        /// <summary>某唯一领取是否已领（本局永久）。</summary>
        public bool CampaignClaimed(GameState s, string resourceId) => s.Progress.Flags.Contains(CampaignKey(resourceId));
    }
}

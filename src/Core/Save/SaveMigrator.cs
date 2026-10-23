using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Save
{
    /// <summary>
    /// 0.2-proto-1（设计文档里的 schemaVersion 2）→ 3 的存档升级（00 案第 5 节）。只对旧版执行，已是 3 的档不重复迁移。
    /// 不补发历史探索奖励，不把旧水卡倒进锅里。
    /// </summary>
    public static class SaveMigrator
    {
        public const string LegacySchema = "0.2-proto-1";

        public static void Migrate(GameState s, GameConfig config)
        {
            if (s.SchemaVersion == GameState.CurrentSchema) return;

            // 各地清理次数按容量初始化，恢复累计 0，更新时间为旧档当前世界分钟
            foreach (var l in config.Locations)
            {
                if (!s.Locations.TryGetValue(l.Id, out var ls)) continue;
                ls.ClearCharges = l.Clear.Cap;
                ls.ClearRefillProgress = 0;
                ls.ClearUpdatedMinute = s.GameMinute;
                ls.Facility.AttachedPotId = null;
            }

            var potIds = new HashSet<int>(s.Items
                .Where(i => config.Items.Any(c => c.Id == i.ItemId && c.ContainerId != null))
                .Select(i => i.InstanceId));
            foreach (var a in new[] { s.Main, s.Sub })
            {
                if (a == null) continue;
                // 旧活动探索按旧流程完成一次，不掉落
                if (a.Type == ActionType.Explore) a.LegacyNoLoot = true;
                // 旧活动烧水：完成仍在地面生成净水，取消返还原料；锅在旧活动结束前保持锁定（且是空锅）
                var rc = config.Recipes.FirstOrDefault(r => r.Id == a.TargetId);
                if (a.Type == ActionType.Craft && rc?.Pot != null)
                {
                    a.LegacyGroundOutput = true;
                    a.PotInstanceId = a.LockedItemIds.Where(potIds.Contains).Select(id => (int?)id).FirstOrDefault();
                }
            }

            s.Layout = new Dictionary<string, List<string>>();
            s.RecentRequestIds = new List<string>();
            s.SchemaVersion = GameState.CurrentSchema;
        }
    }
}

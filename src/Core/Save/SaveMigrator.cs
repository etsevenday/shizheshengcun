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
                // 旧活动烧水（脏水是地面散卡、锅为空）：标记旧流程，交给 v0.4 迁移统一取消并返还
                if (a.Type == ActionType.Craft && a.TargetId == "boil")
                {
                    a.LegacyGroundOutput = true;
                    a.PotInstanceId = a.LockedItemIds.Where(potIds.Contains).Select(id => (int?)id).FirstOrDefault();
                }
            }

            // 旧烧水已不是角色配方：取消，把预留的地面脏水一次返还当前地面，不生成净水、不自动装锅
            foreach (var a in new[] { s.Sub, s.Main })
            {
                if (a == null || !a.LegacyGroundOutput) continue;
                CancelRetiredCraft(s, a, "旧版烧水已取消：预留的脏水已返还当前地面。请把脏水装进金属锅、放进燃烧的火堆自动烧开。");
            }

            s.Layout = new Dictionary<string, List<string>>();
            s.RecentRequestIds = new List<string>();
            s.SchemaVersion = GameState.CurrentSchema;
        }

        /// <summary>取消已废止的旧配方行动：预留物品一次返还当前地面，解除锁定，写日志。</summary>
        private static void CancelRetiredCraft(GameState s, ActionState a, string text)
        {
            foreach (var id in a.ReservedItemIds)
            {
                var inst = s.Items.FirstOrDefault(i => i.InstanceId == id);
                if (inst == null) continue;
                inst.ReservedBy = null;
                inst.Owner = s.CurrentLocation;
            }
            a.ReservedItemIds.Clear();
            a.LockedItemIds.Clear();
            if (ReferenceEquals(s.Sub, a)) s.Sub = null;
            if (ReferenceEquals(s.Main, a)) s.Main = null;
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = text });
        }
    }
}

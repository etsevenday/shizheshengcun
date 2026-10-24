using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Save
{
    /// <summary>旧档无法无损升级（不同锅、同一物品多处出现、火堆容量不够）：保留原档、报告冲突，禁止凭默认顺序挑锅或丢弃内容物。</summary>
    public sealed class SaveMigrationConflictException : Exception
    {
        public IReadOnlyList<string> Conflicts { get; }

        public SaveMigrationConflictException(IReadOnlyList<string> conflicts)
            : base(string.Join("；", conflicts))
        {
            Conflicts = conflicts;
        }
    }

    /// <summary>
    /// 存档升级链：0.2-proto-1（设计文档里的 schemaVersion 2）→ 3（交互与地图扩展）→ 4（卡片统一与设施烹饪）。
    /// 只对旧版执行，已是 4 的档不重复迁移。不补发历史奖励、不自动补水、不凭默认顺序挑锅。
    /// </summary>
    public static class SaveMigrator
    {
        public const string LegacySchema = "0.2-proto-1";
        public const string Schema3 = "3";

        /// <summary>升级到当前版本，返回给玩家看的迁移说明（无变化时为空）。冲突时抛 SaveMigrationConflictException，状态不变。</summary>
        public static List<string> Migrate(GameState s, GameConfig config)
        {
            var notes = new List<string>();
            if (s.SchemaVersion == GameState.CurrentSchema) return notes;
            if (s.SchemaVersion == LegacySchema)
            {
                // 先在副本上整条链试跑：3→4 有冲突时原状态一点不改
                var probe = s.Clone();
                From02To3(probe, config);
                CheckConflicts(probe, config);
                From02To3(s, config);
            }
            if (s.SchemaVersion == Schema3) From3To4(s, config, notes);
            return notes;
        }

        // ───────────────────────── 0.2-proto-1 → 3 ─────────────────────────

        /// <summary>交互与地图扩展 00 案第 5 节：清理次数按上限初始化；旧活动探索不掉落；旧活动烧水标记旧流程（3→4 统一取消）。</summary>
        private static void From02To3(GameState s, GameConfig config)
        {
            foreach (var l in config.Locations)
            {
                if (!s.Locations.TryGetValue(l.Id, out var ls)) continue;
                ls.ClearCharges = l.Clear.Cap;
                ls.ClearRefillProgress = 0;
                ls.ClearUpdatedMinute = s.GameMinute;
                ls.Facility.AttachedPotId = null;
            }

            var potIds = PotIds(s, config);
            foreach (var a in new[] { s.Main, s.Sub })
            {
                if (a == null) continue;
                if (a.Type == ActionType.Explore) a.LegacyNoLoot = true;
                // 首版烧水：脏水是地面散卡、锅为空
                if (a.Type == ActionType.Craft && a.TargetId == "boil")
                {
                    a.LegacyGroundOutput = true;
                    a.PotInstanceId = a.LockedItemIds.Where(potIds.Contains).Select(id => (int?)id).FirstOrDefault();
                }
            }

            s.Layout = new Dictionary<string, List<string>>();
            s.RecentRequestIds = new List<string>();
            s.SchemaVersion = Schema3;
        }

        // ───────────────────────── 3 → 4 ─────────────────────────

        private static HashSet<int> PotIds(GameState s, GameConfig config) => new HashSet<int>(s.Items
            .Where(i => config.Items.Any(c => c.Id == i.ItemId && c.ContainerId != null))
            .Select(i => i.InstanceId));

        private static string FireOwner(string loc) => ItemInstance.FacilityOwner(loc + ":fire");

        private static bool IsOldBoil(ActionState? a) => a != null && a.Type == ActionType.Craft && a.TargetId == "boil" && !a.LegacyGroundOutput;

        /// <summary>某火堆要放进设施的锅：锅位关联（锅须仍在该地面）与进行中烧水用的锅。</summary>
        private static List<int> PotsForFire(GameState s, string loc, HashSet<int> potIds)
        {
            var list = new List<int>();
            var fac = s.Locations[loc].Facility;
            if (fac.AttachedPotId is int a && potIds.Contains(a) && s.Items.First(i => i.InstanceId == a).Owner == loc) list.Add(a);
            foreach (var act in new[] { s.Main, s.Sub })
                if (IsOldBoil(act) && act!.SourceLocation == loc && act.PotInstanceId is int b && !list.Contains(b)) list.Add(b);
            return list;
        }

        /// <summary>不改状态，只找冲突（00 案第 6 节）。</summary>
        private static void CheckConflicts(GameState s, GameConfig config)
        {
            if (s.SchemaVersion != Schema3) return;
            var conflicts = new List<string>();
            foreach (var g in s.Items.GroupBy(i => i.InstanceId).Where(g => g.Count() > 1))
                conflicts.Add($"物品#{g.Key}在存档里出现{g.Count()}次（同一物品多处归属）");
            var potIds = PotIds(s, config);
            var claimed = new Dictionary<int, string>();
            foreach (var l in config.Locations.Where(l => l.HasFire))
            {
                if (!s.Locations.TryGetValue(l.Id, out var ls)) continue;
                var fac = ls.Facility;
                var attached = fac.AttachedPotId is int a && potIds.Contains(a) && s.Items.First(i => i.InstanceId == a).Owner == l.Id ? a : (int?)null;
                foreach (var act in new[] { s.Main, s.Sub }.Where(x => IsOldBoil(x) && x!.SourceLocation == l.Id))
                {
                    if (act!.PotInstanceId is not int b) { conflicts.Add("进行中的烧水没有记录所用的锅"); continue; }
                    var pot = s.Items.FirstOrDefault(i => i.InstanceId == b);
                    if (pot == null) { conflicts.Add($"进行中的烧水所用金属锅#{b}已不在存档里"); continue; }
                    if (!(pot.Owner == ItemInstance.Carried || pot.Owner == l.Id))
                        conflicts.Add($"进行中的烧水所用金属锅#{b}不在{l.Name}，无法放进原火堆");
                    if (attached.HasValue && attached.Value != b)
                        conflicts.Add($"{l.Name}火堆锅位上是金属锅#{attached}，而进行中的烧水用的是金属锅#{b}，不能凭默认顺序挑一把");
                }
                var pots = PotsForFire(s, l.Id, potIds);
                if (pots.Count > config.Fire.MaxPots || pots.Count > config.Fire.ProcessingSlots)
                    conflicts.Add($"{l.Name}火堆最多容纳{config.Fire.MaxPots}口锅，旧档需要放入{pots.Count}口");
                foreach (var p in pots)
                {
                    if (claimed.TryGetValue(p, out var other)) conflicts.Add($"金属锅#{p}同时关联{other}与{l.Name}的火堆");
                    else claimed[p] = l.Name;
                }
            }
            if (conflicts.Count > 0) throw new SaveMigrationConflictException(conflicts.Distinct().ToList());
        }

        /// <summary>
        /// 设施与烹饪 00 案第 6 节：已架锅归属火堆（保留锅 ID、水与到期，去掉原区域卡片顺序，每锅只出现一次）；
        /// 进行中的烧水 → 锅移入原火堆、已完成工作分钟转为受热分钟、释放角色主行动、不补水；
        /// 进行中的烹饪与首版旧烧水 → 取消，预留原料一次返还当前地面并提示改用新方式，不补发成品。
        /// </summary>
        private static void From3To4(GameState s, GameConfig config, List<string> notes)
        {
            CheckConflicts(s, config);
            var potIds = PotIds(s, config);
            var boilCfg = config.Processes.FirstOrDefault(p => p.Target == ProcessTargets.PotLiquidBatch);
            var here = s.CurrentLocation;
            string LocName(string id) => config.Locations.FirstOrDefault(l => l.Id == id)?.Name ?? id;
            string ItemName(string id) => config.Items.FirstOrDefault(i => i.Id == id)?.Name ?? id;

            foreach (var l in config.Locations.Where(l => l.HasFire))
            {
                if (!s.Locations.TryGetValue(l.Id, out var ls)) continue;
                var fac = ls.Facility;
                var pots = PotsForFire(s, l.Id, potIds);
                fac.FacilityId = l.Id + ":fire";
                fac.ProcessingSlots = Enumerable.Repeat((int?)null, config.Fire.ProcessingSlots).ToList();
                fac.Processes = new List<FacilityProcess>();
                fac.Completed = new List<string>();
                var k = 0;
                foreach (var id in pots)
                {
                    var pot = s.Items.First(i => i.InstanceId == id);
                    var oldOwner = pot.Owner;
                    pot.Owner = FireOwner(l.Id);
                    pot.PlacedOrder = s.NextProcessSeq++;
                    fac.ProcessingSlots[k++] = id;
                    if (s.Layout.TryGetValue(oldOwner, out var order)) order.RemoveAll(key => key.EndsWith($"|{pot.ItemId}|{id}"));
                    notes.Add($"金属锅#{id}原先架在{l.Name}火堆上，现在归火堆所有，在地点栏的火堆卡里查看");
                }

                foreach (var act in new[] { s.Sub, s.Main }.Where(x => IsOldBoil(x) && x!.SourceLocation == l.Id).ToList())
                {
                    var potId = act!.PotInstanceId!.Value;
                    foreach (var id in act.ReservedItemIds)
                    {
                        var w = s.Items.FirstOrDefault(i => i.InstanceId == id);
                        if (w != null) w.ReservedBy = null;
                    }
                    var dirty = s.Items.Any(i => i.Owner == ItemInstance.ContainerOwner(potId) && i.ItemId == boilCfg?.InputId);
                    if (boilCfg != null && dirty)
                    {
                        fac.Processes.Add(new FacilityProcess
                        {
                            ProcessId = $"{boilCfg.Id}#{s.NextProcessSeq++}",
                            RecipeId = boilCfg.Id,
                            TargetInstanceId = potId,
                            PotInstanceId = potId,
                            HeatDoneMinutes = Math.Min(boilCfg.HeatMinutes - 1, (int)Math.Floor(act.WorkDone)),
                            RequiredHeatMinutes = boilCfg.HeatMinutes,
                            LastWorldMinute = s.GameMinute,
                        });
                    }
                    ClearAction(s, act);
                    notes.Add($"进行中的烧水已转为火堆自动加工：金属锅#{potId}留在{l.Name}火堆里，已烧{Math.Floor(act.WorkDone)}分钟计入；角色行动已释放，不补水");
                }
                fac.AttachedPotId = null;
            }

            foreach (var act in new[] { s.Sub, s.Main }.Where(a => a != null && a.Type == ActionType.Craft
                         && (a.TargetId == "cook" || (a.TargetId == "boil" && a.LegacyGroundOutput))).ToList())
            {
                var back = new List<string>();
                foreach (var id in act!.ReservedItemIds)
                {
                    var inst = s.Items.FirstOrDefault(i => i.InstanceId == id);
                    if (inst == null) continue;
                    inst.ReservedBy = null;
                    inst.Owner = here;
                    back.Add($"{ItemName(inst.ItemId)}×{inst.Count}");
                }
                ClearAction(s, act);
                var what = back.Count == 0 ? "" : $"预留的{string.Join("、", back)}已返还{LocName(here)}地面";
                notes.Add(act.TargetId == "cook"
                    ? $"旧烹饪已取消，{what}。新方式：生块根放进燃烧火堆的加工位烤熟，或放进有净水的锅里水煮"
                    : $"旧版烧水已取消，{what}。新方式：把脏水装进金属锅、放进燃烧的火堆自动烧开");
            }

            foreach (var n in notes) s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = n + "。" });
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = "存档已升级到版本4（卡片统一与设施烹饪）。" });
            s.SchemaVersion = GameState.CurrentSchema;
        }

        private static void ClearAction(GameState s, ActionState a)
        {
            a.ReservedItemIds.Clear();
            a.LockedItemIds.Clear();
            if (ReferenceEquals(s.Sub, a)) s.Sub = null;
            if (ReferenceEquals(s.Main, a)) s.Main = null;
        }
    }
}

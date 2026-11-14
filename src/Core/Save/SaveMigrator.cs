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

        /// <summary>
        /// 玩家看的冲突明细（S01「B40 Q403–Q411 第二轮定稿」Q403 第二张表）：按现有检查顺序，每句已带句末标点、相同句已去重；
        /// 不带内部实例编号。空表示没有可确认的具体明细（用 Q403 的通用冲突句）。
        /// </summary>
        public IReadOnlyList<string> PlayerReasons { get; }

        public SaveMigrationConflictException(IReadOnlyList<string> conflicts)
            : this(conflicts, Array.Empty<string>())
        {
        }

        public SaveMigrationConflictException(IReadOnlyList<string> conflicts, IReadOnlyList<string> playerReasons)
            : base(string.Join("；", conflicts))
        {
            Conflicts = conflicts;
            PlayerReasons = playerReasons;
        }
    }

    /// <summary>
    /// 存档升级链：0.2-proto-1（设计文档里的 schemaVersion 2）→ 3（交互与地图扩展）→ 4（卡片统一与设施烹饪）→ 5（人物与战斗 v0.6）
    /// → 6（系统策划总册 v1.0 答复：锅无耐久、旧档不补新剧情）→ 7（策划提交 9fdb44f：对话记录与等待烹饪）→ 8（策划提交 5493951：烹饪受热保护）
    /// → 9（策划提交 f172677 B3：主角个人状态进 Actors.p0、旧单装备引用迁到武器槽 1）→ 10（B4：旧装备记白色、0 词条）→ 11（B5：十五门十级熟练度，旧刀斧复制为刀剑与斧、等级截到 10）。
    /// 只对旧版执行，已是当前版本的档不重复迁移。不补发历史奖励、不自动补水、不凭默认顺序挑锅、H/E 只缩放一次。
    /// </summary>
    public static class SaveMigrator
    {
        public const string LegacySchema = "0.2-proto-1";
        public const string Schema3 = "3";
        public const string Schema4 = "4";
        public const string Schema5 = "5";
        public const string Schema6 = "6";
        public const string Schema7 = "7";
        public const string Schema8 = "8";
        public const string Schema9 = "9";
        public const string Schema10 = "10";
        public const string Schema11 = "11";
        public const string Schema12 = "12";
        public const string Schema13 = "13";
        public const string Schema14 = "14";
        public const string Schema15 = "15";
        public const string Schema16 = "16";
        public const string Schema17 = "17";
        public const string Schema18 = "18";
        public const string Schema19 = "19";
        public const string Schema20 = "20";
        public const string Schema21 = "21";
        public const string Schema22 = "22";
        public const string Schema23 = "23";
        public const string Schema24 = "24";
        public const string Schema25 = "25";
        public const string Schema26 = "26";
        public const string Schema27 = "27";
        public const string Schema28 = "28";
        public const string Schema29 = "29";
        public const string Schema30 = "30";
        public const string Schema31 = "31";
        public const string Schema32 = "32";
        public const string Schema33 = "33";
        public const string Schema34 = "34";
        public const string Schema35 = "35";
        public const string Schema36 = "36";
        public const string Schema37 = "37";
        public const string Schema38 = "38";
        public const string Schema39 = "39";
        public const string Schema40 = "40";
        public const string Schema41 = "41";
        public const string Schema42 = "42";
        public const string Schema43 = "43";
        public const string Schema44 = "44";
        public const string Schema45 = "45";
        public const string Schema46 = "46";
        public const string Schema47 = "47";
        public const string Schema48 = "48";
        public const string Schema49 = "49";
        /// <summary>schema 11 以前（B6 之前）的单场敌人上限 3：迁移进行中的旧战斗时作为它锁存的容量 C（Q62：旧战斗保持到结束）。</summary>
        public const int LegacyBattleCapacity = 3;

        /// <summary>升级到当前版本，返回给玩家看的迁移说明（无变化时为空）。冲突时抛 SaveMigrationConflictException，状态不变。</summary>
        public static List<string> Migrate(GameState s, GameConfig config)
        {
            var notes = new List<string>();
            // 配置新增地点时（B11 v3 地图起）当前版本的档也补地点状态；只补缺的，已有的不动（幂等）
            if (s.SchemaVersion == GameState.CurrentSchema) { EnsureStates(s, config); return notes; }
            if (s.SchemaVersion == LegacySchema)
            {
                // 先在副本上整条链试跑：3→4 有冲突时原状态一点不改
                var probe = s.Clone();
                From02To3(probe, config);
                CheckConflicts(probe, config);
                From02To3(s, config);
            }
            if (s.SchemaVersion == Schema3) From3To4(s, config, notes);
            if (s.SchemaVersion == Schema4) From4To5(s, config, notes);
            if (s.SchemaVersion == Schema5) From5To6(s, config, notes);
            if (s.SchemaVersion == Schema6) From6To7(s, config, notes);
            if (s.SchemaVersion == Schema7) From7To8(s, config, notes);
            if (s.SchemaVersion == Schema8) From8To9(s, config, notes);
            if (s.SchemaVersion == Schema9) From9To10(s, config, notes);
            if (s.SchemaVersion == Schema10) From10To11(s, config, notes);
            if (s.SchemaVersion == Schema11) From11To12(s, config, notes);
            if (s.SchemaVersion == Schema12) From12To13(s, notes);
            if (s.SchemaVersion == Schema13) From13To14(s, config, notes);
            if (s.SchemaVersion == Schema14) From14To15(s, notes);
            if (s.SchemaVersion == Schema15) From15To16(s, config, notes);
            if (s.SchemaVersion == Schema16) From16To17(s, config, notes);
            if (s.SchemaVersion == Schema17) From17To18(s, config, notes);
            if (s.SchemaVersion == Schema18) From18To19(s, config, notes);
            if (s.SchemaVersion == Schema19) From19To20(s, config, notes);
            if (s.SchemaVersion == Schema20) From20To21(s, config, notes);
            if (s.SchemaVersion == Schema21) From21To22(s, config, notes);
            if (s.SchemaVersion == Schema22) From22To23(s, config, notes);
            if (s.SchemaVersion == Schema23) From23To24(s, config, notes);
            if (s.SchemaVersion == Schema24) From24To25(s, config, notes);
            if (s.SchemaVersion == Schema25) From25To26(s, config, notes);
            if (s.SchemaVersion == Schema26) From26To27(s, config, notes);
            if (s.SchemaVersion == Schema27) From27To28(s, config, notes);
            if (s.SchemaVersion == Schema28) From28To29(s, config, notes);
            if (s.SchemaVersion == Schema29) From29To30(s, config, notes);
            if (s.SchemaVersion == Schema30) From30To31(s, config, notes);
            if (s.SchemaVersion == Schema31) From31To32(s, config, notes);
            if (s.SchemaVersion == Schema32) From32To33(s, config, notes);
            if (s.SchemaVersion == Schema33) From33To34(s, config, notes);
            if (s.SchemaVersion == Schema34) From34To35(s, config, notes);
            if (s.SchemaVersion == Schema35) From35To36(s, config, notes);
            if (s.SchemaVersion == Schema36) From36To37(s, config, notes);
            if (s.SchemaVersion == Schema37) From37To38(s, config, notes);
            if (s.SchemaVersion == Schema38) From38To39(s, config, notes);
            if (s.SchemaVersion == Schema39) From39To40(s, config, notes);
            if (s.SchemaVersion == Schema40) From40To41(s, config, notes);
            if (s.SchemaVersion == Schema41) From41To42(s, config, notes);
            if (s.SchemaVersion == Schema42) From42To43(s, config);
            if (s.SchemaVersion == Schema43) From43To44(s, config);
            if (s.SchemaVersion == Schema44) From44To45(s);
            if (s.SchemaVersion == Schema45) From45To46(s);
            if (s.SchemaVersion == Schema46) From46To47(s);
            if (s.SchemaVersion == Schema47) From47To48(s);
            if (s.SchemaVersion == Schema48) From48To49(s);
            if (s.SchemaVersion == Schema49) From49To50(s);
            RejudgeGates(s, config);
            AdoptEnemyCards(s, config);
            NormalizeBattleLog(s);
            return notes;
        }

        /// <summary>
        /// 读档后按门槛契约重判一次（Q115 选 B，gate-contract stateRules.legacy、map-adjudications gateOpening.legacy）：旧已开世界边保留，
        /// 条件已满足但还没锁的边补锁存并记两端为已发现；不写到访/探索度、不补奖励或日志，缺事实的不开。幂等。
        /// </summary>
        public static void RejudgeGates(GameState s, GameConfig config)
        {
            if (s.Locations.Count == 0) return;
            new Engine.GameEngine(config).RefreshGates(s);
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
            var player = new List<string>();   // Q403：同一检查点的玩家句（不带实例编号）
            foreach (var g in s.Items.GroupBy(i => i.InstanceId).Where(g => g.Count() > 1))
            {
                conflicts.Add($"物品#{g.Key}在存档里出现{g.Count()}次（同一物品多处归属）");
                var ids = g.Select(i => i.ItemId).Distinct().ToList();
                var name = ids.Count == 1 ? config.Items.FirstOrDefault(i => i.Id == ids[0])?.Name : null;
                player.Add($"{(string.IsNullOrEmpty(name) ? "同一件物品" : name)}在存档中重复记录了{g.Count()}次，归属不一致。");
            }
            var potIds = PotIds(s, config);
            var claimed = new Dictionary<int, string>();
            foreach (var l in config.Locations.Where(l => l.HasFire))
            {
                if (!s.Locations.TryGetValue(l.Id, out var ls)) continue;
                var fac = ls.Facility;
                var attached = fac.AttachedPotId is int a && potIds.Contains(a) && s.Items.First(i => i.InstanceId == a).Owner == l.Id ? a : (int?)null;
                foreach (var act in new[] { s.Main, s.Sub }.Where(x => IsOldBoil(x) && x!.SourceLocation == l.Id))
                {
                    if (act!.PotInstanceId is not int b) { conflicts.Add("进行中的烧水没有记录所用的锅"); player.Add("进行中的烧水缺少用锅记录。"); continue; }
                    var pot = s.Items.FirstOrDefault(i => i.InstanceId == b);
                    if (pot == null) { conflicts.Add($"进行中的烧水所用金属锅#{b}已不在存档里"); player.Add("进行中的烧水所用的金属锅已不在存档中。"); continue; }
                    if (!(pot.Owner == ItemInstance.Carried || pot.Owner == l.Id))
                    {
                        conflicts.Add($"进行中的烧水所用金属锅#{b}不在{l.Name}，无法放进原火堆");
                        player.Add("进行中的烧水所用的金属锅不在原火堆所在地点。");
                    }
                    if (attached.HasValue && attached.Value != b)
                    {
                        conflicts.Add($"{l.Name}火堆锅位上是金属锅#{attached}，而进行中的烧水用的是金属锅#{b}，不能凭默认顺序挑一把");
                        player.Add("原火堆上的金属锅与进行中的烧水所用的锅不同，无法确定使用哪一口锅。");
                    }
                }
                var pots = PotsForFire(s, l.Id, potIds);
                if (pots.Count > config.Fire.MaxPots || pots.Count > config.Fire.ProcessingSlots)
                {
                    conflicts.Add($"{l.Name}火堆最多容纳{config.Fire.MaxPots}口锅，旧档需要放入{pots.Count}口");
                    player.Add($"原火堆最多容纳{config.Fire.MaxPots}口锅，旧存档需要放入{pots.Count}口。");
                }
                foreach (var p in pots)
                {
                    if (claimed.TryGetValue(p, out var other))
                    {
                        conflicts.Add($"金属锅#{p}同时关联{other}与{l.Name}的火堆");
                        player.Add("同一口金属锅同时关联了两处火堆。");
                    }
                    else claimed[p] = l.Name;
                }
            }
            if (conflicts.Count > 0) throw new SaveMigrationConflictException(conflicts.Distinct().ToList(), player.Distinct().ToList());
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
                    notes.Add($"金属锅原先架在{l.Name}火堆上，现在归火堆所有，在地点栏的火堆卡里查看");
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
                    notes.Add($"进行中的烧水已转为火堆自动加工：金属锅留在{l.Name}火堆里，已烧{Math.Floor(act.WorkDone)}分钟计入；角色行动已释放，不补水");
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
            s.SchemaVersion = Schema4;
        }

        // ───────────────────────── 4 → 5 ─────────────────────────

        /// <summary>
        /// 人物与战斗 v0.6 §16：旧 H/E 一次性×0.2（N/W/S/M 不动）；旧装备随机抽新的最大耐久并保持旧当前/旧满成色，旧锅/打火器初始化为区间满耐久一次；
        /// 技能 1 级、免疫初值、无感染；世界敌人按新局初始分布与游标；新增箱子在旧档已越过揭示点的不补发；进行中的主行动保留。
        /// 实例 ID、锅内物品、布局索引不复制、不重排。
        /// </summary>
        private static void From4To5(GameState s, GameConfig config, List<string> notes)
        {
            var engine = new Engine.GameEngine(config);
            var f = config.StateScale.OldHealthAndEnergyConversionFactor;
            var oldH = s.Stats.Health;
            var oldE = s.Stats.Energy;
            s.Stats.HealthMax = config.StateScale.HealthMax;
            s.Stats.EnergyMax = config.StateScale.EnergyMax;
            s.Stats.Health = Math.Min(s.Stats.HealthMax, oldH * f);
            s.Stats.Energy = Math.Min(s.Stats.EnergyMax, oldE * f);
            engine.SyncLatches(s);
            notes.Add($"健康与体力按0.2缩放：健康{Engine.Fmt.N(oldH)}→{Engine.Fmt.N(s.Stats.Health)}、体力{Engine.Fmt.N(oldE)}→{Engine.Fmt.N(s.Stats.Energy)}（上限20）；饱食、水分、清醒、精神不变");

            var gear = new List<string>();
            foreach (var it in s.Items)
            {
                var cfg = config.Items.FirstOrDefault(i => i.Id == it.ItemId);
                if (cfg == null || !cfg.HasDurability || it.MaxDurability.HasValue) continue;
                var max = engine.RollMaxDurability(s, cfg);
                if (it.Durability.HasValue)
                {
                    var legacy = cfg.LegacyMaxDurability ?? Math.Max(1, it.Durability.Value);
                    var ratio = Math.Max(0, Math.Min(1, it.Durability.Value / legacy));
                    it.MaxDurability = max;
                    it.Durability = max * ratio;
                    gear.Add($"{cfg.Name} 成色{Engine.Fmt.Pct(ratio)}→耐久{Engine.Fmt.N(it.Durability.Value)}/{Engine.Fmt.N(max)}");
                }
                else
                {
                    it.MaxDurability = max;
                    it.Durability = max;
                    gear.Add($"{cfg.Name} 满耐久{Engine.Fmt.N(max)}");
                }
            }
            if (gear.Count > 0) notes.Add("装备改为随机最大耐久并保持原成色：" + string.Join("；", gear));

            engine.InitSkills(s);
            s.Body = new BodyState { Immunity = config.Immunity.Initial, Infected = false, VirusLoad = 0 };
            s.LegacyEquippedWeaponId = null;
            s.Battle = null;
            s.Encounter = null;
            s.WorldEnemies = new List<WorldEnemy>();
            s.NextWorldEnemyId = 1;
            engine.InitWorldEnemies(s);
            s.EncounterGraceUntil = 0;

            var skipped = new List<string>();
            foreach (var r in config.Resources)
            {
                if (s.Resources.ContainsKey(r.NodeId)) continue;
                var progress = s.Locations.TryGetValue(r.LocationId, out var ls) ? ls.Progress : 0;
                var passed = r.RevealAt.HasValue && progress >= r.RevealAt.Value;
                s.Resources[r.NodeId] = new ResourceState
                {
                    Available = r.IsUnlimited ? (int?)null : passed ? 0 : r.Cap,
                    Reserved = 0,
                    LastRefillDay = s.GameMinute / Engine.Clock.MinutesPerDay,
                    Discovered = passed,
                };
                if (passed) skipped.Add($"{r.Name}（{config.Locations.First(l => l.Id == r.LocationId).Name}）");
            }
            if (skipped.Count > 0) notes.Add("旧档探索度已越过揭示点的新箱子不补发、显示为已搜空：" + string.Join("、", skipped));
            notes.Add("新增：8项熟练度（1级）、免疫力70、世界敌人6只；探索/清理后第2日起可能遇到本地敌人");

            foreach (var n in notes) s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = n + "。" });
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema5}（人物与战斗 v0.6）。" });
            s.SchemaVersion = Schema5;
        }

        // ───────────────────────── 5 → 6 ─────────────────────────

        /// <summary>
        /// 系统策划总册 v1.0（T06、P02）：旧锅去掉耐久/最大耐久（保留实例 ID、归属、全部内容、保质、水预留与受热进度，不新建锅）；
        /// 进行中的“维护锅”行动取消、预留材料返还当前地面；剧情标记为旧档——不补开场、首战、教学活性物质与“攻击力+0.1”，
        /// 饱饱持续病毒输入不启用；已有世界敌人保留，未来刷新按世界日分层过滤。其余（技能、免疫、敌人、主行动）原样保留。
        /// </summary>
        private static void From5To6(GameState s, GameConfig config, List<string> notes)
        {
            var potIds = PotIds(s, config);
            var cleared = new List<string>();
            foreach (var it in s.Items.Where(i => potIds.Contains(i.InstanceId)))
            {
                if (it.Durability == null && it.MaxDurability == null) continue;
                it.Durability = null;
                it.MaxDurability = null;
                cleared.Add($"金属锅");
            }
            if (cleared.Count > 0) notes.Add($"金属锅改为没有耐久（{string.Join("、", cleared)}）：内容、水与加工进度原样保留，不再磨损、不需要维护");
            foreach (var act in new[] { s.Sub, s.Main }.Where(a => a != null && a.Type == ActionType.Repair && a.ToolInstanceId is int id && potIds.Contains(id)).ToList())
            {
                var back = new List<string>();
                foreach (var id in act!.ReservedItemIds)
                {
                    var inst = s.Items.FirstOrDefault(i => i.InstanceId == id);
                    if (inst == null) continue;
                    inst.ReservedBy = null;
                    inst.Owner = s.CurrentLocation;
                    back.Add($"{config.Items.FirstOrDefault(c => c.Id == inst.ItemId)?.Name ?? inst.ItemId}×{inst.Count}");
                }
                ClearAction(s, act);
                notes.Add($"进行中的锅维护已取消（锅不再需要维护）{(back.Count > 0 ? "，预留的" + string.Join("、", back) + "已返还当前地面" : "")}");
            }
            s.Story = new StoryState { Legacy = true, BaobaoPresent = config.Infection.BaobaoInput.LegacySavePresent };
            s.Traits = new List<TraitInstance>();
            notes.Add("本轮新增的饱饱开场、首战教学与吞噬融合只在新游戏里体验；这份存档保留原有流程，不补发教学物资，也不启用饱饱的体内病毒输入");

            foreach (var n in notes) s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = n + "。" });
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema6}（系统策划总册 v1.0）。" });
            s.SchemaVersion = Schema6;
        }

        // ───────────────────────── 6 → 7 ─────────────────────────

        /// <summary>
        /// 策划提交 9fdb44f（S01 §8、开发假设 A1）：新增对话记录。旧档里能确认已展示过的台词（SeenDialogues）按配置顺序一次性导入为一组
        /// “旧存档记录”（时间、地点未知，已结束）；尚未读完的台词（PendingDialogues）按所属事件建成待读对话，从首句开始在读档后展示——
        /// 不标成已读、不重播已读、不补剧情效果或奖励，也不解除旧档 Legacy 边界。其余世界状态原样保留；免疫/病毒按新阈值从后续 tick 起算，
        /// 已累计的高免疫分钟保留、不倒补（R04 §8）；身体当前值不按 Q7 再缩放（R01 §7）。
        /// </summary>
        private static void From6To7(GameState s, GameConfig config, List<string> notes)
        {
            var st = s.Story;
            if (st.DialogueLog.Count == 0)
            {
                var order = config.Story.Dialogues.Select(d => d.Id).ToList();
                var seen = st.SeenDialogues.Where(order.Contains).Distinct().OrderBy(order.IndexOf).ToList();
                if (seen.Count > 0)
                {
                    var legacy = new DialogueSession { Id = st.NextDialogueSessionId++, EventId = "legacy_save", Ended = true, Legacy = true };
                    foreach (var id in seen)
                    {
                        var cfg = config.Story.Dialogues.First(d => d.Id == id);
                        legacy.Lines.Add(new DialogueLine
                        {
                            LineId = id, Order = legacy.Lines.Count + 1, Speaker = cfg.Speaker, Text = cfg.Text, Note = cfg.Note,
                            Side = string.IsNullOrEmpty(cfg.Speaker) ? DialogueSide.Narration : cfg.Speaker == Survival.Core.Engine.GameEngine.ProtagonistSpeaker ? DialogueSide.Left : DialogueSide.Right,
                        });
                    }
                    legacy.Counterpart = legacy.Lines.Where(l => l.Side == DialogueSide.Right).Select(l => l.Speaker).FirstOrDefault() ?? "";
                    st.DialogueLog.Add(legacy);
                }
                // 未读完的：按事件分组建成待读对话（不登记为已展示；读档后由引擎展示首句）
                var pending = st.PendingDialogues.Where(order.Contains).Distinct().ToList();
                foreach (var group in pending.GroupBy(id => config.Story.Dialogues.First(d => d.Id == id).Event))
                {
                    var speaker = config.Story.Dialogues.Where(d => group.Contains(d.Id) && !string.IsNullOrEmpty(d.Speaker) && d.Speaker != Survival.Core.Engine.GameEngine.ProtagonistSpeaker)
                        .Select(d => d.Speaker).FirstOrDefault() ?? "";
                    st.DialogueLog.Add(new DialogueSession { Id = st.NextDialogueSessionId++, EventId = group.Key, Counterpart = speaker, Pending = group.ToList() });
                }
                st.PendingDialogues = pending;
                // 第一段待读对话的首句在读档这一刻展示（与引擎 ShowNextLine 同一登记口径），保证输入锁下总有可确认的一句
                var first = st.DialogueLog.FirstOrDefault(d => !d.Ended && d.Lines.Count == 0 && d.Pending.Count > 0);
                if (first != null)
                {
                    var id = first.Pending[0];
                    first.Pending.RemoveAt(0);
                    var cfg = config.Story.Dialogues.First(d => d.Id == id);
                    first.Lines.Add(new DialogueLine
                    {
                        LineId = id, Order = 1, Speaker = cfg.Speaker, Text = cfg.Text, Note = cfg.Note,
                        Side = string.IsNullOrEmpty(cfg.Speaker) ? DialogueSide.Narration : cfg.Speaker == Survival.Core.Engine.GameEngine.ProtagonistSpeaker ? DialogueSide.Left : DialogueSide.Right,
                        Minute = s.GameMinute, LocationId = s.CurrentLocation,
                    });
                    first.Minute = s.GameMinute;
                    first.LocationId = s.CurrentLocation;
                }
                if (seen.Count > 0) notes.Add("新增对话记录：这份存档里已经读过的对白整理为一组“旧存档记录”（时间、地点未知），之后的对话按次记录");
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema7}（对话记录与等待烹饪）。" });
            s.SchemaVersion = Schema7;
        }

        // ───────────────────────── 7 → 8 ─────────────────────────

        /// <summary>
        /// 策划提交 5493951（S01 §10、S11 §10、S06 §8，开发假设 H3/H6）：已有有效受热进度、目标仍新鲜的食材进程一次性补烹饪保护；
        /// 已腐败的不复活，旧的继承期限成品不刷新期限。随身超过五张根物品卡的不在迁移里删物或丢地——读档后在安全空闲时由玩家整理。
        /// 重复读档不会再走这里（版本已是 8），不会刷新任何截止时间。
        /// </summary>
        private static void From7To8(GameState s, GameConfig config, List<string> notes)
        {
            var protectedCount = 0;
            foreach (var ls in s.Locations.Values)
                foreach (var p in ls.Facility.Processes)
                {
                    var cfg = config.Processes.FirstOrDefault(x => x.Id == p.RecipeId);
                    if (cfg == null || cfg.IsPotBatch || p.HeatDoneMinutes <= 0 || p.HeatProtected) continue;
                    var t = s.Items.FirstOrDefault(i => i.InstanceId == p.TargetInstanceId);
                    if (t == null || (t.ExpiresAt.HasValue && t.ExpiresAt.Value <= s.GameMinute)) continue;
                    p.HeatProtected = true;
                    protectedCount++;
                }
            if (protectedCount > 0)
            {
                var note = $"火堆里已在受热的{protectedCount}份新鲜食材按新规则受本次烹饪保护（取出即解除）；已腐败的不会恢复";
                notes.Add(note);
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = note + "。" });
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema8}（烹饪受热保护与随身五张卡）。" });
            s.SchemaVersion = Schema8;
        }

        // ───────────────────────── 8 → 9 ─────────────────────────

        /// <summary>
        /// 策划提交 f172677 B3（R08 §5、S06 全系统更新目标 §3、R02 §5，开发计划 v0.9 B3 H7）：主角个人状态已由 SaveSerializer 预处理搬进 Actors.p0
        /// （数值原样，不重置）；Party 只有主角。重复读档不会再走这里（版本已是 9）。
        /// </summary>
        private static void From8To9(GameState s, GameConfig config, List<string> notes)
        {
            s.Player.Id = GameState.PlayerId;
            // 旧单装备引用迁到武器Ⅰ（R02 §5、S06 全系统更新目标 §3）：战斗中的旧档按战内当前武器；引用无效（不在随身或不是武器）则忽略。
            // 从五卡根列表移走同一实例（不复制），其余随身不删、不重抽。
            var legacyId = s.Battle != null ? s.Battle.WeaponInstanceId : s.LegacyEquippedWeaponId;
            var weapon = legacyId.HasValue ? s.Items.FirstOrDefault(i => i.InstanceId == legacyId.Value) : null;
            var wcfg = weapon != null ? config.Weapons.FirstOrDefault(w => w.Id == weapon.ItemId && !w.IsFists) : null;
            if (weapon != null && wcfg != null && weapon.Owner == ItemInstance.Carried)
            {
                weapon.Owner = ItemInstance.EquipOwner(GameState.PlayerId, EquipSlots.Weapon1);
                weapon.PlacedOrder = null;
                var note = $"原来的当前武器{wcfg.Name}迁到武器Ⅰ（穿戴中不再占随身卡位，重量照计）；武器Ⅱ与其他穿戴槽为空";
                notes.Add(note);
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = note + "。" });
            }
            else if (legacyId.HasValue)
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"旧存档记录的当前武器已不在随身，装备槽保持为空。" });
            if (s.Battle != null)
            {
                s.Battle.ActiveWeaponSlot = 1;
                s.Battle.WeaponInstanceId = null;
            }
            s.LegacyEquippedWeaponId = null;

            if (s.Party.Count == 0 || s.Party[0] != GameState.PlayerId)
            {
                s.Party.Remove(GameState.PlayerId);
                s.Party.Insert(0, GameState.PlayerId);
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema9}（角色编号与七个穿戴槽）。" });
            s.SchemaVersion = Schema9;
        }

        // ───────────────────────── 9 → 10 ─────────────────────────

        /// <summary>
        /// 策划提交 f172677 B4（A05 §4、R02 §2/§3，开发计划 v0.9 B4 H6）：旧档里已有的装备实例一律记为白色、0 条附加词条；
        /// 不改耐久与最大耐久、不消耗随机数、不补抽品质。重复读档不会再走这里（版本已是 10）。
        /// </summary>
        private static void From9To10(GameState s, GameConfig config, List<string> notes)
        {
            var equip = new HashSet<string>(config.Items.Where(i => i.EquipSlot != null).Select(i => i.Id));
            var n = 0;
            foreach (var it in s.Items.Where(i => equip.Contains(i.ItemId) && i.Quality == null))
            {
                it.Quality = "white";
                it.Affixes = null;
                n++;
            }
            if (n > 0)
            {
                var note = $"旧存档里的{n}件装备记为白色品质、没有附加词条（耐久不变）";
                notes.Add(note);
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = note + "。" });
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema10}（装备品质与附加词条）。" });
            s.SchemaVersion = Schema10;
        }

        // ───────────────────────── 10 → 11 ─────────────────────────

        /// <summary>
        /// 策划提交 f172677 B5（A06 §4、R01 §5，开发计划 v0.9 B5 H5）：每个角色的熟练度一次性迁到十五门十级——
        /// 旧 blade（刀斧）的等级与经验复制给 blade（刀剑）与 axe（斧）；所有门类等级截到上限 10，到 10 级的当前经验清零，
        /// 原等级/经验只记在 LegacyLevel/LegacyXp 作历史（不发物资、不兑换点数）；新增门类 1 级 0 经验。
        /// 世界敌人与战斗中敌人的 blade 防刷计数同样复制给 axe（不因迁移重置，SK03）。不回补旧行为经验。
        /// </summary>
        private static void From10To11(GameState s, GameConfig config, List<string> notes)
        {
            var caps = config.Skills.ToDictionary(x => x.Id, x => x.LevelCap ?? config.SkillGrowth.SkillLevelCap ?? int.MaxValue);
            var parts = new List<string>();
            foreach (var actor in s.Actors.Values)
            {
                if (actor.Skills.TryGetValue("blade", out var blade) && !actor.Skills.ContainsKey("axe"))
                {
                    actor.Skills["axe"] = blade.Clone();
                    if (actor.IsPlayer) parts.Add($"刀斧{blade.Level}级（经验{blade.Xp}）复制为刀剑与斧");
                }
                foreach (var kv in actor.Skills)
                {
                    if (!caps.TryGetValue(kv.Key, out var cap)) continue;
                    var sk = kv.Value;
                    if (sk.Level < cap) continue;
                    if (sk.Level > cap || sk.Xp > 0)
                    {
                        sk.LegacyLevel = sk.Level;
                        sk.LegacyXp = sk.Xp;
                        if (actor.IsPlayer) parts.Add($"{config.Skills.First(x => x.Id == kv.Key).Name}{sk.Level}级截到{cap}级（多余经验只作历史记录）");
                    }
                    sk.Level = cap;
                    sk.Xp = 0;
                }
                var added = 0;
                foreach (var sc in config.Skills.Where(x => x.Id != Engine.GameEngine.StealthSkill))   // B41c：第 16 门潜行不属于这次迁移，读档时另补（AdoptEnemyCards）
                {
                    if (actor.Skills.ContainsKey(sc.Id)) continue;
                    actor.Skills[sc.Id] = new SkillState { Level = sc.InitialLevel, Xp = 0 };
                    added++;
                }
                if (actor.IsPlayer && added > 0) parts.Add($"新增{added}门熟练度从1级开始");
            }
            foreach (var w in s.WorldEnemies)
                if (w.XpHits.TryGetValue("blade", out var h) && !w.XpHits.ContainsKey("axe")) w.XpHits["axe"] = h;
            if (s.Battle != null)
                foreach (var e in s.Battle.Enemies)
                    if (e.XpHits.TryGetValue("blade", out var h) && !e.XpHits.ContainsKey("axe")) e.XpHits["axe"] = h;
            if (parts.Count > 0)
            {
                var note = "熟练度改为十五门、最高10级：" + string.Join("；", parts);
                notes.Add(note);
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = note + "。" });
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema11}（十五门十级熟练度）。" });
            s.SchemaVersion = Schema11;
        }

        // ───────────────────────── 11 → 12 ─────────────────────────

        /// <summary>
        /// 策划提交 f172677 B6（R03 §1—§6，开发计划 v0.9 B6）：四对四战斗内核。只有进行中的战斗需要迁移——
        /// 敌方旧站位 F1/F2/B1 改为 1/2/3 号位并紧凑排位；容量 C 按默认值锁存（不少于场上已有敌人数，不删已在场的敌人）；
        /// 我方站位只有主角。轮中进度（队列、游标、AP）原样保留，本轮照旧走完，下一轮起按新规则排序。不补发、不重抽。
        /// </summary>
        private static void From11To12(GameState s, GameConfig config, List<string> notes)
        {
            var b = s.Battle;
            if (b != null)
            {
                var map = new Dictionary<string, int> { ["F1"] = 1, ["F2"] = 2, ["B1"] = 3 };
                var k = 1;
                foreach (var e in b.Enemies.Where(e => !e.Dead && !e.Fled).OrderBy(e => map.TryGetValue(e.Slot, out var p) ? p : 9).ThenBy(e => e.Index))
                    e.Slot = (k++).ToString();
                foreach (var e in b.Enemies.Where(e => e.Dead || e.Fled)) e.Slot = "";
                // 旧战斗保持旧口径直到结束（Q62：activeLegacyBattleCapacityPreserved）：schema 11 时单场最多 3 敌
                b.Capacity = Math.Max(LegacyBattleCapacity, b.Enemies.Count);
                b.Allies = new List<string> { GameState.PlayerId };
                var note = "进行中的战斗改为四对四站位：敌人按原顺序排到1—" + (k - 1) + "号位，本回合照旧走完，下一回合起按新的速度与行动点规则";
                notes.Add(note);
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = note + "。" });
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema12}（四对四战斗内核）。" });
            s.SchemaVersion = Schema12;
        }

        // ───────────────────────── 12 → 13 ─────────────────────────

        /// <summary>
        /// 策划答复 Q53—Q61（开发计划 v0.9 B7b）：工作量余额从 0 开始、不补历史；进行中的行动保留规则版本 0，按开工时保存的工作量与旧规则结束
        /// （不读档重套新公式）；已有设施进程没有发起者，完成不补烹饪经验；当日经验计数与走过的路照旧保留。无须改数据，只升版本号。
        /// </summary>
        private static void From12To13(GameState s, List<string> notes)
        {
            if (s.Main != null && s.Main.IsWorkType)
            {
                var note = $"进行中的“{s.Main.Label}”按开工时的规则完成；之后的同类工作才开始结转不足5分钟的工作量余量";
                notes.Add(note);
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = note + "。" });
            }
            s.WorkCarry.Clear();
            // 真实 v12 档没有余额字段；手改或非真实档残留的锁定余额一并清掉，防止结出不存在的余量（B7b 试玩修复）
            foreach (var a in new[] { s.Main, s.Sub })
                if (a != null) { a.CarryKey = null; a.CarryReserved = 0; }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema13}（成长经验与工作量余量）。" });
            s.SchemaVersion = Schema13;
        }

        // ───────────────────────── 13 → 14 ─────────────────────────

        /// <summary>
        /// 策划答复 Q80（开发计划 v0.9 B7d）：双方保持真实 1—4 槽与空位，不再自动压紧。只有进行中的战斗需要处理——
        /// 已有的合法站位原样保留（旧档本来就是紧凑的，照旧是真实槽）；缺失、越界或重复的才按原顺序补到空槽一次
        /// （oldSaveRestoreExplicitPositionsPreserved / oldSaveMissingPositionsInitializeOnce）。我方站位表缺主角或格式不对时主角回 1 号位。
        /// 不重抽、不补发、不改轮中进度。
        /// </summary>
        private static void From13To14(GameState s, GameConfig config, List<string> notes)
        {
            var b = s.Battle;
            if (b != null)
            {
                var max = Math.Max(1, config.Scope.Slots.Count);
                var fixedCount = 0;
                var taken = new HashSet<int>();
                var pending = new List<BattleEnemy>();
                foreach (var e in b.Enemies.OrderBy(e => e.Index))
                {
                    if (!e.Alive) { e.Slot = ""; continue; }
                    if (int.TryParse(e.Slot, out var p) && p >= 1 && p <= max && taken.Add(p)) continue;
                    pending.Add(e);
                }
                foreach (var e in pending)
                {
                    var free = Enumerable.Range(1, max).FirstOrDefault(p => !taken.Contains(p));
                    if (free == 0) { e.Slot = ""; continue; }
                    taken.Add(free);
                    e.Slot = free.ToString();
                    fixedCount++;
                }
                var allies = b.Allies ?? new List<string>();
                var valid = allies.Count <= max && allies.Count(a => a == GameState.PlayerId) == 1
                            && allies.Where(a => !string.IsNullOrEmpty(a)).Distinct().Count() == allies.Count(a => !string.IsNullOrEmpty(a));
                if (!valid)
                {
                    b.Allies = new List<string> { GameState.PlayerId };
                    fixedCount++;
                }
                else
                {
                    b.Allies = allies.Select(a => a ?? "").ToList();
                    while (b.Allies.Count > 0 && string.IsNullOrEmpty(b.Allies[b.Allies.Count - 1])) b.Allies.RemoveAt(b.Allies.Count - 1);
                }
                var note = "进行中的战斗改为真实站位：双方保留现在的号位与空位，之后不再自动往前补位" + (fixedCount > 0 ? $"（{fixedCount}处缺失或重复的站位已补到空位）" : "");
                notes.Add(note);
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = note + "。" });
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema14}（真实站位与空位）。" });
            s.SchemaVersion = Schema14;
        }

        // ───────────────────────── 14 → 15 ─────────────────────────

        /// <summary>
        /// 队伍与共同生存（R08 §5，开发计划 v0.9 B8）：落下名册、战前编成预设、遗物与借用位（空）；预设与同行队伍对齐（主角恒在）；
        /// 同行里没有名册条目的角色（只可能来自测试注入）补一条。进行中战斗的主角战斗字段已在读档预处理搬进 Fighters.p0，这里只保证存在。
        /// 不重抽、不补发、不改轮中进度。
        /// </summary>
        private static void From14To15(GameState s, List<string> notes)
        {
            s.Roster ??= new List<RosterEntry>();
            s.Remains ??= new Dictionary<string, RemainsState>();
            s.LoanSlots ??= new Dictionary<string, int>();
            s.Party ??= new List<string>();
            if (!s.Party.Contains(GameState.PlayerId)) s.Party.Insert(0, GameState.PlayerId);
            foreach (var id in s.Party.Where(id => id != GameState.PlayerId && s.Roster.All(r => r.ActorId != id)))
                s.Roster.Add(new RosterEntry { ActorId = id, Kind = "named", Name = s.Actors.TryGetValue(id, out var a) && a.Name != "" ? a.Name : id, JoinedMinute = s.GameMinute });
            var preset = (s.FormationPreset ?? new List<string>()).Where(s.Party.Contains).Distinct().ToList();
            foreach (var id in s.Party) if (!preset.Contains(id)) preset.Add(id);
            s.FormationPreset = preset;
            if (s.Battle != null) s.Battle.Fighter(GameState.PlayerId);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema15}（队伍与候补）。" });
            s.SchemaVersion = Schema15;
        }

        // ───────────────────────── 15 → 16 ─────────────────────────

        /// <summary>
        /// 编队真实号位与敌人本轮 AP 账（策划答复 Q86/Q87，S01/R08 本轮章；开发计划 v0.9 B8a）：
        /// 旧的紧凑预设（第 k 个人在 k 号位）按原顺序原样落到 1..n 号位、其余空位，只迁移一次（缺失/重复/越界/死亡离队的去掉，
        /// 合法成员保留原槽，缺的人按同行顺序补最前空槽，主角缺失也只补空槽）；阵容版本从 0 起。
        /// 进行中的战斗保持已存战场槽，不因补预设改变本场；轮中已行动的敌人本轮已花多少 AP 旧档没记，按已用完处理（不能替后方补位），
        /// 没行动的从 0 起。不重抽、不补发、不改轮中进度。
        /// </summary>
        private static void From15To16(GameState s, GameConfig config, List<string> notes)
        {
            s.Party ??= new List<string>();
            if (!s.Party.Contains(GameState.PlayerId)) s.Party.Insert(0, GameState.PlayerId);
            s.FormationPreset = Engine.GameEngine.RepairedFormation(s, s.FormationPreset, config.Scope.Slots.Count);
            s.FormationVersion = 0;
            s.FormationMigrated = true;
            if (s.Battle != null)
                foreach (var e in s.Battle.Enemies.Where(e => e.Alive && e.Acted))
                    e.RoundApUsed = config.Enemies.FirstOrDefault(x => x.Id == e.EnemyId)?.Ap ?? 0;
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema16}（编队号位）。" });
            s.SchemaVersion = Schema16;
        }

        // ───────────────────────── 16 → 17 ─────────────────────────

        /// <summary>
        /// 服务地点迁址标记与公共候补遗物（策划答复 Q88/Q89，R08/A11/S01 本轮章；开发计划 v0.9 B8b）：
        /// 旧档服务都在替身地点（camp），迁址标记为 false（地图导入后由 GameEngine.MigrateServiceLocations 一次迁移）。
        /// 遗物是否公共候补遗物旧档没记：战斗中死亡的、或不在替身地点的必是同行者（false）；在替身地点、非战斗中死亡的分不清，记 null（迁址时不迁，B8b-3、待确认 Q98）。
        /// 在办的 RB01 登记已在行动里记着开始地点（SourceLocation），不用补字段。不重抽、不补发、不改物品。
        /// </summary>
        private static void From16To17(GameState s, GameConfig config, List<string> notes)
        {
            s.ServiceMapMigrated = false;
            foreach (var rem in s.Remains.Values)
                rem.PublicReserve = rem.BattleId != null || rem.LocationId != config.Party.HubStandIn ? false : (bool?)null;
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema17}（服务地点）。" });
            s.SchemaVersion = Schema17;
        }

        // ───────────────────────── 17 → 18 ─────────────────────────

        /// <summary>
        /// 候补自主照护（策划答复 Q90，R08/S13/S01 本轮章；开发计划 v0.9 B8c）：照护规则版本记为 2，每名存活候补按自身 S/E 初始化 rest/sleep 模式一次
        /// （S&lt;60 或 E&lt;6 → sleep，否则 rest）；同行者与死者不设模式。不补历史恢复、不开离线恢复、不把已有 N/W/M 截到 80、不改病伤与 owner。
        /// 旧 22—06 时段与 N/W 托底 40 的入口随代码移除，不再并行。在办的旧普通 wait 主行动按原快照继续完成或取消。
        /// </summary>
        private static void From17To18(GameState s, GameConfig config, List<string> notes)
        {
            var care = config.Party.ReserveCare;
            s.ReserveCareRule = care.RuleVersion;
            s.Party ??= new List<string>();
            foreach (var r in s.Roster.Where(r => !r.Dead && !s.Party.Contains(r.ActorId)))
            {
                if (!s.Actors.TryGetValue(r.ActorId, out var a)) continue;
                a.ReserveMode = a.Stats.Wakefulness < care.EnterSleepWakefulnessBelow - 1e-9 || a.Stats.Energy < care.EnterSleepEnergyBelow - 1e-9 ? "sleep" : "rest";
            }
            foreach (var id in s.Party) if (s.Actors.TryGetValue(id, out var p)) p.ReserveMode = null;
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema18}（候补自主照护）。" });
            s.SchemaVersion = Schema18;
        }

        // ───────────────────────── 18 → 19 ─────────────────────────

        /// <summary>
        /// Q104 选 B + 旧档补充选 A（A06/R03/S01 本轮章）：攻击经验计数从“每世界敌每门全队共享”改为“每人每世界敌每门独立 3 次”。
        /// 冻结“当时名册 actorId（含已死亡）+ 主角”快照；每个当时存在的世界敌人（及只在在途战斗里的敌人）每门旧共享已用次数 h（合法整数 0—3，
        /// 世界与在途副本逐门取大）给快照里每人继承为个人已用次数；已有的个人计数优先保留不被覆盖（幂等）。不补发、不倒扣历史经验；
        /// 旧账保留作来源证据、此后不再参与计数。进行中战斗的位置、队列、生命与随机计划不动。
        /// </summary>
        private static void From18To19(GameState s, GameConfig config, List<string> notes)
        {
            MigrateAttackXp(s, config);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema19}（攻击经验次数改为每人各自计）。" });
            s.SchemaVersion = Schema19;
        }

        // ───────────────────────── 19 → 20 ─────────────────────────

        /// <summary>
        /// v3 地图（开发计划 v0.9 B11，R33/R34；S02「地牢、位置与旧档」、gate-contract stateRules.legacy）：为新增的 135 个节点补地点状态
        /// （未发现、未到访、探索 0、清理次数按配置初值）；已开的世界边原样保留、不回退，不补旧奖励、不伪造事实；
        /// 然后按门槛契约重判一次（例如旧档村庄已 100% 时揭示通往旧公路的未开通路线）。地牢实例与资源标记从空开始。
        /// </summary>
        private static void From19To20(GameState s, GameConfig config, List<string> notes)
        {
            var added = EnsureLocationStates(s, config);
            s.DungeonInstances ??= new Dictionary<string, DungeonInstanceState>();
            s.RevealedResourceMarkers ??= new HashSet<string>();
            new Engine.GameEngine(config).RefreshGates(s);
            if (added > 0) notes.Add($"地图扩展到{config.Locations.Count}个地点，已走过的路线保留");
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema20}（v3 地图：新增{added}个地点，已开路线不回退）。" });
            s.SchemaVersion = Schema20;
        }

        // ───────────────────────── 20 → 21 ─────────────────────────

        /// <summary>
        /// 资源四账（开发计划 v0.9 B12，R36/R37；S05「定量采集与四生命周期」、A08「来源、知识与旧档迁移」）：96 条增量资源并入资源表，
        /// 补资源状态（库存取 cap，flowing 不限量）；B11 已揭示的资源点标记与“到访过且揭示度已到”的资源点记为已发现（不补物、不扣库存）；
        /// 旧档已持有的核心记录/领取事实（church_route_record、academy_safety_record、communication_power_claimed 等）记为对应
        /// campaignId 已领，已领不补；地牢实例普通箱库存记在当前代。幂等：再跑一次不改任何东西。
        /// </summary>
        private static void From20To21(GameState s, GameConfig config, List<string> notes)
        {
            var added = EnsureResourceStates(s, config);
            var claimed = SyncLegacyClaims(s, config);
            s.RevealedResourceMarkers = null;
            if (added > 0) notes.Add($"资源点扩展到{config.Resources.Count}处，已发现的资源点线索可以直接采集");
            // 这句的版本号原来拼 CurrentSchema，上线后一直写到 42；B27 起写死 42，存档再升版本时句子不变（Q248：既有日志保持）
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema42}（资源四账：新增{added}处资源点" +
                (claimed.Count > 0 ? $"；已持有的{string.Join("、", claimed)}记为已领取，不再重复领取" : "") + "）。" });
            s.SchemaVersion = Schema21;
        }

        // ───────────────────────── 21 → 22 ─────────────────────────

        /// <summary>
        /// 新手序章（开发计划 v0.9 B13，R39/R42；S15 §7 七日检查与旧档、§8、A10-TUTOR §7）：建序章状态；按可靠记录追认任务
        /// （首战胜利或旧档等效、教学融合、净水 boiled、块根熟食 cooked、已建棚、当前已装备的伤害型武器），标为“旧档追认”，不发反馈、不播反应、
        /// 不补任何奖励；取水、睡眠累计、往返、残页等没有可靠记录的不猜。七日检查已记过的只作历史统计（不撤、不重播）。已开的出区路线不反锁。
        /// 当前世界分钟已过来客阈值且首战已胜（或旧档等效）的，来客在读档后的下一安全边界登记，不再等五天。幂等。
        /// </summary>
        private static void From21To22(GameState s, GameConfig config, List<string> notes)
        {
            s.Prologue ??= new PrologueState();
            s.Prologue.MigratedAt ??= s.GameMinute;
            var engine = new Engine.GameEngine(config);
            var retro = engine.RetroPrologue(s);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema22}（新手序章：七日目标改为任务册" +
                (retro.Count > 0 ? $"，已追认{string.Join("、", retro)}" : "") + (s.Progress.SevenDayShown ? "；七日检查记录保留为历史" : "") + "）。" });
            s.SchemaVersion = Schema22;
        }

        // ───────────────────────── 22 → 23 ─────────────────────────

        /// <summary>
        /// 资源来源补齐（开发计划 v0.9 B13b，R129；S05/A08 Q120 本轮章、resource-adjudications.json legacyMigration）：
        /// 新导入而从未存在的来源按表初始化（库存=cap、无限为空、从升级时当前世界日起算日补、不追补漏过的天数）；
        /// 旧档到访过且探索度已到揭示值的只补资源卡可见，不发物品；唯一领取（支援包、消防斧箱、图册）按未领处理——旧档里没有这些来源的领取账，
        /// 不凭背包猜（已持有的消防斧不收回、不复制）。旧在办动作按开工快照完成/取消一次（新工具表只对新开工生效）。幂等。
        /// </summary>
        private static void From22To23(GameState s, GameConfig config, List<string> notes)
        {
            var added = EnsureResourceStates(s, config);
            if (added > 0) notes.Add($"资源点扩展到{config.Resources.Count}处（补回营区净水配给等旧来源），已到过且探索够的地点直接显示");
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema23}（资源来源补齐：新增{added}处资源点；部分资源点可用石刀/简易斧/撬棍省5分钟）。" });
            s.SchemaVersion = Schema23;
        }

        // ───────────────────────── 23 → 24 ─────────────────────────

        /// <summary>
        /// 旧唯一箱与核心同授知识（开发计划 v0.9 B13c，R130；A08/S05 Q122/Q123 本轮章、resource-adjudications.json legacyMigration）：
        /// 6 个旧 d03 唯一箱入资源表——旧档从未有过它们的领取账，一律按未领（不凭背包里的枪猜，已持有的不收回、不复制）；到过且探索够的只补卡片可见。
        /// 四份核心记录同授的 28 张蓝图按可信领取事实（campaignId:/claim:&lt;核心&gt;，d03 另认 communication_power_claimed）读档即会，
        /// 这里只记一条升级日志写明补学了几张；不补物资/经验/容量，不写本代守卫或 recordClaimedAndClear（出口仍须本代真实核对）。幂等。
        /// </summary>
        private static void From23To24(GameState s, GameConfig config, List<string> notes)
        {
            var added = EnsureResourceStates(s, config);
            var engine = new Engine.GameEngine(config);
            // B23：只算 B13c 当时的核心记录同授蓝图（d05—d08 可选地牢知识包是 B23 新增的来源，不进这条历史日志）
            var packs = config.Resources.Where(r => r.IsRecord && config.ExpansionQuests.Packs.All(p => p.ClaimResourceId != r.NodeId))
                .Select(r => (Res: r, Know: config.Knowledge.Where(k => k.Id != "blueprint_armory_station" && k.GrantedByFacts.Contains(Engine.GameEngine.ClaimKey(r.NodeId))).ToList()))
                .Where(x => x.Know.Count > 0 && x.Know.Any(k => engine.KnowsKnowledge(s, k.Id)))
                .ToList();
            var learned = packs.Sum(x => x.Know.Count(k => engine.KnowsKnowledge(s, k.Id)));
            if (added > 0) notes.Add($"资源点扩展到{config.Resources.Count}处（旧城警备仓的6个旧唯一箱可以领取）");
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema24}（旧城警备仓6个旧唯一箱可领取；核心记录领取同时授予蓝图" +
                (learned > 0 ? $"：已领取的{string.Join("、", packs.Select(x => x.Res.Name))}补学{learned}张蓝图，不补物资与经验" : "") + "）。" });
            s.SchemaVersion = Schema24;
        }

        // ───────────────────────── 24 → 25 ─────────────────────────

        /// <summary>
        /// 序章操作前引导（开发计划 v0.9 B13d，R132/R134；S15/A10 Q128—Q133 本轮章、prologue-adjudications.json legacyPolicy）：
        /// 旧的“完成后排队播放”反应队列整队丢弃、不补播；旧档里已实际播过的 PROxx_reaction 记为该项引导“已示”（换了文案也不重新弹），
        /// 其余未完成且从未示的等之后真实相关的界面再触发，读档本身不一次排出。PRO18 改为只看实际到过旧公路：可信已到者无声追认
        /// （标“旧档追认”，不补奖励、不发完成反馈，不伪造 south_sign_known）；其他任务、已读剧情、提示关闭/暂停偏好、进行中的动作原样保留。幂等。
        /// </summary>
        private static void From24To25(GameState s, GameConfig config, List<string> notes)
        {
            var p = s.Prologue ??= new PrologueState();
            var dropped = p.PendingReactions.Count;
            p.PendingReactions.Clear();
            foreach (var t in config.Prologue.Tasks)
                if (!string.IsNullOrEmpty(t.ReactionSet) && s.Story.DialogueLog.Any(d => d.EventId == t.ReactionSet)) p.GuideSeen.Add(t.Id);
            p.GuideActive = p.GuidePending = p.GuidePendingAt = "";
            var retro = new Engine.GameEngine(config).RetroPrologue(s);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema25}（序章改为操作前引导：相关界面打开时先给提示，完成后只给简短结果" +
                (dropped > 0 ? $"；旧的{dropped}段待播短句不再补播" : "") + (retro.Count > 0 ? $"；已追认{string.Join("、", retro)}" : "") + "）。" });
            s.SchemaVersion = Schema25;
        }

        // ───────────────────────── 25 → 26 ─────────────────────────

        /// <summary>
        /// B14（R44、R45；N01 §3/§6、A11 §6、S02 门槛规则 3/5）：建城市岗位状态；旧势力事实 faction_works/faction_beacon 原样保留并另记
        /// legacy_service:ID（不改 ID 成新组织、不伪造新组织接触）；任何 joined_faction* 事实改记 legacy_service（新规则永不写入会籍）；
        /// 已到访 c02/r02 的旧档补 church_contacted/academy_contacted（等效接触，不补对白）；已有 hub_registered 的补 registration_authorized。
        /// 不发物、不改容量与知识。幂等。
        /// </summary>
        private static void From25To26(GameState s, GameConfig config, List<string> notes)
        {
            s.City ??= new CityState();
            var legacy = Engine.GameEngine.MigrateFactionFacts(s, config);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema26}（三城岗位人物与服务上线：营门登记、医务室、施济处等实地办理" +
                (legacy.Count > 0 ? $"；{string.Join("、", legacy)}" : "") + "）。" });
            s.SchemaVersion = Schema26;
        }

        // ───────────────────────── 26 → 27 ─────────────────────────

        /// <summary>
        /// 主线与支线任务账（开发计划 v0.9 B15，R46/R47；A11 §6.3“旧玩家仍可做新故事，容量不再加、原一次物资奖不重发”）：建任务账；
        /// 按已有可靠事实无声追认（已领公开路线记 FA02/FA03 已接、首次核心记录记完成、FA04_merged 记完成、已领通信电源记 FA05 已接、
        /// 联络日志记完成、林昭已在名册记 RN01 完成），标“旧档追认”，不播对白、不补奖励。到营区广场的 FA01 说明照常在下一次到达时播。幂等。
        /// </summary>
        private static void From26To27(GameState s, GameConfig config, List<string> notes)
        {
            s.Quests ??= new QuestState();
            var retro = new Engine.GameEngine(config).RetroQuests(s);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema27}（主线与城市委托上线" +
                (retro.Count > 0 ? $"；已按现有记录追认{string.Join("、", retro)}" : "") + "）。" });
            s.SchemaVersion = Schema27;
        }

        /// <summary>
        /// 配方框架、水壶与复合锅（开发计划 v0.9 B16，R48—R51）：实例加水壶水量（LiquidId/LiquidMl），设施进程加复合锅食谱的
        /// 主食材/辅料锁定（InputInstanceIds/AuxInstanceIds），角色加护理冷却（Cooldowns）。旧档没有水壶内容、没有复合锅进程、没有护理冷却：
        /// 旧进程（单目标烤/煮/净水/煅烧）原样保留，锁定字段保持空；旧水壶（只可能来自开发入口）按空壶；不补发、不改进度。幂等。
        /// </summary>
        private static void From27To28(GameState s, GameConfig config, List<string> notes)
        {
            var canteens = new HashSet<string>(config.Items.Where(i => i.Canteen != null).Select(i => i.Id));
            foreach (var it in s.Items)
                if (!canteens.Contains(it.ItemId) || it.LiquidMl <= 0 || it.LiquidId == null) { it.LiquidId = null; it.LiquidMl = 0; }
            foreach (var a in s.Actors.Values) a.Cooldowns ??= new Dictionary<string, int>();
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema28}（新配方、分量水壶与复合锅上线；进行中的加工原样保留）。" });
            s.SchemaVersion = Schema28;
        }

        // ───────────────────────── 28 → 29 ─────────────────────────

        /// <summary>
        /// 安全屋与分区设施（开发计划 v0.9 B17，R52—R56；S09 X2.8、验收 SH15）：队伍记当前所在区（旧档一律在外部区）与收藏的安全屋（无），
        /// 地点加安全屋（旧档一律未建；许可按新地图逐点 buildAllowed.safehouse）。火堆 ID 按“地点+区”生成：旧档只有外部区火堆，
        /// 其 ID 仍是 &lt;地点&gt;:fire、锅与直烤/煅烧/锅食谱进程、受热进度与受热保护原样归入外部区，不改 owner；旧营地设施、地面与随身不动，
        /// 不补柜、不补建筑或材料；登记只解锁建屋资格。幂等。
        /// </summary>
        private static void From28To29(GameState s, GameConfig config, List<string> notes)
        {
            if (s.CurrentZone != GameState.ExteriorZone && s.CurrentZone != GameState.SafehouseZone) s.CurrentZone = GameState.ExteriorZone;
            foreach (var kv in s.Locations)
            {
                var f = kv.Value.Facility;
                // 外部区火堆 ID 显式落成“地点+外部区”（与旧 ID 相同），进程与锅 owner 不需改写
                if (config.Locations.Any(l => l.Id == kv.Key && l.HasFire) && string.IsNullOrEmpty(f.FacilityId))
                    f.FacilityId = Engine.GameEngine.FireIdOf(kv.Key, GameState.ExteriorZone);
                // 旧档不可能有安全屋：缺省未建（只有本版本新建的才写）
                if (kv.Value.Safehouse != null && !kv.Value.Safehouse.Built) kv.Value.Safehouse = null;
            }
            if (s.CurrentZone == GameState.SafehouseZone && s.Locations.TryGetValue(s.CurrentLocation, out var here) && here.Safehouse?.Built != true)
                s.CurrentZone = GameState.ExteriorZone;
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema29}（安全屋与分区设施上线：原营地火堆、锅与加工进程归入营地外部区，原样保留；旧营地不补储物柜）。" });
            s.SchemaVersion = Schema29;
        }

        /// <summary>
        /// 鉴定交易（开发计划 v0.9 B18，R57—R59；R07 X3.4/X3.7）：旧档物品一律没有鉴定记录（appraisalRecord 为空，原样保留来源与教学标记）；
        /// 城市状态补空的报价表。当天报价表不在这里补抽：按规则在下一次安全访问鉴定所时生成（数量由固定种子决定，与何时生成无关）。
        /// 旧档不可能有进行中的鉴定/换物事务（本版本前未开放）。不发物、不改额度。幂等。
        /// </summary>
        private static void From29To30(GameState s, GameConfig config, List<string> notes)
        {
            s.City ??= new CityState();
            s.City.Quotes ??= new List<AppraisalQuoteSnapshot>();
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema30}（营区鉴定所开放详细鉴定与以物换物；原有活性物质记为未鉴定，来源与教学标记原样保留）。" });
            s.SchemaVersion = Schema30;
        }

        /// <summary>
        /// 周期招募（开发计划 v0.9 B19，R60/R61；R08 RC7、S01 本轮章）：补空的招募状态——旧档的收容所开放时刻不按过去推算，
        /// 等 QMAP01 登记后首次实际安全到达收容所时才记（不补历史周期、不补抽离线候选）。旧名册条目原样（命名/护送员），没有退役记录。
        /// 旧档不可能有进行中的随机登记（本版本前未开放）。不发物、不改名额。幂等。
        /// </summary>
        private static void From30To31(GameState s, GameConfig config, List<string> notes)
        {
            s.Recruit ??= new RecruitState();
            s.Recruit.Candidates ??= new List<RecruitCandidate>();
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema31}（收容所开放周期招募：营区登记后第一次实际到达收容所时开始计周期，每72小时3名候选；不补算之前的周期）。" });
            s.SchemaVersion = Schema31;
        }

        /// <summary>
        /// 31→32（开发计划 v0.9 B20）：建空的人口区/固定战/旧精英挑战账（Population）。不补算：已到过的新野外节点不追补首次人口、已进入过的
        /// 人口区从下次实际到达起才开始刷新（“未进入区不追补”）；地牢固定战在本代下一次实际到达该房间时才揭示（按当时容量 C）；
        /// 旧档里留在世界上的敌人照旧（旧 8 地点全局 12 只数旧 8 地点的种群，开发入口放进新区的敌人改算所在人口区）。
        /// 旧精英挑战：旧档没有按 enemyId 记的结算/奖励凭据，五处挑战一律从未结算开始（不按背包里的材料推测）。幂等。
        /// </summary>
        private static void From31To32(GameState s, GameConfig config, List<string> notes)
        {
            s.Population ??= new PopulationState();
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema32}（新增12种敌人、地牢固定战、9个人口区与五处可选强敌挑战：新野外第一次实际到达时才生成本地敌人，地牢房间下一次到达时才揭示固定战；不补算之前的刷新）。" });
            s.SchemaVersion = Schema32;
        }

        /// <summary>
        /// 32→33（开发计划 v0.9 B21，开发假设 B21-13）：地牢实例补状态与本代到过的房间；存档时人在地牢危险房间的按已开始的远征补建一次（成员=当前同行，
        /// 轨迹=01 到当前的已走通最短路），人在 01 不建；不补扣终止惩罚、不加世界时间、没有惊魂未定。幂等。
        /// </summary>
        private static void From32To33(GameState s, GameConfig config, List<string> notes)
        {
            s.ExpeditionLog ??= new List<ExpeditionRecord>();
            s.AppliedEventKeys ??= new HashSet<string>();
            EnsureLocationStates(s, config);
            var extra = new Engine.GameEngine(config).MigrateExpeditions32(s);
            notes.AddRange(extra);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema33}（地牢远征：从入口缓冲第一次确认走进危险房间才开始远征，未通关离开要在安全点终止远征并承受惩罚与至少30分钟的撤出；战斗撤退会退回来路上没有敌人的房间）。" + (extra.Count > 0 ? string.Join("；", extra) + "。" : "") });
            s.SchemaVersion = Schema33;
        }

        /// <summary>
        /// 33→34（B22，开发假设 B22-9）：实例补 clearedAt/resetEligibleAt（已通关的从升级时起算 72 小时）、旧版已通关者的首通补给记已领、物品新增地牢奖励来源字段
        /// （旧物一律无来源，不能封存）。不发物资、不加时间。幂等。
        /// </summary>
        private static void From33To34(GameState s, GameConfig config, List<string> notes)
        {
            var extra = new Engine.GameEngine(config).MigrateRewards33(s);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema34}（地牢奖励分账：守卫回收材料每代一次、首次通关补给本局一次；通关后72世界小时可在父地点确认重置；地牢里拿到的奖励可以封存，成功撤出后放进父地点的远征回收箱）。" + (extra.Count > 0 ? string.Join("；", extra) + "。" : "") });
            s.SchemaVersion = Schema34;
        }

        /// <summary>
        /// 34→35（开发计划 v0.9 B22g，Q180/Q181/Q183）：已生成的旧候选（含在办登记快照）与已招随机角色保留原显示名、数值与 actorId，
        /// 性别/外观字段留空（显示“未说明/未记录”，不补抽）；新周期候选用新池。旧在册候补位置保持营区广场（ReserveSafehouse 为空），不自动搬屋；
        /// 旧在办登记没有去向选择，完成时默认广场候补、不自动入队。不发物资、不加时间。幂等。
        /// </summary>
        private static void From34To35(GameState s, GameConfig config, List<string> notes)
        {
            s.Recruit ??= new RecruitState();
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema35}（收容所周期候选有了姓名、性别与文字外观：已生成的候选与已招入的角色保留原名，性别显示未说明、外观显示未记录，下一批起用新名字；登记时可选编入同行或安排候补到营区广场或已建成的安全屋；原有候补仍在营区广场）。" });
            s.SchemaVersion = Schema35;
        }

        /// <summary>
        /// 35→36（开发计划 v0.9 B22h，Q196 选 C，retreat.currentBattles）：普通主世界战斗撤退成功退回“最近一次已完成的地点移动”的来处。
        /// 旧档里没有可信的已完成移动记录（只有不分先后的已走边集合），LastMove 留空、不猜；下一次实际移动完成后才有来处。
        /// 读档时进行中的战斗没有开战快照，按旧落点（撤退留在原地）。不加时间、不动敌人。幂等。
        /// </summary>
        private static void From35To36(GameState s, GameConfig config, List<string> notes)
        {
            s.LastMove = null;
            if (s.Battle != null) s.Battle.WorldRetreat = null;
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema36}（野外与城市外部的战斗撤退成功后，全队退回最近一次走来的地点；旧存档没有可信的移动记录，下一次实际移动后才生效；读档时正在进行的战斗仍按原规则，撤退后留在原地）。" });
            s.SchemaVersion = Schema36;
        }

        /// <summary>
        /// 36→37（开发计划 v0.9 B22b，Q168 migration）：旧野外修补包逐份转为白色满包 100/100（数量、来源、所有者、堆叠不变，已有包耐久的不动，幂等）；
        /// 已用掉的不追补。进行中的旧 15 分钟现场小修（没有 PatchInstanceId）按开工快照完成：完成时扣掉预留的旧包 1 份、恢复最大耐久 10%，
        /// 不再按新公式扣包耐久（预留包这里也记满包耐久，只在取消后作为白包回到地面，不会新旧同时扣费）。不加时间、不发物资。
        /// </summary>
        private static void From36To37(GameState s, GameConfig config, List<string> notes)
        {
            var packs = 0;
            foreach (var it in s.Items)
            {
                var fp = config.Items.FirstOrDefault(c => c.Id == it.ItemId)?.FieldPatch;
                if (fp == null || it.Durability.HasValue) continue;
                it.MaxDurability = fp.MaxDurability;
                it.Durability = fp.MaxDurability;
                packs += it.Count;
            }
            var legacy = new[] { s.Main, s.Sub }.Any(a => a != null && a.Type == ActionType.Repair && a.TargetId == Engine.GameEngine.FieldPatchRepairId && a.PatchInstanceId == null);
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema37}（野外修补包分白、绿、蓝三品阶，每包耐久100，按品阶扣包耐久修补金属武器、工具和防具：" +
                (packs > 0 ? $"原有野外修补包{packs}份已转为白色满包100/100" : "原有野外修补包都转为白色满包100/100") +
                (legacy ? "；正在进行的现场小修仍按开工时的旧规则完成（消耗1份、恢复最大耐久10%）" : "") + "）。" });
            s.SchemaVersion = Schema37;
        }

        /// <summary>
        /// 37→38（开发计划 v0.9 B22c，Q170 migration）：晾制架开放、8 个独立晾制位。区设施新增在用晾制位列表（旧档为空）；
        /// 已建的晾制架照旧保留（旧版本建不了晾制架，正常没有），8 位全空；没建的不赠送，不发原料、不回算历史经验，不加时间。
        /// </summary>
        private static void From37To38(GameState s, GameConfig config, List<string> notes)
        {
            var racks = 0;
            foreach (var ls in s.Locations.Values)
            {
                ls.Facility.DryingSlots ??= new List<DryingSlot>();
                if (ls.Facility.Workshops.Contains("drying_rack")) racks++;
                if (ls.Safehouse != null)
                {
                    ls.Safehouse.Interior.DryingSlots ??= new List<DryingSlot>();
                    if (ls.Safehouse.Interior.Workshops.Contains("drying_rack")) racks++;
                }
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema38}（晾制架开放：木材4＋绳索2建在临时营地或已建成的安全屋屋内，" +
                "8个独立晾制位，每位一批果干或肉干，被动晾制" + (racks > 0 ? $"；已有晾制架{racks}座保留，8位都是空的" : "；没有建过的不赠送") + "）。" });
            s.SchemaVersion = Schema38;
        }

        /// <summary>
        /// 38→39（开发计划 v0.9 B22k，策划答复 Q215 migration）：肉干在制暂停原料保质。先按旧规则核对：已标 spoiled 的肉干位不动（不复活、不补产物，清理时腐肉按原规则、
        /// 未腐败的盐原样退回一次）；晾制中但位内原料在升级时刻 U 已到期的也不动（下一个 5 分钟段按旧规则标腐败停止）。晾制中且原料都未腐败的肉干位从 U 开始保护：
        /// 每份有截止的原料快照 R＝旧原截止 E − U（不倒推补偿开工到升级已耗的时间），ExpiresAt 暂为空；保留已晾分钟与经验快照。已完成的肉干截止不改，果干位不改。
        /// 快照只在这里初始化一次（版本号随即升到 39），读档、转移、预览都不重算。
        /// </summary>
        private static void From38To39(GameState s, GameConfig config, List<string> notes)
        {
            var u = s.GameMinute;
            var protectedSlots = 0;
            foreach (var kv in s.Locations)
            {
                var facs = new List<(string Place, FacilityState Fac)> { (kv.Key, kv.Value.Facility) };
                if (kv.Value.Safehouse != null) facs.Add((Engine.GameEngine.ZonePlace(kv.Key, GameState.SafehouseZone), kv.Value.Safehouse.Interior));
                foreach (var (place, fac) in facs)
                {
                    foreach (var b in fac.DryingSlots ?? new List<DryingSlot>())
                    {
                        if (b.Status != DryingSlot.Drying || b.ShelfPaused) continue;
                        var rc = config.Recipes.FirstOrDefault(r => r.Id == b.RecipeId);
                        if (rc == null || !rc.IsDrying || !rc.Outputs.ContainsKey(Engine.GameEngine.DryingShelfPauseOutput)) continue;
                        var owner = ItemInstance.DryingOwner(place, b.Slot);
                        var items = s.Items.Where(i => i.Owner == owner).ToList();
                        if (items.Any(i => i.ExpiresAt is int e && e <= u)) continue;
                        foreach (var it in items)
                        {
                            if (it.ShelfPausedMinutes != null || it.ExpiresAt is not int e) continue;
                            it.ShelfPausedMinutes = e - u;
                            it.ExpiresAt = null;
                        }
                        b.ShelfPaused = true;
                        protectedSlots++;
                    }
                }
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema39}（晾制肉干开始制作后暂停原料保质计时，取消后恢复剩余时长，" +
                "完成后肉干从完成起保质168小时；果干不变" + (protectedSlots > 0 ? $"；正在晾制的肉干{protectedSlots}批从现在起暂停原料保质，按现在的剩余保质恢复" : "") +
                "；已腐败的批次不复活）。" });
            s.SchemaVersion = Schema39;
        }

        // ───────────────────────── 39 → 40 ─────────────────────────

        /// <summary>
        /// 可选地牢知识支线（开发计划 v0.9 B23，R71；A11「可选地牢任务与知识」EXQ-A05）：资源点状态新增“核心记录固定等待已提交分钟”，旧档一律 0
        /// （旧档没有可续做的段；进行中的领取按开工时的规则走完）。知识按授予事实读档即会（已领 d05—d08 记录的旧档自动掌握对应知识），
        /// 不补物资、经验、任务完成或容量。幂等。
        /// </summary>
        private static void From39To40(GameState s, GameConfig config, List<string> notes)
        {
            foreach (var rs in s.Resources.Values) rs.ClaimMinutesDone = 0;
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = "存档已升级到版本40（可选地牢任务与公开抄录：" +
                "废水厂、矿洞、商城、冷链仓的核心记录领取改为固定等待10分钟两段、中途停下保留已完成的段；不补物资、经验或任务完成）。" });
            s.SchemaVersion = Schema40;
        }

        /// <summary>
        /// 40→41（开发计划 v0.9 B24，A10-CITY §3“旧档已在城中但无展示标记可首次显示一次纯UI说明，不补历史奖励或伪造接触”）：
        /// 人正在某城内（父节点或城内建筑）且到过该城父节点的，记该城待自动打开（旧档补显，界面写一句说明）；
        /// 其余到过父节点的城记为已展示（不是首次到达，不再自动弹）。不补任何奖励、事实或接触。
        /// </summary>
        private static void From40To41(GameState s, GameConfig config, List<string> notes)
        {
            var e = new Engine.GameEngine(config);
            var here = e.CityOf(s.CurrentLocation);
            var c = s.City;
            c.OverviewShown ??= new HashSet<string>();
            foreach (var p in e.CityParents())
            {
                if (!s.Locations.TryGetValue(p, out var l) || !l.Visited) continue;
                if (p == here && c.OverviewPending == null) { c.OverviewPending = p; c.OverviewPendingLegacy = true; }
                else c.OverviewShown.Add(p);
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{Schema41}（三城主城鸟瞰：" +
                "在三城内可从当前地点卡旁的“主城鸟瞰”查看城内建筑与路线；不补奖励、不改位置）。" });
            s.SchemaVersion = Schema41;
        }

        // ───────────────────────── 41 → 42 ─────────────────────────

        /// <summary>
        /// 代班接续说明只自动播一次（开发计划 v0.9 B26a，Q235 migration）：DialogueLog 里本岗 institution_substitute 组（含 #序号的重播）实际显示过台词的，无声记为已展示；
        /// 不按 Talks 次数或原人物问候推定，还在排队没显示的旧对白留给开始显示时记录。对话队列、Greeted、任务、配额、知识、奖励都不动。幂等。
        /// 升级日志是 Q240 定稿的逐字句，版本号写死 42：以后再升版本，这一步写的仍是这句。
        /// </summary>
        private static void From41To42(GameState s, GameConfig config, List<string> notes)
        {
            s.City.SubstituteIntroShown ??= new HashSet<string>();
            s.City.SubstituteIntroDeferred ??= new List<SubstituteIntroDeferral>();   // Q243 save：旧档没有可信证据，不按 Talks、服务日志或未示状态造待补播
            foreach (var n in config.City.Npcs)
            {
                if (!n.States.TryGetValue(Engine.GameEngine.SubstituteIntroState, out var set)) continue;
                if (s.Story.DialogueLog.Any(d => d.Lines.Count > 0 && d.EventId.Split('#')[0] == set))
                    s.City.SubstituteIntroShown.Add(Engine.GameEngine.SubstituteIntroKey(s, n.NpcId));
            }
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = "存档已升级到版本42（岗位代班：" +
                "各岗位的代班接续说明只自动播一次，已播过的不再播；不补播对白，不改任务、配额与奖励）。" });
            s.SchemaVersion = Schema42;
        }

        // ───────────────────────── 42 → 43 ─────────────────────────

        /// <summary>
        /// 五张蓝图取消登记默认（开发计划 v0.9 B27，Q216；knowledge-source-adjudications.json migration）：只在这一步看一次旧档——
        /// 已有 hub_registered 的五项写成永久知识（Q246）；已有 registration_authorized 且无 hub_registered 的记下旧登记在办资格（Q247），
        /// 迁移时不提前完成登记、不提前授知识，留给真实到广场的 ArriveCity；其余旧档什么都不写。之后的档不再走这里，不按事实补发或重判。
        /// 静默（Q248）：不写日志、提示或对白，不发物资经验。幂等。
        /// </summary>
        private static void From42To43(GameState s, GameConfig config)
        {
            var reg = config.City.Registration;
            var f = s.Progress.Flags;
            if (f.Contains(reg.RegisteredFact))
                foreach (var id in reg.LegacyKnowledge) f.Add("knowledge:" + id);
            else if (f.Contains(reg.AuthorizedFact)) s.City.LegacyRegistrationPending = true;
            s.SchemaVersion = Schema43;
        }

        // ───────────────────────── 43 → 44 ─────────────────────────

        /// <summary>
        /// 随机资源点掉落按点持久化（开发计划 v0.9 B27c，Q271 选 B；q269-q273-display-loot-adjudications.json loot.legacy）：
        /// 旧档没有记录默认未抽定。只有仍在办、目标是带互斥/独立稀有表的资源点的 Gather，从它已保存的快照补写该点记录——互斥读原
        /// LootPlanned/LootItemId/LootCount（LootRoll 一并带上），独立稀有读原 RareHit（false 也是已抽定），命中项按该资源点的原奖励定义；
        /// 原行动、时间和预留一点不动。已退出无快照的点不写（下次合法开工再抽一次），已领耗尽的点保持耗尽，迁移不发奖励。
        /// 已有记录不覆盖（幂等）。互斥表的快照缺损（LootPlanned 为假或缺物品/数量）不替它抽、不写记录，按原完成结算处理。不抽随机，静默不写日志。
        /// </summary>
        private static void From43To44(GameState s, GameConfig config)
        {
            foreach (var a in new[] { s.Main, s.Sub })
            {
                if (a == null || a.Type != ActionType.Gather) continue;
                var res = config.Resources.FirstOrDefault(r => r.NodeId == a.TargetId);
                if (res == null || !s.Resources.TryGetValue(res.NodeId, out var rs) || rs.LootDraw != null) continue;
                var hasExclusive = res.ExclusiveLoot != null && res.ExclusiveLoot.Count > 0;
                if (!hasExclusive && res.IndependentRare == null) continue;
                if (hasExclusive && (!a.LootPlanned || a.LootItemId == null || a.LootCount <= 0)) continue;
                var d = new GatherLootDraw();
                if (hasExclusive)
                {
                    d.ExclusiveItemId = a.LootItemId;
                    d.ExclusiveCount = a.LootCount;
                    d.ExclusiveRoll = a.LootRoll;
                }
                if (res.IndependentRare != null && a.RareHit)
                {
                    d.RareHit = true;
                    d.RareItemId = res.IndependentRare.ItemId;
                    d.RareCount = res.IndependentRare.Count;
                }
                rs.LootDraw = d;
            }
            s.SchemaVersion = Schema44;
        }

        // ───────────────────────── 44 → 45 ─────────────────────────

        /// <summary>
        /// 陶窑与固定净水台多批排队（S08「2026-11-10 Q380」经验与存档，开发计划 B32e）：旧档每座最多一批，迁移后这一批就是加工中批
        /// （Queued=false），已有进度、原料与燃料预留、经验快照原样接入，等待队列为空。万一同一座有多条（旧规则不允许），按原列表先后保留，
        /// 第一条为加工中、其余转为排队（进度保持）。不改时间、不发产物与经验，静默。幂等。
        /// </summary>
        private static void From44To45(GameState s)
        {
            foreach (var loc in s.Locations.Values)
            {
                NormalizeStationQueue(loc.Facility);
                if (loc.Safehouse != null) NormalizeStationQueue(loc.Safehouse.Interior);
            }
            s.SchemaVersion = Schema45;
        }

        // ───────────────────────── 45 → 46 ─────────────────────────

        /// <summary>
        /// 新版敌踪卡模型的存档结构（R05 文首「Q342 新版敌踪、潜行和躲避定稿」「Q425 旧档与持久化」，开发记录 B41a）：只补空结构——
        /// 组卡表为空、采纳标记 0、世界敌人不属于任何组、进行中的战斗没有卡键（按旧快照打完）。不改敌人、行动、保护期，不抽随机，静默。幂等。
        /// 旧待处理名单转组卡、在办绕行取消等规则性的转换不在这里：由 <see cref="AdoptEnemyCards"/> 在读档链尾做一次（B41e3 新版敌踪上线）。
        /// </summary>
        private static void From45To46(GameState s)
        {
            s.EnemyCards ??= new EnemyCardsState();
            s.SchemaVersion = Schema46;
        }

        // ───────────────────────── 46 → 47 ─────────────────────────

        /// <summary>
        /// B41b 敌踪接近时限、撤退保护与战后撤退离地保留的存档结构（R05 Q416、Q439—Q443、Q463—Q476，开发记录 B41b）：只补默认值——
        /// 没有任何卡的时限／保护记录（首次可操作现身时才抽，Q425）、行动没有敌方接近原因与保留标记、没有开工来源记录（首次采纳 Q466 时按真实归属补）、
        /// 没有暂存工作（Q472「旧档无暂存记录即按无暂存处理」）。不改敌人、行动、保护期，不抽随机，静默。幂等。
        /// </summary>
        private static void From46To47(GameState s)
        {
            s.EnemyCards ??= new EnemyCardsState();
            s.EnemyCards.Timers ??= new Dictionary<string, EnemyCardTimer>();
            s.SchemaVersion = Schema47;
        }

        // ───────────────────────── 47 → 48 ─────────────────────────

        /// <summary>
        /// B41c 小队队长、潜行行动、躲避隐藏与先手的存档结构（R05 Q342、Q420—Q428、Q445、Q503—Q508，开发记录 B41c）：只补默认值——
        /// 队长为主角、没有隐藏中的敌人、所有卡现身次数为 0、没有领过潜行经验、进行中的战斗没有先手。第 16 门潜行在采纳新版敌踪时补（1 级 0 经验）。
        /// 不改敌人、行动、技能，不抽随机，静默。幂等。
        /// </summary>
        private static void From47To48(GameState s)
        {
            s.EnemyCards ??= new EnemyCardsState();
            s.EnemyCards.Appearances ??= new Dictionary<string, int>();
            s.EnemyCards.StealthXpClaims ??= new List<string>();
            s.SchemaVersion = Schema48;
        }

        // ───────────────────────── 48 → 49 ─────────────────────────

        /// <summary>
        /// B41c2 开工 5 分钟不可中途退出（S01 Q506 第 6 条、Q516、Q524，开发记录 B41c2）：在办的主行动、子行动与暂存原工作只按原固定计划初始化一次——
        /// 固定时长计划为 5 分钟、固定等待处理配方为 5 分钟，以及原本就不可取消的单段事务与潜行接近／躲避记为不可退出；
        /// 工作量型、移动没有保存开工同源预计，不按基础分钟或当前剩余反推；旧在办等待加工的计划分钟混有受热需求兜底，一律保持可退出（Q524）。
        /// 不改进度、预留、费用与日志，静默。幂等（49 档不再经过这里）。
        /// </summary>
        private static void From48To49(GameState s)
        {
            foreach (var a in new[] { s.Main, s.Sub, s.StashedWork })
                if (a != null) a.NoExit = Survival.Core.Engine.GameEngine.LegacyFixedPlanLock(a);
            s.SchemaVersion = Schema49;
        }

        /// <summary>
        /// 49 → 50（BM5c，R05 Q605 §5「新旧探索按开工规则版本区分」、Q624）：不给在办探索补唯一检查段——缺 EncounterSegment 就是旧开工规则，
        /// 沿原已锁签在实际完成边界只检查一次，不逆插中途检查、不重抽；同次探索的原地到期延后读档即按新规则。其余新字段默认值即可。
        /// </summary>
        private static void From49To50(GameState s)
        {
            s.SchemaVersion = GameState.CurrentSchema;
        }

        /// <summary>
        /// 读档时采纳新版敌踪卡模型（已采纳过不做任何事，幂等）：链尾与“已是当前版本”的读档（EnsureStates）都调用——B41e3 新版敌踪上线时
        /// 线上存档早已是 46—49，采纳发生在上线后的第一次读档。规则见 Engine.GameEngine.AdoptEnemyCards。
        /// </summary>
        public static void AdoptEnemyCards(GameState s, GameConfig config)
        {
            s.EnemyCards ??= new EnemyCardsState();
            // B41c：每次读档都给所有人补第 16 门潜行（缺才补，1 级 0 经验；R05 Q342 / R01），已有的不动
            new Engine.GameEngine(config).InitSkills(s);
            if (s.EnemyCards.ModelVersion >= Engine.GameEngine.EnemyCardsModelV2) return;
            new Engine.GameEngine(config).AdoptEnemyCards(s);
        }

        private static void NormalizeStationQueue(FacilityState fac)
        {
            foreach (var g in fac.StationBatches.GroupBy(b => b.StationId))
            {
                var first = true;
                foreach (var b in g)
                {
                    b.Queued = !first;
                    first = false;
                }
            }
        }

        /// <summary>当前版本读档也补配置新增的地点与资源点状态，并按门槛契约重判一次（Q115，B13a）（幂等）。</summary>
        public static void EnsureStates(GameState s, GameConfig config)
        {
            EnsureLocationStates(s, config);
            EnsureResourceStates(s, config);
            RejudgeGates(s, config);
            AdoptEnemyCards(s, config);   // B41a：新版敌踪上线后的第一次读档采纳新版敌踪卡模型（已采纳不做事）
            NormalizeBattleLog(s);
        }

        /// <summary>旧版战斗日志截存上限（B34 前）：满这么多条的旧战斗无法确认早段完整。</summary>
        public const int LegacyBattleLogCap = 80;

        /// <summary>
        /// B34（S14 Q528.2）：旧档里进行中的战斗没有 LogComplete——当时不足 80 条说明从未被截，记为完整；满 80 条记为“较早记录可能未保存”。
        /// 已定过的不动（幂等）；不补造、不删除任何已有记录。
        /// </summary>
        public static void NormalizeBattleLog(GameState s)
        {
            if (s.Battle is { LogComplete: null } b) b.LogComplete = b.Log.Count < LegacyBattleLogCap;
        }

        /// <summary>
        /// 为配置里有、存档里没有的资源点补状态：库存取 cap（不限量为 null）、日补日记为今天；已揭示的旧标记或“到访过且探索度已到揭示点”的
        /// 记为已发现。地牢普通箱的代号取该地牢当前实例代号（没有实例为 0，建实例时补满）。返回补了几个。幂等。
        /// </summary>
        public static int EnsureResourceStates(GameState s, GameConfig config)
        {
            var added = 0;
            var day = s.GameMinute / Engine.Clock.MinutesPerDay;
            foreach (var r in config.Resources)
            {
                if (s.Resources.ContainsKey(r.NodeId)) continue;
                var ls = s.Locations.TryGetValue(r.LocationId, out var l) ? l : null;
                    var unlocked = r.UnlockAnyFacts.Count == 0 || r.UnlockAnyFacts.Any(s.Progress.Flags.Contains);
                var dungeon = config.Locations.FirstOrDefault(x => x.Id == r.LocationId)?.DungeonId;
                // B13c（Q122）：守卫战利品箱看本代实例事实
                var factMet = r.RevealInstanceFact != null && dungeon != null && s.DungeonInstances.TryGetValue(dungeon, out var di) && di.Facts.Contains(r.RevealInstanceFact);
                var seen = unlocked && ((s.RevealedResourceMarkers?.Contains(r.NodeId) ?? false)
                           || (ls != null && ls.Visited && (r.RevealOnVisit || (r.RevealAt.HasValue && ls.Progress >= r.RevealAt.Value) || factMet)));
                s.Resources[r.NodeId] = new ResourceState
                {
                    Available = r.IsUnlimited ? (int?)null : r.Cap,
                    Reserved = 0,
                    LastRefillDay = day,
                    Discovered = seen,
                    Generation = r.IsInstance && dungeon != null && s.DungeonInstances.TryGetValue(dungeon, out var inst) ? inst.Generation : 0,
                };
                added++;
            }
            return added;
        }

        /// <summary>
        /// 旧档领取账（R37，A08「来源、知识与旧档迁移」、gate-contract factProducers）：存档已有某份核心记录（记录 ID 事实，如容量用的
        /// church_route_record）或其领取事实（communication_power_claimed 等）时，记该 campaignId 已领，已领不补。返回本次新记的记录名。幂等。
        /// </summary>
        public static List<string> SyncLegacyClaims(GameState s, GameConfig config)
        {
            var list = new List<string>();
            foreach (var r in config.Resources.Where(r => r.IsCampaign))
            {
                var key = Engine.GameEngine.CampaignKey(r.NodeId);
                if (s.Progress.Flags.Contains(key)) continue;
                var held = (r.RecordId != null && s.Progress.Flags.Contains(r.RecordId)) || r.ClaimFacts.Any(s.Progress.Flags.Contains);
                if (!held) continue;
                s.Progress.Flags.Add(key);
                s.Progress.Flags.Add(Engine.GameEngine.ClaimKey(r.NodeId));
                if (s.Resources.TryGetValue(r.NodeId, out var rs)) rs.Available = 0;
                list.Add(r.Name);
            }
            return list;
        }

        /// <summary>为配置里有、存档里没有的地点补状态（未发现、未到访、探索度取初值、清理次数取初值）。返回补了几个。幂等。</summary>
        public static int EnsureLocationStates(GameState s, GameConfig config)
        {
            var added = 0;
            foreach (var l in config.Locations)
            {
                if (s.Locations.ContainsKey(l.Id)) continue;
                s.Locations[l.Id] = new LocationState
                {
                    Discovered = l.InitiallyDiscovered,
                    Visited = false,
                    Progress = l.InitialProgress,
                    ClearCharges = l.Clear.InitialCharges,
                    ClearRefillProgress = 0,
                    ClearUpdatedMinute = s.GameMinute,
                };
                added++;
            }
            return added;
        }

        /// <summary>Q104 旧共享攻击经验计数一次迁移（幂等：已有快照不重冻、已有个人计数不覆盖）。测试可直接调用。</summary>
        public static void MigrateAttackXp(GameState s, GameConfig config)
        {
            var cap = Math.Max(0, config.Xp.MaxHitsPerEncounterEnemySkill);
            var roster = s.AttackXpLegacyRoster;
            if (roster == null)
            {
                roster = new List<string> { GameState.PlayerId };
                foreach (var r in s.Roster) if (!string.IsNullOrEmpty(r.ActorId) && !roster.Contains(r.ActorId)) roster.Add(r.ActorId);
                s.AttackXpLegacyRoster = roster;
            }
            int Legal(int h) => Math.Max(0, Math.Min(cap, h));
            // 逐世界敌 ID 汇总旧共享账（世界与在途副本逐门取大）
            var legacy = new Dictionary<int, Dictionary<string, int>>();
            void Collect(int id, Dictionary<string, int>? hits)
            {
                if (hits == null) return;
                if (!legacy.TryGetValue(id, out var m)) legacy[id] = m = new Dictionary<string, int>();
                foreach (var kv in hits) m[kv.Key] = Math.Max(m.TryGetValue(kv.Key, out var o) ? o : 0, Legal(kv.Value));
            }
            foreach (var w in s.WorldEnemies) Collect(w.Id, w.XpHits);
            if (s.Battle != null) foreach (var e in s.Battle.Enemies) Collect(e.WorldId, e.XpHits);
            void Apply(int id, Dictionary<string, int> personal)
            {
                if (!legacy.TryGetValue(id, out var m)) return;
                foreach (var kv in m)
                {
                    if (kv.Value <= 0) continue;
                    foreach (var actor in roster)
                    {
                        var key = AttackXpKeys.Key(actor, kv.Key);
                        if (!personal.ContainsKey(key)) personal[key] = kv.Value;   // 已有个人计数优先保留
                    }
                }
            }
            foreach (var w in s.WorldEnemies) { w.ActorXpHits ??= new Dictionary<string, int>(); Apply(w.Id, w.ActorXpHits); }
            if (s.Battle != null)
                foreach (var e in s.Battle.Enemies) { e.ActorXpHits ??= new Dictionary<string, int>(); Apply(e.WorldId, e.ActorXpHits); }
            // 世界与在途副本的个人账逐键取大，避免一份落后拷贝恢复额度
            if (s.Battle != null)
                foreach (var e in s.Battle.Enemies)
                {
                    var w = s.WorldEnemies.FirstOrDefault(x => x.Id == e.WorldId);
                    if (w == null) continue;
                    foreach (var k in e.ActorXpHits.Keys.Union(w.ActorXpHits.Keys).ToList())
                    {
                        var v = Math.Max(e.ActorXpHits.TryGetValue(k, out var a) ? a : 0, w.ActorXpHits.TryGetValue(k, out var bb) ? bb : 0);
                        e.ActorXpHits[k] = v;
                        w.ActorXpHits[k] = v;
                    }
                }
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

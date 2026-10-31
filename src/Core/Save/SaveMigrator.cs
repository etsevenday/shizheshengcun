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
            if (s.SchemaVersion == Schema4) From4To5(s, config, notes);
            if (s.SchemaVersion == Schema5) From5To6(s, config, notes);
            if (s.SchemaVersion == Schema6) From6To7(s, config, notes);
            if (s.SchemaVersion == Schema7) From7To8(s, config, notes);
            if (s.SchemaVersion == Schema8) From8To9(s, config, notes);
            if (s.SchemaVersion == Schema9) From9To10(s, config, notes);
            if (s.SchemaVersion == Schema10) From10To11(s, config, notes);
            if (s.SchemaVersion == Schema11) From11To12(s, config, notes);
            if (s.SchemaVersion == Schema12) From12To13(s, notes);
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
                    gear.Add($"{cfg.Name}#{it.InstanceId} 成色{Engine.Fmt.Pct(ratio)}→耐久{Engine.Fmt.N(it.Durability.Value)}/{Engine.Fmt.N(max)}");
                }
                else
                {
                    it.MaxDurability = max;
                    it.Durability = max;
                    gear.Add($"{cfg.Name}#{it.InstanceId} 满耐久{Engine.Fmt.N(max)}");
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
                cleared.Add($"金属锅#{it.InstanceId}");
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
                var note = $"原来的当前武器{wcfg.Name}#{weapon.InstanceId}迁到武器Ⅰ（穿戴中不再占随身卡位，重量照计）；武器Ⅱ与其他穿戴槽为空";
                notes.Add(note);
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = note + "。" });
            }
            else if (legacyId.HasValue)
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"旧存档记录的当前武器#{legacyId}已不在随身，装备槽保持为空。" });
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
                foreach (var sc in config.Skills)
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
                var cap = Math.Max(1, Math.Min(config.Scope.Slots.Count, config.Scope.DefaultCapacity));
                b.Capacity = Math.Max(cap, b.Enemies.Count);
                b.Allies = new List<string> { GameState.PlayerId };
                var note = "进行中的战斗改为四对四站位：敌人按原顺序排到1—" + (k - 1) + "号位，本轮照旧走完，下一轮起按新的速度与行动点规则";
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
            s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = GameEventKind.Info.ToString(), Text = $"存档已升级到版本{GameState.CurrentSchema}（成长经验与工作量余量）。" });
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

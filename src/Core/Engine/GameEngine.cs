using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 内核对外唯一入口。所有方法只读写传入的 GameState，引擎本身只持有只读配置。
    /// 规则来源：基础系统 v0.2 三份系统案与 doc/实施方案 v0.1.md。
    /// </summary>
    public sealed partial class GameEngine
    {
        public GameConfig Config { get; }
        private readonly Dictionary<string, ItemConfig> _items;
        private readonly Dictionary<string, LocationConfig> _locs;
        private readonly Dictionary<string, EdgeConfig> _edges;
        private readonly Dictionary<string, ResourceConfig> _res;
        private readonly Dictionary<string, RecipeConfig> _recipes;

        private const double Eps = 1e-6;

        public GameEngine(GameConfig config)
        {
            Config = config;
            _items = config.Items.ToDictionary(i => i.Id);
            _locs = config.Locations.ToDictionary(l => l.Id);
            _edges = config.Edges.ToDictionary(e => e.Id);
            _res = config.Resources.ToDictionary(r => r.NodeId);
            _recipes = config.Recipes.ToDictionary(r => r.Id);
        }

        private int Tick => Config.Time.TickMinutes;

        // ───────────────────────── 查询 ─────────────────────────

        public ItemConfig Item(string id) => _items[id];
        public LocationConfig Location(string id) => _locs[id];
        public EdgeConfig Edge(string id) => _edges[id];
        public ResourceConfig Resource(string id) => _res[id];
        public RecipeConfig Recipe(string id) => _recipes[id];
        public bool HasItem(string id) => _items.ContainsKey(id);

        public string ItemName(string id) => _items.TryGetValue(id, out var i) ? i.Name : id;
        public string LocName(string id) => _locs.TryGetValue(id, out var l) ? l.Name : id;

        public bool IsDaylight(int minute) => Clock.IsDaylight(minute, Config.Time.DaylightStartMinute, Config.Time.DaylightEndMinute);

        /// <summary>当前地点已解锁的相邻连接。</summary>
        public IEnumerable<EdgeConfig> AdjacentUnlocked(GameState s) =>
            Config.Edges.Where(e => e.Touches(s.CurrentLocation) && s.UnlockedEdges.Contains(e.Id));

        public bool IsSheltered(GameState s, string locationId)
        {
            var cfg = _locs[locationId];
            var fac = s.Locations[locationId].Facility;
            return cfg.IsIndoor || (cfg.CanBuildShelter && fac.ShelterBuilt);
        }

        public bool IsBusy(GameState s) => s.Main != null || s.Sub != null;

        // ───────────────────────── 新游戏 ─────────────────────────

        public GameState NewGame(uint seed)
        {
            var c = Config;
            var s = new GameState
            {
                ConfigVersion = c.Version,
                GameMinute = c.Time.StartAbsoluteMinute,
                Seed = seed,
                Rng = Rng.FromSeed(seed),
            };
            s.Stats = new Stats
            {
                Health = c.InitialStats.Health,
                Satiety = c.InitialStats.Satiety,
                Hydration = c.InitialStats.Hydration,
                Energy = c.InitialStats.Energy,
                Wakefulness = c.InitialStats.Wakefulness,
                Morale = c.InitialStats.Morale,
            };
            foreach (var st in Stats.All)
            {
                s.AlertLatched[st] = false;
                s.ZeroLatched[st] = false;
            }

            foreach (var l in c.Locations)
            {
                s.Locations[l.Id] = new LocationState
                {
                    Discovered = l.InitiallyDiscovered || l.InitiallyVisited,
                    Visited = l.InitiallyVisited,
                    Progress = l.InitialProgress,
                };
                if (l.InitiallyVisited) s.CurrentLocation = l.Id;
            }

            foreach (var e in c.Edges.Where(e => e.InitiallyUnlocked))
            {
                s.UnlockedEdges.Add(e.Id);
                s.Locations[e.A].Discovered = true;
                s.Locations[e.B].Discovered = true;
            }

            var day0 = s.GameMinute / Clock.MinutesPerDay;
            foreach (var r in c.Resources)
            {
                s.Resources[r.NodeId] = new ResourceState
                {
                    Available = r.IsUnlimited ? (int?)null : r.Cap,
                    Reserved = 0,
                    LastRefillDay = day0,
                    Discovered = false,
                };
            }

            Visit(s, s.CurrentLocation);

            foreach (var kv in c.StartingCarried) CreateItems(s, kv.Key, kv.Value, ItemInstance.Carried);
            foreach (var kv in c.StartingCampGround) CreateItems(s, kv.Key, kv.Value, s.CurrentLocation);

            Emit(s, GameEventKind.Info, $"{Clock.DayTime(s.GameMinute)}，你在{LocName(s.CurrentLocation)}醒来。目标：活过七天。");
            UpdateFlags(s);
            s.PendingEvents.Clear();
            return s;
        }

        // ───────────────────────── 事件 ─────────────────────────

        private void Emit(GameState s, GameEventKind kind, string text, StatId? stat = null, IEnumerable<string>? highlight = null)
        {
            var ev = new GameEvent { Kind = kind, Minute = s.GameMinute, Text = text, Stat = stat };
            if (highlight != null) ev.HighlightIds.AddRange(highlight);
            s.PendingEvents.Add(ev);
            if (!string.IsNullOrEmpty(text))
            {
                s.Log.Add(new LogEntry { Minute = s.GameMinute, Kind = kind.ToString(), Text = text });
                if (s.Log.Count > 500) s.Log.RemoveRange(0, s.Log.Count - 500);
            }
        }

        public List<GameEvent> DrainEvents(GameState s)
        {
            var list = new List<GameEvent>(s.PendingEvents);
            s.PendingEvents.Clear();
            return list;
        }

        // ───────────────────────── 行动计划与校验 ─────────────────────────

        private sealed class Plan
        {
            public ActionType Type;
            public bool IsSub;
            public bool TravelPaused;
            public string TargetId = "";
            public string Label = "";
            public double BaseWork;
            public int Planned;
            public ItemInstance? Tool;
            public int Units;
            public ResourceConfig? Res;
            public RecipeConfig? Recipe;
            public List<ItemInstance> Catalysts = new List<ItemInstance>();
            public ItemInstance? UseItem;
            public bool IgniteConsumesWood;
            public ItemInstance? Lighter;
            /// <summary>烧水、烹饪选中的锅；烧水开始时要从散水补入锅的批次与数量。</summary>
            public ItemInstance? Pot;
            public List<(ItemInstance inst, int count)> PotFill = new List<(ItemInstance, int)>();
            public List<OutputLine> Outputs = new List<OutputLine>();
            public List<string> Notes = new List<string>();
        }

        private string? Validate(GameState s, ActionRequest r, out Plan plan)
        {
            plan = new Plan { Type = r.Type, TargetId = r.TargetId };
            if (s.Progress.Dead) return "角色已死亡";

            if (s.Main == null)
            {
                plan.IsSub = false;
            }
            else if (s.Main.Status == ActionStatus.Paused && s.Sub == null)
            {
                plan.IsSub = true;
                if (r.Type != ActionType.Use && r.Type != ActionType.Rest && r.Type != ActionType.Ignite)
                    return "暂停中只能补给、休息或点火维护";
                plan.TravelPaused = s.Main.Type == ActionType.Travel;
            }
            else
            {
                return "已有进行中的行动";
            }

            var here = s.CurrentLocation;
            var hereCfg = _locs[here];
            var hereState = s.Locations[here];
            var allowGround = !plan.TravelPaused;

            switch (r.Type)
            {
                case ActionType.Travel:
                {
                    if (!_locs.ContainsKey(r.TargetId)) return "目标地点不存在";
                    if (r.TargetId == here) return "已在此地";
                    var edge = Config.Edges.FirstOrDefault(e => e.Touches(here) && e.Other(here) == r.TargetId);
                    if (edge == null || !s.UnlockedEdges.Contains(edge.Id))
                    {
                        var target = s.Locations[r.TargetId];
                        if (edge == null && target.Discovered) return "请先前往相邻地点";
                        return "尚未发现路线";
                    }
                    plan.BaseWork = edge.BaseMinutes;
                    plan.Label = $"前往{LocName(r.TargetId)}";
                    return null;
                }

                case ActionType.Explore:
                {
                    if (hereState.Progress >= 100) return "已充分探索，可继续搜集";
                    plan.TargetId = here;
                    plan.BaseWork = Config.Actions.ExploreMinutes;
                    plan.Label = "探索周边";
                    return null;
                }

                case ActionType.Gather:
                {
                    if (!_res.TryGetValue(r.TargetId, out var res)) return "资源点不存在";
                    if (res.LocationId != here) return "该资源点不在当前地点";
                    var rs = s.Resources[res.NodeId];
                    if (!rs.Discovered) return "该资源点尚未发现";
                    plan.Res = res;

                    ItemInstance? tool = null;
                    if (res.ToolBonus != null) tool = BestTool(s, res.ToolBonus.ToolTag);
                    plan.Tool = tool;

                    if (res.IsCache)
                    {
                        if ((rs.Available ?? 0) <= 0) return "该容器已搜空，不再补充";
                        plan.Units = 1;
                        foreach (var kv in res.Outputs) plan.Outputs.Add(Line(kv.Key, kv.Value));
                    }
                    else if (res.IsUnlimited)
                    {
                        foreach (var kv in res.Outputs) plan.Outputs.Add(Line(kv.Key, kv.Value));
                    }
                    else
                    {
                        var avail = rs.Available ?? 0;
                        if (avail <= 0)
                            return res.Refills ? $"库存为0，下次00:00补充{res.DailyRefill}" : "已采尽，不再补充";
                        var kv = res.Outputs.First();
                        var per = kv.Value * (tool != null && res.ToolBonus != null ? res.ToolBonus.OutputMultiplier : 1);
                        plan.Units = Math.Min(avail, per);
                        plan.Outputs.Add(Line(kv.Key, plan.Units));
                        if (plan.Units < per) plan.Notes.Add($"库存不足，本次产出{plan.Units}{ItemName(kv.Key)}，耗时不减少");
                    }

                    var minutes = res.BaseMinutes;
                    if (tool != null && res.ToolBonus?.BaseMinutesOverride != null) minutes = res.ToolBonus.BaseMinutesOverride.Value;
                    plan.BaseWork = minutes;
                    plan.Label = res.IsCache ? $"搜集{res.Name}" : $"采集{res.Name}";
                    return null;
                }

                case ActionType.Craft:
                case ActionType.Build:
                {
                    if (!_recipes.TryGetValue(r.TargetId, out var rc)) return "配方不存在";
                    plan.Recipe = rc;
                    plan.Type = rc.IsBuild ? ActionType.Build : ActionType.Craft;
                    if (rc.FacilityResult != null)
                    {
                        if (!hereCfg.CanBuildShelter) return "只能在营地建造";
                        if (FacilityFlag(hereState.Facility, rc.FacilityResult)) return "已建造";
                    }
                    if (rc.Requirement != null)
                    {
                        switch (rc.Requirement.Facility)
                        {
                            case "fire":
                                if (!hereCfg.HasFire) return "只能在营地火堆操作";
                                if (!hereState.Facility.FireLit) return "需要燃烧的火堆";
                                if (hereState.Facility.FuelMinutes < Tick) return "火堆燃料不足，请先投入木材";
                                break;
                            case "camp":
                                if (!hereCfg.CanBuildShelter) return "只能在营地建造";
                                break;
                            case "shelter":
                                if (!hereCfg.CanBuildShelter) return "只能在营地建造";
                                if (!hereState.Facility.ShelterBuilt) return "需要先建成遮蔽棚";
                                break;
                        }
                    }
                    // 催化工具：锅与其他工具都可以在随身或当前地面（拖拽与容器案 5、6.2）
                    foreach (var tag in rc.Catalysts)
                    {
                        var isPot = Config.Items.Any(i => i.ToolTag == tag && i.ContainerId != null);
                        if (isPot)
                        {
                            var pot = SelectPot(s, r.PotInstanceId, out var why);
                            if (pot == null) return why;
                            plan.Pot = pot;
                            plan.Catalysts.Add(pot);
                            continue;
                        }
                        var cat = AccessibleTool(s, tag);
                        if (cat == null)
                        {
                            var name = Config.Items.First(i => i.ToolTag == tag).Name;
                            return $"缺少{name}（需随身或在当前地面）";
                        }
                        plan.Catalysts.Add(cat);
                    }
                    if (plan.Pot != null)
                    {
                        var err = ValidatePot(s, rc, plan);
                        if (err != null) return err;
                    }
                    foreach (var kv in rc.Inputs)
                    {
                        if (rc.Pot != null && kv.Key == rc.Pot.InputItem) continue;
                        var have = AvailableCount(s, kv.Key, true);
                        if (have < kv.Value) return $"缺少{ItemName(kv.Key)}（{have}/{kv.Value}）";
                    }
                    foreach (var kv in rc.Outputs) plan.Outputs.Add(Line(kv.Key, kv.Value));
                    plan.BaseWork = rc.BaseMinutes;
                    plan.Label = rc.IsBuild ? rc.Name : $"制作{rc.Name}";
                    if (rc.Id == "boil") plan.Label = "烧水";
                    if (rc.Id == "cook") plan.Label = "烹饪块根";
                    return null;
                }

                case ActionType.Wait:
                    if (plan.IsSub) return "暂停中只能补给、休息或点火维护";
                    if (!Config.Actions.WaitMinutes.Contains(r.Minutes)) return "等待时长无效";
                    plan.Planned = r.Minutes;
                    plan.Label = $"等待{Clock.Duration(r.Minutes)}";
                    return null;

                case ActionType.Rest:
                    if (!Config.Actions.RestMinutes.Contains(r.Minutes)) return "休息时长无效";
                    plan.Planned = r.Minutes;
                    plan.Label = $"休息{Clock.Duration(r.Minutes)}";
                    return null;

                case ActionType.Sleep:
                    if (plan.IsSub) return "暂停中只能补给、休息或点火维护";
                    if (!Config.Actions.SleepMinutes.Contains(r.Minutes)) return "睡眠时长无效";
                    plan.Planned = r.Minutes;
                    plan.Label = $"睡眠{Clock.Duration(r.Minutes)}";
                    return null;

                case ActionType.Use:
                {
                    ItemInstance? inst;
                    if (r.InstanceId.HasValue)
                    {
                        inst = s.Items.FirstOrDefault(i => i.InstanceId == r.InstanceId.Value);
                        if (inst == null) return "物品不存在";
                        var place = PlaceOf(s, inst);
                        if (!(place == ItemInstance.Carried || (allowGround && place == here)))
                            return plan.TravelPaused ? "移动途中只能使用随身物品" : "只能使用随身或当前地面的物品";
                    }
                    else
                    {
                        if (!_items.ContainsKey(r.TargetId)) return "物品不存在";
                        // 锅内水也可按物品饮用（开发计划 v0.2 A7）
                        inst = UsableBatches(s, r.TargetId, allowGround, true).FirstOrDefault();
                        if (inst == null)
                        {
                            var any = Accessible(s, allowGround, true).FirstOrDefault(i => i.ItemId == r.TargetId);
                            if (any == null) return $"没有可用的{ItemName(r.TargetId)}";
                            inst = any;
                        }
                    }
                    var cfg = _items[inst.ItemId];
                    if (inst.ReservedBy != null) return "该物品已被预留";
                    if (cfg.Use == null)
                        return cfg.RequiresCooking ? $"{cfg.Name}需先烹饪，不可直接食用" : "该物品不能直接使用";
                    if (IsSpoiled(s, inst)) return "已腐败，不能食用";
                    if (cfg.Use.IsBandage && s.BandageReadyAt > s.GameMinute)
                        return $"绷带冷却中，还需{s.BandageReadyAt - s.GameMinute}分钟";
                    plan.UseItem = inst;
                    plan.TargetId = inst.ItemId;
                    plan.Planned = Config.ItemActions.UseMinutes;
                    plan.Label = UseVerb(cfg) + "1份" + cfg.Name;
                    return null;
                }

                case ActionType.Ignite:
                {
                    if (plan.TravelPaused) return "移动途中无法点火";
                    if (!hereCfg.HasFire) return "这里没有火堆";
                    if (hereState.Facility.FireLit) return "火堆已在燃烧";
                    // 打火器可在随身或当前地面（开发计划 v0.2 B1）；拖入的那一把优先
                    ItemInstance? lighter;
                    if (r.InstanceId.HasValue)
                    {
                        lighter = FindItem(s, r.InstanceId.Value);
                        if (lighter == null || _items[lighter.ItemId].ToolTag != "lighter") return "这不是打火器";
                        if (!(lighter.IsCarried || lighter.Owner == here)) return "只能使用随身或当前地面的打火器";
                        if (lighter.ReservedBy != null || IsLocked(s, lighter)) return "行动占用：打火器正被使用";
                    }
                    else lighter = AccessibleTool(s, "lighter");
                    if (lighter == null) return "缺少打火器（需随身或在当前地面）";
                    plan.Lighter = lighter;
                    var fuel = hereState.Facility.FuelMinutes;
                    if (fuel < Config.Fire.MinFuelToIgnite)
                    {
                        if (fuel == 0 && AvailableCount(s, "wood", true) > 0) plan.IgniteConsumesWood = true;
                        else return "请先投入木材";
                    }
                    plan.Planned = Config.Fire.IgniteMinutes;
                    plan.Label = "点燃火堆";
                    return null;
                }
            }
            return "未知行动";
        }

        private OutputLine Line(string itemId, int count) => new OutputLine { ItemId = itemId, Name = ItemName(itemId), Count = count };

        public static string UseVerb(ItemConfig cfg) =>
            cfg.Use != null && cfg.Use.IsBandage ? "使用" : cfg.Kind == "water" ? "饮用" : "食用";

        private static bool FacilityFlag(FacilityState f, string key) => key switch
        {
            "shelterBuilt" => f.ShelterBuilt,
            "bedBuilt" => f.BedBuilt,
            _ => false,
        };

        private static void SetFacility(FacilityState f, string key)
        {
            if (key == "shelterBuilt") f.ShelterBuilt = true;
            else if (key == "bedBuilt") f.BedBuilt = true;
        }

        // ───────────────────────── 开始 / 取消 / 继续 ─────────────────────────

        /// <summary>只做开始前校验，不模拟、不改状态。返回 null 表示可以开始，否则为不能开始的原因。</summary>
        public string? CanStart(GameState s, ActionRequest r) => Validate(s, r, out _);

        /// <summary>空闲时开始主行动；主行动暂停时开始子行动（补给、休息、点火）。</summary>
        public ActionResult Start(GameState s, ActionRequest r)
        {
            var err = Validate(s, r, out var p);
            if (err != null) return ActionResult.Fail(err);

            var a = new ActionState
            {
                ActionId = s.NextActionId++,
                Type = p.Type,
                IsSub = p.IsSub,
                TargetId = p.TargetId,
                SourceLocation = s.CurrentLocation,
                StartMinute = s.GameMinute,
                BaseWorkMinutes = p.BaseWork,
                PlannedMinutes = p.Planned,
                Label = p.Label,
                ForceOutdoor = p.TravelPaused,
            };

            switch (p.Type)
            {
                case ActionType.Gather:
                {
                    var res = p.Res!;
                    var rs = s.Resources[res.NodeId];
                    if (!res.IsUnlimited)
                    {
                        rs.Available = (rs.Available ?? 0) - p.Units;
                        rs.Reserved += p.Units;
                        a.ReservedResourceUnits = p.Units;
                    }
                    if (p.Tool != null)
                    {
                        a.ToolInstanceId = p.Tool.InstanceId;
                        a.LockedItemIds.Add(p.Tool.InstanceId);
                    }
                    if (res.Hazard != null && res.Hazard.Probability > 0)
                        a.HazardHit = s.Rng.NextDouble() < res.Hazard.Probability;
                    break;
                }
                case ActionType.Craft:
                case ActionType.Build:
                {
                    var rc = p.Recipe!;
                    if (p.Pot != null)
                    {
                        a.PotInstanceId = p.Pot.InstanceId;
                        if (rc.Pot != null)
                        {
                            // 烧水：先把缺的散水原子转入锅，再预留锅内水本身（不复制、不另起虚拟锅水）
                            var owner = ItemInstance.ContainerOwner(p.Pot.InstanceId);
                            foreach (var (inst, count) in p.PotFill) MoveItems(s, inst, count, owner);
                            foreach (var c in PotContents(s, p.Pot.InstanceId).Where(i => i.ItemId == rc.Pot.InputItem))
                            {
                                c.ReservedBy = a.ActionId;
                                a.ReservedItemIds.Add(c.InstanceId);
                            }
                        }
                    }
                    foreach (var kv in rc.Inputs)
                    {
                        if (rc.Pot != null && kv.Key == rc.Pot.InputItem) continue;
                        a.ReservedItemIds.AddRange(Reserve(s, kv.Key, kv.Value, a.ActionId));
                    }
                    foreach (var c in p.Catalysts) a.LockedItemIds.Add(c.InstanceId);
                    break;
                }
                case ActionType.Use:
                {
                    var inst = p.UseItem!;
                    var cfg = _items[inst.ItemId];
                    var u = cfg.Use!;
                    ConsumeOne(s, inst);
                    var parts = new List<string>();
                    if (u.Hydration != 0) { s.Stats.Add(StatId.Hydration, u.Hydration); parts.Add($"水分+{u.Hydration:0}"); }
                    if (u.Satiety != 0) { s.Stats.Add(StatId.Satiety, u.Satiety); parts.Add($"饱食+{u.Satiety:0}"); }
                    if (u.Morale != 0) { s.Stats.Add(StatId.Morale, u.Morale); parts.Add($"精神+{u.Morale:0}"); }
                    if (u.Health != 0) { s.Stats.Add(StatId.Health, u.Health); parts.Add($"健康+{u.Health:0}"); }
                    if (u.CausesDiarrhea)
                    {
                        s.DiarrheaUntil = s.GameMinute + Config.ItemActions.DiarrheaMinutes;
                        parts.Add("出现腹泻（12小时）");
                    }
                    if (u.IsBandage) s.BandageReadyAt = s.GameMinute + Config.ItemActions.BandageCooldownMinutes;
                    Emit(s, GameEventKind.Info, $"{p.Label}：{string.Join("，", parts)}。");
                    break;
                }
                case ActionType.Ignite:
                {
                    var fac = s.Locations[s.CurrentLocation].Facility;
                    if (p.IgniteConsumesWood)
                    {
                        var wood = UsableBatches(s, "wood", true).First();
                        ConsumeOne(s, wood);
                        fac.FuelMinutes += Config.Fire.BurnMinutesPerWood;
                    }
                    fac.FireLit = true;
                    a.LockedItemIds.Add(p.Lighter!.InstanceId);
                    break;
                }
            }

            if (p.IsSub) s.Sub = a; else s.Main = a;
            if (p.Type != ActionType.Use)
                Emit(s, GameEventKind.ActionStarted, $"开始{a.Label}。");
            return ActionResult.Success(a.ActionId);
        }

        /// <summary>在当前时间段边界取消：先取消子行动，否则取消主行动。已流逝的时间与身体消耗不返还。</summary>
        public ActionResult Cancel(GameState s)
        {
            if (s.Progress.Dead) return ActionResult.Fail("角色已死亡");
            var a = s.Sub ?? s.Main;
            if (a == null) return ActionResult.Fail("没有进行中的行动");
            if (a.Type == ActionType.Use || a.Type == ActionType.Ignite) return ActionResult.Fail("该行动不可中途取消");

            Release(s, a, s.CurrentLocation);
            if (a.IsSub) s.Sub = null; else s.Main = null;

            string text = a.Type switch
            {
                ActionType.Travel => $"已取消移动，留在{LocName(a.SourceLocation)}；已经流逝的时间与身体消耗保留。",
                ActionType.Explore => "已取消探索，探索度不变；已消耗的时间保留。",
                ActionType.Gather => "已取消搜集，未获得产物，预留库存已释放。",
                ActionType.Craft when a.PotInstanceId.HasValue && !a.LegacyGroundOutput && _recipes[a.TargetId].Pot != null
                    => $"已取消{a.Label}，{ItemName(_recipes[a.TargetId].Pot!.InputItem)}仍留在原锅，加工进度清零。",
                ActionType.Craft or ActionType.Build => "已取消制作，预留材料已返还当前地面，未生成半成品。",
                ActionType.Sleep => "已提前醒来。",
                _ => $"已结束{a.Label}。",
            };
            Emit(s, GameEventKind.ActionCancelled, text);
            UpdateFlags(s);
            return ActionResult.Success(a.ActionId);
        }

        /// <summary>暂停中的主行动继续推进。</summary>
        public ActionResult Resume(GameState s)
        {
            if (s.Progress.Dead) return ActionResult.Fail("角色已死亡");
            if (s.Main == null || s.Main.Status != ActionStatus.Paused) return ActionResult.Fail("没有暂停中的行动");
            if (s.Sub != null) return ActionResult.Fail("请等待当前补给或维护完成");
            s.Main.Status = ActionStatus.Running;
            s.Main.PauseReason = PauseReason.None;
            s.Main.PauseStat = null;
            return ActionResult.Success(s.Main.ActionId);
        }

        /// <summary>
        /// 释放行动的全部预留：资源库存归还，预留的散物品返还指定地点地面；
        /// 锅内预留的水只解除预留、留在原锅（烧水取消不复制到地面，拖拽与容器案 6.2）。
        /// </summary>
        private void Release(GameState s, ActionState a, string groundLocation)
        {
            if (a.Type == ActionType.Gather && a.ReservedResourceUnits > 0)
            {
                var rs = s.Resources[a.TargetId];
                rs.Reserved -= a.ReservedResourceUnits;
                rs.Available = (rs.Available ?? 0) + a.ReservedResourceUnits;
                a.ReservedResourceUnits = 0;
            }
            if (a.ReservedItemIds.Count > 0)
            {
                var owners = new HashSet<string> { groundLocation };
                foreach (var id in a.ReservedItemIds)
                {
                    var inst = s.Items.FirstOrDefault(i => i.InstanceId == id);
                    if (inst == null) continue;
                    inst.ReservedBy = null;
                    if (inst.InContainer) owners.Add(inst.Owner);
                    else inst.Owner = groundLocation;
                }
                a.ReservedItemIds.Clear();
                foreach (var o in owners) Normalize(s, o);
            }
            a.LockedItemIds.Clear();
        }

        /// <summary>当前可从该地面堆拾取的最大数量（受30公斤上限约束；锅按锅加内容计）。</summary>
        public int MaxPickup(GameState s, ItemInstance inst)
        {
            var w = inst.Count > 0 ? ItemGrams(s, inst) / inst.Count : _items[inst.ItemId].WeightGrams;
            var room = Config.Carry.HardGrams - CarriedGrams(s);
            if (room < 0) room = 0;
            return Math.Min(inst.Count, room / w);
        }
    }
}

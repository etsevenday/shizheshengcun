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
        private readonly Dictionary<string, WeaponConfig> _weapons;
        private readonly Dictionary<string, EnemyConfig> _enemies;

        private const double Eps = 1e-6;

        public GameEngine(GameConfig config)
        {
            Config = config;
            _items = config.Items.ToDictionary(i => i.Id);
            _locs = config.Locations.ToDictionary(l => l.Id);
            _edges = config.Edges.ToDictionary(e => e.Id);
            _res = config.Resources.ToDictionary(r => r.NodeId);
            _recipes = config.Recipes.ToDictionary(r => r.Id);
            _weapons = config.Weapons.ToDictionary(w => w.Id);
            _enemies = config.Enemies.ToDictionary(x => x.Id);
        }

        /// <summary>该物品（或 "fists"）的武器定义；不是武器为 null。</summary>
        public WeaponConfig? Weapon(string id) => _weapons.TryGetValue(id, out var w) ? w : null;
        public EnemyConfig Enemy(string id) => _enemies[id];

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

        /// <summary>角色忙碌：有主/子行动、战斗中或敌踪待选（世界交互、拖拽、即时操作统一拒绝）。</summary>
        public bool IsBusy(GameState s) => s.Main != null || s.Sub != null || s.Battle != null || s.Encounter != null;

        /// <summary>
        /// 当前状态异常（v0.7 第 6.1 节）：腹泻剩余时间、绷带冷却剩余。右栏人物页与左栏 HUD 读同一份数据，不各自维护。
        /// </summary>
        public List<ActiveCondition> ActiveConditions(GameState s)
        {
            var list = new List<ActiveCondition>();
            if (s.DiarrheaUntil.HasValue && s.DiarrheaUntil.Value > s.GameMinute)
            {
                var extra = -Config.ItemActions.DiarrheaExtraHydrationPerHour;
                list.Add(new ActiveCondition
                {
                    Id = "diarrhea", Name = "腹泻", RemainingMinutes = s.DiarrheaUntil.Value - s.GameMinute,
                    Effect = $"每小时额外失水{Fmt.T(extra)}", Harmful = true,
                });
            }
            var bandage = Config.Items.FirstOrDefault(i => i.Use != null && i.Use.IsBandage)?.Name ?? "绷带";
            if (s.BandageReadyAt > s.GameMinute)
            {
                list.Add(new ActiveCondition
                {
                    Id = "bandageCooldown", Name = "绷带冷却", RemainingMinutes = s.BandageReadyAt - s.GameMinute,
                    Effect = $"冷却结束前{bandage}不回血（仍可止血或压病毒）",
                });
            }
            var b = s.Body;
            if (b.BleedTriggers > 0)
                list.Add(new ActiveCondition
                {
                    Id = "bleed", Name = "出血", RemainingMinutes = 0, Harmful = true,
                    Effect = $"还要流血{b.BleedTriggers}次，每次健康−{Fmt.T(b.BleedDamage)}；{bandage}可止血",
                });
            if (s.Story.BaobaoPresent)
            {
                var input = BaobaoInput(s, b.Immunity);
                var bi = Config.Infection.BaobaoInput;
                list.Add(new ActiveCondition
                {
                    Id = "baobao", Name = "饱饱持续影响", RemainingMinutes = 0, Harmful = input > 0,
                    Effect = input > 0
                        ? $"体内病毒每小时+{Fmt.T(input)}（免疫越高越慢，免疫≥{Fmt.T(bi.FullSuppressImmunityAtLeast)}完全压制）；治愈只清当前感染，饱饱仍在"
                        : $"免疫≥{Fmt.T(bi.FullSuppressImmunityAtLeast)}，饱饱的病毒输入被完全压制；免疫跌下来后会重新积累",
                });
            }
            if (b.Infected)
            {
                var (_, vr, _) = CurrentBodyRates(s);
                list.Add(new ActiveCondition
                {
                    Id = "infected", Name = "病毒感染", RemainingMinutes = 0, Harmful = true,
                    Effect = $"病毒{Fmt.N(b.VirusLoad)}，每小时{Fmt.SignedT(vr)}；达到{Fmt.T(Config.Infection.ZombificationAtLoad)}尸变。高免疫已持续{Clock.Duration(b.HighImmunityMinutes)}/{Clock.Duration(Config.Infection.NaturalCure.Minutes)}",
                });
            }
            if (b.AntibioticUntil.HasValue && b.AntibioticUntil.Value > s.GameMinute)
                list.Add(new ActiveCondition
                {
                    Id = "antibiotic", Name = "抗生素作用中", RemainingMinutes = b.AntibioticUntil.Value - s.GameMinute,
                    Effect = $"病毒正增长×{Fmt.T(Config.Infection.Antibiotics.PositiveProgressFactor)}",
                });
            if (b.AntibioticReadyAt > s.GameMinute)
                list.Add(new ActiveCondition
                {
                    Id = "antibioticCooldown", Name = "抗生素给药间隔", RemainingMinutes = b.AntibioticReadyAt - s.GameMinute,
                    Effect = "间隔结束前不能再用抗生素",
                });
            if (b.Infected && b.BandageVirusReadyAt > s.GameMinute)
                list.Add(new ActiveCondition
                {
                    Id = "bandageVirusCooldown", Name = "绷带病毒减量冷却", RemainingMinutes = b.BandageVirusReadyAt - s.GameMinute,
                    Effect = $"冷却结束前{bandage}不压病毒",
                });
            return list;
        }

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
                HealthMax = c.StateScale.HealthMax,
                EnergyMax = c.StateScale.EnergyMax,
            };
            s.Body = new BodyState
            {
                Immunity = c.Immunity.Initial,
                Infected = c.Infection.InitialInfected,
                VirusLoad = c.Infection.InitialLoad,
            };
            foreach (var st in Stats.All)
            {
                s.AlertLatched[st] = false;
                s.ZeroLatched[st] = false;
            }

            foreach (var l in c.Locations)
            {
                // 未访问地点也从开局起维护清理次数，只是界面不展示（开发计划 v0.2 A6）
                s.Locations[l.Id] = new LocationState
                {
                    Discovered = l.InitiallyDiscovered || l.InitiallyVisited,
                    Visited = l.InitiallyVisited,
                    Progress = l.InitialProgress,
                    ClearCharges = l.Clear.InitialCharges,
                    ClearRefillProgress = 0,
                    ClearUpdatedMinute = s.GameMinute,
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
            InitSkills(s);
            InitWorldEnemies(s);
            InitStory(s);

            foreach (var kv in c.StartingCarried) CreateItems(s, kv.Key, kv.Value, ItemInstance.Carried);
            foreach (var kv in c.StartingCampGround) CreateItems(s, kv.Key, kv.Value, s.CurrentLocation);
            // 有火堆的地点初始化设施 ID 与空加工位（营地 camp:fire）
            SyncFacilities(s);

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
            public bool ExplicitTool;
            public int Units;
            public ResourceConfig? Res;
            public RecipeConfig? Recipe;
            public List<ItemInstance> Catalysts = new List<ItemInstance>();
            public ItemInstance? UseItem;
            public bool IgniteConsumesWood;
            public ItemInstance? Lighter;
            public List<OutputLine> Outputs = new List<OutputLine>();
            public List<string> Notes = new List<string>();
            public int Batch = 1;
            public List<string> CookTargets = new List<string>();
            public List<int> CookTargetItems = new List<int>();
        }

        private string? Validate(GameState s, ActionRequest r, out Plan plan)
        {
            plan = new Plan { Type = r.Type, TargetId = r.TargetId };
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "战斗中：请使用战斗指令";
            if (s.Encounter != null && r.Type != ActionType.Bypass) return "发现敌踪：请先选择接敌或绕行";
            if (r.Type == ActionType.Bypass)
            {
                if (s.Encounter == null) return "当前没有需要绕行的敌踪";
                plan.Planned = Config.Encounters.BypassMinutes;
                plan.Label = "绕行敌踪";
                plan.TargetId = s.Encounter.LocationId;
                return null;
            }

            if (s.Main == null)
            {
                plan.IsSub = false;
            }
            else if (s.Main.Status == ActionStatus.Paused && s.Sub == null)
            {
                plan.IsSub = true;
                if (r.Type == ActionType.WaitCook) return "主行动暂停中：请先退出原行动再等待烹饪";
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
                    if (hereState.Progress >= 100) return "已充分探索，可清理或搜集已发现资源";
                    plan.TargetId = here;
                    plan.BaseWork = Config.Actions.ExploreMinutes;
                    plan.Label = "探索周边";
                    return null;
                }

                case ActionType.Clear:
                {
                    if (!hereState.Visited) return "只能清理已访问的当前地点";
                    var cap = hereCfg.Clear.Cap;
                    var (charges, progress) = ClearChargesAt(s, here);
                    if (charges < 1)
                        return $"清理次数0/{cap}，{Clock.Duration(hereCfg.Clear.RefillIntervalMinutes - progress)}后恢复1次";
                    plan.TargetId = here;
                    plan.BaseWork = Config.Actions.ClearMinutes;
                    plan.Label = "清理";
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
                    if (r.ToolInstanceId.HasValue)
                    {
                        // 显式拖入的工具：必须随身、标签匹配、未损坏；失效时不暗中换另一把（拖拽与容器案 5）
                        tool = FindItem(s, r.ToolInstanceId.Value);
                        if (tool == null) return "这件工具已不在，请重新选择";
                        var tag = _items[tool.ItemId].ToolTag;
                        if (res.ToolBonus == null || tag != res.ToolBonus.ToolTag) return "这件工具对该资源点没有加成";
                        if (!tool.IsCarried) return "先拾取工具";
                        if (tool.ReservedBy != null || IsLocked(s, tool)) return "行动占用：这件工具正被使用";
                        if (!HasWear(tool, GatherWear)) return tool.Durability <= 0 ? "这件工具已损坏，请重新选择" : $"这件工具耐久不足（{DurabilityText(tool)}），请维护或重新选择";
                        plan.ExplicitTool = true;
                    }
                    else if (res.ToolBonus != null) tool = BestTool(s, res.ToolBonus.ToolTag);
                    plan.Tool = tool;

                    if (res.IsCache)
                    {
                        if ((rs.Available ?? 0) <= 0) return "该容器已搜空，不再补充";
                        plan.Units = 1;
                        foreach (var kv in res.Outputs) plan.Outputs.Add(Line(kv.Key, kv.Value));
                        if (res.ExclusiveLoot != null)
                            plan.Notes.Add("随机得到其一：" + string.Join("；", res.ExclusiveLoot.Select(x => $"{ItemName(x.ItemId)}×{x.Count}（{x.Weight}%）")));
                        if (res.IndependentRare != null)
                            plan.Notes.Add($"另有{Fmt.Pct(res.IndependentRare.Chance)}独立概率附带{ItemName(res.IndependentRare.ItemId)}×{res.IndependentRare.Count}");
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
                    plan.Type = rc.IsBuild ? ActionType.Build : rc.IsRepair ? ActionType.Repair : ActionType.Craft;
                    var batch = Math.Max(1, r.BatchCount);
                    if (batch > 1 && (rc.IsBuild || rc.IsRepair)) return "建造与维护每次只能做1件";
                    plan.Batch = batch;
                    foreach (var kv in rc.MinSkills)
                    {
                        var lv = SkillLevel(s, kv.Key);
                        if (lv < kv.Value) return $"需要{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）";
                    }
                    if (rc.IsRepair)
                    {
                        var why = RepairBlocked(s, r.InstanceId, out var target);
                        if (why != null) return why;
                        plan.Tool = target;
                    }
                    if (rc.NeedsExplicitPot)
                    {
                        // v0.6 §11：须明确指定一口取下并清空的锅（不消耗）；净水作为原料消耗，不自动装锅
                        var why = ExplicitPotBlocked(s, r.PotInstanceId, out var pot);
                        if (why != null) return why;
                        plan.Catalysts.Add(pot!);
                    }
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
                                if (!hereCfg.CanBuildShelter) return rc.IsBuild ? "只能在营地建造" : "只能在营地制作";
                                break;
                            case "shelter":
                                if (!hereCfg.CanBuildShelter) return "只能在营地建造";
                                if (!hereState.Facility.ShelterBuilt) return "需要先建成遮蔽棚";
                                break;
                        }
                    }
                    // 催化工具可以在随身或当前地面（拖拽与容器案 5）；烧水、烤制、水煮已改为设施自动加工，不再是配方
                    foreach (var tag in rc.Catalysts)
                    {
                        var cat = AccessibleTool(s, tag);
                        if (cat == null)
                        {
                            var name = Config.Items.First(i => i.ToolTag == tag).Name;
                            return $"缺少{name}（需随身或在当前地面）";
                        }
                        plan.Catalysts.Add(cat);
                    }
                    foreach (var kv in rc.Inputs)
                    {
                        var have = AvailableCount(s, kv.Key, true);
                        var need = kv.Value * batch;
                        if (have < need) return $"缺少{ItemName(kv.Key)}（{have}/{need}）";
                    }
                    // 材料槽里手工指定的批次：仍须可用且属于本配方，否则要求刷新重选
                    foreach (var id in r.PreferredInstanceIds)
                    {
                        var b = FindItem(s, id);
                        if (b == null || !rc.Inputs.ContainsKey(b.ItemId) || !UsableBatches(s, b.ItemId, true).Contains(b))
                            return "指定的材料批次已失效，请重新选择";
                    }
                    foreach (var kv in rc.Outputs) plan.Outputs.Add(Line(kv.Key, kv.Value * batch));
                    // 制造熟练度在开始时折减基础工作量（v0.6 §03）；同批次多件合并成一个工作量，段内结转余量
                    plan.BaseWork = rc.BaseMinutes * batch * CraftWorkFactor(s);
                    plan.Label = rc.IsBuild ? rc.Name : rc.IsRepair ? $"维护{ItemName(plan.Tool!.ItemId)}#{plan.Tool.InstanceId}"
                        : batch > 1 ? $"制作{rc.Name}×{batch}" : $"制作{rc.Name}";
                    return null;
                }

                case ActionType.Butcher:
                    if (plan.IsSub) return "暂停中只能补给、休息或点火维护";
                    return ValidateButcher(s, r, plan);

                case ActionType.Fuse:
                    return ValidateFuse(s, r, plan, allowGround);

                case ActionType.WaitCook:
                    return ValidateCookWait(s, plan);

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
                    if (inst.InContainer && IsLiquid(inst) && PotFreeWaterUnits(s, inst) < 1) return ReservedWaterText(s, inst);
                    if (cfg.Use == null)
                        return cfg.RequiresCooking ? $"{cfg.Name}需先烹饪，不可直接食用" : "该物品不能直接使用";
                    if (IsSpoiled(s, inst)) return "已腐败，不能食用";
                    // Q16：受热保护只保这次烹饪能完成，不让已过原保质期的原料被直接吃掉（开发方保守做法）
                    if (PastExpiryButProtected(s, inst)) return "已过原保质期，只受本次烹饪保护，不能直接食用";
                    if (cfg.Use.IsMedicine && MedicineBlocked(s, cfg.Use) is string medWhy) return medWhy;
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
                    // 点火必须指定一把打火器：拖到火堆即指定，按钮点火也要先选（策划答复 2026-10-24 第 1 条）
                    if (!r.InstanceId.HasValue)
                        return AccessibleTool(s, "lighter") == null ? "缺少打火器（需随身或在当前地面）" : "请选择要使用的打火器";
                    // 选中的工具失效时提示重新选择，不暗换另一把（v0.7 第 4.3 节）
                    var lighter = FindItem(s, r.InstanceId.Value);
                    if (lighter == null) return "这把打火器已不在，请重新选择";
                    if (_items[lighter.ItemId].ToolTag != "lighter") return "这不是打火器";
                    if (!(lighter.IsCarried || lighter.Owner == here)) return "只能使用随身或当前地面的打火器";
                    if (lighter.ReservedBy != null || IsLocked(s, lighter)) return "行动占用：打火器正被使用";
                    // v0.6 §05：每次成功点火磨损1，耐久不足一次磨损时拒绝（可维护或换一把）
                    if (!HasWear(lighter, LighterWear)) return $"这把打火器耐久不足（{DurabilityText(lighter)}），请维护或换一把";
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

        /// <summary>自用物品的健康恢复量：绷带按医疗公式（v0.6 §03），其他物品为配置值。</summary>
        public double UseHealthAmount(GameState s, ItemUseConfig u) => u.IsBandage ? BandageHeal(s) : u.Health;

        /// <summary>“水分+5”“水分−2”：正负号如实显示，避免“水分+-2”。</summary>
        public static string Signed(string stat, double v) => stat + Fmt.SignedT(v);

        /// <summary>食用说明，如“饱食+25，水分−2，精神+2”。</summary>
        public static string UseEffectText(ItemUseConfig u)
        {
            var parts = new List<string>();
            if (u.Satiety != 0) parts.Add(Signed("饱食", u.Satiety));
            if (u.Hydration != 0) parts.Add(Signed("水分", u.Hydration));
            if (u.Morale != 0) parts.Add(Signed("精神", u.Morale));
            if (u.Health != 0) parts.Add(Signed("健康", u.Health));
            if (u.IsAntibiotic) parts.Add("抑制病毒");
            if (u.IsAntiviral) parts.Add("清除感染");
            return string.Join("，", parts);
        }

        public static string UseVerb(ItemConfig cfg) =>
            cfg.Use != null && cfg.Use.IsMedicine ? "使用" : cfg.Kind == "water" ? "饮用" : "食用";

        /// <summary>自用带既有风险（配置 causesDiarrhea，脏水）：按钮写“饮用1份（风险）”（v0.7 第 7 节）。</summary>
        public static bool UseRisky(ItemConfig cfg) => cfg.Use != null && cfg.Use.CausesDiarrhea;

        /// <summary>是否在玩法白名单内可对自身使用（配置 use 不为空）；材料、工具、空锅、生块根为否。</summary>
        public static bool CanSelfUse(ItemConfig cfg) => cfg.Use != null;

        /// <summary>卡下自用按钮文案：“食用1份”“饮用1份”“饮用1份（风险）”“使用1份”。</summary>
        public static string UseButtonText(ItemConfig cfg) => UseVerb(cfg) + "1份" + (UseRisky(cfg) ? "（风险）" : "");

        /// <summary>风险后果，写在按钮旁：“必定腹泻12小时：期间每小时额外失水3”；无风险为 null。</summary>
        public string? UseRiskText(ItemConfig cfg)
        {
            if (!UseRisky(cfg)) return null;
            var extra = -Config.ItemActions.DiarrheaExtraHydrationPerHour;
            return $"必定腹泻{Clock.Duration(Config.ItemActions.DiarrheaMinutes)}：期间每小时额外失水{Fmt.T(extra)}";
        }

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
            // 重发同一开始请求不再扣次数、不再抽签（清理案 C15、探索案 E05）；点火同一 requestId 不重复点火（v0.7 第 4.3 节）
            if (SeenRequest(s, r.RequestId)) return ActionResult.Repeated();
            var err = Validate(s, r, out var p);
            if (err != null) return ActionResult.Fail(err);
            RememberRequest(s, r.RequestId);

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
                case ActionType.Explore:
                    a.StartProgress = s.Locations[s.CurrentLocation].Progress;
                    PlanLoot(s, a, s.CurrentLocation);
                    // 随机接敌判定开始时锁定（v0.6 §15），完成时按当时的本地敌对密度比较；读档不重抽
                    a.EncounterRoll = s.Rng.NextDouble();
                    break;
                case ActionType.Bypass:
                    s.Encounter = null;
                    break;
                case ActionType.Butcher:
                    StartButcher(s, a, p);
                    break;
                case ActionType.Clear:
                {
                    // 先按世界时间更新再扣 1 次；取消、死亡、空结果都不退（地图清理案 3.1、6）
                    UpdateClearCharges(s, s.CurrentLocation);
                    s.Locations[s.CurrentLocation].ClearCharges -= 1;
                    a.ChargeSpent = true;
                    PlanLoot(s, a, s.CurrentLocation);
                    a.EncounterRoll = s.Rng.NextDouble();
                    break;
                }
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
                        a.ExplicitTool = p.ExplicitTool;
                    }
                    if (res.Hazard != null && res.Hazard.Probability > 0)
                        a.HazardHit = s.Rng.NextDouble() < res.Hazard.Probability;
                    // 箱子的随机部分开始时锁定，完成才公布，读档不重抽（v0.6 §11）
                    if (res.IndependentRare != null) a.RareHit = s.Rng.NextDouble() < res.IndependentRare.Chance;
                    if (res.ExclusiveLoot != null && res.ExclusiveLoot.Count > 0)
                    {
                        var roll = (int)Math.Floor(s.Rng.NextDouble() * 100);
                        var acc = 0;
                        var pick = res.ExclusiveLoot[res.ExclusiveLoot.Count - 1];
                        foreach (var x in res.ExclusiveLoot) { acc += x.Weight; if (roll < acc) { pick = x; break; } }
                        a.LootPlanned = true;
                        a.LootRoll = roll;
                        a.LootItemId = pick.ItemId;
                        a.LootCount = pick.Count;
                    }
                    break;
                }
                case ActionType.Craft:
                case ActionType.Build:
                case ActionType.Repair:
                {
                    var rc = p.Recipe!;
                    a.BatchCount = p.Batch;
                    foreach (var kv in rc.Inputs)
                    {
                        a.ReservedItemIds.AddRange(Reserve(s, kv.Key, kv.Value * p.Batch, a.ActionId, r.PreferredInstanceIds));
                    }
                    foreach (var c in p.Catalysts) a.LockedItemIds.Add(c.InstanceId);
                    if (rc.IsRepair && p.Tool != null)
                    {
                        a.ToolInstanceId = p.Tool.InstanceId;
                        a.LockedItemIds.Add(p.Tool.InstanceId);
                    }
                    break;
                }
                case ActionType.Use:
                {
                    var inst = p.UseItem!;
                    var cfg = _items[inst.ItemId];
                    var u = cfg.Use!;
                    ConsumeOne(s, inst);
                    if (inst.InContainer || inst.InFacility) SyncFacilities(s);
                    var parts = new List<string>();
                    // 即时效果先应用并夹紧 0—100，再按 wait 推进 5 分钟；负数如实写“水分−2”（设施与烹饪案 5.3）
                    if (u.Satiety != 0) { s.Stats.Add(StatId.Satiety, u.Satiety); parts.Add(Signed("饱食", u.Satiety)); }
                    if (u.Hydration != 0) { s.Stats.Add(StatId.Hydration, u.Hydration); parts.Add(Signed("水分", u.Hydration)); }
                    if (u.Morale != 0) { s.Stats.Add(StatId.Morale, u.Morale); parts.Add(Signed("精神", u.Morale)); }
                    if (u.Health != 0 && !u.IsBandage) { s.Stats.Add(StatId.Health, u.Health); parts.Add(Signed("健康", u.Health)); }
                    if (u.CausesDiarrhea)
                    {
                        s.DiarrheaUntil = s.GameMinute + Config.ItemActions.DiarrheaMinutes;
                        parts.Add("出现腹泻（12小时）");
                    }
                    // 绷带、抗生素、特效药：效果在确认时应用，各自冷却只在对应效果实际生效时启动（v0.6 §11）
                    if (u.IsMedicine) ApplyMedicine(s, u, parts);
                    // 效果在开始时一次结清、不可取消，所以按“行动完成”发事件，界面的结果提示条才会跟着更新
                    Emit(s, GameEventKind.ActionCompleted, $"{p.Label}：{string.Join("，", parts)}。");
                    break;
                }
                case ActionType.Fuse:
                    StartFuse(s, a, p);
                    break;
                case ActionType.WaitCook:
                    a.CookTargets = new List<string>(p.CookTargets);
                    a.CookTargetItems = new List<int>(p.CookTargetItems);
                    break;
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
                    a.ToolInstanceId = p.Lighter.InstanceId;
                    break;
                }
            }

            if (p.IsSub) s.Sub = a; else s.Main = a;
            if (p.Type != ActionType.Use && p.Type != ActionType.Fuse)
                Emit(s, GameEventKind.ActionStarted, $"开始{a.Label}。");
            return ActionResult.Success(a.ActionId);
        }

        /// <summary>在当前时间段边界取消：先取消子行动，否则取消主行动。已流逝的时间与身体消耗不返还。</summary>
        public ActionResult Cancel(GameState s)
        {
            if (s.Progress.Dead) return ActionResult.Fail("角色已死亡");
            if (DialogueLocked(s)) return ActionResult.Fail(DialogueLockedReason);
            var a = s.Sub ?? s.Main;
            if (a == null) return ActionResult.Fail("没有进行中的行动");
            if (a.Type == ActionType.Use || a.Type == ActionType.Ignite || a.Type == ActionType.Fuse) return ActionResult.Fail("该行动不可中途取消");

            Release(s, a, s.CurrentLocation);
            if (a.IsSub) s.Sub = null; else s.Main = null;

            string text = a.Type switch
            {
                ActionType.Travel => $"已取消移动，留在{LocName(a.SourceLocation)}；已经流逝的时间与身体消耗保留。",
                ActionType.Explore => "已取消探索，探索度不变，未获得物资；已消耗的时间保留。",
                ActionType.Clear => "已取消清理，未获得物资；已扣除的1次清理机会不返还，已消耗的时间保留。",
                ActionType.Gather => "已取消搜集，未获得产物，预留库存已释放。",
                ActionType.Craft or ActionType.Build => "已取消制作，预留材料已返还当前地面，未生成半成品。",
                ActionType.Sleep => "已提前醒来。",
                ActionType.Butcher => "已取消处理尸体，尸体返还当前地面，保质期不变，没有产物。",
                ActionType.Bypass => "已取消绕行。",
                ActionType.WaitCook => $"已退出等待烹饪（已完成{a.CookDone.Count}项）：火堆、锅与受热进度原样保留，成品留在原位。",
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
            if (DialogueLocked(s)) return ActionResult.Fail(DialogueLockedReason);
            if (s.Main == null || s.Main.Status != ActionStatus.Paused) return ActionResult.Fail("没有暂停中的行动");
            if (s.Sub != null) return ActionResult.Fail("请等待当前补给或维护完成");
            s.Main.Status = ActionStatus.Running;
            s.Main.PauseReason = PauseReason.None;
            s.Main.PauseStat = null;
            return ActionResult.Success(s.Main.ActionId);
        }

        /// <summary>释放行动的全部预留：资源库存归还，预留的物品返还指定地点地面。</summary>
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
                    inst.Owner = groundLocation;
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

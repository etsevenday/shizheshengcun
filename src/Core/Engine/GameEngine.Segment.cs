using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    public sealed partial class GameEngine
    {
        /// <summary>一个 5 分钟段开始时确定的全部变化率（每小时）。</summary>
        public sealed class SegmentContext
        {
            public RateBlock Rates = new RateBlock();
            public double HealthPerHour;
            public double Factor = 1;
            public bool IsWork;
            public bool Sheltered;
            public string DamageCause = "";
            public List<string> FactorReasons = new List<string>();
        }

        private static string RateKey(ActionType t) => t switch
        {
            ActionType.Travel => "travel",
            ActionType.Explore => "explore",
            ActionType.Gather => "gather",
            ActionType.Clear => "clear",
            ActionType.Craft => "craft",
            ActionType.Build => "build",
            ActionType.Ignite => "ignite",
            ActionType.Use => "use",
            ActionType.Rest => "rest",
            _ => "wait",
        };

        /// <summary>按段首状态计算本段速率、工作倍率、归零伤害与自然恢复（求生案 01/02/04/05）。</summary>
        public SegmentContext ComputeSegment(GameState s, ActionState a)
        {
            var ctx = new SegmentContext();
            var st = s.Stats;
            var loc = s.CurrentLocation;
            var locCfg = _locs[loc];
            var fac = s.Locations[loc].Facility;
            var c = Config;

            ctx.Sheltered = !a.ForceOutdoor && IsSheltered(s, loc);
            ctx.IsWork = a.IsWorkType;

            string key;
            if (a.Type == ActionType.Sleep) key = ctx.Sheltered ? "sleep_sheltered" : "sleep_outdoors";
            else
            {
                var raw = RateKey(a.Type);
                key = c.Actions.RateAliases.TryGetValue(raw, out var alias) ? alias : raw;
            }
            ctx.Rates = c.HourlyRates[key].Copy();

            if (a.Type == ActionType.Rest && !a.ForceOutdoor && locCfg.CanBuildShelter && fac.ShelterBuilt)
                ctx.Rates.Morale += c.FacilityEffects.ShelteredCampRestMoralePerHour;
            if (a.Type == ActionType.Sleep && ctx.Sheltered && fac.BedBuilt)
                ctx.Rates.Energy += c.FacilityEffects.BedExtraSleepEnergyPerHour;
            if ((a.Type == ActionType.Rest || a.Type == ActionType.Sleep) && ctx.Rates.Energy > 0
                && c.Thresholds["morale"].IsBelow(st.Morale))
                ctx.Rates.Energy *= c.FacilityEffects.LowMoraleEnergyRecoveryFactor;
            if (s.DiarrheaUntil.HasValue && s.DiarrheaUntil.Value > s.GameMinute)
                ctx.Rates.Hydration += c.ItemActions.DiarrheaExtraHydrationPerHour;

            // 归零伤害：段首为0才计（求生案 02 统一伤害计算）
            var hr = c.HealthRules;
            var causes = new List<string>();
            double health = 0;
            if (st.Satiety <= 0) { health += hr.ZeroSatietyPerHour; causes.Add("饥饿"); }
            if (st.Hydration <= 0) { health += hr.ZeroHydrationPerHour; causes.Add("缺水"); }
            if (st.Wakefulness <= 0) { health += hr.ZeroWakefulnessPerHour; causes.Add("缺乏睡眠"); }
            if (st.Energy <= 0 && ctx.IsWork) { health += hr.ZeroEnergyWhileWorkingPerHour; causes.Add("体力透支"); }
            if (causes.Count > 0) ctx.DamageCause = causes[0];

            // 自然恢复：休息或睡眠、遮蔽、段首饱食与水分≥40、精神≥20（求生案 04）
            if ((a.Type == ActionType.Rest || a.Type == ActionType.Sleep) && ctx.Sheltered
                && st.Satiety >= hr.RecoveryMinSatiety && st.Hydration >= hr.RecoveryMinHydration
                && st.Morale >= hr.RecoveryMinMorale)
                health += hr.ShelteredRestRecoveryPerHour;
            ctx.HealthPerHour = health;

            if (ctx.IsWork)
            {
                ctx.Factor = WorkFactor(s, a.Type, ctx.FactorReasons);
            }
            return ctx;
        }

        /// <summary>M = min(上限, 夜间×负重×低体力)。移动始终视为无照明；其他工作在火堆燃烧的地点不吃夜间系数。</summary>
        public double WorkFactor(GameState s, ActionType type, List<string>? reasons = null)
        {
            var c = Config.WorkFactors;
            double f = 1;
            var fac = s.Locations[s.CurrentLocation].Facility;
            var night = !IsDaylight(s.GameMinute);
            var lit = type != ActionType.Travel && fac.FireLit;
            if (night && !lit)
            {
                f *= c.Night;
                reasons?.Add($"夜间无照明 ×{c.Night}");
            }
            if (CarriedGrams(s) > Config.Carry.SoftGrams)
            {
                f *= c.Overweight;
                reasons?.Add($"负重超过{Config.Carry.SoftGrams / 1000}公斤 ×{c.Overweight}");
            }
            if (Config.Thresholds["energy"].IsBelow(s.Stats.Energy))
            {
                f *= c.LowEnergy;
                reasons?.Add($"体力低于{Config.Thresholds["energy"].Value:0} ×{c.LowEnergy}");
            }
            if (f > c.Cap)
            {
                f = c.Cap;
                reasons?.Add($"倍率封顶 {c.Cap}");
            }
            return f;
        }

        /// <summary>推进一个 5 分钟段（求生案 06 每个5分钟段）。</summary>
        public SegmentResult Advance(GameState s)
        {
            if (s.Progress.Dead) return new SegmentResult(SegmentStatus.Dead);
            var a = s.Sub ?? s.Main;
            if (a == null) return new SegmentResult(SegmentStatus.Idle);
            if (!a.IsSub && a.Status == ActionStatus.Paused) return new SegmentResult(SegmentStatus.Paused);

            // 0. 段前持续条件：原料腐败（烧水、烤制、水煮已是设施自动进程，熄火不再暂停角色行动）
            if ((a.Type == ActionType.Craft || a.Type == ActionType.Build) && AnyReservedSpoiled(s, a))
            {
                AbortSpoiled(s, a);
                return new SegmentResult(SegmentStatus.Aborted);
            }

            // 1. 段首计算：角色本段速率；各火堆段首是否燃烧、燃料够不够、哪些加工目标可受热（设施与烹饪案第 7 节）
            var ctx = ComputeSegment(s, a);
            var snap = SnapshotFacilities(s, a);

            // 2. 推进时间并应用变化
            var h = Tick / 60.0;
            s.Stats.Add(StatId.Satiety, ctx.Rates.Satiety * h);
            s.Stats.Add(StatId.Hydration, ctx.Rates.Hydration * h);
            s.Stats.Add(StatId.Energy, ctx.Rates.Energy * h);
            s.Stats.Add(StatId.Wakefulness, ctx.Rates.Wakefulness * h);
            s.Stats.Add(StatId.Morale, ctx.Rates.Morale * h);
            s.Stats.Add(StatId.Health, ctx.HealthPerHour * h);
            if (!string.IsNullOrEmpty(ctx.DamageCause)) s.Progress.LastDamageCause = ctx.DamageCause;

            s.GameMinute += Tick;
            s.SegmentCount++;
            BurnFires(s);
            UpdateAllClearCharges(s);
            if (s.GameMinute % Clock.MinutesPerDay == 0) NewDay(s);

            // 3. 判死：死亡则本段设施加工不提交
            if (s.Stats.Health <= 0)
            {
                Kill(s, s.Progress.LastDamageCause);
                return new SegmentResult(SegmentStatus.Dead);
            }

            // 3b. 设施自动加工：按段首快照给合格目标 +5 受热分钟，达标即原地转化；不替换、不暂停角色主行动
            AdvanceFacilities(s, snap);

            // 4. 累积进度
            bool done;
            if (a.IsWorkType)
            {
                a.WorkDone += Tick / ctx.Factor;
                a.ElapsedMinutes += Tick;
                done = a.WorkDone >= a.BaseWorkMinutes - Eps;
            }
            else
            {
                a.ElapsedMinutes += Tick;
                done = a.ElapsedMinutes >= a.PlannedMinutes;
            }
            if (a.Type == ActionType.Sleep) s.Progress.Flags.Add("sleptOnce");

            var status = SegmentStatus.Running;
            if (done)
            {
                // 5. 提交结果（含末尾环境伤害）
                var ok = Complete(s, a);
                if (s.Progress.Dead) return new SegmentResult(SegmentStatus.Dead);
                status = ok ? SegmentStatus.Completed : SegmentStatus.Aborted;
            }

            // 6. 阈值提醒
            if (CheckAlerts(s, a, done) && status == SegmentStatus.Running)
                status = SegmentStatus.Paused;

            UpdateFlags(s);
            CheckSevenDay(s);
            return new SegmentResult(status);
        }

        private void Pause(GameState s, ActionState a, PauseReason reason, StatId? stat, string text)
        {
            a.Status = ActionStatus.Paused;
            a.PauseReason = reason;
            a.PauseStat = stat;
            Emit(s, GameEventKind.ActionPaused, text, stat);
        }

        private void BurnFires(GameState s)
        {
            foreach (var kv in s.Locations)
            {
                var f = kv.Value.Facility;
                if (!f.FireLit) continue;
                f.FuelMinutes -= Tick;
                if (f.FuelMinutes <= 0)
                {
                    f.FuelMinutes = 0;
                    f.FireLit = false;
                    f.WentOut = true;
                    Emit(s, GameEventKind.FireOut, $"{LocName(kv.Key)}的火堆燃料耗尽，已熄灭。");
                }
            }
        }

        /// <summary>00:00 跨日：自然资源补充，补充上限按“可用+已预留”计算（搜集案 04/05）。</summary>
        private void NewDay(GameState s)
        {
            var day = s.GameMinute / Clock.MinutesPerDay;
            foreach (var r in Config.Resources)
            {
                var rs = s.Resources[r.NodeId];
                if (rs.LastRefillDay >= day) continue;
                rs.LastRefillDay = day;
                if (!r.Refills || !r.Cap.HasValue) continue;
                var avail = rs.Available ?? 0;
                rs.Available = Math.Min(r.Cap.Value - rs.Reserved, avail + r.DailyRefill);
                if (rs.Available < avail) rs.Available = avail;
            }
            Emit(s, GameEventKind.NewDay, $"第{Clock.Day(s.GameMinute)}日 00:00。自然资源已补充。");
        }

        private bool AnyReservedSpoiled(GameState s, ActionState a) =>
            a.ReservedItemIds.Any(id => s.Items.Any(i => i.InstanceId == id && IsSpoiled(s, i)));

        private void AbortSpoiled(GameState s, ActionState a)
        {
            Release(s, a, s.CurrentLocation);
            if (a.IsSub) s.Sub = null; else s.Main = null;
            Emit(s, GameEventKind.ActionAborted, $"{a.Label}中止：原料已腐败，预留物返还当前地面，未生成产物。");
        }

        /// <summary>提交行动结果。返回 false 表示在完成时刻因原料腐败而中止。</summary>
        private bool Complete(GameState s, ActionState a)
        {
            var here = s.CurrentLocation;
            switch (a.Type)
            {
                case ActionType.Travel:
                {
                    s.CurrentLocation = a.TargetId;
                    var revealed = Visit(s, a.TargetId);
                    ClearAction(s, a);
                    var t = $"到达{LocName(a.TargetId)}。";
                    if (revealed.Count > 0) t += $"发现{string.Join("、", revealed.Select(id => _res[id].Name))}。";
                    Emit(s, GameEventKind.ActionCompleted, t, null, revealed);
                    return true;
                }
                case ActionType.Explore:
                    return CompleteExplore(s, a);
                case ActionType.Clear:
                    return CompleteClear(s, a);
                case ActionType.Gather:
                    return CompleteGather(s, a);
                case ActionType.Craft:
                case ActionType.Build:
                {
                    if (AnyReservedSpoiled(s, a))
                    {
                        AbortSpoiled(s, a);
                        return false;
                    }
                    var rc = _recipes[a.TargetId];
                    foreach (var id in a.ReservedItemIds) s.Items.RemoveAll(i => i.InstanceId == id);
                    a.ReservedItemIds.Clear();
                    var made = new List<string>();
                    foreach (var kv in rc.Outputs)
                    {
                        CreateItems(s, kv.Key, kv.Value, here);
                        made.Add($"{ItemName(kv.Key)}+{kv.Value}");
                    }
                    if (rc.FacilityResult != null) SetFacility(s.Locations[here].Facility, rc.FacilityResult);
                    ClearAction(s, a);
                    var text = rc.FacilityResult != null
                        ? $"{rc.Name.Replace("建", "").Replace("铺", "")}已完成。"
                        : $"{string.Join("，", made)}，已放在{LocName(here)}地面。";
                    if (rc.Id == "shelter") text = "遮蔽棚建成：营地可遮蔽睡眠，休息时额外恢复精神。";
                    if (rc.Id == "bed") text = "床垫铺好：营地睡眠体力恢复每小时额外+5。";
                    Emit(s, GameEventKind.ActionCompleted, text, null, rc.Outputs.Keys);
                    return true;
                }
                case ActionType.Sleep:
                    ClearAction(s, a);
                    Emit(s, GameEventKind.ActionCompleted, $"睡眠结束（{Clock.Duration(a.PlannedMinutes)}）。");
                    return true;
                case ActionType.Rest:
                    ClearAction(s, a);
                    Emit(s, GameEventKind.ActionCompleted, $"休息结束（{Clock.Duration(a.PlannedMinutes)}）。");
                    return true;
                case ActionType.Wait:
                    ClearAction(s, a);
                    Emit(s, GameEventKind.ActionCompleted, $"等待结束（{Clock.Duration(a.PlannedMinutes)}）。");
                    return true;
                case ActionType.Ignite:
                {
                    ClearAction(s, a);
                    var f = s.Locations[here].Facility;
                    Emit(s, GameEventKind.ActionCompleted, f.FireLit
                        ? $"火堆已点燃，剩余燃料{f.FuelMinutes}分钟。"
                        : "火堆点燃后燃料随即耗尽。");
                    return true;
                }
                default:
                    ClearAction(s, a);
                    return true;
            }
        }

        private void ClearAction(GameState s, ActionState a)
        {
            a.LockedItemIds.Clear();
            if (a.IsSub) s.Sub = null;
            else s.Main = null;
        }

        private bool CompleteGather(GameState s, ActionState a)
        {
            var here = s.CurrentLocation;
            var res = _res[a.TargetId];
            var rs = s.Resources[res.NodeId];
            var made = new List<string>();
            if (res.IsUnits)
            {
                rs.Reserved -= a.ReservedResourceUnits;
                var kv = res.Outputs.First();
                CreateItems(s, kv.Key, a.ReservedResourceUnits, here);
                made.Add($"{ItemName(kv.Key)}+{a.ReservedResourceUnits}");
                a.ReservedResourceUnits = 0;
            }
            else
            {
                if (res.IsCache)
                {
                    rs.Reserved -= a.ReservedResourceUnits;
                    a.ReservedResourceUnits = 0;
                }
                foreach (var kv in res.Outputs)
                {
                    CreateItems(s, kv.Key, kv.Value, here);
                    made.Add($"{ItemName(kv.Key)}+{kv.Value}");
                }
            }

            var text = $"{string.Join("，", made)}，已放在{LocName(here)}地面";
            if (a.ToolInstanceId.HasValue)
            {
                var tool = s.Items.FirstOrDefault(i => i.InstanceId == a.ToolInstanceId.Value);
                if (tool?.Durability != null)
                {
                    var before = tool.Durability.Value;
                    tool.Durability = Math.Max(0, before - Config.ToolEffects.SuccessfulActionWear);
                    text += $"；{ItemName(tool.ItemId)}耐久{before}→{tool.Durability}";
                }
            }
            ClearAction(s, a);
            Emit(s, GameEventKind.ActionCompleted, text + "。", null, res.Outputs.Keys);

            if (a.HazardHit && res.Hazard != null)
            {
                s.Stats.Add(StatId.Health, -res.Hazard.HealthLoss);
                s.Progress.LastDamageCause = $"搜集时{res.Hazard.Label}";
                Emit(s, GameEventKind.Hazard, $"{res.Hazard.Label}，健康−{res.Hazard.HealthLoss:0}。");
                if (s.Stats.Health <= 0) Kill(s, s.Progress.LastDamageCause);
            }
            return true;
        }

        private void Kill(GameState s, string cause)
        {
            var loc = s.CurrentLocation;
            foreach (var a in new[] { s.Sub, s.Main })
                if (a != null) Release(s, a, loc);
            s.Main = null;
            s.Sub = null;
            s.Stats.Health = 0;
            s.Progress.Dead = true;
            s.Progress.Death = new DeathInfo
            {
                Cause = string.IsNullOrEmpty(cause) ? "伤势过重" : cause,
                Minute = s.GameMinute,
                LocationId = loc,
            };
            Emit(s, GameEventKind.Death, $"你在{LocName(loc)}倒下了。直接原因：{s.Progress.Death.Cause}。");
        }

        private static readonly StatId[] ZeroStats = { StatId.Satiety, StatId.Hydration, StatId.Wakefulness, StatId.Energy };

        public static string StatName(StatId id) => id switch
        {
            StatId.Health => "健康",
            StatId.Satiety => "饱食",
            StatId.Hydration => "水分",
            StatId.Energy => "体力",
            StatId.Wakefulness => "清醒度",
            _ => "精神",
        };

        public static string StatKey(StatId id) => id switch
        {
            StatId.Health => "health",
            StatId.Satiety => "satiety",
            StatId.Hydration => "hydration",
            StatId.Energy => "energy",
            StatId.Wakefulness => "wakefulness",
            _ => "morale",
        };

        public string AlertText(StatId id, bool zero)
        {
            if (zero)
            {
                return id switch
                {
                    StatId.Satiety => $"饱食归零：健康每小时减{-Config.HealthRules.ZeroSatietyPerHour:0}，请尽快进食。",
                    StatId.Hydration => $"水分归零：健康每小时减{-Config.HealthRules.ZeroHydrationPerHour:0}，请立即饮水。",
                    StatId.Wakefulness => $"清醒度归零：健康每小时减{-Config.HealthRules.ZeroWakefulnessPerHour:0}，请尽快睡眠。",
                    _ => $"体力归零：继续工作时健康每小时减{-Config.HealthRules.ZeroEnergyWhileWorkingPerHour:0}，建议休息。",
                };
            }
            var th = Config.Thresholds[StatKey(id)];
            var cmp = th.Inclusive ? "降至" : "低于";
            return id switch
            {
                StatId.Health => $"健康{cmp}{th.Value:0}，处于高危状态。",
                StatId.Satiety => $"饱食{cmp}{th.Value:0}，请尽快进食。",
                StatId.Hydration => $"水分{cmp}{th.Value:0}，请尽快饮水。",
                StatId.Wakefulness => $"清醒度{cmp}{th.Value:0}，需要睡眠。",
                StatId.Energy => $"体力{cmp}{th.Value:0}，工作耗时增加，建议休息。",
                _ => $"精神{cmp}{th.Value:0}，休息与睡眠的体力恢复减半。",
            };
        }

        /// <summary>阈值与归零提醒（求生案 02）。返回是否暂停了刚推进的主行动。子行动不会被暂停（实施方案 9A）。</summary>
        private bool CheckAlerts(GameState s, ActionState ran, bool completed)
        {
            var triggers = new List<(StatId stat, bool zero)>();
            foreach (var id in ZeroStats)
            {
                var isZero = s.Stats.Get(id) <= 0;
                if (isZero && !s.ZeroLatched[id]) { s.ZeroLatched[id] = true; triggers.Add((id, true)); }
                else if (!isZero) s.ZeroLatched[id] = false;
            }
            foreach (var id in Stats.All)
            {
                var below = Config.Thresholds[StatKey(id)].IsBelow(s.Stats.Get(id));
                if (below && !s.AlertLatched[id]) { s.AlertLatched[id] = true; triggers.Add((id, false)); }
                else if (!below) s.AlertLatched[id] = false;
            }
            if (triggers.Count == 0) return false;

            // 归零优先于普通提醒
            triggers = triggers.OrderBy(t => t.zero ? 0 : 1).ToList();
            foreach (var t in triggers)
                Emit(s, GameEventKind.Alert, AlertText(t.stat, t.zero), t.stat);

            var main = s.Main;
            if (!ran.IsSub && !completed && main != null && main.ActionId == ran.ActionId && main.Status == ActionStatus.Running)
            {
                var first = triggers[0];
                Pause(s, main, first.zero ? PauseReason.StatZero : PauseReason.StatAlert, first.stat,
                    $"{main.Label}已暂停：{AlertText(first.stat, first.zero)}");
                return true;
            }
            return false;
        }

        private void CheckSevenDay(GameState s)
        {
            if (s.Progress.SevenDayShown || s.Progress.Dead) return;
            if (s.GameMinute < Config.Progress.SevenDayCheckMinute) return;
            s.Progress.SevenDayShown = true;
            Emit(s, GameEventKind.SevenDay, "七日基础生存检查完成。你可以继续生存下去。");
        }

        /// <summary>引导“熟制块根”接受的物品（设施与烹饪案 5.3 goals.acceptedIds）。</summary>
        public static readonly string[] CookedFoodIds = { "cooked_root", "boiled_root" };

        /// <summary>引导与七日检查标记：达成即锁存（实施方案 9B-13）。</summary>
        public void UpdateFlags(GameState s)
        {
            var f = s.Progress.Flags;
            if (s.Locations.TryGetValue("field", out var field) && field.Progress >= 50) f.Add("field50");
            if ((s.Locations.TryGetValue("stream", out var st) && st.Visited) || (s.Locations.TryGetValue("woods", out var wd) && wd.Visited))
                f.Add("visitedStreamOrWoods");
            if (s.Locations.TryGetValue("village", out var vg) && vg.Visited) f.Add("visitedVillage");
            if (s.Locations.Values.Any(l => l.Facility.ShelterBuilt)) f.Add("shelterBuilt");
            if (s.Locations.Values.All(l => l.Visited)) f.Add("allVisited");

            var camp = Config.Locations.FirstOrDefault(l => l.CanBuildShelter)?.Id ?? "camp";
            // 按根位置计入：随身、营地地面，以及营地火堆与锅里的有效物品；每个实例只算一次（设施与烹饪案 5.3）
            int Count(params string[] ids) => s.Items
                .Where(i => ids.Contains(i.ItemId) && !IsSpoiled(s, i))
                .Where(i => { var p = PlaceOf(s, i); return p == ItemInstance.Carried || p == camp; })
                .GroupBy(i => i.InstanceId).Sum(g => g.First().Count);
            // “熟制块根”按烤熟与水煮合计
            if (Count("clean_water") >= 3 && Count(CookedFoodIds) >= 2) f.Add("supplyReady");
            SyncLayout(s);
        }
    }
}

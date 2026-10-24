using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    public sealed partial class GameEngine
    {
        private const int SimLimit = 4000;

        /// <summary>
        /// 只读预览：在状态副本上真实执行一遍（风险视为未触发、提醒自动继续），
        /// 得到预计耗时、完成时刻与状态变化。不修改传入状态。
        /// </summary>
        public ActionPreview Preview(GameState s, ActionRequest r)
        {
            var p = new ActionPreview { Type = r.Type, StartMinute = s.GameMinute, StatsBefore = s.Stats.Clone() };
            var err = Validate(s, r, out var plan);
            if (err != null)
            {
                p.Ok = false;
                p.Reason = err;
                p.Label = DefaultLabel(r);
                FillStatic(s, r, p, null);
                return p;
            }

            p.Ok = true;
            p.Type = plan.Type;
            p.Label = plan.Label;
            p.AsSubAction = plan.IsSub;
            FillStatic(s, r, p, plan);

            var c = s.Clone();
            var res = Start(c, r.WithRequestId(null));
            if (!res.Ok) { p.Ok = false; p.Reason = res.Reason; return p; }
            var id = res.ActionId!.Value;
            var a = plan.IsSub ? c.Sub : c.Main;
            if (a != null) a.HazardHit = false;

            var alerts = new HashSet<StatId>();
            c.PendingEvents.Clear();
            // 逐段跟踪工作倍率：火堆燃尽、入夜、体力跌破阈值都会让后续段变慢，
            // 预览必须把变化的时刻与原因列出来（UI 案 06、地图案 07）。
            var trackFactor = p.IsWork && !plan.IsSub;
            var prevFactor = p.FactorAtStart;
            var prevLit = s.Locations[s.CurrentLocation].Facility.FireLit;
            for (var k = 0; k < SimLimit; k++)
            {
                var cur = plan.IsSub ? c.Sub : c.Main;
                if (cur == null || cur.ActionId != id || c.Progress.Dead) break;
                if (!plan.IsSub && cur.Status == ActionStatus.Paused)
                {
                    if (cur.PauseStat.HasValue) alerts.Add(cur.PauseStat.Value);
                    Resume(c);
                }
                if (trackFactor)
                {
                    var reasons = new List<string>();
                    var f = WorkFactor(c, r.Type, reasons);
                    var litNow = c.Locations[c.CurrentLocation].Facility.FireLit;
                    if (System.Math.Abs(f - prevFactor) > Eps)
                    {
                        var cause = prevLit && !litNow && r.Type != ActionType.Travel ? "火堆燃尽，" : "";
                        var why = reasons.Count > 0 ? $"（{string.Join("，", reasons)}）" : "";
                        p.Warnings.Add($"预计{Clock.HHmm(c.GameMinute)}起{cause}工作倍率变为×{f:0.##}{why}");
                        prevFactor = f;
                    }
                    prevLit = litNow;
                }
                Advance(c);
            }
            p.AlertsExpected = alerts.ToList();
            p.WouldDie = c.Progress.Dead;
            p.StatsAfter = c.Stats.Clone();
            p.EstimatedEndMinute = c.GameMinute;
            p.EstimatedMinutes = c.GameMinute - s.GameMinute;
            p.FacilityChanges = FacilityChanges(s, c);

            if (p.WouldDie) p.Warnings.Add("按当前状态，行动途中可能死亡");
            foreach (var st in alerts) p.Warnings.Add($"途中{StatName(st)}将越过提醒阈值，行动会暂停");
            return p;
        }

        /// <summary>对比行动前后的状态，列出期间设施的自动加工完成、受热进度与燃料变化（不含随机结果）。</summary>
        private List<string> FacilityChanges(GameState before, GameState after)
        {
            var list = new List<string>();
            foreach (var ev in after.PendingEvents)
            {
                if (ev.Kind == GameEventKind.FacilityDone || ev.Kind == GameEventKind.FireOut)
                    list.Add($"{Clock.HHmm(ev.Minute)} {ev.Text.TrimEnd('。')}");
            }
            foreach (var l in Config.Locations.Where(l => l.HasFire))
            {
                if (!before.Locations.TryGetValue(l.Id, out var b) || !after.Locations.TryGetValue(l.Id, out var a)) continue;
                foreach (var p in b.Facility.Processes)
                {
                    var q = a.Facility.Processes.FirstOrDefault(x => x.ProcessId == p.ProcessId);
                    if (q == null || q.HeatDoneMinutes == p.HeatDoneMinutes) continue;
                    var t = FindItem(before, p.TargetInstanceId);
                    var name = t == null ? "" : $"{ItemName(t.ItemId)}#{t.InstanceId}";
                    list.Add($"{name}{Process(p.RecipeId).Name}受热{p.HeatDoneMinutes}→{q.HeatDoneMinutes}/{q.RequiredHeatMinutes}分钟");
                }
                if (b.Facility.FuelMinutes != a.Facility.FuelMinutes)
                    list.Add($"{LocName(l.Id)}火堆燃料{b.Facility.FuelMinutes}→{a.Facility.FuelMinutes}分钟");
            }
            return list;
        }

        private static string DefaultLabel(ActionRequest r) => r.Type switch
        {
            ActionType.Travel => "移动",
            ActionType.Explore => "探索周边",
            ActionType.Gather => "采集",
            ActionType.Craft => "制作",
            ActionType.Build => "建造",
            ActionType.Wait => "等待",
            ActionType.Rest => "休息",
            ActionType.Sleep => "睡眠",
            ActionType.Use => "使用",
            ActionType.Clear => "清理",
            _ => "点燃火堆",
        };

        private void FillStatic(GameState s, ActionRequest r, ActionPreview p, Plan? plan)
        {
            p.IsWork = ActionState.IsWork(r.Type);
            if (p.IsWork)
            {
                var reasons = new List<string>();
                p.FactorAtStart = WorkFactor(s, r.Type, reasons);
                p.FactorReasons = reasons;
            }
            p.OutputPlace = $"{LocName(s.CurrentLocation)}地面";

            switch (r.Type)
            {
                case ActionType.Travel:
                {
                    var edge = Config.Edges.FirstOrDefault(e => e.Touches(s.CurrentLocation) && e.Other(s.CurrentLocation) == r.TargetId);
                    if (edge != null) p.BaseMinutes = edge.BaseMinutes;
                    p.ConfirmText = "确认移动";
                    break;
                }
                case ActionType.Explore:
                {
                    p.BaseMinutes = Config.Actions.ExploreMinutes;
                    var ls = s.Locations[s.CurrentLocation];
                    if (ls.Progress < 100)
                    {
                        var next = System.Math.Min(100, ls.Progress + Config.Actions.ExploreProgress);
                        p.Notes.Add($"完成后探索度{ls.Progress}%→{next}%");
                        p.Notes.Add(NextMilestoneHasContent(s, s.CurrentLocation) ? "本次可能发现新线索" : "本次没有新线索，仍会推进探索度");
                        p.Notes.Add("探索推进地块调查，并有机会取得零散物资");
                        p.Notes.Add("完成时按下表抽取1次本地物资，产物留在当前地面，不会自动拾取");
                        p.Notes.Add("探索不扣清理次数");
                        p.LootOdds = LootOdds(s.CurrentLocation);
                    }
                    else p.Notes.Add("该地点不再探索，可清理或搜集已发现资源");
                    p.ConfirmText = "开始探索";
                    break;
                }
                case ActionType.Clear:
                {
                    var here = s.CurrentLocation;
                    var cfg = _locs[here].Clear;
                    var (charges, _) = ClearChargesAt(s, here);
                    p.BaseMinutes = Config.Actions.ClearMinutes;
                    p.ClearCharges = charges;
                    p.ClearCap = cfg.Cap;
                    p.NextRefillMinutes = NextClearRefillMinutes(s, here);
                    p.LootOdds = LootOdds(here);
                    p.Notes.Add($"开始时扣1次清理机会（当前{charges}/{cfg.Cap}）；取消、中途死亡或未找到物资都不返还");
                    p.Notes.Add(p.NextRefillMinutes.HasValue
                        ? $"次数随世界时间串行恢复：每{Clock.Duration(cfg.RefillIntervalMinutes)}恢复1次，距下一次{Clock.Duration(p.NextRefillMinutes.Value)}"
                        : $"次数已满；用掉后每{Clock.Duration(cfg.RefillIntervalMinutes)}恢复1次");
                    p.Notes.Add("不增加探索度，不发现新路线或资源点，不消耗资源点库存");
                    p.Notes.Add("产物留在当前地面，不会自动拾取");
                    p.ConfirmText = $"开始清理·{Config.Actions.ClearMinutes}分钟";
                    break;
                }
                case ActionType.Gather:
                {
                    if (!_res.TryGetValue(r.TargetId, out var res)) break;
                    p.BaseMinutes = res.BaseMinutes;
                    if (res.Hazard != null)
                    {
                        p.HazardProbability = res.Hazard.Probability;
                        p.HazardHealthLoss = res.Hazard.HealthLoss;
                        p.HazardLabel = res.Hazard.Label;
                    }
                    var tool = plan != null ? plan.Tool : res.ToolBonus != null ? BestTool(s, res.ToolBonus.ToolTag) : null;
                    if (tool != null)
                    {
                        if (res.ToolBonus!.BaseMinutesOverride.HasValue) p.BaseMinutes = res.ToolBonus.BaseMinutesOverride.Value;
                        p.ToolName = ItemName(tool.ItemId);
                        p.ToolExplicit = plan != null && plan.ExplicitTool;
                        p.ToolDurabilityBefore = tool.Durability;
                        p.ToolMaxDurability = _items[tool.ItemId].MaxDurability;
                        if (tool.Durability.HasValue)
                            p.ToolDurabilityAfter = System.Math.Max(0, tool.Durability.Value - Config.ToolEffects.SuccessfulActionWear);
                    }
                    else if (res.ToolBonus != null)
                    {
                        var tname = Config.Items.First(i => i.ToolTag == res.ToolBonus.ToolTag).Name;
                        p.Notes.Add($"随身携带可用的{tname}可提高效率");
                    }
                    if (plan != null)
                    {
                        p.Outputs = plan.Outputs;
                        p.Notes.AddRange(plan.Notes);
                    }
                    p.Notes.Add("产物先放在当前地点地面，不会自动拾取");
                    p.ConfirmText = res.IsCache ? "开始搜集" : "开始采集";
                    break;
                }
                case ActionType.Craft:
                case ActionType.Build:
                {
                    if (!_recipes.TryGetValue(r.TargetId, out var rc)) break;
                    p.BaseMinutes = rc.BaseMinutes;
                    p.Outputs = rc.Outputs.Select(kv => Line(kv.Key, kv.Value)).ToList();
                    p.ConfirmText = rc.IsBuild ? "开始建造" : "开始制作";
                    if (plan != null) p.Notes.AddRange(plan.Notes);
                    if (rc.NeedsFire)
                    {
                        var f = s.Locations[s.CurrentLocation].Facility;
                        p.Notes.Add($"火堆剩余燃料{f.FuelMinutes}分钟；燃尽时加工暂停");
                    }
                    break;
                }
                case ActionType.Wait:
                    p.BaseMinutes = r.Minutes;
                    p.ConfirmText = $"等待{Clock.Duration(r.Minutes)}";
                    break;
                case ActionType.Rest:
                {
                    p.BaseMinutes = r.Minutes;
                    p.ConfirmText = $"休息{Clock.Duration(r.Minutes)}";
                    p.Notes.Add("休息只恢复体力，不恢复清醒度");
                    break;
                }
                case ActionType.Sleep:
                {
                    p.BaseMinutes = r.Minutes;
                    p.ConfirmText = $"睡眠{Clock.Duration(r.Minutes)}";
                    var sheltered = IsSheltered(s, s.CurrentLocation);
                    p.Notes.Add(sheltered ? "遮蔽睡眠：每小时清醒度+12、体力+15、精神+1" : "露天睡眠：每小时清醒度+6、体力+10、精神−0.5");
                    if (sheltered && s.Locations[s.CurrentLocation].Facility.BedBuilt) p.Notes.Add("床垫：睡眠体力每小时额外+5");
                    p.Notes.Add("睡眠中仍会消耗饱食、水分和火堆燃料");
                    break;
                }
                case ActionType.Use:
                    p.BaseMinutes = Config.ItemActions.UseMinutes;
                    p.ConfirmText = plan != null ? $"{plan.Label}·{Config.ItemActions.UseMinutes}分钟" : "使用";
                    break;
                case ActionType.Ignite:
                {
                    p.BaseMinutes = Config.Fire.IgniteMinutes;
                    p.ConfirmText = $"点燃火堆·{Config.Fire.IgniteMinutes}分钟";
                    if (plan != null && plan.IgniteConsumesWood) p.Notes.Add($"将消耗1木材，提供{Config.Fire.BurnMinutesPerWood}分钟燃料");
                    p.Notes.Add($"点火过程也消耗{Config.Fire.IgniteMinutes}分钟燃料");
                    break;
                }
            }
            if (p.IsWork && p.FactorAtStart > 1 + Eps)
                p.Notes.Add($"当前工作倍率×{p.FactorAtStart:0.##}：{string.Join("，", p.FactorReasons)}");
        }

        /// <summary>估算当前行动的完成时刻（提醒自动继续、风险不计）。火堆会熄灭等无法预估时返回 null。</summary>
        public int? EstimateEndMinute(GameState s)
        {
            var a = s.Sub ?? s.Main;
            if (a == null) return null;
            var c = s.Clone();
            var target = (c.Sub ?? c.Main)!;
            var id = target.ActionId;
            var sub = target.IsSub;
            target.HazardHit = false;
            if (!sub && target.Status == ActionStatus.Paused) Resume(c);
            for (var k = 0; k < SimLimit; k++)
            {
                var cur = sub ? c.Sub : c.Main;
                if (cur == null || cur.ActionId != id) return c.GameMinute;
                if (c.Progress.Dead) return null;
                if (!sub && cur.Status == ActionStatus.Paused) Resume(c);
                Advance(c);
            }
            return null;
        }
    }
}

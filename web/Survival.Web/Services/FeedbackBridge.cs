using Microsoft.JSInterop;
using Newtonsoft.Json;
using Survival.Core.Engine;
using Survival.Core.Save;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// 给通用留言组件（wwwroot/js/feedback-widget.js）提供游戏上下文：当前界面、时间地点、行动、状态与存档。
/// 只读，不改变游戏状态。
/// </summary>
public sealed class FeedbackBridge
{
    public const string Version = "原型 v0.2（交互与地图扩展 v0.3）";
    private readonly GameSession G;

    public FeedbackBridge(GameSession session) => G = session;

    [JSInvokable]
    public string GetFeedbackContext()
    {
        var ctx = new Dictionary<string, object?>
        {
            ["game"] = "适者生存",
            ["version"] = $"{Version} · 配置{G.Config?.Version} · 存档{GameState.CurrentSchema}",
        };

        if (G.State == null || G.Engine == null)
        {
            ctx["screen"] = "标题界面";
            ctx["summary"] = $"标题界面 · {(G.HasSave ? "有存档" : "无存档")}";
            return JsonConvert.SerializeObject(ctx);
        }

        var s = G.S;
        var e = G.E;
        var main = s.Main;
        var sub = s.Sub;
        var screen = ScreenName(s);
        var where = e.LocName(s.CurrentLocation);
        var action = main == null ? "空闲" : ActionText(main) + (sub != null ? $"；补给中：{sub.Label}" : "");
        var st = s.Stats;

        ctx["screen"] = screen;
        ctx["day"] = Clock.Day(s.GameMinute);
        ctx["time"] = Clock.HHmm(s.GameMinute);
        ctx["gameMinute"] = s.GameMinute;
        ctx["location"] = new { id = s.CurrentLocation, name = where, progress = s.Locations[s.CurrentLocation].Progress };
        ctx["selection"] = G.Selected.Kind == SelectionKind.None ? null : new { kind = G.Selected.Kind.ToString(), id = G.Selected.Id, owner = G.Selected.Owner };
        ctx["action"] = action;
        ctx["stats"] = new
        {
            health = Math.Round(st.Health, 2), satiety = Math.Round(st.Satiety, 2), hydration = Math.Round(st.Hydration, 2),
            energy = Math.Round(st.Energy, 2), wakefulness = Math.Round(st.Wakefulness, 2), morale = Math.Round(st.Morale, 2),
        };
        ctx["carriedKg"] = Math.Round(e.CarriedGrams(s) / 1000.0, 1);
        ctx["fires"] = s.Locations.Where(kv => e.Location(kv.Key).HasFire)
            .ToDictionary(kv => kv.Key, kv => new { lit = kv.Value.Facility.FireLit, fuel = kv.Value.Facility.FuelMinutes, shelter = kv.Value.Facility.ShelterBuilt, bed = kv.Value.Facility.BedBuilt });
        ctx["clear"] = s.Locations.Where(kv => kv.Value.Visited)
            .ToDictionary(kv => kv.Key, kv => e.ClearChargesText(s, kv.Key));
        ctx["pots"] = s.Items.Where(i => e.IsPot(i))
            .Select(p => $"{e.ItemName(p.ItemId)}#{p.InstanceId}@{p.Owner}：{e.PotLabel(s, p)}，食材{e.PotFood(s, p.InstanceId).Count}，{e.ContentStatus(s, p)}")
            .ToList();
        ctx["carried"] = Summarize(s, e, ItemInstance.Carried);
        ctx["ground"] = Summarize(s, e, s.CurrentLocation);
        ctx["flags"] = s.Progress.Flags.OrderBy(x => x).ToList();
        ctx["dead"] = s.Progress.Dead ? s.Progress.Death?.Cause : null;
        ctx["seed"] = s.Seed;
        ctx["segments"] = s.SegmentCount;
        ctx["notice"] = G.Notice;
        ctx["recentLog"] = s.Log.TakeLast(12).Select(l => $"{Clock.DayTime(l.Minute)} {l.Text}").ToList();
        ctx["summary"] =
            $"第{Clock.Day(s.GameMinute)}日 {Clock.HHmm(s.GameMinute)} · {where} · 界面：{screen} · 行动：{action}\n" +
            $"健康{Ui.Floor(st.Health)} 饱食{Ui.Floor(st.Satiety)} 水分{Ui.Floor(st.Hydration)} 体力{Ui.Floor(st.Energy)} 清醒{Ui.Floor(st.Wakefulness)} 精神{Ui.Floor(st.Morale)} · 负重{Ui.Kg(e.CarriedGrams(s))}公斤 · 种子{s.Seed}" +
            (s.Progress.Dead ? $"\n已死亡：{s.Progress.Death?.Cause}" : "");
        return JsonConvert.SerializeObject(ctx);
    }

    /// <summary>当前存档全文，供作者复现问题。没有进行中的游戏时返回 null。</summary>
    [JSInvokable]
    public string? GetSaveJson() => G.State == null ? null : SaveSerializer.Serialize(G.S);

    private string ScreenName(GameState s)
    {
        if (s.Progress.Dead) return "死亡结算";
        if (G.SevenDayPending && s.Main == null) return "七日检查";
        if (G.Panel != null)
        {
            if (G.Panel.StartsWith("status:")) return "状态说明";
            return G.Panel switch
            {
                "map" => "区域地图",
                "craft" => "手工制作",
                "rest" => "休息与睡眠",
                "log" => "行动日志",
                "menu" => "菜单",
                _ => G.Panel,
            };
        }
        if (s.Sub != null) return "补给/维护中";
        if (s.Main?.Status == ActionStatus.Paused) return "暂停面板";
        // v0.7：没有内容物层级菜单了，记录点火工具菜单与右栏页签
        var tab = G.DetailTab == DetailTab.Character ? "右栏人物页" : "右栏对象信息";
        if (G.IgniteMenuOpen) return $"主界面 · 点火工具菜单（{tab}）";
        return s.Main != null ? $"主界面（行动进行中，{tab}）" : $"主界面（{tab}）";
    }

    private static string ActionText(ActionState a)
    {
        var progress = a.IsWorkType ? $"{a.WorkDone:0.#}/{a.BaseWorkMinutes:0}工作分钟" : $"{a.ElapsedMinutes}/{a.PlannedMinutes}分钟";
        var status = a.Status == ActionStatus.Paused ? $"已暂停（{a.PauseReason}{(a.PauseStat.HasValue ? " " + GameEngine.StatName(a.PauseStat.Value) : "")}）" : "进行中";
        return $"{a.Label}（{status}，{progress}）";
    }

    private static List<string> Summarize(GameState s, GameEngine e, string owner) =>
        s.Items.Where(i => i.Owner == owner)
            .GroupBy(i => i.ItemId)
            .Select(g => $"{e.ItemName(g.Key)}×{g.Sum(i => i.Count)}" +
                         (g.Any(i => i.Durability.HasValue) ? $"(耐久{string.Join("/", g.Select(i => i.Durability))})" : "") +
                         (g.Any(i => e.IsSpoiled(s, i)) ? "(含腐败)" : ""))
            .ToList();
}

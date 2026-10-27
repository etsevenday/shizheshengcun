using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>左栏属性下方的一个状态图标（S14 §12.2、S12 §7）。</summary>
public sealed class StatusChip
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    /// <summary>实际效果（与内核/配置同源，不另算）。</summary>
    public string Effect { get; init; } = "";
    /// <summary>剩余游戏分钟；无时限或未知为 null（如实写“无固定时限”，不捏造倒计时）。</summary>
    public int? RemainingMinutes { get; init; }
    /// <summary>不利状态（警示样式）。</summary>
    public bool Harmful { get; init; }
    /// <summary>危险类（出血、归零、尸变风险）：一级容量不足时也必须留在一级。</summary>
    public bool Danger { get; init; }
    /// <summary>挂在哪个属性下：health / satiety / hydration / energy / wakefulness / morale / immunity / virus。</summary>
    public string Anchor { get; init; } = "health";
    /// <summary>图标键（Icons.Status）；没有专用图时用统一状态占位图。</summary>
    public string Icon { get; init; } = "status_unknown";
}

/// <summary>
/// 左栏状态图标的数据：只收游戏里实际生效的状态（内核 ActiveConditions、属性归零/低值的真实惩罚、战斗恐慌惩罚），
/// 不为填满图标编造效果；免疫/病毒数值仍是原属性，这里只是展示，不重复结算。
/// </summary>
public static class StatusView
{
    private static readonly Dictionary<string, (string anchor, string icon)> Kernel = new()
    {
        ["diarrhea"] = ("hydration", "status_diarrhea"),
        ["bleed"] = ("health", "status_bleed"),
        ["bandageCooldown"] = ("health", "status_bandage_cd"),
        ["baobao"] = ("virus", "status_baobao"),
        ["infected"] = ("virus", "status_infected"),
        ["antibiotic"] = ("virus", "status_antibiotic"),
        ["antibioticCooldown"] = ("virus", "status_antibiotic_cd"),
        ["bandageVirusCooldown"] = ("virus", "status_bandage_virus_cd"),
    };

    public static List<StatusChip> All(GameEngine e, GameState s)
    {
        var list = new List<StatusChip>();
        var cfg = e.Config;
        foreach (var c in e.ActiveConditions(s))
        {
            var (anchor, icon) = Kernel.TryGetValue(c.Id, out var m) ? m : ("health", "status_unknown");
            list.Add(new StatusChip
            {
                Id = c.Id, Name = c.Name, Effect = c.Effect, Harmful = c.Harmful, Anchor = anchor, Icon = icon,
                RemainingMinutes = c.RemainingMinutes > 0 ? c.RemainingMinutes : null,
                Danger = c.Id is "bleed" or "infected",
            });
        }
        // 归零：健康按配置每小时扣减（S12 §3），文案与暂停提示同源（AlertText）
        foreach (var id in new[] { StatId.Satiety, StatId.Hydration, StatId.Wakefulness, StatId.Energy })
        {
            if (s.Stats.Get(id) > 0) continue;
            list.Add(new StatusChip
            {
                Id = "zero_" + GameEngine.StatKey(id), Name = $"{GameEngine.StatName(id)}归零", Effect = e.AlertText(id, true),
                Harmful = true, Danger = true, Anchor = GameEngine.StatKey(id), Icon = "status_zero",
            });
        }
        // 低体力：工作耗时倍率（S12 §3）；低精神：休息/睡眠体力恢复倍率（S13）
        var energy = s.Stats.Energy;
        if (energy > 0 && cfg.Thresholds["energy"].IsBelow(energy))
            list.Add(new StatusChip
            {
                Id = "low_energy", Name = "体力偏低", Effect = $"工作类行动耗时×{Ui.T(cfg.WorkFactors.LowEnergy)}", Harmful = true,
                Anchor = "energy", Icon = "status_low_energy",
            });
        if (cfg.Thresholds["morale"].IsBelow(s.Stats.Morale))
            list.Add(new StatusChip
            {
                Id = "low_morale", Name = "精神低落", Effect = $"休息与睡眠的体力恢复×{Ui.T(cfg.FacilityEffects.LowMoraleEnergyRecoveryFactor)}", Harmful = true,
                Anchor = "morale", Icon = "status_low_morale",
            });
        // 战斗恐慌：只在惩罚档生效时显示（R03 §2.1），战斗结束后清除
        if (s.Battle != null && cfg.Panic.PenaltyFor(s.Battle.Panic) is { } pp)
            list.Add(new StatusChip
            {
                Id = "panic", Name = "战斗恐慌", Effect = $"恐慌{Ui.N(s.Battle.Panic)}（≥{Ui.T(pp.Minimum)}）：命中能力×{Ui.T(pp.AccuracyRatingFactor)}；战斗结束后清除",
                Harmful = true, Anchor = "morale", Icon = "status_panic",
            });
        return list;
    }

    /// <summary>一级排序：危险 → 不利 → 其他；同级保持内核顺序。</summary>
    public static List<StatusChip> Ordered(IEnumerable<StatusChip> chips) =>
        chips.Select((c, i) => (c, i)).OrderBy(x => x.c.Danger ? 0 : x.c.Harmful ? 1 : 2).ThenBy(x => x.i).Select(x => x.c).ToList();

    public static string RemainingText(StatusChip c) =>
        c.RemainingMinutes is int m ? $"还剩{Clock.Duration(m)}（游戏时间）" : "无固定时限，条件改变时消失";
}

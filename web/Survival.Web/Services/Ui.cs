using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// 界面显示用的格式化。内部数值保留原始小数；显示统一两位小数、正且不足 0.01 写“&lt;0.01”（人物与战斗 v0.6 §02），
/// 实现在内核 Fmt，网页与将来的 UGUI 共用。
/// </summary>
public static class Ui
{
    /// <summary>状态当前值、成本：固定两位小数。</summary>
    public static string N(double v) => Fmt.N(v);

    /// <summary>阈值、速率、规则常量：至多两位小数。</summary>
    public static string T(double v) => Fmt.T(v);

    /// <summary>进度条宽度百分比（0—100），按该项上限换算。</summary>
    public static string Bar(double v, double max) => (max <= 0 ? 0 : Math.Clamp(v / max * 100, 0, 100)).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

    public static string Kg(int grams) => (grams / 1000.0).ToString("0.0");

    public static string Fresh(int minutesLeft)
    {
        if (minutesLeft <= 0) return "已腐败";
        if (minutesLeft < 60) return $"{minutesLeft}分钟后腐败";
        return $"{minutesLeft / 60}小时后腐败";
    }

    public static string ItemLine(GameEngine e, GameState s, ItemInstance i)
    {
        var cfg = e.Item(i.ItemId);
        if (i.ExpiresAt.HasValue) return Fresh(i.ExpiresAt.Value - s.GameMinute);
        if (i.MaxDurability.HasValue && i.Durability.HasValue)
            return i.Durability.Value <= 0 ? "已损坏" : $"耐久{GameEngine.DurabilityText(i)}" + (i.LoadedAmmoId != null ? $" · 弹{i.LoadedAmmo}" : "");
        if (cfg.IsTool) return cfg.ToolTag == "pot" ? "催化工具" : cfg.ToolTag == "lighter" ? "点火工具" : "工具";
        if (cfg.Use != null)
        {
            var u = cfg.Use;
            if (u.Hydration > 0 && u.Satiety == 0) return $"水分+{T(u.Hydration)}" + (u.CausesDiarrhea ? "·腹泻" : "");
            if (u.Satiety > 0) return $"饱食+{T(u.Satiety)}";
            if (u.IsBandage) return "止血·回血";
            if (u.IsAntibiotic) return "抑制病毒";
            if (u.IsAntiviral) return "治愈感染";
            if (u.Health > 0) return $"健康+{T(u.Health)}";
        }
        return $"{Kg(cfg.WeightGrams * i.Count)}公斤";
    }

    /// <summary>状态说明：数值全部读配置（v0.6 改值后不再写死）。</summary>
    public static string StatExplain(GameEngine e, StatId id)
    {
        var c = e.Config;
        var hr = c.HealthRules;
        string Th(string k) => T(c.Thresholds[k].Value);
        var sh = c.HourlyRates["sleep_sheltered"];
        var ex = c.HourlyRates["sleep_exposed"];
        var bandage = c.Items.FirstOrDefault(i => i.Use != null && i.Use.IsBandage);
        return id switch
        {
            StatId.Health => $"身体损伤总量，上限{T(c.StateScale.HealthMax)}，健康与战斗共用。归0即死亡。仅在休息或睡眠、处于遮蔽、饱食与水分不低于{T(hr.RecoveryMinSatiety)}且精神不低于{T(hr.RecoveryMinMorale)}时，每小时自然恢复{T(hr.ShelteredRestRecoveryPerHour)}。{bandage?.Name ?? "绷带"}止血并恢复{T(bandage?.Use?.Health ?? 0)}×(1+0.02×医疗等级)（{c.ItemActions.BandageCooldownMinutes}分钟冷却）。",
            StatId.Satiety => $"基础营养储备，所有时间都会消耗。降至{Th("satiety")}以下会提醒；归0后健康每小时减{T(-hr.ZeroSatietyPerHour)}。",
            StatId.Hydration => $"体内水分储备，消耗最快。降至{Th("hydration")}以下会提醒；归0后健康每小时减{T(-hr.ZeroHydrationPerHour)}。脏水必定腹泻12小时（每小时额外失水{T(-c.ItemActions.DiarrheaExtraHydrationPerHour)}）。",
            StatId.Energy => $"短期活动能力，上限{T(c.StateScale.EnergyMax)}。行动按每小时速率消耗（移动{T(-c.HourlyRates["travel"].Energy)}、探索{T(-c.HourlyRates["explore"].Energy)}、搜集{T(-c.HourlyRates["gather"].Energy)}、制造{T(-c.HourlyRates["craft"].Energy)}），战斗只付招式成本；浏览与整理不扣体力。低于{Th("energy")}时工作耗时×{T(c.WorkFactors.LowEnergy)}；归0后继续工作每小时健康减{T(-hr.ZeroEnergyWhileWorkingPerHour)}。休息每小时{SignedT(c.HourlyRates["rest"].Energy)}，睡眠恢复。",
            StatId.Wakefulness => $"睡眠需求，清醒时每小时下降{T(-c.HourlyRates["wait"].Wakefulness)}。只有睡眠能恢复（遮蔽{SignedT(sh.Wakefulness)}/小时，露天{SignedT(ex.Wakefulness)}/小时）。降至{Th("wakefulness")}以下提醒；归0后健康每小时减{T(-hr.ZeroWakefulnessPerHour)}。",
            _ => $"简化的情绪与舒适状态。工作时缓慢下降，遮蔽睡眠与营地遮蔽棚休息可恢复。低于{Th("morale")}时休息与睡眠的体力恢复×{T(c.FacilityEffects.LowMoraleEnergyRecoveryFactor)}；也决定战斗开始时的恐慌。",
        };
    }

    public static string SignedT(double v) => Fmt.SignedT(v);

    public static string Pct(double p) => Fmt.Pct(p);

    // ───────────── 场景昼夜光照（B 方案地点行） ─────────────

    public readonly record struct SceneLight(double Brightness, double Saturate, string Tint, double Stars, double Night, string Phase);

    private static readonly (int m, double b, double s, int r, int g, int bl, double a, double stars)[] LightKeys =
    {
        (0,    .42, .55,  18,  28,  64, .50, .9),
        (270,  .42, .55,  18,  28,  64, .50, .9),
        (330,  .60, .70, 120,  90, 110, .30, .3),
        (375,  .88, .90, 230, 150,  90, .16, 0),
        (450, 1.00, 1.0,   0,   0,   0, 0,   0),
        (990, 1.00, 1.0,   0,   0,   0, 0,   0),
        (1050, .90, .95, 235, 135,  70, .20, 0),
        (1110, .66, .80, 120,  70,  90, .30, .2),
        (1170, .42, .55,  18,  28,  64, .50, .9),
        (1440, .42, .55,  18,  28,  64, .50, .9),
    };

    /// <summary>按游戏时刻插值场景亮度、饱和度、色调与星空；室内变化更柔和。</summary>
    public static SceneLight Light(int minuteOfDay, bool indoor)
    {
        var k = LightKeys;
        var i = 0;
        while (i < k.Length - 2 && minuteOfDay >= k[i + 1].m) i++;
        var a = k[i];
        var b = k[i + 1];
        var f = b.m == a.m ? 0 : (minuteOfDay - a.m) / (double)(b.m - a.m);
        double L(double x, double y) => x + (y - x) * f;
        var bright = L(a.b, b.b);
        var sat = L(a.s, b.s);
        // 色调：从无色调过渡时沿用另一端的颜色，只改透明度，避免中途发灰
        var (r, g, bl) = a.a == 0 ? (b.r, b.g, b.bl) : b.a == 0 ? (a.r, a.g, a.bl) : ((int)L(a.r, b.r), (int)L(a.g, b.g), (int)L(a.bl, b.bl));
        var alpha = L(a.a, b.a);
        var stars = L(a.stars, b.stars);
        if (indoor)
        {
            bright = .55 + .45 * bright;
            sat = .7 + .3 * sat;
            alpha *= .55;
            stars = 0;
        }
        var night = Math.Clamp((1 - bright) / .58, 0, 1);
        var phase = minuteOfDay < 300 || minuteOfDay >= 1140 ? "夜间" : minuteOfDay < 420 ? "清晨" : minuteOfDay < 1020 ? "白昼" : "黄昏";
        var tint = FormattableString.Invariant($"rgba({r},{g},{bl},{alpha:0.###})");
        return new SceneLight(bright, sat, tint, stars, night, phase);
    }
}

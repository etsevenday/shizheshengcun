using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>界面显示用的格式化。内部数值保留小数，显示一律向下取整（求生案 01）。</summary>
public static class Ui
{
    public static int Floor(double v) => (int)Math.Floor(v + 1e-9);

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
        if (cfg.MaxDurability.HasValue && i.Durability.HasValue)
            return i.Durability.Value <= 0 ? "已损坏" : $"耐久{i.Durability}/{cfg.MaxDurability}";
        if (cfg.IsTool) return cfg.ToolTag == "pot" ? "催化工具" : cfg.ToolTag == "lighter" ? "点火工具" : "工具";
        if (cfg.Use != null)
        {
            var u = cfg.Use;
            if (u.Hydration > 0 && u.Satiety == 0) return $"水分+{u.Hydration:0}" + (u.CausesDiarrhea ? "·腹泻" : "");
            if (u.Satiety > 0) return $"饱食+{u.Satiety:0}";
            if (u.Health > 0) return $"健康+{u.Health:0}";
        }
        return $"{Kg(cfg.WeightGrams * i.Count)}公斤";
    }

    public static string StatExplain(StatId id) => id switch
    {
        StatId.Health => "身体损伤总量。归0即死亡。仅在休息或睡眠、处于遮蔽、饱食与水分不低于40且精神不低于20时，每小时自然恢复1。草药绷带可立即恢复5（60分钟冷却）。",
        StatId.Satiety => "基础营养储备，所有时间都会消耗。降至20以下会提醒；归0后健康每小时减2。进食恢复：罐头35、烤熟块根25（水分−2）、水煮块根23（水分+5）、野果10。",
        StatId.Hydration => "体内水分储备，消耗最快。降至20以下会提醒；归0后健康每小时减8。净水与脏水各补30，但脏水必定腹泻12小时（每小时额外失水3）。",
        StatId.Energy => "短期活动能力，工作时下降。低于20时移动、探索、搜集和制作耗时×1.25；归0后继续工作每小时健康减1。休息和睡眠恢复。",
        StatId.Wakefulness => "睡眠需求，清醒时每小时下降5。只有睡眠能恢复（遮蔽+12/小时，露天+6/小时）。降至20以下提醒；归0后健康每小时减2。",
        _ => "简化的情绪与舒适状态。工作时缓慢下降，遮蔽睡眠与营地遮蔽棚休息可恢复。低于20时休息与睡眠的体力恢复减半。",
    };

    public static string Pct(double p) => $"{p * 100:0.#}%";

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

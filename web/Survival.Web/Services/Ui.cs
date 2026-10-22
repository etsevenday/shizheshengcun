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
            if (u.Hydration > 0 && u.Satiety == 0) return $"水分+{u.Hydration:0}" + (u.CausesDiarrhea ? "·会腹泻" : "");
            if (u.Satiety > 0) return $"饱食+{u.Satiety:0}";
            if (u.Health > 0) return $"健康+{u.Health:0}";
        }
        return $"{Kg(cfg.WeightGrams * i.Count)}公斤";
    }

    public static string StatExplain(StatId id) => id switch
    {
        StatId.Health => "身体损伤总量。归0即死亡。仅在休息或睡眠、处于遮蔽、饱食与水分不低于40且精神不低于20时，每小时自然恢复1。草药绷带可立即恢复5（60分钟冷却）。",
        StatId.Satiety => "基础营养储备，所有时间都会消耗。降至20以下会提醒；归0后健康每小时减2。进食恢复：罐头35、熟块根25、野果10。",
        StatId.Hydration => "体内水分储备，消耗最快。降至20以下会提醒；归0后健康每小时减8。净水与脏水各补30，但脏水必定腹泻12小时（每小时额外失水3）。",
        StatId.Energy => "短期活动能力，工作时下降。低于20时移动、探索、搜集和制作耗时×1.25；归0后继续工作每小时健康减1。休息和睡眠恢复。",
        StatId.Wakefulness => "睡眠需求，清醒时每小时下降5。只有睡眠能恢复（遮蔽+12/小时，露天+6/小时）。降至20以下提醒；归0后健康每小时减2。",
        _ => "简化的情绪与舒适状态。工作时缓慢下降，遮蔽睡眠与营地遮蔽棚休息可恢复。低于20时休息与睡眠的体力恢复减半。",
    };

    public static string Pct(double p) => $"{p * 100:0.#}%";
}

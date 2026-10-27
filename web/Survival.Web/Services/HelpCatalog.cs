using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>系统说明的一个阅读主题（S14 §13.3 表：玩家目录用中文主题名，不展示内部系统编号）。</summary>
public sealed class HelpTopic
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    /// <summary>目录图标（Icons 键）。</summary>
    public string Icon { get; init; } = "placeholder";
    /// <summary>目录卡上的一句话范围。</summary>
    public string Summary { get; init; } = "";
    public List<HelpEntry> Entries { get; } = new();
}

/// <summary>主题下的一个条目：标题 + 段落。段落按当前配置/实例现算（说明跟随生效参数，不另建第二套数值）。</summary>
public sealed class HelpEntry
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Icon { get; init; }
    public Func<GameEngine, GameState, IEnumerable<string>> Paragraphs { get; init; } = (_, _) => Array.Empty<string>();
}

/// <summary>
/// 系统说明目录（S14 §13.3）。本里程碑只搭目录框架与浏览层（M61）；各主题条目正文由 M66 按各主案整理后填入 Entries，
/// 并由属性/状态详情通过 GameSession.OpenHelp(topicId, entryId) 直接定位。没有条目的主题显示“内容整理中”。
/// </summary>
public static class HelpCatalog
{
    public static readonly IReadOnlyList<HelpTopic> Topics = new List<HelpTopic>
    {
        new() { Id = "basics", Title = "入门与操作", Icon = "camp", Summary = "卡片选择、拖拽与转移、数量、界面层级、对白与记录、开局引导" },
        new() { Id = "time", Title = "时间、行动与保存", Icon = "bed", Summary = "世界时钟、工作与固定时长、暂停与退出、休息睡眠、保存与恢复" },
        new() { Id = "map", Title = "地图、探索与资源", Icon = "route", Summary = "地点移动、发现、清理恢复、采集与搜集" },
        new() { Id = "items", Title = "物品、容器与负重", Icon = "pot", Summary = "物品归属、批次与堆叠、保质、重量与负重、锅内容与液体、装备耐久" },
        new() { Id = "body", Title = "人物、生存与医疗", Icon = "bandage", Summary = "属性与熟练度、饮食、恢复、异常状态、免疫与感染、医疗" },
        new() { Id = "camp", Title = "制作、营地与烹饪", Icon = "fire_lit", Summary = "配方与预留、建造、点火与燃料、被动加工、等待烹饪" },
        new() { Id = "combat", Title = "敌踪与战斗", Icon = "enemy_shambler", Summary = "遭遇、回合与行动点、动作与站位、伤害与状态、撤退与掉落" },
        new() { Id = "baobao", Title = "饱饱与吞噬融合", Icon = "status_baobao", Summary = "饱饱已公开的身份与功能、吞噬融合流程" },
    };

    public static HelpTopic? Topic(string? id) => Topics.FirstOrDefault(t => t.Id == id);

    public static HelpEntry? Entry(string? topic, string? entry) => Topic(topic)?.Entries.FirstOrDefault(x => x.Id == entry);
}

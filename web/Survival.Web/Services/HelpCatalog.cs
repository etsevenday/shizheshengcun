using Survival.Core.Config;
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
    public List<HelpEntry> Entries { get; init; } = new();
}

/// <summary>插图行的一格：图标键（Icons 键；"dir:N" 为第 N 个方位箭头）+ 名称 + 一句说明。</summary>
public sealed record HelpFigure(string Icon, string Label, string? Note = null);

/// <summary>主题下的一个条目：标题 + 段落（+ 可选插图行）。段落按当前配置/实例现算（说明跟随生效参数，不另建第二套数值）。</summary>
public sealed class HelpEntry
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Icon { get; init; }
    public Func<GameEngine, GameState, IEnumerable<string>> Paragraphs { get; init; } = (_, _) => Array.Empty<string>();
    /// <summary>插图/图例（例如方位箭头、状态图标、指令图），为空不显示。</summary>
    public Func<GameEngine, GameState, IEnumerable<HelpFigure>>? Figures { get; init; }
}

/// <summary>
/// 系统说明目录（S14 §13.3、总纲 §3.2）：8 个玩家主题，正文按各主案的现行规则整理成玩家文案（M66）。
/// 所有数值都从当前配置/实例现取（Q7 新值随配置生效），不写系统编号、存档结构、开发日志、待确认事项或未实现功能；
/// 未发现的地点/对象不在这里点名，首战教学奖励等剧情内容只在玩家已经经历后才写出。
/// 属性/状态详情通过 GameSession.OpenHelp(topicId, entryId) 直接定位到同一条目，返回时回到原入口。
/// </summary>
public static class HelpCatalog
{
    private static string T(double v) => Fmt.T(v);
    private static string ST(double v) => Fmt.SignedT(v);
    private static string Pct(double v) => Fmt.Pct(v);
    private static string Kg(long g) => Fmt.Kg(g);
    private static string Dur(int m) => Clock.Duration(m);
    private static string HHmm(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";

    private static string Rates(RateBlock r) =>
        string.Join("、", new[] { ("饱食", r.Satiety), ("水分", r.Hydration), ("体力", r.Energy), ("清醒", r.Wakefulness), ("精神", r.Morale) }
            .Where(x => Math.Abs(x.Item2) > 1e-9).Select(x => $"{x.Item1}{ST(x.Item2)}"));

    public static readonly IReadOnlyList<HelpTopic> Topics = new List<HelpTopic>
    {
        // ───────────── 入门与操作 ─────────────
        new()
        {
            Id = "basics", Title = "入门与操作", Icon = "camp", Summary = "卡片选择、拖拽与转移、数量、界面层级、对白与记录、开局引导",
            Entries =
            {
                new HelpEntry
                {
                    Id = "screen", Title = "界面分区", Icon = "route",
                    Paragraphs = (e, s) => new[]
                    {
                        "中间从上到下是四行，高度一样、始终同屏：地图与场景（当前地点大图和相邻地点）、设施与资源（火堆、营地设施、敌踪、可采集的资源）、当前地面、随身物品。选卡、展开锅、打开面板都不会改变行高，内容多时在所在行里滚动。",
                        $"随身物品行固定{e.MaxRootCards}个卡位，最多{e.MaxRootCards}张卡全部可见，不滚动；空位显示为“空位”。",
                        "人物对话、战斗和顶栏面板只盖住上面两行，当前地面和随身两行位置不变。",
                        "左栏是时间、基础属性、免疫与病毒、状态图标和携带重量；右栏分“对象信息”和“人物”两页，只用来阅读详情与完整预测。人物页是熟练度和词条，可分别展开/收起。",
                        "顶栏是区域地图、手工制作、休息、行动日志，以及生存目标、生存提示、对话记录和本页“系统说明”。打开它们只是浏览，不推进游戏时间。",
                    },
                },
                new HelpEntry
                {
                    Id = "cards", Title = "卡片、选择与转移", Icon = "canned_food",
                    Paragraphs = (e, s) => new[]
                    {
                        "点一张卡选中它：卡片下方弹出它的小操作窗（拾取/放下数量、食用、取出等），右栏显示它的详情。操作窗浮在卡旁，不会把这一行撑高；下方放不下时会翻到上方。点“×”、再点别处或按 Esc 收起，不执行任何东西。",
                        "物品卡右下角的三角箭头是“转移1份”：▼ 从当前地面取到随身，▲ 从随身放到当前地面，按住不放会连续转移。锅里的水和食材、火堆里的锅和食物，箭头都是“取到随身”。要改数量或目的地，点卡片本身在操作窗里明确执行。",
                        "选中物品卡后直接按数字键会跳到数量框；数量框里回车执行、Esc 退出。",
                    },
                },
                new HelpEntry
                {
                    Id = "drag", Title = "拖拽与落点", Icon = "wood",
                    Paragraphs = (e, s) => new[]
                    {
                        "按住卡片拖动，经过能接收它的地方会高亮，松手才执行：木材拖到火堆燃料区投料，打火器拖到火堆点火，锅和生块根拖到火堆空加工位，水拖到锅的水量条，食材拖到锅的食材区，工具拖到资源卡指定使用，材料拖到制作面板的材料槽指定批次。",
                        "拖到不能接收的地方松手，物品回到原处，什么也不发生。按住 Shift 松手可以先选数量。",
                    },
                },
                new HelpEntry
                {
                    Id = "nearby", Title = "就近执行与浮窗", Icon = "route",
                    Paragraphs = (e, s) => new[]
                    {
                        "需要确认的行动，按钮就在对象卡上或卡旁的小浮窗里：点相邻地图卡、区域地图上的相邻地点或“前往”，在旁边看耗时、到达时刻、负重和入夜影响后“确认移动”；敌踪卡点“接近”，在卡旁看风险后“接近并开战”；把工具拖到资源卡后在资源卡旁“开始”；制作和建造在所选配方卡下方“开始”；休息、睡眠、等待在所选时长旁“开始”。右栏只看信息和预测，没有执行按钮。",
                        "浮窗里的“×”、“取消”或 Esc 只收起浮窗，不执行任何东西，也不会取消已经在进行的行动。对象滚出视野或消失时浮窗自动收起。",
                        "转移、食用、点火、等待烹饪这些直接操作不额外弹确认。",
                    },
                },
                new HelpEntry
                {
                    Id = "esc", Title = "Esc 与界面层级", Icon = "placeholder",
                    Paragraphs = (e, s) => new[]
                    {
                        "Esc 每次只退一层：先收起正在拖拽的卡、数量框、状态说明或操作浮窗，再关闭打开的面板或说明，再取消选中，最后才打开菜单。",
                        "Esc 不会退出正在进行的行动，退出请点屏幕中央推进条上的“退出”。战斗中 Esc 也不能脱战，战斗菜单只有“继续”和“保存并返回标题”。",
                    },
                },
                new HelpEntry
                {
                    Id = "dialogue", Title = "人物对话与对话记录", Icon = "status_baobao",
                    Paragraphs = (e, s) => new[]
                    {
                        "人物对话会盖住地图与设施两行，左侧是你，右侧是说话的对象。对话一句一句累计显示，点“继续”看下一句，最后一句是“结束对话”。",
                        "对话没读完时，游戏的其他操作都会暂停受理；正在进行的行动或战斗停在原处等你读完，不会在你阅读时偷偷推进时间。",
                        "读过的对话都能在顶栏“对话记录”里按次重看，重看不会重新触发剧情。",
                    },
                },
                new HelpEntry
                {
                    Id = "start", Title = "开局与生存目标", Icon = "camp",
                    Paragraphs = (e, s) => new[]
                    {
                        $"新的一局从第1日{HHmm(e.Config.Time.StartAbsoluteMinute % e.Config.Time.MinutesPerDay)}在临时营地开始。营地有一个未点燃的火堆，随身和营地地面有少量起步物资。",
                        "顶栏“生存目标”列出本局的阶段目标和完成情况（实际做到才记录，之后消耗也不回退）；“生存提示”给当前处境的求生建议。它们只是建议，不按日期判失败。",
                        "健康归0会死亡，病毒达到上限会尸变，两者都会结束本局。存活到第8日早上会有一次七日生存检查，之后可以继续生存。",
                    },
                },
            },
        },

        // ───────────── 时间、行动与保存 ─────────────
        new()
        {
            Id = "time", Title = "时间、行动与保存", Icon = "bed", Summary = "世界时钟、工作与固定时长、暂停与退出、休息睡眠、保存与恢复",
            Entries =
            {
                new HelpEntry
                {
                    Id = "clock", Title = "世界时钟与昼夜", Icon = "outdoor",
                    Paragraphs = (e, s) => new[]
                    {
                        $"游戏时间只在你开始行动后流逝，按{e.Config.Time.TickMinutes}分钟一段推进；浏览、整理物品、看说明都不推进时间。关闭页面后时间不会自己走。",
                        $"白天是{HHmm(e.Config.Time.DaylightStartMinute)}到{HHmm(e.Config.Time.DaylightEndMinute)}，其余时间是夜晚。夜里工作类行动更慢（见“工作量与倍率”）。",
                        "所有行动都同时消耗饱食和水分；不同行动对体力、清醒和精神的影响不同，开始前的预测会写出每项变化。",
                    },
                },
                new HelpEntry
                {
                    Id = "work", Title = "工作量与倍率", Icon = "hatchet",
                    Paragraphs = (e, s) =>
                    {
                        var w = e.Config.WorkFactors;
                        return new[]
                        {
                            "移动、探索、清理、采集、制作和建造是“工作”：标的是基础工作量，实际耗时会乘上当时的倍率。休息、睡眠、等待、点火、食用是固定时长，不受倍率影响。",
                            $"夜间×{T(w.Night)}，负重超过轻装上限×{T(w.Overweight)}，体力偏低×{T(w.LowEnergy)}；同时出现时相乘，最多×{T(w.Cap)}。行动途中进入夜晚等情况，倍率会在那一刻改变，预测里会写明时刻。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "progress", Title = "中央推进条、暂停与退出", Icon = "placeholder",
                    Paragraphs = (e, s) => new[]
                    {
                        "行动开始后，屏幕中央出现推进条：行动名、进度、剩余游戏时间和“退出”。固定时长写“剩余”，会受倍率或燃料影响的写“预计剩余”。",
                        "途中某项属性越过提醒线时，行动会自动暂停：可以先吃喝、包扎或给火堆加木，再“继续”。",
                        "点“退出”会在当前这一段结束时停下：已经过去的时间和已经发生的结果不退回，没做完的部分不结算；制作会把预留材料按原规则返还。点火、食用、吞噬融合这类一段就完成的事，已经开始就会做完这一段。",
                        "跳过动画只是加快显示，不改变任何结果，任何速度下都可以退出。",
                    },
                },
                new HelpEntry
                {
                    Id = "rest", Title = "休息、睡眠与等待", Icon = "bed",
                    Paragraphs = (e, s) =>
                    {
                        var c = e.Config;
                        var hr = c.HourlyRates;
                        var list = new List<string>
                        {
                            $"休息（每小时{Rates(hr["rest"])}）只恢复体力，不恢复清醒。可选{string.Join("、", c.Actions.RestMinutes.Select(Dur))}。",
                            $"睡眠是唯一恢复清醒的方式：遮蔽处每小时{Rates(hr["sleep_sheltered"])}；露天每小时{Rates(hr["sleep_exposed"])}。可选{string.Join("、", c.Actions.SleepMinutes.Select(Dur))}，任何时间都能睡。",
                            $"等待（每小时{Rates(hr["wait"])}）什么也不恢复，只让时间过去，例如等天亮或等火堆加工。",
                            $"室内地点和营地建成遮蔽棚后算“遮蔽”。营地床垫让睡眠的体力每小时额外{ST(c.FacilityEffects.BedExtraSleepEnergyPerHour)}；精神低于提醒线时，休息与睡眠的体力恢复×{T(c.FacilityEffects.LowMoraleEnergyRecoveryFactor)}。",
                            "休息和睡眠满了也不会自动结束；途中越过提醒线会暂停。",
                        };
                        return list;
                    },
                },
                new HelpEntry
                {
                    Id = "save", Title = "保存与继续", Icon = "placeholder",
                    Paragraphs = (e, s) => new[]
                    {
                        "游戏只有一个存档，在本浏览器里自动保存：每完成一段时间、每次转移或使用物品、每条战斗指令之后都会保存，页脚显示“已保存”。",
                        "关闭页面或返回标题不会丢失进度，下次点“继续”从原处接着玩，进行中的行动、战斗和未读完的对话都会恢复。保存失败时页脚会提示，可以点“重试”。",
                        "开始新游戏会覆盖旧存档，覆盖前会再确认一次。",
                    },
                },
            },
        },

        // ───────────── 地图、探索与资源 ─────────────
        new()
        {
            Id = "map", Title = "地图、探索与资源", Icon = "route", Summary = "地点移动、方位图例、发现、清理恢复、采集与搜集",
            Entries =
            {
                new HelpEntry
                {
                    Id = "travel", Title = "移动", Icon = "route",
                    Paragraphs = (e, s) => new[]
                    {
                        "一次只能沿一条已知路线走到相邻地点，没有自动寻路。点相邻地图卡或区域地图上的相邻地点，在旁边的浮窗里确认移动。",
                        "路线标的是基础时间，实际会乘夜间、超重、低体力倍率。途中不能取用出发地或目的地的物资；到达那一刻才换到新地点。中途退出就留在出发地，已经走过的时间不退。",
                        "移动不享受出发地火堆的照明。",
                    },
                },
                new HelpEntry
                {
                    Id = "direction", Title = "方位图例", Icon = "route",
                    Paragraphs = (e, s) => new[]
                    {
                        "相邻地图卡图片左上角的小圆章是方位：箭头指向这个地点在区域地图上相对你当前所在地的方向，和区域地图的布局一致。悬停会写出“位于当前地点右侧”等。",
                        "它只是方位提示，点它不会移动，也不能用方向键走格；移动仍要点“前往”并确认。你所在的地点卡标“当前地点”，没有自己的箭头。没发现的地点不会出现。",
                        "区域地图上已知的其他地点框左上角也有同样的方位圆章（你所在的地点没有）。方位只说明在哪个方向，不代表能直接走过去：不相邻、没解锁路线的地点照样不能直达。",
                        "移动途中仍以出发地为准，到达后所有卡和区域地图的方位一起刷新。",
                    },
                    Figures = (e, s) => Enumerable.Range(1, 8).Select(i => new HelpFigure($"dir:{i}", GameEngine.DirectionName((MapDirection)i))),
                },
                new HelpEntry
                {
                    Id = "explore", Title = "探索与发现", Icon = "outdoor",
                    Paragraphs = (e, s) => new[]
                    {
                        $"探索一次{e.Config.Actions.ExploreMinutes}分钟（工作量），推进当前地点的探索度{e.Config.Actions.ExploreProgress}%，到100%为止。探索度到达一定阶段时会发现新路线或资源点，地图只显示已经发现的地点和路线。",
                        "每次探索完成时，从当地物资表里抽一次，可能找到零散物资，也可能什么都没有；产物留在当前地面，不会自动拾取。",
                    },
                },
                new HelpEntry
                {
                    Id = "clear", Title = "清理", Icon = "stone",
                    Paragraphs = (e, s) =>
                    {
                        var lc = e.Location(s.CurrentLocation);
                        return new[]
                        {
                            $"清理一次{e.Config.Actions.ClearMinutes}分钟（工作量），翻找地块表层的零散基础物资，完成时抽一次物资表，产物留在当前地面。清理不增加探索度。",
                            $"每个地点有自己的清理次数，开始时扣1次，退出或没找到东西都不返还；次数随游戏时间慢慢恢复。你现在所在的{lc.Name}最多{lc.Clear.Cap}次，每{Dur(lc.Clear.RefillIntervalMinutes)}恢复1次。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "gather", Title = "采集与搜集", Icon = "fiber",
                    Paragraphs = (e, s) => new[]
                    {
                        "资源卡显示每次产出、耗时和库存。有的资源不限量，有的有库存并且每天补充一些；“搜集”类的旧箱子只能搜一次。",
                        "随身带着合适的工具会自动用成色最好的一件提高效率（比如刀让纤维更快、斧让木材翻倍），也可以把某一把工具拖到资源卡上指定使用。工具每次使用会磨损。",
                        "部分资源点有受伤风险，卡上和预测里会写出概率与伤害。",
                    },
                },
            },
        },

        // ───────────── 物品、容器与负重 ─────────────
        new()
        {
            Id = "items", Title = "物品、容器与负重", Icon = "pot", Summary = "物品归属、批次与堆叠、保质、重量与负重、锅内容与液体、装备耐久",
            Entries =
            {
                new HelpEntry
                {
                    Id = "where", Title = "物品在哪里", Icon = "canned_food",
                    Paragraphs = (e, s) => new[]
                    {
                        "每件物品只在一个地方：随身、某个地点的地面、火堆里或锅里。只有随身物品和你所在地点的地面能直接使用；别处地面上的东西要走过去才能拿。",
                        "同一种物品、到期时间相同的会合成一堆，卡上显示数量；到期时间不同的分开显示。制作开始后被预留的那几份会标“已预留”，不能再转移或使用。",
                    },
                },
                new HelpEntry
                {
                    Id = "weight", Title = "重量与负重", Icon = "wood",
                    Paragraphs = (e, s) =>
                    {
                        var c = e.Config.Carry;
                        return new[]
                        {
                            "所有重量统一按公斤显示两位小数，例如10克显示为0.01公斤。合计时先按真实克数相加再显示，不会因为四舍五入多算或少算。",
                            $"随身总重超过{c.SoftGrams / 1000}公斤算超重，工作类行动耗时×{T(e.Config.WorkFactors.Overweight)}；上限是{c.HardGrams / 1000}公斤，超过上限的拾取整次拒绝。左栏和随身栏标题一直显示当前携重。",
                            $"随身最多{c.MaxRootCards}张物品卡，和重量同时检查：同一批能叠在一起的算1张（超过堆叠上限会分成几张），每件工具、武器各算1张，锅连同里面的水和食材只算1张，武器弹仓里的弹药不另算。会多出第{c.MaxRootCards + 1}张卡的拾取、取出整次拒绝，什么都不会先动；能并进已有那一堆的照常拾取。把物品装进随身的锅里可以腾出卡位，用掉最后一份才释放那张卡。",
                            "锅、装填的弹药等连同里面的内容一起计重；把锅里的水倒出一份成为随身水卡，总重不变，但会多占一张卡。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "spoil", Title = "保质与腐败", Icon = "berry",
                    Paragraphs = (e, s) => new[]
                    {
                        "食物有保质时间，卡上写“N小时后腐败”。到期就变成腐败，不能再吃，也不能再放进火堆或锅里烹饪。保质只随游戏时间减少，仅仅放进锅里、等水净化或被制作预留都不会暂停。",
                        "烹饪中的保护：新鲜食材放进燃烧的火堆（或火堆上有净水的锅）、实际受热满一段后，就受这次烹饪保护，卡上写“受热保护中”，即使原来的保质时间到了也会照常做熟。火熄灭时保护保留；把这份食材或整口锅取出来，保护就结束，若原保质时间已过，取出后立刻腐败（取出前操作窗会写明）。受保护期间，本来就能直接吃的食物（如锅里复煮的烤肉）即使过了原保质时间也可以直接吃，按原来的食物结算；吃掉就终止这一份的加工、释放它预留的水，已烧掉的燃料不退，也不会再做出成品。",
                        $"烹饪和复煮做出来的成品，从完成那一刻重新计算自己的保质（例如水煮肉{Dur(e.Config.Items.FirstOrDefault(i => i.Id == "boiled_meat")?.ShelfLifeMinutes ?? 0)}）；成品之后照常腐败，水煮肉再放回锅里只是存放，不会再刷新。",
                    },
                },
                new HelpEntry
                {
                    Id = "pot", Title = "锅、水与食材", Icon = "pot",
                    Paragraphs = (e, s) =>
                    {
                        var pot = e.Config.Containers.TryGetValue("pot", out var pc) ? pc : null;
                        var list = new List<string>();
                        if (pot != null)
                        {
                            list.Add($"金属锅能装{pot.CapacityMl}毫升水（每份{pot.UnitMl}毫升），另有{pot.FoodSlots}个食材位。净水和脏水不能混装；锅没有耐久。");
                        }
                        list.Add("把水卡拖到锅的水量条装入，把生块根、生肉、烤肉拖到食材区；水煮肉也能放进锅里存放。锅放进燃烧的火堆才会加热，加热规则见“制作、营地与烹饪”。");
                        list.Add("锅里水和食材那一行的箭头是“从锅中取到随身”：取1份成为独立的随身卡。锅本身在随身时也是如此，总携重不变，但要有空的随身卡位（能并进已有那一堆的除外），卡位满了会整次拒绝。被加工预留的水暂时不能取。");
                        list.Add("取出正在受热的食材或整口锅，会清掉对应的加工进度并结束烹饪保护；原保质时间已过的食材取出后立刻腐败。取出失败（超重、卡位满）时什么都不变。");
                        return list;
                    },
                },
                new HelpEntry
                {
                    Id = "durability", Title = "工具与武器耐久", Icon = "stone_knife",
                    Paragraphs = (e, s) => new[]
                    {
                        "刀、斧、武器和打火器有耐久，每件的最大耐久在一定范围内随机。采集时用到的工具、每次执行攻击用到的武器都会磨损，耐久不足就不能再用来攻击。",
                        "手工制作里的“工具维护”可以把一件工具恢复一部分耐久，不超过它自己的上限；选中有耐久的装备卡，卡下也有“维修”。",
                        "装备有白、绿、蓝三种品质，分别带0、1、2条附加词条，生成时抽定，维修、穿脱、读档都不会重抽。手工做出的装备默认绿色1条，拾荒箱里的按新手拾荒概率（白80%、绿20%），开局物品、丧尸掉落等没写品质的来源都是白色。战斗饰品可能带校准（佩戴者所有攻击命中+4，空手拳击也算）、轻量或稳固，求生饰品可能带熟作、节用、舒缓或稳步；同一条词条穿在几件装备上只算最强的一件。词条池上线前拿到的绿/蓝饰品没有词条，保持原样、不补抽。",
                        "防具、战斗饰品、上衣、下衣、求生饰品各有一个穿戴槽。求生类效果（工作量、体力消耗、恢复、划伤、维护比例等）穿上就生效；速度、护甲、命中、暴击、忽略护甲、伤害乘数、招式体力、噪声等战斗类效果在战斗中生效（正速度合计最多+3，护甲最多0.9）；位移抵抗在我方多人时才有用（只剩你一人时被拉扯、击退也没有位置可移动）。耐久为0的装备效果暂停。",
                        "不需要的装备可以在卡下“拆解”：15分钟，按装备的拆解表回收材料（木石装备返木材/石块，金属件返废金属，衣物返布料或皮革，饰品与器具各有对应材料；对象信息栏“拆解”一行写明），产物放在当前地面；能自己做的装备回收价值不超过标准配方材料价值的80%，品质高不一定多给。拆解看制造熟练度：制造1级可拆0阶、3级1阶、7级2阶、10级3阶装备，每件完成制造经验+2。穿戴中的先卸下，弹仓里有弹药的先打空，耐久为0也能拆。",
                    },
                },
            },
        },

        // ───────────── 人物、生存与医疗 ─────────────
        new()
        {
            Id = "body", Title = "人物、生存与医疗", Icon = "bandage", Summary = "基础属性与熟练度、饮食、恢复、异常状态、免疫与感染、医疗",
            Entries =
            {
                new HelpEntry { Id = "stat-health", Title = "健康", Icon = "bandage", Paragraphs = (e, s) => new[] { Ui.StatExplain(e, StatId.Health) } },
                new HelpEntry { Id = "stat-satiety", Title = "饱食", Icon = "canned_food", Paragraphs = (e, s) => new[] { Ui.StatExplain(e, StatId.Satiety) } },
                new HelpEntry { Id = "stat-hydration", Title = "水分", Icon = "clean_water", Paragraphs = (e, s) => new[] { Ui.StatExplain(e, StatId.Hydration) } },
                new HelpEntry { Id = "stat-energy", Title = "体力", Icon = "status_low_energy", Paragraphs = (e, s) => new[] { Ui.StatExplain(e, StatId.Energy) } },
                new HelpEntry { Id = "stat-wakefulness", Title = "清醒度", Icon = "bed", Paragraphs = (e, s) => new[] { Ui.StatExplain(e, StatId.Wakefulness) } },
                new HelpEntry { Id = "stat-morale", Title = "精神", Icon = "status_low_morale", Paragraphs = (e, s) => new[] { Ui.StatExplain(e, StatId.Morale) } },
                new HelpEntry
                {
                    Id = "food", Title = "饮食与饮水", Icon = "clean_water",
                    Paragraphs = (e, s) =>
                    {
                        var c = e.Config;
                        return new[]
                        {
                            $"选中食物或水，点卡下的“食用/饮用”，耗时{c.ItemActions.UseMinutes}分钟，效果开始时立即生效；超出上限的部分浪费，按钮旁会提示溢出。",
                            $"脏水必定引起腹泻{Dur(c.ItemActions.DiarrheaMinutes)}，期间每小时额外失水{T(-c.ItemActions.DiarrheaExtraHydrationPerHour)}。脏水在火堆上的锅里烧开就成了净水。",
                            "生块根、生肉要烤或煮熟才能吃。战斗中不能吃喝。",
                        };
                    },
                },
                new HelpEntry { Id = "immunity", Title = "免疫力", Icon = "status_antibiotic", Paragraphs = (e, s) => new[] { Ui.ImmunityExplain(e) } },
                new HelpEntry { Id = "virus", Title = "感染与病毒", Icon = "status_infected", Paragraphs = (e, s) => new[] { Ui.VirusExplain(e) } },
                new HelpEntry
                {
                    Id = "medical", Title = "医疗", Icon = "bandage",
                    Paragraphs = (e, s) =>
                    {
                        var c = e.Config;
                        var bandage = c.Items.FirstOrDefault(i => i.Use?.IsBandage == true);
                        var list = new List<string>
                        {
                            $"{bandage?.Name ?? "绷带"}：止血，并恢复健康{T(bandage?.Use?.Health ?? 0)}×(1+0.02×医疗等级)，回血之后{c.ItemActions.BandageCooldownMinutes}分钟冷却；已感染时还能压低一点病毒。止血、回血、压低病毒三样里只要有一样这次真的有用就能包扎；冷却中这次可能不回血，按钮旁会写明这次的实际效果。三样都没用时按钮灰掉并写原因。",
                            "抗生素：只对已感染有效，压低病毒，有给药间隔；病毒特效药：立即清除感染。",
                            $"战斗中也能包扎和用药，每次{c.CombatActions.FirstOrDefault(a => a.Id == "bandage")?.ApCost ?? 2}个行动点。",
                        };
                        return list;
                    },
                },
                new HelpEntry
                {
                    Id = "skills", Title = "熟练度与经验", Icon = "stone_knife",
                    Paragraphs = (e, s) => new[]
                    {
                        $"共有{e.Config.Skills.Count}门熟练度（{string.Join("、", e.Config.Skills.Select(k => k.Name))}），都从1级开始，最高10级；升到下一级要50×(当前等级+1)经验，1级到10级共{e.TotalXpToCap("crafting")}经验，到10级后这一门不再获得经验。",
                        $"经验来源：攻击命中每个目标+{e.Config.Xp.DamagingAttackCommand}（同一个敌人对同一门最多{e.Config.Xp.MaxHitsPerEncounterEnemySkill}次）；有效医疗+{e.Config.Xp.Medical}；制作、建造、维护完成按配方时间给制造经验；采集或搜集每成功一次+{e.Config.Xp.GatherSubmit}（同一资源点每天最多{e.Config.Xp.GatherDailyPerNode}次计经验，箱子练搜集，其余练采集）；每走完一段路+{e.Config.Xp.TravelEdge}行旅（同一方向每天一次）。预估、取消不给经验。",
                        "武器熟练度提高伤害与命中能力、减少技能体力。制造熟练度不改变制作工时，按等级开放拆解（制造1/3/7/10级可拆0/1/2/3阶装备，每件制造经验+2），10级回收专家。每门在1/3/5/7/10级各有一个专属技能：制造、采集、搜集、烹饪、行旅五门的是求生被动（如采集7级工作量×0.9），达到等级自动生效；拳、刀剑、斧、矛、钝器、弓弩、枪械、盾、指挥、医疗十门的是战斗技能，装上这一类武器（空手即拳）后出现在该武器页，1级攻击技能就是普通攻击，其余随等级解锁。人物页“熟练度”悬停一门可看它的五个技能。",
                    },
                },
                new HelpEntry
                {
                    Id = "statuses", Title = "状态图标一览", Icon = "status_unknown",
                    Paragraphs = (e, s) => new[]
                    {
                        "左栏各属性下方的小图标是正在生效的状态，悬停看名称、效果和剩余时间，点击可以固定阅读。危险状态（出血、属性归零、感染）总是优先显示，放不下的进“全部状态”。",
                        "下面是可能出现的图标。没出现的说明你现在没有这个状态。",
                    },
                    Figures = (e, s) => new[]
                    {
                        new HelpFigure("status_bleed", "出血", "每隔一段时间扣健康，包扎止血"),
                        new HelpFigure("status_diarrhea", "腹泻", "额外失水，到时自愈"),
                        new HelpFigure("status_bandage_cd", "绷带冷却", "冷却中包扎可能本次不回血"),
                        new HelpFigure("status_zero", "属性归零", "健康按小时下降"),
                        new HelpFigure("status_low_energy", "体力偏低", "工作变慢"),
                        new HelpFigure("status_low_morale", "精神低落", "休息睡眠回体力变慢"),
                        new HelpFigure("status_infected", "感染", "病毒随免疫变化"),
                        new HelpFigure("status_antibiotic", "抗生素", "压低病毒的给药效果"),
                        new HelpFigure("status_baobao", "饱饱", "体内的饱饱对病毒的影响"),
                        new HelpFigure("status_panic", "战斗恐慌", "命中能力下降，战后清除"),
                    },
                },
            },
        },

        // ───────────── 制作、营地与烹饪 ─────────────
        new()
        {
            Id = "camp", Title = "制作、营地与烹饪", Icon = "fire_lit", Summary = "配方与预留、建造、点火与燃料、被动加工、等待烹饪",
            Entries =
            {
                new HelpEntry
                {
                    Id = "craft", Title = "手工制作", Icon = "stone_knife",
                    Paragraphs = (e, s) => new[]
                    {
                        "顶栏“手工制作”列出全部配方。材料从随身和当前地面取，火堆和锅里的要先取出。选一个配方查看材料够不够，在配方卡下方点“开始制作”。",
                        "选配方、改件数、指定批次都只是预览，不会扣东西；点开始时才重新检查材料、工具和所在地，并把材料预留。完成时一次扣除材料、产物放在当前地面。",
                        "中途退出：材料全部返还当前地面，已经过去的时间不退，不会得到半成品。材料在完成前腐败则制作中止。同一配方可以批量制作。",
                    },
                },
                new HelpEntry
                {
                    Id = "build", Title = "营地设施", Icon = "shelter",
                    Paragraphs = (e, s) =>
                    {
                        var sh = e.Config.HourlyRates["sleep_sheltered"];
                        return new[]
                        {
                            $"营地可以建遮蔽棚和床垫，建在营地里，不能搬走。遮蔽棚让营地算作遮蔽：睡眠每小时{Rates(sh)}，休息额外恢复精神。床垫要先建遮蔽棚，让营地睡眠的体力每小时额外{ST(e.Config.FacilityEffects.BedExtraSleepEnergyPerHour)}。",
                            "设施行里虚线框的“未建造”卡点开就是对应的建造配方。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "fire", Title = "火堆、点火与燃料", Icon = "fire_lit",
                    Paragraphs = (e, s) =>
                    {
                        var f = e.Config.Fire;
                        return new[]
                        {
                            $"火堆燃料最多{Dur(f.MaxFuelMinutes)}，每投入1份木材+{f.BurnMinutesPerWood}分钟，投料不耗时。燃烧时每过一段时间消耗燃料，燃尽就熄灭，剩余燃料保留。",
                            $"点火需要打火器：点“点燃”选一把，或把打火器拖到火堆，耗时{f.IgniteMinutes}分钟并磨损打火器。燃料为0时点火会自动投入1份木材。",
                            $"火堆有{f.ProcessingSlots}个加工位，可以放一口锅和直接烤的食物。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "cook", Title = "自动加工", Icon = "boiled_root",
                    Paragraphs = (e, s) =>
                    {
                        var list = new List<string>
                        {
                            "烧水、烤制、水煮不额外占你的主行动，但仍需要世界时间推进：放进燃烧的火堆后，你去探索、清理、休息、睡觉或等待时，世界时间每推进一段就自动加热一段。火熄灭时加工暂停、进度和烹饪保护保留；只看界面不推进。",
                            "已经腐败的食材不能放进去烹饪；新鲜食材实际受热后受这次烹饪保护，做熟不会因原保质时间到了而失败（详见“保质与腐败”）。",
                        };
                        foreach (var p in e.Config.Processes)
                        {
                            var input = e.ItemName(p.InputId);
                            var output = e.ItemName(p.OutputId);
                            list.Add(p.IsPotBatch
                                ? $"{p.Name}：锅里的{input}受热{p.HeatMinutes}分钟变成{output}，0.5升或1升同样时间。"
                                : p.InPot
                                    ? $"{p.Name}：{input}放在有{e.ItemName(p.RequiredLiquid ?? "clean_water")}的锅里受热{p.HeatMinutes}分钟成为{output}，完成时用掉{p.WaterCostMl}毫升。"
                                    : $"{p.Name}：{input}直接放进火堆加工位，受热{p.HeatMinutes}分钟成为{output}。");
                        }
                        list.Add("成品留在原位，不会自动吃掉或取出；保质从完成那一刻起算。");
                        return list;
                    },
                },
                new HelpEntry
                {
                    Id = "waitcook", Title = "等待烹饪", Icon = "fire_lit",
                    Paragraphs = (e, s) => new[]
                    {
                        "火堆里有东西时，火堆卡上会出现“等待烹饪”：一直等到这次能完成的加工全部做完才结束，多样东西同时加热时按最晚完成的算。悬停能看到每样的预计时间和没被纳入的原因。",
                        "做熟前才会过原保质时间的新鲜食材照样纳入（受热后受烹饪保护）；只有在第一次实际受热前就会腐败的（例如还在等水净化）才不纳入并写明原因。按下等待本身不会提前给任何食材保护。",
                        "等待期间身体消耗和普通等待一样。它不会自动点火、加木或补水，燃料不够时会写明；可以随时从中央推进条退出，已有的加工进度保留。",
                    },
                },
            },
        },

        // ───────────── 敌踪与战斗 ─────────────
        new()
        {
            Id = "combat", Title = "敌踪与战斗", Icon = "enemy_shambler", Summary = "遭遇、回合与行动点、动作与站位、伤害与状态、撤退与掉落",
            Entries =
            {
                new HelpEntry
                {
                    Id = "encounter", Title = "敌踪与遭遇", Icon = "enemy_shambler",
                    Paragraphs = (e, s) =>
                    {
                        var en = e.Config.Encounters;
                        return new[]
                        {
                            "敌人是世界里真实存在的个体，会在地点之间游荡；只会遇到当地已有的敌人，不会凭空出现。营地是安全的。",
                            $"从第{en.NoRandomBeforeDay}日起，在有敌对敌人的地点完成探索或清理后可能发现敌踪。原来的行动结果已经算好，接下来选“接敌”或“绕行”：绕行{en.BypassMinutes}分钟、按移动消耗，之后{Dur(en.PostExitRandomEncounterGraceMinutes)}内不会再随机遇敌。",
                            "设施行“敌踪”分组里是当前地点能看到的敌人，点卡上的“接近”在卡旁看风险，再点“接近并开战”主动交战。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "rounds", Title = "回合与行动点", Icon = "cmd_end_turn",
                    Paragraphs = (e, s) =>
                    {
                        var sc = e.Config.Scope;
                        return new[]
                        {
                            "战斗按大回合进行，每轮开始时按有效速度从快到慢排出行动顺序（顶部的顺序条），轮中不会重排。有效速度 = 基础4 + 装备速度修正（正加成最多+3）+ 状态，体力低于4、清醒低于20各−1，最低1。速度可以有小数：合计后四舍五入到0.01（显示1—2位小数，如4.0、4.05）再比；速度完全相同时按入场时锁定的随机数排序，读档不重抽。追击进度按0.0001累积，不会每轮取整丢掉。",
                            $"轮到你时有{e.NormalActivationAp(s.Player)}个行动点（AP；战术训练阶位{s.Player.TacticalTier}，阶位0/1/2分别为{string.Join("/", sc.ApByTier)}AP），用不完不能留到下一次。普通攻击和调整呼吸2AP，防御、换位、装填1AP，撤退2AP。",
                            "技能的行动点与体力按技能表：推挡、盾击、护身、蓄力、架矛、猎标、快速装填、鼓舞1AP，多数攻击、控制、保护、医疗2AP，连拳、处决、破阵、连刺、重锤、连射、决胜射击、坚守、总攻令、危急救护3AP。体力=技能表基准×（这把武器的基础体力÷这一类的基准体力），熟练越高越省。部分技能有冷却（按大回合算：冷却1轮的这轮用了，下一轮不能用、再下一轮可以），两个武器页和追击共用冷却。",
                            "敌人每次正常激活普通3AP、精英4AP，执行一个公开的主意图；攻击够不到你时先花1AP往前挪一位，再只做一次普通攻击，剩下的行动点不再加攻击。敌人的意图按自己的循环轮流出现（例如酸液感染者奇数次喷吐、偶数次近战）。",
                            $"追击：每轮开始时比一比你和对方最快的人谁更快——你的速度是对方的2倍，大约每两轮多一次追击；3倍以上每轮一次。追击排在本轮正常行动之后，固定{e.Config.CombatMath.PursuitAp}AP，每人每轮最多一次；跑得比你快的敌人同样会追击你。顺序条里写“追”的就是追击。",
                            $"一整轮（含追击）只推进{e.Config.BattleTime.RoundWorldMinutes}分钟世界时间。战斗中饱食、水分不会下降（腹泻也不扣水），战后也不补扣；清醒、精神照常变化，体力只由招式消耗。已经饿到0或渴到0的健康损失不会因此停下。",
                            "战斗中只有包扎和用药能用随身物品，不能吃喝；地面和随身物品可以查看。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "actions", Title = "指令一览", Icon = "fists",
                    Paragraphs = (e, s) => new[]
                    {
                        "战斗占上面两行：第一行是队伍，你在左、敌人在右；第二行是操作台，有当前武器、技能、通用指令、道具和战斗状态。",
                        "先在操作台点一个技能，能打到的敌人会亮起；把鼠标移到亮起的敌人上，右栏显示对它的实际命中率、伤害和消耗；点击亮起的敌人就执行这一招，不用再点别的按钮。没选技能时点敌人只是选中查看，不会攻击；场上只剩一个敌人时也不会自动攻击。右键或 Esc 取消瞄准，不耗行动点。",
                        "防御、调整呼吸、装填、包扎、用药、撤退、结束行动这类不用选敌人的指令，点选后在操作台旁边点“确认”才执行。",
                        "操作台左边是武器Ⅰ、武器Ⅱ两个页签：开战停在第一个有武器的页（只装了武器Ⅱ就从武器Ⅱ页开始），点另一页切换，0AP、不耗体力，只换这次能用的技能，已选的目标会取消；轮到敌人时也能切页查看。两把武器是战前选中随身或当前地面的武器卡点“装备到武器Ⅰ/Ⅱ”装好的（右栏人物“装备”区可卸下），战斗中不能换上第三把；空的武器页用拳。",
                        "下面只列当前武器页和熟练度能用的指令，切换武器页后会跟着变；未到等级的技能不列（在人物页熟练度里看），暂时用不了的（行动点不足、冷却中、弹仓空、没有药品等）仍然列出并写原因。我方现在只有你一人：指挥、医疗技能以你自己为目标，保护、掩护、整队、协同需要队友，列出但不能用。",
                    },
                    Figures = (e, s) =>
                    {
                        var weaponArt = e.WeaponOnPage(s, s.Battle?.ActiveWeaponSlot ?? e.DefaultWeaponPage(s), out _)?.ItemId ?? "fists";
                        return e.CompatibleCommands(s)
                            .Select(o => new HelpFigure(o.NodeId != null ? weaponArt : CmdIcon(o.Action), o.Label,
                                (o.Ap < 0 ? "用掉剩余AP" : $"{o.Ap}AP") + (o.CooldownRounds > 0 ? $" · 冷却{o.CooldownRounds}轮" : "") + (o.EffectText != null ? $" · {o.EffectText}" : "") + (o.Available ? "" : $" · {o.Reason}")));
                    },
                },
                new HelpEntry
                {
                    Id = "hit", Title = "命中、伤害与恐慌", Icon = "enemy_feral_dog",
                    Paragraphs = (e, s) =>
                    {
                        var m = e.Config.CombatMath;
                        return new[]
                        {
                            "命中能力不是命中率：实际命中率 = 你的命中能力 ÷（命中能力 + 对方闪避），预览会同时写两者。命中后还有机会暴击，暴击伤害×" + T(m.CritFactor) + "。",
                            $"你的命中能力 =（武器命中 + 3×熟练等级 + 基础{T(m.PlayerAccuracyAssistance)} + 装备命中 + 标记/鼓舞等加成）× 招式系数 × 疲劳 × 恐慌 × 失衡。疲劳：付完这次体力后体力低于{T(m.LowEnergyExclusive)}或清醒不高于{T(m.LowWakefulnessInclusive)}时×{T(m.FatigueAccuracyFactor)}，两项都低也只乘一次（低体力、低清醒另外各让速度−1）。敌人没有这项基础加成。",
                            $"伤害先乘熟练度、招式、处决、暴击和伤害增益，再减去对方护甲（教学词条的攻击力+0.1在乘完之后、扣甲之前加，同一目标每条指令只加一次）。伤害增益（装备与武器特性、总攻令、斧蓄力）同名取最强、不同来源相乘，合计超过×{T(m.DamageBuffKnee)}的部分逐渐打折，最多接近×{T(m.DamageBuffKnee + m.DamageBuffTail)}；护甲（忽略护甲的词条和武器特性先抵掉一部分）；对方“防御”时再×{T(m.GuardFactor)}。你防御时下一次受伤×{T(m.GuardFactor)}；防御时当前武器页是耐久至少1的盾才是盾类格挡×{T(m.ShieldGuardFactor)}（盾耐久−1），另一页的盾不算，格挡在防御那一刻锁定，之后切页不改。",
                            $"推挡、震击、盾击（失衡）、压制、致残射击（减速）、逼退、震退（击退）、打断、重锤（虚弱）、扯腿、扑击这类控制：先要打中，再判一次控制，成功率={Ui.Pct(m.ControlBase)}−对方控制抗性（最低{Ui.Pct(m.ControlMin)}、最高{Ui.Pct(m.ControlMax)}；敌人的控制抗性写在敌卡详情里）；成功后对方进入“稳态”到下一轮结束，期间再被控制的成功率低{T(m.SteadyResistBonus)}个百分点。你的基础控制抗性{T(e.Config.Scope.BasePlayerControlResist)}（敌人对你的失衡、减速成功率{Ui.Pct(m.ControlBase - e.Config.Scope.BasePlayerControlResist / 100)}）；装备的“位移抵抗”只防拉扯、击退，不同装备相加最多{T(e.Config.ActorBaseDefense.DisplacementGearAggregateCap)}，和“稳固”这类状态取较强的一个。破甲劈（护甲−0.25）、猎标（标记：我方对它命中能力+6）、火力压制（虚弱：伤害×0.8）打中就生效，持续2轮。出血在每轮开始时扣健康。",
                            "战斗开始时的恐慌由当时的精神决定，受到伤害会上升；恐慌60以上命中能力×0.9，90以上×0.8，战斗结束后清除，但会让精神下降。",
                            "丧尸的咬击命中并造成伤害时可能感染病毒，免疫越低越容易；抓击、喷吐、尖啸和人类、动物的攻击不会传染，但部分动物会造成出血。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "layout", Title = "站位与目标", Icon = "enemy_shambler",
                    Paragraphs = (e, s) => new[]
                    {
                        "四对四站位：双方各四个位置，1号位最靠中央、4号位最后；画面上我方从左到右是4/3/2/1，敌方是1/2/3/4。开战时敌人从1号位起排开，你按顶栏“编队”里保存的号位入场（新局在1号位；空闲时任何地点都能改，保存不花时间，不会按武器自动改位），噪声引来的增援站到最后一个敌人身后的空位（身后没有空位就站最前的空位）；之后各自保留真实号位：倒下、逃走、被拉动都不会让后面的人自动往前补，空位就是空位。本场最多来几个敌人由队伍容量C决定：新局C1，营区登记后C2，取得教会路线记录或学院安全记录之一后C3、两种都有后C4（只增不减）。敌踪比容量多时，只有排在最前的C只接战，其余留在原地，不算击杀、没有战利品；噪声引来的增援也算在本场C里。开战后容量锁定到战斗结束。" + $"（你现在：{e.CapacityText(s)}）",
                        "每件武器写着“使用位→目标位”：刀剑打敌方1、2号位，斧只打1号位，矛打1—3号位，弓、手枪、步枪打1—4号位；拳（普通拳击、勉强拳击、推挡）只打1号位，戴着护拳绑带（战斗饰品）时空槽徒手页的普通拳击可打1、2号位，拳10级“连拳”也打1、2号位。选好技能后够不到的敌人不亮起，悬停写明原因。使用位按你的真实号位判断（只剩你一人也一样）：例如弓要在2—4号位、刀要在1、2号位；不在使用位时先用“调整站位”（1AP、体力0.1，挪到相邻空位或和相邻同伴换位，不得经验）。矛10级前的“贯穿”从你点的敌人所在号位一直打到4号位，按离起点的格数伤害×0.65/0.50/0.35/0.25，空位不重排。",
                        "每种敌人有自己的“己位→目标位”（敌卡详情写明）：多数近战敌人要在1、2号位才能出手，站在后面时会先花1AP往前挪一格（前面空着就走过去，有同伴就和它换位），挪到位后再普通攻击（敌卡意图写“前移到2号位后抓击”），余下的AP不用；如果挡在前面的同伴本来就能打到你、它前面还有空位，就由那个同伴先花它自己本轮的1AP往前补一格，后面的再花1AP跟上（同伴本轮没AP了，后面的就原地等待，不会把同伴换下去）；你退到它够不到的后排、你前面又全是空位时，站在合法位置的敌人会花1AP“迫近”再花2AP做一次弱近袭（伤害×0.7，不咬、不附控制，你的号位不变）。敌人主动前移、补位、跟进和迫近都会触发你架好的矛（迎击），被打死或打断时后面的动作取消，没执行的AP不扣；酸液感染者、流窜枪手这类在3、4号位出手的敌人被挤到1、2号位时改用弱攻击×0.7。嚎叫、尖啸、防御、蓄力、保护、鸣响这类不需要够到你的动作照常执行。敌人打谁：每次单体攻击出手时，才在它目标位里站着的存活我方中等概率随机选一名（敌卡详情“目标”写范围与现在可选的人，不提前告诉你会打谁；选中谁写进战斗日志），连续两次攻击可能打同一人；扫击这类范围攻击打范围内全部人，迫近只打最前那一名。现在队友还不会跟你进战斗，所以目前总是打你。",
                        "有些敌人会“保护”身边的同伴：之后你对那个同伴的单体攻击由保护者接下（不拦群攻），一次为止。被拉扯（往前一格）、击退（往后一格）要先被打中、再判控制，成功才移动：目的格空着就移过去，有同伴就互换；已经在1号位再被拉、在4号位再被击退时不移动、也不判控制，其他伤害照算。挣脱可以在4号位原地解除减速，突进斩在1号位原地出手。",
                        "场上只剩一个敌人时会自动选中它查看，但不会自动攻击，仍要先选技能再点它。",
                    },
                },
                new HelpEntry
                {
                    Id = "retreat", Title = "撤退、胜利与掉落", Icon = "cmd_retreat",
                    Paragraphs = (e, s) => new[]
                    {
                        "撤退每轮只能尝试一次，成功率看双方速度，预览里写明；成功后留在原地，活着的敌人保留血量回到世界。失败则这一轮的行动点用尽。",
                        "全部敌人倒下或逃走即胜利，掉落物放在当前地面。打倒的动物会留下尸体，可以处理成肉和皮。",
                        "Esc 不能关闭战斗，也没有免费脱战。",
                    },
                },
            },
        },

        // ───────────── 饱饱与吞噬融合 ─────────────
        new()
        {
            Id = "baobao", Title = "饱饱与吞噬融合", Icon = "status_baobao", Summary = "饱饱已公开的身份与功能、吞噬融合流程",
            Entries =
            {
                new HelpEntry
                {
                    Id = "baobao", Title = "饱饱", Icon = "status_baobao",
                    Paragraphs = (e, s) =>
                    {
                        var bi = e.Config.Infection.BaobaoInput;
                        return new[]
                        {
                            "饱饱是你体内的一种特殊变异病毒，会在脑中跟你说话。它不知道自己叫什么，只知道自己饿了，“饱饱”是你给它起的名字。它会陪伴你、提醒你找吃的喝的，也会在战斗中提点你。",
                            "它不会替你吃喝、不会自动恢复，也不会替你选择攻击。",
                            $"饱饱会给身体带来缓慢的病毒压力：免疫越低压力越大，每小时最多{T(bi.MaxPerHour)}；免疫达到{T(bi.FullSuppressImmunityAtLeast)}及以上时完全压住。左栏的“饱饱”状态图标显示它此刻的影响。",
                        };
                    },
                },
                new HelpEntry
                {
                    Id = "fusion", Title = "吞噬融合", Icon = "active_substance_white_unidentified",
                    Paragraphs = (e, s) =>
                    {
                        var a = e.Config.ActiveSubstance;
                        var list = new List<string>
                        {
                            $"“活性物质”是一种道具卡，可以让饱饱吞噬融合。选中随身或当前地面的活性物质，点卡下的“吞噬融合”，耗时{a.Fusion.Minutes}分钟、每次1份；战斗中或行动进行中不能融合。",
                            $"丧尸被击败时有小概率（每只{Pct(a.NormalDrop.ChancePerEnemy)}）留下活性物质。普通的活性物质融合后目前不会带来属性变化。",
                        };
                        // 首战教学奖励属于剧情内容：玩家经历过才写出（不因说明目录提前泄露）
                        if (s.Traits.Count > 0)
                            list.Add("融合得到的词条会显示在人物页的“词条”里，可以点“详情”查看效果。");
                        return list;
                    },
                },
            },
        },
    };

    private static string CmdIcon(string id) => id switch
    {
        "basic_attack" or "desperate_punch" => "fists",
        "guard" => "cmd_guard",
        "breathe" => "cmd_breathe",
        "reload" => "cmd_reload",
        "retreat" => "cmd_retreat",
        "end_turn" => "cmd_end_turn",
        "switch_weapon" => "cmd_switch",
        "swap" => "cmd_switch",
        "bandage" => "bandage",
        "use_antibiotics" => "antibiotics",
        "use_antiviral_cure" => "antiviral_cure",
        _ => "placeholder",
    };

    public static HelpTopic? Topic(string? id) => Topics.FirstOrDefault(t => t.Id == id);

    public static HelpEntry? Entry(string? topic, string? entry) => Topic(topic)?.Entries.FirstOrDefault(x => x.Id == entry);

    /// <summary>属性/状态详情页“相关说明”要定位的条目（同源规则，S14 §13.2/§13.3）。</summary>
    public static (string Topic, string Entry)? ForAttr(string page) => page switch
    {
        "stat:health" => ("body", "stat-health"),
        "stat:satiety" => ("body", "stat-satiety"),
        "stat:hydration" => ("body", "stat-hydration"),
        "stat:energy" => ("body", "stat-energy"),
        "stat:wakefulness" => ("body", "stat-wakefulness"),
        "stat:morale" => ("body", "stat-morale"),
        "immunity" => ("body", "immunity"),
        "virus" => ("body", "virus"),
        "skills" => ("body", "skills"),
        "traits" => ("baobao", "fusion"),
        _ => null,
    };

    /// <summary>状态图标说明的“相关说明”条目。</summary>
    public static (string Topic, string Entry) ForStatus(string statusId) => statusId switch
    {
        "diarrhea" => ("body", "food"),
        "bleed" or "bandageCooldown" or "bandageVirusCooldown" or "antibiotic" or "antibioticCooldown" => ("body", "medical"),
        "infected" => ("body", "virus"),
        "baobao" => ("baobao", "baobao"),
        "panic" => ("combat", "hit"),
        "low_energy" => ("body", "stat-energy"),
        "low_morale" => ("body", "stat-morale"),
        "zero_satiety" => ("body", "stat-satiety"),
        "zero_hydration" => ("body", "stat-hydration"),
        "zero_energy" => ("body", "stat-energy"),
        "zero_wakefulness" => ("body", "stat-wakefulness"),
        _ => ("body", "statuses"),
    };
}

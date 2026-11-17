using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B35a1：设施卡的阶段（施工中／已建成）。只读判定集中在 <see cref="GameEngine.BuildStageHere"/>，
    /// 后续 B57（Q935 持续工地）替换施工中显示时只改这一处。
    /// </summary>
    public enum FacilityStage { Building, Built }

    /// <summary>
    /// B35a1（S14／S08 Q594、Q596、Q795、Q904、Q908）：一座真实设施工作区的身份——自建工坊按 世界种子＋地点＋区＋设施类别，
    /// 公共设施按 世界种子＋公共站配置 Id。只读，不进存档；名称取原真实设施名（公共站为卡名，如“营区工坊工作台”）。
    /// </summary>
    public sealed class CraftWorkspace
    {
        /// <summary>稳定身份键（分类记忆与重验都用它，不用列表序号或显示名）。</summary>
        public string Key { get; set; } = "";
        public string StationId { get; set; } = "";
        /// <summary>公共站配置 Id；自建工坊为 null。</summary>
        public string? PublicId { get; set; }
        public string Location { get; set; } = "";
        /// <summary>所在区（外部区为地点 Id，安全屋屋内为屋内地面）。</summary>
        public string Ground { get; set; } = "";
        public string Name { get; set; } = "";
        public bool Public => PublicId != null;
    }

    /// <summary>B35a1（Q795／Q796／Q909／Q936）：设施栏一张独立设施卡（遮蔽棚、床垫、每座自建工坊、每座公共设施）。</summary>
    public sealed class FacilityCardView
    {
        /// <summary>shelter、bed、station（自建工坊）、public（公共设施）。</summary>
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string StationId { get; set; } = "";
        public FacilityStage Stage { get; set; }
        /// <summary>状态行（工坊、公共设施；遮蔽棚、床垫由网页壳按原效果句写）。</summary>
        public string State { get; set; } = "";
        /// <summary>公共设施未开放的真实原因；开放为 null。</summary>
        public string? Blocked { get; set; }
        /// <summary>点击进入的真实工作区（遮蔽棚、床垫为 null）。</summary>
        public CraftWorkspace? Workspace { get; set; }
        /// <summary>本人正在这座设施主动制作（Q796；Q972 暂用 A：主行动没退出就算制作中）。</summary>
        public bool Crafting { get; set; }
    }

    /// <summary>B35a1（Q904／Q933／Q934）：拖拽候选的一条；需设施的配方带所去的真实设施工作区。</summary>
    public sealed class RecipeCandidate
    {
        public string RecipeId { get; set; } = "";
        /// <summary>该配方要进入的真实设施工作区；顶栏三类（徒手制造、设施建造、维护与修补）为 null。</summary>
        public CraftWorkspace? Workspace { get; set; }
    }

    /// <summary>
    /// B35a1「UI 重做：制作分流、独立设施卡、秘密配方、设施建造命名」的内核只读规则：
    /// <list type="bullet">
    /// <item>Q600（S08／S14 2026-11-14）：秘密配方显现判定 <see cref="RecipeVisible"/>，各目录／拖拽候选／来源／直接请求共用。</item>
    /// <item>Q594／Q596 分流与 Q797／Q908 分类：顶栏“手工制作”四类；设施工作区只列本座配方，按原配方 Group 分组。</item>
    /// <item>Q795／Q796／Q909／Q936：每座真实设施一张卡，状态“{设施名} · 制作中／空闲”（公共卡只写“制作中／空闲”）。</item>
    /// <item>Q904 B／Q933 A：材料／营地地点卡拖拽候选另列当前同地同区真实已有设施的相关配方。</item>
    /// <item>Q931 B：安全屋面板只在人物当前地点已建安全屋时可开（<see cref="SafehousePanelBlocked"/>）。</item>
    /// </list>
    /// 全部只读、0 分钟，不预留、不抽签、不写存档。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── Q600 秘密配方显现 ─────────────

        /// <summary>逐字稿 2026-11-14 Q600「直接请求未显现配方」。</summary>
        public const string RecipeNotFoundText = "尚未发现这项配方。";
        /// <summary>逐字稿 2026-11-14 Q600「当前分类无可见配方」。</summary>
        public const string NoVisibleRecipeText = "当前分类没有可显示的配方。";
        /// <summary>逐字稿 2026-11-14 Q600「系统说明正式段」（Q937：放在系统说明“手工制作”条目入口段之后）。</summary>
        public const string SecretRecipeHelp = "部分做法需要通过技能成长、蓝图或调查任务逐步了解。看见配方不等于已经掌握，也不免除原有设施、技能和材料条件。已学会的做法不会因这次显示调整丢失。";

        /// <summary>
        /// Q600：配方此刻能否显示。公开配方（无 <see cref="RecipeConfig.Reveal"/>）恒可；秘密配方要
        /// 已掌握对应知识（原 KnowsKnowledge，含初始与授予事实），或主角满足全部显现技能，或接取／完成任一指定任务，或完成任一指定完成任务。
        /// 空技能表／空任务表不算满足。只是从已有事实现算，不写发现旗标。
        /// </summary>
        public bool RecipeVisible(GameState s, RecipeConfig rc)
        {
            var rv = rc.Reveal;
            if (rv == null) return true;
            if (rc.Knowledge != null && KnowsKnowledge(s, rc.Knowledge)) return true;
            if (rv.SkillsAll.Count > 0 && rv.SkillsAll.All(kv => SkillLevel(s, kv.Key) >= kv.Value)) return true;
            if (rv.AnyAcceptedQuest.Any(q => QuestAccepted(s, q) || QuestDone(s, q))) return true;
            return rv.AnyCompletedQuest.Any(q => QuestDone(s, q));
        }

        public bool RecipeVisible(GameState s, string recipeId) => _recipes.TryGetValue(recipeId, out var rc) && RecipeVisible(s, rc);

        // ───────────── Q594／Q596：顶栏“手工制作”四类 ─────────────

        public const string TabAll = "all";
        public const string TabHand = "hand";
        public const string TabBuild = "build";
        public const string TabRepair = "repair";

        /// <summary>Q594 最新补充（S14 2026-11-13）与 Q805：顶栏分类“全部配方”“徒手制造”“设施建造”“维护与修补”。</summary>
        public static readonly IReadOnlyList<(string Id, string Name)> HandCraftTabs = new[]
        {
            (TabAll, "全部配方"), (TabHand, "徒手制造"), (TabBuild, "设施建造"), (TabRepair, "维护与修补"),
        };

        /// <summary>Q594：首次顶栏默认“徒手制造”；Q797：记忆失效回原合法默认。</summary>
        public const string HandCraftDefaultTab = TabHand;
        /// <summary>Q797／Q908：设施工作区首次／记忆失效默认“全部配方”。</summary>
        public const string WorkspaceDefaultTab = TabAll;

        /// <summary>
        /// 顶栏分类里的哪一类：建造配方→设施建造；原无设施需求的维护／修补→维护与修补；原无设施需求的其他制作→徒手制造。
        /// 需设施（工坊、锅、晾架）的配方与拆解不在顶栏，返回 null。
        /// </summary>
        public string? HandCraftTabOf(RecipeConfig rc)
        {
            if (rc.IsSalvage || rc.IsPot || rc.IsDrying) return null;
            if (rc.IsBuild) return TabBuild;
            if (rc.Station != "hand") return null;
            return rc.IsRepair ? TabRepair : TabHand;
        }

        /// <summary>按原稳定顺序排：可开工（无门槛原因）在前，同档保持配置顺序（沿原制作列表）。</summary>
        private List<RecipeConfig> SortForList(GameState s, IEnumerable<RecipeConfig> list) =>
            list.OrderBy(r => RecipeLocks(s, r).Count > 0 ? 1 : 0).ToList();

        /// <summary>顶栏某分类的可显示配方（“全部配方”为三类合计，不含需设施的配方）。</summary>
        public List<RecipeConfig> HandCraftRecipes(GameState s, string tab) =>
            SortForList(s, Config.Recipes.Where(r => HandCraftTabOf(r) is string t && (tab == TabAll || t == tab) && RecipeVisible(s, r)));

        // ───────────── Q594／Q797：设施工作区 ─────────────

        /// <summary>Q797：复用原配方分组名（equipment“装备”、material“材料”）；其他无原名的分组不新造，只留在“全部配方”。</summary>
        public static string? RecipeGroupName(string? group) => group switch
        {
            "equipment" => "装备",
            "material" => "材料",
            _ => null,
        };

        /// <summary>需要固定工坊设施（工作台、锻炉……陶窑、固定净水台）的配方；锅食谱、晾制、建造、拆解与手工配方不是。</summary>
        public bool IsWorkspaceRecipe(RecipeConfig rc) =>
            !rc.IsSalvage && !rc.IsBuild && !rc.IsPot && !rc.IsDrying && rc.Station != "hand" && rc.Station != DryingStationId
            && Config.Workshops.Stations.Any(x => x.Id == rc.Station);

        /// <summary>某类设施工作区的可显示配方（Q594：只列本设施真实配方）；tab 为“全部配方”或原分组 Id。</summary>
        public List<RecipeConfig> WorkspaceRecipes(GameState s, string stationId, string tab) =>
            SortForList(s, Config.Recipes.Where(r => IsWorkspaceRecipe(r) && r.Station == stationId && RecipeVisible(s, r)
                                                     && (tab == TabAll || r.Group == tab)));

        /// <summary>Q797：工作区左列分类——“全部配方”＋本座可显示配方里非空的原分组（按原配置首次出现顺序）。</summary>
        public List<(string Id, string Name)> WorkspaceTabs(GameState s, string stationId)
        {
            var tabs = new List<(string, string)> { (TabAll, "全部配方") };
            foreach (var r in Config.Recipes.Where(r => IsWorkspaceRecipe(r) && r.Station == stationId && RecipeVisible(s, r)))
                if (RecipeGroupName(r.Group) is string name && tabs.All(t => t.Item1 != r.Group)) tabs.Add((r.Group!, name));
            return tabs;
        }

        /// <summary>
        /// 当前地点当前区真实存在的设施工作区：已建成的自建工坊（晾制架另有面板，不在这里）与外部区的公共设施（开放与否都在，未开放只浏览）。
        /// 按原工坊设施顺序，公共站按配置顺序。
        /// </summary>
        public List<CraftWorkspace> WorkspacesHere(GameState s)
        {
            var list = new List<CraftWorkspace>();
            var loc = s.CurrentLocation;
            var ground = s.Ground;
            foreach (var st in Config.Workshops.Stations)
            {
                if (st.Id == "hand" || st.Id == DryingStationId || st.BuildRecipe == null || !WorkshopBuilt(s, ground, st.Id)) continue;
                list.Add(new CraftWorkspace { Key = $"ws|{s.Seed}|{loc}|{ground}|{st.Id}", StationId = st.Id, Location = loc, Ground = ground, Name = StationName(st.Id) });
            }
            if (s.CurrentZone == ExteriorZone)
                foreach (var p in PublicStationsHere(s))
                    list.Add(new CraftWorkspace { Key = $"pub|{s.Seed}|{p.Id}", StationId = p.StationId, PublicId = p.Id, Location = loc, Ground = ground, Name = p.Name });
            return list;
        }

        /// <summary>按稳定身份重验：这座设施此刻仍在当前地点当前区真实存在才返回（返回／恢复／开工前都用它，不靠缓存）。</summary>
        public CraftWorkspace? WorkspaceHere(GameState s, string key) => WorkspacesHere(s).FirstOrDefault(w => w.Key == key);

        /// <summary>当前区某类配方所在的真实设施工作区（自建优先）；没有为 null。</summary>
        public CraftWorkspace? WorkspaceForStationHere(GameState s, string stationId) => WorkspacesHere(s).FirstOrDefault(w => w.StationId == stationId);

        /// <summary>Q594 逐字稿：旧配方链接需要尚未存在的工位。</summary>
        public static string NeedStationText(string stationName) => $"需要在{stationName}内查看和操作此配方。";
        /// <summary>Q594 逐字稿：工作区绑定的设施失效。</summary>
        public const string WorkspaceGoneText = "该设施已不可用，请重新选择。";

        // ───────────── Q795／Q796／Q909／Q936：独立设施卡 ─────────────

        /// <summary>Q796 状态词。</summary>
        public const string CraftingText = "制作中";
        public const string IdleText = "空闲";
        /// <summary>Q936：公共设施卡角标与悬停。</summary>
        public const string PublicBadge = "公共";
        public const string PublicTooltip = "公共设施。";
        /// <summary>Q935 合同 stageLabels.built（遮蔽棚／床垫原有角标同字）。</summary>
        public const string BuiltBadge = "已建成";

        /// <summary>
        /// 建造类设施在当前区的阶段：已建成；或本人主行动（含暂存的在办工作）正在建它＝施工中；都不是为 null（不出占位卡）。
        /// B57 持续工地把“施工中”换成工地状态时只改这里。
        /// </summary>
        public FacilityStage? BuildStageHere(GameState s, RecipeConfig rc)
        {
            if (FacilityBuiltHere(s, rc)) return FacilityStage.Built;
            if (CardStates.BusyBuild(s, rc.Id)) return FacilityStage.Building;
            return null;
        }

        /// <summary>Q796／Q909：本人正在这座真实设施主动制作（主行动或暂存的在办工作；暂停未退出仍算，Q972 暂用 A）。同类另一座不算。</summary>
        public bool CraftingAt(GameState s, CraftWorkspace w) =>
            s.Ground == w.Ground && s.MainWorks().Any(a => a.Type == ActionType.Craft && a.SourceLocation == w.Location
                && _recipes.TryGetValue(a.TargetId, out var rc) && rc.Station == w.StationId);

        /// <summary>
        /// Q795 B：当前区设施栏的独立设施卡（火堆、安全屋、晾制架、遮雨布另有各自卡）：
        /// 遮蔽棚、床垫（营地外部区，已建成或施工中才出卡）；每座已建自建工坊；外部区每座公共设施。未建设施不出占位卡（Q594）。
        /// </summary>
        public List<FacilityCardView> FacilityCardsHere(GameState s)
        {
            var list = new List<FacilityCardView>();
            var exterior = s.CurrentZone == ExteriorZone;
            if (exterior && _locs[s.CurrentLocation].CanBuildShelter)
                foreach (var id in new[] { "shelter", "bed" })
                    if (_recipes.TryGetValue(id, out var rc) && BuildStageHere(s, rc) is FacilityStage st)
                        list.Add(new FacilityCardView { Kind = id, StationId = id, Name = rc.Name, Stage = st });
            foreach (var w in WorkspacesHere(s))
            {
                var crafting = CraftingAt(s, w);
                if (w.Public)
                {
                    var why = PublicStationBlocked(s, Config.City.PublicStations.First(p => p.Id == w.PublicId));
                    list.Add(new FacilityCardView
                    {
                        Kind = "public", Name = w.Name, StationId = w.StationId, Stage = FacilityStage.Built, Workspace = w, Blocked = why, Crafting = crafting,
                        // Q936：状态只写“制作中／空闲”；未开放保留原真实原因
                        State = why ?? (crafting ? CraftingText : IdleText),
                    });
                    continue;
                }
                list.Add(new FacilityCardView
                {
                    Kind = "station", Name = w.Name, StationId = w.StationId, Stage = FacilityStage.Built, Workspace = w, Crafting = crafting,
                    // Q727：陶窑、固定净水台沿被动批次汇总句；其余手动工坊 Q796“{设施名} · 制作中／空闲”
                    State = IsPassiveBatchStation(w.StationId) ? StationSummary(s, w.Ground, w.StationId) : $"{w.Name} · {(crafting ? CraftingText : IdleText)}",
                });
            }
            return list;
        }

        /// <summary>陶窑、固定净水台：按投料批数被动加工的设施（卡面写 Q727 汇总句）。</summary>
        private static bool IsPassiveBatchStation(string stationId) => stationId == "kiln" || stationId == "water_station";

        // ───────────── Q904／Q933：拖拽候选 ─────────────

        /// <summary>
        /// 材料拖拽的配方候选（配置顺序）：campTarget=false 为材料拖到另一材料卡——原手工制作／设施建造／维护与修补里同时用到这些物品的配方；
        /// campTarget=true 为材料拖到营地地点卡——原设施建造（遮蔽棚、床垫、工坊，不含安全屋三项与野外火堆），不另加徒手制造（Q933①）。
        /// 两者都另列当前同地同区真实已有设施（自建已建成或公共设施，未开放的也列、只能浏览，Q933③）的相关配方，带所去工作区（Q904 B）。
        /// 锅食谱与晾制不列（Q933②）；都只列已掌握且可显示的配方（Q600）。
        /// </summary>
        public List<RecipeCandidate> DropRecipeCandidates(GameState s, IReadOnlyCollection<string> itemIds, bool campTarget)
        {
            var res = new List<RecipeCandidate>();
            foreach (var r in Config.Recipes)
            {
                if (!itemIds.All(r.Inputs.ContainsKey) || r.Group == "safehouse" || !RecipeKnown(s, r) || !RecipeVisible(s, r)) continue;
                if (IsWorkspaceRecipe(r))
                {
                    if (WorkspaceForStationHere(s, r.Station) is CraftWorkspace w) res.Add(new RecipeCandidate { RecipeId = r.Id, Workspace = w });
                    continue;
                }
                var tab = HandCraftTabOf(r);
                if (tab == null) continue;
                if (campTarget && (tab != TabBuild || r.FacilityResult == null || r.Group == WildBuildGroup)) continue;
                res.Add(new RecipeCandidate { RecipeId = r.Id });
            }
            return res;
        }

        // ───────────── Q931：安全屋面板入口 ─────────────

        /// <summary>
        /// Q931 B：安全屋面板只在人物当前地点确有已建安全屋时可开（屋外、屋内都行）；否则返回内部原因（不显示给玩家、不新增教学文案）。
        /// 所有打开／恢复路径共用。
        /// </summary>
        public string? SafehousePanelBlocked(GameState s) =>
            SafehouseBuilt(s, s.CurrentLocation) ? null : "not_local_built_safehouse";

        /// <summary>
        /// 开发入口「B35a1 设施试用组」：只造试玩起点——在能建工坊的当前区直接记工作台、锻炉、缝纫台已建成，
        /// 当前地面放废料3、铁矿石3、木炭2、木材4（拖拽候选、独立设施卡、工作区分组与“制作中／空闲”用）。不开工、不推进时间。
        /// </summary>
        public string DebugCraftScopeKit(GameState s)
        {
            var fac = FacOf(s, s.Ground);
            if (fac == null || !CanBuildWorkshopsHere(s)) return "开发：这里不能建工坊（到临时营地或已建成安全屋屋内再试）。";
            foreach (var id in new[] { "workbench", "forge", "sewing" })
                if (!fac.Workshops.Contains(id)) fac.Workshops.Add(id);
            foreach (var (id, n) in new[] { ("scrap", 3), ("iron_ore", 3), ("charcoal", 2), ("wood", 4) }) CreateItems(s, id, n, s.Ground);
            var text = $"开发：{ZoneName(s, s.Ground)}记工作台、锻炉、缝纫台已建成；地面放废料3、铁矿石3、木炭2、木材4";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
        }
    }
}

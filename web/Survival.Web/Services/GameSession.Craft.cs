using Survival.Core.Config;
using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Web.Services;

/// <summary>
/// B35a1「UI 重做：制作分流、独立设施卡」（S14／S08 Q594、Q596、Q795、Q797、Q904、Q907、Q908、Q930、Q931、Q933、Q934）：
/// 制作面板（一级面板 "craft"）有两种上下文——顶栏“手工制作”（<see cref="CraftSite"/> 为 null，四类分流：全部配方／徒手制造／设施建造／维护与修补），
/// 和某座真实设施的工作区（只列本座配方，左列按原配方分组）。分类按上下文分别存在浏览器本地（<see cref="CraftTabPrefs"/>，不进存档）。
/// 材料拖拽候选里的设施配方点了进该设施工作区，叠在候选上：返回／ESC 先回保留的候选列表（重验后），再按一次才关（Q934）。
/// 全部只是界面状态：不扣物、不预留、不推进时间。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>当前制作面板所在的真实设施工作区；null 为顶栏“手工制作”。</summary>
    public CraftWorkspace? CraftSite { get; private set; }

    /// <summary>下一次新开制作面板要进入的工作区（OpenWorkspace 先设，SwitchPanel 新开时取用一次）。</summary>
    private CraftWorkspace? _pendingSite;

    /// <summary>从拖拽候选进入设施工作区前保留的候选上下文（Q934）；没有为 null。</summary>
    private CandidateReturn? _candidateReturn;

    private sealed record CandidateReturn(List<RecipeCandidate> Candidates, string? Recipe, int? Pot);

    /// <summary>本次工作区是从拖拽候选进入的（返回／ESC 先回候选）。</summary>
    public bool CraftFromCandidates => _candidateReturn != null && Panel == "craft" && CraftSite != null;

    private CraftTabPrefs? _craftTabs;

    private CraftTabPrefs CraftTabs
    {
        get
        {
            if (_craftTabs != null) return _craftTabs;
            string? raw = null;
            try { raw = _store.GetPref("craftTabs"); } catch { /* 本地存储不可用：只在内存里记 */ }
            return _craftTabs = CraftTabPrefs.Parse(raw);
        }
    }

    /// <summary>当前上下文的分类记忆键：顶栏为 top，工作区为该设施稳定身份。</summary>
    private string CraftTabKey => CraftSite?.Key ?? CraftTabPrefs.TopKey;

    /// <summary>当前上下文的分类（顶栏四类，或工作区“全部配方”＋原分组）。</summary>
    public List<(string Id, string Name)> CraftTabList() =>
        State == null ? new() : CraftSite is { } w ? E.WorkspaceTabs(S, w.StationId) : GameEngine.HandCraftTabs.ToList();

    /// <summary>当前上下文某分类的可显示配方（已排好序）。</summary>
    public List<RecipeConfig> CraftTabRecipes(string tab) =>
        State == null ? new() : CraftSite is { } w ? E.WorkspaceRecipes(S, w.StationId, tab) : E.HandCraftRecipes(S, tab);

    /// <summary>当前上下文的默认分类（Q594 顶栏徒手制造；Q797／Q908 工作区全部配方）。</summary>
    private string CraftDefaultTab => CraftSite != null ? GameEngine.WorkspaceDefaultTab : GameEngine.HandCraftDefaultTab;

    /// <summary>按本机记忆恢复当前上下文的合法分类；失效或没有记录回默认（不写回）。</summary>
    private string RememberedCraftTab() => CraftTabs.Resolve(CraftTabKey, CraftTabList().Select(t => t.Id), CraftDefaultTab);

    /// <summary>配方在当前上下文里能列出（顶栏三类，或本座设施配方）且此刻可显示。</summary>
    public bool InCraftScope(RecipeConfig rc) =>
        State != null && E.RecipeVisible(S, rc) && (CraftSite is { } w ? E.IsWorkspaceRecipe(rc) && rc.Station == w.StationId : E.HandCraftTabOf(rc) != null);

    /// <summary>当前上下文里含这条配方的分类（优先记忆里的分类，否则第一个非“全部配方”的分类，再否则全部配方）。</summary>
    private string TabContaining(RecipeConfig rc, string preferred)
    {
        if (CraftTabRecipes(preferred).Any(r => r.Id == rc.Id)) return preferred;
        foreach (var (id, _) in CraftTabList())
            if (id != GameEngine.TabAll && CraftTabRecipes(id).Any(r => r.Id == rc.Id)) return id;
        return GameEngine.TabAll;
    }

    /// <summary>
    /// 新开制作面板时按上下文初始化分类与所选配方：主行动正在做本上下文的配方就选它，否则调用方指定的配方；
    /// 都不在本上下文时按记忆分类选第一条。指定了需设施的配方却从顶栏打开时提示“需要在{设施名}内查看和操作此配方。”（Q594）。
    /// </summary>
    private void InitCraftContext(string? recipe)
    {
        CraftSite = _pendingSite;
        _pendingSite = null;
        _candidateReturn = null;
        var tab = RememberedCraftTab();
        RecipeConfig? pick = null;
        var main = S.Main;
        if (main != null && main.Type is ActionType.Craft or ActionType.Build && E.Config.Recipes.FirstOrDefault(r => r.Id == main.TargetId) is { } mr && InCraftScope(mr))
            pick = mr;
        else if (recipe != null && E.Config.Recipes.FirstOrDefault(r => r.Id == recipe) is { } rr)
        {
            if (InCraftScope(rr)) pick = rr;
            else if (CraftSite == null && E.IsWorkspaceRecipe(rr) && E.RecipeVisible(S, rr)) Notice = GameEngine.NeedStationText(E.StationName(rr.Station));
        }
        if (pick != null) tab = TabContaining(pick, tab);
        CraftTab = tab;
        CraftRecipe = pick?.Id ?? CraftTabRecipes(tab).FirstOrDefault()?.Id;
    }

    /// <summary>打开顶栏“手工制作”（可预选一条顶栏配方）。</summary>
    public void OpenHandCraft(string? recipe = null)
    {
        _pendingSite = null;
        OpenPanel("craft", recipe);
    }

    /// <summary>
    /// 从独立设施卡进入该座真实设施的工作区（Q795 B；公共设施未开放也可进入浏览，开工按原锁定原因禁用）。
    /// 进入前按稳定身份重验仍在当前区。
    /// </summary>
    public void OpenWorkspace(CraftWorkspace w, string? recipe = null)
    {
        if (State == null || E.WorkspaceHere(S, w.Key) is not { } real) { Notice = GameEngine.WorkspaceGoneText; Notify(); return; }
        _pendingSite = real;
        OpenPanel("craft", recipe);
        _pendingSite = null;
    }

    /// <summary>玩家明确点一个分类：切换并写入本上下文的本机记忆，选该分类第一条（不自动开工）。</summary>
    public void PickCraftTab(string tab)
    {
        if (State == null || CraftTabList().All(t => t.Id != tab)) return;
        CraftTab = tab;
        CraftTabs.Set(CraftTabKey, tab);
        try { _store.SetPref("craftTabs", CraftTabs.Serialize()); } catch { /* 本地存储不可用时只在本次有效 */ }
        var first = CraftTabRecipes(tab).FirstOrDefault();
        SelectCraftRecipe(first?.Id);
    }

    /// <summary>选中一条配方（换配方时份数回 1、清指定目标／锅）。</summary>
    public void SelectCraftRecipe(string? id)
    {
        PeekClear();
        // B51c2b 试玩遗留 N1：点配方行回到这条配方的右栏预估——清掉工作区里批内物品卡的选中与数量窗
        DropFacilityItemSel();
        if (CraftRecipe != id)
        {
            CraftBatch = 1;
            CraftTarget = null;
            CraftPot = null;
        }
        CraftRecipe = id;
        if (id != null) Guide("recipe", id);
        Notify();
    }

    /// <summary>
    /// 候选列表里点一条：顶栏三类的配方就在本窗口选中；设施配方进该真实设施工作区（Q904 B），候选上下文保留在下面（Q934）。
    /// 设施已不在时提示“该设施已不可用，请重新选择。”并留在候选。
    /// </summary>
    public void PickCandidate(RecipeCandidate c)
    {
        if (State == null || CraftCandidates == null) return;
        if (c.Workspace == null) { SelectCraftRecipe(c.RecipeId); return; }
        if (E.WorkspaceHere(S, c.Workspace.Key) is not { } real) { Notice = GameEngine.WorkspaceGoneText; Notify(); return; }
        PeekClear();
        _candidateReturn = new CandidateReturn(CraftCandidates, CraftRecipe, CraftPot);
        CraftCandidates = null;
        CraftSite = real;
        CraftBatch = 1;
        CraftTarget = null;
        CraftPot = null;
        CraftPatch = null;
        var rc = E.Recipe(c.RecipeId);
        CraftTab = TabContaining(rc, RememberedCraftTab());
        CraftRecipe = rc.Id;
        Guide("recipe", rc.Id);
        Notify();
    }

    /// <summary>
    /// Q934：从候选进入的工作区返回——回保留的候选列表，按当前真实设施、知识与显现重验（失效的条目去掉；一条都不剩就关闭面板），
    /// 不重新投料、不扣物、不推进时间。
    /// </summary>
    private void ReturnToCandidates()
    {
        var back = _candidateReturn!;
        _candidateReturn = null;
        CraftSite = null;
        var keep = back.Candidates.Where(CandidateStillValid).ToList();
        if (keep.Count == 0) { CraftCandidates = null; OpenPanel(null); return; }
        if (keep.Count < back.Candidates.Count) Notice = GameEngine.ContentChangedText;
        CraftCandidates = keep;
        CraftBatch = 1;
        CraftTarget = null;
        CraftPatch = null;
        CraftPot = back.Pot is int p && E.FindItem(S, p) != null ? p : null;
        CraftRecipe = back.Recipe != null && keep.Any(k => k.RecipeId == back.Recipe && k.Workspace == null) ? back.Recipe
            : keep.FirstOrDefault(k => k.Workspace == null)?.RecipeId;
        Notify();
    }

    /// <summary>候选条目此刻仍合法：配方已掌握且可显示；设施配方的那座设施仍在当前区。</summary>
    private bool CandidateStillValid(RecipeCandidate c) =>
        E.Config.Recipes.FirstOrDefault(r => r.Id == c.RecipeId) is { } rc && E.RecipeKnown(S, rc) && E.RecipeVisible(S, rc)
        && (c.Workspace == null || E.WorkspaceHere(S, c.Workspace.Key) != null);

    /// <summary>
    /// Q970 A 定稿（R305）：候选表头“显示全部”——退出候选，进入顶栏“手工制作”的“全部配方”（不含设施配方）；不写分类记忆。
    /// </summary>
    public void CandidatesShowAll()
    {
        if (State == null) return;
        CraftCandidates = null;
        _candidateReturn = null;
        CraftSite = null;
        CraftTab = GameEngine.TabAll;
        if (CraftRecipe == null || E.Config.Recipes.FirstOrDefault(r => r.Id == CraftRecipe) is not { } rc || !InCraftScope(rc))
            CraftRecipe = CraftTabRecipes(GameEngine.TabAll).FirstOrDefault()?.Id;
        Notify();
    }

    /// <summary>
    /// 每次刷新前：制作面板绑定的工作区已不在当前区（设施失效、换区）就关面板并提示“该设施已不可用，请重新选择。”（Q594）；
    /// 安全屋面板在人物离开已建安全屋所在地时关闭，不留悬空页（Q931）。
    /// </summary>
    private void ReconcileCraftAndSafehouse()
    {
        if (State == null) return;
        if (Panel == "craft" && CraftSite is { } w && E.WorkspaceHere(S, w.Key) == null)
        {
            Panel = null;
            CraftSite = null;
            _candidateReturn = null;
            ClearPanelChain();
            Notice = GameEngine.WorkspaceGoneText;
        }
        if (Panel == "safehouse" && E.SafehousePanelBlocked(S) != null)
        {
            Panel = null;
            ClearPanelChain();
        }
    }
}

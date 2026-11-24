using Survival.Core.Config;
using Survival.Core.Engine;

namespace Survival.Web.Services;

/// <summary>
/// B67 R353「已知工艺」的入口（S08 D28 与 2026-11-24 Q1040／Q1041、S14 D28-01、Q1041-01；02 Q1041 A）：
/// 点一步工序打开该配方的原详情——手工制作入口的配方（徒手制造、设施建造、维护与修补）走原「手工制作」入口并选中它；
/// 设施工作区配方在当前区有可合法使用的真实设施时走原工作区入口并选中它；找不到合法工位时不跳转，留在物品详情里只读显示原门槛说明。
/// 进入只是浏览：不移动、不改选中的实物，不预填、不预留、不开工，不推进时间。
/// </summary>
public sealed partial class GameSession
{
    /// <summary>该工作区配方此刻在当前区可合法使用的真实设施工作区（原门槛 StationBlocked 无原因且设施在本区）；手工制作配方或不可用为 null。</summary>
    public CraftWorkspace? ChainWorkspace(RecipeConfig rc) =>
        State != null && E.IsWorkspaceRecipe(rc) && E.StationBlocked(S, rc) == null ? E.WorkspaceForStationHere(S, rc.Station) : null;

    /// <summary>
    /// 点工序：按当前真实状态重验后进原入口，进了返回 true；该配方已不公开、或实体工位不可用（以及战斗、对白锁等原规则不让开面板）返回 false。
    /// </summary>
    public bool OpenChainRecipe(string recipeId)
    {
        if (State == null || E.Config.Recipes.FirstOrDefault(r => r.Id == recipeId) is not { } rc || !E.InCraftChainScope(S, rc)) return false;
        if (E.HandCraftTabOf(rc) != null) OpenHandCraft(rc.Id);
        else if (ChainWorkspace(rc) is { } w) OpenWorkspace(w, rc.Id);
        else return false;
        return Panel == "craft" && CraftRecipe == rc.Id;
    }

    /// <summary>
    /// 只读详情里的原门槛说明（Q1041 A“沿原门槛说明”）：配方原门槛原因（知识、熟练、「需要{设施名}…」）；都没有时用 Q594 原句「需要在{设施名}内查看和操作此配方。」。
    /// </summary>
    public List<string> ChainReadOnlyReasons(RecipeConfig rc)
    {
        if (State == null) return new();
        var list = E.RecipeLocks(S, rc);
        if (list.Count == 0) list.Add(GameEngine.NeedStationText(E.StationName(rc.Station)));
        return list;
    }
}

using Survival.Core.Engine;
using Survival.Core.State;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 B45b「加工菜单补齐与禁用来源」（R208、R209）。规则出处：S14「2026-11-13 B45 Q581—Q590试玩答复与下方对白布局」
/// Q581、Q582、Q583、Q584、Q585（首段）、Q587、Q588 及本批验收；正式句见《重墨 V2 开工裁定与界面逐字稿》同名节。
/// 网页壳的接线以源码检查（同 B45FacMenuShellTests）。
/// </summary>
public class B45bFacMenuTests : TestBase
{
    // ───────────── Q581 主层补齐火源快捷操作 ─────────────

    /// <summary>
    /// Q581-A01：加工菜单主层与火堆卡面挂同一个快捷入口组件（不复制一份）——按钮原句、可用条件、原因、点火盘、耗时与结算同源；
    /// 点火盘锚在发起它的按钮；耗时行动成功开始才收起菜单（BeginAction → CloseFireMenuForAction），未成功留在原层。
    /// </summary>
    [Test]
    public void Q581_A01_MenuMainLayerSharesCardQuickActions()
    {
        var card = Web("Components", "FireCard.razor");
        var menu = Web("Components", "FireMenu.razor");
        var quick = Web("Components", "FireQuickActions.razor");
        Assert.That(card, Does.Contain("<FireQuickActions />"), "卡面入口保留");
        Assert.That(menu, Does.Contain("<FireQuickActions InMenu=\"true\" />"), "主层同一组件");
        foreach (var label in new[] { "<strong>投入木材</strong>", "<strong>熄灭</strong>", "<strong>点燃</strong>", "<strong>等待加工</strong>" })
            Assert.That(quick, Does.Contain(label));
        Assert.That(quick, Does.Contain("G.ToggleIgniteMenu(igniteId)"), "点燃只打开原点火盘");
        Assert.That(quick, Does.Contain("await G.StartAction(ActionRequest.WaitCook());"));
        Assert.That(quick, Does.Contain("G.FireWoodWhy(out var wood)"));
        Assert.That(quick, Does.Contain("G.CookWaitPreview()"));
        Assert.That(card, Does.Not.Contain("ActionRequest.WaitCook()"), "卡面不再自带一份");
        Assert.That(Web("Components", "IgniteMenu.razor"), Does.Contain("AnchorId=\"@G.IgniteAnchor\""));
        var session = Web("Services", "GameSession.cs");
        Assert.That(session, Does.Contain("IgniteAnchorMenu = \"fm-ignite\""));
        // 成功开始才收起：BeginAction 里 res.Ok 之后
        var begin = session.IndexOf("private ActionResult BeginAction(", StringComparison.Ordinal);
        var fail = session.IndexOf("if (!res.Ok)", begin, StringComparison.Ordinal);
        var close = session.IndexOf("CloseFireMenuForAction();", begin, StringComparison.Ordinal);
        Assert.That(fail, Is.GreaterThan(0));
        Assert.That(close, Is.GreaterThan(fail), "开工失败先返回，不收菜单");
    }

    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "web", "Survival.sln"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找到仓库根目录");
        return dir!.FullName;
    }

    private static string Web(params string[] parts) => File.ReadAllText(Path.Combine(new[] { Root(), "web", "Survival.Web" }.Concat(parts).ToArray()));
}

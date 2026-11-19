using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Survival.Core.Engine;
using Survival.Core.State;
using Survival.Web.Services;

namespace Survival.Web.Tests;

/// <summary>
/// 网页层测试的起点（2026-11-19 bUnit 样板，试点文档 §10）：每条测试一个 bUnit 上下文，注入与 Program.cs 相同的三项服务，
/// 浏览器脚本（存档、浮窗定位等）一律走 bUnit 的宽松 JS 模拟（不报错、返回默认值）；GameSession 读内嵌配置、开一局新游戏。
/// 起点局面用内核 CreateItems／Debug* 造（只造起点，不直接设结果）；之后的操作都走组件里真实的按钮、输入框和会话方法。
/// </summary>
public abstract class WebTestBase
{
    protected Bunit.TestContext Ctx = null!;
    protected GameSession G = null!;
    protected GameEngine E => G.E;
    protected GameState S => G.S;

    [SetUp]
    public async Task StartGame()
    {
        Ctx = new Bunit.TestContext();
        Ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        Ctx.Services.AddSingleton(new HttpClient());
        Ctx.Services.AddScoped<SaveStore>();
        Ctx.Services.AddScoped<BrowserSaveStorage>();
        Ctx.Services.AddScoped<GameSession>();
        G = Ctx.Services.GetRequiredService<GameSession>();
        await G.InitAsync();
        Assert.That(G.LoadError, Is.Null, "内嵌配置载入失败");
        G.NewGame();
        // 同 Survival.Core.Tests 的 New()：去掉开场剧情（否则开局对白锁住界面），不让敌踪、巡逻在测试中途插进来
        E.DebugLegacyStory(S);
        S.SuppressEnemyApproach = true;
        S.SpawnNextCheck = int.MaxValue;
        S.PatrolNextCheck = int.MaxValue;
        G.Notify();
        Assert.That(G.DialogueLocked, Is.False, "起点不应有对白");
    }

    [TearDown]
    public void Dispose() => Ctx.Dispose();
}

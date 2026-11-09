using Survival.Core.Save;

namespace Survival.Core.Tests;

/// <summary>
/// 开发计划 v0.9 批次 B27d（R109 Q282）。规则来源：S01「2026-11-09 Q281–Q282 右栏切换与存档提示」Q282：
/// 按浏览器存储异常类别识别配额用尽，底栏整句「存档失败：浏览器存储空间已满。」，其他错误走原“保存失败：”分支。
/// 测试工程不引用网页壳，接线处读组件源码确认。
/// </summary>
public class B27dQ282Tests
{
    private static string Web(string relative)
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return File.ReadAllText(Path.Combine(dir!.FullName, "web", "Survival.Web", relative));
    }

    // ───────────── Q282 浏览器存储配额用尽 ─────────────

    [TestCase("QuotaExceededError", "Failed to execute 'setItem' on 'Storage': Setting the value of 'survival.save.v1.tmp' exceeded the quota.")]
    [TestCase("NS_ERROR_DOM_QUOTA_REACHED", "Persistent storage maximum size reached")]
    [TestCase("QUOTA_EXCEEDED_ERR", "")]
    public void Q282_QuotaCategory_Recognized(string category, string message)
    {
        Assert.That(SaveWriteError.IsQuotaExceeded(category, message), Is.True);
        Assert.That(SaveWriteError.PlayerText(true, message), Is.EqualTo("存档失败：浏览器存储空间已满。"), "整句，不拼“保存失败：”");
    }

    [Test]
    public void Q282_JsExceptionMessage_CategoryNameRecognized()
    {
        // JSInterop 直接抛出（没有单独回传类别）时，在异常信息里找类别名
        Assert.That(SaveWriteError.IsQuotaExceeded(null, "QuotaExceededError: Failed to execute 'setItem' on 'Storage'"), Is.True);
    }

    /// <summary>Q282-A01 另测：非配额错误走原失败分支（“保存失败：”+原始信息）；只认类别名，不按英文描述判断。</summary>
    [Test]
    public void Q282_A01_NonQuotaErrors_KeepGenericBranch()
    {
        Assert.That(SaveWriteError.IsQuotaExceeded("SecurityError", "The operation is insecure."), Is.False);
        Assert.That(SaveWriteError.IsQuotaExceeded("", "exceeded the quota"), Is.False, "没有类别名不猜");
        Assert.That(SaveWriteError.IsQuotaExceeded(null, null), Is.False);
        Assert.That(SaveWriteError.PlayerText(false, "The operation is insecure."), Is.EqualTo("保存失败：The operation is insecure."));
    }

    /// <summary>接线：saveSet 回传类别名；Save 失败按类别标记，成功才清错误；底栏用整句并后接原重试（重试走原 Save）。</summary>
    [Test]
    public void Q282_Wiring_FooterAndRetry()
    {
        var js = Web("wwwroot/js/app.js");
        Assert.That(js, Does.Contain("e.name").And.Contain("console.warn"), "回传异常类别；原始异常留控制台诊断");
        Assert.That(Web("Services/SaveStore.cs"), Does.Contain("throw SaveWriteException.FromJs(err)"));
        var g = Web("Services/GameSession.cs");
        Assert.That(g, Does.Contain("SaveErrorQuota = SaveWriteError.IsQuotaExceeded((ex as SaveWriteException)?.Category, ex.Message);"));
        var retry = g.Substring(g.IndexOf("public void RetrySave()"), 80);
        Assert.That(retry, Does.Contain("Save();"), "重试沿原存档流程");
        var screen = Web("Components/GameScreen.razor");
        Assert.That(screen, Does.Contain("<span>@G.SaveErrorText </span><button class=\"linkish\" @onclick=\"G.RetrySave\">重试</button>"));
        Assert.That(screen, Does.Not.Contain("保存失败：@G.SaveError"));
    }
}

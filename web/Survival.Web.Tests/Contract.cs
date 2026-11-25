using System.Text.Json;

namespace Survival.Web.Tests;

/// <summary>
/// B36a：网页层测试读策划合同 JSON（同 Survival.Core.Tests 的 SourceText.Contract）：文案期望从合同取，不在测试里另抄一遍。
/// 按文件名在仓库 doc/策划案 下找唯一一份；取值用「a.b.c」路径。
/// </summary>
internal static class Contract
{
    private static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    public static string Text(string fileName, string path)
    {
        var hits = Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), fileName, SearchOption.AllDirectories);
        Assert.That(hits, Has.Length.EqualTo(1), "合同应唯一：" + fileName);
        using var doc = JsonDocument.Parse(File.ReadAllText(hits[0]));
        var node = doc.RootElement;
        foreach (var part in path.Split('.')) node = node.GetProperty(part);
        return node.GetString()!;
    }

    /// <summary>B36a 次要答复合同（02 Q950—Q955）。</summary>
    public static string B36a(string path) => Text("b36a-q950-q955-ui-copy.json", path);
}

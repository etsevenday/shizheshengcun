using Newtonsoft.Json.Linq;

namespace Survival.Core.Tests;

/// <summary>
/// N02 对白与玩家可见文字重写（批次 B38）的逐字数据 assets/20261107/dialogue-rewrite.json：383 条对白按 id 原位换 text/note，
/// 95 项配置字段按 section/id/field 从 old 换成 replacement。旧合同（Q217、Q236、Q245 等）逐字断言的期望值经这里换成 N02 新稿：
/// 合同句的 id 在 N02 里有新稿时，先核对合同原文等于 N02 的 oldText，再返回新文；没有列出的 id 原样返回合同原文。
/// </summary>
internal static class N02
{
    public static readonly JObject Data =
        JObject.Parse(File.ReadAllText(Path.Combine(TestContext.CurrentContext.TestDirectory, "design.dialogue-rewrite.json")));

    public static IEnumerable<JObject> Rows => ((JArray)Data["dialogueRows"]!).Cast<JObject>();

    public static IEnumerable<JObject> ConfigCopy => ((JArray)Data["configCopy"]!).Cast<JObject>();

    private static readonly Dictionary<string, JObject> ById = Rows.ToDictionary(r => (string)r["id"]!);

    /// <summary>对白 id 的 N02 新正文；合同原文 old 须等于 N02 的 oldText。id 不在 N02 里则返回 old。</summary>
    public static string Line(string id, string old)
    {
        if (!ById.TryGetValue(id, out var r)) return old;
        Assert.That((string)r["oldText"]!, Is.EqualTo(old), $"{id}：合同原文与 N02 oldText 不一致");
        return (string)r["text"]!;
    }

    /// <summary>对白 id 的 N02 新正文（必须在 N02 里）。</summary>
    public static string Line(string id) => (string)ById[id]["text"]!;

    /// <summary>对白 id 的 N02 新附注；合同原附注 old 须等于 N02 的 oldNote。没有新附注则返回 old。</summary>
    public static string Note(string id, string old)
    {
        if (!ById.TryGetValue(id, out var r) || r["note"] == null) return old;
        Assert.That((string)r["oldNote"]!, Is.EqualTo(old), $"{id}：合同原附注与 N02 oldNote 不一致");
        return (string)r["note"]!;
    }

    /// <summary>配置字段的 N02 新值；合同原值 old 须等于 N02 的 old。没有列出则返回 old。</summary>
    public static string Field(string section, string? id, string field, string old)
    {
        var e = ConfigCopy.SingleOrDefault(x => (string)x["section"]! == section && (string?)x["id"] == id && (string)x["field"]! == field);
        if (e == null) return old;
        Assert.That((string)e["old"]!, Is.EqualTo(old), $"{section}/{id}/{field}：合同原值与 N02 old 不一致");
        return (string)e["replacement"]!;
    }
}

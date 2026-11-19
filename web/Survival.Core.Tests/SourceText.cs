using System.Text;
using Newtonsoft.Json.Linq;

namespace Survival.Core.Tests;

/// <summary>
/// 新写源码断言与文案断言的统一入口（2026-11-19 测试策略调整，见 doc/开发记录/试玩场景库试点.md §9）：
/// <list type="bullet">
/// <item><see cref="Web"/>／<see cref="Code"/>：读 .cs／.razor／.css／.js 时先去掉注释再交给字符串断言——断言只看代码，不拿注释当锚点；
/// 起因是 B56 一条“旧句不得出现”的断言因新注释引用旧句而变红。旧测试大量以“代码＋行尾注释”为锚，不批量改，碰到时再换。</item>
/// <item><see cref="Contract"/>：按文件名读策划合同 JSON（doc/策划案 下 assets），文案期望从合同取，不在测试里另抄一遍。</item>
/// </list>
/// </summary>
internal static class SourceText
{
    private static readonly HashSet<string> CodeExt = new(StringComparer.OrdinalIgnoreCase) { ".cs", ".razor", ".css", ".js" };

    /// <summary>仓库根目录（含 doc/策划案 的那一层）。</summary>
    public static string Root()
    {
        var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "doc", "策划案"))) dir = dir.Parent;
        Assert.That(dir, Is.Not.Null, "找不到仓库根目录");
        return dir!.FullName;
    }

    /// <summary>网页壳源码（相对 web/Survival.Web），去注释、换行统一为 LF。</summary>
    public static string Web(string relative) => Code(Path.Combine(Root(), "web", "Survival.Web", relative));

    /// <summary>任意源码文件，去注释、换行统一为 LF；非源码扩展名原样返回。</summary>
    public static string Code(string path)
    {
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        return CodeExt.Contains(Path.GetExtension(path)) ? StripComments(text, Path.GetExtension(path)) : text;
    }

    /// <summary>策划合同 JSON：按文件名在 doc/策划案 下找唯一一份。</summary>
    public static JObject Contract(string fileName)
    {
        var hits = Directory.GetFiles(Path.Combine(Root(), "doc", "策划案"), fileName, SearchOption.AllDirectories);
        Assert.That(hits, Has.Length.EqualTo(1), "合同应唯一：" + fileName);
        return JObject.Parse(File.ReadAllText(hits[0]));
    }

    /// <summary>
    /// 去掉 Razor 注释 <c>@* … *@</c>、块注释 <c>/* … */</c>、行注释 <c>// …</c>（含 <c>///</c>）。
    /// 字符串与字符字面量里的内容不动（"http://"、"/* 不是注释 */" 等）；逐字字符串 @"…" 与插值字符串按引号配对处理。
    /// .css 只有块注释；.razor 里 HTML 文本中的 <c>//</c>（如网址）若不在引号里会被当成行注释——仓库界面文字不写网址，按此处理。
    /// </summary>
    public static string StripComments(string s, string ext)
    {
        var css = ext.Equals(".css", StringComparison.OrdinalIgnoreCase);
        var sb = new StringBuilder(s.Length);
        int i = 0;
        char quote = '\0';
        bool verbatim = false;
        while (i < s.Length)
        {
            char c = s[i];
            if (quote != '\0')
            {
                sb.Append(c);
                if (verbatim)
                {
                    if (c == '"') { if (i + 1 < s.Length && s[i + 1] == '"') { sb.Append('"'); i += 2; continue; } quote = '\0'; verbatim = false; }
                }
                else if (c == '\\' && i + 1 < s.Length) { sb.Append(s[i + 1]); i += 2; continue; }
                else if (c == quote || c == '\n') { quote = '\0'; }
                i++;
                continue;
            }
            if (!css && c == '@' && i + 1 < s.Length && s[i + 1] == '*')
            {
                int end = s.IndexOf("*@", i + 2, StringComparison.Ordinal);
                i = end < 0 ? s.Length : end + 2;
                continue;
            }
            if (c == '/' && i + 1 < s.Length && s[i + 1] == '*')
            {
                int end = s.IndexOf("*/", i + 2, StringComparison.Ordinal);
                i = end < 0 ? s.Length : end + 2;
                continue;
            }
            if (!css && c == '/' && i + 1 < s.Length && s[i + 1] == '/' && (i == 0 || s[i - 1] != ':'))
            {
                int end = s.IndexOf('\n', i);
                i = end < 0 ? s.Length : end;   // 换行留给下一轮原样写入
                continue;
            }
            if (c == '"' || (c == '\'' && !css && IsCharLiteralStart(s, i)) || (c == '`' && !css))
            {
                verbatim = c == '"' && i > 0 && s[i - 1] == '@';
                quote = c;
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    /// <summary>只把 'x'、'\n' 这样的短字面量当成字符；Razor／HTML 文本里的撇号（如英文缩写）不开引号。</summary>
    private static bool IsCharLiteralStart(string s, int i) =>
        (i + 2 < s.Length && s[i + 2] == '\'') || (i + 3 < s.Length && s[i + 1] == '\\' && s[i + 3] == '\'');
}

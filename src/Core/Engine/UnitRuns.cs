using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Survival.Core.Engine
{
    /// <summary>
    /// BM4b（M835）：窄栏里「数值＋单位」不断行的 HTML 片段。先在纯文本上找出数字连同紧跟单位的片段，
    /// 各段分别 HTML 转义，数字片段包进 <c>span.nb</c>；文字本身不改。不能在已转义文本上匹配（会把「&amp;#183;」等实体里的数字也包进去）。
    /// </summary>
    public static class UnitRuns
    {
        private static readonly Regex Run = new Regex(@"[0-9][0-9.]*(?:/[0-9][0-9.]*)?(?:毫升|分钟|小时|公斤|%)?", RegexOptions.CultureInvariant);

        public static string Html(string text)
        {
            var sb = new StringBuilder();
            var last = 0;
            foreach (Match m in Run.Matches(text))
            {
                sb.Append(WebUtility.HtmlEncode(text.Substring(last, m.Index - last)));
                sb.Append("<span class=\"nb\">").Append(WebUtility.HtmlEncode(m.Value)).Append("</span>");
                last = m.Index + m.Length;
            }
            sb.Append(WebUtility.HtmlEncode(text.Substring(last)));
            return sb.ToString();
        }
    }
}

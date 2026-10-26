using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Survival.Core.Config
{
    /// <summary>
    /// 策划配置里的公式字符串（如 "1+0.035*L"）：数值只存在于字符串里。内核按“形状”（数字换成 #）认出公式，
    /// 再按位置取出系数，配置仍是唯一来源；形状变了 ConfigLoader 报错，提醒同步改代码，而不是静默用旧系数。
    /// </summary>
    public static class Formula
    {
        private static readonly Regex Number = new Regex(@"\d+(\.\d+)?", RegexOptions.Compiled);
        private static readonly Dictionary<string, double[]> Cache = new Dictionary<string, double[]>();

        /// <summary>公式里的全部数字，按出现顺序。</summary>
        public static double[] Nums(string? formula)
        {
            if (string.IsNullOrEmpty(formula)) return new double[0];
            lock (Cache)
            {
                if (Cache.TryGetValue(formula!, out var v)) return v;
                v = Number.Matches(formula!).Cast<Match>().Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToArray();
                Cache[formula!] = v;
                return v;
            }
        }

        public static double Num(string? formula, int index)
        {
            var n = Nums(formula);
            return index < n.Length ? n[index] : double.NaN;
        }

        /// <summary>数字换成 # 后的形状，用于校验。</summary>
        public static string Shape(string? formula) => formula == null ? "" : Number.Replace(formula, "#");
    }
}

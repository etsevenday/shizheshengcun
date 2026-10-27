using System;
using System.Globalization;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 统一数值显示（v0.6 §02）：两位小数，四舍五入（先加 1e-6 个百分位的吸附量，消除 2.7999999 这类浮点误差），
    /// 正且小于 0.01 显示“&lt;0.01”，不显示为 0；负数用全角减号“−”。内部数值保持原始 double，这里只做显示。
    /// 试玩修复 D1：原向零截断会让“3.60−0.80”显示成 2.79、“3.20−1.15”显示成 2.04（被减数实际是 1.157 之类），改为四舍五入后前后两数口径一致。
    /// </summary>
    public static class Fmt
    {
        public const string Tiny = "<0.01";
        private const double Snap = 1e-6;

        private static double Round2(double abs) => Math.Floor(abs * 100 + 0.5 + 1e-6) / 100;

        /// <summary>固定两位小数，如 17.53、0.40、“&lt;0.01”。用于状态当前值、成本、伤害等。</summary>
        public static string N(double v)
        {
            if (double.IsNaN(v)) return "—";
            var abs = Math.Abs(v);
            if (abs > 0 && abs < 0.01 - Snap / 100) return v > 0 ? Tiny : "−" + Tiny;
            var t = Round2(abs);
            var s = t.ToString("0.00", CultureInfo.InvariantCulture);
            return v < 0 && t > 0 ? "−" + s : s;
        }

        /// <summary>至多两位小数、去掉末尾 0，如 4、0.6、1.25。用于阈值、每小时速率、规则说明里的常量。</summary>
        public static string T(double v)
        {
            if (double.IsNaN(v)) return "—";
            var abs = Math.Abs(v);
            if (abs > 0 && abs < 0.01 - Snap / 100) return v > 0 ? Tiny : "−" + Tiny;
            var t = Round2(abs);
            var s = t.ToString("0.##", CultureInfo.InvariantCulture);
            return v < 0 && t > 0 ? "−" + s : s;
        }

        /// <summary>带符号的变化量：“+0.40”“−1.20”；为 0 写“0.00”。</summary>
        public static string Delta(double v) => v > 0 ? "+" + N(v) : N(v);

        /// <summary>带符号的常量：“+6”“−1.6”。</summary>
        public static string SignedT(double v) => v > 0 ? "+" + T(v) : T(v);

        /// <summary>百分比，至多两位小数：“51.92%”。</summary>
        public static string Pct(double p) => T(p * 100) + "%";

        /// <summary>
        /// 重量数值（S06 §7 Q13）：公斤固定两位小数，10g→“0.01”、1000g→“1.00”、0g→“0.00”。调用方先按精确克数合计再调用；
        /// 中点远离零舍入、不随区域设置变化（开发假设 A10）。只做显示，负重阈值比较一律用克。
        /// </summary>
        public static string KgValue(long grams) =>
            Math.Round(grams / 1000m, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);

        /// <summary>带单位的重量：“0.05公斤”“12.34公斤”。</summary>
        public static string Kg(long grams) => KgValue(grams) + "公斤";
    }
}

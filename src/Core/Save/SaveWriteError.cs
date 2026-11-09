using System;

namespace Survival.Core.Save
{
    /// <summary>
    /// 存档写入失败的分类（S01「2026-11-09 Q281–Q282」Q282）：按浏览器存储异常类别区分配额用尽与其他保存错误。
    /// 配额用尽时底栏整句用定稿中文，不再拼“保存失败：”前缀；其他错误走原失败分支（“保存失败：”+原始信息）。
    /// </summary>
    public static class SaveWriteError
    {
        /// <summary>Q282 定稿逐字稿：浏览器存储配额用尽导致存档写入失败时底栏的完整句。</summary>
        public const string QuotaFullText = "存档失败：浏览器存储空间已满。";

        /// <summary>原失败分支的前缀（Q282 之前的现状）。</summary>
        public const string GenericPrefix = "保存失败：";

        /// <summary>
        /// 浏览器 Storage 配额异常的类别名：标准 DOMException 名 QuotaExceededError（Chrome、Edge、Safari、新版 Firefox），
        /// 旧版 Firefox 的 NS_ERROR_DOM_QUOTA_REACHED，旧版 Safari 的常量名 QUOTA_EXCEEDED_ERR。
        /// </summary>
        private static readonly string[] QuotaCategories = { "QuotaExceededError", "NS_ERROR_DOM_QUOTA_REACHED", "QUOTA_EXCEEDED_ERR" };

        /// <summary>
        /// 是否配额用尽：category 为浏览器异常的 name（网页壳 saveSet 单独回传）；category 缺失时（如 JSInterop 直接抛出的 JSException）
        /// 在异常信息里找类别名。只认类别名，不按英文描述句（各浏览器措辞不同）判断。
        /// </summary>
        public static bool IsQuotaExceeded(string? category, string? message = null)
        {
            foreach (var q in QuotaCategories)
            {
                if (!string.IsNullOrEmpty(category) && string.Equals(category!.Trim(), q, StringComparison.Ordinal)) return true;
                if (!string.IsNullOrEmpty(message) && message!.IndexOf(q, StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        /// <summary>底栏失败句（后接现有“重试”按钮）：配额用尽为定稿整句，其他为原分支“保存失败：”+原始信息。</summary>
        public static string PlayerText(bool quota, string? message) => quota ? QuotaFullText : GenericPrefix + (message ?? "");
    }
}

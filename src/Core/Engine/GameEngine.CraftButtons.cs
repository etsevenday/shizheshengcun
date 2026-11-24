using System.Linq;
using Survival.Core.Config;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B35b「UI 重做：制作页三列与来源侧页」的内核文字（S14／S08／逐字稿 2026-11-14 B35 次要 Q597、2026-11-13 B35 Q593、
    /// 2026-11-16 B35 Q801—Q802、2026-11-17 B35 次要 Q912、2026-11-18 B35b Q946—Q948、2026-11-18 BM11 Q965；S14 B51c Q873）。
    /// 开工按钮“{动作名}（{时长}）”的时长与右栏完整预估同源（同一次 <see cref="ActionPreview"/>）；只改文字，不改能否开工与拒绝条件。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───── 逐字（Q597、Q947、Q912、Q965） ─────
        public const string StartCraftVerb = "开始制作";
        public const string StartBuildVerb = "开始建造";
        /// <summary>Q597／Q912／Q965：工具维护与现场修补的开工按钮都叫“开始维修”（配方名“工具维护”“现场修补”不改）。</summary>
        public const string StartRepairVerb = "开始维修";
        public const string StartSalvageVerb = "开始拆解";

        /// <summary>逐字稿 P06a、05 P06／Q593：制作窗口内右列标题与两行字段名。</summary>
        public const string CraftRunTitle = "本次制作";
        public const string CraftRunTime = "耗时";
        public const string CraftRunOutput = "产物";

        /// <summary>
        /// 配方开工按钮的动作名（Q597：制作／建造／两种维修／拆解；维修只统一按钮叫法，不合并两个动作）。
        /// </summary>
        public static string StartVerb(RecipeConfig rc) =>
            rc.IsBuild ? StartBuildVerb : rc.IsRepair ? StartRepairVerb : rc.IsSalvage ? StartSalvageVerb : StartCraftVerb;

        /// <summary>
        /// 开工按钮全文（Q597 A、Q801 A、Q873 A、Q947 A）：
        /// 不能开工（缺料、工具、等级、资格、忙碌…）只写动作名，不带括号；
        /// 能开工且完成可靠写“{动作名}（{实际预计时长}）”，时长与右栏“预计耗时”同一次预估、同一格式；
        /// 能开工但求不出可信完成写“{动作名}（未预估：{实际原因}）”，没有可靠原因写“{动作名}（未预估）”（不嵌两层括号）。
        /// </summary>
        public static string StartButtonText(RecipeConfig rc, ActionPreview p)
        {
            var verb = StartVerb(rc);
            if (!p.Ok) return verb;
            if (p.PlanFailed) return p.PlanFailShort is string why ? $"{verb}（{NotEstimated}：{why}）" : $"{verb}（{NotEstimated}）";
            return $"{verb}（{Clock.Duration(p.EstimatedMinutes)}）";
        }

        /// <summary>
        /// Q597／Q947：陶窑／固定净水台的投料按钮“投料（{批数}批 · {时长}）”。{时长}是原固定配方的被动加工时长（各批同时加工，每批同长），
        /// 不因这次投料受阻而丢掉；投料本身仍 0 分钟、不占行动。
        /// </summary>
        public static string StationLoadText(RecipeConfig rc, int batches) => $"投料（{System.Math.Max(1, batches)}批 · {Clock.Duration(rc.BaseMinutes)}）";

        /// <summary>Q593（逐字稿 B35 表“开工位置脚注”）：右栏预估脚注，{按钮名}为实际按钮全文、不含键帽。</summary>
        public static string CraftAreaFootNote(string button) => $"在‘本次制作’区点‘{button}’；时间为预计值。";

        /// <summary>Q593（逐字稿 B35 表“副标题悬停”）：制作窗口副标题的悬停说明。</summary>
        public static string CraftAreaHint(string button) => $"在‘本次制作’区点‘{button}’；右栏显示完整预估。";

        /// <summary>Q593（逐字稿 B35 表“陶窑／净水台对应操作指引”）。</summary>
        public static string StationAreaGuide(string button) => $"在‘本次制作’区点‘{button}’。";

        /// <summary>Q593：制作窗口副标题（“配方 · 完整预估在右栏 ⓘ”；工作区沿 B35a1 前缀“工坊作业”／“公共设施”）。</summary>
        public const string CraftSubtitleTail = " · 完整预估在右栏 ⓘ";

        /// <summary>
        /// Q946 B（S14／S08 2026-11-18 B35b）：全局右栏燃料短注记——锻炉“每份耗{燃料名}{数量}”，陶窑／固定净水台“每批耗{燃料名}{数量}”；
        /// 不重复设施全名、不写原料＋燃料＝总计。“每份／每批”指原配方执行一次／投料一批，不按成品件数。无燃料配方为 null。
        /// </summary>
        public string? RecipeFuelPerNote(RecipeConfig rc) => rc.Fuel.Count == 0 ? null
            : $"{(IsStationBatchRecipe(rc) ? "每批" : "每份")}耗{string.Join("、", rc.Fuel.Select(kv => $"{ItemName(kv.Key)}{kv.Value}"))}";
    }
}

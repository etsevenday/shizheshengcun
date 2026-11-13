using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B34a 全屏战斗阅读区与操作台的正式文字（《重墨 V2 开工裁定与界面逐字稿》2026-11-13 Q570 表、2026-11-12 BM3 Q528—Q530 表、
    /// §1 Q298、§2 P05a／P05c／P05d／P05f／P05h）。动态字段由调用方取真实数据，这里只拼句。
    /// </summary>
    public static class BattleInfoCopy
    {
        // Q570 页面入口与数据
        public const string InfoTab = "战斗信息";
        public const string LogTab = "战报";
        public const string Collapse = "收起";
        public const string Detail = "预测详情";
        public const string FullLog = "全记录";
        public const string LogTitle = "战斗记录";

        // Q528 五行字段名
        public const string RowTarget = "目标";
        public const string RowCost = "成本";
        public const string RowHit = "命中与暴击";
        public const string RowDamage = "伤害";
        public const string RowState = "目标状态";
        public const string TakenBy = "实际承伤";
        public const string DamageNoCrit = "伤害（未暴击）";
        public static string Affected(int count) => $"受影响{count}个目标";

        // Q528.2 战报与全记录
        public const string LogEmpty = "还没有战斗记录。";
        public const string BackToLatest = "回到最新";
        public static string NewRecords(int count) => $"有{count}条新记录";
        public const string LegacyMissing = "旧存档中的较早战斗记录可能未保存，以下为已保存内容。";
        public const string BackToBattle = "返回战斗";
        public const string NoPreview = "当前没有可查看的指令预测。";

        // Q528.3 提示
        public const string HintLabel = "系统提示";
        public const string HintOpen = "查看提示";
        public const string HintTitle = "操作提示";
        public const string HintEmpty = "当前没有操作提示。";

        // Q298／P05d 医疗与物品
        public const string MedEntry = "医疗与物品";
        public const string MedDrugs = "药品";
        public const string MedTactical = "战术物品";
        public const string MedDrugsNote = "药品只能由当前行动者自用。";
        public const string MedTacticalNote = "每人每完整回合可使用1次战术物品。";

        // P05a 操作台三区，P05c／P05f 入口与页名
        public const string WhoPanel = "当前行动者";
        public const string WhoAp = "行动点";
        public const string WhoEnergy = "体力";
        public const string WhoPanic = "恐慌";
        public const string WhoWeapon = "当前武器";
        public const string SkillPanel = "武器与技能";
        public const string GeneralPanel = "通用行动";
        public const string SuppliesEntry = "查看物资";
        public const string SuppliesTitle = "战斗物资";
        public const string EquipEntry = "查看装备";
        public const string EquipTitle = "战斗装备";

        /// <summary>阅读区「战报」页计数：「最近{显示条数}条 · 共{本场记录数}条」；旧档较早记录可能缺失时「· 已保存{条数}条」。</summary>
        public static string RecentCount(int shown, int total, bool maybeIncomplete) =>
            maybeIncomplete ? $"最近{shown}条 · 已保存{total}条" : $"最近{shown}条 · 共{total}条";

        /// <summary>战斗记录子页副标：「本场战斗 · 共{N}条」／「本场战斗 · 已保存{N}条」。</summary>
        public static string LogSubtitle(int total, bool maybeIncomplete) =>
            maybeIncomplete ? $"本场战斗 · 已保存{total}条" : $"本场战斗 · 共{total}条";

        /// <summary>最近 5 条（按发生顺序，最新在最后），带本场日志下标；不足 5 条只给已有的，不补假条。</summary>
        public static List<(int Index, string Text)> Recent(IReadOnlyList<string> log, int max = 5)
        {
            var from = System.Math.Max(0, log.Count - max);
            return Enumerable.Range(from, log.Count - from).Select(i => (i, log[i])).ToList();
        }

        /// <summary>
        /// Q530：未指定目标时的标题「{AP文案} · 体力−{体力消耗} · 可选目标{数量}个」（再接原「 · 冷却{轮数}轮」后缀）；
        /// 消耗为 0 或按原精度显示为 0 时省略体力项，不写负零。
        /// </summary>
        public static string AimNoTargetHeader(string apText, double energyCost, int candidates, int cooldownRounds)
        {
            var energy = energyCost > 0 && Fmt.N(energyCost) != Fmt.N(0) ? $" · 体力−{Fmt.N(energyCost)}" : "";
            var cd = cooldownRounds > 0 ? $" · 冷却{cooldownRounds}轮" : "";
            return $"{apText}{energy} · 可选目标{candidates}个{cd}";
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41e1 新版敌踪界面（敌踪卡、居中操作盘、敌踪详情）用到的只读数据与原句。挂开关 encounters.enemyCardsV2（壳层只在 <see cref="GameEngine.CardsV2"/> 为真时读）。
    /// 原句出处：《重墨 V2 开工裁定与界面逐字稿》「B41 Q426–Q428 正式文案」之「卡片、操作盘与倒计时」、「B41d／B41e Q445—Q447 补充定稿」Q446 ④⑥、Q447。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>逐字稿「敌人详情脚注」，Q446 ⑥ 三类卡悬停共用句。</summary>
        public const string EnemyCardFootnote = "点击敌踪卡查看行动。可先核对风险，再选择处理方式。";

        /// <summary>逐字稿「固定关键敌人详情补句」（Q446 ⑥ 固定卡悬停的「必须战胜说明」也用这句）。</summary>
        public const string KeyEnemyDetailNote = "必须战胜这处敌踪，才能解除对应的资源或路线限制。";

        /// <summary>逐字稿「右栏原「接敌还是绕行？」标题」。</summary>
        public const string EnemyCardDetailTitle = "敌踪详情";

        /// <summary>Q447：躲避按失败入战的受惊预览前缀。</summary>
        public const string EvadeFailFearPrefix = "躲避失败时";

        /// <summary>逐字稿「三种处理按钮」与「操作盘关闭」。</summary>
        public const string ApproachCardButton = "主动接近";
        public const string EnemyDiskBack = "返回";

        /// <summary>逐字稿「操作盘信息标签」（按原顺序）。</summary>
        public static readonly IReadOnlyList<string> EnemyDiskLabels = new[] { "警惕", "接近", "队长", "成功率", "耗时" };

        /// <summary>逐字稿「警惕值」。</summary>
        public static string VigilanceText(int level) => $"警惕{level}级";

        /// <summary>逐字稿「队长值」：姓名用现有公开称呼，主角写「你」。</summary>
        public string SquadLeaderText(GameState s) => $"队长：{ActorDisplayName(s, SquadLeaderId(s))}";

        /// <summary>逐字稿「潜行／躲避成功率」。</summary>
        public static string StealthRateText(int rate) => $"成功率{rate}%";

        /// <summary>逐字稿「潜行／躲避耗时」（分钟取配置 encounters.stealth.checkMinutes，现为 5）。</summary>
        public string StealthCheckMinutesText => $"耗时{Config.Encounters.Stealth.CheckMinutes}分钟";

        /// <summary>当前地点、当前区域可操作的这张卡（不在 <see cref="EnemyCardsHere"/> 里为 null）：界面开盘、选中、刷新与提交前的重验都用它（R05 Q457）。</summary>
        public EnemyCardView? EnemyCardHere(GameState s, string? cardKey) =>
            string.IsNullOrEmpty(cardKey) ? null : EnemyCardsHere(s).FirstOrDefault(c => c.Key == cardKey);

        /// <summary>
        /// 敌踪卡悬停（Q446 ⑥）：常驻、固定战、可选强敌复用脚注句，加该卡真实状态句（<see cref="CardApproachStatus"/>）；固定卡补必须战胜说明
        /// （可选卡「由你决定何时处理」就在它的状态句里）。按行返回，界面自行排版。
        /// 随机组卡：Q539 未答，没有专用悬停句，返回空表（TODO Q539：答复后按原句补）。
        /// </summary>
        public List<string> EnemyCardHoverLines(GameState s, EnemyCardView card)
        {
            var lines = new List<string>();
            if (card.Kind == "group") return lines;   // TODO Q539：随机组卡的悬停句等策划答复
            lines.Add(EnemyCardFootnote);
            if (card.Kind == "fixed") lines.Add(KeyEnemyDetailNote);
            if (CardApproachStatus(s, card) is string st) lines.Add(st);
            return lines;
        }

        /// <summary>
        /// 操作盘与详情的逐成员预览名单（Q447：组卡及固定战卡按锁定名单原顺序逐个成员）：(敌种, 世界实例)。
        /// 组卡不含隐藏中的成员（与主动接近实际入场一致）；可选强敌第一次实际处理前还没有实例，给 (挑战敌种, null)。只读。
        /// </summary>
        public List<(string EnemyId, WorldEnemy? Enemy)> EnemyCardPreviewMembers(GameState s, EnemyCardView card)
        {
            var list = card.Members.Where(w => card.Kind != "group" || !Hidden(s, w)).Select(w => (w.EnemyId, (WorldEnemy?)w)).ToList();
            if (list.Count == 0 && card.Kind == "elite" && card.EnemyIds.Count > 0) list.Add((card.EnemyIds[0], null));
            return list;
        }

        /// <summary>
        /// 操作盘受惊预览（Q447）：主动接近、潜行接近按 0.5 情境（<see cref="FearApproach"/>）；躲避按失败入战的 1.0 情境（<see cref="FearEncounter"/>）。
        /// 沿现有 <see cref="FearNote"/> 句（受惊源按全名单真实取，与实际入战同一口径），不把情境系数写成受惊概率。只读。
        /// </summary>
        public string EnemyCardFearNote(GameState s, EnemyCardView card, bool evadeFail)
        {
            var group = EnemyCardPreviewMembers(s, card).Select(m => m.Enemy ?? new WorldEnemy { EnemyId = m.EnemyId }).ToList();
            return FearNote(s, group, evadeFail ? FearEncounter : FearApproach);
        }
    }
}

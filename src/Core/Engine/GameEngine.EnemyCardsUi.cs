using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41e1 新版敌踪界面（敌踪卡、居中操作盘、敌踪详情）用到的只读数据与原句。（B41e3 起新版敌踪是唯一流程。）
    /// 原句出处：《重墨 V2 开工裁定与界面逐字稿》「B41 Q426–Q428 正式文案」之「卡片、操作盘与倒计时」、「B41d／B41e Q445—Q447 补充定稿」Q446 ④⑥、Q447。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>首战门控时其他敌踪的处理拒绝原因（逐字稿「新操作的禁用与拒绝」）；BM5b1（R05 Q639）起也是这类卡的卡面受阻原因。</summary>
        public const string TutorialGateReason = "先战胜农野荒地的游荡尸，再处理其他敌踪。";

        /// <summary>BM5b1（S14／R05 Q639 逐字稿「原确实无咬伤传播能力者」）：右栏战斗详情与中央盘统一句。</summary>
        public const string NonBiteNote = "它不会咬伤，攻击不传播病毒。";

        /// <summary>
        /// BM5b1（R05 Q639）：右栏身份「{身份}·{原卡名}」——只在真实守卫标记（固定战 guardian）时写「守卫·{原卡名}」，原卡名已含同前缀不重复；
        /// 其他卡沿原卡名（身份类别仍在原角标）。不凭风险或“强敌”造身份；只影响文字。
        /// </summary>
        public static string EnemyRailIdentity(EnemyCardView card) =>
            card.Guardian && !card.Title.StartsWith(GuardianIdentity) ? $"{GuardianIdentity}·{card.Title}" : card.Title;

        /// <summary>R05 Q639：真实守卫标记的身份名（沿原角标「守卫」）。</summary>
        public const string GuardianIdentity = "守卫";

        /// <summary>
        /// BM5b1（R05 Q639）：敌踪卡面的受阻原因——卡自身的真实优先限制：首战门控时 <see cref="TutorialGateReason"/>，固定关键敌人（教学、固定战、守卫、检查哨）
        /// 原不含时间的限制句 <see cref="KeyEnemyDetailNote"/>；其他为 null。行动进行中等全局状态不上卡面（仍在中央按钮禁用／拒绝处），点击仍原重验。
        /// </summary>
        public static string? EnemyCardFaceReason(EnemyCardView card) =>
            card.TutorialGated ? TutorialGateReason : card.KeyEnemy ? KeyEnemyDetailNote : null;

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
        /// 敌踪卡悬停（Q446 ⑥、Q539、Q550、Q551）：按行返回，界面自行排版。顺序：通用句 → 关键句（固定关键敌人，含教学卡；Q550）
        /// → 可选强敌特殊掉落说明（Q551）→ 该卡真实状态句（<see cref="CardApproachStatus"/>）。随机组卡也复用通用句再接同一状态句（Q539）。
        /// 可选卡「由你决定何时处理」就在它的状态句里；普通常驻、随机组、可选强敌不补关键句。
        /// </summary>
        public List<string> EnemyCardHoverLines(GameState s, EnemyCardView card)
        {
            var lines = new List<string> { EnemyCardFootnote };
            // BM5b1（R05 Q639）：卡面受阻原因在悬停／焦点完整可读（关键句沿原位置）
            if (card.TutorialGated) lines.Add(TutorialGateReason);
            if (card.KeyEnemy) lines.Add(KeyEnemyDetailNote);
            if (EliteDropNote(card) is string drop) lines.Add(drop);
            if (CardApproachStatus(s, card) is string st) lines.Add(st);
            return lines;
        }

        /// <summary>
        /// 卡面第一副标的成员名单（Q539：随机组卡照固定卡格式）：锁定名单原序的当前存活真实成员公开名，以「、」连接，同名不合并。
        /// 只给随机组卡与固定战卡，其余为 null。
        /// </summary>
        public string? EnemyCardMemberNames(EnemyCardView card) =>
            card.Kind is "group" or "fixed" ? string.Join("、", card.Members.Select(w => _enemies[w.EnemyId].Name)) : null;

        /// <summary>
        /// 同卡撤退保护的长句（Q538；逐字稿 B41e Q537—Q541 补充表）：只在该卡自己的撤退保护有效时给出，放敌踪详情与居中操作盘的主动接近风险区；到期为 null。旧档历史全地点保护不套此句。
        /// BM5b1（R05 Q639）：改无时长句「此敌踪处于撤退保护中，期间可主动处理。」，不再显示剩余时长（内部截止不变）。
        /// </summary>
        public string? CardGuardNote(GameState s, EnemyCardView card)
        {
            if (card.Optional) return null;
            var g = CardTimer(s, card.Key)?.GuardUntil;
            return g is int until && until > s.GameMinute ? RetreatGuardText : null;
        }

        /// <summary>
        /// 新版可选强敌卡的特殊掉落说明（Q551；Q446 ⑤）：卡悬停与敌踪详情两处同源。沿旧告示说明的「可选强敌 · 单局一次」与特殊掉落片段，
        /// 概率分支的「首次接战时抽定」改为「首次处理时抽定」，100% 分支沿原短句；不写旧「已交过手」前缀，不揭示锁定命中。只读，不生成敌人、不抽签。
        /// 不是可选强敌卡为 null。
        /// </summary>
        public string? EliteDropNote(EnemyCardView card)
        {
            if (card.Kind != "elite" || !(EliteConfig(card.Key.Substring(ElitePrefix.Length)) is LegacyEliteConfig c)) return null;
            var drop = c.RewardChance >= 1
                ? $"特殊掉落{ItemName(c.RewardItemId)}×{c.RewardCount}"
                : $"特殊掉落{ItemName(c.RewardItemId)}×{c.RewardCount}（{Fmt.Pct(c.RewardChance)}，首次处理时抽定）";
            return "可选强敌 · 单局一次 · " + drop;
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

        /// <summary>
        /// 可躲避卡的躲避失败受惊行（Q549：整卡每情境一行；逐字稿 B41e1 Q549—Q551 补充表「躲避失败时：{原受惊预览}」），按失败入战 1.0 情境。
        /// 固定关键敌人不能躲避，为 null。
        /// </summary>
        public string? EvadeFailFearLine(GameState s, EnemyCardView card) =>
            card.KeyEnemy ? null : $"{EvadeFailFearPrefix}：{EnemyCardFearNote(s, card, true)}";

        // ───────────── 练习盘（逐字稿 B41「队长与练习潜行」；Q540） ─────────────

        /// <summary>逐字稿「队长与练习潜行」各字段原句。</summary>
        public const string PracticeTitle = "练习潜行";
        public const string PracticeActorLabel = "练习者";
        public const string PracticeNote = "练习按等待消耗身体状态。做满30分钟才获得经验，退出不退已耗时间。";
        public const string PracticeStart = "开始练习";
        public const string PracticeBodyLabel = "真实身体预估";

        /// <summary>逐字稿「时长字段及值」：耗时30分钟（分钟取配置 encounters.stealth.practiceMinutes）。</summary>
        public string PracticeMinutesText => $"耗时{Config.Encounters.Stealth.PracticeMinutes}分钟";

        /// <summary>练习盘可选的练习者：主角与同地同区存活的同行者（同行队伍原顺序，主角在前）。</summary>
        public List<string> PracticeCandidates(GameState s) =>
            new[] { GameState.PlayerId }.Concat(s.Party.Where(id => id != GameState.PlayerId)).Where(id => SquadMemberValid(s, id)).ToList();

        /// <summary>
        /// 练习者本人此刻的等待身体费率（Q540）：按该人当前状态、所在地点与装备修正算一段等待（ComputeSegment，与真实练习每段同源）。
        /// 只读，不扣费。练习者不存在为 null。
        /// </summary>
        public RateBlock? PracticeBodyRates(GameState s, string actorId)
        {
            if (!s.Actors.TryGetValue(actorId, out var a)) return null;
            return ComputeSegment(s, new ActionState { Type = ActionType.Wait }, a).Rates;
        }

        /// <summary>
        /// 练习盘「真实身体预估」一行（Q540；逐字稿「等待期间每小时 {变化列表}」「等待期间每小时 无变化」）：饱食、水分、体力、清醒、精神原顺序，
        /// 非零项用原带正负号精度、空格连接。每世界小时速率，不是 30 分钟后的值。练习者不存在为 null。
        /// </summary>
        public string? PracticeBodyRateText(GameState s, string actorId)
        {
            if (!(PracticeBodyRates(s, actorId) is RateBlock r)) return null;
            var parts = new[] { ("饱食", r.Satiety), ("水分", r.Hydration), ("体力", r.Energy), ("清醒", r.Wakefulness), ("精神", r.Morale) }
                .Where(x => x.Item2 != 0).Select(x => x.Item1 + Fmt.SignedT(x.Item2)).ToList();
            return "等待期间每小时 " + (parts.Count == 0 ? "无变化" : string.Join(" ", parts));
        }

        // ───────────── 队长（逐字稿 B41「队长与练习潜行」） ─────────────

        /// <summary>逐字稿「编队队长标签」「队长说明」。</summary>
        public const string SquadLeaderLabel = "小队队长";
        public const string SquadLeaderNote = "潜行和躲避使用小队队长的潜行等级、清醒和精神进行判定。";

        // ───────────── 先手显示（R05 Q537；逐字稿 B41「先手说明与攻击预览」、B41e Q537—Q541 补充表） ─────────────

        /// <summary>逐字稿「角色仍有伤害资格的状态」。</summary>
        public const string FirstStrikeReadyText = "先手伤害加成可用。";

        /// <summary>括注里的先手项「先手×1.5」（倍率取配置 encounters.firstStrike）。</summary>
        public static string FirstStrikeTag(double factor) => $"先手×{Fmt.T(factor)}";

        /// <summary>
        /// 某名参战者本轮有效速度里的先手倍率（Q537、R05 Q422）：本场先手方的开场参战者在第一完整回合为配置倍率，中途增援与其余情况为 1。
        /// key 为我方人员 ID 或敌方 "e:世界ID"。与行动队列同源，只读。
        /// </summary>
        public double FirstStrikeRoundSpeedFactor(BattleState b, string key)
        {
            if (key.StartsWith("e:"))
            {
                var e = b.Enemies.FirstOrDefault(x => FirstStrikeEnemyKey(x.WorldId) == key);
                return e != null && e.JoinRound <= 1 ? FirstStrikeSpeedFactor(b, FirstStrikeEnemy) : 1;
            }
            return FirstStrikeSpeedFactor(b, FirstStrikeAlly);
        }

        /// <summary>敌人的先手键（界面查 FirstStrikeReady、FirstStrikeRoundSpeedFactor 用）。</summary>
        public static string FirstStrikeKeyOf(BattleEnemy e) => FirstStrikeEnemyKey(e.WorldId);

        /// <summary>敌人本轮有效速度（Q537：含第一完整回合的先手×1.5，与行动队列同源）与括注；不适用先手时括注为空。</summary>
        public (double Speed, string Note) EnemyRoundSpeed(BattleState b, BattleEnemy e)
        {
            var f = FirstStrikeRoundSpeedFactor(b, FirstStrikeEnemyKey(e.WorldId));
            return (EnemySpeedWithFirstStrike(e, f), f > 1 ? $"（含{FirstStrikeTag(f)}）" : "");
        }

        // ───────────── 中央行动位与暂停层的暂存摘要（逐字稿「B41b Q472—Q476 暂存与移动」「B41b Q439—Q444」行动地点） ─────────────

        /// <summary>逐字稿「暂存原工作摘要」：暂存行动：{原行动名}。</summary>
        public static string StashedWorkText(string label) => $"暂存行动：{label}";

        /// <summary>逐字稿「原工作地点摘要」／「原行动地点标签」：行动地点：{原地点}。</summary>
        public static string WorkPlaceText(string place) => $"行动地点：{place}";

        /// <summary>逐字稿「暂存说明」。</summary>
        public const string StashedWorkNote = "原行动保持暂停，移动结束后返回原行动的暂停状态。";
    }
}

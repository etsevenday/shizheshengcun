using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41b 新版敌踪的接近时限（R05「Q416、Q421 接近时间与到期入战」「B41b Q439—Q444」；逐字稿「卡片、操作盘与倒计时」；开发记录 B41b）。
    /// - 计时卡：普通敌对单卡、随机组卡、固定战（含守卫、检查哨）、教学首战卡。可选强敌不倒计时（Q419），普通中立单卡不抽不计时（Q439），
    ///   首战未胜时教学卡以外的卡不抽不计时（Q421）。
    /// - 每次现身首次与队伍同地同区且可操作时，从 encounters.approachMinutes 等概率抽一次并保存；读档、刷新、巡游、离开返回、撤退都不重抽。
    /// - 队伍与卡同地点同区、非战斗、真实推进一个 5 世界分钟段时减 5，最低 0；移动（含撤出）开始即视为离开起点，起点冻结、途中不扣。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>这一局此刻要按 B41b 结算敌踪接近（战斗开放、不是模拟／冻结的副本）。</summary>
        private bool ApproachLive(GameState s) => Config.CombatEnabled && !s.SuppressEnemyApproach;

        /// <summary>
        /// 这张卡按接近时限计时（Q416、Q419、Q421、Q439）：可选强敌、受首战门控的卡、普通中立单卡都不计时。
        /// 固定战、守卫、检查哨与教学卡按真实遭遇身份计时（关卡身份不因敌种中立而解除）。
        /// </summary>
        public bool CardTimed(EnemyCardView card) =>
            !card.Optional && !card.TutorialGated && !(card.Kind == "single" && !card.KeyEnemy && card.Members.Count > 0 && !card.Members[0].Hostile);

        /// <summary>某张卡的时限／保护记录（只读）；没有为 null。</summary>
        public EnemyCardTimer? CardTimer(GameState s, string cardKey) =>
            s.EnemyCards.Timers.TryGetValue(cardKey, out var t) ? t : null;

        private EnemyCardTimer TimerFor(GameState s, string cardKey)
        {
            if (!s.EnemyCards.Timers.TryGetValue(cardKey, out var t))
            {
                t = new EnemyCardTimer();
                s.EnemyCards.Timers[cardKey] = t;
            }
            return t;
        }

        /// <summary>队伍正在移动（地图移动或地牢撤出）：已视为离开起点，起点敌踪冻结、途中不拦截（Q416）。</summary>
        private static bool PartyMoving(GameState s) =>
            s.Main != null && (s.Main.Type == ActionType.Travel || s.Main.Type == ActionType.Extract);

        /// <summary>
        /// 首次可操作现身时抽接近时间（Q416、Q425、Q439）：当前地点、当前区域可操作的计时卡里还没抽过的，各按 encounters.approachMinutes 等概率抽一次。
        /// 移动途中、战斗中、终局时不抽。已有记录（含已到期、只有撤退保护的）不重抽。
        /// </summary>
        private void DrawCardTimers(GameState s)
        {
            if (!ApproachLive(s) || s.Progress.Dead || s.Battle != null || PartyMoving(s)) return;
            var opts = Config.Encounters.ApproachMinutes;
            foreach (var card in EnemyCardsHere(s))
            {
                if (!CardTimed(card)) continue;
                var t = TimerFor(s, card.Key);
                if (t.Left != null) continue;
                t.Left = opts[(int)Math.Min(opts.Count - 1, Math.Floor(s.Rng.NextDouble() * opts.Count))];
                if (t.Left <= 0) { t.Left = 0; t.DueMinute = s.GameMinute; }
            }
        }

        /// <summary>
        /// 一个非战斗 5 分钟段推进后的倒计时（Q416、Q441、Q467）：队伍与卡同地点同区、没在移动，已抽过的计时卡剩余减 5（最低 0），减到 0 记到期时刻。
        /// 菜单、阅读、离线、读档不经这里，不扣时。
        /// </summary>
        private void TickCardTimers(GameState s)
        {
            if (!ApproachLive(s) || PartyMoving(s)) return;
            foreach (var card in EnemyCardsHere(s))
            {
                if (!CardTimed(card) || !(CardTimer(s, card.Key) is EnemyCardTimer t) || t.Left == null || t.Left <= 0) continue;
                t.Left = Math.Max(0, t.Left.Value - Tick);
                if (t.Left == 0) t.DueMinute = s.GameMinute;
            }
        }

        /// <summary>卡已失效（敌人死亡、组散、单卡入组、固定战已胜）的时限／保护记录移除（Q442：单卡入新组后其撤退保护随之失效）。</summary>
        private void PruneCardTimers(GameState s)
        {
            if (s.EnemyCards.Timers.Count == 0) return;
            foreach (var key in s.EnemyCards.Timers.Keys.ToList())
                if (FindEnemyCard(s, key, out _) == null) s.EnemyCards.Timers.Remove(key);
        }

        /// <summary>BM5b1（R05 Q639 无倒计时正式句）：普通可主动接近的敌对卡。</summary>
        public const string MayApproachText = "可能主动接近。";
        /// <summary>R05 Q639：中立且原未激怒。</summary>
        public const string NeutralCardText = "中立，受到攻击后才转为敌对。";
        /// <summary>R05 Q639：可选强敌原不主动接近。</summary>
        public const string OptionalCardText = "可选强敌，不会主动接近。";
        /// <summary>R05 Q639：卡撤退保护有效（卡状态与中央风险句同一句）。</summary>
        public const string RetreatGuardText = "此敌踪处于撤退保护中，期间可主动处理。";
        /// <summary>R05 Q639：旧档全局接敌保护有效（状态与预览原因同一句）。</summary>
        public const string LegacyGraceText = "旧存档接敌保护中。";

        /// <summary>
        /// 卡接近状态句（逐字稿「卡片、操作盘与倒计时」，按可选挑战、首战门控、普通中立、有效保护、普通敌对的顺序取一条）；
        /// 旧档历史保护与同卡撤退保护并存时仍按原逻辑取较晚截止对应的身份，只输出无时长句。
        /// BM5b1（R05 Q639）：所有展示出口不再给剩余时长／截止／“已到期”阶段——普通可主动接近的敌对卡一律「可能主动接近。」（含还没抽接近时间、已到期等待的）；
        /// 固定战与教学卡（固定关键敌人）沿原不含时间的限制句（<see cref="KeyEnemyDetailNote"/>，卡面与悬停），不给它们造普通接近状态，无保护时为 null。
        /// 内部时限、到期接近、保护与 Q620 收尾一律不变。
        /// </summary>
        public string? CardApproachStatus(GameState s, EnemyCardView card)
        {
            if (card.Optional) return OptionalCardText;
            if (card.TutorialGated) return "首战胜利后开始接近计时。";
            if (!CardTimed(card)) return NeutralCardText;
            var t = CardTimer(s, card.Key);
            var guard = t?.GuardUntil is int g && g > s.GameMinute ? g : (int?)null;
            var legacy = s.EncounterGraceUntil > s.GameMinute ? s.EncounterGraceUntil : (int?)null;
            if (guard != null && (legacy == null || guard >= legacy)) return RetreatGuardText;
            if (legacy != null) return LegacyGraceText;
            return card.KeyEnemy ? null : MayApproachText;
        }

        /// <summary>
        /// 敌踪接近的检查边界（Q416 第 4 条：空闲、到达、强制对白结束、战后回到非战斗；以及每个行动段提交后）：
        /// 修剪失效卡的记录，给首次可操作现身的卡抽接近时间，再看有没有已到期且此刻合法入战的卡（一次只开一场）。返回是否开了战。
        /// </summary>
        private bool EnemyApproachBoundary(GameState s, bool allowStart = true)
        {
            if (!ApproachLive(s)) return false;
            PruneCardTimers(s);
            DrawCardTimers(s);
            return allowStart && TryEnemyApproach(s);
        }

        /// <summary>
        /// 敌踪接近的空闲边界（Q416 第 4 条），给壳层与测试在“战后回到非战斗”等时刻显式调用：抽时限，到期且合法就开一场。返回是否开了战。
        /// 模拟副本（SuppressEnemyApproach）或战斗关闭时什么都不做。
        /// </summary>
        public bool CheckEnemyApproach(GameState s) => EnemyApproachBoundary(s);

        /// <summary>一次战斗指令／推进让战斗结束、回到非战斗时（不在结算途中）：敌踪接近边界，其他到期卡可在此接战（Q416）。</summary>
        private void AfterBattleBoundary(GameState s, BattleState? had)
        {
            if (had != null && s.Battle == null && !s.Progress.Dead) EnemyApproachBoundary(s);
        }

        // ───────────── 同卡撤退保护（R05 Q416、Q442、Q443） ─────────────

        /// <summary>某个世界敌人现在所属的卡键：组员是组卡，固定战／可选强敌实例是各自的卡，其余是普通单卡。</summary>
        public static string CardKeyOfEnemy(WorldEnemy w) =>
            w.GroupId is int gid ? GroupCardKey(gid) : w.FixedOf ?? SingleCardKey(w.Id);

        /// <summary>
        /// 成功撤退后的同卡保护（Q416）：这次交战的同一张敌踪卡获得 encounters.cardRetreatGuardMinutes 的主动接近保护，从撤退结算时刻起算，
        /// 重复撤退取新的截止；离开地点仍按世界时钟走。该卡计时照走，到期后等待保护结束；玩家仍可主动处理。不新增全地点保护。
        /// 无对应卡的旧在途战斗（Q443）：每个参战幸存敌人所属的真实卡各得一份同样的保护（同卡只记一次），不创建全地点保护、不生成敌人。
        /// 可选强敌不主动开战，不记保护。本事件随这次撤退结算只提交一次。
        /// </summary>
        private void GrantRetreatGuard(GameState s, BattleState b)
        {
            var until = s.GameMinute + Config.Encounters.CardRetreatGuardMinutes;
            var keys = new List<string>();
            if (b.CardKey != null) keys.Add(b.CardKey);
            else
                foreach (var e in b.Enemies.Where(e => !e.Dead))
                    if (FindWorldEnemy(s, e.WorldId) is WorldEnemy w) keys.Add(CardKeyOfEnemy(w));
            foreach (var key in keys.Distinct())
            {
                if (!(FindEnemyCard(s, key, out _) is EnemyCardView card) || card.Optional) continue;
                TimerFor(s, key).GuardUntil = until;
            }
        }

        // ───────────── 到期队列与敌方主动开战（R05 Q416、Q441、Q467—Q470） ─────────────

        /// <summary>同刻到期的先后：固定卡→随机组卡→普通单卡（Q468）。</summary>
        private static int CardKindRank(string kind) => kind == "fixed" ? 0 : kind == "group" ? 1 : 2;

        /// <summary>同类稳定编号：数字编号按数值，固定节点等字符串稳定键按序数（Q468；不依赖界面排序、语言或刷新）。</summary>
        private static int CompareStableKey(string a, string b)
        {
            var ia = a.IndexOf(':');
            var ib = b.IndexOf(':');
            var ra = ia < 0 ? a : a.Substring(ia + 1);
            var rb = ib < 0 ? b : b.Substring(ib + 1);
            if (long.TryParse(ra, out var na) && long.TryParse(rb, out var nb)) return na.CompareTo(nb);
            return string.CompareOrdinal(ra, rb);
        }

        /// <summary>
        /// 当前地点、当前区域已到期且没有被保护挡住的计时卡，按 Q468 排好：到期时刻早的在前；同刻固定卡→组卡→单卡；同类稳定编号升序。
        /// 同卡撤退保护未结束、或旧档历史全地点保护未结束（Q425：计时卡到期后等待该期限结束）时不在列。可选强敌不进队列。只读。
        /// </summary>
        public List<EnemyCardView> DueCardsHere(GameState s)
        {
            var list = new List<EnemyCardView>();
            if (s.GameMinute < s.EncounterGraceUntil) return list;
            foreach (var card in EnemyCardsHere(s))
            {
                if (!CardTimed(card) || !(CardTimer(s, card.Key) is EnemyCardTimer t) || t.Left != 0) continue;
                if (t.GuardUntil is int g && g > s.GameMinute) continue;
                list.Add(card);
            }
            list.Sort((x, y) =>
            {
                var tx = CardTimer(s, x.Key)!.DueMinute ?? int.MinValue;
                var ty = CardTimer(s, y.Key)!.DueMinute ?? int.MinValue;
                if (tx != ty) return tx.CompareTo(ty);
                var kx = CardKindRank(x.Kind);
                var ky = CardKindRank(y.Kind);
                return kx != ky ? kx.CompareTo(ky) : CompareStableKey(x.Key, y.Key);
            });
            return list;
        }

        /// <summary>
        /// 主行动的普通休息、普通等待与原子事务（城市服务、招募登记、单段事务）：按本次指定时长整次结束（或合法退出）后才开战（Q467）。
        /// 野营布等部署／收起与 B41c 练习潜行按可暂停多段处理（段后暂停，Q507）。
        /// </summary>
        private static bool WholeBeforeEnemyApproach(ActionState a) =>
            a.Type == ActionType.Rest || (a.Type == ActionType.Wait && !IsCampingTarget(a.TargetId) && !IsStealthPractice(a)) || IsSingleTransaction(a.Type);

        /// <summary>
        /// 此刻的行动边界要不要推迟敌方开战（Q416、Q441、Q467）：有未结束的子行动（暂停中的休息整次做完；补给、点火本就单段）、
        /// 正在移动或撤出（途中不拦截，也不回起点拦截）、主行动是普通休息／等待／原子事务且还没结束（暂停中也算没结束），都推迟。
        /// BM5c（R05 Q605、Q620）：队伍在一项未结束探索的原地点／区域时也推迟（含开工前已到期的卡），探索结束边界再按原顺序检查。
        /// </summary>
        private static bool EnemyApproachDeferred(GameState s)
        {
            if (s.Sub != null) return true;
            // BM5c（R05 Q605 §1、Q620）：同一次未结束探索的原地点／区域，普通到期等这次探索结束再检查
            if (ExploreDefersApproach(s)) return true;
            var m = s.Main;
            if (m == null) return false;
            if (m.Type == ActionType.Travel || m.Type == ActionType.Extract) return true;
            return WholeBeforeEnemyApproach(m);
        }

        /// <summary>
        /// 到期卡在当前边界主动开战（Q416、Q468）：已在战斗、主角终局、未读强制对白、撤出途中都不开；行动边界要推迟的不开（到期卡挂起等待交战）。
        /// 否则取队列第一张开一场，下一张等下一边界重验。
        /// </summary>
        private bool TryEnemyApproach(GameState s)
        {
            if (!ApproachLive(s) || s.Progress.Dead || DialogueLocked(s) || s.Battle != null || s.Expedition?.IsExtracting == true) return false;
            if (EnemyApproachDeferred(s)) return false;
            var card = DueCardsHere(s).FirstOrDefault();
            if (card == null) return false;
            StartEnemyApproach(s, card);
            return true;
        }

        /// <summary>被敌方打断的暂停原因字段（Q444、Q470）：「{敌踪名}主动接近」，取不到名时「敌人主动接近」。</summary>
        public static string EnemyApproachPauseNote(string? cardTitle) =>
            string.IsNullOrEmpty(cardTitle) ? "敌人主动接近" : $"{cardTitle}主动接近";

        /// <summary>
        /// 敌方主动开战（逐字稿「结果、取消与队长回退」：「{敌踪名}主动接近了。」，原行动因此暂停时追加「{原行动名}已暂停，战后可以继续或退出。」）：
        /// 正在进行的可暂停多段主行动在本段已完整提交后暂停，进度、余额、预留与工具锁原样保留（Q416 第 2 条），暂停原因在开战瞬间快照卡名（Q470）；
        /// 原本已经暂停的行动不伪造第二次暂停，原原因保留。战报入战方式前缀一律「敌方接近」（Q444、Q469），受惊情境系数 1.0（Q422）；
        /// 首战、固定战、守卫战的身份留在战斗记录（教学标记、固定战键）与关卡回执里。
        /// </summary>
        private void StartEnemyApproach(GameState s, EnemyCardView card)
        {
            var m = s.Main;
            var text = $"{card.Title}主动接近了。";
            if (m != null && m.Status == ActionStatus.Running)
            {
                m.Status = ActionStatus.Paused;
                m.PauseReason = PauseReason.EnemyApproach;
                m.PauseStat = null;
                m.PauseNote = EnemyApproachPauseNote(card.Title);
                // B41c2（逐字稿 Q517）：开工 5 分钟锁定的行动不承诺退出
                Emit(s, GameEventKind.ActionPaused, text + (ExitLocked(m) ? $"{m.Label}已暂停，战后可以继续。" : $"{m.Label}已暂停，战后可以继续或退出。"));
            }
            else Emit(s, GameEventKind.Encounter, text, null, card.Members.Select(w => $"enemy:{w.Id}"));
            switch (card.Kind)
            {
                case "single":
                {
                    var tutorial = IsTutorialEnemy(s, card.Members[0].Id);
                    StartBattle(s, card.Members, false, EnemyApproachSource, FearEncounter, tutorial, cardKey: card.Key, firstStrike: FirstStrikeEnemy);
                    if (tutorial) TutorialBattleStarted(s);
                    break;
                }
                case "group":
                    StartBattle(s, card.Members.Where(w => !Hidden(s, w)).ToList(), false, EnemyApproachSource, FearEncounter, cardKey: card.Key, firstStrike: FirstStrikeEnemy);
                    break;
                case "fixed":
                    StartBattle(s, card.Members, false, EnemyApproachSource, FearEncounter, fixedKey: card.Key, cardKey: card.Key, firstStrike: FirstStrikeEnemy);
                    break;
            }
        }

        /// <summary>敌方到期主动开战的战报入战方式前缀（Q444、Q469）。</summary>
        public const string EnemyApproachSource = "敌方接近";

        /// <summary>
        /// 教学首战实际开始（主动接近、潜行接近或敌方到期；R05 Q421、S15 B41 首战入口）：记教学战斗号；第一次真正开战时播原首战警告对白与
        /// PRO04 序章引导（顺序同旧到达开战：战斗建立后先展示警告，读完才接受第一条战斗指令），撤退后再进教学战不重播。
        /// </summary>
        private void TutorialBattleStarted(GameState s)
        {
            if (!s.Story.DialogueLog.Any(d => d.EventId == DialogueTutorialArrive))
            {
                OpenDialogue(s, EventOf(DialogueTutorialArrive));
                TutorialBattleGuide(s);
            }
            s.Story.TutorialBattleId = s.Battle?.BattleId;
        }

        // ───────────────────────── 战后撤退离地保留暂停（R05 Q440、Q463—Q466、Q472—Q474） ─────────────────────────

        /// <summary>某地点所属地牢的当前代号；不在地牢里为 null。</summary>
        private int? DungeonGenerationAt(GameState s, string locationId) =>
            DungeonAt(locationId) is string d ? (s.DungeonInstances.TryGetValue(d, out var inst) ? inst.Generation : 0) : (int?)null;

        /// <summary>开工时记下所在区、地牢世代与每个预留批次的开工来源（真实物主或地面地点／区域）；B41e3 起所有新开工都记（Q440、Q466）。</summary>
        private void RecordWorkOrigin(GameState s, ActionState a)
        {
            a.StartZone = s.CurrentZone;
            a.StartGeneration = DungeonGenerationAt(s, a.SourceLocation);
            BackfillReserveSources(s, a);
        }

        /// <summary>给还没有来源记录的预留批次补来源：取它此刻的真实归属（预留只打标记不搬物；Q466 旧档补齐也走这里）。已记的不改写。</summary>
        private void BackfillReserveSources(GameState s, ActionState a)
        {
            foreach (var id in a.ReservedItemIds)
            {
                if (a.ReserveSources != null && a.ReserveSources.ContainsKey(id)) continue;
                var inst = FindItem(s, id);
                if (inst == null) continue;
                (a.ReserveSources ??= new Dictionary<int, string>())[id] = inst.Owner;
            }
        }

        /// <summary>
        /// 撤退成功、队伍落地之后：主行动已经暂停且队伍已离开它的原地点／区域，就按 Q440 保留这次暂停（不限最初暂停原因，Q463）——
        /// 记保留标记、开放地图移动（Q464、Q473），旧行动缺的区域／世代／来源在这里按仍在原处的真实状态补齐（Q466）。移动、撤出不适用。
        /// </summary>
        private void KeepPausedWorkOnRetreat(GameState s)
        {
            var a = s.Main;
            if (a == null || a.IsSub || a.Status != ActionStatus.Paused || a.Type is ActionType.Travel or ActionType.Extract) return;
            // 暂停期间不能进出安全屋、不能移动（除本例外），开战只在屋外：旧行动缺区域时就是屋外，缺世代时取原地点当前世代（撤退不换代）
            a.StartZone ??= ExteriorZone;
            if (a.StartGeneration == null) a.StartGeneration = DungeonGenerationAt(s, a.SourceLocation);
            if (s.CurrentLocation == a.SourceLocation && s.CurrentZone == a.StartZone) return;
            BackfillReserveSources(s, a);
            a.RetreatKept = true;
            a.RetreatMoveOpen = true;
        }

        /// <summary>移动到达或退出后把暂存的原工作放回主行动位（仍暂停、不自动继续）；没有暂存返回 null（Q472 第 4、5 条）。</summary>
        private ActionState? RestoreStashedWork(GameState s)
        {
            var w = s.StashedWork;
            if (w == null || s.Main != null) return null;
            s.Main = w;
            s.StashedWork = null;
            return w;
        }

        /// <summary>移动到达／退出结果末尾追加一次的事实分句（逐字稿 Q472 表）。</summary>
        private static string StillPausedText(ActionState w) => $"{w.Label}仍保持暂停。";

        /// <summary>原行动地点／区域的公开名称（屋外为地点名，屋内为安全屋名）。</summary>
        public string WorkPlaceName(GameState s, ActionState a) => ZoneName(s, ZonePlace(a.SourceLocation, a.StartZone ?? ExteriorZone));

        /// <summary>
        /// 战后撤退离地保留的原工作继续前的地点与世代校验（Q440）：原地牢世代失效「原行动地点已失效，无法继续。」，
        /// 人不在原地点／区域「请返回{地点}后再继续{行动名}。」。其余工作不受这里限制。
        /// </summary>
        private string? KeptWorkResumeBlock(GameState s, ActionState a)
        {
            if (!a.RetreatKept || a.Type == ActionType.Travel) return null;
            if (a.StartGeneration is int g && DungeonGenerationAt(s, a.SourceLocation) != g) return "原行动地点已失效，无法继续。";
            if (s.CurrentLocation != a.SourceLocation || s.CurrentZone != (a.StartZone ?? ExteriorZone))
                return $"请返回{WorkPlaceName(s, a)}后再继续{a.Label}。";
            return null;
        }

        /// <summary>
        /// 按开工来源返还预留（Q466）：原随身部分解除预留留在原物主随身，原地面部分回原行动地点／区域地面；不按当前队伍地点改落点。
        /// 返回实际返还的随身批数、地面落点（给 Q471 文案）。
        /// </summary>
        private (bool carried, string? groundPlace) ReleaseToSources(GameState s, ActionState a)
        {
            var carried = false;
            string? ground = null;
            var owners = new HashSet<string>();
            foreach (var id in a.ReservedItemIds)
            {
                var inst = FindItem(s, id);
                if (inst == null) continue;
                inst.ReservedBy = null;
                var src = a.ReserveSources != null && a.ReserveSources.TryGetValue(id, out var o) ? o : inst.Owner;
                if (TryPlace(src, out _, out _))
                {
                    inst.Owner = src;
                    ground ??= src;
                }
                else carried = true;
                owners.Add(inst.Owner);
            }
            a.ReservedItemIds.Clear();
            a.ReserveSources = null;
            foreach (var o in owners) Normalize(s, o);
            return (carried, ground);
        }
    }
}

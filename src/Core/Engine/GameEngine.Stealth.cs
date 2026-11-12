using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41c 新版敌踪的小队队长、潜行接近／躲避、练习潜行（R05「队长、潜行与 Q424 练习」「潜行与躲避判定」「B41c Q503—Q508」，
    /// R08「Q342 小队队长」；逐字稿 B41「结果、取消与队长回退」「新操作的禁用与拒绝」「队长与练习潜行」；开发记录 B41c）。
    /// 全部挂在 <see cref="CardsV2"/> 后面：开关 encounters.enemyCardsV2 关着时没有队长、潜行行动与潜行经验，旧版流程原样。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── 小队队长（R08 Q342；R05「队长、潜行与 Q424 练习」） ─────────────

        /// <summary>
        /// 某人此刻能不能当队长（或当练习者）：主角存活；同行者存活、仍在同行队伍里（同行者与主角同地同区）。
        /// </summary>
        public bool SquadMemberValid(GameState s, string actorId)
        {
            if (s.Progress.Dead) return false;
            if (actorId == GameState.PlayerId) return true;
            if (!s.Party.Contains(actorId) || !s.Actors.TryGetValue(actorId, out var a)) return false;
            if (a.Stats.Health <= 0 || RosterOf(s, actorId)?.Dead == true) return false;
            return true;
        }

        /// <summary>当前小队队长（缺省与失效都为主角）。</summary>
        public string SquadLeaderId(GameState s) =>
            s.SquadLeader != null && SquadMemberValid(s, s.SquadLeader) ? s.SquadLeader : GameState.PlayerId;

        /// <summary>
        /// 有人死亡或离开同行队伍后（原死亡／离队结果已写）：他是队长时立即改回主角，主角仍存活时追加回退句；
        /// 他是进行中练习潜行的练习者时收束那次练习（Q508）。重复调用不重复写。
        /// </summary>
        private void AfterSquadLeave(GameState s, string actorId)
        {
            if (s.SquadLeader == actorId && !SquadMemberValid(s, actorId))
            {
                s.SquadLeader = null;
                if (!s.Progress.Dead && CardsV2(s)) Emit(s, GameEventKind.Info, "原队长已无法带队，小队队长已改为你。");
            }
            if (!SquadMemberValid(s, actorId)) EndPracticeOf(s, actorId);
        }

        // ───────────── 潜行接近／躲避（R05「潜行与躲避判定」「Q420」「Q505」「Q506」；逐字稿 B41「结果、取消与队长回退」「新操作的禁用与拒绝」） ─────────────

        /// <summary>潜行行动的等待目标前缀："stealth:sneak:&lt;卡键&gt;"、"stealth:evade:&lt;卡键&gt;"、"stealth:practice:&lt;练习者&gt;"。</summary>
        public const string StealthTargetPrefix = "stealth:";
        public const string SneakKind = "sneak";
        public const string EvadeKind = "evade";
        public const string PracticeKind = "practice";
        public const string StealthSkill = "stealth";

        /// <summary>这个等待目标是不是潜行行动（三种之一）。</summary>
        public static bool IsStealthTarget(string targetId) => targetId.StartsWith(StealthTargetPrefix);

        private static string StealthKindOf(string targetId)
        {
            var rest = targetId.Substring(StealthTargetPrefix.Length);
            var i = rest.IndexOf(':');
            return i < 0 ? rest : rest.Substring(0, i);
        }

        /// <summary>潜行接近／躲避（5 分钟判定）：开工即不可中途退出（Q506，沿自用／点火／融合的不可取消机制）。</summary>
        public static bool IsStealthCheck(ActionState a)
        {
            if (a.Type != ActionType.Wait || !IsStealthTarget(a.TargetId)) return false;
            var k = StealthKindOf(a.TargetId);
            return k == SneakKind || k == EvadeKind;
        }

        /// <summary>练习潜行（30 分钟，可暂停、可退出）。</summary>
        public static bool IsStealthPractice(ActionState a) =>
            a.Type == ActionType.Wait && IsStealthTarget(a.TargetId) && StealthKindOf(a.TargetId) == PracticeKind;

        /// <summary>行动名（逐字稿：「潜行接近」「躲避」「练习潜行」）。</summary>
        public static string StealthActionName(string kind) => kind == SneakKind ? "潜行接近" : kind == EvadeKind ? "躲避" : "练习潜行";

        /// <summary>
        /// 成功率（整数百分比，R05「潜行与躲避判定」）：Δ = 5×(潜行等级−警惕) + 0.25×(清醒−90) + 0.20×(精神−60)；
        /// 潜行接近 round(clamp(50+Δ,10,90))，躲避 round(clamp(60+Δ,15,95))，0.5 向上。预览和结算同公式。
        /// </summary>
        public int StealthRate(string kind, int level, double wakefulness, double morale, int vigilance)
        {
            var c = Config.Encounters.Stealth;
            var d = c.LevelFactor * (level - vigilance) + c.WakefulnessFactor * (wakefulness - c.WakefulnessRef) + c.MoraleFactor * (morale - c.MoraleRef);
            var raw = kind == SneakKind ? Math.Max(c.SneakMin, Math.Min(c.SneakMax, c.SneakBase + d)) : Math.Max(c.EvadeMin, Math.Min(c.EvadeMax, c.EvadeBase + d));
            return (int)Math.Floor(raw + 0.5 + 1e-9);
        }

        /// <summary>按某人此刻的潜行等级、清醒、精神对某警惕的成功率。</summary>
        public int StealthRate(GameState s, string kind, string actorId, int vigilance)
        {
            var a = s.Actors[actorId];
            return StealthRate(kind, SkillLevel(a, StealthSkill), a.Stats.Wakefulness, a.Stats.Morale, vigilance);
        }

        /// <summary>
        /// 潜行接近／躲避的拒绝原因：逐字稿「新操作的禁用与拒绝」自上而下取第一条（与主动接近共用前面各行，再加队长、固定关键敌躲避与快照已变）。
        /// leaderId／expectedRate 是界面提交时看到的队长与成功率（不传不比对）。可以开始为 null。只读、不抽签。
        /// </summary>
        public string? CanStealthCard(GameState s, string cardKey, string kind, string? leaderId = null, int? expectedRate = null)
        {
            if (!CardsV2(s) || (kind != SneakKind && kind != EvadeKind)) return "当前无法发起战斗。";
            var why = CanApproachCard(s, cardKey);
            if (why != null) return why;
            if (leaderId != null && !SquadMemberValid(s, leaderId)) return "队长已失效，请重新选择。";
            var card = FindEnemyCard(s, cardKey, out _)!;
            if (kind == EvadeKind && card.KeyEnemy) return "这是必须战胜的敌踪，无法躲避。";
            var leader = SquadLeaderId(s);
            if (leaderId != null && leaderId != leader) return "当前情况已变化，请重新选择后再操作。";
            if (expectedRate != null && expectedRate.Value != StealthRate(s, kind, leader, card.Vigilance)) return "当前情况已变化，请重新选择后再操作。";
            return null;
        }

        /// <summary>
        /// 开始潜行接近／躲避（盘内点击即提交 5 世界分钟行动，身体按等待费率）：开工时快照队长身份、清醒、精神、潜行等级、目标警惕、成功率与本次现身号；
        /// 拒绝不耗时、不抽签。requestId 重放不另开。
        /// </summary>
        public ActionResult StartStealth(GameState s, string cardKey, string kind, string? requestId = null, string? leaderId = null, int? expectedRate = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = CanStealthCard(s, cardKey, kind, leaderId, expectedRate);
            if (why != null) return ActionResult.Fail(why);
            var card = FindEnemyCard(s, cardKey, out _)!;
            var leader = SquadLeaderId(s);
            var actor = s.Actors[leader];
            var snap = new StealthCheck
            {
                Kind = kind, CardKey = cardKey, ActorId = leader, CardTitle = card.Title,
                Level = SkillLevel(actor, StealthSkill), Wakefulness = actor.Stats.Wakefulness, Morale = actor.Stats.Morale,
                Vigilance = card.Vigilance, Appearance = AppearanceOf(s, cardKey),
            };
            snap.Rate = StealthRate(kind, snap.Level, snap.Wakefulness, snap.Morale, snap.Vigilance);
            var req = ActionRequest.Wait(Config.Encounters.Stealth.CheckMinutes);
            req.TargetId = StealthTargetPrefix + kind + ":" + cardKey;
            req.RequestId = requestId;
            var res = Start(s, req);
            if (!res.Ok) return res;
            s.Main!.Stealth = snap;
            return res;
        }

        /// <summary>某张卡本次现身的序号（躲避隐藏后真正重现一次 +1；没有记录为 0）。</summary>
        public int AppearanceOf(GameState s, string cardKey) => s.EnemyCards.Appearances.TryGetValue(cardKey, out var n) ? n : 0;

        /// <summary>Validate 的潜行等待分支：时长取配置（判定 5、练习 30），行动名取逐字稿；开关关着不认识这种行动。</summary>
        private string? ValidateStealth(GameState s, ActionRequest r, Plan plan)
        {
            if (!CardsV2(s)) return "未知行动";
            var kind = StealthKindOf(r.TargetId);
            var minutes = kind == PracticeKind ? Config.Encounters.Stealth.PracticeMinutes : kind == SneakKind || kind == EvadeKind ? Config.Encounters.Stealth.CheckMinutes : -1;
            if (minutes <= 0 || r.Minutes != minutes) return "等待时长无效";
            plan.Planned = minutes;
            plan.Label = StealthActionName(kind);
            return null;
        }

        /// <summary>
        /// 潜行接近／躲避段末结算（R05「潜行与躲避判定」）：主角存活且快照队长、目标仍合法时只抽一次 0–99，小于成功率为成功；
        /// 队长失效或敌踪离开则取消（不抽签、不发经验，已耗时间保留）。潜行成功进入我方先手战斗，两种失败进入正常战斗，躲避成功隐藏整卡。
        /// 返回 false 为取消。
        /// </summary>
        private bool CompleteStealthCheck(GameState s, ActionState a)
        {
            var st = a.Stealth!;
            var name = StealthActionName(st.Kind);
            if (!SquadMemberValid(s, st.ActorId))
            {
                Emit(s, GameEventKind.ActionCancelled, $"本次{name}已取消。队长已失效，请重新选择。已耗时{Clock.Duration(a.ElapsedMinutes)}。");
                return false;
            }
            var card = FindEnemyCard(s, st.CardKey, out _);
            if (card == null || card.LocationId != s.CurrentLocation || s.CurrentZone == SafehouseZone || card.Hidden || card.Engaged
                || (st.Kind == EvadeKind && card.KeyEnemy))
            {
                Emit(s, GameEventKind.ActionCancelled, $"本次{name}已取消。这处敌踪已无法处理。已耗时{Clock.Duration(a.ElapsedMinutes)}。");
                return false;
            }
            var roll = (int)Math.Min(99, Math.Floor(s.Rng.NextDouble() * 100));
            var ok = roll < st.Rate;
            var c = Config.Encounters.Stealth;
            var xp = StealthXpLine(s, st, ok ? c.XpSuccess : c.XpFail);
            var leader = ActorDisplayName(s, st.ActorId);
            if (st.Kind == SneakKind)
            {
                Emit(s, GameEventKind.ActionCompleted, (ok ? $"{leader}潜行接近{card.Title}成功，获得先手。" : $"{leader}潜行接近{card.Title}失败，进入战斗。") + xp);
                // R05 Q444：战报入战方式前缀 潜行成功「潜行接近」、潜行失败「潜行失败」、躲避失败「躲避失败」
                StartCardBattle(s, card, ok ? "潜行接近" : "潜行失败", FearApproach, ok ? FirstStrikeAlly : null);
            }
            else if (ok)
            {
                Emit(s, GameEventKind.ActionCompleted, "你避开了这处敌踪。" + xp);
                HideCardByEvade(s, card);
            }
            else
            {
                Emit(s, GameEventKind.ActionCompleted, $"{leader}未能避开{card.Title}，进入战斗。" + xp);
                StartCardBattle(s, card, "躲避失败", FearEncounter, null);
            }
            return true;
        }

        /// <summary>
        /// 潜行判定经验的追加句（Q505）：本现身还没领过且实际可增加（按快照队长与原成长封顶）大于 0 时，原子提交经验与领取回执，写「+实际经验」；
        /// 否则满级写满级句，已领写已获得句（优先顺序：实际经验、满级、已获得；只取一条）。
        /// </summary>
        private string StealthXpLine(GameState s, StealthCheck st, int amount)
        {
            var actor = s.Actors[st.ActorId];
            var who = ActorDisplayName(s, st.ActorId);
            var claim = st.CardKey + "@" + st.Appearance;
            var claimed = s.EnemyCards.StealthXpClaims.Contains(claim);
            if (!claimed)
            {
                var actual = Math.Min(amount, XpRoomToCap(actor, StealthSkill));
                if (actual > 0)
                {
                    GainXp(s, actor, StealthSkill, actual);
                    s.EnemyCards.StealthXpClaims.Add(claim);
                    return $"{who}潜行经验+{actual}。";
                }
            }
            if (SkillLevel(actor, StealthSkill) >= SkillCap(StealthSkill)) return $"{who}的潜行已满级。";
            return claimed ? "本次现身的潜行经验已获得。" : "";
        }

        /// <summary>某人某门离满级还能获得多少经验（已满为 0）。</summary>
        public long XpRoomToCap(ActorState actor, string id)
        {
            var lv = SkillLevel(actor, id);
            var cap = SkillCap(id);
            if (lv >= cap) return 0;
            long room = 0;
            for (var l = lv; l < cap; l++) room = checked(room + NextLevelXp(id, l));
            return room - (actor.Skills.TryGetValue(id, out var sk) ? sk.Xp : 0);
        }

        /// <summary>
        /// 按卡开一场战（与主动接近同一套名单）：单卡打这一只（教学首战仍记教学），随机组卡锁定名单里未隐藏的成员，固定战全名单（禁增援），
        /// 可选强敌第一次实际处理时才建唯一敌人并锁定掉落抽签。firstStrike 为先手方（R03 Q422）。
        /// </summary>
        private void StartCardBattle(GameState s, EnemyCardView card, string source, string fear, string? firstStrike)
        {
            switch (card.Kind)
            {
                case "single":
                {
                    var tutorial = IsTutorialEnemy(s, card.Members[0].Id);
                    StartBattle(s, card.Members, false, source, fear, tutorial, cardKey: card.Key, firstStrike: firstStrike);
                    if (tutorial) TutorialBattleStarted(s);
                    break;
                }
                case "group":
                    StartBattle(s, card.Members.Where(w => !Hidden(s, w)).ToList(), false, source, fear, cardKey: card.Key, firstStrike: firstStrike);
                    break;
                case "fixed":
                    StartBattle(s, card.Members, false, source, fear, fixedKey: card.Key, cardKey: card.Key, firstStrike: firstStrike);
                    break;
                default:
                {
                    var id = card.Key.Substring(ElitePrefix.Length);
                    StartBattle(s, new List<WorldEnemy> { EnsureEliteEnemy(s, EliteConfig(id)!) }, false, source, fear, fixedKey: card.Key, cardKey: card.Key, firstStrike: firstStrike);
                    break;
                }
            }
        }

        /// <summary>
        /// 躲避成功隐藏整卡（R05 Q420）：成员、实例、HP、奖励与经验记录保留；隐藏时长在 720—1440 分钟间按 5 分钟一档含两端等概率抽一次（整卡共用）。
        /// 首次隐藏时按当时所在地点锁定每名成员的原人口账（Q503）。本次现身的接近时限与撤退保护随之结束（重现为新一次现身）。
        /// 可选强敌还没有敌人实例时先建唯一实例（第一次实际处理）。
        /// </summary>
        private void HideCardByEvade(GameState s, EnemyCardView card)
        {
            var members = card.Kind == "elite"
                ? new List<WorldEnemy> { EnsureEliteEnemy(s, EliteConfig(card.Key.Substring(ElitePrefix.Length))!) }
                : card.Kind == "group" ? card.Members.Where(w => !Hidden(s, w)).ToList() : card.Members;
            var c = Config.Encounters.Stealth;
            var steps = (c.HideMaxMinutes - c.HideMinMinutes) / c.HideStepMinutes + 1;
            var until = s.GameMinute + c.HideMinMinutes + c.HideStepMinutes * (int)Math.Min(steps - 1, Math.Floor(s.Rng.NextDouble() * steps));
            foreach (var w in members)
            {
                w.PopAccount ??= PopAccountOf(w);
                w.EvadeHiddenUntil = until;
            }
            s.EnemyCards.Timers.Remove(card.Key);
        }

        // ───────────── 练习潜行（R05「队长、潜行与 Q424 练习」「Q504」「Q507—Q508」；逐字稿 B41「队长与练习潜行」） ─────────────

        /// <summary>
        /// 当前区域有没有可行动的敌对敌踪（Q424、Q504）：同地同区未隐藏、未交战、玩家此刻可处理的卡——首战门控卡与普通中立单卡不算；
        /// 固定关键敌、可选强敌告示（尚未生成敌人实例也算）都算。只读卡视图，不创建实例、不抽签。
        /// </summary>
        public bool ActionableHostileCardHere(GameState s) =>
            EnemyCardsHere(s).Any(c => !c.TutorialGated && !(c.Kind == "single" && !c.KeyEnemy && c.Members.Count > 0 && !c.Members[0].Hostile));

        /// <summary>
        /// 练习潜行的拒绝原因（逐字稿「新操作的禁用与拒绝」表自上而下，取与练习有关的行）。可以开始为 null。只读、不抽签。
        /// </summary>
        public string? CanPracticeStealth(GameState s, string? actorId)
        {
            if (!CardsV2(s)) return "未知行动";
            if (s.Progress.Dead) return "本局已结束。";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return "当前正在战斗。";
            if (s.Expedition?.IsExtracting == true) return "撤出途中无法开始这项行动。";
            if (s.Sub != null || (s.Main != null && s.Main.Status != ActionStatus.Paused)) return "当前行动还没结束。";
            if (s.Main != null) return "行动暂停中，请先继续或退出行动。";
            if (DuplicateWeaponsIllegal(s) || CarriedOverflow(s)) return "请先完成随身物品整理。";
            if (s.Recruit.Pending?.AwaitingPlacement == true) return "请先办完当前事务。";
            if (string.IsNullOrEmpty(actorId) || !s.Actors.ContainsKey(actorId!)) return "请选择练习者。";
            if (actorId != GameState.PlayerId)
            {
                if (s.Actors[actorId!].Stats.Health <= 0 || RosterOf(s, actorId!)?.Dead == true) return "练习者已死亡，请重新选择。";
                if (!s.Party.Contains(actorId!)) return "练习者已不在同行队伍中，请重新选择。";
            }
            if (ActionableHostileCardHere(s)) return "当前区域有可行动的敌对敌踪，无法练习潜行。";
            return null;
        }

        /// <summary>
        /// 开始练习潜行（30 世界分钟，按等待身体费率，无材料）：明确选练习者（主角或同地同区存活同行者）。开工日志「开始练习潜行。」。
        /// 条件只在开工检查；途中巡入敌踪不取消，继续时也不重查（Q504）。
        /// </summary>
        public ActionResult StartPractice(GameState s, string? actorId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = CanPracticeStealth(s, actorId);
            if (why != null) return ActionResult.Fail(why);
            var req = ActionRequest.Wait(Config.Encounters.Stealth.PracticeMinutes);
            req.TargetId = StealthTargetPrefix + PracticeKind + ":" + actorId;
            req.RequestId = requestId;
            var res = Start(s, req);
            if (!res.Ok) return res;
            s.Main!.Stealth = new StealthCheck { Kind = PracticeKind, ActorId = actorId! };
            return res;
        }

        /// <summary>练习盘的经验预览（逐字稿：「完成后{练习者}潜行经验+{实际预估经验}。」／满级句）；练习者不存在为 null。</summary>
        public string? PracticeXpPreview(GameState s, string actorId)
        {
            if (!s.Actors.TryGetValue(actorId, out var a)) return null;
            var who = ActorDisplayName(s, actorId);
            var actual = Math.Min(Config.Encounters.Stealth.PracticeXp, XpRoomToCap(a, StealthSkill));
            return actual > 0 ? $"完成后{who}潜行经验+{actual}。" : $"{who}的潜行已满级，完成后不再获得经验。";
        }

        /// <summary>累计实际完成 30 分钟：给练习者 20 潜行经验（按原成长封顶，满级不另加）。</summary>
        private bool CompletePractice(GameState s, ActionState a)
        {
            var id = a.Stealth?.ActorId ?? "";
            if (!s.Actors.TryGetValue(id, out var actor)) return false;
            var who = ActorDisplayName(s, id);
            var actual = Math.Min(Config.Encounters.Stealth.PracticeXp, XpRoomToCap(actor, StealthSkill));
            if (actual > 0)
            {
                GainXp(s, actor, StealthSkill, actual);
                Emit(s, GameEventKind.ActionCompleted, $"{who}完成潜行练习，潜行经验+{actual}。");
            }
            else Emit(s, GameEventKind.ActionCompleted, $"{who}完成潜行练习，潜行已满级。");
            return true;
        }

        /// <summary>练习退出／中止句（Q508）：时长只算已实际提交的练习段。</summary>
        private string PracticeEndText(GameState s, ActionState a) =>
            $"{ActorDisplayName(s, a.Stealth?.ActorId ?? "")}的潜行练习已结束。已耗时{Clock.Duration(a.ElapsedMinutes)}，未获得经验。";

        /// <summary>
        /// 练习者死亡／离队（Q508）：主角尚未终局时，在原结果之后收束他正在进行（运行、暂停或暂存）的那次练习——释放占用、不再恢复、不给经验，
        /// 追加一次中止句。主角终局只走终局呈现。
        /// </summary>
        private void EndPracticeOf(GameState s, string actorId)
        {
            if (s.Progress.Dead) return;
            foreach (var a in new[] { s.Main, s.StashedWork })
            {
                if (a == null || !IsStealthPractice(a) || a.Stealth?.ActorId != actorId) continue;
                Release(s, a, s.Ground);
                if (ReferenceEquals(s.Main, a)) s.Main = null;
                else s.StashedWork = null;
                Emit(s, GameEventKind.ActionCancelled, PracticeEndText(s, a));
            }
        }

        public const string PopAccountLegacy = "legacy";
        public const string PopAccountRegion = "region:";
        public const string PopAccountNone = "none";

        /// <summary>
        /// 某名敌人的原人口账（Q503）："legacy" 旧 8 地点总账、"region:&lt;ID&gt;" 人口区账、"none" 不计（固定／可选强敌等原来就不计随机人口的，
        /// 以及不在任何人口账地点的）。
        /// </summary>
        private string PopAccountOf(WorldEnemy w)
        {
            if (w.FixedOf != null) return PopAccountNone;
            var r = PopRegionOf(w.LocationId);
            if (r != null) return PopAccountRegion + r.Id;
            return InLegacyPopulation(w.LocationId) ? PopAccountLegacy : PopAccountNone;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>任务册与岗位“委托”区里的一项任务（只读视图，0 分钟）。</summary>
    public sealed class QuestView
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>main / side / recruit。</summary>
        public string Kind { get; set; } = "";
        /// <summary>“主线”“委托”“同行邀约”。</summary>
        public string KindLabel { get; set; } = "";
        public string NpcId { get; set; } = "";
        public string NpcName { get; set; } = "";
        public string LocationName { get; set; } = "";
        /// <summary>locked（已知但前置未满足）/ available（可接）/ active（进行中）/ done（已完成）。</summary>
        public string Status { get; set; } = "";
        public string StatusText { get; set; } = "";
        /// <summary>下一步（完成后为结果短句）。</summary>
        public string Step { get; set; } = "";
        /// <summary>可接但现在接不了的原因；可接（且在任务条目里接）时 null。</summary>
        public string? AcceptBlocked { get; set; }
        /// <summary>在任务条目里点“接取”（与岗位交谈同效）；接取要办服务或开始工作的为 false。</summary>
        public bool AcceptHere { get; set; }
        /// <summary>任务条目里的工作按钮（0 分钟阅读、CH01 核对、RN01 登记）；没有为 null。</summary>
        public string? WorkLabel { get; set; }
        public string? WorkBlocked { get; set; }
        public int WorkTotal { get; set; }
        public int WorkDone { get; set; }
        public string? ChoiceLabel { get; set; }
        public int? CompletedAt { get; set; }
        public bool Retro { get; set; }
        /// <summary>完成后可回看的对白（回看 0 分钟，不重发效果）。</summary>
        public bool CanReview { get; set; }
        /// <summary>AC02 外部医疗线索可回看。</summary>
        public bool HasNote { get; set; }
        /// <summary>进行中时的工作提示（narrative-texts XX_work 系统句，预览说明，不弹对白；B15b Q161 选 C：八条委托各自的定稿句，任务册与岗位事项同显）。</summary>
        public string WorkHint { get; set; } = "";
    }

    /// <summary>
    /// 主线与支线任务（开发计划 v0.9 B15，R46、R47；A11「4 主线任务骨架修订与容量」「5 支线委托最小表」「7 招募与补充任务表」、
    /// A08 门槛规则 6/7、S15「gate事实接口」、narrative-texts 8 支线与主线场景）。
    /// <list type="bullet">
    /// <item>任务事实写 Progress.Flags 的 quest:ID:accepted / quest:ID:completed（N01 §3），门槛与前置都读它；任务账（时间、分支、工作分钟）在 <see cref="QuestState"/>。</item>
    /// <item>主线：FA01 到营区广场且已登记即说明；FA02/FA03 在接待处两选择同写路线事实；首次核心记录（FA02/FA03_record_credit）即完成，
    /// 容量 C3/C4 沿原记录事实；FA04 两份记录到齐后营区广场 wait10（两段，中断保段）写 FA04_merged；FA05 已领通信电源后与罗铮交谈接取，联络日志（chapter_contact）完成。</item>
    /// <item>支线：与岗位交谈接取（播 offer）；0 分钟的阅读当场完成，10 分钟的核对/抄录走城市服务或任务条目里的 wait，按 5 分钟段，完成播交付对白；回看不重奖。</item>
    /// <item>招募（B15a，Q152 选 A、Q156 选 B）：RN01—RN06 各在自己的地点、前置满足时出同行邀约卡；“交谈”打开本人逐字邀约，读完选“登记同行”
    /// （原子完成任务、入名册候补、首次两件装备只生成一次、不自动编队、满员也能登记）或“暂不登记”（不完成、可再谈）；0 分钟 0 物资、不计经验。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>CH01 这类没有城市服务的任务工作，借城市计时服务通道办理时用的服务 ID 前缀。</summary>
        public const string QuestWorkServicePrefix = "questwork_";
        /// <summary>h03 战术训练（R03 §3）挂在罗铮事项里的服务 ID。</summary>
        public const string TrainingServiceId = "tactical_training";
        public const string Ch02BackupChoice = "CH02_backup_hub";

        public QuestConfig? Quest(string id) => Config.Quests.List.FirstOrDefault(q => q.Id == id);

        public static bool QuestAccepted(GameState s, string id) => s.Progress.Flags.Contains(QuestFact(id, "accepted"));

        public static bool QuestDone(GameState s, string id) => s.Progress.Flags.Contains(QuestFact(id, "completed"));

        private IEnumerable<QuestConfig> LiveQuests => Config.Quests.List.Where(q => q.Implemented);

        /// <summary>某条前置事实的玩家说法（门槛说明表；任务事实写“接取/完成「任务名」”）。</summary>
        private string RequirementText(string fact)
        {
            if (fact.StartsWith(QuestFactPrefix, StringComparison.Ordinal) && !Config.Map.FactLabels.ContainsKey(fact))
            {
                var parts = fact.Split(':');
                var q = parts.Length == 3 ? Quest(parts[1]) : null;
                if (q != null) return $"{(parts[2] == "accepted" ? "接取" : "完成")}“{q.Title}”";
            }
            return FactLabel(fact);
        }

        /// <summary>接取前置不满足的第一条原因（“需要：…”）；满足为 null。</summary>
        private string? QuestRequiresBlocked(GameState s, QuestConfig q)
        {
            var miss = q.RequiresAll.FirstOrDefault(f => !s.Progress.Flags.Contains(f));
            if (miss != null) return "需要：" + RequirementText(miss);
            if (q.RequiresAny.Count > 0 && !q.RequiresAny.Any(s.Progress.Flags.Contains)) return "需要：" + AnyRequirementText(q.RequiresAny);
            foreach (var kv in q.RequiresExplore)
            {
                var now = ExploreOf(s, kv.Key, null);
                if (now < kv.Value) return $"需要：把{LocName(kv.Key)}探索到{kv.Value}%（现在{now}%）";
            }
            if (q.RequiresRosterMember && !s.Roster.Any(r => r.Present)) return "需要：名册里有一名同行者";
            return null;
        }

        /// <summary>“任一成立”前置的玩家说法：同一任务的接取/完成两条合并为“接取或完成「任务名」”，其余用“，或”连接。</summary>
        private string AnyRequirementText(List<string> any)
        {
            var parts = new List<string>();
            foreach (var f in any)
            {
                var p = f.Split(':');
                var isQuest = p.Length == 3 && f.StartsWith(QuestFactPrefix, StringComparison.Ordinal);
                if (isQuest && p[2] == "completed" && any.Contains(QuestFact(p[1], "accepted"))) continue;
                if (isQuest && p[2] == "accepted" && any.Contains(QuestFact(p[1], "completed")) && Quest(p[1]) is QuestConfig q)
                    parts.Add($"接取或完成“{q.Title}”");
                else parts.Add(RequirementText(f));
            }
            return string.Join("，或", parts);
        }

        private bool QuestShown(GameState s, QuestConfig q)
        {
            if (!q.Implemented) return false;
            if (QuestAccepted(s, q.Id) || QuestDone(s, q.Id)) return true;
            return q.ShowAny.Count == 0 ? QuestRequiresBlocked(s, q) == null : q.ShowAny.Any(s.Progress.Flags.Contains);
        }

        private string NpcNameOf(string npcId) => CityNpc(npcId)?.Name ?? Config.Party.Named(npcId)?.Name ?? npcId;

        /// <summary>任务的当前视图（未显示的为 null）。</summary>
        public QuestView? QuestViewOf(GameState s, string id)
        {
            var q = Quest(id);
            if (q == null || !QuestShown(s, q)) return null;
            var rec = s.Quests.Records.TryGetValue(q.Id, out var r) ? r : null;
            var done = QuestDone(s, q.Id);
            var acc = QuestAccepted(s, q.Id);
            var v = new QuestView
            {
                Id = q.Id, Title = q.Title, Kind = q.Kind, NpcId = q.NpcId, NpcName = NpcNameOf(q.NpcId), LocationName = LocName(q.LocationId),
                KindLabel = KindWord(q),
                WorkTotal = q.WorkMinutes, WorkDone = done ? q.WorkMinutes : rec?.WorkMinutes ?? 0,
                CompletedAt = rec?.CompletedAt, Retro = rec?.Retro ?? false,
                ChoiceLabel = rec?.Choice == null ? null : ChoiceLabelOf(rec.Choice),
            };
            if (done)
            {
                v.Status = "done";
                v.StatusText = "已完成";
                v.Step = q.Result;
                v.CanReview = !string.IsNullOrEmpty(q.RepeatDialogue ?? q.CompleteDialogue ?? q.OfferDialogue);
                v.HasNote = !string.IsNullOrEmpty(q.NoteDialogue);
                return v;
            }
            var req = QuestRequiresBlocked(s, q);
            if (acc)
            {
                v.Status = "active";
                v.StatusText = q.WorkMinutes > 0 && v.WorkDone > 0 ? $"进行中（已完成{v.WorkDone}/{q.WorkMinutes}分钟）" : "进行中";
                v.Step = q.StepActive;
                // B23（A11 §2）：可选地牢任务领到记录后“记录已取得”，下一步回岗位交谈
                if (HasQuestRecord(s, q)) { v.StatusText = "记录已取得"; v.Step = q.StepTurnin; }
                v.WorkHint = string.IsNullOrEmpty(q.WorkDialogue) ? "" : string.Join("", Config.Story.Dialogues.Where(d => d.Event == q.WorkDialogue).Select(d => d.Text));
            }
            else
            {
                v.Status = req == null ? "available" : "locked";
                v.StatusText = req == null ? "可接" : "未满足条件";
                v.Step = req != null && !string.IsNullOrEmpty(q.StepLocked) ? q.StepLocked : q.StepOffer;
                // B23 试玩修复：先领了记录还没接取的可选任务写明记录已取得（沿用领取日志原句）
                if (HasQuestRecord(s, q))
                {
                    if (req == null) v.StatusText = "可接 · 记录已取得";
                    v.Step = $"记录已保存，{(req == null ? "" : "营区登记后")}在{LocName(q.LocationId)}和{NpcNameOf(q.NpcId)}交谈接取时即完成，不用再进一次。";
                }
                v.AcceptBlocked = req;
                if (req == null && (q.Accept == "talk"))
                {
                    v.AcceptHere = true;
                    v.AcceptBlocked = AcceptBlocked(s, q);
                }
            }
            // 任务条目里的工作按钮：0 分钟阅读（已接）、没有城市服务的计时工作（CH01）、招募登记
            if (q.Accept == "roster" && !acc)
            {
                v.WorkLabel = q.WorkLabel;
                v.WorkBlocked = req ?? AcceptBlocked(s, q);
            }
            else if (acc && q.WorkServiceIds.Count == 0 && q.Accept != "service" && q.Accept != "auto" && q.CompleteByAny.Count == 0
                     && (q.RecordFacts.Count == 0 || HasQuestRecord(s, q)))
            {
                v.WorkLabel = q.WorkMinutes > 0 ? $"{q.WorkLabel}（{q.WorkMinutes - v.WorkDone}分钟）" : $"{q.WorkLabel}（0分钟）";
                v.WorkBlocked = QuestWorkBlocked(s, q);
            }
            return v;
        }

        /// <summary>任务类别的玩家说法：主线 / 委托 / 可选任务（B23，A11“四条可选任务”）/ 同行邀约。</summary>
        public static string KindWord(QuestConfig q) => q.Kind switch { "main" => "主线", "recruit" => "同行邀约", "optional" => "可选任务", _ => "委托" };

        /// <summary>B23：记录类任务的记录事实已在（领取核心记录同一事务写入，本局永久）。</summary>
        public static bool HasQuestRecord(GameState s, QuestConfig q) => q.RecordFacts.Count > 0 && q.RecordFacts.Any(s.Progress.Flags.Contains);

        /// <summary>可选任务的接取/回报台词（B26 Q217 起是 EXQ05—08 专属组）：走回放，不受“同一组只播一次”影响；其余任务的台词照旧只播一次。</summary>
        private void PlayQuestSet(GameState s, QuestConfig q, string set)
        {
            if (q.Kind == "optional") OpenReplay(s, set);
            else OpenDialogue(s, set);
        }

        /// <summary>EXQ05—08（配置里 kind=optional 的四项）：接取走专属句，不要通用首次铺垫（Q235_B scope）。</summary>
        private static bool DedicatedOffer(QuestConfig q) => q.Kind == "optional";

        /// <summary>
        /// 事项里的“接取”与交谈同效（B26a，Q235 scope/sequence）：EXQ05—08 从这里接取时，本人在岗把首次铺垫记为已处理、不补播；
        /// 岗位离岗时先播一次未示的代班接续说明。其他任务的这个入口保持原样。
        /// </summary>
        private void DedicatedOfferLead(GameState s, QuestConfig q)
        {
            if (!DedicatedOffer(q) || CityNpc(q.NpcId) is not CityNpcConfig n) return;
            if (s.City.Absent.Contains(n.NpcId)) PlaySubstituteIntro(s, n);
            else s.City.Greeted.Add(n.NpcId);
        }

        private string? ChoiceLabelOf(string choiceId) =>
            Config.City.Services.SelectMany(x => x.Choices).FirstOrDefault(c => c.Id == choiceId)?.Label;

        /// <summary>任务册：已显示的全部任务（主线、委托、同行邀约，各按配置顺序）。</summary>
        public List<QuestView> QuestBook(GameState s) =>
            new[] { "main", "side", "optional", "recruit" }.SelectMany(k => LiveQuests.Where(q => q.Kind == k)).Select(q => QuestViewOf(s, q.Id))
                .Where(v => v != null).Select(v => v!).ToList();

        /// <summary>某岗位（或命名队友）名下已显示的任务。</summary>
        public List<QuestView> QuestsOfNpc(GameState s, string npcId) =>
            LiveQuests.Where(q => q.NpcId == npcId).Select(q => QuestViewOf(s, q.Id)).Where(v => v != null).Select(v => v!).ToList();

        /// <summary>岗位卡上的委托短句（“委托：鉴定之前（可接）”）；没有为空。</summary>
        private string QuestCardText(GameState s, string npcId)
        {
            var list = QuestsOfNpc(s, npcId).Where(v => v.Status != "done").ToList();
            return list.Count == 0 ? "" : "委托：" + string.Join("、", list.Select(v => $"{v.Title}（{v.StatusText}）"));
        }

        /// <summary>在当地交谈/条目里接取的通用阻拦（地点、空闲、对话）；可以为 null。</summary>
        private string? AcceptBlocked(GameState s, QuestConfig q)
        {
            if (s.CurrentLocation != q.LocationId) return $"须实际到达{LocName(q.LocationId)}";
            return TalkBlocked(s);
        }

        // ───────────────────────── 接取 ─────────────────────────

        /// <summary>
        /// 在任务条目里接取（与和岗位交谈同效，0 分钟）：talk 类播接取对白并写 quest:ID:accepted；招募类为选择登记（入名册即完成）。
        /// 不预扣任何东西、不自动开始工作。
        /// </summary>
        public ActionResult AcceptQuest(GameState s, string questId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var q = Quest(questId);
            if (q == null || !q.Implemented) return ActionResult.Fail("没有这项委托");
            if (QuestDone(s, q.Id)) return ActionResult.Fail("这项已经完成");
            if (QuestAccepted(s, q.Id) && q.Accept != "roster") return ActionResult.Fail("已经接取");
            if (q.Accept == "roster" && RosterOf(s, q.NpcId) is RosterEntry old)
                return ActionResult.Fail(old.Dead ? $"{old.Name}已经死亡，命名角色不会复活" : $"{old.Name}已在名册里");
            var why = QuestRequiresBlocked(s, q) ?? AcceptBlocked(s, q);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            switch (q.Accept)
            {
                case "talk":
                    DedicatedOfferLead(s, q);
                    AcceptCore(s, q, true);
                    return ActionResult.Success();
                case "roster":
                    return TalkRecruit(s, q);
                default:
                    return ActionResult.Fail("这项要在相应事项里办理");
            }
        }

        /// <summary>写接取事实与时间，播接取对白（只第一次），日志写下一步。</summary>
        private void AcceptCore(GameState s, QuestConfig q, bool dialogue, string? choice = null)
        {
            var rec = s.Quests.Of(q.Id);
            if (s.Progress.Flags.Add(QuestFact(q.Id, "accepted"))) rec.AcceptedAt ??= s.GameMinute;
            if (choice != null) rec.Choice = choice;
            if (dialogue && !string.IsNullOrEmpty(q.OfferDialogue)) PlayQuestSet(s, q, q.OfferDialogue!);
            // B23 试玩修复：记录已在手的可选任务接取即追认完成，只记完成那一条
            if (!QuestDone(s, q.Id) && !(q.RecordFacts.Count > 0 && HasQuestRecord(s, q)))
                Emit(s, GameEventKind.Info, $"{KindWord(q)}“{q.Title}”已接取：{q.StepActive}");
            // B23（A11 §2“任务可提前拾取后补接，不重复要求再进一次”）：接取时记录已在手——就在岗位面前，接取即追认完成（同 FA05 先例，不再给第二份知识/物资）
            if (q.RecordFacts.Count > 0 && HasQuestRecord(s, q)) CompleteQuest(s, q, false);
            // B15a（Q157 选 A）：须先接取才完成的（FA05），接取时完成事实已在手（先领了联络日志）——接取后这一安全边界追认完成：
            // 写完成与日志，不补播联络完成场景（场景属于 d04_07 联络提交那一刻，见待确认 Q163）、不加奖励
            if (q.CompleteRequiresAccepted && q.CompleteByAny.Any(s.Progress.Flags.Contains)) CompleteQuest(s, q, false, quiet: true);
        }

        /// <summary>招募任务：选择登记 → 命名队友入名册候补（首次两件装备随登记一次生成，不自动编队、不看队伍是否满员），任务完成；0 分钟 0 物资。</summary>
        private ActionResult RecruitByQuest(GameState s, QuestConfig q)
        {
            var res = AddNamedToRoster(s, q.NpcId);
            if (!res.Ok) return res;
            s.Progress.Flags.Add(QuestFact(q.Id, "accepted"));
            CompleteQuest(s, q, false);
            return ActionResult.Success();
        }

        /// <summary>
        /// 同行邀约“交谈”（B15a，Q156 选 B）：前置满足、在任务地点安全空闲、本人未登记且未死亡时，打开本人的逐字邀约对白（每次一段新对话记录，可重读）；
        /// 不写任务事实、不推进时间、不扣物、不计经验。读完由 <see cref="AnswerRecruit"/> 选“登记同行”或“暂不登记”。
        /// </summary>
        private ActionResult TalkRecruit(GameState s, QuestConfig q)
        {
            if (string.IsNullOrEmpty(q.OfferDialogue)) return RecruitByQuest(s, q);
            OpenReplay(s, q.OfferDialogue!);
            return ActionResult.Success();
        }

        /// <summary>当前正在显示末句的那段对话是某招募任务的邀约、且仍可登记时返回任务 ID（对白层把“结束对话”换成“登记同行”“暂不登记”）；否则 null。</summary>
        public string? RecruitOfferPending(GameState s)
        {
            var d = ActiveDialogue(s);
            if (d == null || d.Lines.Count == 0 || d.Pending.Count > 0) return null;
            var ev = d.EventId.Split('#')[0];
            var q = LiveQuests.FirstOrDefault(x => x.Kind == "recruit" && x.OfferDialogue == ev);
            if (q == null || QuestDone(s, q.Id) || RosterOf(s, q.NpcId) != null) return null;
            return q.Id;
        }

        /// <summary>
        /// 读完邀约后的选择（B15a，Q156 选 B）：lineId 须是邀约正在显示的末句（防双击）。register=true“登记同行”：前置、地点、空闲重验后
        /// 同一事务完成任务并入名册候补（首次两件装备只生成一次，不自动编队，队伍满员也能登记），再结束对话；
        /// register=false“暂不登记”：只结束对话，不完成、不永久拒招，以后可再谈。都是 0 分钟 0 物资、不计经验。
        /// </summary>
        public ActionResult AnswerRecruit(GameState s, string questId, string lineId, bool register)
        {
            var q = Quest(questId);
            if (q == null || !q.Implemented || q.Accept != "roster") return ActionResult.Fail("没有这项同行邀约");
            var d = ActiveDialogue(s);
            var cur = d?.Lines.LastOrDefault();
            if (d == null || cur == null || cur.LineId != lineId || RecruitOfferPending(s) != q.Id) return ActionResult.Repeated();
            string? why = null;
            if (register)
            {
                why = QuestRequiresBlocked(s, q)
                      ?? (s.CurrentLocation != q.LocationId ? $"须实际到达{LocName(q.LocationId)}" : null)
                      ?? (s.Battle != null || s.Encounter != null ? "先处理眼前的敌踪" : s.Main != null || s.Sub != null ? "行动进行中" : null);
                if (why == null)
                {
                    var res = RecruitByQuest(s, q);
                    if (!res.Ok) why = res.Reason;
                }
            }
            AckDialogue(s, lineId);
            if (!register)
                Emit(s, GameEventKind.Info, $"暂不登记{NpcNameOf(q.NpcId)}：以后可以再到{LocName(q.LocationId)}和{NpcNameOf(q.NpcId)}交谈。");
            return why == null ? ActionResult.Success() : ActionResult.Fail(why);
        }

        /// <summary>
        /// 回看已完成任务的对白（0 分钟，只回放台词，不重算时间、奖励或事实；narrative-texts replay “history_read_only_no_effect”）。
        /// note：回看 AC02 外部医疗线索。
        /// </summary>
        public ActionResult ReviewQuest(GameState s, string questId, bool note = false)
        {
            var q = Quest(questId);
            if (q == null || !q.Implemented || !QuestDone(s, q.Id)) return ActionResult.Fail("还没有可回看的记录");
            var why = TalkBlocked(s);
            if (why != null) return ActionResult.Fail(why);
            var set = note ? q.NoteDialogue : q.RepeatDialogue ?? q.CompleteDialogue ?? q.OfferDialogue;
            if (string.IsNullOrEmpty(set)) return ActionResult.Fail("没有可回看的对白");
            OpenReplay(s, set!);
            return ActionResult.Success();
        }

        // ───────────────────────── 工作 ─────────────────────────

        /// <summary>开始工作前的阻拦（已接、地点、工作前置、空闲）；可以为 null。</summary>
        private string? QuestWorkBlocked(GameState s, QuestConfig q)
        {
            if (QuestDone(s, q.Id)) return "已完成";
            if (!QuestAccepted(s, q.Id) && q.Accept != "work") return $"先接取“{q.Title}”";
            if (q.RecordFacts.Count > 0 && !HasQuestRecord(s, q)) return "还没有取得记录：" + q.StepActive;
            var miss = q.WorkRequiresAll.FirstOrDefault(f => !s.Progress.Flags.Contains(f));
            if (miss != null) return "需要：" + RequirementText(miss);
            if (s.CurrentLocation != q.LocationId && q.WorkServiceIds.Count == 0) return $"须实际到达{LocName(q.LocationId)}";
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null || s.Encounter != null) return "先处理眼前的敌踪";
            if (s.Main != null || s.Sub != null) return "行动进行中";
            return null;
        }

        /// <summary>
        /// 任务条目里的工作：0 分钟的阅读/了解当场完成（播交付对白）；有工时、没有城市服务的（CH01 核对物资账）开工为主行动 wait，
        /// 按 5 分钟段推进，完成时写完成；中途取消保留已完成的段（A11 §5“工作中断按剩余段续算”）。
        /// </summary>
        public ActionResult StartQuestWork(GameState s, string questId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var q = Quest(questId);
            if (q == null || !q.Implemented) return ActionResult.Fail("没有这项委托");
            if (q.WorkServiceIds.Count > 0) return ActionResult.Fail("这项工作在相应事项里办理");
            var why = QuestWorkBlocked(s, q);
            if (why != null) return ActionResult.Fail(why);
            if (q.WorkMinutes <= 0)
            {
                RememberRequest(s, requestId);
                CompleteQuest(s, q, false);
                return ActionResult.Success();
            }
            return StartCityService(s, QuestWorkServicePrefix + q.Id, null, null, requestId);
        }

        /// <summary>任务工作剩余分钟（按已提交段）。</summary>
        private int QuestWorkLeft(GameState s, QuestConfig q) =>
            Math.Max(0, q.WorkMinutes - (s.Quests.Records.TryGetValue(q.Id, out var r) ? r.WorkMinutes : 0));

        /// <summary>做这项城市服务对应的任务（服务的 questId，或 CH01 这类合成的任务工作服务）。</summary>
        private QuestConfig? QuestOfService(CityServiceConfig svc) =>
            svc.QuestId == null ? null : Quest(svc.QuestId) is QuestConfig q && q.Implemented && q.WorkServiceIds.Concat(new[] { QuestWorkServicePrefix + q.Id }).Contains(svc.Id) ? q : null;

        /// <summary>城市服务里任务工作的阻拦：未接取、已完成、工作前置、分支冲突。</summary>
        private string? QuestServiceBlocked(GameState s, CityServiceConfig svc, QuestConfig q)
        {
            if (QuestDone(s, q.Id)) return $"“{q.Title}”已归档：回看不再扣时、不发第二份奖励";
            if (q.Accept != "work" && !QuestAccepted(s, q.Id))
                return $"先和{NpcNameOf(q.NpcId)}交谈，接取“{q.Title}”";
            var miss = q.RequiresAll.Concat(q.WorkRequiresAll).FirstOrDefault(f => !s.Progress.Flags.Contains(f));
            if (miss != null) return "需要：" + RequirementText(miss);
            // CH02：已在训诫堂开始“回营区备份”的，典藏室不能改成抄录续做
            var rec = s.Quests.Records.TryGetValue(q.Id, out var r) ? r : null;
            if (svc.Choices.Count == 0 && rec?.Choice == Ch02BackupChoice && rec.WorkMinutes > 0)
                return "已选“以已有实地记录回营区备份”，请回训诫堂续做";
            return null;
        }

        /// <summary>计时任务工作完成（段末、死亡已先判过）：记满工作分钟，写完成并播交付；返回给“完成”日志的短句。</summary>
        private void CompleteQuestWork(GameState s, QuestConfig q, CityServiceTxn p)
        {
            var rec = s.Quests.Of(q.Id);
            rec.WorkMinutes = q.WorkMinutes;
            if (p.ChoiceId != null) rec.Choice = p.ChoiceId;
            CompleteQuest(s, q, false);
        }

        /// <summary>取消任务工作：已提交的整 5 分钟段计入任务账（续做只补剩余段），未提交的不算。</summary>
        private void KeepQuestWorkSegments(GameState s, ActionState a, CityServiceTxn p)
        {
            var svc = CityService(p.ServiceId);
            if (svc == null || QuestOfService(svc) is not QuestConfig q) return;
            var seg = Math.Max(0, a.ElapsedMinutes - a.ElapsedMinutes % 5);
            if (seg <= 0) return;
            var rec = s.Quests.Of(q.Id);
            rec.WorkMinutes = Math.Min(q.WorkMinutes - 5, rec.WorkMinutes + seg);
            if (p.ChoiceId != null) rec.Choice = p.ChoiceId;
            Emit(s, GameEventKind.Info, $"“{q.Title}”已保存完成的{rec.WorkMinutes}分钟，下次只需续做剩余{q.WorkMinutes - rec.WorkMinutes}分钟。");
        }

        // ───────────────────────── 完成 ─────────────────────────

        /// <summary>
        /// 写完成（同一事务：quest:ID:accepted/completed、完成事实、时间），重判门槛，播交付/完成对白与附加线索，日志写结果。
        /// retro：旧档追认，只写事实与“旧档追认”标记，不播对白、不写日志、不补奖励。幂等。
        /// </summary>
        private void CompleteQuest(GameState s, QuestConfig q, bool retro, bool quiet = false)
        {
            if (QuestDone(s, q.Id)) return;
            var rec = s.Quests.Of(q.Id);
            var f = s.Progress.Flags;
            if (f.Add(QuestFact(q.Id, "accepted"))) rec.AcceptedAt ??= s.GameMinute;
            f.Add(QuestFact(q.Id, "completed"));
            rec.CompletedAt = s.GameMinute;
            rec.Retro = retro;
            foreach (var x in q.CompleteFacts) f.Add(x);
            if (q.WorkMinutes > 0 && !retro) rec.WorkMinutes = q.WorkMinutes;
            if (retro) { if (q.CompleteFacts.Count > 0) RefreshGates(s); return; }
            var routes = q.CompleteFacts.Count > 0 ? RouteNotes(s, RefreshGates(s), s.CurrentLocation) : new List<string>();
            if (!quiet)
            {
                if (!string.IsNullOrEmpty(q.TurninDialogue)) PlayQuestSet(s, q, q.TurninDialogue!);
                if (!string.IsNullOrEmpty(q.CompleteDialogue)) OpenDialogue(s, q.CompleteDialogue!);
                if (!string.IsNullOrEmpty(q.NoteDialogue)) OpenDialogue(s, q.NoteDialogue!);
            }
            var extra = QuestCompletionDetail(s, q);
            Emit(s, GameEventKind.Info, $"{KindWord(q)}“{q.Title}”完成：{q.Result}" +
                (extra != null ? "；" + extra : "") + (routes.Count > 0 ? "；发现" + string.Join("与", routes) : "") + "。");
        }

        /// <summary>完成日志里按当前状态补的事实说明（HU03 当前容量与名册、HU04 每名同行者的伤病风险）。</summary>
        private string? QuestCompletionDetail(GameState s, QuestConfig q)
        {
            switch (q.Id)
            {
                case "HU03":
                    return $"当前队伍容量C{PartyCapacity(s)}（含你），名册{s.Roster.Count(r => r.Present)}人";
                case "HU04":
                {
                    var list = s.Roster.Where(r => r.Present && s.Actors.ContainsKey(r.ActorId)).Select(r =>
                    {
                        var a = s.Actors[r.ActorId];
                        return $"{r.Name}：健康{Fmt.N(a.Stats.Health)}/{Fmt.N(a.Stats.HealthMax)}、病毒{Fmt.N(a.Body.VirusLoad)}{(a.Body.Infected ? "（已感染）" : "")}、{(a.Body.BleedTriggers > 0 ? "正在出血" : "没有出血")}";
                    }).ToList();
                    return list.Count == 0 ? null : "留守前的风险预览——" + string.Join("；", list) + "（公共照护不治伤病、不清感染）";
                }
            }
            return null;
        }

        // ───────────────────────── 安全边界 ─────────────────────────

        /// <summary>
        /// 任务安全边界（UpdateFlags 末尾，战斗/敌踪中跳过）：核心记录等事实已到的主线写完成（FA02/FA03/FA05）；
        /// 已登记且在营区广场时播 FA01 公开说明；CH02 选回营区备份完成后到营区广场播一次许安回报；名册里已有的命名队友记其招募任务完成。幂等。
        /// </summary>
        private void QuestBoundary(GameState s)
        {
            if (Config.Quests.List.Count == 0 || s.Progress.Dead || s.Battle != null || s.Encounter != null) return;
            foreach (var q in LiveQuests)
            {
                if (QuestDone(s, q.Id)) continue;
                if (q.CompleteByAny.Count > 0 && q.CompleteByAny.Any(s.Progress.Flags.Contains))
                {
                    // B15a（Q157）：FA05 须先接取；没接取时联络日志先留着，接取那一刻追认（AcceptCore）
                    if (!q.CompleteRequiresAccepted || QuestAccepted(s, q.Id)) CompleteQuest(s, q, false);
                }
                else if (q.Accept == "roster" && RosterOf(s, q.NpcId) != null) CompleteQuest(s, q, true);
                else if (q.Accept == "auto" && s.CurrentLocation == q.LocationId && QuestRequiresBlocked(s, q) == null)
                {
                    AcceptCore(s, q, true);
                    CompleteQuest(s, q, false);
                }
            }
            foreach (var q in LiveQuests.Where(q => !string.IsNullOrEmpty(q.HubTurninDialogue)))
            {
                if (!QuestDone(s, q.Id) || !s.Quests.Records.TryGetValue(q.Id, out var rec) || rec.Choice != Ch02BackupChoice) continue;
                if (s.CurrentLocation != Config.City.Registration.SquareLocation || !rec.Played.Add(q.HubTurninDialogue!)) continue;
                OpenDialogue(s, q.HubTurninDialogue!);
            }
        }

        /// <summary>
        /// 旧档追认（schema 26→27）：按已有可靠事实无声记任务账（不播对白、不写日志、不补奖励）。返回追认的任务名。幂等。
        /// 已领公开路线 → FA02/FA03 已接；首次核心记录/联络日志 → 对应主线完成；FA04_merged → FA04 完成；已领通信电源 → FA05 已接；林昭等已在名册 → 招募任务完成。
        /// </summary>
        public List<string> RetroQuests(GameState s)
        {
            var list = new List<string>();
            var f = s.Progress.Flags;
            foreach (var q in LiveQuests)
            {
                if (QuestDone(s, q.Id)) continue;
                var done = (q.CompleteByAny.Count > 0 && q.CompleteByAny.Any(f.Contains))
                           || (q.CompleteFacts.Count > 0 && q.CompleteFacts.All(f.Contains))
                           || (q.Accept == "roster" && RosterOf(s, q.NpcId) != null);
                if (done)
                {
                    CompleteQuest(s, q, true);
                    list.Add(q.Title);
                    continue;
                }
                var svc = q.AcceptServiceId == null ? null : CityService(q.AcceptServiceId);
                var accepted = (svc != null && svc.Choices.SelectMany(c => c.Facts).Distinct().Any(f.Contains))
                               || (q.Accept == "talk" && q.Kind == "main" && q.RequiresAll.Count > 0 && q.RequiresAll.All(f.Contains));
                if (accepted && f.Add(QuestFact(q.Id, "accepted")))
                {
                    var rec = s.Quests.Of(q.Id);
                    rec.AcceptedAt ??= s.GameMinute;
                    rec.Retro = true;
                }
            }
            return list;
        }

        // ───────────────────────── 交谈接取 ─────────────────────────

        /// <summary>交谈时可接的（talk 类、已显示、前置满足、未接）第一项；没有为 null。</summary>
        private QuestConfig? TalkOffer(GameState s, string npcId) =>
            LiveQuests.FirstOrDefault(q => q.NpcId == npcId && q.Accept == "talk" && !QuestAccepted(s, q.Id) && !QuestDone(s, q.Id)
                                           && QuestShown(s, q) && QuestRequiresBlocked(s, q) == null);

        /// <summary>
        /// 交谈时岗位的委托状态（narrative-texts“npc slot state quest_offer/quest_return”）：名下或岗位资料里关联的已实装任务（city-npc-data questIds）
        /// 有可接未接的播“委托”一组，有进行中的播“委托回报”一组；都没有为 null（闲谈）。可在交谈中直接接取的已先接取，不走这里。
        /// </summary>
        private string? QuestTalkState(GameState s, CityNpcConfig n)
        {
            var related = LiveQuests.Where(q => q.NpcId == n.NpcId || n.QuestIds.Contains(q.Id)).ToList();
            if (related.Any(q => !QuestAccepted(s, q.Id) && !QuestDone(s, q.Id) && QuestShown(s, q) && QuestRequiresBlocked(s, q) == null)) return "quest_offer";
            if (related.Any(q => QuestAccepted(s, q.Id) && !QuestDone(s, q.Id))) return "quest_return";
            return null;
        }

        /// <summary>
        /// 代班兜底交谈选哪一组（B26a，Q236 selection）：本岗事项里有已显示、前置满足、未接的委托 → 待接提示；否则岗位关联（名下或 questIds）已接未完成 → 进度提醒；否则本岗闲谈。
        /// 待接只认本岗事项能办的（q.NpcId 是本岗，即事项面板列出的那些）；已被交谈接走的走不到这里。六人招募邀约不由代班代说。
        /// </summary>
        private string SubstituteTalkState(GameState s, CityNpcConfig n)
        {
            var related = LiveQuests.Where(q => q.Kind != "recruit" && (q.NpcId == n.NpcId || n.QuestIds.Contains(q.Id))).ToList();
            if (related.Any(q => q.NpcId == n.NpcId && !QuestAccepted(s, q.Id) && !QuestDone(s, q.Id) && QuestShown(s, q) && QuestRequiresBlocked(s, q) == null)) return "substitute_quest_offer";
            if (related.Any(q => QuestAccepted(s, q.Id) && !QuestDone(s, q.Id))) return "substitute_quest_return";
            return "substitute_idle";
        }

        // ───────────────────────── 同行邀约卡（RN01—RN06） ─────────────────────────

        /// <summary>某命名队友的招募任务（已实装）；没有为 null。</summary>
        private QuestConfig? RecruitQuestOf(string actorId) => LiveQuests.FirstOrDefault(q => q.Kind == "recruit" && q.NpcId == actorId);

        /// <summary>
        /// 当前地点的同行邀约卡（A11 §6“可在相应建筑作可见访客卡，但不会替代该建筑岗位”）：招募任务前置满足、本人不在名册（未登记、未死亡）时，
        /// 在任务地点的设施栏“人物”组出现；按钮“交谈”0 分钟打开本人逐字邀约，读完选“登记同行”或“暂不登记”（B15a，Q156 选 B）。
        /// </summary>
        private IEnumerable<NpcCardView> RecruitCardsHere(GameState s)
        {
            if (!s.Locations.TryGetValue(s.CurrentLocation, out var ls) || !ls.Visited) yield break;
            foreach (var q in LiveQuests.Where(q => q.Kind == "recruit" && q.LocationId == s.CurrentLocation))
            {
                if (QuestDone(s, q.Id) || RosterOf(s, q.NpcId) != null || QuestRequiresBlocked(s, q) != null) continue;
                var c = Config.Party.Named(q.NpcId);
                if (c == null) continue;
                yield return new NpcCardView
                {
                    NpcId = q.NpcId, Name = c.Name, Role = c.Affiliation, Kind = "companion", KindLabel = "固定同行者",   // B24（A10-CITY §6）：命名队友作访客卡标“固定同行者”（原“固定队友”）
                    Status = "可邀约同行 · 不耗时", HasNews = true, ActionLabel = "交谈", Background = c.Background,
                    RecruitText = "交谈读完邀约后可选“登记同行”或“暂不登记”；登记后进名册（候补，在营区公共居所留守，不自动编队，队伍满员也能登记），首次两件装备随登记一次生成；暂不登记可以以后再来",
                    TalkBlocked = TalkBlocked(s),
                    Presence = "在场",
                    QuestMarks = "可接1",
                };
            }
        }

        // ───────────────────────── 开发入口 ─────────────────────────

        /// <summary>
        /// 开发入口（?dev=1）：按原领取事务写某份核心记录的领取事实（campaign/claim/记录 ID/门槛事实，d01_08 等），重判门槛与任务边界——
        /// 地牢实例（B21）上线前用它验证“记录获得 → 主线推进与容量升档”。不发物资、不写本代实例事实。
        /// </summary>
        public string DebugClaimRecord(GameState s, string resourceId)
        {
            var res = Config.Resources.FirstOrDefault(r => r.NodeId == resourceId && r.IsCampaign);
            if (res == null) return $"开发：没有核心记录 {resourceId}";
            if (CampaignClaimed(s, res.NodeId)) return $"开发：{res.Name}本局已领取";
            var cap = BattleCapacity(s);
            foreach (var f in ClaimFactsOf(res)) s.Progress.Flags.Add(f);
            if (s.Resources.TryGetValue(res.NodeId, out var rs)) rs.Available = 0;
            RefreshGates(s);
            UpdateFlags(s);
            return $"开发：已按领取事务记下{res.Name}" + (BattleCapacity(s) > cap ? $"，队伍容量 C{cap}→C{BattleCapacity(s)}" : "") + "。";
        }

        // ───────────────────────── 合成服务（任务工作、战术训练） ─────────────────────────

        private Dictionary<string, CityServiceConfig>? _extraServices;

        /// <summary>
        /// 不在城市 19 项服务表里、借同一计时事务通道办理的条目：CH01 这类没有服务的任务工作（questwork_ID），
        /// 与 h03 战术训练（R03 §3）。它们不改服务表、不计入城市服务名称表。
        /// </summary>
        private Dictionary<string, CityServiceConfig> ExtraServices
        {
            get
            {
                if (_extraServices != null) return _extraServices;
                var d = new Dictionary<string, CityServiceConfig>();
                foreach (var q in Config.Quests.List.Where(q => q.WorkMinutes > 0 && q.WorkServiceIds.Count == 0 && q.CompleteByAny.Count == 0))
                    d[QuestWorkServicePrefix + q.Id] = new CityServiceConfig
                    {
                        Id = QuestWorkServicePrefix + q.Id, Title = q.WorkLabel, Summary = q.StepActive, LocationId = q.LocationId, Kind = "quest_work",
                        WorldMinutes = q.WorkMinutes, QuestId = q.Id, Implemented = q.Implemented,
                    };
                // B23：h02/h04 的公开抄录（h03 用城市服务表里原有的 copy_public_knowledge）
                var copyBase = Config.City.Services.FirstOrDefault(x => x.Id == CopyBaseServiceId);
                foreach (var p in Config.ExpansionQuests.Packs.Where(p => Config.City.Services.All(x => x.Id != p.CopyServiceId)).GroupBy(p => p.CopyServiceId))
                    d[p.Key] = new CityServiceConfig
                    {
                        Id = p.Key, Title = copyBase?.Title ?? "", Summary = copyBase?.Summary ?? "", LocationId = p.First().CopyLocationId, Kind = KnowledgeCopyKind,
                        WorldMinutes = p.First().CopyMinutes, Implemented = true,
                    };
                var t = Config.Scope.Training;
                if (t.Tiers.Count > 0 && !string.IsNullOrEmpty(t.LocationId))
                    d[TrainingServiceId] = new CityServiceConfig
                    {
                        Id = TrainingServiceId, Title = t.Title, Summary = t.Summary, LocationId = t.LocationId, Kind = "training",
                        WorldMinutes = t.Tiers.Min(x => x.Minutes), Implemented = true,
                    };
                return _extraServices = d;
            }
        }
    }
}

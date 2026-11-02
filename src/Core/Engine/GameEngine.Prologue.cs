using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>主界面任务角/任务册的一条（主目标或建议）。只读视图，不改状态。</summary>
    public sealed class PrologueObjectiveView
    {
        /// <summary>稳定 ID：任务 ID（PRO01—PRO18）或 visitor_talk / visitor_return / exit_go / exit_need / route_onward / settle。</summary>
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>下一步可做的具体动作（中性说明）。</summary>
        public string Step { get; set; } = "";
        /// <summary>对应任务（没有为 null）。</summary>
        public string? TaskId { get; set; }
    }

    /// <summary>任务册里的一项任务状态。</summary>
    public sealed class PrologueTaskView
    {
        public PrologueTaskConfig Config { get; set; } = new PrologueTaskConfig();
        public bool Done { get; set; }
        public int? CompletedAt { get; set; }
        /// <summary>旧档迁移时从可靠记录追认。</summary>
        public bool Retro { get; set; }
        /// <summary>当前是否相关（例如来客没到时 PRO16 不相关、旧档没有教学物质时 PRO05 不相关）。</summary>
        public bool Relevant { get; set; }
        /// <summary>不相关时的原因；为空表示相关或已完成。</summary>
        public string? Note { get; set; }
        public bool HintDismissed { get; set; }
    }

    /// <summary>设施栏的人物卡（首个 NPC 卡框架，B13 程望；B14 城市岗位 NPC 在此扩展）。</summary>
    public sealed class NpcCardView
    {
        public string NpcId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        /// <summary>卡面状态短句（“有话要说”“已告知营区路线”）。</summary>
        public string Status { get; set; } = "";
        /// <summary>“交谈”不可用的原因；可用为 null。交谈 0 分钟。</summary>
        public string? TalkBlocked { get; set; }
        /// <summary>首次交谈会取得路线（卡面强调）。</summary>
        public bool HasNews { get; set; }
    }

    /// <summary>设施栏的固定文书/路标卡（PRO15 救助残页、南口路标）。</summary>
    public sealed class DocumentCardView
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ReadBlocked { get; set; }
    }

    /// <summary>PRO17 远行确认的只读预览（S15 §6）。</summary>
    public sealed class JourneyPreview
    {
        public List<(string From, string To, int Minutes, bool Open)> Legs { get; set; } = new List<(string, string, int, bool)>();
        public int BaseMinutesTotal { get; set; }
        public int CleanWater { get; set; }
        public int Food { get; set; }
        public bool HasWeapon { get; set; }
        /// <summary>建议携带的缺项（只提示，不是硬门槛）。</summary>
        public List<string> Missing { get; set; } = new List<string>();
        /// <summary>同行者当前状态短句（N/W/E/S/M/H/I/V）。</summary>
        public List<string> PartyLines { get; set; } = new List<string>();
        /// <summary>随身物资卡与装备（含耐久）。</summary>
        public List<string> CarryLines { get; set; } = new List<string>();
        /// <summary>状态偏低时的建议（仍允许知情继续）。</summary>
        public List<string> Warnings { get; set; } = new List<string>();
        public string? ConfirmBlocked { get; set; }
        /// <summary>程望在场时可以点“先准备”。</summary>
        public bool CanSayNotReady { get; set; }
        public bool Confirmed { get; set; }
    }

    /// <summary>
    /// 新手序章（开发计划 v0.9 B13，R39—R43；S15「2026-10-30 分类联动本地更新」§3—§8、A10-TUTOR、prologue-data.json、narrative-texts.json）。
    /// <list type="bullet">
    /// <item>18 项 PRO 任务按真实事件锁存 completedAt/eventId，先做后看可追认；只写标记，不调用旧奖励回调、不改时间与物资。</item>
    /// <item>七日目标退役：不再弹七日成功窗、不当主线门槛；旧档已记的七日检查只作历史。</item>
    /// <item>来客程望：worldMinute≥6240 且首战已胜（或旧档等效）且主角存活，在下一安全边界登记 visitor_available；
    /// 人在营地空闲时播抵达对白并在设施栏出现 NPC 卡，人在外只发一次非阻断通知，不传送。交谈 0 分钟，首次写 visitor_met、camp_route_known。</item>
    /// <item>出区：QMAP01 = (首战胜利或旧档等效) AND camp_route_known AND visited(village)；交谈与到达都会重判，满足后一次解锁 village↔m01_road。
    /// 南口路标读完写永久凭证 south_sign_known；PRO18 = 路标已识别 AND 实际到达旧公路。</item>
    /// <item>常驻 1 个主目标 + 最多 2 条建议，只在内核安全边界重算（存 StageId/ObjectiveId/Suggestions）。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string FactVisitorAvailable = "visitor_available";
        public const string FactVisitorMet = "visitor_met";
        public const string FactCampRouteKnown = "camp_route_known";
        public const string FactSouthSignKnown = "south_sign_known";

        /// <summary>序章“取水”只认这些产物（S15 PRO06：从合法水源取水）。</summary>
        private static readonly string[] WaterOutputs = { "dirty_water" };

        /// <summary>PRO12 目标：已提交睡眠段累计分钟（prologue-data PRO12“实际睡眠累计120游戏分钟”）。</summary>
        public const int Pro12SleepMinutes = 120;

        /// <summary>非伤害型武器类（S15 PRO03“合法伤害型武器”：盾、指挥、医疗器具不算）。</summary>
        private static readonly string[] NonDamageKinds = { "shield", "command", "medical" };

        /// <summary>
        /// 饱饱反应对白的触发时机（narrative-texts “first relevant event”）：多数在任务完成时；台词明显在动作之前说的几项在“相关的第一刻”播
        /// （PRO04 首战建立、PRO05 教学物质落地、PRO06 到达溪流、PRO12 开始睡眠、PRO15 到达村庄），见开发假设 B13-H3。
        /// </summary>
        private static readonly HashSet<string> ReactionOnStart = new HashSet<string> { "PRO04", "PRO05", "PRO06", "PRO12", "PRO15" };

        private Dictionary<string, PrologueTaskConfig>? _proTasks;
        private Dictionary<string, PrologueTaskConfig> ProTasks => _proTasks ??= Config.Prologue.Tasks.ToDictionary(t => t.Id);

        public PrologueTaskConfig? ProTask(string id) => ProTasks.TryGetValue(id, out var t) ? t : null;

        public static bool TaskDone(GameState s, string id) => s.Prologue.Tasks.ContainsKey(id);

        /// <summary>序章是否有配置（旧配置/测试配置没有 prologue 节时整套不生效）。</summary>
        public bool PrologueEnabled => Config.Prologue.Tasks.Count > 0;

        // ───────────────────────── 任务账 ─────────────────────────

        /// <summary>
        /// 锁存一项任务：只写 completedAt/eventId，发一条非模态完成反馈（PROxx_done），按时机播饱饱反应（饱饱不在时不播）。
        /// 已完成的不重复写、不重复发反馈；读档、重放回调不会再触发。返回是否新完成。
        /// </summary>
        private bool CompleteTask(GameState s, string id, string eventId, bool retro = false)
        {
            if (!PrologueEnabled || s.Progress.Dead) return false;
            if (s.Prologue.Tasks.ContainsKey(id)) return false;
            var t = ProTask(id);
            if (t == null) return false;
            s.Prologue.Tasks[id] = new PrologueTaskRecord { CompletedAt = s.GameMinute, EventId = eventId, Retro = retro };
            if (retro) return true;
            // 完成反馈（PROxx_done）是中性系统反馈：非模态，只在任务角短暂显示、任务册留完成时间；不进行动日志、不当行动结果（A10-TUTOR §2）
            s.Prologue.LastDoneTask = id;
            s.Prologue.LastDoneAt = s.GameMinute;
            if (!ReactionOnStart.Contains(id)) QueueReaction(s, id);
            return true;
        }

        /// <summary>饱饱反应在空闲安全边界播（不在动作中途抢输入）；饱饱不在或旧档不播。同一组只播一次（对白事件去重）。</summary>
        private void QueueReaction(GameState s, string taskId)
        {
            var t = ProTask(taskId);
            if (t == null || string.IsNullOrEmpty(t.ReactionSet) || !StoryActive(s) || !s.Story.BaobaoPresent) return;
            if (s.Story.DialogueLog.Any(d => d.EventId == t.ReactionSet) || s.Prologue.PendingReactions.Contains(t.ReactionSet)) return;
            s.Prologue.PendingReactions.Add(t.ReactionSet);
        }

        /// <summary>“相关的第一刻”立即播的反应（PRO04 首战建立时，排在首战警告对白之后）。</summary>
        private void PlayReactionNow(GameState s, string taskId)
        {
            var t = ProTask(taskId);
            if (!PrologueEnabled || t == null || string.IsNullOrEmpty(t.ReactionSet) || !StoryActive(s) || !s.Story.BaobaoPresent) return;
            if (TaskDone(s, taskId)) return;
            OpenDialogue(s, t.ReactionSet);
        }

        /// <summary>按当前状态追认可从状态可靠确认的任务（先做后看）：防身武器、首战、融合、净水、块根熟食、棚、路线、出区抵达。</summary>
        private void SyncPrologueTasks(GameState s, bool retro = false)
        {
            if (!PrologueEnabled) return;
            var f = s.Progress.Flags;
            if (HasDamageWeaponEquipped(s)) CompleteTask(s, "PRO03", "state:weapon_equipped", retro);
            if (s.Story.TutorialWon) CompleteTask(s, "PRO04", "story:tutorial_won", retro);
            else if (s.Story.Legacy) CompleteTask(s, "PRO04", "legacy:equivalent", true);
            if (s.Story.TutorialFusionDone) CompleteTask(s, "PRO05", "story:tutorial_fusion", retro);
            if (s.Prologue.DrewWater && Visited(s, "stream")) CompleteTask(s, "PRO06", "gather:water", retro);
            if (f.Contains("boiled")) CompleteTask(s, "PRO08", "facility:boiled", retro);
            if (f.Contains("cooked")) CompleteTask(s, "PRO09", "facility:cooked_root", retro);
            if (s.Prologue.SleepMinutes >= Pro12SleepMinutes) CompleteTask(s, "PRO12", "sleep:120", retro);
            if (s.Locations.Values.Any(l => l.Facility.ShelterBuilt)) CompleteTask(s, "PRO13", "state:shelter", retro);
            if (f.Contains(FactCampRouteKnown)) CompleteTask(s, "PRO16", "talk:visitor_route_first", retro);
            var exit = Config.Prologue.Exit;
            if (f.Contains(FactSouthSignKnown) && !string.IsNullOrEmpty(exit.To) && Visited(s, exit.To)) CompleteTask(s, "PRO18", "travel:" + exit.To, retro);
        }

        /// <summary>
        /// 旧档迁移（schema 22）：按可靠记录追认任务（标“旧档追认”，不发反馈、不播反应），再走一次序章边界（来客按当前分钟复核、重算主目标）。
        /// 返回追认的任务标题。
        /// </summary>
        public List<string> RetroPrologue(GameState s)
        {
            if (!PrologueEnabled) return new List<string>();
            var before = new HashSet<string>(s.Prologue.Tasks.Keys);
            SyncPrologueTasks(s, retro: true);
            var added = s.Prologue.Tasks.Keys.Where(k => !before.Contains(k)).OrderBy(k => k).Select(k => ProTask(k)?.Title ?? k).ToList();
            PrologueBoundary(s);
            return added;
        }

        /// <summary>主角武器槽里有合法伤害型武器（拳不算，盾/指挥/医疗器具不算）。</summary>
        public bool HasDamageWeaponEquipped(GameState s) =>
            EquipSlots.Weapons.Select(slot => ItemInstance.EquipOwner(GameState.PlayerId, slot))
                .Any(owner => s.Items.Any(i => i.Owner == owner && _weapons.TryGetValue(i.ItemId, out var w)
                                               && (w.WeaponKind == null || !NonDamageKinds.Contains(w.WeaponKind))));

        // ── 事件钩子（各玩法提交成功后调用；只写任务账） ──

        /// <summary>PRO01：玩家实际选中一张地面/随身物资卡并展开操作预览（界面调用，0 时间）。</summary>
        public void NoteObjectInspected(GameState s)
        {
            if (DialogueLocked(s) || InCombatFlow(s)) return;
            if (CompleteTask(s, "PRO01", "select:item")) PrologueBoundary(s);
        }

        /// <summary>PRO02：食用/饮水实际提高了饱食或水分。</summary>
        private void NoteUse(GameState s, ActionState a, double satBefore, double hydBefore)
        {
            if (s.Stats.Satiety > satBefore + 1e-9 || s.Stats.Hydration > hydBefore + 1e-9) CompleteTask(s, "PRO02", $"use:a{a.ActionId}");
        }

        /// <summary>PRO07：点火事务完成（使用真实燃料与打火器）。</summary>
        private void NoteIgnite(GameState s, ActionState a) => CompleteTask(s, "PRO07", $"ignite:a{a.ActionId}");

        /// <summary>PRO06：采集到水（脏水）；溪流到访另由状态判断。</summary>
        private void NoteGather(GameState s, ResourceConfig res)
        {
            if (!res.Outputs.Keys.Any(WaterOutputs.Contains)) return;
            s.Prologue.DrewWater = true;
            SyncPrologueTasks(s);
        }

        /// <summary>PRO10：手工完成一件工具或武器配方（同一次制作也可同时满足 PRO03）。</summary>
        private void NoteCraft(GameState s, ActionState a, RecipeConfig rc)
        {
            if (rc.IsRepair || rc.FacilityResult != null || rc.Station != "hand") return;
            if (rc.Outputs.Keys.Any(id => _weapons.ContainsKey(id) || (_items.TryGetValue(id, out var ic) && !string.IsNullOrEmpty(ic.ToolTag))))
                CompleteTask(s, "PRO10", $"craft:a{a.ActionId}");
        }

        /// <summary>PRO11：一次实际的地面↔随身转移成功。</summary>
        private void NoteTransfer(GameState s) => CompleteTask(s, "PRO11", "transfer");

        /// <summary>PRO12：已提交的睡眠段累计（等待/休息不算）。开始睡眠时播反应。</summary>
        private void NoteSleepSegment(GameState s, int minutes)
        {
            s.Prologue.SleepMinutes += minutes;
            if (s.Prologue.SleepMinutes >= Pro12SleepMinutes) CompleteTask(s, "PRO12", "sleep:120");
        }

        /// <summary>移动到达后的序章事务：PRO14 往返、到达溪流/村庄的反应、出区抵达与重判。</summary>
        private void NoteArrive(GameState s, string from, string to)
        {
            if (!PrologueEnabled) return;
            var camp = Config.Prologue.Visitor.LocationId;
            if (to == "village" || to == "clinic") s.Prologue.ReachedVillageOrClinic = true;
            if (to == camp && s.Prologue.ReachedVillageOrClinic) CompleteTask(s, "PRO14", $"travel:{from}->{to}");
            if (to == "stream" && !TaskDone(s, "PRO06")) QueueReaction(s, "PRO06");
            if (to == "village" && !TaskDone(s, "PRO15")) QueueReaction(s, "PRO15");
            SyncPrologueTasks(s);
        }

        // ───────────────────────── 安全边界 ─────────────────────────

        private bool PrologueIdle(GameState s) =>
            !s.Progress.Dead && s.Main == null && s.Sub == null && s.Battle == null && s.Encounter == null && !DialogueLocked(s);

        /// <summary>
        /// 序章安全边界（段末、战斗结束、即时操作、对话结束、读档迁移、新局）：追认任务 → 来客登记与通知/抵达对白 → 空闲时播排队的反应 → 重算主目标。
        /// 不推进时间、不发物资。幂等。
        /// </summary>
        public void PrologueBoundary(GameState s)
        {
            if (!PrologueEnabled || s.Progress.Dead) return;
            SyncPrologueTasks(s);
            CheckVisitor(s);
            if (PrologueIdle(s) && s.Prologue.PendingReactions.Count > 0)
            {
                var next = s.Prologue.PendingReactions[0];
                s.Prologue.PendingReactions.RemoveAt(0);
                OpenDialogue(s, next);
            }
            RefreshObjective(s);
        }

        /// <summary>来客条件（S15 §5）：绝对世界分钟 ≥ 阈值、首战已胜（或旧档等效）、主角存活。</summary>
        public bool VisitorConditionsMet(GameState s)
        {
            var v = Config.Prologue.Visitor;
            if (string.IsNullOrEmpty(v.NpcId) || s.Progress.Dead) return false;
            if (s.GameMinute < v.NotBeforeWorldMinute) return false;
            return !v.NeedsTutorialBattleWon || s.Story.TutorialWon || s.Story.Legacy;
        }

        public static bool VisitorRegistered(GameState s) => s.Progress.Flags.Contains(FactVisitorAvailable);

        /// <summary>旧档“可选择接续”：Legacy 或从 schema 21 及以前迁移进来的存档，来客不自动弹抵达对白，点“交谈”才接续。</summary>
        public static bool OptInVisitor(GameState s) => s.Story.Legacy || s.Prologue.MigratedAt != null;

        private void CheckVisitor(GameState s)
        {
            var v = Config.Prologue.Visitor;
            var p = s.Prologue;
            if (!VisitorRegistered(s))
            {
                // 战斗/敌踪中不登记，等下一个安全边界（SP10）
                if (InCombatFlow(s) || !VisitorConditionsMet(s)) return;
                s.Progress.Flags.Add(FactVisitorAvailable);
                p.VisitorAvailableAt = s.GameMinute;
                // 晚到：首战胜利晚于阈值；旧档等效；或迁移档在迁移时已过阈值（胜利时刻没有记录，来客是“迟来”的）
                p.VisitorLate = s.Story.Legacy || (s.Story.TutorialWonMinute is int won && won > v.NotBeforeWorldMinute)
                                || (s.Story.TutorialWonMinute == null && p.MigratedAt is int mig && mig >= v.NotBeforeWorldMinute);
            }
            if (p.VisitorArrivalShown || s.Progress.Flags.Contains(FactVisitorMet)) return;
            // 旧档“可选择接续序章”（S15 §7）：Legacy 与所有从旧版本迁移进来的存档都不自动弹抵达对白，营地设施栏出现人物卡，
            // 玩家点“交谈”时才播（开发假设 B13-H6；试玩修复 M269 P4 统一到迁移档）
            if (OptInVisitor(s))
            {
                if (s.CurrentLocation != v.LocationId && !p.VisitorNotified && !InCombatFlow(s))
                {
                    p.VisitorNotified = true;
                    Emit(s, GameEventKind.Notice, Config.Prologue.Talk.ArrivalAwayText);
                }
                return;
            }
            if (s.CurrentLocation == v.LocationId)
            {
                if (!PrologueIdle(s)) return;   // 人在营地但正忙（睡眠、工作）：行动结束后再播抵达
                p.VisitorArrivalShown = true;
                var t = Config.Prologue.Talk;
                OpenDialogue(s, p.VisitorLate && !string.IsNullOrEmpty(t.ArrivalLate) ? t.ArrivalLate : t.ArrivalCamp);
            }
            else if (!p.VisitorNotified && !InCombatFlow(s))
            {
                // 人在外：一次非阻断通知，写日志；不拉回、不传送、不打断当前行动
                p.VisitorNotified = true;
                Emit(s, GameEventKind.Notice, Config.Prologue.Talk.ArrivalAwayText);
            }
        }

        // ───────────────────────── NPC 卡与交谈 ─────────────────────────

        /// <summary>当前地点设施栏的人物卡（B13 只有程望；B14 城市岗位在此扩展）。</summary>
        public List<NpcCardView> NpcsHere(GameState s)
        {
            var list = new List<NpcCardView>();
            var v = Config.Prologue.Visitor;
            if (!PrologueEnabled || string.IsNullOrEmpty(v.NpcId) || !VisitorRegistered(s) || s.CurrentLocation != v.LocationId) return list;
            var known = s.Progress.Flags.Contains(FactCampRouteKnown);
            list.Add(new NpcCardView
            {
                NpcId = v.NpcId, Name = v.Name, Role = v.Role,
                Status = known ? "已告知营区路线 · 可再问" : "有话要说 · 交谈不耗时",
                HasNews = !known,
                TalkBlocked = TalkBlocked(s),
            });
            return list;
        }

        private string? TalkBlocked(GameState s)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null || s.Encounter != null) return "先处理眼前的敌踪";
            if (s.Main != null || s.Sub != null) return "行动进行中";
            return null;
        }

        /// <summary>
        /// 与 NPC 交谈（0 分钟，不扣物、不恢复、不改身体）。程望：首次播“营区路线”，写 visitor_met、camp_route_known（只此一次），
        /// 立即重判 QMAP01（先访过村庄的此刻就开边），之后播饱饱/主角的交谈后对白；再次交谈只播“再问路线”，不重发任何效果。
        /// </summary>
        public ActionResult TalkNpc(GameState s, string npcId)
        {
            var v = Config.Prologue.Visitor;
            if (!PrologueEnabled || npcId != v.NpcId) return ActionResult.Fail("这里没有这个人");
            if (!VisitorRegistered(s) || s.CurrentLocation != v.LocationId) return ActionResult.Fail($"{v.Name}不在这里");
            var why = TalkBlocked(s);
            if (why != null) return ActionResult.Fail(why);
            var t = Config.Prologue.Talk;
            var p = s.Prologue;
            p.VisitorTalks++;
            if (!p.VisitorArrivalShown)
            {
                p.VisitorArrivalShown = true;
                OpenDialogue(s, p.VisitorLate && !string.IsNullOrEmpty(t.ArrivalLate) ? t.ArrivalLate : t.ArrivalCamp);
            }
            if (s.Progress.Flags.Contains(FactCampRouteKnown))
            {
                OpenReplay(s, t.RouteRepeat);
                return ActionResult.Success();
            }
            s.Progress.Flags.Add(FactVisitorMet);
            s.Progress.Flags.Add(FactCampRouteKnown);
            p.VisitorMetAt = s.GameMinute;
            OpenDialogue(s, t.RouteFirst);
            if (StoryActive(s) && s.Story.BaobaoPresent) OpenDialogue(s, t.AfterRouteBaobao);
            else OpenDialogue(s, t.AfterRouteWithoutBaobao);
            var r = RefreshGates(s);
            var routes = RouteNotes(s, r, s.CurrentLocation);
            Emit(s, GameEventKind.Info, "已记录营区路线：废弃村庄南口→旧公路→检查哨→营门（查看路线不移动角色）" + (routes.Count > 0 ? "；发现" + string.Join("与", routes) : "") + "。");
            PrologueBoundary(s);
            return ActionResult.Success();
        }

        /// <summary>可重读的对白组（路线日志、路标、残页、再问路线、先准备）：每次一段新对话记录，只回放台词，不重发任何效果。</summary>
        private void OpenReplay(GameState s, string setId)
        {
            if (string.IsNullOrEmpty(setId)) return;
            if (!s.Story.DialogueLog.Any(d => d.EventId == setId)) { OpenDialogue(s, setId); return; }
            s.Prologue.ReplaySeq++;
            OpenDialogue(s, setId, $"{setId}#{s.Prologue.ReplaySeq}");
        }

        /// <summary>对话记录用的对白组名称（序章组读配置；其他返回 null 由界面兜底）。</summary>
        public string? DialogueSetTitle(string eventId)
        {
            var id = eventId.Split('#')[0];
            return Config.Prologue.DialogueSets.FirstOrDefault(d => d.Id == id)?.Title;
        }

        // ───────────────────────── 文书与路标 ─────────────────────────

        public List<DocumentCardView> DocumentsHere(GameState s)
        {
            var list = new List<DocumentCardView>();
            if (!PrologueEnabled) return list;
            foreach (var d in Config.Prologue.Documents.Where(d => d.LocationId == s.CurrentLocation))
            {
                string status;
                if (d.TaskId != null) status = TaskDone(s, d.TaskId) ? "已读过 · 可重读" : "可查看 · 不耗时";
                else if (d.Fact != null && s.Progress.Flags.Contains(d.Fact)) status = "已识别 · 可重读";
                else status = s.Progress.Flags.Contains(FactCampRouteKnown) ? "可识别 · 不耗时" : "字迹模糊 · 可查看";
                list.Add(new DocumentCardView { Id = d.Id, Title = d.Title, Status = status, ReadBlocked = TalkBlocked(s) });
            }
            return list;
        }

        /// <summary>
        /// 查看固定文书（0 分钟）：救助残页首读锁存 PRO15；南口路标在已知营区路线时识别并写永久凭证 south_sign_known（只此一次，
        /// 开边由 QMAP01 负责，路标不重复发任何东西），不知道路线时只看到模糊字迹、不写凭证。重读只回放台词。
        /// </summary>
        public ActionResult ReadDocument(GameState s, string docId)
        {
            var d = Config.Prologue.Documents.FirstOrDefault(x => x.Id == docId);
            if (d == null) return ActionResult.Fail("没有这份文书");
            if (s.CurrentLocation != d.LocationId) return ActionResult.Fail($"要在{LocName(d.LocationId)}才能查看");
            var why = TalkBlocked(s);
            if (why != null) return ActionResult.Fail(why);
            if (d.Fact != null && !string.IsNullOrEmpty(d.UnknownDialogueSet) && !s.Progress.Flags.Contains(FactCampRouteKnown))
            {
                OpenReplay(s, d.UnknownDialogueSet!);
                return ActionResult.Success();
            }
            OpenReplay(s, d.DialogueSet);
            if (d.Fact != null && s.Progress.Flags.Add(d.Fact)) Emit(s, GameEventKind.Info, $"已识别{d.Title}：旧公路→检查哨→营门（永久凭证，不占物资卡）。");
            if (d.TaskId != null) CompleteTask(s, d.TaskId, "read:" + d.Id);
            PrologueBoundary(s);
            return ActionResult.Success();
        }

        /// <summary>求医路线日志（camp_route_known 后可随时重读；阅读不移动、不揭开未知、不替你探索）。</summary>
        public ActionResult OpenRouteLog(GameState s)
        {
            if (!s.Progress.Flags.Contains(FactCampRouteKnown)) return ActionResult.Fail("还没有得知营区路线");
            if (s.Progress.Dead) return ActionResult.Fail("角色已死亡");
            if (DialogueLocked(s)) return ActionResult.Fail(DialogueLockedReason);
            if (InCombatFlow(s)) return ActionResult.Fail("先处理眼前的敌踪");
            OpenReplay(s, Config.Prologue.Talk.RouteLog);
            return ActionResult.Success();
        }

        // ───────────────────────── PRO17 远行确认 ─────────────────────────

        /// <summary>远行确认预览（只读）：已知基础路线与分段分钟（合计只是已知边基础合计，不是全旅程预算）、全员状态、随身与建议缺项。</summary>
        public JourneyPreview JourneyPreviewOf(GameState s)
        {
            var j = Config.Prologue.Journey;
            var pv = new JourneyPreview { BaseMinutesTotal = j.BaseMinutesTotal, Confirmed = TaskDone(s, "PRO17") };
            foreach (var l in j.Legs)
                pv.Legs.Add((l.From, l.To, l.BaseMinutes, _edges.TryGetValue(l.EdgeId, out var e) && EdgeOpen(s, e)));
            var carried = s.Items.Where(i => PlaceOf(s, i) == ItemInstance.Carried && !IsSpoiled(s, i)).ToList();
            pv.CleanWater = carried.Where(i => i.ItemId == "clean_water").Sum(i => i.Count);
            pv.Food = carried.Where(i => _items.TryGetValue(i.ItemId, out var c) && c.Use != null && !c.Use.IsMedicine && c.Use.Satiety > 0 && !c.RequiresCooking).Sum(i => i.Count);
            pv.HasWeapon = HasDamageWeaponEquipped(s);
            if (pv.CleanWater < j.SuggestedCleanWater) pv.Missing.Add($"净水{j.SuggestedCleanWater}份（现有{pv.CleanWater}）");
            if (pv.Food < j.SuggestedFood) pv.Missing.Add($"可直接吃的食物{j.SuggestedFood}份（现有{pv.Food}）");
            if (j.SuggestedWeapon && !pv.HasWeapon) pv.Missing.Add("装备一件防身武器");
            foreach (var a in s.PartyActors)
            {
                var st = a.Stats;
                pv.PartyLines.Add($"{ActorDisplayName(s, a.Id)}：健康{Fmt.N(st.Health)}/{Fmt.N(st.HealthMax)} 饱食{Fmt.N(st.Satiety)} 水分{Fmt.N(st.Hydration)} 体力{Fmt.N(st.Energy)}/{Fmt.N(st.EnergyMax)} 清醒{Fmt.N(st.Wakefulness)} 精神{Fmt.N(st.Morale)} 免疫{Fmt.N(a.Body.Immunity)} 病毒{(a.Body.Infected ? Fmt.N(a.Body.VirusLoad) : "未感染")}");
                foreach (StatId id in Enum.GetValues(typeof(StatId)))
                    if (Config.Thresholds.TryGetValue(StatKey(id), out var th) && th.IsBelow(st.Get(id)))
                        pv.Warnings.Add($"{ActorDisplayName(s, a.Id)}的{StatName(id)}偏低，建议先恢复");
            }
            foreach (var i in carried.Where(i => i.IsCarried && !i.InContainer).OrderBy(i => i.InstanceId))
                pv.CarryLines.Add($"{ItemName(i.ItemId)}×{i.Count}{(i.Durability != null ? $"（耐久{DurabilityText(i)}）" : "")}");
            foreach (var slot in EquipSlots.All)
            {
                var it = s.Items.FirstOrDefault(i => i.Owner == ItemInstance.EquipOwner(GameState.PlayerId, slot));
                if (it != null) pv.CarryLines.Add($"{EquipSlots.Name(slot)}：{ItemName(it.ItemId)}{(it.Durability != null ? $"（耐久{DurabilityText(it)}）" : "")}");
            }
            pv.ConfirmBlocked = !s.Progress.Flags.Contains(FactCampRouteKnown) ? "先在营地与来客交谈，得知营区路线"
                : s.Progress.Dead ? "角色已死亡" : DialogueLocked(s) ? DialogueLockedReason : InCombatFlow(s) ? "先处理眼前的敌踪" : null;
            pv.CanSayNotReady = pv.ConfirmBlocked == null && NpcsHere(s).Count > 0;
            return pv;
        }

        /// <summary>PRO17“准备好了”：看过路线与预览后的知情确认（缺建议物也可继续），只锁存任务，不赠物、不强吃、不自动拿地面物资。</summary>
        public ActionResult ConfirmJourney(GameState s)
        {
            var pv = JourneyPreviewOf(s);
            if (pv.ConfirmBlocked != null) return ActionResult.Fail(pv.ConfirmBlocked);
            CompleteTask(s, "PRO17", "confirm:journey");
            PrologueBoundary(s);
            return ActionResult.Success();
        }

        /// <summary>远行确认里的“先准备”：程望在场时播 visitor_not_ready（不写任何门槛，可重复）。</summary>
        public ActionResult SayNotReady(GameState s)
        {
            var pv = JourneyPreviewOf(s);
            if (!pv.CanSayNotReady) return ActionResult.Fail(pv.ConfirmBlocked ?? "来客不在这里");
            OpenReplay(s, Config.Prologue.Talk.NotReady);
            return ActionResult.Success();
        }

        // ───────────────────────── 主目标与建议 ─────────────────────────

        /// <summary>任务现在是否相关（不相关的不进建议，任务册写原因）。</summary>
        private string? TaskIrrelevantReason(GameState s, string id)
        {
            var f = s.Progress.Flags;
            switch (id)
            {
                case "PRO04": return s.Story.Legacy ? "旧存档按已有资格等效首战" : null;
                case "PRO05":
                    if (s.Story.Legacy || !StoryActive(s)) return "旧存档没有首战教学物质（不补发）";
                    if (string.IsNullOrEmpty(s.Story.TutorialGrantEventId)) return "首战胜利后出现";
                    if (!s.Items.Any(i => i.Source?.TutorialReward == true)) return "教学物质已不在（不补发）";
                    return null;
                case "PRO16": return VisitorRegistered(s) ? null : "来客还没到";
                case "PRO17":
                case "PRO18": return f.Contains(FactCampRouteKnown) ? null : "得知营区路线后开始";
                default: return null;
            }
        }

        public List<PrologueTaskView> PrologueTasks(GameState s) => Config.Prologue.Tasks.Select(t =>
        {
            var rec = s.Prologue.Tasks.TryGetValue(t.Id, out var r) ? r : null;
            var why = rec == null ? TaskIrrelevantReason(s, t.Id) : null;
            return new PrologueTaskView
            {
                Config = t, Done = rec != null, CompletedAt = rec?.CompletedAt, Retro = rec?.Retro ?? false,
                Relevant = rec == null && why == null, Note = why, HintDismissed = s.Prologue.DismissedHints.Contains(t.Id),
            };
        }).ToList();

        private static readonly string[] StageOrder = { "D1", "D2", "D3", "D4", "D5", "mainline" };

        /// <summary>重算阶段、主目标与建议（只在安全边界调用并存档）。优先级：来客交谈 → 出区路线 → 首战 → 当前阶段最早未完成 → 等待与整备。</summary>
        private void RefreshObjective(GameState s)
        {
            var p = s.Prologue;
            var f = s.Progress.Flags;
            var open = PrologueTasks(s).Where(t => t.Relevant).Select(t => t.Config).ToList();
            string stage, obj;
            if (VisitorRegistered(s) && !f.Contains(FactVisitorMet))
            {
                stage = "D5";
                obj = s.CurrentLocation == Config.Prologue.Visitor.LocationId ? "visitor_talk" : "visitor_return";
            }
            else if (f.Contains(FactCampRouteKnown))
            {
                stage = "mainline";
                var exit = Config.Prologue.Exit;
                var exitEdge = _edges.TryGetValue(exit.EdgeId, out var e) ? e : null;
                if (Visited(s, exit.To)) obj = "route_onward";
                else if (exitEdge != null && EdgeOpen(s, exitEdge)) obj = "exit_go";
                else obj = "exit_need";
            }
            else if (!s.Story.TutorialWon && !s.Story.Legacy)
            {
                stage = "D1";
                obj = "PRO04";
            }
            else
            {
                var first = open.Where(t => t.Stage != "D5" && t.Stage != "mainline").OrderBy(t => Array.IndexOf(StageOrder, t.Stage)).ThenBy(t => t.Id).FirstOrDefault();
                stage = first?.Stage ?? "D5";
                obj = first?.Id ?? "settle";
            }
            p.StageId = stage;
            p.ObjectiveId = obj;
            var curIdx = Array.IndexOf(StageOrder, stage);
            p.Suggestions = open.Where(t => t.Id != obj && t.Id != "PRO16" && t.Id != "PRO18")
                .OrderBy(t => Math.Abs(Array.IndexOf(StageOrder, t.Stage) - curIdx) == 0 ? 0 : Array.IndexOf(StageOrder, t.Stage) < curIdx ? 1 : 2)
                .ThenBy(t => Array.IndexOf(StageOrder, t.Stage)).ThenBy(t => t.Id)
                .Take(2).Select(t => t.Id).ToList();
        }

        public PrologueStageConfig? StageOf(string id) => Config.Prologue.Stages.FirstOrDefault(x => x.Id == id);

        /// <summary>当前主目标（存档里的 ObjectiveId 展开成文字）。序章没配置为 null。</summary>
        public PrologueObjectiveView? CurrentObjective(GameState s)
        {
            if (!PrologueEnabled) return null;
            var p = s.Prologue;
            var v = Config.Prologue.Visitor;
            var exit = Config.Prologue.Exit;
            switch (p.ObjectiveId)
            {
                case "visitor_talk": return new PrologueObjectiveView { Id = p.ObjectiveId, TaskId = "PRO16", Title = $"与来客{v.Name}交谈", Step = $"在设施栏的人物卡上点“交谈”（不耗时）" };
                case "visitor_return": return new PrologueObjectiveView { Id = p.ObjectiveId, TaskId = "PRO16", Title = $"回{LocName(v.LocationId)}，与来客交谈", Step = $"有人在{LocName(v.LocationId)}等候；沿已知道路返回（不会传送，来客不会离开）" };
                case "exit_go": return new PrologueObjectiveView { Id = p.ObjectiveId, TaskId = "PRO18", Title = "前往幸存者营区寻找医疗帮助", Step = $"沿{LocName(exit.From)}南口前往{LocName(exit.To)}（要实际走过去）" };
                case "exit_need": return new PrologueObjectiveView { Id = p.ObjectiveId, TaskId = "PRO18", Title = "前往幸存者营区寻找医疗帮助", Step = ExitNeedText(s) };
                case "route_onward": return new PrologueObjectiveView { Id = p.ObjectiveId, Title = "前往幸存者营区寻找医疗帮助", Step = "旧公路→检查哨→营门→营区：沿道路一段一段走，边走边探清前路" };
                case "settle": return new PrologueObjectiveView { Id = p.ObjectiveId, Title = StageOf("D5")?.Headline ?? "", Step = "继续整备与可选探索" };
            }
            var t = ProTask(p.ObjectiveId);
            if (t == null) return null;
            return new PrologueObjectiveView { Id = t.Id, TaskId = t.Id, Title = StageOf(p.StageId)?.Headline ?? t.Title, Step = $"{t.Title}：{GoalText(t)}" };
        }

        /// <summary>出区还缺哪项（A10-TUTOR §6“日志显示缺哪项”）。</summary>
        public string ExitNeedText(GameState s)
        {
            var exit = Config.Prologue.Exit;
            if (_edges.TryGetValue(exit.EdgeId, out var e))
            {
                var why = GateBlockedReason(s, e);
                if (!string.IsNullOrEmpty(why)) return why;
            }
            return $"沿{LocName(exit.From)}南口前往{LocName(exit.To)}";
        }

        /// <summary>当前建议（最多两条）。</summary>
        public List<PrologueObjectiveView> CurrentSuggestions(GameState s) =>
            s.Prologue.Suggestions.Select(ProTask).Where(t => t != null)
                .Select(t => new PrologueObjectiveView { Id = t!.Id, TaskId = t.Id, Title = t.Title, Step = GoalText(t) }).ToList();

        /// <summary>任务的玩家可读说明（白话；旧配置没有时退回策划原句）。</summary>
        public static string GoalText(PrologueTaskConfig t) => string.IsNullOrEmpty(t.Goal) ? t.CompletionRule : t.Goal;

        /// <summary>
        /// 当前要显示的那一条中性系统提示（同一时刻最多一条）：主目标对应任务的 PROxx_hint，已关闭或已暂停提示时为 null。
        /// 提示不锁输入、不推进时间，关闭不等于完成。
        /// </summary>
        public (string TaskId, string Text)? CurrentHint(GameState s)
        {
            if (!PrologueEnabled || s.Prologue.HintsPaused || s.Progress.Dead) return null;
            var cur = CurrentObjective(s);
            var id = cur?.TaskId;
            if (id == null || TaskDone(s, id) || s.Prologue.DismissedHints.Contains(id)) return null;
            var t = ProTask(id);
            return t == null || string.IsNullOrEmpty(t.Hint) ? ((string, string)?)null : (id, t.Hint);
        }

        /// <summary>“关闭这条”：只记已关闭（不完成任务）；可在任务册回看。</summary>
        public void DismissHint(GameState s, string taskId) => s.Prologue.DismissedHints.Add(taskId);

        /// <summary>“暂停后续操作提示”/重新开启：只开关中性教学提示。</summary>
        public void SetHintsPaused(GameState s, bool paused) => s.Prologue.HintsPaused = paused;

        /// <summary>当前主目标要轻量点亮的对象（每次最多一个）：npc:ID、doc:ID、loc:ID；没有为 null。</summary>
        public string? ObjectiveFocus(GameState s)
        {
            var p = s.Prologue;
            var exit = Config.Prologue.Exit;
            switch (p.ObjectiveId)
            {
                case "visitor_talk": return "npc:" + Config.Prologue.Visitor.NpcId;
                case "visitor_return": return "loc:" + Config.Prologue.Visitor.LocationId;
                case "exit_go":
                    if (s.CurrentLocation == exit.From && !s.Progress.Flags.Contains(FactSouthSignKnown)) return "doc:village_south_sign";
                    return "loc:" + (s.CurrentLocation == exit.From ? exit.To : exit.From);
                case "PRO04": return "loc:" + Config.Story.TutorialBattle.LocationId;
                case "PRO15": return s.CurrentLocation == "village" ? "doc:relief_page_village" : "loc:village";
            }
            return null;
        }

        // ───────────────────────── 开发入口 ─────────────────────────

        /// <summary>开发入口（?dev=1）：把序章推到某一阶段（只改剧情/时间标记，不结算身体与世界）。</summary>
        public string DebugPrologue(GameState s, string step)
        {
            if (IsBusy(s)) return "开发：行动进行中或有敌踪/战斗，先结束";
            switch (step)
            {
                case "won":
                    if (s.Story.TutorialWon) return "开发：首战已经胜利";
                    s.Story.TutorialTriggered = true;
                    s.Story.TutorialWon = true;
                    s.Story.TutorialWonMinute = s.GameMinute;
                    PrologueBoundary(s);
                    return "开发：首战记为胜利（不发教学物质、不移除荒地游荡尸）。";
                case "d5":
                {
                    var at = Config.Prologue.Visitor.NotBeforeWorldMinute;
                    if (s.GameMinute < at) { s.GameMinute = at; ResetWorldCursors(s); }
                    PrologueBoundary(s);
                    return $"开发：时间已到{Clock.DayTime(s.GameMinute)}（只改时钟，不结算身体消耗）；满足条件时来客在此刻登记。";
                }
                case "camp":
                    return DebugMapTeleport(s, Config.Prologue.Visitor.LocationId);
                case "village":
                    return DebugMapTeleport(s, "village");
                default:
                    return "开发：未知序章步骤";
            }
        }
    }
}

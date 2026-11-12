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
        /// <summary>稳定 ID：任务 ID（PRO01—PRO18）或 visitor_talk / visitor_return / exit_go / exit_need / route_onward / settle / main_quest（B15a：营区登记后的当前主线）。</summary>
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>下一步可做的具体动作（中性说明）。</summary>
        public string Step { get; set; } = "";
        /// <summary>对应任务（没有为 null）。</summary>
        public string? TaskId { get; set; }
    }

    /// <summary>前进类主目标下一真实路段的玩家可知状态（Q139）。</summary>
    public enum TravelSegmentKind
    {
        /// <summary>下一段已知且已开通。</summary>
        KnownOpen,
        /// <summary>下一段已揭示（那一端已发现）但未开通。</summary>
        KnownBlocked,
        /// <summary>下一段未知，当前地点可探索且未满 100%。</summary>
        UnknownExplore,
        /// <summary>下一段未知，当前地点已满 100% 或不能探索。</summary>
        UnknownNoExplore,
        /// <summary>B22f（Q193）/ B22f2（Q208 选 A）：route_onward 在 m02、下一段营门未知、拦路战未胜（探索被禁）；下一步“先战胜检查哨拦路敌人。”，不点亮。</summary>
        UnknownFixedBlocked,
    }

    /// <summary>前进类主目标的下一真实路段（只读视图）。NodeId 只在已知时给，未知时不泄露。</summary>
    public sealed class TravelSegmentView
    {
        public TravelSegmentKind Kind { get; set; }
        /// <summary>下一段的另一端（已知时）；未知为 null。</summary>
        public string? NodeId { get; set; }
        /// <summary>主目标此刻指向的地点（内部用，界面不显示）。</summary>
        public string Target { get; set; } = "";
        /// <summary>已知未开通时的领域可公开原因（与相邻栏同一句）。</summary>
        public string Reason { get; set; } = "";
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
        /// <summary>
        /// 身份类型（A11 §6，B14）：visitor（序章来客）/ post（城市岗位）。固定队友、周期候选、应急补员在队伍与招募入口另标，不作岗位卡。
        /// </summary>
        public string Kind { get; set; } = "visitor";
        /// <summary>卡角标（“来客”“岗位”“代班”）。</summary>
        public string KindLabel { get; set; } = "";
        /// <summary>所属组织显示名（岗位）。</summary>
        public string FactionName { get; set; } = "";
        /// <summary>命名岗位人物已退场，当前是机构当班接续员代班（不是原人物复活）。</summary>
        public bool Substitute { get; set; }
        /// <summary>名下服务事项数（含未开放的入口）；0 为没有“事项”按钮。</summary>
        public int ServiceCount { get; set; }
        /// <summary>名下已显示且未完成的委托数（B15，计入“事项 N项”）。</summary>
        public int QuestCount { get; set; }
        /// <summary>名下有已显示的委托（含已完成可回看的）：有就出“事项”按钮。</summary>
        public bool HasQuests { get; set; }
        /// <summary>“事项 N项”与卡面“事项 a/b 项可办”的 b（同一口径，见 NpcItemCounts；M333）。</summary>
        public int ItemCount { get; set; }
        public string QuestText { get; set; } = "";
        public string RecruitText { get; set; } = "";
        /// <summary>主按钮的字（默认“交谈”；B15 同行邀约卡为“登记同行”）。</summary>
        public string ActionLabel { get; set; } = "交谈";
        /// <summary>悬停说明里的人物背景（同行邀约卡：固定队友正式背景句）。</summary>
        public string Background { get; set; } = "";
        /// <summary>B24（A10-CITY §6）：在场状态短标“在场”/“代班”（岗位无作息数据，在岗一律“在场”，离岗由代班接手）；来客为空。</summary>
        public string Presence { get; set; } = "";
        /// <summary>B24（A10-CITY §6）：可接/可交任务短标（“可接1 · 可交1”）；没有为空。只定位，不自动接任务。</summary>
        public string QuestMarks { get; set; } = "";
        /// <summary>
        /// Q312（B39）：「交谈」当前可执行且这次确实会接取一项委托时的预告「这次交谈会接取「{任务名}」。」；只会闲谈、介绍、交付或受阻时为 null。
        /// 壳在推进中（行动进行中）也应省略。只读，执行仍走原交谈路径与重验。
        /// </summary>
        public string? TalkPreview { get; set; }
    }

    /// <summary>设施栏的固定文书/路标卡（PRO15 救助残页、南口路标）。</summary>
    public sealed class DocumentCardView
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public string? ReadBlocked { get; set; }
        /// <summary>这张文书自己不能看的原因（缺前置事实、熟练不够）；行动、暂停、对白这些全局原因不算，只在 ReadBlocked。</summary>
        public string? OwnBlocked { get; set; }
        /// <summary>卡上的状态行（Q259 选 A）：只写文书自身状态，全局原因由按钮禁用和悬停表达。</summary>
        public string CardLine => OwnBlocked ?? Status;
        /// <summary>需先选一项才提交的告示（r07 观察区公开通道告示，B14）；空为直接查看。</summary>
        public List<CityChoiceView> Choices { get; set; } = new List<CityChoiceView>();
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
    /// 南口路标读完写永久凭证 south_sign_known（可选阅读）；PRO18 = 实际到达旧公路（Q131 选 B，B13d）。</item>
    /// <item>常驻 1 个主目标 + 最多 2 条建议，只在内核安全边界重算（存 StageId/ObjectiveId/Suggestions）；标题与下一步用策划正式短句（Q129 选 B）。</item>
    /// <item>操作前引导（Q128 选 C，B13d）：玩家打开相关界面/选中相关对象、动作提交前，按上下文给该项系统提示，首次一并播饱饱/主角短句；
    /// 完成后只给 PROxx_done 简短结果，不再从完成事件排队播反应。忙时暂存、下一安全边界重验，已完成/失效即丢弃。</item>
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

        private Dictionary<string, PrologueTaskConfig>? _proTasks;
        private Dictionary<string, PrologueTaskConfig> ProTasks => _proTasks ??= Config.Prologue.Tasks.ToDictionary(t => t.Id);

        public PrologueTaskConfig? ProTask(string id) => ProTasks.TryGetValue(id, out var t) ? t : null;

        public static bool TaskDone(GameState s, string id) => s.Prologue.Tasks.ContainsKey(id);

        /// <summary>序章是否有配置（旧配置/测试配置没有 prologue 节时整套不生效）。</summary>
        public bool PrologueEnabled => Config.Prologue.Tasks.Count > 0;

        // ───────────────────────── 任务账 ─────────────────────────

        /// <summary>
        /// 锁存一项任务：只写 completedAt/eventId，记一条非模态完成反馈（PROxx_done 简短结果）。Q128 起不再从这里排饱饱反应：
        /// 反应只在操作前的相关上下文播（<see cref="GuideContext"/>）；这一项还挂着的引导与暂存一并作废，不在完成后补播。
        /// 已完成的不重复写、不重复发反馈；读档、重放回调不会再触发。返回是否新完成。
        /// </summary>
        private bool CompleteTask(GameState s, string id, string eventId, bool retro = false)
        {
            if (!PrologueEnabled || s.Progress.Dead) return false;
            if (s.Prologue.Tasks.ContainsKey(id)) return false;
            var t = ProTask(id);
            if (t == null) return false;
            s.Prologue.Tasks[id] = new PrologueTaskRecord { CompletedAt = s.GameMinute, EventId = eventId, Retro = retro };
            if (s.Prologue.GuideActive == id) s.Prologue.GuideActive = "";
            if (s.Prologue.GuidePending == id) { s.Prologue.GuidePending = ""; s.Prologue.GuidePendingAt = ""; }
            if (retro) return true;
            // 完成反馈（PROxx_done）是中性系统反馈：非模态，只在任务角短暂显示、任务册留完成时间；不进行动日志、不当行动结果（A10-TUTOR §2）
            s.Prologue.LastDoneTask = id;
            s.Prologue.LastDoneAt = s.GameMinute;
            return true;
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
            // Q131 选 B：PRO18 只看本局实际到过旧公路；南口路标（south_sign_known）只是可选阅读与凭证，不是条件
            if (!string.IsNullOrEmpty(exit.To) && Visited(s, exit.To)) CompleteTask(s, "PRO18", "travel:" + exit.To, retro);
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
            // 读档本身不触发引导（Q128：未示的引导等之后真实相关的界面/上下文，不在加载时一次排出）
            PrologueBoundary(s, guides: false);
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

        /// <summary>PRO12：已提交的睡眠段累计（等待/休息不算）。引导在打开恢复面板/选睡眠时（操作前），不在开始睡眠时（Q128）。</summary>
        private void NoteSleepSegment(GameState s, int minutes)
        {
            s.Prologue.SleepMinutes += minutes;
            if (s.Prologue.SleepMinutes >= Pro12SleepMinutes) CompleteTask(s, "PRO12", "sleep:120");
        }

        /// <summary>移动到达后的序章事务：PRO14 往返、出区抵达与重判。到达溪流/村庄的引导由随后的安全边界按上下文给（Q128）。</summary>
        private void NoteArrive(GameState s, string from, string to)
        {
            if (!PrologueEnabled) return;
            var camp = Config.Prologue.Visitor.LocationId;
            if (to == "village" || to == "clinic") s.Prologue.ReachedVillageOrClinic = true;
            if (to == camp && s.Prologue.ReachedVillageOrClinic) CompleteTask(s, "PRO14", $"travel:{from}->{to}");
            SyncPrologueTasks(s);
        }

        // ───────────────────────── 安全边界 ─────────────────────────

        private bool PrologueIdle(GameState s) =>
            !s.Progress.Dead && s.Main == null && s.Sub == null && s.Battle == null && !DialogueLocked(s);

        /// <summary>
        /// 序章安全边界（段末、战斗结束、即时操作、对话结束、读档迁移、新局）：追认任务 → 来客登记与通知/抵达对白 →（空闲时）重验暂存的引导、
        /// 给当前状态相关的引导 → 重算主目标。不推进时间、不发物资。幂等。guides=false 时（读档迁移）不触发任何引导。
        /// </summary>
        public void PrologueBoundary(GameState s, bool guides = true)
        {
            if (!PrologueEnabled || s.Progress.Dead) return;
            SyncPrologueTasks(s);
            CheckVisitor(s);
            RefreshObjective(s);
            if (guides && GuideSafe(s) && !FlushPendingGuide(s)) StateGuide(s);
        }

        // ───────────────────────── 操作前引导（Q128 选 C） ─────────────────────────

        /// <summary>
        /// 界面在真实上下文里调用（0 分钟，不改玩法状态）：打开/选中相关界面或对象、动作提交前，按上下文给一项未完成任务的系统提示，
        /// 该项首次呈现时一并播饱饱/主角短句（饱饱在体内且非旧档、提示未暂停才播；不在完成后补播）。
        /// <list type="bullet">
        /// <item>ctx：item（id=物品，owner=所在）、fire（火堆）、resource（id=资源点）、location（id=地点）、recipe（id=配方，手工制作面板）、
        /// process（id=自动加工）、gear（整备）、rest（恢复面板/选睡眠）、journey（远行准备）。</item>
        /// <item>同一上下文多项相关时按 PRO 编号取未示过的最前一项，只呈现一项；其余等之后新的实际上下文或在任务册查看。</item>
        /// <item>提示已暂停：什么都不记（不积压）；已关闭的项不再自动出；行动中/对话中/敌踪战斗中：暂存，下一安全边界重验仍相关才呈现。</item>
        /// </list>
        /// 返回状态是否改变（界面据此存档）。
        /// </summary>
        public bool GuideContext(GameState s, string ctx, string? id = null, string? owner = null)
        {
            if (!PrologueEnabled || s.Progress.Dead || s.Prologue.HintsPaused) return false;
            var p = s.Prologue;
            var cands = GuideCandidates(s, ctx, id, owner).Distinct().Where(t => GuideOpen(s, t)).OrderBy(t => t, StringComparer.Ordinal).ToList();
            if (cands.Count == 0) return false;
            var pick = cands.FirstOrDefault(t => !p.GuideSeen.Contains(t)) ?? (cands.Contains(p.GuideActive) ? p.GuideActive : cands[0]);
            if (!GuideSafe(s))
            {
                if (p.GuidePending == pick && p.GuidePendingAt == s.CurrentLocation) return false;
                p.GuidePending = pick;
                p.GuidePendingAt = s.CurrentLocation;
                return true;
            }
            if (p.GuideActive == pick && p.GuideActiveAt == s.CurrentLocation && p.GuideSeen.Contains(pick)) return false;
            ActivateGuide(s, pick);
            return true;
        }

        /// <summary>某上下文可能相关的任务（未按完成/相关过滤）。</summary>
        private IEnumerable<string> GuideCandidates(GameState s, string ctx, string? id, string? owner)
        {
            var here = s.CurrentLocation;
            switch (ctx)
            {
                case "item":
                {
                    if (id == null || !_items.TryGetValue(id, out var ic)) yield break;
                    var u = ic.Use;
                    // PRO02：可直接食用/饮用（脏水等会致病的不算，交给 PRO08“先处理”）
                    if (u != null && !u.IsMedicine && !u.IsCare && !u.CausesDiarrhea && !ic.RequiresCooking && (u.Satiety > 0 || u.Hydration > 0)) yield return "PRO02";
                    var groundOrCarried = owner == ItemInstance.Carried || owner == here;
                    // PRO03：随身/地面上的伤害型武器卡（卡下有装备入口）
                    if (groundOrCarried && _weapons.TryGetValue(id, out var w) && (w.WeaponKind == null || !NonDamageKinds.Contains(w.WeaponKind))) yield return "PRO03";
                    // PRO08：随身/锅里的脏水，或锅本身（净水配置）
                    if ((id == "dirty_water" && owner != here) || ic.ToolTag == "pot") yield return "PRO08";
                    // PRO09：生块根食材卡
                    if (id == RootItemId) yield return "PRO09";
                    // PRO11：可在地面与随身之间转移的物资卡
                    if (groundOrCarried) yield return "PRO11";
                    yield break;
                }
                case "fire":
                    yield return "PRO07";
                    yield return "PRO08";
                    yield break;
                case "resource":
                {
                    var res = id == null ? null : Config.Resources.FirstOrDefault(r => r.NodeId == id);
                    if (res == null) yield break;
                    if (res.Outputs.Keys.Any(WaterOutputs.Contains)) yield return "PRO06";
                    if (res.Outputs.ContainsKey(RootItemId)) yield return "PRO09";
                    yield break;
                }
                case "location":
                {
                    if (id == null || id == here || !Known(s, id)) yield break;
                    if (id == "stream" && !Visited(s, "stream")) yield return "PRO06";
                    var camp = Config.Prologue.Visitor.LocationId;
                    if (((id == "village" || id == "clinic") && !s.Prologue.ReachedVillageOrClinic) || (id == camp && s.Prologue.ReachedVillageOrClinic)) yield return "PRO14";
                    var exit = Config.Prologue.Exit;
                    if (id == exit.From || id == exit.To) yield return "PRO18";
                    yield break;
                }
                case "recipe":
                {
                    var rc = id == null ? null : Config.Recipes.FirstOrDefault(r => r.Id == id);
                    if (rc == null) yield break;
                    if (rc.FacilityResult == "shelterBuilt") yield return "PRO13";
                    if (rc.Station != "hand" || rc.IsBuild || rc.IsRepair || rc.IsSalvage) yield break;
                    if (rc.Outputs.Keys.Any(o => _weapons.TryGetValue(o, out var rw) && (rw.WeaponKind == null || !NonDamageKinds.Contains(rw.WeaponKind)))) yield return "PRO03";
                    // 打开手工制作即“手工制作”上下文（PRO10）
                    yield return "PRO10";
                    yield break;
                }
                case "process":
                    if (id == "boil_water") yield return "PRO08";
                    if (id == "roast_root" || id == "boil_root") yield return "PRO09";
                    yield break;
                case "gear": yield return "PRO03"; yield break;
                case "rest": yield return "PRO12"; yield break;
                case "journey": yield return "PRO17"; yield break;
            }
        }

        /// <summary>这项引导现在还能给吗：未完成、没关闭、任务相关，且引导自身的前提（地点、来客、路线）成立。</summary>
        private bool GuideOpen(GameState s, string id)
        {
            if (TaskDone(s, id) || s.Prologue.DismissedHints.Contains(id) || ProTask(id) == null || TaskIrrelevantReason(s, id) != null) return false;
            var f = s.Progress.Flags;
            switch (id)
            {
                case "PRO13": return Config.Locations.FirstOrDefault(l => l.Id == s.CurrentLocation)?.CanBuildShelter == true;
                case "PRO15": return s.CurrentLocation == "village";
                case "PRO16": return VisitorRegistered(s) && !f.Contains(FactVisitorMet);
                case "PRO18": return f.Contains(FactCampRouteKnown) && !Visited(s, Config.Prologue.Exit.To);
                default: return true;
            }
        }

        /// <summary>安全上下文：不在对话读完锁、敌踪/战斗、子行动或进行中的主行动里（暂停中的主行动不算忙）。</summary>
        private bool GuideSafe(GameState s) =>
            !s.Progress.Dead && !DialogueLocked(s) && s.Battle == null && s.Sub == null
            && (s.Main == null || s.Main.Status == ActionStatus.Paused);

        /// <summary>呈现一项引导：成为当前系统提示；该项首次呈现时播饱饱/主角短句（饱饱在体内且非旧档才播；同组对白只播一次）。</summary>
        private void ActivateGuide(GameState s, string id)
        {
            var p = s.Prologue;
            p.GuideActive = id;
            p.GuideActiveAt = s.CurrentLocation;
            if (p.GuidePending == id) { p.GuidePending = ""; p.GuidePendingAt = ""; }
            if (!p.GuideSeen.Add(id)) return;
            var t = ProTask(id);
            if (t == null || string.IsNullOrEmpty(t.ReactionSet) || !StoryActive(s) || !s.Story.BaobaoPresent) return;
            if (s.Story.DialogueLog.Any(d => d.EventId == t.ReactionSet)) return;
            OpenDialogue(s, t.ReactionSet);
        }

        /// <summary>重验忙时暂存的引导：仍在原地点、未完成且相关就呈现，否则丢弃。返回是否呈现了。</summary>
        private bool FlushPendingGuide(GameState s)
        {
            var p = s.Prologue;
            if (string.IsNullOrEmpty(p.GuidePending)) return false;
            var id = p.GuidePending;
            var at = p.GuidePendingAt;
            p.GuidePending = "";
            p.GuidePendingAt = "";
            if (p.HintsPaused || at != s.CurrentLocation || !GuideOpen(s, id)) return false;
            ActivateGuide(s, id);
            return true;
        }

        /// <summary>
        /// 由当前状态构成的上下文（安全边界时检查，各项只首次）：开场剧情读完后的首次可操作界面（PRO01，新局）、首战奖励落地且胜利剧情读完后（PRO05）、
        /// 真实到达溪流尚未取水（PRO06）、真实到达村庄残页可查看（PRO15）、营地出现来客人物卡尚未交谈（PRO16）。一次只给一项。
        /// </summary>
        private void StateGuide(GameState s)
        {
            var p = s.Prologue;
            // 地点类上下文错过后，离开那里才算失效（之后再到达是新的上下文）
            foreach (var k in p.GuideMissed.Where(kv => kv.Value != "" && kv.Value != s.CurrentLocation).Select(kv => kv.Key).ToList()) p.GuideMissed.Remove(k);
            foreach (var id in new[] { "PRO01", "PRO05", "PRO06", "PRO15", "PRO16" })
            {
                if (p.GuideSeen.Contains(id) || p.GuideMissed.ContainsKey(id) || !GuideOpen(s, id)) continue;
                var ok = id switch
                {
                    "PRO01" => !PrePrologueRun(s) && (s.Story.AwakeningDone || !StoryActive(s)),
                    "PRO06" => s.CurrentLocation == "stream" && !p.DrewWater,
                    // 旧档（Legacy/迁移档）由是否主动点“交谈”选择接续（Q132），人物卡出现时不自动弹短句；系统提示仍随主目标显示
                    "PRO16" => s.CurrentLocation == Config.Prologue.Visitor.LocationId && !OptInVisitor(s),
                    _ => true,
                };
                if (!ok) continue;
                if (p.HintsPaused)
                {
                    // 试玩修复 M298（Q128“暂停……不积压”）：暂停期间到来的状态上下文记为错过，恢复后不在别处补弹；
                    // 地点类（溪流/村庄/营地人物卡）到离开那里为止，开场与首战奖励这类一次性的永久错过
                    p.GuideMissed[id] = id is "PRO06" or "PRO15" or "PRO16" ? s.CurrentLocation : "";
                    continue;
                }
                ActivateGuide(s, id);
                return;
            }
        }

        /// <summary>首战建立（原首战警告之后、首次行动结算之前）的首战教学上下文：PRO04 引导与饱饱短句（Q128；战斗中直接呈现，不走忙时暂存）。</summary>
        private void TutorialBattleGuide(GameState s)
        {
            if (!PrologueEnabled || s.Prologue.HintsPaused || !GuideOpen(s, "PRO04")) return;
            ActivateGuide(s, "PRO04");
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

        /// <summary>
        /// 这一局开局时是否还没有序章系统（Q138 选 A）：只认可信来源标记——Legacy（0.2 原型档、schema 5 及以前迁入剧情系统的档），
        /// 或 <see cref="PrologueState.MigratedAt"/>（从 schema 21 及以前迁入序章系统，21→22 迁移时写入）。B13 序章上线后新开的局（存档 22 起）
        /// 两个标记都没有，之后升级到 25、26 或未来版本也不会补写，所以不按“读档时升过版本/当前 schema 编号”判断。
        /// </summary>
        public static bool PrePrologueRun(GameState s) => s.Story.Legacy || s.Prologue.MigratedAt != null;

        /// <summary>旧档“可选择接续”（Q132，经 Q138 细化）：序章前开局的档来客不自动弹抵达对白与 PRO16 短句，点“交谈”才接续；序章后新局照常自动抵达。</summary>
        public static bool OptInVisitor(GameState s) => PrePrologueRun(s);

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
            // 旧档“可选择接续序章”（S15 §7；Q138 细化）：Legacy 与从 schema 21 及以前迁入序章系统的档不自动弹抵达对白，营地设施栏出现人物卡，
            // 玩家点“交谈”时才播（开发假设 B13-H6；试玩修复 M269 P4）。序章后新开的局（22 起）升级到 25/26 仍照常自动抵达
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
            var list = CityNpcsHere(s).ToList();
            list.AddRange(RecruitCardsHere(s));
            var v = Config.Prologue.Visitor;
            if (!PrologueEnabled || string.IsNullOrEmpty(v.NpcId) || !VisitorRegistered(s) || s.CurrentLocation != v.LocationId) return list;
            var known = s.Progress.Flags.Contains(FactCampRouteKnown);
            list.Insert(0, new NpcCardView
            {
                NpcId = v.NpcId, Name = v.Name, Role = v.Role, KindLabel = known ? "可交谈" : "来客",
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
            if (s.Battle != null) return "先处理眼前的敌踪";
            if (s.Main != null || s.Sub != null) return "行动进行中";
            return null;
        }

        /// <summary>
        /// 文档卡“查看”的全局拒绝原因（Q269 选 A，q269-q273-display-loot-adjudications.json documents.policy）：同 <see cref="TalkBlocked"/>
        /// 的原因和优先级，只在原分支是主行动占用且处于暂停（没有子行动在推进）时改写“行动暂停中”；实际推进仍写“行动进行中”。
        /// </summary>
        private string? DocumentBlocked(GameState s)
        {
            var why = TalkBlocked(s);
            if (why == "行动进行中" && s.Sub == null && s.Main?.Status == ActionStatus.Paused) return DocumentPausedReason;
            return why;
        }

        /// <summary>主行动暂停时文档卡“查看”的禁用提示（Q269，合同 documents.pausedReason）。</summary>
        public const string DocumentPausedReason = "行动暂停中";

        /// <summary>
        /// 与 NPC 交谈（0 分钟，不扣物、不恢复、不改身体）。程望：首次播“营区路线”，写 visitor_met、camp_route_known（只此一次），
        /// 立即重判 QMAP01（先访过村庄的此刻就开边），之后播饱饱/主角的交谈后对白；再次交谈只播“再问路线”，不重发任何效果。
        /// </summary>
        public ActionResult TalkNpc(GameState s, string npcId)
        {
            if (CityNpc(npcId) is CityNpcConfig cn) return TalkCityNpc(s, cn);
            if (RecruitQuestOf(npcId) is QuestConfig rq) return AcceptQuest(s, rq.Id);
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
            return Config.Prologue.DialogueSets.FirstOrDefault(d => d.Id == id)?.Title ?? CityDialogueSetTitle(id);
        }

        // ───────────────────────── 文书与路标 ─────────────────────────

        public List<DocumentCardView> DocumentsHere(GameState s)
        {
            var list = CityNoticesHere(s).ToList();
            if (!PrologueEnabled) return list;
            foreach (var d in Config.Prologue.Documents.Where(d => d.LocationId == s.CurrentLocation))
            {
                string status;
                if (d.TaskId != null) status = TaskDone(s, d.TaskId) ? "已读过 · 可重读" : "可查看 · 不耗时";
                else if (d.Fact != null && s.Progress.Flags.Contains(d.Fact)) status = "已识别 · 可重读";
                else status = s.Progress.Flags.Contains(FactCampRouteKnown) ? "可识别 · 不耗时" : "字迹模糊 · 可查看";
                list.Add(new DocumentCardView { Id = d.Id, Title = d.Title, Status = status, ReadBlocked = DocumentBlocked(s) });
            }
            return list;
        }

        /// <summary>
        /// 查看固定文书（0 分钟）：救助残页首读锁存 PRO15；南口路标在已知营区路线时识别并写永久凭证 south_sign_known（只此一次，
        /// 开边由 QMAP01 负责，路标不重复发任何东西），不知道路线时只看到模糊字迹、不写凭证。重读只回放台词。
        /// </summary>
        public ActionResult ReadDocument(GameState s, string docId) => ReadDocument(s, docId, null);

        /// <summary>查看文书/告示；城市告示（B14）有选项时 choiceId 为所选一项。</summary>
        public ActionResult ReadDocument(GameState s, string docId, string? choiceId)
        {
            if (Config.City.Notices.FirstOrDefault(x => x.Id == docId) is CityNoticeConfig cn) return ReadCityNotice(s, cn, choiceId);
            var d = Config.Prologue.Documents.FirstOrDefault(x => x.Id == docId);
            if (d == null) return ActionResult.Fail("没有这份文书");
            if (s.CurrentLocation != d.LocationId) return ActionResult.Fail($"要在{LocName(d.LocationId)}才能查看");
            var why = DocumentBlocked(s);
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
            pv.Food = carried.Where(i => _items.TryGetValue(i.ItemId, out var c) && c.Use != null && !c.Use.IsMedicine && !c.Use.IsCare && c.Use.Satiety > 0 && !c.RequiresCooking).Sum(i => i.Count);
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
            // B15a 试玩修复 M339（Q155“对白锁仍优先”）：登记那次到达的对白还没读完时不切，读完对白（安全边界）再切到当前主线
            else if (MainQuestObjectiveOn(s) && !(DialogueLocked(s) && p.ObjectiveId != MainQuestObjectiveId))
            {
                // B15a（Q155 选 A）：营区登记完成后主目标接续到当前主线（取代 S15 登记后仍显示 route_onward 的冲突部分）
                stage = "mainline";
                obj = MainQuestObjectiveId;
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
            // 试玩修复 M298：主目标切换时，上一个上下文给的提示让位（之后新的真实上下文再给）
            if (p.ObjectiveId != obj && !string.IsNullOrEmpty(p.ObjectiveId)) p.GuideActive = "";
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
            var o = BaseObjective(s);
            if (o == null) return null;
            // Q139 选 B：主目标指引实际前进时，按下一真实路段的状态替换“下一步”（保留标题；只读、0 分钟）
            var step = TravelStepText(s, NextTravelSegment(s));
            if (step != null) o.Step = step;
            return o;
        }

        private PrologueObjectiveView? BaseObjective(GameState s)
        {
            if (!PrologueEnabled) return null;
            var p = s.Prologue;
            switch (p.ObjectiveId)
            {
                case "visitor_talk":
                case "visitor_return":
                    return Special(p.ObjectiveId, "PRO16");
                case "exit_go":
                    return Special(p.ObjectiveId, "PRO18");
                case "exit_need":
                {
                    // 策划 exit_need 的 step 是“按真实缺项显示”的说明，玩家看到的是 adaptiveSteps 里第一项缺的那句
                    var o = Special(p.ObjectiveId, "PRO18");
                    o.Step = AdaptiveStep(s, "exit_need") ?? SpecialOf("exit_go")?.Step ?? "";
                    return o;
                }
                case "route_onward":
                case "settle":
                    return Special(p.ObjectiveId, null);
                case MainQuestObjectiveId:
                    return MainQuestObjective(s);
            }
            var t = ProTask(p.ObjectiveId);
            if (t == null) return null;
            // Q129 选 B：普通主目标标题直接用任务标题，下一步用策划“下一步”短句（按实际缺项可替换），不显示内部完成条件
            return new PrologueObjectiveView { Id = t.Id, TaskId = t.Id, Title = t.Title, Step = StepText(s, t) };
        }

        public PrologueSpecialObjectiveConfig? SpecialOf(string id) => Config.Prologue.SpecialObjectives.FirstOrDefault(x => x.Id == id);

        /// <summary>营区登记后的主目标 ID（B15a，Q155 选 A）。</summary>
        public const string MainQuestObjectiveId = "main_quest";

        /// <summary>主线显示顺序（Q155 显示算法：FA01→FA02→FA03→FA04→FA05 固定顺序）。</summary>
        private static readonly string[] MainQuestOrder = { "FA01", "FA02", "FA03", "FA04", "FA05" };

        /// <summary>营区登记已完成、且主线已实装：主目标接续到当前主线。</summary>
        private bool MainQuestObjectiveOn(GameState s)
        {
            var reg = Config.City.Registration.RegisteredFact;
            return !string.IsNullOrEmpty(reg) && s.Progress.Flags.Contains(reg) && MainQuestOrder.Any(id => Quest(id)?.Implemented == true);
        }

        /// <summary>
        /// 登记后的主目标（B15a，Q155 选 A 显示算法，只读、不自动接取、不伪造已发现路线）：按 FA01→FA05 固定顺序，在任务册已显示的未完成主线里
        /// 先取进行中的、再取可接的（平行 FA02/FA03 都已接时 FA02 优先，完成后自然切到另一条）；标题用正式任务名，下一步用任务册同状态的短句。
        /// 暂无可显示的未完成主线 →“查看主线与委托”；FA01—FA05 全部完成 →“保持联络，继续求生”（不强制终局）。
        /// 危险提示、战斗、对白与动作锁照旧优先（任务框在战斗界面隐藏，对白层覆盖在上）。
        /// </summary>
        private PrologueObjectiveView MainQuestObjective(GameState s)
        {
            var live = MainQuestOrder.Where(id => Quest(id)?.Implemented == true).ToList();
            if (live.All(id => QuestDone(s, id))) return Special("main_quest_done", null, MainQuestObjectiveId);
            var views = live.Select(id => QuestViewOf(s, id)).Where(v => v != null && v.Status != "done").Select(v => v!).ToList();
            var pick = views.FirstOrDefault(v => v.Status == "active") ?? views.FirstOrDefault(v => v.Status == "available");
            if (pick == null) return Special("main_quest_none", null, MainQuestObjectiveId);
            return new PrologueObjectiveView { Id = MainQuestObjectiveId, Title = pick.Title, Step = pick.Step };
        }

        private PrologueObjectiveView Special(string id, string? taskId, string? viewId = null)
        {
            var sp = SpecialOf(id);
            return new PrologueObjectiveView { Id = viewId ?? id, TaskId = taskId, Title = sp?.Title ?? "", Step = sp?.Step ?? "" };
        }

        /// <summary>
        /// 按实际缺项替换的下一步（Q129 adaptiveSteps）：PRO06 已取水未到溪流 / 已到溪流未取水；PRO14 已到过村庄或诊所；
        /// exit_need 按首战、路线已知、到访村庄的顺序取第一项缺的。都不满足为 null。
        /// </summary>
        public string? AdaptiveStep(GameState s, string objective)
        {
            foreach (var a in Config.Prologue.AdaptiveSteps.Where(x => x.Objective == objective))
                if (AdaptiveWhen(s, a.When)) return a.Step;
            return null;
        }

        private bool AdaptiveWhen(GameState s, string when)
        {
            var p = s.Prologue;
            switch (when)
            {
                case "drew_dirty_water_and_not_visited_stream": return p.DrewWater && !Visited(s, "stream");
                case "visited_stream_and_not_drew_dirty_water": return Visited(s, "stream") && !p.DrewWater;
                case "visited_village_or_clinic": return p.ReachedVillageOrClinic;
                case "not_tutorial_win_or_legacy": return !s.Story.TutorialWon && !s.Story.Legacy;
                case "not_camp_route_known": return !s.Progress.Flags.Contains(FactCampRouteKnown);
                case "not_visited_village": return !Visited(s, Config.Prologue.Exit.From);
                default: return false;
            }
        }

        /// <summary>任务的玩家“下一步”（Q129：策划短句，按实际缺项替换）。</summary>
        public string StepText(GameState s, PrologueTaskConfig t) => AdaptiveStep(s, t.Id) ?? GoalText(t);

        /// <summary>当前建议（最多两条）：标题用任务标题，说明用“下一步”短句。</summary>
        public List<PrologueObjectiveView> CurrentSuggestions(GameState s) =>
            s.Prologue.Suggestions.Select(ProTask).Where(t => t != null)
                .Select(t => new PrologueObjectiveView { Id = t!.Id, TaskId = t.Id, Title = t.Title, Step = StepText(s, t) }).ToList();

        /// <summary>任务的玩家“下一步”短句（Q129 nextStep；旧配置没有时退回策划原句）。</summary>
        public static string GoalText(PrologueTaskConfig t) => string.IsNullOrEmpty(t.Goal) ? t.CompletionRule : t.Goal;

        /// <summary>
        /// 当前要显示的那一条中性系统提示（同一时刻最多一条，Q128 guidancePolicy.order“当前打开的上下文优先，其次主目标”）：
        /// 最近一次真实上下文触发、仍未完成且相关的引导；没有时退回主目标对应任务的 PROxx_hint。已关闭或已暂停提示时为 null。
        /// 提示不锁输入、不推进时间，关闭不等于完成。
        /// </summary>
        public (string TaskId, string Text)? CurrentHint(GameState s)
        {
            if (!PrologueEnabled || s.Prologue.HintsPaused || s.Progress.Dead) return null;
            var id = s.Prologue.GuideActive;
            if (!GuideActiveValid(s)) id = CurrentObjective(s)?.TaskId;
            if (id == null || TaskDone(s, id) || s.Prologue.DismissedHints.Contains(id)) return null;
            // Q140 选 B：战斗类操作提示（PRO04）只在实际首战内给；战外（营地空闲、休息、开场对白、战前敌踪）回退主目标时只显示标题与下一步，
            // 离开/结束战斗后也不残留
            if (Config.Prologue.BattleOnlyHintTaskIds.Contains(id) && s.Battle?.Tutorial != true) return null;
            var t = ProTask(id);
            return t == null || string.IsNullOrEmpty(t.Hint) ? ((string, string)?)null : (id, t.Hint);
        }

        /// <summary>
        /// 上下文给的那条引导还算数：未完成、未关闭、仍相关，且人还在给出它的地点（试玩修复 M298：离开即失效，提示回到当前主目标对应项；
        /// 主目标切换时也让位，见 <see cref="RefreshObjective"/>）。
        /// </summary>
        private bool GuideActiveValid(GameState s)
        {
            var p = s.Prologue;
            if (string.IsNullOrEmpty(p.GuideActive) || !GuideOpen(s, p.GuideActive)) return false;
            return string.IsNullOrEmpty(p.GuideActiveAt) || p.GuideActiveAt == s.CurrentLocation;
        }

        /// <summary>
        /// 首战内的引导（Q128 PRO04：原首战警告之后、首次行动结算之前的首战教学上下文；显示在全屏战斗的对象信息区，不放被战斗盖住的常态任务框）。
        /// 只在首战、PRO04 未完成、提示未暂停/未关闭时给；不给 AP、不替你行动、不推进时间。
        /// </summary>
        public (string TaskId, string Text)? BattleHint(GameState s)
        {
            if (!PrologueEnabled || s.Battle?.Tutorial != true || s.Prologue.HintsPaused || TaskDone(s, "PRO04") || s.Prologue.DismissedHints.Contains("PRO04")) return null;
            var t = ProTask("PRO04");
            return t == null || string.IsNullOrEmpty(t.Hint) ? ((string, string)?)null : ("PRO04", t.Hint);
        }

        /// <summary>“关闭这条”：只记已关闭（不完成任务）；可在任务册回看。</summary>
        public void DismissHint(GameState s, string taskId) => s.Prologue.DismissedHints.Add(taskId);

        /// <summary>“暂停后续操作提示”/重新开启：只开关中性教学提示。</summary>
        public void SetHintsPaused(GameState s, bool paused) => s.Prologue.HintsPaused = paused;

        /// <summary>
        /// 当前主目标要轻量点亮的对象（每次最多一个）：npc:ID、doc:ID、loc:ID；没有为 null。地点指向真实的下一路段
        /// （Q131：沿已开通的已知道路走向目标的第一段；出区不再指向南口路标，路标只是可选阅读）。
        /// Q139 选 B：下一段已知（可走或未开通）点亮那一端；下一段未知且当前地点还能探索（未满 100%）点亮当前地点卡（探索入口）；
        /// 未知且不能再探索不点亮（不点亮未知目标、不暗示重复探索）。只读。
        /// </summary>
        public string? ObjectiveFocus(GameState s)
        {
            switch (s.Prologue.ObjectiveId)
            {
                case "visitor_talk": return "npc:" + Config.Prologue.Visitor.NpcId;
                case "PRO15" when s.CurrentLocation == "village": return "doc:relief_page_village";
            }
            var seg = NextTravelSegment(s);
            switch (seg?.Kind)
            {
                case TravelSegmentKind.KnownOpen:
                case TravelSegmentKind.KnownBlocked: return "loc:" + seg.NodeId;
                case TravelSegmentKind.UnknownExplore: return "loc:" + s.CurrentLocation;
                default: return null;
            }
        }

        /// <summary>主目标正在指引实际前进时的目标地点（Q131/Q139）；不是前进类主目标、或已在目标处为 null。</summary>
        private string? TravelTarget(GameState s)
        {
            var exit = Config.Prologue.Exit;
            var here = s.CurrentLocation;
            string? target;
            switch (s.Prologue.ObjectiveId)
            {
                case "visitor_return": target = Config.Prologue.Visitor.LocationId; break;
                case "exit_go": target = here == exit.From ? exit.To : exit.From; break;
                case "exit_need":
                    if (!s.Story.TutorialWon && !s.Story.Legacy) target = Config.Story.TutorialBattle.LocationId;
                    else if (!s.Progress.Flags.Contains(FactCampRouteKnown)) target = Config.Prologue.Visitor.LocationId;
                    else target = exit.From;
                    break;
                case "route_onward":
                {
                    var leg = Config.Prologue.Journey.Legs.FirstOrDefault(l => !Visited(s, l.To));
                    target = leg == null ? null : here == leg.From ? leg.To : leg.From;
                    break;
                }
                case "PRO04": target = Config.Story.TutorialBattle.LocationId; break;
                case "PRO15": target = here == "village" ? null : "village"; break;
                default: target = null; break;
            }
            return string.IsNullOrEmpty(target) || target == here ? null : target;
        }

        /// <summary>
        /// 前进类主目标的下一真实路段（Q139 选 B，只读）：先找已开通、两端已知的路（<see cref="MapPathTo"/>）的第一段；没有时沿真实物理连接
        /// （全部可从这一端出发的边，最少段数，同段数按配置顺序；不按屏幕方位猜）取第一段，再按玩家是否已知分类——
        /// 边已揭示且那一端已发现：已开通为 KnownOpen，未开通为 KnownBlocked（原因用相邻栏同一句领域可公开原因）；
        /// 否则未知：当前地点可探索且未满 100% 为 UnknownExplore，不能再探索为 UnknownNoExplore。不是前进类主目标或不连通为 null。
        /// </summary>
        public TravelSegmentView? NextTravelSegment(GameState s)
        {
            if (!PrologueEnabled) return null;
            var target = TravelTarget(s);
            if (target == null) return null;
            var here = s.CurrentLocation;
            var path = MapPathTo(s, target);
            if (path != null && path.Count > 0) return new TravelSegmentView { Kind = TravelSegmentKind.KnownOpen, NodeId = path[0].To, Target = target };
            var first = PhysicalFirstStep(here, target);
            if (first == null) return null;
            var (edge, node) = first.Value;
            if (EdgeRevealed(s, edge) && Known(s, node))
            {
                return EdgeOpen(s, edge)
                    ? new TravelSegmentView { Kind = TravelSegmentKind.KnownOpen, NodeId = node, Target = target }
                    : new TravelSegmentView { Kind = TravelSegmentKind.KnownBlocked, NodeId = node, Target = target, Reason = GateBlockedReason(s, edge) };
            }
            var explorable = _locs.TryGetValue(here, out var lc) && lc.ExploreEnabled && s.Locations.TryGetValue(here, out var ls) && ls.Progress < 100;
            // B22f（Q193）/ B22f2（Q208 选 A，取代开发假设 B22f-2 的范围）：只在主目标 route_onward、人在 m02（序章固定战地点）、
            // 真实下一段是序章路线的下一段（营门）且未知、本局拦路战未胜时，下一步写“先战胜检查哨拦路敌人。”、不点亮任何卡；
            // 覆盖 Q139 未知下一段的探索句与查询句（探索度满 100 也一样）。其他目标/地点/已知/已胜走原分支。只读。
            if (s.Prologue.ObjectiveId == "route_onward" && FixedBattleAt(here) is { IsCampaign: true }
                && Config.Prologue.Journey.Legs.Any(l => l.From == here && l.To == node)
                && FixedStateAt(s, here) is { Won: false } fst && fst.WorldIds.Count > 0)   // 已揭示、本局未胜（交战中也算未胜）
                return new TravelSegmentView { Kind = TravelSegmentKind.UnknownFixedBlocked, Target = target };
            return new TravelSegmentView { Kind = explorable ? TravelSegmentKind.UnknownExplore : TravelSegmentKind.UnknownNoExplore, Target = target };
        }

        /// <summary>真实物理连接上从 from 到 target 最少段数路线的第一段（边与下一端）；不连通为 null。只读，不看玩家是否已知。</summary>
        private (EdgeConfig Edge, string Node)? PhysicalFirstStep(string from, string target)
        {
            var prev = new Dictionary<string, (string From, EdgeConfig Edge)>();
            var seen = new HashSet<string> { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                if (u == target) break;
                foreach (var e in EdgesAt(u))
                {
                    if (!e.TraversableFrom(u)) continue;
                    var v = e.Other(u);
                    if (!seen.Add(v)) continue;
                    prev[v] = (u, e);
                    queue.Enqueue(v);
                }
            }
            if (!prev.ContainsKey(target)) return null;
            var at = target;
            while (prev[at].From != from) at = prev[at].From;
            return (prev[at].Edge, at);
        }

        /// <summary>
        /// Q139 的下一步句（文字取 config prologue.travelStep，B38 起为 N02 新稿）：未知且可探索 →“先探索脚下这个地点，找出往前走的路。”；未知且不能探索 →“先看看地图上已经认出的路和通行条件。”
        /// （所有前进类主目标）。已知可走/已知未开通两句只替换出区之后“沿旧公路、检查哨、营门逐段前进”（route_onward）那句泛指的下一步，
        /// 其余前进类主目标在下一段已知时保留 Q129 原下一步（开发假设 B14a-3）。不替换时为 null。
        /// </summary>
        private string? TravelStepText(GameState s, TravelSegmentView? seg)
        {
            var ts = Config.Prologue.TravelStep;
            if (seg == null) return null;
            switch (seg.Kind)
            {
                case TravelSegmentKind.UnknownExplore: return NonEmpty(ts.Explore);
                case TravelSegmentKind.UnknownNoExplore: return NonEmpty(ts.UnknownNoExplore);
                case TravelSegmentKind.UnknownFixedBlocked: return CheckpointBlockText + "。";
                case TravelSegmentKind.KnownOpen when s.Prologue.ObjectiveId == "route_onward":
                    return NonEmpty(ts.KnownOpen)?.Replace("{实际下一相邻地点名}", LocName(seg.NodeId!));
                case TravelSegmentKind.KnownBlocked when s.Prologue.ObjectiveId == "route_onward":
                    return NonEmpty(ts.KnownBlocked)?.Replace("{已发现地点名}", LocName(seg.NodeId!)).Replace("{领域可公开原因}", PublicReason(seg.Reason));
                default: return null;
            }
        }

        private static string? NonEmpty(string t) => string.IsNullOrEmpty(t) ? null : t;

        /// <summary>相邻栏的未开通原因“需要：A；B”放进“前往……还需要{原因}。”（B38 前为“……的路线条件：{原因}。”）时去掉“需要：”前缀与句末句号，避免重复标点。</summary>
        private static string PublicReason(string reason)
        {
            var r = reason.StartsWith("需要：") ? reason.Substring(3) : reason;
            return r.TrimEnd('。');
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
                case "guides_reset":
                    s.Prologue.GuideSeen.Clear();
                    s.Prologue.DismissedHints.Clear();
                    s.Prologue.GuideActive = s.Prologue.GuideActiveAt = s.Prologue.GuidePending = s.Prologue.GuidePendingAt = "";
                    s.Prologue.GuideMissed.Clear();
                    s.Prologue.HintsPaused = false;
                    PrologueBoundary(s);
                    return "开发：已清空操作引导的已示/关闭记录（任务完成不变；播过的饱饱短句按对白去重不再播）。";
                default:
                    return "开发：未知序章步骤";
            }
        }
    }
}

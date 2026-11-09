using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>城市岗位 NPC 的一项服务在当前状态下的预览（A11 Q33 服务表；preview 0 分钟，不提交任何东西）。</summary>
    public sealed class CityServiceView
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>玩家摘要（Q145 playerSummary）。</summary>
        public string Summary { get; set; } = "";
        /// <summary>工时说明（“0分钟”“10分钟”“沿原契约”）。</summary>
        public string TimeText { get; set; } = "";
        /// <summary>费用说明（“无额外耗材/费用”或“草药绷带×1、布料×1”）。</summary>
        public string CostText { get; set; } = "";
        /// <summary>效果说明（玩家能看懂的短句，规则来自服务表）。</summary>
        public string EffectText { get; set; } = "";
        /// <summary>岗位对这项服务说的一句（narrative-texts serviceOfferLines）。</summary>
        public string NpcLine { get; set; } = "";
        /// <summary>已实装（未实装的不进事项列表，Q146 选 B；只有直接请求时预览才会出现，Blocked 为中性拒绝）。</summary>
        public bool Implemented { get; set; }
        /// <summary>现在不能办的原因（与目标/选项无关的部分）；能办为 null。</summary>
        public string? Blocked { get; set; }
        public bool NeedsTarget { get; set; }
        public List<CityTargetView> Targets { get; set; } = new List<CityTargetView>();
        public List<CityChoiceView> Choices { get; set; } = new List<CityChoiceView>();
        /// <summary>办理后界面打开制作面板（公共工坊入口）。</summary>
        public bool OpensCraft { get; set; }
        /// <summary>办理按钮上的字（为空时界面写“{服务名}（{工时}）”，B38b Q285）。</summary>
        public string? StartLabel { get; set; }
        /// <summary>完成结果之后另列的行（B38b：施济日额、工坊工位、任务工作进度）。</summary>
        public List<string> Lines { get; set; } = new List<string>();
    }

    public sealed class CityTargetView
    {
        public string ActorId { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>此目标的现有状态说明（咨询内容、预计效果）。</summary>
        public string Detail { get; set; } = "";
        public string? Blocked { get; set; }
        /// <summary>不单列姓名时的完整原因（按钮旁、开工失败）；B38b Q286 施济写“{人物}的……”，其余“{人物}：{原因}”。</summary>
        public string? BlockedLine { get; set; }
    }

    public sealed class CityChoiceView
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string? Blocked { get; set; }
    }

    /// <summary>
    /// 势力事实、营区登记与城市岗位 NPC 框架（开发计划 v0.9 B14，R44、R45；N01 §3/§6、A11「城市建筑、岗位NPC与八支线」§1—§6、
    /// A11「Q33 城市岗位完整服务表」、S02 门槛规则 3/5/6；Q32/Q33 选 B）。
    /// <list type="bullet">
    /// <item>事实：只写 hub_registered、registration_authorized、church_contacted、academy_contacted、academy_observation_pass 与任务的
    /// quest:ID:accepted/completed/declined_deed；永不写 joined_faction（旧档有的改记 legacy_service）。</item>
    /// <item>登记：h01 值守登记员 0 分钟登记写 registration_authorized（开 h01↔h02），真实走到 h02 才写 hub_registered（C2、登记默认知识）。</item>
    /// <item>岗位 NPC：实际到达该地点才在设施栏出现人物卡；不可招募；命名人物退场由同机构当班接续员代班，服务、配额与任务账不变，不复活。</item>
    /// <item>服务：统一预览→校验→开工（预留材料/配额与事务号）→5 分钟段→完成时原子核销；未实装的只注册，从事项列表与计数里隐藏（B14b，Q146 选 B）。</item>
    /// <item>名称：19 项服务、知识与两份告示用策划正式名（Q145 选 B），玩家文字不带内部任务编号。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>计时城市服务的行动标记前缀（ActionState.TargetId = "citysvc:serviceId"）。</summary>
        public const string CityServicePrefix = "citysvc:";
        public const string QuestFactPrefix = "quest:";
        /// <summary>任务事实的三种结局（N01 §3）。</summary>
        public static readonly string[] QuestFactKinds = { "accepted", "completed", "declined_deed" };
        public const string LegacyServicePrefix = "legacy_service:";
        /// <summary>旧提案里的两个外部势力 ID（N01 §6.2：不改 ID 成新组织，保留为 legacy_service 记录）。</summary>
        public static readonly string[] LegacyFactionIds = { "faction_works", "faction_beacon" };

        /// <summary>任务事实键（N01 §3“每任务accepted/completed/declined_deed”）：quest:QMAP01:completed。</summary>
        public static string QuestFact(string questId, string kind)
        {
            if (!QuestFactKinds.Contains(kind)) throw new ArgumentException($"未知任务事实 {kind}");
            return $"{QuestFactPrefix}{questId}:{kind}";
        }

        /// <summary>会籍类事实（N01 §3“接触和完成任务从不写joined_faction”）：任何时候都不应出现在存档事实里。</summary>
        public static bool IsMembershipFact(string fact) => fact.StartsWith("joined_faction", StringComparison.Ordinal);

        /// <summary>
        /// 旧势力事实迁移（N01 §6.2、A11 §6.2；schema 25→26）：faction_works/faction_beacon 原样保留并另记 legacy_service:ID；
        /// joined_faction* 改记 legacy_service（新规则不写会籍）；已到访 c02/r02 的补等效接触事实（不补对白）；已登记的补登记授权。
        /// 不发物、不改容量与知识。返回给升级日志的短句。幂等。
        /// </summary>
        public static List<string> MigrateFactionFacts(GameState s, GameConfig config)
        {
            var notes = new List<string>();
            var f = s.Progress.Flags;
            foreach (var id in LegacyFactionIds)
                if (f.Contains(id) && f.Add(LegacyServicePrefix + id)) notes.Add($"旧势力记录 {id} 保留为营区旧服务记录");
            foreach (var j in f.Where(IsMembershipFact).ToList())
            {
                f.Remove(j);
                f.Add(LegacyServicePrefix + j);
                notes.Add($"旧会籍记录 {j} 改为旧服务记录（现在没有入会）");
            }
            foreach (var kv in config.Map.ArrivalFacts)
                if (s.Locations.TryGetValue(kv.Key, out var ls) && ls.Visited)
                    foreach (var fact in kv.Value)
                        if (f.Add(fact)) notes.Add($"到访过的{config.Locations.FirstOrDefault(l => l.Id == kv.Key)?.Name ?? kv.Key}记为已公开接触");
            var reg = config.City.Registration;
            if (!string.IsNullOrEmpty(reg.RegisteredFact) && f.Contains(reg.RegisteredFact)) f.Add(reg.AuthorizedFact);
            return notes;
        }

        private bool CityEnabled => Config.City.Npcs.Count > 0;

        public CityNpcConfig? CityNpc(string npcId) => Config.City.Npcs.FirstOrDefault(n => n.NpcId == npcId);

        /// <summary>城市服务（含 B15 借同一事务通道的任务工作与战术训练条目）。</summary>
        public CityServiceConfig? CityService(string serviceId) =>
            Config.City.Services.FirstOrDefault(x => x.Id == serviceId) ?? (ExtraServices.TryGetValue(serviceId, out var e) ? e : null);

        public string FactionName(string factionId) => Config.City.Factions.TryGetValue(factionId, out var n) ? n : factionId;

        // ───────────────────────── 营区登记（S02 规则 3） ─────────────────────────

        /// <summary>
        /// 实际到达成功后的城市事务：持登记授权真实走到广场，写 hub_registered（C2、登记默认知识）与 QMAP01 完成；幂等。
        /// 带旧登记在办资格的（Q247，只由 42→43 迁移写入）在同一事务把五项写成永久知识，按旧 26 项完成并清掉资格；其余登记只有 21 项。
        /// </summary>
        private void ArriveCity(GameState s, string locationId, List<string> notes)
        {
            // B18：当日首次安全到达鉴定所时锁定当日报价表（R07 X3.3、开发假设 B18-1）
            if (locationId == Ap.LocationId) EnsureAppraisalQuotes(s);
            // B19：营区登记后首次实际安全到达收容所时开放周期候选（R08 RC1、v3 校准）；之后每次到达只确保当前批已生成
            if (locationId == Rc.LocationId && !TryOpenShelterRecruitment(s, notes)) EnsureRecruitCycle(s);
            var reg = Config.City.Registration;
            if (string.IsNullOrEmpty(reg.SquareLocation) || locationId != reg.SquareLocation) return;
            var f = s.Progress.Flags;
            if (!f.Contains(reg.AuthorizedFact) || !f.Add(reg.RegisteredFact)) return;
            if (!string.IsNullOrEmpty(reg.QuestId)) f.Add(QuestFact(reg.QuestId, "completed"));
            var know = Config.Knowledge.Count(k => k.GrantedByFacts.Contains(reg.RegisteredFact));
            if (s.City.LegacyRegistrationPending)
            {
                foreach (var id in reg.LegacyKnowledge) f.Add("knowledge:" + id);
                know += reg.LegacyKnowledge.Count;
                s.City.LegacyRegistrationPending = false;
            }
            notes.Add($"营区登记完成：队伍容量升到 C{BattleCapacity(s)}" + (know > 0 ? $"，学会营区登记默认做法知识{know}项" : "") + "（不发额外物资，再到广场不会重复登记）");
        }

        // ───────────────────────── 岗位 NPC 卡（A11 §1、§6） ─────────────────────────

        /// <summary>当前地点的城市岗位人物卡：只在实际到过且正站在该地点时生成（看地图、预览不生成）。</summary>
        private IEnumerable<NpcCardView> CityNpcsHere(GameState s)
        {
            if (!CityEnabled || !s.Locations.TryGetValue(s.CurrentLocation, out var ls) || !ls.Visited) yield break;
            foreach (var n in Config.City.Npcs.Where(n => n.LocationId == s.CurrentLocation))
            {
                var absent = s.City.Absent.Contains(n.NpcId);
                // Q146 选 B：只统计已实装的事项；可办数按当前资格（分母不含未实装）；没有已实装事项不显示事项数占位
                var services = CityServicesOf(s, n.NpcId);
                var (canDo, total) = NpcItemCounts(s, n.NpcId, services);
                yield return new NpcCardView
                {
                    NpcId = n.NpcId,
                    Name = absent ? n.DutySpeaker : n.Name,
                    Role = absent ? $"代{n.Role}（{n.Name}已不在岗）" : n.Role,
                    FactionName = FactionName(n.FactionId),
                    Kind = "post",
                    KindLabel = absent ? "代班" : "岗位",
                    Substitute = absent,
                    Status = total > 0 ? $"可交谈 · 事项 {canDo}/{total} 项可办" : "可交谈",
                    ItemCount = total,
                    ServiceCount = services.Count,
                    QuestCount = QuestsOfNpc(s, n.NpcId).Count(q => q.Status != "done"),
                    HasQuests = QuestsOfNpc(s, n.NpcId).Count > 0,
                    // B15：只写已实装且已显示的委托（正式名 + 状态），不出内部任务编号（Q145/Q146）
                    QuestText = QuestCardText(s, n.NpcId),
                    RecruitText = "岗位人物，不可招募",
                    HasNews = !s.City.Greeted.Contains(n.NpcId) && !absent,
                    TalkBlocked = TalkBlocked(s),
                    Presence = absent ? "代班" : "在场",
                    QuestMarks = NpcQuestMarks(s, n.NpcId),
                };
            }
        }

        /// <summary>B24：岗位卡的可接/可交短标（与鸟瞰任务标记同一口径：可接=已显示可接；可交=已接且记录已取得或有可在这里办的工作）。</summary>
        private string NpcQuestMarks(GameState s, string npcId)
        {
            var qs = QuestsOfNpc(s, npcId);
            var acc = qs.Count(v => v.Status == "available");
            var turn = qs.Count(v => v.Status == "active" && (Quest(v.Id) is QuestConfig q && HasQuestRecord(s, q) || v.WorkLabel != null));
            var parts = new List<string>();
            if (acc > 0) parts.Add($"可接{acc}");
            if (turn > 0) parts.Add($"可交{turn}");
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// 岗位卡“事项 a/b 项可办”与按钮“N项”的同一口径（B15 试玩修复 M333）：b = 未关闭的已实装服务 + 未完成且不经本岗服务办理的委托；
        /// 任务类服务对应的那一步已办完（路线已领、委托已完成）记为关闭、不计；经本岗服务接取/办理的委托算在该服务里，不重复计；
        /// a = 其中现在能办的（服务能办，或它所服务的委托现在可接；委托能接或能做）。
        /// </summary>
        private (int Doable, int Total) NpcItemCounts(GameState s, string npcId, List<CityServiceView> services)
        {
            var open = services.Where(v => !CityServiceClosed(s, v.Id)).ToList();
            var quests = QuestsOfNpc(s, npcId).Where(q => q.Status != "done").ToList();
            bool Served(QuestView q, CityServiceView v) => Quest(q.Id) is QuestConfig c && (c.AcceptServiceId == v.Id || c.WorkServiceIds.Contains(v.Id));
            static bool QuestDoable(QuestView q) => (q.AcceptHere && q.AcceptBlocked == null) || (q.WorkLabel != null && q.WorkBlocked == null);
            var own = quests.Where(q => !open.Any(v => Served(q, v))).ToList();
            var doable = open.Count(v => CityServiceDoable(v) || quests.Any(q => Served(q, v) && QuestDoable(q))) + own.Count(QuestDoable);
            return (doable, open.Count + own.Count);
        }

        /// <summary>任务类服务对应的那一步已办完：委托已完成，或接待处路线已领（B15 试玩修复 M333）。</summary>
        private bool CityServiceClosed(GameState s, string serviceId)
        {
            var svc = CityService(serviceId);
            if (svc == null) return false;
            if (QuestOfService(svc) is QuestConfig q && QuestDone(s, q.Id)) return true;
            return Config.Quests.List.Any(x => x.Implemented && x.AcceptServiceId == serviceId) && svc.Choices.SelectMany(c => c.Facts).Any(s.Progress.Flags.Contains);
        }

        /// <summary>一项事项现在能不能办（卡面“可办数”）：没有通用阻拦，要目标的至少有一名合法目标。</summary>
        private static bool CityServiceDoable(CityServiceView v) => v.Blocked == null && (!v.NeedsTarget || v.Targets.Any(t => t.Blocked == null));

        /// <summary>
        /// 与岗位人物交谈（0 分钟，不扣物、不恢复、不改身体、不写事实）：首次播“初次见面”与“岗位说明”；之后播“闲谈”
        /// （B14b，Q146 选 B：未实装本身不触发“暂不可办”；目前没有已实装且剧情条件不足的事项，locked 组暂不使用）；
        /// 命名人物退场时由代班员接续，接续说明只自动播一次（B26a，Q235），之后没有事务对白时播一句代班专用交谈（Q236）。回看只回放台词。
        /// B26a（Q235_B）：本次实际将接取的是 EXQ05—08 时跳过“初次见面/岗位说明”，首次铺垫记为已处理、不补播；
        /// 其他首次交谈与其他任务照旧。顺序：铺垫（或代班说明）→ 回报（可连播）→ 至多接一项。
        /// </summary>
        private ActionResult TalkCityNpc(GameState s, CityNpcConfig n)
        {
            if (s.CurrentLocation != n.LocationId) return ActionResult.Fail($"{n.Name}不在这里");
            var why = TalkBlocked(s);
            if (why != null) return ActionResult.Fail(why);
            var c = s.City;
            c.Talks[n.NpcId] = (c.Talks.TryGetValue(n.NpcId, out var k) ? k : 0) + 1;
            // B15：可接的委托在交谈时接取（播该委托的接取对白；narrative-texts offer 触发“quest eligible, local safe talk”）
            var offer = TalkOffer(s, n.NpcId);
            // B23（A11 §2“返营交谈0分钟0材料”、EXQ-A07）：记录已取得的可选任务，交谈即回报完成；岗位不在由代班员接手
            var turnins = LiveQuests.Where(q => q.NpcId == n.NpcId && q.RecordFacts.Count > 0 && QuestAccepted(s, q.Id) && !QuestDone(s, q.Id) && HasQuestRecord(s, q)).ToList();
            if (c.Absent.Contains(n.NpcId))
            {
                // 联系人退场不锁任务：代班员接续，委托照接，接取台词由当班接续员说（M333：换 speaker，见 SpeakerNow）。
                // 和代班员交谈不写 Greeted（Q238）：本人回岗后第一次交谈才按原首次规则处理
                var intro = PlaySubstituteIntro(s, n);
                foreach (var q in turnins) CompleteQuest(s, q, false);
                if (offer != null) AcceptCore(s, offer, true);
                // Q236 entry/selection：说明已示之后、这次又没有实际回报与接取，才播一句代班专用交谈；首次说明这一次只播说明
                if (!intro && turnins.Count == 0 && offer == null) OpenReplay(s, StateSet(n, SubstituteTalkState(s, n)));
                return ActionResult.Success();
            }
            var first = c.Greeted.Add(n.NpcId);
            if (first && !(offer != null && DedicatedOffer(offer)))
            {
                OpenReplay(s, StateSet(n, "first_greeting"));
                OpenReplay(s, StateSet(n, "role"));
            }
            foreach (var q in turnins) CompleteQuest(s, q, false);
            if (offer != null)
            {
                AcceptCore(s, offer, true);
                return ActionResult.Success();
            }
            if (first || turnins.Count > 0) return ActionResult.Success();
            OpenReplay(s, StateSet(n, QuestTalkState(s, n) ?? "idle"));
            return ActionResult.Success();
        }

        /// <summary>代班接续说明的状态名（narrative-texts npcDialogueSets）。</summary>
        public const string SubstituteIntroState = "institution_substitute";

        /// <summary>代班接续说明的已示键（Q235 substituteOnce）：本局 campaignId + 岗位 npcId；现版本没有个人代班 ID，岗位 npcId 代表该代班身份，显示名不进键。</summary>
        public static string SubstituteIntroKey(GameState s, string npcId) => $"{CampaignIdOf(s)}|{npcId}";

        public static bool SubstituteIntroShown(GameState s, string npcId) => s.City.SubstituteIntroShown.Contains(SubstituteIntroKey(s, npcId));

        /// <summary>
        /// 自动入口（交谈、事项接取、服务结束）请求一次代班接续说明：已示不再播；同一组还在队列里不再排。已示记录不在这里写，
        /// 在说明实际开始显示时写（<see cref="NoteSubstituteIntroShown"/>，Q235 receipt）。返回这次是否还有说明要播（新排的或已在队列里的）。
        /// </summary>
        private bool PlaySubstituteIntro(GameState s, CityNpcConfig n)
        {
            if (SubstituteIntroShown(s, n.NpcId)) return false;
            var set = StateSet(n, SubstituteIntroState);
            if (!s.Story.DialogueLog.Any(d => !d.Ended && d.EventId.Split('#')[0] == set)) OpenReplay(s, set);
            return true;
        }

        /// <summary>某段对话首句开始显示：是代班接续说明就记已示（含升级前已排队的旧对白），并清掉同键待补播（Q243 receipt）。</summary>
        private void NoteSubstituteIntroShown(GameState s, DialogueSession d)
        {
            var set = d.EventId.Split('#')[0];
            var n = Config.City.Npcs.FirstOrDefault(x => StateSet(x, SubstituteIntroState) == set);
            if (n == null) return;
            var key = SubstituteIntroKey(s, n.NpcId);
            s.City.SubstituteIntroShown.Add(key);
            s.City.SubstituteIntroDeferred.RemoveAll(r => r.IdentityKey == key);
        }

        /// <summary>服务结束入口的说明被战斗/敌踪挡住：记一条待补播（Q243 record），同键合并。不算已示；已示或这岗没有说明不记。</summary>
        private void DeferSubstituteIntro(GameState s, CityNpcConfig n)
        {
            if (SubstituteIntroShown(s, n.NpcId) || string.IsNullOrEmpty(StateSet(n, SubstituteIntroState))) return;
            var key = SubstituteIntroKey(s, n.NpcId);
            if (s.City.SubstituteIntroDeferred.Any(r => r.IdentityKey == key)) return;
            s.City.SubstituteIntroDeferred.Add(new SubstituteIntroDeferral { CampaignId = CampaignIdOf(s), NpcId = n.NpcId, LocationId = n.LocationId, IdentityKey = key });
        }

        /// <summary>这条待补播还算数：同一局、岗位还在原地点、主队没离开、仍由同一代班身份接续、该身份未示。</summary>
        private bool SubstituteIntroDeferralLive(GameState s, SubstituteIntroDeferral r) =>
            r.CampaignId == CampaignIdOf(s) && CityNpc(r.NpcId) is CityNpcConfig n && n.LocationId == r.LocationId && s.CurrentLocation == r.LocationId
            && s.City.Absent.Contains(r.NpcId) && r.IdentityKey == SubstituteIntroKey(s, r.NpcId) && !s.City.SubstituteIntroShown.Contains(r.IdentityKey);

        /// <summary>
        /// 待补播的安全边界（Q243 automatic/departure/identity）：先取消已不算数的记录（有锁时也取消，所以离开后回到原地不恢复），
        /// 再看原对白显示条件（<see cref="TalkBlocked"/>：死亡、对白、战斗/敌踪、主/子行动）；都放开才显示一条，其余等这段对白读完。
        /// 不推进时间、不结算。调用点：<see cref="UpdateFlags"/> 末尾（每段末、战斗结束、取消、即时操作、传送）、对白读完、敌踪自行散去、段前中止、读档、离岗/回岗开发入口。
        /// 返回状态是否改变。
        /// </summary>
        public bool SubstituteIntroBoundary(GameState s)
        {
            var list = s.City.SubstituteIntroDeferred;
            if (list.Count == 0) return false;
            var dropped = list.RemoveAll(r => !SubstituteIntroDeferralLive(s, r)) > 0;
            if (list.Count == 0 || TalkBlocked(s) != null) return dropped;
            var head = list[0];
            PlaySubstituteIntro(s, CityNpc(head.NpcId)!);
            list.Remove(head);   // 正常已在开始显示时移除；配置里台词被删掉、什么也没显示时也不留着反复重试
            return true;
        }

        private static string StateSet(CityNpcConfig n, string state) => n.States.TryGetValue(state, out var id) ? id : "";

        /// <summary>城市对白组在对话记录里的名称；不是城市组为 null。</summary>
        private string? CityDialogueSetTitle(string id) =>
            Config.City.DialogueSets.FirstOrDefault(d => d.Id == id)?.Title ?? Config.Quests.DialogueSets.FirstOrDefault(d => d.Id == id)?.Title;

        // ───────────────────────── 服务预览（preview 0 分钟） ─────────────────────────

        /// <summary>
        /// 某岗位名下已实装服务的当前预览（Q146 选 B：未实装的不列出、不计数；已实装但条件不足的照常列出并写真实原因）。
        /// 不在该地点时全部写“须实际到达”。
        /// </summary>
        public List<CityServiceView> CityServicesOf(GameState s, string npcId)
        {
            var n = CityNpc(npcId);
            if (n == null) return new List<CityServiceView>();
            var ids = n.ServiceIds.ToList();
            // B15：h03 战术训练（R03 §3）挂在当地岗位的事项里
            if (Config.Scope.Training.NpcId == npcId && ExtraServices.ContainsKey(TrainingServiceId)) ids.Add(TrainingServiceId);
            // B23：可选地牢知识包的公开抄录挂在替代来源岗位（h03 罗铮原服务、h02 许安/h04 陈芷合成服务）
            foreach (var sid in Config.ExpansionQuests.Packs.Where(p => p.CopyNpcId == npcId).Select(p => p.CopyServiceId).Distinct())
                if (!ids.Contains(sid)) ids.Add(sid);
            return ids.Select(CityService).Where(x => x != null && x.Implemented)
                .Select(x => CityServicePreview(s, x!.Id, n)).Where(v => v != null).Select(v => v!).ToList();
        }

        /// <summary>服务面板开头岗位说的一句（service_offer 组首句；代班时同句由当班接续员说）。</summary>
        public string CityOfferLine(string npcId)
        {
            var n = CityNpc(npcId);
            var set = n == null ? "" : StateSet(n, "service_offer");
            return Config.Story.Dialogues.FirstOrDefault(d => d.Event == set)?.Text ?? "";
        }

        /// <summary>CityCommonBlocked 未开放/直接请求拒绝分支（N02 附录 Q286）。</summary>
        public const string CityNotOffered = "这里不办理这项业务。";
        /// <summary>CityCommonBlocked 行动占用分支，CityServiceOverlay 两处、AppraisalPanel 一处界面忙碌提示同句（N02 附录 Q286）。</summary>
        public const string CityBusyText = "当前行动还没结束。";

        /// <summary>嵌进外层句子前补齐句末句号（已有终止符的不重复）：B38b Q283/Q286“每段末尾一个句号”。</summary>
        public static string Sentence(string text)
        {
            var t = text.TrimEnd().TrimEnd('；', '，', '、');
            if (t.Length == 0) return "";
            var last = t[t.Length - 1];
            return last is '。' or '！' or '？' ? t : t + "。";
        }

        /// <summary>去掉句末句号（完整句嵌入外层括号/冒号前，句末标点由外层统一，N02 附录 Q286）。</summary>
        public static string NoStop(string text) => text.TrimEnd().TrimEnd('。');

        private string? CityCommonBlocked(GameState s, CityServiceConfig svc)
        {
            // Q146：隐藏后的旧入口/直接请求拒绝，零扣费零效果；不写“后续版本开放”
            // B38b（N02 附录 Q286 逐字稿）：原分支顺序不变，只换句子
            if (!svc.Implemented) return CityNotOffered;
            if (s.Progress.Dead) return "本局已结束，无法办理。";
            if (s.CurrentLocation != svc.LocationId) return $"到{LocName(svc.LocationId)}后才能办理。";
            if (DialogueLocked(s)) return "请先读完当前对话。";
            if (s.Battle != null || s.Encounter != null) return "先处理眼前的敌踪。";
            if (s.Main != null || s.Sub != null) return CityBusyText;
            return null;
        }

        private string CostOf(CityServiceConfig svc) => svc.Inputs.Count == 0 ? "无额外耗材/费用"
            : string.Join("、", svc.Inputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}")) + "（完成时扣，随身或当地地面）";

        public CityServiceView? CityServicePreview(GameState s, string serviceId, CityNpcConfig? npc = null)
        {
            var svc = CityService(serviceId);
            if (svc == null) return null;
            npc ??= Config.City.Npcs.FirstOrDefault(n => n.ServiceIds.Contains(serviceId));
            var v = new CityServiceView
            {
                Id = svc.Id, Title = svc.Title, Summary = svc.Summary, Implemented = svc.Implemented, CostText = CostOf(svc),
                TimeText = svc.WorldMinutes is int m ? $"{m}分钟" : "沿原契约",
                NpcLine = npc != null && npc.ServiceOfferLines.TryGetValue(svc.Id, out var line) ? line : "",
                Blocked = CityCommonBlocked(s, svc),
            };
            var f = s.Progress.Flags;
            var reg = Config.City.Registration;
            switch (svc.Id)
            {
                case "hub_registration_existing":
                    // B38b（N02 附录 §2、Q286、Q291-4）：预览不写知识数量，实际学会的项数由广场完成日志写
                    v.EffectText = "在营门确认登记，再沿通道到营区广场完成登记。公共配给到广场另领。";
                    if (v.Blocked == null && f.Contains(reg.RegisteredFact)) v.Blocked = "营区登记已完成。";
                    else if (v.Blocked == null && f.Contains(reg.AuthorizedFact)) v.Blocked = $"营门已授权。前往{LocName(reg.SquareLocation)}完成登记。";
                    break;
                case "hub_workshop_access":
                case "academy_workshop_access":
                    v.OpensCraft = true;
                    v.EffectText = WorkshopAccessHead;
                    v.Lines.AddRange(WorkshopAccessLines(s, svc));
                    break;
                case "clinic_consult":
                    v.NeedsTarget = true;
                    v.EffectText = "查看所选对象当前的健康、免疫、病毒、出血和用药冷却。此项只读。";
                    v.Targets = PresentTargets(s, a => null, ConsultText);
                    break;
                case "clinic_dressing":
                {
                    v.NeedsTarget = true;
                    // B38b（Q285 选 A）：预估挂在对象行，主句指向“所选对象旁的预估”
                    v.EffectText = "医务室为所选对象换药。可处理的出血、回血与病毒变化见所选对象旁的预估。";
                    var lack = svc.Inputs.Where(kv => AvailableCount(s, kv.Key, true) < kv.Value).Select(kv => $"{ItemName(kv.Key)}{AvailableCount(s, kv.Key, true)}/{kv.Value}").ToList();
                    if (v.Blocked == null && lack.Count > 0) v.Blocked = "材料不足（随身或当地地面）：" + string.Join("、", lack);
                    // 试玩修复 M306：不能换药的目标只写一句原因（原先预计/冷却与原因各写一遍，冷却重复嵌套）
                    v.Targets = PresentTargets(s, a => DressingBlocked(s, a), a => DressingBlocked(s, a) != null ? "" : DressingText(s, a, svc));
                    break;
                }
                case "clinic_observation_rest":
                case "church_quiet_rest":
                    v.EffectText = $"全体当前同行者在这里普通有遮蔽休息{svc.WorldMinutes}分钟，按原休息规则逐段恢复（不提供床、没有额外加成，饮食与病情照常进展，不是一次补满）；可随时停止，已休息的部分保留";
                    if (v.Blocked == null && !IsSheltered(s, svc.LocationId)) v.Blocked = "这里没有遮蔽";
                    break;
                case "church_relief_ration":
                {
                    v.NeedsTarget = true;
                    var day = Clock.Day(s.GameMinute);
                    var used = CityQuotaUsed(s, svc.Id, day);
                    // B38b（N02 附录 §2、Q286、Q291-8）：已领 = 已核销 + 同日有效在办预留（与额度判定同源）
                    v.EffectText = "符合条件时选一项。办完在这里的地面领取，之后自行食用或饮用。";
                    var held = ActiveCityTxn(s) is CityServiceTxn rp && rp.ServiceId == svc.Id && rp.StartDay == day && rp.QuotaReserved;
                    if (svc.DailyPartyQuota is int dq) v.Lines.Add($"本日全队已领{used}/{dq}份" + (held ? "（含在办预留）。" : "。"));
                    if (v.Blocked == null && svc.DailyPartyQuota is int q && used >= q) v.Blocked = $"第{day}日的全队救助额度已用完（每日{q}份）。";
                    v.Targets = PresentTargets(s, a => svc.Choices.Any(c => ReliefChoiceOk(a, c)) ? null : "饱食和水分均不低于40，当前没有可领的配给。", a => $"饱食{Fmt.N(a.Stats.Satiety)}、水分{Fmt.N(a.Stats.Hydration)}",
                        (name, why) => $"{name}的{why}");
                    v.Choices = svc.Choices.Select(c => new CityChoiceView { Id = c.Id, Label = c.Label }).ToList();
                    break;
                }
                case "academy_observation_record":
                    // Q146：不指向未实装的事项、不写“后续版本开放”或内部任务编号
                    v.EffectText = "查看已有的观察范围与记录（0分钟，不开始检测、不改身体）：" + (f.Contains(QuestFact("AC01", "completed"))
                        ? "已有观察核对记录" : "还没有观察核对记录");
                    break;
                case "shelter_recruitment":
                    v.EffectText = $"周期候选（每{Clock.Duration(Rc.RefreshMinutes)}一批{Rc.CandidatesPerCycle}名，0材料、{Clock.Duration(Rc.RegistrationMinutes)}登记）与生成角色退役在本面板办理；同行补员（RB01，{Clock.Duration(Config.Party.EscortTemplate.RegistrationMinutes)}）与借用矛报失在“队伍”面板；查看名册不耗时，不会替你招人";
                    break;
                case "church_reception_existing":
                case "academy_reception_existing":
                {
                    // B15（A08 规则 6、S15 gate事实接口）：两个选择同写同一路线事实，不宣誓/不检测也照样领到路线
                    var route = svc.Choices.SelectMany(c => c.Facts).FirstOrDefault() ?? "";
                    v.EffectText = svc.Id == "church_reception_existing"
                        ? "询问封锁区的公开路线（0分钟）：接受公开路线调查或拒绝宣誓，都领到同一条公开路线；不入教、不捐献、不作见证"
                        : "询问环境调查的公开路线（0分钟）：只调查环境记录或拒绝检测，都领到同一条公开路线；不做身体检测、不签空白同意";
                    v.Choices = svc.Choices.Select(c => new CityChoiceView { Id = c.Id, Label = c.Label }).ToList();
                    if (v.Blocked == null && f.Contains(route)) v.Blocked = "已领取公开路线（再问只回看，不再写入）";
                    break;
                }
                case TrainingServiceId:
                    TrainingPreview(s, svc, v);
                    break;
                case var ap when IsAppraisalService(ap):
                    // B18（R07 X3.2，Q33 选 B）：沿原契约，三项在鉴定交易面板里分页办理，不叠收本表费用
                    v.TimeText = $"说明0分钟 · 鉴定{Ap.AppraiseMinutes}分钟 · 换物{Ap.TradeMinutes}分钟";
                    v.CostText = $"鉴定：{string.Join("、", Ap.AppraiseConsume.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"))}（样本不消耗）；换物：交出普通活性物质1份";
                    v.EffectText = "免费说明样本品质、来源与既定融合结果；详细鉴定只给普通白色写档案（不升品、无新词条、不改变融合结果）；"
                        + $"以物换物按当日已锁定报价交出1份普通样本换一包常用品（全队每世界日共{Ap.TradeDailyPartyQuota}包），产物落鉴定所地面。教学样本不可交易";
                    v.StartLabel = "打开鉴定交易面板";
                    break;
                default:
                    // Q145：不把内部 effect 原文拼给玩家，用玩家摘要
                    v.EffectText = svc.Summary;
                    break;
            }
            if (QuestOfService(svc) is QuestConfig wq) QuestServicePreview(s, svc, wq, v);
            if (svc.Kind == KnowledgeCopyKind) KnowledgeCopyPreview(s, svc, v);
            return v;
        }

        /// <summary>任务类服务的预览（B15）：剩余工时（保留已完成的段）、效果说明、选项与真实阻拦原因。</summary>
        private void QuestServicePreview(GameState s, CityServiceConfig svc, QuestConfig q, CityServiceView v)
        {
            var left = QuestWorkLeft(s, q);
            var done = q.WorkMinutes - left;
            var going = done > 0 && !QuestDone(s, q.Id);
            v.TimeText = going ? $"{left}分钟" : $"{q.WorkMinutes}分钟";
            v.EffectText = svc.Id switch
            {
                "hub_affairs_record" => "整理圣所旧路档册与封存区安全规程。两段完成后，旧城后段的通行按这份记录办理。",
                "church_public_mediation" => "协商抄录公开路线，或以已有实地记录回营区备份（固定等待10分钟，两条路同一份委托记录）；不宣誓、不交出任何人，拒绝不扣精神或免疫、不封主线",
                "church_archive_copy" => "抄录已公开的路线备份（固定等待10分钟）；已在训诫堂完成同一归档的只回看，不再收时间、不加知识或物资",
                "academy_noninvasive_observation" => "按你选的范围核对：只核对环境记录，或只读你现有的状态（固定等待10分钟，两种选择同一份委托记录）；不抽血、不提交样本、不改变任何身体数值",
                "academy_report_compare" => "对照两份观察报告与带回的流程记录，把证据和猜测分开（固定等待10分钟）；完成后得到核对笔记与外部医疗线索，不授新药、永久免疫或容量",
                _ => q.StepActive,
            };
            // B38b（N02 附录 §2、Q291-7）：FA04 与 CH01/CH02/AC01/AC02 共用进度句，只在已有提交进度且任务未完成时另列
            if (going) v.Lines.Add($"已完成{done}分钟，还需{left}分钟。");
            if (svc.Choices.Count > 0 && v.Choices.Count == 0) v.Choices = svc.Choices.Select(c => new CityChoiceView { Id = c.Id, Label = c.Label }).ToList();
            if (v.Blocked == null) v.Blocked = QuestServiceBlocked(s, svc, q);
        }

        /// <summary>
        /// 公共工坊入口的说明（Q141 选 B）：0 分钟、0 额外费用、0 经验打开制作面板；逐站写当地公共站能不能用及真实原因；
        /// 随身手工配方照常；实际制作按原配方材料、工时、知识、熟练与锻炉燃料（每份配方另需木炭1）。
        /// </summary>
        /// <remarks>B38b（N02 附录 §2、Q291-3）：主句 + “已开放 {工位列表}”“{工位列表}未开放（{真实原因}）”分行（无对象的行省略），科学院另加告示行；
        /// 锻炉木炭数量从实际配方条件显示，不再写在这里。</remarks>
        public const string WorkshopAccessHead = "打开当地制作面板。随身手工配方也可以在这里查看。";

        private List<string> WorkshopAccessLines(GameState s, CityServiceConfig svc)
        {
            var stations = PublicStationsAt(svc.LocationId).ToList();
            var lines = new List<string>();
            var open = stations.Where(p => PublicStationBlocked(s, p) == null).Select(p => p.Name).ToList();
            if (open.Count > 0) lines.Add($"已开放 {string.Join("、", open)}");
            foreach (var g in stations.Where(p => !open.Contains(p.Name)).GroupBy(p => PublicStationBlocked(s, p) ?? ""))
                lines.Add($"{string.Join("、", g.Select(p => p.Name))}未开放（{g.Key}）");
            if (svc.Id == "academy_workshop_access") lines.Add("观察区通行请读门旁的「观察区公开通道告示」。");
            return lines;
        }

        private List<CityTargetView> PresentTargets(GameState s, Func<ActorState, string?> blocked, Func<ActorState, string> detail, Func<string, string, string>? line = null) =>
            s.Party.Where(s.Actors.ContainsKey).Select(id => s.Actors[id])
                .Select(a =>
                {
                    var name = ActorDisplayName(s, a.Id);
                    var why = blocked(a);
                    return new CityTargetView
                    {
                        ActorId = a.Id, Name = name, Blocked = why, Detail = detail(a),
                        BlockedLine = why == null ? null : line?.Invoke(name, why) ?? $"{name}：{why}",
                    };
                }).ToList();

        /// <summary>医疗咨询的内容：本人现有 H/I/V、出血与两种冷却（只读）。</summary>
        public string ConsultText(ActorState a)
        {
            var b = a.Body;
            var parts = new List<string> { $"健康{Fmt.N(a.Stats.Health)}/{Fmt.N(a.Stats.HealthMax)}", $"免疫{Fmt.N(b.Immunity)}" };
            // B38b（N02 附录 Q286 咨询感染状态）
            parts.Add(b.Infected ? $"已感染（病毒{Fmt.N(b.VirusLoad)}）" : $"未感染（病毒{Fmt.N(b.VirusLoad)}）");
            parts.Add(b.BleedTriggers > 0 ? "正在出血" : "没有出血");
            return string.Join("，", parts);
        }

        private string ConsultCooldowns(GameState s, ActorState a)
        {
            var parts = new List<string>();
            parts.Add(a.BandageReadyAt > s.GameMinute ? $"回血冷却还需{Clock.Duration(a.BandageReadyAt - s.GameMinute)}" : "回血冷却已就绪");
            if (a.Body.Infected) parts.Add(a.Body.BandageVirusReadyAt > s.GameMinute ? $"病毒减量冷却还需{Clock.Duration(a.Body.BandageVirusReadyAt - s.GameMinute)}" : "病毒减量冷却已就绪");
            return string.Join("，", parts);
        }

        /// <summary>咨询的冷却说明（N02 附录 Q286；换药无收益自 B38c 改用 RunningDressingCooldowns，只列在走的冷却）：“回血冷却还需{剩余时长}。”／“回血冷却已就绪。”，已感染再接病毒减量冷却句。</summary>
        public string CooldownSentences(GameState s, ActorState a)
        {
            var text = a.BandageReadyAt > s.GameMinute ? $"回血冷却还需{Clock.Duration(a.BandageReadyAt - s.GameMinute)}。" : "回血冷却已就绪。";
            if (a.Body.Infected)
                text += a.Body.BandageVirusReadyAt > s.GameMinute ? $"病毒减量冷却还需{Clock.Duration(a.Body.BandageVirusReadyAt - s.GameMinute)}。" : "病毒减量冷却已就绪。";
            return text;
        }

        private ItemUseConfig? BandageUse => BandageItem?.Use;

        /// <summary>换药对某名同行者有没有实际收益（与原绷带同一判定：回血、止血、病毒减量三者都无收益时拒绝）。</summary>
        private string? DressingBlocked(GameState s, ActorState a)
        {
            if (BandageUse == null) return "没有绷带定义";
            string? why;
            using (FocusOutOfBattleOn(s, a.Id)) why = MedicineBlocked(s, BandageUse);
            if (why == null) return null;
            // 冷却造成的无收益：只写一句（试玩修复 M306）。B38c（N02 附录 Q300）：回血或病毒减量（仅仍感染）有任一冷却在走就用
            // “当前换药没有收益。”，按回血、病毒减量顺序只列正在冷却的项目；两项都无冷却时沿用原实际原因。预览与事务重验同走这里。
            // B38d（N02 附录 Q302）：健康已满且无出血时回血冷却不算适用冷却；因此省掉回血句后什么都不剩时用“健康已满且没有出血，包扎没有效果”；仍感染（病毒已到下限）时按 Q304 用含“病毒减量也不可用”的一句。
            var fullNoBleed = a.Stats.Health >= a.Stats.HealthMax - 1e-9 && a.Body.BleedTriggers <= 0;
            var running = RunningDressingCooldowns(s, a, !fullNoBleed);
            if (running.Length > 0) return "当前换药没有收益。" + running;
            // Q304：仍感染且病毒已到包扎减量下限时，回血冷却结束前后都用同一句（与 MedicineBlocked 的原因一致）。
            if (!fullNoBleed || a.BandageReadyAt <= s.GameMinute) return why;
            return a.Body.Infected ? "健康已满、没有出血，病毒减量也不可用，包扎没有效果" : "健康已满且没有出血，包扎没有效果";
        }

        /// <summary>换药无收益时正在走的适用冷却（N02 附录 Q300、Q302）：“回血冷却还需{剩余时长}。”（健康已满且无出血时不列），仍感染再接“病毒减量冷却还需{剩余时长}。”；没有在走的为空串。</summary>
        private static string RunningDressingCooldowns(GameState s, ActorState a, bool healApplies)
        {
            var text = "";
            if (healApplies && a.BandageReadyAt > s.GameMinute) text += $"回血冷却还需{Clock.Duration(a.BandageReadyAt - s.GameMinute)}。";
            if (a.Body.Infected && a.Body.BandageVirusReadyAt > s.GameMinute) text += $"病毒减量冷却还需{Clock.Duration(a.Body.BandageVirusReadyAt - s.GameMinute)}。";
            return text;
        }

        private string DressingText(GameState s, ActorState a, CityServiceConfig svc)
        {
            var lv = svc.ActorSkillLevel ?? 1;
            using (FocusOutOfBattleOn(s, a.Id))
            {
                var parts = new List<string>();
                if (s.Body.BleedTriggers > 0) parts.Add("止血");
                var heal = s.BandageReadyAt <= s.GameMinute ? Math.Min(BandageHeal(s, lv, false), s.Stats.HealthMax - s.Stats.Health) : 0;
                if (heal > 0) parts.Add($"健康{Fmt.N(s.Stats.Health)}→{Fmt.N(s.Stats.Health + heal)}");
                if (BandageVirusUseful(s)) parts.Add($"病毒{Fmt.N(s.Body.VirusLoad)}→{Fmt.N(Math.Max(SuppressMin, s.Body.VirusLoad - BandageVirusReduction(s, lv)))}");
                return (parts.Count > 0 ? "预计：" + string.Join("，", parts) + "；" : "") + ConsultCooldowns(s, a);
            }
        }

        private static double StatOf(ActorState a, string stat) => stat switch
        {
            "satiety" => a.Stats.Satiety,
            "hydration" => a.Stats.Hydration,
            _ => double.MaxValue,
        };

        private static bool ReliefChoiceOk(ActorState a, CityServiceChoiceConfig c) => StatOf(a, c.Stat) < c.Below;

        private static string QuotaKey(string serviceId, int day) => $"{serviceId}@{day}";

        /// <summary>某服务某世界日已核销 + 进行中预留的份数（R07 日桶：预留占额，未完成不扣，完成才核销）。</summary>
        public int CityQuotaUsed(GameState s, string serviceId, int day)
        {
            var used = s.City.QuotaUsed.TryGetValue(QuotaKey(serviceId, day), out var n) ? n : 0;
            var p = ActiveCityTxn(s);
            if (p != null && p.ServiceId == serviceId && p.StartDay == day && p.QuotaReserved) used++;
            return used;
        }

        /// <summary>仍挂在主行动上的计时服务事务；行动已取消/结束的旧快照视为不存在。</summary>
        public CityServiceTxn? ActiveCityTxn(GameState s)
        {
            var p = s.City.Pending;
            if (p == null) return null;
            var a = s.Main;
            return a != null && a.ActionId == p.ActionId && a.TargetId == CityServicePrefix + p.ServiceId ? p : null;
        }

        // ───────────────────────── 办理 ─────────────────────────

        /// <summary>
        /// 办理一项城市服务：先按预览校验（地点、空闲、目标、选项、材料、配额），0 分钟的当场提交；有工时的开工为主行动（wait 或 rest），
        /// 预留材料与配额、记事务快照，按 5 分钟段推进，完成时重验并原子核销（取消或失败不扣材料、不扣额度、无完成效果）。
        /// </summary>
        public ActionResult StartCityService(GameState s, string serviceId, string? targetActorId = null, string? choiceId = null, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var svc = CityService(serviceId);
            if (svc == null) return ActionResult.Fail("没有这项事项");
            var v = CityServicePreview(s, serviceId)!;
            if (v.Blocked != null) return ActionResult.Fail(v.Blocked);
            CityTargetView? t = null;
            if (v.NeedsTarget)
            {
                t = v.Targets.FirstOrDefault(x => x.ActorId == (targetActorId ?? GameState.PlayerId));
                if (t == null) return ActionResult.Fail("目标不在当前同行队伍里");
                if (t.Blocked != null) return ActionResult.Fail(t.BlockedLine ?? $"{t.Name}：{t.Blocked}");
            }
            CityServiceChoiceConfig? choice = null;
            if (svc.Choices.Count > 0)
            {
                choice = svc.Choices.FirstOrDefault(c => c.Id == choiceId);
                if (choice == null) return ActionResult.Fail("先选一项。");
                if (!string.IsNullOrEmpty(choice.Stat) && !ReliefChoiceOk(s.Actors[t!.ActorId], choice)) return ActionResult.Fail($"{t.Name}当前不符合「{choice.Label}」的领取条件。");
            }
            var npc = Config.City.Npcs.FirstOrDefault(n => n.ServiceIds.Contains(serviceId))
                      ?? Config.City.Npcs.FirstOrDefault(n => Config.ExpansionQuests.Packs.Any(p => p.CopyServiceId == serviceId && p.CopyNpcId == n.NpcId));
            // B23：公开抄录选知识包（只有一包的直接用它），完全已知的包禁用
            KnowledgePackConfig? copyPack = null;
            if (svc.Kind == KnowledgeCopyKind)
            {
                var opts = CopyPacksOf(svc.Id);
                copyPack = opts.FirstOrDefault(p => p.Id == choiceId) ?? (opts.Count == 1 ? opts[0] : null);
                if (copyPack == null) return ActionResult.Fail("先选一项。");
                if (v.Choices.FirstOrDefault(c => c.Id == copyPack.Id)?.Blocked is string pb) return ActionResult.Fail(pb);
            }
            // B18：鉴定/换物要先在鉴定交易面板里选确切样本与报价（StartAppraise / StartAppraisalTrade），这里不代选
            if (IsAppraisalService(svc.Id)) return ActionResult.Fail("请在鉴定交易面板里选样本后办理");
            switch (svc.Id)
            {
                case "hub_registration_existing":
                {
                    RememberRequest(s, requestId);
                    var reg = Config.City.Registration;
                    OpenDialogue(s, reg.DialogueSet);
                    s.Progress.Flags.Add(reg.AuthorizedFact);
                    var r = RefreshGates(s);
                    var routes = RouteNotes(s, r, s.CurrentLocation);
                    // B38b（N02 附录 Q286）：营门授权成功句，实际发现路线时追加“新发现路线：……。”
                    Emit(s, GameEventKind.Info, $"营门已授权。沿通道前往{LocName(reg.SquareLocation)}完成登记。公共配给到广场另领。" + (routes.Count > 0 ? "新发现路线：" + string.Join("与", routes) + "。" : ""));
                    return ActionResult.Success();
                }
                case "clinic_consult":
                {
                    RememberRequest(s, requestId);
                    var a = s.Actors[t!.ActorId];
                    // B38b（N02 附录 Q286）：“{人物}的现有记录：……。{冷却说明}本次只查看记录。”
                    Emit(s, GameEventKind.Info, $"{t.Name}的现有记录：{ConsultText(a)}。{CooldownSentences(s, a)}本次只查看记录。");
                    return ActionResult.Success();
                }
                case "hub_workshop_access":
                case "academy_workshop_access":
                    // B38b（Q283、Q291-3）：办理日志直接用工坊定稿句与工位说明，不再外包“{服务名}：”；一条日志，按工位分行
                    RememberRequest(s, requestId);
                    Emit(s, GameEventKind.Info, string.Join("\n", new[] { v.EffectText }.Concat(v.Lines)));
                    return ActionResult.Success();
                case "academy_observation_record":
                case "shelter_recruitment":
                    RememberRequest(s, requestId);
                    Emit(s, GameEventKind.Info, $"{svc.Title}：{v.EffectText}。");
                    return ActionResult.Success();
                case "church_reception_existing":
                case "academy_reception_existing":
                {
                    // B15：FA02/FA03 接取——两个选择同写同一路线事实（只首次明确提交写入），重判门槛
                    RememberRequest(s, requestId);
                    var rq = Config.Quests.List.FirstOrDefault(x => x.AcceptServiceId == svc.Id && x.Implemented);
                    foreach (var fact in choice!.Facts) s.Progress.Flags.Add(fact);
                    if (rq != null) AcceptCore(s, rq, true, choice.Id);
                    var routes = RouteNotes(s, RefreshGates(s), s.CurrentLocation);
                    Emit(s, GameEventKind.Info, $"{svc.Title}：已领取公开路线（{choice.Label}）" + (routes.Count > 0 ? "；发现" + string.Join("与", routes) : "") + "。");
                    return ActionResult.Success();
                }
                case TrainingServiceId:
                    return StartTraining(s, svc, t!, npc, requestId);
            }
            // 计时服务
            var minutes = svc.WorldMinutes ?? 0;
            var quest = QuestOfService(svc);
            // B15：任务工作只补剩余的段（中断保留已完成的段）
            if (quest != null) minutes = QuestWorkLeft(s, quest);
            if (minutes <= 0) return ActionResult.Fail(CityNotOffered);
            var req = svc.Mode == "rest" ? ActionRequest.Rest(minutes) : ActionRequest.Wait(minutes);
            req.TargetId = CityServicePrefix + svc.Id;
            req.RequestId = requestId;
            var res = Start(s, req);
            if (!res.Ok) return res;
            var act = s.Main!;
            act.Label = $"{svc.Title}（{Clock.Duration(minutes)}）" + (t != null && t.ActorId != GameState.PlayerId ? $"：{t.Name}" : "");
            var txn = new CityServiceTxn
            {
                TxnId = s.City.NextTxn++, ActionId = act.ActionId, ServiceId = svc.Id, NpcId = npc?.NpcId ?? "", TargetActorId = t?.ActorId ?? "",
                ChoiceId = copyPack?.Id ?? choice?.Id, LocationId = s.CurrentLocation, StartDay = Clock.Day(s.GameMinute), QuotaReserved = svc.DailyPartyQuota != null,
            };
            foreach (var kv in svc.Inputs)
            {
                var ids = Reserve(s, kv.Key, kv.Value, act.ActionId);
                act.ReservedItemIds.AddRange(ids);
                txn.ReservedItemIds.AddRange(ids);
            }
            s.City.Pending = txn;
            if (quest != null)
            {
                // FA04 开始整理即接取（播接取对白，对话本身不替代工作提交）。支线的工作提示（XX_work，B15b Q161 选 C 起为八条各自的定稿句）
                // 是预览说明，显示在事项面板里，开工时不再弹对白（开发假设 B15-7）
                if (!QuestAccepted(s, quest.Id)) AcceptCore(s, quest, true, choice?.Id);
                if (choice != null) s.Quests.Of(quest.Id).Choice ??= choice.Id;
            }
            return res;
        }

        /// <summary>计时服务完成（段末结算后、死亡优先已判过）：重验地点、目标与条件，成功才原子核销材料/配额并给效果；失败原物原处、不扣额。</summary>
        private void CompleteCityService(GameState s, ActionState a)
        {
            var p = s.City.Pending;
            s.City.Pending = null;
            var svcId = a.TargetId.Substring(CityServicePrefix.Length);
            var svc = CityService(svcId);
            var reserved = s.Items.Where(i => i.ReservedBy == a.ActionId).ToList();
            void Release() { foreach (var i in reserved) i.ReservedBy = null; a.ReservedItemIds.Clear(); }
            if (svc == null || p == null || p.ActionId != a.ActionId)
            {
                Release();
                return;
            }
            if (p.AppraisalMode != null)
            {
                CompleteAppraisal(s, a, p, svc);   // B18 鉴定/换物：自有重验与提交
                return;
            }
            string? fail = s.Progress.Dead ? "本局已结束" : s.CurrentLocation != p.LocationId ? $"不在{LocName(p.LocationId)}" : null;
            ActorState? target = null;
            if (fail == null && p.TargetActorId != "")
            {
                if (!s.Party.Contains(p.TargetActorId) || !s.Actors.TryGetValue(p.TargetActorId, out target)) fail = "目标已不在同行队伍里";
            }
            var made = new List<string>();
            string? whole = null;   // B38b：定稿给出完整成功句的（施济、换药、抄录），日志只写这句、不外包“{服务名}完成：”
            if (fail == null && svc.Kind == KnowledgeCopyKind) fail = CompleteKnowledgeCopy(s, p, out whole);
            else if (fail == null)
            {
                switch (svc.Id)
                {
                    case "clinic_dressing":
                    {
                        if (svc.Inputs.Any(kv => reserved.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) < kv.Value)) { fail = "预留的换药材料已不足"; break; }
                        var why = DressingBlocked(s, target!);
                        if (why != null) { fail = why; break; }
                        var parts = new List<string>();
                        using (FocusOutOfBattleOn(s, target!.Id)) ApplyMedicine(s, BandageUse!, parts, svc.ActorSkillLevel ?? 1, grantXp: false, gearBonus: false);
                        foreach (var kv in svc.Inputs)
                        {
                            var left = kv.Value;
                            foreach (var i in reserved.Where(i => i.ItemId == kv.Key).ToList())
                            {
                                while (left > 0 && i.Count > 0) { ConsumeOne(s, i); left--; }
                            }
                        }
                        foreach (var i in reserved) i.ReservedBy = null;
                        a.ReservedItemIds.Clear();
                        // B38b（N02 附录 Q286、Q289）：“已为{人物}换药。{实际效果列表}。消耗{材料和数量列表}。”效果沿 ApplyMedicine 一条日志的逗号连接
                        whole = $"已为{ActorDisplayName(s, target.Id)}换药。{NoStop(string.Join("，", parts))}。消耗{string.Join("、", svc.Inputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"))}。";
                        break;
                    }
                    case "church_relief_ration":
                    {
                        var c = svc.Choices.FirstOrDefault(x => x.Id == p.ChoiceId);
                        // B38b（N02 附录 Q286）：失效、重验不合格、开工日额度用完三种原因，外层统一“{服务名}未完成。{真实原因}。材料和额度均未扣除。”
                        if (c == null) { fail = "所选配给项目已失效"; break; }
                        if (!ReliefChoiceOk(target!, c)) { fail = $"{ActorDisplayName(s, target!.Id)}已不符合「{c.Label}」的领取条件"; break; }
                        var key = QuotaKey(svc.Id, p.StartDay);
                        var used = s.City.QuotaUsed.TryGetValue(key, out var u) ? u : 0;
                        if (svc.DailyPartyQuota is int q && used >= q) { fail = $"第{p.StartDay}日的全队救助额度已用完"; break; }
                        s.City.QuotaUsed[key] = used + 1;
                        foreach (var kv in c.Outputs) CreateItems(s, kv.Key, kv.Value, s.Ground);
                        whole = $"{string.Join("、", c.Outputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"))}已放在{ZoneName(s, s.Ground)}地面。可自行领取。";
                        break;
                    }
                    case TrainingServiceId:
                        fail = CompleteTraining(s, p, target!, reserved, made);
                        break;
                    default:
                        Release();
                        break;
                }
            }
            if (fail == null && QuestOfService(svc) is QuestConfig wq)
            {
                // B15 任务工作：重验仍可做（未被别处完成），记满工作并写完成（交付对白代替“办完事项”）
                var why = QuestServiceBlocked(s, svc, wq);
                if (why != null) fail = why;
                else
                {
                    s.City.Completed++;
                    Emit(s, GameEventKind.ActionCompleted, $"{svc.Title}完成（{Clock.Duration(a.PlannedMinutes)}）。");
                    CompleteQuestWork(s, wq, p);
                    return;
                }
            }
            if (fail != null)
            {
                Release();
                // B38b（N02 附录 Q286）：施济、换药完成阶段的失败外层；其余服务（抄录 Q287 等）沿原外层，嵌入的完整句去掉句末句号
                Emit(s, GameEventKind.ActionCompleted, svc.Id is "church_relief_ration" or "clinic_dressing"
                    ? $"{svc.Title}未完成。{Sentence(fail)}材料和额度均未扣除。"
                    : $"{svc.Title}没有完成：{NoStop(fail)}（材料与额度未扣）。");
                return;
            }
            s.City.Completed++;
            Emit(s, GameEventKind.ActionCompleted, whole ?? $"{svc.Title}完成" + (made.Count > 0 ? "：" + string.Join("；", made) : "") + "。", null, svc.Choices.SelectMany(c => c.Outputs.Keys));
            CityServiceEnd(s, p.NpcId);
        }

        /// <summary>取消计时服务：预留材料只解除预留、留在原持有处（不像制作那样返还到地面），事务快照作废（配额未扣）。</summary>
        private void ReleaseCityService(GameState s, ActionState a)
        {
            if (s.City.Pending is CityServiceTxn ap && ap.ActionId == a.ActionId && ap.AppraisalMode != null)
            {
                // B18：样本/纤维原物原处、日额度预留随事务作废（S01：非消耗服务专用，不套制作返地）
                ReleaseAppraisal(s, a);
                s.City.Pending = null;
                return;
            }
            foreach (var i in s.Items.Where(i => i.ReservedBy == a.ActionId)) i.ReservedBy = null;
            a.ReservedItemIds.Clear();
            // B15：任务工作中断保留已提交的整段（A11 §5“工作中断按剩余段续算”、FA04“离开保留已完成段”）
            if (s.City.Pending is CityServiceTxn p && p.ActionId == a.ActionId) KeepQuestWorkSegments(s, a, p);
            if (s.City.Pending?.ActionId == a.ActionId) s.City.Pending = null;
        }

        /// <summary>休息类服务结束（rest 段已按原规则逐段结算）：只写结束与岗位“办完事项”台词，不另给效果。</summary>
        private void CompleteCityRest(GameState s, ActionState a)
        {
            var p = s.City.Pending;
            s.City.Pending = null;
            var svc = CityService(a.TargetId.Substring(CityServicePrefix.Length));
            Emit(s, GameEventKind.ActionCompleted, $"{svc?.Title ?? "休息"}结束（{Clock.Duration(a.PlannedMinutes)}，按普通有遮蔽休息逐段恢复）。");
            if (p != null && p.ActionId == a.ActionId) { s.City.Completed++; CityServiceEnd(s, p.NpcId); }
        }

        private void CityServiceEnd(GameState s, string npcId)
        {
            var n = CityNpc(npcId);
            if (n == null || s.Progress.Dead || s.CurrentLocation != n.LocationId) return;
            if (s.City.Absent.Contains(n.NpcId))
            {
                // B26a：Q235 定了服务结束入口也只介绍一次：未示且没有战斗/敌踪挡着才播；已示不重播。
                // 挡着时这次不播不记已示，记一条待补播，处理完在下一个安全边界自动显示（Q243 选 B，见 SubstituteIntroBoundary）。
                // 已示之后服务结束不播任何对白（Q237），只留原结束日志和事务效果
                if (InCombatFlow(s)) DeferSubstituteIntro(s, n);
                else PlaySubstituteIntro(s, n);
                return;
            }
            OpenReplay(s, StateSet(n, "service_end"));
        }

        // ───────────────────────── 公开告示 ─────────────────────────

        private IEnumerable<DocumentCardView> CityNoticesHere(GameState s)
        {
            if (!s.Locations.TryGetValue(s.CurrentLocation, out var ls) || !ls.Visited) yield break;
            foreach (var n in Config.City.Notices.Where(n => n.LocationId == s.CurrentLocation))
            {
                var done = s.Progress.Flags.Contains(n.GrantsFact);
                // B16 试玩修复 M351：告示授的知识已从别处掌握（开发材料包、d06_08 矿道作业手册）时写“已掌握”，不再当作可学
                var knownElsewhere = !done && n.KnowledgeId != null && KnowsKnowledge(s, n.KnowledgeId);
                string? need = knownElsewhere ? null : NoticeNeed(s, n);
                yield return new DocumentCardView
                {
                    Id = n.Id, Title = n.Title,
                    Status = knownElsewhere ? "已掌握 · 可查看" : done ? (n.KnowledgeId != null ? "已学会 · 可重读" : "已办理通行 · 可重读") : need ?? (n.Choices.Count > 0 ? "选择通行范围 · 不耗时" : "可查看 · 不耗时"),
                    ReadBlocked = need ?? DocumentBlocked(s),
                    OwnBlocked = need,
                    Choices = done ? new List<CityChoiceView>() : n.Choices.Select(c => new CityChoiceView { Id = c.Id, Label = c.Label }).ToList(),
                };
            }
        }

        /// <summary>告示现在还不能看的原因：缺前置事实，或熟练不够（B16 基础陶工说明要制造3）；能看为 null。</summary>
        private string? NoticeNeed(GameState s, CityNoticeConfig n)
        {
            if (n.RequiresFact != null && !s.Progress.Flags.Contains(n.RequiresFact)) return $"需要：{FactLabelOf(n.RequiresFact)}";
            foreach (var kv in n.MinSkills)
            {
                var lv = SkillLevel(s, kv.Key);
                if (lv < kv.Value) return $"需要：{SkillName(kv.Key)}{kv.Value}级（当前{lv}级）";
            }
            return null;
        }

        private string FactLabelOf(string key) => Config.Map.FactLabels.TryGetValue(key, out var t) ? t : key;

        /// <summary>
        /// 查看城市公开告示（0 分钟）：h03 制作告示（要营区登记）首次查看写 h03_notice_read 即学会 expedition_basics（一次，不发物、不给经验）；
        /// r07 观察区公开通道告示两个选项同写 academy_observation_pass 并重判门槛（拒绝试验同样通行，S02 规则 6）。重读只回放。
        /// </summary>
        private ActionResult ReadCityNotice(GameState s, CityNoticeConfig n, string? choiceId)
        {
            if (s.CurrentLocation != n.LocationId) return ActionResult.Fail("不在这里");
            if (n.KnowledgeId != null && !s.Progress.Flags.Contains(n.GrantsFact) && KnowsKnowledge(s, n.KnowledgeId))
            {
                if (DocumentBlocked(s) is string busy) return ActionResult.Fail(busy);
                s.Progress.Flags.Add(n.GrantsFact);
                var known = Config.Knowledge.FirstOrDefault(x => x.Id == n.KnowledgeId);
                Emit(s, GameEventKind.Info, $"{n.Title}：已掌握{known?.Name ?? n.KnowledgeId}，不再重复学会（不发物、不给经验）。");
                return ActionResult.Success();
            }
            if (NoticeNeed(s, n) is string need) return ActionResult.Fail(need);
            var why = DocumentBlocked(s);
            if (why != null) return ActionResult.Fail(why);
            var f = s.Progress.Flags;
            if (f.Contains(n.GrantsFact))
            {
                if (!string.IsNullOrEmpty(n.DialogueSet)) OpenReplay(s, n.DialogueSet!);
                else Emit(s, GameEventKind.Info, $"{n.Title}已看过：内容已记下，重读不会再给什么。");
                return ActionResult.Success();
            }
            CityChoiceConfig? choice = null;
            if (n.Choices.Count > 0)
            {
                choice = n.Choices.FirstOrDefault(c => c.Id == choiceId);
                if (choice == null) return ActionResult.Fail("先选择通行范围");
            }
            if (!string.IsNullOrEmpty(n.DialogueSet)) OpenDialogue(s, n.DialogueSet!);
            f.Add(n.GrantsFact);
            if (n.KnowledgeId != null)
            {
                var k = Config.Knowledge.FirstOrDefault(x => x.Id == n.KnowledgeId);
                Emit(s, GameEventKind.Info, $"已从{n.Title}学会做法知识：{k?.Name ?? n.KnowledgeId}（0分钟，不发物、不给经验）。");
            }
            else
            {
                var r = RefreshGates(s);
                var routes = RouteNotes(s, r, s.CurrentLocation);
                Emit(s, GameEventKind.Info, $"已办理观察区公开通行（{choice!.Label}；不改变身体状态）" + (routes.Count > 0 ? "；发现" + string.Join("与", routes) : "") + "。");
            }
            return ActionResult.Success();
        }

        // ───────────────────────── 战斗外临时视角 ─────────────────────────

        /// <summary>战斗外临时把“主角视角”切到某名同行者（换药对目标调用原绷带函数，冷却记在目标身上）；using 结束恢复。</summary>
        public IDisposable FocusOutOfBattleOn(GameState s, string actorId) => new OutOfBattleFocus(s, actorId);

        private sealed class OutOfBattleFocus : IDisposable
        {
            private readonly GameState _s;
            private readonly string? _prev;

            public OutOfBattleFocus(GameState s, string actorId)
            {
                _s = s;
                _prev = s.FocusOutOfBattle;
                s.FocusOutOfBattle = actorId;
            }

            public void Dispose() => _s.FocusOutOfBattle = _prev;
        }

        // ───────────────────────── 开发入口 ─────────────────────────

        /// <summary>开发入口（?dev=1）：当前地点岗位人物离岗/回岗（离岗由当班接续员代班，测试岗位接续；回岗只供测试，正常游戏不复活）。</summary>
        public string DebugToggleCityPostAbsent(GameState s)
        {
            var list = Config.City.Npcs.Where(n => n.LocationId == s.CurrentLocation).ToList();
            if (list.Count == 0) return "开发：当前地点没有岗位人物";
            var notes = new List<string>();
            foreach (var n in list)
            {
                if (s.City.Absent.Remove(n.NpcId)) notes.Add($"{n.Name}回岗");
                else { s.City.Absent.Add(n.NpcId); notes.Add($"{n.Name}离岗（由机构当班接续员代班）"); }
            }
            SubstituteIntroBoundary(s);   // Q243 identity：回岗当场取消待补播，再离岗不恢复
            return "开发：" + string.Join("、", notes) + "。";
        }
    }
}

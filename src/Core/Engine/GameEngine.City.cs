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
        /// <summary>办理按钮上的字（为空时界面按工时写“办理（0分钟）”/“开始（N分钟）”）。</summary>
        public string? StartLabel { get; set; }
    }

    public sealed class CityTargetView
    {
        public string ActorId { get; set; } = "";
        public string Name { get; set; } = "";
        /// <summary>此目标的现有状态说明（咨询内容、预计效果）。</summary>
        public string Detail { get; set; } = "";
        public string? Blocked { get; set; }
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

        /// <summary>实际到达成功后的城市事务：持登记授权真实走到广场，写 hub_registered（C2、登记默认知识）与 QMAP01 完成；幂等。</summary>
        private void ArriveCity(GameState s, string locationId, List<string> notes)
        {
            var reg = Config.City.Registration;
            if (string.IsNullOrEmpty(reg.SquareLocation) || locationId != reg.SquareLocation) return;
            var f = s.Progress.Flags;
            if (!f.Contains(reg.AuthorizedFact) || !f.Add(reg.RegisteredFact)) return;
            if (!string.IsNullOrEmpty(reg.QuestId)) f.Add(QuestFact(reg.QuestId, "completed"));
            var know = Config.Knowledge.Count(k => k.GrantedByFacts.Contains(reg.RegisteredFact));
            notes.Add($"营区登记完成：队伍容量升到 C{BattleCapacity(s)}" + (know > 0 ? $"，学会营区登记默认做法知识 {know} 项" : "") + "（不发额外物资，再到广场不会重复登记）");
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
                var canDo = services.Count(CityServiceDoable);
                yield return new NpcCardView
                {
                    NpcId = n.NpcId,
                    Name = absent ? n.DutySpeaker : n.Name,
                    Role = absent ? $"代{n.Role}（{n.Name}已不在岗）" : n.Role,
                    FactionName = FactionName(n.FactionId),
                    Kind = "post",
                    KindLabel = absent ? "代班" : "岗位",
                    Substitute = absent,
                    Status = services.Count > 0 ? $"可交谈 · 事项 {canDo}/{services.Count} 项可办" : "可交谈",
                    ServiceCount = services.Count,
                    // B15：只写已实装且已显示的委托（正式名 + 状态），不出内部任务编号（Q145/Q146）
                    QuestText = QuestCardText(s, n.NpcId),
                    RecruitText = "岗位人物，不可招募",
                    HasNews = !s.City.Greeted.Contains(n.NpcId) && !absent,
                    TalkBlocked = TalkBlocked(s),
                };
            }
        }

        /// <summary>一项事项现在能不能办（卡面“可办数”）：没有通用阻拦，要目标的至少有一名合法目标。</summary>
        private static bool CityServiceDoable(CityServiceView v) => v.Blocked == null && (!v.NeedsTarget || v.Targets.Any(t => t.Blocked == null));

        /// <summary>
        /// 与岗位人物交谈（0 分钟，不扣物、不恢复、不改身体、不写事实）：首次播“初次见面”与“岗位说明”；之后播“闲谈”
        /// （B14b，Q146 选 B：未实装本身不触发“暂不可办”；目前没有已实装且剧情条件不足的事项，locked 组暂不使用）；
        /// 命名人物退场时播“岗位接续”（代班员，不复活原人物）。回看只回放台词。
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
            if (c.Absent.Contains(n.NpcId))
            {
                // 联系人退场不锁任务：代班员接续，委托照接（不播原人物的接取台词）
                OpenReplay(s, StateSet(n, "institution_substitute"));
                if (offer != null) AcceptCore(s, offer, false);
                return ActionResult.Success();
            }
            var first = c.Greeted.Add(n.NpcId);
            if (first)
            {
                OpenReplay(s, StateSet(n, "first_greeting"));
                OpenReplay(s, StateSet(n, "role"));
            }
            if (offer != null)
            {
                AcceptCore(s, offer, true);
                return ActionResult.Success();
            }
            if (first) return ActionResult.Success();
            OpenReplay(s, StateSet(n, QuestTalkState(s, n) ?? "idle"));
            return ActionResult.Success();
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

        private string? CityCommonBlocked(GameState s, CityServiceConfig svc)
        {
            // Q146：隐藏后的旧入口/直接请求拒绝，零扣费零效果；不写“后续版本开放”
            if (!svc.Implemented) return "这里现在不办理这项事项";
            if (s.Progress.Dead) return "角色已死亡";
            if (s.CurrentLocation != svc.LocationId) return $"须实际到达{LocName(svc.LocationId)}才能办理";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null || s.Encounter != null) return "先处理眼前的敌踪";
            if (s.Main != null || s.Sub != null) return "行动进行中";
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
                    v.EffectText = "核对来路与去向后登记授权（不选阵营、不交同行者）；之后沿营门→广场的路实际走到营区广场，才完成营区登记（队伍容量 C2 与登记默认做法知识，各一次）";
                    if (v.Blocked == null && f.Contains(reg.RegisteredFact)) v.Blocked = "已完成营区登记";
                    else if (v.Blocked == null && f.Contains(reg.AuthorizedFact)) v.Blocked = $"已登记授权：沿通道走到{LocName(reg.SquareLocation)}即完成登记";
                    break;
                case "hub_workshop_access":
                case "academy_workshop_access":
                    v.OpensCraft = true;
                    v.EffectText = WorkshopAccessText(s, svc);
                    break;
                case "clinic_consult":
                    v.NeedsTarget = true;
                    v.EffectText = "选一名当前同行者，说明本人现有健康、免疫、病毒、出血与回血/病毒减量冷却；不新抽诊断、不恢复、不治愈";
                    v.Targets = PresentTargets(s, a => null, ConsultText);
                    break;
                case "clinic_dressing":
                {
                    v.NeedsTarget = true;
                    v.EffectText = $"{svc.WorldMinutes}分钟后由医务室代办一次草药绷带：止血、按原公式回血（医疗按1级、不加装备加值）、病毒减量（最低1）；目标的回血与病毒减量冷却照常共享，主角不得经验；没有任何实际收益不开始";
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
                    v.EffectText = $"{svc.WorldMinutes}分钟后在这里地面领到一份普通食物或净水（不自动吃喝）：目标饱食低于40可领熟块根1份，或水分低于40可领净水1份，只选一项；全队每世界日合计最多{svc.DailyPartyQuota}份（今天已领{used}份）";
                    if (v.Blocked == null && svc.DailyPartyQuota is int q && used >= q) v.Blocked = $"今天（第{day}日）的救助配给额度已用完（全队每日{q}份，代班或换人来问不会多出一份）";
                    v.Targets = PresentTargets(s, a => svc.Choices.Any(c => ReliefChoiceOk(a, c)) ? null : "饱食与水分都不低于40，没有可领的一项", a => $"饱食{Fmt.N(a.Stats.Satiety)}、水分{Fmt.N(a.Stats.Hydration)}");
                    v.Choices = svc.Choices.Select(c => new CityChoiceView { Id = c.Id, Label = c.Label }).ToList();
                    break;
                }
                case "academy_observation_record":
                    // Q146：不指向未实装的事项、不写“后续版本开放”或内部任务编号
                    v.EffectText = "查看已有的观察范围与记录（0分钟，不开始检测、不改身体）：" + (f.Contains(QuestFact("AC01", "completed"))
                        ? "已有观察核对记录" : "还没有观察核对记录");
                    break;
                case "shelter_recruitment":
                    v.EffectText = "沿用收容所原有入口：同行补员登记（30分钟）、借用矛报失与名册在“队伍”面板办理；查看名册不耗时，不会替你招人";
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
                default:
                    // Q145：不把内部 effect 原文拼给玩家，用玩家摘要
                    v.EffectText = svc.Summary;
                    break;
            }
            if (QuestOfService(svc) is QuestConfig wq) QuestServicePreview(s, svc, wq, v);
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
                "hub_affairs_record" => "交接并整理圣所旧路档册与封存区安全规程两份调查记录（固定等待10分钟，按两段5分钟提交，两段都完成才算整理完成；中途停下保留已完成的段）；完成后旧城后段（警备仓）的通行按这份记录办理，已获得的容量与物资不再发第二次",
                "church_public_mediation" => "协商抄录公开路线，或以已有实地记录回营区备份（固定等待10分钟，两条路同一份委托记录）；不宣誓、不交出任何人，拒绝不扣精神或免疫、不封主线",
                "church_archive_copy" => "抄录已公开的路线备份（固定等待10分钟）；已在训诫堂完成同一归档的只回看，不再收时间、不加知识或物资",
                "academy_noninvasive_observation" => "按你选的范围核对：只核对环境记录，或只读你现有的状态（固定等待10分钟，两种选择同一份委托记录）；不抽血、不提交样本、不改变任何身体数值",
                "academy_report_compare" => "对照两份观察报告与带回的流程记录，把证据和猜测分开（固定等待10分钟）；完成后得到核对笔记与外部医疗线索，不授新药、永久免疫或容量",
                _ => q.StepActive,
            } + (going ? $"；已完成{done}分钟，续做剩余{left}分钟" : "");
            if (svc.Choices.Count > 0 && v.Choices.Count == 0) v.Choices = svc.Choices.Select(c => new CityChoiceView { Id = c.Id, Label = c.Label }).ToList();
            if (v.Blocked == null) v.Blocked = QuestServiceBlocked(s, svc, q);
        }

        /// <summary>
        /// 公共工坊入口的说明（Q141 选 B）：0 分钟、0 额外费用、0 经验打开制作面板；逐站写当地公共站能不能用及真实原因；
        /// 随身手工配方照常；实际制作按原配方材料、工时、知识、熟练与锻炉燃料（每份配方另需木炭1）。
        /// </summary>
        private string WorkshopAccessText(GameState s, CityServiceConfig svc)
        {
            var stations = PublicStationsAt(svc.LocationId).ToList();
            var parts = new List<string>();
            var open = stations.Where(p => PublicStationBlocked(s, p) == null).Select(p => p.Name).ToList();
            if (open.Count > 0) parts.Add($"已开放：{string.Join("、", open)}");
            foreach (var g in stations.Where(p => !open.Contains(p.Name)).GroupBy(p => PublicStationBlocked(s, p) ?? ""))
                parts.Add($"{string.Join("、", g.Select(p => p.Name))}未开放（{g.Key}）");
            var text = "打开制作面板（0分钟，无额外费用、不给经验）：" + (parts.Count > 0 ? string.Join("；", parts) + "；" : "")
                + "随身手工配方照常。实际制作仍按配方的材料（只取随身与当地地面）、工时、做法知识与熟练门槛，由你执行"
                + (stations.Any(p => p.StationId == "forge") ? "；锻炉每份配方另需木炭1" : "");
            if (svc.Id == "academy_workshop_access") text += "；观察区通行另看门旁的“观察区公开通道告示”，打开工坊不代办";
            return text;
        }

        private List<CityTargetView> PresentTargets(GameState s, Func<ActorState, string?> blocked, Func<ActorState, string> detail) =>
            s.Party.Where(s.Actors.ContainsKey).Select(id => s.Actors[id])
                .Select(a => new CityTargetView { ActorId = a.Id, Name = ActorDisplayName(s, a.Id), Blocked = blocked(a), Detail = detail(a) }).ToList();

        /// <summary>医疗咨询的内容：本人现有 H/I/V、出血与两种冷却（只读）。</summary>
        public string ConsultText(ActorState a)
        {
            var b = a.Body;
            var parts = new List<string> { $"健康{Fmt.N(a.Stats.Health)}/{Fmt.N(a.Stats.HealthMax)}", $"免疫{Fmt.N(b.Immunity)}" };
            parts.Add(b.Infected ? $"已感染，病毒{Fmt.N(b.VirusLoad)}" : $"未感染（病毒{Fmt.N(b.VirusLoad)}）");
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

        private ItemUseConfig? BandageUse => BandageItem?.Use;

        /// <summary>换药对某名同行者有没有实际收益（与原绷带同一判定：回血、止血、病毒减量三者都无收益时拒绝）。</summary>
        private string? DressingBlocked(GameState s, ActorState a)
        {
            if (BandageUse == null) return "没有绷带定义";
            string? why;
            using (FocusOutOfBattleOn(s, a.Id)) why = MedicineBlocked(s, BandageUse);
            if (why == null) return null;
            // 冷却造成的无收益：只写一句“现在换药没有效果：回血冷却还需…，病毒减量冷却还需…”（试玩修复 M306）
            return a.BandageReadyAt > s.GameMinute ? "现在换药没有效果：" + ConsultCooldowns(s, a) : why;
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
                if (t.Blocked != null) return ActionResult.Fail($"{t.Name}：{t.Blocked}");
            }
            CityServiceChoiceConfig? choice = null;
            if (svc.Choices.Count > 0)
            {
                choice = svc.Choices.FirstOrDefault(c => c.Id == choiceId);
                if (choice == null) return ActionResult.Fail("先选一项");
                if (!string.IsNullOrEmpty(choice.Stat) && !ReliefChoiceOk(s.Actors[t!.ActorId], choice)) return ActionResult.Fail($"{t.Name}不符合这一项（{choice.Label}）");
            }
            var npc = Config.City.Npcs.FirstOrDefault(n => n.ServiceIds.Contains(serviceId));
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
                    Emit(s, GameEventKind.Info, $"{svc.Title}：已在营门确认登记意愿，沿通道实际走到{LocName(reg.SquareLocation)}才完成营区登记（不预发任何物资）" + (routes.Count > 0 ? "；发现" + string.Join("与", routes) : "") + "。");
                    return ActionResult.Success();
                }
                case "clinic_consult":
                {
                    RememberRequest(s, requestId);
                    var a = s.Actors[t!.ActorId];
                    Emit(s, GameEventKind.Info, $"{svc.Title}（{t.Name}）：{ConsultText(a)}；{ConsultCooldowns(s, a)}。医务室只说明现有记录，不新抽诊断、不处理伤口、不清除感染。");
                    return ActionResult.Success();
                }
                case "hub_workshop_access":
                case "academy_workshop_access":
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
            if (minutes <= 0) return ActionResult.Fail("这里现在不办理这项事项");
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
                ChoiceId = choice?.Id, LocationId = s.CurrentLocation, StartDay = Clock.Day(s.GameMinute), QuotaReserved = svc.DailyPartyQuota != null,
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
                // FA04 开始整理即接取（播接取对白，对话本身不替代工作提交）；有工作提示的委托首次开工播一次
                if (!QuestAccepted(s, quest.Id)) AcceptCore(s, quest, true, choice?.Id);
                else if (!string.IsNullOrEmpty(quest.WorkDialogue)) OpenDialogue(s, quest.WorkDialogue!);
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
            string? fail = s.Progress.Dead ? "本局已结束" : s.CurrentLocation != p.LocationId ? $"不在{LocName(p.LocationId)}" : null;
            ActorState? target = null;
            if (fail == null && p.TargetActorId != "")
            {
                if (!s.Party.Contains(p.TargetActorId) || !s.Actors.TryGetValue(p.TargetActorId, out target)) fail = "目标已不在同行队伍里";
            }
            var made = new List<string>();
            if (fail == null)
            {
                switch (svc.Id)
                {
                    case "clinic_dressing":
                    {
                        if (svc.Inputs.Any(kv => reserved.Where(i => i.ItemId == kv.Key).Sum(i => i.Count) < kv.Value)) { fail = "预留的材料已不在"; break; }
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
                        made.Add($"{ActorDisplayName(s, target.Id)}：{string.Join("，", parts)}；消耗{string.Join("、", svc.Inputs.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"))}");
                        break;
                    }
                    case "church_relief_ration":
                    {
                        var c = svc.Choices.FirstOrDefault(x => x.Id == p.ChoiceId);
                        if (c == null || !ReliefChoiceOk(target!, c)) { fail = $"{ActorDisplayName(s, target!.Id)}已不符合这一项"; break; }
                        var key = QuotaKey(svc.Id, p.StartDay);
                        var used = s.City.QuotaUsed.TryGetValue(key, out var u) ? u : 0;
                        if (svc.DailyPartyQuota is int q && used >= q) { fail = "当日救助配给额度已用完"; break; }
                        s.City.QuotaUsed[key] = used + 1;
                        foreach (var kv in c.Outputs) CreateItems(s, kv.Key, kv.Value, s.CurrentLocation);
                        made.Add($"{string.Join("、", c.Outputs.Select(kv => $"{ItemName(kv.Key)}+{kv.Value}"))}，已放在{LocName(s.CurrentLocation)}地面（不自动吃喝）");
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
                Emit(s, GameEventKind.ActionCompleted, $"{svc.Title}没有完成：{fail}（材料与额度未扣）。");
                return;
            }
            s.City.Completed++;
            Emit(s, GameEventKind.ActionCompleted, $"{svc.Title}完成" + (made.Count > 0 ? "：" + string.Join("；", made) : "") + "。", null, svc.Choices.SelectMany(c => c.Outputs.Keys));
            CityServiceEnd(s, p.NpcId);
        }

        /// <summary>取消计时服务：预留材料只解除预留、留在原持有处（不像制作那样返还到地面），事务快照作废（配额未扣）。</summary>
        private void ReleaseCityService(GameState s, ActionState a)
        {
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
            OpenReplay(s, StateSet(n, s.City.Absent.Contains(n.NpcId) ? "institution_substitute" : "service_end"));
        }

        // ───────────────────────── 公开告示 ─────────────────────────

        private IEnumerable<DocumentCardView> CityNoticesHere(GameState s)
        {
            if (!s.Locations.TryGetValue(s.CurrentLocation, out var ls) || !ls.Visited) yield break;
            foreach (var n in Config.City.Notices.Where(n => n.LocationId == s.CurrentLocation))
            {
                var done = s.Progress.Flags.Contains(n.GrantsFact);
                string? need = n.RequiresFact != null && !s.Progress.Flags.Contains(n.RequiresFact) ? $"需要：{FactLabelOf(n.RequiresFact)}" : null;
                yield return new DocumentCardView
                {
                    Id = n.Id, Title = n.Title,
                    Status = done ? (n.KnowledgeId != null ? "已学会 · 可重读" : "已办理通行 · 可重读") : need ?? (n.Choices.Count > 0 ? "选择通行范围 · 不耗时" : "可查看 · 不耗时"),
                    ReadBlocked = need ?? TalkBlocked(s),
                    Choices = done ? new List<CityChoiceView>() : n.Choices.Select(c => new CityChoiceView { Id = c.Id, Label = c.Label }).ToList(),
                };
            }
        }

        private string FactLabelOf(string key) => Config.Map.FactLabels.TryGetValue(key, out var t) ? t : key;

        /// <summary>
        /// 查看城市公开告示（0 分钟）：h03 制作告示（要营区登记）首次查看写 h03_notice_read 即学会 expedition_basics（一次，不发物、不给经验）；
        /// r07 观察区公开通道告示两个选项同写 academy_observation_pass 并重判门槛（拒绝试验同样通行，S02 规则 6）。重读只回放。
        /// </summary>
        private ActionResult ReadCityNotice(GameState s, CityNoticeConfig n, string? choiceId)
        {
            if (s.CurrentLocation != n.LocationId) return ActionResult.Fail("不在这里");
            if (n.RequiresFact != null && !s.Progress.Flags.Contains(n.RequiresFact)) return ActionResult.Fail($"需要：{FactLabelOf(n.RequiresFact)}");
            var why = TalkBlocked(s);
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
            return "开发：" + string.Join("、", notes) + "。";
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 队伍与共同生存（R08 §1—§5、A11 §5—§8；开发计划 v0.9 批次 B8，R31/R32）：名册、一次初始化、容量 C 内编队与战前预设、候补。
    /// 正式招募任务（RN01—RN06）在后续批次，本批只能经开发入口加入名册（DebugRecruit）。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>队伍容量 C（含主角，1—4）：与战斗容量同源，事实只增不减（Q62）。</summary>
        public int PartyCapacity(GameState s) => BattleCapacity(s);

        public RosterEntry? RosterOf(GameState s, string actorId) => s.Roster.FirstOrDefault(r => r.ActorId == actorId);

        /// <summary>名册里存活、不在同行队伍的角色（候补，在营区接受公共照护）。</summary>
        public List<RosterEntry> Reserves(GameState s) => s.Roster.Where(r => !r.Dead && !s.Party.Contains(r.ActorId) && s.Actors.ContainsKey(r.ActorId)).ToList();

        /// <summary>同行队伍里的名册角色（不含主角）。</summary>
        public List<RosterEntry> ActiveCompanions(GameState s) => s.Roster.Where(r => !r.Dead && s.Party.Contains(r.ActorId)).ToList();

        public bool IsReserve(GameState s, string actorId) => Reserves(s).Any(r => r.ActorId == actorId);

        /// <summary>文案里的称呼：主角“你”，队友为名册名（无名册的测试注入角色为“队友&lt;id&gt;”）。</summary>
        public string ActorDisplayName(GameState s, string actorId) =>
            actorId == GameState.PlayerId ? "你" : s.Actors.TryGetValue(actorId, out var a) ? ActorLabel(a) : RosterOf(s, actorId)?.Name ?? $"队友{actorId}";

        /// <summary>营区（v3 地图前用替身地点，待确认 Q89 选 A）。</summary>
        public string HubLocation => Config.Party.HubStandIn;
        public bool AtHub(GameState s) => s.CurrentLocation == HubLocation;

        /// <summary>人物基础档（Q63）：命名队友与主角按固定档，护送员按 rb01Escort；都没有时为 null。</summary>
        public ActorDefenseProfileConfig? ActorProfile(GameState s, string actorId)
        {
            var abd = Config.ActorBaseDefense;
            if (RosterOf(s, actorId) is RosterEntry r && r.IsEscort) return abd.Rb01Escort;
            return abd.Profile(actorId);
        }

        /// <summary>人物基础速度（A11 §6：六人 4/3/5/6/4/5，补员 4；主角 4）。</summary>
        public double ActorBaseSpeed(GameState s, string actorId)
        {
            if (actorId == GameState.PlayerId) return Config.Scope.BasePlayerSpeed;
            if (RosterOf(s, actorId) is RosterEntry r && r.IsEscort) return Config.Party.EscortTemplate.BaseSpeed;
            return Config.Party.Named(actorId)?.BaseSpeed ?? ActorProfile(s, actorId)?.BaseSpeed ?? Config.Scope.BasePlayerSpeed;
        }

        // ───────────── 一次初始化（R08 §2、A11 §5.2/§6） ─────────────

        /// <summary>新角色个人状态：通用招募初值（H20 上限20、N85/W85/E18/S90/M70/I70/V0、未感染，无饱饱输入），15 门技能。</summary>
        private ActorState NewRecruitActor(string actorId, string name, IDictionary<string, int>? skills, int defaultLevel)
        {
            var r = Config.Party.RecruitInitialStats;
            var a = new ActorState
            {
                Id = actorId,
                Name = name,
                Stats = new Stats
                {
                    Health = r.Health, HealthMax = r.HealthMax, Satiety = r.Satiety, Hydration = r.Hydration,
                    Energy = r.Energy, EnergyMax = Config.StateScale.EnergyMax, Wakefulness = r.Wakefulness, Morale = r.Morale,
                },
                Body = new BodyState { Immunity = r.Immunity, Infected = false, VirusLoad = r.VirusLoad },
            };
            foreach (var st in Stats.All) { a.AlertLatched[st] = false; a.ZeroLatched[st] = false; }
            // 未列出的技能 defaultLevel 级、级内经验 0；列出等级的经验置该级起点（级内 0，开发假设 B8-3）
            foreach (var sk in Config.Skills)
                a.Skills[sk.Id] = new SkillState { Level = skills != null && skills.TryGetValue(sk.Id, out var lv) ? lv : defaultLevel, Xp = 0 };
            return a;
        }

        /// <summary>给角色在指定武器槽生成一件首次装备（白色 0 词条：没有写品质概率的来源，Q44）。</summary>
        private ItemInstance GrantWorn(GameState s, string actorId, string slot, string itemId, string grantEventId)
        {
            var it = CreateItems(s, itemId, 1, ItemInstance.EquipOwner(actorId, slot), false, new ItemSource { GrantEventId = grantEventId }).Single();
            it.PlacedOrder = null;
            return it;
        }

        /// <summary>
        /// 把一名命名角色登记入名册（A11 §6/§7 RN01—RN06 的奖励；本批只经开发入口调用）：首次一次初始化个人状态与两件武器，进候补。
        /// 已在名册（含已死亡）时不重发、不重抽，返回失败原因。0 时间。
        /// </summary>
        public ActionResult AddNamedToRoster(GameState s, string actorId)
        {
            var cfg = Config.Party.Named(actorId);
            if (cfg == null) return ActionResult.Fail("没有这名角色");
            if (RosterOf(s, actorId) is RosterEntry old) return ActionResult.Fail(old.Dead ? $"{old.Name}已经死亡，命名角色不会复活" : $"{old.Name}已在名册里");
            if (s.Progress.Dead) return ActionResult.Fail("本局已结束");
            var actor = NewRecruitActor(actorId, cfg.Name, cfg.Skills, 1);
            s.Actors[actorId] = actor;
            GrantWorn(s, actorId, EquipSlots.Weapon1, cfg.Weapon1, "recruit:" + actorId);
            GrantWorn(s, actorId, EquipSlots.Weapon2, cfg.Weapon2, "recruit:" + actorId);
            s.Roster.Add(new RosterEntry { ActorId = actorId, Kind = "named", Name = cfg.Name, JoinedMinute = s.GameMinute });
            Emit(s, GameEventKind.Info, $"{cfg.Name}登记入名册（候补，在{LocName(HubLocation)}留守）。");
            return ActionResult.Success();
        }

        // ───────────── 编队（R08 §1、A11 §5.1、待确认 Q93 选 A） ─────────────

        /// <summary>现在能不能换人或重排：营区（替身）安全空闲——不在战斗/敌踪、没有主/子行动、没有未读对白、未终局。</summary>
        public string? PartyEditBlocked(GameState s)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (InCombatFlow(s)) return "战斗中不能编队";
            if (s.Main != null || s.Sub != null) return "正在行动，不能编队";
            if (DialogueLocked(s)) return "先读完对话";
            if (!AtHub(s)) return $"只能在{LocName(HubLocation)}（营区）安全空闲时编队";
            return null;
        }

        /// <summary>把候补编入同行，或把同行者留作候补的只读预演；null 为可以。</summary>
        public string? SetActiveBlocked(GameState s, string actorId, bool active)
        {
            if (actorId == GameState.PlayerId) return active ? "你总在队伍里" : "主角必须在队伍里，不能留作候补";
            var r = RosterOf(s, actorId);
            if (r == null) return "名册里没有这名角色";
            if (r.Dead) return $"{r.Name}已经死亡";
            var why = PartyEditBlocked(s);
            if (why != null) return why;
            var inParty = s.Party.Contains(actorId);
            if (active && inParty) return $"{r.Name}已在同行队伍里";
            if (!active && !inParty) return $"{r.Name}已是候补";
            var cap = PartyCapacity(s);
            if (active && s.Party.Count >= cap) return $"同行已满 {s.Party.Count}/{cap}（含你；容量 C{cap}）";
            return null;
        }

        /// <summary>
        /// 编入同行或留作候补（0 时间、不推进世界、不结算身体、不重发装备，R08 §3/§5、NP-A12）。编入时接在预设末尾，留下时从预设移除。
        /// </summary>
        public ActionResult SetActive(GameState s, string actorId, bool active, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = SetActiveBlocked(s, actorId, active);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            ApplySetActive(s, actorId, active);
            var name = RosterOf(s, actorId)!.Name;
            Emit(s, GameEventKind.Info, active ? $"{name}编入同行队伍。" : $"{name}留在{LocName(HubLocation)}作候补（公共照护：饱食/水分不低于{Fmt.T(Config.Party.ReserveCare.SatietyFloor)}，伤病照常）。");
            return ActionResult.Success();
        }

        private void ApplySetActive(GameState s, string actorId, bool active)
        {
            if (active)
            {
                if (!s.Party.Contains(actorId)) s.Party.Add(actorId);
                if (!s.FormationPreset.Contains(actorId)) s.FormationPreset.Add(actorId);
            }
            else
            {
                s.Party.Remove(actorId);
                s.FormationPreset.Remove(actorId);
            }
            NormalizeFormation(s);
        }

        /// <summary>预设与 Party 保持同一组人：去掉不在队的、补上缺的（接在末尾），主角恒在。</summary>
        public void NormalizeFormation(GameState s)
        {
            s.FormationPreset.RemoveAll(id => !s.Party.Contains(id));
            foreach (var id in s.Party) if (!s.FormationPreset.Contains(id)) s.FormationPreset.Add(id);
            s.FormationPreset = s.FormationPreset.Distinct().ToList();
        }

        public string? SetFormationBlocked(GameState s, IList<string> order)
        {
            var why = PartyEditBlocked(s);
            if (why != null) return why;
            if (order.Count != s.Party.Count || order.Distinct().Count() != order.Count || order.Any(id => !s.Party.Contains(id)))
                return "号位顺序必须正好是同行队伍里的每个人各一次";
            if (order.Count > Config.Scope.Slots.Count) return "超过四个号位";
            return null;
        }

        /// <summary>设置战前编成预设（槽序 1..n 紧凑；R03 §1 战前重排、Q80“编成预设与场内真实位置分离”）。0 时间。</summary>
        public ActionResult SetFormation(GameState s, IList<string> order, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = SetFormationBlocked(s, order);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            s.FormationPreset = order.ToList();
            Emit(s, GameEventKind.Info, "战前号位：" + string.Join("，", order.Select((id, k) => $"{k + 1}号位{ActorDisplayName(s, id)}")) + "。");
            return ActionResult.Success();
        }

        // ───────────── 开发入口（B8-19；不改正式条件函数） ─────────────

        /// <summary>开发入口：命名角色直接登记入名册；同行未满 C 时直接编入（跳过营区限制）。返回说明。</summary>
        public string DebugRecruit(GameState s, string actorId)
        {
            var res = AddNamedToRoster(s, actorId);
            if (!res.Ok) return res.Reason;
            var name = RosterOf(s, actorId)!.Name;
            if (s.Party.Count < PartyCapacity(s))
            {
                ApplySetActive(s, actorId, true);
                return $"{name}已入名册并编入同行（{s.Party.Count}/{PartyCapacity(s)}）";
            }
            return $"{name}已入名册，同行已满 {s.Party.Count}/{PartyCapacity(s)}，留作候补";
        }

        /// <summary>开发入口：在队/候补切换（跳过营区与行动限制，仍守容量、主角与死亡规则）。</summary>
        public string DebugToggleActive(GameState s, string actorId)
        {
            var r = RosterOf(s, actorId);
            if (r == null || r.Dead) return "不能切换";
            var active = !s.Party.Contains(actorId);
            if (active && s.Party.Count >= PartyCapacity(s)) return $"同行已满 {s.Party.Count}/{PartyCapacity(s)}";
            ApplySetActive(s, actorId, active);
            return active ? $"{r.Name}编入同行" : $"{r.Name}设为候补";
        }
    }
}

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
        public ActionResult AddNamedToRoster(GameState s, string actorId, bool announce = true)
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
            if (announce) Emit(s, GameEventKind.Info, $"{cfg.Name}登记入名册（候补，在{LocName(HubLocation)}留守）。");
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

        /// <summary>
        /// 编入同行或留作候补后同步编队预设（Q86 本轮章）：新成员在同一换队事务里补到最前空槽；离开的只清自己的格，其余槽与空位不动；
        /// 阵容版本 +1。不增加 C、不改角色状态或物品。
        /// </summary>
        private void ApplySetActive(GameState s, string actorId, bool active)
        {
            if (active)
            {
                if (!s.Party.Contains(actorId)) s.Party.Add(actorId);
            }
            else s.Party.Remove(actorId);
            RepairFormation(s);
            s.FormationVersion++;
        }

        // ───────────── 战前编队（Q86：R08/S14/S01/R03 本轮章「顶栏编队与真实开战预设」） ─────────────

        /// <summary>编队格数（真实 1—4 槽）。</summary>
        public int FormationSlotCount => Config.Scope.Slots.Count;

        /// <summary>某人在编队预设里的号位（1—4）；不在为 0。</summary>
        public static int FormationSlotOf(GameState s, string actorId)
        {
            var i = s.FormationPreset.IndexOf(actorId);
            return i >= 0 ? i + 1 : 0;
        }

        /// <summary>
        /// 把一份预设整理成合法的 4 格（不改输入）：去掉越界、空 ID、不在当前同行（含已死/离队）与重复的成员（重复的保留最前那格），
        /// 合法唯一的槽原样保留；没有槽的同行者（含缺失的主角）按同行顺序依次补到最前空槽，绝不挤走已有合法成员；不压紧、不按武器改位。
        /// </summary>
        public List<string> RepairedFormation(GameState s, IList<string>? preset) => RepairedFormation(s, preset, FormationSlotCount);

        /// <summary>同上（存档迁移用的静态版本，n 为格数）。</summary>
        public static List<string> RepairedFormation(GameState s, IList<string>? preset, int n)
        {
            var slots = Enumerable.Repeat("", n).ToList();
            var alive = s.Party.Distinct().Where(id => id == GameState.PlayerId || (s.Actors.ContainsKey(id) && s.Roster.FirstOrDefault(r => r.ActorId == id)?.Dead != true)).ToList();
            if (preset != null)
                for (var k = 0; k < Math.Min(n, preset.Count); k++)
                {
                    var id = preset[k];
                    if (string.IsNullOrEmpty(id) || !alive.Contains(id) || slots.Contains(id)) continue;
                    slots[k] = id;
                }
            foreach (var id in alive)
            {
                if (slots.Contains(id)) continue;
                var free = slots.IndexOf("");
                if (free < 0) break;
                slots[free] = id;
            }
            return slots;
        }

        /// <summary>把存档里的预设整理合法（成员变更、迁移与读档后调用）；返回是否改动。</summary>
        public bool RepairFormation(GameState s)
        {
            var fixedSlots = RepairedFormation(s, s.FormationPreset);
            if (s.FormationPreset.SequenceEqual(fixedSlots)) return false;
            s.FormationPreset = fixedSlots;
            return true;
        }

        /// <summary>
        /// 空白操作时期（Q86 R08/S14 本轮章）：主角存活、无战斗、无主行动（含暂停/等待继续，移动也是主行动）、无子行动、无待处理敌踪、
        /// 无未读强制对白、无旧档强制整理（同类双武器、超五卡）；只有后台设施加工不算。不限地点。null 为可以编队。
        /// </summary>
        public string? FormationEditBlocked(GameState s)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (s.Battle != null) return "战斗中不能编队（战斗里改站位用“调整站位”）";
            if (s.Encounter != null) return "先处理眼前的敌踪（接敌或绕行）";
            if (s.Main != null)
            {
                if (s.Main.Type == ActionType.Travel) return "移动中不能编队";
                return s.Main.Status == ActionStatus.Paused ? $"{MainActionName(s.Main.Type)}已暂停：先继续或结束它再编队" : $"正在{MainActionName(s.Main.Type)}，结束后再编队";
            }
            if (s.Sub != null) return $"正在{MainActionName(s.Sub.Type)}，结束后再编队";
            if (DialogueLocked(s)) return "先读完对话";
            if (DuplicateWeaponsIllegal(s)) return "先整理身上的同类型武器";
            if (CarriedOverflow(s)) return "先整理超出五张卡的随身物品";
            return null;
        }

        /// <summary>
        /// 保存编队的只读预演（提交时同一函数重验）：空闲；草稿基于的阵容版本未变；正好 4 格；每名存活当前同行者恰一格、每格至多一人、
        /// 主角必在、不含同行以外的人；与当前预设相同为“没有改动”。C 只限制同行人数，不限制可选号位。null 为可以保存。
        /// </summary>
        public string? SaveFormationBlocked(GameState s, IList<string> slots, int baseVersion)
        {
            var why = FormationEditBlocked(s);
            if (why != null) return why;
            if (baseVersion != s.FormationVersion) return "队伍刚有变化，这份编队草稿已过期：请关闭后按最新队伍重新编排";
            if (slots.Count != FormationSlotCount) return $"编队必须是{FormationSlotCount}个号位";
            var members = slots.Where(id => !string.IsNullOrEmpty(id)).ToList();
            if (members.Distinct().Count() != members.Count) return "每人只能占一个号位";
            if (!members.Contains(GameState.PlayerId)) return "你必须在编队里";
            var party = RepairedFormation(s, s.FormationPreset).Where(id => id != "").ToList();
            if (members.Any(id => !party.Contains(id))) return "编队里有不在当前同行队伍里的人";
            if (party.Any(id => !members.Contains(id))) return "每名当前同行者都要有一个号位";
            if (slots.Select(x => x ?? "").SequenceEqual(s.FormationPreset)) return "编队没有改动";
            return null;
        }

        /// <summary>
        /// 保存编队（Q86）：原子写入 4 格预设并把阵容版本 +1；0 世界分钟、0AP、0E、0 经验，不治疗、不恢复状态、不重抽、不动工作余额/队列/设施计时。
        /// 重复请求幂等；任何条件不满足整份不变。下一场战斗按它开局，战斗中的站位变化不反写。
        /// </summary>
        public ActionResult SaveFormation(GameState s, IList<string> slots, int baseVersion, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = SaveFormationBlocked(s, slots, baseVersion);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            s.FormationPreset = slots.Select(x => x ?? "").ToList();
            s.FormationVersion++;
            Emit(s, GameEventKind.Info, "编队已保存：" + FormationText(s, s.FormationPreset) + "（下一场战斗按此开局）。");
            return ActionResult.Success();
        }

        /// <summary>“1号位你，2号位空，3号位唐梨，4号位空”。</summary>
        public string FormationText(GameState s, IList<string> slots) =>
            string.Join("，", slots.Select((id, k) => $"{k + 1}号位{(string.IsNullOrEmpty(id) ? "空" : ActorDisplayName(s, id))}"));

        /// <summary>
        /// 某人开战时停在的武器页上的武器与合法使用位（Q86 编队面板参考，不自动更正位置）：武器Ⅰ有武器取Ⅰ，否则Ⅱ，双空为拳。
        /// </summary>
        public (WeaponConfig weapon, HashSet<int> own) StartWeaponOf(GameState s, string actorId)
        {
            foreach (var page in new[] { 1, 2 })
            {
                var slot = EquipSlots.WeaponSlotOfPage(page);
                var it = slot != null ? WornItem(s, actorId, slot) : null;
                if (it != null && _weapons.TryGetValue(it.ItemId, out var w)) return (w, OwnPositionsOf(w));
            }
            var fists = _weapons[Fists];
            return (fists, OwnPositionsOf(fists));
        }

        /// <summary>开局号位：按已保存的合法预设（整理后），主角所在格；单人也生效。</summary>
        public int PresetPlayerSlot(GameState s)
        {
            var slot = RepairedFormation(s, s.FormationPreset).IndexOf(GameState.PlayerId) + 1;
            return slot <= 0 ? 1 : slot;
        }

        // ───────────── 候补公共照护（R08 §3、A11 §5.3；M184） ─────────────

        /// <summary>本段要按公共照护结算的候补（存活、在名册、不在同行）。</summary>
        private List<ActorState> ReserveActors(GameState s) => Reserves(s).Select(r => s.Actors[r.ActorId]).ToList();

        /// <summary>候补此刻（日内分钟）是否在遮蔽睡眠时段（22:00—06:00）。</summary>
        public bool ReserveSleeping(int minute)
        {
            var care = Config.Party.ReserveCare;
            var m = ((minute % Clock.MinutesPerDay) + Clock.MinutesPerDay) % Clock.MinutesPerDay;
            return care.SleepStartMinute > care.SleepEndMinute
                ? m >= care.SleepStartMinute || m < care.SleepEndMinute
                : m >= care.SleepStartMinute && m < care.SleepEndMinute;
        }

        /// <summary>
        /// 候补本段速率：营区强制遮蔽、无火堆与床垫加成；22—06 按遮蔽睡眠，其余按 wait（待确认 Q90 选 A；配置 awakeAction=rest 时按休息）。
        /// 装备求生效果、低精神折半、腹泻、归零伤害与自然恢复条件同主行动（S13 现有速率）。
        /// </summary>
        private SegmentContext ReserveSegment(GameState s, ActorState actor)
        {
            var c = Config;
            var st = actor.Stats;
            var sleep = ReserveSleeping(s.GameMinute);
            var type = sleep ? ActionType.Sleep : c.Party.ReserveCare.AwakeAction == "rest" ? ActionType.Rest : ActionType.Wait;
            var a = new ActionState { Type = type, TargetId = "" };
            var ctx = new SegmentContext { Sheltered = true, IsWork = false, ActionType = type };
            string key;
            if (sleep) key = "sleep_sheltered";
            else
            {
                var raw = RateKey(type);
                key = c.Actions.RateAliases.TryGetValue(raw, out var alias) ? alias : raw;
            }
            ctx.Rates = c.HourlyRates[key].Copy();
            ApplyGearRates(s, actor, a, ctx);
            if ((type == ActionType.Rest || type == ActionType.Sleep) && ctx.Rates.Energy > 0 && c.Thresholds["morale"].IsBelow(st.Morale))
                ctx.Rates.Energy *= c.FacilityEffects.LowMoraleEnergyRecoveryFactor;
            if (actor.DiarrheaUntil.HasValue && actor.DiarrheaUntil.Value > s.GameMinute)
                ctx.Rates.Hydration += c.ItemActions.DiarrheaExtraHydrationPerHour;
            var hr = c.HealthRules;
            var causes = new List<string>();
            double health = 0;
            if (st.Satiety <= 0) { health += hr.ZeroSatietyPerHour; causes.Add("饥饿"); }
            if (st.Hydration <= 0) { health += hr.ZeroHydrationPerHour; causes.Add("缺水"); }
            if (st.Wakefulness <= 0) { health += hr.ZeroWakefulnessPerHour; causes.Add("缺乏睡眠"); }
            if (causes.Count > 0) ctx.DamageCause = causes[0];
            if ((type == ActionType.Rest || type == ActionType.Sleep)
                && st.Satiety >= hr.RecoveryMinSatiety && st.Hydration >= hr.RecoveryMinHydration && st.Morale >= hr.RecoveryMinMorale)
                health += hr.ShelteredRestRecoveryPerHour;
            ctx.HealthPerHour = health;
            return ctx;
        }

        /// <summary>N/W 托底：低于 40 只补到 40，高于 40 保持（R08 §3）。</summary>
        private void ReserveFloor(ActorState actor)
        {
            var care = Config.Party.ReserveCare;
            if (actor.Stats.Satiety < care.SatietyFloor) actor.Stats.Satiety = care.SatietyFloor;
            if (actor.Stats.Hydration < care.HydrationFloor) actor.Stats.Hydration = care.HydrationFloor;
        }

        /// <summary>候补红线（A11 §5.3、NP-A21、TX06）：H 首次 ≤5、V 首次 ≥80 各提示一次；离开阈值后清锁存，可再提示。不暂停主行动。</summary>
        private void ReserveRedLines(GameState s, ActorState actor)
        {
            var care = Config.Party.ReserveCare;
            void Line(string key, bool on, string text)
            {
                actor.RedLatched.TryGetValue(key, out var latched);
                if (on && !latched)
                {
                    actor.RedLatched[key] = true;
                    Emit(s, GameEventKind.Alert, text);
                }
                else if (!on && latched) actor.RedLatched[key] = false;
            }
            var name = ActorLabel(actor);
            Line("health", actor.Stats.Health <= care.RedHealth + 1e-9, $"候补{name}健康降到{Fmt.T(actor.Stats.Health)}（≤{Fmt.T(care.RedHealth)}），留在{LocName(HubLocation)}，需要你回去救治。");
            Line("virus", actor.Body.VirusLoad >= care.RedVirus - 1e-9, $"候补{name}病毒程度到{Fmt.T(actor.Body.VirusLoad)}（≥{Fmt.T(care.RedVirus)}），公共照护不治病，需要你回去处理。");
        }

        // ───────────── 开发入口（B8-19；不改正式条件函数） ─────────────

        /// <summary>开发入口：命名角色直接登记入名册；同行未满 C 时直接编入（跳过营区限制）。返回说明。</summary>
        public string DebugRecruit(GameState s, string actorId)
        {
            // 日志按实际去处写一条（试玩修复 M191：编入同行时不再写“候补留守”）
            var res = AddNamedToRoster(s, actorId, false);
            if (!res.Ok) return res.Reason;
            var name = RosterOf(s, actorId)!.Name;
            string text;
            if (s.Party.Count < PartyCapacity(s))
            {
                ApplySetActive(s, actorId, true);
                text = $"{name}登记入名册并编入同行（{s.Party.Count}/{PartyCapacity(s)}）";
            }
            else text = $"{name}登记入名册，同行已满 {s.Party.Count}/{PartyCapacity(s)}，留作候补（在{LocName(HubLocation)}）";
            Emit(s, GameEventKind.Info, text + "。");
            return text;
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

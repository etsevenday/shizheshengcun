using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    public sealed partial class GameEngine
    {
        /// <summary>一个 5 分钟段开始时确定的全部变化率（每小时）。</summary>
        public sealed class SegmentContext
        {
            public RateBlock Rates = new RateBlock();
            public double HealthPerHour;
            public double Factor = 1;
            public bool IsWork;
            public bool Sheltered;
            public string DamageCause = "";
            public List<string> FactorReasons = new List<string>();
            /// <summary>本段角色在做什么（免疫的睡眠/休息加成按它判断）；战斗与无行动为 null。</summary>
            public ActionType? ActionType;
        }

        private static string RateKey(ActionType t) => t switch
        {
            ActionType.Travel => "travel",
            ActionType.Explore => "explore",
            ActionType.Gather => "gather",
            ActionType.Clear => "clear",
            ActionType.Craft => "craft",
            ActionType.Build => "build",
            ActionType.Ignite => "ignite",
            ActionType.Use => "use",
            ActionType.Rest => "rest",
            ActionType.Butcher => "butcher",
            ActionType.Repair => "repair",
            ActionType.Salvage => "salvage",
            ActionType.Bypass => "bypass",
            ActionType.Survey => "gather",   // 遗迹勘整按正常搜集身体费率（Q55）
            _ => "wait",
        };

        /// <summary>按段首状态计算主角本段速率、工作倍率、归零伤害与自然恢复（求生案 01/02/04/05）。</summary>
        public SegmentContext ComputeSegment(GameState s, ActionState a) => ComputeSegment(s, a, s.Player);

        /// <summary>按某角色的段首状态计算本段速率（R08 §2：各角色身体各自结算；同行者的行动类型见 CompanionAction）。</summary>
        public SegmentContext ComputeSegment(GameState s, ActionState a, ActorState actor)
        {
            var ctx = new SegmentContext();
            var st = actor.Stats;
            var loc = s.CurrentLocation;
            var locCfg = _locs[loc];
            var fac = s.Locations[loc].Facility;
            var c = Config;

            ctx.Sheltered = !a.ForceOutdoor && IsSheltered(s, loc);
            ctx.IsWork = a.IsWorkType;
            ctx.ActionType = a.Type;

            string key;
            if (a.Type == ActionType.Sleep) key = ctx.Sheltered ? "sleep_sheltered" : "sleep_exposed";
            else
            {
                var raw = RateKey(a.Type);
                key = c.Actions.RateAliases.TryGetValue(raw, out var alias) ? alias : raw;
            }
            ctx.Rates = c.HourlyRates[key].Copy();

            if (a.Type == ActionType.Rest && !a.ForceOutdoor && locCfg.CanBuildShelter && fac.ShelterBuilt)
                ctx.Rates.Morale += c.FacilityEffects.ShelteredCampRestMoralePerHour;
            if (a.Type == ActionType.Sleep && ctx.Sheltered && fac.BedBuilt)
                ctx.Rates.Energy += c.FacilityEffects.BedExtraSleepEnergyPerHour;
            // 装备求生效果（R16、A05 §2/§4，B4 H3）：负小时率缩放（下限×0.75），正恢复先加算，之后才受精神低于阈值折半
            ApplyGearRates(s, actor, a, ctx);
            if ((a.Type == ActionType.Rest || a.Type == ActionType.Sleep) && ctx.Rates.Energy > 0
                && c.Thresholds["morale"].IsBelow(st.Morale))
                ctx.Rates.Energy *= c.FacilityEffects.LowMoraleEnergyRecoveryFactor;
            if (actor.DiarrheaUntil.HasValue && actor.DiarrheaUntil.Value > s.GameMinute)
                ctx.Rates.Hydration += c.ItemActions.DiarrheaExtraHydrationPerHour;

            // 归零伤害：段首为0才计（求生案 02 统一伤害计算）
            var hr = c.HealthRules;
            var causes = new List<string>();
            double health = 0;
            if (st.Satiety <= 0) { health += hr.ZeroSatietyPerHour; causes.Add("饥饿"); }
            if (st.Hydration <= 0) { health += hr.ZeroHydrationPerHour; causes.Add("缺水"); }
            if (st.Wakefulness <= 0) { health += hr.ZeroWakefulnessPerHour; causes.Add("缺乏睡眠"); }
            if (st.Energy <= 0 && ctx.IsWork) { health += hr.ZeroEnergyWhileWorkingPerHour; causes.Add("体力透支"); }
            if (causes.Count > 0) ctx.DamageCause = causes[0];

            // 自然恢复：休息或睡眠、遮蔽、段首饱食与水分≥40、精神≥20（求生案 04）
            if ((a.Type == ActionType.Rest || a.Type == ActionType.Sleep) && ctx.Sheltered
                && st.Satiety >= hr.RecoveryMinSatiety && st.Hydration >= hr.RecoveryMinHydration
                && st.Morale >= hr.RecoveryMinMorale)
                health += hr.ShelteredRestRecoveryPerHour;
            ctx.HealthPerHour = health;

            if (ctx.IsWork)
            {
                // Q61：新规则装备/节点倍率已在开工时乘进需求工作量，段内只按 S01 倍率 M 推进；旧在途动作照旧逐段乘装备倍率
                ctx.Factor = a.RuleVersion >= WorkRuleVersion
                    ? TimeFactor(s, actor, a.Type, ctx.FactorReasons)
                    : WorkFactor(s, actor, a.Type, ctx.FactorReasons, a.TargetId);
            }
            return ctx;
        }

        /// <summary>
        /// 执行者以外的同行者本段按什么行动计速率（R08 §2“S01统一一名执行者，其他同行wait，移动全员travel，休息睡眠同计划各自条件”）。
        /// </summary>
        public static ActionState CompanionAction(ActionState a)
        {
            var c = a.Clone();
            if (a.Type != ActionType.Travel && a.Type != ActionType.Rest && a.Type != ActionType.Sleep) c.Type = ActionType.Wait;
            return c;
        }

        /// <summary>M = min(上限, 夜间×负重×低体力)（主角）。</summary>
        public double WorkFactor(GameState s, ActionType type, List<string>? reasons = null, string? targetId = null) => WorkFactor(s, s.Player, type, reasons, targetId);

        /// <summary>
        /// M = min(上限, 夜间×负重×低体力)。移动始终视为无照明；其他工作在火堆燃烧的地点不吃夜间系数。负重与体力按该角色本人。
        /// 再乘装备的求生工作量倍率（B4：A05 §4 末段“求生同类工作量合成下限×0.70，后乘现行S01工作倍率”，等价于基础工作量先乘装备倍率）。
        /// </summary>
        public double WorkFactor(GameState s, ActorState actor, ActionType type, List<string>? reasons = null, string? targetId = null) =>
            TimeFactor(s, actor, type, reasons) * GearWorkFactor(s, actor.Id, type, targetId, reasons);

        /// <summary>
        /// S01 时间倍率 M = min(上限, 夜间×负重×低体力)（不含装备与熟练节点的工作量倍率）。
        /// 多人移动（S01 多角色段、TX01，B8）：主角带队移动时取同行者里最大（最慢）的倍率，原因写最慢的人。
        /// </summary>
        public double TimeFactor(GameState s, ActorState actor, ActionType type, List<string>? reasons = null)
        {
            if (type == ActionType.Travel && actor.IsPlayer && s.Party.Count > 1)
            {
                var best = actor;
                var bestF = TimeFactorOf(s, actor, type, null);
                foreach (var other in s.PartyActors.Where(a => !a.IsPlayer))
                {
                    var f = TimeFactorOf(s, other, type, null);
                    if (f > bestF + 1e-12) { best = other; bestF = f; }
                }
                var own = new List<string>();
                var res = TimeFactorOf(s, best, type, own);
                if (reasons != null)
                {
                    if (!best.IsPlayer) reasons.Add($"按最慢的同行者{ActorLabel(best)}");
                    reasons.AddRange(own);
                }
                return res;
            }
            return TimeFactorOf(s, actor, type, reasons);
        }

        private double TimeFactorOf(GameState s, ActorState actor, ActionType type, List<string>? reasons)
        {
            var c = Config.WorkFactors;
            double f = 1;
            var fac = s.Locations[s.CurrentLocation].Facility;
            var night = !IsDaylight(s.GameMinute);
            var lit = type != ActionType.Travel && fac.FireLit;
            if (night && !lit)
            {
                f *= c.Night;
                reasons?.Add($"夜间无照明 ×{c.Night}");
            }
            if (CarriedGramsOf(s, actor.Id) > Config.Carry.SoftGrams)
            {
                f *= c.Overweight;
                reasons?.Add($"负重超过{Config.Carry.SoftGrams / 1000}公斤 ×{c.Overweight}");
            }
            if (Config.Thresholds["energy"].IsBelow(actor.Stats.Energy))
            {
                f *= c.LowEnergy;
                reasons?.Add($"体力低于{Fmt.T(Config.Thresholds["energy"].Value)} ×{c.LowEnergy}");
            }
            if (f > c.Cap)
            {
                f = c.Cap;
                reasons?.Add($"倍率封顶 {c.Cap}");
            }
            return f;
        }

        /// <summary>
        /// 推进一个 5 分钟段（求生案 06 每个5分钟段）：先做一次世界 tick（WorldTick），再累积主行动进度与提交结果。
        /// 战斗中与敌踪待选时不经这里推进，战斗每轮结束自己调用一次 WorldTick（v0.6 §07、§16）。
        /// </summary>
        public SegmentResult Advance(GameState s)
        {
            if (s.Progress.Dead) return new SegmentResult(SegmentStatus.Dead);
            // 对白安全边界（S01 §8）：上一段已完整提交并存档；有未读对话时本段不开始，原行动/进度/预留原样保留
            if (DialogueLocked(s)) return new SegmentResult(SegmentStatus.Dialogue);
            if (InCombatFlow(s)) return new SegmentResult(SegmentStatus.Idle);
            var a = s.Sub ?? s.Main;
            if (a == null) return new SegmentResult(SegmentStatus.Idle);
            if (!a.IsSub && a.Status == ActionStatus.Paused) return new SegmentResult(SegmentStatus.Paused);

            // 0. 段前持续条件：原料腐败（烧水、烤制、水煮已是设施自动进程，熄火不再暂停角色行动）
            if ((a.Type == ActionType.Craft || a.Type == ActionType.Build || a.Type == ActionType.Butcher) && AnyReservedSpoiled(s, a))
            {
                AbortSpoiled(s, a);
                return new SegmentResult(SegmentStatus.Aborted);
            }
            // 等待烹饪：火堆不能给这一段完整供热就停止，不 tick（S11 §9、开发假设 A8）
            if (CookWaitStoppedBeforeSegment(s, a)) return new SegmentResult(SegmentStatus.Aborted);

            // 1–3. 段首计算角色本段速率，再做一次世界 tick（身体、燃料、设施、免疫、病毒、终局、刷新）
            var ctx = ComputeSegment(s, a);
            var companion = CompanionAction(a);
            if (!WorldTick(s, actor => actor.IsPlayer ? ctx : ComputeSegment(s, companion, actor), a, false)) return new SegmentResult(SegmentStatus.Dead);

            // 4. 累积进度
            bool done;
            SegmentStatus? cookEnded = null;
            if (a.IsWorkType)
            {
                a.WorkDone += Tick / ctx.Factor;
                a.ElapsedMinutes += Tick;
                // Q61：原锁余额 C + 本次已做工作 ≥ 需求工作量即完成（旧在途动作 C=0）；每次至少推进一个真实 5 分钟段
                done = a.CarryReserved + a.WorkDone >= a.BaseWorkMinutes - Eps;
            }
            else if (a.Type == ActionType.WaitCook)
            {
                a.ElapsedMinutes += Tick;
                if (CookWaitAfterSegment(s, a, out var cooked)) cookEnded = cooked ? SegmentStatus.Completed : SegmentStatus.Aborted;
                done = cookEnded != null;
            }
            else
            {
                a.ElapsedMinutes += Tick;
                done = a.ElapsedMinutes >= a.PlannedMinutes;
            }
            if (a.Type == ActionType.Sleep) s.Progress.Flags.Add("sleptOnce");

            var status = SegmentStatus.Running;
            if (cookEnded != null) status = cookEnded.Value;
            else if (done)
            {
                // 5. 提交结果（含末尾环境伤害）；探索/清理先提交原产物，再按密度检查随机接敌（v0.6 §15）
                var ok = Complete(s, a);
                if (ok) SettleCarry(s, a); else RestoreCarry(s, a);
                if (s.Progress.Dead) return new SegmentResult(SegmentStatus.Dead);
                status = ok ? SegmentStatus.Completed : SegmentStatus.Aborted;
                if (ok) CheckRandomEncounter(s, a);
            }

            // 6. 阈值提醒
            if (CheckAlerts(s, a, done) && status == SegmentStatus.Running)
                status = SegmentStatus.Paused;

            UpdateFlags(s);
            CheckSevenDay(s);
            return new SegmentResult(status);
        }

        /// <summary>
        /// 单段世界 tick（v0.6 §16 建议顺序）：段首快照 → 身体/燃料/药物时间/免疫/病毒 → 终局判定 → 自然治愈 →
        /// 清理及资源恢复 → 设施加工提交（死亡先于本段未提交产物）→ 敌人刷新/巡游。主行动推进与战斗轮末都调用它，每段只结算一次。
        /// 多角色（R08 §2、COMP04，开发计划 v0.9 B3 H10）：世界部分（时间、燃料、设施、清理、刷新）每段只推进一次；
        /// 身体部分对 Party 每人各结算一次（ApplyRates/TickBody/终局/自然治愈），速率由 ctxOf 按各自段首状态给出。只有主角时与单人版逐段一致。
        /// 返回 false 表示本段主角死亡或尸变（已锁局）。
        /// </summary>
        private bool WorldTick(GameState s, Func<ActorState, SegmentContext> ctxOf, ActionState? running, bool battle)
        {
            var party = s.PartyActors.ToList();
            var ctxs = party.Select(ctxOf).ToList();
            // 候补（B8，R08 §3、A11 §5.3）：同一世界 tick 里按营区公共照护各结算一次（战斗轮末也一样，不吃战斗 N/W 冻结）
            var reserves = ReserveActors(s);
            var rctxs = reserves.Select(a => ReserveSegment(s, a)).ToList();
            // 段首：火堆加工资格与身体快照（免疫、病毒按段首状态计算）
            SyncFacilities(s);
            var snap = SnapshotFacilities(s, running);
            var starts = party.Select(actor => TickStart.Capture(s, actor)).ToList();
            var rstarts = reserves.Select(actor => TickStart.Capture(s, actor)).ToList();

            // 身体变化（每小时速率 × 5/60），按项上限夹紧
            for (var k = 0; k < party.Count; k++) ApplyRates(s, party[k], ctxs[k]);
            // 候补：先结算该段自然消耗，再把 N/W 托到不低于 40（高于 40 不提高，不生成任何物品）
            for (var k = 0; k < reserves.Count; k++)
            {
                ApplyRates(s, reserves[k], rctxs[k]);
                ReserveFloor(reserves[k]);
            }

            s.GameMinute += Tick;
            s.SegmentCount++;
            BurnFires(s);
            for (var k = 0; k < party.Count; k++) TickBody(s, party[k], starts[k], ctxs[k], battle);
            for (var k = 0; k < reserves.Count; k++) TickBody(s, reserves[k], rstarts[k], rctxs[k], false);

            // 终局：健康归零先于尸变，二者都先于自然治愈与本段未提交的设施产物
            for (var k = 0; k < party.Count; k++)
            {
                var actor = party[k];
                if (actor.Stats.Health <= 0)
                {
                    if (actor.IsPlayer) { Kill(s, s.Progress.LastDamageCause); return false; }
                    CompanionDown(s, actor, string.IsNullOrEmpty(ctxs[k].DamageCause) ? "伤势过重" : ctxs[k].DamageCause);
                    continue;
                }
                if (Zombified(actor))
                {
                    if (actor.IsPlayer) { Zombify(s); return false; }
                    CompanionDown(s, actor, ZombifiedCause);
                }
            }
            for (var k = 0; k < party.Count; k++)
                if (s.Party.Contains(party[k].Id)) TickNaturalCure(s, party[k], starts[k]);
            // 候补终局（到达死亡条件立即判死，同段不靠自然恢复复活）→ 自然治愈 → 红线提示（不暂停主行动）
            for (var k = 0; k < reserves.Count; k++)
            {
                var actor = reserves[k];
                if (actor.Stats.Health <= 0) { CompanionDown(s, actor, string.IsNullOrEmpty(rctxs[k].DamageCause) ? "伤势过重" : rctxs[k].DamageCause); continue; }
                if (Zombified(actor)) { CompanionDown(s, actor, ZombifiedCause); continue; }
                TickNaturalCure(s, actor, rstarts[k]);
                ReserveRedLines(s, actor);
            }

            UpdateAllClearCharges(s);
            if (s.GameMinute % Clock.MinutesPerDay == 0) NewDay(s);

            // 设施自动加工：按段首快照给合格目标 +5 受热分钟，达标即原地转化；不替换、不暂停角色主行动
            AdvanceFacilities(s, snap);
            TickWorldEnemies(s);
            return true;
        }

        /// <summary>一个角色本段的六项状态变化（每小时速率 × 5/60，按项上限夹紧）；主角记最近伤害原因。</summary>
        private void ApplyRates(GameState s, ActorState actor, SegmentContext ctx)
        {
            var h = Tick / 60.0;
            var st = actor.Stats;
            st.Add(StatId.Satiety, ctx.Rates.Satiety * h);
            st.Add(StatId.Hydration, ctx.Rates.Hydration * h);
            st.Add(StatId.Energy, ctx.Rates.Energy * h);
            st.Add(StatId.Wakefulness, ctx.Rates.Wakefulness * h);
            st.Add(StatId.Morale, ctx.Rates.Morale * h);
            st.Add(StatId.Health, ctx.HealthPerHour * h);
            if (actor.IsPlayer && !string.IsNullOrEmpty(ctx.DamageCause)) s.Progress.LastDamageCause = ctx.DamageCause;
        }

        /// <summary>
        /// 队友（同行或候补）健康归零或尸变（R08 §4、A11 §8，B8）：永久退出可控名册（名称、技能与死亡记录保留），离开同行与预设、
        /// 战斗中清出号位（不压紧）；穿戴与随身一次原子迁到死亡地点的遗物容器（owner 唯一、保质/耐久不变）。容量 C 不降。主角不走这里。
        /// </summary>
        private void CompanionDown(GameState s, ActorState actor, string cause)
        {
            if (actor.IsPlayer) return;
            if (cause != ZombifiedCause) actor.Stats.Health = 0;
            var wasActive = s.Party.Contains(actor.Id);
            var where = wasActive ? (s.Battle?.LocationId ?? s.CurrentLocation) : HubLocation;
            s.Party.Remove(actor.Id);
            s.FormationPreset.Remove(actor.Id);
            if (s.Battle != null)
            {
                var b = s.Battle;
                for (var k = 0; k < b.Allies.Count; k++) if (b.Allies[k] == actor.Id) b.Allies[k] = "";
                while (b.Allies.Count > 0 && string.IsNullOrEmpty(b.Allies[b.Allies.Count - 1])) b.Allies.RemoveAt(b.Allies.Count - 1);
            }
            if (RosterOf(s, actor.Id) is RosterEntry r && !r.Dead)
            {
                r.Dead = true;
                r.DeathMinute = s.GameMinute;
                r.DeathLocation = where;
                r.DeathCause = cause;
            }
            var moved = MoveToRemains(s, actor, where, cause, wasActive ? s.Battle?.BattleId : null);
            Emit(s, GameEventKind.Alert, $"{ActorLabel(actor)}死了（{cause}）" + (moved > 0 ? $"，遗物留在{LocName(where)}。" : "。"));
        }

        /// <summary>角色在文案里的称呼：主角为“你”，队友为名册名（B8）；没有名字的测试注入角色为“队友&lt;id&gt;”。</summary>
        public static string ActorLabel(ActorState actor) => actor.IsPlayer ? "你" : !string.IsNullOrEmpty(actor.Name) ? actor.Name : $"队友{actor.Id}";

        /// <summary>队友提示前缀（主角为空，保持单人版文案不变）。</summary>
        private static string ActorPrefix(ActorState actor) => actor.IsPlayer ? "" : $"{ActorLabel(actor)}：";

        /// <summary>段首快照：免疫、病毒、出血按它计算（v0.6 §08“每5分钟读取段首状态”）。</summary>
        private sealed class TickStart
        {
            public int Minute;
            public Stats Stats = new Stats();
            public double Immunity;
            public bool Infected;
            public bool Diarrhea;
            public bool AntibioticActive;

            public static TickStart Capture(GameState s, ActorState actor) => new TickStart
            {
                Minute = s.GameMinute,
                Stats = actor.Stats.Clone(),
                Immunity = actor.Body.Immunity,
                Infected = actor.Body.Infected,
                Diarrhea = actor.DiarrheaUntil.HasValue && actor.DiarrheaUntil.Value > s.GameMinute,
                AntibioticActive = actor.Body.AntibioticUntil.HasValue && actor.Body.AntibioticUntil.Value > s.GameMinute,
            };
        }

        private void Pause(GameState s, ActionState a, PauseReason reason, StatId? stat, string text)
        {
            a.Status = ActionStatus.Paused;
            a.PauseReason = reason;
            a.PauseStat = stat;
            Emit(s, GameEventKind.ActionPaused, text, stat);
        }

        private void BurnFires(GameState s)
        {
            foreach (var kv in s.Locations)
            {
                var f = kv.Value.Facility;
                if (!f.FireLit) continue;
                f.FuelMinutes -= Tick;
                if (f.FuelMinutes <= 0)
                {
                    f.FuelMinutes = 0;
                    f.FireLit = false;
                    f.WentOut = true;
                    Emit(s, GameEventKind.FireOut, $"{LocName(kv.Key)}的火堆燃料耗尽，已熄灭。");
                }
            }
        }

        /// <summary>00:00 跨日：自然资源补充，补充上限按“可用+已预留”计算（搜集案 04/05）。</summary>
        private void NewDay(GameState s)
        {
            var day = s.GameMinute / Clock.MinutesPerDay;
            foreach (var r in Config.Resources)
            {
                var rs = s.Resources[r.NodeId];
                if (rs.LastRefillDay >= day) continue;
                rs.LastRefillDay = day;
                if (!r.Refills || !r.Cap.HasValue) continue;
                var avail = rs.Available ?? 0;
                rs.Available = Math.Min(r.Cap.Value - rs.Reserved, avail + r.DailyRefill);
                if (rs.Available < avail) rs.Available = avail;
            }
            Emit(s, GameEventKind.NewDay, $"第{Clock.Day(s.GameMinute)}日 00:00。自然资源已补充。");
        }

        private bool AnyReservedSpoiled(GameState s, ActionState a) =>
            a.ReservedItemIds.Any(id => s.Items.Any(i => i.InstanceId == id && IsSpoiled(s, i)));

        private void AbortSpoiled(GameState s, ActionState a)
        {
            Release(s, a, s.CurrentLocation);
            if (a.IsSub) s.Sub = null; else s.Main = null;
            Emit(s, GameEventKind.ActionAborted, $"{a.Label}中止：原料已腐败，预留物返还当前地面，未生成产物。");
        }

        /// <summary>提交行动结果。返回 false 表示在完成时刻因原料腐败而中止。</summary>
        private bool Complete(GameState s, ActionState a)
        {
            var here = s.CurrentLocation;
            switch (a.Type)
            {
                case ActionType.Travel:
                {
                    var from = s.CurrentLocation;
                    var walked = Config.Edges.FirstOrDefault(e => e.Touches(from) && e.Other(from) == a.TargetId);
                    if (walked != null) s.TraveledEdges.Add(walked.Id);
                    s.CurrentLocation = a.TargetId;
                    var revealed = Visit(s, a.TargetId);
                    ClearAction(s, a);
                    var t = $"到达{LocName(a.TargetId)}（{AwardTravelXp(s, from, a.TargetId)}）。";
                    if (revealed.Count > 0) t += $"发现{string.Join("、", revealed.Select(id => _res[id].Name))}。";
                    Emit(s, GameEventKind.ActionCompleted, t, null, revealed);
                    // 首次 camp→field：落点并提交到达后，由保留游荡尸触发一次剧情首战（S15 §5、S02 §7）
                    TryStartTutorialBattle(s, a);
                    return true;
                }
                case ActionType.Explore:
                    return CompleteExplore(s, a);
                case ActionType.Clear:
                    return CompleteClear(s, a);
                case ActionType.Gather:
                    return CompleteGather(s, a);
                case ActionType.Craft:
                case ActionType.Build:
                case ActionType.Repair:
                {
                    if (AnyReservedSpoiled(s, a))
                    {
                        AbortSpoiled(s, a);
                        return false;
                    }
                    var rc = _recipes[a.TargetId];
                    var batch = Math.Max(1, a.BatchCount);
                    foreach (var id in a.ReservedItemIds) s.Items.RemoveAll(i => i.InstanceId == id);
                    a.ReservedItemIds.Clear();
                    var made = new List<string>();
                    foreach (var kv in rc.Outputs)
                    {
                        // 制造产物按“普通制造”来源定品质（A05 §4：普通制造默认绿色1条，B4 H4）
                        var crafted = CreateItems(s, kv.Key, kv.Value * batch, here, false, null, Config.Equipment.CraftOrigin == "" ? null : Config.Equipment.CraftOrigin);
                        made.Add($"{ItemName(kv.Key)}+{kv.Value * batch}{QualitySuffix(crafted)}");
                    }
                    if (rc.IsRepair) made.Add(CompleteRepair(s, a));
                    // 有效制造按实际提交的配方次数发经验（v0.6 §03）
                    var xp = CraftXp(rc.BaseMinutes) * batch;
                    made.Add(XpText(s, Crafting, xp));
                    GainXp(s, Crafting, xp);
                    if (rc.FacilityResult != null) SetFacility(s.Locations[here].Facility, rc.FacilityResult);
                    ClearAction(s, a);
                    var text = rc.FacilityResult != null
                        ? $"{rc.Name.Replace("建", "").Replace("铺", "")}已完成（{made.Last()}）。"
                        : rc.IsRepair ? $"{string.Join("，", made)}。"
                        : $"{string.Join("，", made)}；产物已放在{LocName(here)}地面。";
                    if (rc.Id == "shelter") text = "遮蔽棚建成：营地可遮蔽睡眠，休息时额外恢复精神。";
                    if (rc.Id == "bed") text = $"床垫铺好：营地睡眠体力恢复每小时额外{Fmt.SignedT(Config.FacilityEffects.BedExtraSleepEnergyPerHour)}。";
                    Emit(s, GameEventKind.ActionCompleted, text, null, rc.Outputs.Keys);
                    return true;
                }
                case ActionType.Salvage:
                    return CompleteSalvage(s, a);
                case ActionType.Survey:
                    return CompleteSurvey(s, a);
                case ActionType.Sleep:
                    ClearAction(s, a);
                    Emit(s, GameEventKind.ActionCompleted, $"睡眠结束（{Clock.Duration(a.PlannedMinutes)}）。");
                    return true;
                case ActionType.Rest:
                    ClearAction(s, a);
                    Emit(s, GameEventKind.ActionCompleted, $"休息结束（{Clock.Duration(a.PlannedMinutes)}）。");
                    return true;
                case ActionType.Wait:
                    ClearAction(s, a);
                    if (a.TargetId == EscortRegistrationTarget) { CompleteEscortRegistration(s, a); return true; }
                    Emit(s, GameEventKind.ActionCompleted, $"等待结束（{Clock.Duration(a.PlannedMinutes)}）。");
                    return true;
                case ActionType.Butcher:
                    return CompleteButcher(s, a);
                case ActionType.Bypass:
                {
                    ClearAction(s, a);
                    // 绕行不杀死、不清空世界敌人，只暂时避开；之后60分钟内不再随机接敌（主动接敌仍可）
                    s.EncounterGraceUntil = s.GameMinute + Config.Encounters.PostExitRandomEncounterGraceMinutes;
                    Emit(s, GameEventKind.ActionCompleted, $"绕行完成，暂时避开了敌踪；{Clock.Duration(Config.Encounters.PostExitRandomEncounterGraceMinutes)}内不会再随机遇敌。");
                    return true;
                }
                case ActionType.Ignite:
                {
                    ClearAction(s, a);
                    var f = s.Locations[here].Facility;
                    // 成功点火磨损打火器（v0.6 §05）；点火不可取消，完成即成功
                    var lighter = a.ToolInstanceId.HasValue ? FindItem(s, a.ToolInstanceId.Value) : null;
                    var worn = lighter != null ? "；" + Wear(lighter, LighterWear) : "";
                    Emit(s, GameEventKind.ActionCompleted, f.FireLit
                        ? $"火堆已点燃，剩余燃料{f.FuelMinutes}分钟{worn}。"
                        : $"火堆点燃后燃料随即耗尽{worn}。");
                    return true;
                }
                default:
                    ClearAction(s, a);
                    return true;
            }
        }

        private void ClearAction(GameState s, ActionState a)
        {
            a.LockedItemIds.Clear();
            if (a.IsSub) s.Sub = null;
            else s.Main = null;
        }

        private bool CompleteGather(GameState s, ActionState a)
        {
            var here = s.CurrentLocation;
            var res = _res[a.TargetId];
            var rs = s.Resources[res.NodeId];
            var made = new List<string>();
            if (res.IsUnits)
            {
                rs.Reserved -= a.ReservedResourceUnits;
                var kv = res.Outputs.First();
                CreateItems(s, kv.Key, a.ReservedResourceUnits, here);
                made.Add($"{ItemName(kv.Key)}+{a.ReservedResourceUnits}");
                a.ReservedResourceUnits = 0;
            }
            else
            {
                if (res.IsCache)
                {
                    rs.Reserved -= a.ReservedResourceUnits;
                    a.ReservedResourceUnits = 0;
                }
                foreach (var kv in res.Outputs)
                {
                    CreateItems(s, kv.Key, kv.Value, here, res.Scavenged);
                    made.Add($"{ItemName(kv.Key)}+{kv.Value}");
                }
                if (res.ExclusiveLoot != null && a.LootPlanned && a.LootItemId != null && a.LootCount > 0)
                {
                    CreateItems(s, a.LootItemId, a.LootCount, here, res.Scavenged);
                    made.Add($"{ItemName(a.LootItemId)}+{a.LootCount}");
                }
                if (res.IndependentRare != null && a.RareHit)
                {
                    CreateItems(s, res.IndependentRare.ItemId, res.IndependentRare.Count, here);
                    made.Add($"{ItemName(res.IndependentRare.ItemId)}+{res.IndependentRare.Count}（稀有）");
                }
            }

            var text = $"{string.Join("，", made)}，已放在{LocName(here)}地面；{AwardGatherXp(s, res, a)}";
            if (a.ToolInstanceId.HasValue)
            {
                var tool = s.Items.FirstOrDefault(i => i.InstanceId == a.ToolInstanceId.Value);
                if (tool?.Durability != null) text += "；" + Wear(tool, GatherWear);
            }
            ClearAction(s, a);
            Emit(s, GameEventKind.ActionCompleted, text + "。", null, res.Outputs.Keys);

            if (a.HazardHit && res.Hazard != null)
            {
                var loss = HazardLoss(s, res);
                s.Stats.Add(StatId.Health, -loss);
                s.Progress.LastDamageCause = $"搜集时{res.Hazard.Label}";
                Emit(s, GameEventKind.Hazard, $"{res.Hazard.Label}，健康−{Fmt.T(loss)}。");
                if (s.Stats.Health <= 0) Kill(s, s.Progress.LastDamageCause);
            }
            return true;
        }

        /// <summary>某角色死亡：主角终局（同 Kill(s, cause)）；队友死亡留遗物（R08 §4，CompanionDown）。</summary>
        private void Kill(GameState s, ActorState actor, string cause)
        {
            if (actor.IsPlayer) Kill(s, cause);
            else CompanionDown(s, actor, cause);
        }

        private void Kill(GameState s, string cause)
        {
            var loc = s.CurrentLocation;
            foreach (var a in new[] { s.Sub, s.Main })
                if (a != null) Release(s, a, loc);
            s.Main = null;
            s.Sub = null;
            s.Stats.Health = 0;
            s.Progress.Dead = true;
            s.Progress.Death = new DeathInfo
            {
                Cause = string.IsNullOrEmpty(cause) ? "伤势过重" : cause,
                Minute = s.GameMinute,
                LocationId = loc,
            };
            Emit(s, GameEventKind.Death, $"你在{LocName(loc)}倒下了。直接原因：{s.Progress.Death.Cause}。");
        }

        private static readonly StatId[] ZeroStats = { StatId.Satiety, StatId.Hydration, StatId.Wakefulness, StatId.Energy };

        public static string StatName(StatId id) => id switch
        {
            StatId.Health => "健康",
            StatId.Satiety => "饱食",
            StatId.Hydration => "水分",
            StatId.Energy => "体力",
            StatId.Wakefulness => "清醒度",
            _ => "精神",
        };

        public static string StatKey(StatId id) => id switch
        {
            StatId.Health => "health",
            StatId.Satiety => "satiety",
            StatId.Hydration => "hydration",
            StatId.Energy => "energy",
            StatId.Wakefulness => "wakefulness",
            _ => "morale",
        };

        public string AlertText(StatId id, bool zero)
        {
            if (zero)
            {
                return id switch
                {
                    StatId.Satiety => $"饱食归零：健康每小时减{Fmt.T(-Config.HealthRules.ZeroSatietyPerHour)}，请尽快进食。",
                    StatId.Hydration => $"水分归零：健康每小时减{Fmt.T(-Config.HealthRules.ZeroHydrationPerHour)}，请立即饮水。",
                    StatId.Wakefulness => $"清醒度归零：健康每小时减{Fmt.T(-Config.HealthRules.ZeroWakefulnessPerHour)}，请尽快睡眠。",
                    _ => $"体力归零：继续工作时健康每小时减{Fmt.T(-Config.HealthRules.ZeroEnergyWhileWorkingPerHour)}，建议休息。",
                };
            }
            var th = Config.Thresholds[StatKey(id)];
            var cmp = th.Inclusive ? "降至" : "低于";
            return id switch
            {
                StatId.Health => $"健康{cmp}{Fmt.T(th.Value)}，处于高危状态。",
                StatId.Satiety => $"饱食{cmp}{Fmt.T(th.Value)}，请尽快进食。",
                StatId.Hydration => $"水分{cmp}{Fmt.T(th.Value)}，请尽快饮水。",
                StatId.Wakefulness => $"清醒度{cmp}{Fmt.T(th.Value)}，需要睡眠。",
                StatId.Energy => $"体力{cmp}{Fmt.T(th.Value)}，工作耗时增加，建议休息。",
                _ => $"精神{cmp}{Fmt.T(th.Value)}，休息与睡眠的体力恢复减半。",
            };
        }

        /// <summary>
        /// 阈值与归零提醒（求生案 02）。返回是否暂停了刚推进的主行动。子行动不会被暂停（实施方案 9A）。
        /// 多角色（B3 H10）：同行每人各自锁存；队友的提醒带“队友xx：”前缀，同样会暂停主行动（只有主角时与单人版一致）。
        /// </summary>
        private bool CheckAlerts(GameState s, ActionState ran, bool completed)
        {
            var triggers = new List<(ActorState actor, StatId stat, bool zero)>();
            foreach (var actor in s.PartyActors.ToList()) CollectAlerts(actor, triggers);
            if (triggers.Count == 0) return false;

            // 归零优先于普通提醒
            triggers = triggers.OrderBy(t => t.zero ? 0 : 1).ToList();
            foreach (var t in triggers)
                Emit(s, GameEventKind.Alert, ActorPrefix(t.actor) + AlertText(t.stat, t.zero), t.stat);

            var main = s.Main;
            if (!ran.IsSub && !completed && main != null && main.ActionId == ran.ActionId && main.Status == ActionStatus.Running)
            {
                var first = triggers[0];
                Pause(s, main, first.zero ? PauseReason.StatZero : PauseReason.StatAlert, first.stat,
                    $"{main.Label}已暂停：{ActorPrefix(first.actor)}{AlertText(first.stat, first.zero)}");
                return true;
            }
            return false;
        }

        /// <summary>某角色本段新触发的归零与阈值提醒（锁存按角色）。</summary>
        private void CollectAlerts(ActorState actor, List<(ActorState actor, StatId stat, bool zero)> triggers)
        {
            foreach (var id in ZeroStats)
            {
                var isZero = actor.Stats.Get(id) <= 0;
                var latched = actor.ZeroLatched.TryGetValue(id, out var z) && z;
                if (isZero && !latched) { actor.ZeroLatched[id] = true; triggers.Add((actor, id, true)); }
                else if (!isZero) actor.ZeroLatched[id] = false;
            }
            foreach (var id in Stats.All)
            {
                var below = Config.Thresholds[StatKey(id)].IsBelow(actor.Stats.Get(id));
                var latched = actor.AlertLatched.TryGetValue(id, out var al) && al;
                if (below && !latched) { actor.AlertLatched[id] = true; triggers.Add((actor, id, false)); }
                else if (!below) actor.AlertLatched[id] = false;
            }
        }

        private void CheckSevenDay(GameState s)
        {
            if (s.Progress.SevenDayShown || s.Progress.Dead) return;
            if (s.GameMinute < Config.Progress.SevenDayCheckMinute) return;
            s.Progress.SevenDayShown = true;
            Emit(s, GameEventKind.SevenDay, "七日基础生存检查完成。你可以继续生存下去。");
        }

        /// <summary>引导“熟制块根”接受的物品（设施与烹饪案 5.3 goals.acceptedIds）。</summary>
        public static readonly string[] CookedFoodIds = { "cooked_root", "boiled_root" };

        /// <summary>引导与七日检查标记：达成即锁存（实施方案 9B-13）。</summary>
        public void UpdateFlags(GameState s)
        {
            var f = s.Progress.Flags;
            if (s.Locations.TryGetValue("field", out var field) && field.Progress >= 50) f.Add("field50");
            if ((s.Locations.TryGetValue("stream", out var st) && st.Visited) || (s.Locations.TryGetValue("woods", out var wd) && wd.Visited))
                f.Add("visitedStreamOrWoods");
            if (s.Locations.TryGetValue("village", out var vg) && vg.Visited) f.Add("visitedVillage");
            if (s.Locations.Values.Any(l => l.Facility.ShelterBuilt)) f.Add("shelterBuilt");
            if (s.Locations.Values.All(l => l.Visited)) f.Add("allVisited");

            var camp = Config.Locations.FirstOrDefault(l => l.CanBuildShelter)?.Id ?? "camp";
            // 按根位置计入：随身、营地地面，以及营地火堆与锅里的有效物品；每个实例只算一次（设施与烹饪案 5.3）
            int Count(params string[] ids) => s.Items
                .Where(i => ids.Contains(i.ItemId) && !IsSpoiled(s, i))
                .Where(i => { var p = PlaceOf(s, i); return p == ItemInstance.Carried || p == camp; })
                .GroupBy(i => i.InstanceId).Sum(g => g.First().Count);
            // “熟制块根”按烤熟与水煮合计
            if (Count("clean_water") >= 3 && Count(CookedFoodIds) >= 2) f.Add("supplyReady");
            SyncLayout(s);
        }
    }
}

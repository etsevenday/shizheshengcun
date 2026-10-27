using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 免疫力、病毒、药效与出血（v0.6 §08–§11）。每个世界 tick 按段首快照更新免疫与病毒；V≥100 尸变、健康≤0 死亡，二者优先于自然治愈；
    /// 只有“高免疫连续 720 分钟且 V≤10 的自然治愈”和“病毒特效药”能清除感染，绷带与抗生素只压低病毒（最低 1）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string ZombifiedCause = "尸变";
        private const double VirusEps = 1e-8;

        private ItemConfig? BandageItem => Config.Items.FirstOrDefault(i => i.Use != null && i.Use.IsBandage);

        private ImmunityConfig Imm => Config.Immunity;
        private InfectionConfig Inf => Config.Infection;

        // ───────────── 世界 tick ─────────────

        /// <summary>本段免疫每小时变化（按段首状态，§08 表格各项相加）。</summary>
        public double ImmunityRate(Stats st, double healthMax, bool diarrhea, bool infected, ActionType? type, bool sheltered, List<string>? why = null)
        {
            double r = 0;
            void Add(double v, string text) { if (v == 0) return; r += v; why?.Add($"{text}{Fmt.SignedT(v)}"); }
            var hc = Imm.HealthyConditions;
            if (st.Satiety >= hc.SatietyAtLeast && st.Hydration >= hc.HydrationAtLeast && st.Wakefulness >= hc.WakefulnessAtLeast
                && st.Health >= healthMax * hc.HealthRatioAtLeast && st.Morale >= hc.MoraleAtLeast)
                Add(Imm.HealthyBonusPerHour, "身体状况良好");
            if (type == ActionType.Sleep) Add(sheltered ? Imm.ShelteredSleepPerHour : Imm.ExposedSleepPerHour, sheltered ? "遮蔽睡眠" : "露天睡眠");
            if (type == ActionType.Rest) Add(sheltered ? Imm.ShelteredRestPerHour : Imm.ExposedRestPerHour, sheltered ? "遮蔽休息" : "露天休息");
            var p = Imm.PenaltiesPerHour;
            if (st.Satiety < 20) Add(p.SatietyBelow20, "饱食<20");
            if (st.Hydration < 20) Add(p.HydrationBelow20, "水分<20");
            if (st.Wakefulness < 20) Add(p.WakefulnessBelow20, "清醒<20");
            if (st.Health < 5) Add(p.HealthBelow5, "健康<5");
            if (st.Morale < 20) Add(p.MoraleBelow20, "精神<20");
            if (diarrhea) Add(p.Diarrhea, "腹泻");
            if (infected) Add(p.VirusInfected, "已感染");
            return r;
        }

        /// <summary>已感染时病毒每小时变化（按段首免疫分档）；抗生素有效期内正增长×0.5，负向不减半。</summary>
        public double VirusRate(double immunity, bool antibiotic)
        {
            var band = Inf.ProgressPerHour.FirstOrDefault(b => b.Contains(immunity));
            var rate = band?.Rate ?? 0;
            if (rate > 0 && antibiotic) rate *= Inf.Antibiotics.PositiveProgressFactor;
            return rate;
        }

        /// <summary>饱饱持续体内输入（每小时，R04 §7.2）：baobaoPresent=false（旧档）为 0。</summary>
        public double BaobaoInput(GameState s, double immunity) => s.Story.BaobaoPresent ? Inf.BaobaoInput.Rate(immunity) : 0;

        /// <summary>当前状态下的免疫与病毒速率（界面说明用；按当前行动与地点）。</summary>
        public (double immunity, double virus, List<string> why) CurrentBodyRates(GameState s)
        {
            var a = s.Sub ?? s.Main;
            var why = new List<string>();
            var diarrhea = s.DiarrheaUntil.HasValue && s.DiarrheaUntil.Value > s.GameMinute;
            var sheltered = a != null ? !a.ForceOutdoor && IsSheltered(s, s.CurrentLocation) : IsSheltered(s, s.CurrentLocation);
            var ir = ImmunityRate(s.Stats, s.Stats.HealthMax, diarrhea, s.Body.Infected, a?.Type, sheltered, why);
            var antibiotic = s.Body.AntibioticUntil.HasValue && s.Body.AntibioticUntil.Value > s.GameMinute;
            var vr = (s.Body.Infected ? VirusRate(s.Body.Immunity, antibiotic) : 0) + BaobaoInput(s, s.Body.Immunity);
            return (ir, vr, why);
        }

        private void TickBody(GameState s, TickStart start, SegmentContext ctx, bool battle)
        {
            var b = s.Body;
            var h = Tick / 60.0;
            var ir = ImmunityRate(start.Stats, start.Stats.HealthMax, start.Diarrhea, start.Infected, ctx.ActionType, ctx.Sheltered);
            b.Immunity = Math.Max(Imm.Min, Math.Min(Imm.Max, b.Immunity + ir * h));

            // 饱饱持续体内输入 B(I)（R04 §7.2，按段首免疫）：与已感染进展率相加；抗生素只减缓 R，不关闭 B
            var input = BaobaoInput(s, start.Immunity);
            if (b.Infected)
            {
                var vr = (start.Infected ? VirusRate(start.Immunity, start.AntibioticActive) : 0) + input;
                var v = b.VirusLoad + vr * h;
                if (v < Inf.NaturalLoadMinimumWhileInfected) v = Inf.NaturalLoadMinimumWhileInfected;
                b.VirusLoad = Math.Min(Inf.LoadRange[1], v);
                VirusWarnings(s);
            }
            else if (input > 0)
            {
                // 未感染且本段净输入为正：转为感染，V=max(最低值, 本段实际增加)，不套外部首次咬伤的 V20
                b.Infected = true;
                b.VirusLoad = Math.Max(Inf.BaobaoInput.FirstInfectionMinLoad, input * h);
                b.HighImmunityMinutes = 0;
                Emit(s, GameEventKind.Alert, $"饱饱在体内的病毒积累起来了：进入感染状态，病毒{Fmt.N(b.VirusLoad)}。免疫≥{Fmt.T(Inf.BaobaoInput.FullSuppressImmunityAtLeast)}时它会被完全压制，连续高免疫可自然治愈。");
                VirusWarnings(s);
            }
            else b.VirusLoad = 0;

            // 战外出血：每个世界 tick 扣一次；战斗轮末的 tick 不扣（战内出血在自己激活时扣，退出瞬间不补扣）
            if (!battle && b.BleedTriggers > 0)
            {
                s.Stats.Add(StatId.Health, -b.BleedDamage);
                b.BleedTriggers--;
                s.Progress.LastDamageCause = "出血";
                if (b.BleedTriggers == 0) b.BleedDamage = 0;
            }
        }

        /// <summary>病毒越过 20/50/80 时的阶段警告：当前变化速率与理论剩余时间（假设免疫与用药不变，不是必定倒计时）。不暂停行动。</summary>
        private void VirusWarnings(GameState s)
        {
            var b = s.Body;
            foreach (var stage in Inf.ProgressWarnings)
            {
                if (b.VirusLoad + VirusEps >= stage)
                {
                    if (b.VirusWarned.Contains(stage)) continue;
                    b.VirusWarned.Add(stage);
                    var rate = VirusRate(b.Immunity, b.AntibioticUntil.HasValue && b.AntibioticUntil.Value > s.GameMinute) + BaobaoInput(s, b.Immunity);
                    var left = rate > 0 ? $"，按现状理论上约{Clock.Duration((int)Math.Ceiling((Inf.ZombificationAtLoad - b.VirusLoad) / rate * 60))}后尸变（免疫或用药改变时随之变化）" : "，按现状不会继续升高";
                    Emit(s, GameEventKind.Alert, $"病毒程度达到{Fmt.T(stage)}（当前{Fmt.N(b.VirusLoad)}）：每小时{Fmt.SignedT(rate)}{left}。");
                }
                else b.VirusWarned.Remove(stage);
            }
        }

        private bool Zombified(GameState s) => s.Body.Infected && s.Body.VirusLoad >= Inf.ZombificationAtLoad - VirusEps;

        /// <summary>尸变：本局结束，复用死亡结算（文案区分）。</summary>
        private void Zombify(GameState s)
        {
            var loc = s.CurrentLocation;
            foreach (var a in new[] { s.Sub, s.Main })
                if (a != null) Release(s, a, loc);
            s.Main = null;
            s.Sub = null;
            s.Body.VirusLoad = Inf.ZombificationAtLoad;
            s.Progress.Dead = true;
            s.Progress.Death = new DeathInfo { Cause = ZombifiedCause, Minute = s.GameMinute, LocationId = loc, Zombified = true };
            Emit(s, GameEventKind.Death, $"病毒程度达到{Fmt.T(Inf.ZombificationAtLoad)}，你在{LocName(loc)}尸变了。本局结束。");
        }

        /// <summary>高免疫连续时间与自然治愈：段首与段末免疫都≥门槛（config naturalCure，Q7 起 80）才 +5 分钟；≥720 且 V≤10 才治愈。</summary>
        private void TickNaturalCure(GameState s, TickStart start)
        {
            var b = s.Body;
            var nc = Inf.NaturalCure;
            if (!b.Infected) { b.HighImmunityMinutes = 0; return; }
            if (start.Immunity >= nc.ConsecutiveImmunityAtLeast && b.Immunity >= nc.ConsecutiveImmunityAtLeast) b.HighImmunityMinutes += Tick;
            else b.HighImmunityMinutes = 0;
            if (b.HighImmunityMinutes >= nc.Minutes && b.VirusLoad <= nc.LoadAtMost + VirusEps)
            {
                b.Infected = false;
                b.VirusLoad = 0;
                b.HighImmunityMinutes = 0;
                b.VirusWarned.Clear();
                Emit(s, GameEventKind.Info, $"高免疫持续{Clock.Duration(nc.Minutes)}，病毒已被自然清除（高免疫自然治愈）。");
            }
        }

        // ───────────── 暴露 ─────────────

        /// <summary>一次有效咬击的感染暴露概率 0.05+0.55×(1−I/100)。</summary>
        public double ExposureChance(double immunity) =>
            Inf.ExposureBase + Inf.ExposureScale * (1 - immunity / Inf.ExposureImmunityDivisor);

        /// <summary>
        /// 丧尸咬击命中且造成正直接伤害时调用一次：在提交时抽签（读档不重抽）。成功：首次感染 V=20、计时清零；再次 V+10、计时清零。
        /// </summary>
        public bool ExposeToVirus(GameState s, out double chance)
        {
            var b = s.Body;
            chance = ExposureChance(b.Immunity);
            var hit = s.Rng.NextDouble() < chance;
            if (!hit) return false;
            if (!b.Infected)
            {
                b.Infected = true;
                b.VirusLoad = Inf.OnFirstSuccessfulExposure.Load;
                b.HighImmunityMinutes = Inf.OnFirstSuccessfulExposure.HighImmunityMinutes;
            }
            else
            {
                b.VirusLoad = Math.Min(Inf.LoadRange[1], b.VirusLoad + Inf.OnRepeatedSuccessfulExposure.LoadDelta);
                if (Inf.OnRepeatedSuccessfulExposure.ResetHighImmunityMinutes) b.HighImmunityMinutes = 0;
            }
            VirusWarnings(s);
            return true;
        }

        // ───────────── 药物 ─────────────

        /// <summary>绷带一次的健康恢复量 1×(1+0.02×医疗L)（未扣除已满部分）。</summary>
        public double BandageHeal(GameState s) => MedicalHeal(BandageItem?.Use?.Health ?? 0, SkillLevel(s, Medicine));

        /// <summary>绷带一次的病毒减量 2×(1+0.01×医疗L)。</summary>
        public double BandageVirusReduction(GameState s) => Inf.Bandage.Base * (1 + Inf.Bandage.PerLevel * SkillLevel(s, Medicine));

        private double SuppressMin => Inf.AllSuppressantsMinLoad;

        private bool BandageVirusUseful(GameState s) =>
            s.Body.Infected && s.Body.BandageVirusReadyAt <= s.GameMinute && s.Body.VirusLoad > SuppressMin + VirusEps;

        /// <summary>
        /// 药物能否使用（v0.6 §11）：绷带回血、病毒减量与止血三种效果都没有实际收益时禁用；抗生素与特效药未感染时无效，抗生素另有给药间隔。
        /// </summary>
        public string? MedicineBlocked(GameState s, ItemUseConfig u)
        {
            var b = s.Body;
            if (u.IsBandage)
            {
                var healReady = s.BandageReadyAt <= s.GameMinute;
                var canHeal = healReady && s.Stats.Health < s.Stats.HealthMax - 1e-9;
                if (canHeal || b.BleedTriggers > 0 || BandageVirusUseful(s)) return null;
                if (!healReady) return $"绷带冷却中，还需{s.BandageReadyAt - s.GameMinute}分钟" + (b.Infected ? $"（病毒减量{(b.BandageVirusReadyAt > s.GameMinute ? $"冷却还需{Clock.Duration(b.BandageVirusReadyAt - s.GameMinute)}" : "已到最低值")}）" : "");
                return b.Infected ? "健康已满、没有出血，病毒减量也不可用，包扎没有效果" : "健康已满且没有出血，包扎没有效果";
            }
            if (u.IsAntibiotic)
            {
                if (!b.Infected) return "未感染病毒，抗生素没有效果";
                if (b.AntibioticReadyAt > s.GameMinute) return $"抗生素给药间隔中，还需{Clock.Duration(b.AntibioticReadyAt - s.GameMinute)}";
                return null;
            }
            if (u.IsAntiviral)
            {
                if (!b.Infected) return "未感染病毒，特效药没有效果";
                return null;
            }
            return null;
        }

        /// <summary>药物效果在确认时应用（战外 5 分钟 wait、战内 2AP），各冷却只在对应效果实际生效时启动；有效才给医疗经验，同次只发一次。</summary>
        private void ApplyMedicine(GameState s, ItemUseConfig u, List<string> parts)
        {
            var b = s.Body;
            var effective = false;
            if (u.IsBandage)
            {
                if (b.BleedTriggers > 0)
                {
                    b.BleedTriggers = 0;
                    b.BleedDamage = 0;
                    parts.Add("已止血");
                    effective = true;
                }
                var heal = s.BandageReadyAt <= s.GameMinute ? Math.Min(BandageHeal(s), s.Stats.HealthMax - s.Stats.Health) : 0;
                if (heal > 0)
                {
                    s.Stats.Add(StatId.Health, heal);
                    parts.Add("健康" + Fmt.Delta(heal));
                    // 回血冷却只在回血实际生效时启动；只止血不延长原回血冷却
                    s.BandageReadyAt = s.GameMinute + Config.ItemActions.BandageCooldownMinutes;
                    effective = true;
                }
                else parts.Add(s.BandageReadyAt > s.GameMinute ? "回血冷却中，本次不回血" : "健康已满，本次不回血");
                if (BandageVirusUseful(s))
                {
                    var before = b.VirusLoad;
                    b.VirusLoad = Math.Max(SuppressMin, b.VirusLoad - BandageVirusReduction(s));
                    b.BandageVirusReadyAt = s.GameMinute + Inf.Bandage.VirusCooldownMinutes;
                    parts.Add($"病毒{Fmt.N(before)}→{Fmt.N(b.VirusLoad)}（仍感染）");
                    effective = true;
                    VirusWarnings(s);
                }
            }
            else if (u.IsAntibiotic && b.Infected)
            {
                var before = b.VirusLoad;
                b.VirusLoad = Math.Max(SuppressMin, b.VirusLoad - Inf.Antibiotics.InstantLoadReduction);
                b.AntibioticUntil = s.GameMinute + Inf.Antibiotics.DurationMinutes;
                b.AntibioticReadyAt = s.GameMinute + Inf.Antibiotics.UseCooldownMinutes;
                parts.Add($"病毒{Fmt.N(before)}→{Fmt.N(b.VirusLoad)}（仍感染），之后{Clock.Duration(Inf.Antibiotics.DurationMinutes)}病毒正增长×{Fmt.T(Inf.Antibiotics.PositiveProgressFactor)}");
                effective = b.VirusLoad < before - VirusEps;
                VirusWarnings(s);
            }
            else if (u.IsAntiviral && b.Infected)
            {
                b.Infected = false;
                b.VirusLoad = Inf.AntiviralCure.SetLoad;
                b.HighImmunityMinutes = Inf.AntiviralCure.SetHighImmunityMinutes;
                b.VirusWarned.Clear();
                parts.Add("病毒感染已清除（特效药）");
                effective = true;
            }
            // 有效治疗给医疗经验，同次只发一次（v0.6 §03、§11）；特效药清除感染同样是有效医疗（开发假设 26，试玩修复 L2）
            if (effective)
            {
                GainXp(s, Medicine, Config.Xp.Medical);
                parts.Add($"医疗经验+{Config.Xp.Medical}");
            }
        }

        /// <summary>
        /// 草药绷带这一次的实际效果（与 ApplyMedicine 同一判定）：出血时止血；回血冷却中写“本次不回血”，否则写健康前后；
        /// 已感染且病毒减量就绪时写病毒前后，冷却中写剩余时间（试玩修复 G4：预估与实际一致）。
        /// </summary>
        public string BandageEffectText(GameState s)
        {
            var b = s.Body;
            var parts = new List<string>();
            if (b.BleedTriggers > 0) parts.Add("止血");
            var healReady = s.BandageReadyAt <= s.GameMinute;
            var heal = healReady ? Math.Min(BandageHeal(s), s.Stats.HealthMax - s.Stats.Health) : 0;
            if (!healReady) parts.Add($"回血冷却中（还需{Clock.Duration(s.BandageReadyAt - s.GameMinute)}），本次不回血");
            else if (heal > 0) parts.Add($"健康{Fmt.N(s.Stats.Health)}→{Fmt.N(s.Stats.Health + heal)}");
            else parts.Add("健康已满，本次不回血");
            if (b.Infected)
            {
                if (BandageVirusUseful(s)) parts.Add($"病毒{Fmt.N(b.VirusLoad)}→{Fmt.N(Math.Max(SuppressMin, b.VirusLoad - BandageVirusReduction(s)))}（仍感染）");
                else if (b.BandageVirusReadyAt > s.GameMinute) parts.Add($"病毒减量冷却中（还需{Clock.Duration(b.BandageVirusReadyAt - s.GameMinute)}）");
                else parts.Add("病毒已到最低值，不再减量");
            }
            return string.Join("；", parts);
        }

        /// <summary>药品这一次的实际效果文字（绷带、抗生素、特效药），卡下操作条、右栏详情与战斗预览共用；非药品返回 null。</summary>
        public string? MedicineEffectText(GameState s, ItemUseConfig u)
        {
            var b = s.Body;
            if (u.IsBandage) return BandageEffectText(s);
            if (u.IsAntibiotic)
                return b.Infected
                    ? $"病毒{Fmt.N(b.VirusLoad)}→{Fmt.N(Math.Max(SuppressMin, b.VirusLoad - Inf.Antibiotics.InstantLoadReduction))}（仍感染），之后{Clock.Duration(Inf.Antibiotics.DurationMinutes)}病毒正增长×{Fmt.T(Inf.Antibiotics.PositiveProgressFactor)}"
                    : "未感染，没有效果";
            if (u.IsAntiviral) return b.Infected ? $"清除感染（病毒{Fmt.N(b.VirusLoad)}→{Fmt.N(Inf.AntiviralCure.SetLoad)}），不回健康与体力" : "未感染，没有效果";
            return null;
        }

        /// <summary>出血（v0.6 statuses.bleed）：0.4×3 次，不叠层，重复施加只刷新到 3 次。</summary>
        public void ApplyPlayerBleed(GameState s)
        {
            var bl = Config.Statuses.Bleed;
            s.Body.BleedDamage = bl.DamagePerOwnActivation;
            s.Body.BleedTriggers = bl.RefreshMaxTriggers;
        }
    }
}

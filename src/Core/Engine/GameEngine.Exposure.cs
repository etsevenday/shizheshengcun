using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>一次病毒暴露判定的结果（B49，R04 Q652「命中、承伤、生命态与随机顺序」）：只在真实正伤且承伤者仍活时产生，随原攻击事务一起提交。</summary>
    public sealed class ExposureOutcome
    {
        public string ProfileId { get; set; } = "";
        public string ProfileName { get; set; } = "";
        /// <summary>判定概率（未舍入，u&lt;p 成功）。</summary>
        public double Chance { get; set; }
        public bool Success { get; set; }
        /// <summary>受伤前已感染（成功时走“再次暴露”）。</summary>
        public bool WasInfected { get; set; }
        /// <summary>成功后的实际病毒量（首次）。</summary>
        public double Load { get; set; }
        /// <summary>成功时实际增加量（再次，按 100 封顶后）。</summary>
        public double Gain { get; set; }
    }

    /// <summary>
    /// B49 多途径病毒暴露（R04 文首「2026-11-14 Q652」「2026-11-15 B49 Q739—Q748」；同源 exposure-profiles-q652.json）：
    /// 五类 profile（咬伤／抓伤／刮伤／污染器械伤／变异体液／冲撞擦伤）概率 p=clamp(p0×倍率,0,1)、首次／再次病毒量；
    /// 敌种静态可能途径并集（步骤＋咬击替换＋默认物理＋合法喷溅，原表序去重）；条件预估与三态结果原句（逐字稿 B49 表）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string ExposureNone = "none";

        // ───────────── 逐字稿 B49 正式短句 ─────────────

        /// <summary>逐字稿 B49 none：静态途径并集为空的敌人（人类、普通动物等）在各传播说明处。</summary>
        public const string NoInfectionSourceText = "无感染源。";
        /// <summary>逐字稿 B49 group：有效成员≥2 且至少一人有源时，右栏战斗详情折叠顶与中央盘成员列表前各一次。</summary>
        public const string GroupInfectionSourceText = "本组有感染源，详见成员。";
        /// <summary>逐字稿 B49 companion：已感染同伴详情病毒行下。</summary>
        public const string CompanionVirusNote = "同伴病毒不自然增长；新暴露可增加，高免疫和药物可降低。";

        /// <summary>逐字稿 B49 member：「{敌人名}：可通过{途径列表}传播病毒。」</summary>
        public string EnemyInfectionMemberText(string enemyName, EnemyConfig cfg) =>
            $"{enemyName}：可通过{ExposureTypeList(cfg)}传播病毒。";

        /// <summary>逐字稿 B49 reference：「{人物} · 命中且正伤害后：」（主角“你”，同伴实际显示名）。</summary>
        public static string ExposureReferenceTitle(string who) => $"{who} · 命中且正伤害后：";

        // ───────────── 表与概率 ─────────────

        /// <summary>按 ID 取 profile；none、未知或空为 null。</summary>
        public ExposureProfileConfig? ExposureProfile(string? id) =>
            string.IsNullOrEmpty(id) || id == ExposureNone ? null : Inf.ExposureProfiles.FirstOrDefault(p => p.Id == id);

        /// <summary>原 p0=clamp(0.05+0.55×(1−clamp(I,0,100)/100),0,1)（咬伤即倍率 1，免疫页「咬伤感染概率」同源）。</summary>
        public double ExposureChance(double immunity)
        {
            var i = Math.Max(0, Math.Min(Inf.ExposureImmunityDivisor, immunity));
            return Math.Max(0, Math.Min(1, Inf.ExposureBase + Inf.ExposureScale * (1 - i / Inf.ExposureImmunityDivisor)));
        }

        /// <summary>某类型的判定概率 clamp(p0×倍率,0,1)，不按显示精度先舍入。</summary>
        public double ExposureChance(double immunity, ExposureProfileConfig p) => Math.Max(0, Math.Min(1, ExposureChance(immunity) * p.Factor));

        /// <summary>本人成功时的病毒变化：未感染为首次量；已感染为 min(100−当前V, 再次增量)。</summary>
        public double ExposureGain(BodyState b, ExposureProfileConfig p) =>
            b.Infected ? Math.Max(0, Math.Min(Inf.LoadRange[1] - b.VirusLoad, p.RepeatDelta)) : p.FirstLoad;

        // ───────────── 静态可能途径（Q740—Q741） ─────────────

        /// <summary>
        /// Q740 A：该敌种全部可能类型＝opening＋cycle 全部步骤 exposure＋咬击替换＋defaultPhysicalExposure＋合法死亡喷溅 deathSplashExposure 的并集，
        /// 去 none／null／重复，按原 profile 表序。只读物种固定配置，不读本回合锁定意图、站位或 AP。
        /// </summary>
        public List<ExposureProfileConfig> EnemyExposureTypes(EnemyConfig cfg)
        {
            var ids = new HashSet<string>();
            foreach (var st in cfg.Intents.Opening.Concat(cfg.Intents.Cycle))
            {
                if (st.Exposure != null) ids.Add(st.Exposure);
                if (st.BiteReplacementExposure != null) ids.Add(st.BiteReplacementExposure);
            }
            if (cfg.DefaultPhysicalExposure != null) ids.Add(cfg.DefaultPhysicalExposure);
            if (cfg.DeathSplash > 1e-12 && cfg.DeathSplashExposure != null) ids.Add(cfg.DeathSplashExposure);
            return Inf.ExposureProfiles.Where(p => ids.Contains(p.Id)).ToList();
        }

        /// <summary>Q741 A：类型之间用“、”，类型名内部“／”保留。</summary>
        public string ExposureTypeList(EnemyConfig cfg) => string.Join("、", EnemyExposureTypes(cfg).Select(p => p.Name));

        /// <summary>该敌种有没有任何真实传播类型（无则各说明处写「无感染源。」）。</summary>
        public bool HasInfectionSource(EnemyConfig cfg) => EnemyExposureTypes(cfg).Count > 0;

        /// <summary>
        /// Q742 条件预估的一行（逐字稿 firstEstimate／repeatEstimate）：按参考者本人 I、infected 与当前 V；概率原 Fmt.Pct，病毒量 Fmt.T，再次按封顶后实际增量。只读、不抽签。
        /// </summary>
        public string ExposureEstimateLine(BodyState b, ExposureProfileConfig p)
        {
            var chance = Fmt.Pct(ExposureChance(b.Immunity, p));
            return b.Infected
                ? $"{p.Name}：概率{chance}，病毒+{Fmt.T(ExposureGain(b, p))}。"
                : $"{p.Name}：概率{chance}，感染后病毒{Fmt.T(p.FirstLoad)}。";
        }

        // ───────────── 真实判定 ─────────────

        /// <summary>
        /// 对当前视角承伤者（已 FocusOn）判一次该类型暴露：u∈[0,1) 取原持久随机流，u&lt;p 成功、相等失败。未感染成功设感染与首次量，已感染成功 V+增量（100 封顶），
        /// 二者都把连续高免疫计时归 0；失败不加 V、不重置计时。调用方须已确认命中、D&gt;0 且生命判定后仍活（含濒死）。
        /// </summary>
        public ExposureOutcome ExposeToVirus(GameState s, ExposureProfileConfig p)
        {
            var b = s.Body;
            var r = new ExposureOutcome { ProfileId = p.Id, ProfileName = p.Name, Chance = ExposureChance(b.Immunity, p), WasInfected = b.Infected };
            r.Success = s.Rng.NextDouble() < r.Chance;
            if (!r.Success) return r;
            if (!b.Infected)
            {
                b.Infected = true;
                b.VirusLoad = Math.Min(Inf.LoadRange[1], p.FirstLoad);
                b.HighImmunityMinutes = Inf.OnFirstSuccessfulExposure.HighImmunityMinutes;
                r.Load = b.VirusLoad;
            }
            else
            {
                var before = b.VirusLoad;
                b.VirusLoad = Math.Min(Inf.LoadRange[1], b.VirusLoad + p.RepeatDelta);
                if (Inf.OnRepeatedSuccessfulExposure.ResetHighImmunityMinutes) b.HighImmunityMinutes = 0;
                r.Gain = b.VirusLoad - before;
                r.Load = b.VirusLoad;
            }
            VirusWarnings(s);
            return r;
        }

        /// <summary>旧接口（咬伤）：一次有效咬击的暴露，成功返回 true。</summary>
        public bool ExposeToVirus(GameState s, out double chance)
        {
            var r = ExposeToVirus(s, ExposureProfile("bite")!);
            chance = r.Chance;
            return r.Success;
        }

        /// <summary>
        /// 逐字稿 B49 firstResult／repeatResult／failedResult：{人物}取当前视角承伤者（“你”或真实姓名），{途径}中文类型名，概率原 Fmt.Pct，量 Fmt.T（再次为封顶后增量）。
        /// 句末无终止标点，由调用方接原伤害主句分隔符或喷溅逗号。
        /// </summary>
        public string ExposureResultText(GameState s, ExposureOutcome r)
        {
            var who = ActorDisplayName(s, s.Player.Id);   // 当前视角承伤者（战内 FocusOn，战外 FocusOutOfBattle）
            var pct = Fmt.Pct(r.Chance);
            if (!r.Success) return $"{who}本次{r.ProfileName}未增加感染（判定概率{pct}）";
            return r.WasInfected
                ? $"{who}因{r.ProfileName}再次受到感染暴露，病毒+{Fmt.T(r.Gain)}（判定概率{pct}）"
                : $"{who}因{r.ProfileName}感染，病毒量{Fmt.T(r.Load)}（判定概率{pct}）";
        }

        /// <summary>
        /// Q739 A：这次真实攻击取哪一种类型——实际咬击取咬击替换（kind=bite 步骤取本步）；追击、迫近弱近袭、后排降格弱攻击取 defaultPhysicalExposure；
        /// 其余（正常执行、前移／协同前压后够到再攻击）取本次锁定步骤自身 exposure，不因 plain=true 回退；合成的普通攻击回退步骤已带默认类型。
        /// </summary>
        private static string AttackExposure(EnemyConfig cfg, EnemyIntentStepConfig st, bool bite, bool degraded)
        {
            // 真实咬击只来自可改判咬击的步骤（咬击替换）或 kind=bite 步骤；别的情况不凭“咬击”二字造咬伤，退回该敌种默认类型（人类／动物为 none）
            if (bite) return st.BiteReplacementExposure ?? (st.Kind == "bite" ? st.Exposure : null) ?? cfg.DefaultPhysicalExposure ?? ExposureNone;
            if (degraded) return cfg.DefaultPhysicalExposure ?? ExposureNone;
            return st.Exposure ?? cfg.DefaultPhysicalExposure ?? ExposureNone;
        }
    }
}

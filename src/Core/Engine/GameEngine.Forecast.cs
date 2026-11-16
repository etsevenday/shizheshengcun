using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B47a3 行动与撤出预估截断（R09 文首「B47a3 Q682—Q687」与「B47a Q673—Q678」中的 Q674、Q675；S01、S14、R03、R04、逐字稿同批节；开发记录 B47a3）。
    /// 预估只在只读副本上跑原 5 分钟段：副本里健康归零视为濒死存活、需要致死判定时不抽签不确认死亡、病毒到上限不尸变（完成计划假设存活完成）；
    /// 同时由 <see cref="SimProbe"/> 记下每个相关人物的首个危险段，身体预估在那里截止（普通行动取同行者最早的一段，撤出逐人各自截止）。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───── 逐字稿原句（R09 Q674、Q675、Q683—Q687；逐字稿「B47a3 Q682—Q687」表） ─────
        public const string BodyCutTitle = "预估至危险段";
        public const string BodyCutRange = "身体预估仅到首次濒死或需要致死判定的危险段；后续身体变化未预估。";
        public const string NotEstimated = "未预估";
        public const string PlanTitle = "完成计划（假设能够存活并完成）";
        public const string PlanNote = "本区按能够存活并完成本次行动估算，已考虑途中已知的速度与环境变化；不保证实际能够完成。";
        public const string PlanFailedText = "按当前条件无法预估完成。";
        /// <summary>S08 Q725：制作卡静态产物标题与完成不可靠时固定配方的输出行标签。</summary>
        public const string RecipeOutputLabel = "配方产物";

        /// <summary>
        /// S08 Q725：预估输出行状态。完成可靠照旧“产出 · 留在{地点}”；求不出可信完成时，有固定配方的制作／建造／处理尸体／拆解写“配方产物 · 留在{地点}”
        /// （数量仍是配方×份数，成果落在开工所在区地面、地点可靠）；采集等没有固定配方的输出不在这里换（Q776 待定）。
        /// </summary>
        public static string OutputStatusText(ActionPreview p, ActionType type) =>
            p.PlanFailed && IsFixedRecipeOutput(type) ? $"{RecipeOutputLabel} · 留在{p.OutputPlace}"
            : p.PlanFailed && type == ActionType.Gather ? PossibleOutputLabel   // B51c1（S08 B51 Q776）：不可靠采集状态「可能产物」
            : $"产出 · 留在{p.OutputPlace}";

        /// <summary>S08 Q725：有固定配方的输出（制作、建造、处理尸体、拆解）。</summary>
        public static bool IsFixedRecipeOutput(ActionType type) => type is ActionType.Craft or ActionType.Build or ActionType.Butcher or ActionType.Salvage;

        /// <summary>B51c1（S08／S14 B51 Q776）：搜索／清理候选清单标题与不可靠采集的输出状态。</summary>
        public const string PossibleOutputLabel = "可能产物";
        /// <summary>B51c1（Q776）：搜索／清理「可能产物」下一行，原括注另起一行。</summary>
        public const string PossibleOutputDrawNote = "完成时只抽1次，物品留在当前地面。";

        /// <summary>B51c1（Q776）：不能开工的固定配方静态行「配方产物：{产物清单}」（清单「{名}×{数}」以「、」连接）。</summary>
        public static string StaticRecipeOutputLine(IEnumerable<string> items) => $"{RecipeOutputLabel}：{string.Join("、", items)}";

        /// <summary>B51c1（Q776）：输出数量。采集完成不可靠时写「未预估（{实际原因}）」，其余为「×{数量}」（固定配方仍配方×份数）。</summary>
        public static string OutputQtyText(ActionPreview p, ActionType type, int count) =>
            p.PlanFailed && type == ActionType.Gather ? UnknownText(p) : $"×{count}";

        /// <summary>
        /// B51c1（S08 B51 Q775、Q873 A 暂用）：依赖完成的未知值。有同源实际原因时「未预估（{原因}）」，没有原因只写「未预估」。
        /// </summary>
        public static string UnknownText(ActionPreview p) => p.PlanFailShort is string r ? $"{NotEstimated}（{r}）" : NotEstimated;

        /// <summary>
        /// B51c1（Q873 A 暂用）：从同一预估的真实失败原因派生括号里的短原因——续做被拒原因去句末标点；中止／取消日志只取「原因：」后的正文（到第一个句末标点）；
        /// 两者都没有（推到上限仍未完成、或日志里没有「原因：」）为 null。
        /// </summary>
        public static string? ShortFailReason(string? text, bool fromResume)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            string body;
            if (fromResume) body = text;
            else
            {
                var at = text.IndexOf(ReasonMark, StringComparison.Ordinal);
                if (at < 0) return null;
                body = text.Substring(at + ReasonMark.Length);
                var end = body.IndexOfAny(SentenceEnds);
                if (end >= 0) body = body.Substring(0, end);
            }
            body = body.TrimEnd(SentenceEnds).TrimEnd('，', '；', ',', ';', ' ').Trim();
            return body.Length == 0 ? null : body;
        }

        private const string ReasonMark = "原因：";
        private static readonly char[] SentenceEnds = { '。', '！', '？', '.', '!', '?' };

        /// <summary>
        /// B51c1（S08 B51 Q775、Q873 A 暂用）：同类余量行。完成可信「{当前} → {完成后}分钟」；完成后值不可靠时保留可信当前值「{当前}分钟 → 未预估（{实际原因}）」；
        /// 本来就没有完成后值时只写当前。
        /// </summary>
        public static string CarryRowText(ActionPreview p) =>
            p.CarryAfter is double ca ? $"{Fmt.N(p.CarryBefore)} → {Fmt.N(ca)}分钟"
            : p.PlanFailed ? $"{Fmt.N(p.CarryBefore)}分钟 → {UnknownText(p)}"
            : $"{Fmt.N(p.CarryBefore)}分钟";

        /// <summary>S08 Q725：工具完成后耐久依赖完成，求不出可信完成时写“未预估”（当前耐久照旧）；B51c1（Q873 A）带实际原因。</summary>
        public static string ToolAfterText(ActionPreview p) =>
            p.PlanFailed ? UnknownText(p) : Fmt.N(p.ToolDurabilityAfter ?? 0);

        /// <summary>
        /// B54（S08／R09 Q792）：完整完成计划不可信、又没有身体危险共同截止时，身体各展示行后值一律“未预估”（前值仍为当前实值，不按副本停下处着色）；
        /// 有危险截止时沿 Q682／Q686 截止内可信值，只有原依赖致死判定的健康未预估。
        /// </summary>
        public static bool BodyAfterUnknown(ActionPreview p) => p.PlanFailed && !p.BodyCut;

        /// <summary>主角身体某属性的预估后值是否写“未预估”（Q792 整体未知，或 Q683 截止段健康取决于致死判定）。</summary>
        public static bool BodyStatAfterUnknown(ActionPreview p, StatId id) =>
            BodyAfterUnknown(p) || (id == StatId.Health && p.HealthUnknown.Contains(GameState.PlayerId));

        /// <summary>
        /// B51c1（Q775、Q873 A 暂用）：身体未知后值的写法——完成不可信（Q792）带同源实际原因；只取决于致死判定的健康（Q683，可信危险截止）仍只写「未预估」。
        /// </summary>
        public static string BodyUnknownText(ActionPreview p) => BodyAfterUnknown(p) ? UnknownText(p) : NotEstimated;

        /// <summary>S08 Q725：完成后经验依赖完成，求不出可信完成时整行值写“未预估”。</summary>
        public static string XpAfterText(ActionPreview p) =>
            p.PlanFailed ? UnknownText(p) : string.Join("、", p.XpGains);
        public const string LaterRiskTitle = "各人风险";
        public const string LaterRiskNote = "以下为各人截至计划结束的首个风险检查，不表示已预估危险处以后的身体数值。";
        public const string ProbabilityOnlyNote = "身体变化按未触发本次概率受伤估算；概率受伤风险另列，不代表保证安全。";
        public const string ResistanceMeaning = "致死抵抗是濒死后每次再受正伤害时的抵抗机会，不是整段行动的存活率。";
        /// <summary>Q675：撤出原复合风险句拆出的身体／感染机制说明。</summary>
        public const string ExtractBodyMechanism = "已经归零的饱食、水分、清醒继续扣健康，出血、感染与病毒照常发展。";

        /// <summary>R09 Q702：“各人风险”逐条前缀“约{时长}后：”（时长＝本人首风险段末−本次预估起点，原游戏时长格式）。</summary>
        public static string LaterRiskPrefix(int minutes) => $"约{Clock.Duration(minutes)}后：";

        /// <summary>普通行动共同身体截止：“身体预估截至从现在起{时长}；此后身体变化未预估。”</summary>
        public static string BodyCutAtText(int minutes) => $"身体预估截至从现在起{Clock.Duration(minutes)}；此后身体变化未预估。";

        /// <summary>撤出某人身体截止：“{人物}的撤出身体预估截至从现在起{时长}；此后身体变化未预估。”</summary>
        public static string ExtractCutAtText(string who, int minutes) => $"{who}的撤出身体预估截至从现在起{Clock.Duration(minutes)}；此后身体变化未预估。";

        /// <summary>Q684 尸变风险（普通／撤出）。</summary>
        public static string VirusRiskText(string who, bool extract) => extract
            ? $"按当前已知状态推算，若继续撤出，{who}的病毒量预计在计划结束前或结束时达到尸变上限。致死抵抗不能阻止尸变。"
            : $"按当前已知状态推算，若本次行动继续推进，{who}的病毒量预计在计划结束前或结束时达到尸变上限。致死抵抗不能阻止尸变。";

        /// <summary>Q675 六句：普通行动／撤出 × 首次归零／已濒死确定正伤／已濒死仅概率正伤；{抵抗率}为本人当前整数百分点。</summary>
        public static string DyingRiskText(DangerKind kind, string who, int percent, bool extract)
        {
            var pct = $"{percent}%";
            var body = kind switch
            {
                DangerKind.Enter => $"按当前状态，{who}预计在本段进入濒死。此后仍处于濒死状态时，每次再受到正伤害都将进行致死判定；{who}当前致死抵抗为{pct}。",
                DangerKind.Certain => $"{who}已处于濒死状态，本段将受到正伤害；濒死期间每次受到正伤害都将进行致死判定，{who}当前致死抵抗为{pct}。",
                _ => $"{who}已处于濒死状态。若本段在濒死状态下受到正伤害，每次都将进行致死判定；{who}当前致死抵抗为{pct}。",
            };
            return extract ? "撤出途中，" + body : body;
        }

        /// <summary>
        /// 预估副本上的一次概率伤检查点（搜集划伤、勘整风险未触发时）：已濒死者记“仅有概率正伤”的危险（Q675 第三句、Q687 末段仍截断）；
        /// 尚未濒死者若这一下可能把健康打到 0，只记 Q687 限定（不截断）。真实状态不做任何事。
        /// </summary>
        private void SimPossibleHit(GameState s, ActorState actor, double loss)
        {
            if (!s.SimCopy || s.SimProbe == null || !(loss > 0) || IsDead(s, actor)) return;
            if (IsDying(s, actor)) s.SimProbe.Dangers.Add((actor.Id, DangerKind.Possible));
            else if (actor.Stats.Health - loss <= 0) s.SimProbe.ProbabilityZero.Add(actor.Id);
        }

        /// <summary>一次只读预估推进的结果（按 5 分钟段计）。</summary>
        private sealed class ForecastRun
        {
            /// <summary>每个相关人物的首个危险：类型、第几段（从 1 起）、该段结束时的世界分钟。</summary>
            public readonly Dictionary<string, (DangerKind Kind, int Seg, int Minute)> First = new Dictionary<string, (DangerKind, int, int)>();
            /// <summary>出现首个危险的那一段结束时，全部相关人物的身体（按段号）。</summary>
            public readonly Dictionary<int, Dictionary<string, Stats>> Snapshots = new Dictionary<int, Dictionary<string, Stats>>();
            /// <summary>（段号, 人物）：这一段该人有需要致死判定的正伤（确定或可能），段末健康取决于抵抗结果，写“未预估”。</summary>
            public readonly HashSet<(int, string)> ResistSegments = new HashSet<(int, string)>();
            /// <summary>Q687：仅概率伤可能归零的人物与首次段号。</summary>
            public readonly Dictionary<string, int> ProbabilityZero = new Dictionary<string, int>();
            /// <summary>行动暂停在提醒阈值上的（段号, 属性）。</summary>
            /// <summary>
            /// B51b（Q728、Q703）：主行动因该边界暂停（提醒、归零或濒死）时，该边界按原规则收集、且从预估起点看是真实未来跨线的全部触发（段、人物、属性），
            /// 按段与原触发顺序（归零类先）。
            /// </summary>
            public readonly List<(int Seg, string ActorId, StatId Stat)> Alerts = new List<(int, string, StatId)>();
            public HashSet<string> Virus = new HashSet<string>();
            /// <summary>完成计划求得可信完成（行动正常完成）。</summary>
            public bool Done;
            /// <summary>求不出完成时的已有真实原因（暂停无法继续的原因、中止日志）；没有为 null。</summary>
            public string? FailReason;
            /// <summary>B51c1（Q873）：FailReason 来自续做被拒的原因（否则来自中止／取消日志）。</summary>
            public bool FailFromResume;
            /// <summary>普通行动的共同截止段：相关人物中最早的危险段。</summary>
            public int? CutSeg => First.Count == 0 ? (int?)null : First.Values.Min(v => v.Seg);
        }

        /// <summary>
        /// 在只读副本 c 上把行动 id 推到结束（提醒按原估时规则自动继续，概率风险视为未触发），按段记下相关人物 who 的危险、阈值提醒与病毒上限。
        /// beforeSegment 在每段推进前调用（普通行动跟踪工作倍率变化）。只改副本。
        /// </summary>
        private ForecastRun RunForecast(GameState c, int id, bool sub, IReadOnlyCollection<string> who, Action<GameState>? beforeSegment = null)
        {
            var run = new ForecastRun();
            var probe = c.SimProbe = new SimProbe();
            var seg = 0;
            var last = SegmentStatus.Running;
            string? resumeWhy = null;
            // Q703：从预估起点按原阈值谓词（含等号）判断各人各属性是否“还在未越线一侧”；起点已在低值区的不算将来跨线，恢复到原解除条件后重获资格（同原锁存解除）
            var armed = new Dictionary<(string, StatId, bool), bool>();
            void Rearm(bool init)
            {
                foreach (var x in who)
                {
                    if (!c.Actors.TryGetValue(x, out var ac)) continue;
                    foreach (var st in Stats.All)
                        if (!Config.Thresholds[StatKey(st)].IsBelow(ac.Stats.Get(st))) armed[(x, st, false)] = true;
                        else if (init) armed[(x, st, false)] = false;
                    foreach (var st in ZeroStats)
                        if (ac.Stats.Get(st) > 0) armed[(x, st, true)] = true;
                        else if (init) armed[(x, st, true)] = false;
                }
            }
            Rearm(true);
            for (var k = 0; k < SimLimit; k++)
            {
                SimReadDialogues(c);
                var cur = sub ? c.Sub : c.Main;
                if (cur == null || cur.ActionId != id || c.Progress.Dead) break;
                if (!sub && cur.Status == ActionStatus.Paused)
                {
                    var rr = ResumeCore(c, false);
                    if (!rr.Ok) { resumeWhy = rr.Reason; break; }   // 不推进时间也解不开暂停：按当前条件求不出完成
                }
                beforeSegment?.Invoke(c);
                var d0 = probe.Dangers.Count;
                var z0 = probe.ProbabilityZero.Count;
                var a0 = probe.Alerts.Count;
                last = Advance(c).Status;
                seg++;
                var fresh = false;
                for (var i = d0; i < probe.Dangers.Count; i++)
                {
                    var (actorId, kind) = probe.Dangers[i];
                    if (!who.Contains(actorId)) continue;   // 候补、异地人物不是本次相关者
                    if (kind != DangerKind.Enter) run.ResistSegments.Add((seg, actorId));
                    if (run.First.ContainsKey(actorId)) continue;
                    run.First[actorId] = (kind, seg, c.GameMinute);
                    fresh = true;
                }
                if (fresh && !run.Snapshots.ContainsKey(seg))
                    run.Snapshots[seg] = who.Where(c.Actors.ContainsKey).ToDictionary(x => x, x => c.Actors[x].Stats.Clone());
                for (var i = z0; i < probe.ProbabilityZero.Count; i++)
                    if (who.Contains(probe.ProbabilityZero[i]) && !run.ProbabilityZero.ContainsKey(probe.ProbabilityZero[i])) run.ProbabilityZero[probe.ProbabilityZero[i]] = seg;
                var now = sub ? c.Sub : c.Main;
                // Q728：原机制在这一边界确实暂停本行动时，列出该边界全部有效触发（真实只停一次）；撤出、子行动、已完成的段原本不因此暂停，不列
                var paused = !sub && now != null && now.ActionId == id && now.Status == ActionStatus.Paused
                    && now.PauseReason is PauseReason.StatAlert or PauseReason.StatZero or PauseReason.Dying;
                for (var i = a0; i < probe.Alerts.Count; i++)
                {
                    var (actorId, st, zero) = probe.Alerts[i];
                    if (!who.Contains(actorId) || !armed.TryGetValue((actorId, st, zero), out var on) || !on) continue;
                    armed[(actorId, st, zero)] = false;
                    if (paused) run.Alerts.Add((seg, actorId, st));
                }
                Rearm(false);
            }
            run.Virus = new HashSet<string>(probe.Virus.Where(who.Contains));
            var end = sub ? c.Sub : c.Main;
            var still = end != null && end.ActionId == id;
            run.Done = !still && !c.Progress.Dead && last != SegmentStatus.Aborted;
            if (!run.Done)
            {
                run.FailReason = still ? resumeWhy : c.PendingEvents.LastOrDefault(ev => ev.Kind == GameEventKind.ActionAborted || ev.Kind == GameEventKind.ActionCancelled)?.Text;
                run.FailFromResume = still;
            }
            c.SimProbe = null;
            return run;
        }

        /// <summary>
        /// 普通行动预估的身体截断与风险（Q674、Q675、Q683、Q684、Q686、Q687）：相关人物为主角与当前同行的存活者（含濒死，不含候补）；
        /// 共同身体截止取最早危险段，主角（与食饮对象）身体取该段末，需要抵抗结果的健康写“未预估”；截止段内的风险进身体区，更晚的首个风险进“各人风险”。
        /// </summary>
        private void ApplyBodyForecast(GameState s, GameState c, ActionPreview p, ForecastRun run, IReadOnlyList<string> who)
        {
            var cut = run.CutSeg;
            // Q703／Q728：只列身体共同截止以内的真实未来跨线，按人物＋属性各留首次（同属性两人不合并）；多于一名相关人物时带“{人物}：”
            var alerts = run.Alerts.Where(x => cut == null || x.Seg <= cut.Value)
                .GroupBy(x => (x.ActorId, x.Stat)).Select(g => g.First()).ToList();
            p.AlertsExpected = alerts.Select(x => x.Stat).Distinct().ToList();
            foreach (var x in alerts)
                p.BodyAlerts.Add((who.Count > 1 ? $"{ActorDisplayName(s, x.ActorId)}：" : "") + $"途中{StatName(x.Stat)}将越过提醒阈值，行动会暂停");
            if (cut is int seg)
            {
                var snap = run.Snapshots[seg];
                p.BodyCutoffMinutes = run.First.Values.First(v => v.Seg == seg).Minute - s.GameMinute;
                if (snap.TryGetValue(GameState.PlayerId, out var mine)) p.StatsAfter = mine.Clone();
                if (p.Feed != null && snap.TryGetValue(p.Feed.TargetActorId, out var fed)) p.Feed.TargetAfter = fed.Clone();
                foreach (var id in who) if (run.ResistSegments.Contains((seg, id))) p.HealthUnknown.Add(id);
            }
            foreach (var id in who)
            {
                if (!run.First.TryGetValue(id, out var f)) continue;
                var text = DyingRiskText(f.Kind, ActorDisplayName(s, id), ResistancePercent(s.Actors[id]), false);
                if (cut == f.Seg) p.BodyRisks.Add(text);
                else p.LaterRisks.Add(LaterRiskPrefix(f.Minute - s.GameMinute) + text);   // Q702：从本次预估起点到本人首风险段末
            }
            if (p.BodyRisks.Count > 0 || p.LaterRisks.Count > 0) p.ResistanceNote = ResistanceMeaning;
            foreach (var id in who) if (run.Virus.Contains(id)) p.VirusRisks.Add(VirusRiskText(ActorDisplayName(s, id), false));
            if (run.ProbabilityZero.Values.Any(z => cut == null || z <= cut.Value)) p.ProbabilityNote = ProbabilityOnlyNote;
        }

        /// <summary>
        /// 把预估的各类提示摊平成一列（就近浮窗、制作／休息摘要等紧凑位置照旧读 Warnings，常只显示第一条）：
        /// 求不出完成 → 身体截止与风险 → 各人风险 → 抵抗含义 → 计划提示 → 阈值提醒 → 尸变 → 概率限定。无危险时与原顺序相同（计划提示在阈值提醒前）。
        /// </summary>
        private static List<string> FlatWarnings(ActionPreview p)
        {
            var list = new List<string>();
            if (p.PlanFailed) list.Add(PlanFailedText + (p.PlanFailReason is string why ? why : ""));
            if (p.BodyCutoffMinutes is int m) list.Add(BodyCutAtText(m));
            list.AddRange(p.BodyRisks);
            if (p.LaterRisks.Count > 0) { list.Add(LaterRiskNote); list.AddRange(p.LaterRisks); }
            if (p.ResistanceNote != null) list.Add(p.ResistanceNote);
            list.AddRange(p.PlanWarnings);
            list.AddRange(p.BodyAlerts);
            list.AddRange(p.VirusRisks);
            if (p.ProbabilityNote != null) list.Add(p.ProbabilityNote);
            return list;
        }

        /// <summary>
        /// 撤出逐人预估（R03／R09 Q685、Q684、Q675）：副本先按原确认事务一次扣包（已在撤出中只推剩余路线、不重扣），再按真实路线、锁定倍率、
        /// 5 分钟段与撤出专用身体规则推到撤出完成；每人在自己的首个危险段截止身体，病毒单独查到撤出结束。只读。
        /// </summary>
        private void FillExtractForecast(GameState s, AbortPreviewInfo p)
        {
            var e = s.Expedition;
            if (e == null || s.Battle != null) return;
            if (!e.IsExtracting && p.Reason != null) return;
            var c = s.Clone();
            c.SuppressEnemyApproach = true;
            c.SimCopy = true;
            SimReadDialogues(c);
            if (!e.IsExtracting && !AbortExpedition(c).Ok) return;
            var a = c.Main;
            if (a == null || a.Type != ActionType.Extract) return;
            var who = c.PartyActors.Where(x => !IsDead(c, x)).Select(x => x.Id).ToList();
            var from = who.ToDictionary(x => x, x => c.Actors[x].Stats.Health);
            var run = RunForecast(c, a.ActionId, false, who);
            foreach (var id in who)
            {
                var name = ActorDisplayName(s, id);
                var row = new ExtractBodyRow { Id = id, Name = name, HealthFrom = from[id] };
                if (run.First.TryGetValue(id, out var f))
                {
                    row.CutoffMinutes = f.Minute - s.GameMinute;
                    row.CutoffText = ExtractCutAtText(name, row.CutoffMinutes.Value);
                    row.HealthTo = run.ResistSegments.Contains((f.Seg, id)) ? (double?)null : run.Snapshots[f.Seg][id].Health;
                    row.Risk = DyingRiskText(f.Kind, name, ResistancePercent(s.Actors[id]), true);
                }
                else row.HealthTo = run.Done && c.Actors.TryGetValue(id, out var end) ? end.Stats.Health : (double?)null;
                p.BodyRows.Add(row);
                if (run.Virus.Contains(id)) p.VirusRisks.Add(VirusRiskText(name, true));
            }
            if (p.BodyRows.Any(r => r.Risk != null)) p.ResistanceNote = ResistanceMeaning;
        }
    }

    /// <summary>撤出逐人身体检查（Q685）：从确认扣包后（已在撤出中为当前）到本人截止处或撤出结束的健康，以及本人截止与风险句。</summary>
    public sealed class ExtractBodyRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public double HealthFrom { get; set; }
        /// <summary>本人截止处（无截止为撤出结束）的健康；取决于致死判定结果时为 null（显示“未预估”）。</summary>
        public double? HealthTo { get; set; }
        /// <summary>从现在起到本人首个危险段末的分钟；没有截断为 null。</summary>
        public int? CutoffMinutes { get; set; }
        public string? CutoffText { get; set; }
        /// <summary>本人对应的 Q675 撤出风险句；没有危险为 null。</summary>
        public string? Risk { get; set; }
    }
}

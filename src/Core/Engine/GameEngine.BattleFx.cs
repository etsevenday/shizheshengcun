using System;
using System.Collections.Generic;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B34b（R03 文首「2026-11-13 B34主要Q570—Q572」§Q571／§Q572）：出手事件快照的记录器。
    /// 内核照原事务结算（命中、伤害、倒地、经验、掉落都只在原处算一次），只在结算的同一时刻把“谁对谁、按真实顺序出了什么结果”抄进一份
    /// 只读快照；界面拿快照排队播放，播放／跳过不回头调用任何结算。快照不进存档、不进 GameState（刷新、读档后直接是真实已提交状态）。
    /// </summary>
    public sealed partial class GameEngine
    {
        private readonly List<BattleFxEvent> _fx = new List<BattleFxEvent>();
        private readonly List<BattleFxEvent> _fxOpen = new List<BattleFxEvent>();
        private int _fxSeq;
        /// <summary>B47b（Q617）：最近一次结束的出手结束时的显示快照（父出手在子事件之后继续出段时，作为续段开始前的显示）。</summary>
        private GameState? _fxLastClose;

        /// <summary>取走自上次以来记下的出手事件（按真实发生顺序；反击／迎击插在触发处）。界面每次提交或每一步之后调用一次。</summary>
        public List<BattleFxEvent> DrainBattleFx()
        {
            var r = new List<BattleFxEvent>(_fx);
            _fx.Clear();
            _fxOpen.Clear();
            _fxLastClose = null;
            return r;
        }

        private sealed class FxClose : IDisposable
        {
            private readonly GameEngine _e;
            private readonly BattleFxEvent? _ev;
            private readonly GameState? _s;
            private readonly Dictionary<string, List<string>>? _before;
            public FxClose(GameEngine e, BattleFxEvent? ev, GameState? s = null, Dictionary<string, List<string>>? before = null) { _e = e; _ev = ev; _s = s; _before = before; }
            public void Dispose()
            {
                if (_ev == null) return;
                _e.FxApplyLifeMarks(_ev);   // B47b：结算途中记下的生命姿态变化并到承受者本出手最后一段
                if (_s != null && _before != null) _e.FxStampStates(_s, _ev, _before);
                if (_s != null && !_ev.Short && !_s.SimCopy) _e._fxLastClose = _s.DisplayClone();   // B47b（Q617）：续段开始前的显示快照
                // 没有任何结果的出手（目标全部失效、医疗无健康变化）不留快照，不播空镜头
                if (!_ev.Short) _e._fx.RemoveAll(x => (x == _ev || x.ContinuesSeq == _ev.Seq) && x.Results.Count == 0);
                // 栈顶可能已换成本事件的续段（子事件插入后父事件继续出段）——连同续段一起出栈
                for (var i = _e._fxOpen.Count - 1; i >= 0; i--)
                {
                    var o = _e._fxOpen[i];
                    var mine = o == _ev || o.ContinuesSeq == _ev.Seq;
                    _e._fxOpen.RemoveAt(i);
                    if (mine) break;
                }
            }
        }

        /// <summary>开始记一次出手（特写或短反馈）。不在战斗中时不记。返回的对象 Dispose 时结束这一次出手。</summary>
        private IDisposable FxBegin(GameState s, string name, string motion, BattleFxUnit actor, bool shortFeedback = false, bool noNumbers = false, GameState? view = null)
        {
            var b = s.Battle;
            if (b == null || s.SimCopy) return new FxClose(this, null);
            var ev = new BattleFxEvent
            {
                Seq = ++_fxSeq,
                BattleId = b.BattleId,
                Name = name,
                Motion = motion,
                Actor = actor,
                Short = shortFeedback,
                NoNumbers = noNumbers,
                ParentSeq = _fxOpen.Count > 0 ? _fxOpen[_fxOpen.Count - 1].RootSeq : (int?)null,
                // B47b（R03 §Q617）：这一段开始前的显示快照（播放时上一段结果帧之后切到它）
                View = shortFeedback ? null : view ?? s.DisplayClone(),
            };
            if (_fx.Count >= 400) _fx.RemoveRange(0, _fx.Count - 300);   // 没人取走（测试、离线推进）时不无限增长
            _fx.Add(ev);
            _fxOpen.Add(ev);
            return shortFeedback ? new FxClose(this, ev) : new FxClose(this, ev, s, FxStatesNow(s));
        }

        /// <summary>
        /// 站位上每名角色此刻已生效的状态名（与战场角标同一组判断，用全名）：出手开始时抄一份，结束时比对，
        /// 新出现的就是这次出手真实施加的状态（S14 Q573“实际已生效状态名”）。只读。
        /// </summary>
        private Dictionary<string, List<string>> FxStatesNow(GameState s)
        {
            var r = new Dictionary<string, List<string>>();
            var b = s.Battle;
            if (b == null) return r;
            foreach (var en in b.Enemies) if (en.Alive) r[BattleFxUnit.EnemySide + ":e" + en.Index] = FxStateNames(s, FxFoe(en));
            foreach (var id in AlliesOnField(b)) r[BattleFxUnit.AllySide + ":" + id] = FxStateNames(s, FxAlly(s, id));
            return r;
        }

        /// <summary>一名角色此刻已生效的战斗状态名（战场角标同源）。</summary>
        public List<string> FxStateNames(GameState s, BattleFxUnit u)
        {
            var list = new List<string>();
            var b = s.Battle;
            if (b == null) return list;
            if (!u.IsAlly)
            {
                BattleEnemy? en = null;
                foreach (var x in b.Enemies) if ("e" + x.Index == u.Id) en = x;
                if (en == null || !en.Alive) return list;
                if (en.Guard) list.Add("防御");
                if (en.ProtectingIndex > 0) list.Add("保护");
                if (en.Stagger) list.Add("失衡");
                if (en.SlowActive || en.SlowPending) list.Add("减速");
                if (en.BleedTriggers > 0) list.Add("出血");
                if (EnemyMarked(b, en)) list.Add("标记");
                if (en.ArmorBreak > 0 && en.ArmorBreakUntilRound >= b.Round) list.Add("破甲");
                if (EnemyWeak(b, en)) list.Add("虚弱");
                return list;
            }
            if (!AllyInBattle(b, u.Id)) return list;
            using (FocusOn(s, u.Id))
            {
                if (b.PlayerGuard) list.Add(b.PlayerGuardSourceId != null ? "盾格挡" : "防御");
                if (b.BraceCharges > 0) list.Add("架矛");
                if (b.CounterFactor > 0) list.Add("反击架势");
                if (AxeChargeOn(b)) list.Add("蓄力");
                if (b.PlayerStagger) list.Add("失衡");
                if (b.PlayerSlowActive || b.PlayerSlowPending) list.Add("减速");
                if (PlayerArmorBroken(b)) list.Add("破甲");
                if (PlayerWeakActive(b)) list.Add("虚弱");
                if (s.Body.BleedTriggers > 0) list.Add("出血");
                if (s.Body.Infected) list.Add("感染");
            }
            return list;
        }

        /// <summary>出手结束：每名承受者新生效的状态名挂到他本次最后一段上（续段一并算）。</summary>
        private void FxStampStates(GameState s, BattleFxEvent ev, Dictionary<string, List<string>> before)
        {
            var last = new Dictionary<string, BattleFxResult>();
            foreach (var x in _fx)
                if (x == ev || x.ContinuesSeq == ev.Seq)
                    foreach (var r in x.Results) last[r.Target.Side + ":" + r.Target.Id] = r;
            foreach (var kv in last)
            {
                if (kv.Value.Down) continue;
                var now = FxStateNames(s, kv.Value.Target);
                before.TryGetValue(kv.Key, out var was);
                foreach (var n in now) if (was == null || !was.Contains(n)) kv.Value.States.Add(n);
            }
        }

        /// <summary>BM7a（S14 Q631、逐字稿 Q629—Q636 表「调息短反馈」）：调息成功的动作短反馈原句「{人物名}喘了口气」。</summary>
        public static string BreatheShortText(string who) => $"{who}喘了口气";

        /// <summary>短反馈（R03 §Q571：防御、调息、调整站位、撤退、装填、整队；敌方非攻击意图；支撑护带包）。只记行动者，不造数值。</summary>
        private void FxShort(GameState s, string name, BattleFxUnit actor)
        {
            using (FxBegin(s, name, BattleFxMotion.Short, actor, shortFeedback: true)) { }
        }

        /// <summary>敌人这一步实际执行的动作名（意图正式名去掉括注与数值，与执行时的战报动词同源）；只在执行时调用，不预告未来计划。</summary>
        private string FxEnemyActName(BattleEnemy e, BattleState b)
        {
            var label = IntentLabel(e, b);
            var cut = label.IndexOfAny(new[] { ' ', '（', '，' });
            return cut > 0 ? label.Substring(0, cut) : label;
        }

        /// <summary>
        /// 当前出手的一段结果（命中／暴击／未中／护甲挡住／无伤效果／治疗）。子事件（反击、迎击）已插在本出手之后又继续出段时，
        /// 后续段记进同一出手的续段（不重播整套镜头，界面接着原出手显示）。
        /// </summary>
        private void FxHit(BattleFxUnit target, string outcome, double amount = 0, bool guarded = false, bool down = false)
        {
            if (_fxOpen.Count == 0) return;
            var ev = _fxOpen[_fxOpen.Count - 1];
            if (_fx.Count > 0 && _fx[_fx.Count - 1] != ev && !ev.Short)
            {
                var cont = new BattleFxEvent
                {
                    Seq = ++_fxSeq,
                    BattleId = ev.BattleId,
                    Name = ev.Name,
                    Motion = ev.Motion,
                    Actor = ev.Actor,
                    NoNumbers = ev.NoNumbers,
                    ParentSeq = ev.ParentSeq,
                    ContinuesSeq = ev.RootSeq,
                    View = _fxLastClose,   // B47b（Q617）：插进来的子事件结束时的显示
                };
                _fx.Add(cont);
                _fxOpen[_fxOpen.Count - 1] = cont;
                ev = cont;
            }
            var r = new BattleFxResult { Target = target, Outcome = outcome, Amount = amount, Guarded = guarded, Down = down };
            ev.Results.Add(r);
            // B47b：这次结果之前刚记下的同一承受者生命变化（受伤／回血入口先记、出手结果随后写）并到这一段
            var root = FxRootOf(ev);
            for (var i = 0; i < root.LifeMarks.Count; i++)
            {
                var m = root.LifeMarks[i];
                if (m.Unit.Side != target.Side || m.Unit.Id != target.Id) continue;
                FxPutLife(r, m.Life, m.Dead);
                root.LifeMarks.RemoveAt(i);
                break;
            }
        }

        /// <summary>B47b：续段所属的根出手（生命变化记在根上，出手结束时统一并入）。</summary>
        private BattleFxEvent FxRootOf(BattleFxEvent ev)
        {
            if (ev.ContinuesSeq == null) return ev;
            foreach (var x in _fx) if (x.Seq == ev.ContinuesSeq) return x;
            return ev;
        }

        private static void FxPutLife(BattleFxResult r, string? life, bool dead)
        {
            if (dead) { r.Down = true; r.Life = null; return; }
            if (r.Down) return;
            // 同一段先入濒死、又抵抗：转姿态仍是“濒死”，抵抗另列（Q691：抵抗成功没有新姿态，附在已有正伤表现上）
            if (r.Life == BattleFxLife.Dying && life == BattleFxLife.Resisted) { r.Changes.Add(LifeWord(BattleFxLife.Resisted)); return; }
            r.Life = life;
        }

        /// <summary>出手结束：还没并入的生命变化挂到承受者本出手最后一段；本出手里没有他的结果就在最后一段补一条。</summary>
        private void FxApplyLifeMarks(BattleFxEvent ev)
        {
            if (ev.LifeMarks.Count == 0) return;
            BattleFxEvent seg = ev;
            foreach (var x in _fx) if (x == ev || x.ContinuesSeq == ev.Seq) seg = x;
            foreach (var m in ev.LifeMarks)
            {
                BattleFxResult? last = null;
                foreach (var x in _fx)
                    if (x == ev || x.ContinuesSeq == ev.Seq)
                        foreach (var r in x.Results) if (r.Target.Side == m.Unit.Side && r.Target.Id == m.Unit.Id) last = r;
                if (last == null)
                {
                    last = new BattleFxResult { Target = m.Unit, Outcome = m.Amount > 0 ? "hit" : "effect", Amount = m.Amount };
                    seg.Results.Add(last);
                }
                FxPutLife(last, m.Life, m.Dead);
            }
            ev.LifeMarks.Clear();
        }

        /// <summary>B47b（R09 §8、Q691、Q693；逐字稿 B47b 表）：生命结果字。确死为“死亡”。</summary>
        public static string LifeWord(string? life) => life switch
        {
            BattleFxLife.Dying => "濒死",
            BattleFxLife.Resisted => "抵抗致死",
            BattleFxLife.Revived => "脱离濒死",
            _ => DeathWord,
        };

        /// <summary>B47b（Q693）：敌我确死的特写结果字。</summary>
        public const string DeathWord = "死亡";

        /// <summary>B47b（R09 Q691、逐字稿 B47b 表）：无公开伤因的独立生命特写兜底标题。</summary>
        public const string LifeFallbackTitle = "生命状态变化";

        /// <summary>B47b（R09 Q694、逐字稿 B47b 表）：支援真实解除。</summary>
        public static string RemovedText(string status) => $"解除{status}";

        /// <summary>B47b（Q694）：支援真实减恐慌（取真实前后差，沿 Fmt.N 精度）。</summary>
        public static string PanicCutText(double amount) => $"恐慌−{Fmt.N(amount)}";

        /// <summary>B47b（Q694）：协同真实未来奖励。</summary>
        public static string NextApText(int amount) => $"下次行动点+{amount}";

        /// <summary>B47b（R09 Q692、逐字稿 B47b 表）：远处生命特写标识行“{人物} · {地点}”；缺可信地点只写人物名。</summary>
        public static string LifeWhereText(string who, string? where) => string.IsNullOrEmpty(where) ? who : $"{who} · {where}";

        /// <summary>
        /// B47b（R03 §Q615）：支援／增益特写——行动者与实际受益者（同一人只一条，全队一段）。返回的对象到写完本条战报再结束（战报原句归到这一段）。
        /// 没有实际受益者时退回原行动者短反馈。
        /// </summary>
        private IDisposable FxSupport(GameState s, string name, BattleFxUnit actor, IEnumerable<BattleFxUnit> beneficiaries)
        {
            var list = new List<BattleFxUnit>();
            foreach (var u in beneficiaries) if (!list.Exists(x => x.Side == u.Side && x.Id == u.Id)) list.Add(u);
            if (list.Count == 0) { FxShort(s, name, actor); return new FxClose(this, null); }
            var scope = FxBegin(s, name, BattleFxMotion.Support, actor);
            foreach (var u in list) FxHit(u, "effect");
            return scope;
        }

        /// <summary>B47b（Q694）：给本出手里某承受者的最后一段加一个真实变化结果字；moved 标真实换位（只做动作）。</summary>
        private void FxChange(BattleFxUnit target, string text, bool moved = false)
        {
            if (_fxOpen.Count == 0) return;
            BattleFxResult? last = null;
            var root = _fxOpen[_fxOpen.Count - 1].RootSeq;
            foreach (var x in _fx)
                if (x.RootSeq == root)
                    foreach (var r in x.Results) if (r.Target.Side == target.Side && r.Target.Id == target.Id) last = r;
            if (last == null) return;
            if (text != "") last.Changes.Add(text);
            if (moved) last.Moved = true;
        }

        // ───────────── B47b 生命姿态变化（R09 §7、Q691—Q693） ─────────────

        /// <summary>B47b：独立生命特写的来源名作用域（null 为没有公开来源，用兜底标题）。</summary>
        private string? _lifeCause;
        private bool _lifeCauseSet;
        /// <summary>B47b（Q732 待答复）：本批不做独立生命特写的来源（多原因合成身体损伤、搜集／勘整划伤）。</summary>
        private bool _lifeCauseSkip;

        private sealed class LifeCauseScope : IDisposable
        {
            private readonly GameEngine _e;
            private readonly string? _cause; private readonly bool _set, _skip;
            public LifeCauseScope(GameEngine e) { _e = e; _cause = e._lifeCause; _set = e._lifeCauseSet; _skip = e._lifeCauseSkip; }
            public void Dispose() { _e._lifeCause = _cause; _e._lifeCauseSet = _set; _e._lifeCauseSkip = _skip; }
        }

        /// <summary>接下来的受伤若造成生命姿态转变而又没有出手镜头，独立特写标题用这个真实公开来源名（“出血”或原伤因）。</summary>
        private IDisposable LifeCause(string? cause)
        {
            var scope = new LifeCauseScope(this);
            _lifeCause = string.IsNullOrEmpty(cause) ? null : cause;
            _lifeCauseSet = true;
            _lifeCauseSkip = false;
            return scope;
        }

        /// <summary>B47b（Q732 待答复）：接下来的受伤不做独立生命特写（只写战报／日志）。</summary>
        private IDisposable NoLifeCloseUp()
        {
            var scope = new LifeCauseScope(this);
            _lifeCauseSkip = true;
            return scope;
        }

        private readonly List<BattleFxEvent> _lifeWorld = new List<BattleFxEvent>();

        /// <summary>
        /// B47b（R09 Q692）：取走战斗外已提交的生命姿态转变镜头（在场与远处候补／驻屋者；按真实发生顺序）。
        /// 网页壳在世界推进段提交后的原安全边界逐段播放；只读，不再结算。
        /// </summary>
        public List<BattleFxEvent> DrainLifeFx()
        {
            var r = new List<BattleFxEvent>(_lifeWorld);
            _lifeWorld.Clear();
            return r;
        }

        /// <summary>
        /// B47b（R09 §7、Q691、Q692、Q735）：一次真实生命姿态变化（受伤入口与医疗回血入口调用）。
        /// <list type="bullet">
        /// <item>战斗中、此人在场且正在记一次出手（攻击、医疗、喷溅）：并入该出手的结果帧，不另播。</item>
        /// <item>战斗中在场、没有出手镜头（出血、回合结算）：单独一段生命特写，标题取真实来源名（缺失为“生命状态变化”）；抵抗成功没有新姿态，不播。</item>
        /// <item>战斗中不在场（远处候补、驻屋者、没入场的同行者）：只写战报与日志（Q735 待答复）。</item>
        /// <item>战斗外：记一段只读生命镜头（“{人物} · {地点}”），由网页壳在段边界播放；医疗脱离濒死（Q733）、多原因合成伤害与划伤（Q732）本批不做。</item>
        /// </list>
        /// 只读预估副本不记。
        /// </summary>
        private void NoteLife(GameState s, ActorState actor, string? life, bool dead, double amount, GameState? before)
        {
            if (s.SimCopy) return;
            var unit = new BattleFxUnit { Side = BattleFxUnit.AllySide, Id = actor.Id, Name = actor.IsPlayer ? "你" : ActorDisplayName(s, actor.Id) };
            if (s.Battle != null)
            {
                if (!AllyInBattle(s.Battle, actor.Id) && !(dead && s.Battle.Allies.Contains(actor.Id))) return;
                if (_fxOpen.Count > 0)
                {
                    FxRootOf(_fxOpen[_fxOpen.Count - 1]).LifeMarks.Add((unit, life, dead, amount));
                    return;
                }
                if (life == BattleFxLife.Resisted || life == BattleFxLife.Revived || _lifeCauseSkip) return;
                using (FxBegin(s, _lifeCause ?? LifeFallbackTitle, BattleFxMotion.Life, unit, view: before))
                {
                    FxHit(unit, amount > 0 ? "hit" : "effect", amount, down: dead);
                    if (!dead) _fxOpen[_fxOpen.Count - 1].Results[^1].Life = life;
                    _fxOpen[_fxOpen.Count - 1].Lines.Add(dead ? FailedResistText(unit.Name) : EnterDyingText(unit.Name));
                }
                return;
            }
            if (life == BattleFxLife.Resisted || life == BattleFxLife.Revived || _lifeCauseSkip) return;
            var where = s.Party.Contains(actor.Id) || actor.IsPlayer ? LocName(s.CurrentLocation)
                : RosterOf(s, actor.Id) is RosterEntry re ? ReserveWhereName(s, re) : null;
            var ev = new BattleFxEvent
            {
                Seq = ++_fxSeq,
                Name = _lifeCause ?? LifeFallbackTitle,
                Motion = BattleFxMotion.Life,
                Actor = unit,
                World = true,
                Where = LifeWhereText(unit.Name, where),
            };
            ev.Results.Add(new BattleFxResult { Target = unit, Outcome = amount > 0 ? "hit" : "effect", Amount = amount, Down = dead, Life = dead ? null : life });
            ev.Lines.Add(dead ? FailedResistText(unit.Name) : EnterDyingText(unit.Name));
            if (_lifeWorld.Count >= 200) _lifeWorld.RemoveRange(0, _lifeWorld.Count - 100);   // 没人取走（测试、离线推进）时不无限增长
            _lifeWorld.Add(ev);
        }

        /// <summary>同一段内的倒地补记（结算途中目标倒下时，把它最后一段标成倒地）。</summary>
        private void FxMarkDown(BattleFxUnit target)
        {
            if (_fxOpen.Count == 0) return;
            var ev = _fxOpen[_fxOpen.Count - 1];
            for (var i = ev.Results.Count - 1; i >= 0; i--)
                if (ev.Results[i].Target.Side == target.Side && ev.Results[i].Target.Id == target.Id) { ev.Results[i].Down = true; return; }
        }

        /// <summary>战报行归到正在记的那一次出手（特写底部显示同一句原文，不另写新句）。</summary>
        private void FxLine(string text)
        {
            if (_fxOpen.Count > 0) _fxOpen[_fxOpen.Count - 1].Lines.Add(text);
        }

        private BattleFxUnit FxAlly(GameState s, string id) =>
            new BattleFxUnit { Side = BattleFxUnit.AllySide, Id = id, Name = id == GameState.PlayerId ? "你" : ActorDisplayName(s, id) };

        private BattleFxUnit FxFoe(BattleEnemy e) =>
            new BattleFxUnit { Side = BattleFxUnit.EnemySide, Id = "e" + e.Index, Name = EnemyName(e), EnemyId = e.EnemyId };

        /// <summary>武器是不是远程（弓弩、枪械）：特写走 ranged（保持站位、弹迹），其余走 melee。</summary>
        public static bool RangedWeapon(WeaponConfig w) => w.WeaponKind is "archery" or "firearms";
    }
}

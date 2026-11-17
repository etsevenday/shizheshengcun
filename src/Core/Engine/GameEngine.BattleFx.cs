using System;
using System.Collections.Generic;
using System.Linq;
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
        /// <summary>BM11（R280，R03 §Q617）：本次指令提交前行动者的 AP；本条指令记下的第一条出手取走它（见 <see cref="FxBegin"/>）。</summary>
        private (string Actor, int Ap)? _fxCmdAp;

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
            // BM11（R280，R03 §Q617）：本次指令真实扣 AP 在第一段出手开始之前；这条指令的第一段全屏出手的开始快照把行动者 AP 还原成提交前值，
            // AP 与 HP 一样到该段结果帧才追齐（续段、子事件不再改）。第一条记录是短反馈的（防御、装填等）沿原规则立即显示真实 AP。
            if (_fxCmdAp is { } pre)
            {
                _fxCmdAp = null;
                if (!shortFeedback && _fxOpen.Count == 0 && ev.View?.Battle is BattleState vb && vb.BattleId == b.BattleId) vb.Fighter(pre.Actor).Ap = pre.Ap;
            }
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
        private void FxShort(GameState s, string name, BattleFxUnit actor, BattleFxAmmoHold? ammoHold = null)
        {
            using (FxBegin(s, name, BattleFxMotion.Short, actor, shortFeedback: true))
                if (ammoHold != null && _fxOpen.Count > 0) _fxOpen[_fxOpen.Count - 1].AmmoHold = ammoHold;
        }

        /// <summary>
        /// BM11（Q919 选 B，R03／S14 文首 2026-11-17 Q918—Q920 节）：成功装填／快速装填的短反馈带上这把武器装填前的弹仓数，
        /// 界面在本次短反馈期间按它显示、短反馈结束才追齐真实值。真实装弹、扣弹药、AP 与战报照原事务提交。装填前后弹数没变（未实际装填）不带。
        /// </summary>
        private static BattleFxAmmoHold? AmmoHoldOf(ItemInstance wi, int loadedBefore) =>
            wi.LoadedAmmo == loadedBefore ? null : new BattleFxAmmoHold { WeaponInstanceId = wi.InstanceId, LoadedBefore = loadedBefore };

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
            BattleFxLife.Zombie => ZombieWord,   // B52b（R09 Q732）
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

        // ───────────── B52b（R03 文首 B47b Q729—Q738 §Q729／§Q731；逐字稿同批表 accuracy…charged、mark、watch） ─────────────

        /// <summary>Q729：真实命中能力增益（鼓舞等）。</summary>
        public const string GainAccuracy = "命中强化";
        /// <summary>Q729：真实伤害倍率增益（总攻令、敌方增益／鼓动／低吟）。</summary>
        public const string GainDamage = "伤害强化";
        /// <summary>Q729：掩护／坚守给实际受益者的下一次直接伤害倍率（原规则也给自身的同类效果）。</summary>
        public const string GainCover = "减伤";
        /// <summary>Q729：我方／敌方保护的真实被保护对象取得保护关系（给被保护者，不给施术者）。</summary>
        public const string GainProtected = "受保护";
        /// <summary>Q729：支撑护带包、稳固／坚守等真实位移抵抗增量或有效续期。</summary>
        public const string GainDisplace = "位移抵抗提升";
        /// <summary>Q729：敌方蓄力（原无现成状态字）实际完成。</summary>
        public const string GainCharged = "蓄力完成";
        /// <summary>Q729：已有状态之后六个短名按此表稳定排列。</summary>
        private static readonly string[] GainOrder = { GainAccuracy, GainDamage, GainCover, GainProtected, GainDisplace, GainCharged };

        /// <summary>Q731：敌方“标记／守卫标记”真实执行后在行动敌人旁的短反馈。</summary>
        public const string EnemyMarkShort = "标记";
        /// <summary>Q731：敌方“警戒观察”（含追击分支真正执行观察）真实执行后的短反馈。</summary>
        public const string EnemyWatchShort = "警戒观察";

        /// <summary>
        /// Q729：一项有时限的增益在本次提交前后是否真实新增、强化或有效续期（按提交前后快照；0 变化不算）。
        /// <paramref name="lowerIsStronger"/> 为倍率越小越强（减伤 ×0.8）；未生效时按无效果（加值 0、倍率 1）比较。
        /// </summary>
        private static bool BuffGained(double v0, int until0, double v1, int until1, int round, bool lowerIsStronger = false)
        {
            var none = lowerIsStronger ? 1.0 : 0.0;
            var on0 = until0 >= round && (lowerIsStronger ? v0 < 1 - 1e-9 : v0 > 1e-9);
            var on1 = until1 >= round && (lowerIsStronger ? v1 < 1 - 1e-9 : v1 > 1e-9);
            if (!on1) return false;
            var e0 = on0 ? v0 : none;
            if (lowerIsStronger ? v1 < e0 - 1e-9 : v1 > e0 + 1e-9) return true;
            return !on0 || until1 > until0;
        }

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
            // B52b（Q729）：同一效果只一份；Q694 原结果字在前，六个增益短名之后按表稳定排列
            if (text != "" && !last.Changes.Contains(text))
            {
                last.Changes.Add(text);
                var sorted = last.Changes.OrderBy(c => Array.IndexOf(GainOrder, c)).ToList();
                last.Changes.Clear();
                last.Changes.AddRange(sorted);
            }
            if (moved) last.Moved = true;
        }

        // ───────────── B47b 生命姿态变化（R09 §7、Q691—Q693） ─────────────

        /// <summary>B47b：独立生命特写的来源名作用域（null 为没有公开来源，用兜底标题）。</summary>
        private string? _lifeCause;
        private bool _lifeCauseSet;
        /// <summary>B52b（R09 Q733）：战斗外这次实际治疗的公开名（物品、服务或技能名）；null 为不在战外治疗事务里。</summary>
        private string? _lifeHeal;

        private sealed class LifeCauseScope : IDisposable
        {
            private readonly GameEngine _e;
            private readonly string? _cause, _heal; private readonly bool _set;
            public LifeCauseScope(GameEngine e) { _e = e; _cause = e._lifeCause; _set = e._lifeCauseSet; _heal = e._lifeHeal; }
            public void Dispose() { _e._lifeCause = _cause; _e._lifeCauseSet = _set; _e._lifeHeal = _heal; }
        }

        /// <summary>接下来的受伤若造成生命姿态转变而又没有出手镜头，独立特写标题用这个真实公开来源名（“出血”或原伤因）。</summary>
        private IDisposable LifeCause(string? cause)
        {
            var scope = new LifeCauseScope(this);
            _lifeCause = string.IsNullOrEmpty(cause) ? null : cause;
            _lifeCauseSet = true;
            return scope;
        }

        /// <summary>
        /// B52b（R09 Q733）：接下来的战斗外医疗回血若使濒死者脱离濒死，独立生命镜头标题用这次实际物品／服务／技能的公开名
        /// （与同次完成日志同源）。战斗中不用（战斗医疗已有同次医疗特写合入）。
        /// </summary>
        private IDisposable LifeHeal(string name)
        {
            var scope = new LifeCauseScope(this);
            _lifeHeal = name;
            return scope;
        }

        // ── B52b（R09 文首 B47b Q729—Q738 §Q732；逐字稿同批 multiCause／search／survey／zombieTitle／zombieResult） ──

        /// <summary>Q732：多个真实原因合成的一次身体损伤“多原因伤害：{原因清单}”（顿号连接）。</summary>
        public static string MultiCauseTitle(IEnumerable<string> causes) => $"多原因伤害：{string.Join("、", causes)}";

        /// <summary>Q732：搜集划伤“搜索：{伤因短名}”。</summary>
        public static string SearchLifeTitle(string label) => $"搜索：{HazardShortName(label)}";

        /// <summary>Q732：勘整风险“勘整：{伤因短名}”。</summary>
        public static string SurveyLifeTitle(string label) => $"勘整：{HazardShortName(label)}";

        /// <summary>Q732：尸变的生命镜头标题。</summary>
        public const string ZombieLifeTitle = "尸变：病毒";

        /// <summary>Q732：尸变的结果字。</summary>
        public const string ZombieWord = "尸变";

        /// <summary>Q732：伤因短名取本次公开 hazard.Label，去掉重复的“搜集时／搜索时／勘整时”前缀。</summary>
        public static string HazardShortName(string label)
        {
            foreach (var p in new[] { "搜集时", "搜索时", "勘整时" })
                if (label.StartsWith(p, StringComparison.Ordinal)) return label.Substring(p.Length);
            return label;
        }

        /// <summary>Q732：世界段身体损伤的生命特写标题——一个原因用原名，多个用“多原因伤害：…”；没有为 null（兜底标题）。</summary>
        private static string? DamageLifeTitle(List<string> causes) =>
            causes.Count == 0 ? null : causes.Count == 1 ? causes[0] : MultiCauseTitle(causes);

        /// <summary>Q732：本段原因里小时率确为负（真实贡献正伤）的那些，保持原顺序、去重；0 伤害原因不列。</summary>
        private static List<string> RealDamageCauses(List<string> causes, double satiety, double hydration, double wakefulness, double energy)
        {
            var rate = new Dictionary<string, double> { ["饥饿"] = satiety, ["缺水"] = hydration, ["缺乏睡眠"] = wakefulness, ["体力透支"] = energy };
            return causes.Where(c => rate.TryGetValue(c, out var r) && r < 0).Distinct().ToList();
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
        /// <item>战斗外：记一段只读生命镜头（“{人物} · {地点}”），由网页壳在段边界播放；B52b：多原因、搜集／勘整划伤按 Q732 标题，
        /// 战外用药／换药服务脱离濒死（Q733）标题为实际治疗名、结果“脱离濒死”。</item>
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
                    // 脱离濒死的回血量已由同次医疗结果段写出（“+{数值}”），这里只并姿态
                    FxRootOf(_fxOpen[_fxOpen.Count - 1]).LifeMarks.Add((unit, life, dead, life == BattleFxLife.Revived ? 0 : amount));
                    return;
                }
                if (life == BattleFxLife.Resisted || life == BattleFxLife.Revived) return;
                using (FxBegin(s, _lifeCause ?? LifeFallbackTitle, BattleFxMotion.Life, unit, view: before))
                {
                    FxHit(unit, amount > 0 ? "hit" : "effect", amount, down: dead);
                    if (!dead) _fxOpen[_fxOpen.Count - 1].Results[^1].Life = life;
                    _fxOpen[_fxOpen.Count - 1].Lines.Add(dead ? FailedResistText(unit.Name) : EnterDyingText(unit.Name));
                }
                return;
            }
            if (life == BattleFxLife.Resisted) return;
            if (life == BattleFxLife.Revived)
            {
                // B52b（R09 Q733）：战斗外合法用药／换药服务真正使濒死者成为正常活人——标题为这次实际治疗名，结果“脱离濒死”，
                // 真实正健康差才写“+{实际恢复量}”；不在战外治疗事务里（没有公开名）的不造镜头
                if (_lifeHeal == null) return;
                AddWorldLife(s, actor, unit, _lifeHeal, new BattleFxResult { Target = unit, Outcome = amount > 0 ? "heal" : "effect", Amount = amount, Life = life }, LeaveDyingText(unit.Name));
                return;
            }
            AddWorldLife(s, actor, unit, _lifeCause ?? LifeFallbackTitle,
                new BattleFxResult { Target = unit, Outcome = amount > 0 ? "hit" : "effect", Amount = amount, Down = dead, Life = dead ? null : life },
                dead ? FailedResistText(unit.Name) : EnterDyingText(unit.Name));
        }

        /// <summary>B47b（R09 Q692）：记一段战斗外只读生命镜头（标识行“{人物} · {地点}”取事件提交时的位置），由网页壳在段边界播放。</summary>
        private void AddWorldLife(GameState s, ActorState actor, BattleFxUnit unit, string name, BattleFxResult result, string? line)
        {
            var where = s.Party.Contains(actor.Id) || actor.IsPlayer ? LocName(s.CurrentLocation)
                : RosterOf(s, actor.Id) is RosterEntry re ? ReserveWhereName(s, re) : null;
            var ev = new BattleFxEvent
            {
                Seq = ++_fxSeq,
                Name = name,
                Motion = BattleFxMotion.Life,
                Actor = unit,
                World = true,
                Where = LifeWhereText(unit.Name, where),
            };
            ev.Results.Add(result);
            if (line != null) ev.Lines.Add(line);
            if (_lifeWorld.Count >= 200) _lifeWorld.RemoveRange(0, _lifeWorld.Count - 100);   // 没人取走（测试、离线推进）时不无限增长
            _lifeWorld.Add(ev);
        }

        /// <summary>
        /// B52b（R09 Q732）：同伴（同行或候补）在世界段达到原尸变条件、真实尸变时记一段只读生命镜头：标题“尸变：病毒”、结果“尸变”。
        /// 主角尸变直接终局（终局优先，不播）；只读预估副本与战斗中不记。必须在离队（CompanionDown）之前调用，地点取尸变时的位置。
        /// </summary>
        private void NoteZombieLife(GameState s, ActorState actor)
        {
            if (s.SimCopy || s.Battle != null || actor.IsPlayer) return;
            var unit = new BattleFxUnit { Side = BattleFxUnit.AllySide, Id = actor.Id, Name = ActorDisplayName(s, actor.Id) };
            AddWorldLife(s, actor, unit, ZombieLifeTitle, new BattleFxResult { Target = unit, Outcome = "effect", Life = BattleFxLife.Zombie }, null);
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

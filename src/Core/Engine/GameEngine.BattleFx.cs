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

        /// <summary>取走自上次以来记下的出手事件（按真实发生顺序；反击／迎击插在触发处）。界面每次提交或每一步之后调用一次。</summary>
        public List<BattleFxEvent> DrainBattleFx()
        {
            var r = new List<BattleFxEvent>(_fx);
            _fx.Clear();
            _fxOpen.Clear();
            return r;
        }

        private sealed class FxClose : IDisposable
        {
            private readonly GameEngine _e;
            private readonly BattleFxEvent? _ev;
            public FxClose(GameEngine e, BattleFxEvent? ev) { _e = e; _ev = ev; }
            public void Dispose()
            {
                if (_ev == null) return;
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
        private IDisposable FxBegin(GameState s, string motion, BattleFxUnit actor, bool shortFeedback = false, bool noNumbers = false)
        {
            var b = s.Battle;
            if (b == null) return new FxClose(this, null);
            var ev = new BattleFxEvent
            {
                Seq = ++_fxSeq,
                BattleId = b.BattleId,
                Motion = motion,
                Actor = actor,
                Short = shortFeedback,
                NoNumbers = noNumbers,
                ParentSeq = _fxOpen.Count > 0 ? _fxOpen[_fxOpen.Count - 1].RootSeq : (int?)null,
            };
            if (_fx.Count >= 400) _fx.RemoveRange(0, _fx.Count - 300);   // 没人取走（测试、离线推进）时不无限增长
            _fx.Add(ev);
            _fxOpen.Add(ev);
            return new FxClose(this, ev);
        }

        /// <summary>短反馈（R03 §Q571：防御、调息、调整站位、撤退、装填、整队；敌方非攻击意图；支撑护带包）。只记行动者，不造数值。</summary>
        private void FxShort(GameState s, BattleFxUnit actor)
        {
            using (FxBegin(s, BattleFxMotion.Short, actor, shortFeedback: true)) { }
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
                    Motion = ev.Motion,
                    Actor = ev.Actor,
                    NoNumbers = ev.NoNumbers,
                    ParentSeq = ev.ParentSeq,
                    ContinuesSeq = ev.RootSeq,
                };
                _fx.Add(cont);
                _fxOpen[_fxOpen.Count - 1] = cont;
                ev = cont;
            }
            ev.Results.Add(new BattleFxResult { Target = target, Outcome = outcome, Amount = amount, Guarded = guarded, Down = down });
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

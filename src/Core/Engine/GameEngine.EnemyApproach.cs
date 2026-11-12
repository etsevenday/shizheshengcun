using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B41b 新版敌踪的接近时限（R05「Q416、Q421 接近时间与到期入战」「B41b Q439—Q444」；逐字稿「卡片、操作盘与倒计时」；开发记录 B41b）。
    /// 全部挂在 <see cref="CardsV2"/> 后面：开关 encounters.enemyCardsV2 关着时不抽、不扣、不开战，旧版流程原样。
    /// - 计时卡：普通敌对单卡、随机组卡、固定战（含守卫、检查哨）、教学首战卡。可选强敌不倒计时（Q419），普通中立单卡不抽不计时（Q439），
    ///   首战未胜时教学卡以外的卡不抽不计时（Q421）。
    /// - 每次现身首次与队伍同地同区且可操作时，从 encounters.approachMinutes 等概率抽一次并保存；读档、刷新、巡游、离开返回、撤退都不重抽。
    /// - 队伍与卡同地点同区、非战斗、真实推进一个 5 世界分钟段时减 5，最低 0；移动（含撤出）开始即视为离开起点，起点冻结、途中不扣。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>这一局此刻要按 B41b 结算敌踪接近（新版敌踪、战斗开放、不是模拟／冻结的副本）。</summary>
        private bool ApproachLive(GameState s) => CardsV2(s) && Config.CombatEnabled && !s.SuppressEnemyApproach;

        /// <summary>
        /// 这张卡按接近时限计时（Q416、Q419、Q421、Q439）：可选强敌、受首战门控的卡、普通中立单卡都不计时。
        /// 固定战、守卫、检查哨与教学卡按真实遭遇身份计时（关卡身份不因敌种中立而解除）。
        /// </summary>
        public bool CardTimed(EnemyCardView card) =>
            !card.Optional && !card.TutorialGated && !(card.Kind == "single" && !card.KeyEnemy && card.Members.Count > 0 && !card.Members[0].Hostile);

        /// <summary>某张卡的时限／保护记录（只读）；没有为 null。</summary>
        public EnemyCardTimer? CardTimer(GameState s, string cardKey) =>
            s.EnemyCards.Timers.TryGetValue(cardKey, out var t) ? t : null;

        private EnemyCardTimer TimerFor(GameState s, string cardKey)
        {
            if (!s.EnemyCards.Timers.TryGetValue(cardKey, out var t))
            {
                t = new EnemyCardTimer();
                s.EnemyCards.Timers[cardKey] = t;
            }
            return t;
        }

        /// <summary>队伍正在移动（地图移动或地牢撤出）：已视为离开起点，起点敌踪冻结、途中不拦截（Q416）。</summary>
        private static bool PartyMoving(GameState s) =>
            s.Main != null && (s.Main.Type == ActionType.Travel || s.Main.Type == ActionType.Extract);

        /// <summary>
        /// 首次可操作现身时抽接近时间（Q416、Q425、Q439）：当前地点、当前区域可操作的计时卡里还没抽过的，各按 encounters.approachMinutes 等概率抽一次。
        /// 移动途中、战斗中、终局时不抽。已有记录（含已到期、只有撤退保护的）不重抽。
        /// </summary>
        private void DrawCardTimers(GameState s)
        {
            if (!ApproachLive(s) || s.Progress.Dead || s.Battle != null || PartyMoving(s)) return;
            var opts = Config.Encounters.ApproachMinutes;
            foreach (var card in EnemyCardsHere(s))
            {
                if (!CardTimed(card)) continue;
                var t = TimerFor(s, card.Key);
                if (t.Left != null) continue;
                t.Left = opts[(int)Math.Min(opts.Count - 1, Math.Floor(s.Rng.NextDouble() * opts.Count))];
                if (t.Left <= 0) { t.Left = 0; t.DueMinute = s.GameMinute; }
            }
        }

        /// <summary>
        /// 一个非战斗 5 分钟段推进后的倒计时（Q416、Q441、Q467）：队伍与卡同地点同区、没在移动，已抽过的计时卡剩余减 5（最低 0），减到 0 记到期时刻。
        /// 菜单、阅读、离线、读档不经这里，不扣时。
        /// </summary>
        private void TickCardTimers(GameState s)
        {
            if (!ApproachLive(s) || PartyMoving(s)) return;
            foreach (var card in EnemyCardsHere(s))
            {
                if (!CardTimed(card) || !(CardTimer(s, card.Key) is EnemyCardTimer t) || t.Left == null || t.Left <= 0) continue;
                t.Left = Math.Max(0, t.Left.Value - Tick);
                if (t.Left == 0) t.DueMinute = s.GameMinute;
            }
        }

        /// <summary>卡已失效（敌人死亡、组散、单卡入组、固定战已胜）的时限／保护记录移除（Q442：单卡入新组后其撤退保护随之失效）。</summary>
        private void PruneCardTimers(GameState s)
        {
            if (s.EnemyCards.Timers.Count == 0) return;
            foreach (var key in s.EnemyCards.Timers.Keys.ToList())
                if (FindEnemyCard(s, key, out _) == null) s.EnemyCards.Timers.Remove(key);
        }

        /// <summary>
        /// 卡面接近状态句（逐字稿「卡片、操作盘与倒计时」，按可选挑战、首战门控、普通中立、有效保护、已到期、正常倒计时的顺序取一条）；
        /// 旧档历史保护与同卡撤退保护并存时取较晚截止的对应句。还没抽接近时间的计时卡为 null。界面接线在 B41e。
        /// </summary>
        public string? CardApproachStatus(GameState s, EnemyCardView card)
        {
            if (card.Optional) return "可选挑战，由你决定何时处理。";
            if (card.TutorialGated) return "首战胜利后开始接近计时。";
            if (!CardTimed(card)) return "中立，不会主动接近。";
            var t = CardTimer(s, card.Key);
            var guard = t?.GuardUntil is int g && g > s.GameMinute ? g : (int?)null;
            var legacy = s.EncounterGraceUntil > s.GameMinute ? s.EncounterGraceUntil : (int?)null;
            if (guard != null && (legacy == null || guard >= legacy)) return $"撤退保护剩余{Clock.Duration(guard.Value - s.GameMinute)}。";
            if (legacy != null) return $"旧存档接敌保护剩余{Clock.Duration(legacy.Value - s.GameMinute)}。";
            if (t?.Left == null) return null;
            return t.Left <= 0 ? "已到期，等待交战。" : $"距敌人接近{Clock.Duration(t.Left.Value)}";
        }

        /// <summary>
        /// 敌踪接近的检查边界（Q416 第 4 条：空闲、到达、强制对白结束、战后回到非战斗；以及每个行动段提交后）：
        /// 修剪失效卡的记录，给首次可操作现身的卡抽接近时间。
        /// </summary>
        private void EnemyApproachBoundary(GameState s)
        {
            if (!ApproachLive(s)) return;
            PruneCardTimers(s);
            DrawCardTimers(s);
        }
    }
}

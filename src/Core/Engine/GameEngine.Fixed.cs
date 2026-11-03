using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B20 R63 固定战（A09「E2 战斗通则与差异化环境」「E4 固定战逐节点名单」；R05「2026-10-30 B4 实例敌人、人口区与奖励账」、
    /// 「Q62—Q74」当前优先节；expansion-combat.json enemySets；gate-contract factProducers.checkpoint_obstruction_cleared）：
    /// - 首次揭示（本代第一次实际到达该节点）时按当时容量 C 截名单前 C 个，建固定世界敌人并锁存；之后扩容不重编、少带队员不缩怪、
    ///   读档/撤退不重抽，HP 持久（EC03、EC06、DR21）。
    /// - 公开敌踪，可离开；固定战敌人还在时本节点不能探索、清理翻找或采集（地牢房间：Q187 选A；m02 检查哨：Q193，280f8be 起不再例外）。明确接战全名单一起入场，禁增援（EC04）。
    /// - 最终胜利且主角存活：守卫写本代实例事实 guardianDefeated（守卫门开、守卫战利品箱出现），m02 写 campaign 事实
    ///   checkpoint_obstruction_cleared（营门路线开通）；本代/本局不再出现。地牢换代时旧代固定敌移除、下一代重新揭示。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string FixedPrefix = "fixed:";
        public const string ElitePrefix = "elite:";

        private Dictionary<string, FixedBattleConfig>? _fixedCfg;
        private Dictionary<string, FixedBattleConfig> FixedCfg => _fixedCfg ??= WE.FixedBattles.ToDictionary(f => f.NodeId);

        public FixedBattleConfig? FixedBattleAt(string nodeId) => FixedCfg.TryGetValue(nodeId, out var f) ? f : null;

        /// <summary>该固定战当前应属的代号：地牢按当前实例代号（没有实例为 0），campaign 为 0。</summary>
        private int FixedGeneration(GameState s, FixedBattleConfig f) =>
            f.IsCampaign || f.DungeonId == null ? 0 : s.DungeonInstances.TryGetValue(f.DungeonId, out var inst) ? inst.Generation : 0;

        /// <summary>本代（或本局）这个节点的固定战状态；还没揭示或属于旧代为 null。</summary>
        public FixedBattleState? FixedStateAt(GameState s, string nodeId)
        {
            var f = FixedBattleAt(nodeId);
            if (f == null || !s.Population.Fixed.TryGetValue(nodeId, out var st)) return null;
            return st.Generation == FixedGeneration(s, f) ? st : null;
        }

        /// <summary>某节点还活着的固定战敌人（按名单顺序；不含交战中的）。</summary>
        public List<WorldEnemy> FixedEnemiesAt(GameState s, string nodeId)
        {
            var st = FixedStateAt(s, nodeId);
            if (st == null || st.Won) return new List<WorldEnemy>();
            var key = FixedPrefix + nodeId;
            return st.WorldIds.Select(id => FindWorldEnemy(s, id)).Where(w => w != null && w.FixedOf == key && !w.Engaged).Select(w => w!).ToList();
        }

        /// <summary>
        /// 首次揭示：本代第一次实际到达有固定战的节点时按当时容量 C 截名单前 C 个、建固定世界敌人并锁存（A09 E2：“首次揭示后固定敌踪名单、
        /// worldEnemyId 和随机结果；按当时已解锁容量C截候选前C人”）。已揭示的不重建。返回给到达日志的短语。
        /// </summary>
        private void RevealFixedBattle(GameState s, string nodeId, List<string> notes)
        {
            var f = FixedBattleAt(nodeId);
            if (f == null || !Config.CombatEnabled) return;
            var gen = FixedGeneration(s, f);
            if (s.Population.Fixed.TryGetValue(nodeId, out var old) && old.Generation == gen) return;
            var c = BattleCapacity(s);
            var st = new FixedBattleState { NodeId = nodeId, DungeonId = f.DungeonId, Generation = gen, Capacity = c, RevealedMinute = s.GameMinute };
            // 它要写的事实本代/本局已经成立（开发入口记过“守卫已击败/检查哨路障已解决”，或旧档已有事实）：视为已结清，不再放敌人
            if (FixedFactMet(s, f))
            {
                st.Won = true;
                st.WonMinute = s.GameMinute;
                s.Population.Fixed[nodeId] = st;
                return;
            }
            foreach (var id in f.EnemyOrder.Take(c))
            {
                var w = AddWorldEnemy(s, id, nodeId);
                w.FixedOf = FixedPrefix + nodeId;
                w.Generation = gen;
                st.Roster.Add(id);
                st.WorldIds.Add(w.Id);
            }
            s.Population.Fixed[nodeId] = st;
            var names = string.Join("、", st.Roster.Select(id => _enemies[id].Name));
            var cut = f.EnemyOrder.Count > c ? $"（按当前容量C{c}截前{c}只，名单从此固定）" : "（名单从此固定）";
            notes.Add($"{(f.Guardian ? "守卫拦在这里" : "发现固定敌踪")}：{names}{cut}——可以接战，也可以离开");
        }

        /// <summary>B20 试玩修复：固定战撤退后卡上写“已交过手：守卫 健康7.20/10.50、…”（只列受过伤的；都满血为 null）。</summary>
        public string? FixedHurtNote(GameState s, string nodeId)
        {
            var hurt = FixedEnemiesAt(s, nodeId).Where(w => w.Hp < _enemies[w.EnemyId].Hp - 1e-9).ToList();
            return hurt.Count == 0 ? null : "已交过手：" + string.Join("、", hurt.Select(w => $"{_enemies[w.EnemyId].Name} 健康{Fmt.N(w.Hp)}/{Fmt.N(_enemies[w.EnemyId].Hp)}"));
        }

        /// <summary>m02 检查哨拦路战未胜时探索/清理翻找/采集的原因（PCE checkpoint.policy 原句）。</summary>
        public const string CheckpointBlockText = "先战胜检查哨拦路敌人";

        /// <summary>
        /// 固定战敌人还在这里时，本节点不能开始探索、清理翻找或采集（可以沿已开通的边离开）。地牢房间：Q187 选A（原开发假设 B20-5）；
        /// m02 检查哨拦路战：Q193（280f8be）取代 B20 的“m02 不挡探索采集”，原因写“先战胜检查哨拦路敌人”。只拦新开工，
        /// 已在办的动作按开工快照完成（各段推进不重验本条）；撤退/重访/探索度 100 都不清除，只有最终胜利（或旧档已有可信清障事实）才放开。
        /// 返回原因，没有为 null。
        /// </summary>
        public string? FixedBlockHere(GameState s)
        {
            if (s.CurrentZone == SafehouseZone) return null;
            if (FixedBattleAt(s.CurrentLocation) is not FixedBattleConfig f) return null;
            var left = FixedEnemiesAt(s, s.CurrentLocation);
            if (left.Count == 0) return null;
            var names = string.Join("、", left.Select(w => _enemies[w.EnemyId].Name));
            if (f.IsCampaign) return $"{CheckpointBlockText}（{names}）：可以接战，也可以离开";
            return $"{(f.Guardian ? "守卫" : "固定敌踪")}还在这里（{names}）：先接战取胜，或离开";
        }

        public string? CanEngageFixed(GameState s)
        {
            var why = EngageBlocked(s);
            if (why != null) return why;
            if (FixedEnemiesAt(s, s.CurrentLocation).Count == 0) return "这里没有固定战";
            return null;
        }

        /// <summary>接战前的通用拦截（与主动接近同口径）。</summary>
        private string? EngageBlocked(GameState s)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (!Config.CombatEnabled) return "战斗未启用";
            if (s.Battle != null) return "已在战斗中";
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (s.Main != null || s.Sub != null) return s.Main?.Status == ActionStatus.Paused ? "行动暂停中，先结束或取消行动" : "行动进行中";
            if (s.CurrentZone == SafehouseZone) return "在安全屋屋内：先出门再接近敌人";
            if (TutorialGateOpen(s)) return "先完成农野荒地的首战（游荡尸），再接近其他敌人";
            return null;
        }

        /// <summary>明确接战本地固定战：揭示时锁定的全名单一起入场（从 1 号位紧凑排位），受惊按主动接近；本场禁增援（EC04）。</summary>
        public ActionResult EngageFixed(GameState s, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = CanEngageFixed(s);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var f = FixedBattleAt(s.CurrentLocation)!;
            StartBattle(s, FixedEnemiesAt(s, s.CurrentLocation), false, f.Guardian ? "守卫战" : "固定战", FearApproach, fixedKey: FixedPrefix + s.CurrentLocation);
            return ActionResult.Success();
        }

        /// <summary>固定类战斗（固定战/旧精英挑战）最终胜利：结固定账、写事实并重判门槛，返回给结算日志的短语。</summary>
        private List<string> FixedVictory(GameState s, BattleState b)
        {
            var lines = new List<string>();
            if (b.FixedKey == null) return lines;
            if (b.FixedKey.StartsWith(ElitePrefix)) { EliteVictory(s, b, lines); return lines; }
            var node = b.FixedKey.Substring(FixedPrefix.Length);
            var f = FixedBattleAt(node);
            var st = f == null ? null : FixedStateAt(s, node);
            if (f == null || st == null || st.Won) return lines;
            // 名单里还有活着的（不在这场里）不算结清——正常流程全名单同场，不会发生
            if (FixedEnemiesAt(s, node).Count > 0) return lines;
            CommitFixedWin(s, f, st, node, lines);
            // B22（A09 E6.2）：守卫战最终胜利（唯一 tick 已先结算、主角存活）才发本代守卫回收材料
            if (f.Guardian && f.DungeonId != null && GrantBossSalvage(s, f.DungeonId, node) is string sv) lines.Add(sv);
            return lines;
        }

        /// <summary>固定战结清：记已胜，写事实（守卫=本代实例事实，m02=campaign 事实）并重判门槛、揭示守卫战利品箱。</summary>
        private void CommitFixedWin(GameState s, FixedBattleConfig f, FixedBattleState st, string node, List<string> lines)
        {
            st.Won = true;
            st.WonMinute = s.GameMinute;
            if (f.Fact == null) { lines.Add("固定战已结清（本代不再出现）"); return; }
            if (f.IsCampaign) s.Progress.Flags.Add(f.Fact);
            else if (f.DungeonId != null) EnsureInstance(s, f.DungeonId).Facts.Add(f.Fact);
            var r = RefreshGates(s);
            var routes = RouteNotes(s, r, node);
            var shown = RevealByInstanceFacts(s);   // Q122：守卫战利品箱当场出现
            lines.Add(f.Guardian ? "守卫已击败（本代）" : f.IsCampaign ? "拦路的敌人已清除" : "固定战已结清");
            if (routes.Count > 0) lines.Add("发现" + string.Join("与", routes));
            if (shown.Count > 0) lines.Add(string.Join("、", shown) + "已出现，可当场领取");
        }

        /// <summary>固定战要写的事实是否已成立（守卫看本代实例事实，m02 看 campaign 事实）。</summary>
        private bool FixedFactMet(GameState s, FixedBattleConfig f) =>
            f.Fact != null && (f.IsCampaign ? MapFact(s, f.Fact) : f.DungeonId != null && InstanceFact(s, f.DungeonId, f.Fact));

        /// <summary>
        /// 事实由别处写下（开发入口“本地牢守卫已击败”“检查哨路障已解决”）时，本代对应的固定战视为已结清：未交战的固定敌移除，不发掉落。
        /// </summary>
        private void SettleFixedByFacts(GameState s)
        {
            foreach (var st in s.Population.Fixed.Values.Where(x => !x.Won).ToList())
            {
                var f = FixedBattleAt(st.NodeId);
                if (f == null || st.Generation != FixedGeneration(s, f) || !FixedFactMet(s, f)) continue;
                st.Won = true;
                st.WonMinute = s.GameMinute;
                s.WorldEnemies.RemoveAll(w => w.FixedOf == FixedPrefix + st.NodeId && !w.Engaged);
            }
        }

        /// <summary>开发入口与测试：当前地点的固定战直接记为已胜（先揭示；固定敌移除、不发掉落；写守卫/检查哨事实并重判）。</summary>
        public string DebugWinFixedHere(GameState s)
        {
            var node = s.CurrentLocation;
            var f = FixedBattleAt(node);
            if (f == null) return $"开发：{LocName(node)}没有固定战";
            if (s.Battle != null) return "开发：战斗中，先结束战斗";
            RevealFixedBattle(s, node, new List<string>());
            var st = FixedStateAt(s, node)!;
            if (st.Won) return "开发：本代固定战已结清";
            s.WorldEnemies.RemoveAll(w => w.FixedOf == FixedPrefix + node && !w.Engaged);
            var lines = new List<string>();
            CommitFixedWin(s, f, st, node, lines);
            UpdateFlags(s);
            return "开发：固定战记为已胜——" + string.Join("；", lines) + "。";
        }

        /// <summary>地牢换代（B12 换代钩子）：旧代固定战与其未交战的固定敌一并移除，下一代第一次到达时重新揭示（新 worldEnemyId）。</summary>
        private void ResetFixedForGeneration(GameState s, string dungeonId)
        {
            var nodes = WE.FixedBattles.Where(f => f.DungeonId == dungeonId).Select(f => f.NodeId).ToHashSet();
            s.WorldEnemies.RemoveAll(w => w.FixedOf != null && w.FixedOf.StartsWith(FixedPrefix) && nodes.Contains(w.FixedOf.Substring(FixedPrefix.Length)) && !w.Engaged);
            foreach (var n in nodes) s.Population.Fixed.Remove(n);
        }

        /// <summary>开发入口：在当前地点重新做一次固定战揭示（已揭示的不变）。返回说明。</summary>
        public string DebugRevealFixedHere(GameState s)
        {
            var f = FixedBattleAt(s.CurrentLocation);
            if (f == null) return $"开发：{LocName(s.CurrentLocation)}没有固定战";
            var notes = new List<string>();
            RevealFixedBattle(s, s.CurrentLocation, notes);
            return notes.Count > 0 ? "开发：" + notes[0] : "开发：本代固定战已揭示过（名单不重编）";
        }
    }
}

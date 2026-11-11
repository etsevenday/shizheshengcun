using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>遭遇身份（R05「Q423 警惕逐项定稿」：角色身份以本次遭遇来源判定，同一敌种换遭遇身份时取对应列）。</summary>
    public enum EnemyCardIdentity
    {
        /// <summary>普通常驻（单卡、随机组卡成员）。</summary>
        Resident,
        /// <summary>固定战成员（含守卫战随从、检查哨拦路战）。</summary>
        FixedMember,
        /// <summary>守卫本体（守卫战名单的首名）。</summary>
        GuardianBody,
        /// <summary>可选强敌。</summary>
        OptionalElite,
        /// <summary>教学首战的游荡尸（特例）。</summary>
        Tutorial,
    }

    /// <summary>
    /// B41a 新版敌踪卡模型（R05 文首「Q342 新版敌踪、潜行和躲避定稿」；重墨 V2 逐字稿「B41 Q426–Q428 正式文案」；开发记录 B41a）。
    /// 全部新逻辑挂在 <see cref="CardsV2"/> 后面：开关 encounters.enemyCardsV2 关着时旧版接敌／绕行流程原样。
    /// </summary>
    public sealed partial class GameEngine
    {
        // ───────────── 警惕（R05 Q423） ─────────────

        /// <summary>
        /// 敌种在某遭遇身份下的公开警惕等级（1–10 级）：直接取 R05 Q423 表的对应列（表中已是取高后的值）；教学首战的游荡尸取特例值。
        /// </summary>
        public int VigilanceOf(string enemyId, EnemyCardIdentity identity)
        {
            if (identity == EnemyCardIdentity.Tutorial) return Config.Story.TutorialBattle.Vigilance;
            var v = _enemies[enemyId].Vigilance ?? throw new InvalidOperationException($"敌人 {enemyId} 缺少警惕等级");
            return identity switch
            {
                EnemyCardIdentity.FixedMember => v.FixedMember,
                EnemyCardIdentity.GuardianBody => v.GuardianBody,
                EnemyCardIdentity.OptionalElite => v.OptionalElite,
                _ => v.Resident,
            };
        }

        // ───────────── 开关与采纳（R05 Q425、Q436—Q438） ─────────────

        /// <summary>EnemyCardsState.ModelVersion 的“已采纳新版卡模型”值。</summary>
        public const int EnemyCardsModelV2 = 1;

        /// <summary>
        /// 这一局走新版敌踪：开关 encounters.enemyCardsV2 开着，且存档已采纳新版卡模型（新局直接采纳；旧档读档时采纳一次）。
        /// 为假时旧版接敌／绕行流程原样（s.Encounter、全局“敌踪待处理”阻断、绕行、战后全地点保护）。
        /// </summary>
        public bool CardsV2(GameState s) => Config.Encounters.EnemyCardsV2 && s.EnemyCards.ModelVersion >= EnemyCardsModelV2;

        /// <summary>动物（配置 family=animal）：不进随机组卡、不混编（R05「B41a Q436 动物单卡」）。</summary>
        public bool IsAnimal(string enemyId) => _enemies[enemyId].IsAnimal;

        /// <summary>旧档在办或暂停的绕行被取消时的统一日志（R05 Q425／Q437，逐字稿「结果、取消与队长回退」）。</summary>
        public const string LegacyBypassCancelledText = "原绕行行动已取消。";

        /// <summary>
        /// 旧档采纳新版敌踪卡模型（R05「Q425 旧档与持久化」、Q436、Q437；只在开关开着且还没采纳过时做一次，重复调用不再改任何东西）：
        /// 1. 正在进行或暂停的绕行取消：已耗时间与身体变化保留，预留沿原取消路径释放一次；不结算绕行完成、不给保护期、不重取名单、不建组卡，
        ///    当地敌人保持各自单卡；无论当地有没有敌人都只写「原绕行行动已取消。」。
        /// 2. 不是绕行的旧待处理名单（s.Encounter）：原名单里仍在当地、未交战、未隐藏的成员，先去掉动物（还原单卡），再按原顺序取前 C 个
        ///    （C 为当前队伍容量）成一张组卡，超出容量的保持单卡；筛完没有成员就不留空组。清掉 s.Encounter。不写日志。
        /// 3. 历史全地点保护（EncounterGraceUntil）原值不动；进行中的旧战斗不动（没有卡键，按旧快照打完，幸存者各自是单卡）。
        /// 全程不抽随机、不制造敌人、不改 HP／人口账／奖励。
        /// </summary>
        public void AdoptEnemyCards(GameState s)
        {
            if (!Config.Encounters.EnemyCardsV2) return;
            if (s.EnemyCards.ModelVersion >= EnemyCardsModelV2) return;

            // 1. 旧绕行（主行动；绕行不会是子行动，这里两处都看以防坏档）
            var cancelled = false;
            if (s.Sub != null && s.Sub.Type == ActionType.Bypass)
            {
                Release(s, s.Sub, s.Ground);
                s.Sub = null;
                cancelled = true;
            }
            if (s.Main != null && s.Main.Type == ActionType.Bypass)
            {
                Release(s, s.Main, s.Ground);
                s.Main = null;
                cancelled = true;
                // 暂停的绕行下正在做的补给／休息／点火子行动：绕行没了，它接成主行动继续（保持合法状态，不退它已耗的时间与预留）
                if (s.Sub != null)
                {
                    s.Sub.IsSub = false;
                    s.Main = s.Sub;
                    s.Sub = null;
                }
            }
            if (cancelled) Emit(s, GameEventKind.ActionCancelled, LegacyBypassCancelledText);

            // 2. 旧待处理名单 → 组卡
            if (s.Encounter is PendingEncounter enc)
            {
                var members = enc.EnemyIds.Distinct()
                    .Select(id => FindWorldEnemy(s, id))
                    .Where(w => w != null && w.LocationId == enc.LocationId && !w.Engaged && !Hidden(s, w) && w.FixedOf == null && w.GroupId == null)
                    .Select(w => w!)
                    .Where(w => !IsAnimal(w.EnemyId))
                    .Take(BattleCapacity(s))
                    .ToList();
                if (members.Count > 0) AddGroupCard(s, enc.LocationId, members, enc.Source, enc.Minute);
                s.Encounter = null;
            }

            s.EnemyCards.ModelVersion = EnemyCardsModelV2;
        }

        // ───────────── 随机组卡（R05 Q417–Q419、Q427、Q436） ─────────────

        public const string GroupCardPrefix = "group:";
        public const string SingleCardPrefix = "we:";

        /// <summary>建一张组卡并把成员挂到组上（成员按给定顺序锁定；容量记当时的队伍容量 C）。</summary>
        private EnemyGroupCard AddGroupCard(GameState s, string locationId, List<WorldEnemy> members, string source, int formedMinute)
        {
            var g = new EnemyGroupCard
            {
                Id = s.EnemyCards.NextGroupId++,
                LocationId = locationId,
                MemberIds = members.Select(w => w.Id).ToList(),
                Source = source,
                FormedMinute = formedMinute,
                Capacity = BattleCapacity(s),
            };
            foreach (var w in members) w.GroupId = g.Id;
            s.EnemyCards.Groups.Add(g);
            return g;
        }

        public EnemyGroupCard? FindGroupCard(GameState s, int groupId) => s.EnemyCards.Groups.FirstOrDefault(g => g.Id == groupId);

        /// <summary>组卡锁定名单里还活着的成员（原顺序）。</summary>
        public List<WorldEnemy> GroupMembers(GameState s, EnemyGroupCard g) =>
            g.MemberIds.Select(id => FindWorldEnemy(s, id)).Where(w => w != null && w.GroupId == g.Id).Select(w => w!).ToList();

        /// <summary>随机组卡标题（Q427）：「{第一个存活敌人名}等{当前存活数量}只」，按锁定名单的原顺序找首名（只剩 1 只也写“等1只”）。</summary>
        public string GroupCardTitle(GameState s, EnemyGroupCard g)
        {
            var alive = GroupMembers(s, g);
            return alive.Count == 0 ? "" : $"{_enemies[alive[0].EnemyId].Name}等{alive.Count}只";
        }

        /// <summary>
        /// 世界敌人被移除（死亡、清场）后修剪组卡：名单里已不存在的成员删去，成员全灭的组卡移除，指向已不存在的组的 GroupId 清掉。
        /// 标题变化、成员伤亡不改变组身份。
        /// </summary>
        private void PruneEnemyCards(GameState s)
        {
            if (s.EnemyCards.Groups.Count == 0) return;
            foreach (var g in s.EnemyCards.Groups)
                g.MemberIds.RemoveAll(id => !(FindWorldEnemy(s, id) is WorldEnemy w && w.GroupId == g.Id));
            s.EnemyCards.Groups.RemoveAll(g => g.MemberIds.Count == 0);
            var live = s.EnemyCards.Groups.Select(g => g.Id).ToHashSet();
            foreach (var w in s.WorldEnemies.Where(w => w.GroupId is int gid && !live.Contains(gid))) w.GroupId = null;
        }
    }
}

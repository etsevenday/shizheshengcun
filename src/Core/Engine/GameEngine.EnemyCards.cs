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
    /// 一张新版敌踪卡的只读视图（B41a；界面在 B41e 接）：卡键、标题、锁定名单里还活着的成员、警惕、身份与角标数据。
    /// 接近时限、撤退保护、隐藏重现等状态在后续批次补。
    /// </summary>
    public sealed class EnemyCardView
    {
        /// <summary>"we:&lt;世界敌人ID&gt;" 单卡／"group:&lt;组ID&gt;" 随机组卡／"fixed:&lt;节点&gt;" 固定战／"elite:&lt;挑战ID&gt;" 可选强敌。</summary>
        public string Key { get; set; } = "";
        /// <summary>single / group / fixed / elite。</summary>
        public string Kind { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>卡所在地点（可选强敌还没生成敌人时是告示地点）。</summary>
        public string LocationId { get; set; } = "";
        /// <summary>锁定名单里还活着的成员（原顺序）；可选强敌第一次实际处理前还没有敌人，为空。</summary>
        public List<WorldEnemy> Members { get; set; } = new List<WorldEnemy>();
        /// <summary>成员敌种（与 Members 同序；可选强敌未生成时是挑战的敌种）。</summary>
        public List<string> EnemyIds { get; set; } = new List<string>();
        /// <summary>公开警惕等级：按遭遇身份取列，组卡与固定战取当前存活成员最高值（R05 Q423）。</summary>
        public int Vigilance { get; set; }
        /// <summary>固定关键敌人（教学首战、固定战、守卫、检查哨）：必须战胜，躲避禁用（R05 Q420）。</summary>
        public bool KeyEnemy { get; set; }
        /// <summary>可选强敌（不倒计时、不主动开战，R05 Q419）。</summary>
        public bool Optional { get; set; }
        /// <summary>受首战门控（首战未胜、且不是教学卡）。</summary>
        public bool TutorialGated { get; set; }
        /// <summary>整卡隐藏中（不显示、不可处理）。</summary>
        public bool Hidden { get; set; }
        /// <summary>有成员正在交战。</summary>
        public bool Engaged { get; set; }
        /// <summary>角标（Q427）：常驻「敌对／中立」、固定「守卫／固定」、可选「可选强敌」、随机组「敌踪待处理」。</summary>
        public string Badge { get; set; } = "";
        /// <summary>危险叠层（Q427、Q357）：随机组卡、固定战、可选强敌、精英常驻挂；普通常驻不挂。</summary>
        public bool Danger { get; set; }
        /// <summary>BM5b1（R05 Q639）：真实守卫标记（固定战配置 guardian），右栏身份「守卫·{原卡名}」用；只影响文字。</summary>
        public bool Guardian { get; set; }
    }

    /// <summary>
    /// B41a 新版敌踪卡模型（R05 文首「Q342 新版敌踪、潜行和躲避定稿」；重墨 V2 逐字稿「B41 Q426–Q428 正式文案」；开发记录 B41a）。
    /// B41e3 起是唯一的敌踪流程（旧版接敌／绕行、全局“敌踪待处理”阻断、战后全地点保护已删除；旧档读档时由 <see cref="AdoptEnemyCards"/> 转换一次）。
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

        // ───────────── 旧档采纳（R05 Q425、Q436—Q438） ─────────────

        /// <summary>EnemyCardsState.ModelVersion 的“已采纳新版卡模型”值（旧档采纳一次后置此值；新局直接置此值）。</summary>
        public const int EnemyCardsModelV2 = 1;

        /// <summary>动物（配置 family=animal）：不进随机组卡、不混编（R05「B41a Q436 动物单卡」）。</summary>
        public bool IsAnimal(string enemyId) => _enemies[enemyId].IsAnimal;

        /// <summary>旧档在办或暂停的绕行被取消时的统一日志（R05 Q425／Q437，逐字稿「结果、取消与队长回退」）。</summary>
        public const string LegacyBypassCancelledText = "原绕行行动已取消。";

        /// <summary>
        /// 旧档采纳新版敌踪卡模型（R05「Q425 旧档与持久化」、Q436、Q437；还没采纳过时做一次，重复调用不再改任何东西）：
        /// 1. 正在进行或暂停的绕行取消：已耗时间与身体变化保留，预留沿原取消路径释放一次；不结算绕行完成、不给保护期、不重取名单、不建组卡，
        ///    当地敌人保持各自单卡；无论当地有没有敌人都只写「原绕行行动已取消。」。暂停绕行下的在途子行动转为独立行动续做（R05 Q454）。
        /// 2. 不是绕行的旧待处理名单（s.Encounter）：原名单里仍在当地、未交战、未隐藏的成员，先去掉动物（还原单卡），再按原顺序取前 C 个
        ///    （C 为当前队伍容量）成一张组卡，超出容量的保持单卡；筛完没有成员就不留空组。清掉 s.Encounter。不写日志。
        /// 3. 历史全地点保护（EncounterGraceUntil）原值不动；进行中的旧战斗不动（没有卡键，按旧快照打完，幸存者各自是单卡）。
        /// 全程不抽随机、不制造敌人、不改 HP／人口账／奖励。
        /// </summary>
        public void AdoptEnemyCards(GameState s)
        {
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
                // Q454（B41a2 定稿）：暂停的绕行下正在做的补给／休息／点火子行动转为独立行动续做——同一行动身份（ActionId）、
                // 已耗时间、进度、已提交的开工效果、自身预留与工具锁原样保留，不重开工、不重扣物或耐久、不推进时间；
                // 上面的 Release 只释放绕行自己的占用。完成后按原行动结果规则结算、回空闲，不恢复绕行；不另写转接日志。
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
            // B41b（R05 Q442）：单卡进入新组后原单卡的接近时限与撤退保护随之失效，组卡在首次可操作时新抽时限，不继承最晚保护
            foreach (var w in members) s.EnemyCards.Timers.Remove(SingleCardKey(w.Id));
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

        /// <summary>某地未隐藏的存续随机组卡（至少一名成员还活着、未隐藏）；没有为 null。</summary>
        public EnemyGroupCard? VisibleGroupCardAt(GameState s, string loc) =>
            s.EnemyCards.Groups.Where(g => g.LocationId == loc).OrderBy(g => g.Id)
                .FirstOrDefault(g => GroupMembers(s, g).Any(w => !Hidden(s, w)));

        /// <summary>
        /// 新随机组卡的候选（R05 Q417、Q436）：当地未交战、未隐藏的敌对敌人里，先排除动物和已在组里的，按最早出现取至多 groupMax 只，
        /// 再按当时队伍容量 C 截取；超容量者保留独立单卡。没有非动物合法候选时为空（不用动物补位、不重抽）。
        /// </summary>
        public List<WorldEnemy> GroupCardCandidates(GameState s, string loc) =>
            LocalHostiles(s, loc).Where(w => w.GroupId == null && !IsAnimal(w.EnemyId))
                .Take(Config.Encounters.GroupMax).Take(BattleCapacity(s)).ToList();

        /// <summary>形成随机组卡的日志（逐字稿「结果、取消与队长回退」）：「发现敌踪：{敌踪名}。」。</summary>
        public static string GroupCardFoundText(string title) => $"发现敌踪：{title}。";

        /// <summary>
        /// 探索／清理完成、原结果已提交、概率判定已命中后形成随机组卡（R05 Q417）：按成组时容量锁名单，成员挂到组上（原单卡不再另出）；
        /// 不阻断其他操作、不设“待处理敌踪”。没有合法候选就不形成组卡。
        /// </summary>
        private void FormRandomGroupCard(GameState s, ActionState a, string loc)
        {
            var members = GroupCardCandidates(s, loc);
            if (members.Count == 0) return;
            var g = AddGroupCard(s, loc, members, a.Type == ActionType.Explore ? "explore" : "clear", s.GameMinute);
            Emit(s, GameEventKind.Encounter, GroupCardFoundText(GroupCardTitle(s, g)), null, members.Select(w => $"enemy:{w.Id}"));
        }

        /// <summary>
        /// 探索／清理预览里的随机组卡说明（逐字稿「系统说明与旧句替换」预览各句，Q438）。几种“不会形成”的情况沿旧版预览的判断顺序取一条：
        /// 首战未胜 → 未到开放日 → 安全区 → 地牢／城市与公共地点 → 旧档历史全地点保护 → 当前已有组卡 → 普通风险行。
        /// 历史保护与已有组卡同时成立只写历史保护句，到期后才写已有组卡句（R05 Q455 定稿）；预览不改组卡与保护。
        /// BM5c（R05 Q622）：只替换探索分支——五种排除句与正常风险行改为探索途中随机遭遇的正式句，原优先顺序与判断条件不变；
        /// 地牢句、旧档保护句与清理全部原句不改。
        /// </summary>
        public string GroupCardPreviewNote(GameState s, string loc, ActionType type)
        {
            var en = Config.Encounters;
            var explore = type == ActionType.Explore;
            if (TutorialGateOpen(s)) return explore ? "首战胜利前不会触发探索随机遭遇。" : "首战胜利前不会形成随机遭遇组卡。";
            if (Clock.Day(s.GameMinute) < en.NoRandomBeforeDay)
                return explore ? $"从第{en.NoRandomBeforeDay}日起，探索途中才可能触发随机遭遇。" : $"从第{en.NoRandomBeforeDay}日起，探索或清理后才可能发现随机组卡。";
            if (en.SafeLocations.Contains(loc)) return explore ? "安全区不会触发探索随机遭遇。" : "安全区不会形成随机遭遇组卡。";
            if (_locs.TryGetValue(loc, out var lc) && (!lc.RandomEncounterEnabled || lc.SafeExterior))
                return lc.IsDungeon ? "地牢房间按固定敌踪处理。" : explore ? "城市与公共地点不会触发探索随机遭遇。" : "城市与公共地点不会形成随机遭遇组卡。";
            // Q438：历史保护沿原截止，到期不再显示。BM5b1（R05 Q639）：不显示剩余时长，改无时长句（内部截止不变）
            if (s.GameMinute < s.EncounterGraceUntil) return LegacyGraceText;
            if (VisibleGroupCardAt(s, loc) != null)
                return explore ? "当前已有一组敌踪，本次探索不会触发探索随机遭遇。" : "当前已有一组敌踪，本次探索或清理不会形成新组卡。";
            var p = EncounterChance(s, loc, type);
            var n = LocalHostiles(s, loc).Count;
            var cap = LocalCapOf(loc);
            return explore
                ? $"本次探索途中随机遭遇概率{Fmt.Pct(p)}（本地敌对{n}/上限{cap}）；随机遭遇会短暂提示后直接进入战斗。"
                : $"完成后发现敌踪概率{Fmt.Pct(p)}（本地敌对{n}/上限{cap}）。";
        }

        // ───────────── 统一卡视图（R05 Q417–Q419、Q423、Q427） ─────────────

        public const string FixedCardTitle = "固定敌踪";
        public const string CheckpointCardTitle = "拦路的感染者";

        public static string SingleCardKey(int worldEnemyId) => SingleCardPrefix + worldEnemyId;
        public static string GroupCardKey(int groupId) => GroupCardPrefix + groupId;
        public static string FixedCardKey(string nodeId) => FixedPrefix + nodeId;
        public static string EliteCardKey(string challengeId) => ElitePrefix + challengeId;

        /// <summary>
        /// 按卡键取卡（不看玩家在哪、卡是否隐藏或交战，这些由视图字段给出）。卡已失效（敌人已死、组已散、固定战未揭示或已胜、不是独立卡）为 null；
        /// 可选强敌的告示还没出现或本局已挑战成功时也为 null，并经 gone 给出现有的关卡原句。
        /// R05 Q457：这些 null 就是“没有这张卡”——卡视图不出、界面不留入口；gone 只作过期请求的内核兜底原因，不为显示它造卡或对白。
        /// 已成立未完成的可选挑战没有敌人实例也是合法卡（Q419）。本方法只读，不创建挑战记录或敌人。
        /// </summary>
        public EnemyCardView? FindEnemyCard(GameState s, string cardKey, out string? gone)
        {
            gone = null;
            if (string.IsNullOrEmpty(cardKey)) return null;
            if (cardKey.StartsWith(SingleCardPrefix))
            {
                if (!int.TryParse(cardKey.Substring(SingleCardPrefix.Length), out var wid)) return null;
                var w = FindWorldEnemy(s, wid);
                // 固定战／可选强敌的实例各有自己的卡；组员不另出可交战单卡
                if (w == null || w.FixedOf != null || w.GroupId != null) return null;
                var cfg = _enemies[w.EnemyId];
                var tutorial = IsTutorialEnemy(s, w.Id);
                return new EnemyCardView
                {
                    Key = cardKey, Kind = "single", Title = cfg.Name, LocationId = w.LocationId,
                    Members = new List<WorldEnemy> { w }, EnemyIds = new List<string> { w.EnemyId },
                    Vigilance = VigilanceOf(w.EnemyId, tutorial ? EnemyCardIdentity.Tutorial : EnemyCardIdentity.Resident),
                    KeyEnemy = tutorial, TutorialGated = TutorialGateOpen(s) && !tutorial,
                    Hidden = Hidden(s, w), Engaged = w.Engaged,
                    Badge = w.Hostile ? "敌对" : "中立", Danger = cfg.Elite,
                };
            }
            if (cardKey.StartsWith(GroupCardPrefix))
            {
                if (!int.TryParse(cardKey.Substring(GroupCardPrefix.Length), out var gid)) return null;
                var g = FindGroupCard(s, gid);
                var members = g == null ? new List<WorldEnemy>() : GroupMembers(s, g);
                if (g == null || members.Count == 0) return null;
                return new EnemyCardView
                {
                    Key = cardKey, Kind = "group", Title = GroupCardTitle(s, g), LocationId = g.LocationId,
                    Members = members, EnemyIds = members.Select(w => w.EnemyId).ToList(),
                    Vigilance = members.Max(w => VigilanceOf(w.EnemyId, EnemyCardIdentity.Resident)),
                    TutorialGated = TutorialGateOpen(s),
                    Hidden = members.All(w => Hidden(s, w)), Engaged = members.Any(w => w.Engaged),
                    Badge = "敌踪待处理", Danger = true,
                };
            }
            if (cardKey.StartsWith(FixedPrefix))
            {
                var node = cardKey.Substring(FixedPrefix.Length);
                var f = FixedBattleAt(node);
                var st = f == null ? null : FixedStateAt(s, node);
                if (f == null || st == null || st.Won) return null;
                // 名单原顺序；守卫战名单首名是守卫本体，其余（随从、普通固定战、检查哨）取固定战成员列
                var members = new List<WorldEnemy>();
                var vig = 0;
                for (var i = 0; i < st.WorldIds.Count; i++)
                {
                    if (!(FindWorldEnemy(s, st.WorldIds[i]) is WorldEnemy w) || w.FixedOf != cardKey) continue;
                    members.Add(w);
                    vig = Math.Max(vig, VigilanceOf(w.EnemyId, f.Guardian && i == 0 ? EnemyCardIdentity.GuardianBody : EnemyCardIdentity.FixedMember));
                }
                if (members.Count == 0) return null;
                return new EnemyCardView
                {
                    Key = cardKey, Kind = "fixed", LocationId = node,
                    Title = f.Guardian ? _enemies[members[0].EnemyId].Name : f.IsCampaign ? CheckpointCardTitle : FixedCardTitle,
                    Members = members, EnemyIds = members.Select(w => w.EnemyId).ToList(),
                    Vigilance = vig, KeyEnemy = true, TutorialGated = TutorialGateOpen(s),
                    Hidden = false, Engaged = members.Any(w => w.Engaged),
                    Badge = f.Guardian ? "守卫" : "固定", Danger = true, Guardian = f.Guardian,
                };
            }
            if (cardKey.StartsWith(ElitePrefix))
            {
                var id = cardKey.Substring(ElitePrefix.Length);
                var c = EliteConfig(id);
                if (c == null) return null;
                if (!EliteVisible(s, c))
                {
                    gone = s.Population.Elites.TryGetValue(id, out var est) && est.Settled ? "本局已挑战成功" : "挑战还没有出现";
                    return null;
                }
                var w = EliteEnemy(s, id);
                return new EnemyCardView
                {
                    Key = cardKey, Kind = "elite", Title = _enemies[c.EnemyId].Name, LocationId = w?.LocationId ?? c.NodeId,
                    Members = w == null ? new List<WorldEnemy>() : new List<WorldEnemy> { w }, EnemyIds = new List<string> { c.EnemyId },
                    Vigilance = VigilanceOf(c.EnemyId, EnemyCardIdentity.OptionalElite),
                    Optional = true, TutorialGated = TutorialGateOpen(s),
                    Hidden = w != null && Hidden(s, w), Engaged = w != null && w.Engaged,
                    Badge = "可选强敌", Danger = true,
                };
            }
            return null;
        }

        /// <summary>
        /// 当前地点、当前区域可操作的敌踪卡（未隐藏、未交战）：固定战、可选强敌、随机组卡、普通单卡（组员不另出单卡）。安全屋屋内没有。
        /// </summary>
        public List<EnemyCardView> EnemyCardsHere(GameState s)
        {
            var list = new List<EnemyCardView>();
            if (!Config.CombatEnabled || s.CurrentZone == SafehouseZone) return list;
            var here = s.CurrentLocation;
            var keys = new List<string>();
            if (FixedBattleAt(here) != null) keys.Add(FixedCardKey(here));
            keys.AddRange(EliteChallengesHere(s).Select(c => EliteCardKey(c.Id)));
            // B41c（R05 Q420）：躲避后重现到别处的可选强敌，卡在实际所在地点显示（原告示处不另补敌人）
            keys.AddRange(s.WorldEnemies.Where(w => w.LocationId == here && w.FixedOf != null && w.FixedOf.StartsWith(ElitePrefix)).Select(w => w.FixedOf!)
                .Where(k => !keys.Contains(k)).ToList());
            keys.AddRange(s.EnemyCards.Groups.Where(g => g.LocationId == here).OrderBy(g => g.Id).Select(g => GroupCardKey(g.Id)));
            keys.AddRange(VisibleEnemiesAt(s, here).Where(w => w.GroupId == null).Select(w => SingleCardKey(w.Id)));
            foreach (var k in keys)
                if (FindEnemyCard(s, k, out _) is EnemyCardView v && v.LocationId == here && !v.Hidden && !v.Engaged) list.Add(v);
            return list;
        }

        // ───────────── 主动接近（R05 Q417–Q419、Q428；逐字稿「新操作的禁用与拒绝」） ─────────────

        /// <summary>
        /// 「主动接近」的拒绝原因：逐字稿「新操作的禁用与拒绝」表自上而下取第一条实际原因（只取与主动接近有关的行）。可以接近为 null。
        /// 普通敌踪在场本身不拒绝任何操作；可选强敌的告示没出现／已挑战成功沿现有关卡原句（只作内核兜底，R05 Q457）。
        /// R05 Q456 定稿：卡已失效或在别的地点 →「这处敌踪已不在这里。」（先于区域判断，异地卡不当成本地跨区卡）；
        /// 卡在本地、玩家在安全屋屋内 →「只能处理当前地点、当前区域可见的敌踪。」；隐藏、交战、门控沿原对应原因。
        /// 提交时（<see cref="ApproachCard"/>）重验一次，过期请求不执行、不扣费、不抽签、不生成敌人或对白、不改选别的敌人。
        /// </summary>
        public string? CanApproachCard(GameState s, string cardKey)
        {
            if (s.Progress.Dead) return "本局已结束。";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (!Config.CombatEnabled || !Config.Encounters.ActiveContactEnabled) return "当前无法发起战斗。";
            if (s.Battle != null) return "当前正在战斗。";
            if (s.Expedition?.IsExtracting == true) return "撤出途中无法开始这项行动。";
            if (s.Sub != null || (s.Main != null && s.Main.Status != ActionStatus.Paused)) return "当前行动还没结束。";
            if (s.Main != null) return LockedMainPaused(s) ? LockedPausedBusy : "行动暂停中，请先继续或退出行动。";   // B41c2（Q517）
            if (DuplicateWeaponsIllegal(s) || CarriedOverflow(s)) return "请先完成随身物品整理。";
            if (s.Recruit.Pending?.AwaitingPlacement == true) return "请先办完当前事务。";
            var card = FindEnemyCard(s, cardKey, out var gone);
            if (card == null) return gone ?? "这处敌踪已不在这里。";
            if (card.LocationId != s.CurrentLocation) return "这处敌踪已不在这里。";
            if (card.Hidden) return "这处敌踪暂时不可见。";
            if (s.CurrentZone == SafehouseZone) return "只能处理当前地点、当前区域可见的敌踪。";
            if (card.Engaged) return "这处敌踪正在交战。";
            if (card.TutorialGated) return TutorialGateReason;
            return null;
        }

        /// <summary>
        /// 主动接近一张敌踪卡（盘内点击即提交，0 世界分钟；受惊情境按主动接近 0.5）：单卡打这一只真实实例，随机组卡全锁定名单入场，
        /// 固定战全名单入场（禁增援），可选强敌第一次实际处理时才建唯一敌人并锁定掉落抽签。
        /// </summary>
        public ActionResult ApproachCard(GameState s, string cardKey, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = CanApproachCard(s, cardKey);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            var card = FindEnemyCard(s, cardKey, out _)!;
            switch (card.Kind)
            {
                case "single":
                {
                    // 首战撤退后再接近同一保留游荡尸：仍是教学首战（胜利才发教学物质）
                    var tutorial = IsTutorialEnemy(s, card.Members[0].Id);
                    StartBattle(s, card.Members, false, tutorial ? "首战" : "主动接近", FearApproach, tutorial, cardKey: cardKey);
                    if (tutorial) TutorialBattleStarted(s);   // B41b（R05 Q421）：教学战真正开始时才播警告与引导
                    break;
                }
                case "group":
                    StartBattle(s, card.Members.Where(w => !Hidden(s, w)).ToList(), false, "主动接近", FearApproach, cardKey: cardKey);
                    break;
                case "fixed":
                    StartBattle(s, card.Members, false, FixedBattleAt(card.LocationId)!.Guardian ? "守卫战" : "固定战", FearApproach, fixedKey: cardKey, cardKey: cardKey);
                    break;
                default:
                {
                    var id = cardKey.Substring(ElitePrefix.Length);
                    StartBattle(s, new List<WorldEnemy> { EnsureEliteEnemy(s, EliteConfig(id)!) }, false, "可选强敌挑战", FearApproach, fixedKey: cardKey, cardKey: cardKey);
                    break;
                }
            }
            return ActionResult.Success();
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

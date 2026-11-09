using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 我方多人入战（开发计划 v0.9 批次 B9a，需求 R113；R03 §1—§6、R08 §4、A11 §8，Q62/Q73/Q75/Q80/Q86/Q91）：
    /// 同行队友按编队预设入场，每人速度/AP/恐慌/受惊入行动队列，玩家逐人下指令；敌人抽中谁就按谁本人结算；队友战死其余参战者恐慌+8。
    /// 实现方式：战斗代码沿用“主角视角”的 Player*/Stats/Body/Skills 写法，视角（BattleState.FocusId）随激活者切换，
    /// 结算某名我方受击、出血、被治疗时用 FocusOn 临时切到他。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>临时把战斗视角切到某名我方成员（using 结束恢复）。不在战斗中时什么也不做。</summary>
        public IDisposable FocusOn(GameState s, string actorId) => new FocusScope(s.Battle, actorId);

        private sealed class FocusScope : IDisposable
        {
            private readonly BattleState? _b;
            private readonly string? _prev;

            public FocusScope(BattleState? b, string actorId)
            {
                _b = b;
                if (b == null) return;
                _prev = b.FocusOverride;
                b.FocusOverride = actorId;
            }

            public void Dispose()
            {
                if (_b != null) _b.FocusOverride = _prev;
            }
        }

        /// <summary>当前视角成员（战斗外为主角）。</summary>
        public static string MeId(GameState s) => s.Battle?.FocusId ?? GameState.PlayerId;

        /// <summary>物品在当前视角成员的随身里（主角 owner "carried"，队友 "carried:&lt;actorId&gt;"）：战斗中弹药、药品只取本人随身（R03 §3）。</summary>
        public static bool CarriedByMe(GameState s, ItemInstance i) => i.Owner == ItemInstance.CarriedOwner(MeId(s));

        /// <summary>当前视角成员在文案里的称呼：主角“你”，队友名册名。</summary>
        public string Who(GameState s) => ActorDisplayName(s, MeId(s));

        /// <summary>本场入战成员（入场顺序，主角在首位）；旧档没有记录时只有主角。</summary>
        public static List<string> MemberIds(BattleState b) => b.Members.Count > 0 ? b.Members : new List<string> { GameState.PlayerId };

        /// <summary>入战成员表补齐（旧档为空时补主角；测试或旧档直接放进号位的角色补到末尾），保证队列编码稳定。</summary>
        public static void SyncMembers(BattleState b)
        {
            if (b.Members.Count == 0) b.Members.Add(GameState.PlayerId);
            foreach (var a in b.Allies)
                if (!string.IsNullOrEmpty(a) && !b.Members.Contains(a)) b.Members.Add(a);
        }

        /// <summary>成员在行动队列里的编码：主角 0，第 k 名成员 −k。</summary>
        public static int QueueCode(BattleState b, string actorId)
        {
            if (actorId == GameState.PlayerId) return 0;
            var k = MemberIds(b).IndexOf(actorId);
            return k > 0 ? -k : int.MinValue;
        }

        /// <summary>队列编码对应的我方成员（不是我方为 null）。</summary>
        public static string? QueueActor(BattleState b, int code)
        {
            if (code == 0) return GameState.PlayerId;
            if (code > 0) return null;
            var m = MemberIds(b);
            return -code < m.Count ? m[-code] : null;
        }

        /// <summary>我方成员此刻还在场上（存活、有号位）。</summary>
        public static bool AllyInBattle(BattleState b, string actorId) => b.Allies.Contains(actorId);

        /// <summary>场上存活的我方成员（按号位升序）。</summary>
        public static List<string> AlliesOnField(BattleState b) => b.Allies.Where(a => !string.IsNullOrEmpty(a)).OrderBy(a => b.Allies.IndexOf(a)).ToList();

        /// <summary>正在激活、等待指令的我方成员；没有为 null（旧档主角激活时 ActiveAlly 为空，按主角）。</summary>
        public static string? ActingAlly(BattleState b)
        {
            var id = string.IsNullOrEmpty(b.ActiveAlly) ? GameState.PlayerId : b.ActiveAlly;
            return b.Fighters.TryGetValue(id, out var f) && f.Acting ? id : null;
        }

        /// <summary>某名成员的真实号位（不在场为 0）。</summary>
        public static int AllyPos(BattleState b, string actorId) => b.Allies.IndexOf(actorId) + 1;

        // ───────────── 人物基础档（Q63）：主角沿用 scope 数值，队友读 actorBaseDefense / A11 速度 ─────────────

        private ActorDefenseProfileConfig? MyProfile(GameState s) => MeId(s) == GameState.PlayerId ? null : ActorProfile(s, MeId(s));

        /// <summary>当前视角成员的基础闪避（主角 36；队友按人物基础档）。</summary>
        public double MyBaseDodge(GameState s) => MyProfile(s)?.Evasion ?? Config.Scope.BasePlayerDodge;

        /// <summary>当前视角成员的基础护甲（主角 0；队友按人物基础档）。</summary>
        public double MyBaseArmor(GameState s) => MyProfile(s)?.BaseArmor ?? Config.Scope.BasePlayerArmor;

        /// <summary>当前视角成员的基础控制抗性（主角 5；队友按人物基础档）。</summary>
        public double MyBaseControlResist(GameState s) => MyProfile(s)?.ControlResist ?? Config.Scope.BasePlayerControlResist;

        // ───────────── 入场 ─────────────

        /// <summary>
        /// 开战时的我方号位（Q86：按已保存的编队预设，不压紧、不按武器自动改位）：预设里每名存活的当前同行者占他那一格；
        /// 人数不超过本场容量 C（主角必在，其余按号位从前到后取；正常编队已保证同行 ≤ C，这里只防开发入口越界）。
        /// 返回下标=号位−1 的稀疏表（末尾空位省略）。
        /// </summary>
        public List<string> EntryAllies(GameState s, int capacity)
        {
            var preset = RepairedFormation(s, s.FormationPreset);
            var slots = Enumerable.Repeat("", Config.Scope.Slots.Count).ToList();
            var taken = 0;
            var me = preset.IndexOf(GameState.PlayerId);
            if (me < 0) me = 0;
            slots[me] = GameState.PlayerId;
            taken++;
            for (var k = 0; k < preset.Count && k < slots.Count; k++)
            {
                var id = preset[k];
                if (string.IsNullOrEmpty(id) || id == GameState.PlayerId || !s.Party.Contains(id)) continue;
                if (!s.Actors.TryGetValue(id, out var a) || a.Stats.Health <= 0) continue;
                if (taken >= Math.Max(1, capacity) || slots[k] != "") continue;
                slots[k] = id;
                taken++;
            }
            while (slots.Count > 0 && slots[slots.Count - 1] == "") slots.RemoveAt(slots.Count - 1);
            return slots;
        }

        // ───────────── 队友战死 ─────────────

        /// <summary>
        /// 死亡喷溅等“结算写完再判”的主角死亡：主角健康已归零就按主角视角终局（返回 true）。队友在 DeathSplash 里当场按战死处理。
        /// </summary>
        private bool PlayerDownAfterSplash(GameState s)
        {
            if (s.Battle == null || s.Progress.Dead) return s.Progress.Dead;
            if (s.Actor(GameState.PlayerId).Stats.Health > 0) return false;
            using (FocusOn(s, GameState.PlayerId)) BattleDeath(s, s.Progress.LastDamageCause ?? "喷溅");
            return true;
        }

        /// <summary>
        /// 队友在战斗中倒下（R03 §5、R08 §4、A11 §8）：走既有队友死亡/遗物流程（名册记死亡、离队、清号位、遗物留战斗地点、C 不降），
        /// 其余仍在场的我方每人恐慌 +8（配置 panic.allyDeathGain；一次死亡事件只加一次）；他正在激活则这次激活结束（剩余 AP 作废），
        /// 他在队列里后面的激活跳过。不复活、不改敌人意图。
        /// </summary>
        private void AllyDownInBattle(GameState s, string actorId, string cause)
        {
            var b = s.Battle;
            if (b == null || actorId == GameState.PlayerId || !s.Actors.TryGetValue(actorId, out var actor)) return;
            if (!b.Allies.Contains(actorId) && actor.Stats.Health > 0) return;
            var name = ActorDisplayName(s, actorId);
            var wasActing = ActingAlly(b) == actorId;
            int moved;
            using (FocusOn(s, GameState.PlayerId)) moved = CompanionDown(s, actor, cause, inBattle: true);
            var f = b.Fighter(actorId);
            f.Acting = false;
            f.Ap = 0;
            var gain = Config.Panic.AllyDeathGain;
            var others = AlliesOnField(b);
            var rises = new List<(string Name, string Inc)>();
            if (gain > 0)
                foreach (var id in others)
                {
                    var of = b.Fighter(id);
                    var before = of.Panic;
                    of.Panic = Math.Min(Config.Panic.Range[1], of.Panic + gain);
                    of.PanicPeak = Math.Max(of.PanicPeak, of.Panic);
                    if (of.Panic > before + 1e-9) rises.Add((ActorDisplayName(s, id), Fmt.T(of.Panic - before)));
                }
            // B38b（Q284）：首句“{人物}已战死（{原因}）。”；实际有遗物才接遗物句；只有恐慌实际增加者按增量合组（顿号），不同增量分别成句，顺序沿参战顺序
            var text = $"{name}已战死（{cause}）。" + (moved > 0 ? $"遗物留在{LocName(b.LocationId)}，战后可取。" : "");
            foreach (var g in rises.GroupBy(x => x.Inc)) text += $"{string.Join("、", g.Select(x => x.Name))}恐慌+{g.Key}。";
            BattleLog(s, text);
            if (wasActing)
            {
                b.ActiveAlly = "";
                b.ActingPursuit = false;
                b.Cursor++;
            }
        }

        // ───────────── 以友方为目标的技能（A06 §2 目标列；Q75 医疗任一存活友方含自己） ─────────────

        /// <summary>这个非攻击技能要不要选友方目标（指挥/医疗/保护，config skillExec.targetMode = "ally"）。</summary>
        public static bool TargetsAlly(SkillExecConfig x) => x.TargetMode == "ally" && (x.Type == "ally" || x.Type == "medical");

        /// <summary>目标可以是自己吗：保护/掩护（相邻友方）与协同（任一其他友方）不行，其余“任一友方”含自己（整队的 A/B 也可以是施术者）。</summary>
        private static bool SelfTargetAllowed(SkillExecConfig x) => !x.Protect && x.ApBonus <= 0;

        /// <summary>
        /// 技能当前可选的友方目标（按号位升序）：保护/掩护 = 与施术者相邻的在场队友（A06“相邻友方”）；协同 = 其他在场队友；
        /// 整队 = 第一目标 A 的候选（在场存活、至少有一名真实相邻的存活友方，含施术者）；
        /// 鼓舞/稳心/总攻令与医疗 = 任一在场友方含自己（Q75）。调用时视角为施术者。
        /// </summary>
        public List<string> AllyTargetsOf(GameState s, BattleState b, SkillExecConfig x)
        {
            var me = b.FocusId;
            var mp = AllyPos(b, me);
            var all = AlliesOnField(b);
            if (x.Reorder) return all.Where(a => ReorderPartners(b, a).Count > 0).ToList();
            if (x.Protect) return all.Where(a => a != me && Math.Abs(AllyPos(b, a) - mp) == 1).ToList();
            if (!SelfTargetAllowed(x)) return all.Where(a => a != me).ToList();
            return all;
        }

        /// <summary>
        /// 整队（Q105）第二目标 B 的候选：与 A 真实槽号差恰为 1 的在场存活友方（不同 actorId，可以是施术者；空格不算相邻、不跨空位找更远的人），按号位升序。
        /// </summary>
        public static List<string> ReorderPartners(BattleState b, string a)
        {
            var list = new List<string>();
            var pa = AllyPos(b, a);
            if (pa <= 0) return list;
            foreach (var p in new[] { pa - 1, pa + 1 })
                if (AllyAt(b, p) is string o && o != a) list.Add(o);
            return list;
        }

        /// <summary>指令里的友方目标：写了就用它，没写且可以对自己用就是自己；否则 null。整队必须明确点 A，不默认自己。</summary>
        private static string? AllyTargetOf(BattleState b, BattleCommand c, SkillExecConfig x) =>
            c.AllyTarget ?? (SelfTargetAllowed(x) && !x.Reorder ? b.FocusId : null);

        /// <summary>友方目标是否合法（只读）：没选、不在场、不在可选范围都写原因。</summary>
        private string? AllyTargetBlocked(GameState s, BattleState b, BattleCommand c, SkillNodeConfig n, SkillExecConfig x)
        {
            if (x.Reorder) return ReorderBlocked(s, b, c, n);
            var t = AllyTargetOf(b, c, x);
            if (t == null) return x.Protect ? $"请选择与{Who(s)}相邻的一名队友" : "请选择一名其他队友";
            if (!AllyInBattle(b, t)) return $"{ActorDisplayName(s, t)}不在场上";
            if (!AllyTargetsOf(s, b, x).Contains(t))
                return x.Protect ? $"{n.Name}只能给相邻的队友（{ActorDisplayName(s, t)}在{PosName(AllyPos(b, t))}，{Who(s)}在{PosName(PlayerPos(b))}）"
                    : t == b.FocusId ? $"{n.Name}不能对自己用" : $"{ActorDisplayName(s, t)}不是{n.Name}的合法目标";
            return null;
        }

        /// <summary>
        /// 整队两目标的合法性（Q105 选 A，R03/A06 本轮章；只读）：A 在场存活；B 与 A 不同、在场存活且真实槽号差恰为 1；
        /// 带了预览时的槽快照就按快照重验，任一变化整条拒绝、不自动改选。两目标的武器使用位不影响合法性。
        /// </summary>
        private string? ReorderBlocked(GameState s, BattleState b, BattleCommand c, SkillNodeConfig n)
        {
            var a = c.AllyTarget;
            if (a == null) return $"请先点要交换站位的第一名友方（可以是{Who(s)}自己）";
            if (!AllyInBattle(b, a)) return $"{ActorDisplayName(s, a)}不在场上（不能选空位、倒下或候补的人）";
            if (c.AllyTargetPos is int pa && pa != AllyPos(b, a)) return $"{ActorDisplayName(s, a)}的号位已变化（{PosName(pa)}→{PosName(AllyPos(b, a))}），请重新选择";
            var partners = ReorderPartners(b, a);
            if (partners.Count == 0) return $"{ActorDisplayName(s, a)}（{PosName(AllyPos(b, a))}）两侧没有相邻的存活友方，不能整队（不跨空位找更远的人）";
            var bb = c.AllyTarget2;
            if (bb == null) return $"再点与{ActorDisplayName(s, a)}相邻的一名友方（{string.Join("或", partners.Select(o => $"{ActorDisplayName(s, o)}·{PosName(AllyPos(b, o))}"))}）";
            if (bb == a) return "两个目标不能是同一人";
            if (!AllyInBattle(b, bb)) return $"{ActorDisplayName(s, bb)}不在场上（不能选空位、倒下或候补的人）";
            if (c.AllyTarget2Pos is int pb && pb != AllyPos(b, bb)) return $"{ActorDisplayName(s, bb)}的号位已变化（{PosName(pb)}→{PosName(AllyPos(b, bb))}），请重新选择";
            if (!partners.Contains(bb)) return $"{ActorDisplayName(s, bb)}（{PosName(AllyPos(b, bb))}）与{ActorDisplayName(s, a)}（{PosName(AllyPos(b, a))}）不相邻，{n.Name}只能交换相邻的两人";
            return null;
        }

        /// <summary>指挥/保护经验的去重键（每场每目标每 skillId 至多一次，A06 §3）；主角对自己沿用旧键（只写技能 ID）。</summary>
        private static string CommandKey(string nodeId, string caster, string target) =>
            caster == GameState.PlayerId && target == GameState.PlayerId ? nodeId : $"{nodeId}@{caster}>{target}";

        /// <summary>待发指挥经验的施术者与目标（旧档只有技能 ID：主角给自己）。</summary>
        private static (string Node, string Caster, string Target) ParseCommandKey(string key)
        {
            var at = key.IndexOf('@');
            if (at < 0) return (key, GameState.PlayerId, GameState.PlayerId);
            var gt = key.IndexOf('>', at);
            return gt < 0 ? (key.Substring(0, at), key.Substring(at + 1), GameState.PlayerId) : (key.Substring(0, at), key.Substring(at + 1, gt - at - 1), key.Substring(gt + 1));
        }

        /// <summary>给施术者发一次指挥/保护经验（每场每目标每 skillId 一次；满级不发）。返回是否发了。</summary>
        private bool GrantSupportXp(GameState s, BattleState b, string nodeId, string caster, string target, string skill)
        {
            var key = CommandKey(nodeId, caster, target);
            if (b.CommandXp.Contains(key)) return false;
            b.CommandXp.Add(key);
            using (FocusOn(s, caster))
            {
                if (SkillMaxed(s, skill)) return false;
                GainXp(s, skill, Config.Xp.DamagingAttackCommand);
                BattleLog(s, $"{(caster == GameState.PlayerId ? "" : Who(s) + "的")}{SkillName(skill)}经验+{Config.Xp.DamagingAttackCommand}（{(SkillNames.TryGetValue(nodeId, out var nm) ? nm : nodeId)}生效{(target != caster ? "：" + ActorDisplayName(s, target) : "")}）。");
            }
            return true;
        }

        // ───────────── 开发入口（?dev=1，B9a） ─────────────

        /// <summary>
        /// 开发入口：一步组出 n 人（2—4）同行队伍——记下营区登记与两种关键记录（C4），按配置顺序把还不在队的命名角色登记并编入同行，
        /// 直到同行 n 人；号位按编队修复规则补到空格。不改正式条件函数。返回说明。
        /// </summary>
        public string DebugQuickParty(GameState s, int n)
        {
            if (s.Battle != null) return "战斗中不能组队";
            n = Math.Max(1, Math.Min(Config.Scope.Slots.Count, n));
            foreach (var u in Config.Scope.Capacity.Unlocks.SelectMany(u => u.AllOf.Concat(u.AnyOf)).Distinct()) s.Progress.Flags.Add(u);
            var added = new List<string>();
            foreach (var c in Config.Party.NamedCompanions)
            {
                if (s.Party.Count >= Math.Min(n, PartyCapacity(s))) break;
                if (s.Party.Contains(c.ActorId)) continue;
                var r = RosterOf(s, c.ActorId);
                if (r != null && r.Dead) continue;
                if (r == null && !AddNamedToRoster(s, c.ActorId, false).Ok) continue;
                ApplySetActive(s, c.ActorId, true);
                added.Add(RosterOf(s, c.ActorId)!.Name);
            }
            var text = $"开发：容量C{PartyCapacity(s)}，同行{s.Party.Count}人" + (added.Count > 0 ? $"（新编入{string.Join("、", added)}）" : "") +
                       $"；战前号位：{string.Join("，", RepairedFormation(s, s.FormationPreset).Select((id, k) => id == "" ? null : $"{k + 1}号位{ActorDisplayName(s, id)}").Where(x => x != null))}。下一场战斗全员按号位入场。";
            Emit(s, GameEventKind.Info, text);
            return text;
        }

        /// <summary>
        /// 开发入口（B9a）：给每名存活同行队友的随身发药品（绷带、医用纱布、生理盐水各 2），用于试玩队友的医疗技能（R03 §3 只取施术者本人随身）。
        /// 给队友递物品的正式入口在 B10。不校验五卡与负重。
        /// </summary>
        public string DebugGiveCompanionMeds(GameState s)
        {
            var who = ActiveCompanions(s).Select(r => r.ActorId).ToList();
            foreach (var id in who)
                foreach (var item in new[] { "bandage", "medical_gauze", "saline" })
                    CreateItems(s, item, 2, ItemInstance.CarriedOwner(id));
            var text = who.Count == 0 ? "开发：没有同行队友" : $"开发：{string.Join("、", who.Select(id => ActorDisplayName(s, id)))}随身各加绷带、医用纱布、生理盐水各2（只供试玩队友医疗技能）。";
            Emit(s, GameEventKind.Info, text);
            return text;
        }

        /// <summary>
        /// 开发入口（?dev=1，B10a 试玩 Q103/Q105）：你与每名同行队友武器Ⅰ换成白色木矛、武器Ⅱ换成白色信号哨（原武器放到当前地面），
        /// 矛术至少 5 级（架矛）、指挥至少 3 级（整队）。只供试玩多人架矛顺延与整队，不改正式来源。战斗中不可用。
        /// </summary>
        public string DebugPartyDrill(GameState s)
        {
            if (s.Battle != null) return "战斗中不能用";
            var who = new List<string> { GameState.PlayerId };
            who.AddRange(ActiveCompanions(s).Select(r => r.ActorId));
            foreach (var id in who)
            {
                foreach (var (slot, item) in new[] { (EquipSlots.Weapon1, "wooden_spear"), (EquipSlots.Weapon2, "signal_whistle") })
                {
                    var owner = ItemInstance.EquipOwner(id, slot);
                    foreach (var old in s.Items.Where(i => i.Owner == owner).ToList()) old.Owner = s.Ground;
                    DebugCreateEquipment(s, item, "white", owner);
                }
                var a = s.Actor(id);
                foreach (var (skill, lv) in new[] { ("spear", 5), ("command", 3) })
                    if (!a.Skills.TryGetValue(skill, out var st) || st.Level < lv) a.Skills[skill] = new SkillState { Level = lv, Xp = 0 };
            }
            Normalize(s, s.Ground);
            var text = $"开发：{string.Join("、", who.Select(id => ActorDisplayName(s, id)))}武器Ⅰ木矛、武器Ⅱ信号哨（原武器放到{ZoneName(s, s.Ground)}地面），矛术≥5级、指挥≥3级（只供试玩架矛顺延与整队）。";
            Emit(s, GameEventKind.Info, text);
            return text;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 50 个战斗/医疗主动技能（A06 §2；开发计划 v0.9 B7 R25）。两武器页各列本门已解锁的五节点技能（Q26：未达等级的不列出），
    /// 1 级攻击技能就是“普通攻击”指令 basic_attack（拳击/斩击/劈砍/刺击/敲击/射击/开火/盾击）；其余用节点 ID 作指令。
    /// AP、基准E、使用位→目标、冷却读 config.skillNodes，效果读 config.skillExec。E 成本 = 表内基准E × 武器E÷该类基准E ÷(1+0.025L)。
    /// 预览（PreviewCommand）、操作台（BattleOptions）、系统说明（CompatibleCommands）与执行（ExecuteCommand）都走同一组函数（Q19/Q26 同源）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string BasicAttack = "basic_attack";
        public const string DesperatePunch = "desperate_punch";

        private readonly Dictionary<string, SkillNodeConfig> _nodes;
        private readonly Dictionary<string, SkillExecConfig> _exec;
        /// <summary>技能节点 ID → 中文名（静态 ActionName 用）。</summary>
        private static readonly ConcurrentDictionary<string, string> SkillNames = new ConcurrentDictionary<string, string>();

        public SkillNodeConfig? SkillNode(string id) => _nodes.TryGetValue(id, out var n) ? n : null;
        public SkillExecConfig? SkillExecOf(string id) => _exec.TryGetValue(id, out var x) ? x : null;

        /// <summary>该指令是不是武器技能（普通攻击或技能节点 ID）。</summary>
        public bool IsSkillAction(string action) => action == BasicAttack || _exec.ContainsKey(action);

        /// <summary>
        /// 指令对应当前武器的技能节点：basic_attack → 本门 1 级攻击技能；节点 ID 须属于本武器的门类（不兼容返回 null）。
        /// 指挥、医疗器具的 1 级技能不是攻击，没有 basic_attack。
        /// </summary>
        public SkillNodeConfig? SkillOf(string action, WeaponConfig w)
        {
            var id = action == BasicAttack ? $"{w.SkillId}_01" : action;
            if (!_nodes.TryGetValue(id, out var n) || !_exec.TryGetValue(id, out var x)) return null;
            if (n.SkillId != w.SkillId) return null;
            if (action == BasicAttack && !x.IsAttack) return null;
            return n;
        }

        /// <summary>界面与日志用的指令 ID：1 级攻击技能写 basic_attack，其余写节点 ID。</summary>
        public string ActionIdOf(SkillNodeConfig n) => n.Level == 1 && _exec[n.Id].IsAttack ? BasicAttack : n.Id;

        public bool SkillUnlocked(GameState s, SkillNodeConfig n) => SkillLevel(s, n.SkillId) >= n.Level;

        /// <summary>指令在当前武器上的名字：basic_attack 写本门 1 级技能名（斩击、射击……），技能写节点名，其余同 ActionName。</summary>
        public string CommandLabel(GameState s, string action)
        {
            if (action == BasicAttack || _exec.ContainsKey(action))
                return SkillOf(action, CurrentWeapon(s, out _))?.Name ?? ActionName(action);
            return ActionName(action);
        }

        /// <summary>
        /// 技能体力成本（A06 §1）：表内基准E × 武器E÷该类基准E（同类不同装备等比例换算；1 级攻击技能即武器基础E）÷(1+0.025L)，
        /// 再乘本武器“平衡”类词条（只作用本武器技能）。
        /// </summary>
        public double SkillEnergy(GameState s, SkillNodeConfig n, WeaponConfig w, int? page = null)
        {
            var kindBase = Config.SkillKindBaseEnergy.TryGetValue(w.SkillId, out var kb) && kb > 0 ? kb : 0;
            var baseCost = kindBase > 0 ? n.BaseEnergy * w.EnergyCost / kindBase : n.BaseEnergy;
            var factor = w.IsFists ? 1 : EquipmentEffects(s, GameState.PlayerId, page ?? s.Battle?.ActiveWeaponSlot ?? DefaultWeaponPage(s)).EnergyCostFactor;
            return SkillEnergyCost(baseCost, SkillLevel(s, w.SkillId)) * factor;
        }

        /// <summary>指令的体力成本：技能按 SkillEnergy，应急拳 0，通用动作按配置。</summary>
        public double CommandEnergy(GameState s, string action, WeaponConfig w, int? page = null)
        {
            if (action == DesperatePunch) return 0;
            if (SkillOf(action, w) is SkillNodeConfig n) return SkillEnergy(s, n, w, page);
            return Act(action)?.Energy ?? 0;
        }

        /// <summary>冷却（A06 §2）：r 使用 CD1 → r+1 不可用、r+2 可用；两武器页与追击共享。还在冷却返回起可用的轮次。</summary>
        public static int? CooldownReadyRound(BattleState b, SkillNodeConfig n) =>
            n.CooldownRounds > 0 && b.SkillUsedRound.TryGetValue(n.Id, out var r) && b.Round <= r + n.CooldownRounds ? r + n.CooldownRounds + 1 : (int?)null;

        private static HashSet<int> Digits(string t) => new HashSet<int>(t.Where(char.IsDigit).Select(ch => ch - '0'));

        /// <summary>技能的己方使用位（A06“使用位”；“按武器”取武器使用位）。</summary>
        public HashSet<int> SkillOwnPositions(SkillNodeConfig n, WeaponConfig w) => n.OwnPart != "" ? Digits(n.OwnPart) : OwnPositionsOf(w);

        /// <summary>
        /// 技能的敌方目标位（A06“目标”；“按武器”取武器目标位）。Q64：拳击 12→1、连拳 12→12；穿着护拳绑带且当前页是空槽（徒手）时，
        /// 普通拳击 unarmed_01 改为绑带写的 12（应急拳、推挡不变）。
        /// </summary>
        public HashSet<int> SkillTargetPositions(GameState s, SkillNodeConfig n, WeaponConfig w) =>
            w.IsFists && n.Id == UnarmedBasic && WrapsPunchTargets(s) is string wt ? Digits(wt)
            : n.TargetPart != "" ? Digits(n.TargetPart) : TargetPositionsOf(w);

        public const string UnarmedBasic = "unarmed_01";

        /// <summary>主角穿着的护拳绑带写的普通拳击目标位（"12"）；没穿为 null。只在空槽徒手页由 SkillTargetPositions 使用。</summary>
        public string? WrapsPunchTargets(GameState s) =>
            WornItem(s, GameState.PlayerId, EquipSlots.CombatAccessory) is ItemInstance it && Gear(it.ItemId) is GearConfig g ? g.UnarmedBasicTargets : null;

        /// <summary>专长射击：霰弹枪改为对敌方 1、2 号位各一击（A06 §2、A05 霰弹枪“散布”）。</summary>
        public static bool IsAreaShot(SkillExecConfig x, WeaponConfig w) => x.AreaWeapons.Contains(w.Id);

        /// <summary>界面目标方式：single 点一个目标 / suffix 点起点敌人打它所在号位到4号位（贯穿）/ area 点任一亮起目标打全部 / self 无目标就近确认。</summary>
        public static string OptionTargetMode(SkillExecConfig x, WeaponConfig w) =>
            !x.IsAttack ? "self" : IsAreaShot(x, w) ? "area" : x.TargetMode is "pair" or "suffix" ? x.TargetMode : "single";

        /// <summary>
        /// 这次攻击实际打到的目标（预览与结算同源）：单体=所选；贯穿（Q82 suffix）=所选敌人所在号位 k 到 4 号位的全部存活敌；
        /// 专长射击（霰弹枪）=敌方 1、2 号位的全部存活者。
        /// </summary>
        public List<BattleEnemy> SkillTargets(BattleState b, SkillExecConfig x, WeaponConfig w, int? target)
        {
            if (IsAreaShot(x, w)) return b.Alive.Where(e => e.Pos is 1 or 2).OrderBy(e => e.Pos).ToList();
            var t = target.HasValue ? BattleEnemyAt(b, target.Value) : null;
            if (t == null || !t.Alive) return new List<BattleEnemy>();
            if (x.TargetMode == "suffix") return SuffixTargets(b, t.Pos);
            if (x.TargetMode == "pair")
            {
                var set = t.Pos <= 2 ? new[] { 1, 2 } : new[] { 2, 3 };
                return b.Alive.Where(e => set.Contains(e.Pos)).OrderBy(e => e.Pos).ToList();
            }
            return new List<BattleEnemy> { t };
        }

        /// <summary>贯穿（Q82）：真实槽 k 至 4 的全部存活敌（按号位）；空位跳过但不重排。起点可以是空位，只要后面还有活目标。</summary>
        public static List<BattleEnemy> SuffixTargets(BattleState b, int start) =>
            b.Alive.Where(e => e.Pos >= start && e.Pos >= 1).OrderBy(e => e.Pos).ToList();

        /// <summary>贯穿对某目标的伤害倍率：按“目标号位−起点”的槽偏移取 0.65/0.50/0.35/0.25（空位占偏移，不重排）。</summary>
        public static double SuffixFactor(SkillExecConfig x, int start, int pos)
        {
            var k = pos - start;
            return k >= 0 && k < x.SuffixFactors.Count ? x.SuffixFactors[k] : 0;
        }

        /// <summary>一次攻击要用的弹药：A06 写明的（猎标 1、连射 2）；否则有弹药的武器按段数各 1，专长射击霰弹 1 发。</summary>
        public static int AmmoNeed(SkillExecConfig x, WeaponConfig w) =>
            w.AmmoId == null ? 0 : x.Ammo > 0 ? x.Ammo : IsAreaShot(x, w) ? 1 : Math.Max(1, x.Hits);

        // ───────────── 应急拳击的门槛 ─────────────

        /// <summary>
        /// 应急拳击的门槛（R03 §3）：两页已装备武器（空槽页为拳）的全部已解锁攻击技能里，最低的体力成本；体力低于它才可用应急拳击。
        /// 损坏/没子弹不算“付不起”（Q39）。两页都没有攻击技能（指挥+医疗器具）时为 0：应急拳击不开放（两槽满不另给拳）。
        /// </summary>
        public double MinEquippedAttackEnergy(GameState s)
        {
            var min = double.MaxValue;
            foreach (var page in new[] { 1, 2 })
            {
                WeaponOnPage(s, page, out var w);
                foreach (var n in Config.SkillNodes.Where(n => n.SkillId == w.SkillId && !n.IsSurvival && _exec.TryGetValue(n.Id, out var x) && x.IsAttack && SkillUnlocked(s, n)))
                    min = Math.Min(min, SkillEnergy(s, n, w, page));
            }
            return min == double.MaxValue ? 0 : min;
        }

        // ───────────── 指令列表 ─────────────

        /// <summary>
        /// 当前武器页的技能指令（Q26：未达等级的不列出；暂时缺 AP/E/弹药/耐久/冷却的列出写原因）与应急拳击。
        /// 战斗中可用性与原因取 CommandBlocked（与执行同源）；战斗外只写暂时条件（弹仓空、耐久不足、没有随身物资、需要队友）。
        /// </summary>
        private List<BattleCommandOption> SkillOptions(GameState s, bool inBattle)
        {
            var list = new List<BattleCommandOption>();
            var b = s.Battle;
            var w = CurrentWeapon(s, out var wi);
            foreach (var n in Config.SkillNodes.Where(n => n.SkillId == w.SkillId && !n.IsSurvival && _exec.ContainsKey(n.Id)).OrderBy(n => n.Level))
            {
                if (!SkillUnlocked(s, n)) continue;
                var x = _exec[n.Id];
                var id = ActionIdOf(n);
                var mode = OptionTargetMode(x, w);
                var opt = new BattleCommandOption
                {
                    Action = id, Label = n.Name, Group = id == BasicAttack ? "attack" : "move", Ap = n.Ap, Energy = SkillEnergy(s, n, w),
                    NeedsTarget = mode is "single" or "pair" or "suffix", TargetMode = mode, NodeId = n.Id, CooldownRounds = n.CooldownRounds, EffectText = n.Text,
                };
                if (inBattle && b != null)
                {
                    if (mode == "area")
                    {
                        opt.Reason = CommandBlocked(s, BattleCommand.Of(id));
                        if (opt.Reason == null) opt.Targets = SkillTargets(b, x, w, null).Select(e => e.Index).ToList();
                    }
                    else if (opt.NeedsTarget)
                    {
                        foreach (var e in b.Alive) if (CommandBlocked(s, BattleCommand.Of(id, e.Index)) == null) opt.Targets.Add(e.Index);
                        opt.Reason = CommandBlocked(s, BattleCommand.Of(id, opt.Targets.Count > 0 ? opt.Targets[0] : b.Alive.Select(e => (int?)e.Index).FirstOrDefault()));
                    }
                    else opt.Reason = CommandBlocked(s, BattleCommand.Of(id));
                }
                else opt.Reason = OutOfBattleSkillReason(s, n, x, w, wi);
                opt.Available = opt.Reason == null;
                list.Add(opt);
            }
            if (Act(DesperatePunch) is CombatActionConfig dp)
            {
                var opt = new BattleCommandOption { Action = DesperatePunch, Label = ActionName(DesperatePunch), Group = "attack", Ap = dp.ApCost, NeedsTarget = true, TargetMode = "single" };
                if (inBattle && b != null)
                {
                    foreach (var e in b.Alive) if (CommandBlocked(s, BattleCommand.Of(DesperatePunch, e.Index)) == null) opt.Targets.Add(e.Index);
                    opt.Reason = CommandBlocked(s, BattleCommand.Of(DesperatePunch, opt.Targets.Count > 0 ? opt.Targets[0] : b.Alive.Select(e => (int?)e.Index).FirstOrDefault()));
                }
                opt.Available = opt.Reason == null;
                list.Add(opt);
            }
            return list;
        }

        /// <summary>战斗外的技能条件（系统说明“指令一览”）：只写暂时条件，与战斗中原因同一措辞。</summary>
        private string? OutOfBattleSkillReason(GameState s, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi)
        {
            if (x.IsAttack)
            {
                if (wi != null && !HasWear(wi, Config.Wear.PerExecutedAttackCommand)) return $"{w.Name}耐久不足（{DurabilityText(wi)}）";
                if (!AmmoReady(s, w, wi, AmmoNeed(x, w))) return AmmoReason(w, x);
                if (x.NoSingleShot && w.UsesMagazine && w.MagazineCapacity < 2) return $"{w.Name}是单发弩，不能{n.Name}";
                return null;
            }
            return NonAttackStaticReason(s, n, x, w, wi);
        }

        private string AmmoReason(WeaponConfig w, SkillExecConfig x)
        {
            var need = AmmoNeed(x, w);
            if (w.UsesMagazine) return need > 1 ? $"弹仓不足{need}发：先装填" : "弹仓空了：先装填";
            return need > 1 ? $"随身{ItemName(w.AmmoId!)}不足{need}支" : $"随身没有{ItemName(w.AmmoId!)}";
        }

        // ───────────── 能否执行 ─────────────

        /// <summary>技能与应急拳击能否执行（只读；CommandBlocked 的技能分支）。顺序：解锁 → 冷却 → AP → 体力 → 耐久 → 弹药 → 使用位 → 目标。</summary>
        private string? SkillBlocked(GameState s, BattleState b, BattleCommand c, WeaponConfig w, ItemInstance? wi)
        {
            if (c.Action == DesperatePunch)
            {
                var dp = Act(DesperatePunch)!;
                if (dp.ApCost > b.PlayerAp) return $"行动点不足（需{dp.ApCost}，剩{b.PlayerAp}）";
                // R03 §3：只有付不起两页已装备攻击技能的最低体力成本时可用；弹仓空、耐久不足时应装填或切页，不放开（试玩修复 L1、Q39）
                if (s.Stats.Energy >= MinEquippedAttackEnergy(s) - 1e-9)
                    return "只在体力不足以使用任何已装备攻击技能时可用" + (w.UsesMagazine && !AmmoReady(s, w, wi, 1) ? "；弹仓空请装填或切换武器页" : wi != null && !HasWear(wi, Config.Wear.PerExecutedAttackCommand) ? "；耐久不足请切换武器页" : "");
                var fists = _weapons[Fists];
                if (!OwnPositionOk(b, fists)) return $"拳只能在我方{PosList(OwnPositionsOf(fists))}使用";
                return TargetBlocked(b, c, TargetPositionsOf(fists), fists.Name);
            }
            var n = SkillOf(c.Action, w);
            if (n == null)
            {
                if (c.Action == BasicAttack) return w.CanAttack ? "当前武器没有普通攻击" : SkillLockedReason(w);
                return $"当前武器（{w.Name}）不能用{ActionName(c.Action)}";
            }
            var x = _exec[n.Id];
            if (!SkillUnlocked(s, n)) return $"需要{SkillName(n.SkillId)}{n.Level}级（当前{SkillLevel(s, n.SkillId)}级）";
            if (CooldownReadyRound(b, n) is int ready) return $"冷却中（第{ready}轮起可用）";
            // 结构性原因（需要队友、耐久、单发弩、弹药、随身物资、弹仓满）先于“行动点不足”显示（试玩修复 M150 UX6）
            var structural = x.IsAttack
                ? wi != null && !HasWear(wi, Config.Wear.PerExecutedAttackCommand) ? $"{w.Name}耐久不足（{DurabilityText(wi)}），可切换武器页"
                  : x.NoSingleShot && w.UsesMagazine && w.MagazineCapacity < 2 ? $"{w.Name}是单发弩，不能{n.Name}"
                  : !AmmoReady(s, w, wi, AmmoNeed(x, w)) ? AmmoReason(w, x) : null
                : NonAttackStaticReason(s, n, x, w, wi);
            if (structural != null) return structural;
            if (n.Ap > b.PlayerAp) return $"行动点不足（需{n.Ap}，剩{b.PlayerAp}）";
            var energy = SkillEnergy(s, n, w);
            if (s.Stats.Energy < energy - 1e-9)
                return $"体力不足（需{Fmt.N(energy)}，现有{Fmt.N(s.Stats.Energy)}）" + (x.IsAttack ? "；可勉强拳击、防御、调整呼吸或撤退" : "");
            var own = SkillOwnPositions(n, w);
            // Q80：取消单人全武器兼容 1 号位，按真实己位限制（可先“调整站位”或切换武器页）
            if (!own.Contains(PlayerPos(b))) return $"{n.Name}只能在我方{PosList(own)}使用（你在{PosName(PlayerPos(b))}），可调整站位或切换武器页";
            if (!x.IsAttack) return NonAttackBlocked(s, b, c, n, x, w, wi);
            if (wi != null && !HasWear(wi, Config.Wear.PerExecutedAttackCommand)) return $"{w.Name}耐久不足（{DurabilityText(wi)}），可切换武器页";
            if (x.NoSingleShot && w.UsesMagazine && w.MagazineCapacity < 2) return $"{w.Name}是单发弩，不能{n.Name}";
            if (!AmmoReady(s, w, wi, AmmoNeed(x, w))) return AmmoReason(w, x);
            // 突进斩（Q80）：使用位 123；1 号位原地出手，2/3 号位前移一格（前格空则移、有同伴则交换）后攻击；4 号位由使用位拒绝
            var targets = SkillTargetPositions(s, n, w);
            if (IsAreaShot(x, w)) return SkillTargets(b, x, w, null).Count == 0 ? "敌方1、2号位没有目标" : null;
            return TargetBlocked(b, c, targets, n.Name);
        }

        private string? TargetBlocked(BattleState b, BattleCommand c, HashSet<int> targets, string what)
        {
            if (!c.Target.HasValue) return "请选择目标";
            var t = BattleEnemyAt(b, c.Target.Value);
            if (t == null || !t.Alive) return "目标已不在";
            if (!targets.Contains(t.Pos)) return $"射程不足：{EnemyName(t)}在{PosName(t.Pos)}，{what}只能打{PosList(targets)}";
            return null;
        }

        // ───────────── 攻击计算（预览与结算同源） ─────────────

        /// <summary>一次攻击的口径：出手武器、吃哪一页的攻击类词条、技能倍率与加成。</summary>
        private sealed class AttackSpec
        {
            public string Action = "";
            public SkillNodeConfig? Node;
            public SkillExecConfig? Exec;
            public WeaponConfig Weapon = null!;
            public int Page;
            public bool Desperate;
            public double DamageFactor = 1;
            public double AccuracyFactor = 1;
            public double AccuracyBonus;
            public double ExtraPen;
            public int Hits = 1;
            /// <summary>反击/迎击等反应攻击：不吃鼓舞、总攻令、斧蓄力，不扣成本。</summary>
            public bool Reaction;
            /// <summary>贯穿（Q82）：提交时锁定的起点号位；各目标倍率按槽偏移取（0 为不是贯穿）。</summary>
            public int SuffixStart;

            /// <summary>对某目标的技能伤害倍率：贯穿按槽偏移，其余为统一 DamageFactor。</summary>
            public Dictionary<int, int> SnapPos = new Dictionary<int, int>();
            public double FactorFor(BattleEnemy t) => SuffixStart > 0 && Exec != null ? SuffixFactor(Exec, SuffixStart, SnapPos.TryGetValue(t.Index, out var p) ? p : t.Pos) : DamageFactor;
        }

        private AttackSpec SpecFor(GameState s, string action, WeaponConfig w)
        {
            if (action == DesperatePunch)
            {
                var a = Act(DesperatePunch)!;
                return new AttackSpec { Action = action, Weapon = _weapons[Fists], Page = 0, Desperate = true, DamageFactor = a.DamageFactor, AccuracyFactor = a.AccuracyRatingFactor };
            }
            var n = SkillOf(action, w)!;
            var x = _exec[n.Id];
            var sp = new AttackSpec
            {
                Action = action, Node = n, Exec = x, Weapon = w, Page = s.Battle?.ActiveWeaponSlot ?? DefaultWeaponPage(s),
                DamageFactor = IsAreaShot(x, w) ? x.AreaDamageFactor : x.DamageFactor, AccuracyBonus = x.AccuracyBonus, ExtraPen = x.ArmorIgnore, Hits = x.Hits,
            };
            // 武器固定特性（A05 §1）：扫切=流血技能命中+4、钩刺=逼退（击退）命中+8、稳枪=火力压制（虚弱）命中+6
            if (x.Effects.Any(e => e.Status == "bleed")) sp.AccuracyBonus += w.Trait("bleedAccuracy");
            if (x.Effects.Any(e => e.Status == "knockback")) sp.AccuracyBonus += w.Trait("knockbackAccuracy");
            if (x.Effects.Any(e => e.Status == "weak") && w.SkillId == "firearms") sp.AccuracyBonus += w.Trait("suppressAccuracy");
            return sp;
        }

        private static bool Active(int untilRound, BattleState b) => untilRound >= b.Round;
        private static double ActiveArmorBreak(BattleState b, BattleEnemy t) => Active(t.ArmorBreakUntilRound, b) ? t.ArmorBreak : 0;
        public static bool EnemyMarked(BattleState b, BattleEnemy t) => t.MarkBonus > 0 && Active(t.MarkUntilRound, b);
        public static bool EnemyWeak(BattleState b, BattleEnemy t) => t.WeakFactor < 1 && Active(t.WeakUntilRound, b);

        /// <summary>
        /// 某攻击对某目标的命中、暴击与伤害区间（R03 §4；体力按支付本次成本后；不推进随机；预览与结算同源）：
        /// 攻击能力 A=max(1,(武器A+3L+8+装备命中+技能/标记/鼓舞加成)×招式×疲劳×恐慌×失衡)，命中 P=A/(A+max(1,目标闪避))；暴击 C=max(0,武器C+0.3L+装备暴击)；
        /// 直接伤害=max(0, 抽值×(1+0.035L)×技能倍率×处决×伤害增益(≤1.5)×虚弱 + 平加 − max(0,护甲−破甲−穿透))，再乘格挡。
        /// 伤害增益同名取强、不同来源乘算：装备伤害乘数、总攻令、斧蓄力、武器特性（狩猎/精校/救援）。
        /// </summary>
        private BattleTargetPreview TargetCalc(GameState s, BattleState b, AttackSpec sp, BattleEnemy t, double energyAfter)
        {
            var cfg = _enemies[t.EnemyId];
            var m = Config.CombatMath;
            var w = sp.Weapon;
            var lv = SkillLevel(s, w.SkillId);
            var gear = EquipmentEffects(s, GameState.PlayerId, sp.Page);
            var marked = EnemyMarked(b, t);
            var bonus = sp.AccuracyBonus + (marked ? t.MarkBonus + w.Trait("markedAccuracy") : 0) + (!sp.Reaction && InspireActive(b) ? b.InspireBonus : 0);
            // Q67：我方攻击能力另加基础 8（攻击能力项，不是命中率百分点）；疲劳一次×0.95
            var why = new List<string>();
            var body = BodyAccuracyFactor(s, b, energyAfter, why);
            var acc = Math.Max(m.MinAccuracy, (w.AccuracyRating + SkillAccuracyBonus(lv) + m.PlayerAccuracyAssistance + gear.Accuracy + bonus) * sp.AccuracyFactor * body);
            var parts = new List<string> { $"武器{Fmt.T(w.AccuracyRating)}", $"熟练{Fmt.T(SkillAccuracyBonus(lv))}", $"基础{Fmt.T(m.PlayerAccuracyAssistance)}" };
            if (Math.Abs(gear.Accuracy) > 1e-12) parts.Add($"装备{Fmt.T(gear.Accuracy)}");
            if (Math.Abs(bonus) > 1e-12) parts.Add($"技能/标记/鼓舞{Fmt.T(bonus)}");
            var mults = new List<string>();
            if (Math.Abs(sp.AccuracyFactor - 1) > 1e-12) mults.Add($"招式×{Fmt.T(sp.AccuracyFactor)}");
            mults.AddRange(why);
            var accText = string.Join("＋", parts) + (mults.Count > 0 ? "，再乘" + string.Join("、", mults) : "");
            var crit = Math.Max(0, w.CritRatingBase + m.CritAccuracyPerLevel * lv + gear.Crit);
            var buff = 1 + gear.DamageMultiplier;
            if (!sp.Reaction && AllOutActive(b)) buff *= b.AllOutFactor;
            if (!sp.Reaction && AxeChargeActive(s, b, sp)) buff *= b.ChargeFactor;
            if (cfg.Family == "animal") buff *= w.Trait("animalDamage", 1);
            if (marked) buff *= w.Trait("markedDamage", 1);
            if (t.ProtectingIndex > 0) buff *= w.Trait("protectorDamage", 1);
            var rawBuff = buff;
            buff = DamageBuffCurve(rawBuff);   // Q82：同名取强、不同来源先乘成 rawB，再走递减曲线（取代旧硬上限×1.5）
            var execF = sp.Exec?.ExecuteHpRatio is double r && t.Hp <= cfg.Hp * r + 1e-9 ? sp.Exec.ExecuteFactor : 1;
            var weak = PlayerWeakActive(b) ? b.PlayerWeakFactor : 1;
            var mult = SkillDamageFactor(lv) * sp.FactorFor(t) * buff * execF * weak;
            var pen = gear.ArmorIgnore + (sp.Desperate ? 0 : w.ArmorIgnore) + sp.ExtraPen;
            var baseArmor = Math.Max(0, Math.Min(Config.Equipment.Limits.ArmorCap, cfg.Armor) - ActiveArmorBreak(b, t));
            var armor = Math.Max(0, baseArmor - pen);
            var guarded = t.Guard && (sp.Exec?.RemoveGuard ?? 0) == 0;
            var flat = sp.DamageFactor > 0 && w.DamageMax > 0 ? FlatAttack(s) : 0;
            double Dmg(double raw, double critF) => AttackDamage(raw * mult * critF, flat, armor, guarded, m.GuardFactor);
            var cf = m.CritFactor;
            var evasion = Math.Max(m.MinEvasion, cfg.Dodge);
            return new BattleTargetPreview
            {
                Index = t.Index, Name = cfg.Name, Slot = t.Slot, Hp = t.Hp, Armor = baseArmor, Guarded = guarded, RearPenalty = false,
                Penetration = pen, DamageBuff = buff, RawDamageBuff = rawBuff, Multiplier = mult, Flat = flat,
                AccuracyRating = acc, AccuracyText = accText, Evasion = evasion, HitChance = HitChance(acc, evasion),
                CritRating = crit, CritChance = CritChance(crit),
                DamageMin = Dmg(w.DamageMin, 1), DamageMax = Dmg(w.DamageMax, 1), CritDamageMin = Dmg(w.DamageMin, cf), CritDamageMax = Dmg(w.DamageMax, cf),
            };
        }

        /// <summary>
        /// 正伤害增益递减（Q82，取代旧硬上限×1.5）：rawB≤1.35 原值；以上 B=1.35+0.25×(1−exp(−(rawB−1.35)/0.25))，渐近 1.6、单调递增，
        /// 每个合法新增正增益仍有正收益。rawB&lt;1（减益）原值。
        /// </summary>
        public double DamageBuffCurve(double rawB)
        {
            var knee = Config.CombatMath.DamageBuffKnee;
            var tail = Config.CombatMath.DamageBuffTail;
            if (rawB <= knee || tail <= 0) return rawB;
            return knee + tail * (1 - Math.Exp(-(rawB - knee) / tail));
        }

        /// <summary>攻击类预览：目标、伤害、弹药、耐久、噪声与技能说明（只读）。</summary>
        private void AttackPreview(GameState s, BattleState b, BattleCommand c, BattlePreview p, WeaponConfig w, ItemInstance? wi)
        {
            var sp = SpecFor(s, c.Action, w);
            p.EnergyCost = sp.Desperate ? 0 : SkillEnergy(s, sp.Node!, w);
            p.EnergyAfter = Math.Max(0, s.Stats.Energy - p.EnergyCost);
            var targets = sp.Desperate
                ? (c.Target.HasValue && BattleEnemyAt(b, c.Target.Value) is BattleEnemy one ? new List<BattleEnemy> { one } : new List<BattleEnemy>())
                : SkillTargets(b, sp.Exec!, w, c.Target);
            if (!sp.Desperate && sp.Exec!.TargetMode == "suffix" && c.Target.HasValue && BattleEnemyAt(b, c.Target.Value) is BattleEnemy st0) { sp.SuffixStart = st0.Pos; foreach (var t in targets) sp.SnapPos[t.Index] = t.Pos; }
            // B9（A10 §4.1/§4.5）：单体攻击会被保护者接下时，预测改按真实承伤者算，并写明“瞄准A，实际由B承受”
            if (ProtectRedirect(b, sp, targets) is BattleEnemy guard)
            {
                var aimed = targets[0];
                p.Notes.Add($"瞄准{PosName(aimed.Pos)}{EnemyName(aimed)}，实际由{PosName(guard.Pos)}{EnemyName(guard)}承受（它在保护{EnemyName(aimed)}，单体攻击由它接下）");
                targets = new List<BattleEnemy> { guard };
                var tp = TargetCalc(s, b, sp, guard, p.EnergyAfter);
                tp.AimedIndex = aimed.Index;
                p.Targets.Add(tp);
            }
            else foreach (var t in targets) p.Targets.Add(TargetCalc(s, b, sp, t, p.EnergyAfter));
            if (!sp.Desperate)
            {
                var x = sp.Exec!;
                var need = AmmoNeed(x, w);
                if (w.UsesMagazine && wi != null && need > 0) p.AmmoText = $"弹仓{wi.LoadedAmmo}→{Math.Max(0, wi.LoadedAmmo - need)}/{w.MagazineCapacity}";
                else if (w.ConsumesLooseAmmo && need > 0) p.AmmoText = $"随身{ItemName(w.AmmoId!)}{CarriedCount(s, w.AmmoId!)}→{Math.Max(0, CarriedCount(s, w.AmmoId!) - need)}";
                if (wi?.Durability != null) p.WearText = $"{w.Name}耐久{Fmt.N(wi.Durability.Value)}→{Fmt.N(Math.Max(0, wi.Durability.Value - Config.Wear.PerExecutedAttackCommand))}（命中与否都扣）";
                p.NoiseAfter = Math.Min(Config.Noise.Max, b.Noise + AttackNoise(s, w));
                foreach (var note in SkillNotes(s, b, sp, targets)) p.Notes.Add(note);
            }
            else p.Notes.Add("用拳头勉强一击：不磨损原武器，伤害×0.5，命中能力×0.65，没有技能附加控制");
            var lv = SkillLevel(s, sp.Weapon.SkillId);
            p.Notes.Add($"{SkillName(sp.Weapon.SkillId)}{lv}级：伤害×{Fmt.T(SkillDamageFactor(lv))}，命中能力+{Fmt.T(SkillAccuracyBonus(lv))}；" +
                (SkillMaxed(s, sp.Weapon.SkillId) ? XpText(s, sp.Weapon.SkillId, 0)
                    : $"每命中一个目标+{Config.Xp.DamagingAttackCommand}经验（含被护甲挡住的命中，同一指令多段只计1次，每个敌人每技能最多{Config.Xp.MaxHitsPerEncounterEnemySkill}次）"));
            var fl = sp.DamageFactor > 0 && sp.Weapon.DamageMax > 0 ? FlatAttack(s) : 0;
            if (fl > 0) p.Notes.Add($"词条攻击力+{Fmt.T(fl)}：在技能/熟练/暴击/增益乘算后、扣护甲前平加（不被倍率放大），同一目标每条指令只加在第一下命中{(sp.Hits > 1 ? "（下方区间按第一下；第二下不再加）" : "")}，防御再×{Fmt.T(Config.CombatMath.GuardFactor)}");
            if (p.Targets.FirstOrDefault(x => x.RawDamageBuff > Config.CombatMath.DamageBuffKnee + 1e-9) is BattleTargetPreview hb)
                p.Notes.Add($"伤害增益合计×{Fmt.T(hb.RawDamageBuff)}，超过×{Fmt.T(Config.CombatMath.DamageBuffKnee)}的部分递减，实际×{Fmt.T(hb.DamageBuff)}（最多接近×{Fmt.T(Config.CombatMath.DamageBuffKnee + Config.CombatMath.DamageBuffTail)}）");
        }

        /// <summary>技能效果说明（预览）：段数、目标方式、附带状态与控制率、冷却、增益消耗。</summary>
        private List<string> SkillNotes(GameState s, BattleState b, AttackSpec sp, List<BattleEnemy> targets)
        {
            var n = sp.Node!;
            var x = sp.Exec!;
            var w = sp.Weapon;
            var notes = new List<string> { $"{n.Name}：{n.Text}" };
            if (x.Hits > 1) notes.Add($"同一目标{x.Hits}次独立命中，每击伤害×{Fmt.T(x.DamageFactor)}（下方区间为每击）");
            if (IsAreaShot(x, w)) notes.Add($"霰弹散布：敌方1、2号位各判一次，伤害×{Fmt.T(x.AreaDamageFactor)}，只耗1发");
            if (x.TargetMode == "suffix")
                notes.Add(sp.SuffixStart > 0
                    ? $"贯穿从{PosName(sp.SuffixStart)}打到4号位：" + string.Join("，", targets.Select(t => $"{PosName(t.Pos)}{EnemyName(t)}×{Fmt.T(SuffixFactor(x, sp.SuffixStart, t.Pos))}")) + "；每个目标独立判命中/暴击/护甲，空位不重排，途中倒下不补位"
                    : "点起点敌人：从它的号位打到4号位，按离起点的格数伤害×" + string.Join("/", x.SuffixFactors.Select(Fmt.T)));
            if (x.ExecuteHpRatio is double r) notes.Add($"目标健康≤上限{Fmt.Pct(r)}时伤害再×{Fmt.T(x.ExecuteFactor)}");
            if (x.RemoveGuard > 0) notes.Add("先移除目标1次格挡，再结算伤害");
            if (x.Advance > 0) notes.Add(PlayerPos(b) <= 1 ? "在1号位：原地出手，不前移" : $"先前移到{PosName(PlayerPos(b) - 1)}再攻击（{(AllyAt(b, PlayerPos(b) - 1) != null ? "与前面的同伴交换" : "前面空着，直接移过去")}；不另付调整站位的AP）");
            foreach (var fx in x.Effects)
            {
                var t0 = targets.FirstOrDefault();
                var cc = fx.Control && t0 != null ? $"，控制率{Fmt.Pct(ControlChance(0, _enemies[t0.EnemyId].ControlResist + (t0.SteadyUntilRound >= b.Round ? Config.CombatMath.SteadyResistBonus : 0)))}" : "";
                notes.Add(StatusNote(fx, w) + (fx.Control ? $"（命中后判控制{cc}）" : fx.Positive ? "（须造成正伤害）" : "（命中即施加）"));
            }
            if (n.CooldownRounds > 0) notes.Add($"冷却{n.CooldownRounds}轮：本轮用后第{b.Round + n.CooldownRounds + 1}轮起可再用（两武器页与追击共享）");
            if (InspireActive(b)) notes.Add($"鼓舞：本次命中能力+{Fmt.T(b.InspireBonus)}（用掉）");
            if (AllOutActive(b)) notes.Add($"总攻令：本次伤害×{Fmt.T(b.AllOutFactor)}（用掉）");
            if (AxeChargeActive(s, b, sp)) notes.Add($"斧蓄力：本次伤害×{Fmt.T(b.ChargeFactor)}（用掉）");
            foreach (var t in targets.Where(t => EnemyMarked(b, t))) notes.Add($"{EnemyName(t)}已被标记：命中能力+{Fmt.T(t.MarkBonus + w.Trait("markedAccuracy"))}");
            return notes;
        }

        private string StatusNote(SkillStatusConfig fx, WeaponConfig w) => fx.Status switch
        {
            "stagger" => w.Trait("staggerUntilAttack") > 0 ? "失衡：目标下一次攻击命中能力×0.9，持续到它下次攻击后" : "失衡：目标下一次攻击命中能力×0.9",
            "slow" => $"减速：目标下轮速度−{Fmt.T(Config.Statuses.Slow.NextRoundSpeedPenalty)}",
            "weak" => $"虚弱：目标直接伤害×{Fmt.T(fx.Value)}，{fx.Rounds}轮",
            "mark" => $"标记：我方对它攻击命中能力+{Fmt.T(fx.Value)}，{fx.Rounds}轮",
            "armor_break" => $"破甲：护甲−{Fmt.T(fx.Value + w.Trait("armorBreakBonus"))}，{fx.Rounds}轮（最低0）",
            "knockback" => "击退：目标往后移1格（后面空着就移过去，有同伴就互换；已在4号位不移、不判控制）",
            "cancel_charge" => "打断：取消目标本轮还没执行的蓄力，改为普通攻击" + (w.Trait("interruptSlow") > 0 ? $"，下轮速度额外−{Fmt.T(w.Trait("interruptSlow"))}" : ""),
            "bleed" => $"流血：每大回合开始−{Fmt.T(Config.Statuses.Bleed.DamagePerOwnActivation)}，共{Config.Statuses.Bleed.RefreshMaxTriggers + (int)w.Trait("bleedTriggers")}次（丧尸免疫）",
            _ => fx.Status,
        };

        // ───────────── 执行攻击 ─────────────

        /// <summary>
        /// 攻击技能与应急拳击：先原子扣体力、弹药、耐久与噪声，记冷却；再逐目标、逐段独立判命中 → 暴击 → 伤害；命中后施加技能状态（需控制的再判控制）；
        /// 经验逐命中目标计（同一指令多段对同目标只计 1 次）。单体攻击打到正在被保护的敌人时由保护者接下（R03 §4）。
        /// </summary>
        private void PlayerSkillAttack(GameState s, BattleState b, BattleCommand c, WeaponConfig w, ItemInstance? wi)
        {
            var sp = SpecFor(s, c.Action, w);
            var weapon = sp.Weapon;
            var spent = new List<string>();
            var energy = sp.Desperate ? 0 : SkillEnergy(s, sp.Node!, w);
            s.Stats.Add(StatId.Energy, -energy);
            if (energy > 0) spent.Add($"体力−{Fmt.N(energy)}");
            var parts = new List<string>();
            if (!sp.Desperate)
            {
                var x = sp.Exec!;
                var need = AmmoNeed(x, w);
                if (w.UsesMagazine && wi != null && need > 0) { wi.LoadedAmmo -= need; spent.Add($"弹仓{wi.LoadedAmmo}/{w.MagazineCapacity}"); b.ShotsFired += need; }
                else if (w.ConsumesLooseAmmo && need > 0) { TakeCarried(s, w.AmmoId!, need); spent.Add($"{ItemName(w.AmmoId!)}−{need}"); b.ShotsFired += need; }
                if (wi?.Durability != null) spent.Add(Wear(wi, Config.Wear.PerExecutedAttackCommand));
                AddNoise(s, b, AttackNoise(s, w));
                b.SkillUsedRound[sp.Node!.Id] = b.Round;
                if (x.Advance > 0) parts.Add(PlayerPos(b) <= 1 ? "在1号位原地出手" : AdvancePlayer(b));
            }
            var targets = sp.Desperate ? new List<BattleEnemy> { BattleEnemyAt(b, c.Target!.Value)! } : SkillTargets(b, sp.Exec!, w, c.Target);
            // 贯穿（Q82）：提交时锁全部目标与起点号位，途中倒下不把后面的目标补到前格刷倍率
            if (!sp.Desperate && sp.Exec!.TargetMode == "suffix" && targets.Count > 0)
            {
                sp.SuffixStart = targets[0].Pos;
                foreach (var t in targets) sp.SnapPos[t.Index] = t.Pos;
            }
            // 单体直接攻击打到被保护者：由保护者接下（不拦范围攻击；R03 §4、A09 保护）
            if (ProtectRedirect(b, sp, targets) is BattleEnemy guard)
            {
                parts.Add($"{EnemyName(guard)}挡在{EnemyName(targets[0])}前面接下了这一击");
                guard.ProtectingIndex = 0;
                targets = new List<BattleEnemy> { guard };
            }
            var hitTargets = new List<BattleEnemy>();
            var energyAfter = s.Stats.Energy;
            foreach (var t in targets)
            {
                var cfg = _enemies[t.EnemyId];
                // 主动攻击中立雄鹿：转敌对
                if (!t.Hostile)
                {
                    t.Hostile = true;
                    if (FindWorldEnemy(s, t.WorldId) is WorldEnemy we) we.Hostile = true;
                    RelockAsAttack(s, t);
                    parts.Add($"{cfg.Name}被激怒");
                }
                var anyHit = false;
                var positive = false;
                // 教学攻击力 +0.1（Q82）：每个目标每条指令只在第一次命中时平加一次（未命中不结、护甲挡成 0 也算用过），连击不变 +0.2
                var flatUsed = false;
                for (var hit = 0; hit < sp.Hits && t.Alive; hit++)
                {
                    var seg = sp.Hits > 1 ? $"第{hit + 1}击" : "";
                    if (!sp.Desperate && sp.Exec!.RemoveGuard > 0 && t.Guard) { t.Guard = false; parts.Add($"{cfg.Name}的格挡被破除"); }
                    var pv = TargetCalc(s, b, sp, t, energyAfter);
                    if (s.Rng.NextDouble() >= pv.HitChance) { parts.Add($"{cfg.Name}{seg}未命中（{Fmt.Pct(pv.HitChance)}）"); continue; }
                    anyHit = true;
                    var crit = s.Rng.NextDouble() < pv.CritChance;
                    var raw = weapon.DamageMin + s.Rng.NextDouble() * (weapon.DamageMax - weapon.DamageMin);
                    var multiplied = raw * pv.Multiplier * (crit ? Config.CombatMath.CritFactor : 1);
                    var armor = Math.Max(0, pv.Armor - pv.Penetration);
                    var flat = flatUsed ? 0 : pv.Flat;
                    flatUsed = true;
                    var guarded = pv.Guarded && Math.Max(0, multiplied + flat - armor) > 0;
                    var dmg = AttackDamage(multiplied, flat, armor, guarded, Config.CombatMath.GuardFactor);
                    if (guarded) t.Guard = false;
                    if (sp.DamageFactor <= 0) { parts.Add($"{cfg.Name}{seg}被命中"); continue; }
                    if (dmg <= 0) { parts.Add($"{cfg.Name}{seg}被护甲完全挡住"); continue; }
                    positive = true;
                    t.Hp -= dmg;
                    parts.Add($"{cfg.Name}{seg}{(crit ? "暴击" : "命中")}−{Fmt.N(dmg)}{(guarded ? "（防御减半）" : "")}");
                    if (t.Hp <= 1e-9) KillEnemy(s, t);
                }
                if (anyHit) hitTargets.Add(t);
                if (anyHit && !sp.Desperate && t.Alive) ApplySkillStatuses(s, b, sp, t, positive, parts);
                if (!t.Alive && t.Dead) parts.Add($"{cfg.Name}倒下");
                else if (t.Alive) CheckFlee(s, t);
            }
            b.PlayerStagger = false;   // 失衡在本次攻击结算后消失
            ConsumeAttackBuffs(s, b, sp);
            var xpNote = AttackXp(s, weapon.SkillId, hitTargets);
            BattleLog(s, $"你{(sp.Desperate ? ActionName(DesperatePunch) : sp.Node!.Name)}{(sp.Desperate ? "" : $"（{weapon.Name}）")}：{string.Join("，", parts)}{(spent.Count > 0 ? "；" + string.Join("，", spent) : "")}{xpNote}。");
            if (s.Stats.Health <= 0) { BattleDeath(s, s.Progress.LastDamageCause ?? "喷溅"); return; }
            CheckVictory(s);
        }

        private bool IsAreaShotSpec(AttackSpec sp) => sp.Exec != null && IsAreaShot(sp.Exec, sp.Weapon);

        /// <summary>
        /// 单体直接攻击打到被保护者时接下这一击的保护者（R03 §4、A09 保护；不拦范围攻击）。执行与预览共用（A10 §4.1/§4.5：
        /// 预览写“瞄准A，实际由B承受”，与实际同源）；没有重定向返回 null。
        /// </summary>
        private BattleEnemy? ProtectRedirect(BattleState b, AttackSpec sp, List<BattleEnemy> targets) =>
            targets.Count == 1 && (sp.Desperate || sp.Exec!.TargetMode == "single") && !IsAreaShotSpec(sp) ? ProtectorOf(b, targets[0]) : null;

        /// <summary>噪声增加（满 100 时请求一次增援；v0.6 §15）。</summary>
        private void AddNoise(GameState s, BattleState b, double gain)
        {
            if (gain <= 0) return;
            b.Noise = Math.Min(Config.Noise.Max, b.Noise + gain);
            if (b.Noise >= Config.Noise.Max - 1e-9 && !b.ReinforcementRequested && !b.ReinforcementUsed && Config.Noise.MaxReinforcementsPerBattle > 0)
            {
                b.ReinforcementRequested = true;
                BattleLog(s, "噪声满了：附近的丧尸可能被吸引过来。");
            }
        }

        /// <summary>
        /// 攻击经验（A06 §3、R01 §3）：逐命中目标计——每个命中且该目标该门未满 3 次的目标各 +2（含护甲全抵的 0 伤、无伤控制命中），
        /// 同一指令多段对同目标只计 1 次；计数随 worldEnemyId 保存，撤退、巡游、换 encounterId 都不重置。返回日志后缀。
        /// </summary>
        private string AttackXp(GameState s, string skill, IEnumerable<BattleEnemy> hitTargets)
        {
            var gained = 0;
            foreach (var t in hitTargets.Distinct())
            {
                t.XpHits.TryGetValue(skill, out var hits);
                if (hits >= Config.Xp.MaxHitsPerEncounterEnemySkill) continue;
                t.XpHits[skill] = hits + 1;
                if (FindWorldEnemy(s, t.WorldId) is WorldEnemy we) we.XpHits[skill] = hits + 1;
                if (SkillMaxed(s, skill)) continue;      // Lv10 停止获取该门经验（A06 §1）；计数照记
                GainXp(s, skill, Config.Xp.DamagingAttackCommand);
                gained += Config.Xp.DamagingAttackCommand;
            }
            return gained > 0 ? $"；{SkillName(skill)}经验+{gained}" : "";
        }

        /// <summary>技能附带状态（命中后；需控制的再判控制，R03 §4）。</summary>
        private void ApplySkillStatuses(GameState s, BattleState b, AttackSpec sp, BattleEnemy t, bool positive, List<string> parts)
        {
            var cfg = _enemies[t.EnemyId];
            var w = sp.Weapon;
            foreach (var fx in sp.Exec!.Effects)
            {
                if (!t.Alive) break;
                if (fx.Positive && !positive) continue;
                if (fx.Status == "bleed")
                {
                    if (cfg.ImmuneBleed) { parts.Add($"{cfg.Name}不会流血"); continue; }
                    var bl = Config.Statuses.Bleed;
                    t.BleedTriggers = bl.RefreshMaxTriggers + (int)w.Trait("bleedTriggers");
                    t.BleedDamage = bl.DamagePerOwnActivation;
                    parts.Add($"{cfg.Name}出血（{Fmt.T(t.BleedDamage)}×{t.BleedTriggers}）");
                    continue;
                }
                // 击退（Q80）：目标已在 4 号位时没有可去的格，不移动、不判控制、不给稳态
                if (fx.Status == "knockback" && t.Pos >= Config.Scope.Slots.Count) { parts.Add($"{cfg.Name}已在{PosName(t.Pos)}，无法再被击退（不判控制）"); continue; }
                var ctl = "";
                if (fx.Control)
                {
                    var steady = t.SteadyUntilRound;
                    if (!RollControl(s, b, cfg.ControlResist, ref steady, out var cc))
                    {
                        parts.Add($"{cfg.Name}稳住了，没有{StatusVerb(fx.Status)}（控制{Fmt.Pct(cc)}）");
                        continue;
                    }
                    t.SteadyUntilRound = steady;
                    ctl = $"（控制{Fmt.Pct(cc)}）";
                }
                switch (fx.Status)
                {
                    case "stagger":
                        if (cfg.StaggerImmuneOnce && !t.StaggerImmuneUsed) { t.StaggerImmuneUsed = true; parts.Add($"{cfg.Name}的护盔挡住了冲击，首次失衡免疫{ctl}"); break; }
                        t.Stagger = true;
                        t.StaggerRound = b.Round;
                        t.StaggerSticky = w.Trait("staggerUntilAttack") > 0;
                        parts.Add($"{cfg.Name}失衡了{ctl}");
                        break;
                    case "slow":
                        t.SlowPending = true;
                        parts.Add($"{cfg.Name}下轮速度−{Fmt.T(Config.Statuses.Slow.NextRoundSpeedPenalty)}{ctl}");
                        break;
                    case "weak":
                        t.WeakFactor = EnemyWeak(b, t) ? Math.Min(t.WeakFactor, fx.Value) : fx.Value;
                        t.WeakUntilRound = Math.Max(EnemyWeak(b, t) ? t.WeakUntilRound : 0, b.Round + fx.Rounds);
                        parts.Add($"{cfg.Name}虚弱（伤害×{Fmt.T(t.WeakFactor)}，到第{t.WeakUntilRound}轮末）{ctl}");
                        break;
                    case "mark":
                        t.MarkBonus = EnemyMarked(b, t) ? Math.Max(t.MarkBonus, fx.Value) : fx.Value;
                        t.MarkUntilRound = Math.Max(EnemyMarked(b, t) ? t.MarkUntilRound : 0, b.Round + fx.Rounds);
                        parts.Add($"{cfg.Name}被标记（命中能力+{Fmt.T(t.MarkBonus)}，到第{t.MarkUntilRound}轮末）");
                        break;
                    case "armor_break":
                    {
                        var v = fx.Value + w.Trait("armorBreakBonus");
                        var on = Active(t.ArmorBreakUntilRound, b) && t.ArmorBreak > 0;
                        t.ArmorBreak = on ? Math.Max(t.ArmorBreak, v) : v;
                        t.ArmorBreakUntilRound = Math.Max(on ? t.ArmorBreakUntilRound : 0, b.Round + fx.Rounds);
                        parts.Add($"{cfg.Name}破甲（护甲−{Fmt.T(t.ArmorBreak)}，到第{t.ArmorBreakUntilRound}轮末）");
                        break;
                    }
                    case "knockback":
                    {
                        // Q80：后移一格——后格空则移入，有同侧存活同伴则一次原子交换（被换的同伴不另判控制、不多激活）
                        var from = t.Pos;
                        var behind = b.Alive.FirstOrDefault(e => e.Pos == from + 1);
                        if (behind != null) behind.Slot = from.ToString();
                        t.Slot = (from + 1).ToString();
                        parts.Add($"{cfg.Name}被击退到{PosName(t.Pos)}{(behind != null ? $"（{EnemyName(behind)}到{PosName(from)}）" : "")}{ctl}");
                        break;
                    }
                    case "cancel_charge":
                    {
                        var extra = w.Trait("interruptSlow");
                        if (extra > 0) t.SlowExtra = extra;
                        if (t.Intent == "charge_prepare" && !t.Acted)
                        {
                            RelockAsAttack(s, t);
                            parts.Add($"{cfg.Name}的蓄力被打断，改为普通攻击{ctl}");
                        }
                        else parts.Add($"{cfg.Name}被打断（本轮没有待执行的蓄力）{ctl}");
                        if (extra > 0) parts.Add($"{cfg.Name}下轮速度额外−{Fmt.T(extra)}");
                        break;
                    }
                }
            }
        }

        private static string StatusVerb(string status) => status switch
        {
            "stagger" => "失衡",
            "slow" => "被减速",
            "weak" => "虚弱",
            "knockback" => "被击退",
            "cancel_charge" => "被打断",
            _ => "受影响",
        };

        /// <summary>突进斩（Q80）：前移一格——前格空则移入，有同伴则交换；在 1 号位时不调用（原地出手）。</summary>
        private string AdvancePlayer(BattleState b)
        {
            var me = PlayerPos(b);
            var mate = AllyAt(b, me - 1);
            MoveAlly(b, me, me - 1);
            return $"你前移到{PosName(PlayerPos(b))}{(mate != null ? "（与同伴交换）" : "")}";
        }
    }
}

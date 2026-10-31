using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 回合制战斗（v0.5 §06–§12，v0.6 §04–§07 改数值与概率）：一名玩家对敌方 F1/F2/B1 至多3敌；每轮按先手值排一次队列，
    /// 玩家激活得 2AP、敌人激活执行一个公开意图；命中概率 A/(A+D)、命中后暴击 C/(C+100)、小数伤害不取整、无最低1伤；
    /// 每个有效轮只推进一次 5 分钟世界 tick（中途胜利/撤退在退出时推进一次）；每条指令、每次敌方行动后都可存档，读档从游标继续。
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string Fists = "fists";
        private const double PlayerIndex = 0;

        private CombatActionConfig? Act(string id) => Config.CombatActions.FirstOrDefault(a => a.Id == id);

        public static string ActionName(string id) => id switch
        {
            "basic_attack" => "普通攻击",
            "desperate_punch" => "勉强拳击",
            "guard" => "防御",
            "breathe" => "调整呼吸",
            "switch_weapon" => "切换武器页",
            "swap" => "调整站位",
            "reload" => "装填",
            "bandage" => "包扎",
            "use_antibiotics" => "用抗生素",
            "use_antiviral_cure" => "用特效药",
            "retreat" => "撤退",
            "end_turn" => "结束行动",
            _ => SkillNames.TryGetValue(id, out var n) ? n : id,
        };

        // ───────────── 查询 ─────────────

        public BattleEnemy? BattleEnemyAt(BattleState b, int index) => b.Enemies.FirstOrDefault(e => e.Index == index);

        public string EnemyName(BattleEnemy e) => _enemies[e.EnemyId].Name;

        /// <summary>“1号位”。</summary>
        public static string PosName(int pos) => $"{pos}号位";

        /// <summary>拳没有物品：基础拳、应急拳、推挡只打敌方 1 号位（Q64，取代 B6-4 的 12）；护拳绑带与 10 级连拳是明确扩展，见 SkillTargetPositions。</summary>
        public const string FistTargetPositions = "1";

        /// <summary>武器可打的敌方站位集合（A05 §1 目标位，数字连写；“友方”等非数字为空集）。</summary>
        public static HashSet<int> TargetPositionsOf(WeaponConfig w)
        {
            var text = w.IsFists || string.IsNullOrEmpty(w.TargetPositions) ? FistTargetPositions : w.TargetPositions!;
            return new HashSet<int>(text.Where(char.IsDigit).Select(ch => ch - '0'));
        }

        /// <summary>武器的己方使用位集合（A05 §1）；拳与未填的为 1—4 全部。</summary>
        public static HashSet<int> OwnPositionsOf(WeaponConfig w) =>
            w.IsFists || string.IsNullOrEmpty(w.OwnPositions) ? new HashSet<int> { 1, 2, 3, 4 } : new HashSet<int>(w.OwnPositions!.Where(char.IsDigit).Select(ch => ch - '0'));

        /// <summary>
        /// 主角在我方的真实站位（1 起）。Q80：我方 Allies 是按槽排的稀疏表（下标=槽号−1，空位为 ""，末尾空位省略），
        /// 单人也可以在 2/3/4 号位，不自动归 1。
        /// </summary>
        public static int PlayerPos(BattleState b) => Math.Max(1, b.Allies.IndexOf(GameState.PlayerId) + 1);

        /// <summary>我方某槽上的角色 actorId；空位或越界为 null。</summary>
        public static string? AllyAt(BattleState b, int pos) =>
            pos >= 1 && pos <= b.Allies.Count && !string.IsNullOrEmpty(b.Allies[pos - 1]) ? b.Allies[pos - 1] : null;

        /// <summary>我方在场角色数（不数空位）。</summary>
        public static int AllyCount(BattleState b) => b.Allies.Count(a => !string.IsNullOrEmpty(a));

        /// <summary>
        /// 我方两槽原子交换（Q80：目的为空直接移入，目的有同侧角色则一次交换）；只改站位，不多激活、不改已锁行动队列。
        /// 表按需补空位、去掉末尾空位（只有主角在 1 号位时仍是旧档的 [p0]）。
        /// </summary>
        public static void MoveAlly(BattleState b, int from, int to)
        {
            var n = Math.Max(from, to);
            while (b.Allies.Count < n) b.Allies.Add("");
            (b.Allies[from - 1], b.Allies[to - 1]) = (b.Allies[to - 1], b.Allies[from - 1]);
            while (b.Allies.Count > 0 && string.IsNullOrEmpty(b.Allies[b.Allies.Count - 1])) b.Allies.RemoveAt(b.Allies.Count - 1);
        }

        /// <summary>
        /// 使用位是否合法（R03 §1，Q80 起严格按真实己位）：取消“我方只剩一人时所有武器兼容 1 号位”的总豁免，单人也须在武器使用位里。
        /// </summary>
        public bool OwnPositionOk(BattleState b, WeaponConfig w) => OwnPositionsOf(w).Contains(PlayerPos(b));

        /// <summary>“1、2号位”。</summary>
        public static string PosList(IEnumerable<int> ps) => string.Join("、", ps.OrderBy(x => x)) + "号位";

        // Q80：取消“每条指令后/死亡后/轮初自动压紧”（原 CompactEnemies）。双方保持真实 1—4 槽与空位；只有新战初次生成按名单紧凑排位。

        /// <summary>
        /// 当前武器（B3 H6、A10 §4.2、A05 §5）：当前武器页对应武器槽里的那把（战外为开战默认页，见 DefaultWeaponPage）；空槽页即徒手（拳）。
        /// 损坏、没子弹的武器仍是当前武器（照旧在指令上写原因，Q39 选 A：坏或无弹不视为空槽、不变拳）。
        /// </summary>
        public WeaponConfig CurrentWeapon(GameState s, out ItemInstance? item)
        {
            item = WeaponOnPage(s, s.Battle?.ActiveWeaponSlot ?? DefaultWeaponPage(s), out var w);
            return w;
        }

        /// <summary>
        /// 开战默认停在哪一页（开发计划 v0.9 B3a H3）：第一个有武器的槽所在页——武器Ⅰ有武器为 1，武器Ⅰ空而武器Ⅱ有武器为 2，两槽都空为 1（拳页）。
        /// 坏或没子弹的武器也算“有武器”（Q39）。战外的当前武器、系统说明指令一览按同一页。
        /// </summary>
        public int DefaultWeaponPage(GameState s)
        {
            if (WeaponOnPage(s, 1, out _) != null) return 1;
            return WeaponOnPage(s, 2, out _) != null ? 2 : 1;
        }

        /// <summary>某武器页（1/2）上的武器：该武器槽的穿戴实例与定义；空槽为拳、实例为 null。</summary>
        public ItemInstance? WeaponOnPage(GameState s, int page, out WeaponConfig weapon)
        {
            weapon = _weapons[Fists];
            var slot = EquipSlots.WeaponSlotOfPage(page);
            var it = slot != null ? WornItem(s, GameState.PlayerId, slot) : null;
            if (it == null || !_weapons.TryGetValue(it.ItemId, out var w)) return null;
            weapon = w;
            return it;
        }

        /// <summary>本人身上（随身与穿戴）全部武器实例（开发机器人挑武器、旧调试用）。</summary>
        public List<ItemInstance> CarriedWeapons(GameState s) =>
            s.Items.Where(i => OnPlayer(i) && _weapons.ContainsKey(i.ItemId)).OrderBy(i => i.InstanceId).ToList();

        /// <summary>
        /// 切换武器页（A10 §4.2“切页只是切换本次可选技能组、零AP”、A05 §5、B3 H6）：战斗中随时可切（非本人激活也可查看），不耗 AP、不耗体力、
        /// 不推进时间；两槽在开战时锁定，不能从随身换入第三把。界面切页时同时取消尚未提交的瞄准。页号随战斗存档。
        /// </summary>
        public string? WeaponPageBlocked(GameState s, int page)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (s.Battle == null) return "不在战斗中";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (page != 1 && page != 2) return "只有武器Ⅰ、武器Ⅱ两页";
            if (s.Battle.ActiveWeaponSlot == page) return $"已在{EquipSlots.Name(EquipSlots.WeaponSlotOfPage(page)!)}页";
            return null;
        }

        public ActionResult SelectWeaponPage(GameState s, int page, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = WeaponPageBlocked(s, page);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            s.Battle!.ActiveWeaponSlot = page;
            return ActionResult.Success();
        }

        /// <summary>
        /// 玩家有效速度（R03 §2、R01 §4）：max(1, 基础4 + 装备速度修正（正加成合计最多+3，负值照算，耐久0的装备暂停）+ 状态修正)，
        /// E&lt;4 与 S&lt;20 各−1，减速−2。
        /// </summary>
        public double PlayerEffectiveSpeed(GameState s, BattleState? b, double? energy = null) =>
            PlayerSpeedParts(s, b, energy).Speed;

        /// <summary>速度的组成（预览与说明用）：基础、装备、状态各项与合计。</summary>
        public (double Speed, double Base, double Gear, List<string> Status) PlayerSpeedParts(GameState s, BattleState? b, double? energy = null)
        {
            var m = Config.CombatMath;
            var e = energy ?? s.Stats.Energy;
            var status = new List<string>();
            var baseSp = Config.Scope.BasePlayerSpeed;
            var gear = EquipmentEffects(s, GameState.PlayerId, b?.ActiveWeaponSlot).Speed;
            var sp = baseSp + gear;
            if (e < m.LowEnergyExclusive) { sp -= m.LowEnergySpeedPenalty; status.Add($"体力<{Fmt.T(m.LowEnergyExclusive)} −{Fmt.T(m.LowEnergySpeedPenalty)}"); }
            if (s.Stats.Wakefulness < m.LowWakefulnessSpeedExclusive) { sp -= m.LowWakefulnessSpeedPenalty; status.Add($"清醒<{Fmt.T(m.LowWakefulnessSpeedExclusive)} −{Fmt.T(m.LowWakefulnessSpeedPenalty)}"); }
            if (b != null && b.PlayerSlowActive) { sp -= Config.Statuses.Slow.NextRoundSpeedPenalty; status.Add($"减速 −{Fmt.T(Config.Statuses.Slow.NextRoundSpeedPenalty)}"); }
            return (QuantizeSpeed(sp), baseSp, gear, status);
        }

        /// <summary>敌人有效速度：max(1, 四舍五入到0.01(A09 面板速度 − 减速))（Q71）。</summary>
        public double EnemyEffectiveSpeed(BattleEnemy e) =>
            QuantizeSpeed(_enemies[e.EnemyId].Speed - (e.SlowActive ? Config.Statuses.Slow.NextRoundSpeedPenalty : 0) - e.SlowExtraActive);

        /// <summary>
        /// 有效速度的统一量化（Q71）：max(1, 四舍五入到 0.01(基础+装备+状态))，不先取整；只在这里量化一次，排序按整数百分速度比较。
        /// </summary>
        public double QuantizeSpeed(double raw) => Math.Max(SpeedHundredths(Config.CombatMath.MinSpeed), SpeedHundredths(raw)) / 100.0;

        /// <summary>速度的整数百分值（四舍五入、半数远离零；先吸附 1e-7 消除 4.005 这类浮点误差）。</summary>
        public static int SpeedHundredths(double v) => (int)Math.Floor(Math.Abs(v) * 100 + 0.5 + 1e-7) * Math.Sign(v);

        /// <summary>追击进度按 0.0001 保存（Q71：不每轮取整、不清小数）。</summary>
        public static double RoundPursuit(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

        /// <summary>追击进度增量 q=clamp(50×(自己的轮初有效速度/敌方最快存活角色轮初有效速度−1), 0, 100)（R03 §2）。</summary>
        public double PursuitGain(double own, double fastestOpponent)
        {
            var m = Config.CombatMath;
            if (fastestOpponent <= 0) return 0;
            return Math.Max(0, Math.Min(m.PursuitThreshold, m.PursuitScale * (own / fastestOpponent - 1)));
        }

        /// <summary>
        /// 身体修正（乘命中能力，Q67）：支付本次成本后 E&lt;4 或 S≤20 时疲劳一次 ×0.95（双低不叠乘）；恐慌≥60 ×0.9、≥90 ×0.8 择一；失衡×0.9。
        /// </summary>
        public double BodyAccuracyFactor(GameState s, BattleState? b, double energyAfter, List<string>? why = null)
        {
            var m = Config.CombatMath;
            double f = 1;
            var lowE = energyAfter < m.LowEnergyExclusive;
            var lowS = s.Stats.Wakefulness <= m.LowWakefulnessInclusive;
            if (lowE || lowS)
            {
                f *= m.FatigueAccuracyFactor;
                why?.Add($"{(lowE && lowS ? "低体力且低清醒" : lowE ? "低体力" : "低清醒")}×{Fmt.T(m.FatigueAccuracyFactor)}（只算一次）");
            }
            if (b == null) return f;
            var pp = Config.Panic.PenaltyFor(b.Panic);
            if (pp != null) { f *= pp.AccuracyRatingFactor; why?.Add($"恐慌≥{Fmt.T(pp.Minimum)}×{Fmt.T(pp.AccuracyRatingFactor)}"); }
            if (b.PlayerStagger) { f *= Config.Statuses.Stagger.NextAttackAccuracyFactor; why?.Add($"失衡×{Fmt.T(Config.Statuses.Stagger.NextAttackAccuracyFactor)}"); }
            return f;
        }

        /// <summary>
        /// 战斗外对某敌人的命中率估算（接敌浮窗、敌人详情；与战斗中 TargetCalc 同一公式，Q67/A10“预览执行同源”）：
        /// 开战默认页武器的 (武器A+3L+8+装备命中)×疲劳(按支付该页普通攻击体力后)；入战恐慌/失衡开战后才知道，不计。
        /// </summary>
        public (double Accuracy, double HitChance) OutOfBattleHit(GameState s, EnemyConfig enemy)
        {
            var page = DefaultWeaponPage(s);
            WeaponOnPage(s, page, out var w);
            var cost = SkillOf(BasicAttack, w) is SkillNodeConfig n ? SkillEnergy(s, n, w, page) : 0;
            var acc = Math.Max(Config.CombatMath.MinAccuracy,
                (w.AccuracyRating + SkillAccuracyBonus(SkillLevel(s, w.SkillId)) + Config.CombatMath.PlayerAccuracyAssistance + EquipmentEffects(s, GameState.PlayerId, page).Accuracy)
                * BodyAccuracyFactor(s, null, Math.Max(0, s.Stats.Energy - cost)));
            return (acc, HitChance(acc, Math.Max(Config.CombatMath.MinEvasion, enemy.Dodge)));
        }

        public static double HitChance(double accuracy, double evasion) => accuracy <= 0 ? 0 : accuracy / (accuracy + Math.Max(1, evasion));

        /// <summary>控制判定成功率 clamp(0.75 + 控制能力/100 − 目标控制抗性/100, 0.10, 0.95)（R03 §4）。</summary>
        public double ControlChance(double controlBonus, double resist)
        {
            var m = Config.CombatMath;
            return Math.Max(m.ControlMin, Math.Min(m.ControlMax, m.ControlBase + controlBonus / 100 - resist / 100));
        }

        /// <summary>
        /// 主角对某种控制的抗性（Q63）：基础控制抗性 5；拉扯/击退再加“位移抵抗”=max(装备合计（同名取强、不同来源相加、封顶40）, 主动同类状态（稳固）)，
        /// 失衡/减速等不吃位移抵抗。稳态 +30 在 RollControl 里另加，最终成功率 10%—95%。
        /// </summary>
        public double PlayerControlResist(GameState s, BattleState? b, string status)
        {
            var r = Config.Scope.BasePlayerControlResist;
            if (Config.ActorBaseDefense.DisplacementGearOnlyStatuses.Contains(status))
            {
                var gear = EquipmentEffects(s, GameState.PlayerId, 0).DisplaceResist;
                var state = b != null && b.DisplaceUntilRound >= b.Round ? b.DisplaceResist : 0;
                r += Math.Max(gear, state);
            }
            return r;
        }

        /// <summary>主角对某种控制的成功率（不推进随机；稳态期间+30）：预览与执行同源。</summary>
        public double PlayerControlChance(GameState s, BattleState b, string status) =>
            ControlChance(0, PlayerControlResist(s, b, status) + (b.PlayerSteadyUntilRound >= b.Round ? Config.CombatMath.SteadyResistBonus : 0));

        /// <summary>
        /// 命中后判一次控制（R03 §4：击退/拉扯/减速/打断/失衡）：控制能力默认 0；目标抗性 = 基准 + 稳态+30。成功则目标获稳态至下一大回合末，
        /// 同轮重复成功不再加第二层。返回是否成功与成功率（写进日志）。
        /// </summary>
        private bool RollControl(GameState s, BattleState b, double baseResist, ref int steadyUntil, out double chance)
        {
            var steady = steadyUntil >= b.Round;
            chance = ControlChance(0, baseResist + (steady ? Config.CombatMath.SteadyResistBonus : 0));
            if (s.Rng.NextDouble() >= chance) return false;
            if (!steady) steadyUntil = b.Round + 1;
            return true;
        }

        /// <summary>
        /// 保护是否合法（R03 §4“只可一名保护者接一名目标，不允许相互/循环保护”）：不能保护自己；一名保护者只接一名目标；
        /// 一名目标只有一名保护者；沿保护链从目标走回保护者即成环，拒绝。只读。
        /// </summary>
        public static string? ProtectBlocked(IReadOnlyDictionary<string, string> links, string protector, string target)
        {
            if (protector == target) return "不能保护自己";
            if (links.ContainsKey(protector)) return "每名保护者只能保护一名目标";
            if (links.Values.Contains(target)) return "目标已有保护者";
            var seen = new HashSet<string>();
            var cur = target;
            while (links.TryGetValue(cur, out var next) && seen.Add(cur))
            {
                if (next == protector) return "不能相互或循环保护";
                cur = next;
            }
            return null;
        }

        /// <summary>主角护甲：基础 0 + 装备护甲（总上限 0.9，耐久 0 的防具暂停；A05 §2）。</summary>
        public double PlayerArmor(GameState s) =>
            Math.Max(0, Math.Min(Config.Equipment.Limits.ArmorCap, Config.Scope.BasePlayerArmor + EquipmentEffects(s, GameState.PlayerId, 0).Armor));

        public double CritChance(double critRating) => critRating <= 0 ? 0 : critRating / (critRating + Config.CombatMath.CritDenominator);

        // ───────────── 开始 ─────────────

        /// <summary>主动接近当前地块的可见敌人（v0.6 §15）：不受随机接敌保护期限制。</summary>
        public string? CanApproach(GameState s, int worldEnemyId)
        {
            if (s.Progress.Dead) return "角色已死亡";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (!Config.CombatEnabled || !Config.Encounters.ActiveContactEnabled) return "战斗未启用";
            if (s.Battle != null) return "已在战斗中";
            if (s.Encounter != null) return "发现敌踪：请先选择接敌或绕行";
            if (s.Main != null || s.Sub != null) return s.Main?.Status == ActionStatus.Paused ? "行动暂停中，先结束或取消行动" : "行动进行中";
            var w = FindWorldEnemy(s, worldEnemyId);
            if (w == null) return "这个敌人已不在";
            if (w.LocationId != s.CurrentLocation || Hidden(s, w)) return "只能接近当前地点可见的敌人";
            if (w.Engaged) return "这个敌人正在交战";
            // 首战胜利前只开放保留的游荡尸（S15 §5：第一次接敌必须由首战承接）
            if (TutorialGateOpen(s) && s.Story.TutorialEnemyId != w.Id) return "先完成农野荒地的首战（游荡尸），再接近其他敌人";
            return null;
        }

        public ActionResult Approach(GameState s, int worldEnemyId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var why = CanApproach(s, worldEnemyId);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, requestId);
            // 首战撤退后再接近同一保留游荡尸：仍是教学首战（胜利才发教学物质），情境按主动接近
            var tutorial = IsTutorialEnemy(s, worldEnemyId);
            StartBattle(s, new List<WorldEnemy> { FindWorldEnemy(s, worldEnemyId)! }, false, tutorial ? "首战" : "主动接近", FearApproach, tutorial);
            if (tutorial) s.Story.TutorialBattleId = s.Battle?.BattleId;
            return ActionResult.Success();
        }

        /// <summary>敌踪选择“接敌”：用开始时那组同地现有敌人入战（受惊情境系数 K=1，R03 §2.1）。</summary>
        public ActionResult Engage(GameState s, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (s.Progress.Dead) return ActionResult.Fail("角色已死亡");
            if (DialogueLocked(s)) return ActionResult.Fail(DialogueLockedReason);
            if (s.Encounter == null) return ActionResult.Fail("当前没有敌踪");
            var group = EncounterGroup(s);
            RememberRequest(s, requestId);
            if (group.Count == 0)
            {
                s.Encounter = null;
                Emit(s, GameEventKind.Info, "敌踪已经离开。");
                return ActionResult.Success();
            }
            s.Encounter = null;
            StartBattle(s, group, true, "遭遇", FearEncounter);
            return ActionResult.Success();
        }

        /// <summary>敌踪里还在本地、未交战的有序名单（接敌时按它截前 C 个）。</summary>
        public List<WorldEnemy> EncounterGroup(GameState s) =>
            EncounterEnemies(s).Where(w => !w.Engaged && w.LocationId == s.CurrentLocation).ToList();

        /// <summary>接敌时实际入战的敌人：有序名单的前 C 个（Q62：未选的留在原地、不判死亡与奖励）。受惊判定也只看这几只。</summary>
        public List<WorldEnemy> EncounterJoiners(GameState s) => EncounterGroup(s).Take(BattleCapacity(s)).ToList();

        /// <summary>敌踪名单超过容量时的说明：“本场容量C1：只有排在最前的1只接战，其余2只留在原地”；不超过为 null。</summary>
        public string? EncounterCapacityNote(GameState s) =>
            EncounterStayNote(s) is string n ? $"本场容量C{BattleCapacity(s)}：{n}" : null;

        /// <summary>同上，不带“本场容量C”前缀（右栏已另写容量时用，避免重复）：“只有排在最前的1只接战，其余2只留在原地……”。</summary>
        public string? EncounterStayNote(GameState s)
        {
            var all = EncounterGroup(s).Count;
            var c = BattleCapacity(s);
            return all > c ? $"只有排在最前的{c}只接战，其余{all - c}只留在原地（不算击杀、没有战利品）" : null;
        }

        /// <summary>入战情境（R03 §2.1）：主动接近、探索/清理接敌、剧情首战。</summary>
        public const string FearApproach = "approach";
        public const string FearEncounter = "encounter";
        public const string FearTutorial = "tutorial";

        private void StartBattle(GameState s, List<WorldEnemy> group, bool surprise, string source, string context, bool tutorial = false)
        {
            var b = new BattleState
            {
                BattleId = s.NextBattleId++,
                LocationId = s.CurrentLocation,
                StartMinute = s.GameMinute,
                Surprise = surprise,
                Context = context,
                Tutorial = tutorial,
                // 开战锁定两个武器槽（B3 H6）；停在第一个有武器的页，两槽都空才是武器Ⅰ拳页（B3a H3）；战内不换装、战后不回写
                ActiveWeaponSlot = DefaultWeaponPage(s),
            };
            b.EncounterId = $"enc-{b.BattleId}";
            // 容量 C 锁存（R03 §1、A11：决定敌人数上限，不按实到人数缩怪）；有序名单截前 C 个，从 1 号位起紧凑排位
            b.Capacity = BattleCapacity(s);
            b.Allies = new List<string> { GameState.PlayerId };   // 新战初次生成默认紧凑：主角在 1 号位（队友参战与战前编成在 B8）
            var k = 0;
            var joined = group.Take(Math.Min(b.Capacity, Config.Scope.Slots.Count)).ToList();
            foreach (var w in joined)
            {
                w.Engaged = true;
                b.Enemies.Add(InitBattleEnemy(new BattleEnemy { WorldId = w.Id, Index = k + 1, EnemyId = w.EnemyId, Slot = (k + 1).ToString(), Hp = w.Hp, Hostile = w.Hostile, XpHits = new Dictionary<string, int>(w.XpHits), JoinRound = 1 }));
                k++;
            }

            var pc = Config.Panic;
            // 受惊（R03 §2.1、R05 §6.3）：同一 battleId 只判一次并锁存；恐慌基础值 (100−M)/2，受惊才加主惊吓源的惊吓值
            RollFear(s, b, joined, context);
            b.Panic = Math.Max(pc.Range[0], Math.Min(pc.Range[1], (pc.InitialBase - s.Stats.Morale) / pc.InitialDivisor + b.FearPanic));
            b.PanicPeak = b.Panic;
            s.Battle = b;
            var names = string.Join("、", b.Enemies.Select(e => $"{PosName(e.Pos)}{EnemyName(e)}"));
            var fearText = !b.FearSourceWorldId.HasValue ? ""
                : b.Frightened ? $"（受惊：{_enemies[joined.First(w => w.Id == b.FearSourceWorldId).EnemyId].Name}，受惊率{Fmt.Pct(b.FearChance)}，恐慌+{Fmt.T(b.FearPanic)}）"
                : $"（未受惊，受惊率{Fmt.Pct(b.FearChance)}）";
            var left = group.Count - joined.Count;
            BattleLog(s, $"{source}：{names}。恐慌{Fmt.N(b.Panic)}{fearText}。" + (left > 0 ? $"本场容量C{b.Capacity}，另有{left}只留在原地。" : ""));
            Emit(s, GameEventKind.Battle, $"战斗开始：{names}。", null, b.Enemies.Select(e => $"enemy:{e.WorldId}"));
            // 同速排序用的锁定随机数 u（R03 §2）：入场时各抽一次并存档（在受惊判定之后抽，不改受惊结果）
            b.PlayerTieRoll = s.Rng.NextDouble();
            foreach (var be in b.Enemies) be.TieRoll = s.Rng.NextDouble();
            BeginRound(s);
        }

        /// <summary>主惊吓源：基础惊吓概率最高，平手取惊吓值较高，再平手 worldEnemyId 小（R03 §2.1）。</summary>
        public WorldEnemy? FearSource(IEnumerable<WorldEnemy> group) =>
            group.OrderByDescending(w => _enemies[w.EnemyId].FearChance).ThenByDescending(w => _enemies[w.EnemyId].FearValue).ThenBy(w => w.Id).FirstOrDefault();

        /// <summary>受惊概率 p=q×(1−clamp(M,0,100)/100)×K。</summary>
        public double FearChance(double fearChance, double morale, string context)
        {
            var m = Math.Max(0, Math.Min(100, morale));
            return fearChance * (1 - m / 100) * Config.Panic.Fear.Factor(context);
        }

        /// <summary>
        /// 入战受惊判定（R03 §2.1）：主惊吓源的随机数 u 在 60 游戏分钟内复用（超过才重新生成），每次新入战按当时精神与情境重算 p；
        /// u&lt;p 则受惊、恐慌加该敌惊吓值。结果写进 BattleState，读档、回调、换目标都不重抽。
        /// </summary>
        private void RollFear(GameState s, BattleState b, List<WorldEnemy> group, string context)
        {
            var main = FearSource(group);
            if (main == null) return;
            var cfg = _enemies[main.EnemyId];
            double u;
            if (main.FearRollMinute.HasValue && s.GameMinute - main.FearRollMinute.Value <= Config.Panic.Fear.ReuseRollMinutes) u = main.FearRoll;
            else
            {
                u = s.Rng.NextDouble();
                main.FearRoll = u;
                main.FearRollMinute = s.GameMinute;
            }
            b.FearSourceWorldId = main.Id;
            b.FearMorale = s.Stats.Morale;
            b.FearRoll = u;
            b.FearChance = FearChance(cfg.FearChance, s.Stats.Morale, context);
            b.Frightened = u < b.FearChance;
            b.FearPanic = b.Frightened ? cfg.FearValue : 0;
        }

        /// <summary>入战前的受惊说明（敌踪选择、接近详情）：“可能受惊：游荡尸受惊率14.00%，受惊时恐慌+6”。</summary>
        public string FearNote(GameState s, IEnumerable<WorldEnemy> group, string context)
        {
            var main = FearSource(group);
            if (main == null) return "";
            var cfg = _enemies[main.EnemyId];
            var p = FearChance(cfg.FearChance, s.Stats.Morale, context);
            return p <= 0 ? $"精神{Fmt.N(s.Stats.Morale)}，不会受惊" : $"可能受惊：{cfg.Name}受惊率{Fmt.Pct(p)}，受惊时恐慌+{Fmt.T(cfg.FearValue)}";
        }

        private void BattleLog(GameState s, string text)
        {
            var b = s.Battle;
            if (b == null) return;
            b.Log.Add($"第{b.Round}轮 · {text}");
            if (b.Log.Count > 80) b.Log.RemoveRange(0, b.Log.Count - 80);
        }

        // ───────────── 轮与队列 ─────────────

        /// <summary>
        /// 轮初（R03 §2）：增援加入、减速生效；按轮初有效速度降序排正常激活队列（同速按锁定随机数 u 降序、再按 worldActorId 升序），
        /// 不在轮中重新洗先手；每个存活角色的追击进度加 q，满 100 扣 100、在正常队列之后获得一次 2AP 追击（同样排序，每轮至多一次，
        /// 首战教学敌人不追击）；锁定并公开敌人意图。
        /// </summary>
        private void BeginRound(GameState s)
        {
            var b = s.Battle!;
            if (!RoundStartBleed(s)) return;
            TryReinforce(s);
            b.PlayerSlowActive = b.PlayerSlowPending;
            b.PlayerSlowPending = false;
            foreach (var e in b.Alive)
            {
                e.SlowActive = e.SlowPending;
                e.SlowPending = false;
                e.SlowExtraActive = e.SlowExtra;   // 绝缘警棍“打断成功后下轮速度额外−1”
                e.SlowExtra = 0;
                e.Acted = false;
            }
            b.RoundStartEnergy = s.Stats.Energy;
            b.BreatheUsed = false;
            b.RetreatUsed = false;
            var entries = new List<(int idx, double speed, double u, int actor)>
            {
                (0, PlayerEffectiveSpeed(s, b), b.PlayerTieRoll, 0),
            };
            foreach (var e in b.Alive.OrderBy(e => e.Index)) entries.Add((e.Index, EnemyEffectiveSpeed(e), e.TieRoll, e.WorldId));
            List<(int idx, double speed, double u, int actor)> Sort(IEnumerable<(int idx, double speed, double u, int actor)> xs) =>
                xs.OrderByDescending(x => SpeedHundredths(x.speed)).ThenByDescending(x => x.u).ThenBy(x => x.actor).ToList();   // Q71：百分速度降序→u降序→ID升序
            var ordered = Sort(entries);
            // 追击：对方阵营最快存活角色的轮初速度为基准
            var playerSpeed = entries[0].speed;
            var fastestEnemy = entries.Skip(1).Select(x => x.speed).DefaultIfEmpty(0).Max();
            var pursuit = new List<(int idx, double speed, double u, int actor)>();
            var th = Config.CombatMath.PursuitThreshold;
            foreach (var x in entries)
            {
                if (x.idx == 0)
                {
                    b.PlayerPursuit = RoundPursuit(b.PlayerPursuit + PursuitGain(x.speed, fastestEnemy));
                    if (b.PlayerPursuit >= th - 1e-9) { b.PlayerPursuit = RoundPursuit(b.PlayerPursuit - th); pursuit.Add(x); }
                }
                else if (BattleEnemyAt(b, x.idx) is BattleEnemy e && !b.Tutorial)
                {
                    e.PursuitProgress = RoundPursuit(e.PursuitProgress + PursuitGain(x.speed, playerSpeed));
                    if (e.PursuitProgress >= th - 1e-9) { e.PursuitProgress = RoundPursuit(e.PursuitProgress - th); pursuit.Add(x); }
                }
            }
            var chase = Sort(pursuit);
            b.Queue = ordered.Concat(chase).Select(x => x.idx).ToList();
            b.Initiative = ordered.Concat(chase).Select(x => x.speed).ToList();
            b.NormalCount = ordered.Count;
            b.Cursor = 0;
            b.PlayerActing = false;
            b.ActingPursuit = false;
            b.PlayerAp = 0;
            foreach (var e in b.Alive) LockIntent(s, b, e);
            string Name(int idx) => idx == 0 ? "你" : EnemyName(BattleEnemyAt(b, idx)!);
            BattleLog(s, "行动顺序（速度）：" + string.Join(" → ", ordered.Select(x => $"{Name(x.idx)}({Fmt.Speed(x.speed)})"))
                         + (chase.Count > 0 ? "；追击：" + string.Join(" → ", chase.Select(x => Name(x.idx))) : ""));
        }

        /// <summary>
        /// 普通激活的 AP（R03 §3、R01 §4）：战术训练阶位 0/1/2 → 3/4/5AP，加临时 +AP（同源取强；本批还没有来源）后不超过 6；
        /// 不按熟练度门类堆叠（RG02）。
        /// </summary>
        public int NormalActivationAp(ActorState actor, int tempBonus = 0)
        {
            var sc = Config.Scope;
            var tier = Math.Max(0, Math.Min(sc.ApByTier.Count - 1, actor.TacticalTier));
            return Math.Min(sc.ApCap, sc.ApByTier[tier] + Math.Max(0, tempBonus));
        }

        /// <summary>开发入口：设主角战术训练阶位（0/1/2）。训练入口在后续批次。</summary>
        public string DebugSetTacticalTier(GameState s, int tier)
        {
            s.Player.TacticalTier = Math.Max(0, Math.Min(Config.Scope.ApByTier.Count - 1, tier));
            return $"战术训练阶位设为{s.Player.TacticalTier}：每次普通激活{NormalActivationAp(s.Player)}AP（开发用）。";
        }

        /// <summary>
        /// 盾类格挡的盾（Q69 选 B）：提交防御时“当前武器页”绑定的、本人可付 1 耐久的盾，才授本次 ×0.4 并磨损盾 1；
        /// 另一页的盾不算，耐久不足 1 的盾不算（都按通用防御 ×0.5、不磨备用盾）。没有为 null。
        /// </summary>
        public ItemInstance? GuardShield(GameState s)
        {
            var page = s.Battle?.ActiveWeaponSlot ?? DefaultWeaponPage(s);
            var it = WeaponOnPage(s, page, out var w);
            return it != null && w.WeaponKind == "shield" && HasWear(it, Config.Wear.PerExecutedAttackCommand) ? it : null;
        }

        /// <summary>防御/格挡类指令没有盾类格挡时的原因（预览用）：另一页有盾、当前盾耐久不足；都没有为 null。</summary>
        public string? GuardShieldMissingReason(GameState s)
        {
            var page = s.Battle?.ActiveWeaponSlot ?? DefaultWeaponPage(s);
            var cur = WeaponOnPage(s, page, out var cw);
            if (cur != null && cw.WeaponKind == "shield" && !HasWear(cur, Config.Wear.PerExecutedAttackCommand))
                return $"{cw.Name}耐久不足1（{DurabilityText(cur)}），按普通防御";
            var other = WeaponOnPage(s, page == 1 ? 2 : 1, out var ow);
            if (other != null && ow.WeaponKind == "shield")
                return $"{ow.Name}在另一页：盾类格挡要先切到盾所在的页再防御";
            return null;
        }

        /// <summary>
        /// 授予一次格挡（Q69）：按提交时的当前页锁定来源盾与系数（之后切页、浏览、盾耐久归零都不改这次已付的格挡），盾类格挡磨损 1。
        /// 返回盾的磨损文字（不是盾为空串）。
        /// </summary>
        private string GrantGuard(GameState s, BattleState b, int charges)
        {
            var shield = GuardShield(s);
            b.PlayerGuard = true;
            b.PlayerGuardCharges = Math.Max(1, charges);
            b.PlayerGuardFactor = shield != null ? Config.CombatMath.ShieldGuardFactor : Config.CombatMath.GuardFactor;
            b.PlayerGuardSourceId = shield?.InstanceId;
            return shield != null ? Wear(shield, Config.Wear.PerExecutedAttackCommand) : "";
        }

        /// <summary>
        /// 大回合开始的出血（R03 §4）：每个出血者 H−0.4、次数−1（刷新次数不叠强度，丧尸免疫），追击不多扣；先判主角死亡，
        /// 再判敌人倒下/重伤逃离。出血让最后一个敌人倒下即胜利，这一轮没有任何行动，不另推进世界时间（B6 开发假设 B6-6）。
        /// 返回 false 表示战斗已结束。
        /// </summary>
        private bool RoundStartBleed(GameState s)
        {
            var b = s.Battle!;
            var body = s.Body;
            if (body.BleedTriggers > 0)
            {
                s.Stats.Add(StatId.Health, -body.BleedDamage);
                body.BleedTriggers--;
                BattleLog(s, $"你流血，健康−{Fmt.N(body.BleedDamage)}（还剩{body.BleedTriggers}次）。");
                if (body.BleedTriggers == 0) body.BleedDamage = 0;
                if (s.Stats.Health <= 0) { BattleDeath(s, "出血"); return false; }
            }
            foreach (var e in b.Alive.Where(e => e.BleedTriggers > 0).OrderBy(e => e.Pos).ToList())
            {
                e.Hp -= e.BleedDamage;
                e.BleedTriggers--;
                BattleLog(s, $"{EnemyName(e)}流血，−{Fmt.N(e.BleedDamage)}。");
                if (e.Hp <= 1e-9) KillEnemy(s, e);
                else CheckFlee(s, e);
                if (s.Stats.Health <= 0) { BattleDeath(s, s.Progress.LastDamageCause ?? "喷溅"); return false; }   // 膨胀感染者死亡喷溅
            }
            if (!b.Alive.Any()) { FinishBattle(s, "victory", false); return false; }
            return true;
        }

        /// <summary>队列第 k 项是不是追击激活。</summary>
        public static bool IsPursuitSlot(BattleState b, int k) => b.NormalCount > 0 && k >= b.NormalCount;

        public string AttackVerb(EnemyConfig cfg) => cfg.Behavior switch
        {
            "slow_on_hit" => "扯腿",
            "stagger_on_hit" => "扑击",
            "strike" => "挥击",
            "guard_first_round" => "撞击",
            "bleed_on_hit_25" => "撕咬",
            "charge_prepare_odd_attack_even" => "拱撞",
            "neutral_until_attacked" => "顶撞",
            _ => "抓击",
        };

        /// <summary>等待玩家输入指令。</summary>
        public bool AwaitingPlayer(GameState s) => s.Battle != null && s.Battle.PlayerActing && !s.Progress.Dead;

        /// <summary>
        /// 推进一步：玩家激活开始、一个敌人的激活、或轮末。玩家激活中返回 AwaitPlayer 不推进。界面按步调用以逐条展示敌方行动；每步都可存档。
        /// </summary>
        public BattleStepResult BattleStep(GameState s)
        {
            var b = s.Battle;
            if (b == null || s.Progress.Dead) return new BattleStepResult { Kind = BattleStepKind.None };
            // 对白安全边界（R03 §7.1）：已提交的原子事务保留，下一条动作等对话读完再开始
            if (DialogueLocked(s)) return new BattleStepResult { Kind = BattleStepKind.Dialogue };
            if (b.PlayerActing) return new BattleStepResult { Kind = BattleStepKind.AwaitPlayer };
            if (b.Cursor >= b.Queue.Count)
            {
                EndRound(s);
                return new BattleStepResult { Kind = s.Battle == null ? BattleStepKind.Ended : BattleStepKind.RoundEnded, Text = b.Log.LastOrDefault() ?? "" };
            }
            var idx = b.Queue[b.Cursor];
            var chase = IsPursuitSlot(b, b.Cursor);
            if (idx == 0)
            {
                StartPlayerActivation(s, chase);
                return new BattleStepResult { Kind = s.Battle == null ? BattleStepKind.Ended : BattleStepKind.PlayerTurnStarted, Text = b.Log.LastOrDefault() ?? "" };
            }
            var e = BattleEnemyAt(b, idx);
            b.Cursor++;
            if (e == null || !e.Alive) return new BattleStepResult { Kind = BattleStepKind.EnemyActed, Text = "" };
            EnemyActivation(s, e, chase);
            return new BattleStepResult { Kind = s.Battle == null ? BattleStepKind.Ended : BattleStepKind.EnemyActed, Text = b.Log.LastOrDefault() ?? "" };
        }

        /// <summary>玩家激活开始：自己的出血 → 判死 → 清旧防御 → 恢复本次 AP。追击激活固定 2AP，不扣出血、不清防御（R03 §2）。</summary>
        private void StartPlayerActivation(GameState s, bool pursuit = false)
        {
            var b = s.Battle!;
            var body = s.Body;
            b.ActingPursuit = pursuit;
            if (pursuit)
            {
                b.PlayerAp = Config.CombatMath.PursuitAp;
                b.ActivationAp = b.PlayerAp;
                b.PlayerActing = true;
                BattleLog(s, $"你追击：{b.PlayerAp}AP（速度优势带来的额外激活）。");
                return;
            }
            // 出血改在大回合开始结算（R03 §4，见 RoundStartBleed）；这里只清旧防御、反击架势与架矛（持续到下一正常激活）、恢复 AP
            b.PlayerGuard = false;
            b.PlayerGuardCharges = 0;
            b.PlayerGuardSourceId = null;
            b.CounterFactor = 0;
            b.BraceCharges = 0;
            b.PlayerAp = NormalActivationAp(s.Player);
            b.ActivationAp = b.PlayerAp;
            b.PlayerActing = true;
            BattleLog(s, $"轮到你行动：{b.PlayerAp}AP。");
        }

        private void EndPlayerActivation(GameState s)
        {
            var b = s.Battle;
            if (b == null) return;
            b.PlayerAp = 0;
            b.PlayerActing = false;
            b.ActingPursuit = false;
            b.Cursor++;
        }

        /// <summary>
        /// 轮末：后排压到最左空前排（已行动标记不变）；减速与失衡到期；推进本轮唯一一次 5 分钟世界 tick；未结束则开下一轮。
        /// </summary>
        private void EndRound(GameState s)
        {
            var b = s.Battle!;
            if (b.PlayerStagger && b.PlayerStaggerRound < b.Round) b.PlayerStagger = false;
            b.PlayerSlowActive = false;
            foreach (var e in b.Alive)
            {
                e.SlowActive = false;
                e.SlowExtraActive = 0;
                // 钢制钉锤“震荡”：失衡持续到目标下次攻击后，不在轮末清除
                if (e.Stagger && !e.StaggerSticky && e.StaggerRound < b.Round) e.Stagger = false;
            }
            if (!BattleTick(s)) return;
            b.Round++;
            BeginRound(s);
        }

        /// <summary>
        /// 新战斗的容量 C（Q62 选 B）：按存档里的真实解锁事实（营区登记 C2、教会路线/学院安全记录之一 C3、二者都有 C4），
        /// 新局及没有这些事实的旧局为 C1；不把 B6 临时的 3 当解锁。进行中的战斗用开战时锁存的 BattleState.Capacity，不随事实变化。
        /// </summary>
        public int BattleCapacity(GameState s) => Math.Min(Config.Scope.Slots.Count, Config.Scope.Capacity.CapacityFor(s.Progress.Flags));

        /// <summary>容量 C 的来源说明：“容量C1（新局：营区登记后C2）”。</summary>
        public string CapacityText(GameState s)
        {
            var c = BattleCapacity(s);
            var met = Config.Scope.Capacity.Unlocks.Where(u => u.Met(s.Progress.Flags)).OrderByDescending(u => u.Capacity).FirstOrDefault();
            var next = Config.Scope.Capacity.Unlocks.Where(u => u.Capacity > c).OrderBy(u => u.Capacity).FirstOrDefault();
            return $"容量C{c}（{(met != null ? $"已有{met.Text}" : "尚无解锁记录")}{(next != null ? $"；{next.Text}后C{next.Capacity}" : "")}）";
        }

        /// <summary>开发入口：加一条容量解锁事实（hub_registered / church_route_record / academy_safety_record）或全部清除。返回说明。</summary>
        public string DebugCapacityFact(GameState s, string? fact)
        {
            var all = Config.Scope.Capacity.Unlocks.SelectMany(u => u.AllOf.Concat(u.AnyOf)).Distinct().ToList();
            if (fact == null) s.Progress.Flags.RemoveWhere(all.Contains);
            else if (all.Contains(fact)) s.Progress.Flags.Add(fact);
            var know = fact == null ? new List<string>() : Config.Knowledge.Where(k => k.GrantedByFacts.Contains(fact)).Select(k => k.Name).ToList();
            return $"开发：{(fact == null ? "已清除容量解锁事实" : $"已记下事实 {fact}")}，下一场新战斗{CapacityText(s)}；进行中的战斗不变"
                   + (know.Count > 0 ? $"；获得默认知识：{string.Join("、", know)}" : "") + "。";
        }

        /// <summary>本轮唯一一次世界 tick（战斗速率，体力只付招式成本，计入工作类归零损血）。返回 false 表示 tick 内死亡或尸变。</summary>
        private bool BattleTick(GameState s)
        {
            var b = s.Battle!;
            b.Ticks++;
            // 每个同行角色按自己的段首状态计战斗段速率，世界只推进一次（B3 H10、COMP04）
            var alive = WorldTick(s, actor => BattleSegment(s, actor), null, true);
            if (!alive)
            {
                // tick 本身致死：该 tick 已发生，不回滚、不补第二段；不提交胜利掉落
                FinishBattleOnDeath(s);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 战斗段（R03 §6、R01 §3）：N/W 负变化全部为 0（含腹泻扣水，战后不补扣），S−5/M−0.5 每小时，E 自然率 0（只付动作）；
        /// 进入时已为 0 的饱食/水分/清醒与体力归零（按工作计）的健康伤害照旧；I/V、燃料、保质、加工按同一个世界 tick 继续。
        /// </summary>
        private SegmentContext BattleSegment(GameState s, ActorState actor)
        {
            var c = Config;
            var ctx = new SegmentContext { Rates = c.BattleTime.HourlyRates.Copy(), IsWork = c.BattleTime.CountsAsWorkingForZeroEnergyDamage, ActionType = null };
            ctx.Sheltered = IsSheltered(s, s.CurrentLocation);
            if (actor.DiarrheaUntil.HasValue && actor.DiarrheaUntil.Value > s.GameMinute) ctx.Rates.Hydration += c.ItemActions.DiarrheaExtraHydrationPerHour;
            if (c.BattleTime.FreezeNegativeSatietyHydration)
            {
                ctx.Rates.Satiety = Math.Max(0, ctx.Rates.Satiety);
                ctx.Rates.Hydration = Math.Max(0, ctx.Rates.Hydration);
            }
            var hr = c.HealthRules;
            var st = actor.Stats;
            var causes = new List<string>();
            double health = 0;
            if (st.Satiety <= 0) { health += hr.ZeroSatietyPerHour; causes.Add("饥饿"); }
            if (st.Hydration <= 0) { health += hr.ZeroHydrationPerHour; causes.Add("缺水"); }
            if (st.Wakefulness <= 0) { health += hr.ZeroWakefulnessPerHour; causes.Add("缺乏睡眠"); }
            if (st.Energy <= 0 && ctx.IsWork) { health += c.BattleTime.ZeroEnergyWorkingHealthPerHour; causes.Add("体力透支"); }
            if (causes.Count > 0) ctx.DamageCause = causes[0];
            ctx.HealthPerHour = health;
            return ctx;
        }

        // ───────────── 指令 ─────────────

        /// <summary>界面指令条：本页技能（已解锁的，含不可用原因）、应急拳击、通用指令与医疗物品指令；不保留瞄准空位。</summary>
        public List<BattleCommandOption> BattleOptions(GameState s)
        {
            var b = s.Battle;
            if (b == null) return new List<BattleCommandOption>();
            var list = SkillOptions(s, true);
            var w = CurrentWeapon(s, out _);
            foreach (var id in new[] { "guard", "breathe", "swap", "reload", "retreat", "end_turn" })
            {
                var a = Act(id);
                if (a == null) continue;
                if (id == "reload" && !w.UsesMagazine) continue;
                if (id == "swap")
                {
                    // 调整站位（Q80）：每个相邻目的槽一条（ItemInstanceId 带目的槽号，界面点选后就近确认）
                    foreach (var to in SwapTargets(b))
                    {
                        var sw = CommandBlocked(s, BattleCommand.Of(id, null, to));
                        list.Add(new BattleCommandOption { Action = id, Label = $"{ActionName(id)}→{PosName(to)}", Group = "general", Ap = a.ApCost, Energy = a.Energy ?? 0, ItemInstanceId = to, Available = sw == null, Reason = sw, TargetMode = "self" });
                    }
                    continue;
                }
                var why = CommandBlocked(s, BattleCommand.Of(id));
                list.Add(new BattleCommandOption { Action = id, Label = ActionName(id), Group = "general", Ap = a.ApCost, Energy = id is "retreat" ? (a.Energy ?? 0) : 0, Available = why == null, Reason = why, TargetMode = "self" });
            }
            // 切换武器页不在指令条里：界面用武器Ⅰ/武器Ⅱ页签（SelectWeaponPage，0AP）
            foreach (var id in new[] { "bandage", "use_antibiotics", "use_antiviral_cure" })
            {
                var a = Act(id);
                if (a?.ItemId == null) continue;
                var why = CommandBlocked(s, BattleCommand.Of(id));
                var have = s.Items.Where(i => i.IsCarried && i.ItemId == a.ItemId).Sum(i => i.Count);
                list.Add(new BattleCommandOption { Action = id, Label = $"{ActionName(id)}（随身×{have}）", Group = "item", Ap = a.ApCost, Available = why == null, Reason = why, TargetMode = "self" });
            }
            return list;
        }

        /// <summary>
        /// 系统说明“指令一览”（Q19/Q26）：按当前实际装备武器与技能等级列出本页已解锁技能（与操作台同一函数 SkillOptions），再列通用指令与医疗物品指令；
        /// 战斗内外都能用。战斗中可用性与原因直接取 CommandBlocked（与操作台同源）；战斗外只写暂时条件。只读。
        /// </summary>
        public List<BattleCommandOption> CompatibleCommands(GameState s)
        {
            if (s.Battle != null)
            {
                var seen = new HashSet<string>();
                var inBattle = BattleOptions(s).Where(o => seen.Add(o.Action + "#" + o.ItemInstanceId)).ToList();
                if (Act("switch_weapon") is CombatActionConfig swb)
                    inBattle.Add(new BattleCommandOption { Action = "switch_weapon", Label = ActionName("switch_weapon"), Group = "general", Ap = swb.ApCost, Available = true, TargetMode = "self" });
                return inBattle;
            }
            var list = SkillOptions(s, false);
            var w = CurrentWeapon(s, out var wi);
            foreach (var id in new[] { "guard", "breathe", "swap", "reload", "switch_weapon", "retreat", "end_turn" })
            {
                var a = Act(id);
                if (a == null) continue;
                if (id == "reload" && !w.UsesMagazine) continue;
                string? why = null;
                if (id == "reload" && wi != null)
                    why = wi.LoadedAmmo >= w.MagazineCapacity ? "弹仓已满" : CarriedCount(s, w.AmmoId!) <= 0 ? $"随身没有{ItemName(w.AmmoId!)}" : null;
                list.Add(new BattleCommandOption { Action = id, Label = ActionName(id), Group = "general", Ap = a.ApCost, Available = why == null, Reason = why, TargetMode = "self" });
            }
            foreach (var id in new[] { "bandage", "use_antibiotics", "use_antiviral_cure" })
            {
                var a = Act(id);
                if (a?.ItemId == null) continue;
                var have = s.Items.Where(i => i.IsCarried && i.ItemId == a.ItemId).Sum(i => i.Count);
                list.Add(new BattleCommandOption
                {
                    Action = id, Label = $"{ActionName(id)}（随身×{have}）", Group = "item", Ap = a.ApCost,
                    Available = have > 0, Reason = have > 0 ? null : $"随身没有{ItemName(a.ItemId)}", TargetMode = "self",
                });
            }
            return list;
        }


        /// <summary>武器按当前熟练度的普通攻击口径：伤害×(1+0.035L)、命中能力+3L（不含技能、身体、恐慌与站位修正）。界面列表与预览统一用它。</summary>
        public (int Level, double DamageMin, double DamageMax, double Accuracy) WeaponStats(GameState s, WeaponConfig w)
        {
            var lv = SkillLevel(s, w.SkillId);
            var f = SkillDamageFactor(lv);
            return (lv, w.DamageMin * f, w.DamageMax * f, w.AccuracyRating + SkillAccuracyBonus(lv) + Config.CombatMath.PlayerAccuracyAssistance);   // Q67 +8
        }

        /// <summary>旧接口：普通攻击（本门 1 级技能）与应急拳的体力成本；技能统一用 SkillEnergy。</summary>
        public double AttackEnergy(GameState s, CombatActionConfig a, WeaponConfig w, int? page = null) => CommandEnergy(s, a.Id, w, page);

        private ItemInstance? CarriedItem(GameState s, string itemId, int? instanceId)
        {
            if (instanceId.HasValue)
            {
                var it = FindItem(s, instanceId.Value);
                return it != null && it.IsCarried && it.ItemId == itemId && !IsSpoiled(s, it) ? it : null;
            }
            return s.Items.Where(i => i.IsCarried && i.ItemId == itemId && i.ReservedBy == null && !IsSpoiled(s, i))
                .OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).FirstOrDefault();
        }

        private int CarriedCount(GameState s, string itemId) => s.Items.Where(i => i.IsCarried && i.ItemId == itemId).Sum(i => i.Count);

        /// <summary>
        /// 调整站位（Q80，指令 ID 沿用 swap）可去的我方槽：相邻且在 1—4 内（空位直接移入；有同侧角色则双方同意交换）。单人也可用。
        /// </summary>
        public List<int> SwapTargets(BattleState b)
        {
            var me = PlayerPos(b);
            return new[] { me - 1, me + 1 }.Where(p => p >= 1 && p <= Config.Scope.Slots.Count).ToList();
        }

        /// <summary>调整站位的目的槽：界面用 ItemInstanceId 传（优先，避免和选中的敌人序号混淆），内核/测试也可放 Target。</summary>
        private static int? SwapDestination(BattleCommand c) => c.ItemInstanceId ?? c.Target;

        /// <summary>该武器能否打到这个目标（R03 §1、A05 §1）：目标站位在武器目标位集合里（矛123、刀剑12、斧1、弓枪1234；不是群攻）。</summary>
        public bool CanReach(BattleState b, WeaponConfig w, BattleEnemy t) => t.Alive && TargetPositionsOf(w).Contains(t.Pos);

        /// <summary>
        /// 指令能否执行（只读）：行动点、体力、耐久、弹药、技能门槛、射程与目标、每回合一次限制、随身物品。不可用时返回原因。
        /// </summary>
        public string? CommandBlocked(GameState s, BattleCommand c)
        {
            var b = s.Battle;
            // 切换武器页：ItemInstanceId 为目标页号 1/2；随时可切、0AP，不要求轮到自己（B3 H6）
            if (c.Action == "switch_weapon") return c.ItemInstanceId.HasValue ? WeaponPageBlocked(s, c.ItemInstanceId.Value) : "请选择武器页（1或2）";
            if (s.Progress.Dead) return "角色已死亡";
            if (b == null) return "不在战斗中";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (!b.PlayerActing) return "还没轮到你行动";
            // 武器技能与应急拳击（B7 R25，A06 §2）：解锁、冷却、AP、体力、耐久、弹药、使用位与目标
            if (IsSkillAction(c.Action) || c.Action == DesperatePunch)
            {
                var sw = CurrentWeapon(s, out var swi);
                return SkillBlocked(s, b, c, sw, swi);
            }
            var a = Act(c.Action);
            if (a == null) return c.Action == "aim" ? "瞄准已移除" : "未知指令";
            // 每大回合一次的限制先于行动点判断：本轮已用过就写“本回合已…”，不写“行动点不足”（试玩修复 M137 UX4）
            if (c.Action == "breathe" && b.BreatheUsed) return "本回合已调整过呼吸";
            if (c.Action == "retreat" && b.RetreatUsed) return "本回合已尝试撤退";
            var w = CurrentWeapon(s, out var wi);
            // 结构性原因（没有队友、弹仓满/没弹药、随身没有药品）先于“行动点不足”显示（试玩修复 M150 UX6）
            var structural = c.Action switch
            {
                "reload" => !w.UsesMagazine || wi == null ? (w.ConsumesLooseAmmo ? "弓不需要装填，射击直接消耗随身箭" : "当前武器不用装填")
                    : wi.LoadedAmmo >= w.MagazineCapacity ? "弹仓已满" : CarriedCount(s, w.AmmoId!) <= 0 ? $"随身没有{ItemName(w.AmmoId!)}" : null,
                "bandage" or "use_antibiotics" or "use_antiviral_cure" => CarriedItem(s, a.ItemId!, c.ItemInstanceId) == null ? $"随身没有可用的{ItemName(a.ItemId!)}（地面、火堆里的不能在战斗中使用）" : null,
                _ => null,
            };
            if (structural != null) return structural;
            var ap = a.ApCost < 0 ? b.PlayerAp : a.ApCost;
            if (a.ApCost > 0 && ap > b.PlayerAp) return $"行动点不足（需{ap}，剩{b.PlayerAp}）";
            switch (c.Action)
            {
                case "end_turn": return null;
                case "guard": return b.PlayerGuard ? "已在防御（不叠层）" : null;
                case "breathe": return b.BreatheUsed ? "本回合已调整过呼吸" : null;
                case "retreat": return b.RetreatUsed ? "本回合已尝试撤退" : null;
                case "swap":
                {
                    // 调整站位（Q80）：1AP/E0.1 到相邻空位，或与相邻同侧角色双方同意交换（无固定占位锁）；单人也可用，0 经验
                    if (!(SwapDestination(c) is int to) || !SwapTargets(b).Contains(to)) return "请选择相邻的我方站位（1—4号位）";
                    var cost = a.Energy ?? 0;
                    if (s.Stats.Energy < cost - 1e-9) return $"体力不足（需{Fmt.N(cost)}，现有{Fmt.N(s.Stats.Energy)}）";
                    return null;
                }
                case "reload":
                {
                    if (!w.UsesMagazine || wi == null) return w.ConsumesLooseAmmo ? "弓不需要装填，射击直接消耗随身箭" : "当前武器不用装填";
                    if (wi.LoadedAmmo >= w.MagazineCapacity) return "弹仓已满";
                    if (CarriedCount(s, w.AmmoId!) <= 0) return $"随身没有{ItemName(w.AmmoId!)}";
                    return null;
                }
                case "bandage":
                case "use_antibiotics":
                case "use_antiviral_cure":
                {
                    var it = CarriedItem(s, a.ItemId!, c.ItemInstanceId);
                    if (it == null) return $"随身没有可用的{ItemName(a.ItemId!)}（地面、火堆里的不能在战斗中使用）";
                    var use = _items[it.ItemId].Use;
                    return use == null ? "不能使用" : MedicineBlocked(s, use);
                }
            }
            return null;
        }

        private bool AmmoReady(GameState s, WeaponConfig w, ItemInstance? wi, int need)
        {
            if (w.AmmoId == null) return true;
            if (w.UsesMagazine) return wi != null && wi.LoadedAmmo >= need;
            return CarriedCount(s, w.AmmoId) >= need;
        }

        /// <summary>指令预览（右栏）：命中能力值与实际命中率、暴击、伤害区间、体力、弹药、耐久、噪声、撤退率。只读、不推进随机。</summary>
        public BattlePreview PreviewCommand(GameState s, BattleCommand c)
        {
            var p = new BattlePreview { Title = ActionName(c.Action) };
            var b = s.Battle;
            var why = CommandBlocked(s, c);
            p.Ok = why == null;
            p.Reason = why ?? "";
            if (b == null) return p;
            p.EnergyBefore = s.Stats.Energy;
            p.NoiseBefore = b.Noise;
            p.NoiseAfter = b.Noise;
            var w = CurrentWeapon(s, out var wi);
            if (IsSkillAction(c.Action) || c.Action == DesperatePunch)
            {
                var node = SkillOf(c.Action, w);
                p.Title = c.Action == DesperatePunch ? ActionName(c.Action) : node?.Name ?? ActionName(c.Action);
                if (node == null && c.Action != DesperatePunch) return p;
                p.Ap = node?.Ap ?? Act(DesperatePunch)!.ApCost;
                if (node != null && !_exec[node.Id].IsAttack) NonAttackPreview(s, b, c, p, node, _exec[node.Id], w, wi);
                else AttackPreview(s, b, c, p, w, wi);
                p.ConfirmText = p.Ok ? $"执行{p.Title}·{p.Ap}AP" : p.Title;
                return p;
            }
            var a = Act(c.Action);
            if (a == null) return p;
            p.Ap = a.ApCost < 0 ? b.PlayerAp : a.ApCost;
            {
                p.EnergyAfter = s.Stats.Energy;
                switch (c.Action)
                {
                    case "guard":
                    {
                        var shield = GuardShield(s);
                        p.Notes.Add(shield != null
                            ? $"盾类格挡（当前页{ItemName(shield.ItemId)}）：下一次受到的直接伤害×{Fmt.T(Config.CombatMath.ShieldGuardFactor)}，只挡一次，下次自己正常行动开始时清除；{ItemName(shield.ItemId)}耐久−{Fmt.T(Config.Wear.PerExecutedAttackCommand)}（{DurabilityText(shield)}）；之后切页不改这次格挡"
                            : $"普通防御：下一次受到的直接伤害×{Fmt.T(Config.CombatMath.GuardFactor)}，只挡一次，下次自己正常行动开始时清除（追击不清）" + (GuardShieldMissingReason(s) is string gw ? $"；{gw}" : ""));
                        break;
                    }
                    case "breathe":
                        p.EnergyAfter = Math.Min(s.Stats.EnergyMax, s.Stats.Energy + a.RestoreEnergy);
                        p.Notes.Add($"体力+{Fmt.T(a.RestoreEnergy)}、恐慌−{Fmt.T(a.ReducePanic)}；每大回合至多一次（追击不刷新），敌人仍正常行动"); break;
                    case "reload":
                        if (wi != null && w.UsesMagazine)
                        {
                            var n = Math.Min(w.MagazineCapacity - wi.LoadedAmmo, CarriedCount(s, w.AmmoId!));
                            p.AmmoText = $"弹仓{wi.LoadedAmmo}→{wi.LoadedAmmo + n}/{w.MagazineCapacity}（随身{ItemName(w.AmmoId!)}−{n}，总重量不变）";
                        }
                        break;
                    case "switch_weapon":
                    {
                        // 切到哪一页、那页是什么武器，数值与普通攻击预览同一口径（含熟练度，不含招式与身体修正）
                        var page = c.ItemInstanceId ?? (b.ActiveWeaponSlot == 1 ? 2 : 1);
                        var to = WeaponOnPage(s, page, out var tw);
                        var st = WeaponStats(s, tw);
                        var pageName = EquipSlots.Name(EquipSlots.WeaponSlotOfPage(page) ?? EquipSlots.Weapon1);
                        var toName = to == null ? "空槽（拳）" : $"{tw.Name}#{to.InstanceId}";
                        p.Title = $"切换到{pageName}页：{toName}";
                        p.Notes.Add($"{toName}：{SkillName(tw.SkillId)}{st.Level}级，伤害{Fmt.N(st.DamageMin)}—{Fmt.N(st.DamageMax)}，命中能力{Fmt.N(st.Accuracy)}，可打敌方{PosList(to == null && SkillNode(UnarmedBasic) is SkillNodeConfig un ? SkillTargetPositions(s, un, tw) : TargetPositionsOf(tw))}{(to?.Durability != null ? $"，耐久{DurabilityText(to)}" : "")}{(tw.UsesMagazine && to != null ? $"，弹仓{to.LoadedAmmo}/{tw.MagazineCapacity}" : "")}");
                        p.Notes.Add("切页0AP、不耗体力，只换本次可选技能组；两把武器开战时已锁定，不能从随身换入第三把");
                        break;
                    }
                    case "retreat":
                    {
                        var (chance, energy, low) = RetreatChance(s, b, a);
                        p.RetreatChance = chance;
                        p.EnergyCost = energy;
                        p.EnergyAfter = s.Stats.Energy - energy;
                        p.Notes.Add($"成功：本轮推进5分钟后脱战，留在{LocName(s.CurrentLocation)}（不传回营地），存活敌人保留血量回到世界；失败：本回合行动点用尽");
                        if (low) p.Notes.Add($"体力不足{Fmt.N(a.Energy ?? 0)}：强行撤退不耗体力，成功率×{Fmt.T(a.LowEnergyChanceFactor)}");
                        break;
                    }
                    case "bandage":
                    case "use_antibiotics":
                    case "use_antiviral_cure":
                        // 效果数值放在第一条（与执行同一份计算 MedicineEffectText），战斗区执行条直接显示它
                        if (CarriedItem(s, a.ItemId!, c.ItemInstanceId) is ItemInstance med && _items[med.ItemId].Use is ItemUseConfig mu && MedicineEffectText(s, mu) is string mt) { p.EffectText = mt; p.Notes.Add(mt); }
                        p.Notes.Add("效果在确认时应用，不另跳5分钟；本轮结束时统一推进一次世界时间");
                        break;
                    case "swap":
                    {
                        p.EnergyCost = a.Energy ?? 0;
                        p.EnergyAfter = Math.Max(0, s.Stats.Energy - p.EnergyCost);
                        var to = SwapDestination(c) ?? SwapTargets(b).FirstOrDefault();
                        p.Title = $"调整站位：{PosName(PlayerPos(b))}→{PosName(to)}";
                        p.EffectText = AllyAt(b, to) != null ? $"与{PosName(to)}的同伴交换站位" : $"移到{PosName(to)}（空位）";
                        p.Notes.Add(p.EffectText);
                        p.Notes.Add($"{a.ApCost}AP、体力{Fmt.N(p.EnergyCost)}，不得经验；技能按真实站位在使用时判断（可以先换位再出手）");
                        p.Notes.Add(PositionHint(s, b, to));
                        break;
                    }
                    case "end_turn": p.Notes.Add("剩余行动点清零，不积攒到下轮"); break;
                }
            }
            p.ConfirmText = p.Ok ? $"执行{ActionName(c.Action)}·{p.Ap}AP" : ActionName(c.Action);
            return p;
        }

        /// <summary>防具槽的装备磨损一次（A05 §2：每次遭到正直接伤害磨损 1，整条多段指令最多 1）；没有防具或已为 0 返回空串。</summary>
        private string WearArmor(GameState s, string actorId)
        {
            var armor = WornItem(s, actorId, EquipSlots.Armor);
            if (armor?.Durability == null || armor.Durability.Value <= 1e-9) return "";
            return Wear(armor, Config.Equipment.ArmorWearPerHit);
        }

        /// <summary>
        /// 指挥、医疗器具（A05 §1“不可直接攻击”）：没有攻击技能，只用本门的指挥/医疗技能（B7）；要攻击请切换武器页。
        /// </summary>
        public static string SkillLockedReason(WeaponConfig w) =>
            $"{w.Name}是{WeaponKinds.Name(w.WeaponKind)}，不能直接攻击，只能用{WeaponKinds.Name(w.WeaponKind)}技能（要攻击请切换武器页）";

        /// <summary>
        /// 一次攻击的噪声增量（B4 H7 配置化）：枪械类按 noise.gunGain（A01 枪 +25），用弩矢的弩按 noise.crossbowGain（A01 弩 +5），
        /// 弓与近战按 noise.bowAndMeleeGain（0）。新枪弩（勤务手枪、精校步枪、轻型战术弩）A05 没写噪声，按同类沿用（待确认）。
        /// </summary>
        public double NoiseGain(WeaponConfig w) =>
            w.WeaponKind == "firearms" || w.SkillId == "firearms" ? Config.Noise.GunGain
            : w.AmmoId == "bolt" ? Config.Noise.CrossbowGain
            : Config.Noise.BowAndMeleeGain;

        /// <summary>本次攻击的实际噪声增量：武器噪声 + 本页武器“隐声”类词条（combat.noise），不低于 0（B6 R23）。</summary>
        public double AttackNoise(GameState s, WeaponConfig w) =>
            Math.Max(0, NoiseGain(w) + (w.IsFists ? 0 : EquipmentEffects(s, GameState.PlayerId, s.Battle?.ActiveWeaponSlot).Noise));

        /// <summary>撤退率 sigmoid((己方有效速度−敌方最高有效速度)/3+0.7)×恐慌惩罚×低体力惩罚，结果自然在0—1。</summary>
        public (double chance, double energy, bool lowEnergy) RetreatChance(GameState s, BattleState b, CombatActionConfig? a = null)
        {
            a ??= Act("retreat")!;
            var r = Config.Retreat;
            var me = PlayerEffectiveSpeed(s, b);
            var enemy = b.Alive.Where(e => e.Hostile).Select(EnemyEffectiveSpeed).DefaultIfEmpty(0).Max();
            var x = (me - enemy) / r.SpeedDivisor + r.Offset;
            var p = 1 / (1 + Math.Exp(-x));
            var pp = Config.Panic.PenaltyFor(b.Panic);
            if (pp != null) p *= pp.RetreatFactor;
            var cost = a.Energy ?? 0;
            var low = s.Stats.Energy < cost - 1e-9;
            if (low) p *= a.LowEnergyChanceFactor;
            return (p, low ? 0 : cost, low);
        }

        /// <summary>执行战斗指令：带 requestId 幂等；先原子扣 AP、体力、弹药与耐久，再结算命中。</summary>
        public ActionResult ExecuteCommand(GameState s, BattleCommand c)
        {
            if (SeenRequest(s, c.RequestId)) return ActionResult.Repeated();
            // 切换武器页：0AP，不结束激活、不推进队列（B3 H6）
            if (c.Action == "switch_weapon") return c.ItemInstanceId.HasValue ? SelectWeaponPage(s, c.ItemInstanceId.Value, c.RequestId) : ActionResult.Fail("请选择武器页（1或2）");
            var why = CommandBlocked(s, c);
            if (why != null) return ActionResult.Fail(why);
            RememberRequest(s, c.RequestId);
            var b = s.Battle!;
            // 武器技能与应急拳击（B7 R25）：扣技能 AP 后按类型执行（攻击 / 格挡增益指挥医疗装填）
            if (IsSkillAction(c.Action) || c.Action == DesperatePunch)
            {
                var sw = CurrentWeapon(s, out var swi);
                var node = SkillOf(c.Action, sw);
                b.PlayerAp -= node?.Ap ?? Act(DesperatePunch)!.ApCost;
                if (node != null && !_exec[node.Id].IsAttack) ExecuteNonAttack(s, b, c, node, _exec[node.Id], sw, swi);
                else PlayerSkillAttack(s, b, c, sw, swi);
                if (s.Battle == null || s.Progress.Dead) return ActionResult.Success();
                if (b.PlayerAp <= 0) EndPlayerActivation(s);
                return ActionResult.Success();
            }
            var a = Act(c.Action)!;
            var ap = a.ApCost < 0 ? b.PlayerAp : a.ApCost;
            b.PlayerAp -= ap;
            var w = CurrentWeapon(s, out var wi);
            switch (c.Action)
            {
                case "end_turn":
                    BattleLog(s, "你结束了行动。");
                    b.PlayerAp = 0;
                    break;
                case "guard":
                {
                    var noShield = GuardShieldMissingReason(s);
                    var wear = GrantGuard(s, b, 1);
                    BattleLog(s, b.PlayerGuardSourceId is int sid ? $"你举起{ItemName(FindItem(s, sid)?.ItemId ?? "")}格挡（下一次直接伤害×{Fmt.T(b.PlayerGuardFactor)}；{wear}）。"
                        : $"你摆出防御姿态（下一次直接伤害×{Fmt.T(b.PlayerGuardFactor)}{(noShield != null ? "；" + noShield : "")}）。");
                    break;
                }
                case "breathe":
                {
                    b.BreatheUsed = true;
                    var e0 = s.Stats.Energy;
                    s.Stats.Add(StatId.Energy, a.RestoreEnergy);
                    b.Panic = Math.Max(Config.Panic.Range[0], b.Panic - a.ReducePanic);
                    BattleLog(s, $"你调整呼吸：体力{Fmt.N(e0)}→{Fmt.N(s.Stats.Energy)}，恐慌{Fmt.N(b.Panic)}。");
                    break;
                }
                case "reload":
                {
                    var need = w.MagazineCapacity - wi!.LoadedAmmo;
                    var took = TakeCarried(s, w.AmmoId!, need);
                    wi.LoadedAmmo += took;
                    wi.LoadedAmmoId = w.AmmoId;
                    BattleLog(s, $"你装填{ItemName(w.AmmoId!)}{took}发（弹仓{wi.LoadedAmmo}/{w.MagazineCapacity}）。");
                    break;
                }
                case "bandage":
                case "use_antibiotics":
                case "use_antiviral_cure":
                {
                    var it = CarriedItem(s, a.ItemId!, c.ItemInstanceId)!;
                    var use = _items[it.ItemId].Use!;
                    ConsumeOne(s, it);
                    var parts = new List<string>();
                    ApplyMedicine(s, use, parts);
                    BattleLog(s, $"你{ActionName(c.Action)}：{string.Join("，", parts)}。");
                    break;
                }
                case "swap":
                {
                    var from = PlayerPos(b);
                    var to = SwapDestination(c)!.Value;
                    var mate = AllyAt(b, to);
                    MoveAlly(b, from, to);
                    var cost = a.Energy ?? 0;
                    s.Stats.Add(StatId.Energy, -cost);
                    BattleLog(s, $"你调整站位：{PosName(from)}→{PosName(to)}{(mate != null ? "（与同伴交换）" : "")}（体力−{Fmt.N(cost)}）。");
                    break;
                }
                case "retreat":
                {
                    b.RetreatUsed = true;
                    var (chance, energy, low) = RetreatChance(s, b, a);
                    s.Stats.Add(StatId.Energy, -energy);
                    if (s.Rng.NextDouble() < chance)
                    {
                        BattleLog(s, $"撤退成功（{Fmt.Pct(chance)}）。");
                        FinishBattle(s, "retreat");
                        return ActionResult.Success();
                    }
                    BattleLog(s, $"撤退失败（{Fmt.Pct(chance)}{(low ? "，体力不足强行撤退" : "")}），本回合行动点用尽。");
                    b.PlayerAp = 0;
                    break;
                }
                default:
                    break;
            }
            if (s.Battle != null && b.PlayerAp <= 0) EndPlayerActivation(s);
            return ActionResult.Success();
        }

        /// <summary>从随身取用指定数量（按最早实例），返回实际取到的数量。</summary>
        private int TakeCarried(GameState s, string itemId, int count)
        {
            var left = count;
            foreach (var st in s.Items.Where(i => i.IsCarried && i.ItemId == itemId && i.ReservedBy == null).OrderBy(i => i.InstanceId).ToList())
            {
                if (left <= 0) break;
                var n = Math.Min(left, st.Count);
                st.Count -= n;
                left -= n;
                if (st.Count <= 0) s.Items.Remove(st);
            }
            return count - left;
        }

        /// <summary>狼≤1.2、鹿≤2.0 时下次行动逃离（意图随即公开改为逃离）。</summary>
        private void CheckFlee(GameState s, BattleEnemy e)
        {
            var cfg = _enemies[e.EnemyId];
            if (cfg.FleeAtHp.HasValue && e.Alive && e.Hp <= cfg.FleeAtHp.Value + 1e-9 && !e.WillFlee)
            {
                e.WillFlee = true;
                e.Intent = "flee";
                e.IntentLabel = IntentLabel(e);
                BattleLog(s, $"{cfg.Name}重伤，下次行动将逃离。");
            }
        }

        /// <summary>敌人死亡：立即移除卡（不留阻挡位），首次死亡锁定掉落，世界实例删除（死亡不计上限、不复活）。</summary>
        private void KillEnemy(GameState s, BattleEnemy e)
        {
            var b = s.Battle!;
            e.Dead = true;
            e.Hp = 0;
            e.Slot = "";
            var cfg = _enemies[e.EnemyId];
            if (cfg.IsZombie)
            {
                var r = (int)Math.Floor(s.Rng.NextDouble() * 100);
                var pick = LootTable.Pick(Config.ZombieLoot.Table, r);
                b.Loot.Add(new BattleLoot { WorldId = e.WorldId, ItemId = pick.ItemId, Count = pick.ItemId == null ? 0 : pick.Count });
            }
            // R07 §3：普通丧尸每只独立 0.5% 附加 1 份白色活性物质（不覆盖原互斥掉落；动物不抽；首战教学敌人不抽，胜利另发教学实例）
            var sub = Config.ActiveSubstance;
            if (!string.IsNullOrEmpty(sub.ItemId) && sub.NormalDrop.Families.Contains(cfg.Family) && !b.Tutorial)
            {
                if (s.Rng.NextDouble() < sub.NormalDrop.ChancePerEnemy)
                    b.Loot.Add(new BattleLoot
                    {
                        WorldId = e.WorldId, ItemId = sub.ItemId, Count = sub.NormalDrop.Count,
                        Source = new ItemSource { WorldEnemyId = e.WorldId, BattleId = b.BattleId, GrantEventId = $"substance-b{b.BattleId}-w{e.WorldId}" },
                    });
            }
            else
            {
                var carcass = Config.AnimalCarcasses.FirstOrDefault(x => x.EnemyId == e.EnemyId);
                if (carcass != null) b.Loot.Add(new BattleLoot { WorldId = e.WorldId, ItemId = carcass.CarcassItemId, Count = carcass.Count });
            }
            // A09 §1 附加掉落：独立额外判一次（“固定”为必掉），首次死亡锁定、胜利才落地
            if (cfg.ExtraDrop is EnemyDropConfig xd && (xd.Chance >= 1 || s.Rng.NextDouble() < xd.Chance))
                b.Loot.Add(new BattleLoot { WorldId = e.WorldId, ItemId = xd.ItemId, Count = xd.Count });
            s.WorldEnemies.RemoveAll(w => w.Id == e.WorldId);
            DeathSplash(s, b, e, cfg);
        }

        private void CheckVictory(GameState s)
        {
            var b = s.Battle;
            if (b == null || s.Progress.Dead) return;
            if (b.Alive.Any()) return;
            FinishBattle(s, "victory");
        }

        /// <summary>
        /// 战斗结束时把最后一轮的战斗记录写进行动日志（试玩修复 M150 BUG2）：战斗状态随结束清掉，致胜/致死那条指令及其连带结算
        /// （死亡喷溅、反击、迎击、掉落锁定等）原本只在战斗记录里，结束后玩家看不到。
        /// </summary>
        private void EmitLastRoundLog(GameState s, BattleState b)
        {
            var prefix = $"第{b.Round}轮 · ";
            foreach (var line in b.Log.Where(l => l.StartsWith(prefix)).ToList())
                Emit(s, GameEventKind.Battle, "战斗记录 " + line);
        }

        private void BattleDeath(GameState s, string cause)
        {
            var b = s.Battle;
            if (b != null) EmitLastRoundLog(s, b);
            Kill(s, cause);
            if (b != null)
            {
                foreach (var e in b.Enemies.Where(e => e.Alive))
                    if (FindWorldEnemy(s, e.WorldId) is WorldEnemy we) { we.Engaged = false; we.Hp = e.Hp; }
                Emit(s, GameEventKind.BattleEnd, $"战斗中倒下：{cause}。");
            }
            s.Battle = null;
        }

        private void FinishBattleOnDeath(GameState s)
        {
            var b = s.Battle;
            if (b != null) EmitLastRoundLog(s, b);
            if (b != null)
                foreach (var e in b.Enemies.Where(e => e.Alive))
                    if (FindWorldEnemy(s, e.WorldId) is WorldEnemy we) { we.Engaged = false; we.Hp = e.Hp; }
            s.Battle = null;
        }

        /// <summary>
        /// 结束（胜利或撤退成功）：本轮唯一一次世界 tick（中途退出也只推进一次；tick 致死则不提交掉落）；存活敌带 HP 回世界池、清临时状态；
        /// 逃离的狼/鹿迁到合法相邻地或原地隐藏60分钟；胜利一次落地掉落；战后精神−min(5,P/20)，恐慌清除；60分钟随机接敌保护。
        /// </summary>
        private void FinishBattle(GameState s, string outcome, bool tick = true)
        {
            var b = s.Battle!;
            if (tick && !BattleTick(s)) return;
            EmitLastRoundLog(s, b);
            var loc = b.LocationId;
            foreach (var e in b.Enemies)
            {
                var we = FindWorldEnemy(s, e.WorldId);
                if (we == null) continue;
                we.Engaged = false;
                we.Hp = e.Hp;
                if (e.Fled) RelocateFled(s, we);
            }
            var lines = new List<string>();
            if (outcome == "victory")
            {
                foreach (var l in b.Loot.Where(l => l.ItemId != null && l.Count > 0))
                {
                    CreateItems(s, l.ItemId!, l.Count, loc, false, l.Source);
                    lines.Add($"{ItemName(l.ItemId!)}+{l.Count}");
                }
                if (GrantTutorialReward(s, b) is string reward) lines.Add(reward);
            }
            var loss = Math.Min(Config.Panic.MoraleLossMax, b.Panic / Config.Panic.MoraleLossDivisor);
            if (loss > 0) s.Stats.Add(StatId.Morale, -loss);
            s.EncounterGraceUntil = s.GameMinute + Config.Encounters.PostExitRandomEncounterGraceMinutes;
            var head = outcome == "victory" ? "战斗胜利" : "撤退成功";
            // 撤退：只有场上确有倒下敌人的掉落/尸体时才写“放弃”（试玩修复 L3）
            var loot = outcome == "victory" ? (lines.Count > 0 ? $"战利品{string.Join("、", lines)}已放在{LocName(loc)}地面" : "没有战利品")
                : b.Loot.Any(l => l.ItemId != null && l.Count > 0) ? "放弃了场上的尸体与掉落" : null;
            var text = $"{head}：共{b.Round}轮（世界时间{Clock.Duration(b.Ticks * Tick)}）{(loss > 1e-9 ? $"，精神−{Fmt.N(loss)}" : "")}{(loot != null ? "；" + loot : "")}。";   // 试玩修复 B7c-1：精神没变时不写“精神−0.00”
            BattleLog(s, text);
            s.Battle = null;
            Emit(s, GameEventKind.BattleEnd, text, null, outcome == "victory" ? b.Loot.Where(l => l.ItemId != null).Select(l => l.ItemId!) : null);
            UpdateFlags(s);
        }

        /// <summary>狼/鹿自行逃离：迁到合法相邻地（非营地、有空位），没有空位则原地隐藏60分钟；不删除、不补刷、不给尸体。</summary>
        private void RelocateFled(GameState s, WorldEnemy we)
        {
            var targets = PhysicalNeighbors(we.LocationId)
                .Where(l => !Config.Encounters.SafeLocations.Contains(l) && WE.CapOf(l) > 0 && LocalCount(s, l) < WE.CapOf(l))
                .Distinct().OrderBy(l => l).ToList();
            if (targets.Count > 0) we.LocationId = targets[(int)Math.Min(targets.Count - 1, Math.Floor(s.Rng.NextDouble() * targets.Count))];
            else we.HiddenUntil = s.GameMinute + WE.FleeWithoutValidNeighborHiddenMinutes;
        }

        /// <summary>噪声增援（v0.6 §15）：只调用同地现有、未交战的丧尸，保留原类型与 worldEnemyId；场上满3则等空位；没有可用实例就没有增援。</summary>
        private void TryReinforce(GameState s)
        {
            var b = s.Battle!;
            if (!b.ReinforcementRequested || b.ReinforcementUsed) return;
            // 剧情首战只有保留游荡尸，不调用噪声增援（S15 §5）
            if (b.Tutorial && Config.Story.TutorialBattle.NoReinforcement) { b.ReinforcementUsed = true; BattleLog(s, "首战不会引来其他丧尸。"); return; }
            // 容量 C（R05 §4）：场上存活加增援、本场累计投入（含已死/逃离）都不超过 C；没有空位就等下一轮
            if (b.Alive.Count() >= b.Capacity || b.Enemies.Count >= b.Capacity) return;
            b.ReinforcementUsed = true;
            var cand = s.WorldEnemies.Where(w => w.LocationId == b.LocationId && !w.Engaged && !Hidden(s, w)
                                                 && _enemies[w.EnemyId].Family == Config.Noise.ExistingEnemyFamilyFilter)
                .OrderBy(w => w.Id).FirstOrDefault();
            if (cand == null)
            {
                BattleLog(s, "附近没有被吸引的丧尸。");
                return;
            }
            var slot = ReinforceSlot(b).ToString();
            cand.Engaged = true;
            var idx = b.Enemies.Count == 0 ? 1 : b.Enemies.Max(e => e.Index) + 1;
            b.Enemies.Add(InitBattleEnemy(new BattleEnemy { WorldId = cand.Id, Index = idx, EnemyId = cand.EnemyId, Slot = slot, Hp = cand.Hp, Hostile = true, XpHits = new Dictionary<string, int>(cand.XpHits), TieRoll = s.Rng.NextDouble(), JoinRound = b.Round }));
            LockIntent(s, b, b.Enemies[b.Enemies.Count - 1]);
            BattleLog(s, $"枪声引来了{_enemies[cand.EnemyId].Name}（{PosName(int.Parse(slot))}）。");
        }

        /// <summary>
        /// 增援落位（Q80 不再压紧；开发假设 B7d-4）：最后一个存活敌人身后的第一个空槽；身后没有空槽则取最前的空槽。
        /// </summary>
        public int ReinforceSlot(BattleState b)
        {
            var max = Config.Scope.Slots.Count;
            var taken = b.Alive.Select(e => e.Pos).ToHashSet();
            var last = taken.Count > 0 ? taken.Max() : 0;
            for (var p = last + 1; p <= max; p++) if (!taken.Contains(p)) return p;
            for (var p = 1; p <= max; p++) if (!taken.Contains(p)) return p;
            return max;
        }

        /// <summary>
        /// 某个我方槽上当前两页武器的可用情况（调整站位预览）：“在2号位：武器Ⅰ石刀可用；武器Ⅱ木弓可用”。
        /// </summary>
        public string PositionHint(GameState s, BattleState b, int pos)
        {
            var parts = new List<string>();
            foreach (var page in new[] { 1, 2 })
            {
                var it = WeaponOnPage(s, page, out var w);
                var name = it == null ? "拳" : w.Name;
                parts.Add($"武器{(page == 1 ? "Ⅰ" : "Ⅱ")}{name}{(OwnPositionsOf(w).Contains(pos) ? "可用" : $"不可用（要在{PosList(OwnPositionsOf(w))}）")}");
            }
            return $"到{PosName(pos)}后：{string.Join("；", parts)}";
        }

        // ───────────── 战外装备 ─────────────

        // 旧“战外装备当前武器 EquipWeapon”已由七槽换装 Equip/Unequip 取代（GameEngine.Equip.cs，B3 H5）。
    
        // ───────────── 词条平加攻击力（R07 §4、R03 §2） ─────────────

        /// <summary>人物词条的攻击力平加合计（教学融合的绿色“攻击力+0.1”）。</summary>
        public double FlatAttack(GameState s) => s.Traits.Sum(x => x.FlatAttack);

        /// <summary>
        /// 直接伤害 = max(0, 乘算后伤害 + 平加攻击力 − 护甲)，防御生效时再×0.5（R03 §2 建议公式）。
        /// R07 §4.1 算例：乘算后1.20、护甲0.30 → 无词条0.90、有词条1.00；防御0.45/0.50；暴击后1.80 → 1.60。
        /// </summary>
        public static double AttackDamage(double multiplied, double flat, double armor, bool guard, double guardFactor)
        {
            var d = Math.Max(0, multiplied + flat - armor);
            if (guard && d > 0) d *= guardFactor;
            return d;
        }
    }
}

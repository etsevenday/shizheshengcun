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
        /// <summary>嚎叫尸奇数轮使玩家恐慌+8（v0.5 §10、v0.6 §06 正文；参考配置未单列字段）。</summary>
        private const double HowlPanic = 8;
        /// <summary>野猪偶数轮冲撞伤害×1.5（v0.5 §10、v0.6 §06 正文）。</summary>
        private const double BoarChargeFactor = 1.5;
        private const double PlayerIndex = 0;

        private CombatActionConfig? Act(string id) => Config.CombatActions.FirstOrDefault(a => a.Id == id);

        public static readonly string[] AttackActions = { "basic_attack", "cleave", "cut", "precise_thrust", "push", "scatter", "desperate_punch" };

        public static string ActionName(string id) => id switch
        {
            "basic_attack" => "普通攻击",
            "cleave" => "重劈",
            "cut" => "割伤",
            "precise_thrust" => "精准突刺",
            "push" => "推挡",
            "scatter" => "散射",
            "desperate_punch" => "勉强拳击",
            "guard" => "防御",
            "breathe" => "调整呼吸",
            "switch_weapon" => "切换武器页",
            "reload" => "装填",
            "bandage" => "包扎",
            "use_antibiotics" => "用抗生素",
            "use_antiviral_cure" => "用特效药",
            "retreat" => "撤退",
            "end_turn" => "结束行动",
            _ => id,
        };

        // ───────────── 查询 ─────────────

        public BattleEnemy? BattleEnemyAt(BattleState b, int index) => b.Enemies.FirstOrDefault(e => e.Index == index);

        public string EnemyName(BattleEnemy e) => _enemies[e.EnemyId].Name;

        private static bool IsFront(BattleEnemy e) => e.Slot.StartsWith("F");

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

        public double PlayerEffectiveSpeed(GameState s, BattleState b, double? energy = null)
        {
            var m = Config.CombatMath;
            var e = energy ?? s.Stats.Energy;
            var sp = Config.Scope.BasePlayerSpeed;
            if (e < m.LowEnergyExclusive) sp -= m.LowEnergySpeedPenalty;
            if (s.Stats.Wakefulness <= m.LowWakefulnessInclusive) sp -= m.LowWakefulnessSpeedPenalty;
            if (b.PlayerSlowActive) sp -= Config.Statuses.Slow.NextRoundSpeedPenalty;
            return Math.Max(m.MinEffectiveSpeed, sp);
        }

        public double EnemyEffectiveSpeed(BattleEnemy e) =>
            Math.Max(Config.CombatMath.MinEffectiveSpeed, _enemies[e.EnemyId].Speed - (e.SlowActive ? Config.Statuses.Slow.NextRoundSpeedPenalty : 0));

        /// <summary>身体修正（乘命中能力）：E&lt;4、S≤20 各×0.85；恐慌≥60 ×0.85、≥90 ×0.7 择一；失衡×0.9。体力按支付本次成本后计算。</summary>
        public double BodyAccuracyFactor(GameState s, BattleState b, double energyAfter, List<string>? why = null)
        {
            var m = Config.CombatMath;
            double f = 1;
            if (energyAfter < m.LowEnergyExclusive) { f *= m.LowEnergyAccuracyFactor; why?.Add($"低体力×{Fmt.T(m.LowEnergyAccuracyFactor)}"); }
            if (s.Stats.Wakefulness <= m.LowWakefulnessInclusive) { f *= m.LowWakefulnessAccuracyFactor; why?.Add($"低清醒×{Fmt.T(m.LowWakefulnessAccuracyFactor)}"); }
            var pp = Config.Panic.PenaltyFor(b.Panic);
            if (pp != null) { f *= pp.AccuracyRatingFactor; why?.Add($"恐慌≥{Fmt.T(pp.Minimum)}×{Fmt.T(pp.AccuracyRatingFactor)}"); }
            if (b.PlayerStagger) { f *= Config.Statuses.Stagger.NextAttackAccuracyFactor; why?.Add($"失衡×{Fmt.T(Config.Statuses.Stagger.NextAttackAccuracyFactor)}"); }
            return f;
        }

        public static double HitChance(double accuracy, double evasion) => accuracy <= 0 ? 0 : accuracy / (accuracy + evasion);

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
            var group = EncounterEnemies(s).Where(w => !w.Engaged && w.LocationId == s.CurrentLocation).ToList();
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

        /// <summary>入战情境（R03 §2.1）：主动接近、探索/清理接敌、剧情首战。</summary>
        public const string FearApproach = "approach";
        public const string FearEncounter = "encounter";
        public const string FearTutorial = "tutorial";

        private void StartBattle(GameState s, List<WorldEnemy> group, bool surprise, string source, string context, bool tutorial = false)
        {
            var slots = Config.Scope.Slots;
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
            var k = 0;
            var joined = group.Take(Config.Scope.MaxEnemies).ToList();
            foreach (var w in joined)
            {
                w.Engaged = true;
                b.Enemies.Add(new BattleEnemy { WorldId = w.Id, Index = k + 1, EnemyId = w.EnemyId, Slot = slots[k], Hp = w.Hp, Hostile = w.Hostile, XpHits = new Dictionary<string, int>(w.XpHits) });
                k++;
            }
            var pc = Config.Panic;
            // 受惊（R03 §2.1、R05 §6.3）：同一 battleId 只判一次并锁存；恐慌基础值 (100−M)/2，受惊才加主惊吓源的惊吓值
            RollFear(s, b, joined, context);
            b.Panic = Math.Max(pc.Range[0], Math.Min(pc.Range[1], (pc.InitialBase - s.Stats.Morale) / pc.InitialDivisor + b.FearPanic));
            b.PanicPeak = b.Panic;
            s.Battle = b;
            var names = string.Join("、", b.Enemies.Select(e => $"{e.Slot}{EnemyName(e)}"));
            var fearText = !b.FearSourceWorldId.HasValue ? ""
                : b.Frightened ? $"（受惊：{_enemies[joined.First(w => w.Id == b.FearSourceWorldId).EnemyId].Name}，受惊率{Fmt.Pct(b.FearChance)}，恐慌+{Fmt.T(b.FearPanic)}）"
                : $"（未受惊，受惊率{Fmt.Pct(b.FearChance)}）";
            BattleLog(s, $"{source}：{names}。恐慌{Fmt.N(b.Panic)}{fearText}。");
            Emit(s, GameEventKind.Battle, $"战斗开始：{names}。", null, b.Enemies.Select(e => $"enemy:{e.WorldId}"));
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

        /// <summary>轮初：增援加入、减速生效、锁定敌人意图、按先手值排队列（平手按稳定序号，玩家为 0）。</summary>
        private void BeginRound(GameState s)
        {
            var b = s.Battle!;
            TryReinforce(s);
            b.PlayerSlowActive = b.PlayerSlowPending;
            b.PlayerSlowPending = false;
            foreach (var e in b.Alive)
            {
                e.SlowActive = e.SlowPending;
                e.SlowPending = false;
                e.Acted = false;
            }
            b.RoundStartEnergy = s.Stats.Energy;
            var m = Config.CombatMath;
            var entries = new List<(int idx, double ini)>();
            double Roll() => m.InitiativeRollMin + Math.Floor(s.Rng.NextDouble() * (m.InitiativeRollMax - m.InitiativeRollMin + 1));
            entries.Add((0, PlayerEffectiveSpeed(s, b) + Roll()));
            foreach (var e in b.Alive.OrderBy(e => e.Index)) entries.Add((e.Index, EnemyEffectiveSpeed(e) + Roll()));
            var ordered = entries.OrderByDescending(x => x.ini).ThenBy(x => x.idx).ToList();
            b.Queue = ordered.Select(x => x.idx).ToList();
            b.Initiative = ordered.Select(x => x.ini).ToList();
            b.Cursor = 0;
            b.PlayerActing = false;
            b.PlayerAp = 0;
            foreach (var e in b.Alive) LockIntent(s, b, e);
            BattleLog(s, "行动顺序：" + string.Join(" → ", ordered.Select(x => x.idx == 0 ? $"你({Fmt.T(x.ini)})" : $"{EnemyName(BattleEnemyAt(b, x.idx)!)}({Fmt.T(x.ini)})")));
        }

        /// <summary>轮初锁定并公开敌人意图（不公开伤害抽值）；丧尸准备普通伤害动作时 20% 选咬击。</summary>
        private void LockIntent(GameState s, BattleState b, BattleEnemy e)
        {
            var cfg = _enemies[e.EnemyId];
            string intent;
            if (e.WillFlee) intent = "flee";
            else if (!e.Hostile) intent = "watch";
            else switch (cfg.Behavior)
            {
                case "guard_first_round" when b.Round == 1: intent = "guard"; break;
                case "howl_odd_rounds" when b.Round % 2 == 1: intent = "howl"; break;
                case "charge_prepare_odd_attack_even": intent = b.Round % 2 == 1 ? "charge_prepare" : "charge"; break;
                default: intent = "attack"; break;
            }
            if (intent == "attack" && cfg.BiteIntentWeightPct > 0 && s.Rng.NextDouble() * 100 < cfg.BiteIntentWeightPct) intent = "bite";
            e.Intent = intent;
            e.IntentLabel = IntentLabel(e);
        }

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

        public string IntentLabel(BattleEnemy e)
        {
            var cfg = _enemies[e.EnemyId];
            var dmg = $"{Fmt.N(cfg.DamageMin)}—{Fmt.N(cfg.DamageMax)}";
            return e.Intent switch
            {
                "attack" => $"{AttackVerb(cfg)} {dmg}",
                "bite" => $"咬击 {dmg}（可能感染）",
                "charge_prepare" => "蓄力（下轮冲撞）",
                "charge" => $"冲撞 {Fmt.N(cfg.DamageMin * BoarChargeFactor)}—{Fmt.N(cfg.DamageMax * BoarChargeFactor)}",
                "guard" => "防御",
                "howl" => $"嚎叫（恐慌+{Fmt.T(HowlPanic)}）",
                "flee" => "逃离",
                "watch" => "警戒（不主动攻击）",
                _ => e.Intent,
            };
        }

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
            if (idx == 0)
            {
                StartPlayerActivation(s);
                return new BattleStepResult { Kind = s.Battle == null ? BattleStepKind.Ended : BattleStepKind.PlayerTurnStarted, Text = b.Log.LastOrDefault() ?? "" };
            }
            var e = BattleEnemyAt(b, idx);
            b.Cursor++;
            if (e == null || !e.Alive) return new BattleStepResult { Kind = BattleStepKind.EnemyActed, Text = "" };
            EnemyActivation(s, e);
            return new BattleStepResult { Kind = s.Battle == null ? BattleStepKind.Ended : BattleStepKind.EnemyActed, Text = b.Log.LastOrDefault() ?? "" };
        }

        /// <summary>玩家激活开始：自己的出血 → 判死 → 清旧防御 → 恢复本次 AP。</summary>
        private void StartPlayerActivation(GameState s)
        {
            var b = s.Battle!;
            var body = s.Body;
            if (body.BleedTriggers > 0)
            {
                s.Stats.Add(StatId.Health, -body.BleedDamage);
                body.BleedTriggers--;
                BattleLog(s, $"你流血，健康−{Fmt.N(body.BleedDamage)}（还剩{body.BleedTriggers}次）。");
                if (body.BleedTriggers == 0) body.BleedDamage = 0;
                if (s.Stats.Health <= 0) { BattleDeath(s, "出血"); return; }
            }
            b.PlayerGuard = false;
            b.PlayerAp = Config.Scope.HumanApPerActivation;
            b.PlayerActing = true;
            b.BreatheUsed = false;
            b.RetreatUsed = false;
            BattleLog(s, $"轮到你行动：{b.PlayerAp}AP。");
        }

        private void EndPlayerActivation(GameState s)
        {
            var b = s.Battle;
            if (b == null) return;
            b.PlayerAp = 0;
            b.PlayerActing = false;
            b.Cursor++;
        }

        /// <summary>
        /// 轮末：后排压到最左空前排（已行动标记不变）；减速与失衡到期；推进本轮唯一一次 5 分钟世界 tick；未结束则开下一轮。
        /// </summary>
        private void EndRound(GameState s)
        {
            var b = s.Battle!;
            AdvanceRear(b);
            if (b.PlayerStagger && b.PlayerStaggerRound < b.Round) b.PlayerStagger = false;
            b.PlayerSlowActive = false;
            foreach (var e in b.Alive)
            {
                e.SlowActive = false;
                if (e.Stagger && e.StaggerRound < b.Round) e.Stagger = false;
            }
            if (!BattleTick(s)) return;
            b.Round++;
            BeginRound(s);
        }

        private void AdvanceRear(BattleState b)
        {
            foreach (var rear in b.Alive.Where(e => !IsFront(e)).OrderBy(e => e.Index).ToList())
            {
                var free = Config.Scope.Slots.Where(x => x.StartsWith("F")).FirstOrDefault(x => b.Alive.All(e => e.Slot != x));
                if (free == null) break;
                rear.Slot = free;
            }
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

        /// <summary>战斗段：N−1.875/W−4.5/S−5/M−0.5 每小时，E 自然率 0；体力归零按工作计损血；腹泻照常。</summary>
        private SegmentContext BattleSegment(GameState s, ActorState actor)
        {
            var c = Config;
            var ctx = new SegmentContext { Rates = c.BattleTime.HourlyRates.Copy(), IsWork = c.BattleTime.CountsAsWorkingForZeroEnergyDamage, ActionType = null };
            ctx.Sheltered = IsSheltered(s, s.CurrentLocation);
            if (actor.DiarrheaUntil.HasValue && actor.DiarrheaUntil.Value > s.GameMinute) ctx.Rates.Hydration += c.ItemActions.DiarrheaExtraHydrationPerHour;
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

        // ───────────── 敌方行动 ─────────────

        private void EnemyActivation(GameState s, BattleEnemy e)
        {
            var b = s.Battle!;
            var cfg = _enemies[e.EnemyId];
            e.Acted = true;
            // 自己的出血（丧尸免疫，不会有）
            if (e.BleedTriggers > 0)
            {
                e.Hp -= e.BleedDamage;
                e.BleedTriggers--;
                BattleLog(s, $"{EnemyName(e)}流血，−{Fmt.N(e.BleedDamage)}。");
                if (e.Hp <= 1e-9) { KillEnemy(s, e); CheckVictory(s); return; }
                CheckFlee(s, e);
            }
            e.Guard = false;
            var frontBarrier = !IsFront(e) && b.Alive.Any(x => x != e && IsFront(x));
            switch (e.Intent)
            {
                case "flee":
                    e.Fled = true;
                    e.Slot = "";
                    BattleLog(s, $"{EnemyName(e)}逃离了战斗（没有尸体，不算击杀）。");
                    CheckVictory(s);
                    return;
                case "watch":
                    BattleLog(s, $"{EnemyName(e)}警戒地观察，没有攻击。");
                    return;
                case "guard":
                    e.Guard = true;
                    BattleLog(s, $"{EnemyName(e)}举盾防御（下一次受到的直接伤害×{Fmt.T(Config.CombatMath.GuardFactor)}）。");
                    return;
                case "howl":
                    b.Panic = Math.Min(Config.Panic.Range[1], b.Panic + HowlPanic);
                    b.PanicPeak = Math.Max(b.PanicPeak, b.Panic);
                    BattleLog(s, $"{EnemyName(e)}嚎叫，你的恐慌+{Fmt.T(HowlPanic)}（{Fmt.N(b.Panic)}）。");
                    return;
                case "charge_prepare":
                    e.Charged = true;
                    BattleLog(s, $"{EnemyName(e)}蓄力，下轮冲撞。");
                    return;
            }
            if (frontBarrier)
            {
                BattleLog(s, $"{EnemyName(e)}在后排前压/等待（前排有屏障，不能隔人攻击）。");
                return;
            }
            EnemyAttack(s, e, cfg);
        }

        private void EnemyAttack(GameState s, BattleEnemy e, EnemyConfig cfg)
        {
            var b = s.Battle!;
            var bite = e.Intent == "bite";
            var charge = e.Intent == "charge";
            var verb = bite ? "咬击" : charge ? "冲撞" : AttackVerb(cfg);
            var a = cfg.AttackRating;
            if (e.Stagger) { a *= Config.Statuses.Stagger.NextAttackAccuracyFactor; e.Stagger = false; }
            var p = HitChance(a, Config.Scope.BasePlayerDodge);
            if (s.Rng.NextDouble() >= p)
            {
                BattleLog(s, $"{EnemyName(e)}{verb}落空（命中率{Fmt.Pct(p)}）。");
                return;
            }
            var raw = cfg.DamageMin + s.Rng.NextDouble() * (cfg.DamageMax - cfg.DamageMin);
            if (charge) raw *= BoarChargeFactor;
            var dmg = Math.Max(0, raw - Config.Scope.BasePlayerArmor);
            var guarded = false;
            if (b.PlayerGuard && dmg > 0)
            {
                dmg *= Config.CombatMath.GuardFactor;
                b.PlayerGuard = false;
                guarded = true;
            }
            if (charge) e.Charged = false;
            if (dmg <= 0)
            {
                BattleLog(s, $"{EnemyName(e)}{verb}被完全挡住。");
                return;
            }
            s.Stats.Add(StatId.Health, -dmg);
            s.Progress.LastDamageCause = $"被{EnemyName(e)}{verb}";
            b.Panic = Math.Min(Config.Panic.Range[1], b.Panic + dmg * Config.Panic.DamageGainFactor);
            b.PanicPeak = Math.Max(b.PanicPeak, b.Panic);
            var extra = new List<string>();
            if (guarded) extra.Add("防御减半");
            if (s.Stats.Health <= 0)
            {
                BattleLog(s, $"{EnemyName(e)}{verb}命中，健康−{Fmt.N(dmg)}。");
                BattleDeath(s, s.Progress.LastDamageCause);
                return;
            }
            if (bite)
            {
                // 只有丧尸咬击命中且造成正直接伤害才判一次病毒暴露，结果随事务保存（v0.6 §09）
                var infectedBefore = s.Body.Infected;
                var hit = ExposeToVirus(s, out var chance);
                extra.Add(hit ? (infectedBefore ? $"病毒暴露成功（{Fmt.Pct(chance)}），病毒+{Fmt.T(Config.Infection.OnRepeatedSuccessfulExposure.LoadDelta)}" : $"病毒暴露成功（{Fmt.Pct(chance)}），你被感染了") : $"病毒暴露判定未感染（{Fmt.Pct(chance)}）");
            }
            else
            {
                switch (cfg.Behavior)
                {
                    case "slow_on_hit": b.PlayerSlowPending = true; extra.Add($"你被扯腿，下轮速度−{Fmt.T(Config.Statuses.Slow.NextRoundSpeedPenalty)}"); break;
                    case "stagger_on_hit": b.PlayerStagger = true; b.PlayerStaggerRound = b.Round; extra.Add("你失衡了"); break;
                }
                if (cfg.Behavior.StartsWith("bleed_on_hit_") && s.Rng.NextDouble() * 100 < Formula.Num(cfg.Behavior, 0))
                {
                    ApplyPlayerBleed(s);
                    extra.Add($"你开始出血（{Fmt.T(s.Body.BleedDamage)}×{s.Body.BleedTriggers}）");
                }
            }
            BattleLog(s, $"{EnemyName(e)}{verb}命中，健康−{Fmt.N(dmg)}（恐慌{Fmt.N(b.Panic)}）{(extra.Count > 0 ? "：" + string.Join("，", extra) : "")}。");
        }

        // ───────────── 指令 ─────────────

        /// <summary>界面指令条：全部指令（含不可用原因），不保留瞄准空位。</summary>
        public List<BattleCommandOption> BattleOptions(GameState s)
        {
            var list = new List<BattleCommandOption>();
            var b = s.Battle;
            if (b == null) return list;
            var w = CurrentWeapon(s, out _);
            foreach (var id in AttackActions)
            {
                var a = Act(id);
                if (a == null || !CommandCompatible(s, a, w)) continue;
                var opt = new BattleCommandOption { Action = id, Label = ActionName(id), Group = id == "basic_attack" || id == "desperate_punch" ? "attack" : "move", Ap = a.ApCost, NeedsTarget = id != "scatter" };
                opt.Energy = AttackEnergy(s, a, w);
                foreach (var e in b.Alive) if (CommandBlocked(s, BattleCommand.Of(id, e.Index)) == null) opt.Targets.Add(e.Index);
                opt.Reason = CommandBlocked(s, BattleCommand.Of(id, opt.Targets.Count > 0 ? opt.Targets[0] : b.Alive.Select(x => (int?)x.Index).FirstOrDefault()));
                if (id == "scatter") opt.Reason = CommandBlocked(s, BattleCommand.Of(id));
                opt.Available = opt.Reason == null;
                list.Add(opt);
            }
            foreach (var id in new[] { "guard", "breathe", "reload", "retreat", "end_turn" })
            {
                var a = Act(id);
                if (a == null) continue;
                if (id == "reload" && !w.UsesMagazine) continue;
                var why = CommandBlocked(s, BattleCommand.Of(id));
                list.Add(new BattleCommandOption { Action = id, Label = ActionName(id), Group = "general", Ap = a.ApCost, Energy = id == "retreat" ? (a.Energy ?? 0) : 0, Available = why == null, Reason = why });
            }
            // 切换武器页不在指令条里：界面用武器Ⅰ/武器Ⅱ页签（SelectWeaponPage，0AP）
            foreach (var id in new[] { "bandage", "use_antibiotics", "use_antiviral_cure" })
            {
                var a = Act(id);
                if (a?.ItemId == null) continue;
                var why = CommandBlocked(s, BattleCommand.Of(id));
                var have = s.Items.Where(i => i.IsCarried && i.ItemId == a.ItemId).Sum(i => i.Count);
                list.Add(new BattleCommandOption { Action = id, Label = $"{ActionName(id)}（随身×{have}）", Group = "item", Ap = a.ApCost, Available = why == null, Reason = why });
            }
            return list;
        }

        /// <summary>
        /// 招式与当前武器/技能是否兼容（Q19、R03 §7.4）：武器技能类别、专属武器、技能等级门槛。战斗操作台与系统说明“指令一览”共用，
        /// 不兼容的不列出；AP、弹药、耐久等暂时不足不在这里过滤（列出并写禁用原因）。
        /// </summary>
        public bool CommandCompatible(GameState s, CombatActionConfig a, WeaponConfig w)
        {
            var common = a.Id == "basic_attack" || a.Id == "desperate_punch";
            if (!common && a.Skills.Count > 0 && !a.Skills.Contains(w.SkillId)) return false;
            if (a.WeaponId != null && a.WeaponId != w.Id) return false;
            if (!common && a.MinLevel > 0 && SkillLevel(s, w.SkillId) < a.MinLevel) return false;
            return true;
        }

        /// <summary>
        /// 系统说明“指令一览”（Q19）：按当前实际装备武器与技能等级筛出兼容招式，再列通用指令与医疗物品指令；战斗内外都能用。
        /// 战斗中可用性与原因直接取 CommandBlocked（与操作台同源）；战斗外只写暂时条件（弹仓空、耐久不足、没有随身药品）。只读。
        /// </summary>
        public List<BattleCommandOption> CompatibleCommands(GameState s)
        {
            if (s.Battle != null)
            {
                var seen = new HashSet<string>();
                var inBattle = BattleOptions(s).Where(o => seen.Add(o.Action)).ToList();
                if (Act("switch_weapon") is CombatActionConfig swb)
                    inBattle.Add(new BattleCommandOption { Action = "switch_weapon", Label = ActionName("switch_weapon"), Group = "general", Ap = swb.ApCost, Available = true });
                return inBattle;
            }
            var list = new List<BattleCommandOption>();
            var w = CurrentWeapon(s, out var wi);
            foreach (var id in AttackActions)
            {
                var a = Act(id);
                if (a == null || !CommandCompatible(s, a, w)) continue;
                string? why = null;
                if (id != "desperate_punch")
                {
                    if (!w.CanAttack) why = SkillLockedReason(w);
                    else if (wi != null && !HasWear(wi, Config.Wear.PerExecutedAttackCommand)) why = $"{w.Name}耐久不足（{DurabilityText(wi)}）";
                    else if (!AmmoReady(s, w, wi, Math.Max(1, a.AmmoCost))) why = w.UsesMagazine ? "弹仓空了：先装填" : $"随身没有{ItemName(w.AmmoId!)}";
                }
                list.Add(new BattleCommandOption
                {
                    Action = id, Label = ActionName(id), Group = id == "basic_attack" || id == "desperate_punch" ? "attack" : "move",
                    Ap = a.ApCost, NeedsTarget = id != "scatter", Energy = AttackEnergy(s, a, w), Available = why == null, Reason = why,
                });
            }
            foreach (var id in new[] { "guard", "breathe", "reload", "switch_weapon", "retreat", "end_turn" })
            {
                var a = Act(id);
                if (a == null) continue;
                if (id == "reload" && !w.UsesMagazine) continue;
                string? why = null;
                if (id == "reload" && wi != null)
                    why = wi.LoadedAmmo >= w.MagazineCapacity ? "弹仓已满" : CarriedCount(s, w.AmmoId!) <= 0 ? $"随身没有{ItemName(w.AmmoId!)}" : null;
                list.Add(new BattleCommandOption { Action = id, Label = ActionName(id), Group = "general", Ap = a.ApCost, Available = why == null, Reason = why });
            }
            foreach (var id in new[] { "bandage", "use_antibiotics", "use_antiviral_cure" })
            {
                var a = Act(id);
                if (a?.ItemId == null) continue;
                var have = s.Items.Where(i => i.IsCarried && i.ItemId == a.ItemId).Sum(i => i.Count);
                list.Add(new BattleCommandOption
                {
                    Action = id, Label = $"{ActionName(id)}（随身×{have}）", Group = "item", Ap = a.ApCost,
                    Available = have > 0, Reason = have > 0 ? null : $"随身没有{ItemName(a.ItemId)}",
                });
            }
            return list;
        }

        /// <summary>攻击类指令的实际体力成本：基础/(1+0.025×对应技能L)；勉强拳击为 0。</summary>
        /// <summary>武器按当前熟练度的普通攻击口径：伤害×(1+0.035L)、命中能力+3L（不含招式、身体、恐慌与站位修正）。界面列表与预览统一用它。</summary>
        public (int Level, double DamageMin, double DamageMax, double Accuracy) WeaponStats(GameState s, WeaponConfig w)
        {
            var lv = SkillLevel(s, w.SkillId);
            var f = SkillDamageFactor(lv);
            return (lv, w.DamageMin * f, w.DamageMax * f, w.AccuracyRating + SkillAccuracyBonus(lv));
        }

        public double AttackEnergy(GameState s, CombatActionConfig a, WeaponConfig w)
        {
            if (a.Id == "desperate_punch") return 0;
            var baseCost = a.EnergySource == "weapon" ? w.EnergyCost : a.Energy ?? 0;
            return SkillEnergyCost(baseCost, SkillLevel(s, w.SkillId));
        }

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

        /// <summary>该武器在当前站位下能否打到这个目标（v0.5 §07）：前排武器只打前排，前排全空时可直接打剩余后排。</summary>
        public bool CanReach(BattleState b, WeaponConfig w, BattleEnemy t, bool frontOnly)
        {
            if (!t.Alive) return false;
            if (IsFront(t)) return true;
            var barrier = b.Alive.Any(IsFront);
            if (!barrier) return true;
            return !frontOnly && w.ReachesRear;
        }

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
            var a = Act(c.Action);
            if (a == null) return c.Action == "aim" ? "瞄准已移除" : "未知指令";
            var ap = a.ApCost < 0 ? b.PlayerAp : a.ApCost;
            if (a.ApCost > 0 && ap > b.PlayerAp) return $"行动点不足（需{ap}，剩{b.PlayerAp}）";
            var w = CurrentWeapon(s, out var wi);
            switch (c.Action)
            {
                case "end_turn": return null;
                case "guard": return b.PlayerGuard ? "已在防御（不叠层）" : null;
                case "breathe": return b.BreatheUsed ? "本回合已调整过呼吸" : null;
                case "retreat": return b.RetreatUsed ? "本回合已尝试撤退" : null;
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
            // 攻击类
            if (c.Action != "desperate_punch" && !w.CanAttack) return SkillLockedReason(w);
            if (c.Action != "basic_attack" && c.Action != "desperate_punch" && a.Skills.Count > 0 && !a.Skills.Contains(w.SkillId))
                return $"当前武器（{w.Name}）不能用{ActionName(c.Action)}";
            if (a.WeaponId != null && a.WeaponId != w.Id) return $"{ActionName(c.Action)}需要{_weapons[a.WeaponId].Name}";
            if (a.MinLevel > 0 && c.Action != "basic_attack" && c.Action != "desperate_punch")
            {
                var lv = SkillLevel(s, w.SkillId);
                if (lv < a.MinLevel) return $"需要{SkillName(w.SkillId)}{a.MinLevel}级（当前{lv}级）";
            }
            var basic = Act("basic_attack")!;
            if (c.Action == "desperate_punch")
            {
                // v0.5 动作表：“仅体力不足正常攻击时”；弹仓空、耐久不足时应装填或换武器，不放开勉强拳击（试玩修复 L1）
                if (s.Stats.Energy >= AttackEnergy(s, basic, w) - 1e-9)
                    return "只在体力不足以普通攻击时可用" + (w.UsesMagazine && !AmmoReady(s, w, wi, 1) ? "；弹仓空请装填或切换武器页" : wi != null && !HasWear(wi, Config.Wear.PerExecutedAttackCommand) ? "；耐久不足请切换武器页" : "");
            }
            else
            {
                var energy = AttackEnergy(s, a, w);
                if (s.Stats.Energy < energy - 1e-9) return $"体力不足（需{Fmt.N(energy)}，现有{Fmt.N(s.Stats.Energy)}）；可勉强拳击、防御、调整呼吸或撤退";
                if (wi != null && !HasWear(wi, Config.Wear.PerExecutedAttackCommand)) return $"{w.Name}耐久不足（{DurabilityText(wi)}），可切换武器页";
                var ammo = Math.Max(1, a.AmmoCost);
                if (!AmmoReady(s, w, wi, ammo))
                    return w.UsesMagazine ? "弹仓空了：先装填" : $"随身没有{ItemName(w.AmmoId!)}";
            }
            if (c.Action == "scatter")
            {
                if (!b.Alive.Any(x => IsFront(x) || !b.Alive.Any(IsFront))) return "前排没有目标";
                return null;
            }
            if (!c.Target.HasValue) return "请选择目标";
            var t = BattleEnemyAt(b, c.Target.Value);
            if (t == null || !t.Alive) return "目标已不在";
            var weapon = c.Action == "desperate_punch" ? _weapons[Fists] : w;
            var frontOnly = weapon.Targets == "front";
            if (!CanReach(b, weapon, t, frontOnly)) return "后排：前排还有敌人，射程不足";
            return null;
        }

        private bool AmmoReady(GameState s, WeaponConfig w, ItemInstance? wi, int need)
        {
            if (w.AmmoId == null) return true;
            if (w.UsesMagazine) return wi != null && wi.LoadedAmmo >= need;
            return CarriedCount(s, w.AmmoId) >= need;
        }

        /// <summary>某攻击对某目标的命中、暴击与伤害区间（体力按支付本次成本后；不推进随机）。</summary>
        private BattleTargetPreview TargetCalc(GameState s, BattleState b, CombatActionConfig a, WeaponConfig w, BattleEnemy t, double energyAfter)
        {
            var cfg = _enemies[t.EnemyId];
            var lv = SkillLevel(s, w.SkillId);
            var acc = (w.AccuracyRating + SkillAccuracyBonus(lv)) * a.AccuracyRatingFactor * BodyAccuracyFactor(s, b, energyAfter);
            var crit = w.CritRatingBase + Config.CombatMath.CritAccuracyPerLevel * lv;
            var barrier = b.Alive.Any(IsFront);
            var rear = !IsFront(t) && barrier && w.RearDamageFactor < 1;
            var rank = rear ? w.RearDamageFactor : 1;
            var mult = SkillDamageFactor(lv) * a.DamageFactor * rank;
            var flat = FlatAttackFor(s, a, w);
            double Dmg(double raw, double critF) => AttackDamage(raw * mult * critF, flat, cfg.Armor, t.Guard, Config.CombatMath.GuardFactor);
            var cf = Config.CombatMath.CritFactor;
            return new BattleTargetPreview
            {
                Index = t.Index, Name = cfg.Name, Slot = t.Slot, Hp = t.Hp, Armor = cfg.Armor, Guarded = t.Guard, RearPenalty = rear,
                AccuracyRating = acc, Evasion = cfg.Dodge, HitChance = HitChance(acc, cfg.Dodge),
                CritRating = crit, CritChance = CritChance(crit),
                DamageMin = Dmg(w.DamageMin, 1), DamageMax = Dmg(w.DamageMax, 1), CritDamageMin = Dmg(w.DamageMin, cf), CritDamageMax = Dmg(w.DamageMax, cf),
            };
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
            var a = Act(c.Action);
            if (a == null) return p;
            p.Ap = a.ApCost < 0 ? b.PlayerAp : a.ApCost;
            p.EnergyBefore = s.Stats.Energy;
            p.NoiseBefore = b.Noise;
            p.NoiseAfter = b.Noise;
            var w = CurrentWeapon(s, out var wi);
            if (AttackActions.Contains(c.Action))
            {
                var weapon = c.Action == "desperate_punch" ? _weapons[Fists] : w;
                p.EnergyCost = AttackEnergy(s, a, w);
                p.EnergyAfter = Math.Max(0, s.Stats.Energy - p.EnergyCost);
                var targets = c.Action == "scatter"
                    ? ScatterTargets(b, a)
                    : c.Target.HasValue && BattleEnemyAt(b, c.Target.Value) is BattleEnemy one ? new List<BattleEnemy> { one } : new List<BattleEnemy>();
                foreach (var t in targets) p.Targets.Add(TargetCalc(s, b, a, weapon, t, p.EnergyAfter));
                if (c.Action != "desperate_punch")
                {
                    if (w.UsesMagazine && wi != null) p.AmmoText = $"弹仓{wi.LoadedAmmo}→{Math.Max(0, wi.LoadedAmmo - Math.Max(1, a.AmmoCost))}/{w.MagazineCapacity}";
                    else if (w.ConsumesLooseAmmo) p.AmmoText = $"随身{ItemName(w.AmmoId!)}{CarriedCount(s, w.AmmoId!)}→{Math.Max(0, CarriedCount(s, w.AmmoId!) - 1)}";
                    if (wi?.Durability != null) p.WearText = $"{w.Name}耐久{Fmt.N(wi.Durability.Value)}→{Fmt.N(Math.Max(0, wi.Durability.Value - Config.Wear.PerExecutedAttackCommand))}（命中与否都扣）";
                    p.NoiseAfter = Math.Min(Config.Noise.Max, b.Noise + NoiseGain(w));
                }
                else p.Notes.Add("用拳头勉强一击：不磨损原武器，伤害×0.5，命中能力×0.65");
                if (c.Action == "cut") p.Notes.Add($"命中后施加出血{Fmt.T(a.Bleed)}×{a.BleedTriggers}（丧尸免疫）");
                if (c.Action == "push") p.Notes.Add("零直接伤害，命中令目标下次攻击失衡（命中能力×0.9）");
                if (c.Action == "scatter") p.Notes.Add($"前排至多{a.MaxTargets}个目标各判一次，伤害×{Fmt.T(a.DamageFactor)}，只耗1发");
                var lv = SkillLevel(s, weapon.SkillId);
                p.Notes.Add($"{SkillName(weapon.SkillId)}{lv}级：伤害×{Fmt.T(SkillDamageFactor(lv))}，命中能力+{Fmt.T(SkillAccuracyBonus(lv))}；每命中一个目标+{Config.Xp.DamagingAttackCommand}经验（含被护甲挡住的命中，每个敌人每技能最多{Config.Xp.MaxHitsPerEncounterEnemySkill}次）");
                var fl = FlatAttackFor(s, a, weapon);
                if (fl > 0) p.Notes.Add($"词条攻击力+{Fmt.T(fl)}：在技能/招式/站位/暴击乘算后、扣护甲前平加，防御再×{Fmt.T(Config.CombatMath.GuardFactor)}");
            }
            else
            {
                p.EnergyAfter = s.Stats.Energy;
                switch (c.Action)
                {
                    case "guard": p.Notes.Add($"下一次受到的直接伤害×{Fmt.T(Config.CombatMath.GuardFactor)}，只挡一次，下次自己行动开始时清除"); break;
                    case "breathe":
                        p.EnergyAfter = Math.Min(s.Stats.EnergyMax, s.Stats.Energy + a.RestoreEnergy);
                        p.Notes.Add($"体力+{Fmt.T(a.RestoreEnergy)}、恐慌−{Fmt.T(a.ReducePanic)}；每回合至多一次，敌人仍正常行动"); break;
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
                        p.Notes.Add($"{toName}：{SkillName(tw.SkillId)}{st.Level}级，伤害{Fmt.N(st.DamageMin)}—{Fmt.N(st.DamageMax)}，命中能力{Fmt.N(st.Accuracy)}，{(tw.Targets == "front" ? "只打前排" : tw.RearDamageFactor < 1 ? $"可打后排（×{Fmt.T(tw.RearDamageFactor)}）" : "前后排都可打")}{(to?.Durability != null ? $"，耐久{DurabilityText(to)}" : "")}{(tw.UsesMagazine && to != null ? $"，弹仓{to.LoadedAmmo}/{tw.MagazineCapacity}" : "")}");
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
                    case "end_turn": p.Notes.Add("剩余行动点清零，不积攒到下轮"); break;
                }
            }
            p.ConfirmText = p.Ok ? $"执行{ActionName(c.Action)}·{p.Ap}AP" : ActionName(c.Action);
            return p;
        }

        private List<BattleEnemy> ScatterTargets(BattleState b, CombatActionConfig a)
        {
            var front = b.Alive.Where(IsFront).OrderBy(e => e.Slot).ToList();
            if (front.Count == 0) front = b.Alive.OrderBy(e => e.Slot).ToList();
            return front.Take(Math.Max(1, a.MaxTargets)).ToList();
        }

        /// <summary>
        /// 盾、指挥、医疗器具（B4 H2）：可穿进武器槽、占各自 kind，但对应技能在 B5/B7 才上线，现在不能用它攻击；通用指令与勉强拳击规则照旧。
        /// </summary>
        public static string SkillLockedReason(WeaponConfig w) =>
            $"{w.Name}是{WeaponKinds.Name(w.WeaponKind)}，它的技能尚未开放，现在不能用它攻击（可切换武器页）";

        /// <summary>
        /// 一次攻击的噪声增量（B4 H7 配置化）：枪械类按 noise.gunGain（A01 枪 +25），用弩矢的弩按 noise.crossbowGain（A01 弩 +5），
        /// 弓与近战按 noise.bowAndMeleeGain（0）。新枪弩（勤务手枪、精校步枪、轻型战术弩）A05 没写噪声，按同类沿用（待确认）。
        /// </summary>
        public double NoiseGain(WeaponConfig w) =>
            w.WeaponKind == "firearms" || w.SkillId == "firearms" ? Config.Noise.GunGain
            : w.AmmoId == "bolt" ? Config.Noise.CrossbowGain
            : Config.Noise.BowAndMeleeGain;

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
                    b.PlayerGuard = true;
                    BattleLog(s, "你摆出防御姿态。");
                    break;
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
                    PlayerAttack(s, b, a, w, wi, c);
                    if (s.Battle == null || s.Progress.Dead) return ActionResult.Success();
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

        private void PlayerAttack(GameState s, BattleState b, CombatActionConfig a, WeaponConfig w, ItemInstance? wi, BattleCommand c)
        {
            var desperate = a.Id == "desperate_punch";
            var weapon = desperate ? _weapons[Fists] : w;
            // 1. 原子扣费：体力、弹药、耐久、噪声
            var energy = AttackEnergy(s, a, w);
            s.Stats.Add(StatId.Energy, -energy);
            var spent = new List<string>();
            if (energy > 0) spent.Add($"体力−{Fmt.N(energy)}");
            if (!desperate)
            {
                if (w.UsesMagazine && wi != null) { wi.LoadedAmmo -= Math.Max(1, a.AmmoCost); spent.Add($"弹仓{wi.LoadedAmmo}/{w.MagazineCapacity}"); b.ShotsFired++; }
                else if (w.ConsumesLooseAmmo) { TakeCarried(s, w.AmmoId!, 1); spent.Add($"{ItemName(w.AmmoId!)}−1"); b.ShotsFired++; }
                if (wi?.Durability != null) spent.Add(Wear(wi, Config.Wear.PerExecutedAttackCommand));
                var gain = NoiseGain(w);
                if (gain > 0)
                {
                    b.Noise = Math.Min(Config.Noise.Max, b.Noise + gain);
                    if (b.Noise >= Config.Noise.Max - 1e-9 && !b.ReinforcementRequested && !b.ReinforcementUsed && Config.Noise.MaxReinforcementsPerBattle > 0)
                    {
                        b.ReinforcementRequested = true;
                        BattleLog(s, "噪声满了：附近的丧尸可能被吸引过来。");
                    }
                }
            }
            // 2. 结算：每个目标独立判命中 → 暴击 → 伤害
            var targets = a.Id == "scatter" ? ScatterTargets(b, a) : new List<BattleEnemy> { BattleEnemyAt(b, c.Target!.Value)! };
            var parts = new List<string>();
            var hitTargets = new List<BattleEnemy>();
            var energyAfter = s.Stats.Energy;
            var flat = FlatAttackFor(s, a, weapon);
            foreach (var t in targets)
            {
                var pv = TargetCalc(s, b, a, weapon, t, energyAfter);
                var cfg = _enemies[t.EnemyId];
                // 主动攻击中立雄鹿：转敌对
                if (!t.Hostile)
                {
                    t.Hostile = true;
                    if (FindWorldEnemy(s, t.WorldId) is WorldEnemy we) we.Hostile = true;
                    t.Intent = "attack";
                    t.IntentLabel = IntentLabel(t);
                    parts.Add($"{cfg.Name}被激怒");
                }
                if (s.Rng.NextDouble() >= pv.HitChance)
                {
                    parts.Add($"{cfg.Name}未命中（{Fmt.Pct(pv.HitChance)}）");
                    continue;
                }
                hitTargets.Add(t);
                var crit = s.Rng.NextDouble() < pv.CritChance;
                var raw = weapon.DamageMin + s.Rng.NextDouble() * (weapon.DamageMax - weapon.DamageMin);
                var lv = SkillLevel(s, weapon.SkillId);
                // 伤害 = max(0, 乘算后伤害 + 平加攻击力 − 护甲)，防御再×0.5（R03 §2、R07 §4：+0.1 在乘算后、扣甲前，不被暴击再放大）
                var multiplied = raw * SkillDamageFactor(lv) * a.DamageFactor * (pv.RearPenalty ? weapon.RearDamageFactor : 1) * (crit ? Config.CombatMath.CritFactor : 1);
                var guarded = t.Guard && Math.Max(0, multiplied + flat - cfg.Armor) > 0;
                var dmg = AttackDamage(multiplied, flat, cfg.Armor, guarded, Config.CombatMath.GuardFactor);
                if (guarded) t.Guard = false;
                if (a.Id == "push")
                {
                    t.Stagger = true;
                    t.StaggerRound = b.Round;
                    parts.Add($"{cfg.Name}被推得失衡");
                    continue;
                }
                if (dmg <= 0)
                {
                    parts.Add($"{cfg.Name}被护甲完全挡住");
                    continue;
                }
                t.Hp -= dmg;
                parts.Add($"{cfg.Name}{(crit ? "暴击" : "命中")}−{Fmt.N(dmg)}{(guarded ? "（防御减半）" : "")}{(pv.RearPenalty ? "（后排×" + Fmt.T(weapon.RearDamageFactor) + "）" : "")}");
                if (a.Id == "cut" && !cfg.ImmuneBleed && t.Hp > 0)
                {
                    t.BleedTriggers = a.BleedTriggers;
                    t.BleedDamage = a.Bleed;
                    parts.Add($"{cfg.Name}出血");
                }
                if (t.Hp <= 1e-9) { KillEnemy(s, t); parts.Add($"{cfg.Name}倒下"); }
                else CheckFlee(s, t);
            }
            b.PlayerStagger = false;   // 失衡在本次攻击结算后消失
            // 3. 经验（v1.0 P01，R01 §3）：逐命中目标计——每个命中且该目标该技能未满3次的目标各+2，含护甲完全抵消的0伤命中与推挡命中；
            //    未命中不计，已满的目标不给但不影响其他目标；计数随 worldEnemyId 保存，撤退、巡游、换 encounterId 都不重置
            var xpNote = "";
            var skill = weapon.SkillId;
            var gained = 0;
            foreach (var t in hitTargets)
            {
                t.XpHits.TryGetValue(skill, out var hits);
                if (hits >= Config.Xp.MaxHitsPerEncounterEnemySkill) continue;
                t.XpHits[skill] = hits + 1;
                if (FindWorldEnemy(s, t.WorldId) is WorldEnemy we) we.XpHits[skill] = hits + 1;
                GainXp(s, skill, Config.Xp.DamagingAttackCommand);
                gained += Config.Xp.DamagingAttackCommand;
            }
            if (gained > 0) xpNote = $"；{SkillName(skill)}经验+{gained}";
            BattleLog(s, $"你{ActionName(a.Id)}{(desperate ? "" : $"（{weapon.Name}）")}：{string.Join("，", parts)}{(spent.Count > 0 ? "；" + string.Join("，", spent) : "")}{xpNote}。");
            CheckVictory(s);
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
            s.WorldEnemies.RemoveAll(w => w.Id == e.WorldId);
        }

        private void CheckVictory(GameState s)
        {
            var b = s.Battle;
            if (b == null || s.Progress.Dead) return;
            if (b.Alive.Any()) return;
            FinishBattle(s, "victory");
        }

        private void BattleDeath(GameState s, string cause)
        {
            var b = s.Battle;
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
            if (b != null)
                foreach (var e in b.Enemies.Where(e => e.Alive))
                    if (FindWorldEnemy(s, e.WorldId) is WorldEnemy we) { we.Engaged = false; we.Hp = e.Hp; }
            s.Battle = null;
        }

        /// <summary>
        /// 结束（胜利或撤退成功）：本轮唯一一次世界 tick（中途退出也只推进一次；tick 致死则不提交掉落）；存活敌带 HP 回世界池、清临时状态；
        /// 逃离的狼/鹿迁到合法相邻地或原地隐藏60分钟；胜利一次落地掉落；战后精神−min(5,P/20)，恐慌清除；60分钟随机接敌保护。
        /// </summary>
        private void FinishBattle(GameState s, string outcome)
        {
            var b = s.Battle!;
            if (!BattleTick(s)) return;
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
            var text = $"{head}：共{b.Round}轮（世界时间{Clock.Duration(b.Ticks * Tick)}），精神−{Fmt.N(loss)}{(loot != null ? "；" + loot : "")}。";
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
            if (b.Alive.Count() >= Config.Scope.MaxEnemies) return;
            b.ReinforcementUsed = true;
            var cand = s.WorldEnemies.Where(w => w.LocationId == b.LocationId && !w.Engaged && !Hidden(s, w)
                                                 && _enemies[w.EnemyId].Family == Config.Noise.ExistingEnemyFamilyFilter)
                .OrderBy(w => w.Id).FirstOrDefault();
            if (cand == null)
            {
                BattleLog(s, "附近没有被吸引的丧尸。");
                return;
            }
            var slot = Config.Scope.Slots.FirstOrDefault(x => b.Alive.All(e => e.Slot != x)) ?? Config.Scope.Slots.Last();
            cand.Engaged = true;
            var idx = b.Enemies.Count == 0 ? 1 : b.Enemies.Max(e => e.Index) + 1;
            b.Enemies.Add(new BattleEnemy { WorldId = cand.Id, Index = idx, EnemyId = cand.EnemyId, Slot = slot, Hp = cand.Hp, Hostile = true, XpHits = new Dictionary<string, int>(cand.XpHits) });
            LockIntent(s, b, b.Enemies[b.Enemies.Count - 1]);
            BattleLog(s, $"枪声引来了{_enemies[cand.EnemyId].Name}（{slot}）。");
        }

        // ───────────── 战外装备 ─────────────

        // 旧“战外装备当前武器 EquipWeapon”已由七槽换装 Equip/Unequip 取代（GameEngine.Equip.cs，B3 H5）。
    
        // ───────────── 词条平加攻击力（R07 §4、R03 §2） ─────────────

        /// <summary>人物词条的攻击力平加合计（教学融合的绿色“攻击力+0.1”）。</summary>
        public double FlatAttack(GameState s) => s.Traits.Sum(x => x.FlatAttack);

        /// <summary>本次攻击是否吃平加：只作用于有正基础伤害的直接攻击（含拳）；推挡这类基础0伤动作不加（R07 §4）。</summary>
        public double FlatAttackFor(GameState s, CombatActionConfig a, WeaponConfig w) =>
            a.DamageFactor > 0 && w.DamageMax > 0 ? FlatAttack(s) : 0;

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

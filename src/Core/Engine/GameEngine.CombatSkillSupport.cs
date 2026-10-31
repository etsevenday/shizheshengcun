using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 非攻击技能（格挡、自身增益、指挥、医疗、装填）、攻击增益的消耗与反应攻击（反击、迎击）（A06 §2、R03 §3/§4；开发计划 v0.9 B7 R25）。
    /// 我方只有主角一人（队友参战在 B8）：“任一友方/全队”的指挥与医疗以自己为目标；需要相邻/其他队友的（保护、掩护、整队、协同）列出但禁用。
    /// 增益持续“N轮”= 施加的这一轮起到第 r+N 轮末（开发假设 B7-3）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public static bool InspireActive(BattleState b) => b.InspireBonus > 0 && b.InspireUntilRound >= b.Round;
        public static bool AllOutActive(BattleState b) => b.AllOutFactor > 1 && b.AllOutUntilRound >= b.Round;
        public static bool PlayerWeakActive(BattleState b) => b.PlayerWeakFactor < 1 && b.PlayerWeakUntilRound >= b.Round;
        public static bool AxeChargeOn(BattleState b) => b.ChargeFactor > 1 && b.ChargeUntilRound >= b.Round;
        public static bool PlayerArmorBroken(BattleState b) => b.PlayerArmorBreak > 0 && b.PlayerArmorBreakUntilRound >= b.Round;

        /// <summary>斧蓄力只给“下次该斧攻击”：同一把斧、未过期。</summary>
        private bool AxeChargeActive(GameState s, BattleState b, AttackSpec sp)
        {
            if (!AxeChargeOn(b) || sp.Desperate) return false;
            var it = WeaponOnPage(s, sp.Page, out _);
            return it != null && it.InstanceId == b.ChargeItem;
        }

        /// <summary>攻击结算后用掉“下一次攻击”的增益（鼓舞、总攻令、斧蓄力）；鼓舞/总攻令被实际用掉才发指挥经验（A06 §3）。</summary>
        private void ConsumeAttackBuffs(GameState s, BattleState b, AttackSpec sp)
        {
            if (sp.Reaction || sp.Desperate) return;
            if (InspireActive(b)) { b.InspireBonus = 0; GrantPendingCommandXp(s, b, "command_01"); }
            if (AllOutActive(b)) { b.AllOutFactor = 0; GrantPendingCommandXp(s, b, "command_10"); }
            if (AxeChargeActive(s, b, sp)) b.ChargeFactor = 0;
        }

        /// <summary>
        /// 增益被当前视角成员的攻击用掉（鼓舞/总攻令）或协同 AP 被他用掉：给当初施放的人发指挥经验（B9a：待发键带施术者与目标；旧档键只有技能 ID = 主角给自己）。
        /// </summary>
        private void GrantPendingCommandXp(GameState s, BattleState b, string nodeId)
        {
            var me = b.FocusId;
            foreach (var key in b.PendingCommandXp.ToList())
            {
                var (node, caster, target) = ParseCommandKey(key);
                if (node != nodeId || target != me) continue;
                b.PendingCommandXp.Remove(key);
                if (caster == GameState.PlayerId && target == GameState.PlayerId) { b.PendingCommandXp.Add(nodeId); GrantCommandXp(s, b, nodeId); }
                else GrantSupportXp(s, b, nodeId, caster, target, "command");
            }
        }

        /// <summary>指挥经验：每场每目标每 skillId 至多 +2，须有实际增益（A06 §3）；鼓舞/总攻令在增益被用掉时才发。</summary>
        private void GrantCommandXp(GameState s, BattleState b, string nodeId)
        {
            if (!b.PendingCommandXp.Remove(nodeId) && nodeId is "command_01" or "command_10") return;
            if (b.CommandXp.Contains(nodeId)) return;
            b.CommandXp.Add(nodeId);
            if (SkillMaxed(s, "command")) return;
            GainXp(s, "command", Config.Xp.DamagingAttackCommand);
            BattleLog(s, $"{SkillName("command")}经验+{Config.Xp.DamagingAttackCommand}（{(SkillNames.TryGetValue(nodeId, out var nm) ? nm : nodeId)}生效）。");
        }

        /// <summary>把敌人本轮意图改为普通攻击（被激怒的雄鹿、蓄力被打断）。</summary>
        private void RelockAsAttack(GameState s, BattleEnemy t)
        {
            t.Intent = "attack";
            t.IntentLabel = IntentLabel(t);
        }

        /// <summary>正在保护 t 的敌人（存活、不是它自己）。</summary>
        private static BattleEnemy? ProtectorOf(BattleState b, BattleEnemy t) =>
            b.Alive.FirstOrDefault(e => e.ProtectingIndex == t.Index && e.Index != t.Index);

        // ───────────── 非攻击技能：能否执行 ─────────────

        private static string NeedAlliesReason(SkillNodeConfig n) => $"只有你一人在场，没有可{(n.Id.StartsWith("shield") ? "保护" : n.Id == "command_03" ? "交换站位" : "协同")}的队友（队友参战在后续版本）";

        /// <summary>医疗技能缺的随身物资（只取本人随身、未变质的；地面/营区的不能在战斗中用）。</summary>
        private string? MissingMedicalItem(GameState s, SkillExecConfig x)
        {
            foreach (var kv in x.Items)
                if (s.Items.Where(i => CarriedByMe(s, i) && i.ItemId == kv.Key && i.ReservedBy == null && !IsSpoiled(s, i)).Sum(i => i.Count) < kv.Value)
                    return $"随身没有{ItemName(kv.Key)}" + (kv.Value > 1 ? $"×{kv.Value}" : "");
            return null;
        }

        /// <summary>医疗技能的效果判定（与执行同源）：能止血、能回血（共享绷带 60 分钟回血冷却）、能解除失衡/虚弱。</summary>
        private (bool bleed, double heal, bool status, string? why) MedicalOutcome(GameState s, BattleState? b, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, int? medLevel = null)
        {
            var bleed = x.StopBleed && s.Body.BleedTriggers > 0;
            double heal = 0;
            string? why = null;
            if (x.Heal > 0)
            {
                var ready = s.BandageReadyAt <= s.GameMinute;
                var low = !x.HealMaxRatio.HasValue || s.Stats.Health <= s.Stats.HealthMax * x.HealMaxRatio.Value + 1e-9;
                var amount = MedicalHeal(x.Heal, medLevel ?? SkillLevel(s, Medicine)) + w.Trait("medicalHealBonus") + (x.HealMaxRatio.HasValue ? w.Trait("criticalCareBonus") : 0);
                if (!low) why = $"健康高于上限的{Fmt.Pct(x.HealMaxRatio!.Value)}，不需要{n.Name}";
                else if (!ready) why = $"回血冷却中（与绷带共享），还需{s.BandageReadyAt - s.GameMinute}分钟";
                else if (s.Stats.Health >= s.Stats.HealthMax - 1e-9) why = "健康已满";
                else heal = Math.Min(amount, s.Stats.HealthMax - s.Stats.Health);
            }
            var status = x.RemoveWeakOrStagger && b != null && (PlayerWeakActive(b) || b.PlayerStagger);
            if (x.RemoveWeakOrStagger && !status) why = $"{Who(s)}没有失衡或虚弱";
            return (bleed, heal, status, bleed || heal > 0 || status ? null : why ?? "没有可处理的伤势");
        }

        /// <summary>非攻击技能能否执行（战斗中；CommandBlocked 的分支，已过解锁/冷却/AP/体力/使用位）。</summary>
        private string? NonAttackBlocked(GameState s, BattleState b, BattleCommand c, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi)
        {
            if (x.NeedsAllies && AllyCount(b) <= 1) return NeedAlliesReason(n);
            // B9a：指挥/医疗/保护以友方为目标（Q75：医疗任一存活友方含自己、扣施术者物资）；目标的状态按目标本人判断
            if (TargetsAlly(x) && AllyTargetBlocked(s, b, c, n, x) is string tw) return tw;
            var target = TargetsAlly(x) ? AllyTargetOf(b, c, x)! : b.FocusId;
            switch (x.Type)
            {
                case "guard":
                    if (b.PlayerGuard) return "已在防御（不叠层）";
                    if (x.CounterFactor > 0 && b.CounterFactor > 0) return "已在反击架势";
                    return null;
                case "buff":
                    if (x.ChargeFactor > 0 && AxeChargeOn(b)) return "已蓄力（不叠）";
                    if (x.BraceCharges > 0 && b.BraceCharges > 0) return "已架矛（不叠）";
                    if (x.Cleanse && StruggleOutcome(b, x) is var so && !so.cleanse && so.to == 0) return "你在4号位不能再后移，也没有可解除的减速/束缚/标记（整条无收益，不扣成本）";
                    return null;
                case "ally":
                {
                    if (x.Protect && ProtectBlocked(b.Protections.Where(kv => kv.Key != b.FocusId).ToDictionary(kv => kv.Key, kv => kv.Value), b.FocusId, target) is string pw) return pw;
                    using (FocusOn(s, target))
                    {
                        var who = target == GameState.PlayerId ? "你" : Who(s);
                        if (x.InspireAccuracy > 0 && InspireActive(b) && b.InspireBonus >= x.InspireAccuracy) return $"{(target == GameState.PlayerId ? "" : who)}已有鼓舞（同名不叠）";
                        if (x.AllOutFactor > 0 && AllOutActive(b)) return $"{(target == GameState.PlayerId ? "" : who)}已有总攻令（同名不叠）";
                        if (x.PanicReduce > 0)
                        {
                            if (b.CalmRound == b.Round) return $"本回合已对{who}用过稳心（同目标每大回合一次）";
                            if (b.Panic <= 1e-9) return $"{(target == GameState.PlayerId ? "" : who)}恐慌已是0，没有效果";
                        }
                        if (x.ApBonus > 0 && b.Me.ApBonusNext > 0) return $"{who}已有协同（不叠）";
                    }
                    return null;
                }
                case "medical":
                {
                    if (MissingMedicalItem(s, x) is string miss) return miss + "（地面、营区的不能在战斗中用）";
                    var lv = SkillLevel(s, Medicine);
                    var caster = b.FocusId;
                    using (FocusOn(s, target))
                    {
                        var pre = target != caster ? $"{Who(s)}：" : "";
                        if (x.UseBandage)
                        {
                            var use = BandageItem?.Use;
                            return use == null ? "不能使用" : MedicineBlocked(s, use) is string mb ? pre + mb : null;
                        }
                        return MedicalOutcome(s, b, n, x, w, lv).why is string mw ? pre + mw : null;
                    }
                }
                case "reload":
                    if (!w.UsesMagazine || wi == null) return "当前武器不用装填";
                    if (wi.LoadedAmmo >= w.MagazineCapacity) return "弹仓已满";
                    if (CarriedCount(s, w.AmmoId!) <= 0) return $"随身没有{ItemName(w.AmmoId!)}";
                    return null;
            }
            return null;
        }

        /// <summary>非攻击技能的战外条件（系统说明）：需要队友、随身物资、弹仓。</summary>
        private string? NonAttackStaticReason(GameState s, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi)
        {
            if (x.NeedsAllies) return NeedAlliesReason(n);
            if (x.Type == "medical") return MissingMedicalItem(s, x);
            if (x.Type == "reload" && wi != null && w.UsesMagazine)
                return wi.LoadedAmmo >= w.MagazineCapacity ? "弹仓已满" : CarriedCount(s, w.AmmoId!) <= 0 ? $"随身没有{ItemName(w.AmmoId!)}" : null;
            return null;
        }

        // ───────────── 非攻击技能：预览与执行 ─────────────

        private void NonAttackPreview(GameState s, BattleState b, BattleCommand c, BattlePreview p, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi)
        {
            p.EnergyCost = SkillEnergy(s, n, w);
            p.EnergyAfter = Math.Max(0, s.Stats.Energy - p.EnergyCost);
            p.Notes.Add($"{n.Name}：{n.Text}");
            var solo = AllyCount(b) <= 1;
            switch (x.Type)
            {
                case "guard":
                {
                    var shield = GuardShield(s);
                    var f = shield != null ? Config.CombatMath.ShieldGuardFactor : Config.CombatMath.GuardFactor;
                    p.Notes.Add($"格挡{x.GuardCharges}次：下一次受到的直接伤害×{Fmt.T(f)}{(shield != null ? $"（盾类格挡：当前页{ItemName(shield.ItemId)}耐久−{Fmt.T(Config.Wear.PerExecutedAttackCommand)}）" : GuardShieldMissingReason(s) is string gw ? $"（{gw}）" : "")}，到你下次正常行动开始清除");
                    if (x.CounterFactor > 0) p.Notes.Add($"反击：受到直接攻击命中后，用{w.Name}反击1次，伤害×{Fmt.T(x.CounterFactor * w.Trait("counterDamage", 1))}（反击不再触发反应）");
                    if (x.DisplaceResist > 0) p.Notes.Add($"位移抵抗+{Fmt.T(x.DisplaceResist)}个百分点，到第{b.Round + x.BuffRounds}轮末（被拉扯/击退判控制时生效，与装备位移抵抗取强）");
                    if (x.TeamGuardFactor > 0 && solo) p.Notes.Add("其他队员的减伤：现在只有你一人，没有效果");
                    break;
                }
                case "buff":
                    if (x.ChargeFactor > 0) p.Notes.Add($"下次用这把斧攻击伤害×{Fmt.T(x.ChargeFactor)}，到第{b.Round + 1}轮末；不叠");
                    if (x.BraceCharges > 0)
                    {
                        var charges = Math.Min(Math.Max(x.BraceMax, 1), x.BraceCharges + (int)w.Trait("braceExtra"));
                        p.Notes.Add($"迎击{charges}次：敌人主动前移后，用{w.Name}迎击，伤害×{Fmt.T(x.BraceFactor * w.Trait("braceDamage", 1))}，到你下次正常行动开始清除");
                    }
                    if (x.Cleanse)
                    {
                        var (cl, to) = StruggleOutcome(b, x);
                        p.EffectText = (cl ? "解除减速" : "没有可解除的减速/束缚/标记") + "；" + (to > 0 ? $"后移到{PosName(to)}{(AllyAt(b, to) != null ? "（与同伴交换）" : "")}" : "已在4号位，原地不移");
                        p.Notes.Insert(0, p.EffectText);
                        p.Notes.Add("只解减速/束缚/标记；不解失衡、虚弱、出血、破甲、感染、保护");
                    }
                    break;
                case "ally":
                    if (x.NeedsAllies && solo) p.Notes.Add(NeedAlliesReason(n));
                    else if (AllySkillNotDone(x)) p.Notes.Add($"{n.Name}（目标与相邻友方交换站位）多人战斗里还没做（开发计划 B9a.4）");
                    else if (x.Protect || x.ApBonus > 0)
                    {
                        var pt = AllyTargetOf(b, c, x);
                        p.Notes.Add(pt != null ? $"目标：{ActorDisplayName(s, pt)}（{PosName(AllyPos(b, pt))}）" : x.Protect ? "目标：与你相邻的一名队友" : "目标：一名其他队友");
                        if (x.Protect) p.Notes.Add($"接下目标下一次单体直接攻击（范围攻击、流血不转移），到{Who(s)}下次正常行动开始" + (x.TeamGuardFactor > 0 ? $"；双方下一次受到的直接伤害×{Fmt.T(x.TeamGuardFactor)}，到第{b.Round + x.BuffRounds}轮末" : ""));
                        if (x.ApBonus > 0) p.Notes.Add($"目标下一次正常行动 AP+{x.ApBonus}（总数不超过{Config.Scope.ApCap}，不加追击）");
                        p.Notes.Add(x.Protect ? "有效拦截时得盾经验（每场每目标一次）" : "增益被实际用掉才得指挥经验（每场每目标每技能一次）");
                    }
                    else
                    {
                        var at = AllyTargetOf(b, c, x) ?? b.FocusId;
                        p.Notes.Add(solo ? "目标：你自己（我方只有你一人）" : $"目标：{(at == b.FocusId ? "自己" : ActorDisplayName(s, at))}（可点我方任一在场成员）");
                        if (x.InspireAccuracy > 0) p.Notes.Add($"下一次攻击命中能力+{Fmt.T(x.InspireAccuracy)}，到第{b.Round + x.BuffRounds + (int)w.Trait("commandRounds")}轮末");
                        if (x.PanicReduce > 0) { var tp = b.Fighter(at).Panic; p.Notes.Add($"恐慌{Fmt.N(tp)}→{Fmt.N(Math.Max(Config.Panic.Range[0], tp - x.PanicReduce))}；同目标每大回合一次"); }
                        if (x.AllOutFactor > 0) p.Notes.Add($"下一次攻击伤害×{Fmt.T(x.AllOutFactor)}，到第{b.Round + x.BuffRounds + (int)w.Trait("commandRounds")}轮末；同名不叠");
                        if (w.Trait("firstCommandAccuracy") > 0 && !b.FirstCommandUsed) p.Notes.Add($"{w.Name}“预判”：本场首次号令，另加下一击命中能力+{Fmt.T(w.Trait("firstCommandAccuracy"))}");
                        p.Notes.Add($"{w.Name}耐久−1（有效时）；" + (SkillMaxed(s, "command") ? XpText(s, "command", 0) : $"增益被实际用掉才得指挥经验+{Config.Xp.DamagingAttackCommand}（每场每技能一次）"));
                    }
                    break;
                case "medical":
                {
                    var mt = AllyTargetOf(b, c, x) ?? b.FocusId;
                    var lv = SkillLevel(s, Medicine);
                    p.Notes.Add($"消耗{Who(s)}随身{string.Join("、", x.Items.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"))}；目标：" + (solo ? "你自己（我方只有你一人）" : $"{(mt == b.FocusId ? "自己" : ActorDisplayName(s, mt))}（可点我方任一在场成员；回血冷却按目标本人）"));
                    using (FocusOn(s, mt))
                    {
                        if (x.UseBandage && BandageItem?.Use is ItemUseConfig bu && MedicineEffectText(s, bu, lv) is string bt) { p.EffectText = bt; p.Notes.Insert(0, bt); }
                        else
                        {
                            var (bleed, heal, status, why) = MedicalOutcome(s, b, n, x, w, lv);
                            var fx = new List<string>();
                            if (bleed) fx.Add("止血");
                            if (heal > 0) fx.Add($"健康{Fmt.N(s.Stats.Health)}→{Fmt.N(s.Stats.Health + heal)}（共享绷带60分钟回血冷却）");
                            if (status) fx.Add(PlayerWeakActive(b) ? "解除虚弱" : "解除失衡");
                            p.EffectText = fx.Count > 0 ? string.Join("；", fx) : why;
                            if (p.EffectText != null) p.Notes.Insert(0, p.EffectText);
                        }
                    }
                    p.Notes.Add($"有效时{ItemName(w.Id)}耐久−1、{(SkillMaxed(s, Medicine) ? XpText(s, Medicine, 0) : $"医疗经验+{Config.Xp.Medical}")}；治疗不暴击");
                    break;
                }
                case "reload":
                    if (wi != null && w.UsesMagazine)
                    {
                        var k = Math.Min(w.MagazineCapacity - wi.LoadedAmmo, CarriedCount(s, w.AmmoId!));
                        p.AmmoText = $"弹仓{wi.LoadedAmmo}→{wi.LoadedAmmo + k}/{w.MagazineCapacity}（随身{ItemName(w.AmmoId!)}−{k}）";
                        p.Notes.Add("装满本武器弹仓可容数量，本次不再另付通用装填");
                    }
                    break;
            }
            if (n.CooldownRounds > 0) p.Notes.Add($"冷却{n.CooldownRounds}轮：本轮用后第{b.Round + n.CooldownRounds + 1}轮起可再用");
        }

        private void ExecuteNonAttack(GameState s, BattleState b, BattleCommand c, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi)
        {
            var energy = SkillEnergy(s, n, w);
            s.Stats.Add(StatId.Energy, -energy);
            b.SkillUsedRound[n.Id] = b.Round;
            var parts = new List<string>();
            var solo = AllyCount(b) <= 1;
            var page = b.ActiveWeaponSlot;
            switch (x.Type)
            {
                case "guard":
                {
                    var wear = GrantGuard(s, b, x.GuardCharges);
                    parts.Add($"格挡{b.PlayerGuardCharges}次（直接伤害×{Fmt.T(b.PlayerGuardFactor)}）");
                    if (wear != "") parts.Add(wear);
                    if (x.CounterFactor > 0)
                    {
                        b.CounterFactor = x.CounterFactor * w.Trait("counterDamage", 1);
                        b.CounterPage = page;
                        parts.Add($"受到直接攻击命中后反击1次（伤害×{Fmt.T(b.CounterFactor)}）");
                    }
                    if (x.DisplaceResist > 0)
                    {
                        b.DisplaceResist = x.DisplaceResist;
                        b.DisplaceUntilRound = b.Round + x.BuffRounds;
                        parts.Add($"位移抵抗+{Fmt.T(x.DisplaceResist)}个百分点到第{b.DisplaceUntilRound}轮末");
                    }
                    if (x.TeamGuardFactor > 0 && solo) parts.Add("没有其他队员可受减伤");
                    else if (x.TeamGuardFactor > 0)
                    {
                        // 坚守（shield_10，B9a）：其他在场队员下一次直接伤害×0.8，持续 buffRounds 轮
                        var others = AlliesOnField(b).Where(a => a != b.FocusId).ToList();
                        foreach (var o in others) { var of = b.Fighter(o); of.CoverFactor = Math.Min(of.CoverUntilRound >= b.Round ? of.CoverFactor : 1, x.TeamGuardFactor); of.CoverUntilRound = Math.Max(of.CoverUntilRound, b.Round + x.BuffRounds); }
                        parts.Add($"{string.Join("、", others.Select(o => ActorDisplayName(s, o)))}下一次直接伤害×{Fmt.T(x.TeamGuardFactor)}（到第{b.Round + x.BuffRounds}轮末）");
                    }
                    break;
                }
                case "buff":
                    if (x.ChargeFactor > 0)
                    {
                        b.ChargeFactor = x.ChargeFactor;
                        b.ChargeUntilRound = b.Round + 1;
                        b.ChargeItem = wi?.InstanceId;
                        parts.Add($"下次用{w.Name}攻击伤害×{Fmt.T(x.ChargeFactor)}（到第{b.ChargeUntilRound}轮末）");
                    }
                    if (x.BraceCharges > 0)
                    {
                        b.BraceCharges = Math.Min(Math.Max(x.BraceMax, 1), x.BraceCharges + (int)w.Trait("braceExtra"));
                        b.BraceFactor = x.BraceFactor * w.Trait("braceDamage", 1);
                        b.BracePage = page;
                        parts.Add($"架矛：敌人主动前移后迎击{b.BraceCharges}次（伤害×{Fmt.T(b.BraceFactor)}）");
                    }
                    if (x.Cleanse)
                    {
                        // 挣脱（Q80）：清减速/束缚/标记（现在主角身上只有减速会出现）并后移一格；4 号位原地清
                        var (had, to) = StruggleOutcome(b, x);
                        if (had) { b.PlayerSlowPending = false; b.PlayerSlowActive = false; }
                        parts.Add(had ? "挣脱了束缚（减速解除）" : "没有束缚");
                        if (to > 0)
                        {
                            var from = PlayerPos(b);
                            var mate = AllyAt(b, to);
                            MoveAlly(b, from, to);
                            parts.Add($"后移到{PosName(to)}{(mate != null ? "（与同伴交换）" : "")}");
                        }
                        else parts.Add("已在4号位，原地挣脱");
                    }
                    break;
                case "ally":
                {
                    var effective = false;
                    var caster = b.FocusId;
                    var target = AllyTargetOf(b, c, x) ?? caster;
                    var key = CommandKey(n.Id, caster, target);
                    if (target != caster) parts.Add($"目标{ActorDisplayName(s, target)}");
                    if (x.Protect)
                    {
                        // 保护/掩护（B9a）：接下目标下一次单体直接攻击，到施术者下次正常激活开始；掩护另给双方下一次直接伤害×0.8（buffRounds 轮）
                        b.Protections[caster] = target;
                        parts.Add($"护住{ActorDisplayName(s, target)}（接下它下一次单体直接攻击，到{Who(s)}下次正常行动前）");
                        if (x.TeamGuardFactor > 0)
                            foreach (var o in new[] { caster, target })
                            {
                                var of = b.Fighter(o);
                                of.CoverFactor = Math.Min(of.CoverUntilRound >= b.Round ? of.CoverFactor : 1, x.TeamGuardFactor);
                                of.CoverUntilRound = Math.Max(of.CoverUntilRound, b.Round + x.BuffRounds);
                            }
                        if (x.TeamGuardFactor > 0) parts.Add($"双方下一次直接伤害×{Fmt.T(x.TeamGuardFactor)}（到第{b.Round + x.BuffRounds}轮末）");
                        effective = true;
                    }
                    if (x.ApBonus > 0)
                    {
                        var tf = b.Fighter(target);
                        tf.ApBonusNext = Math.Max(tf.ApBonusNext, x.ApBonus);
                        if (!b.PendingCommandXp.Contains(key)) b.PendingCommandXp.Add(key);
                        parts.Add($"{ActorDisplayName(s, target)}下一次正常行动AP+{x.ApBonus}");
                        effective = true;
                    }
                    var calmed = false;
                    using (FocusOn(s, target))
                    {
                        if (x.InspireAccuracy > 0)
                        {
                            b.InspireBonus = x.InspireAccuracy;
                            b.InspireUntilRound = b.Round + x.BuffRounds + (int)w.Trait("commandRounds");
                            if (!b.PendingCommandXp.Contains(key)) b.PendingCommandXp.Add(key);
                            parts.Add($"下一次攻击命中能力+{Fmt.T(b.InspireBonus)}（到第{b.InspireUntilRound}轮末）");
                            effective = true;
                        }
                        if (x.PanicReduce > 0)
                        {
                            var before = b.Panic;
                            b.Panic = Math.Max(Config.Panic.Range[0], b.Panic - x.PanicReduce);
                            b.CalmRound = b.Round;
                            parts.Add($"恐慌{Fmt.N(before)}→{Fmt.N(b.Panic)}");
                            effective = b.Panic < before - 1e-9;
                            calmed = effective;
                        }
                        if (x.AllOutFactor > 0)
                        {
                            b.AllOutFactor = x.AllOutFactor;
                            b.AllOutUntilRound = b.Round + x.BuffRounds + (int)w.Trait("commandRounds");
                            if (!b.PendingCommandXp.Contains(key)) b.PendingCommandXp.Add(key);
                            parts.Add($"下一次攻击伤害×{Fmt.T(b.AllOutFactor)}（到第{b.AllOutUntilRound}轮末）");
                            effective = true;
                        }
                        var first = w.Trait("firstCommandAccuracy");
                        if (first > 0 && !b.FirstCommandUsed && !x.Protect)
                        {
                            b.FirstCommandUsed = true;
                            b.InspireBonus += first;
                            b.InspireUntilRound = Math.Max(b.InspireUntilRound, b.Round + Math.Max(1, x.BuffRounds));
                            parts.Add($"预判：下一击命中能力再+{Fmt.T(first)}");
                        }
                    }
                    if (calmed)
                    {
                        if (key == n.Id) GrantCommandXp(s, b, n.Id);
                        else GrantSupportXp(s, b, n.Id, caster, target, "command");
                    }
                    if (effective && wi?.Durability != null) parts.Add(Wear(wi, 1));
                    break;
                }
                case "medical":
                {
                    // Q75：扣施术者本人随身物资，按目标本人的健康/出血/状态与回血冷却结算（PT10：冷却记在目标身上，与绷带共享）；经验给施术者
                    foreach (var kv in x.Items) TakeCarriedFresh(s, kv.Key, kv.Value);
                    var effective = false;
                    var target = AllyTargetOf(b, c, x) ?? b.FocusId;
                    var lv = SkillLevel(s, Medicine);
                    if (target != b.FocusId) parts.Add($"目标{ActorDisplayName(s, target)}");
                    using (FocusOn(s, target))
                    {
                        if (x.UseBandage && BandageItem?.Use is ItemUseConfig bu) effective = ApplyMedicine(s, bu, parts, lv, false);
                        else
                        {
                            var (bleed, heal, status, _) = MedicalOutcome(s, b, n, x, w, lv);
                            if (bleed) { s.Body.BleedTriggers = 0; s.Body.BleedDamage = 0; parts.Add("已止血"); }
                            if (heal > 0)
                            {
                                s.Stats.Add(StatId.Health, heal);
                                s.BandageReadyAt = s.GameMinute + Config.ItemActions.BandageCooldownMinutes;
                                parts.Add("健康" + Fmt.Delta(heal));
                            }
                            if (status)
                            {
                                if (PlayerWeakActive(b)) { b.PlayerWeakFactor = 1; b.PlayerWeakUntilRound = 0; parts.Add("解除虚弱"); }
                                else { b.PlayerStagger = false; parts.Add("解除失衡"); }
                            }
                            effective = bleed || heal > 0 || status;
                        }
                    }
                    if (effective)
                    {
                        parts.Add(XpText(s, Medicine, Config.Xp.Medical));
                        GainXp(s, Medicine, Config.Xp.Medical);
                    }
                    if (effective)
                    {
                        if (wi?.Durability != null) parts.Add(Wear(wi, 1));
                        var post = w.Trait("postMedicalDamage");
                        if (post > 0) { b.PlayerNextDamageFactor = post; parts.Add($"下一次受到的直接伤害×{Fmt.T(post)}"); }
                    }
                    parts.Insert(0, "消耗" + string.Join("、", x.Items.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}")));
                    break;
                }
                case "reload":
                {
                    var need = w.MagazineCapacity - wi!.LoadedAmmo;
                    var took = TakeCarried(s, w.AmmoId!, need);
                    wi.LoadedAmmo += took;
                    wi.LoadedAmmoId = w.AmmoId;
                    parts.Add($"装填{ItemName(w.AmmoId!)}{took}发（弹仓{wi.LoadedAmmo}/{w.MagazineCapacity}）");
                    break;
                }
            }
            if (energy > 0) parts.Add($"体力−{Fmt.N(energy)}");
            BattleLog(s, $"{Who(s)}{n.Name}（{w.Name}）：{string.Join("，", parts)}。");
        }

        /// <summary>
        /// 挣脱的效果（预览、判定、执行同源）：能否清掉可解状态（合同 cleanseStatuses：减速/束缚/标记；主角目前只会有减速），
        /// 后移的目的槽（4 号位为 0：原地清）。两样都没有即“整条无收益”。
        /// </summary>
        private (bool cleanse, int to) StruggleOutcome(BattleState b, SkillExecConfig x)
        {
            var slow = (x.CleanseStatuses.Count == 0 || x.CleanseStatuses.Contains("slow")) && (b.PlayerSlowPending || b.PlayerSlowActive);
            var me = PlayerPos(b);
            var to = x.MoveBack > 0 && me + x.MoveBack <= Config.Scope.Slots.Count ? me + x.MoveBack : 0;
            return (slow, to);
        }

        /// <summary>从随身取用未变质的物资（按先到期），返回实际取到的数量。</summary>
        private int TakeCarriedFresh(GameState s, string itemId, int count)
        {
            var left = count;
            foreach (var st in s.Items.Where(i => CarriedByMe(s, i) && i.ItemId == itemId && i.ReservedBy == null && !IsSpoiled(s, i))
                         .OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).ToList())
            {
                if (left <= 0) break;
                var k = Math.Min(left, st.Count);
                ConsumeCount(s, st, k);
                left -= k;
            }
            return count - left;
        }

        private void ConsumeCount(GameState s, ItemInstance it, int k)
        {
            it.Count -= k;
            if (it.Count <= 0) s.Items.Remove(it);
        }

        // ───────────── 反应攻击：反击、迎击 ─────────────

        /// <summary>
        /// 反应攻击此刻能否出手（Q82：来源页武器按当前真实己位/射程重验）：耐久、使用位、目标位。不能出手为原因，否则 null。
        /// 不能出手时不扣反应次数（无合法反应目标不白扣），架势按原期限继续。
        /// </summary>
        private string? ReactionBlocked(GameState s, BattleState b, int page, BattleEnemy t)
        {
            var it = WeaponOnPage(s, page, out var w);
            if (it != null && !HasWear(it, Config.Wear.PerExecutedAttackCommand)) return $"{w.Name}耐久不足";
            if (!OwnPositionsOf(w).Contains(PlayerPos(b))) return $"{w.Name}要在我方{PosList(OwnPositionsOf(w))}（{Who(s)}在{PosName(PlayerPos(b))}）";
            if (!TargetPositionsOf(w).Contains(t.Pos)) return $"够不到{EnemyName(t)}（{PosName(t.Pos)}）";
            return null;
        }

        /// <summary>
        /// 一次反应攻击（反击/迎击；R03 §4“反应动作不再触发反应链”）：用架势/架矛时那一页的武器，单段、独立判命中暴击；
        /// 不扣体力、弹药与耐久（Q82），不吃也不消耗鼓舞/总攻令/斧蓄力；静态装备特性与教学平加照常；命中计攻击经验。调用前已过 ReactionBlocked。
        /// </summary>
        private void ReactionStrike(GameState s, BattleState b, int page, double factor, BattleEnemy t, string label)
        {
            WeaponOnPage(s, page, out var w);
            var sp = new AttackSpec { Action = label, Weapon = w, Page = page, DamageFactor = factor, Reaction = true };
            var cfg = _enemies[t.EnemyId];
            var pv = TargetCalc(s, b, sp, t, s.Stats.Energy);
            if (s.Rng.NextDouble() >= pv.HitChance) { BattleLog(s, $"{Who(s)}{label}{cfg.Name}未命中（{Fmt.Pct(pv.HitChance)}）。"); return; }
            var crit = s.Rng.NextDouble() < pv.CritChance;
            var raw = w.DamageMin + s.Rng.NextDouble() * (w.DamageMax - w.DamageMin);
            var multiplied = raw * pv.Multiplier * (crit ? Config.CombatMath.CritFactor : 1);
            var armor = Math.Max(0, pv.Armor - pv.Penetration);
            var guarded = pv.Guarded && Math.Max(0, multiplied + pv.Flat - armor) > 0;
            var dmg = AttackDamage(multiplied, pv.Flat, armor, guarded, Config.CombatMath.GuardFactor);
            if (guarded) t.Guard = false;
            var text = dmg <= 0 ? "被护甲完全挡住" : $"{(crit ? "暴击" : "命中")}−{Fmt.N(dmg)}";
            if (dmg > 0) t.Hp -= dmg;
            if (t.Hp <= 1e-9) { KillEnemy(s, t); text += $"，{cfg.Name}倒下"; }
            else CheckFlee(s, t);
            var xp = AttackXp(s, w.SkillId, new[] { t });
            BattleLog(s, $"{Who(s)}{label}（{w.Name}）：{cfg.Name}{text}{xp}。");
            if (PlayerDownAfterSplash(s) || s.Battle == null) return;
            CheckVictory(s);
        }

        /// <summary>反击架势：受到直接攻击命中（含护甲挡成 0）后反击 1 次（主角存活、攻击者存活、来源页此刻合法）。</summary>
        private void TryCounter(GameState s, BattleState b, BattleEnemy attacker)
        {
            if (b.CounterFactor <= 0 || s.Progress.Dead || !attacker.Alive) return;
            if (ReactionBlocked(s, b, b.CounterPage, attacker) is string why) { BattleLog(s, $"{Who(s)}的反击没有出手：{why}（架势保留）。"); return; }
            var f = b.CounterFactor;
            b.CounterFactor = 0;
            ReactionStrike(s, b, b.CounterPage, f, attacker, "反击");
        }

        /// <summary>架矛：敌人主动前移后迎击（每次合法迎击消耗 1 次；够不到不扣次数；次数用完或下次正常激活开始清除）。</summary>
        private void TryBrace(GameState s, BattleState b, BattleEnemy mover)
        {
            // B9a（B8d-1 多人口径）：在场每名架着矛的我方各自按自己的来源页、号位、射程、耐久与剩余次数重验，合法就各迎击一次（号位从前到后）
            foreach (var id in AlliesOnField(b))
            {
                if (s.Battle == null || s.Progress.Dead || !mover.Alive) return;
                if (b.Fighter(id).BraceCharges <= 0) continue;
                using (FocusOn(s, id))
                {
                    if (ReactionBlocked(s, b, b.BracePage, mover) is string why) { BattleLog(s, $"{Who(s)}的迎击没有出手：{why}（不扣次数）。"); continue; }
                    b.BraceCharges--;
                    ReactionStrike(s, b, b.BracePage, b.BraceFactor, mover, "迎击");
                }
            }
        }
    }
}

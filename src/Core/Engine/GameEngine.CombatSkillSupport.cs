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
            if (InspireActive(b)) { b.InspireBonus = 0; GrantCommandXp(s, b, "command_01"); }
            if (AllOutActive(b)) { b.AllOutFactor = 0; GrantCommandXp(s, b, "command_10"); }
            if (AxeChargeActive(s, b, sp)) b.ChargeFactor = 0;
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
                if (s.Items.Where(i => i.IsCarried && i.ItemId == kv.Key && i.ReservedBy == null && !IsSpoiled(s, i)).Sum(i => i.Count) < kv.Value)
                    return $"随身没有{ItemName(kv.Key)}" + (kv.Value > 1 ? $"×{kv.Value}" : "");
            return null;
        }

        /// <summary>医疗技能的效果判定（与执行同源）：能止血、能回血（共享绷带 60 分钟回血冷却）、能解除失衡/虚弱。</summary>
        private (bool bleed, double heal, bool status, string? why) MedicalOutcome(GameState s, BattleState? b, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w)
        {
            var bleed = x.StopBleed && s.Body.BleedTriggers > 0;
            double heal = 0;
            string? why = null;
            if (x.Heal > 0)
            {
                var ready = s.BandageReadyAt <= s.GameMinute;
                var low = !x.HealMaxRatio.HasValue || s.Stats.Health <= s.Stats.HealthMax * x.HealMaxRatio.Value + 1e-9;
                var amount = MedicalHeal(x.Heal, SkillLevel(s, Medicine)) + w.Trait("medicalHealBonus") + (x.HealMaxRatio.HasValue ? w.Trait("criticalCareBonus") : 0);
                if (!low) why = $"健康高于上限的{Fmt.Pct(x.HealMaxRatio!.Value)}，不需要{n.Name}";
                else if (!ready) why = $"回血冷却中（与绷带共享），还需{s.BandageReadyAt - s.GameMinute}分钟";
                else if (s.Stats.Health >= s.Stats.HealthMax - 1e-9) why = "健康已满";
                else heal = Math.Min(amount, s.Stats.HealthMax - s.Stats.Health);
            }
            var status = x.RemoveWeakOrStagger && b != null && (PlayerWeakActive(b) || b.PlayerStagger);
            if (x.RemoveWeakOrStagger && !status) why = "你没有失衡或虚弱";
            return (bleed, heal, status, bleed || heal > 0 || status ? null : why ?? "没有可处理的伤势");
        }

        /// <summary>非攻击技能能否执行（战斗中；CommandBlocked 的分支，已过解锁/冷却/AP/体力/使用位）。</summary>
        private string? NonAttackBlocked(GameState s, BattleState b, BattleCommand c, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi)
        {
            if (x.NeedsAllies && AllyCount(b) <= 1) return NeedAlliesReason(n);
            switch (x.Type)
            {
                case "guard":
                    if (b.PlayerGuard) return "已在防御（不叠层）";
                    if (x.CounterFactor > 0 && b.CounterFactor > 0) return "已在反击架势";
                    return null;
                case "buff":
                    if (x.ChargeFactor > 0 && AxeChargeOn(b)) return "已蓄力（不叠）";
                    if (x.BraceCharges > 0 && b.BraceCharges > 0) return "已架矛（不叠）";
                    if (x.Cleanse && !b.PlayerSlowPending && !b.PlayerSlowActive && b.Allies.Count <= 1) return "你没有被扯腿/拉扯等束缚，只剩你一人也无法后移";
                    return null;
                case "ally":
                    if (x.InspireAccuracy > 0 && InspireActive(b) && b.InspireBonus >= x.InspireAccuracy) return "已有鼓舞（同名不叠）";
                    if (x.AllOutFactor > 0 && AllOutActive(b)) return "已有总攻令（同名不叠）";
                    if (x.PanicReduce > 0)
                    {
                        if (b.CalmRound == b.Round) return "本回合已对你用过稳心（同目标每大回合一次）";
                        if (b.Panic <= 1e-9) return "恐慌已是0，没有效果";
                    }
                    return null;
                case "medical":
                {
                    if (MissingMedicalItem(s, x) is string miss) return miss + "（地面、营区的不能在战斗中用）";
                    if (x.UseBandage)
                    {
                        var use = BandageItem?.Use;
                        return use == null ? "不能使用" : MedicineBlocked(s, use);
                    }
                    return MedicalOutcome(s, b, n, x, w).why;
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
                    if (x.DisplaceResist > 0) p.Notes.Add($"位移抵抗+{Fmt.T(x.DisplaceResist)}个百分点，到第{b.Round + x.BuffRounds}轮末（只剩你一人时没有可被移动的位置）");
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
                    if (x.Cleanse) p.Notes.Add(solo ? "解除扯腿/拉扯造成的减速；只剩你一人，不能后移" : "解除束缚/标记并后移1位");
                    break;
                case "ally":
                    if (x.NeedsAllies) p.Notes.Add(NeedAlliesReason(n));
                    else
                    {
                        p.Notes.Add(solo ? "目标：你自己（我方只有你一人）" : "目标：任一友方");
                        if (x.InspireAccuracy > 0) p.Notes.Add($"下一次攻击命中能力+{Fmt.T(x.InspireAccuracy)}，到第{b.Round + x.BuffRounds + (int)w.Trait("commandRounds")}轮末");
                        if (x.PanicReduce > 0) p.Notes.Add($"恐慌{Fmt.N(b.Panic)}→{Fmt.N(Math.Max(Config.Panic.Range[0], b.Panic - x.PanicReduce))}；同目标每大回合一次");
                        if (x.AllOutFactor > 0) p.Notes.Add($"下一次攻击伤害×{Fmt.T(x.AllOutFactor)}，到第{b.Round + x.BuffRounds + (int)w.Trait("commandRounds")}轮末；同名不叠");
                        if (w.Trait("firstCommandAccuracy") > 0 && !b.FirstCommandUsed) p.Notes.Add($"{w.Name}“预判”：本场首次号令，另加下一击命中能力+{Fmt.T(w.Trait("firstCommandAccuracy"))}");
                        p.Notes.Add($"{w.Name}耐久−1（有效时）；" + (SkillMaxed(s, "command") ? XpText(s, "command", 0) : $"增益被实际用掉才得指挥经验+{Config.Xp.DamagingAttackCommand}（每场每技能一次）"));
                    }
                    break;
                case "medical":
                {
                    p.Notes.Add($"消耗随身{string.Join("、", x.Items.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"))}；目标：你自己（我方只有你一人）");
                    if (x.UseBandage && BandageItem?.Use is ItemUseConfig bu && MedicineEffectText(s, bu) is string bt) { p.EffectText = bt; p.Notes.Insert(0, bt); }
                    else
                    {
                        var (bleed, heal, status, why) = MedicalOutcome(s, b, n, x, w);
                        var fx = new List<string>();
                        if (bleed) fx.Add("止血");
                        if (heal > 0) fx.Add($"健康{Fmt.N(s.Stats.Health)}→{Fmt.N(s.Stats.Health + heal)}（共享绷带60分钟回血冷却）");
                        if (status) fx.Add(PlayerWeakActive(b) ? "解除虚弱" : "解除失衡");
                        p.EffectText = fx.Count > 0 ? string.Join("；", fx) : why;
                        if (p.EffectText != null) p.Notes.Insert(0, p.EffectText);
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
                        var had = b.PlayerSlowPending || b.PlayerSlowActive;
                        b.PlayerSlowPending = false;
                        b.PlayerSlowActive = false;
                        parts.Add(had ? "挣脱了束缚（减速解除）" : "没有束缚");
                        parts.Add(solo ? "只剩你一人，不后移" : "后移需队友换位（后续版本）");
                    }
                    break;
                case "ally":
                {
                    var effective = false;
                    if (x.InspireAccuracy > 0)
                    {
                        b.InspireBonus = x.InspireAccuracy;
                        b.InspireUntilRound = b.Round + x.BuffRounds + (int)w.Trait("commandRounds");
                        if (!b.PendingCommandXp.Contains(n.Id)) b.PendingCommandXp.Add(n.Id);
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
                        if (effective) GrantCommandXp(s, b, n.Id);
                    }
                    if (x.AllOutFactor > 0)
                    {
                        b.AllOutFactor = x.AllOutFactor;
                        b.AllOutUntilRound = b.Round + x.BuffRounds + (int)w.Trait("commandRounds");
                        if (!b.PendingCommandXp.Contains(n.Id)) b.PendingCommandXp.Add(n.Id);
                        parts.Add($"下一次攻击伤害×{Fmt.T(b.AllOutFactor)}（到第{b.AllOutUntilRound}轮末）");
                        effective = true;
                    }
                    var first = w.Trait("firstCommandAccuracy");
                    if (first > 0 && !b.FirstCommandUsed)
                    {
                        b.FirstCommandUsed = true;
                        b.InspireBonus += first;
                        b.InspireUntilRound = Math.Max(b.InspireUntilRound, b.Round + Math.Max(1, x.BuffRounds));
                        parts.Add($"预判：下一击命中能力再+{Fmt.T(first)}");
                    }
                    if (effective && wi?.Durability != null) parts.Add(Wear(wi, 1));
                    break;
                }
                case "medical":
                {
                    foreach (var kv in x.Items) TakeCarriedFresh(s, kv.Key, kv.Value);
                    var effective = false;
                    if (x.UseBandage && BandageItem?.Use is ItemUseConfig bu) effective = ApplyMedicine(s, bu, parts);
                    else
                    {
                        var (bleed, heal, status, _) = MedicalOutcome(s, b, n, x, w);
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
                        if (effective)
                        {
                            parts.Add(XpText(s, Medicine, Config.Xp.Medical));
                            GainXp(s, Medicine, Config.Xp.Medical);
                        }
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
            BattleLog(s, $"你{n.Name}（{w.Name}）：{string.Join("，", parts)}。");
        }

        /// <summary>从随身取用未变质的物资（按先到期），返回实际取到的数量。</summary>
        private int TakeCarriedFresh(GameState s, string itemId, int count)
        {
            var left = count;
            foreach (var st in s.Items.Where(i => i.IsCarried && i.ItemId == itemId && i.ReservedBy == null && !IsSpoiled(s, i))
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
        /// 一次反应攻击（反击/迎击；R03 §4“反应动作不再触发反应链”）：用架势/架矛时那一页的武器，单段、独立判命中暴击；
        /// 不扣体力、弹药与耐久（A06 只写主动攻击的成本），不吃鼓舞/总攻令/斧蓄力；命中计攻击经验。目标须在该武器目标位内。
        /// </summary>
        private void ReactionStrike(GameState s, BattleState b, int page, double factor, BattleEnemy t, string label)
        {
            var it = WeaponOnPage(s, page, out var w);
            if (it != null && !HasWear(it, Config.Wear.PerExecutedAttackCommand)) { BattleLog(s, $"你的{label}没有出手：{w.Name}耐久不足。"); return; }
            if (!TargetPositionsOf(w).Contains(t.Pos)) { BattleLog(s, $"你的{label}够不到{EnemyName(t)}（{PosName(t.Pos)}）。"); return; }
            var sp = new AttackSpec { Action = label, Weapon = w, Page = page, DamageFactor = factor, Reaction = true };
            var cfg = _enemies[t.EnemyId];
            var pv = TargetCalc(s, b, sp, t, s.Stats.Energy);
            if (s.Rng.NextDouble() >= pv.HitChance) { BattleLog(s, $"你{label}{cfg.Name}未命中（{Fmt.Pct(pv.HitChance)}）。"); return; }
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
            BattleLog(s, $"你{label}（{w.Name}）：{cfg.Name}{text}{xp}。");
            if (s.Stats.Health <= 0) { BattleDeath(s, s.Progress.LastDamageCause ?? "喷溅"); return; }
            CheckVictory(s);
        }

        /// <summary>反击架势：受到直接攻击命中后反击 1 次（主角存活、攻击者存活）。</summary>
        private void TryCounter(GameState s, BattleState b, BattleEnemy attacker)
        {
            if (b.CounterFactor <= 0 || s.Progress.Dead || !attacker.Alive) return;
            var f = b.CounterFactor;
            b.CounterFactor = 0;
            ReactionStrike(s, b, b.CounterPage, f, attacker, "反击");
        }

        /// <summary>架矛：敌人主动前移后迎击（每次前移消耗 1 次，次数用完或下次正常激活开始清除）。</summary>
        private void TryBrace(GameState s, BattleState b, BattleEnemy mover)
        {
            if (b.BraceCharges <= 0 || s.Progress.Dead || !mover.Alive) return;
            b.BraceCharges--;
            ReactionStrike(s, b, b.BracePage, b.BraceFactor, mover, "迎击");
        }
    }
}

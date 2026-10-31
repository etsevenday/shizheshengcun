using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 非攻击技能（格挡、自身增益、指挥、医疗、装填）与攻击增益的状态判定（A06 §2、R03 §3/§4；开发计划 v0.9 B7 R25）。
    /// </summary>
    public sealed partial class GameEngine
    {
        public static bool InspireActive(BattleState b) => b.InspireBonus > 0 && b.InspireUntilRound >= b.Round;
        public static bool AllOutActive(BattleState b) => b.AllOutFactor > 1 && b.AllOutUntilRound >= b.Round;
        public static bool PlayerWeakActive(BattleState b) => b.PlayerWeakFactor < 1 && b.PlayerWeakUntilRound >= b.Round;

        /// <summary>斧蓄力只给“下次该斧攻击”：同一把斧、未过期。</summary>
        private bool AxeChargeActive(GameState s, BattleState b, AttackSpec sp)
        {
            if (b.ChargeFactor <= 1 || b.ChargeUntilRound < b.Round || sp.Desperate) return false;
            var it = WeaponOnPage(s, sp.Page, out _);
            return it != null && it.InstanceId == b.ChargeItem;
        }

        /// <summary>攻击结算后用掉“下一次攻击”的增益（鼓舞、总攻令、斧蓄力）；增益被实际用掉才发指挥经验（A06 §3）。</summary>
        private void ConsumeAttackBuffs(GameState s, BattleState b, AttackSpec sp)
        {
            if (sp.Reaction || sp.Desperate) return;
        }

        /// <summary>正在保护 t 的敌人（存活、不是它自己）。</summary>
        private static BattleEnemy? ProtectorOf(BattleState b, BattleEnemy t) =>
            b.Alive.FirstOrDefault(e => e.ProtectingIndex == t.Index && e.Index != t.Index);

        /// <summary>把敌人本轮意图改为普通攻击（被激怒的雄鹿、蓄力被打断）。</summary>
        private void RelockAsAttack(GameState s, BattleEnemy t)
        {
            t.Intent = "attack";
            t.IntentLabel = IntentLabel(t);
        }

        // ───────────── 非攻击技能 ─────────────

        private string? NonAttackBlocked(GameState s, BattleState b, BattleCommand c, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi) =>
            "这个技能在下一里程碑接入";

        private string? NonAttackStaticReason(GameState s, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi) =>
            "这个技能在下一里程碑接入";

        private void NonAttackPreview(GameState s, BattleState b, BattleCommand c, BattlePreview p, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi)
        {
            p.Notes.Add($"{n.Name}：{n.Text}");
        }

        private void ExecuteNonAttack(GameState s, BattleState b, BattleCommand c, SkillNodeConfig n, SkillExecConfig x, WeaponConfig w, ItemInstance? wi)
        {
        }
    }
}

using System.Collections.Generic;

namespace Survival.Core.Engine
{
    /// <summary>一条战斗指令（v0.5 §06、v0.6 §05）：动作 ID（basic_attack、cleave、guard、reload、retreat……）、目标与物品。</summary>
    public sealed class BattleCommand
    {
        public string Action { get; set; } = "";
        /// <summary>目标敌人的本场序号（BattleEnemy.Index）；不需要目标为 null。</summary>
        public int? Target { get; set; }
        /// <summary>切换武器页（switch_weapon）的目标页号 1/2；用药的物品实例（为空时取随身最早一份）。</summary>
        public int? ItemInstanceId { get; set; }
        public string? RequestId { get; set; }

        public static BattleCommand Of(string action, int? target = null, int? item = null) =>
            new BattleCommand { Action = action, Target = target, ItemInstanceId = item };

        public BattleCommand WithRequestId(string? id)
        {
            var c = (BattleCommand)MemberwiseClone();
            c.RequestId = id;
            return c;
        }
    }

    /// <summary>Dialogue：有未读对话，下一条战斗动作不开始（不扣 AP、不执行敌行动、不推进轮末 tick；R03 §7.1）。</summary>
    public enum BattleStepKind { None, AwaitPlayer, PlayerTurnStarted, EnemyActed, RoundEnded, Ended, Dialogue }

    public sealed class BattleStepResult
    {
        public BattleStepKind Kind { get; set; }
        public string Text { get; set; } = "";
    }

    /// <summary>战斗指令按钮（界面列出全部指令，不可用的写原因；不保留瞄准空位）。</summary>
    public sealed class BattleCommandOption
    {
        public string Action { get; set; } = "";
        public string Label { get; set; } = "";
        /// <summary>attack / move / general / item。</summary>
        public string Group { get; set; } = "";
        public int Ap { get; set; }
        public double Energy { get; set; }
        public bool NeedsTarget { get; set; }
        public bool Available { get; set; }
        public string? Reason { get; set; }
        /// <summary>用药的物品实例。</summary>
        public int? ItemInstanceId { get; set; }
        /// <summary>该指令当前可以选的目标序号。</summary>
        public List<int> Targets { get; set; } = new List<int>();
        /// <summary>single 点一个目标 / pair 点一个目标打相邻双位 / area 点任一亮起目标打全部 / self 无目标就近确认（B7）。</summary>
        public string TargetMode { get; set; } = "single";
        /// <summary>技能节点 ID（A06 §2）；通用指令为 null。</summary>
        public string? NodeId { get; set; }
        public int CooldownRounds { get; set; }
        /// <summary>A06 效果文字（界面悬停）。</summary>
        public string? EffectText { get; set; }
    }

    /// <summary>对一个目标的预测：命中能力值与实际命中率分开写（命中能力27 不是 27%）。</summary>
    public sealed class BattleTargetPreview
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Slot { get; set; } = "";
        public double AccuracyRating { get; set; }
        /// <summary>命中能力的构成（试玩修复 B7c-1）：“武器26＋熟练3＋基础8＋装备3 ×低体力×0.95（只算一次）”，与 AccuracyRating 同一次计算。</summary>
        public string AccuracyText { get; set; } = "";
        public double Evasion { get; set; }
        public double HitChance { get; set; }
        public double CritRating { get; set; }
        public double CritChance { get; set; }
        /// <summary>命中未暴击时的伤害区间（已乘技能、招式、站位，已扣护甲与对方防御）。</summary>
        public double DamageMin { get; set; }
        public double DamageMax { get; set; }
        /// <summary>暴击时的伤害区间。</summary>
        public double CritDamageMin { get; set; }
        public double CritDamageMax { get; set; }
        public double Armor { get; set; }
        /// <summary>穿透（装备“忽略护甲”词条 + 武器固定特性），扣甲时 max(0, 护甲−穿透)。</summary>
        public double Penetration { get; set; }
        /// <summary>伤害增益（装备“直接伤害乘数”合计，乘算总上限×1.5；不含技能/熟练/暴击）。</summary>
        public double DamageBuff { get; set; } = 1;
        /// <summary>递减前的合法正增益乘积 rawB（Q82）；DamageBuff 为递减后的 B。</summary>
        public double RawDamageBuff { get; set; } = 1;
        /// <summary>未暴击时的总乘数：熟练(1+0.035L)×招式×伤害增益（结算与预览同源）。</summary>
        public double Multiplier { get; set; } = 1;
        /// <summary>平加攻击力（R07 词条）。</summary>
        public double Flat { get; set; }
        public bool Guarded { get; set; }
        public bool RearPenalty { get; set; }
        public double Hp { get; set; }
    }

    /// <summary>战斗指令预览：只读，不推进随机、不改状态（v0.5 B503）。</summary>
    public sealed class BattlePreview
    {
        public bool Ok { get; set; }
        public string Reason { get; set; } = "";
        public string Title { get; set; } = "";
        public int Ap { get; set; }
        public double EnergyCost { get; set; }
        public double EnergyBefore { get; set; }
        public double EnergyAfter { get; set; }
        public List<BattleTargetPreview> Targets { get; set; } = new List<BattleTargetPreview>();
        public double? RetreatChance { get; set; }
        public string? AmmoText { get; set; }
        public string? WearText { get; set; }
        public double NoiseBefore { get; set; }
        public double NoiseAfter { get; set; }
        public List<string> Notes { get; set; } = new List<string>();
        /// <summary>用药的效果数值（“健康3.00→4.00，止血”“病毒12.00→7.00”等，与执行结算同源）；其他指令为 null。</summary>
        public string? EffectText { get; set; }
        public string ConfirmText { get; set; } = "";
    }
}

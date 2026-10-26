using System.Collections.Generic;

namespace Survival.Core.Engine
{
    /// <summary>一条战斗指令（v0.5 §06、v0.6 §05）：动作 ID（basic_attack、cleave、guard、reload、retreat……）、目标与物品。</summary>
    public sealed class BattleCommand
    {
        public string Action { get; set; } = "";
        /// <summary>目标敌人的本场序号（BattleEnemy.Index）；不需要目标为 null。</summary>
        public int? Target { get; set; }
        /// <summary>换武器的目标实例（0 = 徒手）；用药的物品实例（为空时取随身最早一份）。</summary>
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

    public enum BattleStepKind { None, AwaitPlayer, PlayerTurnStarted, EnemyActed, RoundEnded, Ended }

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
        /// <summary>换武器/用药的物品实例（每把随身武器、每种药各一条）。</summary>
        public int? ItemInstanceId { get; set; }
        /// <summary>该指令当前可以选的目标序号。</summary>
        public List<int> Targets { get; set; } = new List<int>();
    }

    /// <summary>对一个目标的预测：命中能力值与实际命中率分开写（命中能力27 不是 27%）。</summary>
    public sealed class BattleTargetPreview
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
        public string Slot { get; set; } = "";
        public double AccuracyRating { get; set; }
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
        public string ConfirmText { get; set; } = "";
    }
}

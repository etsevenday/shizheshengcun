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
        /// <summary>
        /// 下指令的我方成员（B9a，R113；BattleCommand.ActorId）：必须是正在激活的那一名；为 null 时按当前激活者（旧调用与测试）。
        /// </summary>
        public string? ActorId { get; set; }
        /// <summary>以友方为目标的指挥/医疗/保护技能的目标 actorId（B9a）；为 null 时以自己为目标。</summary>
        public string? AllyTarget { get; set; }
        /// <summary>整队（command_03，Q105）的第二名友方 B（须与 AllyTarget 真实相邻）；其他技能为 null。</summary>
        public string? AllyTarget2 { get; set; }
        /// <summary>
        /// 整队选目标时看到的 A/B 号位快照（Q105：提交时按稳定 actorId 与预览槽重验，任一变化整条拒绝、不自动改选）；null 为不校验快照。
        /// </summary>
        public int? AllyTargetPos { get; set; }
        public int? AllyTarget2Pos { get; set; }

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
        /// <summary>以友方为目标的技能（TargetMode "ally"，B9a）当前可以选的我方 actorId（按号位）。</summary>
        public List<string> AllyTargets { get; set; } = new List<string>();
        /// <summary>single 点一个目标 / pair 点一个目标打相邻双位 / area 点任一亮起目标打全部 / self 无目标就近确认（B7）/ ally 点我方一名成员（B9a，多人时的指挥/医疗/保护）/ ally_pair 先点友方 A 再点与 A 相邻的友方 B，确认后提交（整队，Q105）。</summary>
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
        /// <summary>保护重定向（B9，A10 §4.1）：玩家瞄准的是另一名敌人（本场序号），这一击实际由本目标承受；没有重定向为 null。</summary>
        public int? AimedIndex { get; set; }
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

    /// <summary>B34b（R03 §Q571）：特写的动作表现。七类里 miss／guard／defeat 由结果决定（见 <see cref="BattleFxEvent.Look"/>），这里是出手方式。</summary>
    public static class BattleFxMotion
    {
        public const string Melee = "melee";
        public const string Ranged = "ranged";
        public const string Area = "area";
        public const string Heal = "heal";
        /// <summary>短反馈：只在行动者身上，不切特写、不进播放队列。</summary>
        public const string Short = "short";
    }

    /// <summary>出手事件里的一名角色（行动者或承受者）的只读快照：阵营、稳定编号（我方 actorId、敌方 "e{本场序号}"）、名字、敌人配置 ID。</summary>
    public sealed class BattleFxUnit
    {
        public const string AllySide = "ally";
        public const string EnemySide = "enemy";
        public string Side { get; set; } = AllySide;
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string? EnemyId { get; set; }
        public bool IsAlly => Side == AllySide;
    }

    /// <summary>一段真实结果：Outcome 为 hit／crit／miss／blocked（护甲完全挡住）／effect（命中但无伤或无数值的效果）／heal。</summary>
    public sealed class BattleFxResult
    {
        public BattleFxUnit Target { get; set; } = new BattleFxUnit();
        public string Outcome { get; set; } = "";
        /// <summary>实际变化值（伤害、治疗）；无数值时为 0。</summary>
        public double Amount { get; set; }
        /// <summary>被格挡／防御减伤。</summary>
        public bool Guarded { get; set; }
        /// <summary>这一段后倒地。</summary>
        public bool Down { get; set; }
    }

    /// <summary>
    /// 一次真实出手的结果快照（B34b，R03 §Q572）：场次、序号、行动者、按真实顺序的各段结果和同一次写下的战报原文。
    /// 内核结算时抄下，界面只读播放；不进存档。反击／迎击是独立事件，ParentSeq 指向触发它的出手；父出手在其后继续出的段是续段（ContinuesSeq）。
    /// </summary>
    public sealed class BattleFxEvent
    {
        public int Seq { get; set; }
        public int BattleId { get; set; }
        public string Motion { get; set; } = BattleFxMotion.Melee;
        public BattleFxUnit Actor { get; set; } = new BattleFxUnit();
        public List<BattleFxResult> Results { get; } = new List<BattleFxResult>();
        public List<string> Lines { get; } = new List<string>();
        public bool Short { get; set; }
        /// <summary>不显示数值（鼓舞提示贴等：R03 §Q571“不显示虚构治疗数值”）。</summary>
        public bool NoNumbers { get; set; }
        public int? ParentSeq { get; set; }
        public int? ContinuesSeq { get; set; }
        /// <summary>同一出手（含续段）的根序号。</summary>
        public int RootSeq => ContinuesSeq ?? Seq;

        /// <summary>
        /// 七类表现（a9b93f0 动效合同 melee/ranged/miss/heal/area/guard/defeat）：多目标为 area；有倒地为 defeat；全部未中为 miss；
        /// 有格挡为 guard；其余按出手方式（近战／远程／治疗）。
        /// </summary>
        public string Look
        {
            get
            {
                if (Motion == BattleFxMotion.Heal) return BattleFxMotion.Heal;
                var targets = new HashSet<string>();
                foreach (var r in Results) targets.Add(r.Target.Side + ":" + r.Target.Id);
                if (targets.Count > 1 || Motion == BattleFxMotion.Area) return BattleFxMotion.Area;
                var anyDown = false; var allMiss = Results.Count > 0; var anyGuard = false;
                foreach (var r in Results) { anyDown |= r.Down; allMiss &= r.Outcome == "miss"; anyGuard |= r.Guarded; }
                if (anyDown) return "defeat";
                if (allMiss) return "miss";
                if (anyGuard) return "guard";
                return Motion;
            }
        }
    }
}

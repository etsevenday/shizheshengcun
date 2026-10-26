namespace Survival.Core.State
{
    /// <summary>
    /// 一项熟练度（v0.6 §03）：等级无游戏上限，经验逐级扣 50×(L+1)。等级用 int、经验用 long，
    /// 加减一律 checked：溢出直接报错，不静默回零。
    /// </summary>
    public sealed class SkillState
    {
        public int Level { get; set; } = 1;
        public long Xp { get; set; }

        public SkillState Clone() => (SkillState)MemberwiseClone();
    }
}

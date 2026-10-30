namespace Survival.Core.State
{
    /// <summary>
    /// 一项熟练度（A06 §1，取代 v0.6 §03 的无上限）：最高 10 级，经验逐级扣 50×(L+1)。等级用 int、经验用 long，
    /// 加减一律 checked：溢出直接报错，不静默回零。
    /// </summary>
    public sealed class SkillState
    {
        public int Level { get; set; } = 1;
        public long Xp { get; set; }
        /// <summary>schema 11 迁移前超过 10 级（或 10 级仍有经验）时的原等级与经验，只作历史记录，不参与任何计算（R01 §5）。</summary>
        public int? LegacyLevel { get; set; }
        public long? LegacyXp { get; set; }

        public SkillState Clone() => (SkillState)MemberwiseClone();
    }
}

using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.State
{
    /// <summary>
    /// 收容所周期招募（R08 RC1—RC7，开发计划 v0.9 B19，需求 R60/R61；schema 31 起）：开放时刻、当前周期的 3 名候选快照、在办登记、流水号。
    /// 候选是招募资料快照，不是角色实体（不在 Actors 里、不结算身体，RC3/RC14）；登记完成才建 actorId。
    /// </summary>
    public sealed class RecruitState
    {
        /// <summary>shelterOpenedAt：QMAP01 登记后首次实际到达收容所的世界分钟；没开放为 null（旧档首次实到才起算，不补历史周期）。</summary>
        public int? ShelterOpenedAt { get; set; }
        /// <summary>当前这批候选的周期号 k = floor((worldMinute − 开放时刻) / 4320)；还没生成为 null。</summary>
        public int? Cycle { get; set; }
        /// <summary>本周期候选（生成后保存，同周期不重抽）；周期切换时未招者离开，换成新一批。</summary>
        public List<RecruitCandidate> Candidates { get; set; } = new List<RecruitCandidate>();
        /// <summary>在办登记（锁 candidateId 与 1 个名额，跨周期保留到结束）；没有为 null。</summary>
        public RecruitTxn? Pending { get; set; }
        /// <summary>候选名流水号（全局递增、不复用；开发假设 B19-2）。</summary>
        public int NextNameSeq { get; set; } = 1;
        /// <summary>招入角色的 actorId 流水号（recruit_n，不复用；退役者保留历史 actorId）。</summary>
        public int NextActorSeq { get; set; } = 1;

        public RecruitState Clone() => new RecruitState
        {
            ShelterOpenedAt = ShelterOpenedAt,
            Cycle = Cycle,
            Candidates = Candidates.Select(c => c.Clone()).ToList(),
            Pending = Pending?.Clone(),
            NextNameSeq = NextNameSeq,
            NextActorSeq = NextActorSeq,
        };
    }

    /// <summary>一名周期候选的完整快照（RC2/RC3/Q62：首次生成后固定，与姓名独立）。</summary>
    public sealed class RecruitCandidate
    {
        public string CandidateId { get; set; } = "";
        public int Cycle { get; set; }
        public string TemplateId { get; set; } = "";
        public string Name { get; set; } = "";
        public string MainSkill { get; set; } = "";
        public int MainLevel { get; set; }
        public string SubSkill { get; set; } = "";
        public int SubLevel { get; set; }
        public double Health { get; set; }
        public double HealthMax { get; set; }
        public double Energy { get; set; }
        public double EnergyMax { get; set; }
        public double Satiety { get; set; }
        public double Hydration { get; set; }
        public double Wakefulness { get; set; }
        public double Morale { get; set; }
        public double Immunity { get; set; }
        public double VirusLoad { get; set; }
        public double BaseSpeed { get; set; }
        public double ControlResist { get; set; }
        public double Evasion { get; set; }
        public double BaseArmor { get; set; }
        /// <summary>available 可招 / registering 登记中 / recruited 已招（已招的不被刷新替换，退役后同周期也不能再招）。</summary>
        public string Status { get; set; } = Available;
        /// <summary>招入后的 actorId。</summary>
        public string? ActorId { get; set; }
        public int GeneratedAt { get; set; }

        public const string Available = "available";
        public const string Registering = "registering";
        public const string Recruited = "recruited";

        public RecruitCandidate Clone() => (RecruitCandidate)MemberwiseClone();
    }

    /// <summary>在办登记（RC4/RC7）：开始时锁定候选快照与名额，10 分钟 wait 完成时主角存活才建人；requestId 只建一人。</summary>
    public sealed class RecruitTxn
    {
        public int ActionId { get; set; }
        public string? RequestId { get; set; }
        public string CandidateId { get; set; } = "";
        public int Cycle { get; set; }
        public string LocationId { get; set; } = "";
        public int StartMinute { get; set; }
        /// <summary>锁定时的候选快照（跨周期后原批已轮换，仍按它建人）。</summary>
        public RecruitCandidate Candidate { get; set; } = new RecruitCandidate();

        public RecruitTxn Clone()
        {
            var c = (RecruitTxn)MemberwiseClone();
            c.Candidate = Candidate.Clone();
            return c;
        }
    }
}

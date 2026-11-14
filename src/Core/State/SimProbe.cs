using System.Collections.Generic;

namespace Survival.Core.State
{
    /// <summary>预估副本里记下的身体危险类型（R09 Q674、Q675、Q685—Q687）。</summary>
    public enum DangerKind
    {
        /// <summary>正常者预计首次归零、进入濒死（本次不判致死）。</summary>
        Enter,
        /// <summary>已濒死，确定再受正伤（需要致死判定）。</summary>
        Certain,
        /// <summary>已濒死，仅有概率再受正伤（搜集划伤、勘整风险等未触发的概率伤）。</summary>
        Possible,
    }

    /// <summary>
    /// B47a3（R09 Q682—Q687）：只读预估副本上的危险记录。由统一受伤入口、概率伤检查点与世界段尸变检查在副本（SimCopy）上追加，
    /// 预估循环按段读取；真实状态上恒为 null，不进存档。
    /// </summary>
    public sealed class SimProbe
    {
        /// <summary>按发生顺序的（人物, 危险类型）；同人同段可多条，预估只取每人第一条。</summary>
        public List<(string ActorId, DangerKind Kind)> Dangers { get; } = new List<(string, DangerKind)>();

        /// <summary>Q687：尚未濒死者仅概率伤可能把健康打到 0（不截断，只附限定句）的人物，按发生顺序。</summary>
        public List<string> ProbabilityZero { get; } = new List<string>();

        /// <summary>Q684：副本里病毒量已到尸变上限的人物（不在副本里尸变／离队，只记风险）。</summary>
        public HashSet<string> Virus { get; } = new HashSet<string>();
    }
}

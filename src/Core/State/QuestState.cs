using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.State
{
    /// <summary>
    /// 主线与支线任务账（开发计划 v0.9 B15，R46、R47；schema 27 起）。
    /// 接取/完成的事实本身在 Progress.Flags（quest:ID:accepted / quest:ID:completed，门槛与前置都读它）；这里记时间、所选分支与工作进度。
    /// </summary>
    public sealed class QuestState
    {
        public Dictionary<string, QuestRecord> Records { get; set; } = new Dictionary<string, QuestRecord>();

        public QuestRecord Of(string id)
        {
            if (!Records.TryGetValue(id, out var r)) Records[id] = r = new QuestRecord();
            return r;
        }

        public QuestState Clone() => new QuestState { Records = Records.ToDictionary(kv => kv.Key, kv => kv.Value.Clone()) };
    }

    public sealed class QuestRecord
    {
        public int? AcceptedAt { get; set; }
        public int? CompletedAt { get; set; }
        /// <summary>所选分支（CH02 抄录/回营区备份，AC01 环境记录/非侵入观察，FA02/FA03 接待选择）。</summary>
        public string? Choice { get; set; }
        /// <summary>已提交的工作分钟（按 5 分钟段；中断保留，续做只补剩余段）。</summary>
        public int WorkMinutes { get; set; }
        /// <summary>旧档按已有事实无声追认（不播对白、不补奖励）。</summary>
        public bool Retro { get; set; }
        /// <summary>已播过的一次性附加对白（CH02 营区备份回报等）。</summary>
        public HashSet<string> Played { get; set; } = new HashSet<string>();

        public QuestRecord Clone()
        {
            var c = (QuestRecord)MemberwiseClone();
            c.Played = new HashSet<string>(Played);
            return c;
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.State
{
    /// <summary>
    /// 新手序章持久状态（开发计划 v0.9 B13；S15 §8 任务和状态存储接口、A10-TUTOR §5）：各教学目标 completedAt/eventId、阶段与当前主目标、
    /// 来客 available/notified/met、系统提示关闭状态与累计睡眠。路线与出口事实（visitor_available、visitor_met、camp_route_known、
    /// south_sign_known）写在 Progress.Flags，与门槛求值同一字典。教学显示状态与奖励状态分开：这里只记标记，不调用任何奖励回调。
    /// schema 22 起记录；旧档迁移时按可靠记录追认。
    /// </summary>
    public sealed class PrologueState
    {
        /// <summary>已完成的任务：键为任务 ID（PRO01—PRO18），一经写入不回退。</summary>
        public Dictionary<string, PrologueTaskRecord> Tasks { get; set; } = new Dictionary<string, PrologueTaskRecord>();
        /// <summary>当前阶段（D1—D5、mainline）：只在内核安全边界重算，鼠标移动不会让主目标跳动。</summary>
        public string StageId { get; set; } = "D1";
        /// <summary>当前主目标的稳定 ID（任务 ID 或 visitor_talk / exit_go / exit_need / route_onward 等）。</summary>
        public string ObjectiveId { get; set; } = "";
        /// <summary>当前最多两条求生建议（任务 ID）。</summary>
        public List<string> Suggestions { get; set; } = new List<string>();

        /// <summary>PRO12：已提交的睡眠段累计分钟（等待/休息不算）。</summary>
        public int SleepMinutes { get; set; }
        /// <summary>PRO14：自上次离开临时营地后已实际到过 village 或 clinic（回到营地即完成）。</summary>
        public bool ReachedVillageOrClinic { get; set; }
        /// <summary>PRO06：已完成一次从合法水源取水（采集到脏水）；与“到访溪流”一起满足才完成。</summary>
        public bool DrewWater { get; set; }
        /// <summary>排队等空闲安全边界播放的饱饱反应对白组（不在动作中途抢输入）。</summary>
        public List<string> PendingReactions { get; set; } = new List<string>();

        /// <summary>来客登记（visitor_available）的世界分钟；未登记为 null。</summary>
        public int? VisitorAvailableAt { get; set; }
        /// <summary>登记时首战是在阈值之后才赢（晚到）或是旧档等效：抵达对白用 visitor_late_or_legacy。</summary>
        public bool VisitorLate { get; set; }
        /// <summary>人在外时的一次非阻断通知已发（visitor notified）。</summary>
        public bool VisitorNotified { get; set; }
        /// <summary>营地抵达对白已播（在营地登记或之后第一次回到营地且空闲时）。</summary>
        public bool VisitorArrivalShown { get; set; }
        /// <summary>首次路线交谈完成（visitor_met）的世界分钟。</summary>
        public int? VisitorMetAt { get; set; }
        /// <summary>与程望交谈次数（重复交谈只播“再问路线”，不重发任何效果）。</summary>
        public int VisitorTalks { get; set; }

        /// <summary>已关闭的系统提示（任务 ID）：关闭不等于完成，之后不再自动弹同一条，可在任务册回看。</summary>
        public HashSet<string> DismissedHints { get; set; } = new HashSet<string>();
        /// <summary>“暂停后续操作提示”：只关中性教学提示，不跳剧情、不清危急反馈、不自动完成任务。</summary>
        public bool HintsPaused { get; set; }
        /// <summary>重复打开的对白组计数（路线日志、路标、再问路线……），用于生成每次回看的独立对话记录编号。</summary>
        public int ReplaySeq { get; set; }
        /// <summary>从 schema 21 及以前迁移进来时的世界分钟（新局为 null）：迁移档的来客不自动弹抵达对白（H6）。</summary>
        public int? MigratedAt { get; set; }
        /// <summary>最近一项新完成的任务与世界分钟（任务角显示一次 PROxx_done 中性反馈）。</summary>
        public string LastDoneTask { get; set; } = "";
        public int LastDoneAt { get; set; }

        public PrologueState Clone()
        {
            var c = (PrologueState)MemberwiseClone();
            c.Tasks = Tasks.ToDictionary(kv => kv.Key, kv => kv.Value.Clone());
            c.Suggestions = new List<string>(Suggestions);
            c.PendingReactions = new List<string>(PendingReactions);
            c.DismissedHints = new HashSet<string>(DismissedHints);
            return c;
        }
    }

    /// <summary>一项教学目标的锁存记录（S15 §8：completedAt/eventId）。</summary>
    public sealed class PrologueTaskRecord
    {
        /// <summary>锁存时的世界分钟；旧档追认为迁移时刻。</summary>
        public int CompletedAt { get; set; }
        /// <summary>引起锁存的真实事件（如 use:a17、travel:a42、legacy:boiled）。</summary>
        public string EventId { get; set; } = "";
        /// <summary>旧档迁移时从可靠记录追认（不是本局新发生的事件）。</summary>
        public bool Retro { get; set; }

        public PrologueTaskRecord Clone() => (PrologueTaskRecord)MemberwiseClone();
    }
}

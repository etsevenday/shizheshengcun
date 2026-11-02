using System.Collections.Generic;

namespace Survival.Core.Config
{
    /// <summary>
    /// 主线与支线任务（开发计划 v0.9 B15，R46、R47；A11「4 主线任务骨架修订与容量」「5 支线委托最小表」「7 招募与补充任务表」）。
    /// 由 tools/import-b15-quests.py 导入；对白在 story.dialogues（eventId = dialogueSetId）。任务事实写 Progress.Flags 的 quest:ID:accepted/completed。
    /// </summary>
    public sealed class QuestsConfig
    {
        public string Source { get; set; } = "";
        public List<QuestConfig> List { get; set; } = new List<QuestConfig>();
        /// <summary>对话记录里任务对白组的名称（“鉴定之前：接取”等，不带内部编号）。</summary>
        public List<PrologueDialogueSetConfig> DialogueSets { get; set; } = new List<PrologueDialogueSetConfig>();
    }

    public sealed class QuestConfig
    {
        public string Id { get; set; } = "";
        /// <summary>正式中文名（A11 表的任务名，玩家看到的名字，不带内部编号）。</summary>
        public string Title { get; set; } = "";
        /// <summary>main / side / recruit。</summary>
        public string Kind { get; set; } = "";
        /// <summary>接取/交付的岗位人物（招募任务为被招募的命名队友）。</summary>
        public string NpcId { get; set; } = "";
        public string LocationId { get; set; } = "";
        /// <summary>本批是否实装；未实装的不显示、不能接（RN02—RN06，待策划确认前置）。</summary>
        public bool Implemented { get; set; }
        /// <summary>任一事实成立才在任务册里出现（空 = 满足 RequiresAll 才出现）。</summary>
        public List<string> ShowAny { get; set; } = new List<string>();
        /// <summary>接取前置（全部成立）。</summary>
        public List<string> RequiresAll { get; set; } = new List<string>();
        /// <summary>HU04“已有一名同行”：名册里至少有一名存活的同行者（不含主角）。</summary>
        public bool RequiresRosterMember { get; set; }
        /// <summary>auto（到达即触发）/ talk（与岗位交谈接取）/ service（办理 AcceptServiceId 接取）/ work（开始工作即接取）/ roster（选择登记）。</summary>
        public string Accept { get; set; } = "";
        public string? AcceptServiceId { get; set; }
        public string? OfferDialogue { get; set; }
        public string? WorkDialogue { get; set; }
        public string? TurninDialogue { get; set; }
        public string? RepeatDialogue { get; set; }
        /// <summary>完成时另播的场景（FA04 整理完成、FA05 联络完成）。</summary>
        public string? CompleteDialogue { get; set; }
        /// <summary>CH02 选“以已有实地记录回营区备份”完成后，在营区广场播一次的回报对白。</summary>
        public string? HubTurninDialogue { get; set; }
        /// <summary>AC02 完成时的外部医疗线索（之后可在任务册回看）。</summary>
        public string? NoteDialogue { get; set; }
        /// <summary>任务工作的固定 wait 分钟（0 = 阅读/选择即完成）；按 5 分钟段推进，中断保留已完成段。</summary>
        public int WorkMinutes { get; set; }
        /// <summary>做这项工作的城市服务（可在其中任一处办理）；空 = 在任务条目里直接开始（CH01）。</summary>
        public List<string> WorkServiceIds { get; set; } = new List<string>();
        /// <summary>开始工作前须有的事实（CH01：取得圣所旧路档册后核对）。</summary>
        public List<string> WorkRequiresAll { get; set; } = new List<string>();
        /// <summary>任务条目里工作按钮的动词（“阅读说明”“核对物资账”）。</summary>
        public string WorkLabel { get; set; } = "";
        /// <summary>任一事实出现即完成（FA02/FA03 首次核心记录、FA05 联络日志）。</summary>
        public List<string> CompleteByAny { get; set; } = new List<string>();
        /// <summary>完成同一事务写入的事实（FA04_merged）。</summary>
        public List<string> CompleteFacts { get; set; } = new List<string>();
        /// <summary>可接时的“下一步”。</summary>
        public string StepOffer { get; set; } = "";
        /// <summary>进行中的“下一步”。</summary>
        public string StepActive { get; set; } = "";
        /// <summary>完成结果短句（日志与任务册）。</summary>
        public string Result { get; set; } = "";
    }

    /// <summary>战术训练（R03 §3）：入口在 h03，每角色每阶一次；阶位 0/1/2 → 每次普通激活 3/4/5AP。</summary>
    public sealed class TacticalTrainingConfig
    {
        public string Source { get; set; } = "";
        public string LocationId { get; set; } = "";
        /// <summary>入口挂在这名岗位的“事项”里（h03 罗铮）。</summary>
        public string NpcId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Summary { get; set; } = "";
        public List<TrainingTierConfig> Tiers { get; set; } = new List<TrainingTierConfig>();
    }

    public sealed class TrainingTierConfig
    {
        /// <summary>训练后达到的阶位（1、2）。</summary>
        public int Tier { get; set; }
        /// <summary>须达到的队伍容量 C（阶位 1：C3 解锁后）；0 为不要求。</summary>
        public int MinCapacity { get; set; }
        public List<string> RequiresAll { get; set; } = new List<string>();
        public Dictionary<string, int> Inputs { get; set; } = new Dictionary<string, int>();
        public int Minutes { get; set; }
    }
}

using System.Collections.Generic;

namespace Survival.Core.Config
{
    /// <summary>
    /// 新手序章（开发计划 v0.9 B13，R39—R43）：18 项 PRO 任务、来客程望、出区、远行确认与阶段主目标。
    /// 由 tools/import-b13-prologue.py 从 prologue-data.json 与 narrative-texts.json（Q32B 定稿）导入，B13d 起另读 prologue-adjudications.json
    /// （Q128 操作前引导、Q129 阶段/特殊目标/下一步短句）；对白本身在 story.dialogues（eventId = dialogueSetId）。
    /// </summary>
    public sealed class PrologueConfig
    {
        public string Source { get; set; } = "";
        public PrologueVisitorConfig Visitor { get; set; } = new PrologueVisitorConfig();
        public PrologueExitConfig Exit { get; set; } = new PrologueExitConfig();
        public List<PrologueDocumentConfig> Documents { get; set; } = new List<PrologueDocumentConfig>();
        public PrologueTalkConfig Talk { get; set; } = new PrologueTalkConfig();
        public PrologueJourneyConfig Journey { get; set; } = new PrologueJourneyConfig();
        public List<PrologueStageConfig> Stages { get; set; } = new List<PrologueStageConfig>();
        /// <summary>6 种特殊主目标的正式文字（Q129：visitor_talk、visitor_return、exit_go、exit_need、route_onward、settle）。</summary>
        public List<PrologueSpecialObjectiveConfig> SpecialObjectives { get; set; } = new List<PrologueSpecialObjectiveConfig>();
        /// <summary>按实际缺项替换的下一步短句（Q129 objectivePolicy.adaptiveSteps：PRO06、PRO14、exit_need）。</summary>
        public List<PrologueAdaptiveStepConfig> AdaptiveSteps { get; set; } = new List<PrologueAdaptiveStepConfig>();
        /// <summary>主目标指引实际前进时按下一真实路段状态替换的下一步（Q139 选 B，objectivePolicy.travelNextStep）。</summary>
        public PrologueTravelStepConfig TravelStep { get; set; } = new PrologueTravelStepConfig();
        /// <summary>只在实际战斗内给操作提示的任务（Q140 选 B，guidancePolicy.nonContextFallback.battleOnlyHintTaskIds：PRO04）：
        /// 战斗外没有有效上下文而回退主目标时，任务框只显示标题与下一步，不显示这些任务的系统提示。</summary>
        public List<string> BattleOnlyHintTaskIds { get; set; } = new List<string>();
        public List<PrologueTaskConfig> Tasks { get; set; } = new List<PrologueTaskConfig>();
        public List<PrologueDialogueSetConfig> DialogueSets { get; set; } = new List<PrologueDialogueSetConfig>();
    }

    /// <summary>来客（S15 §5、A10-TUTOR §6、prologue-data visitor）。</summary>
    public sealed class PrologueVisitorConfig
    {
        public string NpcId { get; set; } = "";
        public string Name { get; set; } = "";
        public string LocationId { get; set; } = "";
        /// <summary>绝对世界分钟阈值（6240 = 第5日08:00）；比较绝对分钟，不写成“第5日且8点后”。</summary>
        public int NotBeforeWorldMinute { get; set; }
        public bool NeedsTutorialBattleWon { get; set; } = true;
        /// <summary>NPC 卡上的身份短句（S15 §5“幸存者营区的普通传讯跑腿”）。</summary>
        public string Role { get; set; } = "";
    }

    /// <summary>出区（S15 出口解锁、QMAP01）：边本身的门槛在 map.gates.QMAP01。</summary>
    public sealed class PrologueExitConfig
    {
        public string EdgeId { get; set; } = "";
        public string From { get; set; } = "";
        public string To { get; set; } = "";
    }

    /// <summary>固定公开文书/路标（PRO15 救助残页、村庄南口路标）：设施栏可查看的对象卡，0 分钟，不占物资卡。</summary>
    public sealed class PrologueDocumentConfig
    {
        public string Id { get; set; } = "";
        public string LocationId { get; set; } = "";
        public string Title { get; set; } = "";
        public string DialogueSet { get; set; } = "";
        /// <summary>前置未满足（还不知道营区路线）时改放的对白组（南口路标 route_unknown）。</summary>
        public string? UnknownDialogueSet { get; set; }
        /// <summary>读完（且前置满足）写入的永久凭证事实（south_sign_known）。</summary>
        public string? Fact { get; set; }
        /// <summary>读完锁存的任务（PRO15）。</summary>
        public string? TaskId { get; set; }
    }

    /// <summary>程望各分支对白组 ID（narrative-texts contentBindings.prologueTalk）。</summary>
    public sealed class PrologueTalkConfig
    {
        public string ArrivalCamp { get; set; } = "";
        public string ArrivalLate { get; set; } = "";
        /// <summary>人在外时的一次非阻断通知（visitor_arrival_away 的系统句）。</summary>
        public string ArrivalAwayText { get; set; } = "";
        public string RouteFirst { get; set; } = "";
        public string AfterRouteBaobao { get; set; } = "";
        public string AfterRouteWithoutBaobao { get; set; } = "";
        public string RouteRepeat { get; set; } = "";
        public string NotReady { get; set; } = "";
        public string RouteLog { get; set; } = "";
    }

    /// <summary>PRO17 远行确认：已知基础路线与建议携带（S15 §6；建议不是硬门槛）。</summary>
    public sealed class PrologueJourneyConfig
    {
        public List<PrologueLegConfig> Legs { get; set; } = new List<PrologueLegConfig>();
        public int BaseMinutesTotal { get; set; }
        public int SuggestedCleanWater { get; set; }
        public int SuggestedFood { get; set; }
        public bool SuggestedWeapon { get; set; }
    }

    public sealed class PrologueLegConfig
    {
        public string From { get; set; } = "";
        public string To { get; set; } = "";
        public string EdgeId { get; set; } = "";
        public int BaseMinutes { get; set; }
    }

    /// <summary>阶段（Q129 选 B：prologue-data stageTexts；标题只作任务册分组标题，不当主目标标题）。</summary>
    public sealed class PrologueStageConfig
    {
        public string Id { get; set; } = "";
        public string Headline { get; set; } = "";
        /// <summary>该阶段的重点（任务册分组副标题）。</summary>
        public string Focus { get; set; } = "";
    }

    /// <summary>特殊主目标（来客、出区、主线、整备）的正式标题与下一步（Q129 specialObjectiveTexts）。</summary>
    public sealed class PrologueSpecialObjectiveConfig
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Step { get; set; } = "";
    }

    /// <summary>按实际缺项替换的下一步（Q129 adaptiveSteps）：Objective 为任务 ID 或特殊目标 ID，When 为条件键。</summary>
    public sealed class PrologueAdaptiveStepConfig
    {
        public string Objective { get; set; } = "";
        public string When { get; set; } = "";
        public string Step { get; set; } = "";
    }

    /// <summary>
    /// Q139 动态下一步的四句（策划原文，含占位符）：Explore 下一段未知且当前可探索未满 100；KnownOpen 下一段已知可走（{实际下一相邻地点名}）；
    /// KnownBlocked 下一段已知未开通（{已发现地点名}、{领域可公开原因}）；UnknownNoExplore 下一段未知且当前不能再探索。
    /// </summary>
    public sealed class PrologueTravelStepConfig
    {
        public string Explore { get; set; } = "";
        public string KnownOpen { get; set; } = "";
        public string KnownBlocked { get; set; } = "";
        public string UnknownNoExplore { get; set; } = "";
    }

    /// <summary>一项 PRO 教学目标（S15 §3 表、prologue-data tasks、narrative-texts taskTexts）。</summary>
    public sealed class PrologueTaskConfig
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>策划原写的建议阶段（D1、D1以后、D5以后、主线接续……）。</summary>
        public string SuggestedStage { get; set; } = "";
        /// <summary>归并后的阶段 ID（D1—D5、mainline）。</summary>
        public string Stage { get; set; } = "";
        public string CompletionRule { get; set; } = "";
        /// <summary>给玩家看的“下一步”短句（Q129 选 B：prologue-data goal = prologue-adjudications nextStep，策划正式文案）。</summary>
        public string Goal { get; set; } = "";
        public string Guidance { get; set; } = "";
        /// <summary>是不是离开门槛（PRO04 首战、PRO16 路线、PRO18 实际抵达）。</summary>
        public bool ExitGate { get; set; }
        public string ExitGateText { get; set; } = "";
        /// <summary>PROxx_hint：操作前的中性系统提示（Q128：在相关界面/预览出现、动作提交前显示；不是饱饱台词，不锁输入）。</summary>
        public string Hint { get; set; } = "";
        /// <summary>PROxx_reaction 对白组（饱饱/主角；Q128 起与系统提示同在首次相关上下文播一次、不从完成事件排队；饱饱不在或 Legacy 时整组不播）。</summary>
        public string ReactionSet { get; set; } = "";
        /// <summary>PROxx_done：真实完成后的简短结果（非模态，只在任务框短暂显示，不进行动日志）。</summary>
        public string Done { get; set; } = "";
    }

    /// <summary>序章对白组的显示名（对话记录标题）与能否重复打开（路线日志、路标、残页、再问路线可重读，回看不重发效果）。</summary>
    public sealed class PrologueDialogueSetConfig
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public bool Repeatable { get; set; }
    }
}

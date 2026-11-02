using System.Collections.Generic;

namespace Survival.Core.Config
{
    /// <summary>
    /// 新手序章（开发计划 v0.9 B13，R39—R43）：18 项 PRO 任务、来客程望、出区、远行确认与阶段主目标。
    /// 由 tools/import-b13-prologue.py 从 prologue-data.json 与 narrative-texts.json（Q32B 定稿）导入；对白本身在 story.dialogues（eventId = dialogueSetId）。
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

    /// <summary>阶段（S15 §2 建议五日节奏表的“当前叙事问题”作主目标标题；mainline 为取得路线后的主线接续）。</summary>
    public sealed class PrologueStageConfig
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        public string Headline { get; set; } = "";
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
        /// <summary>给玩家看的“要做什么”（开发方按 CompletionRule 改写的白话，地点用显示名；试玩修复 M269）。</summary>
        public string Goal { get; set; } = "";
        public string Guidance { get; set; } = "";
        /// <summary>是不是离开门槛（PRO04 首战、PRO16 路线、PRO18 实际抵达）。</summary>
        public bool ExitGate { get; set; }
        public string ExitGateText { get; set; } = "";
        /// <summary>PROxx_hint：中性系统提示（不是饱饱台词，不锁输入）。</summary>
        public string Hint { get; set; } = "";
        /// <summary>PROxx_reaction 对白组（饱饱/主角；饱饱不在时整组不播）。</summary>
        public string ReactionSet { get; set; } = "";
        /// <summary>PROxx_done：完成时的系统反馈（非模态，进日志）。</summary>
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

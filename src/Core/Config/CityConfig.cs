using System.Collections.Generic;

namespace Survival.Core.Config
{
    /// <summary>
    /// 势力事实、营区登记与城市岗位 NPC 框架（开发计划 v0.9 B14，R44、R45；Q32/Q33 答复）。
    /// 由 tools/import-b14-city.py 从 city-npc-data.json、city-services.json、narrative-texts.json 导入；对白在 story.dialogues（eventId = dialogueSetId）。
    /// </summary>
    public sealed class CityConfig
    {
        public string Source { get; set; } = "";
        /// <summary>势力 ID → 显示名（hub_survivor 幸存者营区 / faction_church 新生教会 / faction_academy 灰塔科学院）。</summary>
        public Dictionary<string, string> Factions { get; set; } = new Dictionary<string, string>();
        public CityRegistrationConfig Registration { get; set; } = new CityRegistrationConfig();
        /// <summary>公开告示（h03 制作告示授 expedition_basics；r07 观察区公开通道告示两选项写 academy_observation_pass）。</summary>
        public List<CityNoticeConfig> Notices { get; set; } = new List<CityNoticeConfig>();
        public List<CityNpcConfig> Npcs { get; set; } = new List<CityNpcConfig>();
        public List<CityServiceConfig> Services { get; set; } = new List<CityServiceConfig>();
        /// <summary>
        /// 公共工坊站（B14b，Q141 选 B；city-adjudications.json publicStations）：固定公共设施，实际在该地点且事实满足才开放，
        /// 0 额外使用费；不需自建、不占私人限额、不可拆取。未列出的站不提供。
        /// </summary>
        public List<PublicStationConfig> PublicStations { get; set; } = new List<PublicStationConfig>();
        /// <summary>对话记录用的对白组名称。</summary>
        public List<PrologueDialogueSetConfig> DialogueSets { get; set; } = new List<PrologueDialogueSetConfig>();
    }

    /// <summary>一个公共工坊站（Q141）：地点 + 工坊类型，开放条件 = AllFacts 全有且（AnyFacts 为空或有其一）。</summary>
    public sealed class PublicStationConfig
    {
        public string Id { get; set; } = "";
        public string LocationId { get; set; } = "";
        /// <summary>workbench/forge/sewing/medical/armory（与 workshops.stations 同 ID）。</summary>
        public string StationId { get; set; } = "";
        /// <summary>显示名（“营区工坊工作台”等，策划给定）。</summary>
        public string Name { get; set; } = "";
        public List<string> AllFacts { get; set; } = new List<string>();
        /// <summary>任一即可的资格（锻炉/军械台：FA02_record_credit 或可信旧 QMAP02 完成 trusted_legacy_QMAP02_completed）；空为不要求。</summary>
        public List<string> AnyFacts { get; set; } = new List<string>();
    }

    /// <summary>营区登记（S02 门槛规则 3、A11 Q33 hub_registration_existing）：h01 0 分钟登记写授权，真实走到 h02 才写 hub_registered/C2。</summary>
    public sealed class CityRegistrationConfig
    {
        public string ServiceId { get; set; } = "";
        public string GateLocation { get; set; } = "";
        public string SquareLocation { get; set; } = "";
        public string DialogueSet { get; set; } = "";
        public string AuthorizedFact { get; set; } = "";
        public string RegisteredFact { get; set; } = "";
        public string QuestId { get; set; } = "";
    }

    public sealed class CityNoticeConfig
    {
        public string Id { get; set; } = "";
        public string LocationId { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>查看前须有的事实（h03 制作告示要营区登记）；null 为公开。</summary>
        public string? RequiresFact { get; set; }
        /// <summary>查看前须达到的熟练等级（B16：营区工坊“基础陶工说明”要制造3，A07 X1.3 ceramic_methods）。</summary>
        public Dictionary<string, int> MinSkills { get; set; } = new Dictionary<string, int>();
        /// <summary>首次明确查看/选择时写入的事实。</summary>
        public string GrantsFact { get; set; } = "";
        public string? KnowledgeId { get; set; }
        public string? DialogueSet { get; set; }
        public List<CityChoiceConfig> Choices { get; set; } = new List<CityChoiceConfig>();
    }

    public sealed class CityChoiceConfig
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
    }

    /// <summary>一名岗位 NPC（A11 §1—§4、§6；city-npc-data.json）。岗位不可招募；退场时由同机构当班接续员代班，不复活命名人物。</summary>
    public sealed class CityNpcConfig
    {
        public string NpcId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public string FactionId { get; set; } = "";
        public string LocationId { get; set; } = "";
        public List<string> QuestIds { get; set; } = new List<string>();
        public List<string> ServiceIds { get; set; } = new List<string>();
        /// <summary>service_only（岗位）。固定队友、周期候选、应急补员不在这张表里（A11 §6）。</summary>
        public string RecruitmentType { get; set; } = "";
        public bool CanRecruit { get; set; }
        public string DialogueSet { get; set; } = "";
        /// <summary>九种状态 → 对白组 ID（first_greeting/role/quest_offer/quest_return/idle/locked/service_end/institution_substitute/service_offer）。</summary>
        public Dictionary<string, string> States { get; set; } = new Dictionary<string, string>();
        /// <summary>服务面板里该岗位对每项服务说的一句（narrative-texts serviceOfferLines）。</summary>
        public Dictionary<string, string> ServiceOfferLines { get; set; } = new Dictionary<string, string>();
        public string DutySpeaker { get; set; } = "";
    }

    /// <summary>一项城市服务（A11「Q33 城市岗位完整服务表」、city-services.json）。</summary>
    public sealed class CityServiceConfig
    {
        public string Id { get; set; } = "";
        /// <summary>正式中文名（B14b，Q145 选 B：city-services.json displayName）。</summary>
        public string Title { get; set; } = "";
        /// <summary>玩家摘要（Q145：playerSummary；不带内部任务编号）。真实工时、费用与可办原因仍看运行时预览。</summary>
        public string Summary { get; set; } = "";
        public string LocationId { get; set; } = "";
        public string Kind { get; set; } = "";
        /// <summary>工时（世界分钟）；null 为沿所引原契约。</summary>
        public int? WorldMinutes { get; set; }
        public Dictionary<string, int> Inputs { get; set; } = new Dictionary<string, int>();
        public string Effect { get; set; } = "";
        public string? QuestId { get; set; }
        public List<string> Facts { get; set; } = new List<string>();
        public List<CityServiceChoiceConfig> Choices { get; set; } = new List<CityServiceChoiceConfig>();
        public int? DailyPartyQuota { get; set; }
        public string? Mode { get; set; }
        public string? Target { get; set; }
        /// <summary>NPC 代办的有效熟练等级（clinic_dressing：medicine 1）。</summary>
        public int? ActorSkillLevel { get; set; }
        /// <summary>
        /// 是否已实装。未实装的从“事项”面板、事项数与岗位卡里隐藏（B14b，Q146 选 B）；直接请求拒绝且零扣费零效果。
        /// 后续批次实装同 ID 后改为 true 即自然出现。
        /// </summary>
        public bool Implemented { get; set; }
    }

    /// <summary>
    /// 服务的一项选择：施济配给为目标某项状态低于 Below 才可选、完成时产出 Outputs；
    /// 任务类服务（B15：接待路线、观察范围、CH02 两条路）Stat 为空，只记选择与 Facts。
    /// </summary>
    public sealed class CityServiceChoiceConfig
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        /// <summary>satiety / hydration。</summary>
        public string Stat { get; set; } = "";
        public double Below { get; set; }
        public Dictionary<string, int> Outputs { get; set; } = new Dictionary<string, int>();
        /// <summary>选这一项首次明确提交时写入的事实（B15：FA02/FA03 两个选择同写同一路线事实）；空为不写。</summary>
        public List<string> Facts { get; set; } = new List<string>();
    }
}

namespace Survival.Core.Config
{
    /// <summary>
    /// 鉴定所服务（R07 X3.2—X3.5、appraisal-service-contract.json；开发计划 v0.9 B18）：由 tools/import-b18-appraisal.py 导入。
    /// 基础说明 0 分钟；详细鉴定固定 wait、耗材完成时扣、样本不消耗；换物固定 wait、交出样本 1、全队每世界日共享份数；三档有界随机报价。
    /// </summary>
    public sealed class AppraisalConfig
    {
        public string Source { get; set; } = "";
        public string ServiceId { get; set; } = "";
        public string LocationId { get; set; } = "";
        public string SampleItemId { get; set; } = "";
        /// <summary>鉴定记录的识别范围版本（R07 X3.4 r07_white_v1）。</summary>
        public string ScopeVersion { get; set; } = "";
        public int InfoMinutes { get; set; }
        public int AppraiseMinutes { get; set; }
        public System.Collections.Generic.Dictionary<string, int> AppraiseConsume { get; set; } = new System.Collections.Generic.Dictionary<string, int>();
        public int TradeMinutes { get; set; }
        public int TradeDailyPartyQuota { get; set; }
        /// <summary>报价版本（white_barter_v2）；事务里记的版本不是它时安全取消（contract unknownQuoteVersion）。</summary>
        public string QuoteVersion { get; set; } = "";
        /// <summary>存档里保留最近几个世界日的报价表（开发假设 B18-2）。</summary>
        public int QuoteKeepDays { get; set; } = 7;
        public System.Collections.Generic.List<AppraisalOfferConfig> Offers { get; set; } = new System.Collections.Generic.List<AppraisalOfferConfig>();
    }

    /// <summary>一档报价：输出普通物资 ItemId，数量取闭区间 [Min, Max] 内等概率整数。</summary>
    public sealed class AppraisalOfferConfig
    {
        public string Id { get; set; } = "";
        public string ItemId { get; set; } = "";
        public int Min { get; set; }
        public int Max { get; set; }
        public int MinGrams { get; set; }
        public int MaxGrams { get; set; }
    }
}

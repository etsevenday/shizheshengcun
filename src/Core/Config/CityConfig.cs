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
        /// <summary>对话记录用的对白组名称。</summary>
        public List<PrologueDialogueSetConfig> DialogueSets { get; set; } = new List<PrologueDialogueSetConfig>();
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
        /// <summary>显示名（开发方措辞，策划只给 serviceId）。</summary>
        public string Title { get; set; } = "";
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
        /// <summary>本批是否实装；未实装的界面写 PendingText（开发假设 B14-6）。</summary>
        public bool Implemented { get; set; }
        public string? PendingText { get; set; }
    }

    /// <summary>施济配给的一项选择：目标某项状态低于 Below 才可选，完成时产出 Outputs。</summary>
    public sealed class CityServiceChoiceConfig
    {
        public string Id { get; set; } = "";
        public string Label { get; set; } = "";
        /// <summary>satiety / hydration。</summary>
        public string Stat { get; set; } = "";
        public double Below { get; set; }
        public Dictionary<string, int> Outputs { get; set; } = new Dictionary<string, int>();
    }
}

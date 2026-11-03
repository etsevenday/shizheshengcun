using System.Collections.Generic;
using System.Linq;

namespace Survival.Core.State
{
    /// <summary>
    /// 城市岗位 NPC 与服务的持久状态（开发计划 v0.9 B14，R45；schema 26 起）。
    /// 势力/登记/接触事实不在这里，统一在 Progress.Flags（hub_registered、church_contacted、academy_contacted、quest:ID:accepted 等）。
    /// </summary>
    public sealed class CityState
    {
        /// <summary>已见过面（首次交谈播过 first_greeting + role）的岗位 npcId。</summary>
        public HashSet<string> Greeted { get; set; } = new HashSet<string>();
        /// <summary>命名岗位人物已退场（由同机构当班接续员代班）。不复活；服务与任务账、日配额沿用同一本账。</summary>
        public HashSet<string> Absent { get; set; } = new HashSet<string>();
        /// <summary>每名岗位的交谈次数（只作记录，不发奖励）。</summary>
        public Dictionary<string, int> Talks { get; set; } = new Dictionary<string, int>();
        /// <summary>服务日配额已核销数：键 serviceId@开工世界日（Q33 church_relief_ration quotaKey 的 serviceId+startWorldDay；单局单队伍）。</summary>
        public Dictionary<string, int> QuotaUsed { get; set; } = new Dictionary<string, int>();
        /// <summary>进行中的计时服务事务（同一时刻至多一笔，随主行动）；完成/取消/失败后清空。</summary>
        public CityServiceTxn? Pending { get; set; }
        public int NextTxn { get; set; } = 1;
        /// <summary>已完成的计时服务笔数（completionSerial，只作记录）。</summary>
        public int Completed { get; set; }
        /// <summary>
        /// 鉴定所世界日报价表（R07 X3.3、S01「世界日报价与服务快照」，存档 30）：当日首次安全访问时一次生成三档并保存；
        /// 开窗、取消、读档、代班都读这里，不重抽。保留最近若干世界日（AppraisalConfig.QuoteKeepDays）。
        /// </summary>
        public List<AppraisalQuoteSnapshot> Quotes { get; set; } = new List<AppraisalQuoteSnapshot>();
        /// <summary>
        /// 主城鸟瞰已自动展示过的城（父城节点 ID，A10-CITY §3“首到记录存按城ID的一次展示标记”，开发计划 v0.9 B24，存档 41）：刷新/读档/再到达不再自动打开。
        /// </summary>
        public HashSet<string> OverviewShown { get; set; } = new HashSet<string>();
        /// <summary>待自动打开鸟瞰的城（首次真实到达父节点时记下，对白读完、安全空闲时界面打开并转入 OverviewShown）；没有为 null。</summary>
        public string? OverviewPending { get; set; }
        /// <summary>待打开的是旧档补显（存档 40→41 时人已在城里）：界面顶部写一句纯说明，不补奖励。</summary>
        public bool OverviewPendingLegacy { get; set; }

        public CityState Clone() => new CityState
        {
            Greeted = new HashSet<string>(Greeted),
            Absent = new HashSet<string>(Absent),
            Talks = new Dictionary<string, int>(Talks),
            QuotaUsed = new Dictionary<string, int>(QuotaUsed),
            Pending = Pending?.Clone(),
            NextTxn = NextTxn,
            Completed = Completed,
            Quotes = Quotes.Select(q => q.Clone()).ToList(),
            OverviewShown = new HashSet<string>(OverviewShown),
            OverviewPending = OverviewPending,
            OverviewPendingLegacy = OverviewPendingLegacy,
        };
    }

    /// <summary>计时城市服务事务快照（city-services.json common.saveFields：serviceTxnId/serviceId/actorAndTarget/locationId/startDay/quotaReservation）。</summary>
    public sealed class CityServiceTxn
    {
        public int TxnId { get; set; }
        /// <summary>挂在哪个主行动上（行动取消/结束后快照失效）。</summary>
        public int ActionId { get; set; }
        public string ServiceId { get; set; } = "";
        public string NpcId { get; set; } = "";
        public string TargetActorId { get; set; } = "";
        public string? ChoiceId { get; set; }
        public string LocationId { get; set; } = "";
        public int StartDay { get; set; }
        /// <summary>开工时占用了日配额（未完成不扣，完成才核销）。</summary>
        public bool QuotaReserved { get; set; }
        /// <summary>开工时预留的材料实例（随主行动 ReservedItemIds，完成时核销）。</summary>
        public List<int> ReservedItemIds { get; set; } = new List<int>();

        // ── 鉴定所事务锁（R07 X3.5、contract transactionLocks，B18；其他服务为空） ──
        /// <summary>appraise（详细鉴定）/ trade（换物）；其他服务为 null。</summary>
        public string? AppraisalMode { get; set; }
        /// <summary>所选样本的确切实例（份额固定 1，开工时已拆出并预留）。</summary>
        public int? SampleInstanceId { get; set; }
        /// <summary>样本开工时的 owner（sourceOwner）；取消/失败解除预留后仍在原处。</summary>
        public string? SampleOwner { get; set; }
        /// <summary>换物所用报价表的世界日（= StartDay）。</summary>
        public int QuoteDay { get; set; }
        public string? QuoteVersion { get; set; }
        public string? OfferId { get; set; }
        public string? OutputItemId { get; set; }
        /// <summary>开工时锁定的确切输出份数；跨午夜完成仍发这个数。</summary>
        public int ExactOutput { get; set; }

        public CityServiceTxn Clone()
        {
            var c = (CityServiceTxn)MemberwiseClone();
            c.ReservedItemIds = ReservedItemIds.ToList();
            return c;
        }
    }

    /// <summary>某世界日的鉴定所报价表（锁键 campaignId+serviceId+worldDay+quoteVersion）：每档确切输出份数。</summary>
    public sealed class AppraisalQuoteSnapshot
    {
        public string CampaignId { get; set; } = "";
        public string ServiceId { get; set; } = "";
        public int Day { get; set; }
        public string QuoteVersion { get; set; } = "";
        /// <summary>offerId → 确切份数。</summary>
        public Dictionary<string, int> Offers { get; set; } = new Dictionary<string, int>();
        /// <summary>生成（锁存）时的世界分钟。</summary>
        public int LockedAt { get; set; }

        public AppraisalQuoteSnapshot Clone() => new AppraisalQuoteSnapshot
        {
            CampaignId = CampaignId, ServiceId = ServiceId, Day = Day, QuoteVersion = QuoteVersion,
            Offers = new Dictionary<string, int>(Offers), LockedAt = LockedAt,
        };
    }
}

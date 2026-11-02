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

        public CityState Clone() => new CityState
        {
            Greeted = new HashSet<string>(Greeted),
            Absent = new HashSet<string>(Absent),
            Talks = new Dictionary<string, int>(Talks),
            QuotaUsed = new Dictionary<string, int>(QuotaUsed),
            Pending = Pending?.Clone(),
            NextTxn = NextTxn,
            Completed = Completed,
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

        public CityServiceTxn Clone()
        {
            var c = (CityServiceTxn)MemberwiseClone();
            c.ReservedItemIds = ReservedItemIds.ToList();
            return c;
        }
    }
}

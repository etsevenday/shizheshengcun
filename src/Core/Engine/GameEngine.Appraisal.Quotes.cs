using System.Globalization;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 鉴定所世界日报价表（开发计划 v0.9 B18；R07 X3.3、S01「世界日报价与服务快照」、contract quoteGeneration）：
    /// 锁键 campaignId+serviceId+worldDay+quoteVersion；每档按 campaignId+serviceId+worldDay+offerId+quoteVersion 的稳定种子取闭区间等概率整数；
    /// 当日首次安全访问时一次生成三档并存档，开窗/取消/读档/代班/换人都读存档里的表，不重抽；午夜后只在下一次安全访问生成新表。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>campaignId（开发假设 B18-2）：本局新游戏种子，一局一个、读档不变。</summary>
        public static string CampaignIdOf(GameState s) => s.Seed.ToString(CultureInfo.InvariantCulture);

        /// <summary>FNV-1a 64 位（与平台无关的稳定哈希）。</summary>
        private static ulong Fnv64(string text)
        {
            var h = 14695981039346656037UL;
            foreach (var ch in text)
            {
                h ^= ch;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>一档的确切份数：种子字段按 contract seedFields 顺序拼接后哈希，下界 + 哈希 mod 区间长度（闭区间内每个整数等概率）。</summary>
        public static int QuoteQuantity(string campaignId, string serviceId, int worldDay, AppraisalOfferConfig offer, string quoteVersion)
        {
            var span = (ulong)(offer.Max - offer.Min + 1);
            var h = Fnv64($"{campaignId}|{serviceId}|{worldDay}|{offer.Id}|{quoteVersion}");
            return offer.Min + (int)(h % span);
        }

        /// <summary>某世界日已锁存的报价表（按锁键找）；没有为 null。</summary>
        public AppraisalQuoteSnapshot? QuoteSnapshot(GameState s, int day) =>
            s.City.Quotes.FirstOrDefault(q => q.Day == day && q.ServiceId == Ap.ServiceId && q.QuoteVersion == Ap.QuoteVersion && q.CampaignId == CampaignIdOf(s));

        /// <summary>现在是不是在鉴定所的安全访问：实际在鉴定所、未死亡、没有战斗或敌踪待处理。</summary>
        private bool SafeAtAppraisal(GameState s) =>
            !string.IsNullOrEmpty(Ap.LocationId) && s.CurrentLocation == Ap.LocationId && !s.Progress.Dead && s.Battle == null && s.Encounter == null;

        /// <summary>
        /// 当日首次安全访问时生成并持久化完整三档报价表（开发假设 B18-1：实际到达鉴定所，或在鉴定所打开面板/开工）。
        /// 已有当日表直接返回，不重抽；不在安全访问时不生成、返回已有表或 null。存档只保留最近若干世界日。
        /// </summary>
        public AppraisalQuoteSnapshot? EnsureAppraisalQuotes(GameState s)
        {
            var day = Clock.Day(s.GameMinute);
            var have = QuoteSnapshot(s, day);
            if (have != null || !SafeAtAppraisal(s) || Ap.Offers.Count == 0) return have;
            var snap = new AppraisalQuoteSnapshot
            {
                CampaignId = CampaignIdOf(s), ServiceId = Ap.ServiceId, Day = day, QuoteVersion = Ap.QuoteVersion, LockedAt = s.GameMinute,
                Offers = Ap.Offers.ToDictionary(o => o.Id, o => QuoteQuantity(CampaignIdOf(s), Ap.ServiceId, day, o, Ap.QuoteVersion)),
            };
            s.City.Quotes.RemoveAll(q => q.Day <= day - System.Math.Max(1, Ap.QuoteKeepDays));
            s.City.Quotes.Add(snap);
            return snap;
        }

        /// <summary>面板里的报价部分：当日表（只读存档里的）、份数/普通品质/重量/落点、报价日与下次世界午夜。</summary>
        private void FillQuotePreview(GameState s, AppraisalView v)
        {
            var day = Clock.Day(s.GameMinute);
            var snap = QuoteSnapshot(s, day);
            v.QuoteTitle = $"本营区当日回收报价v{Ap.QuoteVersion.Split('_').Last().TrimStart('v')}";
            v.QuoteDay = day;
            v.QuoteLocked = snap != null;
            v.NextRefreshText = $"下次世界午夜 {Clock.DayTime(day * Clock.MinutesPerDay)} 起额度恢复{Ap.TradeDailyPartyQuota}包；新报价在那之后第一次安全到访时锁定（同日不再变动）";
            v.OutputPlace = $"{ZoneName(s, s.Ground)}地面（不会自动入包，拾取另按五卡/30公斤校验）";
            if (snap == null) return;
            foreach (var o in Ap.Offers)
            {
                if (!snap.Offers.TryGetValue(o.Id, out var n)) continue;
                var grams = n * (Config.Items.FirstOrDefault(i => i.Id == o.ItemId)?.WeightGrams ?? 0);
                v.Offers.Add(new AppraisalOfferView
                {
                    OfferId = o.Id, ItemId = o.ItemId, ItemName = ItemName(o.ItemId), Quantity = n, Grams = grams,
                    Text = $"{ItemName(o.ItemId)}×{n}（普通，{Fmt.N(grams / 1000.0)}公斤）",
                    RangeText = $"本档区间{o.Min}—{o.Max}份、每个数量等概率；第{snap.Day}日已锁定为{n}份",
                });
            }
        }
    }
}

using System;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// 鉴定所以物换物（开发计划 v0.9 B18；R07 X3.3/X3.5、S01「鉴定交易服务与跨日请求」「世界日报价与服务快照」、contract transactionLocks/crossMidnight）：
    /// 交出普通白色 1 份，按当日已锁定报价换一档普通物资，5 分钟 wait；全队每世界日三档合计 2 包，开工预留占开始日的额度，完成才核销；
    /// 跨午夜仍按开始日的确切报价与日桶；未知报价版本安全取消；产物落当前服务区地面；没有货币、回售与买回入口。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>所选样本能不能交易（与通用阻拦、报价、额度无关的部分）。教学样本永禁售（服务端每次重验）。</summary>
        private string? TradeSampleBlocked(GameState s, ItemInstance i)
        {
            if (i.ReservedBy != null || IsLocked(s, i)) return "这份样本已被预留";
            if (IsTutorialSample(i)) return "教学样本不可交易";
            if (i.Source == null) return "来源不明的样本：保留不交易";
            return null;
        }

        /// <summary>某世界日的换物额度：已核销、进行中预留。</summary>
        private (int Used, int Reserved) TradeQuota(GameState s, int day)
        {
            var used = s.City.QuotaUsed.TryGetValue(QuotaKey(Ap.ServiceId, day), out var n) ? n : 0;
            var p = ActiveCityTxn(s);
            var reserved = p != null && p.ServiceId == Ap.ServiceId && p.QuotaReserved && p.StartDay == day ? 1 : 0;
            return (used, reserved);
        }

        private void FillTradePreview(GameState s, AppraisalView v)
        {
            FillQuotePreview(s, v);
            var day = Clock.Day(s.GameMinute);
            var (used, reserved) = TradeQuota(s, day);
            v.QuotaTotal = Ap.TradeDailyPartyQuota;
            v.QuotaUsed = used;
            v.QuotaReserved = reserved;
            v.QuotaAvailable = Math.Max(0, Ap.TradeDailyPartyQuota - used - reserved);
            v.TradeBlocked = !v.QuoteLocked ? "当日报价表还没锁定：在鉴定所安全时打开本面板即锁定（同日不再变动）"
                : v.QuotaAvailable <= 0 ? $"今天（第{day}日）的换物额度已用完（全队每日{Ap.TradeDailyPartyQuota}包，三档合计；换人、代班或读档都不会多出）"
                : null;
        }

        /// <summary>
        /// 开始以物换物：先完整校验地点/空闲/样本/报价/当日额度，再一次建立事务——锁 serviceTxnId、startDay、quoteDay、quoteVersion、offerId、
        /// exactOutput、样本实例与份额、原 owner 与开始日额度预留；主行动 5 分钟 wait。未确认前（关窗、换样本、改报价）0 耗时 0 扣费。
        /// </summary>
        public ActionResult StartAppraisalTrade(GameState s, int instanceId, string offerId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var svc = CityService(Ap.ServiceId);
            if (svc == null) return ActionResult.Fail("没有这项事项");
            if (CityCommonBlocked(s, svc) is string common) return ActionResult.Fail(common);
            var snap = EnsureAppraisalQuotes(s);
            if (snap == null) return ActionResult.Fail("当日报价表还没锁定");
            var offer = Ap.Offers.FirstOrDefault(o => o.Id == offerId);
            if (offer == null || !snap.Offers.TryGetValue(offer.Id, out var exact)) return ActionResult.Fail("没有这一档报价");
            var i = FindItem(s, instanceId);
            if (i == null || !(i.IsCarried || i.Owner == s.Ground)) return ActionResult.Fail("只能交出主角随身或当前地面的样本");
            if (i.ItemId != Ap.SampleItemId) return ActionResult.Fail("鉴定所只收普通活性物质，不回收、不出售其他物品");
            if (TradeSampleBlocked(s, i) is string why) return ActionResult.Fail(why);
            var day = Clock.Day(s.GameMinute);
            var (used, reserved) = TradeQuota(s, day);
            if (used + reserved >= Ap.TradeDailyPartyQuota)
                return ActionResult.Fail($"今天（第{day}日）的换物额度已用完（全队每日{Ap.TradeDailyPartyQuota}包，三档合计）");
            var req = ActionRequest.Wait(Ap.TradeMinutes);
            req.TargetId = CityServicePrefix + svc.Id;
            req.RequestId = requestId;
            var res = Start(s, req);
            if (!res.Ok) return res;
            var act = s.Main!;
            act.Label = $"换物（{Clock.Duration(Ap.TradeMinutes)}）：交出1份→{ItemName(offer.ItemId)}×{exact}";
            var sample = ReserveSample(s, i, act.ActionId);
            act.ReservedItemIds.Add(sample.InstanceId);
            var txn = NewAppraisalTxn(s, act, TradeMode, sample);
            txn.ReservedItemIds.Add(sample.InstanceId);
            txn.QuoteDay = snap.Day;
            txn.QuoteVersion = snap.QuoteVersion;
            txn.OfferId = offer.Id;
            txn.OutputItemId = offer.ItemId;
            txn.ExactOutput = exact;
            txn.QuotaReserved = true;
            s.City.Pending = txn;
            return res;
        }

        /// <summary>
        /// 换物完成（地点与样本已由调用方重验）：报价版本未知则安全取消（不按新表替补）；开始日额度若已满则失败；
        /// 否则消耗所选 1 份、产物按 exactOutput 落当前服务区地面、核销开始日日桶。返回失败原因，成功为 null。
        /// </summary>
        private string? CompleteTrade(GameState s, CityServiceTxn p, ItemInstance sample, out string made)
        {
            made = "";
            if (p.QuoteVersion != Ap.QuoteVersion) return $"报价版本{p.QuoteVersion ?? "（空）"}已不可用，安全取消（不按新报价替换）";
            if (string.IsNullOrEmpty(p.OutputItemId) || !Config.Items.Any(x => x.Id == p.OutputItemId) || p.ExactOutput <= 0) return "报价内容无效，安全取消";
            if (TradeSampleBlocked(s, Unreserved(sample)) is string why) return why;
            var key = QuotaKey(Ap.ServiceId, p.StartDay);
            var used = s.City.QuotaUsed.TryGetValue(key, out var u) ? u : 0;
            if (used >= Ap.TradeDailyPartyQuota) return $"第{p.StartDay}日的换物额度已用完";
            var label = SubstanceLabel(sample);
            ConsumeOne(s, sample);
            CreateItems(s, p.OutputItemId!, p.ExactOutput, s.Ground);
            s.City.QuotaUsed[key] = used + 1;
            var grams = p.ExactOutput * (Config.Items.First(x => x.Id == p.OutputItemId).WeightGrams);
            made = $"交出{label}1份，{ItemName(p.OutputItemId!)}×{p.ExactOutput}（普通，{Fmt.N(grams / 1000.0)}公斤）已放在{ZoneName(s, s.Ground)}地面（不会自动入包）；"
                   + $"按第{p.QuoteDay}日报价，计入第{p.StartDay}日额度（已用{used + 1}/{Ap.TradeDailyPartyQuota}包）";
            return null;
        }

        /// <summary>重验用：把本事务自己的预留视为未预留（只读副本）。</summary>
        private static ItemInstance Unreserved(ItemInstance i)
        {
            var c = i.Clone();
            c.ReservedBy = null;
            return c;
        }
    }
}

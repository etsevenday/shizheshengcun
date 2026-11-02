using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>鉴定所面板的当前预览（R07 X3.6 必要字段；0 分钟，只读）。</summary>
    public sealed class AppraisalView
    {
        public string ServiceId { get; set; } = "";
        /// <summary>服务所在地（鉴定所）。</summary>
        public string LocationName { get; set; } = "";
        /// <summary>执行者（固定主角）。</summary>
        public string ActorName { get; set; } = "";
        /// <summary>服务可用状态（“可办理”或不能办的通用原因）。</summary>
        public string Status { get; set; } = "";
        /// <summary>通用阻拦（不在鉴定所、行动中、战斗、对话）；能办为 null。</summary>
        public string? Blocked { get; set; }
        public List<AppraisalSampleView> Samples { get; set; } = new List<AppraisalSampleView>();
        public int AppraiseMinutes { get; set; }
        /// <summary>“植物纤维×1（完成时扣）”。</summary>
        public string AppraiseCost { get; set; } = "";
        /// <summary>纤维的真实来源（“随身2、鉴定所地面0”）。</summary>
        public string FiberSourceText { get; set; } = "";
        /// <summary>纤维不足的原因；够为 null。</summary>
        public string? FiberBlocked { get; set; }
        public int TradeMinutes { get; set; }
        /// <summary>“本营区当日回收报价v2”。</summary>
        public string QuoteTitle { get; set; } = "";
        /// <summary>当日报价表已锁存（首次安全访问生成）；没有时换物不可用。</summary>
        public bool QuoteLocked { get; set; }
        public int QuoteDay { get; set; }
        public List<AppraisalOfferView> Offers { get; set; } = new List<AppraisalOfferView>();
        public int QuotaTotal { get; set; }
        public int QuotaUsed { get; set; }
        public int QuotaReserved { get; set; }
        public int QuotaAvailable { get; set; }
        /// <summary>下次世界午夜刷新说明。</summary>
        public string NextRefreshText { get; set; } = "";
        /// <summary>换物产物落点（“鉴定所地面（不会自动入包）”）。</summary>
        public string OutputPlace { get; set; } = "";
        /// <summary>换物通用阻拦（报价未锁存、当日额度用完）；与样本无关。能换为 null。</summary>
        public string? TradeBlocked { get; set; }
        /// <summary>进行中的鉴定/换物说明；没有为 null。</summary>
        public string? PendingText { get; set; }
    }

    /// <summary>一份可在鉴定所选用的活性物质样本（主角随身或当前服务区地面）。</summary>
    public sealed class AppraisalSampleView
    {
        public int InstanceId { get; set; }
        public string Name { get; set; } = "";
        /// <summary>物质品质（白色）。</summary>
        public string QualityText { get; set; } = "";
        /// <summary>实际来源类别：“首战教学”“普通丧尸回收”“来源不明”。</summary>
        public string SourceText { get; set; } = "";
        public bool Tutorial { get; set; }
        public bool Ordinary { get; set; }
        /// <summary>所选数量固定 1；这里写这一堆共有几份。</summary>
        public int Count { get; set; }
        /// <summary>持有者/当前地面（“主角随身”“鉴定所地面”）。</summary>
        public string HolderText { get; set; } = "";
        public bool Reserved { get; set; }
        public bool Appraised { get; set; }
        /// <summary>鉴定状态（“未鉴定”“已鉴定：普通白色，融合无属性（第N日 hh:mm 记录）”）。</summary>
        public string AppraisalText { get; set; } = "";
        /// <summary>既定融合预览（普通“无属性变化”；教学“固定绿色攻击力+0.1”，分清物质白色与词条绿色）。</summary>
        public string FusionText { get; set; } = "";
        public string? AppraiseBlocked { get; set; }
        public string? TradeBlocked { get; set; }
        /// <summary>开发核验用来源事件 ID（界面只在悬停里给，不代替玩家说明）。</summary>
        public string DevSource { get; set; } = "";
    }

    /// <summary>当日一档报价（已锁存的确切份数）。</summary>
    public sealed class AppraisalOfferView
    {
        public string OfferId { get; set; } = "";
        public string ItemId { get; set; } = "";
        public string ItemName { get; set; } = "";
        public int Quantity { get; set; }
        public int Grams { get; set; }
        /// <summary>“布料×2（普通，0.2公斤）”。</summary>
        public string Text { get; set; } = "";
        /// <summary>闭区间说明（“1—3份等概率”），只作说明，交换按 Quantity。</summary>
        public string RangeText { get; set; } = "";
    }

    /// <summary>
    /// 营区鉴定所的有限鉴定与活性物质换物（开发计划 v0.9 B18；R07「X3 营区有限鉴定与活性物质换物」X3.1—X3.7、
    /// S01「鉴定交易服务与跨日请求」「世界日报价与服务快照」、appraisal-service-contract.json；Q33 选 B：沿原契约，不在城市服务表里重复收费）。
    /// <list type="bullet">
    /// <item>基础说明 0 分钟：样本品质、来源类别、既定融合预览；教学样本直接看免费固定结果说明（写 fixed_tutorial_attack 记录）。</item>
    /// <item>详细鉴定：只收普通合法白色，10 分钟 wait，开工预留所选 1 份与植物纤维 1，完成才扣纤维并写 ordinary_no_effect 记录；样本不消耗。</item>
    /// <item>事务沿 B14 城市服务：主行动 wait、事务快照在 CityState.Pending、取消/失败原物原处不扣、完成重验后原子提交。</item>
    /// </list>
    /// </summary>
    public sealed partial class GameEngine
    {
        public const string AppraiseMode = "appraise";
        public const string TradeMode = "trade";

        private AppraisalConfig Ap => Config.Appraisal;

        /// <summary>是不是鉴定所服务。</summary>
        public bool IsAppraisalService(string serviceId) => !string.IsNullOrEmpty(Ap.ServiceId) && serviceId == Ap.ServiceId;

        /// <summary>样本来源类别（R07 X3.6）：首战教学 / 普通丧尸回收 / 来源不明。</summary>
        public static string SampleSourceText(ItemInstance i) =>
            i.Source == null ? "来源不明" : i.Source.TutorialReward ? "首战教学" : "普通丧尸回收";

        /// <summary>普通合法白色样本：有来源字段且不是教学奖励（开发假设 B18-4）。</summary>
        public static bool IsOrdinarySample(ItemInstance i) => i.Source != null && !i.Source.TutorialReward;

        public static bool IsTutorialSample(ItemInstance i) => i.Source?.TutorialReward == true;

        /// <summary>鉴定状态说明（物品卡与面板共用）。</summary>
        public string AppraisalStateText(ItemInstance i)
        {
            var r = i.Appraisal;
            if (r == null) return "未鉴定";
            var when = Clock.DayTime(r.Minute);
            return r.Result == AppraisalRecord.FixedTutorialAttack
                ? $"已看教学说明：融合固定得绿色词条“{Config.ActiveSubstance.TutorialEffect.Label}”（{when}记录）"
                : $"已鉴定：普通白色，融合无属性（{when}记录）";
        }

        /// <summary>既定融合预览（R07 X3.6）：分清物质白色与词条绿色；说明直接融合的原入口照常可用。</summary>
        public string AppraisalFusionText(GameState s, ItemInstance i)
        {
            var te = Config.ActiveSubstance.TutorialEffect;
            var core = IsTutorialSample(i) && !s.Story.TutorialFusionDone
                ? $"融合结果：固定绿色词条“{te.Label}”（物质本身仍是白色品质，词条是绿色品质，两者不是同一字段；只取得一次）"
                : "融合结果：无属性变化（普通白色活性物质）";
            return core + "；不必先鉴定，物品卡下“吞噬融合”原入口照常可用";
        }

        /// <summary>鉴定所能选用的样本：主角随身与当前服务区地面的活性物质（开发假设 B18-3：同行者个人包尚未独立 owner；远处柜子/候补不可选）。</summary>
        private IEnumerable<ItemInstance> AppraisalSamples(GameState s) =>
            s.Items.Where(i => i.ItemId == Ap.SampleItemId && (i.IsCarried || i.Owner == s.Ground))
                .OrderBy(i => i.IsCarried ? 0 : 1).ThenBy(i => i.InstanceId);

        private string HolderOf(GameState s, ItemInstance i) => i.IsCarried ? $"{ActorDisplayName(s, GameState.PlayerId)}随身" : $"{ZoneName(s, s.Ground)}地面";

        /// <summary>所选样本能不能做详细鉴定（与通用阻拦、纤维无关的部分）。</summary>
        private string? AppraiseSampleBlocked(GameState s, ItemInstance i)
        {
            if (i.ReservedBy != null || IsLocked(s, i)) return "这份样本已被预留";
            if (i.Source == null) return "来源不明的样本：鉴定所不受理（保留不交易）";
            if (IsTutorialSample(i)) return "教学样本不用付费鉴定：直接看免费说明（0分钟，不扣纤维）";
            if (i.Appraisal != null) return "已记录：普通白色、融合无属性（再看免费，不重复收费）";
            return null;
        }

        /// <summary>纤维（或其他鉴定耗材）是否够：取主角随身或当地地面（开发假设 B18-6）。</summary>
        private string? AppraiseCostBlocked(GameState s)
        {
            var lack = Ap.AppraiseConsume.Where(kv => AvailableCount(s, kv.Key, true) < kv.Value)
                .Select(kv => $"{ItemName(kv.Key)}{AvailableCount(s, kv.Key, true)}/{kv.Value}").ToList();
            return lack.Count > 0 ? "鉴定耗材不足（随身或当地地面）：" + string.Join("、", lack) : null;
        }

        private string AppraiseCostSource(GameState s) => string.Join("；", Ap.AppraiseConsume.Keys.Select(id =>
        {
            var carried = s.Items.Where(i => i.ItemId == id && i.IsCarried && i.ReservedBy == null && !IsLocked(s, i)).Sum(i => i.Count);
            var ground = s.Items.Where(i => i.ItemId == id && i.Owner == s.Ground && i.ReservedBy == null && !IsLocked(s, i)).Sum(i => i.Count);
            return $"{ItemName(id)}：随身{carried}、{ZoneName(s, s.Ground)}地面{ground}";
        }));

        /// <summary>鉴定所面板预览（R07 X3.6）：0 分钟、不提交、不生成报价表（报价表在首次安全访问时生成，见 EnsureAppraisalQuotes）。</summary>
        public AppraisalView AppraisalPreview(GameState s)
        {
            var svc = CityService(Ap.ServiceId);
            var v = new AppraisalView
            {
                ServiceId = Ap.ServiceId,
                LocationName = LocName(Ap.LocationId),
                ActorName = ActorDisplayName(s, GameState.PlayerId),
                Blocked = svc == null ? "这里现在不办理这项事项" : CityCommonBlocked(s, svc),
                AppraiseMinutes = Ap.AppraiseMinutes,
                AppraiseCost = string.Join("、", Ap.AppraiseConsume.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}")) + "（完成时才扣；样本不消耗）",
                FiberSourceText = AppraiseCostSource(s),
                FiberBlocked = AppraiseCostBlocked(s),
                TradeMinutes = Ap.TradeMinutes,
            };
            v.Status = v.Blocked ?? "可办理";
            foreach (var i in AppraisalSamples(s))
            {
                v.Samples.Add(new AppraisalSampleView
                {
                    InstanceId = i.InstanceId,
                    Name = SubstanceLabel(i),
                    QualityText = "白色",
                    SourceText = SampleSourceText(i),
                    Tutorial = IsTutorialSample(i),
                    Ordinary = IsOrdinarySample(i),
                    Count = i.Count,
                    HolderText = HolderOf(s, i),
                    Reserved = i.ReservedBy != null || IsLocked(s, i),
                    Appraised = i.Appraisal != null,
                    AppraisalText = AppraisalStateText(i),
                    FusionText = AppraisalFusionText(s, i),
                    AppraiseBlocked = AppraiseSampleBlocked(s, i),
                    TradeBlocked = TradeSampleBlocked(s, i),
                    DevSource = i.Source?.GrantEventId ?? "",
                });
            }
            FillTradePreview(s, v);
            if (ActiveCityTxn(s) is CityServiceTxn p && p.AppraisalMode != null)
            {
                var left = s.Main!.PlannedMinutes - s.Main.ElapsedMinutes;
                v.PendingText = p.AppraisalMode == AppraiseMode
                    ? $"详细鉴定进行中（还需{Clock.Duration(left)}）：样本与植物纤维已预留，完成才扣纤维、写记录"
                    : $"换物进行中（还需{Clock.Duration(left)}）：交出1份换{ItemName(p.OutputItemId ?? "")}×{p.ExactOutput}（第{p.QuoteDay}日报价），完成才交样、落地";
            }
            return v;
        }

        /// <summary>
        /// 教学样本免费说明（R07 X3.2：不要求先到鉴定所，0 分钟、不扣纤维、不推进时间）：首次查看在该实例写 fixed_tutorial_attack 记录（开发假设 B18-5），
        /// 之后只回看。只对主角随身或当前地面的教学样本。
        /// </summary>
        public ActionResult AppraisalTutorialNote(GameState s, int instanceId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (s.Progress.Dead) return ActionResult.Fail("角色已死亡");
            if (DialogueLocked(s)) return ActionResult.Fail(DialogueLockedReason);
            var i = FindItem(s, instanceId);
            if (i == null || i.ItemId != Ap.SampleItemId || !(i.IsCarried || i.Owner == s.Ground)) return ActionResult.Fail("这份样本不在随身或当前地面");
            if (!IsTutorialSample(i)) return ActionResult.Fail("这不是教学样本：普通样本的说明就在面板里，详细鉴定另需10分钟与植物纤维");
            RememberRequest(s, requestId);
            var first = i.Appraisal == null;
            if (first)
            {
                i.Appraisal = new AppraisalRecord { InstanceId = i.InstanceId, ScopeVersion = Ap.ScopeVersion, Result = AppraisalRecord.FixedTutorialAttack, Minute = s.GameMinute };
                Normalize(s, i.Owner);
            }
            var te = Config.ActiveSubstance.TutorialEffect;
            Emit(s, GameEventKind.Info, $"教学样本说明（免费，0分钟）：首战教学的活性物质，物质品质白色；{(s.Story.TutorialFusionDone ? "教学融合已完成，再融合按普通处理" : $"融合固定得绿色词条“{te.Label}”，只取得一次")}。"
                + "教学样本不可交易；直接融合的原入口照常可用。" + (first ? "" : "（已记录过，回看不扣任何东西）"));
            return ActionResult.Success();
        }

        /// <summary>
        /// 开始详细鉴定（R07 X3.2/X3.5、S01）：校验地点/空闲/样本/纤维 → 主行动 10 分钟 wait（同行者按等待结算）→ 预留所选 1 份与纤维，记事务锁。
        /// 关闭面板、换样本都 0 耗时；只有明确开始才开工。
        /// </summary>
        public ActionResult StartAppraise(GameState s, int instanceId, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            var svc = CityService(Ap.ServiceId);
            if (svc == null) return ActionResult.Fail("没有这项事项");
            if (CityCommonBlocked(s, svc) is string common) return ActionResult.Fail(common);
            var i = FindItem(s, instanceId);
            if (i == null || i.ItemId != Ap.SampleItemId || !(i.IsCarried || i.Owner == s.Ground)) return ActionResult.Fail("只能选主角随身或当前地面的活性物质");
            if (AppraiseSampleBlocked(s, i) is string why) return ActionResult.Fail(why);
            if (AppraiseCostBlocked(s) is string lack) return ActionResult.Fail(lack);
            var req = ActionRequest.Wait(Ap.AppraiseMinutes);
            req.TargetId = CityServicePrefix + svc.Id;
            req.RequestId = requestId;
            var res = Start(s, req);
            if (!res.Ok) return res;
            var act = s.Main!;
            act.Label = $"详细鉴定（{Clock.Duration(Ap.AppraiseMinutes)}）";
            var sample = ReserveSample(s, i, act.ActionId);
            act.ReservedItemIds.Add(sample.InstanceId);
            var txn = NewAppraisalTxn(s, act, AppraiseMode, sample);
            txn.ReservedItemIds.Add(sample.InstanceId);
            foreach (var kv in Ap.AppraiseConsume)
            {
                var ids = Reserve(s, kv.Key, kv.Value, act.ActionId);
                act.ReservedItemIds.AddRange(ids);
                txn.ReservedItemIds.AddRange(ids);
            }
            s.City.Pending = txn;
            return res;
        }

        /// <summary>预留所选样本的确切 1 份：整堆只有 1 份直接预留；否则拆出 1 份新实例（开发假设 B18-7）。返回被预留的实例。</summary>
        private ItemInstance ReserveSample(GameState s, ItemInstance i, int actionId)
        {
            if (i.Count <= 1)
            {
                i.ReservedBy = actionId;
                return i;
            }
            i.Count -= 1;
            var n = i.Clone();
            n.InstanceId = s.NextInstanceId++;
            n.Count = 1;
            n.ReservedBy = actionId;
            s.Items.Add(n);
            return n;
        }

        private CityServiceTxn NewAppraisalTxn(GameState s, ActionState act, string mode, ItemInstance sample) => new CityServiceTxn
        {
            TxnId = s.City.NextTxn++, ActionId = act.ActionId, ServiceId = Ap.ServiceId,
            NpcId = Config.City.Npcs.FirstOrDefault(n => n.ServiceIds.Contains(Ap.ServiceId))?.NpcId ?? "",
            LocationId = s.CurrentLocation, StartDay = Clock.Day(s.GameMinute),
            AppraisalMode = mode, SampleInstanceId = sample.InstanceId, SampleOwner = sample.Owner,
        };

        /// <summary>解除本行动的预留并整理所在位置的堆（拆出的样本份额回到原堆；已鉴定的不并回未鉴定）。</summary>
        private void ReleaseAppraisal(GameState s, ActionState a)
        {
            var owners = new HashSet<string>();
            foreach (var i in s.Items.Where(i => i.ReservedBy == a.ActionId).ToList())
            {
                i.ReservedBy = null;
                owners.Add(i.Owner);
            }
            a.ReservedItemIds.Clear();
            foreach (var o in owners) Normalize(s, o);
        }

        /// <summary>
        /// 鉴定/换物完成（段末，死亡优先已判过）：重验后原子提交；任何一项不符就安全失败——解除预留、原物原处、不扣耗材/样本/额度、不出物。
        /// </summary>
        private void CompleteAppraisal(GameState s, ActionState a, CityServiceTxn p, CityServiceConfig svc)
        {
            var reserved = s.Items.Where(i => i.ReservedBy == a.ActionId).ToList();
            var sample = reserved.FirstOrDefault(i => i.InstanceId == p.SampleInstanceId);
            string? fail = s.Progress.Dead ? "本局已结束" : s.CurrentLocation != p.LocationId ? $"不在{LocName(p.LocationId)}" : sample == null ? "所选样本已不在" : null;
            var made = "";
            if (fail == null && p.AppraisalMode == AppraiseMode)
            {
                if (Ap.AppraiseConsume.Any(kv => reserved.Where(i => i.ItemId == kv.Key && i.InstanceId != p.SampleInstanceId).Sum(i => i.Count) < kv.Value))
                    fail = "预留的鉴定耗材已不在";
                else if (sample!.Appraisal != null) fail = "这份样本已有鉴定记录";
                else
                {
                    foreach (var kv in Ap.AppraiseConsume)
                    {
                        var left = kv.Value;
                        foreach (var i in reserved.Where(i => i.ItemId == kv.Key && i.InstanceId != p.SampleInstanceId).ToList())
                            while (left > 0 && i.Count > 0) { ConsumeOne(s, i); left--; }
                    }
                    sample.Appraisal = new AppraisalRecord { InstanceId = sample.InstanceId, ScopeVersion = Ap.ScopeVersion, Result = AppraisalRecord.OrdinaryNoEffect, Minute = s.GameMinute };
                    made = $"{SubstanceLabel(sample)}已记录为普通白色、融合无属性（不升品、不产生新词条、不改变融合结果；样本仍在{HolderOf(s, sample)}）；消耗"
                           + string.Join("、", Ap.AppraiseConsume.Select(kv => $"{ItemName(kv.Key)}×{kv.Value}"));
                }
            }
            else if (fail == null && p.AppraisalMode == TradeMode) fail = CompleteTrade(s, p, sample!, out made);
            ReleaseAppraisal(s, a);
            var what = p.AppraisalMode == TradeMode ? "换物" : "详细鉴定";
            if (fail != null)
            {
                Emit(s, GameEventKind.ActionCompleted, $"{what}没有完成：{fail}（样本{(p.AppraisalMode == TradeMode ? "未交出、日额度未扣、没有产物" : "与植物纤维都未扣")}，原物在原处）。");
                return;
            }
            s.City.Completed++;
            Emit(s, GameEventKind.ActionCompleted, $"{what}完成：{made}。", null, p.OutputItemId != null ? new[] { p.OutputItemId } : null);
            CityServiceEnd(s, p.NpcId);
        }

        /// <summary>取消/中止时的日志（S01：只保留已流逝时间，不收费、不交样、不出物，释放预留；不套用制作返地）。</summary>
        private string AppraisalCancelText(ActionState a, CityServiceTxn? p) =>
            p?.AppraisalMode == TradeMode
                ? $"已停止换物（实际进行了{Clock.Duration(a.ElapsedMinutes)}）：样本未交出、仍在原处，日额度已释放，没有产物。"
                : $"已停止详细鉴定（实际进行了{Clock.Duration(a.ElapsedMinutes)}）：样本与植物纤维都未扣、仍在原处，没有写鉴定记录。";
    }
}

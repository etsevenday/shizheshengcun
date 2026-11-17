using System.Collections.Generic;
using System.Linq;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B51c2a1「被动设施取出 1：统一门槛＋陶窑／净水台」（S11「2026-11-17 B51c2、B47b与B52b三个完整原批」Q870—Q872、
    /// 「2026-11-17 B51c次要原批Q873—Q881」Q876—Q880、「2026-11-17 B51c2a／B51c2b完整两原批」Q921—Q923；S14 FAC-Q870-01、FAC-Q871-01／02、FAC-Q872-01／02、B51-Q778-02—06、B51-Q779-01）：
    /// <list type="bullet">
    /// <item>统一取出门槛 <see cref="FacilityTakeBlocked"/>：死亡 → 对白 → 该设施原战斗拒绝句 → 角色主／子行动（含暂停）「行动进行中，暂不能整理物品」（Q871 B）→ 不在当地。</item>
    /// <item>陶窑／净水台每批「取出」<see cref="TakeStationBatch"/>（Q870 B）与「全部取出」<see cref="TakeAll"/>（Q778）：0 世界分钟、不开行动；本批（各批）设施内原料放回设施所在区地面，
    /// 同一事务结束该批（没有产物和经验、已耗时间不退）；燃料只解锁、原地不动、不列清单（Q921，燃料库存待 B35a2a）。</item>
    /// <item>提交带计划快照戳（<see cref="StationTakeStamp"/>）与请求号：计划变了整次拒「物品已变，请重选。」，同一请求号只生效一次。</item>
    /// <item>结果「已取出至{地点}地面：{物品清单}。」（Q876 B：名×数、同名合量、顿号；地点为设施所在区名），不接中止批数／成本尾句（Q878）、不接保质恢复句（Q882）。</item>
    /// </list>
    /// 晾架与火堆的取出、全部取出属于 B51c2a2；卡片箭头、数量窗、拖出与余料属于 B51c2b。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>每批按钮（S11 设施按钮及反馈；逐字稿 B51c2 节）。</summary>
        public const string TakeLabel = "取出";
        /// <summary>操作区按钮（逐字稿 B51 表 takeAll）。</summary>
        public const string TakeAllLabel = "全部取出";
        /// <summary>Q871 B：角色主／子行动进行或暂停时取出的拒绝句（沿原「行动进行中，暂不能整理物品」）。</summary>
        public const string TakeBusyText = "行动进行中，暂不能整理物品";
        /// <summary>逐字稿 B51 表：没有任何本来合法可移实物。</summary>
        public const string TakeEmptyText = "没有可取出的物品。";
        /// <summary>过时计划（S11 Q880「独立有效的快照变化仍用原“物品已变，请重选。”」）。</summary>
        public const string TakeChangedText = DiscardCopy.Changed;
        /// <summary>不在设施所在区（沿火堆取出原句「异地不能取用设施里的物品」）。</summary>
        public const string TakeElsewhereText = "异地不能取用设施里的物品";

        /// <summary>本批支持「全部取出」的设施（陶窑、固定净水台）；晾架、火堆随 B51c2a2 加入。</summary>
        public static bool TakeAllSupported(string facility) => facility is "kiln" or "water_station";

        /// <summary>
        /// 四类被动设施取出的统一门槛（Q871 B）：依次 死亡、对白、该设施原战斗拒绝句、角色主／子行动（进行或暂停）、不在设施所在区；能取为 null。
        /// 设施自己的被动加工不算角色忙。battleText 为该设施原有的战斗拒绝句。
        /// </summary>
        public string? FacilityTakeBlocked(GameState s, string place, string battleText)
        {
            if (s.Progress.Dead) return "本局已结束";
            if (DialogueLocked(s)) return DialogueLockedReason;
            if (s.Battle != null) return battleText;
            if (s.Main != null || s.Sub != null) return TakeBusyText;
            if (place != s.Ground) return TakeElsewhereText;
            return null;
        }

        /// <summary>陶窑／净水台的取出门槛（原战斗句「战斗时不能操作」，BM5a Q563）。</summary>
        public string? StationTakeBlocked(GameState s, string place) => FacilityTakeBlocked(s, place, StationBattleBlocked);

        /// <summary>这次取出的计划：所涉每批及其在设施内的原料（batchId 为 null＝这座设施全部批）。燃料不在计划里（Q921）。</summary>
        private List<(StationBatch Batch, List<ItemInstance> Inputs)> StationTakePlan(GameState s, string place, string stationId, int? batchId) =>
            StationList(FacOf(s, place), stationId)
                .Where(b => batchId == null || b.BatchId == batchId.Value)
                .Select(b => (b, StationBatchInputs(s, place, b)))
                .ToList();

        /// <summary>
        /// 计划快照戳（只读）：设施所在区、设施、范围，以及所涉各批的批次 ID 与批内每份原料的实例号、份数。渲染按钮时取一次随请求提交，
        /// 提交时重算不同就整次拒「物品已变，请重选。」。不含进度（正常推进不算计划变化）。
        /// </summary>
        public string StationTakeStamp(GameState s, string place, string stationId, int? batchId = null) =>
            $"{place}|{stationId}|{(batchId?.ToString() ?? "*")}|" + string.Join(";", StationTakePlan(s, place, stationId, batchId)
                .Select(p => $"{p.Batch.BatchId}:" + string.Join(",", p.Inputs.Select(i => $"{i.InstanceId}x{i.Count}"))));

        /// <summary>这一批「取出」现在不能做的原因（门槛→批已不在→批内没有可取出的原料）；能做为 null。</summary>
        public string? StationBatchTakeBlocked(GameState s, string place, string stationId, int batchId)
        {
            if (StationTakeBlocked(s, place) is string why) return why;
            var plan = StationTakePlan(s, place, stationId, batchId);
            if (plan.Count == 0) return TakeChangedText;
            return plan.All(p => p.Inputs.Count == 0) ? TakeEmptyText : null;
        }

        /// <summary>「全部取出」现在不能做的原因（门槛→没有可取出的物品）；能做为 null。只支持陶窑／净水台（B51c2a1）。</summary>
        public string? TakeAllBlocked(GameState s, string place, string facility)
        {
            if (!TakeAllSupported(facility)) return TakeEmptyText;
            if (StationTakeBlocked(s, place) is string why) return why;
            return StationTakePlan(s, place, facility, null).All(p => p.Inputs.Count == 0) ? TakeEmptyText : null;
        }

        /// <summary>
        /// 陶窑／净水台的一批「取出」（Q870 B）：只结束所点的这一批（稳定 batchId），批内原料放回设施所在区地面，燃料只解锁；其他批不动。0 分钟。
        /// stamp 为渲染时的 <see cref="StationTakeStamp"/>（本批范围）；批已结束或计划变了拒「物品已变，请重选。」。同一 requestId 只生效一次。
        /// </summary>
        public ActionResult TakeStationBatch(GameState s, string place, string stationId, int batchId, string stamp, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (StationTakeBlocked(s, place) is string why) return ActionResult.Fail(why);
            var plan = StationTakePlan(s, place, stationId, batchId);
            if (plan.Count == 0 || StationTakeStamp(s, place, stationId, batchId) != stamp) return ActionResult.Fail(TakeChangedText);
            if (plan.All(p => p.Inputs.Count == 0)) return ActionResult.Fail(TakeEmptyText);
            return CommitStationTake(s, place, plan, requestId);
        }

        /// <summary>
        /// 「全部取出」（Q778）：本批只支持陶窑／净水台——这座设施全部在办批的原料一次放回设施所在区地面，各批同一事务结束（无产物、无经验、已耗时间不退），
        /// 燃料只解锁、原地不动、不列清单（Q921）。0 分钟。stamp 为渲染时的 <see cref="StationTakeStamp"/>（全部范围），计划变了整次拒「物品已变，请重选。」；
        /// 没有可取出的原料拒「没有可取出的物品。」。同一 requestId 只生效一次。
        /// </summary>
        public ActionResult TakeAll(GameState s, string place, string facility, string stamp, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (!TakeAllSupported(facility)) return ActionResult.Fail(TakeEmptyText);
            if (StationTakeBlocked(s, place) is string why) return ActionResult.Fail(why);
            if (StationTakeStamp(s, place, facility, null) != stamp) return ActionResult.Fail(TakeChangedText);
            var plan = StationTakePlan(s, place, facility, null).Where(p => p.Inputs.Count > 0).ToList();
            if (plan.Count == 0) return ActionResult.Fail(TakeEmptyText);
            return CommitStationTake(s, place, plan, requestId);
        }

        /// <summary>执行取出计划：各批先移出设施列表再释放（原料回地面、燃料解锁、恢复 C＋R），合堆，记请求号，写一条结果。</summary>
        private ActionResult CommitStationTake(GameState s, string place, List<(StationBatch Batch, List<ItemInstance> Inputs)> plan, string? requestId)
        {
            var fac = FacOf(s, place)!;
            var moved = new List<(string Name, int Count)>();
            foreach (var (b, _) in plan)
            {
                fac.StationBatches.Remove(b);
                foreach (var it in ReleaseStationBatch(s, place, b)) moved.Add((ItemName(it.ItemId), it.Count));
            }
            Normalize(s, place);
            RememberRequest(s, requestId);
            Emit(s, GameEventKind.Info, TakeResultText(ZoneName(s, place), moved));
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>Q876 B：「已取出至{地点}地面：{物品清单}。」——同名按真实移出量合为「{物名}×{数量}」，按首次出现先后，顿号连接。</summary>
        public static string TakeResultText(string zone, IEnumerable<(string Name, int Count)> moved)
        {
            var list = new List<(string Name, int Count)>();
            foreach (var (name, n) in moved)
            {
                var k = list.FindIndex(x => x.Name == name);
                if (k < 0) list.Add((name, n)); else list[k] = (name, list[k].Count + n);
            }
            return $"已取出至{zone}地面：{string.Join("、", list.Select(x => $"{x.Name}×{x.Count}"))}。";
        }
    }
}

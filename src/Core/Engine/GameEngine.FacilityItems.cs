using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    /// <summary>
    /// B51c2b「被动设施卡片箭头、余料与拖入」（S11／S14「2026-11-17 B51c2、B47b与B52b三个完整原批」§Q870—Q872 快捷箭头、
    /// 「2026-11-17 B51c2a／B51c2b完整两原批」§Q924 B、§Q925 B、§Q926 A、§Q927 B；S07 同节；验收 FAC-Q870-02—04、FACF-Q924-01—Q927-02）：
    /// <list type="bullet">
    /// <item>晾制位、陶窑／固定净水台批内的真实物品卡：卡片箭头（1 份到随身，长按同卡）、卡本体数量取出（取到随身／取到地面）、拖出到随身／当前地面，
    /// 都走同一事务 <see cref="TakeFacilityItem"/>；门槛同 Q871 B 统一取出门槛（角色主／子行动进行或暂停都不能取）。</item>
    /// <item>容量按真实释放 C＋R 后的合堆、五卡与 30 公斤重验，失败不移、不中止、不恢复保质，不自动改落地（FAC-Q870-03）。</item>
    /// <item>取走在制原料的任一部分，同一事务结束所属位／批（无成品无经验、已耗时间不退、陶窑燃料只解锁一次）；未移余料解锁留原位、恢复 C＋R、仍占位（FAC-Q870-04），
    /// 晾制位改为 <see cref="DryingSlot.Residual"/>、设施批标 <see cref="StationBatch.Residual"/>；补投时余料先用（Q924 B，见 GameEngine.Drying.cs／Stations.cs）。</item>
    /// </list>
    /// 绑定按下／打开数量窗时的真实设施位／批与卡实例（<see cref="FacilityItemStamp"/>）：位换成新批、卡已不在原位都拒「物品已变，请重选。」，不改取别卡。
    /// </summary>
    public sealed partial class GameEngine
    {
        /// <summary>S11 §Q924 B「短状态可用“余料”」：余料位／余料条目的短状态。</summary>
        public const string ResidualText = "余料";

        /// <summary>这件物品在晾制位或陶窑／净水台批内（B51c2b 的设施物品卡）。</summary>
        public static bool IsFacilityItem(ItemInstance i) => i.InDrying || i.InStation;

        /// <summary>
        /// 设施物品卡的身份戳（只读）：物主（区＋晾制位或设施批）＋所在位的批 ID（晾制位物主按位号，批 ID 区分同一位上的新旧批）＋卡实例号。
        /// 不含份数（长按同卡连续取、份数变化不算换卡）；份数超出现有在提交时另拒。
        /// </summary>
        public string FacilityItemStamp(GameState s, ItemInstance i) =>
            ItemInstance.TryDrying(i.Owner, out var place, out var slot)
                ? $"{i.Owner}|{DryingSlotAt(s, place, slot)?.BatchId}|{i.InstanceId}"
                : $"{i.Owner}|{i.InstanceId}";

        /// <summary>
        /// 设施物品卡取出的门槛（不看份数与去向）：该设施的统一取出门槛（Q871 B：死亡、对白、该设施原战斗句、角色主／子行动进行或暂停「行动进行中，暂不能整理物品」、不在当地），
        /// 再要求所属晾制位／设施批仍在；能取为 null。
        /// </summary>
        public string? FacilityItemGate(GameState s, ItemInstance i)
        {
            if (ItemInstance.TryDrying(i.Owner, out var dplace, out var slot))
            {
                if (DryingTakeGate(s, dplace) is string dry) return dry;
                return DryingSlotAt(s, dplace, slot) == null ? TakeChangedText : null;
            }
            if (ItemInstance.TryStation(i.Owner, out var splace, out var batchId))
            {
                if (StationTakeBlocked(s, splace) is string st) return st;
                return StationBatchAt(s, splace, batchId) == null ? TakeChangedText : null;
            }
            return TakeChangedText;
        }

        /// <summary>按「现在释放」恢复保质后的副本（C＋R；无暂停的原样），只用于容量预演，不改状态。</summary>
        private static ItemInstance ResumedCopy(GameState s, ItemInstance i)
        {
            var c = i.Clone();
            c.ExpiresAt = EffectiveExpiry(s, i);
            c.ShelfPausedMinutes = null;
            return c;
        }

        /// <summary>取到 zone 最多几份：随身按真实 C＋R 后的合堆、五卡与 30 公斤；当前地面不限（这张卡的全部份数）。</summary>
        public int FacilityItemMax(GameState s, ItemInstance i, string zone) =>
            zone == ItemInstance.Carried ? MaxPickupBatch(s, ResumedCopy(s, i), i.Count) : i.Count;

        /// <summary>
        /// 把这张设施物品卡的 count 份取到 zone（随身或当前地面）现在不能做的原因：门槛 → 去向 → 份数 → 在制原料已腐败（旧档未受保护）→ 随身容量
        /// （沿原拾取拒绝句：第六张卡／超过 30 公斤，并写最多可取几份；不自动改落地）；能做为 null。
        /// </summary>
        public string? FacilityItemTakeBlocked(GameState s, ItemInstance i, int count, string zone)
        {
            if (FacilityItemGate(s, i) is string gate) return gate;
            if (zone != ItemInstance.Carried && zone != s.Ground) return "只能取到随身或当前地面";
            if (count < 1) return "数量无效";
            if (count > i.Count) return TakeChangedText;
            if (ItemInstance.TryDrying(i.Owner, out var place, out var slot) && DryingSlotAt(s, place, slot) is { Status: DryingSlot.Drying } && IsSpoiled(s, i))
                return DryingSpoiledInputText;
            return zone == ItemInstance.Carried ? PickupBlockedText(s, ResumedCopy(s, i), count) : null;
        }

        /// <summary>
        /// 从晾制位／陶窑净水台批内取出这张卡的 count 份到 zone（随身或当前地面），0 世界分钟、不开行动（卡片箭头、数量取出、拖出共用）。
        /// stamp 为按下／打开数量窗时的 <see cref="FacilityItemStamp"/>，不符整次拒「物品已变，请重选。」；容量等失败什么都不变。
        /// 成功：移出的份数恢复 C＋R；取的是在制原料时同一事务结束所属位／批——晾制位剩下的原料留本位成余料、陶窑／净水台批剩下的原料留批内成余料条目，
        /// 都解锁并恢复 C＋R、仍占位；陶窑燃料只解锁一次；无成品、无经验、已耗时间不退；位／批取空即空出。结果沿火堆单件取出原句「把{物名}从{位置}取出到{去向}。」，
        /// 不报中止批数（Q877／Q878）。
        /// </summary>
        public ActionResult TakeFacilityItem(GameState s, int instanceId, int count, string zone, string stamp)
        {
            var i = FindItem(s, instanceId);
            if (i == null || !IsFacilityItem(i)) return ActionResult.Fail(TakeChangedText);
            if (FacilityItemGate(s, i) is string gate) return ActionResult.Fail(gate);
            if (FacilityItemStamp(s, i) != stamp) return ActionResult.Fail(TakeChangedText);
            if (FacilityItemTakeBlocked(s, i, count, zone) is string why) return ActionResult.Fail(why, zone == ItemInstance.Carried ? FacilityItemMax(s, i, zone) : (int?)null);
            var from = WherePath(s, i);
            var owner = i.Owner;
            var name = count <= 1 ? ItemName(i.ItemId) : $"{ItemName(i.ItemId)}×{count}";
            var moved = count < i.Count ? SplitOff(s, i, count) : i;
            ResumeShelf(s, moved);
            moved.Owner = zone;
            moved.PlacedOrder = null;
            moved.ReservedBy = null;
            Normalize(s, zone);
            if (ItemInstance.TryDrying(owner, out var dplace, out var slot)) SettleDryingAfterTake(s, dplace, slot);
            else if (ItemInstance.TryStation(owner, out var splace, out var batchId)) SettleStationAfterTake(s, splace, batchId);
            var to = zone == ItemInstance.Carried ? "随身" : $"{LocName(zone)}地面";
            Emit(s, GameEventKind.Info, $"把{name}从{from}取出到{to}。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>
        /// 晾制位被取走物品之后：位已空即空出这一位（任何状态）；晾制中的位同一事务结束这一批（Q870 B／Q924 B）——剩下的原料成余料：解锁、恢复 C＋R，
        /// 位改为余料（不计晾制中、不结算、无成品无经验），仍占这一位。已晾好／已停止的位只是少了物品。
        /// </summary>
        private void SettleDryingAfterTake(GameState s, string place, int slot)
        {
            var fac = FacOf(s, place)!;
            var b = DryingSlotAt(s, place, slot);
            if (b == null) return;
            var rest = DryingContents(s, place, slot);
            if (rest.Count == 0)
            {
                fac.DryingSlots.Remove(b);
                return;
            }
            if (b.Status != DryingSlot.Drying) return;
            b.Status = DryingSlot.Residual;
            b.EndMinute = s.GameMinute;
            b.ShelfPaused = false;
            foreach (var it in rest)
            {
                ResumeShelf(s, it);
                it.ReservedBy = null;
            }
        }

        /// <summary>
        /// 陶窑／净水台批被取走原料之后：在办批同一事务结束（燃料只解锁一次、原地不动；剩下原料解锁、恢复 C＋R，标为余料条目，仍计入 10 条上限、不计加工中、不结算）；
        /// 批内已空即删掉这条记录。
        /// </summary>
        private void SettleStationAfterTake(GameState s, string place, int batchId)
        {
            var fac = FacOf(s, place)!;
            var b = StationBatchAt(s, place, batchId);
            if (b == null) return;
            if (!b.Residual)
            {
                ReleaseStationFuel(s, b);
                b.Residual = true;
                foreach (var it in StationBatchInputs(s, place, b)) ResumeReleased(s, it);
            }
            if (StationBatchInputs(s, place, b).Count == 0) fac.StationBatches.Remove(b);
        }

        /// <summary>
        /// 设施物品卡拖到随身／当前地面（S07／S11 §Q925 B）：沿原起拖与 Shift 数量，松手按真实来源身份与目的地重验；跨区默认 1 份，Shift 上限为 <see cref="FacilityItemMax"/>。
        /// 落点提示沿原「拾取1份{物名}到随身」「取出1份{物名}到地面」。落在随身或当前地面的卡上等同落在该区；其他落点不响应。
        /// </summary>
        private DropIntent FacilityItemDrop(GameState s, ItemInstance head, DropTarget dst)
        {
            string? zone = dst.Kind switch
            {
                DropTargetKind.Zone => dst.Id,
                DropTargetKind.Card => dst.Id.Split('|')[0],
                _ => null,
            };
            if (zone == null) return DropIntent.No(Unsupported);
            if (zone != ItemInstance.Carried && zone != s.Ground) return DropIntent.No("只能放到随身或当前地面");
            if (FacilityItemTakeBlocked(s, head, 1, zone) is string why) return DropIntent.No(why);
            var toCarried = zone == ItemInstance.Carried;
            return Instant(DropVerb.TakeOut, $"{(toCarried ? "拾取" : "取出")}1份{InstanceName(head)}到{(toCarried ? "随身" : "地面")}",
                InstantRequest.FacilityTake(head.InstanceId, 1, zone, FacilityItemStamp(s, head)), FacilityItemMax(s, head, zone));
        }

        /// <summary>
        /// 材料卡拖到晾制位（S11／S07 §Q926 A）：只做导航——选中这一位并定位到用这件物品的原合法食谱预览，仍要原「投料…」「确认投料」才迁入开工；
        /// 不预留、不搬物、不推进时间。空位列出用这件物品的晾制食谱（多条时都列，不代选）；正常占用位（在制、成品、停止、不能补投的余料位）拒「已满」；
        /// 同配方可补投的余料位（Q924 B）进该位的补投预览。落点提示沿空位原按钮「查看配方」（Q363）。
        /// </summary>
        private DropIntent DropOnDryingSlot(GameState s, DropSource src, ItemInstance head, string id)
        {
            if (!int.TryParse(id, out var k) || k < 0 || k >= DryingSlotCount) return DropIntent.No(Unsupported);
            if (!DryingRackHere(s)) return DropIntent.No($"{ZoneName(s, s.Ground)}没有晾制架（只能操作当前区的晾制架）");
            if (src.Kind != DropSourceKind.Card || head.InFacility || head.InContainer) return DropIntent.No(Unsupported);
            var recipes = DryingRecipes.Where(r => r.Inputs.ContainsKey(head.ItemId)).ToList();
            if (recipes.Count == 0) return DropIntent.No(Unsupported);
            if (head.ReservedBy != null || IsSpoiled(s, head) || IsSpoiledMatter(head)) return DropIntent.No("这批材料不可用");
            if (DryingSlotAt(s, s.Ground, k) is DryingSlot b)
            {
                var refill = recipes.FirstOrDefault(r => DryingRefillable(s, s.Ground, b, r));
                if (refill == null) return DropIntent.No(DryingFullText);
                recipes = new List<RecipeConfig> { refill };
            }
            return new DropIntent { Ok = true, Verb = DropVerb.PreviewDrying, Label = "查看配方", Recipes = recipes.Select(r => r.Id).ToList(), Slot = k };
        }
    }
}

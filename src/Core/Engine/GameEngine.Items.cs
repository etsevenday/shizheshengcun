using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    public sealed partial class GameEngine
    {
        // ───────────────────────── 物品（搜集案 05） ─────────────────────────

        /// <summary>
        /// 已到期且不在受热保护中（Q16、S11 §10）：设施/锅里的新鲜食材实际完整受热一段后受本次加工保护，原 expiresAt 不再判腐败；
        /// 取出（进程移除）即按原 expiresAt 立即判断。保护不改写 ExpiresAt。
        /// </summary>
        public bool IsSpoiled(GameState s, ItemInstance i) =>
            i.ExpiresAt.HasValue && i.ExpiresAt.Value <= s.GameMinute && !IsHeatProtected(s, i);

        /// <summary>该物品正受本次烹饪加工保护（只可能在火堆加工位或火堆里的锅中）。</summary>
        public bool IsHeatProtected(GameState s, ItemInstance i) =>
            (i.InFacility || i.InContainer) && AllProcesses(s).Any(p => p.HeatProtected && p.TargetInstanceId == i.InstanceId);

        /// <summary>受保护且原保质期已过：取出后会立即腐败（界面提前写明后果）。</summary>
        public bool PastExpiryButProtected(GameState s, ItemInstance i) =>
            i.ExpiresAt.HasValue && i.ExpiresAt.Value <= s.GameMinute && IsHeatProtected(s, i);

        public ItemInstance? FindItem(GameState s, int instanceId) => s.Items.FirstOrDefault(i => i.InstanceId == instanceId);

        /// <summary>
        /// 物品实际所在的根位置：随身或某地点。锅内物品取锅所在，设施内物品取设施所在地点（火堆 → 锅 → 水/食材，最多两层）。
        /// </summary>
        public string PlaceOf(GameState s, ItemInstance i)
        {
            var owner = i.Owner;
            for (var depth = 0; depth < 4; depth++)
            {
                if (ItemInstance.TryContainerId(owner, out var potId))
                {
                    var pot = FindItem(s, potId);
                    if (pot == null) return "";
                    owner = pot.Owner;
                    continue;
                }
                if (ItemInstance.TryFacilityId(owner, out var fid)) return FacilityLocation(fid) ?? "";
                // 穿戴物在本人身上：根位置同该角色的随身（不占五卡，B3 H3）
                if (ItemInstance.TryEquip(owner, out var actorId, out _)) return ItemInstance.CarriedOwner(actorId);
                return owner;
            }
            return "";
        }

        /// <summary>一件物品连同全部后代（锅里的水与食材）的克重，每件只计一次。</summary>
        public int ItemGrams(GameState s, ItemInstance i)
        {
            var g = _items[i.ItemId].WeightGrams * i.Count;
            if (IsPot(i)) g += PotContents(s, i.InstanceId).Sum(c => ItemGrams(s, c));
            // 弹仓内弹药属于武器内容，计重一次（v0.5 §05）
            if (i.LoadedAmmo > 0 && i.LoadedAmmoId != null && _items.TryGetValue(i.LoadedAmmoId, out var ammo)) g += ammo.WeightGrams * i.LoadedAmmo;
            return g;
        }

        /// <summary>
        /// 主角本人的携重：随身物品与穿戴物（七槽）连同后代全部相加（S06 全系统更新目标 §1“穿戴与随身及后代重量全部相加”、B3 H3）。
        /// 20/30 公斤线按它判断；N/5 张卡另按 RootCardCount（不含穿戴）。
        /// </summary>
        public int CarriedGrams(GameState s) => CarriedGramsOf(s, GameState.PlayerId);

        /// <summary>某角色本人的携重：该角色随身 owner 与穿戴 owner 的物品连同后代（队友随身为 "carried:&lt;actorId&gt;"，本批没有队友物品）。</summary>
        public int CarriedGramsOf(GameState s, string actorId)
        {
            var carried = ItemInstance.CarriedOwner(actorId);
            return s.Items.Where(i => i.Owner == carried || i.IsWornBy(actorId)).Sum(i => ItemGrams(s, i));
        }

        /// <summary>主角身上的穿戴物合计克重（左栏悬停“含穿戴 x kg”）。</summary>
        public int WornGrams(GameState s, string actorId = GameState.PlayerId) =>
            s.Items.Where(i => i.IsWornBy(actorId)).Sum(i => ItemGrams(s, i));

        /// <summary>在主角身上：随身或主角穿戴（工具查找、维护对象等“本人可用”判断用，B3 H3）。</summary>
        public static bool OnPlayer(ItemInstance i) => i.IsCarried || i.IsWornBy(GameState.PlayerId);

        public bool IsOverweight(GameState s) => CarriedGrams(s) > Config.Carry.SoftGrams;

        // ───────────────────────── 随身五张根物品卡（S06 §8） ─────────────────────────

        /// <summary>随身最多几张根物品卡（配置 carry.maxRootCards，策划定 5）。</summary>
        public int MaxRootCards => Config.Carry.MaxRootCards;

        /// <summary>合堆键：同物品、同到期、同来源事件（与 Normalize 一致）。</summary>
        private static (string, int?, string?) StackKey(ItemInstance i) => (i.ItemId, i.ExpiresAt, i.Source?.GrantEventId);

        /// <summary>
        /// 真实批次键（Q24，S06 X4）：到期 + 来源（发放事件，与合堆键同一字段）+ 预留状态。现有存档字段能区分的只有这三项，
        /// “鉴定状态”目前没有对应字段（开发假设 B1-H2），不新增存档字段。形如“1500|grant:7|”“-||r”。
        /// </summary>
        public static string BatchKeyOf(ItemInstance i) =>
            $"{(i.ExpiresAt.HasValue ? i.ExpiresAt.Value.ToString() : "-")}|{i.Source?.GrantEventId ?? ""}|{(i.ReservedBy != null ? "r" : "")}";

        /// <summary>
        /// 某处同一物品的真实批次（Q24）：最早到期在前、无保质在后，同期限按批内最小实例号稳定排序。每批列出全部堆、合计份数、
        /// 是否预留、是否已腐败；只读，不合并、不改占格（占格仍按 RootCardsOf 的合堆键计）。
        /// </summary>
        public List<ItemBatch> BatchesOf(GameState s, string owner, string itemId) =>
            s.Items.Where(i => i.Owner == owner && i.ItemId == itemId)
                .GroupBy(BatchKeyOf)
                .Select(g =>
                {
                    var stacks = g.OrderBy(i => i.InstanceId).ToList();
                    var head = stacks[0];
                    return new ItemBatch
                    {
                        Key = g.Key,
                        ItemId = itemId,
                        Owner = owner,
                        ExpiresAt = head.ExpiresAt,
                        GrantEventId = head.Source?.GrantEventId,
                        Reserved = head.ReservedBy != null,
                        Spoiled = stacks.All(i => IsSpoiled(s, i)),
                        Count = stacks.Sum(i => i.Count),
                        Stacks = stacks,
                    };
                })
                .OrderBy(b => b.ExpiresAt ?? int.MaxValue)
                .ThenBy(b => b.Stacks[0].InstanceId)
                .ToList();

        /// <summary>默认批次（Q24“默认指向最前合法可用批次”）：最前一个未预留、未腐败的批；都不可用时取最前未预留的，再不行取最前一批。</summary>
        public static ItemBatch? DefaultBatch(IList<ItemBatch> batches) =>
            batches.FirstOrDefault(b => b.Usable) ?? batches.FirstOrDefault(b => !b.Reserved) ?? batches.FirstOrDefault();

        /// <summary>
        /// 随身根物品卡数（开发假设 H1）：可堆叠物品按合堆键分批，每批 ceil(数量/堆叠上限) 张；工具/武器/锅每件 1 张（锅连同内容、
        /// 武器连同弹仓都只算 1 张）。制作预留临时拆出的堆仍按所在批次合计，不算新卡。
        /// </summary>
        public int RootCardCount(GameState s) => RootCardsAfter(s, null, 0, null, 0);

        /// <summary>
        /// 一次转移后的随身根卡数（只读预演，H2）：add 为将进入随身的物品（addCount 份；锅/工具等整件进入时按 1 张），
        /// remove 为将离开随身的物品（removeCount 份）。
        /// </summary>
        public int RootCardsAfter(GameState s, ItemInstance? add, int addCount, ItemInstance? remove, int removeCount)
        {
            var stacks = new Dictionary<(string, int?, string?), int>();
            var singles = 0;
            foreach (var i in s.Items.Where(i => i.IsCarried))
            {
                var n = i.Count;
                if (remove != null && i.InstanceId == remove.InstanceId) n -= Math.Min(n, removeCount);
                if (n <= 0) continue;
                if (StacksAsBatch(i)) { var k = StackKey(i); stacks[k] = (stacks.TryGetValue(k, out var c) ? c : 0) + n; }
                else singles++;
            }
            if (add != null && addCount > 0 && !add.IsCarried)
            {
                if (StacksAsBatch(add)) { var k = StackKey(add); stacks[k] = (stacks.TryGetValue(k, out var c) ? c : 0) + addCount; }
                else singles++;
            }
            return singles + stacks.Sum(kv => (kv.Value + _items[kv.Key.Item1].StackMax - 1) / Math.Max(1, _items[kv.Key.Item1].StackMax));
        }

        private bool StacksAsBatch(ItemInstance i) => _items[i.ItemId].IsStackable && !IsPot(i);

        /// <summary>这次进入随身后根卡数是否允许：不超过上限；旧档已超限时不得再增加（S06 §8）。</summary>
        public bool RootCardsAllow(GameState s, int after) => after <= Math.Max(MaxRootCards, RootCardCount(s));

        /// <summary>“随身卡位已满（5/5张），……”拒绝原因。</summary>
        public string RootCardsFullText(GameState s) =>
            $"随身卡位已满（{RootCardCount(s)}/{MaxRootCards}张）：会多出一张新卡，请先放下或用掉一张";

        /// <summary>
        /// 从某处取 1..upTo 份进随身时，按根卡上限最多能取几份（单调：份数越多卡数不减）。整件物品（锅、工具、武器）为 0 或 1。
        /// </summary>
        public int MaxIntoCarriedByCards(GameState s, ItemInstance inst, int upTo)
        {
            if (upTo <= 0) return 0;
            if (!StacksAsBatch(inst)) return RootCardsAllow(s, RootCardsAfter(s, inst, 1, null, 0)) ? Math.Min(1, upTo) : 0;
            var lo = 0;
            var hi = upTo;
            while (lo < hi)
            {
                var mid = (lo + hi + 1) / 2;
                if (RootCardsAllow(s, RootCardsAfter(s, inst, mid, null, 0))) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        /// <summary>随身根卡超过上限（旧档或调试物资）：需要在安全空闲时整理（S06 §8、开发假设 H3）。</summary>
        public bool CarriedOverflow(GameState s) => RootCardCount(s) > MaxRootCards;

        /// <summary>
        /// 现在能否做超额整理：安全空闲——未死亡、无战斗/敌踪、无主/子行动（含暂停）、无未读对白。战斗/移动/子行动中先保留原状，
        /// 等安全空闲再整理（S06 §8）。
        /// </summary>
        public bool OverflowResolvable(GameState s) =>
            !s.Progress.Dead && s.Battle == null && s.Encounter == null && s.Main == null && s.Sub == null && !DialogueLocked(s);

        /// <summary>超额整理窗列出的随身根物品（未预留的随身实例，按卡片顺序）。</summary>
        public List<ItemInstance> CarriedRootItems(GameState s) =>
            s.Items.Where(i => i.IsCarried && i.ReservedBy == null).OrderBy(i => i.InstanceId).ToList();

        /// <summary>只保留这些随身实例时的根卡数（同批多堆合计、按堆叠上限折算）。</summary>
        public int RootCardsOf(GameState s, IEnumerable<int> instanceIds)
        {
            var ids = new HashSet<int>(instanceIds);
            var stacks = new Dictionary<(string, int?, string?), int>();
            var singles = 0;
            foreach (var i in s.Items.Where(i => i.IsCarried && ids.Contains(i.InstanceId)))
            {
                if (StacksAsBatch(i)) { var k = StackKey(i); stacks[k] = (stacks.TryGetValue(k, out var c) ? c : 0) + i.Count; }
                else singles++;
            }
            return singles + stacks.Sum(kv => (kv.Value + _items[kv.Key.Item1].StackMax - 1) / Math.Max(1, _items[kv.Key.Item1].StackMax));
        }

        /// <summary>
        /// 超额整理（S06 §8、开发假设 H3）：玩家明确选择保留的随身实例（合计不超过上限），其余一次性原子移到当前地面；
        /// 不删除、不自动合错批次、不随机丢物。仅安全空闲、确实超额时可用；校验失败什么都不变。带 requestId 幂等。
        /// </summary>
        public ActionResult ResolveCarriedOverflow(GameState s, IList<int> keepInstanceIds, string? requestId = null)
        {
            if (SeenRequest(s, requestId)) return ActionResult.Repeated();
            if (!CarriedOverflow(s)) return ActionResult.Fail($"随身没有超过{MaxRootCards}张卡，不需要整理");
            if (!OverflowResolvable(s)) return ActionResult.Fail("现在不能整理随身物品：请先结束战斗、行动或对话");
            var roots = CarriedRootItems(s);
            var keep = new HashSet<int>(keepInstanceIds);
            if (keep.Any(id => roots.All(i => i.InstanceId != id))) return ActionResult.Fail("选择的物品已不在随身");
            var cards = RootCardsOf(s, keep);
            if (cards > MaxRootCards) return ActionResult.Fail($"保留{cards}张，超过{MaxRootCards}张上限");
            var here = s.CurrentLocation;
            var moved = roots.Where(i => !keep.Contains(i.InstanceId)).ToList();
            if (moved.Any(i => IsLocked(s, i))) return ActionResult.Fail("行动占用：有物品正被行动使用");
            foreach (var i in moved) i.Owner = here;
            Normalize(s, here);
            Normalize(s, ItemInstance.Carried);
            RememberRequest(s, requestId);
            var names = moved.GroupBy(i => i.ItemId).Select(g => $"{ItemName(g.Key)}×{g.Sum(i => i.Count)}");
            Emit(s, GameEventKind.Info, $"整理随身：保留{RootCardCount(s)}张卡，{string.Join("、", names)}放到{LocName(here)}地面。");
            UpdateFlags(s);
            return ActionResult.Success();
        }

        /// <summary>
        /// 随身与（可选）当前地面的物品；includeContainers 时也包括这些位置（含当前火堆里）锅中的水（饮用、引导统计用）。
        /// 设施与锅里的食材是独立来源，不在这里自动取用（统一卡片案第 6 节）。
        /// </summary>
        public IEnumerable<ItemInstance> Accessible(GameState s, bool allowGround, bool includeContainers = false) =>
            s.Items.Where(i =>
            {
                if (i.InFacility) return false;
                if (i.InContainer)
                {
                    if (!includeContainers || !IsLiquid(i)) return false;
                    var place = PlaceOf(s, i);
                    return place == ItemInstance.Carried || (allowGround && place == s.CurrentLocation);
                }
                return i.IsCarried || (allowGround && i.Owner == s.CurrentLocation);
            });

        /// <summary>
        /// 可消耗批次：到期时间升序 → 同批次随身优先（锅内水按锅所在） → 实例 ID 升序。配方与投料不取锅内物品；
        /// 锅里的水已全部被水煮预留时不可取。
        /// </summary>
        public List<ItemInstance> UsableBatches(GameState s, string itemId, bool allowGround, bool includeContainers = false) =>
            Accessible(s, allowGround, includeContainers)
                .Where(i => i.ItemId == itemId && i.ReservedBy == null && !IsSpoiled(s, i))
                .Where(i => !i.InContainer || PotFreeWaterUnits(s, i) > 0)
                .OrderBy(i => i.ExpiresAt ?? int.MaxValue)
                .ThenBy(i => PlaceOf(s, i) == ItemInstance.Carried ? 0 : 1)
                .ThenBy(i => i.InstanceId)
                .ToList();

        public int AvailableCount(GameState s, string itemId, bool allowGround, bool includeContainers = false) =>
            UsableBatches(s, itemId, allowGround, includeContainers).Sum(i => i.Count);

        public bool IsLocked(GameState s, ItemInstance i) =>
            (s.Main != null && s.Main.LockedItemIds.Contains(i.InstanceId)) || (s.Sub != null && s.Sub.LockedItemIds.Contains(i.InstanceId));

        // ───────────────────────── 金属锅（拖拽与容器案 6） ─────────────────────────

        public bool IsPot(ItemInstance i) => _items[i.ItemId].ContainerId != null;

        public ContainerConfig PotConfig(ItemInstance pot) => Config.Containers[_items[pot.ItemId].ContainerId!];

        /// <summary>锅里的全部直接内容（水与食材）。</summary>
        public List<ItemInstance> PotContents(GameState s, int potId)
        {
            var owner = ItemInstance.ContainerOwner(potId);
            return s.Items.Where(i => i.Owner == owner).OrderBy(i => i.InstanceId).ToList();
        }

        public bool IsLiquidItem(string itemId) => _items.TryGetValue(itemId, out var c) && c.UnitVolumeMl.HasValue;
        public bool IsLiquid(ItemInstance i) => IsLiquidItem(i.ItemId);

        /// <summary>锅里的水（液体与食材是两个容量维度）。</summary>
        public List<ItemInstance> PotLiquids(GameState s, int potId) => PotContents(s, potId).Where(IsLiquid).ToList();

        /// <summary>锅里的食材：按放入顺序、再按实例 ID；每份单独一个实例，各占一个食材位。</summary>
        public List<ItemInstance> PotFood(GameState s, int potId) =>
            PotContents(s, potId).Where(i => !IsLiquid(i)).OrderBy(i => i.PlacedOrder ?? int.MaxValue).ThenBy(i => i.InstanceId).ToList();

        /// <summary>锅里水的份数（每份 500 毫升）。</summary>
        public int PotUnits(GameState s, int potId) => PotLiquids(s, potId).Sum(i => i.Count);

        /// <summary>锅内液体种类；无水为 null。同一锅只允许一种水。</summary>
        public string? PotLiquid(GameState s, int potId) => PotLiquids(s, potId).FirstOrDefault()?.ItemId;

        /// <summary>水量标签，如“脏水50%·500/1000毫升”；空锅为“空锅0%·0/1000毫升”。</summary>
        public string PotLabel(GameState s, ItemInstance pot)
        {
            var c = PotConfig(pot);
            var units = PotUnits(s, pot.InstanceId);
            var liquid = PotLiquid(s, pot.InstanceId);
            var ml = units * c.UnitMl;
            var pct = c.CapacityMl > 0 ? ml * 100 / c.CapacityMl : 0;
            return $"{(liquid == null ? "空锅" : ItemName(liquid))}{pct}%·{ml}/{c.CapacityMl}毫升";
        }

        /// <summary>
        /// 随身、成色（当前/最大）最高且耐久足够一次磨损的指定标签工具，平手取实例 ID 小者（v0.6 起各实例最大耐久不同，按比例比较）。
        /// </summary>
        public ItemInstance? BestTool(GameState s, string toolTag) =>
            s.Items.Where(i => OnPlayer(i) && i.ReservedBy == null && _items[i.ItemId].ToolTag == toolTag && HasWear(i, ToolWear(toolTag)))
                .OrderByDescending(Condition)
                .ThenBy(i => i.InstanceId)
                .FirstOrDefault();

        /// <summary>该标签工具一次成功使用的磨损：打火器按点火磨损，锅不按次磨损（按加工），其余按采集磨损。</summary>
        public double ToolWear(string toolTag) => toolTag == "lighter" ? LighterWear : toolTag == "pot" ? 0 : GatherWear;

        public ItemInstance? CarriedTool(GameState s, string toolTag) => BestTool(s, toolTag);

        /// <summary>同地全部可用的某标签工具（随身在前，再按实例 ID），供界面让玩家选定。</summary>
        public List<ItemInstance> AccessibleTools(GameState s, string toolTag) =>
            s.Items.Where(i => (OnPlayer(i) || i.Owner == s.CurrentLocation) && i.ReservedBy == null && !IsLocked(s, i)
                               && _items[i.ItemId].ToolTag == toolTag && HasWear(i, ToolWear(toolTag)))
                .OrderBy(i => OnPlayer(i) ? 0 : 1)
                .ThenBy(i => i.InstanceId)
                .ToList();

        /// <summary>同地可用的催化工具：随身优先，其次当前地面（锅位视为地面）；未预留、未被锁定、耐久足够。</summary>
        public ItemInstance? AccessibleTool(GameState s, string toolTag) =>
            s.Items.Where(i => (OnPlayer(i) || i.Owner == s.CurrentLocation) && i.ReservedBy == null && !IsLocked(s, i)
                               && _items[i.ItemId].ToolTag == toolTag && HasWear(i, ToolWear(toolTag)))
                .OrderBy(i => OnPlayer(i) ? 0 : 1)
                .ThenByDescending(Condition)
                .ThenBy(i => i.InstanceId)
                .FirstOrDefault();

        /// <summary>按消耗顺序预留指定数量，必要时拆堆，返回预留实例 ID。手工指定的批次排在最前（拖拽与容器案 5）。</summary>
        private List<int> Reserve(GameState s, string itemId, int count, int actionId, IList<int>? preferred = null)
        {
            var ids = new List<int>();
            var left = count;
            var batches = UsableBatches(s, itemId, true);
            if (preferred != null && preferred.Count > 0)
                batches = batches.OrderBy(b => preferred.Contains(b.InstanceId) ? preferred.IndexOf(b.InstanceId) : int.MaxValue).ToList();
            foreach (var b in batches)
            {
                if (left <= 0) break;
                var take = Math.Min(left, b.Count);
                if (take == b.Count)
                {
                    b.ReservedBy = actionId;
                    ids.Add(b.InstanceId);
                }
                else
                {
                    b.Count -= take;
                    var n = b.Clone();
                    n.InstanceId = s.NextInstanceId++;
                    n.Count = take;
                    n.ReservedBy = actionId;
                    s.Items.Add(n);
                    ids.Add(n.InstanceId);
                }
                left -= take;
            }
            if (left > 0) throw new InvalidOperationException($"预留 {itemId} 数量不足");
            return ids;
        }

        private void ConsumeOne(GameState s, ItemInstance inst)
        {
            inst.Count -= 1;
            if (inst.Count <= 0) s.Items.Remove(inst);
        }

        private void MoveItems(GameState s, ItemInstance inst, int count, string newOwner)
        {
            // 锅内物品的归属是锅本身，随锅一起走；离开设施与锅的物品清掉放入序号
            var intoHolder = newOwner.StartsWith(ItemInstance.ContainerPrefix) || newOwner.StartsWith(ItemInstance.FacilityPrefix);
            if (count >= inst.Count)
            {
                inst.Owner = newOwner;
                if (!intoHolder) inst.PlacedOrder = null;
            }
            else
            {
                inst.Count -= count;
                var n = inst.Clone();
                n.InstanceId = s.NextInstanceId++;
                n.Count = count;
                n.Owner = newOwner;
                if (!intoHolder) n.PlacedOrder = null;
                s.Items.Add(n);
            }
            Normalize(s, newOwner);
        }

        /// <summary>把一份（数量 1）移入设施或锅：整堆只剩 1 份时直接改归属，否则拆出新实例。返回移入的实例。</summary>
        private ItemInstance MoveOne(GameState s, ItemInstance inst, string newOwner)
        {
            ItemInstance moved;
            if (inst.Count <= 1)
            {
                inst.Owner = newOwner;
                moved = inst;
            }
            else
            {
                inst.Count -= 1;
                moved = inst.Clone();
                moved.InstanceId = s.NextInstanceId++;
                moved.Count = 1;
                moved.Owner = newOwner;
                s.Items.Add(moved);
            }
            moved.PlacedOrder = s.NextProcessSeq++;
            return moved;
        }

        /// <summary>
        /// 生成物品到指定位置。工具与武器逐件生成：最大耐久按区间以 0.1 精度抽一次（v0.6 §05），新制品当前=最大，
        /// scavenged（拾荒箱子产出）再乘 0.6—1.0 成色；易腐品按生成时刻记录 expiresAt。返回新生成的实例。
        /// </summary>
        public List<ItemInstance> CreateItems(GameState s, string itemId, int count, string owner, bool scavenged = false, ItemSource? source = null)
        {
            var made = new List<ItemInstance>();
            if (count <= 0) return made;
            var cfg = _items[itemId];
            // 设施与锅里的食材每份各占一位，逐份生成，不合堆
            var single = !cfg.IsStackable || ItemInstance.TryFacilityId(owner, out _) || (owner.StartsWith(ItemInstance.ContainerPrefix) && !cfg.UnitVolumeMl.HasValue);
            if (single)
            {
                for (var k = 0; k < count; k++)
                {
                    var inst = new ItemInstance
                    {
                        InstanceId = s.NextInstanceId++,
                        ItemId = itemId,
                        Count = 1,
                        Owner = owner,
                        ExpiresAt = cfg.ShelfLifeMinutes.HasValue ? s.GameMinute + cfg.ShelfLifeMinutes.Value : (int?)null,
                        PlacedOrder = cfg.IsStackable ? s.NextProcessSeq++ : (int?)null,
                        Source = source?.Clone(),
                    };
                    if (cfg.HasDurability)
                    {
                        inst.MaxDurability = RollMaxDurability(s, cfg);
                        inst.Durability = scavenged ? inst.MaxDurability * RollCondition(s) : inst.MaxDurability;
                    }
                    var w = Weapon(itemId);
                    if (w != null && w.UsesMagazine) inst.LoadedAmmoId = w.AmmoId;
                    s.Items.Add(inst);
                    made.Add(inst);
                }
                return made;
            }
            var stack = new ItemInstance
            {
                InstanceId = s.NextInstanceId++,
                ItemId = itemId,
                Count = count,
                Owner = owner,
                ExpiresAt = cfg.ShelfLifeMinutes.HasValue ? s.GameMinute + cfg.ShelfLifeMinutes.Value : (int?)null,
                Source = source?.Clone(),
            };
            s.Items.Add(stack);
            made.Add(stack);
            Normalize(s, owner);
            return made;
        }

        /// <summary>
        /// 同物品、同到期时间且同来源（R07 活性物质的发放事件）、未预留的实例合堆，超过堆叠上限分为新堆。
        /// 设施里的物品与锅里的食材每份占一个位，不合堆（否则两根会合成“一堆占一位”）；锅里的水照常合堆。
        /// </summary>
        private void Normalize(GameState s, string owner)
        {
            if (ItemInstance.TryFacilityId(owner, out _)) return;
            var inPot = owner.StartsWith(ItemInstance.ContainerPrefix);
            var groups = s.Items
                .Where(i => i.Owner == owner && i.ReservedBy == null && _items[i.ItemId].IsStackable && (!inPot || IsLiquid(i)))
                .GroupBy(i => (i.ItemId, i.ExpiresAt, i.Source?.GrantEventId))
                .ToList();
            foreach (var g in groups)
            {
                var list = g.OrderBy(i => i.InstanceId).ToList();
                var max = _items[g.Key.ItemId].StackMax;
                var total = list.Sum(i => i.Count);
                var needed = (total + max - 1) / max;
                if (list.Count == needed && list.Take(needed - 1).All(i => i.Count == max)) continue;
                foreach (var inst in list)
                {
                    var take = Math.Min(max, total);
                    inst.Count = take;
                    total -= take;
                }
                foreach (var inst in list.Where(i => i.Count <= 0)) s.Items.Remove(inst);
                while (total > 0)
                {
                    var take = Math.Min(max, total);
                    var tmpl = list[0];
                    s.Items.Add(new ItemInstance
                    {
                        InstanceId = s.NextInstanceId++,
                        ItemId = tmpl.ItemId,
                        Count = take,
                        Owner = owner,
                        ExpiresAt = tmpl.ExpiresAt,
                        Source = tmpl.Source?.Clone(),
                    });
                    total -= take;
                }
            }
        }

        // ───────────────────────── 世界（地图案 04–07） ─────────────────────────

        /// <summary>到达地点：标记已访问，揭示进入即显示的资源点。返回新发现的资源点 ID。</summary>
        private List<string> Visit(GameState s, string locationId)
        {
            var ls = s.Locations[locationId];
            ls.Visited = true;
            ls.Discovered = true;
            var revealed = new List<string>();
            foreach (var r in Config.Resources.Where(r => r.LocationId == locationId))
            {
                var rs = s.Resources[r.NodeId];
                if (rs.Discovered) continue;
                if (r.RevealOnVisit || (r.RevealAt.HasValue && r.RevealAt.Value <= ls.Progress))
                {
                    rs.Discovered = true;
                    revealed.Add(r.NodeId);
                }
            }
            return revealed;
        }

        /// <summary>探索完成：+25 探索度，按 25→50→75→100 顺序处理新跨过的里程碑，每个只触发一次。</summary>
        private (int old, int now, List<string> found, List<string> ids) ApplyExplore(GameState s, string locationId)
        {
            var ls = s.Locations[locationId];
            var old = ls.Progress;
            var now = Math.Min(100, old + Config.Actions.ExploreProgress);
            ls.Progress = now;

            var found = new List<string>();
            var ids = new List<string>();
            foreach (var t in new[] { 25, 50, 75, 100 })
            {
                if (t <= old || t > now) continue;
                if (!ls.TriggeredMilestones.Add(t)) continue;
                foreach (var r in Config.Resources.Where(r => r.LocationId == locationId && r.RevealAt.HasValue && r.RevealAt.Value <= t))
                {
                    var rs = s.Resources[r.NodeId];
                    if (rs.Discovered) continue;
                    rs.Discovered = true;
                    found.Add(r.Name);
                    ids.Add(r.NodeId);
                }
            }
            foreach (var e in UnlockEdges(s))
            {
                found.Add($"通往{LocName(e.Touches(locationId) ? e.Other(locationId) : e.B)}的路线");
                ids.Add(e.Id);
            }

            return (old, now, found, ids);
        }

        /// <summary>检查所有未解锁连接，任一条件满足即解锁并发现两端地点。</summary>
        private List<Config.EdgeConfig> UnlockEdges(GameState s)
        {
            var list = new List<Config.EdgeConfig>();
            foreach (var e in Config.Edges)
            {
                if (s.UnlockedEdges.Contains(e.Id)) continue;
                if (!e.UnlockAny.Any(u => s.Locations[u.Location].Progress >= u.Progress)) continue;
                s.UnlockedEdges.Add(e.Id);
                s.Locations[e.A].Discovered = true;
                s.Locations[e.B].Discovered = true;
                list.Add(e);
            }
            return list;
        }

        /// <summary>某地点下一次探索将跨过的里程碑是否有新内容（不泄露具体内容）。</summary>
        public bool NextMilestoneHasContent(GameState s, string locationId)
        {
            var ls = s.Locations[locationId];
            var next = Math.Min(100, ls.Progress + Config.Actions.ExploreProgress);
            var t = next;
            if (ls.TriggeredMilestones.Contains(t)) return false;
            if (Config.Resources.Any(r => r.LocationId == locationId && r.RevealAt == t && !s.Resources[r.NodeId].Discovered)) return true;
            return Config.Edges.Any(e => !s.UnlockedEdges.Contains(e.Id) && e.UnlockAny.Any(u => u.Location == locationId && u.Progress == t));
        }
    }
}

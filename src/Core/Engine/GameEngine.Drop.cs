using System;
using System.Collections.Generic;
using System.Linq;
using Survival.Core.Config;
using Survival.Core.State;

namespace Survival.Core.Engine
{
    public enum DropSourceKind { Card, PotChild }

    /// <summary>拖拽来源：一张卡（堆叠物品组 owner+itemId，或工具/锅/设施内物品实例）或锅里的一份水或食材。</summary>
    public sealed class DropSource
    {
        public DropSourceKind Kind { get; set; }
        /// <summary>卡片所在区：carried、地点 ID 或 facility:&lt;设施ID&gt;；锅内物品为 pot:&lt;锅ID&gt;。</summary>
        public string Owner { get; set; } = "";
        public string ItemId { get; set; } = "";
        public int? InstanceId { get; set; }

        /// <summary>解析网页来源键：card:owner|item[|inst]（owner 可以是 facility:camp:fire）、potchild:inst。</summary>
        public static DropSource? Parse(string key)
        {
            if (key.StartsWith("potchild:") && int.TryParse(key.Substring(9), out var child))
                return new DropSource { Kind = DropSourceKind.PotChild, InstanceId = child };
            if (!key.StartsWith("card:")) return null;
            var parts = key.Substring(5).Split('|');
            if (parts.Length < 2) return null;
            int? inst = parts.Length > 2 && int.TryParse(parts[2], out var n) ? n : (int?)null;
            return new DropSource { Kind = DropSourceKind.Card, Owner = parts[0], ItemId = parts[1], InstanceId = inst };
        }
    }

    public enum DropTargetKind { Zone, Card, FireFuel, FirePotSlot, FireSlot, FireCard, PotFill, PotFood, Resource, Location, RecipeSlot, Self }

    /// <summary>拖拽落点。Id：区 ID / 卡片键 / 地点 ID / 锅实例 ID / 资源点 ID / 配方 ID；ItemId 只给配方材料槽。</summary>
    public sealed class DropTarget
    {
        public DropTargetKind Kind { get; set; }
        public string Id { get; set; } = "";
        public string ItemId { get; set; } = "";
        public int Index { get; set; }

        /// <summary>
        /// 解析网页落点键：zone:、card:、fuel:、potslot:（火堆加工位放锅）、fireslot:（火堆空加工位）、fire: 与 facility:（火堆卡面）、
        /// potfill:（锅水量区）、potfood:（锅食材区）、res:、loc:、slot:recipe:item、self。
        /// </summary>
        public static DropTarget? Parse(string key, int index = 0)
        {
            var i = key.IndexOf(':');
            var head = i < 0 ? key : key.Substring(0, i);
            var rest = i < 0 ? "" : key.Substring(i + 1);
            switch (head)
            {
                case "zone": return new DropTarget { Kind = DropTargetKind.Zone, Id = rest, Index = index };
                case "card": return new DropTarget { Kind = DropTargetKind.Card, Id = rest, Index = index };
                case "fuel": return new DropTarget { Kind = DropTargetKind.FireFuel, Id = rest };
                case "potslot": return new DropTarget { Kind = DropTargetKind.FirePotSlot, Id = rest };
                case "fireslot": return new DropTarget { Kind = DropTargetKind.FireSlot, Id = rest };
                case "fire":
                case "facility": return new DropTarget { Kind = DropTargetKind.FireCard, Id = rest };
                case "potfill": return new DropTarget { Kind = DropTargetKind.PotFill, Id = rest };
                case "potfood": return new DropTarget { Kind = DropTargetKind.PotFood, Id = rest };
                case "res": return new DropTarget { Kind = DropTargetKind.Resource, Id = rest };
                case "loc": return new DropTarget { Kind = DropTargetKind.Location, Id = rest };
                case "self": return new DropTarget { Kind = DropTargetKind.Self };
                case "slot":
                {
                    var p = rest.Split(':');
                    return p.Length == 2 ? new DropTarget { Kind = DropTargetKind.RecipeSlot, Id = p[0], ItemId = p[1] } : null;
                }
            }
            return null;
        }
    }

    /// <summary>拖拽动词，与拖拽与容器案第 4 节白名单一一对应（合堆在按物品分组的界面里即转移）。</summary>
    public enum DropVerb { None, Reorder, Transfer, AddFuel, PreviewIgnite, FillPot, Decant, PutInFire, PutFood, TakeOut, PreviewGatherTool, PreviewRecipe, PreviewUse }

    /// <summary>落点解析结果：即时类带 Instant（壳直接提交），预览类带 Action 或候选配方（壳打开面板预选）。</summary>
    public sealed class DropIntent
    {
        public bool Ok { get; set; }
        public DropVerb Verb { get; set; }
        /// <summary>落点上显示的动词与结果，如“投放1木材，燃料+60分钟”。</summary>
        public string Label { get; set; } = "";
        public string Reason { get; set; } = "";
        public InstantRequest? Instant { get; set; }
        public ActionRequest? Action { get; set; }
        /// <summary>预览配方候选（配置顺序）。</summary>
        public List<string> Recipes { get; set; } = new List<string>();
        /// <summary>按住 Shift 松手可选数量；MaxQuantity 已综合可用量、容量、燃料空位与 30 公斤。</summary>
        public bool Quantifiable { get; set; }
        public int MaxQuantity { get; set; }
        /// <summary>卡面上没有语义组合：界面应改落到所在区域的插入线（热区 → 卡面 → 插入线的优先级）。</summary>
        public bool FallThrough { get; set; }

        public bool IsPreview => Verb == DropVerb.PreviewIgnite || Verb == DropVerb.PreviewGatherTool
                                 || Verb == DropVerb.PreviewRecipe || Verb == DropVerb.PreviewUse;

        public static DropIntent No(string reason, bool fallThrough = false) => new DropIntent { Ok = false, Reason = reason, FallThrough = fallThrough };
    }

    /// <summary>“什么能拖到什么”的白名单放在内核，网页壳与将来的 UGUI 共用（开发计划 v0.2 第 4.6 节）。</summary>
    public sealed partial class GameEngine
    {
        public const string Unsupported = "暂不支持此组合";

        /// <summary>某物品是否是任一配方的材料。</summary>
        public bool IsRecipeMaterial(string itemId) => Config.Recipes.Any(r => r.Inputs.ContainsKey(itemId));

        /// <summary>同时用到这些物品的配方（配置顺序）。</summary>
        public List<string> RecipeCandidates(params string[] itemIds) =>
            Config.Recipes.Where(r => itemIds.All(r.Inputs.ContainsKey)).Select(r => r.Id).ToList();

        /// <summary>卡片代表的实例：工具/锅就是该实例；堆叠组取最早到期、未预留的一堆（与点击转移同一顺序）。</summary>
        public ItemInstance? CardHead(GameState s, DropSource src)
        {
            if (src.Kind == DropSourceKind.PotChild || src.InstanceId.HasValue)
                return src.InstanceId.HasValue ? FindItem(s, src.InstanceId.Value) : null;
            return s.Items.Where(i => i.Owner == src.Owner && i.ItemId == src.ItemId && i.ReservedBy == null)
                .OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).FirstOrDefault();
        }

        /// <summary>该卡当前批次（同物品、同到期、同区、未预留）的总数。</summary>
        public int CardBatchCount(GameState s, ItemInstance head) =>
            s.Items.Where(i => i.Owner == head.Owner && i.ItemId == head.ItemId && i.ExpiresAt == head.ExpiresAt && i.ReservedBy == null)
                .Sum(i => i.Count);

        public DropIntent ResolveDrop(GameState s, DropSource src, DropTarget dst)
        {
            if (s.Progress.Dead) return DropIntent.No("角色已死亡");
            var head = CardHead(s, src);
            if (head == null)
                return DropIntent.No(src.Kind == DropSourceKind.Card && s.Items.Any(i => i.Owner == src.Owner && i.ItemId == src.ItemId)
                    ? "该物品已被预留" : "物品已不在");
            var here = s.CurrentLocation;
            var srcPlace = PlaceOf(s, head);
            if (srcPlace != ItemInstance.Carried && srcPlace != here) return DropIntent.No("其他地点的物品不能操作");

            DropIntent intent;
            switch (dst.Kind)
            {
                case DropTargetKind.Zone: intent = DropOnZone(s, src, head, dst.Id, dst.Index); break;
                case DropTargetKind.Card: intent = DropOnCard(s, src, head, dst); break;
                case DropTargetKind.FireFuel: intent = DropOnFuel(s, head, dst.Id); break;
                case DropTargetKind.FireCard: intent = DropOnFire(s, src, head, dst.Id); break;
                case DropTargetKind.FirePotSlot:
                case DropTargetKind.FireSlot: intent = DropOnFire(s, src, head, dst.Id); break;
                case DropTargetKind.PotFill: intent = DropOnPot(s, src, head, int.TryParse(dst.Id, out var pid) ? FindItem(s, pid) : null); break;
                case DropTargetKind.PotFood: intent = DropOnPotFood(s, src, head, int.TryParse(dst.Id, out var fid) ? FindItem(s, fid) : null); break;
                case DropTargetKind.Resource: intent = DropOnResource(s, src, head, dst.Id); break;
                case DropTargetKind.Location: intent = DropOnLocation(s, src, head, dst.Id); break;
                case DropTargetKind.RecipeSlot: intent = DropOnSlot(s, src, head, dst.Id, dst.ItemId); break;
                case DropTargetKind.Self: intent = DropOnSelf(s, head); break;
                default: intent = DropIntent.No(Unsupported); break;
            }
            return intent.Ok ? Busy(s, intent) ?? intent : intent;
        }

        /// <summary>忙碌表（拖拽与容器案 8）：执行中与子行动中只允许排序；暂停中另允许投料、点火与补给预览。</summary>
        private DropIntent? Busy(GameState s, DropIntent intent)
        {
            if (intent.Verb == DropVerb.Reorder) return null;
            if (s.Sub != null || (s.Main != null && s.Main.Status == ActionStatus.Running))
                return DropIntent.No("行动进行中，只能整理卡片顺序");
            if (s.Main != null && s.Main.Status == ActionStatus.Paused
                && intent.Verb != DropVerb.AddFuel && intent.Verb != DropVerb.PreviewIgnite && intent.Verb != DropVerb.PreviewUse)
                return DropIntent.No("行动暂停中，只能补给、休息或维护火堆");
            return null;
        }

        private static DropIntent Instant(DropVerb verb, string label, InstantRequest r, int max = 0) => new DropIntent
        {
            Ok = true, Verb = verb, Label = label, Instant = r, Quantifiable = max > 1, MaxQuantity = Math.Max(max, 1),
        };

        /// <summary>耗时交互只打开预览：能否开始以内核校验为准，不能时直接给出原因。</summary>
        private DropIntent PreviewFor(GameState s, DropVerb verb, string label, ActionRequest r)
        {
            var why = CanStart(s, r);
            return why != null ? DropIntent.No(why) : new DropIntent { Ok = true, Verb = verb, Label = label, Action = r };
        }

        private DropIntent DropOnZone(GameState s, DropSource src, ItemInstance head, string zone, int index)
        {
            var here = s.CurrentLocation;
            if (zone != ItemInstance.Carried && zone != here) return DropIntent.No("只能放到随身或当前地面");
            var name = ItemName(head.ItemId);
            // 设施与锅里的物品拖到随身/地面：取出（整锅连同内容），与点击转移箭头同一校验（统一卡片案第 5 节）
            if (head.InFacility || (head.InContainer && !IsLiquid(head)))
            {
                var why = TakeOutBlocked(s, head, zone);
                if (why != null) return DropIntent.No(why);
                var to = zone == ItemInstance.Carried ? "随身" : "地面";
                var what = IsPot(head) ? $"{name}#{head.InstanceId}（含内容{ItemGrams(s, head) / 1000.0:0.0}公斤）" : $"1份{name}#{head.InstanceId}";
                var verb = zone == ItemInstance.Carried ? "拾取" : IsPot(head) ? "取下" : "取出";
                return Instant(DropVerb.TakeOut, $"{verb}{what}到{to}", InstantRequest.TakeOut(head.InstanceId, zone));
            }
            if (src.Kind == DropSourceKind.PotChild)
            {
                var max = MaxDecant(s, head, zone, out var why);
                if (why != null) return DropIntent.No(why);
                return Instant(DropVerb.Decant, $"倒出1份{name}到{(zone == ItemInstance.Carried ? "随身" : "地面")}",
                    InstantRequest.Decant(head.InstanceId, zone), max);
            }
            if (head.Owner == zone)
                return new DropIntent { Ok = true, Verb = DropVerb.Reorder, Label = "调整顺序", Instant = InstantRequest.Reorder(zone, CardKey(head), index) };
            if (IsLocked(s, head)) return DropIntent.No("行动占用：该物品正被当前行动使用");
            var batch = CardBatchCount(s, head);
            if (zone == ItemInstance.Carried)
            {
                var max = MaxPickupBatch(s, head, batch);
                if (max < 1) return DropIntent.No($"携带后将超过{Config.Carry.HardGrams / 1000}公斤");
                var extra = IsPot(head) ? $"（{PotLabel(s, head)}）" : "";
                return Instant(DropVerb.Transfer, $"拾取1{name}{extra}到随身", InstantRequest.Pickup(head.InstanceId, 1), max);
            }
            return Instant(DropVerb.Transfer, $"放下1{name}到地面", InstantRequest.Drop(head.InstanceId, 1), batch);
        }

        /// <summary>整批（可能分成几堆）在 30 公斤内最多能拾取多少份。</summary>
        private int MaxPickupBatch(GameState s, ItemInstance head, int batch)
        {
            var unit = head.Count > 0 ? ItemGrams(s, head) / head.Count : _items[head.ItemId].WeightGrams;
            var room = Math.Max(0, Config.Carry.HardGrams - CarriedGrams(s));
            return Math.Min(batch, unit > 0 ? room / unit : batch);
        }

        private DropIntent DropOnCard(GameState s, DropSource src, ItemInstance head, DropTarget dst)
        {
            var parts = dst.Id.Split('|');
            if (parts.Length < 2) return DropIntent.No(Unsupported);
            var zone = parts[0];
            var targetItem = parts[1];
            var targetInst = parts.Length > 2 && int.TryParse(parts[2], out var n) ? FindItem(s, n) : null;

            // 落到锅卡：等同锅的装水区；生块根拖到空锅看烹饪
            if (targetInst != null && IsPot(targetInst)) return DropOnPot(s, src, head, targetInst);

            if (src.Kind == DropSourceKind.Card && targetItem != head.ItemId
                && IsRecipeMaterial(head.ItemId) && IsRecipeMaterial(targetItem))
            {
                var rec = RecipeCandidates(head.ItemId, targetItem);
                if (rec.Count > 0)
                    return new DropIntent { Ok = true, Verb = DropVerb.PreviewRecipe, Label = "查看可制作的配方", Recipes = rec };
            }
            // 同类卡：跨区即合堆（先转移再合堆）
            if (src.Kind == DropSourceKind.Card && targetItem == head.ItemId && zone != head.Owner)
            {
                var z = DropOnZone(s, src, head, zone, 0);
                if (z.Ok) z.Label = z.Label.Replace("到随身", "，与随身同类合堆").Replace("到地面", "，与地面同类合堆");
                return z;
            }
            return DropIntent.No(Unsupported, fallThrough: true);
        }

        private DropIntent DropOnFuel(GameState s, ItemInstance head, string loc)
        {
            if (loc != s.CurrentLocation || !_locs[loc].HasFire) return DropIntent.No("只能给当前营地的火堆投料");
            // 燃料区与加工位都是火堆的一部分：打火器落在上面同样是点火
            if (IsLighter(head)) return DropOnFire(s, new DropSource { Kind = DropSourceKind.Card }, head, loc);
            if (IsLiquid(head)) return DropIntent.No($"{ItemName(head.ItemId)}不能直接倒进火堆（不会产生净水，也不会灭火），请拖进锅里");
            // 燃料区只收木材；可加工的食材写明该拖到哪里（统一卡片案第 5 节：落点失败显示原因）
            if (head.ItemId != "wood" && (CanRoast(head.ItemId) || Config.Containers.Values.Any(c => c.StoredFoodIds.Contains(head.ItemId))))
                return DropIntent.No($"{ItemName(head.ItemId)}请拖到火堆的空加工位或锅的食材区；燃料区只收木材");
            if (head.ItemId != "wood" || head.InContainer || head.InFacility) return DropIntent.No(Unsupported);
            if (head.ReservedBy != null) return DropIntent.No("该物品已被预留");
            var fac = s.Locations[loc].Facility;
            var room = MaxWoodForFire(fac);
            if (room < 1) return DropIntent.No($"至少需要{Config.Fire.BurnMinutesPerWood}分钟空余容量（燃料上限{Config.Fire.MaxFuelMinutes}分钟）");
            var max = Math.Min(room, CardBatchCount(s, head));
            return Instant(DropVerb.AddFuel, $"投放1木材，燃料+{Config.Fire.BurnMinutesPerWood}分钟",
                InstantRequest.AddFuel(1, head.InstanceId), max);
        }

        /// <summary>
        /// 火堆卡面与加工位：打火器 → 点火预览；木材 → 投料；锅 → 放进空加工位；可直烤食材 → 放进空加工位烤制；
        /// 水 → 提示拖进锅（不产生净水、不灭火）；熟食 → 无处理配方。失败给原因，不回落成别的目标（统一卡片案第 5 节）。
        /// </summary>
        private DropIntent DropOnFire(GameState s, DropSource src, ItemInstance head, string loc)
        {
            if (loc != s.CurrentLocation || !_locs[loc].HasFire) return DropIntent.No("异地不能操作火堆");
            if (src.Kind == DropSourceKind.Card && IsLighter(head) && !head.InFacility)
                return PreviewFor(s, DropVerb.PreviewIgnite, $"查看用{ItemName(head.ItemId)}#{head.InstanceId}点火", ActionRequest.Ignite(head.InstanceId));
            if (head.ItemId == "wood" && src.Kind == DropSourceKind.Card && !head.InFacility) return DropOnFuel(s, head, loc);
            var max = MaxPutInFire(s, head, out var why);
            if (why != null) return DropIntent.No(why);
            var name = ItemName(head.ItemId);
            if (IsPot(head))
            {
                var dirty = PotLiquid(s, head.InstanceId) == "dirty_water";
                var food = PotFood(s, head.InstanceId).Count;
                var hint = dirty ? "，燃烧时自动烧水" : food > 0 ? $"，锅内食材{food}份{(PotLiquid(s, head.InstanceId) == null ? "（缺水，不加工）" : "")}" : "";
                return Instant(DropVerb.PutInFire, $"把{name}#{head.InstanceId}放进火堆加工位{hint}", InstantRequest.PutInFire(head.InstanceId));
            }
            var p = FireFoodProcess(head.ItemId)!;
            return Instant(DropVerb.PutInFire, $"放1份{name}进火堆加工位直接{p.Name}（{p.HeatMinutes}分钟）",
                InstantRequest.PutInFire(head.InstanceId), Math.Min(max, CardBatchCount(s, head)));
        }

        private bool IsLighter(ItemInstance i) => _items[i.ItemId].ToolTag == "lighter";

        /// <summary>锅的食材区：只收食材；水请拖到水量区。</summary>
        private DropIntent DropOnPotFood(GameState s, DropSource src, ItemInstance head, ItemInstance? pot)
        {
            if (pot == null || !IsPot(pot)) return DropIntent.No(Unsupported);
            if (head.InstanceId == pot.InstanceId) return DropIntent.No(Unsupported, fallThrough: true);
            if (IsLiquid(head)) return src.Kind == DropSourceKind.PotChild
                ? DropIntent.No("暂不支持锅与锅之间倒水")
                : DropIntent.No($"{ItemName(head.ItemId)}请拖到锅的水量条上装入");
            if (head.Owner == ItemInstance.ContainerOwner(pot.InstanceId)) return DropIntent.No(Unsupported, fallThrough: true);
            return DropFoodOnPot(s, head, pot);
        }

        private DropIntent DropOnPot(GameState s, DropSource src, ItemInstance head, ItemInstance? pot)
        {
            if (pot == null || !IsPot(pot)) return DropIntent.No(Unsupported);
            if (head.InstanceId == pot.InstanceId || head.Owner == ItemInstance.ContainerOwner(pot.InstanceId))
                return DropIntent.No(Unsupported, fallThrough: true);
            if (src.Kind == DropSourceKind.PotChild) return DropIntent.No(IsLiquid(head) ? "暂不支持锅与锅之间倒水" : "先把它从原来的锅里取出来");
            if (!IsLiquid(head)) return DropFoodOnPot(s, head, pot);
            if (!PotConfig(pot).Accepts.Contains(head.ItemId)) return DropIntent.No(Unsupported);
            var max = MaxFill(s, head, pot, out var reason);
            if (reason != null) return DropIntent.No(reason);
            var cfg = PotConfig(pot);
            var before = PotUnits(s, pot.InstanceId) * cfg.UnitMl * 100 / cfg.CapacityMl;
            var after = (PotUnits(s, pot.InstanceId) + 1) * cfg.UnitMl * 100 / cfg.CapacityMl;
            // 同种脏水追加会让整锅净化重新计时，预览写明（设施与烹饪案 5.2）
            var restart = BatchProcess(head.ItemId) != null && ProcessOf(s, pot) is FacilityProcess bp && bp.HeatDoneMinutes > 0
                ? $"；整锅净化进度{bp.HeatDoneMinutes}分钟清零重新计时" : "";
            return Instant(DropVerb.FillPot, $"装入1份{ItemName(head.ItemId)}，水量{before}%→{after}%{restart}",
                InstantRequest.FillPot(head.InstanceId, pot.InstanceId), max);
        }

        /// <summary>食材落到锅（锅卡或锅食材区）：即时放进该锅的真实内容，不冒泡成火堆烤制（统一卡片案第 5 节）。</summary>
        private DropIntent DropFoodOnPot(GameState s, ItemInstance head, ItemInstance pot)
        {
            var max = MaxPutFood(s, head, pot, out var why);
            if (why != null) return DropIntent.No(why);
            return Instant(DropVerb.PutFood, $"放进{ItemName(pot.ItemId)}#{pot.InstanceId}食材区（{PotFoodHint(s, pot)}）",
                InstantRequest.PutFoodInPot(head.InstanceId, pot.InstanceId), max);
        }

        private DropIntent DropOnResource(GameState s, DropSource src, ItemInstance head, string nodeId)
        {
            if (!_res.TryGetValue(nodeId, out var res) || res.LocationId != s.CurrentLocation || !s.Resources[nodeId].Discovered)
                return DropIntent.No("只能对当前地点已发现的资源点使用");
            var tag = _items[head.ItemId].ToolTag;
            if (src.Kind != DropSourceKind.Card || tag == null || res.ToolBonus == null || res.ToolBonus.ToolTag != tag)
                return DropIntent.No(Unsupported);
            if (!head.IsCarried) return DropIntent.No("先拾取工具");
            return PreviewFor(s, DropVerb.PreviewGatherTool, $"查看采集（使用这把{ItemName(head.ItemId)}）",
                ActionRequest.Gather(nodeId, head.InstanceId));
        }

        private DropIntent DropOnLocation(GameState s, DropSource src, ItemInstance head, string loc)
        {
            if (loc != s.CurrentLocation) return DropIntent.No("只能对当前地点操作");
            if (src.Kind != DropSourceKind.Card || !_locs[loc].CanBuildShelter) return DropIntent.No(Unsupported);
            // 原料拖至营地地点卡：列出营地设施配方（遮蔽棚、床垫）
            var rec = Config.Recipes.Where(r => r.FacilityResult != null && r.Inputs.ContainsKey(head.ItemId)).Select(r => r.Id).ToList();
            if (rec.Count == 0) return DropIntent.No(Unsupported);
            return new DropIntent { Ok = true, Verb = DropVerb.PreviewRecipe, Label = "查看营地设施", Recipes = rec };
        }

        private DropIntent DropOnSlot(GameState s, DropSource src, ItemInstance head, string recipeId, string itemId)
        {
            if (!_recipes.TryGetValue(recipeId, out var rc) || !rc.Inputs.ContainsKey(itemId)) return DropIntent.No(Unsupported);
            if (src.Kind != DropSourceKind.Card || head.ItemId != itemId) return DropIntent.No($"这个材料槽需要{ItemName(itemId)}");
            // 设施与锅里的物资是独立来源，普通制作不自动从加工中的内容物抽料，要先手动取出（统一卡片案第 6 节）
            if (head.InFacility) return DropIntent.No("火堆里的物品要先取出才能用于制作");
            if (head.InContainer || head.ReservedBy != null || IsSpoiled(s, head)) return DropIntent.No("这批材料不可用");
            return new DropIntent
            {
                Ok = true, Verb = DropVerb.PreviewRecipe, Label = $"指定这批{ItemName(itemId)}", Recipes = new List<string> { recipeId },
                Action = ActionRequest.Craft(recipeId, null, new[] { head.InstanceId }),
            };
        }

        private DropIntent DropOnSelf(GameState s, ItemInstance head)
        {
            var cfg = _items[head.ItemId];
            if (cfg.Use == null)
                return DropIntent.No(cfg.RequiresCooking ? $"{cfg.Name}需先烹饪，不可直接食用" : $"{cfg.Name}不能直接使用");
            var inst = head;
            if (!head.InContainer && cfg.IsStackable && (head.ReservedBy != null || IsSpoiled(s, head)))
                inst = s.Items.Where(i => i.Owner == head.Owner && i.ItemId == head.ItemId && i.ReservedBy == null && !IsSpoiled(s, i))
                    .OrderBy(i => i.ExpiresAt ?? int.MaxValue).ThenBy(i => i.InstanceId).FirstOrDefault() ?? head;
            var label = $"查看{UseVerb(cfg)}1份{cfg.Name}" + (cfg.Use.CausesDiarrhea ? "（必定腹泻12小时）" : "");
            return PreviewFor(s, DropVerb.PreviewUse, label, ActionRequest.UseInstance(inst.InstanceId));
        }
    }
}
